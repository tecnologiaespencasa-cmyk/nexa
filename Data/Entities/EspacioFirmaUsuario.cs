using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

/// <summary>
/// Firma guardada de un usuario (tipicamente el responsable de tecnologia).
/// Se captura una sola vez y se reutiliza en cada acta que firme como quien entrega,
/// para no tener que volver a trazarla en cada equipo.
/// </summary>
public class EspacioFirmaUsuario
{
    [Key]
    public Guid UserId { get; set; }

    public AppUser User { get; set; } = null!;

    /// <summary>Imagen de la firma como data URL PNG.</summary>
    [Required]
    public string FirmaDataUrl { get; set; } = string.Empty;

    [StringLength(160)]
    public string? NombreFirmante { get; set; }

    /// <summary>Cargo que se imprime bajo la firma en el acta.</summary>
    [StringLength(120)]
    public string? Cargo { get; set; }

    public DateTime ActualizadaAtUtc { get; set; } = DateTime.UtcNow;
}
