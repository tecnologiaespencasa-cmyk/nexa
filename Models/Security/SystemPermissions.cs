namespace Nexa.Models.Security;

public static class SystemPermissions
{
    public const string AuditRead = "AUDIT_READ";
    public const string UserAdministration = "SCREEN_USERS_ADMIN";
    public const string Censo = "SCREEN_CENSO";
    public const string Reportes = "SCREEN_REPORTES";
    public const string InventarioBiomedico = "SCREEN_INVENTARIO_BIOMEDICO";
    public const string Farmacia = "SCREEN_FARMACIA";
    public const string Aprobacion = "APPROVAL_REAPERTURA_KARDEX";
    public const string AnalistaAsistencial = "ANALISTA_ASISTENCIAL";
    public const string EspacioCorporativo = "SCREEN_ESPACIO_CORPORATIVO";
    public const string EspacioCorporativoAdmin = "SCREEN_ESPACIO_CORPORATIVO_ADMIN";

    /// <summary>
    /// Politica compuesta: permite el ingreso al espacio corporativo tanto al usuario basico
    /// como al administrador (no es un permiso almacenado en base de datos).
    /// </summary>
    public const string EspacioCorporativoAccess = "POLICY_ESPACIO_CORPORATIVO_ACCESS";

    public static readonly string[] ScreenPermissions =
    {
        AuditRead,
        UserAdministration,
        Censo,
        Reportes,
        InventarioBiomedico,
        Farmacia,
        Aprobacion,
        AnalistaAsistencial,
        EspacioCorporativo,
        EspacioCorporativoAdmin
    };
}
