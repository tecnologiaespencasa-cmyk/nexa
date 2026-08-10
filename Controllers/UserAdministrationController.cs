using System.Globalization;
using System.Security.Claims;
using System.Text;
using Nexa.Models.Security;
using Nexa.Models.ViewModels;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexa.Controllers;

[Authorize(Policy = SystemPermissions.UserAdministration)]
public class UserAdministrationController : Controller
{
    private const int UsersPageSize = 20;
    private const int OpsPageSize = 25;
    private readonly IUserAdministrationService _userAdministrationService;

    public UserAdministrationController(IUserAdministrationService userAdministrationService)
    {
        _userAdministrationService = userAdministrationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? userSearch,
        string? userStatus,
        int userPage = 1,
        string? opsSearch = null,
        int opsPage = 1,
        CancellationToken cancellationToken = default)
    {
        var model = await BuildIndexViewModelAsync(
            nursingAssistantModel: new NursingAssistantCreateViewModel(),
            opsAssistantModel: new OpsAssistantCreateViewModel(),
            userSearch,
            userStatus,
            userPage,
            opsSearch,
            opsPage,
            cancellationToken);

        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new UserCreateViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(UserCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _userAdministrationService.CreateUserAsync(
            request: new CreateUserRequest
            {
                Username = model.Username,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName1 = model.LastName1,
                LastName2 = model.LastName2,
                NationalId = model.NationalId,
                Password = model.Password
            },
            performedByUserId: GetCurrentUserId(),
            ipAddress: GetClientIpAddress(),
            cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No fue posible crear el usuario.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Usuario creado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userAdministrationService.GetUserForEditAsync(id, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Usuario no encontrado.";
            return RedirectToAction(nameof(Index));
        }

        var dto = result.Value;
        return View(new UserEditViewModel
        {
            Id = dto.Id,
            Username = dto.Username,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName1 = dto.LastName1,
            LastName2 = dto.LastName2,
            NationalId = dto.NationalId,
            IsActive = dto.IsActive
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(UserEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _userAdministrationService.UpdateUserAsync(
            request: new UpdateUserRequest
            {
                UserId = model.Id,
                Username = model.Username,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName1 = model.LastName1,
                LastName2 = model.LastName2,
                NationalId = model.NationalId
            },
            performedByUserId: GetCurrentUserId(),
            ipAddress: GetClientIpAddress(),
            cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No fue posible actualizar el usuario.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Usuario actualizado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var result = await _userAdministrationService.SetUserActiveStatusAsync(
            userId: id,
            isActive: isActive,
            performedByUserId: GetCurrentUserId(),
            ipAddress: GetClientIpAddress(),
            cancellationToken: cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? $"Usuario {(isActive ? "activado" : "desactivado")} correctamente."
                : result.ErrorMessage ?? "No fue posible cambiar el estado del usuario.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AddNursingAssistant(
        [Bind(Prefix = "NewNursingAssistant")] NursingAssistantCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await BuildIndexViewWithAssistantModelsAsync(
                nursingAssistantModel: model,
                opsAssistantModel: new OpsAssistantCreateViewModel(),
                cancellationToken);
        }

        var result = await _userAdministrationService.AddNursingAssistantAsync(
            request: new CreateNursingAssistantRequest
            {
                Name = model.Name
            },
            performedByUserId: GetCurrentUserId(),
            ipAddress: GetClientIpAddress(),
            cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError("NewNursingAssistant.Name", result.ErrorMessage ?? "No fue posible crear el auxiliar.");
            return await BuildIndexViewWithAssistantModelsAsync(
                nursingAssistantModel: model,
                opsAssistantModel: new OpsAssistantCreateViewModel(),
                cancellationToken);
        }

        TempData["SuccessMessage"] = "Auxiliar administrativo de enfermeria agregado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> EditNursingAssistant(int id, string? name, CancellationToken cancellationToken)
    {
        var result = await _userAdministrationService.UpdateNursingAssistantNameAsync(
            nursingAssistantId: id,
            name: name,
            performedByUserId: GetCurrentUserId(),
            ipAddress: GetClientIpAddress(),
            cancellationToken: cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Nombre del auxiliar actualizado correctamente."
                : result.ErrorMessage ?? "No fue posible actualizar el nombre del auxiliar.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetNursingAssistantStatus(int id, bool isActive, CancellationToken cancellationToken)
    {
        var result = await _userAdministrationService.SetNursingAssistantStatusAsync(
            nursingAssistantId: id,
            isActive: isActive,
            performedByUserId: GetCurrentUserId(),
            ipAddress: GetClientIpAddress(),
            cancellationToken: cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? $"Auxiliar administrativo de enfermeria {(isActive ? "activado" : "desactivado")} correctamente."
                : result.ErrorMessage ?? "No fue posible cambiar el estado del auxiliar administrativo de enfermeria.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AddOpsAssistant(
        [Bind(Prefix = "NewOpsAssistant")] OpsAssistantCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await BuildIndexViewWithAssistantModelsAsync(
                nursingAssistantModel: new NursingAssistantCreateViewModel(),
                opsAssistantModel: model,
                cancellationToken);
        }

        var result = await _userAdministrationService.AddOpsAssistantAsync(
            request: new CreateOpsAssistantRequest
            {
                Name = model.Name
            },
            performedByUserId: GetCurrentUserId(),
            ipAddress: GetClientIpAddress(),
            cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError("NewOpsAssistant.Name", result.ErrorMessage ?? "No fue posible crear el auxiliar OPS.");
            return await BuildIndexViewWithAssistantModelsAsync(
                nursingAssistantModel: new NursingAssistantCreateViewModel(),
                opsAssistantModel: model,
                cancellationToken);
        }

        TempData["SuccessMessage"] = "Auxiliar OPS agregado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetOpsAssistantStatus(int id, bool isActive, CancellationToken cancellationToken)
    {
        var result = await _userAdministrationService.SetOpsAssistantStatusAsync(
            opsAssistantId: id,
            isActive: isActive,
            performedByUserId: GetCurrentUserId(),
            ipAddress: GetClientIpAddress(),
            cancellationToken: cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? $"Auxiliar OPS {(isActive ? "activado" : "desactivado")} correctamente."
                : result.ErrorMessage ?? "No fue posible cambiar el estado del auxiliar OPS.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userAdministrationService.GetUserForEditAsync(id, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Usuario no encontrado.";
            return RedirectToAction(nameof(Index));
        }

        return View(new UserResetPasswordViewModel
        {
            UserId = result.Value.Id,
            Username = result.Value.Username
        });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(UserResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _userAdministrationService.ResetPasswordAsync(
            request: new ResetUserPasswordRequest
            {
                UserId = model.UserId,
                NewPassword = model.NewPassword
            },
            performedByUserId: GetCurrentUserId(),
            ipAddress: GetClientIpAddress(),
            cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No fue posible restablecer la contrasena.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Contrasena restablecida correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Permissions(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userAdministrationService.GetUserPermissionsAsync(id, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Usuario no encontrado.";
            return RedirectToAction(nameof(Index));
        }

        return View(MapPermissionAssignment(result.Value));
    }

    [HttpPost]
    public async Task<IActionResult> Permissions(UserPermissionAssignmentViewModel model, CancellationToken cancellationToken)
    {
        model.GrantedPermissionIds ??= [];
        var result = await _userAdministrationService.UpdateUserPermissionsAsync(
            userId: model.UserId,
            grantedPermissionIds: model.GrantedPermissionIds,
            performedByUserId: GetCurrentUserId(),
            ipAddress: GetClientIpAddress(),
            cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No fue posible actualizar los permisos.");
            var reloadResult = await _userAdministrationService.GetUserPermissionsAsync(model.UserId, cancellationToken);
            if (reloadResult.Succeeded && reloadResult.Value is not null)
            {
                model = MapPermissionAssignment(reloadResult.Value);
            }

            return View(model);
        }

        TempData["SuccessMessage"] = "Permisos actualizados correctamente.";
        return RedirectToAction(nameof(Index));
    }

    private static UserListItemViewModel MapToViewModel(UserSummaryDto dto)
    {
        return new UserListItemViewModel
        {
            Id = dto.Id,
            Username = dto.Username,
            FullName = dto.FullName,
            Email = dto.Email,
            NationalId = dto.NationalId,
            IsActive = dto.IsActive
        };
    }

    private static UserPermissionAssignmentViewModel MapPermissionAssignment(UserPermissionAssignmentDto dto)
    {
        return new UserPermissionAssignmentViewModel
        {
            UserId = dto.UserId,
            Username = dto.Username,
            FullName = dto.FullName,
            AvailablePermissions = dto.AvailablePermissions
                .Select(p => new PermissionOptionViewModel
                {
                    Id = p.Id,
                    Code = p.Code,
                    Description = p.Description
                })
                .ToList(),
            GrantedPermissions = dto.GrantedPermissions
                .Select(p => new PermissionOptionViewModel
                {
                    Id = p.Id,
                    Code = p.Code,
                    Description = p.Description
                })
                .ToList(),
            GrantedPermissionIds = dto.GrantedPermissions.Select(p => p.Id).ToList()
        };
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : Guid.Empty;
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static string NormalizeUserSearchTerm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposedValue = value.Trim().Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposedValue.Length);

        foreach (var character in decomposedValue)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark
                && char.IsLetterOrDigit(character))
            {
                normalized.Append(char.ToUpperInvariant(character));
            }
        }

        return normalized.ToString();
    }

    private static List<NursingAssistantListItemViewModel> MapNursingAssistants(IEnumerable<NursingAssistantDto> assistants)
    {
        return assistants
            .Select(nursingAssistant => new NursingAssistantListItemViewModel
            {
                Id = nursingAssistant.Id,
                Name = nursingAssistant.Name,
                IsActive = nursingAssistant.IsActive
            })
            .ToList();
    }

    private static List<OpsAssistantListItemViewModel> MapOpsAssistants(IEnumerable<OpsAssistantDto> assistants)
    {
        return assistants
            .Select(opsAssistant => new OpsAssistantListItemViewModel
            {
                Name = opsAssistant.Name,
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

    private async Task<ViewResult> BuildIndexViewWithAssistantModelsAsync(
        NursingAssistantCreateViewModel nursingAssistantModel,
        OpsAssistantCreateViewModel opsAssistantModel,
        CancellationToken cancellationToken)
    {
        var model = await BuildIndexViewModelAsync(
            nursingAssistantModel,
            opsAssistantModel,
            userSearch: null,
            userStatus: null,
            userPage: 1,
            opsSearch: null,
            opsPage: 1,
            cancellationToken);

        return View("Index", model);
    }

    private async Task<UserAdministrationIndexViewModel> BuildIndexViewModelAsync(
        NursingAssistantCreateViewModel nursingAssistantModel,
        OpsAssistantCreateViewModel opsAssistantModel,
        string? userSearch,
        string? userStatus,
        int userPage,
        string? opsSearch,
        int opsPage,
        CancellationToken cancellationToken)
    {
        var users = await _userAdministrationService.GetUsersAsync(cancellationToken);
        var nursingAssistants = await _userAdministrationService.GetNursingAssistantsAsync(onlyActive: false, cancellationToken);
        var opsAssistants = await _userAdministrationService.GetOpsAssistantsAsync(onlyActive: false, cancellationToken);
        var mappedOpsAssistants = MapOpsAssistants(opsAssistants);

        var userSearchTerm = userSearch?.Trim() ?? string.Empty;
        var normalizedUserSearch = NormalizeUserSearchTerm(userSearchTerm);
        var normalizedUserStatus = userStatus?.Trim() ?? string.Empty;
        if (normalizedUserStatus is not "Activo" and not "Inactivo")
        {
            normalizedUserStatus = string.Empty;
        }

        var filteredUsers = users
            .Where(user => string.IsNullOrWhiteSpace(normalizedUserSearch)
                || NormalizeUserSearchTerm(user.Username).Contains(normalizedUserSearch, StringComparison.Ordinal)
                || NormalizeUserSearchTerm(user.FullName).Contains(normalizedUserSearch, StringComparison.Ordinal)
                || NormalizeUserSearchTerm(user.Email).Contains(normalizedUserSearch, StringComparison.Ordinal)
                || NormalizeUserSearchTerm(user.NationalId).Contains(normalizedUserSearch, StringComparison.Ordinal))
            .Where(user => normalizedUserStatus switch
            {
                "Activo" => user.IsActive,
                "Inactivo" => !user.IsActive,
                _ => true
            })
            .ToList();

        var userTotalCount = filteredUsers.Count;
        var userTotalPages = Math.Max(1, (int)Math.Ceiling(userTotalCount / (double)UsersPageSize));
        var currentUserPage = Math.Clamp(userPage, 1, userTotalPages);
        var pagedUsers = filteredUsers
            .Skip((currentUserPage - 1) * UsersPageSize)
            .Take(UsersPageSize)
            .Select(MapToViewModel)
            .ToList();

        var normalizedSearch = opsSearch?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            mappedOpsAssistants = mappedOpsAssistants
                .Where(assistant =>
                    assistant.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || assistant.NationalId.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var opsTotalCount = mappedOpsAssistants.Count;
        var opsTotalPages = Math.Max(1, (int)Math.Ceiling(opsTotalCount / (double)OpsPageSize));
        var currentPage = Math.Clamp(opsPage, 1, opsTotalPages);
        var pagedOpsAssistants = mappedOpsAssistants
            .Skip((currentPage - 1) * OpsPageSize)
            .Take(OpsPageSize)
            .ToList();

        return new UserAdministrationIndexViewModel
        {
            Users = pagedUsers,
            UserSearchTerm = userSearchTerm,
            UserStatusFilter = normalizedUserStatus,
            UserCurrentPage = currentUserPage,
            UserPageSize = UsersPageSize,
            UserTotalPages = userTotalPages,
            UserTotalCount = userTotalCount,
            NursingAssistants = MapNursingAssistants(nursingAssistants),
            OpsAssistants = pagedOpsAssistants,
            OpsSearchTerm = normalizedSearch,
            OpsCurrentPage = currentPage,
            OpsPageSize = OpsPageSize,
            OpsTotalPages = opsTotalPages,
            OpsTotalCount = opsTotalCount,
            NewNursingAssistant = nursingAssistantModel,
            NewOpsAssistant = opsAssistantModel
        };
    }
}
