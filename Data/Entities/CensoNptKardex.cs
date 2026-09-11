using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

/// <summary>
/// Requisición de insumos de una atención de NPT.
///
/// Vivía en clínica de heridas como un tipo más de kardex, porque ahí estaba la maquinaria de
/// requisiciones. Se trajo al censo de NPT, que es su programa: un registro de NPT tiene a lo sumo
/// una requisición, y el ciclo con farmacia es el mismo de siempre (bandeja, OK de farmacia,
/// entrega parcial, facturación, empaque, firma y cierre).
///
/// A diferencia de heridas no hay "plan": aquel agrupa requisiciones de varios tipos y congela los
/// apósitos con los que se armó. NPT tiene una sola requisición, de lista fija, así que no hay nada
/// que agrupar ni que congelar.
/// </summary>
public class CensoNptKardex
{
    public long Id { get; set; }

    public long CensoNptRecordId { get; set; }

    public CensoNptRecord CensoNptRecord { get; set; } = null!;

    /// <summary>Contenido editado. Si es nulo se muestra el generado automáticamente.</summary>
    public string? KardexJson { get; set; }

    /// <summary>Perfil que abrió y guardó la requisición por última vez.</summary>
    [StringLength(200)]
    public string? ElaboradoPor { get; set; }

    public DateTime? FarmaciaEnviadoAtUtc { get; set; }

    [StringLength(30)]
    public string FarmaciaEstado { get; set; } = "Nuevo";

    public bool FarmaciaOkKardex { get; set; }

    public DateTime? FarmaciaKardexVistoAtUtc { get; set; }

    public DateTime? FarmaciaRequisicionVistoAtUtc { get; set; }

    // Resto del ciclo de despacho, igual que agudos, crónicos y heridas.
    public bool? FarmaciaEsEntregaParcial { get; set; }

    public int? FarmaciaCantidadEntregas { get; set; }

    public int FarmaciaEntregaActual { get; set; } = 1;

    public bool FarmaciaFacturado { get; set; }

    public DateTime? FarmaciaEmpacadoAtUtc { get; set; }

    public bool FarmaciaBolsaDesempacada { get; set; }

    [StringLength(200)]
    public string? FarmaciaNombreRecibe { get; set; }

    public string? FarmaciaFirmaEntregaDataUrl { get; set; }

    public string? FarmaciaFirmaRecibeDataUrl { get; set; }

    public DateTime? FarmaciaFechaHoraRecepcionUtc { get; set; }

    public DateTime? FarmaciaFirmaActualizadaAtUtc { get; set; }

    // Control de los recordatorios mientras la bolsa espera en Empacado.
    public DateTime? FarmaciaNotifAuxiliarUltimaUtc { get; set; }

    public DateTime? FarmaciaNotif24hRestanteUtc { get; set; }

    /// <summary>Al darle OK farmacia, la requisición queda solo para consulta.</summary>
    public DateTime? KardexCerradoAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<CensoNptKardexAdjunto> Adjuntos { get; set; } = [];
}

/// <summary>Archivo que viaja con la requisición de NPT hacia farmacia.</summary>
public class CensoNptKardexAdjunto
{
    public long Id { get; set; }

    public long CensoNptKardexId { get; set; }

    public CensoNptKardex Kardex { get; set; } = null!;

    [Required]
    [StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public byte[] FileData { get; set; } = [];

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
