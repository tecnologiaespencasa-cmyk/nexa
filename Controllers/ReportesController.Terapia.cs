using Nexa.Data;
using Nexa.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

/// <summary>
/// Panel de terapia ambulatoria (tabla <c>censo_terapias_ambulatorias</c>).
///
/// El ingreso se cuenta por <c>FechaIngreso</c>, igual que el tabulado de esta misma pantalla. Antes el
/// gráfico usaba <c>FechaInicio</c> (inicio del tratamiento), que puede caer días o semanas después:
/// un paciente que ingresó el 14/08 con inicio el 18/09 aparecía como ingreso del 18/09.
///
/// Activo hoy: paciente "Activo" y alta distinta de "Cerrado", igual que el informe de pacientes activos.
/// </summary>
public partial class ReportesController
{
    private async Task<ReportesTerapiaViewModel> ConstruirTerapiaAsync(
        ApplicationDbContext contexto,
        ReportesFilterViewModel f,
        Periodo p,
        DateTime hoy,
        CancellationToken ct)
    {
        var query = contexto.CensoTerapiasAmbulatorias.AsNoTracking();
        if (f.Municipio is not null)
        {
            query = query.Where(x => x.MunicipioResidencia == f.Municipio);
        }

        var filas = await query
            .Select(x => new
            {
                x.Id,
                x.FechaIngreso,
                x.EstadoPaciente,
                x.EstadoAlta,
                x.TipoTerapia,
                x.SegundoTratamientoTipoTerapia,
                x.TercerTratamientoTipoTerapia,
                x.EstadoGestion,
                x.Fisioterapeuta,
                x.NumeroIdentificacion
            })
            .ToListAsync(ct);

        var activos = filas.Where(x => EsTerapiaActiva(x.EstadoPaciente, x.EstadoAlta)).ToList();
        var ingresos = filas.Where(x => p.Contiene(x.FechaIngreso)).ToList();

        // Un ingreso puede pedir varias terapias (el campo guarda una lista separada por comas, y hay
        // hasta tres tratamientos). Cada terapia se cuenta una vez por ingreso, así que el porcentaje
        // es "de los ingresos, cuántos la pidieron" y la lista no suma el total a propósito.
        var terapiasPorIngreso = ingresos
            .Select(x => TiposDeTerapia(x.TipoTerapia, x.SegundoTratamientoTipoTerapia, x.TercerTratamientoTipoTerapia))
            .ToList();
        var terapias = terapiasPorIngreso
            .SelectMany(x => x)
            .GroupBy(x => x, StringComparer.Ordinal)
            .Select(g => (g.Key, g.Count()));

        var gestion = ingresos
            .GroupBy(x => string.IsNullOrWhiteSpace(x.EstadoGestion) ? SinDato : x.EstadoGestion.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => (Etiqueta: g.Key, Valor: g.Count(), Tono: TonoGestionTerapia(g.Key)))
            .OrderBy(x => OrdenGestionTerapia(x.Etiqueta))
            .ToArray();

        var modelo = new ReportesTerapiaViewModel
        {
            Nombre = "Terapia ambulatoria",
            ActivosHoy = activos.Count,
            IngresosPeriodo = ingresos.Count,
            IngresosPeriodoAnterior = filas.Count(x => p.Anterior.Contiene(x.FechaIngreso)),
            PromedioIngresosDia = Promedio(ingresos.Count, p.Dias),
            Ingresos = ConstruirSerie(ingresos.Select(x => x.FechaIngreso), p),
            TerapiasSolicitadas = ConstruirCategorias(terapias, ingresos.Count),
            EstadoGestion = ConstruirSegmentos(gestion),
            ActivosPorFisioterapeuta = ConstruirCategorias(
                Contar(activos.Select(x => x.Fisioterapeuta), vacio: "Sin fisioterapeuta asignado"),
                activos.Count,
                maximoFilas: int.MaxValue,
                esSecundaria: x => x == "Sin fisioterapeuta asignado"),
            ActivosSinFisioterapeuta = activos.Count(x => string.IsNullOrWhiteSpace(x.Fisioterapeuta))
        };

        return modelo;
    }

    private static HashSet<string> TiposDeTerapia(params string?[] campos)
    {
        return campos
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(x => x!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(EtiquetaTerapia)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string EtiquetaTerapia(string tipo) => tipo.Trim().ToLowerInvariant() switch
    {
        "terapia fisica" or "terapia física" => "Terapia física",
        "terapia respiratoria" => "Terapia respiratoria",
        "terapia ocupacional" => "Terapia ocupacional",
        "fonoaudiologia" or "fonoaudiología" => "Fonoaudiología",
        _ => FraseEnMinuscula(tipo)
    };

    private static string TonoGestionTerapia(string estado) => estado.Trim().ToLowerInvariant() switch
    {
        "gestión completa" or "gestion completa" => "ok",
        "datos confirmados" => "info",
        "pendiente confirmar datos" => "alerta",
        _ => "neutro"
    };

    private static int OrdenGestionTerapia(string estado) => TonoGestionTerapia(estado) switch
    {
        "alerta" => 0,
        "info" => 1,
        "ok" => 2,
        _ => 3
    };
}
