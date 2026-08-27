namespace Nexa.Models.EspacioCorporativo;

/// <summary>
/// Lo que envía el diseñador de actas desde el navegador.
///
/// Es deliberadamente laxo (todo anulable, todo texto): la validación y la
/// normalización viven en <see cref="Nexa.Helpers.EspacioActaDisenador"/>, que es
/// el único punto por el que una definición entra al sistema.
/// </summary>
public sealed class EspacioActaDefinicionDto
{
    public long? Id { get; set; }

    public string? Nombre { get; set; }

    public string? Descripcion { get; set; }

    public string? Icono { get; set; }

    public string? TituloActa { get; set; }

    public bool NumerarTitulos { get; set; } = true;

    public string? CampoNombre { get; set; }

    public string? CampoDocumento { get; set; }

    public string? CampoCorreo { get; set; }

    public string? CampoUsuario { get; set; }

    public List<CampoDto> Campos { get; set; } = [];

    public List<BloqueDto> Bloques { get; set; } = [];

    public List<FirmaDto> Firmas { get; set; } = [];

    public sealed class CampoDto
    {
        public string? Clave { get; set; }

        public string? Etiqueta { get; set; }

        public string? Tipo { get; set; }

        public bool Requerido { get; set; } = true;

        public string? Placeholder { get; set; }

        public string? Ayuda { get; set; }

        public bool VisibleEnActa { get; set; } = true;

        public List<OpcionDto> Opciones { get; set; } = [];
    }

    public sealed class OpcionDto
    {
        public string? Valor { get; set; }

        public string? Etiqueta { get; set; }
    }

    public sealed class BloqueDto
    {
        public string? Tipo { get; set; }

        public string? Texto { get; set; }

        public List<string> Campos { get; set; } = [];
    }

    public sealed class FirmaDto
    {
        public string? Clave { get; set; }

        public string? Rotulo { get; set; }

        public string? Origen { get; set; }

        public string? CampoNombre { get; set; }

        public string? CampoDocumento { get; set; }

        public string? NombreFijo { get; set; }

        public string? CargoFijo { get; set; }

        public bool Requerida { get; set; } = true;
    }
}

/// <summary>Firma tal como quedó estampada en un acta emitida.</summary>
public sealed record EspacioActaFirmaEmitida
{
    public string Clave { get; init; } = string.Empty;

    public string Rotulo { get; init; } = string.Empty;

    public string Nombre { get; init; } = string.Empty;

    public string? Documento { get; init; }

    public string? Cargo { get; init; }

    public string DataUrl { get; init; } = string.Empty;
}
