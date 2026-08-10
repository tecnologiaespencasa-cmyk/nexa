namespace Nexa.Models.EspacioCorporativo;

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
    Credencial
}

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

public sealed record EspacioActaPlantilla
{
    public required string Codigo { get; init; }

    public required string Nombre { get; init; }

    public required string Descripcion { get; init; }

    /// <summary>Clase de Bootstrap Icons.</summary>
    public required string Icono { get; init; }

    public required string TituloActa { get; init; }

    public required IReadOnlyList<EspacioActaCampo> Campos { get; init; }

    /// <summary>Cuerpo del acta en HTML con marcadores {{clave}}.</summary>
    public required string CuerpoHtml { get; init; }

    /// <summary>Rótulo bajo la firma de quien recibe.</summary>
    public string RotuloRecibe { get; init; } = "Recibe";

    // Claves que se extraen a columnas propias para poder buscar y notificar.
    public required string CampoNombre { get; init; }

    public required string CampoDocumento { get; init; }

    public required string CampoCorreo { get; init; }

    public string? CampoUsuario { get; init; }
}

/// <summary>
/// Catálogo de plantillas de acta. Para agregar una nueva basta con añadir una entrada
/// aquí: el formulario, la previsualización, la firma y el envío por correo son genéricos.
/// </summary>
public static class EspacioActaPlantillas
{
    public const string CodigoAccesosTecnologicos = "ACCESOS_TECNOLOGICOS";

    private static readonly EspacioActaPlantilla AccesosTecnologicos = new()
    {
        Codigo = CodigoAccesosTecnologicos,
        Nombre = "Acta de entrega de accesos tecnológicos",
        Descripcion = "Entrega formal de usuario, contraseña y URLs de un software a un colaborador.",
        Icono = "bi-key-fill",
        TituloActa = "ACTA DE ENTREGA DE ACCESOS TECNOLÓGICOS",
        RotuloRecibe = "Recibe los accesos",
        CampoNombre = "nombre_recibe",
        CampoDocumento = "documento_recibe",
        CampoCorreo = "correo_recibe",
        CampoUsuario = "usuario",
        Campos =
        [
            new EspacioActaCampo
            {
                Clave = "tratamiento",
                Etiqueta = "Tratamiento",
                Tipo = EspacioActaTipoCampo.Seleccion,
                Opciones =
                [
                    new EspacioActaOpcion("al señor", "Señor"),
                    new EspacioActaOpcion("a la señora", "Señora")
                ]
            },
            new EspacioActaCampo
            {
                Clave = "nombre_recibe",
                Etiqueta = "Nombre de quien recibe",
                Placeholder = "Nombre completo",
                MaxLength = 160
            },
            new EspacioActaCampo
            {
                Clave = "documento_recibe",
                Etiqueta = "Documento de identidad",
                Tipo = EspacioActaTipoCampo.Documento,
                Placeholder = "Número de cédula",
                MaxLength = 30
            },
            new EspacioActaCampo
            {
                Clave = "correo_recibe",
                Etiqueta = "Correo electrónico",
                Tipo = EspacioActaTipoCampo.Correo,
                VisibleEnActa = false,
                Placeholder = "nombre@especialistasencasa.com",
                Ayuda = "No aparece en el acta. A este correo se envía la copia firmada.",
                MaxLength = 150
            },
            new EspacioActaCampo
            {
                Clave = "software",
                Etiqueta = "Software",
                Placeholder = "Ej: Manager, Portal administrativo",
                MaxLength = 300
            },
            new EspacioActaCampo
            {
                Clave = "usuario",
                Etiqueta = "Usuario",
                Placeholder = "Ej: LDIAZ",
                MaxLength = 120
            },
            new EspacioActaCampo
            {
                Clave = "contrasena",
                Etiqueta = "Contraseña",
                Tipo = EspacioActaTipoCampo.Credencial,
                Ayuda = "Queda impresa en el acta. Se recomienda exigir cambio en el primer ingreso.",
                MaxLength = 120
            },
            new EspacioActaCampo
            {
                Clave = "urls",
                Etiqueta = "URLs de acceso",
                Tipo = EspacioActaTipoCampo.Enlaces,
                Placeholder = "Una por línea",
                Ayuda = "Escribe una URL por línea; se imprimen como enlaces.",
                MaxLength = 1000
            }
        ],
        CuerpoHtml = """
            <p>
              En la ciudad de {{__ciudad}}, a los {{__fecha_dia}} días del mes de {{__fecha_mes}} del año
              {{__fecha_anio}}, quien suscribe, <strong>{{__firmante_nombre}}</strong>, identificado con
              cédula de ciudadanía No {{__firmante_documento}}, en calidad de {{__firmante_cargo}} de la
              empresa Especialistas en Casa, hace entrega formal de los accesos tecnológicos
              {{tratamiento}} <strong>{{nombre_recibe}}</strong>, identificado con cédula de ciudadanía
              No {{documento_recibe}}, quien asumirá la responsabilidad del uso y administración de estos.
            </p>

            <h2>1. Accesos entregados</h2>
            <ul>
              <li><strong>Software:</strong> {{software}}</li>
              <li><strong>Usuario:</strong> {{usuario}}</li>
              <li><strong>Contraseña:</strong> {{contrasena}}</li>
              <li><strong>URLs:</strong> {{urls}}</li>
            </ul>

            <h2>2. Condiciones de uso y confidencialidad</h2>
            <p>El receptor se compromete a:</p>
            <ul>
              <li>Usar los accesos únicamente para fines laborales autorizados.</li>
              <li>Mantener la confidencialidad de las credenciales.</li>
              <li>No compartir las contraseñas ni permitir el uso de los accesos por parte de terceros.</li>
              <li>Informar de inmediato al área de Tecnología en caso de pérdida, uso indebido o sospecha de acceso no autorizado.</li>
            </ul>

            <h2>3. Aceptación</h2>
            <p>
              Con la firma de la presente acta, el receptor acepta la responsabilidad sobre los accesos
              entregados y se compromete a dar cumplimiento a las condiciones establecidas. También acepta
              que se le informó y se le socializó su correcto uso y funcionamiento.
            </p>
            """
    };

    public static readonly IReadOnlyList<EspacioActaPlantilla> Todas = [AccesosTecnologicos];

    public static EspacioActaPlantilla? Obtener(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo)
            ? null
            : Todas.FirstOrDefault(x => string.Equals(x.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
}
