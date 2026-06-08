using IntranetPrueba.Data.Entities;
using IntranetPrueba.Data.Repositories.Interfaces;
using IntranetPrueba.Models.Security;
using IntranetPrueba.Services.Interfaces;
using IntranetPrueba.Services.Models;

namespace IntranetPrueba.Services;

public class UserAdministrationService : IUserAdministrationService
{
    private readonly IUserAdministrationRepository _repository;
    private readonly INeonOpsAssistantUserRepository _neonOpsAssistantUserRepository;
    private readonly IPasswordService _passwordService;
    private readonly IAuditService _auditService;

    public UserAdministrationService(
        IUserAdministrationRepository repository,
        INeonOpsAssistantUserRepository neonOpsAssistantUserRepository,
        IPasswordService passwordService,
        IAuditService auditService)
    {
        _repository = repository;
        _neonOpsAssistantUserRepository = neonOpsAssistantUserRepository;
        _passwordService = passwordService;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _repository.GetAllUsersAsync(cancellationToken);
        return users
            .Select(u => new UserSummaryDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                NationalId = u.NationalId,
                IsActive = u.IsActive
            })
            .ToList();
    }

    public async Task<IReadOnlyList<NursingAssistantDto>> GetNursingAssistantsAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default)
    {
        var nursingAssistants = await _repository.GetNursingAssistantsAsync(onlyActive, cancellationToken);
        return nursingAssistants
            .Select(nursingAssistant => new NursingAssistantDto
            {
                Id = nursingAssistant.Id,
                Name = nursingAssistant.Name,
                IsActive = nursingAssistant.IsActive
            })
            .ToList();
    }

    public async Task<IReadOnlyList<OpsAssistantDto>> GetOpsAssistantsAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default)
    {
        var opsAssistants = await _neonOpsAssistantUserRepository.GetUsersAsync(onlyActive, cancellationToken);
        return opsAssistants
            .Select(opsAssistant => new OpsAssistantDto
            {
                Name = opsAssistant.FullName,
                IsActive = opsAssistant.IsActive,
                Email = opsAssistant.Email,
                FirstName = opsAssistant.FirstName,
                LastName1 = opsAssistant.LastName1,
                LastName2 = opsAssistant.LastName2,
                Phone = opsAssistant.Phone,
                NationalId = opsAssistant.NationalId,
                Profession = opsAssistant.Profession
            })
            .ToList();
    }

    public async Task<ServiceResult<UserEditDto>> GetUserForEditAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<UserEditDto>.Failure("El usuario no existe.");
        }

        return ServiceResult<UserEditDto>.Success(new UserEditDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName1 = user.LastName1,
            LastName2 = user.LastName2,
            NationalId = user.NationalId,
            IsActive = user.IsActive
        });
    }

    public async Task<ServiceResult<UserPermissionAssignmentDto>> GetUserPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserByIdWithPermissionsAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<UserPermissionAssignmentDto>.Failure("El usuario no existe.");
        }

        var allPermissions = await _repository.GetScreenPermissionsAsync(cancellationToken);
        var grantedPermissionIds = user.UserPermissions.Select(up => up.PermissionId).ToHashSet();

        var available = allPermissions
            .Where(p => !grantedPermissionIds.Contains(p.Id))
            .Select(MapPermission)
            .ToList();

        var granted = allPermissions
            .Where(p => grantedPermissionIds.Contains(p.Id))
            .Select(MapPermission)
            .ToList();

        return ServiceResult<UserPermissionAssignmentDto>.Success(new UserPermissionAssignmentDto
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            AvailablePermissions = available,
            GrantedPermissions = granted
        });
    }

    public async Task<ServiceResult> CreateUserAsync(
        CreateUserRequest request,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var normalizedData = NormalizeIdentityData(request.Username, request.Email, request.NationalId);
        var duplicateValidation = await ValidateDuplicatesAsync(
            normalizedData.NormalizedUsername,
            normalizedData.NormalizedEmail,
            normalizedData.NormalizedNationalId,
            null,
            cancellationToken);

        if (!duplicateValidation.Succeeded)
        {
            return duplicateValidation;
        }

        var user = new AppUser
        {
            Username = normalizedData.Username,
            NormalizedUsername = normalizedData.NormalizedUsername,
            Email = normalizedData.Email,
            NormalizedEmail = normalizedData.NormalizedEmail,
            NationalId = normalizedData.NationalId,
            NormalizedNationalId = normalizedData.NormalizedNationalId,
            FirstName = request.FirstName.Trim(),
            LastName1 = request.LastName1.Trim(),
            LastName2 = NormalizeOptionalText(request.LastName2),
            FullName = BuildFullName(request.FirstName, request.LastName1, request.LastName2),
            PasswordHash = _passwordService.HashPassword(request.Password),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _repository.AddUserAsync(user, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "USER_CREATED",
            entity: "User",
            details: $"Usuario creado: {user.Username} ({user.Email}).",
            performedByUserId: performedByUserId,
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> UpdateUserAsync(
        UpdateUserRequest request,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Failure("El usuario no existe.");
        }

        var normalizedData = NormalizeIdentityData(request.Username, request.Email, request.NationalId);
        var duplicateValidation = await ValidateDuplicatesAsync(
            normalizedData.NormalizedUsername,
            normalizedData.NormalizedEmail,
            normalizedData.NormalizedNationalId,
            request.UserId,
            cancellationToken);

        if (!duplicateValidation.Succeeded)
        {
            return duplicateValidation;
        }

        user.Username = normalizedData.Username;
        user.NormalizedUsername = normalizedData.NormalizedUsername;
        user.Email = normalizedData.Email;
        user.NormalizedEmail = normalizedData.NormalizedEmail;
        user.NationalId = normalizedData.NationalId;
        user.NormalizedNationalId = normalizedData.NormalizedNationalId;
        user.FirstName = request.FirstName.Trim();
        user.LastName1 = request.LastName1.Trim();
        user.LastName2 = NormalizeOptionalText(request.LastName2);
        user.FullName = BuildFullName(request.FirstName, request.LastName1, request.LastName2);

        await _repository.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "USER_UPDATED",
            entity: "User",
            details: $"Usuario actualizado: {user.Username}.",
            performedByUserId: performedByUserId,
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> SetUserActiveStatusAsync(
        Guid userId,
        bool isActive,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (userId == performedByUserId && !isActive)
        {
            return ServiceResult.Failure("No puedes desactivar tu propio usuario.");
        }

        var user = await _repository.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Failure("El usuario no existe.");
        }

        if (user.IsActive == isActive)
        {
            return ServiceResult.Success();
        }

        user.IsActive = isActive;
        await _repository.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: isActive ? "USER_ACTIVATED" : "USER_DEACTIVATED",
            entity: "User",
            details: $"Estado de usuario cambiado: {user.Username} => {(isActive ? "Activo" : "Inactivo")}.",
            performedByUserId: performedByUserId,
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ResetPasswordAsync(
        ResetUserPasswordRequest request,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Failure("El usuario no existe.");
        }

        user.PasswordHash = _passwordService.HashPassword(request.NewPassword);
        await _repository.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "USER_PASSWORD_RESET",
            entity: "User",
            details: $"Contrasena restablecida para: {user.Username}.",
            performedByUserId: performedByUserId,
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> AddNursingAssistantAsync(
        CreateNursingAssistantRequest request,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeOptionalText(request.Name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return ServiceResult.Failure("El nombre del auxiliar es obligatorio.");
        }

        var existingAssistants = await _repository.GetNursingAssistantsAsync(onlyActive: false, cancellationToken);
        var normalized = normalizedName.ToUpperInvariant();

        if (existingAssistants.Any(x => x.NormalizedName == normalized))
        {
            return ServiceResult.Failure("Ese auxiliar ya se encuentra registrado.");
        }

        var nursingAssistant = new NursingAssistant
        {
            Name = normalizedName,
            NormalizedName = normalized,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _repository.AddNursingAssistantAsync(nursingAssistant, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "NURSING_ASSISTANT_CREATED",
            entity: "NursingAssistant",
            details: $"Auxiliar creado: {nursingAssistant.Name}.",
            performedByUserId: performedByUserId,
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> SetNursingAssistantStatusAsync(
        int nursingAssistantId,
        bool isActive,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var nursingAssistant = await _repository.GetNursingAssistantByIdAsync(nursingAssistantId, cancellationToken);
        if (nursingAssistant is null)
        {
            return ServiceResult.Failure("El auxiliar no existe.");
        }

        if (nursingAssistant.IsActive == isActive)
        {
            return ServiceResult.Success();
        }

        nursingAssistant.IsActive = isActive;
        await _repository.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: isActive ? "NURSING_ASSISTANT_ACTIVATED" : "NURSING_ASSISTANT_DEACTIVATED",
            entity: "NursingAssistant",
            details: $"Estado auxiliar {nursingAssistant.Name} => {(isActive ? "Activo" : "Inactivo")}.",
            performedByUserId: performedByUserId,
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> AddOpsAssistantAsync(
        CreateOpsAssistantRequest request,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ServiceResult.Failure("La lista de auxiliares OPS se lee desde Neon y es de solo lectura.");
    }

    public async Task<ServiceResult> SetOpsAssistantStatusAsync(
        int opsAssistantId,
        bool isActive,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ServiceResult.Failure("La lista de auxiliares OPS se lee desde Neon y es de solo lectura.");
    }

    public async Task<ServiceResult> UpdateUserPermissionsAsync(
        Guid userId,
        IReadOnlyCollection<int> grantedPermissionIds,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserByIdWithPermissionsAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Failure("El usuario no existe.");
        }

        var allPermissions = await _repository.GetScreenPermissionsAsync(cancellationToken);
        var allPermissionIds = allPermissions.Select(p => p.Id).ToHashSet();

        var requestedPermissionIds = grantedPermissionIds
            .Where(allPermissionIds.Contains)
            .Distinct()
            .ToHashSet();

        var adminPermission = allPermissions.FirstOrDefault(p => p.Code == SystemPermissions.UserAdministration);
        if (performedByUserId == userId
            && (adminPermission is null || !requestedPermissionIds.Contains(adminPermission.Id)))
        {
            return ServiceResult.Failure("No puedes quitarte tu propio permiso de administracion de usuarios.");
        }

        var currentPermissionIds = user.UserPermissions.Select(p => p.PermissionId).ToHashSet();
        var addedIds = requestedPermissionIds.Except(currentPermissionIds).ToList();
        var removedIds = currentPermissionIds.Except(requestedPermissionIds).ToList();

        await _repository.ReplaceUserPermissionsAsync(userId, requestedPermissionIds.ToList(), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var permissionDictionary = allPermissions.ToDictionary(x => x.Id, x => x.Code);
        var addedCodes = addedIds.Where(permissionDictionary.ContainsKey).Select(id => permissionDictionary[id]);
        var removedCodes = removedIds.Where(permissionDictionary.ContainsKey).Select(id => permissionDictionary[id]);
        var details = $"Permisos usuario {user.Username}. Agregados: [{string.Join(", ", addedCodes)}]. Removidos: [{string.Join(", ", removedCodes)}].";

        await _auditService.LogAsync(
            action: "USER_PERMISSIONS_UPDATED",
            entity: "UserPermission",
            details: details,
            performedByUserId: performedByUserId,
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        return ServiceResult.Success();
    }

    private async Task<ServiceResult> ValidateDuplicatesAsync(
        string normalizedUsername,
        string normalizedEmail,
        string normalizedNationalId,
        Guid? excludeUserId,
        CancellationToken cancellationToken)
    {
        if (await _repository.ExistsByNormalizedUsernameAsync(normalizedUsername, excludeUserId, cancellationToken))
        {
            return ServiceResult.Failure("El nombre de usuario ya esta registrado.");
        }

        if (await _repository.ExistsByNormalizedEmailAsync(normalizedEmail, excludeUserId, cancellationToken))
        {
            return ServiceResult.Failure("El correo electronico ya esta registrado.");
        }

        if (await _repository.ExistsByNormalizedNationalIdAsync(normalizedNationalId, excludeUserId, cancellationToken))
        {
            return ServiceResult.Failure("La cedula ya esta registrada.");
        }

        return ServiceResult.Success();
    }

    private static PermissionOptionDto MapPermission(AppPermission permission)
    {
        return new PermissionOptionDto
        {
            Id = permission.Id,
            Code = permission.Code,
            Description = permission.Description
        };
    }

    private static string BuildFullName(string firstName, string lastName1, string? lastName2)
    {
        var names = new[] { firstName.Trim(), lastName1.Trim(), NormalizeOptionalText(lastName2) }
            .Where(value => !string.IsNullOrWhiteSpace(value));
        return string.Join(' ', names);
    }

    private static string NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized;
    }

    private static (string Username, string NormalizedUsername, string Email, string NormalizedEmail, string NationalId, string NormalizedNationalId)
        NormalizeIdentityData(string username, string email, string nationalId)
    {
        var normalizedUsername = username.Trim();
        var normalizedEmail = email.Trim();
        var normalizedNationalId = nationalId.Trim();

        return (
            Username: normalizedUsername,
            NormalizedUsername: normalizedUsername.ToUpperInvariant(),
            Email: normalizedEmail,
            NormalizedEmail: normalizedEmail.ToUpperInvariant(),
            NationalId: normalizedNationalId,
            NormalizedNationalId: normalizedNationalId.ToUpperInvariant());
    }
}
