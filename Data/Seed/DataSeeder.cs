using IntranetPrueba.Data.Entities;
using IntranetPrueba.Models.Security;
using IntranetPrueba.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace IntranetPrueba.Data.Seed;

public static class DataSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var context = services.GetRequiredService<ApplicationDbContext>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var passwordService = services.GetRequiredService<IPasswordService>();

        var adminEmail = configuration["BootstrapAdmin:Email"]?.Trim();
        var adminPassword = configuration["BootstrapAdmin:Password"];
        var adminUsername = configuration["BootstrapAdmin:Username"]?.Trim();
        var adminNationalId = configuration["BootstrapAdmin:NationalId"]?.Trim() ?? "0000000000";

        if (!context.Database.GetMigrations().Any())
        {
            return;
        }

        await context.Database.MigrateAsync();
        await EnsureScreenPermissionsAsync(context);
        await EnsureMedicamentosAsync(context, app.Environment.ContentRootPath);

        if (await context.Users.AnyAsync())
        {
            try
            {
                await TryRecoverBootstrapAdminAsync(
                    context,
                    configuration,
                    passwordService,
                    adminEmail,
                    adminUsername,
                    adminPassword,
                    adminNationalId,
                    app.Environment.IsDevelopment());
            }
            catch
            {
                // No bloquear el inicio de la aplicacion por una recuperacion de bootstrap.
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(adminEmail)
            || string.IsNullOrWhiteSpace(adminPassword)
            || string.IsNullOrWhiteSpace(adminUsername))
        {
            return;
        }

        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.NormalizedName == "ADMINISTRADOR");
        if (adminRole is null)
        {
            return;
        }

        var normalizedEmail = adminEmail.ToUpperInvariant();
        var normalizedUsername = adminUsername.ToUpperInvariant();
        var normalizedNationalId = adminNationalId.ToUpperInvariant();

        var adminUser = new AppUser
        {
            FirstName = adminUsername,
            LastName1 = "Administrador",
            LastName2 = string.Empty,
            FullName = $"{adminUsername} Administrador",
            NationalId = adminNationalId,
            NormalizedNationalId = normalizedNationalId,
            Email = adminEmail,
            NormalizedEmail = normalizedEmail,
            Username = adminUsername,
            NormalizedUsername = normalizedUsername,
            PasswordHash = passwordService.HashPassword(adminPassword),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Users.Add(adminUser);
        context.UserRoles.Add(new AppUserRole { User = adminUser, RoleId = adminRole.Id });

        var screenPermissions = await context.Permissions
            .Where(p => SystemPermissions.ScreenPermissions.Contains(p.Code))
            .ToListAsync();

        foreach (var permission in screenPermissions)
        {
            context.UserPermissions.Add(new AppUserPermission
            {
                User = adminUser,
                PermissionId = permission.Id,
                GrantedAtUtc = DateTime.UtcNow
            });
        }

        context.AuditLogs.Add(new AuditLog
        {
            Action = "BOOTSTRAP_ADMIN_CREATED",
            Entity = "User",
            Details = $"Usuario administrador inicial creado para {adminUser.Email}.",
            PerformedByUserId = adminUser.Id,
            PerformedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static async Task TryRecoverBootstrapAdminAsync(
        ApplicationDbContext context,
        IConfiguration configuration,
        IPasswordService passwordService,
        string? adminEmail,
        string? adminUsername,
        string? adminPassword,
        string adminNationalId,
        bool isDevelopment)
    {
        var resetPasswordIfExists = configuration.GetValue<bool?>("BootstrapAdmin:ResetPasswordIfExists")
            ?? isDevelopment;
        if (!resetPasswordIfExists
            || string.IsNullOrWhiteSpace(adminUsername)
            || string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var normalizedUsername = adminUsername.Trim().ToUpperInvariant();
        var adminUser = await context.Users.FirstOrDefaultAsync(u => u.NormalizedUsername == normalizedUsername);
        if (adminUser is null)
        {
            return;
        }

        adminUser.PasswordHash = passwordService.HashPassword(adminPassword);
        adminUser.IsActive = true;

        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            var cleanEmail = adminEmail.Trim();
            adminUser.Email = cleanEmail;
            adminUser.NormalizedEmail = cleanEmail.ToUpperInvariant();
        }

        if (string.IsNullOrWhiteSpace(adminUser.FirstName))
        {
            adminUser.FirstName = adminUsername.Trim();
        }

        if (string.IsNullOrWhiteSpace(adminUser.LastName1))
        {
            adminUser.LastName1 = "Administrador";
        }

        adminUser.LastName2 ??= string.Empty;

        if (string.IsNullOrWhiteSpace(adminUser.NationalId))
        {
            adminUser.NationalId = adminNationalId;
            adminUser.NormalizedNationalId = adminNationalId.ToUpperInvariant();
        }

        var names = new[] { adminUser.FirstName, adminUser.LastName1, adminUser.LastName2 }
            .Where(value => !string.IsNullOrWhiteSpace(value));
        adminUser.FullName = string.Join(' ', names);

        var screenPermissionIds = await context.Permissions
            .Where(p => SystemPermissions.ScreenPermissions.Contains(p.Code))
            .Select(p => p.Id)
            .ToListAsync();

        var currentPermissionIds = await context.UserPermissions
            .Where(up => up.UserId == adminUser.Id)
            .Select(up => up.PermissionId)
            .ToListAsync();

        var missingPermissionIds = screenPermissionIds
            .Except(currentPermissionIds)
            .ToList();

        foreach (var permissionId in missingPermissionIds)
        {
            context.UserPermissions.Add(new AppUserPermission
            {
                UserId = adminUser.Id,
                PermissionId = permissionId,
                GrantedAtUtc = DateTime.UtcNow
            });
        }

        context.AuditLogs.Add(new AuditLog
        {
            Action = "BOOTSTRAP_ADMIN_PASSWORD_RESET",
            Entity = "User",
            Details = $"Contrasena de bootstrap admin reiniciada para {adminUser.Username}.",
            PerformedByUserId = adminUser.Id,
            PerformedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static async Task EnsureScreenPermissionsAsync(ApplicationDbContext context)
    {
        var screenPermissions = new[]
        {
            new { Code = SystemPermissions.AuditRead, Description = "Auditoria" },
            new { Code = SystemPermissions.UserAdministration, Description = "Administracion de usuarios" },
            new { Code = SystemPermissions.Censo, Description = "Censo" },
            new { Code = SystemPermissions.Reportes, Description = "Reportes" },
            new { Code = SystemPermissions.InventarioBiomedico, Description = "Inventario biomedico" },
            new { Code = SystemPermissions.Farmacia, Description = "Farmacia" }
        };

        var existingCodes = await context.Permissions
            .Select(p => p.Code)
            .ToListAsync();

        var existingSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newPermissions = screenPermissions
            .Where(permission => !existingSet.Contains(permission.Code))
            .Select(permission => new AppPermission
            {
                Code = permission.Code,
                Description = permission.Description
            })
            .ToList();

        if (newPermissions.Count == 0)
        {
            return;
        }

        await context.Permissions.AddRangeAsync(newPermissions);
        await context.SaveChangesAsync();
    }

    private static async Task EnsureMedicamentosAsync(ApplicationDbContext context, string contentRootPath)
    {
        var path = Path.Combine(contentRootPath, "Data", "Seed", "medicamentos_catalog.json");
        if (!File.Exists(path))
        {
            return;
        }

        IReadOnlyList<MedicamentoSeedItem> seedItems;
        try
        {
            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            seedItems = JsonSerializer.Deserialize<List<MedicamentoSeedItem>>(json) ?? [];
        }
        catch
        {
            return;
        }

        var normalizedSeedItems = seedItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Nombre))
            .Select(item => new { Item = item, NormalizedNombre = NormalizeCatalogKey(item.Nombre) })
            .Where(item => !string.IsNullOrWhiteSpace(item.NormalizedNombre))
            .GroupBy(item => item.NormalizedNombre, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();

        if (normalizedSeedItems.Count == 0)
        {
            return;
        }

        var normalizedNames = normalizedSeedItems
            .Select(item => item.NormalizedNombre)
            .ToList();

        var existing = await context.Medicamentos
            .Where(item => normalizedNames.Contains(item.NormalizedNombre))
            .ToDictionaryAsync(item => item.NormalizedNombre, StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        foreach (var seed in normalizedSeedItems)
        {
            if (!existing.TryGetValue(seed.NormalizedNombre, out var medicamento))
            {
                medicamento = new Medicamento
                {
                    NormalizedNombre = seed.NormalizedNombre
                };
                context.Medicamentos.Add(medicamento);
            }

            medicamento.Nombre = seed.Item.Nombre.Trim();
            medicamento.PresentacionRequisicion = NormalizeNullableSeedText(seed.Item.PresentacionRequisicion);
            medicamento.ConcentracionMiligramos = NormalizeNullableSeedText(seed.Item.ConcentracionMiligramos);
            medicamento.Jeringa = NormalizeNullableSeedText(seed.Item.Jeringa);
            medicamento.SolucionParaDilucion = NormalizeNullableSeedText(seed.Item.SolucionParaDilucion);
            medicamento.DilucionRecomendada = NormalizeNullableSeedText(seed.Item.DilucionRecomendada);
            medicamento.VehiculoReconstitucion = NormalizeNullableSeedText(seed.Item.VehiculoReconstitucion);
            medicamento.TiempoEstabilidad = NormalizeNullableSeedText(seed.Item.TiempoEstabilidad);
            medicamento.TiempoInfusionMinutos = NormalizeNullableSeedText(seed.Item.TiempoInfusionMinutos);
            medicamento.BombaInfusion = NormalizeNullableSeedText(seed.Item.BombaInfusion);
            medicamento.MarcacionRiesgo = NormalizeNullableSeedText(seed.Item.MarcacionRiesgo);
            medicamento.Flebozantes = NormalizeNullableSeedText(seed.Item.Flebozantes);
            medicamento.EquipoFotosensible = NormalizeNullableSeedText(seed.Item.EquipoFotosensible);
            medicamento.CadenaFrio = NormalizeNullableSeedText(seed.Item.CadenaFrio);
            medicamento.IsActive = true;
            medicamento.UpdatedAtUtc = now;
        }

        await context.SaveChangesAsync();
    }

    private static string? NormalizeNullableSeedText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeCatalogKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : ' ');
            }
        }

        return string.Join(
            ' ',
            builder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed class MedicamentoSeedItem
    {
        public string Nombre { get; set; } = string.Empty;

        public string? PresentacionRequisicion { get; set; }

        public string? ConcentracionMiligramos { get; set; }

        public string? Jeringa { get; set; }

        public string? SolucionParaDilucion { get; set; }

        public string? DilucionRecomendada { get; set; }

        public string? VehiculoReconstitucion { get; set; }

        public string? TiempoEstabilidad { get; set; }

        public string? TiempoInfusionMinutos { get; set; }

        public string? BombaInfusion { get; set; }

        public string? MarcacionRiesgo { get; set; }

        public string? Flebozantes { get; set; }

        public string? EquipoFotosensible { get; set; }

        public string? CadenaFrio { get; set; }
    }
}
