using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Models.ViewModels;

namespace Nexa.Controllers;

/// <summary>
/// Tabulado unificado del censo.
///
/// Con "todos los programas" muestra el juego de columnas núcleo, común a los cinco censos. Al
/// filtrar por un solo programa despliega el juego completo de columnas de ese censo, idéntico al
/// que muestra su pantalla propia: la unión literal de los cinco pasaría de seiscientas columnas y
/// sería ilegible.
/// </summary>
public partial class CensoController
{
    private const int LimiteFilasTabulado = 100;
    private const int LimiteFilasTabuladoConRango = 50;




    /// <summary>
    /// Exportable por programa. Agudos, crónicos y terapia reusan tal cual el exportable que ya
    /// tenían, para que el archivo salga con el mismo formato de siempre. Clínica de heridas y NPT
    /// no tenían uno y se les arma con todas las columnas de su tabla.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportarProgramaExcel(
        string? programa,
        string? cedulaPaciente,
        CancellationToken cancellationToken)
    {
        switch (programa)
        {
            case CensoProgramas.Agudos:
                // Con documento se usa el exportable filtrado, que respeta el mismo formato del
                // completo. Sin el, agudos bajaba el censo entero aunque se estuviera mirando un
                // solo paciente, mientras cronicos y terapia si filtraban.
                return string.IsNullOrWhiteSpace(NormalizeCedulaFilter(cedulaPaciente))
                    ? await ExportarExcel(cancellationToken)
                    : await ExportarExcelFiltroPersonalizado(cedulaPaciente, null, null, cancellationToken);
            case CensoProgramas.Cronicos:
                return await ExportarCronicosExcel(cedulaPaciente, cancellationToken);
            case CensoProgramas.TerapiaAmbulatoria:
                return await ExportarTerapiasAmbulatoriasExcel(cedulaPaciente, null, cancellationToken);
            case CensoProgramas.ClinicaHeridas:
            {
                var doc = NormalizeCedulaFilter(cedulaPaciente);
                var registros = await _context.CensoClinicaHeridas.AsNoTracking()
                    .Where(x => string.IsNullOrEmpty(doc) || x.NumeroIdentificacion == doc)
                    .OrderBy(x => x.Id)
                    .ToListAsync(cancellationToken);
                return ExportarPorReflexion(registros, "Clinica de heridas", "censo_clinica_heridas");
            }
            case CensoProgramas.Npt:
            {
                var doc = NormalizeCedulaFilter(cedulaPaciente);
                var registros = await _context.CensoNpt.AsNoTracking()
                    .Where(x => string.IsNullOrEmpty(doc) || x.NumeroIdentificacion == doc)
                    .OrderBy(x => x.Id)
                    .ToListAsync(cancellationToken);
                return ExportarPorReflexion(registros, "NPT", "censo_npt");
            }
            default:
                return await ExportarCensoUnificadoExcel(cedulaPaciente, cancellationToken);
        }
    }

    /// <summary>Exportable de todos los programas, con las columnas núcleo del tabulado.</summary>
    [HttpGet]
    public async Task<IActionResult> ExportarCensoUnificadoExcel(
        string? cedulaPaciente,
        CancellationToken cancellationToken)
    {
        var model = new CensoUnificadoViewModel { CedulaFiltro = NormalizeCedulaFilter(cedulaPaciente) };
        await _censoTabuladoService.ConstruirAsync(model, cancellationToken);

        string[] headers =
        [
            "Programa", "Estado del programa", "Número de registro", "Paciente", "Tipo de documento",
            "Número de documento", "Fecha de ingreso", "Estado", "Asegurador", "Zona Sura",
            "Diagnóstico", "Estado en farmacia", "Adjuntos"
        ];

        var filas = model.Filas.Select(x => (IReadOnlyList<string?>)new List<string?>
        {
            CensoProgramas.Nombre(x.Programa),
            x.Abierto ? "Abierto" : "Cerrado",
            x.RegistroId.ToString(CultureInfo.InvariantCulture),
            x.NombrePaciente,
            x.TipoIdentificacion,
            x.NumeroIdentificacion,
            x.FechaIngreso?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            x.Estado,
            x.Asegurador,
            x.ClasificacionZonaSura,
            x.DiagnosticoDescriptivo,
            x.EstadoFarmacia,
            x.TieneAdjuntos ? "Sí" : "No"
        }).ToList();

        var libro = ExcelWorkbookWriter.BuildTableWorkbook("Censo", headers, filas, DateTime.UtcNow);
        return File(
            libro,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"censo_unificado_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
    }

    /// <summary>
    /// Arma un exportable con todas las propiedades de la entidad. Se usa en los programas que no
    /// tenían exportable propio, para que el archivo salga con la información completa.
    /// </summary>
    private FileContentResult ExportarPorReflexion<T>(IReadOnlyList<T> registros, string hoja, string archivo)
    {
        var propiedades = typeof(T).GetProperties()
            .Where(x => x.PropertyType.IsPrimitive
                || x.PropertyType == typeof(string)
                || x.PropertyType == typeof(decimal)
                || x.PropertyType == typeof(DateTime)
                || x.PropertyType == typeof(DateTime?)
                || x.PropertyType == typeof(TimeSpan)
                || x.PropertyType == typeof(TimeSpan?)
                || Nullable.GetUnderlyingType(x.PropertyType)?.IsPrimitive == true)
            .ToArray();

        var headers = propiedades.Select(HumanizarColumna).ToList();
        var filas = registros.Select(registro => (IReadOnlyList<string?>)propiedades
            .Select(propiedad => FormatearValor(propiedad.GetValue(registro)))
            .ToList())
            .ToList();

        var libro = ExcelWorkbookWriter.BuildTableWorkbook(hoja, headers, filas, DateTime.UtcNow);
        return File(
            libro,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{archivo}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
    }

    internal static string HumanizarColumna(System.Reflection.PropertyInfo propiedad) =>
        System.Text.RegularExpressions.Regex
            .Replace(propiedad.Name, "([a-z0-9])([A-Z])", "$1 $2")
            .Replace(" Utc", " UTC", StringComparison.OrdinalIgnoreCase)
            .Replace(" Id", " ID", StringComparison.OrdinalIgnoreCase)
            .Replace("Cie 10", "CIE10", StringComparison.OrdinalIgnoreCase)
            .Replace("Ips", "IPS", StringComparison.OrdinalIgnoreCase)
            .Replace("Npt", "NPT", StringComparison.OrdinalIgnoreCase);

    internal static string? FormatearValor(object? valor) => valor switch
    {
        null => null,
        DateTime fecha => fecha.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        TimeSpan hora => hora.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
        bool booleano => booleano ? "Sí" : "No",
        _ => valor.ToString()
    };
}
