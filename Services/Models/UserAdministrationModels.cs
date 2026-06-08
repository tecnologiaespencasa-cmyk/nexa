namespace IntranetPrueba.Services.Models;

public class UserSummaryDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class NursingAssistantDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class OpsAssistantDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName1 { get; set; } = string.Empty;
    public string LastName2 { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string Profession { get; set; } = string.Empty;
}

public class UserEditDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName1 { get; set; } = string.Empty;
    public string? LastName2 { get; set; }
    public string NationalId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class PermissionOptionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class UserPermissionAssignmentDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public IReadOnlyList<PermissionOptionDto> AvailablePermissions { get; set; } = [];
    public IReadOnlyList<PermissionOptionDto> GrantedPermissions { get; set; } = [];
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName1 { get; set; } = string.Empty;
    public string? LastName2 { get; set; }
    public string NationalId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName1 { get; set; } = string.Empty;
    public string? LastName2 { get; set; }
    public string NationalId { get; set; } = string.Empty;
}

public class ResetUserPasswordRequest
{
    public Guid UserId { get; set; }
    public string NewPassword { get; set; } = string.Empty;
}

public class CreateNursingAssistantRequest
{
    public string Name { get; set; } = string.Empty;
}

public class CreateOpsAssistantRequest
{
    public string Name { get; set; } = string.Empty;
}
