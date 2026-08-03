using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

/// <summary>
/// Documento institucional publicado en "Mi espacio corporativo".
/// El contenido puede ser un archivo cargado, un enlace externo o texto redactado en la intranet.
/// </summary>
public class EspacioDocumento
{
    public long Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Descripcion { get; set; }

    [Required]
    [StringLength(60)]
    public string Categoria { get; set; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string TipoDocumento { get; set; } = string.Empty;

    /// <summary>Archivo | Enlace | Texto</summary>
    [Required]
    [StringLength(20)]
    public string TipoContenido { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Version { get; set; }

    [StringLength(60)]
    public string? CodigoDocumento { get; set; }

    [StringLength(260)]
    public string? ArchivoNombre { get; set; }

    [StringLength(150)]
    public string? ArchivoContentType { get; set; }

    public long? ArchivoTamanoBytes { get; set; }

    public byte[]? ArchivoContenido { get; set; }

    [StringLength(1000)]
    public string? EnlaceUrl { get; set; }

    public string? ContenidoTexto { get; set; }

    [StringLength(300)]
    public string? Etiquetas { get; set; }

    public bool Publicado { get; set; } = true;

    public bool Destacado { get; set; }

    public DateOnly? FechaVigencia { get; set; }

    public int Descargas { get; set; }

    public bool Eliminado { get; set; }

    public Guid? CreadoPorUserId { get; set; }

    [StringLength(160)]
    public string? CreadoPorNombre { get; set; }

    [StringLength(160)]
    public string? ActualizadoPorNombre { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<EspacioDocumentoFavorito> Favoritos { get; set; } = new List<EspacioDocumentoFavorito>();
}
