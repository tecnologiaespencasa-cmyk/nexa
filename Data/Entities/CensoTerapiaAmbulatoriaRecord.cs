using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

public class CensoTerapiaAmbulatoriaRecord
{
    [Key]
    public long Id { get; set; }

    [Required]
    [StringLength(200)]
    public string NombrePaciente { get; set; } = string.Empty;

    [Required]
    [StringLength(3)]
    public string TipoIdentificacion { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string NumeroIdentificacion { get; set; } = string.Empty;

    public DateTime FechaNacimiento { get; set; }

    public int Edad { get; set; }

    [Required]
    [StringLength(150)]
    public string CorreoElectronico { get; set; } = string.Empty;

    public int Cantidad { get; set; }

    [Required]
    [StringLength(200)]
    public string FrecuenciaTerapia { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string TipoTerapia { get; set; } = string.Empty;

    [Required]
    [StringLength(4)]
    public string CodigoCie10 { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string DiagnosticoDescriptivo { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string NumeroAutorizacion { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string Direccion { get; set; } = string.Empty;

    public bool DireccionValidada { get; set; }

    public bool AsumirDireccionErrada { get; set; }

    [StringLength(200)]
    public string? DetalleDireccion { get; set; }

    [Required]
    [StringLength(30)]
    public string ClasificacionZonaSura { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string MunicipioResidencia { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string Barrio { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string ZonaDireccionSegunMunicipio { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string Area { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string IpsQueRemite { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string TelefonoPrincipal { get; set; } = string.Empty;

    [StringLength(10)]
    public string? TelefonoAdicional1 { get; set; }

    [StringLength(10)]
    public string? TelefonoAdicional2 { get; set; }

    [StringLength(120)]
    public string Fisioterapeuta { get; set; } = string.Empty;

    public bool GestionEnSistema { get; set; }

    [Required]
    [StringLength(30)]
    public string EstadoGestion { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string EstadoPaciente { get; set; } = string.Empty;

    public DateTime FechaIngreso { get; set; }

    public DateTime FechaInicio { get; set; }

    public DateTime? FechaFin { get; set; }

    public DateTime? FechaAlta { get; set; }

    [StringLength(80)]
    public string? MotivoAlta { get; set; }

    [StringLength(30)]
    public string EstadoAlta { get; set; } = "Activo";

    public DateTime? AltaNotificacionEnviadaAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<CensoTerapiaAmbulatoriaProrroga> Prorrogas { get; set; } = [];
}
