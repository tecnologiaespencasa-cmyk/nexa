namespace Nexa.Models.EspacioCorporativo;

// ═════════════════════════════════════════════════════════════════════════════
// Modelo de una plantilla de acta.
//
// Una plantilla describe tres cosas:
//   1. Los CAMPOS que cambian en cada acta (lo que se diligencia).
//   2. Los BLOQUES de texto del pliego, con marcadores {{clave}} donde entran
//      esos campos.
//   3. Las FIRMAS que lleva el documento al pie.
//
// Las plantillas de fábrica viven en código (EspacioActaPlantillas); las que
// arma un administrador desde el diseñador viven en la base de datos. Ambas se
// resuelven al mismo tipo EspacioActaPlantilla, de modo que el formulario, la
// previsualización, la firma y el envío por correo son un solo camino.
// ═════════════════════════════════════════════════════════════════════════════

public enum EspacioActaTipoCampo
{
    Texto,
    TextoLargo,
    Seleccion,
    Correo,
    Documento,
    /// <summary>Lista de URLs (una por línea o separadas por coma) que se renderizan como enlaces.</summary>
    Enlaces,
    /// <summary>Credencial: se muestra enmascarada en el formulario y en el listado.</summary>
    Credencial,
    /// <summary>Entero. Se imprime con separador de miles.</summary>
    Numero,
    /// <summary>Número con decimales.</summary>
    Decimal,
    /// <summary>Importe en pesos. Se imprime como $ 1.250.000.</summary>
    Moneda,
    /// <summary>Fecha. Se imprime como "12 de agosto de 2026".</summary>
    Fecha,
    /// <summary>Hora. Se imprime como "02:30 p. m.".</summary>
    Hora,
    Telefono,
    /// <summary>Casilla de verificación. Se imprime como Sí / No.</summary>
    Casilla
}

/// <summary>Metadatos de cada tipo de campo: rótulo, ícono y ayuda para el diseñador.</summary>
public sealed record EspacioActaTipoCampoInfo(
    EspacioActaTipoCampo Tipo,
    string Etiqueta,
    string Icono,
    string Descripcion,
    bool AdmiteOpciones = false);

public sealed record EspacioActaOpcion(string Valor, string Etiqueta);

public sealed record EspacioActaCampo
{
    public required string Clave { get; init; }

    public required string Etiqueta { get; init; }

    public EspacioActaTipoCampo Tipo { get; init; } = EspacioActaTipoCampo.Texto;

    public bool Requerido { get; init; } = true;

    public string? Placeholder { get; init; }

    public string? Ayuda { get; init; }

    public IReadOnlyList<EspacioActaOpcion> Opciones { get; init; } = [];

    /// <summary>
    /// Falso para datos que se piden pero no se imprimen en el acta (por ejemplo el correo
    /// al que se envía la copia firmada).
    /// </summary>
    public bool VisibleEnActa { get; init; } = true;

    public int MaxLength { get; init; } = 300;
}

// ── Bloques del pliego ───────────────────────────────────────────────────────

public enum EspacioActaTipoBloque
{
    /// <summary>Encabezado de sección. Se numera solo si la plantilla lo pide.</summary>
    Titulo,
    Parrafo,
    /// <summary>Lista con viñetas: una línea del texto por ítem.</summary>
    Lista,
    /// <summary>Cuadro "Etiqueta: valor" con los campos seleccionados.</summary>
    Datos,
    /// <summary>Aviso destacado (recuadro).</summary>
    Nota,
    Separador
}

public sealed record EspacioActaBloque
{
    public EspacioActaTipoBloque Tipo { get; init; } = EspacioActaTipoBloque.Parrafo;

    /// <summary>Texto del bloque. Admite {{clave}}, **negrita**, *cursiva* y saltos de línea.</summary>
    public string Texto { get; init; } = string.Empty;

    /// <summary>Solo para <see cref="EspacioActaTipoBloque.Datos"/>: claves que se listan.</summary>
    public IReadOnlyList<string> Campos { get; init; } = [];
}

// ── Firmas ───────────────────────────────────────────────────────────────────

