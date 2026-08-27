using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

/// <summary>
/// Acta emitida a partir de una plantilla (entrega de accesos, etc.) y firmada por ambas partes.
///
/// Se guarda el cuerpo ya renderizado: si manana se ajusta el texto de la plantilla, las actas
/// firmadas siguen mostrando exactamente lo que se firmo.
/// </summary>
public class EspacioActaDocumental
{
    public long Id { get; set; }

    [Required]
    [StringLength(60)]
    public string PlantillaCodigo { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string PlantillaNombre { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string TituloActa { get; set; } = string.Empty;

    // ── Datos extraidos para busqueda y notificacion ─────────────────────────

    [Required]
    [StringLength(160)]
    public string NombreRecibe { get; set; } = string.Empty;

    [StringLength(30)]
    public string? DocumentoRecibe { get; set; }

    [StringLength(150)]
    public string? CorreoRecibe { get; set; }

    /// <summary>Usuario entregado, cuando la plantilla lo maneja. Sirve para filtrar.</summary>
    [StringLength(120)]
    public string? UsuarioRecibe { get; set; }

    // ── Contenido ────────────────────────────────────────────────────────────

    /// <summary>Valores capturados del formulario, en JSON.</summary>
    [Required]
    public string ValoresJson { get; set; } = string.Empty;

    /// <summary>Cuerpo HTML tal como se firmo.</summary>
    [Required]
    public string CuerpoHtml { get; set; } = string.Empty;

    // ── Firmas ───────────────────────────────────────────────────────────────

    public Guid? EmitidaPorUserId { get; set; }

    [Required]
    [StringLength(160)]
    public string EmitidaPorNombre { get; set; } = string.Empty;

    [StringLength(120)]
    public string? EmitidaPorCargo { get; set; }

    [StringLength(30)]
    public string? EmitidaPorDocumento { get; set; }

    [Required]
    public string FirmaEmiteDataUrl { get; set; } = string.Empty;

    [Required]
    public string FirmaRecibeDataUrl { get; set; } = string.Empty;

    /// <summary>
    /// Todas las firmas del acta en JSON (rótulo, nombre, documento, cargo y trazo).
    ///
    /// Las plantillas del diseñador pueden llevar más de dos firmas. Las dos columnas
    /// de arriba se siguen llenando con la primera firma de cada lado para no romper
    /// las actas anteriores ni las consultas existentes; cuando esta columna trae
    /// datos, es la que manda al mostrar el documento.
    /// </summary>
    public string? FirmasJson { get; set; }

    // ── Envio de la copia ────────────────────────────────────────────────────

    public bool CorreoEnviado { get; set; }

    public DateTime? CorreoEnviadoAtUtc { get; set; }

    [StringLength(500)]
    public string? CorreoError { get; set; }

    public DateTime FirmadaAtUtc { get; set; } = DateTime.UtcNow;
}
