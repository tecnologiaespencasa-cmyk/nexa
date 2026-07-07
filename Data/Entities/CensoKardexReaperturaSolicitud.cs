using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

public static class ReaperturaKardexTipoDocumento
{
    public const string KardexPrincipal = "KardexPrincipal";
    public const string ProrrogaBase = "ProrrogaBase";
    public const string ProrrogaVersion = "ProrrogaVersion";
}

public static class ReaperturaKardexEstado
{
    public const string Pendiente = "Pendiente";
    public const string Aprobada = "Aprobada";
    public const string Rechazada = "Rechazada";
}

public static class ReaperturaKardexMotivos
{
    public static readonly IReadOnlyList<string> Todos = new[]
    {
        "Error en kardex o Requisición",
        "Adición de tratamiento al ingreso",
        "Error en datos básicos",
        "Cambio de profesional asignado",
        "Cambio de dirección del paciente"
    };
}

public class CensoKardexReaperturaSolicitud
{
    [Key]
    public long Id { get; set; }

    public long CensoRecordId { get; set; }

    public CensoRecord CensoRecord { get; set; } = null!;

    /// <summary>
    /// Id de la versión de prórroga (CensoProrroga) cuando el documento es una prórroga versionada.
    /// </summary>
    public long? ProrrogaVersionId { get; set; }

    [Required]
    [StringLength(30)]
    public string TipoDocumento { get; set; } = ReaperturaKardexTipoDocumento.KardexPrincipal;

    [Required]
    [StringLength(80)]
    public string Motivo { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Estado { get; set; } = ReaperturaKardexEstado.Pendiente;

    public Guid SolicitadoPorUserId { get; set; }

    [Required]
    [StringLength(200)]
    public string SolicitadoPorNombre { get; set; } = string.Empty;

    public DateTime SolicitadoAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? ResueltoPorUserId { get; set; }

    [StringLength(200)]
    public string? ResueltoPorNombre { get; set; }

    public DateTime? ResueltoAtUtc { get; set; }

    [StringLength(500)]
    public string? ObservacionResolucion { get; set; }
}
