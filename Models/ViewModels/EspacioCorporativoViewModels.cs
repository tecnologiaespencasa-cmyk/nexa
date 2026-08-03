using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace IntranetPrueba.Models.ViewModels;

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
