using System.Globalization;
using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;
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

// Producción enviaba todo sin comprimir: la ficha de un paciente con tres programas son 1,4 MB de
// HTML (675 KB de scripts en línea) en cada consulta. Con Brotli queda en ~135 KB por unos 12 ms
// de CPU (medido 2026-09-24 con la página real). Sobre HTTPS es seguro: el único secreto de la
// página, el token antifalsificación, cambia en cada respuesta, así que BREACH no tiene qué adivinar.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Optimal);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

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
builder.Services.AddScoped<IPortalPacienteRepository, PortalPacienteRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserAdministrationService, UserAdministrationService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
builder.Services.AddScoped<ICurrentUserPermissionService, CurrentUserPermissionService>();
builder.Services.AddScoped<ICensoPacienteService, CensoPacienteService>();
builder.Services.AddScoped<IHojaVidaPacienteService, HojaVidaPacienteService>();
builder.Services.AddScoped<ICensoTabuladoService, CensoTabuladoService>();
builder.Services.AddScoped<IFarmaciaDispatchNotificationService, FarmaciaDispatchNotificationService>();
builder.Services.AddScoped<ICensoProgramaNotificationService, CensoProgramaNotificationService>();
builder.Services.AddScoped<IEspacioCorporativoNotificationService, EspacioCorporativoNotificationService>();
builder.Services.AddHostedService<EmpacadoNotificationHostedService>();
builder.Services.AddScoped<IFarmaciaReportePorDesempacarService, FarmaciaReportePorDesempacarService>();
builder.Services.AddHostedService<FarmaciaReportePorDesempacarHostedService>();
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
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DirectorioAuxiliaresCache>();

// La cookie antifalsificación salía sin la marca Secure (la de sesión ya la tenía). Con
// SameAsRequest la lleva siempre que la conexión sea HTTPS. En Azure lo es: la plataforma ya lee
// X-Forwarded-For y X-Forwarded-Proto por su cuenta (la auditoría guarda la IP real del usuario y
// las redirecciones salen con https), así que aquí no se configura nada de eso; hacerlo reemplazaría
// esa configuración. No se usa Always: con Always el antifalsificación lanza error en cualquier
// petición que no sea HTTPS, lo que tumbaba el inicio de sesión local (http://localhost).
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);

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

// Cabeceras de seguridad en toda respuesta. nosniff: el navegador no interpreta un archivo con otro
// tipo (un adjunto no se ejecuta como script). Referrer-Policy: la URL completa, que puede llevar el
// documento del paciente (?cedulaPaciente=...), no viaja a otros sitios; solo el dominio. Y
// X-Frame-Options para que ninguna página se incruste en un sitio ajeno, también las que no llevan
// formulario (el antifalsificación ya la ponía en las que sí).
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var cabeceras = context.Response.Headers;
        cabeceras.XContentTypeOptions = "nosniff";
        cabeceras["Referrer-Policy"] = "strict-origin-when-cross-origin";
        if (!cabeceras.ContainsKey("X-Frame-Options"))
        {
            cabeceras.XFrameOptions = "SAMEORIGIN";
        }
        return Task.CompletedTask;
    });
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Antes de los archivos estáticos, para que también se compriman CSS y JS.
app.UseResponseCompression();

var invariantCulture = new RequestCulture(CultureInfo.InvariantCulture, CultureInfo.InvariantCulture);
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = invariantCulture,
    SupportedCultures = [CultureInfo.InvariantCulture],
    SupportedUICultures = [CultureInfo.InvariantCulture]
});

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    // Los archivos que la vista pide con asp-append-version llevan ?v=<hash del contenido>: si el
    // archivo cambia, cambia la URL. Se pueden guardar un año sin volver a preguntar. Sin esto el
    // navegador los revalidaba seguido, y los scripts del censo son ~600 KB (ver wwwroot/js/censo).
    OnPrepareResponse = contexto =>
    {
        if (contexto.Context.Request.Query.ContainsKey("v"))
        {
            contexto.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
    }
});
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
