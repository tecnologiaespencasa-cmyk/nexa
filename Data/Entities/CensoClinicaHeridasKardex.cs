using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

/// <summary>
/// Tipos de atención de clínica de heridas que generan requisición de insumos. Cada uno se activa
/// con su propio "Sí" en la sección 3 y produce un kardex independiente.
/// </summary>
public static class ClinicaHeridasKardexTipos
{
    public const string ManejoHerida = "MANEJO_HERIDA";
    public const string Vac = "VAC";
    public const string Npt = "NPT";
    public const string Picc = "PICC";

    public static readonly string[] Todos = [ManejoHerida, Vac, Npt, Picc];

    public static string Nombre(string tipo) => tipo switch
    {
        ManejoHerida => "Manejo de herida",
        Vac => "VAC",
        Npt => "NPT",
        Picc => "PICC",
        _ => tipo
    };

    public static bool EsValido(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) && Todos.Contains(tipo, StringComparer.Ordinal);
}

/// <summary>
/// Requisición de insumos de una atención de clínica de heridas. Un paciente tiene como máximo un
/// kardex por tipo; el contenido editable se guarda como JSON y el ciclo con farmacia replica el de
/// las agudizaciones de crónicos (bandeja propia, OK de farmacia y cierre).
/// </summary>
public class CensoClinicaHeridasKardex
{
    public long Id { get; set; }

    public long CensoClinicaHeridasRecordId { get; set; }

    public CensoClinicaHeridasRecord CensoClinicaHeridasRecord { get; set; } = null!;

    /// <summary>Plan de requisiciones al que pertenece. Un tipo de atención por plan.</summary>
    public long CensoClinicaHeridasPlanId { get; set; }

    public CensoClinicaHeridasPlan Plan { get; set; } = null!;

    [Required]
    [StringLength(20)]
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Contenido editado del kardex. Si es nulo se muestra el generado automáticamente.</summary>
    public string? KardexJson { get; set; }

    /// <summary>Perfil que abrió y guardó el kardex por última vez.</summary>
    [StringLength(200)]
    public string? ElaboradoPor { get; set; }

    public DateTime? FarmaciaEnviadoAtUtc { get; set; }

    [StringLength(30)]
    public string FarmaciaEstado { get; set; } = "Nuevo";

    public bool FarmaciaOkKardex { get; set; }

    public DateTime? FarmaciaKardexVistoAtUtc { get; set; }

    public DateTime? FarmaciaRequisicionVistoAtUtc { get; set; }

    // Resto del ciclo de despacho, igual que agudos y crónicos: entrega parcial, facturación,
    // empaque, firma de entrega/recibo y desempaque.
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

    // Control de los recordatorios mientras la bolsa espera en Empacado, igual que en agudos: evita
    // repetir el correo al auxiliar antes de 24 h y mandar dos veces el aviso a gerencia.
    public DateTime? FarmaciaNotifAuxiliarUltimaUtc { get; set; }

    public DateTime? FarmaciaNotif24hRestanteUtc { get; set; }

    /// <summary>Al darle OK farmacia, el kardex queda solo para consulta.</summary>
    public DateTime? KardexCerradoAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<CensoClinicaHeridasKardexAdjunto> Adjuntos { get; set; } = [];
}

/// <summary>Archivo que viaja con el kardex hacia farmacia.</summary>
public class CensoClinicaHeridasKardexAdjunto
{
    public long Id { get; set; }

    public long CensoClinicaHeridasKardexId { get; set; }

    public CensoClinicaHeridasKardex Kardex { get; set; } = null!;

    [Required]
    [StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public byte[] FileData { get; set; } = [];

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
