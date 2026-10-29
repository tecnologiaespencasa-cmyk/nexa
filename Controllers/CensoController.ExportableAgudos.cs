using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Helpers;

namespace Nexa.Controllers;

/// <summary>
/// Exportable del censo de agudos (2026-10-29): un libro .xlsx con cuatro hojas.
///
/// - Censo: una fila por atención, con las columnas de siempre y las que faltaban.
/// - Prórrogas: una fila por prórroga. La primera se guarda en la propia atención
///   (<see cref="CensoRecord.ProrrogaJson"/>) y las adicionales en <c>censo_prorrogas</c>; antes solo
///   salía la primera, y 149 atenciones tienen dos o más (una tiene 17).
/// - Kardex: una fila por medicamento programado, de la atención y de cada prórroga.
/// - Requisición: una fila por insumo pedido a farmacia, de la atención y de cada prórroga.
///
/// Varias hojas porque una sola no alcanza: el formato .xls anterior admitía 256 columnas y una sola
/// atención puede tener 17 prórrogas de 51 columnas cada una. Y .xlsx porque el .xls ya pesaba 45 MB
/// y con kardex y requisición pasaba de 100 MB; comprimido queda en una fracción.
/// </summary>
public partial class CensoController
{
    private const string TipoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Las dos firmas de farmacia son imágenes en base64 guardadas en cada registro: 312 de los 330
    /// MB de la tabla (2026-10-29). El exportable no las usa, y leerlas hacía que la consulta se
    /// demorara y, con la base cargada, se quedara sin tiempo.
    /// </summary>
    private static readonly string[] ColumnasQueNoLeeElExportable =
    [
        nameof(CensoRecord.FarmaciaFirmaEntregaDataUrl),
        nameof(CensoRecord.FarmaciaFirmaRecibeDataUrl)
    ];

    private static Expression<Func<CensoRecord, CensoRecord>>? _proyeccionExportableAgudos;

    /// <summary>
    /// <c>x => new CensoRecord { A = x.A, B = x.B, … }</c> con todas las columnas mapeadas menos las
    /// firmas. Se arma una vez a partir del modelo de EF, así que una columna nueva entra sola.
    /// </summary>
    private Expression<Func<CensoRecord, CensoRecord>> ProyeccionExportableAgudos()
    {
        if (_proyeccionExportableAgudos is { } lista)
        {
            return lista;
        }

        var parametro = Expression.Parameter(typeof(CensoRecord), "x");
        var asignaciones = _context.Model.FindEntityType(typeof(CensoRecord))!
            .GetProperties()
            .Where(x => x.PropertyInfo is { CanWrite: true } && !ColumnasQueNoLeeElExportable.Contains(x.Name))
            .Select(x => Expression.Bind(x.PropertyInfo!, Expression.Property(parametro, x.PropertyInfo!)))
            .ToList();
        var proyeccion = Expression.Lambda<Func<CensoRecord, CensoRecord>>(
            Expression.MemberInit(Expression.New(typeof(CensoRecord)), asignaciones),
            parametro);

        _proyeccionExportableAgudos = proyeccion;
        return proyeccion;
    }

