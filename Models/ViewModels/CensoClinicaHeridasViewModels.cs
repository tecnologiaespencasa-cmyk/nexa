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
    // Sin patrón de "letra + 3 dígitos": el listado propio incluye códigos terminados en X (L89X,
    // L97X). Quien valida es el catálogo de clínica de heridas, no la forma del código.
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

    // PICC, VAC y el resto de la seccion 3 se guardan con su propio boton, no al crear el registro.
    [Display(Name = "PICC")]
    public string? Picc { get; set; }

    [Display(Name = "VAC")]
    public string? Vac { get; set; }

    [Display(Name = "NPT")]
    public string? Npt { get; set; }

    [Display(Name = "Manejo de la herida")]
    public string? ManejoHerida { get; set; }

    [StringLength(200, ErrorMessage = "El apósito/medicamento no puede superar 200 caracteres.")]
    [Display(Name = "Apósito/Medicamento 1")]
    public string? ApositoMedicamento1 { get; set; }

    [StringLength(200, ErrorMessage = "El apósito/medicamento no puede superar 200 caracteres.")]
    [Display(Name = "Apósito/Medicamento 2")]
    public string? ApositoMedicamento2 { get; set; }

    [StringLength(200, ErrorMessage = "El apósito/medicamento no puede superar 200 caracteres.")]
    [Display(Name = "Apósito/Medicamento 3")]
    public string? ApositoMedicamento3 { get; set; }

    [StringLength(200, ErrorMessage = "El apósito/medicamento no puede superar 200 caracteres.")]
    [Display(Name = "Apósito/Medicamento 4")]
    public string? ApositoMedicamento4 { get; set; }

    [Range(1, 999, ErrorMessage = "La duración del tratamiento debe estar entre 1 y 999 días.")]
    [Display(Name = "Duración de tratamiento (días)")]
    public int? DuracionTratamientoDias { get; set; }

    // Seleccion unica: se muestra como lista de opciones, pero solo se guarda una.
    [Display(Name = "Frecuencia de visita")]
    public string? FrecuenciaVisita { get; set; }

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
    public IReadOnlyList<string> ApositoMedicamentoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> Cie10Options { get; set; } = [];
    public IReadOnlyList<string> FrecuenciaVisitaOptions { get; set; } = [];

    /// <summary>Kardex disponibles segun los "Si" guardados: uno por tipo de atencion.</summary>
    public IReadOnlyList<CensoClinicaHeridasKardexResumenViewModel> KardexDisponibles { get; set; } = [];

    /// <summary>Planes de requisiciones del paciente, del mas reciente al mas antiguo.</summary>
    public IReadOnlyList<CensoClinicaHeridasPlanResumenViewModel> Planes { get; set; } = [];

    public CensoClinicaHeridasPlanResumenViewModel? PlanVigente =>
        Planes.FirstOrDefault(x => x.Vigente);
    public IReadOnlyList<SelectListItem> MotivoHospitalizacionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> RemitidoPorHospitalizacionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> MotivoNovedadDevolucionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> EstadoDevolucionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> MotivoEgresoOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> EstadoProgramaOptions { get; set; } = [];
    public IReadOnlyList<string> BarrioOptions { get; set; } = [];
    public IReadOnlyList<CensoClinicaHeridasRecord> UltimosRegistros { get; set; } = [];

    /// <summary>Historial de seguimientos leído de la aplicación de clínica de heridas.</summary>
    public CensoClinicaHeridasHistorialViewModel Historial { get; set; } = new();
}

/// <summary>
/// Sección 2 "Características de la herida": historial de solo lectura que la aplicación del Portal
/// Administrativo captura en campo. La intranet no crea ni edita estos seguimientos.
/// </summary>
public class CensoClinicaHeridasHistorialViewModel
{
    public IReadOnlyList<CensoClinicaHeridasSeguimientoViewModel> Seguimientos { get; set; } = [];

    /// <summary>Mensaje a mostrar cuando la consulta a Neon o a SharePoint no se pudo completar.</summary>
    public string? Error { get; set; }

