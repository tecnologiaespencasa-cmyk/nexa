using IntranetPrueba.Data.Entities;
using IntranetPrueba.Data.Repositories.Interfaces;
using IntranetPrueba.Models.Security;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Data.Repositories;

public class UserAdministrationRepository : IUserAdministrationRepository
{
    private readonly ApplicationDbContext _context;

    public UserAdministrationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AppUser>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.UserPermissions)
                .ThenInclude(up => up.Permission)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName1)
            .ThenBy(u => u.Username)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NursingAssistant>> GetNursingAssistantsAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default)
    {
        var query = _context.NursingAssistants.AsNoTracking();
        if (onlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OpsAssistant>> GetOpsAssistantsAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default)
    {
        var query = _context.OpsAssistants.AsNoTracking();
        if (onlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<AppUser?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public Task<NursingAssistant?> GetNursingAssistantByIdAsync(
        int nursingAssistantId,
        CancellationToken cancellationToken = default)
    {
        return _context.NursingAssistants.FirstOrDefaultAsync(x => x.Id == nursingAssistantId, cancellationToken);
    }

    public Task<OpsAssistant?> GetOpsAssistantByIdAsync(
        int opsAssistantId,
        CancellationToken cancellationToken = default)
    {
        return _context.OpsAssistants.FirstOrDefaultAsync(x => x.Id == opsAssistantId, cancellationToken);
    }

    public Task<AppUser?> GetUserByIdWithPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.Users
            .Include(u => u.UserPermissions)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public Task<bool> ExistsByNormalizedEmailAsync(
        string normalizedEmail,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        return _context.Users.AnyAsync(
            u => u.NormalizedEmail == normalizedEmail && (!excludeUserId.HasValue || u.Id != excludeUserId.Value),
            cancellationToken);
    }

    public Task<bool> ExistsByNormalizedUsernameAsync(
        string normalizedUsername,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        return _context.Users.AnyAsync(
            u => u.NormalizedUsername == normalizedUsername && (!excludeUserId.HasValue || u.Id != excludeUserId.Value),
            cancellationToken);
    }

    public Task<bool> ExistsByNormalizedNationalIdAsync(
        string normalizedNationalId,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        return _context.Users.AnyAsync(
            u => u.NormalizedNationalId == normalizedNationalId
                 && (!excludeUserId.HasValue || u.Id != excludeUserId.Value),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AppPermission>> GetScreenPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .Where(p => SystemPermissions.ScreenPermissions.Contains(p.Code))
            .OrderBy(p => p.Description)
            .ToListAsync(cancellationToken);
    }

    public async Task AddUserAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public async Task AddNursingAssistantAsync(NursingAssistant nursingAssistant, CancellationToken cancellationToken = default)
    {
        await _context.NursingAssistants.AddAsync(nursingAssistant, cancellationToken);
    }

    public async Task AddOpsAssistantAsync(OpsAssistant opsAssistant, CancellationToken cancellationToken = default)
    {
        await _context.OpsAssistants.AddAsync(opsAssistant, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceUserPermissionsAsync(
        Guid userId,
        IReadOnlyCollection<int> permissionIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedPermissionIds = permissionIds
            .Distinct()
            .ToHashSet();

        var currentPermissions = await _context.UserPermissions
            .Where(up => up.UserId == userId)
            .ToListAsync(cancellationToken);

        var toRemove = currentPermissions
            .Where(up => !normalizedPermissionIds.Contains(up.PermissionId))
            .ToList();

        if (toRemove.Count > 0)
        {
            _context.UserPermissions.RemoveRange(toRemove);
        }

        var existingPermissionIds = currentPermissions
            .Select(up => up.PermissionId)
            .ToHashSet();

        var toAdd = normalizedPermissionIds
            .Where(permissionId => !existingPermissionIds.Contains(permissionId))
            .Select(permissionId => new AppUserPermission
            {
                UserId = userId,
                PermissionId = permissionId,
                GrantedAtUtc = DateTime.UtcNow
            });

        await _context.UserPermissions.AddRangeAsync(toAdd, cancellationToken);
    }
}