    private async Task<FileContentResult> ArmarExportableAgudosAsync(
        IQueryable<CensoRecord> atencionesOrdenadas,
        string nombreArchivo,
        CancellationToken cancellationToken)
    {
        // Es la consulta más pesada de la intranet (todo el censo con sus kardex y requisiciones). Con
        // los 30 s por defecto, una base ocupada la cortaba y el usuario no veía nada; el contexto es
        // de esta petición, así que el tope más largo solo vale para el exportable.
        _context.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));

        var records = await atencionesOrdenadas
            .Select(ProyeccionExportableAgudos())
            .ToListAsync(cancellationToken);

        var idsConAdjuntos = await BuildIdsConAdjuntosSetAsync(records, cancellationToken);
        var ids = records.Select(x => x.Id).ToList();
        var prorrogasAdicionales = (await _context.CensoProrrogas
                .AsNoTracking()
                .Where(x => ids.Contains(x.CensoRecordId))
                .OrderBy(x => x.CensoRecordId)
                .ThenBy(x => x.Numero)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.CensoRecordId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CensoProrroga>)g.ToList());

        var (encabezados, filas) = BuildAgudosHojaCenso(records, idsConAdjuntos, prorrogasAdicionales);
        var libro = ExcelWorkbookWriter.BuildMultiSheetWorkbook(
            [
                new ExcelWorkbookWriter.TableSheet("Censo", encabezados, filas, DetectDates: true),
                BuildAgudosHojaProrrogas(records, prorrogasAdicionales),
                BuildAgudosHojaKardex(records, prorrogasAdicionales),
                BuildAgudosHojaRequisicion(records, prorrogasAdicionales)
            ],
            DateTime.UtcNow,
            "Censo de agudos");

        return File(libro, TipoXlsx, nombreArchivo);
    }

    // ==========================================================================================
    // Hoja Prórrogas
    // ==========================================================================================

    private static readonly string[] EncabezadosProrroga =
    [
        "Tipo de prórroga",
        "Medicamento principal", "Dosis", "Medida", "Vía", "Frecuencia", "Días",
        "Segundo medicamento", "Medicamento 2", "Dosis 2", "Medida 2", "Vía 2", "Frecuencia 2", "Días 2",
        "Tercer medicamento", "Medicamento 3", "Dosis 3", "Medida 3", "Vía 3", "Frecuencia 3", "Días 3",
        "Cuarto medicamento", "Medicamento 4", "Dosis 4", "Medida 4", "Vía 4", "Frecuencia 4", "Días 4",
        "Quinto medicamento", "Medicamento 5", "Dosis 5", "Medida 5", "Vía 5", "Frecuencia 5", "Días 5",
        "Sexto medicamento", "Medicamento 6", "Dosis 6", "Medida 6", "Vía 6", "Frecuencia 6", "Días 6",
        "Aplicaciones totales", "Días de tratamiento IV",
        "Fecha de inicio del tratamiento", "Fecha de fin del tratamiento", "Fecha promesa de inicio",
        "Hora promesa", "Auxiliar asignado", "Días de extensión", "Notificado al asegurador"
    ];

    private static ExcelWorkbookWriter.TableSheet BuildAgudosHojaProrrogas(
        IReadOnlyList<CensoRecord> records,
        IReadOnlyDictionary<long, IReadOnlyList<CensoProrroga>> prorrogasAdicionales)
    {
        List<string> encabezados =
        [
            .. EncabezadosAtencion,
            "Prórroga #", "Dónde se guarda", "Creada (UTC)", "Actualizada (UTC)", "Kardex de la prórroga cerrado (UTC)",
            .. EncabezadosProrroga,
            "Seguimiento médico"
        ];

        var filas = new List<IReadOnlyList<string?>>();
        foreach (var record in records)
        {
            if (!string.IsNullOrWhiteSpace(record.ProrrogaJson))
            {
                var prorroga = LeerProrroga(record.ProrrogaJson);
                filas.Add(
                [
                    .. ValoresAtencion(record),
                    "1", "En la atención", string.Empty, string.Empty, FechaExportable.FechaHora(record.ProrrogaCerradaAtUtc),
                    .. ValoresProrroga(prorroga),
                    SeguimientoMedicoTexto(prorroga?.SeguimientoMedico)
                ]);
            }

            foreach (var adicional in prorrogasAdicionales.GetValueOrDefault(record.Id) ?? [])
            {
                var prorroga = LeerProrroga(adicional.ProrrogaJson);
                filas.Add(
                [
                    .. ValoresAtencion(record),
                    adicional.Numero.ToString(CultureInfo.InvariantCulture),
                    "Prórroga adicional",
                    FechaExportable.FechaHora(adicional.CreatedAtUtc),
                    FechaExportable.FechaHora(adicional.UpdatedAtUtc),
                    FechaExportable.FechaHora(adicional.CerradaAtUtc),
                    .. ValoresProrroga(prorroga),
                    SeguimientoMedicoTexto(prorroga?.SeguimientoMedico)
                ]);
            }
        }

        return new ExcelWorkbookWriter.TableSheet(
            "Prórrogas", encabezados, filas,
            NumberColumns: [EncabezadosAtencion.Length],
            DetectDates: true);
    }

    private static string SeguimientoMedicoTexto(string? valor) => valor?.Trim().ToLowerInvariant() switch
    {
        null or "" => string.Empty,
        "si" or "sí" => "Sí",
        "no" => "No",
        "no-aplica" or "no aplica" => "No aplica",
        _ => valor.Trim()
    };

    // ==========================================================================================
    // Hojas Kardex y Requisición
    // ==========================================================================================

    /// <summary>
    /// Kardex y requisición existen por documento: el de la atención, el de la prórroga guardada en
    /// la atención y uno por cada prórroga adicional.
    /// </summary>
    private sealed record DocumentoFarmacia(string Nombre, string? KardexJson, string? RequisicionJson, DateTime? CerradoAtUtc);

    private static IEnumerable<DocumentoFarmacia> DocumentosFarmacia(
        CensoRecord record,
        IReadOnlyDictionary<long, IReadOnlyList<CensoProrroga>> prorrogasAdicionales)
    {
        yield return new DocumentoFarmacia("Atención", record.KardexEdicionJson, record.RequisicionFarmaciaJson, record.KardexCerradoAtUtc);

        if (!string.IsNullOrWhiteSpace(record.ProrrogaJson))
        {
            yield return new DocumentoFarmacia("Prórroga #1", record.ProrrogaKardexEdicionJson, record.ProrrogaRequisicionFarmaciaJson, record.ProrrogaCerradaAtUtc);
        }

        foreach (var adicional in prorrogasAdicionales.GetValueOrDefault(record.Id) ?? [])
        {
            yield return new DocumentoFarmacia($"Prórroga #{adicional.Numero}", adicional.KardexEdicionJson, adicional.RequisicionFarmaciaJson, adicional.CerradaAtUtc);
        }
    }

    private static ExcelWorkbookWriter.TableSheet BuildAgudosHojaKardex(
        IReadOnlyList<CensoRecord> records,
        IReadOnlyDictionary<long, IReadOnlyList<CensoProrroga>> prorrogasAdicionales)
    {
        List<string> encabezados =
        [
            .. EncabezadosAtencion,
            "Documento", "Fila", "Medicamento", "Dosis y frecuencia", "Fecha de inicio", "Fecha de fin",
            "Vehículo de reconstitución", "Volumen de dilución", "Tiempo de infusión", "Estabilidad",
            "Cadena de frío", "Fotosensible", "Bomba de infusión", "Aislamiento",
            "Programador", "Médico tratante", "Auxiliar asignado", "Peso", "Cambio de equipo",
            "Observaciones del kardex", "Kardex cerrado (UTC)"
        ];

        var filas = new List<IReadOnlyList<string?>>();
        foreach (var record in records)
        {
            foreach (var documento in DocumentosFarmacia(record, prorrogasAdicionales))
            {
                if (!TryLeerDocumentoJson(documento.KardexJson, "medRows", out var campos, out var medicamentos))
                {
                    continue;
                }

                foreach (var fila in medicamentos)
                {
                    var (etiqueta, medicamento) = SepararPresentacion(Texto(fila, "presentacion"));
                    var dosis = Texto(fila, "dosisFrecuencia");
                    var inicio = Texto(fila, "fechaInicio");
                    var finTratamiento = Texto(fila, "fechaFin");

                    // El formato trae seis renglones fijos ("Medicamento 1" … "Medicamento 6"); los
                    // que nadie diligenció no son medicamentos y no se listan.
                    if (string.IsNullOrWhiteSpace(medicamento) && string.IsNullOrWhiteSpace(dosis)
                        && string.IsNullOrWhiteSpace(inicio) && string.IsNullOrWhiteSpace(finTratamiento))
                    {
                        continue;
                    }

                    filas.Add(
                    [
                        .. ValoresAtencion(record),
                        documento.Nombre,
                        string.IsNullOrWhiteSpace(Texto(fila, "row")) ? etiqueta : Texto(fila, "row"),
                        medicamento,
                        dosis,
                        inicio,
                        finTratamiento,
                        Texto(fila, "vehiculoReconstitucion"),
                        Texto(fila, "volumenDilucion"),
                        Texto(fila, "tiempoInfusion"),
                        Texto(fila, "estabilidad"),
                        Texto(fila, "cadenaFrio"),
                        Texto(fila, "fotosensible"),
                        Texto(fila, "bombaInfusion"),
                        Texto(fila, "aislamiento"),
                        Texto(campos, "programador"),
                        Texto(campos, "medicoTratante"),
                        Texto(campos, "auxiliarAsignado"),
                        Texto(campos, "peso"),
                        Texto(campos, "cambioEquipo"),
                        Texto(campos, "observaciones"),
                        FechaExportable.FechaHora(documento.CerradoAtUtc)
                    ]);
                }
            }
        }

        return new ExcelWorkbookWriter.TableSheet("Kardex", encabezados, filas, DetectDates: true);
    }

    private static ExcelWorkbookWriter.TableSheet BuildAgudosHojaRequisicion(
        IReadOnlyList<CensoRecord> records,
        IReadOnlyDictionary<long, IReadOnlyList<CensoProrroga>> prorrogasAdicionales)
    {
        List<string> encabezados =
        [
            .. EncabezadosAtencion,
            "Documento", "Ítem", "Descripción", "Detalle", "Cantidad", "Fecha de inicio", "Fecha de fin",
            "Fecha de solicitud", "Autorización", "Auxiliar encargado", "Realizado por"
        ];

        var filas = new List<IReadOnlyList<string?>>();
        foreach (var record in records)
        {
            foreach (var documento in DocumentosFarmacia(record, prorrogasAdicionales))
            {
                if (!TryLeerDocumentoJson(documento.RequisicionJson, "rows", out var campos, out var renglones))
                {
                    continue;
                }

                foreach (var fila in renglones)
                {
                    var descripcion = Texto(fila, "descripcion");
                    var cantidad = Texto(fila, "cantidad");
                    var detalle = Texto(fila, "detalle");
                    if (string.IsNullOrWhiteSpace(descripcion) && string.IsNullOrWhiteSpace(cantidad) && string.IsNullOrWhiteSpace(detalle))
                    {
                        continue;
                    }

                    filas.Add(
                    [
                        .. ValoresAtencion(record),
                        documento.Nombre,
                        Texto(fila, "item"),
                        descripcion,
                        detalle,
                        cantidad,
                        Texto(fila, "fechaInicio"),
                        Texto(fila, "fechaFin"),
                        Texto(campos, "fechaSolicitud"),
                        Texto(campos, "autorizacion"),
                        Texto(campos, "auxiliarEncargado"),
                        Texto(campos, "realizadoPor")
                    ]);
                }
            }
        }

        return new ExcelWorkbookWriter.TableSheet(
            "Requisición", encabezados, filas,
            NumberColumns: [EncabezadosAtencion.Length + 4],
            DetectDates: true);
    }

    // ==========================================================================================
    // Piezas comunes
    // ==========================================================================================

    /// <summary>Las primeras columnas de las hojas de detalle: con qué atención va cada fila.</summary>
    private static readonly string[] EncabezadosAtencion =
        ["Id atención", "Tipo de identificación", "Número de identificación", "Paciente", "Fecha de ingreso"];

    private static string?[] ValoresAtencion(CensoRecord record) =>
    [
        record.Id.ToString(CultureInfo.InvariantCulture),
        record.TipoIdentificacion,
        record.NumeroIdentificacion,
        record.NombrePaciente,
        FormatDate(record.FechaIngreso)
    ];

    /// <summary>
    /// Kardex y requisición se guardan como <c>{ "fields": { … }, "&lt;filas&gt;": [ { … } ] }</c>.
    /// Un JSON que no tenga esa forma no se exporta (no hay ninguno así al 2026-10-29).
    /// </summary>
    private static bool TryLeerDocumentoJson(
        string? json,
        string propiedadFilas,
        out JsonElement campos,
        out List<JsonElement> filas)
    {
        campos = default;
        filas = [];
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var documento = JsonDocument.Parse(json);
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            campos = raiz.TryGetProperty("fields", out var f) && f.ValueKind == JsonValueKind.Object ? f.Clone() : default;
            if (raiz.TryGetProperty(propiedadFilas, out var arreglo) && arreglo.ValueKind == JsonValueKind.Array)
            {
                filas = arreglo.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).Select(x => x.Clone()).ToList();
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Texto(JsonElement objeto, string propiedad)
    {
        if (objeto.ValueKind != JsonValueKind.Object || !objeto.TryGetProperty(propiedad, out var valor))
        {
            return string.Empty;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Number => valor.GetRawText(),
            JsonValueKind.True => "Sí",
            JsonValueKind.False => "No",
            _ => string.Empty
        };
    }

    private static readonly Regex EtiquetaMedicamento = new(@"^Medicamento\s*\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// La presentación del kardex trae el rótulo del renglón y el medicamento en dos líneas
    /// ("Medicamento 1\nAMPICILINA /SULBACTAM 1.5 g AMPOLLA"): se separan.
    /// </summary>
    private static (string Etiqueta, string Medicamento) SepararPresentacion(string presentacion)
    {
        var lineas = presentacion.Split('\n', 2, StringSplitOptions.TrimEntries);
        return lineas.Length > 0 && EtiquetaMedicamento.IsMatch(lineas[0])
            ? (lineas[0], lineas.Length > 1 ? lineas[1].Replace('\n', ' ').Trim() : string.Empty)
            : (string.Empty, presentacion.Replace('\n', ' ').Trim());
    }
}
