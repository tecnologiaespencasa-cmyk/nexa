using System.ComponentModel.DataAnnotations;
using Nexa.Data.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Nexa.Models.ViewModels;

public class CensoClinicaHeridasViewModel
{
    public long? EditingRecordId { get; set; }

    public string? CedulaFiltro { get; set; }

    [Required(ErrorMessage = "Selecciona el asegurador.")]
    [StringLength(120, ErrorMessage = "El asegurador no puede superar 120 caracteres.")]
    [Display(Name = "Asegurador")]
    public string Asegurador { get; set; } = string.Empty;

    [Display(Name = "Fuente de ingreso")]
    public string? FuenteIngreso { get; set; }

    [Required(ErrorMessage = "La fecha de ingreso al programa es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de ingreso al programa")]
    public DateTime FechaIngresoPrograma { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Selecciona el tipo de identificación.")]
    [StringLength(3)]
    [Display(Name = "Tipo de identificación")]
    public string TipoIdentificacion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El número de documento es obligatorio.")]
    [StringLength(20, ErrorMessage = "El número de documento no puede superar 20 caracteres.")]
    [Display(Name = "Número de documento")]
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

    [Required(ErrorMessage = "Selecciona el género.")]
    [Display(Name = "Género")]
    public string Genero { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "La dirección no puede superar 300 caracteres.")]
    [Display(Name = "Dirección")]
    public string? Direccion { get; set; }

    [StringLength(200, ErrorMessage = "El detalle de dirección no puede superar 200 caracteres.")]
    [Display(Name = "Detalle de dirección")]
    public string? DetalleDireccion { get; set; }

    [Display(Name = "Clasificación zona Sura")]
    public string? ClasificacionZonaSura { get; set; }

    [Display(Name = "Municipio de residencia")]
    public string? MunicipioResidencia { get; set; }

    [Display(Name = "Barrio")]
    public string? Barrio { get; set; }

    [Display(Name = "Zona de dirección según municipio")]
    public string? ZonaDireccionSegunMunicipio { get; set; }

