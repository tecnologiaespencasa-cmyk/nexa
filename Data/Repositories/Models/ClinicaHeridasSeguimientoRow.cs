namespace Nexa.Data.Repositories.Models;

/// <summary>
/// Un seguimiento de herida capturado en la aplicación del Portal Administrativo
/// (base de datos Neon, tabla "ClinicaHeridas"). La intranet solo lee estos datos.
/// </summary>
public class ClinicaHeridasSeguimientoRow
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Número consecutivo del seguimiento dentro del paciente (1, 2, 3…).</summary>
    public int Numero { get; set; }

    /// <summary>Momento en que el auxiliar registró el seguimiento. Se guarda en UTC.</summary>
    public DateTime CreatedAtUtc { get; set; }

    public string Origen { get; set; } = string.Empty;

    public string Ubicacion { get; set; } = string.Empty;

    public double DiametroVerticalCm { get; set; }

    public double DiametroHorizontalCm { get; set; }

    public double ProfundidadCm { get; set; }

    public string Fondo { get; set; } = string.Empty;

    public string Lecho { get; set; } = string.Empty;

    public string Tejido { get; set; } = string.Empty;

    public string ExudadoCantidad { get; set; } = string.Empty;

    public string ExudadoCaracteristicas { get; set; } = string.Empty;

    /// <summary>Carpeta de SharePoint donde el portal guardó las fotos de este seguimiento.</summary>
    public string? CarpetaDriveItemId { get; set; }

    public string AuxiliarNombre { get; set; } = string.Empty;

    public string AuxiliarCedula { get; set; } = string.Empty;

    public string AuxiliarEmail { get; set; } = string.Empty;

    public string AuxiliarProfesion { get; set; } = string.Empty;

    public IReadOnlyList<ClinicaHeridasFotoRow> Fotos { get; set; } = [];
}

/// <summary>
/// Foto de un seguimiento. El archivo vive en SharePoint; Neon solo guarda su identificador.
/// </summary>
public class ClinicaHeridasFotoRow
{
    /// <summary>Valor del enum TipoFotoHerida: PLANO_GENERAL, MEDIDA_VERTICAL, MEDIDA_HORIZONTAL o LATERAL.</summary>
    public string Tipo { get; set; } = string.Empty;

    public string DriveItemId { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string? MimeType { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
