using IntranetPrueba.Services.Models;

namespace IntranetPrueba.Services.Interfaces;

public interface IUserAdministrationService
{
    Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NursingAssistantDto>> GetNursingAssistantsAsync(bool onlyActive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpsAssistantDto>> GetOpsAssistantsAsync(bool onlyActive, CancellationToken cancellationToken = default);

    Task<ServiceResult<UserEditDto>> GetUserForEditAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ServiceResult<UserPermissionAssignmentDto>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ServiceResult> CreateUserAsync(
        CreateUserRequest request,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateUserAsync(
        UpdateUserRequest request,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> SetUserActiveStatusAsync(
        Guid userId,
        bool isActive,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> ResetPasswordAsync(
        ResetUserPasswordRequest request,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> AddNursingAssistantAsync(
        CreateNursingAssistantRequest request,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> AddOpsAssistantAsync(
        CreateOpsAssistantRequest request,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> SetNursingAssistantStatusAsync(
        int nursingAssistantId,
        bool isActive,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> SetOpsAssistantStatusAsync(
        int opsAssistantId,
        bool isActive,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateUserPermissionsAsync(
        Guid userId,
        IReadOnlyCollection<int> grantedPermissionIds,
        Guid performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
