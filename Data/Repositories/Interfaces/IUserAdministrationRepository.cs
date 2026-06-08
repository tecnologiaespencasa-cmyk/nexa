using IntranetPrueba.Data.Entities;

namespace IntranetPrueba.Data.Repositories.Interfaces;

public interface IUserAdministrationRepository
{
    Task<IReadOnlyList<AppUser>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NursingAssistant>> GetNursingAssistantsAsync(bool onlyActive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpsAssistant>> GetOpsAssistantsAsync(bool onlyActive, CancellationToken cancellationToken = default);

    Task<AppUser?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NursingAssistant?> GetNursingAssistantByIdAsync(int nursingAssistantId, CancellationToken cancellationToken = default);
    Task<OpsAssistant?> GetOpsAssistantByIdAsync(int opsAssistantId, CancellationToken cancellationToken = default);

    Task<AppUser?> GetUserByIdWithPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNormalizedEmailAsync(
        string normalizedEmail,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNormalizedUsernameAsync(
        string normalizedUsername,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNormalizedNationalIdAsync(
        string normalizedNationalId,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppPermission>> GetScreenPermissionsAsync(CancellationToken cancellationToken = default);

    Task AddUserAsync(AppUser user, CancellationToken cancellationToken = default);
    Task AddNursingAssistantAsync(NursingAssistant nursingAssistant, CancellationToken cancellationToken = default);
    Task AddOpsAssistantAsync(OpsAssistant opsAssistant, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task ReplaceUserPermissionsAsync(
        Guid userId,
        IReadOnlyCollection<int> permissionIds,
        CancellationToken cancellationToken = default);
}
