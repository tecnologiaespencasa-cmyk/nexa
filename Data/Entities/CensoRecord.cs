using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

public class CensoRecord
{
    [Key]
    public long Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Asegurador { get; set; } = string.Empty;

    public bool EsProrroga { get; set; }

    public DateTime FechaIngreso { get; set; }

    public TimeSpan HoraIngreso { get; set; }

    public DateTime FechaRespuesta { get; set; }

    public TimeSpan HoraRespuesta { get; set; }

    public int IndicadorTiempoRespuestaMinutos { get; set; }

    [Required]
    [StringLength(120)]
    public string NombrePerfilGestionaCaso { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string NombreRecepcionaCaso { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string NombreRealizaKardex { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string NombrePaciente { get; set; } = string.Empty;

    [Required]
    [StringLength(3)]
    public string TipoIdentificacion { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string NumeroIdentificacion { get; set; } = string.Empty;

    [Required]
    [StringLength(4)]
    public string CodigoCie10 { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string DiagnosticoDescriptivo { get; set; } = string.Empty;

    public DateTime FechaNacimiento { get; set; }

    public int Edad { get; set; }

    [Required]
    [StringLength(150)]
    public string CorreoElectronico { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string Direccion { get; set; } = string.Empty;

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
    [StringLength(2)]
    public string VistoBuenoRangoFueraAnexo { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string Telefono1 { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string Telefono2 { get; set; } = string.Empty;

    [StringLength(10)]
    public string? Telefono3 { get; set; }

    [Required]
    [StringLength(10)]
    public string ClasificacionRiesgo { get; set; } = string.Empty;

    [Required]
    [StringLength(2)]
    public string AdministracionMedicamentos { get; set; } = string.Empty;

    [StringLength(300)]
    public string? NombreMedicamentoPrincipalTratante { get; set; }

    public decimal? DosisMedicamentoPrincipal { get; set; }

    [StringLength(20)]
    public string? MedidaMedicamentoPrincipal { get; set; }

    [StringLength(30)]
    public string? ViaAdministracionMedicamentoPrincipal { get; set; }

    [Required]
    [StringLength(30)]
    public string FrecuenciaAdministracionMxPrincipal { get; set; } = string.Empty;

    public int? DiasMedicamentoPrincipal { get; set; }

    [Required]
    [StringLength(10)]
    public string NumeroDosisDiaMedicamentoPrincipal { get; set; } = string.Empty;

    [StringLength(300)]
    public string? NombreMedicamentoNumero2 { get; set; }

    public decimal? DosisMedicamento2 { get; set; }

    [StringLength(20)]
    public string? MedidaMedicamento2 { get; set; }

    [StringLength(30)]
    public string? ViaAdministracionMedicamento2 { get; set; }

    [StringLength(30)]
    public string? FrecuenciaAdministracionMedicamento2 { get; set; }

    public int? DiasMedicamento2 { get; set; }

    [StringLength(10)]
    public string? NumeroDosisMedicamento2 { get; set; }

    [StringLength(300)]
    public string? NombreMedicamentoNumero3 { get; set; }

    public decimal? DosisMedicamento3 { get; set; }

    [StringLength(20)]
    public string? MedidaMedicamento3 { get; set; }

    [StringLength(30)]
    public string? ViaAdministracionMedicamento3 { get; set; }

    [StringLength(30)]
    public string? FrecuenciaAdministracionMedicamento3 { get; set; }

    public int? DiasMedicamento3 { get; set; }

    [StringLength(10)]
    public string? NumeroDosisMedicamento3 { get; set; }

    [StringLength(50)]
    public string? AplicacionesTotales { get; set; }

    [StringLength(50)]
    public string? DiasTratamientoIv { get; set; }

    [StringLength(200)]
    public string? CambioFrecuenciaAdministracionTto { get; set; }

    [StringLength(100)]
    public string? FrecuenciaAjustada { get; set; }

    [StringLength(1)]
    public string? MedicamentoFrecuenciaAjustada { get; set; }

    public DateTime? FechaInicioTratamiento { get; set; }

    public DateTime? FechaFinTratamiento { get; set; }

    public DateTime? FechaPromesaInicioTto { get; set; }

    [StringLength(50)]
    public string? HoraPromesaInicioTto { get; set; }

    [StringLength(120)]
    public string? AuxiliarAsignado { get; set; }

    [StringLength(80)]
    public string? Estado { get; set; }

    [StringLength(100)]
    public string? AutorizacionEvento { get; set; }

    [StringLength(120)]
    public string? ResponsableLlamadaBienvenida { get; set; }

    [StringLength(20)]
    public string? EstadoLlamadaBienvenida { get; set; }

    [StringLength(2000)]
    public string? ObservacionesPlanManejo { get; set; }

    [StringLength(20)]
    public string? NumeroTelefonoLlamadaBienvenida { get; set; }

    [StringLength(50)]
    public string? NumeroDiasAutorizado { get; set; }

    [StringLength(2)]
    public string? RequiereServiciosComplementarios { get; set; }

    [StringLength(80)]
    public string? ServicioComplementario { get; set; }

    [StringLength(2)]
    public string? PacienteGestante { get; set; }

    [StringLength(2)]
    public string? Nebulizaciones { get; set; }

    [StringLength(2)]
    public string? SistemasPresionNegativaVac { get; set; }

    [StringLength(2)]
    public string? NutricionParenteral { get; set; }

    [StringLength(2)]
    public string? NutricionEnteral { get; set; }

    [StringLength(2)]
    public string? PacienteAnticoagulado { get; set; }

    [StringLength(2)]
    public string? LaboratorioClinicoProcedimiento { get; set; }

    [StringLength(2)]
    public string? ClinicaHeridas { get; set; }

    [StringLength(2)]
    public string? Aislamiento { get; set; }

    [StringLength(20)]
    public string? TipoAislamiento { get; set; }

    [StringLength(2)]
    public string? CateterismoOSv { get; set; }

    [StringLength(2)]
    public string? CateterPicc { get; set; }

    public int? NumeroCalibreSonda { get; set; }

    public DateTime? FechaUltimoCambioSonda { get; set; }

    [StringLength(120)]
    public string? AuxiliarAsignadoCateterismo { get; set; }

    public DateTime? FechaProximoCambioSonda { get; set; }

    public DateTime? FechaUltimaCuracionPicc { get; set; }

    public DateTime? FechaAlta { get; set; }

    [StringLength(200)]
    public string? NombreQuienGestionaAlta { get; set; }

    [StringLength(2)]
    public string? AltaTardia { get; set; }

    public DateTime? FechaPrimerSeguimiento24Horas { get; set; }

    public DateTime? FechaSegundoSeguimiento48Horas { get; set; }

    public DateTime? FechaTercerSeguimiento72Horas { get; set; }

    [StringLength(2000)]
    public string? ObservacionAltaTardia { get; set; }

    [StringLength(120)]
    public string? NombreQuienRealizaSeguimientoAltaTardia { get; set; }

    [StringLength(2)]
    public string? PacienteRehospitalizado { get; set; }

    public DateTime? FechaRegistroReporteRehospitalizacion { get; set; }

    public DateTime? FechaRehospitalizacion { get; set; }

    [StringLength(80)]
    public string? MotivoRehospitalizacion { get; set; }

    [StringLength(2000)]
    public string? AmpliacionMotivoRehospitalizacion { get; set; }

    [StringLength(50)]
    public string? RemitidoPorRehospitalizacion { get; set; }

    [StringLength(200)]
    public string? IpsIntramuralRehospitalizacion { get; set; }

    public DateTime? FechaPrimerSeguimientoRehospitalizacion { get; set; }

    public DateTime? FechaSegundoSeguimientoRehospitalizacion { get; set; }

    public DateTime? FechaTercerSeguimientoRehospitalizacion { get; set; }

    public DateTime? FechaAltaHospitalizacion { get; set; }

    [StringLength(2000)]
    public string? ObservacionRehospitalizacion { get; set; }

    public DateTime? FechaNovedadDevolucionProductos { get; set; }

    [StringLength(40)]
    public string? MotivoNovedadDevolucionProductos { get; set; }

    [StringLength(2)]
    public string? NotificacionAuxiliarDevolucionProductos { get; set; }

    public DateTime? FechaMaximaDevolucionProductos { get; set; }

    [StringLength(20)]
    public string? EstadoDevolucionServicioFarmaceutico { get; set; }

    [StringLength(2)]
    public string? PresentaNovedadKardex { get; set; }

    [StringLength(2)]
    public string? PresentaNovedadRequisicion { get; set; }

    [StringLength(2)]
    public string? PresentaNovedadAutorizacion { get; set; }

    [StringLength(2000)]
    public string? DescripcionNovedadDocumentosPaciente { get; set; }

    public DateTime? FechaReporteNovedadDocumentos { get; set; }

    public TimeSpan? HoraReporteNovedadDocumentos { get; set; }

    public TimeSpan? HoraGestionSolucionNovedadDocumentos { get; set; }

    public DateTime FechaGestionFarmacia { get; set; }

    public TimeSpan HoraGestionFarmacia { get; set; }

    public int IndicadorTiempoGestionMinutos { get; set; }

    [Required]
    [StringLength(20)]
    public string GestionCompletaPendiente { get; set; } = "Pendiente";

    public DateTime? FarmaciaEnviadoAtUtc { get; set; }

    public DateTime? FarmaciaKardexVistoAtUtc { get; set; }

    public DateTime? FarmaciaRequisicionVistoAtUtc { get; set; }

    public string? RequisicionFarmaciaJson { get; set; }

    public string? KardexEdicionJson { get; set; }

    [StringLength(160)]
    public string? FarmaciaNombreRecibe { get; set; }

    public string? FarmaciaFirmaEntregaDataUrl { get; set; }

    public string? FarmaciaFirmaRecibeDataUrl { get; set; }

    public DateTime? FarmaciaFechaHoraRecepcionUtc { get; set; }

    public DateTime? FarmaciaFirmaActualizadaAtUtc { get; set; }

    [StringLength(30)]
    public string FarmaciaEstado { get; set; } = "Nuevo";

    public bool FarmaciaOkKardex { get; set; }

    public bool? FarmaciaEsEntregaParcial { get; set; }

    public int? FarmaciaCantidadEntregas { get; set; }

    public int FarmaciaEntregaActual { get; set; } = 1;

    public bool FarmaciaFacturado { get; set; }

    public DateTime? FarmaciaEmpacadoAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CensoAdjunto> Adjuntos { get; set; } = [];
}
