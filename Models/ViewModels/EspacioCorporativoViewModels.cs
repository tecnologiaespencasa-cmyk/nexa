using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Nexa.Models.ViewModels;

// ─────────────────────────────────────────────────────────────────────────────
// Pantalla principal: Mi espacio corporativo
// ─────────────────────────────────────────────────────────────────────────────

public class EspacioCorporativoIndexViewModel
{
    public string NombreUsuario { get; set; } = string.Empty;

    public bool EsAdministrador { get; set; }

    public IReadOnlyList<EspacioActivoAsignadoViewModel> MisActivos { get; set; } = [];

    public IReadOnlyList<EspacioNovedadResumenViewModel> MisNovedades { get; set; } = [];

    public IReadOnlyList<EspacioDocumentoTarjetaViewModel> Documentos { get; set; } = [];

    public IReadOnlyList<string> Categorias { get; set; } = [];

    public IReadOnlyList<string> TiposDocumento { get; set; } = [];

    public IReadOnlyList<string> TiposNovedad { get; set; } = [];

    /// <summary>Conteo de documentos por categoria para las fichas de filtro.</summary>
    public IReadOnlyDictionary<string, int> ConteoPorCategoria { get; set; } = new Dictionary<string, int>();

    public int TotalDocumentos { get; set; }

    public int NovedadesAbiertas { get; set; }
}

public class EspacioActivoAsignadoViewModel
{
    public long Id { get; set; }

    public string TipoActivo { get; set; } = string.Empty;

    public string? NombreEquipo { get; set; }

    public string Marca { get; set; } = string.Empty;

    public string Serie { get; set; } = string.Empty;

    public string Serial { get; set; } = string.Empty;

    public string? CodigoActivo { get; set; }

    public string? Especificaciones { get; set; }

    public string Estado { get; set; } = string.Empty;

    public string? Nota { get; set; }

    public DateTime? FechaAsignacion { get; set; }

    public int NovedadesAbiertas { get; set; }
}