    /// <summary>Carpeta del paciente en SharePoint, para abrir las fotos originales.</summary>
    public string? CarpetaWebUrl { get; set; }

    public string? CarpetaNombre { get; set; }

    public int TotalSeguimientos => Seguimientos.Count;

    public CensoClinicaHeridasSeguimientoViewModel? Ultimo => Seguimientos.FirstOrDefault();

    /// <summary>
    /// Mayor área (vertical × horizontal) del historial. Sirve de escala común para dibujar cada
    /// medida a proporción y ver cómo cambia la herida entre seguimientos.
    /// </summary>
    public double MedidaMaximaCm => Seguimientos.Count == 0
        ? 0
        : Seguimientos.Max(x => Math.Max(x.DiametroVerticalCm, x.DiametroHorizontalCm));
}

public class CensoClinicaHeridasSeguimientoViewModel
{
    public string Id { get; set; } = string.Empty;

    public int Numero { get; set; }

    /// <summary>Fecha y hora del registro, ya convertidas a la hora de Colombia.</summary>
    public DateTime RegistradoEn { get; set; }

    public string Origen { get; set; } = string.Empty;

    public string Ubicacion { get; set; } = string.Empty;

    public double DiametroVerticalCm { get; set; }

    public double DiametroHorizontalCm { get; set; }

    public double ProfundidadCm { get; set; }

    public string Fondo { get; set; } = string.Empty;

    public string Lecho { get; set; } = string.Empty;

    public string Tejido { get; set; } = string.Empty;

    public string CavitacionTunelizacion { get; set; } = string.Empty;

    public string PielPerilesional { get; set; } = string.Empty;

    public string ExudadoCantidad { get; set; } = string.Empty;

    public string ExudadoCaracteristicas { get; set; } = string.Empty;

    public string AuxiliarNombre { get; set; } = string.Empty;

    public string AuxiliarProfesion { get; set; } = string.Empty;

    public string AuxiliarCedula { get; set; } = string.Empty;

    public string AuxiliarEmail { get; set; } = string.Empty;

    public IReadOnlyList<CensoClinicaHeridasFotoViewModel> Fotos { get; set; } = [];

    public double AreaCm2 => DiametroVerticalCm * DiametroHorizontalCm;

    /// <summary>Cambio de área frente al seguimiento anterior, en porcentaje. Null en el primero.</summary>
    public double? VariacionAreaPorcentaje { get; set; }
}

public class CensoClinicaHeridasFotoViewModel
{
    /// <summary>Código del tipo de foto en Neon (PLANO_GENERAL, MEDIDA_VERTICAL…).</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Etiqueta legible del tipo de foto.</summary>
    public string TipoDescripcion { get; set; } = string.Empty;

    public string DriveItemId { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public bool Disponible => !string.IsNullOrWhiteSpace(DriveItemId);
}

/// <summary>Tarjeta de acceso a un kardex desde el censo.</summary>
public class CensoClinicaHeridasKardexResumenViewModel
{
    public string Tipo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public bool Enviado { get; set; }

    public bool Cerrado { get; set; }

    public DateTime? EnviadoAtUtc { get; set; }

    public int Adjuntos { get; set; }

    public int Aplicaciones { get; set; }

    public int Insumos { get; set; }
}

/// <summary>Un plan de requisiciones en la barra de navegacion de la seccion 3.</summary>
public class CensoClinicaHeridasPlanResumenViewModel
{
    public long Id { get; set; }

    public int Numero { get; set; }

    public bool Vigente { get; set; }

    public string? CreadoPor { get; set; }

    /// <summary>Fecha de creacion ya convertida a hora de Colombia.</summary>
    public DateTime CreadoEn { get; set; }

    public DateTime? CerradoEn { get; set; }

    public string? CerradoPor { get; set; }

    /// <summary>Apositos con los que se armo el plan; en el vigente son los del censo.</summary>
    public IReadOnlyList<string> Apositos { get; set; } = [];

    public int Requisiciones { get; set; }
}
