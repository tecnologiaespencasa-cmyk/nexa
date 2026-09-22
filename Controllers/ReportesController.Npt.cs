using Nexa.Data;
using Nexa.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

/// <summary>
/// Panel de NPT (tabla <c>censo_npt</c>).
///
/// Activo hoy: sin fecha de egreso real y con estado "Activo", igual que el informe de pacientes
/// activos. El programa tiene pocos pacientes, así que el panel los lista uno por uno en vez de
/// graficarlos: con tan pocos pacientes, un gráfico de barras dice menos que la tabla.
/// </summary>
public partial class ReportesController
{
    private async Task<ReportesNptViewModel> ConstruirNptAsync(
        ApplicationDbContext contexto,
        ReportesFilterViewModel f,
        Periodo p,
        DateTime hoy,
        CancellationToken ct)
    {
        var query = contexto.CensoNpt.AsNoTracking();
        if (f.Municipio is not null)
        {
            query = query.Where(x => x.MunicipioResidencia == f.Municipio);
        }

        var filas = await query
            .Select(x => new
            {
                x.Id,
                x.FechaIngresoPrograma,
                x.Estado,
                x.FechaEgreso,
                x.MotivoEgreso,
                x.NombrePaciente,
                x.TipoIdentificacion,
                x.NumeroIdentificacion,
                x.CodigoCie10,
                x.DiagnosticoDescriptivo,
                x.AuxiliarEnfermeriaAsignado,
                x.MunicipioResidencia
            })
            .ToListAsync(ct);

        var activos = filas.Where(x => EsActivoSinEgreso(x.FechaEgreso, x.Estado)).ToList();
        var ingresos = filas.Where(x => p.Contiene(x.FechaIngresoPrograma)).ToList();
        var egresos = filas
            .Where(x => CensoVisibility.HayEgreso(x.FechaEgreso) && p.Contiene(x.FechaEgreso!.Value))
            .ToList();

        string Diagnostico(string? codigo, string? descripcion) =>
            string.Join(" · ", new[] { codigo?.Trim(), string.IsNullOrWhiteSpace(descripcion) ? null : FraseEnMinuscula(descripcion) }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        var modelo = new ReportesNptViewModel
        {
            Nombre = "NPT",
            ActivosHoy = activos.Count,
            IngresosPeriodo = ingresos.Count,
            IngresosPeriodoAnterior = filas.Count(x => p.Anterior.Contiene(x.FechaIngresoPrograma)),
            PromedioIngresosDia = Promedio(ingresos.Count, p.Dias),
            Ingresos = ConstruirSerie(ingresos.Select(x => x.FechaIngresoPrograma), p),
            EgresosPeriodo = egresos.Count,
            PacientesActivos = activos
                .OrderBy(x => x.FechaIngresoPrograma)
                .ThenBy(x => x.Id)
                .Select(x => new ReportesPacienteFilaViewModel
                {
                    Paciente = x.NombrePaciente.Trim(),
                    Documento = Documento(x.TipoIdentificacion, x.NumeroIdentificacion),
                    NumeroIdentificacion = x.NumeroIdentificacion,
                    Fecha = x.FechaIngresoPrograma,
                    // Mismo cálculo que "días de estancia" en el informe de pacientes activos.
                    Dias = Math.Max(0, (hoy - x.FechaIngresoPrograma.Date).Days),
                    Diagnostico = Diagnostico(x.CodigoCie10, x.DiagnosticoDescriptivo),
                    Auxiliar = string.IsNullOrWhiteSpace(x.AuxiliarEnfermeriaAsignado) ? null : x.AuxiliarEnfermeriaAsignado.Trim(),
                    Municipio = string.IsNullOrWhiteSpace(x.MunicipioResidencia) ? null : EtiquetaMunicipio(x.MunicipioResidencia)
                })
                .ToList(),
            IngresosDelPeriodo = ingresos
                .OrderBy(x => x.FechaIngresoPrograma)
                .ThenBy(x => x.Id)
                .Select(x => new ReportesPacienteFilaViewModel
                {
                    Paciente = x.NombrePaciente.Trim(),
                    Documento = Documento(x.TipoIdentificacion, x.NumeroIdentificacion),
                    NumeroIdentificacion = x.NumeroIdentificacion,
                    Fecha = x.FechaIngresoPrograma,
                    Detalle = !CensoVisibility.HayEgreso(x.FechaEgreso)
                        && string.Equals(x.Estado, "Activo", StringComparison.OrdinalIgnoreCase)
                            ? "Activo"
                            : "Egresado"
                })
                .ToList(),
            EgresosDelPeriodo = egresos
                .OrderBy(x => x.FechaEgreso)
                .ThenBy(x => x.Id)
                .Select(x => new ReportesPacienteFilaViewModel
                {
                    Paciente = x.NombrePaciente.Trim(),
                    Documento = Documento(x.TipoIdentificacion, x.NumeroIdentificacion),
                    NumeroIdentificacion = x.NumeroIdentificacion,
                    Fecha = x.FechaEgreso!.Value,
                    Dias = Math.Max(0, (x.FechaEgreso!.Value.Date - x.FechaIngresoPrograma.Date).Days),
                    Detalle = string.IsNullOrWhiteSpace(x.MotivoEgreso) ? "Sin motivo registrado" : FraseEnMinuscula(x.MotivoEgreso)
                })
                .ToList()
        };

        return modelo;
    }
}
