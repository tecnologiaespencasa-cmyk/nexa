using Nexa.Data;
using Nexa.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

/// <summary>
/// Panel de clínica de heridas (tabla <c>censo_clinica_heridas</c>).
///
/// Activo hoy: sin fecha de egreso real y con estado "Activo", igual que el informe de pacientes
/// activos. VAC no es un programa aparte sino el campo VAC en Sí de un paciente activo de heridas; por
/// eso se muestra como dato dentro del programa y no como una tarjeta propia.
/// </summary>
public partial class ReportesController
{
    private async Task<ReportesHeridasViewModel> ConstruirHeridasAsync(
        ApplicationDbContext contexto,
        ReportesFilterViewModel f,
        Periodo p,
        DateTime hoy,
        CancellationToken ct)
    {
        var query = contexto.CensoClinicaHeridas.AsNoTracking();
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
                x.Vac,
                x.CodigoCie10,
                x.DiagnosticoDescriptivo,
                x.AuxiliarEnfermeriaAsignado,
                x.FuenteIngreso,
                x.FrecuenciaVisita,
                x.NumeroIdentificacion
            })
            .ToListAsync(ct);

        var activos = filas.Where(x => EsActivoSinEgreso(x.FechaEgreso, x.Estado)).ToList();
        var ingresos = filas.Where(x => p.Contiene(x.FechaIngresoPrograma)).ToList();
        var egresos = filas
            .Where(x => CensoVisibility.HayEgreso(x.FechaEgreso) && p.Contiene(x.FechaEgreso!.Value))
            .ToList();
        var conVac = activos.Count(x => string.Equals(x.Vac, "Si", StringComparison.OrdinalIgnoreCase));

        // El diagnóstico se agrupa por código. La descripción sale del catálogo propio del programa y no
        // del texto guardado: cuatro registros de L039 conservan la descripción equivocada de L309.
        var catalogo = CensoController.ClinicaHeridasCie10Values;
        string? DescripcionCie10(string codigo)
        {
            if (catalogo.TryGetValue(codigo, out var descripcion))
            {
                return FraseEnMinuscula(descripcion);
            }

            var guardada = activos
                .Where(x => string.Equals(x.CodigoCie10?.Trim(), codigo, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.DiagnosticoDescriptivo)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            return guardada is null ? null : FraseEnMinuscula(guardada);
        }

        var modelo = new ReportesHeridasViewModel
        {
            Nombre = "Clínica de heridas",
            ActivosHoy = activos.Count,
            IngresosPeriodo = ingresos.Count,
            IngresosPeriodoAnterior = filas.Count(x => p.Anterior.Contiene(x.FechaIngresoPrograma)),
            PromedioIngresosDia = Promedio(ingresos.Count, p.Dias),
            Ingresos = ConstruirSerie(ingresos.Select(x => x.FechaIngresoPrograma), p),
            ActivosConVac = conVac,
            EgresosPeriodo = egresos.Count,
            InactivosSinFechaEgreso = filas.Count(x => !CensoVisibility.HayEgreso(x.FechaEgreso)
                && !string.Equals(x.Estado, "Activo", StringComparison.OrdinalIgnoreCase)),
            Flujo = ConstruirFlujo(
                ingresos.Select(x => x.FechaIngresoPrograma),
                egresos.Select(x => x.FechaEgreso!.Value),
                p),
            Diagnosticos = ConstruirCategorias(
                Contar(activos.Select(x => x.CodigoCie10?.Trim().ToUpperInvariant()), vacio: "Sin código"),
                activos.Count,
                maximoFilas: 8,
                detalle: x => x == "Sin código" ? null : DescripcionCie10(x),
                esSecundaria: x => x == "Sin código"),
            ActivosPorAuxiliar = ConstruirCategorias(
                Contar(activos.Select(x => x.AuxiliarEnfermeriaAsignado), vacio: "Sin auxiliar asignado"),
                activos.Count,
                maximoFilas: int.MaxValue,
                esSecundaria: x => x == "Sin auxiliar asignado"),
            ActivosSinAuxiliar = activos.Count(x => string.IsNullOrWhiteSpace(x.AuxiliarEnfermeriaAsignado)),
            MotivosEgreso = ConstruirCategorias(
                Contar(egresos.Select(x => x.MotivoEgreso), FraseEnMinuscula, "Sin motivo registrado"),
                egresos.Count,
                esSecundaria: x => x == "Sin motivo registrado"),
            FuenteIngreso = ConstruirSegmentos(
                ("Asegurador", ingresos.Count(x => EsIgual(x.FuenteIngreso, "Asegurador")), "n1"),
                ("Ordenamiento interno", ingresos.Count(x => EsIgual(x.FuenteIngreso, "Ordenamiento interno")), "n2"),
                ("Sin dato", ingresos.Count(x => !EsIgual(x.FuenteIngreso, "Asegurador")
                    && !EsIgual(x.FuenteIngreso, "Ordenamiento interno")), "neutro")),
            FrecuenciaVisita = ConstruirCategorias(
                Contar(activos.Select(x => x.FrecuenciaVisita), vacio: "Sin frecuencia registrada"),
                activos.Count,
                esSecundaria: x => x == "Sin frecuencia registrada")
        };

        return modelo;
    }
}
