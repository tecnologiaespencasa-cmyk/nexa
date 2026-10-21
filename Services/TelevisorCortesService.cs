using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Data.Repositories.Interfaces;
using Nexa.Helpers;
using Nexa.Models.ViewModels;
using Nexa.Services.Interfaces;

namespace Nexa.Services;

/// <summary>
/// Fotos del Televisor y armado de la pantalla.
///
/// Un turno cerrado no se recalcula: lo que se ve de la mañana después de la 1:45 p. m. y de
/// cualquier día anterior sale de televisor_cortes, porque los datos del censo siguen cambiando y
/// el indicador tiene que quedar como terminó. Las fotos las toma TelevisorCortesHostedService:
/// - NOVEDADES_8AM: pendientes del portal a las 8:00 a. m. (exacta si se toma antes de las 8:20;
///   después, reconstruida con las fechas del portal y marcada así).
/// - MANANA: el turno de la mañana a la 1:45 p. m. de cada día hábil.
/// - DIA: el día completo a la medianoche, con su mañana, su tarde y el total.
/// Si la aplicación estuvo caída en un corte, la foto se toma al volver y queda "recuperada": los
/// estados de gestión se llevan al momento del corte con las horas guardadas de cada marca.
/// </summary>
public sealed class TelevisorCortesService : ITelevisorCortesService
{
    private const int DiasHaciaAtras = 7;
    private static readonly TimeSpan Ocho = new(8, 0, 0);
    private static readonly TimeSpan Tolerancia8am = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan ToleranciaCorte = TimeSpan.FromMinutes(15);

    private readonly ApplicationDbContext _context;
    private readonly ITelevisorIndicadoresService _indicadores;
    private readonly IPortalNovedadRepository _portal;
    private readonly ILogger<TelevisorCortesService> _logger;

    public TelevisorCortesService(
        ApplicationDbContext context,
        ITelevisorIndicadoresService indicadores,
        IPortalNovedadRepository portal,
        ILogger<TelevisorCortesService> logger)
    {
        _context = context;
        _indicadores = indicadores;
        _portal = portal;
        _logger = logger;
    }

    public async Task<TelevisorPantallaViewModel> PantallaAsync(DateTime? dia, string? bloque, CancellationToken ct)
    {
        var ahora = ColombiaTime.Convert(DateTime.UtcNow);
        var hoy = ahora.Date;
        var d = dia?.Date is DateTime pedido && pedido <= hoy ? pedido : hoy;
        var habil = TelevisorHorario.EsHabil(d);
        var corte = d + TelevisorHorario.Corte;
        var esHoy = d == hoy;

        var automatico = string.IsNullOrWhiteSpace(bloque) || bloque == "auto";
        string clave;
        if (!habil)
        {
            clave = TelevisorBloques.Dia;
        }
        else if (automatico)
        {
            clave = !esHoy ? TelevisorBloques.Dia
                : ahora < corte ? TelevisorBloques.Manana
                : TelevisorBloques.Tarde;
        }
        else
        {
            clave = bloque is TelevisorBloques.Manana or TelevisorBloques.Tarde ? bloque : TelevisorBloques.Dia;
            if (clave == TelevisorBloques.Tarde && esHoy && ahora < corte)
            {
                clave = TelevisorBloques.Manana;
            }
        }

        TelevisorTablero tablero;
        if (esHoy)
        {
            tablero = await _indicadores.ConstruirAsync(d, ahora, ct);
            tablero.Origen = "vivo";
        }
        else
        {
            var fotoDia = await LeerAsync(d, TelevisorCorteTipos.Dia, ct);
            if (fotoDia is not null)
            {
                tablero = fotoDia;
            }
            else
            {
                tablero = await _indicadores.ConstruirAsync(d, d.AddDays(1), ct);
                tablero.Origen = "reconstruido";
            }
        }

        // Pasada la 1:45 p. m., la mañana se muestra como quedó en el corte.
        var mananaCongelada = false;
        if (habil && (!esHoy || ahora >= corte))
        {
            var fotoManana = await LeerAsync(d, TelevisorCorteTipos.Manana, ct)
                ?? await _indicadores.ConstruirAsync(d, corte, ct);
            var bloqueManana = fotoManana.Bloque(TelevisorBloques.Manana);
            if (bloqueManana is not null)
            {
                var indice = tablero.Bloques.FindIndex(b => b.Clave == TelevisorBloques.Manana);
                if (indice >= 0)
                {
                    tablero.Bloques[indice] = bloqueManana;
                }

                mananaCongelada = true;
            }
        }

        return new TelevisorPantallaViewModel
        {
            Tablero = tablero,
            BloqueClave = clave,
            BloqueAutomatico = automatico,
            EsHoy = esHoy,
            AhoraColombia = ahora,
            ServidorUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            MananaCongelada = mananaCongelada
        };
    }

