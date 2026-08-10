namespace Nexa.Helpers;

/// <summary>
/// Mapeos de "Mi espacio corporativo" entre valores de catálogo y su representación visual.
/// Los iconos son de Bootstrap Icons (wwwroot/lib/bootstrap-icons); estos métodos devuelven
/// el nombre de la clase para usarla como <c>&lt;i class="bi @EspacioIconos.ParaTipoActivo(x)"&gt;</c>.
/// Los patrones deben quedar en minúscula y con las mismas tildes que
/// <see cref="EspacioCorporativo.EspacioCorporativoCatalogos.TiposActivo"/>, porque solo se
/// aplica <c>ToLowerInvariant()</c> (una tilde no se pierde al pasar a minúsculas).
/// </summary>
public static class EspacioIconos
{
    public static string ParaTipoActivo(string? tipoActivo) => (tipoActivo ?? string.Empty).ToLowerInvariant() switch
    {
        "portátil" => "bi-laptop",
        "computador de escritorio" => "bi-pc-display",
        "monitor" => "bi-display",
        "video proyector" => "bi-projector-fill",
        "impresora" => "bi-printer-fill",
        "escáner" => "bi-upc-scan",
        "teléfono móvil" => "bi-phone-fill",
        "teléfono ip" => "bi-telephone-fill",
        "tablet" => "bi-tablet-fill",
        "diadema" => "bi-headset",
        "teclado" => "bi-keyboard-fill",
        "mouse" => "bi-mouse2-fill",
        "docking station" => "bi-usb-drive-fill",
        "servidor" => "bi-hdd-rack-fill",
        "router / switch" => "bi-router-fill",
        "ups" => "bi-battery-charging",
        "cámara" => "bi-camera-video-fill",
        "disco externo" => "bi-device-hdd-fill",
        _ => "bi-box-seam-fill"
    };

    public static string ParaTipoContenido(string? tipoContenido) => (tipoContenido ?? string.Empty).ToLowerInvariant() switch
    {
        "enlace" => "bi-link-45deg",
        "texto" => "bi-file-text-fill",
        _ => "bi-file-earmark-text-fill"
    };

    public static string ClaseEstadoActivo(string? estado) => (estado ?? string.Empty).ToLowerInvariant() switch
    {
        "activo" => "espacio-chip--activo",
        "disponible" => "espacio-chip--disponible",
        "en mantenimiento" => "espacio-chip--mantenimiento",
        "en reparación" => "espacio-chip--reparacion",
        "extraviado" => "espacio-chip--extraviado",
        "dado de baja" => "espacio-chip--baja",
        _ => "espacio-chip--neutro"
    };

    public static string ClaseEstadoNovedad(string? estado) => (estado ?? string.Empty).ToLowerInvariant() switch
    {
        "reportada" => "espacio-chip--reportada",
        "en proceso" => "espacio-chip--proceso",
        "resuelta" => "espacio-chip--resuelta",
        "rechazada" => "espacio-chip--rechazada",
        _ => "espacio-chip--neutro"
    };

    public static string ClasePrioridad(string? prioridad) => (prioridad ?? string.Empty).ToLowerInvariant() switch
    {
        "baja" => "espacio-chip--prioridad-baja",
        "media" => "espacio-chip--prioridad-media",
        "alta" => "espacio-chip--prioridad-alta",
        "crítica" => "espacio-chip--prioridad-critica",
        _ => "espacio-chip--neutro"
    };

    /// <summary>Icono que acompaña a cada paso del flujo de una novedad.</summary>
    public static string IconoEstadoNovedad(string? estado) => (estado ?? string.Empty).ToLowerInvariant() switch
    {
        "reportada" => "bi-megaphone-fill",
        "en proceso" => "bi-tools",
        "resuelta" => "bi-check-lg",
        "rechazada" => "bi-x-lg",
        _ => "bi-info-circle-fill"
    };
}
