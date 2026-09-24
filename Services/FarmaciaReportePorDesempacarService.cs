using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Models.ViewModels;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;
using Npgsql;

namespace Nexa.Services;

/// <summary>
/// Reporte diario de farmacia para dirección asistencial (pedido del 2026-09-23). Lleva los
/// despachos que en el día cumplieron las 72 horas empacados sin firma y que al corte de las
/// 11:00 p. m. siguen mostrándose "Por desempacar" en la bandeja: sin firmar y sin la bolsa
/// desempacada.
///
/// Cada corte cubre las 24 horas desde el corte anterior, así que ningún despacho queda entre dos
/// reportes. Se envía aunque no haya ninguno: así dirección asistencial sabe que el reporte corrió.
/// Solo lee la base. El paso a Por desempacar lo sigue haciendo la bandeja al abrirse
/// (FarmaciaController.ApplyEmpacadoTimeoutAsync), así que un despacho vencido que nadie ha visto
/// todavía figura como Empacado y el reporte lo cuenta igual.
/// </summary>
public class FarmaciaReportePorDesempacarService : IFarmaciaReportePorDesempacarService
{
    public const string DireccionAsistencialEmail = "direccionasistencial@especialistasencasa.com";

    /// <summary>
    /// Hora del corte, en hora Colombia. En los 90 días anteriores a su creación ningún despacho
    /// venció después de las 10 p. m., así que a esa hora el día ya está completo.
    /// </summary>
    public static readonly TimeSpan HoraCorte = new(23, 0, 0);

    /// <summary>
    /// Si la aplicación estaba caída a la hora del corte (un despliegue, un reinicio), el reporte
    /// sale en cuanto vuelve, pero solo durante estas horas: más tarde ya no se manda el del día
    /// anterior a media mañana.
    /// </summary>
    public static readonly TimeSpan VentanaRecuperacion = TimeSpan.FromHours(8);

    public static readonly TimeSpan EsperaReintento = TimeSpan.FromMinutes(15);

    /// <summary>Un envío que lleva este tiempo "Enviando" es de una instancia que se cayó a mitad.</summary>
    public static readonly TimeSpan EnvioAtascado = TimeSpan.FromMinutes(30);

    public const int MaximoIntentos = 5;

    /// <summary>El cuerpo del correo lista hasta este número de despachos; el Excel los trae todos.</summary>
    private const int FilasEnElCuerpo = 25;

    private const string ColorPendiente = "#b42318";
    private const string FondoPendiente = "#fff1f0";
    private const string ColorLimpio = "#087f5b";
    private const string FondoLimpio = "#e8f6f0";

    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");
    private static readonly string[] OrdenProgramas = ["Agudos", "Crónicos", "Clínica de heridas", "NPT"];

    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IAuditService _auditService;
    private readonly ILogger<FarmaciaReportePorDesempacarService> _logger;

