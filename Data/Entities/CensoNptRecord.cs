using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

public class CensoNptRecord
{
    [Key]
    public long Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Asegurador { get; set; } = string.Empty;

    public DateTime FechaIngresoPrograma { get; set; }

    [Required]
    [StringLength(3)]
    public string TipoIdentificacion { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string NumeroIdentificacion { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string NombrePaciente { get; set; } = string.Empty;

    public DateTime FechaNacimiento { get; set; }

    public int Edad { get; set; }

    [Required]
    [StringLength(20)]
    public string Genero { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Direccion { get; set; }

    public bool DireccionValidada { get; set; }

    public bool AsumirDireccionErrada { get; set; }

    [StringLength(120)]
    public string? Barrio { get; set; }

    [StringLength(120)]
    public string? MunicipioResidencia { get; set; }

    [StringLength(50)]
    public string? ZonaDireccionSegunMunicipio { get; set; }

    [StringLength(30)]
    public string? ClasificacionZonaSura { get; set; }

    [Required]
    [StringLength(10)]
    public string TelefonoPrincipal { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string TelefonoAdicional1 { get; set; } = string.Empty;

    [StringLength(10)]
    public string? TelefonoAdicional2 { get; set; }

    [StringLength(20)]
    public string? LlamadaBienvenida { get; set; }

    [StringLength(10)]
    public string? TelefonoContacto { get; set; }

    [StringLength(2000)]
    public string? Observacion { get; set; }

    [Required]
    [StringLength(4)]
    public string CodigoCie10 { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string DiagnosticoDescriptivo { get; set; } = string.Empty;

    public DateTime FechaValoracion { get; set; }

    [Required]
    [StringLength(40)]
    public string ProgramaPertenece { get; set; } = string.Empty;

    [StringLength(120)]
    public string? AuxiliarEnfermeriaAsignado { get; set; }

    [Required]
    [StringLength(20)]
    public string TipoNutricion { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string TipoSonda { get; set; } = string.Empty;

    [Required]
    [StringLength(2)]
    public string Picc { get; set; } = string.Empty;

    public DateTime? FechaUltimaCuracionPicc { get; set; }

    public DateTime? FechaInicioNpt { get; set; }

    public DateTime? FechaFinNpt { get; set; }

    public int? DiasTratamiento { get; set; }

    public TimeSpan? HoraConexion { get; set; }

    public TimeSpan? HoraDesconexion { get; set; }

    [StringLength(2)]
    public string? CargueLaboratorios { get; set; }

    [StringLength(2)]
    public string? CargueGlucometria { get; set; }

    [StringLength(2)]
    public string? CargueServiciosComplementarios { get; set; }

    [StringLength(2)]
    public string? CargueSeguimientoMedico { get; set; }

    [StringLength(2)]
    public string? EquipoComodato { get; set; }

    [StringLength(200)]
    public string? DescripcionEquipo { get; set; }

    [StringLength(100)]
    public string? NumeroPlacaEquipos { get; set; }

    public DateTime? FechaEntregaEquipo { get; set; }

    public DateTime? FechaDevolucionEquipo { get; set; }

    public DateTime? FechaHospitalizacion { get; set; }

    [StringLength(60)]
    public string? MotivoHospitalizacion { get; set; }

    [StringLength(30)]
    public string? RemitidoPorHospitalizacion { get; set; }

    [StringLength(200)]
    public string? IpsIntramural { get; set; }

    public DateTime? FechaPrimerSeguimiento24Horas { get; set; }

    public DateTime? FechaSegundoSeguimiento48Horas { get; set; }

    public DateTime? FechaTercerSeguimiento72Horas { get; set; }

    public DateTime? FechaCuartoSeguimientoSemana1 { get; set; }

    public DateTime? FechaQuintoSeguimientoSemana2 { get; set; }

    public DateTime? FechaSextoSeguimientoSemana3 { get; set; }

    public DateTime? FechaSeptimoSeguimientoSemana4 { get; set; }

    public DateTime? FechaAltaHospitalizacion { get; set; }

    public DateTime? FechaNovedadDevolucionProductos { get; set; }

    [StringLength(40)]
    public string? MotivoNovedadDevolucionProductos { get; set; }

    [StringLength(2)]
    public string? NotificacionAuxiliarDevolucionProductos { get; set; }

    public DateTime? FechaMaximaDevolucionProductos { get; set; }

    [StringLength(20)]
    public string? EstadoDevolucionServicioFarmaceutico { get; set; }

    [StringLength(60)]
    public string? MotivoEgreso { get; set; }

    public DateTime? FechaEgreso { get; set; }

    [StringLength(20)]
    public string? Estado { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
