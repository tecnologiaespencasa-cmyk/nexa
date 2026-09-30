using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

public class NursingAssistant
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string NormalizedName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Rol administrativo (NursingAssistantRoles). Solo identificativo; null en registros anteriores.</summary>
    [StringLength(30)]
    public string? Role { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
