using System.ComponentModel.DataAnnotations;
using IntranetPrueba.Data.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IntranetPrueba.Models.ViewModels;

public class CensoReceptionViewModel
{
    [Required(ErrorMessage = "La fecha de ingreso es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha y hora de ingreso")]
    public DateTime FechaIngreso { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "La hora de ingreso es obligatoria.")]
    [DataType(DataType.Time)]
    [Display(Name = "Hora de ingreso")]
    public TimeSpan HoraIngreso { get; set; }

    [Required(ErrorMessage = "La fecha de respuesta es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha y hora de respuesta")]
    public DateTime FechaRespuesta { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "La hora de respuesta es obligatoria.")]
    [DataType(DataType.Time)]
    [Display(Name = "Hora de respuesta")]
    public TimeSpan HoraRespuesta { get; set; }

    [Required(ErrorMessage = "Selecciona quien recepciona el caso.")]
    [StringLength(120)]
    [Display(Name = "Nombre de quien recepciona el caso")]
    public string NombreRecepcionaCaso { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona quien realiza kardex.")]
    [StringLength(120)]
    [Display(Name = "Nombre de quien realiza kardex")]
    public string NombreRealizaKardex { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del paciente es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre del paciente no puede superar 200 caracteres.")]
    [Display(Name = "Nombre del paciente")]
    public string NombrePaciente { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona el tipo de identificación.")]
    [StringLength(3)]
    [Display(Name = "Tipo de identificación")]
    public string TipoIdentificacion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El número de identificación es obligatorio.")]
    [StringLength(20, ErrorMessage = "El número de identificación no puede superar 20 caracteres.")]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "El número de identificación solo permite dígitos.")]
    [Display(Name = "Número de identificación")]
    public string NumeroIdentificacion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El código CIE10 es obligatorio.")]
    [StringLength(4, ErrorMessage = "El código CIE10 debe tener 4 caracteres.")]
    [RegularExpression(@"^[A-Za-z][0-9]{3}$", ErrorMessage = "El código CIE10 debe iniciar con letra y continuar con 3 dígitos.")]
    [Display(Name = "Código CIE10")]
    public string CodigoCie10 { get; set; } = string.Empty;

    [Display(Name = "Diagnóstico descriptivo")]
    public string? DiagnosticoDescriptivo { get; set; }

    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de nacimiento")]
    public DateTime FechaNacimiento { get; set; } = DateTime.Today;

    [Display(Name = "Edad")]
    public int Edad { get; set; }

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [StringLength(150, ErrorMessage = "El correo electrónico no puede superar 150 caracteres.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo electrónico válido.")]
    [Display(Name = "Correo electrónico")]
    public string CorreoElectronico { get; set; } = string.Empty;

    [Required(ErrorMessage = "La dirección es obligatoria.")]
    [StringLength(300, ErrorMessage = "La dirección no puede superar 300 caracteres.")]
    [Display(Name = "Dirección")]
    public string Direccion { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "El detalle de dirección no puede superar 200 caracteres.")]
    [Display(Name = "Detalle de dirección")]
    public string? DetalleDireccion { get; set; }

    [Required(ErrorMessage = "Selecciona la clasificación zona Sura.")]
    [Display(Name = "Clasificación zona Sura")]
    public string ClasificacionZonaSura { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona el municipio de residencia.")]
    [Display(Name = "Municipio de residencia")]
    public string MunicipioResidencia { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona o escribe el barrio.")]
    [Display(Name = "Barrio")]
    public string Barrio { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona la zona de dirección según municipio.")]
    [Display(Name = "Zona de dirección según municipio")]
    public string ZonaDireccionSegunMunicipio { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona el area.")]
    [Display(Name = "Area")]
    public string Area { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona la IPS que remite.")]
    [Display(Name = "IPS que remite")]
    public string IpsQueRemite { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona el visto bueno rango fuera del anexo.")]
    [Display(Name = "Visto bueno rango fuera del anexo")]
    public string VistoBuenoRangoFueraAnexo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa al menos un teléfono.")]
    [StringLength(10, ErrorMessage = "El teléfono principal no puede superar 10 dígitos.")]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "El teléfono principal solo permite dígitos.")]
    [Display(Name = "Teléfono principal")]
    public string Telefono1 { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono adicional 1 es obligatorio.")]
    [StringLength(10, ErrorMessage = "El teléfono adicional 1 no puede superar 10 dígitos.")]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "El teléfono adicional 1 solo permite dígitos.")]
    [Display(Name = "Teléfono adicional 1")]
    public string Telefono2 { get; set; } = string.Empty;

    [StringLength(10, ErrorMessage = "El teléfono adicional 2 no puede superar 10 dígitos.")]
    [RegularExpression(@"^[0-9]*$", ErrorMessage = "El teléfono adicional 2 solo permite dígitos.")]
    [Display(Name = "Teléfono adicional 2")]
    public string? Telefono3 { get; set; }

    [Required(ErrorMessage = "Selecciona la clasificación de riesgo.")]
    [Display(Name = "Clasificación de riesgo")]
    public string ClasificacionRiesgo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona administración de medicamentos.")]
    [Display(Name = "Administración de medicamentos")]
    public string AdministracionMedicamentos { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "El nombre del medicamento principal no puede superar 300 caracteres.")]
    [Display(Name = "Nombre medicamento principal / Principio activo")]
    public string? NombreMedicamentoPrincipalTratante { get; set; }

    [Display(Name = "Dosis")]
    public decimal? DosisMedicamentoPrincipal { get; set; }

    [Display(Name = "Medida")]
    public string? MedidaMedicamentoPrincipal { get; set; }

    [Display(Name = "Vía de administración")]
    public string? ViaAdministracionMedicamentoPrincipal { get; set; }

    [Required(ErrorMessage = "Selecciona la frecuencia de administración MX principal.")]
    [Display(Name = "Frecuencia")]
    public string FrecuenciaAdministracionMxPrincipal { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa los días del medicamento principal.")]
    [Range(1, 999, ErrorMessage = "Los días deben estar entre 1 y 999.")]
    [Display(Name = "Días")]
    public int? DiasMedicamentoPrincipal { get; set; }

    [Required(ErrorMessage = "Selecciona el número de dosis día medicamento principal.")]
    [Display(Name = "Nro. de dosis día medicamento principal")]
    public string NumeroDosisDiaMedicamentoPrincipal { get; set; } = string.Empty;

    [Display(Name = "Tiene segundo medicamento")]
    public bool TieneSegundoMedicamento { get; set; }

    [StringLength(300, ErrorMessage = "El nombre del medicamento número 2 no puede superar 300 caracteres.")]
    [Display(Name = "Nombre medicamento número 2 / Principio activo")]
    public string? NombreMedicamentoNumero2 { get; set; }

    [Display(Name = "Dosis")]
    public decimal? DosisMedicamento2 { get; set; }

    [Display(Name = "Medida")]
    public string? MedidaMedicamento2 { get; set; }

    [Display(Name = "Vía de administración")]
    public string? ViaAdministracionMedicamento2 { get; set; }

    [Display(Name = "Frecuencia 2")]
    public string? FrecuenciaAdministracionMedicamento2 { get; set; }

    [Range(1, 999, ErrorMessage = "Los días deben estar entre 1 y 999.")]
    [Display(Name = "Días")]
    public int? DiasMedicamento2 { get; set; }

    [Display(Name = "Nro. de dosis medicamento 2")]
    public string? NumeroDosisMedicamento2 { get; set; }

    [Display(Name = "Tiene tercer medicamento")]
    public bool TieneTercerMedicamento { get; set; }

    [StringLength(300, ErrorMessage = "El nombre del medicamento número 3 no puede superar 300 caracteres.")]
    [Display(Name = "Nombre medicamento número 3 / Principio activo")]
    public string? NombreMedicamentoNumero3 { get; set; }

    [Display(Name = "Dosis")]
    public decimal? DosisMedicamento3 { get; set; }

    [Display(Name = "Medida")]
    public string? MedidaMedicamento3 { get; set; }

    [Display(Name = "Vía de administración")]
    public string? ViaAdministracionMedicamento3 { get; set; }

    [Display(Name = "Frecuencia 3")]
    public string? FrecuenciaAdministracionMedicamento3 { get; set; }

    [Range(1, 999, ErrorMessage = "Los días deben estar entre 1 y 999.")]
    [Display(Name = "Días")]
    public int? DiasMedicamento3 { get; set; }

    [Display(Name = "Nro. de dosis medicamento 3")]
    public string? NumeroDosisMedicamento3 { get; set; }

    [StringLength(50, ErrorMessage = "Las aplicaciones totales no pueden superar 50 caracteres.")]
    [Display(Name = "Aplicaciones totales")]
    public string? AplicacionesTotales { get; set; }

    [StringLength(50, ErrorMessage = "Los días de tratamiento IV no pueden superar 50 caracteres.")]
    [Display(Name = "Días de tratamiento IV")]
    public string? DiasTratamientoIv { get; set; }

    [StringLength(200, ErrorMessage = "El cambio de frecuencia no puede superar 200 caracteres.")]
    [Display(Name = "Se realizó cambio de frecuencia de administración de TTO")]
    public string? CambioFrecuenciaAdministracionTto { get; set; }

    [StringLength(100, ErrorMessage = "La frecuencia ajustada no puede superar 100 caracteres.")]
    [Display(Name = "Frecuencia ajustada")]
    public string? FrecuenciaAjustada { get; set; }

    [StringLength(1, ErrorMessage = "El medicamento ajustado no puede superar 1 caracter.")]
    [Display(Name = "Medicamento con frecuencia ajustada")]
    public string? MedicamentoFrecuenciaAjustada { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de inicio de tratamiento")]
    public DateTime? FechaInicioTratamiento { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha fin de tratamiento")]
    public DateTime? FechaFinTratamiento { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha promesa de inicio de TTO")]
    public DateTime? FechaPromesaInicioTto { get; set; }

    [StringLength(50, ErrorMessage = "La hora promesa de inicio de TTO no puede superar 50 caracteres.")]
    [Display(Name = "Hora promesa de inicio de TTO")]
    public string? HoraPromesaInicioTto { get; set; }

    [StringLength(120, ErrorMessage = "El auxiliar asignado no puede superar 120 caracteres.")]
    [Display(Name = "Auxiliar asignado")]
    public string? AuxiliarAsignado { get; set; }

    [Display(Name = "Estado")]
    public string? Estado { get; set; }

    [StringLength(100, ErrorMessage = "La autorización evento no puede superar 100 caracteres.")]
    [Display(Name = "Autorización evento")]
    public string? AutorizacionEvento { get; set; }

    [StringLength(120, ErrorMessage = "El responsable de llamada de bienvenida no puede superar 120 caracteres.")]
    [Display(Name = "Responsable llamada de bienvenida")]
    public string? ResponsableLlamadaBienvenida { get; set; }

    [Display(Name = "Estado de llamada de bienvenida")]
    public string? EstadoLlamadaBienvenida { get; set; }

    [StringLength(2000, ErrorMessage = "Las observaciones no pueden superar 2000 caracteres.")]
    [Display(Name = "Observaciones")]
    public string? ObservacionesPlanManejo { get; set; }

    [StringLength(10, ErrorMessage = "El número de teléfono no puede superar 10 caracteres.")]
    [RegularExpression(@"^\d{1,10}$", ErrorMessage = "El número de teléfono solo puede contener números.")]
    [Display(Name = "Número de teléfono al que se llama")]
    public string? NumeroTelefonoLlamadaBienvenida { get; set; }

    [StringLength(50, ErrorMessage = "El número de días autorizado no puede superar 50 caracteres.")]
    [Display(Name = "Número de días autorizado")]
    public string? NumeroDiasAutorizado { get; set; }

    [Display(Name = "Requiere servicios complementarios")]
    public string? RequiereServiciosComplementarios { get; set; }

    [StringLength(500, ErrorMessage = "Los servicios complementarios no pueden superar 500 caracteres.")]
    [Display(Name = "Servicio complementario")]
    public string? ServicioComplementario { get; set; }

    [Display(Name = "Paciente gestante")]
    public string? PacienteGestante { get; set; }

    [Display(Name = "Nebulizaciones")]
    public string? Nebulizaciones { get; set; }

    [Display(Name = "Sistemas de presión negativa VAC")]
    public string? SistemasPresionNegativaVac { get; set; }

    [Display(Name = "Nutrición parenteral")]
    public string? NutricionParenteral { get; set; }

    [Display(Name = "Nutrición enteral")]
    public string? NutricionEnteral { get; set; }

    [Display(Name = "Paciente anticoagulado")]
    public string? PacienteAnticoagulado { get; set; }

    [Display(Name = "Laboratorio clínico/Procedimiento")]
    public string? LaboratorioClinicoProcedimiento { get; set; }

    [Display(Name = "Clínica de heridas")]
    public string? ClinicaHeridas { get; set; }

    [Display(Name = "Aislamiento")]
    public string? Aislamiento { get; set; }

    [StringLength(20, ErrorMessage = "El tipo de aislamiento no puede superar 20 caracteres.")]
    [Display(Name = "Tipo de aislamiento")]
    public string? TipoAislamiento { get; set; }

    [Display(Name = "Cateterismo o SV")]
    public string? CateterismoOSv { get; set; }

    [Display(Name = "Cateter PICC")]
    public string? CateterPicc { get; set; }

    [Display(Name = "Número calibre de sonda")]
    public int? NumeroCalibreSonda { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de último cambio")]
    public DateTime? FechaUltimoCambioSonda { get; set; }

    [StringLength(120, ErrorMessage = "El auxiliar asignado para cateterismo no puede superar 120 caracteres.")]
    [Display(Name = "Auxiliar asignado")]
    public string? AuxiliarAsignadoCateterismo { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de próximo cambio")]
    public DateTime? FechaProximoCambioSonda { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de última curación")]
    public DateTime? FechaUltimaCuracionPicc { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de alta")]
    public DateTime? FechaAlta { get; set; }

    [StringLength(200, ErrorMessage = "El nombre de quien gestiona alta no puede superar 200 caracteres.")]
    [Display(Name = "Nombre de quien gestiona alta")]
    public string? NombreQuienGestionaAlta { get; set; }

    [Display(Name = "Alta tardía")]
    public string? AltaTardia { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de 1er seguimiento 24 horas")]
    public DateTime? FechaPrimerSeguimiento24Horas { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de 2do seguimiento 48 horas")]
    public DateTime? FechaSegundoSeguimiento48Horas { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de 3er seguimiento 72 horas")]
    public DateTime? FechaTercerSeguimiento72Horas { get; set; }

    [StringLength(2000, ErrorMessage = "La observación alta tardía no puede superar 2000 caracteres.")]
    [Display(Name = "Observación alta tardía")]
    public string? ObservacionAltaTardia { get; set; }

    [StringLength(120, ErrorMessage = "El nombre de quien realiza el seguimiento no puede superar 120 caracteres.")]
    [Display(Name = "Nombre de quien realiza el seguimiento")]
    public string? NombreQuienRealizaSeguimientoAltaTardia { get; set; }

    [Display(Name = "Paciente rehospitalizado")]
    public string? PacienteRehospitalizado { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de registro del reporte")]
    public DateTime? FechaRegistroReporteRehospitalizacion { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de rehospitalización")]
    public DateTime? FechaRehospitalizacion { get; set; }

    [Display(Name = "Motivo de la rehospitalización")]
    public string? MotivoRehospitalizacion { get; set; }

    [StringLength(2000, ErrorMessage = "La ampliación del motivo no puede superar 2000 caracteres.")]
    [Display(Name = "Ampliación del motivo")]
    public string? AmpliacionMotivoRehospitalizacion { get; set; }

    [Display(Name = "Remitido por")]
    public string? RemitidoPorRehospitalizacion { get; set; }

    [StringLength(200, ErrorMessage = "La IPS intramural no puede superar 200 caracteres.")]
    [Display(Name = "IPS intramural")]
    public string? IpsIntramuralRehospitalizacion { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de 1er seguimiento")]
    public DateTime? FechaPrimerSeguimientoRehospitalizacion { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de 2do seguimiento")]
    public DateTime? FechaSegundoSeguimientoRehospitalizacion { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de 3er seguimiento")]
    public DateTime? FechaTercerSeguimientoRehospitalizacion { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de alta de hospitalización")]
    public DateTime? FechaAltaHospitalizacion { get; set; }

    [StringLength(2000, ErrorMessage = "La observación de rehospitalización no puede superar 2000 caracteres.")]
    [Display(Name = "Observación rehospitalización")]
    public string? ObservacionRehospitalizacion { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de la novedad")]
    public DateTime? FechaNovedadDevolucionProductos { get; set; }

    [Display(Name = "Motivo de la novedad")]
    public string? MotivoNovedadDevolucionProductos { get; set; }

    [Display(Name = "Notificación al auxiliar")]
    public string? NotificacionAuxiliarDevolucionProductos { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha máxima de devolución")]
    public DateTime? FechaMaximaDevolucionProductos { get; set; }

    [Display(Name = "Estado de devolución - Diligencia el servicio farmacéutico")]
    public string? EstadoDevolucionServicioFarmaceutico { get; set; }

    [Display(Name = "Presenta novedad en kardex")]
    public string? PresentaNovedadKardex { get; set; }

    [Display(Name = "Presenta novedad en requisición")]
    public string? PresentaNovedadRequisicion { get; set; }

    [Display(Name = "Presenta novedad en la autorización")]
    public string? PresentaNovedadAutorizacion { get; set; }

    [StringLength(2000, ErrorMessage = "La descripción de la novedad no puede superar 2000 caracteres.")]
    [Display(Name = "Descripción de la novedad")]
    public string? DescripcionNovedadDocumentosPaciente { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de reporte de la novedad")]
    public DateTime? FechaReporteNovedadDocumentos { get; set; }

    [DataType(DataType.Time)]
    [Display(Name = "Hora de reporte de la novedad")]
    public TimeSpan? HoraReporteNovedadDocumentos { get; set; }

    [DataType(DataType.Time)]
    [Display(Name = "Hora de gestión y solución de la novedad")]
    public TimeSpan? HoraGestionSolucionNovedadDocumentos { get; set; }

    [StringLength(20)]
    [Display(Name = "Filtrar por cédula paciente")]
    public string? CedulaFiltro { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de ingreso desde")]
    public DateTime? FechaIngresoFiltroDesde { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de ingreso hasta")]
    public DateTime? FechaIngresoFiltroHasta { get; set; }

    public bool TieneFiltroFechaIngreso { get; set; }

    public int IngresosHoyCount { get; set; }

    public long? EditingRecordId { get; set; }

    public string? HoraPromesaInicioTtoDesde { get; set; }

    public string? HoraPromesaInicioTtoHasta { get; set; }

    public string? HoraPromesaInicioTtoMeridiano { get; set; }

    public bool AsumirDireccionErrada { get; set; }

    public string? DireccionSugerida { get; set; }

    public string? DireccionMensajeValidacion { get; set; }

    public bool DireccionEsValida { get; set; }

    [Display(Name = "Ordenamiento interno (Prórroga)")]
    public bool EsProrroga { get; set; }

    [StringLength(20, ErrorMessage = "El documento de prórroga no puede superar 20 caracteres.")]
    [RegularExpression(@"^[0-9]*$", ErrorMessage = "El documento de prórroga solo permite dígitos.")]
    [Display(Name = "Documento del paciente")]
    public string? DocumentoProrrogaBusqueda { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha y hora de gestión farmacia")]
    public DateTime? FechaGestionFarmacia { get; set; }

    [DataType(DataType.Time)]
    [Display(Name = "Hora de gestión farmacia")]
    public TimeSpan? HoraGestionFarmacia { get; set; }

    public bool GestionCompleta { get; set; }

    public IReadOnlyList<SelectListItem> NursingAssistantOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> OpsAssistantOptions { get; set; } = [];

    public IReadOnlyList<SelectListItem> TipoIdentificacionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ClasificacionZonaSuraOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> MunicipioResidenciaOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ZonaDireccionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> AreaOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> IpsQueRemiteOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> VistoBuenoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ClasificacionRiesgoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> AdministracionMedicamentosOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> CambioFrecuenciaAdministracionTtoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> FrecuenciaAjustadaOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> SiNoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> TipoAislamientoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ServicioComplementarioOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> EstadoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> EstadoLlamadaBienvenidaOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> MotivoRehospitalizacionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> RemitidoPorRehospitalizacionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> MotivoNovedadDevolucionProductosOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> EstadoDevolucionServicioFarmaceuticoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> MedidaMedicamentoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ViaAdministracionMedicamentoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> FrecuenciaAdministracionMxPrincipalOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> NumeroDosisDiaMedicamentoPrincipalOptions { get; set; } = [];
    public IReadOnlyList<string> MedicamentoPrincipalOptions { get; set; } = [];
    public IReadOnlyList<MedicamentoCatalogItemViewModel> MedicamentoCatalog { get; set; } = [];
    public IReadOnlyList<string> BarrioOptions { get; set; } = [];
    public IReadOnlyList<CensoListItemViewModel> CensoListItems { get; set; } = [];
    public IReadOnlyList<CensoRecord> CensoTableRecords { get; set; } = [];
    public IReadOnlyCollection<long> RecordIdsConAdjuntos { get; set; } = [];
}

public class MedicamentoCatalogItemViewModel
{
    public string Nombre { get; set; } = string.Empty;

    public string NormalizedNombre { get; set; } = string.Empty;

    public string? PresentacionRequisicion { get; set; }

    public string? ConcentracionMiligramos { get; set; }

    public string? Jeringa { get; set; }

    public string? SolucionParaDilucion { get; set; }

    public string? DilucionRecomendada { get; set; }

    public string? VehiculoReconstitucion { get; set; }

    public string? TiempoEstabilidad { get; set; }

    public string? TiempoInfusionMinutos { get; set; }

    public string? BombaInfusion { get; set; }

    public string? MarcacionRiesgo { get; set; }

    public string? Flebozantes { get; set; }

    public string? EquipoFotosensible { get; set; }

    public string? CadenaFrio { get; set; }
}

public class CensoListItemViewModel
{
    public long Id { get; set; }

    public DateTime FechaIngreso { get; set; }

    public TimeSpan HoraIngreso { get; set; }

    public string NombrePaciente { get; set; } = string.Empty;

    public string NumeroIdentificacion { get; set; } = string.Empty;

    public string CodigoCie10 { get; set; } = string.Empty;

    public string? Estado { get; set; }

    public string GestionCompletaPendiente { get; set; } = string.Empty;
}
