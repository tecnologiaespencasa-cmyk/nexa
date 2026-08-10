using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

public class CensoCronicoRecord
{
    [Key]
    public long Id { get; set; }

    // ----- Sección 1: Datos básicos -----
    [Required]
    [StringLength(30)]
    public string FuenteIngreso { get; set; } = string.Empty;

    public DateTime FechaIngreso { get; set; }

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

    [StringLength(150)]
    public string? CorreoElectronico { get; set; }

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

    [StringLength(10)]
    public string? Area { get; set; }

    // ----- Sección 2: Gestión del caso -----
    [StringLength(20)]
    public string? ClasificacionCaso { get; set; }

    [StringLength(30)]
    public string? EstadoPaciente { get; set; }

    [StringLength(4)]
    public string? DiagnosticoCronicoCie10 { get; set; }

    [StringLength(300)]
    public string? GrupoPatologiaCronica { get; set; }

    [StringLength(4)]
    public string? DiagnosticoCronicoComplementario { get; set; }

    [StringLength(300)]
    public string? GrupoPatologiaCronicaComplementario { get; set; }

    [StringLength(10)]
    public string? BarthelAuditado { get; set; }

    public DateTime? FechaAuditoria { get; set; }

    [StringLength(50)]
    public string? CalificacionBarthel { get; set; }

    [StringLength(50)]
    public string? Karnofsky { get; set; }

    [StringLength(50)]
    public string? Fast { get; set; }

    [StringLength(50)]
    public string? Rankin { get; set; }

    [StringLength(50)]
    public string? DisneaMmrc { get; set; }

    [StringLength(50)]
    public string? Nyha { get; set; }

    [StringLength(50)]
    public string? Braden { get; set; }

    [StringLength(50)]
    public string? RiesgoCaida { get; set; }

    [StringLength(50)]
    public string? RiesgoLesionPiel { get; set; }

    // ----- Sección 3: Validaciones -----
    [StringLength(2)]
    public string? ClinicaHeridas { get; set; }

    [StringLength(20)]
    public string? EstadoClinicaHeridas { get; set; }

    [StringLength(2)]
    public string? ProgramaNutricion { get; set; }

    public DateTime? FechaInicioNutricion { get; set; }

    [StringLength(120)]
    public string? AuxiliarAsignadoNutricion { get; set; }

    public DateTime? FechaFinNutricion { get; set; }

    [StringLength(2)]
    public string? EducacionPlanCuidados { get; set; }

    [StringLength(2)]
    public string? TerapiaFisica { get; set; }

    [StringLength(2)]
    public string? TerapiaRespiratoria { get; set; }

    [StringLength(2)]
    public string? TerapiaOcupacional { get; set; }

    [StringLength(2)]
    public string? Fonoaudiologia { get; set; }

    [StringLength(2)]
    public string? Nutricion { get; set; }

    [StringLength(2)]
    public string? Psicologia { get; set; }

    [StringLength(2)]
    public string? Traqueostomia { get; set; }

    [StringLength(2)]
    public string? SondaNasogastrica { get; set; }

    [StringLength(50)]
    public string? CalibreSondaNasogastrica { get; set; }

    [StringLength(50)]
    public string? FrecuenciaCambioSondaNasogastrica { get; set; }

    public DateTime? FechaUltimoCambioSondaNasogastrica { get; set; }

    [StringLength(2)]
    public string? SondaGastrostomia { get; set; }

    [StringLength(2)]
    public string? Colostomia { get; set; }

    [StringLength(2)]
    public string? SondaCistostomia { get; set; }

    [StringLength(2)]
    public string? CateterPicc { get; set; }

    [StringLength(2)]
    public string? SondaVesical { get; set; }

    [StringLength(50)]
    public string? CalibreSondaVesical { get; set; }

    [StringLength(50)]
    public string? FrecuenciaCambioSondaVesical { get; set; }

    public DateTime? FechaUltimoCambioSondaVesical { get; set; }

    public DateTime? FechaProximoCambioSondaVesical { get; set; }

    [StringLength(500)]
    public string? ObservacionCambioSonda { get; set; }

    [StringLength(2)]
    public string? FormulaControl { get; set; }

    [StringLength(2)]
    public string? MipresPanales { get; set; }

    [StringLength(5)]
    public string? TallaPanales { get; set; }

    public DateTime? FechaUltimaPrescripcionPanales { get; set; }

    public int? TiempoPrescripcionPanalesMeses { get; set; }

    [StringLength(20)]
    public string? EstadoMipresPanales { get; set; }

    [StringLength(2)]
    public string? MipresNutricion { get; set; }

    public DateTime? FechaUltimaPrescripcionNutricion { get; set; }

    public int? TiempoPrescripcionNutricionMeses { get; set; }

    [StringLength(20)]
    public string? EstadoMipresNutricion { get; set; }

    // ----- Sección 4: Hospitalización y seguimiento -----
    // Los episodios de hospitalización + seguimiento son multi-registro y viven en
    // censo_cronico_hospitalizaciones (JSON). Aquí solo queda el egreso del programa,
    // que es un evento a nivel del paciente y deriva el EstadoPaciente.
    [StringLength(2)]
    public string? EgresaProgramaCronico { get; set; }

    [StringLength(40)]
    public string? MotivoEgreso { get; set; }

    public DateTime? FechaEgreso { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<CensoCronicoAgudizacion> Agudizaciones { get; set; } = [];

    public ICollection<CensoCronicoHospitalizacion> Hospitalizaciones { get; set; } = [];
}
