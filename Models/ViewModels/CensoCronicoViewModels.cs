using System.ComponentModel.DataAnnotations;
using IntranetPrueba.Data.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IntranetPrueba.Models.ViewModels;

public class CensoCronicoViewModel
{
    public long? EditingRecordId { get; set; }

    public string? CedulaFiltro { get; set; }

    /// <summary>Días entre la fecha de ingreso y el día actual (Colombia). Se calcula en vivo, no se persiste.</summary>
    public int DiasDeEstancia { get; set; }

    // ----- Sección 1: Datos básicos -----
    [Required(ErrorMessage = "Selecciona la fuente de ingreso.")]
    [Display(Name = "Fuente de ingreso")]
    public string FuenteIngreso { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de ingreso es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de ingreso")]
    public DateTime FechaIngreso { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Selecciona el tipo de identificación.")]
    [StringLength(3)]
    [Display(Name = "Tipo de identificación")]
    public string TipoIdentificacion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El número de documento es obligatorio.")]
    [StringLength(20, ErrorMessage = "El número de documento no puede superar 20 caracteres.")]
    [Display(Name = "Número de identificación")]
    public string NumeroIdentificacion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del paciente es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre del paciente no puede superar 200 caracteres.")]
    [Display(Name = "Nombre del paciente")]
    public string NombrePaciente { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de nacimiento")]
    public DateTime FechaNacimiento { get; set; } = DateTime.Today;

    [Display(Name = "Edad")]
    public int Edad { get; set; }

    [StringLength(150, ErrorMessage = "El correo electrónico no puede superar 150 caracteres.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo electrónico válido.")]
    [Display(Name = "Correo electrónico")]
    public string? CorreoElectronico { get; set; }

    [Required(ErrorMessage = "Selecciona el género.")]
    [Display(Name = "Género")]
    public string Genero { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "La dirección no puede superar 300 caracteres.")]
    [Display(Name = "Dirección")]
    public string? Direccion { get; set; }

    [StringLength(200, ErrorMessage = "El detalle de dirección no puede superar 200 caracteres.")]
    [Display(Name = "Detalle de la dirección")]
    public string? DetalleDireccion { get; set; }

    [Display(Name = "Clasificación zona Sura")]
    public string? ClasificacionZonaSura { get; set; }

    [Display(Name = "Municipio de residencia")]
    public string? MunicipioResidencia { get; set; }

    [Display(Name = "Barrio")]
    public string? Barrio { get; set; }

    [Display(Name = "Zona de dirección según municipio")]
    public string? ZonaDireccionSegunMunicipio { get; set; }

    [Display(Name = "Area")]
    public string? Area { get; set; }

    // ----- Sección 2: Gestión del caso -----
    [Display(Name = "Clasificación del caso")]
    public string? ClasificacionCaso { get; set; }

    [Display(Name = "Estado del paciente")]
    public string? EstadoPaciente { get; set; }

    [StringLength(4, ErrorMessage = "El diagnóstico crónico CIE10 debe tener 4 caracteres.")]
    [Display(Name = "Diagnóstico crónico CIE10")]
    public string? DiagnosticoCronicoCie10 { get; set; }

    [Display(Name = "Grupo de patología crónica")]
    public string? GrupoPatologiaCronica { get; set; }

    [StringLength(4, ErrorMessage = "El diagnóstico crónico complementario debe tener 4 caracteres.")]
    [Display(Name = "Diagnóstico crónico complementario")]
    public string? DiagnosticoCronicoComplementario { get; set; }

    [Display(Name = "Grupo de patología crónica complementario")]
    public string? GrupoPatologiaCronicaComplementario { get; set; }

    [Display(Name = "Barthel auditado")]
    public string? BarthelAuditado { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de auditoría")]
    public DateTime? FechaAuditoria { get; set; }

    [StringLength(50)]
    [Display(Name = "Calificación Barthel")]
    public string? CalificacionBarthel { get; set; }

    [StringLength(50)]
    [Display(Name = "Karnofsky (cáncer)")]
    public string? Karnofsky { get; set; }

    [StringLength(50)]
    [Display(Name = "Fast (demencia)")]
    public string? Fast { get; set; }

    [StringLength(50)]
    [Display(Name = "Rankin (post ACV)")]
    public string? Rankin { get; set; }

    [StringLength(50)]
    [Display(Name = "Disnea Mmrc (EPOC)")]
    public string? DisneaMmrc { get; set; }

    [StringLength(50)]
    [Display(Name = "Nyha (falla cardíaca)")]
    public string? Nyha { get; set; }

    [StringLength(50)]
    [Display(Name = "Braden")]
    public string? Braden { get; set; }

    [StringLength(50)]
    [Display(Name = "Riesgo de caída")]
    public string? RiesgoCaida { get; set; }

    [StringLength(50)]
    [Display(Name = "Riesgo de lesión de piel")]
    public string? RiesgoLesionPiel { get; set; }

    // ----- Sección 3: Validaciones -----
    [Display(Name = "Clínica de heridas")]
    public string? ClinicaHeridas { get; set; }

    [Display(Name = "Estado en clínica de heridas")]
    public string? EstadoClinicaHeridas { get; set; }

    [Display(Name = "Programa de nutrición (NE/NPT)")]
    public string? ProgramaNutricion { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de inicio")]
    public DateTime? FechaInicioNutricion { get; set; }

    [StringLength(120, ErrorMessage = "El auxiliar asignado no puede superar 120 caracteres.")]
    [Display(Name = "Auxiliar asignado")]
    public string? AuxiliarAsignadoNutricion { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha fin nutrición")]
    public DateTime? FechaFinNutricion { get; set; }

    [Display(Name = "Educación y plan de cuidados / enfermería")]
    public string? EducacionPlanCuidados { get; set; }

    [Display(Name = "Terapia física")]
    public string? TerapiaFisica { get; set; }

    [Display(Name = "Terapia respiratoria")]
    public string? TerapiaRespiratoria { get; set; }

    [Display(Name = "Terapia ocupacional")]
    public string? TerapiaOcupacional { get; set; }

    [Display(Name = "Fonoaudiología")]
    public string? Fonoaudiologia { get; set; }

    [Display(Name = "Nutrición")]
    public string? Nutricion { get; set; }

    [Display(Name = "Psicología")]
    public string? Psicologia { get; set; }

    [Display(Name = "Traqueostomía")]
    public string? Traqueostomia { get; set; }

    [Display(Name = "Sonda nasogástrica")]
    public string? SondaNasogastrica { get; set; }

    [StringLength(50)]
    [Display(Name = "Calibre de la sonda nasogástrica")]
    public string? CalibreSondaNasogastrica { get; set; }

    [StringLength(50)]
    [Display(Name = "Frecuencia de cambio de sonda nasogástrica")]
    public string? FrecuenciaCambioSondaNasogastrica { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de último cambio")]
    public DateTime? FechaUltimoCambioSondaNasogastrica { get; set; }

    [Display(Name = "Sonda gastrostomía")]
    public string? SondaGastrostomia { get; set; }

    [Display(Name = "Colostomía")]
    public string? Colostomia { get; set; }

    [Display(Name = "Sonda cistostomía")]
    public string? SondaCistostomia { get; set; }

    [Display(Name = "Catéter PICC")]
    public string? CateterPicc { get; set; }

    [Display(Name = "Sonda vesical")]
    public string? SondaVesical { get; set; }

    [StringLength(50)]
    [Display(Name = "Calibre de sonda")]
    public string? CalibreSondaVesical { get; set; }

    [StringLength(50)]
    [RegularExpression(@"^[0-9]*$", ErrorMessage = "La frecuencia de cambio solo permite números.")]
    [Display(Name = "Frecuencia de cambio (días)")]
    public string? FrecuenciaCambioSondaVesical { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de último cambio")]
    public DateTime? FechaUltimoCambioSondaVesical { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de próximo cambio")]
    public DateTime? FechaProximoCambioSondaVesical { get; set; }

    [StringLength(500)]
    [Display(Name = "Observación específica del cambio de la sonda si aplica")]
    public string? ObservacionCambioSonda { get; set; }

    [Display(Name = "Fórmula de control")]
    public string? FormulaControl { get; set; }

    [Display(Name = "Mipres pañales")]
    public string? MipresPanales { get; set; }

    [Display(Name = "Talla")]
    public string? TallaPanales { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha última prescripción")]
    public DateTime? FechaUltimaPrescripcionPanales { get; set; }

    [Display(Name = "Tiempo de prescripción (en meses)")]
    public int? TiempoPrescripcionPanalesMeses { get; set; }

    [Display(Name = "Estado Mipres")]
    public string? EstadoMipresPanales { get; set; }

    [Display(Name = "Mipres nutrición")]
    public string? MipresNutricion { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha última prescripción")]
    public DateTime? FechaUltimaPrescripcionNutricion { get; set; }

    [Display(Name = "Tiempo de prescripción (en meses)")]
    public int? TiempoPrescripcionNutricionMeses { get; set; }

    [Display(Name = "Estado Mipres")]
    public string? EstadoMipresNutricion { get; set; }

    // ----- Sección 4: Hospitalización y seguimiento -----
    // Los episodios (fecha, motivo, IPS, CIE10, 7 seguimientos con observación, alta, etc.)
    // son multi-registro y se guardan como JSON en la colección Hospitalizaciones.
    // Aquí solo queda el egreso del programa (evento a nivel del paciente).
    [Display(Name = "Egresa programa crónico")]
    public string? EgresaProgramaCronico { get; set; }

    [Display(Name = "Motivo egreso")]
    public string? MotivoEgreso { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de egreso")]
    public DateTime? FechaEgreso { get; set; }

    // ----- Estado dirección -----
    public bool AsumirDireccionErrada { get; set; }

    public string? DireccionSugerida { get; set; }

    public string? DireccionMensajeValidacion { get; set; }

    public bool DireccionEsValida { get; set; }

    // ----- Opciones -----
    public IReadOnlyList<SelectListItem> FuenteIngresoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> TipoIdentificacionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> GeneroOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ClasificacionZonaSuraOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> MunicipioResidenciaOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ZonaDireccionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> AreaOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ClasificacionCasoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> EstadoPacienteOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> BarthelAuditadoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> CalificacionBarthelOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> KarnofskyOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> FastOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> RankinOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> DisneaMmrcOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> NyhaOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> SiNoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> EstadoClinicaHeridasOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> CalibreSondaVesicalOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> AuxiliarEnfermeriaOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> TallaPanalesOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> EstadoMipresOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> MotivoEgresoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> MedidaMedicamentoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ViaAdministracionMedicamentoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> FrecuenciaAdministracionOptions { get; set; } = [];
    public IReadOnlyList<string> MedicamentoPrincipalOptions { get; set; } = [];
    public IReadOnlyList<string> BarrioOptions { get; set; } = [];
    public IReadOnlyList<CensoCronicoRecord> UltimosRegistros { get; set; } = [];
    public IReadOnlyList<CensoCronicoAgudizacion> Agudizaciones { get; set; } = [];
    public IReadOnlyList<CensoCronicoHospitalizacion> Hospitalizaciones { get; set; } = [];

    // ----- Kardex y requisición de agudizaciones -----
    public IReadOnlyList<MedicamentoCatalogItemViewModel> MedicamentoCatalog { get; set; } = [];
    public bool PuedeAprobarReapertura { get; set; }
    public IReadOnlyList<string> ReaperturaMotivos { get; set; } = [];
}
