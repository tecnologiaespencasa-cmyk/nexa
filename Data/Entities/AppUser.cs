using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

public class AppUser
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string LastName1 { get; set; } = string.Empty;

    [StringLength(80)]
    public string? LastName2 { get; set; }

    [Required]
    [StringLength(20)]
    public string NationalId { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string NormalizedNationalId { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string NormalizedEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string NormalizedUsername { get; set; } = string.Empty;

    [Required]
    [StringLength(512)]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAtUtc { get; set; }

    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();

    public ICollection<AppUserPermission> UserPermissions { get; set; } = new List<AppUserPermission>();

    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
