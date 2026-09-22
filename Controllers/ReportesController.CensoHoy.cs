using Nexa.Data;
using Nexa.Helpers;
using Nexa.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

/// <summary>
/// "Censo de hoy": las tarjetas de arriba del tablero. Es la foto de la operación en este momento y a
/// propósito no mira ningún filtro (ni fechas, ni municipio, ni los filtros propios de los paneles): la
/// pantalla las pone antes de los filtros para que se lea que no cambian con ellos.
///
/// Las reglas de "activo" son las mismas de cada panel y del informe de pacientes activos; viven aquí
/// y los paneles las reutilizan.
/// </summary>
public partial class ReportesController
{
    private async Task<ReportesCensoHoyViewModel> ConstruirCensoHoyAsync(ApplicationDbContext contexto, CancellationToken ct)
    {
        var agudos = await SoloAgudosActivos(contexto.Censos.AsNoTracking().Where(CensoVisibility.EditableRecord(contexto)))
            .Select(x => x.NumeroIdentificacion)
            .ToListAsync(ct);

        var cronicos = (await contexto.CensoCronicos.AsNoTracking()
                .Select(x => new { x.NumeroIdentificacion, x.FechaEgreso, x.EstadoPaciente })
                .ToListAsync(ct))
            .Where(x => EsCronicoActivo(x.FechaEgreso, x.EstadoPaciente))
            .Select(x => x.NumeroIdentificacion)
            .ToList();

        var heridas = (await contexto.CensoClinicaHeridas.AsNoTracking()
                .Select(x => new { x.NumeroIdentificacion, x.FechaEgreso, x.Estado, x.Vac })
                .ToListAsync(ct))
            .Where(x => EsActivoSinEgreso(x.FechaEgreso, x.Estado))
            .ToList();

        var npt = (await contexto.CensoNpt.AsNoTracking()
                .Select(x => new { x.NumeroIdentificacion, x.FechaEgreso, x.Estado })
                .ToListAsync(ct))
            .Where(x => EsActivoSinEgreso(x.FechaEgreso, x.Estado))
            .Select(x => x.NumeroIdentificacion)
            .ToList();

        var terapia = (await contexto.CensoTerapiasAmbulatorias.AsNoTracking()
                .Select(x => new { x.NumeroIdentificacion, x.EstadoPaciente, x.EstadoAlta })
                .ToListAsync(ct))
            .Where(x => EsTerapiaActiva(x.EstadoPaciente, x.EstadoAlta))
            .Select(x => x.NumeroIdentificacion)
            .ToList();

        var programas = new (string Programa, string Vista, IReadOnlyList<string> Documentos, string? Destacado)[]
        {
            ("AGUDOS", ReportesVistas.Agudos, agudos, null),
            ("CRONICOS", ReportesVistas.Cronicos, cronicos, null),
            ("CLINICA_HERIDAS", ReportesVistas.Heridas, heridas.Select(x => x.NumeroIdentificacion).ToList(),
                $"{ReportesFormato.Entero(heridas.Count(x => EsIgual(x.Vac, "Si")))} con VAC"),
            ("NPT", ReportesVistas.Npt, npt, null),
            ("TERAPIA_AMBULATORIA", ReportesVistas.Terapia, terapia, null)
        };

        // Una persona es un documento. Se cuentan programas distintos por documento: dos registros
        // activos del mismo programa (p. ej. dos terapias) no hacen que el paciente "esté en dos
        // programas". Los programas de cada persona quedan en el orden de las tarjetas.
        var programasPorPersona = programas
            .SelectMany((x, orden) => x.Documentos.Select(d => (Documento: NormalizarDocumento(d), Orden: orden)))
            .Where(x => x.Documento.Length > 0)
            .GroupBy(x => x.Documento, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Orden).Distinct().Order().ToList(),
                StringComparer.Ordinal);

        var enVarios = programasPorPersona.Values.Where(x => x.Count > 1).ToList();
        var normalizados = programas.Select(x => x.Documentos.Select(NormalizarDocumento).ToList()).ToList();

