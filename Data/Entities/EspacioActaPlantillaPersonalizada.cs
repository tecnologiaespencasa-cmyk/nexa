using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

/// <summary>
/// Plantilla de acta creada por un administrador desde el diseñador.
///
/// La definición (campos, bloques del pliego y firmas) se guarda en JSON: es una
/// estructura que solo entiende el módulo de actas y que crece con los tipos de
/// campo nuevos sin pedir una migración por cada uno.
///
/// El borrado es lógico porque las actas ya emitidas referencian el código de la
/// plantilla y deben seguir siendo rastreables.
/// </summary>
public class EspacioActaPlantillaPersonalizada
{
    public long Id { get; set; }

    /// <summary>Código único; se genera a partir del nombre al crearla.</summary>
    [Required]
    [StringLength(60)]
    public string Codigo { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [StringLength(400)]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    [StringLength(60)]
    public string Icono { get; set; } = "bi-file-earmark-text-fill";

    [Required]
    [StringLength(200)]
    public string TituloActa { get; set; } = string.Empty;

    // ── Definición ───────────────────────────────────────────────────────────

    /// <summary>Campos que se diligencian en cada acta.</summary>
    [Required]
    public string CamposJson { get; set; } = "[]";

    /// <summary>Bloques del pliego (títulos, párrafos, listas, notas).</summary>
    [Required]
    public string BloquesJson { get; set; } = "[]";

    /// <summary>Firmas al pie del documento.</summary>
    [Required]
    public string FirmasJson { get; set; } = "[]";

    public bool NumerarTitulos { get; set; } = true;

    // ── Enlaces a columnas propias del acta emitida ──────────────────────────

    /// <summary>Campo que identifica a la persona del acta. Alimenta la búsqueda.</summary>
    [Required]
    [StringLength(60)]
    public string CampoNombre { get; set; } = string.Empty;

    [StringLength(60)]
    public string? CampoDocumento { get; set; }

    /// <summary>Campo con el correo al que se envía la copia firmada.</summary>
    [StringLength(60)]
    public string? CampoCorreo { get; set; }

    [StringLength(60)]
    public string? CampoUsuario { get; set; }

    // ── Estado y trazabilidad ────────────────────────────────────────────────

    /// <summary>Una plantilla inactiva ya no se ofrece para emitir actas nuevas.</summary>
    public bool Activa { get; set; } = true;

    public bool Eliminada { get; set; }

    public Guid? CreadaPorUserId { get; set; }

    [Required]
    [StringLength(160)]
    public string CreadaPorNombre { get; set; } = string.Empty;

    public DateTime CreadaAtUtc { get; set; } = DateTime.UtcNow;

    [StringLength(160)]
    public string? ActualizadaPorNombre { get; set; }

    public DateTime? ActualizadaAtUtc { get; set; }

    /// <summary>Sube en cada edición. Sirve para explicar diferencias entre actas emitidas.</summary>
    public int Version { get; set; } = 1;
}