    public async Task TomarCortesPendientesAsync(DateTime ahoraUtc, CancellationToken ct)
    {
        var ahora = ColombiaTime.Convert(ahoraUtc);
        var hoy = ahora.Date;
        var existentes = await _context.TelevisorCortes.AsNoTracking()
            .Where(x => x.Dia >= hoy.AddDays(-DiasHaciaAtras) && x.Dia <= hoy)
            .Select(x => new { x.Dia, x.Tipo })
            .ToListAsync(ct);
        bool Existe(DateTime dia, string tipo) => existentes.Any(x => x.Dia == dia && x.Tipo == tipo);

        // 1. Saldo de novedades de las 8:00 a. m.
        if (ahora.TimeOfDay >= Ocho && !Existe(hoy, TelevisorCorteTipos.Novedades8am))
        {
            var ocho = hoy + Ocho;
            var resumen = await _portal.GetResumenDiaAsync(
                ColombiaTime.ConvertToUtc(hoy),
                ahoraUtc,
                ColombiaTime.ConvertToUtc(ocho),
                ct);
            var exacta = ahora - ocho <= Tolerancia8am;
            var saldo = new TelevisorSaldoNovedades
            {
                Saldo = resumen.Areas.Sum(a => exacta ? a.Pendientes : a.Saldo8am),
                PorArea = resumen.Areas
                    .Select(a => new TelevisorNovedadArea { Area = a.Area, Pendientes = exacta ? a.Pendientes : a.Saldo8am })
                    .ToList()
            };
            await GuardarAsync(hoy, TelevisorCorteTipos.Novedades8am, ocho, !exacta, JsonSerializer.Serialize(saldo), ct);
        }

        // 2. Corte de la mañana de hoy.
        var corteHoy = hoy + TelevisorHorario.Corte;
        if (TelevisorHorario.EsHabil(hoy) && ahora >= corteHoy && !Existe(hoy, TelevisorCorteTipos.Manana))
        {
            await FotografiarAsync(hoy, TelevisorCorteTipos.Manana, corteHoy, ahora - corteHoy > ToleranciaCorte, ct);
        }

        // 3. Días anteriores sin cierre (o sin mañana). Cubre la noche (el cierre de ayer se toma
        //    pasada la medianoche) y los días en que la aplicación estuvo caída.
        for (var i = 1; i <= DiasHaciaAtras; i++)
        {
            var d = hoy.AddDays(-i);
            if (!Existe(d, TelevisorCorteTipos.Dia))
            {
                var fin = d.AddDays(1);
                await FotografiarAsync(d, TelevisorCorteTipos.Dia, fin, ahora - fin > TimeSpan.FromHours(1), ct);
            }

            if (TelevisorHorario.EsHabil(d) && !Existe(d, TelevisorCorteTipos.Manana))
            {
                await FotografiarAsync(d, TelevisorCorteTipos.Manana, d + TelevisorHorario.Corte, true, ct);
            }
        }
    }

    public async Task<bool> AjustarTurnoAsync(DateTime dia, int personaId, string turno, string? usuario, CancellationToken ct)
    {
        dia = dia.Date;
        if (!await _context.NursingAssistants.AnyAsync(x => x.Id == personaId, ct))
        {
            return false;
        }

        var actual = await _context.TelevisorTurnoAjustes.FirstOrDefaultAsync(x => x.Dia == dia && x.NursingAssistantId == personaId, ct);
        if (turno == "auto")
        {
            if (actual is not null)
            {
                _context.TelevisorTurnoAjustes.Remove(actual);
            }
        }
        else
        {
            if (!TelevisorTurnos.Ajustables.Contains(turno))
            {
                return false;
            }

            actual ??= _context.TelevisorTurnoAjustes.Add(new TelevisorTurnoAjuste { Dia = dia, NursingAssistantId = personaId }).Entity;
            actual.Turno = turno;
            actual.AjustadoPor = usuario;
            actual.AjustadoAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }

    private async Task FotografiarAsync(DateTime dia, string tipo, DateTime estadoAl, bool recuperada, CancellationToken ct)
    {
        var tablero = await _indicadores.ConstruirAsync(dia, estadoAl, ct);
        tablero.Origen = recuperada ? "recuperada" : "foto";
        await GuardarAsync(dia, tipo, estadoAl, recuperada, JsonSerializer.Serialize(tablero), ct);
    }

    private async Task GuardarAsync(DateTime dia, string tipo, DateTime estadoAl, bool recuperada, string json, CancellationToken ct)
    {
        var corte = new TelevisorCorte
        {
            Dia = dia.Date,
            Tipo = tipo,
            EstadoAlUtc = ColombiaTime.ConvertToUtc(estadoAl),
            GeneradoAtUtc = DateTime.UtcNow,
            Recuperado = recuperada,
            DatosJson = json,
            Instancia = Environment.MachineName
        };

        _context.TelevisorCortes.Add(corte);
        try
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Televisor: foto {Tipo} del {Dia:yyyy-MM-dd} guardada{Recuperada}.",
                tipo, dia, recuperada ? " (recuperada)" : string.Empty);
        }
        catch (DbUpdateException)
        {
            // Otra instancia la tomó primero (índice único por día y tipo): vale la suya.
            _context.Entry(corte).State = EntityState.Detached;
        }
    }

    private async Task<TelevisorTablero?> LeerAsync(DateTime dia, string tipo, CancellationToken ct)
    {
        var json = await _context.TelevisorCortes.AsNoTracking()
            .Where(x => x.Dia == dia.Date && x.Tipo == tipo)
            .Select(x => x.DatosJson)
            .FirstOrDefaultAsync(ct);
        var tablero = json is null ? null : JsonSerializer.Deserialize<TelevisorTablero>(json);
        if (tablero is not null)
        {
            TelevisorIndicadoresService.CompletarPromedios(tablero);
        }

        return tablero;
    }
}
