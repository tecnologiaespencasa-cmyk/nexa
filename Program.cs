using System.Globalization;
using Nexa.Data;
using Nexa.Data.Repositories;
using Nexa.Data.Repositories.Interfaces;
using Nexa.Data.Seed;
using Nexa.Models.Security;
using Nexa.Security.Authorization;
using Nexa.Services;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection no está configurada. Define la cadena en User Secrets o variable de entorno.");
}

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure();
            npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
        }));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserAdministrationRepository, UserAdministrationRepository>();
builder.Services.AddScoped<INeonOpsAssistantUserRepository, NeonOpsAssistantUserRepository>();
builder.Services.AddScoped<INeonClinicaHeridasRepository, NeonClinicaHeridasRepository>();
builder.Services.AddScoped<IPortalNovedadRepository, PortalNovedadRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserAdministrationService, UserAdministrationService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
builder.Services.AddScoped<ICurrentUserPermissionService, CurrentUserPermissionService>();
builder.Services.AddScoped<IFarmaciaDispatchNotificationService, FarmaciaDispatchNotificationService>();
builder.Services.AddScoped<IEspacioCorporativoNotificationService, EspacioCorporativoNotificationService>();
builder.Services.AddHostedService<EmpacadoNotificationHostedService>();
builder.Services.AddHostedService<AuditRetentionHostedService>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddHttpClient<IAddressValidationService, GoogleAddressValidationService>();
builder.Services.AddHttpClient<IEmailService, GraphEmailService>();
builder.Services.AddHttpClient<ISharePointDocumentService, SharePointDocumentService>();
builder.Services.AddHttpClient<IRemisionExtractionService, RemisionExtractionService>();

// Puente hacia Supabase: un unico HttpClient reutilizado para todos los lotes.
builder.Services.Configure<SupabaseBridgeOptions>(
    builder.Configuration.GetSection(SupabaseBridgeOptions.SectionName));
builder.Services.AddHttpClient<IClinicaHeridasBridgeSyncService, ClinicaHeridasBridgeSyncService>((sp, client) =>
{
    var bridgeOptions = sp.GetRequiredService<IOptions<SupabaseBridgeOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(bridgeOptions.TimeoutSeconds, 5, 120));
});
builder.Services.AddSingleton<IBridgeSyncQueue, BridgeSyncQueue>();
builder.Services.AddHostedService<BridgeSyncPushHostedService>();
builder.Services.AddHostedService<BridgeSyncHostedService>();

builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Nexa.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(SystemRoles.Admin));
    options.AddPolicy(SystemPermissions.AuditRead, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new PermissionRequirement(SystemPermissions.AuditRead));
    });

    foreach (var permissionCode in SystemPermissions.ScreenPermissions)
    {
        options.AddPolicy(permissionCode, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new PermissionRequirement(permissionCode));
        });
    }

    options.AddPolicy(SystemPermissions.EspacioCorporativoAccess, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new PermissionRequirement(
            SystemPermissions.EspacioCorporativo,
            SystemPermissions.EspacioCorporativoAdmin));
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

var invariantCulture = new RequestCulture(CultureInfo.InvariantCulture, CultureInfo.InvariantCulture);
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = invariantCulture,
    SupportedCultures = [CultureInfo.InvariantCulture],
    SupportedUICultures = [CultureInfo.InvariantCulture]
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await DataSeeder.SeedAsync(app);

app.Run();
