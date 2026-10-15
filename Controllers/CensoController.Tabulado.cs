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

                var hoyHeridas = GetColombiaNow().Date;
                return ExportarPorReflexion(
                    registros,
                    "Clinica de heridas",
                    "censo_clinica_heridas",
                    new ColumnaCalculada<CensoClinicaHeridasRecord>(
                        "NombrePaciente",
                        "Programas agregados",
                        x => programasAgregados(x.NumeroIdentificacion, x.CensoPacienteId)),
                    new ColumnaCalculada<CensoClinicaHeridasRecord>(
                        "Programas agregados",
                        "Días de estancia al momento del egreso",
                        x => DiasDeEstancia(
                            x.FechaIngresoPrograma,
                            CensoVisibility.HayEgreso(x.FechaEgreso) ? x.FechaEgreso!.Value : hoyHeridas),
                        Numerica: true),
                    new ColumnaCalculada<CensoClinicaHeridasRecord>(
                        "Días de estancia al momento del egreso",
                        "Días de estancia a la fecha de hoy",
                        x => DiasDeEstancia(x.FechaIngresoPrograma, hoyHeridas),
                        Numerica: true));
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
    /// Informe de ingresos: cada ingreso de los cinco programas entre dos fechas, uno por fila y
    /// ordenado por día. Reemplazó al exportable "Todos los programas (resumen)" (2026-09-23), que
    /// listaba registros sin importar cuándo ingresaron.
    ///
    /// Cuenta como ingreso lo mismo que el tablero de Reportes, para que las cifras cuadren: agudos por
    /// FechaIngreso, sin las copias internas de despacho a farmacia ni las atenciones canceladas o
    /// rechazadas (el mismo filtro de ReportesController.ExcludeCancelledAndRejected); crónicos y
    /// terapia por FechaIngreso; clínica de heridas y NPT por FechaIngresoPrograma. Sin fechas, el mes
    /// en curso.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportarIngresosExcel(
        DateTime? desde,
        DateTime? hasta,
        CancellationToken cancellationToken)
    {
        var hoy = GetColombiaNow().Date;
        var inicio = (desde ?? new DateTime(hoy.Year, hoy.Month, 1)).Date;
        var fin = (hasta ?? hoy).Date;
        if (fin < inicio)
        {
            (inicio, fin) = (fin, inicio);
        }

        // El día siguiente al fin, sin desbordar si alguien pide hasta el 31/12/9999 por la URL.
        var hastaExclusivo = fin < DateTime.MaxValue.Date ? fin.AddDays(1) : DateTime.MaxValue;
        var ingresos = new List<IngresoDelInforme>();

        ingresos.AddRange(await _context.Censos.AsNoTracking()
            .Where(CensoVisibility.EditableRecord(_context))
            .Where(x => x.FechaIngreso >= inicio && x.FechaIngreso < hastaExclusivo)
            .Where(x => x.Estado == null
                || (!EF.Functions.ILike(x.Estado, "%cancelado%")
                    && !EF.Functions.ILike(x.Estado, "%rechazado%")))
            .Select(x => new IngresoDelInforme(x.FechaIngreso, CensoProgramas.Agudos, x.TipoIdentificacion, x.NumeroIdentificacion, x.NombrePaciente))
            .ToListAsync(cancellationToken));

        ingresos.AddRange(await _context.CensoCronicos.AsNoTracking()
            .Where(x => x.FechaIngreso >= inicio && x.FechaIngreso < hastaExclusivo)
            .Select(x => new IngresoDelInforme(x.FechaIngreso, CensoProgramas.Cronicos, x.TipoIdentificacion, x.NumeroIdentificacion, x.NombrePaciente))
            .ToListAsync(cancellationToken));

        ingresos.AddRange(await _context.CensoClinicaHeridas.AsNoTracking()
            .Where(x => x.FechaIngresoPrograma >= inicio && x.FechaIngresoPrograma < hastaExclusivo)
            .Select(x => new IngresoDelInforme(x.FechaIngresoPrograma, CensoProgramas.ClinicaHeridas, x.TipoIdentificacion, x.NumeroIdentificacion, x.NombrePaciente))
            .ToListAsync(cancellationToken));

        ingresos.AddRange(await _context.CensoNpt.AsNoTracking()
            .Where(x => x.FechaIngresoPrograma >= inicio && x.FechaIngresoPrograma < hastaExclusivo)
            .Select(x => new IngresoDelInforme(x.FechaIngresoPrograma, CensoProgramas.Npt, x.TipoIdentificacion, x.NumeroIdentificacion, x.NombrePaciente))
            .ToListAsync(cancellationToken));

        ingresos.AddRange(await _context.CensoTerapiasAmbulatorias.AsNoTracking()
            .Where(x => x.FechaIngreso >= inicio && x.FechaIngreso < hastaExclusivo)
            .Select(x => new IngresoDelInforme(x.FechaIngreso, CensoProgramas.TerapiaAmbulatoria, x.TipoIdentificacion, x.NumeroIdentificacion, x.NombrePaciente))
            .ToListAsync(cancellationToken));

        var porNombre = StringComparer.Create(CultureInfo.GetCultureInfo("es-CO"), ignoreCase: true);
        var filas = ingresos
            .OrderBy(x => x.Fecha.Date)
            .ThenBy(x => Array.IndexOf(CensoProgramas.Todos, x.Programa))
            .ThenBy(x => x.NombrePaciente?.Trim() ?? string.Empty, porNombre)
            .Select(x => (IReadOnlyList<string?>)
            [
                x.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                CensoProgramas.Nombre(x.Programa),
                x.TipoIdentificacion,
                x.NumeroIdentificacion,
                x.NombrePaciente
            ])
            .ToList();

        // La fecha sale como fecha de Excel (dd/mm/aaaa), no como texto, para que el filtro y el
        // orden de la hoja vayan por día. Los programas con el mismo nombre del informe de activos.
        string[] encabezados = ["Fecha de ingreso", "Programa", "Tipo de documento", "Documento", "Paciente"];
        var contenido = ExcelWorkbookWriter.BuildTableWorkbook(
            "Ingresos", encabezados, filas, DateTime.UtcNow,
            documentTitle: "Informe de ingresos",
            dateColumns: [0]);
        return File(
            contenido,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"censo_ingresos_{inicio:yyyyMMdd}_{fin:yyyyMMdd}.xlsx");
    }

    private sealed record IngresoDelInforme(
        DateTime Fecha,
        string Programa,
        string? TipoIdentificacion,
        string? NumeroIdentificacion,
        string? NombrePaciente);

    /// <summary>
    /// Columna que no existe en la tabla y se calcula al exportar.
    /// </summary>
    /// <param name="DespuesDe">
    /// Propiedad de la entidad —o título de otra columna calculada— después de la cual va esta.
    /// </param>
    /// <param name="Numerica">
    /// La columna lleva números: va a Excel como número, no como texto, para poder sumarla u ordenarla.
    /// </param>
    private sealed record ColumnaCalculada<T>(
        string DespuesDe,
        string Titulo,
        Func<T, string?> Valor,
        bool Numerica = false);

    /// <summary>
    /// Días entre dos fechas, contando solo el día: la misma cuenta del informe de pacientes activos.
    /// Sin fecha de inicio real (las cargas antiguas dejaron fechas centinela) la celda queda vacía,
    /// que es distinto de cero días.
    /// </summary>
    private static string? DiasDeEstancia(DateTime inicio, DateTime fin) =>
        inicio.Date < CensoVisibility.FechaEgresoMinima
            ? null
            : Math.Max(0, (fin.Date - inicio.Date).Days).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Arma un exportable con todas las propiedades de la entidad. Se usa en los programas que no
    /// tenían exportable propio, para que el archivo salga con la información completa.
    /// </summary>
    /// <param name="extras">
    /// Columnas calculadas, en el orden en que se insertan.
    /// </param>
    private FileContentResult ExportarPorReflexion<T>(
        IReadOnlyList<T> registros,
        string hoja,
        string archivo,
        params ColumnaCalculada<T>[] extras)
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

        // Las columnas de fecha de la entidad van a Excel como fecha, no como texto: ordenan y
        // filtran por día. La lista acompaña a los encabezados cuando se insertan las calculadas.
        var esFecha = propiedades
            .Select(x => x.PropertyType == typeof(DateTime) || x.PropertyType == typeof(DateTime?))
            .ToList();

        // Cada columna calculada se ubica después de una propiedad de la entidad o de otra columna ya
        // insertada, así que la referencia se busca sobre la lista que va creciendo.
        var claves = propiedades.Select(x => x.Name).ToList();
        var numericas = new List<string>();
        foreach (var columna in extras)
        {
            var posicion = claves.IndexOf(columna.DespuesDe) + 1;
            if (posicion <= 0)
            {
                posicion = headers.Count;
            }

            claves.Insert(posicion, columna.Titulo);
            headers.Insert(posicion, columna.Titulo);
            esFecha.Insert(posicion, false);
            for (var i = 0; i < registros.Count; i++)
            {
                filas[i].Insert(posicion, columna.Valor(registros[i]));
            }

            if (columna.Numerica)
            {
                numericas.Add(columna.Titulo);
            }
        }

        var libro = ExcelWorkbookWriter.BuildTableWorkbook(
            hoja, headers, filas.Select(x => (IReadOnlyList<string?>)x).ToList(), DateTime.UtcNow,
            dateColumns: esFecha.Index().Where(x => x.Item).Select(x => x.Index).ToList(),
            numberColumns: numericas.Select(titulo => headers.IndexOf(titulo)).ToList());
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
        // Las fechas salen en día/mes/año, con hora solo si el dato la trae (ver FechaExportable).
        DateTime fecha => FechaExportable.FechaOFechaHora(fecha),
        TimeSpan hora => hora.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
        bool booleano => booleano ? "Sí" : "No",
        _ => valor.ToString()
    };
}