public enum EspacioActaFirmaOrigen
{
    /// <summary>Usa la firma guardada de quien emite el acta; no se vuelve a trazar.</summary>
    Emisor,
    /// <summary>Se traza en el momento de firmar, con la persona presente.</summary>
    EnVivo
}

public sealed record EspacioActaFirma
{
    public const string ClaveEmisor = "emisor";
    public const string ClaveRecibe = "recibe";

    public required string Clave { get; init; }

    /// <summary>Rótulo bajo la línea de firma: "Recibe", "Testigo", "Jefe inmediato".</summary>
    public required string Rotulo { get; init; }

    public EspacioActaFirmaOrigen Origen { get; init; } = EspacioActaFirmaOrigen.EnVivo;

    /// <summary>Campo del que sale el nombre. Si es nulo se usa <see cref="NombreFijo"/>.</summary>
    public string? CampoNombre { get; init; }

    public string? CampoDocumento { get; init; }

    public string? NombreFijo { get; init; }

    public string? CargoFijo { get; init; }

    public bool Requerida { get; init; } = true;

    /// <summary>Firmas de fábrica: el administrador entrega y el colaborador recibe.</summary>
    public static IReadOnlyList<EspacioActaFirma> PorDefecto(
        string rotuloRecibe,
        string campoNombre,
        string? campoDocumento) =>
    [
        new EspacioActaFirma
        {
            Clave = ClaveEmisor,
            Rotulo = "Entrega",
            Origen = EspacioActaFirmaOrigen.Emisor
        },
        new EspacioActaFirma
        {
            Clave = ClaveRecibe,
            Rotulo = rotuloRecibe,
            Origen = EspacioActaFirmaOrigen.EnVivo,
            CampoNombre = campoNombre,
            CampoDocumento = campoDocumento
        }
    ];
}

// ── Plantilla ────────────────────────────────────────────────────────────────

public sealed record EspacioActaPlantilla
{
    public required string Codigo { get; init; }

    public required string Nombre { get; init; }

    public required string Descripcion { get; init; }

    /// <summary>Clase de Bootstrap Icons.</summary>
    public required string Icono { get; init; }

    public required string TituloActa { get; init; }

    public required IReadOnlyList<EspacioActaCampo> Campos { get; init; }

    /// <summary>
    /// Cuerpo en HTML con marcadores {{clave}}. Solo lo usan las plantillas de fábrica,
    /// cuyo HTML es de confianza porque vive en el código.
    /// </summary>
    public string? CuerpoHtml { get; init; }

    /// <summary>Bloques del pliego. Tienen prioridad sobre <see cref="CuerpoHtml"/>.</summary>
    public IReadOnlyList<EspacioActaBloque> Bloques { get; init; } = [];

    public IReadOnlyList<EspacioActaFirma> Firmas { get; init; } = [];

    /// <summary>Antepone "1.", "2."... a los títulos de sección al renderizar.</summary>
    public bool NumerarTitulos { get; init; }

    /// <summary>Rótulo bajo la firma de quien recibe (plantillas de fábrica).</summary>
    public string RotuloRecibe { get; init; } = "Recibe";

    // Claves que se extraen a columnas propias para poder buscar y notificar.
    public required string CampoNombre { get; init; }

    public string? CampoDocumento { get; init; }

    public string? CampoCorreo { get; init; }

    public string? CampoUsuario { get; init; }

    // ── Origen ───────────────────────────────────────────────────────────────

    /// <summary>Id en base de datos. Nulo en las plantillas de fábrica.</summary>
    public long? Id { get; init; }

    public bool EsPersonalizada => Id.HasValue;

    public bool Activa { get; init; } = true;

    public string? CreadaPorNombre { get; init; }

    public DateTime? ActualizadaAtUtc { get; init; }

    /// <summary>Firmas efectivas: si la plantilla no declara ninguna, se usan las de fábrica.</summary>
    public IReadOnlyList<EspacioActaFirma> FirmasEfectivas =>
        Firmas.Count > 0
            ? Firmas
            : EspacioActaFirma.PorDefecto(RotuloRecibe, CampoNombre, CampoDocumento);
}
