using IntranetPrueba.Data;
using IntranetPrueba.Data.Repositories;
using IntranetPrueba.Data.Repositories.Interfaces;
using IntranetPrueba.Data.Seed;
using IntranetPrueba.Models.Security;
using IntranetPrueba.Security.Authorization;
using IntranetPrueba.Services;
using IntranetPrueba.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserAdministrationService, UserAdministrationService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
builder.Services.AddScoped<ICurrentUserPermissionService, CurrentUserPermissionService>();
builder.Services.AddScoped<IFarmaciaDispatchNotificationService, FarmaciaDispatchNotificationService>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddHttpClient<IAddressValidationService, GoogleAddressValidationService>();
builder.Services.AddHttpClient<IEmailService, GraphEmailService>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "IntranetPrueba.Auth";
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
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

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
