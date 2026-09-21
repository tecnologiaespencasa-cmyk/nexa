using Nexa.Data;
using Nexa.Helpers;
using Nexa.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

/// <summary>
/// Panel de crónicos (tabla <c>censo_cronicos</c>).
///
/// Activo hoy: sin fecha de egreso real y con el estado del paciente distinto de "Inactivo", igual
/// que el informe de pacientes activos. La fecha 0001-01-01 que dejaron algunas cargas antiguas no es
/// un egreso (<see cref="CensoVisibility.HayEgreso"/>).
///
/// Las escalas (Barthel, Karnofsky…), los dispositivos y los servicios complementarios no se grafican:
/// al 2026-09-18 ningún paciente activo los tiene diligenciados con un valor distinto de "No" o vacío, y
/// un gráfico de campos vacíos haría creer que el programa no tiene pacientes con sonda o terapia.
/// </summary>
public partial class ReportesController
{
    private async Task<ResultadoPrograma<ReportesCronicosViewModel>> ConstruirCronicosAsync(
        ApplicationDbContext contexto,
        ReportesFilterViewModel f,
        Periodo p,
        DateTime hoy,
        CancellationToken ct)
    {
        var query = contexto.CensoCronicos.AsNoTracking();
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
                x.FechaEgreso,
                x.MotivoEgreso,
                x.GrupoPatologiaCronica,
                x.MunicipioResidencia,
                x.NumeroIdentificacion
            })
            .ToListAsync(ct);

        bool EsActivo(DateTime? fechaEgreso, string? estado) =>
            !CensoVisibility.HayEgreso(fechaEgreso)
            && !string.Equals(estado, "Inactivo", StringComparison.OrdinalIgnoreCase);

        var activos = filas.Where(x => EsActivo(x.FechaEgreso, x.EstadoPaciente)).ToList();
        var ingresos = filas.Where(x => p.Contiene(x.FechaIngreso)).ToList();
        var egresos = filas
            .Where(x => CensoVisibility.HayEgreso(x.FechaEgreso) && p.Contiene(x.FechaEgreso!.Value))
            .ToList();

        // Las agudizaciones y hospitalizaciones son multi-registro y guardan su fecha clínica dentro de
        // un JSON; aquí se cuentan por la fecha en que se registraron, y así lo dice el panel.
        var ids = filas.Select(x => x.Id).ToList();
        var agudizaciones = await contexto.CensoCronicoAgudizaciones.AsNoTracking()
            .Where(x => ids.Contains(x.CensoCronicoRecordId))
            .Select(x => x.CreatedAtUtc)
            .ToListAsync(ct);
        var hospitalizaciones = await contexto.CensoCronicoHospitalizaciones.AsNoTracking()
            .Where(x => ids.Contains(x.CensoCronicoRecordId))
            .Select(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var modelo = new ReportesCronicosViewModel
        {
            Nombre = "Crónicos",
            ActivosHoy = activos.Count,
            IngresosPeriodo = ingresos.Count,
            IngresosPeriodoAnterior = filas.Count(x => p.Anterior.Contiene(x.FechaIngreso)),
            PromedioIngresosDia = Promedio(ingresos.Count, p.Dias),
            Ingresos = ConstruirSerie(ingresos.Select(x => x.FechaIngreso), p),
            Destacado = egresos.Count == 1 ? "1 egreso en el periodo" : $"{ReportesFormato.Entero(egresos.Count)} egresos en el periodo",
            EgresosPeriodo = egresos.Count,
            InactivosSinFechaEgreso = filas.Count(x => !CensoVisibility.HayEgreso(x.FechaEgreso)
                && string.Equals(x.EstadoPaciente, "Inactivo", StringComparison.OrdinalIgnoreCase)),
            Flujo = ConstruirFlujo(ingresos.Select(x => x.FechaIngreso), egresos.Select(x => x.FechaEgreso!.Value), p),
            Antiguedad = ConstruirAntiguedad(activos.Select(x => x.FechaIngreso), hoy),
            Patologia = ConstruirCategorias(
                Contar(activos.Select(x => x.GrupoPatologiaCronica), FraseEnMinuscula, "Sin diagnóstico registrado"),
                activos.Count,
                maximoFilas: 7,
                esSecundaria: x => x == "Sin diagnóstico registrado"),
            MunicipiosActivos = ConstruirCategorias(
                Contar(activos.Select(x => x.MunicipioResidencia), EtiquetaMunicipio, "Sin municipio"),
                activos.Count,
                maximoFilas: 7,
                esSecundaria: x => x == "Sin municipio"),
            MotivosEgreso = ConstruirCategorias(
                Contar(egresos.Select(x => x.MotivoEgreso), FraseEnMinuscula, "Sin motivo registrado"),
                egresos.Count,
                esSecundaria: x => x == "Sin motivo registrado"),
            AgudizacionesRegistradas = agudizaciones.Count(x => p.Contiene(ColombiaTime.Convert(x))),
            HospitalizacionesRegistradas = hospitalizaciones.Count(x => p.Contiene(ColombiaTime.Convert(x)))
        };

        return new ResultadoPrograma<ReportesCronicosViewModel>(
            modelo,
            activos.Select(x => x.NumeroIdentificacion).ToList());
    }

    /// <summary>
    /// Tiempo en el programa de los pacientes activos, por meses calendario cumplidos (no por días
    /// aproximados): quien ingresó el 18/06 cumple 3 meses el 18/09. Conserva los tramos en cero para
    /// que la forma de la distribución no engañe.
    /// </summary>
    private static List<ReportesCategoriaViewModel> ConstruirAntiguedad(IEnumerable<DateTime> ingresos, DateTime hoy)
    {
        var tramos = new[] { "Menos de 3 meses", "3 a 6 meses", "6 a 12 meses", "1 a 2 años", "2 años o más" };
        var conteo = tramos.ToDictionary(x => x, _ => 0, StringComparer.Ordinal);

        foreach (var ingreso in ingresos.Select(x => x.Date))
        {
            var tramo = ingreso.AddMonths(3) > hoy ? tramos[0]
                : ingreso.AddMonths(6) > hoy ? tramos[1]
                : ingreso.AddYears(1) > hoy ? tramos[2]
                : ingreso.AddYears(2) > hoy ? tramos[3]
                : tramos[4];
            conteo[tramo]++;
        }

        var total = conteo.Values.Sum();
        var mayor = conteo.Values.DefaultIfEmpty(0).Max();
        return tramos
            .Select(x => new ReportesCategoriaViewModel
            {
                Etiqueta = x,
                Valor = conteo[x],
                Porcentaje = total == 0 ? 0 : Math.Round(conteo[x] * 100d / total, 1),
                Barra = mayor == 0 ? 0 : Math.Round(conteo[x] * 100d / mayor, 2)
            })
            .ToList();
    }

    /// <summary>"DEMENCIA, NO ESPECIFICADA" → "Demencia, no especificada".</summary>
    private static string FraseEnMinuscula(string valor)
    {
        var limpio = valor.Trim();
        if (limpio.Any(char.IsLower))
        {
            return limpio;
        }

        var minuscula = limpio.ToLower(Cultura);
        return char.ToUpper(minuscula[0], Cultura) + minuscula[1..];
    }
}
