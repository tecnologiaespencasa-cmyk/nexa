using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

public class CensoClinicaHeridasRecord
{
    [Key]
    public long Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Asegurador { get; set; } = string.Empty;

    [StringLength(60)]
    public string? FuenteIngreso { get; set; }

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

    [StringLength(200)]
    public string? DetalleDireccion { get; set; }

    [StringLength(30)]
    public string? ClasificacionZonaSura { get; set; }

    [StringLength(120)]
    public string? MunicipioResidencia { get; set; }

    [StringLength(120)]
    public string? Barrio { get; set; }

    [StringLength(50)]
    public string? ZonaDireccionSegunMunicipio { get; set; }

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
    [StringLength(20)]
    public string ProgramaPertenece { get; set; } = string.Empty;

    [StringLength(120)]
    public string? AuxiliarEnfermeriaAsignado { get; set; }

    // PICC y VAC se capturan en la seccion 3 "Manejo de la herida", no al crear el registro, por eso
    // admiten nulo: un paciente recien registrado todavia no los tiene diligenciados.
    [StringLength(2)]
    public string? Picc { get; set; }

    [StringLength(2)]
    public string? Vac { get; set; }

    [StringLength(2)]
    public string? Npt { get; set; }

    [StringLength(2)]
    public string? ManejoHerida { get; set; }

    // Seccion 3 "Manejo de la herida": hasta cuatro apositos o medicamentos del catalogo de insumos.
    [StringLength(200)]
    public string? ApositoMedicamento1 { get; set; }

    [StringLength(200)]
    public string? ApositoMedicamento2 { get; set; }

    [StringLength(200)]
    public string? ApositoMedicamento3 { get; set; }

    [StringLength(200)]
    public string? ApositoMedicamento4 { get; set; }

    public int? DuracionTratamientoDias { get; set; }

    // Lista de frecuencias separadas por coma; el usuario puede marcar varias.
    [StringLength(160)]
    public string? FrecuenciaVisita { get; set; }

    // Histórico. La sección 2 dejó de capturar estos tres campos cuando pasó a mostrar los
    // seguimientos que registra la aplicación de clínica de heridas (origen, ubicación, medidas,
    // exudado y fotos vienen de allí). Las columnas se conservan con los datos ya capturados; nada
    // vuelve a escribirlas.
    [StringLength(2000)]
    public string? DescripcionHerida { get; set; }

    [StringLength(30)]
    public string? UbicacionHerida { get; set; }

    public int? FrecuenciaVisitasSemana { get; set; }

    [StringLength(2)]
    public string? EquipoComodato { get; set; }

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