    public FarmaciaReportePorDesempacarService(
        ApplicationDbContext context,
        IEmailService emailService,
        IAuditService auditService,
        ILogger<FarmaciaReportePorDesempacarService> logger)
    {
        _context = context;
        _emailService = emailService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>El último corte de las 11:00 p. m. que ya pasó y la ventana de 24 horas que cierra.</summary>
    public static FarmaciaCorteReporte CorteMasReciente(DateTime ahoraUtc)
    {
        var ahora = ColombiaTime.Convert(ahoraUtc);
        var corteHoy = ahora.Date + HoraCorte;
        var corte = ahora >= corteHoy ? corteHoy : corteHoy.AddDays(-1);
        return new FarmaciaCorteReporte(
            corte.Date,
            ColombiaTime.ConvertToUtc(corte.AddDays(-1)),
            ColombiaTime.ConvertToUtc(corte));
    }

    /// <summary>
    /// Un envío fallido se reintenta pasado un rato, y uno que se quedó "Enviando" se retoma cuando
    /// es claro que quien lo tomó ya no lo va a terminar. Uno enviado no se repite nunca.
    /// </summary>
    public static bool PuedeReintentarse(FarmaciaReporteDiarioEnvio envio, DateTime ahoraUtc) =>
        envio.Intentos < MaximoIntentos && envio.Estado switch
        {
            FarmaciaReporteDiarioEstados.Fallido => ahoraUtc - envio.IntentoAtUtc >= EsperaReintento,
            FarmaciaReporteDiarioEstados.Enviando => ahoraUtc - envio.IntentoAtUtc >= EnvioAtascado,
            _ => false
        };

    public async Task EnviarSiCorrespondeAsync(DateTime ahoraUtc, CancellationToken cancellationToken = default)
    {
        var corte = CorteMasReciente(ahoraUtc);
        if (ahoraUtc - corte.HastaUtc > VentanaRecuperacion)
        {
            return;
        }

        var envio = await TomarEnvioAsync(corte, ahoraUtc, cancellationToken);
        if (envio is null)
        {
            return;
        }

        var despachos = 0;
        try
        {
            var reporte = await ConstruirAsync(corte, ahoraUtc, cancellationToken);
            despachos = reporte.Despachos.Count;
            var resultado = await _emailService.SendAsync(
                ConstruirCorreo(reporte, [DireccionAsistencialEmail]),
                cancellationToken);

            if (!resultado.Succeeded)
            {
                _logger.LogWarning(
                    "No se pudo enviar el reporte de despachos por desempacar del {Dia:yyyy-MM-dd}: {Error}",
                    corte.Dia, resultado.ErrorMessage);
                await MarcarAsync(envio.Id, FarmaciaReporteDiarioEstados.Fallido, despachos, resultado.ErrorMessage, null, cancellationToken);
                return;
            }

            await MarcarAsync(envio.Id, FarmaciaReporteDiarioEstados.Enviado, despachos, null, DateTime.UtcNow, cancellationToken);
            await _auditService.LogAsync(
                "FARMACIA_REPORTE_POR_DESEMPACAR",
                "Farmacia",
                $"Reporte del {corte.Dia:dd/MM/yyyy} enviado a {DireccionAsistencialEmail}: {despachos} despacho(s) por desempacar.",
                null,
                null,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error al armar o enviar el reporte de despachos por desempacar del {Dia:yyyy-MM-dd}.", corte.Dia);
            await MarcarAsync(envio.Id, FarmaciaReporteDiarioEstados.Fallido, despachos, ex.Message, null, CancellationToken.None);
        }
    }

    /// <summary>
    /// Toma el envío del día para esta instancia. Devuelve null si ya salió, si otra instancia lo
    /// tiene o si todavía no toca reintentarlo.
    /// </summary>
    private async Task<FarmaciaReporteDiarioEnvio?> TomarEnvioAsync(
        FarmaciaCorteReporte corte,
        DateTime ahoraUtc,
        CancellationToken cancellationToken)
    {
        var instancia = Recortar(Environment.MachineName, 200);
        var existente = await _context.FarmaciaReporteDiarioEnvios
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Dia == corte.Dia, cancellationToken);

        if (existente is null)
        {
            var nuevo = new FarmaciaReporteDiarioEnvio
            {
                Dia = corte.Dia,
                DesdeUtc = corte.DesdeUtc,
                HastaUtc = corte.HastaUtc,
                Estado = FarmaciaReporteDiarioEstados.Enviando,
                Intentos = 1,
                Destinatarios = DireccionAsistencialEmail,
                Instancia = instancia,
                CreadoAtUtc = ahoraUtc,
                IntentoAtUtc = ahoraUtc
            };
            _context.FarmaciaReporteDiarioEnvios.Add(nuevo);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return nuevo;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Otra instancia insertó el mismo día un instante antes: el correo es suyo.
                _context.Entry(nuevo).State = EntityState.Detached;
                return null;
            }
        }

        if (!PuedeReintentarse(existente, ahoraUtc))
        {
            return null;
        }

        // Solo gana quien encuentre la fila tal cual la leyó: si otra instancia la retomó primero,
        // cambió el número de intentos y aquí no se actualiza nada.
        var tomadas = await _context.FarmaciaReporteDiarioEnvios
            .Where(x => x.Id == existente.Id && x.Estado == existente.Estado && x.Intentos == existente.Intentos)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Estado, FarmaciaReporteDiarioEstados.Enviando)
                .SetProperty(x => x.Intentos, x => x.Intentos + 1)
                .SetProperty(x => x.IntentoAtUtc, ahoraUtc)
                .SetProperty(x => x.Instancia, instancia),
                cancellationToken);

        return tomadas == 1 ? existente : null;
    }

    private Task<int> MarcarAsync(
        long id,
        string estado,
        int despachos,
        string? error,
        DateTime? enviadoAtUtc,
        CancellationToken cancellationToken)
    {
        var errorRecortado = error is null ? null : Recortar(error, 1000);
        return _context.FarmaciaReporteDiarioEnvios
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Estado, estado)
                .SetProperty(x => x.Despachos, despachos)
                .SetProperty(x => x.Error, errorRecortado)
                .SetProperty(x => x.EnviadoAtUtc, enviadoAtUtc),
                cancellationToken);
    }

    public async Task<FarmaciaReportePorDesempacar> ConstruirAsync(
        FarmaciaCorteReporte corte,
        DateTime ahoraUtc,
        CancellationToken cancellationToken = default)
    {
        var plazo = FarmaciaEstados.TiempoLimiteEmpacado;

        // Vencer dentro de la ventana (desde, hasta] es haberse empacado 72 horas antes.
        var empacadoDesde = corte.DesdeUtc - plazo;
        var empacadoHasta = corte.HastaUtc - plazo;

        // Lo empacado antes de esto ya venció aunque siga como Empacado: la bandeja lo pasa a Por
        // desempacar la próxima vez que alguien la abre, con esta misma regla.
        var vencidoSiEmpacadoAntesDe = ahoraUtc - plazo;

        var agudos = await _context.Censos
            .AsNoTracking()
            .Where(x => x.FarmaciaEnviadoAtUtc != null
                && x.FarmaciaEmpacadoAtUtc > empacadoDesde
                && x.FarmaciaEmpacadoAtUtc <= empacadoHasta
                && !x.FarmaciaBolsaDesempacada
                && (x.FarmaciaEstado == FarmaciaEstados.PorDesempacar
                    || (x.FarmaciaEstado == FarmaciaEstados.Empacado && x.FarmaciaEmpacadoAtUtc < vencidoSiEmpacadoAntesDe)))
            .Select(x => new
            {
                x.Id,
                x.NombrePaciente,
                x.TipoIdentificacion,
                x.NumeroIdentificacion,
                x.AuxiliarAsignado,
                Empacado = x.FarmaciaEmpacadoAtUtc!.Value,
                EsProrroga = x.FarmaciaProrrogaDeId != null || x.FarmaciaProrrogaVersionId != null,
                x.FarmaciaEsEntregaParcial,
                x.FarmaciaEntregaActual,
                x.FarmaciaCantidadEntregas
            })
            .ToListAsync(cancellationToken);

        var cronicos = await _context.CensoCronicoAgudizaciones
            .AsNoTracking()
            .Where(x => x.FarmaciaEnviadoAtUtc != null
                && x.FarmaciaEmpacadoAtUtc > empacadoDesde
                && x.FarmaciaEmpacadoAtUtc <= empacadoHasta
                && !x.FarmaciaBolsaDesempacada
                && (x.FarmaciaEstado == FarmaciaEstados.PorDesempacar
                    || (x.FarmaciaEstado == FarmaciaEstados.Empacado && x.FarmaciaEmpacadoAtUtc < vencidoSiEmpacadoAntesDe)))
            .Select(x => new
            {
                x.Id,
                x.Numero,
                x.CensoCronicoRecord.NombrePaciente,
                x.CensoCronicoRecord.TipoIdentificacion,
                x.CensoCronicoRecord.NumeroIdentificacion,
                Empacado = x.FarmaciaEmpacadoAtUtc!.Value,
                x.FarmaciaEsEntregaParcial,
                x.FarmaciaEntregaActual,
                x.FarmaciaCantidadEntregas
            })
            .ToListAsync(cancellationToken);

        var heridas = await _context.CensoClinicaHeridasKardex
            .AsNoTracking()
            .Where(x => x.FarmaciaEnviadoAtUtc != null
                && x.FarmaciaEmpacadoAtUtc > empacadoDesde
                && x.FarmaciaEmpacadoAtUtc <= empacadoHasta
                && !x.FarmaciaBolsaDesempacada
                && (x.FarmaciaEstado == FarmaciaEstados.PorDesempacar
                    || (x.FarmaciaEstado == FarmaciaEstados.Empacado && x.FarmaciaEmpacadoAtUtc < vencidoSiEmpacadoAntesDe)))
            .Select(x => new
            {
                x.Id,
                x.Tipo,
                Plan = x.Plan.Numero,
                x.CensoClinicaHeridasRecord.NombrePaciente,
                x.CensoClinicaHeridasRecord.TipoIdentificacion,
                x.CensoClinicaHeridasRecord.NumeroIdentificacion,
                Auxiliar = x.CensoClinicaHeridasRecord.AuxiliarEnfermeriaAsignado,
                Empacado = x.FarmaciaEmpacadoAtUtc!.Value,
                x.FarmaciaEsEntregaParcial,
                x.FarmaciaEntregaActual,
                x.FarmaciaCantidadEntregas
            })
            .ToListAsync(cancellationToken);

        var npt = await _context.CensoNptKardex
            .AsNoTracking()
            .Where(x => x.FarmaciaEnviadoAtUtc != null
                && x.FarmaciaEmpacadoAtUtc > empacadoDesde
                && x.FarmaciaEmpacadoAtUtc <= empacadoHasta
                && !x.FarmaciaBolsaDesempacada
                && (x.FarmaciaEstado == FarmaciaEstados.PorDesempacar
                    || (x.FarmaciaEstado == FarmaciaEstados.Empacado && x.FarmaciaEmpacadoAtUtc < vencidoSiEmpacadoAntesDe)))
            .Select(x => new
            {
                x.Id,
                x.CensoNptRecord.NombrePaciente,
                x.CensoNptRecord.TipoIdentificacion,
                x.CensoNptRecord.NumeroIdentificacion,
                Auxiliar = x.CensoNptRecord.AuxiliarEnfermeriaAsignado,
                Empacado = x.FarmaciaEmpacadoAtUtc!.Value,
                x.FarmaciaEsEntregaParcial,
                x.FarmaciaEntregaActual,
                x.FarmaciaCantidadEntregas
            })
            .ToListAsync(cancellationToken);

        // Las mismas etiquetas con que la bandeja muestra cada pedido.
        var despachos = agudos
            .Select(x => new FarmaciaDespachoPorDesempacar(
                "Agudos",
                Detalle(x.EsProrroga ? "Prórroga" : null, Entrega(x.FarmaciaEsEntregaParcial, x.FarmaciaEntregaActual, x.FarmaciaCantidadEntregas)),
                x.Id, x.NombrePaciente, x.TipoIdentificacion, x.NumeroIdentificacion, x.AuxiliarAsignado,
                x.Empacado, x.Empacado + plazo))
            .Concat(cronicos.Select(x => new FarmaciaDespachoPorDesempacar(
                "Crónicos",
                Detalle($"Agudización #{x.Numero}", Entrega(x.FarmaciaEsEntregaParcial, x.FarmaciaEntregaActual, x.FarmaciaCantidadEntregas)),
                x.Id, x.NombrePaciente, x.TipoIdentificacion, x.NumeroIdentificacion, null,
                x.Empacado, x.Empacado + plazo)))
            .Concat(heridas.Select(x => new FarmaciaDespachoPorDesempacar(
                "Clínica de heridas",
                Detalle($"{ClinicaHeridasKardexTipos.Nombre(x.Tipo)} · Plan {x.Plan}", Entrega(x.FarmaciaEsEntregaParcial, x.FarmaciaEntregaActual, x.FarmaciaCantidadEntregas)),
                x.Id, x.NombrePaciente, x.TipoIdentificacion, x.NumeroIdentificacion, x.Auxiliar,
                x.Empacado, x.Empacado + plazo)))
            .Concat(npt.Select(x => new FarmaciaDespachoPorDesempacar(
                "NPT",
                Detalle(Entrega(x.FarmaciaEsEntregaParcial, x.FarmaciaEntregaActual, x.FarmaciaCantidadEntregas)),
                x.Id, x.NombrePaciente, x.TipoIdentificacion, x.NumeroIdentificacion, x.Auxiliar,
                x.Empacado, x.Empacado + plazo)))
            .Select(x => x with
            {
                Paciente = x.Paciente?.Trim() ?? string.Empty,
                TipoDocumento = x.TipoDocumento?.Trim() ?? string.Empty,
                Documento = x.Documento?.Trim() ?? string.Empty,
                Auxiliar = string.IsNullOrWhiteSpace(x.Auxiliar) ? null : x.Auxiliar.Trim()
            })
            .OrderBy(x => x.VencioUtc)
            .ThenBy(x => Array.IndexOf(OrdenProgramas, x.Programa))
            .ThenBy(x => x.Paciente, StringComparer.Create(Colombia, ignoreCase: true))
            .ToList();

        return new FarmaciaReportePorDesempacar(corte.Dia, corte.DesdeUtc, corte.HastaUtc, DateTime.UtcNow, despachos);
    }

    public EmailMessage ConstruirCorreo(
        FarmaciaReportePorDesempacar reporte,
        IReadOnlyList<string> destinatarios,
        bool esPrueba = false)
    {
        var total = reporte.Despachos.Count;
        var dia = reporte.Dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var asunto = total == 0
            ? $"Farmacia: ningún despacho por desempacar · {dia}"
            : $"Farmacia: {total} {(total == 1 ? "despacho sigue" : "despachos siguen")} por desempacar · {dia}";
        var archivo = $"farmacia_por_desempacar_{reporte.Dia:yyyyMMdd}.xlsx";

        return new EmailMessage
        {
            To = destinatarios,
            Subject = esPrueba ? $"[PRUEBA] {asunto}" : asunto,
            HtmlBody = ConstruirHtml(reporte, archivo, esPrueba ? destinatarios : null),
            // Sin despachos no hay listado: el correo solo confirma que el día cerró limpio.
            Attachments = total == 0
                ? []
                :
                [
                    new EmailAttachment
                    {
                        FileName = archivo,
                        ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        Content = ConstruirExcel(reporte)
                    }
                ]
        };
    }

    private static byte[] ConstruirExcel(FarmaciaReportePorDesempacar reporte)
    {
        string[] encabezados =
        [
            "Programa", "Detalle", "Pedido", "Paciente", "Tipo de documento", "Documento",
            "Auxiliar asignado", "Empacado", "Pasó a Por desempacar"
        ];
        var filas = reporte.Despachos
            .Select(x => (IReadOnlyList<string?>)
            [
                x.Programa,
                x.Detalle,
                x.Pedido.ToString(CultureInfo.InvariantCulture),
                x.Paciente,
                x.TipoDocumento,
                x.Documento,
                x.Auxiliar,
                FechaHora(x.EmpacadoUtc),
                FechaHora(x.VencioUtc)
            ])
            .ToList();

        return ExcelWorkbookWriter.BuildTableWorkbook(
            "Por desempacar", encabezados, filas, reporte.GeneradoUtc,
            documentTitle: "Despachos por desempacar");
    }

    private static string ConstruirHtml(
        FarmaciaReportePorDesempacar reporte,
        string archivo,
        IReadOnlyList<string>? destinatariosDePrueba)
    {
        var total = reporte.Despachos.Count;
        var color = total == 0 ? ColorLimpio : ColorPendiente;
        var ventana = $"entre el {DiaYHora(reporte.DesdeUtc)} y el {DiaYHora(reporte.HastaUtc)}";
        var html = new StringBuilder();

        html.Append("""<div style="margin:0;padding:24px 12px;background:#f0f4fa;">""");
        html.Append($"""<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:640px;margin:0 auto;border-collapse:separate;background:#ffffff;border:1px solid #dfe7f2;border-top:4px solid {color};border-radius:10px;font-family:'Segoe UI',Arial,Helvetica,sans-serif;color:#162033;">""");

        if (destinatariosDePrueba is not null)
        {
            html.Append($"""<tr><td style="padding:12px 28px;background:#fff7e0;border-radius:6px 6px 0 0;font-size:12px;line-height:1.5;color:#7a5200;"><strong>Correo de prueba</strong> enviado solo a {E(string.Join(", ", destinatariosDePrueba))}. El reporte real sale todos los días a las 11:00 p. m. a {E(DireccionAsistencialEmail)}.</td></tr>""");
        }

        html.Append("""<tr><td style="padding:24px 28px 6px;">""");
        html.Append($"""<p style="margin:0;font-size:11px;font-weight:700;letter-spacing:0.08em;text-transform:uppercase;color:{color};">Farmacia · Corte de las 11:00 p. m.</p>""");

        if (total == 0)
        {
            html.Append("""<h1 style="margin:8px 0 6px;font-size:22px;line-height:1.3;font-weight:700;color:#162033;">Ningún despacho quedó por desempacar</h1>""");
            html.Append($"""<p style="margin:0 0 22px;font-size:14px;line-height:1.55;color:#647084;">{E(Mayuscula(ventana))}, ningún despacho quedó pendiente: o ninguno cumplió las 72 horas empacado sin firma, o los que las cumplieron ya se firmaron o se desempacaron.</p>""");
            html.Append("</td></tr>");
        }
        else
        {
            var titulo = total == 1 ? "1 despacho sigue por desempacar" : $"{total} despachos siguen por desempacar";
            html.Append($"""<h1 style="margin:8px 0 6px;font-size:22px;line-height:1.3;font-weight:700;color:#162033;">{E(titulo)}</h1>""");
            html.Append($"""<p style="margin:0;font-size:14px;line-height:1.55;color:#647084;">Cumplieron las 72 horas empacados sin firma {E(ventana)}, y a la hora del corte no se habían firmado ni desempacado.</p>""");
            html.Append("</td></tr>");

            // Cuántos de cada programa, en el orden de la bandeja.
            html.Append("""<tr><td style="padding:14px 28px 2px;font-size:13px;">""");
            foreach (var grupo in reporte.Despachos
                .GroupBy(x => x.Programa)
                .OrderBy(g => Array.IndexOf(OrdenProgramas, g.Key)))
            {
                html.Append($"""<span style="display:inline-block;margin:0 6px 6px 0;padding:3px 10px;border-radius:999px;background:{FondoPendiente};color:{ColorPendiente};font-weight:700;">{E(grupo.Key)} · {grupo.Count()}</span>""");
            }
            html.Append("</td></tr>");

            html.Append("""<tr><td style="padding:8px 28px 4px;">""");
            html.Append("""<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="border-collapse:collapse;font-size:13px;">""");
            html.Append("<thead><tr>");
            html.Append(Encabezado("Paciente", "left"));
            html.Append(Encabezado("Programa", "left"));
            html.Append(Encabezado("Pasó a Por desempacar", "right"));
            html.Append("</tr></thead><tbody>");
            foreach (var despacho in reporte.Despachos.Take(FilasEnElCuerpo))
            {
                var documento = E((despacho.TipoDocumento + " " + despacho.Documento).Trim());
                var detalle = despacho.Detalle is null
                    ? string.Empty
                    : """<br><span style="font-size:12px;color:#647084;">""" + E(despacho.Detalle) + "</span>";

                html.Append("<tr>");
                html.Append($"""<td style="padding:9px 10px 9px 0;border-bottom:1px solid #eef2f7;vertical-align:top;"><strong>{E(despacho.Paciente)}</strong><br><span style="font-size:12px;color:#647084;">{documento}</span></td>""");
                html.Append($"""<td style="padding:9px 10px 9px 0;border-bottom:1px solid #eef2f7;vertical-align:top;">{E(despacho.Programa)}{detalle}</td>""");
                html.Append($"""<td align="right" style="padding:9px 0;border-bottom:1px solid #eef2f7;vertical-align:top;white-space:nowrap;">{E(DiaCortoYHora(despacho.VencioUtc))}</td>""");
                html.Append("</tr>");
            }
            html.Append("</tbody></table>");
            if (total > FilasEnElCuerpo)
            {
                html.Append($"""<p style="margin:10px 0 0;font-size:13px;color:#647084;">Y {total - FilasEnElCuerpo} más en el Excel adjunto.</p>""");
            }
            html.Append("</td></tr>");

            html.Append($"""<tr><td style="padding:14px 28px 22px;font-size:13px;line-height:1.55;color:#647084;">El Excel adjunto (<strong style="color:#162033;">{E(archivo)}</strong>) trae el listado completo, con el número de pedido, el auxiliar asignado y la hora en que se empacó.</td></tr>""");
        }

        html.Append("""<tr><td style="padding:14px 28px;border-top:1px solid #dfe7f2;font-size:12px;line-height:1.5;color:#8a94a6;">Correo automático de Nexa · Especialistas en Casa. Sale todos los días a las 11:00 p. m.</td></tr>""");
        html.Append("</table></div>");
        return html.ToString();
    }

    private static string Encabezado(string texto, string alineacion) =>
        $"""<th align="{alineacion}" style="padding:8px {(alineacion == "right" ? "0" : "10px")} 8px 0;border-bottom:2px solid #dfe7f2;font-size:11px;font-weight:700;letter-spacing:0.06em;text-transform:uppercase;color:#647084;">{E(texto)}</th>""";

    private static string? Detalle(params string?[] partes)
    {
        var texto = string.Join(" · ", partes.Where(x => !string.IsNullOrWhiteSpace(x)));
        return texto.Length == 0 ? null : texto;
    }

    private static string? Entrega(bool? esParcial, int actual, int? total) =>
        esParcial == true && total > 1 ? $"Entrega {actual} de {total}" : null;

    /// <summary>"martes 22/09/2026 a las 11:00 p. m."</summary>
    private static string DiaYHora(DateTime utc)
    {
        var hora = ColombiaTime.Convert(utc);
        return $"{hora.ToString("dddd dd/MM/yyyy", Colombia)} a las {hora.ToString("h:mm tt", Colombia)}";
    }

    /// <summary>"23/09 · 8:15 p. m.": la ventana cruza la medianoche, así que la hora sola no basta.</summary>
    private static string DiaCortoYHora(DateTime utc)
    {
        var hora = ColombiaTime.Convert(utc);
        return $"{hora.ToString("dd/MM", CultureInfo.InvariantCulture)} · {hora.ToString("h:mm tt", Colombia)}";
    }

    private static string FechaHora(DateTime utc) =>
        ColombiaTime.Convert(utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private static string Mayuscula(string texto) =>
        texto.Length == 0 ? texto : char.ToUpper(texto[0], Colombia) + texto[1..];

    private static string E(string? texto) => WebUtility.HtmlEncode(texto ?? string.Empty);

    private static string Recortar(string texto, int maximo) =>
        texto.Length <= maximo ? texto : texto[..maximo];
}
