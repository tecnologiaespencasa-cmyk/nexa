namespace Nexa.Models.EspacioCorporativo;

/// <summary>
/// Catálogos fijos del módulo "Mi espacio corporativo".
/// </summary>
public static class EspacioCorporativoCatalogos
{
    public const string EstadoActivoDisponible = "Disponible";
    public const string EstadoActivoAsignado = "Activo";
    public const string EstadoActivoMantenimiento = "En mantenimiento";
    public const string EstadoActivoDadoBaja = "Dado de baja";

    public const string EstadoNovedadReportada = "Reportada";
    public const string EstadoNovedadEnProceso = "En proceso";
    public const string EstadoNovedadResuelta = "Resuelta";
    public const string EstadoNovedadRechazada = "Rechazada";

    public const string TipoContenidoArchivo = "Archivo";
    public const string TipoContenidoEnlace = "Enlace";
    public const string TipoContenidoTexto = "Texto";

    public const string ClasificacionSinClasificar = "Sin clasificar";

    public const string ActaEntrega = "Entrega";
    public const string ActaDevolucion = "Devolución";

    /// <summary>Tamaño máximo de una firma almacenada como data URL PNG (~1 MB).</summary>
    public const int LongitudMaximaFirma = 1_000_000;

    /// <summary>
    /// Valida que el valor sea una firma PNG en data URL y no contenido arbitrario.
    /// </summary>
    public static bool EsFirmaValida(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            return false;
        }