public class EspacioNovedadResumenViewModel
{
    public long Id { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string Estado { get; set; } = string.Empty;

    public string? Prioridad { get; set; }

    public string? Clasificacion { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public string EquipoDescripcion { get; set; } = string.Empty;

    public string? RespuestaAdmin { get; set; }

    public DateTime FechaReporte { get; set; }

    public DateTime? FechaResolucion { get; set; }
}

public class EspacioDocumentoTarjetaViewModel
{
    public long Id { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public string Categoria { get; set; } = string.Empty;

    public string TipoDocumento { get; set; } = string.Empty;

    public string TipoContenido { get; set; } = string.Empty;

    public string? Version { get; set; }

    public string? CodigoDocumento { get; set; }

    public string? Etiquetas { get; set; }

    public string? ArchivoNombre { get; set; }

    public string? ExtensionArchivo { get; set; }

    public string? TamanoLegible { get; set; }

    public string? EnlaceUrl { get; set; }

    public bool Destacado { get; set; }

    public bool EsFavorito { get; set; }

    public bool Publicado { get; set; } = true;

    public DateTime FechaPublicacion { get; set; }

    public DateTime? FechaActualizacion { get; set; }

    public string? CreadoPorNombre { get; set; }

    public int Descargas { get; set; }

    /// <summary>Texto plano concatenado para el buscador del cliente.</summary>
    public string TextoBusqueda { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────────────────────
// Reporte de novedad (usuario basico)
// ─────────────────────────────────────────────────────────────────────────────

public class EspacioNovedadFormViewModel
{
    public long? ActivoId { get; set; }

    [StringLength(200, ErrorMessage = "Maximo 200 caracteres.")]
    public string? EquipoReferencia { get; set; }

    [Required(ErrorMessage = "Selecciona el tipo de novedad.")]
    [StringLength(60)]
    public string Tipo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Describe la novedad.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "La descripcion debe tener entre 10 y 2000 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────────────────────
// Administracion de activos
// ─────────────────────────────────────────────────────────────────────────────

public class EspacioActivosAdminViewModel
{
    public string? Busqueda { get; set; }

    public string? EstadoFiltro { get; set; }

    public string? TipoFiltro { get; set; }

    public string? ResponsableFiltro { get; set; }

    public string? EstadoNovedadFiltro { get; set; }

    public IReadOnlyList<EspacioActivoAdminItemViewModel> Activos { get; set; } = [];

    public IReadOnlyList<EspacioNovedadAdminItemViewModel> Novedades { get; set; } = [];

    public IReadOnlyList<EspacioUsuarioOpcionViewModel> Responsables { get; set; } = [];

    public IReadOnlyList<string> TiposActivo { get; set; } = [];

    public IReadOnlyList<string> EstadosActivo { get; set; } = [];

    public IReadOnlyList<string> EstadosNovedad { get; set; } = [];

    public IReadOnlyList<string> PrioridadesNovedad { get; set; } = [];

    public IReadOnlyList<string> ClasificacionesNovedad { get; set; } = [];

    public EspacioActivosMetricasViewModel Metricas { get; set; } = new();

    /// <summary>Firma almacenada del administrador que esta viendo la pantalla.</summary>
    public bool TieneFirmaGuardada { get; set; }

    public string? MiFirmaDataUrl { get; set; }

    public string? MiFirmaNombre { get; set; }

    public string? MiFirmaCargo { get; set; }
}

public class EspacioActivosMetricasViewModel
{
    public int Total { get; set; }

    public int Asignados { get; set; }

    public int Disponibles { get; set; }

    public int EnMantenimiento { get; set; }

    public int DadosDeBaja { get; set; }

    public int NovedadesAbiertas { get; set; }

    public int NovedadesSinClasificar { get; set; }
}

public class EspacioActivoAdminItemViewModel
{
    public long Id { get; set; }

    public string TipoActivo { get; set; } = string.Empty;

    public string? NombreEquipo { get; set; }

    public string Marca { get; set; } = string.Empty;

    public string Serie { get; set; } = string.Empty;

    public string Serial { get; set; } = string.Empty;

    public string? Especificaciones { get; set; }

    public string? CodigoActivo { get; set; }

    public Guid? ResponsableUserId { get; set; }

    public string? ResponsableNombre { get; set; }

    public string Estado { get; set; } = string.Empty;

    public string? Nota { get; set; }

    public DateTime? FechaAsignacion { get; set; }

    public DateTime FechaCreacion { get; set; }

    public DateTime? FechaActualizacion { get; set; }

    public int NovedadesAbiertas { get; set; }

    /// <summary>Tipo de la ultima acta firmada (Entrega / Devolucion), o null si no tiene.</summary>
    public string? UltimaActaTipo { get; set; }

    public DateTime? UltimaActaFecha { get; set; }

    public int TotalActas { get; set; }

    /// <summary>El equipo esta entregado con acta firmada y sin devolucion posterior.</summary>
    public bool EntregaFirmada =>
        string.Equals(UltimaActaTipo, "Entrega", StringComparison.OrdinalIgnoreCase);
}

public class EspacioNovedadAdminItemViewModel
{
    public long Id { get; set; }

    public long? ActivoId { get; set; }

    public string EquipoDescripcion { get; set; } = string.Empty;

    public string? Serial { get; set; }

    public string? CodigoActivo { get; set; }

    public string ReportadoPorNombre { get; set; } = string.Empty;

    public string? ReportadoPorEmail { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public string Estado { get; set; } = string.Empty;

    public string? Prioridad { get; set; }

    public string? Clasificacion { get; set; }

    public string? RespuestaAdmin { get; set; }

    public string? AtendidoPorNombre { get; set; }

    public DateTime FechaReporte { get; set; }

    public DateTime? FechaResolucion { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Actas de entrega / devolucion con firma
// ─────────────────────────────────────────────────────────────────────────────

public class EspacioFirmaGuardadaViewModel
{
    [StringLength(160, ErrorMessage = "Maximo 160 caracteres.")]
    public string? NombreFirmante { get; set; }

    [StringLength(120, ErrorMessage = "Maximo 120 caracteres.")]
    public string? Cargo { get; set; }

    public string? FirmaDataUrl { get; set; }
}

public class EspacioActaFormViewModel
{
    public long ActivoId { get; set; }

    /// <summary>Entrega | Devolucion</summary>
    [Required]
    [StringLength(20)]
    public string Tipo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Indica el nombre de quien recibe.")]
    [StringLength(160, ErrorMessage = "Maximo 160 caracteres.")]
    public string RecibePorNombre { get; set; } = string.Empty;

    [StringLength(30, ErrorMessage = "Maximo 30 caracteres.")]
    public string? RecibePorDocumento { get; set; }

    [StringLength(2000, ErrorMessage = "Maximo 2000 caracteres.")]
    public string? Observaciones { get; set; }

    /// <summary>Firma de quien recibe: siempre se traza en el momento.</summary>
    public string? FirmaRecibeDataUrl { get; set; }

    /// <summary>
    /// Firma de quien entrega. Solo llega desde el cliente cuando el administrador
    /// aun no tiene una firma guardada; en el resto de los casos se toma la almacenada.
    /// </summary>
    public string? FirmaEntregaDataUrl { get; set; }

    /// <summary>Guardar la firma trazada como la firma por defecto del administrador.</summary>
    public bool GuardarFirmaEntrega { get; set; } = true;
}

public class EspacioActaResumenViewModel
{
    public long Id { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string EntregaPorNombre { get; set; } = string.Empty;

    public string RecibePorNombre { get; set; } = string.Empty;

    public string? RecibePorDocumento { get; set; }

    public string? Observaciones { get; set; }

    public DateTime FechaFirma { get; set; }
}

public class EspacioActaDocumentoViewModel
{
    public long Id { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string TituloActa { get; set; } = string.Empty;

    public string EquipoDescripcion { get; set; } = string.Empty;

    public string Serial { get; set; } = string.Empty;

    public string? CodigoActivo { get; set; }

    public string? Especificaciones { get; set; }

    public string EntregaPorNombre { get; set; } = string.Empty;

    public string? EntregaPorCargo { get; set; }

    public string FirmaEntregaDataUrl { get; set; } = string.Empty;

    public string RecibePorNombre { get; set; } = string.Empty;

    public string? RecibePorDocumento { get; set; }

    public string FirmaRecibeDataUrl { get; set; } = string.Empty;

    public string? Observaciones { get; set; }

    public DateTime FechaFirma { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Modulo de Actas por plantilla
// ─────────────────────────────────────────────────────────────────────────────

public class EspacioActasIndexViewModel
{
    public string? Busqueda { get; set; }

    public string? PlantillaFiltro { get; set; }

    public IReadOnlyList<EspacioCorporativo.EspacioActaPlantilla> Plantillas { get; set; } = [];

    public IReadOnlyList<EspacioActaEmitidaViewModel> Actas { get; set; } = [];

    public int TotalActas { get; set; }

    public int TotalCorreosPendientes { get; set; }

    public bool TieneFirmaGuardada { get; set; }
}

public class EspacioActaEmitidaViewModel
{
    public long Id { get; set; }

    public string PlantillaNombre { get; set; } = string.Empty;

    public string TituloActa { get; set; } = string.Empty;

    public string NombreRecibe { get; set; } = string.Empty;

    public string? DocumentoRecibe { get; set; }

    public string? CorreoRecibe { get; set; }

    public string? UsuarioRecibe { get; set; }

    public string EmitidaPorNombre { get; set; } = string.Empty;

    public bool CorreoEnviado { get; set; }

    public string? CorreoError { get; set; }

    public DateTime FechaFirma { get; set; }
}

/// <summary>Formulario de captura de variables de una plantilla.</summary>
public class EspacioActaCapturaViewModel
{
    public string PlantillaCodigo { get; set; } = string.Empty;

    public EspacioCorporativo.EspacioActaPlantilla? Plantilla { get; set; }

    /// <summary>Valores por clave de campo.</summary>
    public Dictionary<string, string?> Valores { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? MensajeError { get; set; }
}

/// <summary>Pantalla de previsualizacion y firma del acta antes de emitirla.</summary>
public class EspacioActaFirmaViewModel
{
    public string PlantillaCodigo { get; set; } = string.Empty;

    public string TituloActa { get; set; } = string.Empty;

    public string PlantillaNombre { get; set; } = string.Empty;

    public string CuerpoHtml { get; set; } = string.Empty;

    public Dictionary<string, string?> Valores { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string NombreRecibe { get; set; } = string.Empty;

    public string? DocumentoRecibe { get; set; }

    public string? CorreoRecibe { get; set; }

    public string EmitidaPorNombre { get; set; } = string.Empty;

    public string? EmitidaPorCargo { get; set; }

    public string? EmitidaPorDocumento { get; set; }

    public string? FirmaEmiteDataUrl { get; set; }

    public bool TieneFirmaGuardada { get; set; }

    /// <summary>Firmas que lleva el documento, en el orden en que se imprimen.</summary>
    public IReadOnlyList<EspacioActaFirmaCapturaViewModel> Firmas { get; set; } = [];

    public DateTime Fecha { get; set; }

    /// <summary>Error de validacion al intentar emitir, sin perder lo ya capturado.</summary>
    public string? MensajeError { get; set; }
}

/// <summary>Una firma pendiente de trazar (o ya resuelta) en la pantalla de firma.</summary>
public class EspacioActaFirmaCapturaViewModel
{
    public string Clave { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    /// <summary>Firma de quien emite: usa la firma guardada del administrador.</summary>
    public bool EsEmisor { get; set; }

    public bool Requerida { get; set; } = true;

    public string Nombre { get; set; } = string.Empty;

    public string? Documento { get; set; }

    public string? Cargo { get; set; }

    /// <summary>Trazo ya disponible. Solo lo trae la firma guardada del emisor.</summary>
    public string? DataUrl { get; set; }

    /// <summary>Verdadero cuando hay que trazarla en esta pantalla.</summary>
    public bool DebeTrazar { get; set; }

    /// <summary>Ofrece guardar el trazo para no volver a pedirlo (solo la del emisor).</summary>
    public bool OfrecerGuardar { get; set; }
}

/// <summary>Firma tal como se imprime en un acta ya emitida.</summary>
public class EspacioActaFirmaImpresaViewModel
{
    public string Rotulo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string? Documento { get; set; }

    public string? Cargo { get; set; }

    public string DataUrl { get; set; } = string.Empty;
}

/// <summary>Acta ya emitida, para verla o imprimirla.</summary>
public class EspacioActaEmitidaDocumentoViewModel
{
    public long Id { get; set; }

    public string TituloActa { get; set; } = string.Empty;

    public string PlantillaNombre { get; set; } = string.Empty;

    public string CuerpoHtml { get; set; } = string.Empty;

    public string NombreRecibe { get; set; } = string.Empty;

    public string? DocumentoRecibe { get; set; }

    public string EmitidaPorNombre { get; set; } = string.Empty;

    public string? EmitidaPorCargo { get; set; }

    public string? EmitidaPorDocumento { get; set; }

    public string FirmaEmiteDataUrl { get; set; } = string.Empty;

    public string FirmaRecibeDataUrl { get; set; } = string.Empty;

    /// <summary>
    /// Firmas guardadas con el acta. Las actas anteriores al diseñador no la traen:
    /// en ese caso se arma con las dos columnas de firma de siempre.
    /// </summary>
    public IReadOnlyList<EspacioActaFirmaImpresaViewModel> Firmas { get; set; } = [];

    public DateTime FechaFirma { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Diseñador de plantillas de acta
// ─────────────────────────────────────────────────────────────────────────────

public class EspacioActaPlantillasIndexViewModel
{
    public IReadOnlyList<EspacioActaPlantillaResumenViewModel> Plantillas { get; set; } = [];

    public IReadOnlyList<EspacioCorporativo.EspacioActaPlantilla> DeFabrica { get; set; } = [];

    public int TotalActivas { get; set; }

    public int TotalInactivas { get; set; }
}

public class EspacioActaPlantillaResumenViewModel
{
    public long Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public string Icono { get; set; } = string.Empty;

    public string TituloActa { get; set; } = string.Empty;

    public int TotalCampos { get; set; }

    public int TotalFirmas { get; set; }

    public bool Activa { get; set; }

    public int Version { get; set; }

    public string CreadaPorNombre { get; set; } = string.Empty;

    public DateTime ActualizadaAt { get; set; }

    /// <summary>Actas ya emitidas con esta plantilla. Bloquea el borrado definitivo.</summary>
    public int ActasEmitidas { get; set; }
}

/// <summary>Estado inicial del diseñador: la definición en JSON y los catálogos.</summary>
public class EspacioActaDisenadorViewModel
{
    public long? Id { get; set; }

    public string Titulo { get; set; } = "Nueva acta";

    public bool EsEdicion => Id.HasValue;

    /// <summary>Falso mientras la plantilla es un borrador que nadie puede usar todavía.</summary>
    public bool Publicada { get; set; }

    /// <summary>Definición serializada que hidrata el editor.</summary>
    public string DefinicionJson { get; set; } = "null";

    /// <summary>Catálogo de tipos de campo, serializado para el editor.</summary>
    public string TiposDeCampoJson { get; set; } = "[]";

    /// <summary>Actas de ejemplo para arrancar, serializadas para el editor.</summary>
    public string ModelosJson { get; set; } = "[]";

    /// <summary>Verdadero cuando el editor debe abrir preguntando por dónde empezar.</summary>
    public bool ElegirModelo { get; set; }

    public IReadOnlyList<EspacioCorporativo.EspacioActaOpcion> MarcadoresDelSistema { get; set; } = [];

    public IReadOnlyList<EspacioCorporativo.EspacioActaOpcion> Iconos { get; set; } = [];

    public int ActasEmitidas { get; set; }
}

public class EspacioUsuarioOpcionViewModel
{
    public Guid Id { get; set; }

    public string NombreCompleto { get; set; } = string.Empty;

    public string Usuario { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}

public class EspacioActivoFormViewModel
{
    public long? Id { get; set; }

    [Required(ErrorMessage = "El tipo de activo es obligatorio.")]
    [StringLength(60)]
    public string TipoActivo { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "Maximo 150 caracteres.")]
    public string? NombreEquipo { get; set; }

    [Required(ErrorMessage = "La marca es obligatoria.")]
    [StringLength(80, ErrorMessage = "Maximo 80 caracteres.")]
    public string Marca { get; set; } = string.Empty;

    [Required(ErrorMessage = "La serie es obligatoria.")]
    [StringLength(120, ErrorMessage = "Maximo 120 caracteres.")]
    public string Serie { get; set; } = string.Empty;

    [Required(ErrorMessage = "El serial es obligatorio.")]
    [StringLength(120, ErrorMessage = "Maximo 120 caracteres.")]
    public string Serial { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Maximo 2000 caracteres.")]
    public string? Especificaciones { get; set; }

    [StringLength(60, ErrorMessage = "Maximo 60 caracteres.")]
    public string? CodigoActivo { get; set; }

    public Guid? ResponsableUserId { get; set; }

    [StringLength(40)]
    public string? Estado { get; set; }

    [StringLength(2000, ErrorMessage = "Maximo 2000 caracteres.")]
    public string? Nota { get; set; }
}

public class EspacioNovedadGestionViewModel
{
    public long Id { get; set; }

    /// <summary>
    /// Estado destino de la transicion. Vacio cuando solo se guarda la clasificacion
    /// sin mover la novedad de paso.
    /// </summary>
    [StringLength(30)]
    public string? Destino { get; set; }

    [StringLength(60)]
    public string? Clasificacion { get; set; }

    [StringLength(20)]
    public string? Prioridad { get; set; }

    [StringLength(2000, ErrorMessage = "Maximo 2000 caracteres.")]
    public string? RespuestaAdmin { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Administracion de documentacion
// ─────────────────────────────────────────────────────────────────────────────

public class EspacioDocumentacionAdminViewModel
{
    public string? Busqueda { get; set; }

    public string? CategoriaFiltro { get; set; }

    public string? TipoFiltro { get; set; }

    public IReadOnlyList<EspacioDocumentoAdminItemViewModel> Documentos { get; set; } = [];

    public IReadOnlyList<string> Categorias { get; set; } = [];

    public IReadOnlyList<string> TiposDocumento { get; set; } = [];

    public IReadOnlyList<string> TiposContenido { get; set; } = [];

    public IReadOnlyList<string> ExtensionesPermitidas { get; set; } = [];

    public int TotalPublicados { get; set; }

    public int TotalBorradores { get; set; }

    public int TotalDescargas { get; set; }
}

public class EspacioDocumentoAdminItemViewModel
{
    public long Id { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public string Categoria { get; set; } = string.Empty;

    public string TipoDocumento { get; set; } = string.Empty;

    public string TipoContenido { get; set; } = string.Empty;

    public string? Version { get; set; }

    public string? CodigoDocumento { get; set; }

    public string? Etiquetas { get; set; }

    public string? ArchivoNombre { get; set; }

    public string? TamanoLegible { get; set; }

    public string? EnlaceUrl { get; set; }

    public string? ContenidoTexto { get; set; }

    public bool Publicado { get; set; }

    public bool Destacado { get; set; }

    public DateOnly? FechaVigencia { get; set; }

    public int Descargas { get; set; }

    public int Favoritos { get; set; }

    public string? CreadoPorNombre { get; set; }

    public DateTime FechaCreacion { get; set; }

    public DateTime? FechaActualizacion { get; set; }
}

public class EspacioDocumentoFormViewModel
{
    public long? Id { get; set; }

    [Required(ErrorMessage = "El titulo es obligatorio.")]
    [StringLength(200, ErrorMessage = "Maximo 200 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Maximo 1000 caracteres.")]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "La categoria es obligatoria.")]
    [StringLength(60)]
    public string Categoria { get; set; } = string.Empty;

    [Required(ErrorMessage = "El tipo de documento es obligatorio.")]
    [StringLength(40)]
    public string TipoDocumento { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona como se cargara el documento.")]
    [StringLength(20)]
    public string TipoContenido { get; set; } = string.Empty;

    [StringLength(30, ErrorMessage = "Maximo 30 caracteres.")]
    public string? Version { get; set; }

    [StringLength(60, ErrorMessage = "Maximo 60 caracteres.")]
    public string? CodigoDocumento { get; set; }

    [StringLength(300, ErrorMessage = "Maximo 300 caracteres.")]
    public string? Etiquetas { get; set; }

    [StringLength(1000, ErrorMessage = "Maximo 1000 caracteres.")]
    public string? EnlaceUrl { get; set; }

    public string? ContenidoTexto { get; set; }

    public IFormFile? Archivo { get; set; }

    public bool Publicado { get; set; } = true;

    public bool Destacado { get; set; }

    public DateOnly? FechaVigencia { get; set; }
}
