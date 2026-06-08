namespace IntranetPrueba.Data.Entities;

public class AppRolePermission
{
    public int RoleId { get; set; }

    public int PermissionId { get; set; }

    public AppRole Role { get; set; } = null!;

    public AppPermission Permission { get; set; } = null!;
}
