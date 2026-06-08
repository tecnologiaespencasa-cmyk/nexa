namespace IntranetPrueba.Data.Entities;

public class AppUserRole
{
    public Guid UserId { get; set; }

    public int RoleId { get; set; }

    public AppUser User { get; set; } = null!;

    public AppRole Role { get; set; } = null!;
}
