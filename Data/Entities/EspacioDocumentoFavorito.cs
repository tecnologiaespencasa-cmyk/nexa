namespace Nexa.Data.Entities;

/// <summary>
/// Marca de favorito de un documento por usuario.
/// </summary>
public class EspacioDocumentoFavorito
{
    public long EspacioDocumentoId { get; set; }

    public EspacioDocumento EspacioDocumento { get; set; } = null!;

    public Guid UserId { get; set; }

    public AppUser User { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