        return new ReportesCensoHoyViewModel
        {
            PacientesUnicos = programasPorPersona.Count,
            PacientesEnVariosProgramas = enVarios.Count,
            // De la suma de las tarjetas a las personas: cada diferencia tiene su causa.
            CuentasEnOtrosProgramas = enVarios.Sum(x => x.Count - 1),
            AtencionesRepetidasEnUnPrograma = normalizados.Sum(l =>
                l.Count(d => d.Length > 0) - l.Where(d => d.Length > 0).Distinct(StringComparer.Ordinal).Count()),
            AtencionesSinDocumento = normalizados.Sum(l => l.Count(d => d.Length == 0)),
            Programas = programas
                .Select(x => new ReportesCensoHoyProgramaViewModel
                {
                    Programa = x.Programa,
                    Vista = x.Vista,
                    Nombre = NombrePrograma(x.Vista),
                    Activos = x.Documentos.Count,
                    EnOtroPrograma = x.Documentos
                        .Select(NormalizarDocumento)
                        .Where(d => d.Length > 0)
                        .Distinct(StringComparer.Ordinal)
                        .Count(d => programasPorPersona[d].Count > 1),
                    Destacado = x.Destacado
                })
                .ToList(),
            Combinaciones = enVarios
                .GroupBy(x => string.Join(",", x))
                .Select(g => new ReportesCombinacionViewModel
                {
                    Programas = g.First().Select(i => NombrePrograma(programas[i].Vista)).ToList(),
                    Claves = g.First().Select(i => programas[i].Programa).ToList(),
                    Pacientes = g.Count()
                })
                .OrderByDescending(x => x.Pacientes)
                .ThenBy(x => x.Programas.Count)
                .ThenBy(x => x.Texto, StringComparer.Create(Cultura, ignoreCase: true))
                .ToList()
        };
    }

    /// <summary>
    /// Novedades del portal que siguen sin resolver, sin importar cuándo se crearon: el pendiente de
    /// hoy, no el del periodo.
    /// </summary>
    private async Task<ReportesPortalHoyViewModel> ConstruirPortalHoyAsync(CancellationToken ct)
    {
        try
        {
            var (pendientes, masAntiguaUtc) = await _portalNovedadRepository.GetPendientesAsync(ct);
            return new ReportesPortalHoyViewModel
            {
                Pendientes = pendientes,
                PendienteMasAntigua = masAntiguaUtc.HasValue ? ColombiaTime.Convert(masAntiguaUtc.Value) : null
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "No se pudieron contar las novedades pendientes del Portal Administrativo.");
            return new ReportesPortalHoyViewModel { Error = true };
        }
    }

    private static string NombrePrograma(string vista) => vista switch
    {
        ReportesVistas.Agudos => "Agudos",
        ReportesVistas.Cronicos => "Crónicos",
        ReportesVistas.Heridas => "Clínica de heridas",
        ReportesVistas.Npt => "NPT",
        ReportesVistas.Terapia => "Terapia ambulatoria",
        _ => vista
    };

    /// <summary>Crónicos: sin egreso real y con el paciente distinto de "Inactivo".</summary>
    private static bool EsCronicoActivo(DateTime? fechaEgreso, string? estadoPaciente) =>
        !CensoVisibility.HayEgreso(fechaEgreso)
        && !string.Equals(estadoPaciente, "Inactivo", StringComparison.OrdinalIgnoreCase);

    /// <summary>Clínica de heridas y NPT: sin egreso real y con estado "Activo".</summary>
    private static bool EsActivoSinEgreso(DateTime? fechaEgreso, string? estado) =>
        !CensoVisibility.HayEgreso(fechaEgreso)
        && string.Equals(estado, "Activo", StringComparison.OrdinalIgnoreCase);

    /// <summary>Terapia ambulatoria: paciente "Activo" y alta distinta de "Cerrado".</summary>
    private static bool EsTerapiaActiva(string? estadoPaciente, string? estadoAlta) =>
        string.Equals(estadoPaciente, "Activo", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(estadoAlta, "Cerrado", StringComparison.OrdinalIgnoreCase);
}
