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

                // Los programas que cada paciente tiene agregados hoy (ver CensoController.ProgramasAgregados.cs).
                var programasAgregados = await ProgramasAgregadosAsync(
                    _context,
                    registros.Select(x => ((string?)x.NumeroIdentificacion, x.CensoPacienteId)).ToList(),
                    cancellationToken);
                return ExportarPorReflexion(
                    registros,
                    "Clinica de heridas",
                    "censo_clinica_heridas",
                    ("NombrePaciente", "Programas agregados", x => programasAgregados(x.NumeroIdentificacion, x.CensoPacienteId)));
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
                // Sin programa no hay exportable: el que bajaba "todos los programas" con las
                // trece columnas nucleo se retiro porque no era la informacion completa de
                // ningun censo. Cada programa se exporta con las suyas.
                return BadRequest("Indica el programa que quieres exportar.");
        }
    }

    /// <summary>
    /// Exportable resumido de los cinco programas juntos: el mismo juego de columnas núcleo que ya
    /// arma <see cref="ICensoTabuladoService.ConstruirFilasResumenAsync"/> para el tabulado en
    /// pantalla, sin el recorte de filas de la pantalla. No reemplaza los exportables por programa
    /// -esos traen todas sus columnas- es para una vista rápida de los cinco a la vez.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportarTodosLosProgramasExcel(
        string? cedulaPaciente,
        DateTime? desde,
        DateTime? hasta,
        CancellationToken cancellationToken)
    {
        var filas = await _censoTabuladoService.ConstruirFilasResumenAsync(
            cedulaPaciente, desde?.Date, hasta?.Date, cancellationToken);

        var ordenadas = filas
            .OrderByDescending(x => x.FechaIngreso ?? DateTime.MinValue)
            .ThenBy(x => CensoProgramas.Jerarquia(x.Programa))
            .ThenByDescending(x => x.RegistroId)
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
        sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
        sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
        sb.AppendLine(" <Styles>");
        sb.AppendLine("  <Style ss:ID=\"Header\"><Font ss:Bold=\"1\"/></Style>");
        sb.AppendLine(" </Styles>");
        sb.AppendLine(" <Worksheet ss:Name=\"Todos los programas\">");
        sb.AppendLine("  <Table>");

        sb.AppendLine("   <Row>");
        AppendHeaderCell(sb, "Programa");
        AppendHeaderCell(sb, "TipoIdentificacion");
        AppendHeaderCell(sb, "NumeroIdentificacion");
        AppendHeaderCell(sb, "NombrePaciente");
        AppendHeaderCell(sb, "FechaIngreso");
        AppendHeaderCell(sb, "Estado");
        AppendHeaderCell(sb, "Abierto");
        AppendHeaderCell(sb, "Asegurador");
        AppendHeaderCell(sb, "ClasificacionZonaSura");
        AppendHeaderCell(sb, "DiagnosticoDescriptivo");
        sb.AppendLine("   </Row>");

        foreach (var fila in ordenadas)
        {
            sb.AppendLine("   <Row>");
            AppendDataCell(sb, CensoProgramas.Nombre(fila.Programa));
            AppendDataCell(sb, fila.TipoIdentificacion);
            AppendDataCell(sb, fila.NumeroIdentificacion);
            AppendDataCell(sb, fila.NombrePaciente);
            AppendDataCell(sb, fila.FechaIngreso.HasValue ? fila.FechaIngreso.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "");
            AppendDataCell(sb, fila.Estado ?? "");
            AppendDataCell(sb, fila.Abierto ? "Abierto" : "Cerrado");
            AppendDataCell(sb, fila.Asegurador ?? "");
            AppendDataCell(sb, fila.ClasificacionZonaSura ?? "");
            AppendDataCell(sb, fila.DiagnosticoDescriptivo ?? "");
            sb.AppendLine("   </Row>");
        }

        sb.AppendLine("  </Table>");
        sb.AppendLine(" </Worksheet>");
        sb.AppendLine("</Workbook>");

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"censo_todos_los_programas_{DateTime.Now:yyyyMMdd_HHmmss}.xls";
        return File(bytes, "application/vnd.ms-excel", fileName);
    }

    /// <summary>
    /// Arma un exportable con todas las propiedades de la entidad. Se usa en los programas que no
    /// tenían exportable propio, para que el archivo salga con la información completa.
    /// </summary>
    /// <param name="extra">
    /// Columna calculada opcional (no existe en la tabla) y la propiedad después de la cual se ubica.
    /// </param>
    private FileContentResult ExportarPorReflexion<T>(
        IReadOnlyList<T> registros,
        string hoja,
        string archivo,
        (string DespuesDe, string Titulo, Func<T, string?> Valor)? extra = null)
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
        var filas = registros.Select(registro => propiedades
            .Select(propiedad => FormatearValor(propiedad.GetValue(registro)))
            .ToList())
            .ToList();

        if (extra is { } columna)
        {
            var posicion = Array.FindIndex(propiedades, x => x.Name == columna.DespuesDe) + 1;
            if (posicion <= 0)
            {
                posicion = headers.Count;
            }

            headers.Insert(posicion, columna.Titulo);
            for (var i = 0; i < registros.Count; i++)
            {
                filas[i].Insert(posicion, columna.Valor(registros[i]));
            }
        }

        var libro = ExcelWorkbookWriter.BuildTableWorkbook(
            hoja, headers, filas.Select(x => (IReadOnlyList<string?>)x).ToList(), DateTime.UtcNow);
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
