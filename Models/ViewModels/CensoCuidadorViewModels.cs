using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nexa.Data.Entities;
using Nexa.Filters;

namespace Nexa.Models.ViewModels;

/// <summary>
/// Formulario del programa Cuidadores. Cada sección tiene su propio formulario y envía solo sus
/// campos, así que aquí no hay [Required]: lo obligatorio lo valida la acción de cada sección.
/// </summary>
public class CensoCuidadorViewModel : ICensoRegistroEditable
{
    public long? EditingRecordId { get; set; }

    public string? CedulaFiltro { get; set; }

    // ----- Recepción del ingreso -----
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de reporte por parte de la EPS")]
    public DateTime? FechaReporteEps { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de inicio de atención")]
    public DateTime? FechaInicioAtencion { get; set; }

    [BindNever]
    public int? OportunidadInicioDias =>
        FechaReporteEps.HasValue && FechaInicioAtencion.HasValue
            ? (FechaInicioAtencion.Value.Date - FechaReporteEps.Value.Date).Days
            : null;

    // ----- Responsable -----
    [StringLength(200, ErrorMessage = "El nombre del responsable no puede superar 200 caracteres.")]
    [Display(Name = "Nombre del responsable")]
    public string? NombreResponsable { get; set; }

    [StringLength(80, ErrorMessage = "El parentesco no puede superar 80 caracteres.")]
    [Display(Name = "Parentesco")]
    public string? ParentescoResponsable { get; set; }

    [StringLength(10, ErrorMessage = "El teléfono admite máximo 10 dígitos.")]
    [Display(Name = "Teléfono")]
    public string? TelefonoResponsable { get; set; }

    [StringLength(300, ErrorMessage = "La dirección no puede superar 300 caracteres.")]
    [Display(Name = "Dirección")]
    public string? DireccionResponsable { get; set; }

    [Display(Name = "Misma dirección del paciente")]
    public bool DireccionMismaDelPaciente { get; set; }

    /// <summary>La dirección de los datos básicos, para llenar el campo al marcar la casilla.</summary>
    [BindNever]
    public string? DireccionPaciente { get; set; }

    // ----- Plan de manejo -----
    [Display(Name = "Tiempo de cuidador (horas al día)")]
    public int? TiempoCuidadorHorasDia { get; set; }

    [Display(Name = "Días de atención")]
    public List<string> DiasAtencion { get; set; } = [];

    [Display(Name = "Horario")]
    public string? Horario { get; set; }

    [BindNever]
    public bool EnProgramaPermanentes { get; set; }

    [BindNever]
    public DateTime? FechaIngresoPermanentes { get; set; }

    [Display(Name = "Escala de Barthel")]
    public int? EscalaBarthel { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de certificación de dependencia funcional")]
    public DateTime? FechaCertificacionDependencia { get; set; }

    [Display(Name = "Tiempo de ordenamiento (meses)")]
    public int? TiempoOrdenamientoMeses { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de vencimiento del certificado de dependencia")]
    public DateTime? FechaVencimientoCertificado { get; set; }

    [StringLength(2000, ErrorMessage = "La observación no puede superar 2000 caracteres.")]
    [Display(Name = "Observación")]
    public string? Observacion { get; set; }

    [StringLength(2000, ErrorMessage = "La verificación de derechos no puede superar 2000 caracteres.")]
    [Display(Name = "Verificación de derechos")]
    public string? VerificacionDerechos { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de verificación de derechos")]
    public DateTime? FechaVerificacionDerechos { get; set; }

    [StringLength(200, ErrorMessage = "El auxiliar no puede superar 200 caracteres.")]
    [Display(Name = "Auxiliar asignado")]
    public string? AuxiliarAsignado { get; set; }

    /// <summary>
    /// El auxiliar que el registro ya tenía. Si salió del directorio se conserva como opción de ese
    /// registro: de lo contrario guardar el plan obligaría a cambiarlo.
    /// </summary>
    [BindNever]
    public string? AuxiliarGuardado { get; set; }

    // ----- Estado -----
    [Display(Name = "Estado en programa")]
    public string? Estado { get; set; } = CuidadorEstados.Activo;

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de egreso")]
    public DateTime? FechaEgreso { get; set; }

    [StringLength(500, ErrorMessage = "El motivo no puede superar 500 caracteres.")]
    [Display(Name = "Motivo de egreso o cancelación")]
    public string? MotivoEgreso { get; set; }

    // ----- Catálogos -----
    [BindNever]
    public IReadOnlyList<string> DiasSemanaOptions { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> HorarioOptions { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> EstadoOptions { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> AuxiliarEnfermeriaOptions { get; set; } = [];
}
