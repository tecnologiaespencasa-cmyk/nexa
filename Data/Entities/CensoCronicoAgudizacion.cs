using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

public class CensoCronicoAgudizacion
{
    [Key]
    public long Id { get; set; }

    public long CensoCronicoRecordId { get; set; }

    public int Numero { get; set; }

    [Required]
    public string AgudizacionJson { get; set; } = string.Empty;

    // ----- Kardex y requisición (independientes del censo de agudos) -----
    public string? KardexEdicionJson { get; set; }

    public string? RequisicionFarmaciaJson { get; set; }

    public DateTime? FarmaciaEnviadoAtUtc { get; set; }

    [StringLength(30)]
    public string FarmaciaEstado { get; set; } = "Nuevo";

    public bool FarmaciaOkKardex { get; set; }

    public DateTime? FarmaciaKardexVistoAtUtc { get; set; }

    public DateTime? FarmaciaRequisicionVistoAtUtc { get; set; }

    public bool? FarmaciaEsEntregaParcial { get; set; }

    public int? FarmaciaCantidadEntregas { get; set; }

    public int FarmaciaEntregaActual { get; set; } = 1;

    public bool FarmaciaFacturado { get; set; }

    public DateTime? FarmaciaEmpacadoAtUtc { get; set; }

    public bool FarmaciaBolsaDesempacada { get; set; }

    [StringLength(160)]
    public string? FarmaciaNombreRecibe { get; set; }

    public string? FarmaciaFirmaEntregaDataUrl { get; set; }

    public string? FarmaciaFirmaRecibeDataUrl { get; set; }

    public DateTime? FarmaciaFechaHoraRecepcionUtc { get; set; }

    public DateTime? FarmaciaFirmaActualizadaAtUtc { get; set; }

    /// <summary>Cuando farmacia da OK al kardex, el documento queda cerrado (solo consulta).</summary>
    public DateTime? KardexCerradoAtUtc { get; set; }

    public bool TuvoReaperturaKardex { get; set; }

    [StringLength(200)]
    public string? ReaperturaSolicitadaPor { get; set; }

    [StringLength(200)]
    public string? ReaperturaAprobadaPor { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public CensoCronicoRecord CensoCronicoRecord { get; set; } = null!;

    public ICollection<CensoCronicoKardexReapertura> Reaperturas { get; set; } = new List<CensoCronicoKardexReapertura>();
}