    [Required(ErrorMessage = "El teléfono principal es obligatorio.")]
    [StringLength(10, ErrorMessage = "El teléfono principal no puede superar 10 dígitos.")]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "El teléfono principal solo permite dígitos.")]
    [Display(Name = "Teléfono principal")]
    public string TelefonoPrincipal { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono adicional 1 es obligatorio.")]
    [StringLength(10, ErrorMessage = "El teléfono adicional 1 no puede superar 10 dígitos.")]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "El teléfono adicional 1 solo permite dígitos.")]
    [Display(Name = "Teléfono adicional 1")]
    public string TelefonoAdicional1 { get; set; } = string.Empty;

    [StringLength(10, ErrorMessage = "El teléfono adicional 2 no puede superar 10 dígitos.")]
    [RegularExpression(@"^[0-9]*$", ErrorMessage = "El teléfono adicional 2 solo permite dígitos.")]
    [Display(Name = "Teléfono adicional 2")]
    public string? TelefonoAdicional2 { get; set; }

    [Display(Name = "Llamada de bienvenida de ingreso a programa")]
    public string? LlamadaBienvenida { get; set; }

    [StringLength(10, ErrorMessage = "El teléfono de contacto no puede superar 10 dígitos.")]
    [RegularExpression(@"^[0-9]*$", ErrorMessage = "El teléfono de contacto solo permite dígitos.")]
    [Display(Name = "Teléfono de contacto")]
    public string? TelefonoContacto { get; set; }

    [StringLength(2000, ErrorMessage = "La observación no puede superar 2000 caracteres.")]
    [Display(Name = "Observación")]
    public string? Observacion { get; set; }

    [Required(ErrorMessage = "El código CIE10 es obligatorio.")]
    [StringLength(4, ErrorMessage = "El código CIE10 debe tener 4 caracteres.")]
    [RegularExpression(@"^[A-Za-z][0-9]{3}$", ErrorMessage = "El código CIE10 debe iniciar con letra y continuar con 3 dígitos.")]
    [Display(Name = "CIE10")]
    public string CodigoCie10 { get; set; } = string.Empty;

    [Display(Name = "Diagnóstico")]
    public string? DiagnosticoDescriptivo { get; set; }

    [Required(ErrorMessage = "La fecha de valoración es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de valoración")]
    public DateTime FechaValoracion { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Selecciona el programa al que pertenece.")]
    [Display(Name = "Programa al que pertenece")]
    public string ProgramaPertenece { get; set; } = string.Empty;

    [StringLength(120, ErrorMessage = "El auxiliar de enfermería asignado no puede superar 120 caracteres.")]
    [Display(Name = "Auxiliar de enfermería asignado")]
    public string? AuxiliarEnfermeriaAsignado { get; set; }

    [Required(ErrorMessage = "Selecciona si el paciente tiene PICC.")]
    [Display(Name = "PICC")]
    public string Picc { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona si el paciente tiene VAC.")]
    [Display(Name = "VAC")]
    public string Vac { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "La descripción de la herida no puede superar 2000 caracteres.")]
    [Display(Name = "Descripción de la herida")]
    public string? DescripcionHerida { get; set; }

    [Display(Name = "Ubicación de la herida")]
    public string? UbicacionHerida { get; set; }

    [Range(1, 7, ErrorMessage = "La frecuencia de visitas debe estar entre 1 y 7.")]
    [Display(Name = "Frecuencia de visitas a la semana")]
    public int? FrecuenciaVisitasSemana { get; set; }

    [Display(Name = "Equipo en comodato")]
    public string? EquipoComodato { get; set; }

    [StringLength(100, ErrorMessage = "El número de placa no puede superar 100 caracteres.")]
    [Display(Name = "Número de placa equipos asignados")]
    public string? NumeroPlacaEquipos { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de entrega de equipo")]
    public DateTime? FechaEntregaEquipo { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de devolución del equipo")]
    public DateTime? FechaDevolucionEquipo { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de hospitalización")]
    public DateTime? FechaHospitalizacion { get; set; }

    [Display(Name = "Motivo de la hospitalización")]
    public string? MotivoHospitalizacion { get; set; }

    [Display(Name = "Remitido por")]
    public string? RemitidoPorHospitalizacion { get; set; }

    [StringLength(200, ErrorMessage = "La IPS intramural no puede superar 200 caracteres.")]
    [Display(Name = "IPS Intramural")]
    public string? IpsIntramural { get; set; }
    public IReadOnlyList<SelectListItem> IpsIntramuralOptions { get; set; } = [];

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de 1er seguimiento (24 horas)")]
    public DateTime? FechaPrimerSeguimiento24Horas { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de 2do seguimiento (48 horas)")]
    public DateTime? FechaSegundoSeguimiento48Horas { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de 3er seguimiento (72 horas)")]
    public DateTime? FechaTercerSeguimiento72Horas { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de 4to seguimiento (Semana 1)")]
    public DateTime? FechaCuartoSeguimientoSemana1 { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de 5to seguimiento (Semana 2)")]
    public DateTime? FechaQuintoSeguimientoSemana2 { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de 6to seguimiento (Semana 3)")]
    public DateTime? FechaSextoSeguimientoSemana3 { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de 7to seguimiento (Semana 4)")]
    public DateTime? FechaSeptimoSeguimientoSemana4 { get; set; }

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

    [Display(Name = "Estado de la devolución - Diligencia el servicio farmacéutico")]
    public string? EstadoDevolucionServicioFarmaceutico { get; set; }

    [Display(Name = "Motivo del egreso")]
    public string? MotivoEgreso { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de egreso")]
    public DateTime? FechaEgreso { get; set; }

    [Display(Name = "Estado")]
    public string? Estado { get; set; }

    public bool AsumirDireccionErrada { get; set; }

    public string? DireccionSugerida { get; set; }

    public string? DireccionMensajeValidacion { get; set; }

    public bool DireccionEsValida { get; set; }

    public IReadOnlyList<SelectListItem> AseguradorOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> FuenteIngresoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> TipoIdentificacionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> GeneroOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ClasificacionZonaSuraOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> MunicipioResidenciaOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ZonaDireccionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> LlamadaBienvenidaOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ProgramaPerteneceOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> AuxiliarEnfermeriaOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> SiNoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> UbicacionHeridaOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> FrecuenciaVisitasOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> MotivoHospitalizacionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> RemitidoPorHospitalizacionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> MotivoNovedadDevolucionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> EstadoDevolucionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> MotivoEgresoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> EstadoProgramaOptions { get; set; } = [];
    public IReadOnlyList<string> BarrioOptions { get; set; } = [];
    public IReadOnlyList<CensoClinicaHeridasRecord> UltimosRegistros { get; set; } = [];
    public IReadOnlyList<CensoClinicaHeridasAdjuntoViewModel> AdjuntosHerida { get; set; } = [];
    public string? AdjuntosHeridaError { get; set; }
}

public class CensoClinicaHeridasAdjuntoViewModel
{
    public string Name { get; set; } = string.Empty;

    public string WebUrl { get; set; } = string.Empty;

    public long Size { get; set; }

    public DateTimeOffset? LastModifiedAt { get; set; }
}
