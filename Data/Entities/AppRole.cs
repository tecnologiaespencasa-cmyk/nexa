using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

public class AppRole
{
    public int Id { get; set; }

    [Required]
    [StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string NormalizedName { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();

    public ICollection<AppRolePermission> RolePermissions { get; set; } = new List<AppRolePermission>();
}