        var normalizado = dataUrl.Trim();
        return normalizado.Length <= LongitudMaximaFirma
            && normalizado.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase);
    }

    public static bool EsTipoActaValido(string? tipo) =>
        string.Equals(tipo, ActaEntrega, StringComparison.OrdinalIgnoreCase)
        || string.Equals(tipo, ActaDevolucion, StringComparison.OrdinalIgnoreCase);

    public static readonly string[] TiposActivo =
    [
        "Portátil",
        "Computador de escritorio",
        "Monitor",
        "Impresora",
        "Escáner",
        "Teléfono móvil",
        "Teléfono IP",
        "Tablet",
        "Diadema",
        "Teclado",
        "Mouse",
        "Docking station",
        "Servidor",
        "Router / Switch",
        "UPS",
        "Cámara",
        "Video proyector",
        "Disco externo",
        "Otro"
    ];

    public static readonly string[] EstadosActivo =
    [
        EstadoActivoAsignado,
        EstadoActivoDisponible,
        EstadoActivoMantenimiento,
        "En reparación",
        "Extraviado",
        EstadoActivoDadoBaja
    ];

    public static readonly string[] TiposNovedad =
    [
        "Daño del equipo",
        "Pérdida o extravío",
        "Robo",
        "Solicitud de cambio",
        "Mantenimiento",
        "Falla de software",
        "Falta de accesorios",
        "Traslado de equipo",
        "Otro"
    ];

    /// <summary>
    /// Flujo de una novedad. Entra en "Reportada" y avanza por transiciones explícitas;
    /// no se elige el estado de forma libre.
    /// </summary>
    public static readonly string[] EstadosNovedad =
    [
        EstadoNovedadReportada,
        EstadoNovedadEnProceso,
        EstadoNovedadResuelta,
        EstadoNovedadRechazada
    ];

    /// <summary>Pasos que se dibujan en la linea de tiempo del flujo.</summary>
    public static readonly string[] PasosFlujoNovedad =
    [
        EstadoNovedadReportada,
        EstadoNovedadEnProceso,
        EstadoNovedadResuelta
    ];

    /// <summary>
    /// Transiciones válidas desde cada estado, en el orden en que se muestran los botones.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> Transiciones =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [EstadoNovedadReportada] = [EstadoNovedadEnProceso, EstadoNovedadRechazada],
            [EstadoNovedadEnProceso] = [EstadoNovedadResuelta, EstadoNovedadRechazada],
            [EstadoNovedadResuelta] = [EstadoNovedadEnProceso],
            [EstadoNovedadRechazada] = [EstadoNovedadEnProceso]
        };

    public static IReadOnlyList<string> TransicionesDesde(string? estadoActual) =>
        !string.IsNullOrWhiteSpace(estadoActual) && Transiciones.TryGetValue(estadoActual, out var destinos)
            ? destinos
            : [];

    public static bool EsTransicionValida(string? estadoActual, string? destino) =>
        !string.IsNullOrWhiteSpace(destino)
        && TransicionesDesde(estadoActual).Contains(destino, StringComparer.OrdinalIgnoreCase);

    /// <summary>Texto del botón que ejecuta cada transición.</summary>
    public static string EtiquetaTransicion(string estadoActual, string destino)
    {
        if (string.Equals(destino, EstadoNovedadEnProceso, StringComparison.OrdinalIgnoreCase))
        {
            return EsEstadoNovedadCerrado(estadoActual) ? "Reabrir novedad" : "Pasar a en proceso";
        }

        return string.Equals(destino, EstadoNovedadResuelta, StringComparison.OrdinalIgnoreCase)
            ? "Marcar como solucionada"
            : "Rechazar novedad";
    }

    /// <summary>Clase de Bootstrap Icons del botón de cada transición.</summary>
    public static string IconoTransicion(string estadoActual, string destino)
    {
        if (string.Equals(destino, EstadoNovedadEnProceso, StringComparison.OrdinalIgnoreCase))
        {
            return EsEstadoNovedadCerrado(estadoActual) ? "bi-arrow-counterclockwise" : "bi-play-fill";
        }

        return string.Equals(destino, EstadoNovedadResuelta, StringComparison.OrdinalIgnoreCase)
            ? "bi-check-circle-fill"
            : "bi-x-circle-fill";
    }

    /// <summary>Clase visual del botón de cada transición.</summary>
    public static string ClaseTransicion(string destino) =>
        string.Equals(destino, EstadoNovedadRechazada, StringComparison.OrdinalIgnoreCase)
            ? "espacio-btn--danger"
            : "espacio-btn--primary";

    public static readonly string[] PrioridadesNovedad =
    [
        "Baja",
        "Media",
        "Alta",
        "Crítica"
    ];

    public static readonly string[] ClasificacionesNovedad =
    [
        ClasificacionSinClasificar,
        "Hardware",
        "Software",
        "Red y conectividad",
        "Accesorios",
        "Garantía",
        "Reposición",
        "Uso indebido"
    ];

    public static readonly string[] CategoriasDocumento =
    [
        "RRHH",
        "TI",
        "Financiera",
        "Administrativa",
        "Calidad",
        "SST",
        "Gerencia",
        "Jurídica",
        "Operaciones",
        "Asistencial"
    ];

    public static readonly string[] TiposDocumento =
    [
        "Política",
        "Manual",
        "Formato",
        "Procedimiento",
        "Instructivo",
        "Guía",
        "Reglamento",
        "Acta",
        "Otro"
    ];

    public static readonly string[] TiposContenido =
    [
        TipoContenidoArchivo,
        TipoContenidoEnlace,
        TipoContenidoTexto
    ];

    /// <summary>Extensiones aceptadas al cargar un documento.</summary>
    public static readonly IReadOnlyDictionary<string, string> ExtensionesPermitidas =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [".txt"] = "text/plain",
            [".csv"] = "text/csv",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".zip"] = "application/zip"
        };

    /// <summary>Tamaño máximo permitido para un archivo cargado (25 MB).</summary>
    public const long TamanoMaximoArchivoBytes = 25L * 1024 * 1024;

    public static bool EsEstadoActivoValido(string? estado) =>
        !string.IsNullOrWhiteSpace(estado) && EstadosActivo.Contains(estado, StringComparer.OrdinalIgnoreCase);

    public static bool EsTipoActivoValido(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) && TiposActivo.Contains(tipo, StringComparer.OrdinalIgnoreCase);

    public static bool EsTipoNovedadValido(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) && TiposNovedad.Contains(tipo, StringComparer.OrdinalIgnoreCase);

    public static bool EsEstadoNovedadValido(string? estado) =>
        !string.IsNullOrWhiteSpace(estado) && EstadosNovedad.Contains(estado, StringComparer.OrdinalIgnoreCase);

    public static bool EsPrioridadValida(string? prioridad) =>
        !string.IsNullOrWhiteSpace(prioridad) && PrioridadesNovedad.Contains(prioridad, StringComparer.OrdinalIgnoreCase);

    public static bool EsClasificacionValida(string? clasificacion) =>
        !string.IsNullOrWhiteSpace(clasificacion) && ClasificacionesNovedad.Contains(clasificacion, StringComparer.OrdinalIgnoreCase);

    public static bool EsCategoriaDocumentoValida(string? categoria) =>
        !string.IsNullOrWhiteSpace(categoria) && CategoriasDocumento.Contains(categoria, StringComparer.OrdinalIgnoreCase);

    public static bool EsTipoDocumentoValido(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) && TiposDocumento.Contains(tipo, StringComparer.OrdinalIgnoreCase);

    public static bool EsTipoContenidoValido(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) && TiposContenido.Contains(tipo, StringComparer.OrdinalIgnoreCase);

    /// <summary>Estados que se consideran cierre de una novedad.</summary>
    public static bool EsEstadoNovedadCerrado(string? estado) =>
        string.Equals(estado, EstadoNovedadResuelta, StringComparison.OrdinalIgnoreCase)
        || string.Equals(estado, EstadoNovedadRechazada, StringComparison.OrdinalIgnoreCase);
}
