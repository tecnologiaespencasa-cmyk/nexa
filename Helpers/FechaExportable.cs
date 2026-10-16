using System.Globalization;

namespace Nexa.Helpers;

/// <summary>
/// Formato de las fechas en los exportables: día/mes/año en todos los archivos, con la hora solo
/// cuando el dato la trae. Vive en un solo lugar para que ningún exportable vuelva a salir con otro
/// formato (hasta 2026-10-15 convivían "dd/MM/yyyy" y "yyyy-MM-dd" según el programa).
///
/// No aplica a los nombres de archivo (van en yyyyMMdd para que ordenen solos) ni a las columnas que
/// se escriben como fecha de Excel, que ya salen en dd/mm/aaaa por su formato de celda.
/// </summary>
public static class FechaExportable
{
    public const string Formato = "dd/MM/yyyy";

    public const string FormatoConHora = "dd/MM/yyyy HH:mm:ss";

    public static string Fecha(DateTime valor) =>
        valor.ToString(Formato, CultureInfo.InvariantCulture);

    public static string Fecha(DateTime? valor) =>
        valor.HasValue ? Fecha(valor.Value) : string.Empty;

    public static string FechaHora(DateTime valor) =>
        valor.ToString(FormatoConHora, CultureInfo.InvariantCulture);

    public static string FechaHora(DateTime? valor) =>
        valor.HasValue ? FechaHora(valor.Value) : string.Empty;

    /// <summary>
    /// Excel no sabe de fechas anteriores a 1900: una celda con fecha fuera de ese rango daña el
    /// archivo entero y no deja abrirlo. Los años mal digitados (0026, 0202, 0959… en el censo de
    /// agudos) se escriben como texto, para que el archivo abra y el error siga a la vista.
    /// </summary>
    public static bool CabeEnExcel(DateTime valor) => valor.Year >= 1900;

    /// <summary>
    /// La fecha sola cuando el dato no trae hora (fecha de ingreso, de egreso, de nacimiento) y con
    /// hora cuando sí la trae (los sellos de creación y actualización).
    /// </summary>
    public static string FechaOFechaHora(DateTime valor) =>
        valor.TimeOfDay == TimeSpan.Zero ? Fecha(valor) : FechaHora(valor);
}
