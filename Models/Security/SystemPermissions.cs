namespace IntranetPrueba.Models.Security;

public static class SystemPermissions
{
    public const string AuditRead = "AUDIT_READ";
    public const string UserAdministration = "SCREEN_USERS_ADMIN";
    public const string Censo = "SCREEN_CENSO";
    public const string Reportes = "SCREEN_REPORTES";
    public const string InventarioBiomedico = "SCREEN_INVENTARIO_BIOMEDICO";
    public const string Farmacia = "SCREEN_FARMACIA";

    public static readonly string[] ScreenPermissions =
    {
        AuditRead,
        UserAdministration,
        Censo,
        Reportes,
        InventarioBiomedico,
        Farmacia
    };
}
