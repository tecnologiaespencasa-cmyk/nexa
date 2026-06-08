namespace IntranetPrueba.Data.Entities;

public class AppUserPermission
{
    public Guid UserId { get; set; }

    public int PermissionId { get; set; }

    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;

    public AppUser User { get; set; } = null!;

    public AppPermission Permission { get; set; } = null!;
}
