using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

public class AppPermission
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(250)]
    public string Description { get; set; } = string.Empty;

    public ICollection<AppRolePermission> RolePermissions { get; set; } = new List<AppRolePermission>();

    public ICollection<AppUserPermission> UserPermissions { get; set; } = new List<AppUserPermission>();
}
