namespace Nexa.Models.EspacioCorporativo;

/// <summary>
/// Actas ya redactadas que sirven de punto de partida en el diseñador.
///
/// Empezar de una hoja en blanco es lo que más traba a quien no está acostumbrado a
/// armar documentos: estos modelos traen el texto, los datos y las firmas típicas de
/// cada caso, y desde ahí solo hay que ajustar palabras.
/// </summary>
public static class EspacioActaModelos
{
    public sealed record Modelo(
        string Clave,
        string Nombre,
        string Descripcion,
        string Icono,
        EspacioActaDefinicionDto Definicion);

    private static EspacioActaDefinicionDto.CampoDto Campo(
        string clave,
        string etiqueta,
        string tipo,
        string? pista = null,
        bool visible = true,
        string? ayuda = null) =>
        new()
        {
            Clave = clave,
            Etiqueta = etiqueta,
            Tipo = tipo,
            Placeholder = pista,
            VisibleEnActa = visible,
            Ayuda = ayuda
        };

    private static EspacioActaDefinicionDto.BloqueDto Parrafo(string texto) =>
        new() { Tipo = nameof(EspacioActaTipoBloque.Parrafo), Texto = texto };

    private static EspacioActaDefinicionDto.BloqueDto Titulo(string texto) =>
        new() { Tipo = nameof(EspacioActaTipoBloque.Titulo), Texto = texto };

    private static EspacioActaDefinicionDto.BloqueDto Lista(string texto) =>
        new() { Tipo = nameof(EspacioActaTipoBloque.Lista), Texto = texto };

    private static EspacioActaDefinicionDto.BloqueDto Datos(params string[] campos) =>
        new() { Tipo = nameof(EspacioActaTipoBloque.Datos), Campos = [.. campos] };

    /// <summary>Apertura estándar: ciudad, fecha y quién suscribe.</summary>
    private const string Encabezado =
        "En la ciudad de {{__ciudad}}, a los {{__fecha_dia}} días del mes de {{__fecha_mes}} "
        + "del año {{__fecha_anio}}, quien suscribe, **{{__firmante_nombre}}**, identificado con "
        + "cédula de ciudadanía No {{__firmante_documento}}, en calidad de {{__firmante_cargo}} "
        + "de Especialistas en Casa, deja constancia de lo siguiente:";

    private static readonly List<EspacioActaDefinicionDto.CampoDto> DatosDeLaPersona =
    [
        Campo("nombre", "Nombre completo", nameof(EspacioActaTipoCampo.Texto), "Nombre de quien firma"),
        Campo("documento", "Documento de identidad", nameof(EspacioActaTipoCampo.Documento), "Número de cédula"),
        Campo(
            "correo",
            "Correo electrónico",
            nameof(EspacioActaTipoCampo.Correo),
            "nombre@especialistasencasa.com",
            visible: false,
            ayuda: "No sale en el acta. A este correo se envía la copia firmada.")
    ];

    private static List<EspacioActaDefinicionDto.FirmaDto> FirmasBasicas(string rotuloRecibe) =>
    [
        new()
        {
            Clave = EspacioActaFirma.ClaveEmisor,
            Rotulo = "Entrega",
            Origen = nameof(EspacioActaFirmaOrigen.Emisor)
        },
        new()
        {
            Clave = EspacioActaFirma.ClaveRecibe,
            Rotulo = rotuloRecibe,
            Origen = nameof(EspacioActaFirmaOrigen.EnVivo),
            CampoNombre = "nombre",
            CampoDocumento = "documento"
        }
    ];

    public static readonly IReadOnlyList<Modelo> Todos =
    [
        new(
            "entrega",
            "Entrega de elementos",
            "Dotación, herramientas o equipos que se entregan a un colaborador.",
            "bi-box-seam",
            new EspacioActaDefinicionDto
            {
                TituloActa = "ACTA DE ENTREGA DE ELEMENTOS",
                Campos =
                [
                    .. DatosDeLaPersona,
                    Campo("elementos", "Elementos entregados", nameof(EspacioActaTipoCampo.TextoLargo), "Uno por línea"),
                    Campo("valor", "Valor total", nameof(EspacioActaTipoCampo.Moneda), "1250000")
                ],
                Bloques =
                [
                    Parrafo(Encabezado),
                    Parrafo(
                        "Se hace entrega a **{{nombre}}**, identificado con cédula de ciudadanía "
                        + "No {{documento}}, de los elementos que se relacionan a continuación."),
                    Titulo("Elementos entregados"),
                    Datos("elementos", "valor"),
                    Titulo("Compromisos de quien recibe"),
                    Lista(
                        "Usar los elementos únicamente para labores de la empresa.\n"
                        + "Cuidarlos y reportar cualquier daño o pérdida.\n"
                        + "Devolverlos al terminar el contrato o cuando la empresa lo solicite."),
                    Titulo("Aceptación"),
                    Parrafo(
                        "Con la firma de la presente acta, quien recibe declara haber recibido los "
                        + "elementos en buen estado y acepta los compromisos aquí descritos.")
                ],
                Firmas = FirmasBasicas("Recibe")
            }),

        new(
            "compromiso",
            "Compromiso o acuerdo",
            "Deja por escrito un compromiso que asume un colaborador.",
            "bi-clipboard-check-fill",
            new EspacioActaDefinicionDto
            {
                TituloActa = "ACTA DE COMPROMISO",
                Campos =
                [
                    .. DatosDeLaPersona,
                    Campo("cargo", "Cargo", nameof(EspacioActaTipoCampo.Texto), "Cargo que ocupa"),
                    Campo("compromiso", "Compromiso que asume", nameof(EspacioActaTipoCampo.TextoLargo)),
                    Campo("fecha_limite", "Fecha límite", nameof(EspacioActaTipoCampo.Fecha))
                ],
                Bloques =
                [
                    Parrafo(Encabezado),
                    Parrafo(
                        "**{{nombre}}**, identificado con cédula de ciudadanía No {{documento}}, "
                        + "quien se desempeña como {{cargo}}, asume de manera voluntaria el "
                        + "compromiso que se describe a continuación."),
                    Titulo("Compromiso"),
                    Parrafo("{{compromiso}}"),
                    Titulo("Plazo y seguimiento"),
                    Parrafo(
                        "El compromiso debe cumplirse a más tardar el {{fecha_limite}}. "
                        + "Su cumplimiento será verificado por el jefe inmediato."),
                    Titulo("Aceptación"),
                    Parrafo(
                        "Con la firma de la presente acta, las partes aceptan su contenido y "
                        + "dejan constancia de que fue leída y explicada.")
                ],
                Firmas = FirmasBasicas("Se compromete")
            }),

        new(
            "reunion",
            "Reunión",
            "Registro de los temas tratados y las decisiones de una reunión.",
            "bi-people-fill",
            new EspacioActaDefinicionDto
            {
                TituloActa = "ACTA DE REUNIÓN",
                Campos =
                [
                    Campo("nombre", "Quién dirige la reunión", nameof(EspacioActaTipoCampo.Texto), "Nombre completo"),
                    Campo("documento", "Documento de identidad", nameof(EspacioActaTipoCampo.Documento), "Número de cédula"),
                    Campo(
                        "correo",
                        "Correo electrónico",
                        nameof(EspacioActaTipoCampo.Correo),
                        "nombre@especialistasencasa.com",
                        visible: false,
                        ayuda: "No sale en el acta. A este correo se envía la copia firmada."),
                    Campo("asunto", "Asunto de la reunión", nameof(EspacioActaTipoCampo.Texto)),
                    Campo("lugar", "Lugar", nameof(EspacioActaTipoCampo.Texto), "Sala, sede o enlace"),
                    Campo("hora", "Hora de inicio", nameof(EspacioActaTipoCampo.Hora)),
                    Campo("asistentes", "Asistentes", nameof(EspacioActaTipoCampo.TextoLargo), "Uno por línea"),
                    Campo("temas", "Temas tratados", nameof(EspacioActaTipoCampo.TextoLargo)),
                    Campo("compromisos", "Decisiones y compromisos", nameof(EspacioActaTipoCampo.TextoLargo))
                ],
                Bloques =
                [
                    Parrafo(
                        "En {{lugar}}, el {{__fecha_completa}} a las {{hora}}, se reunieron los "
                        + "colaboradores de Especialistas en Casa para tratar el asunto "
                        + "**{{asunto}}**. La reunión fue dirigida por **{{nombre}}**."),
                    Titulo("Asistentes"),
                    Parrafo("{{asistentes}}"),
                    Titulo("Temas tratados"),
                    Parrafo("{{temas}}"),
                    Titulo("Decisiones y compromisos"),
                    Parrafo("{{compromisos}}"),
                    Titulo("Cierre"),
                    Parrafo(
                        "Sin más asuntos por tratar, se da por terminada la reunión y se firma "
                        + "la presente acta por quienes en ella intervinieron.")
                ],
                Firmas =
                [
                    new()
                    {
                        Clave = EspacioActaFirma.ClaveEmisor,
                        Rotulo = "Elabora el acta",
                        Origen = nameof(EspacioActaFirmaOrigen.Emisor)
                    },
                    new()
                    {
                        Clave = EspacioActaFirma.ClaveRecibe,
                        Rotulo = "Dirige la reunión",
                        Origen = nameof(EspacioActaFirmaOrigen.EnVivo),
                        CampoNombre = "nombre",
                        CampoDocumento = "documento"
                    }
                ]
            }),

        new(
            "blanco",
            "Empezar en blanco",
            "Solo los datos de la persona y las dos firmas. El texto lo escribes tú.",
            "bi-file-earmark",
            new EspacioActaDefinicionDto
            {
                TituloActa = string.Empty,
                Campos = [.. DatosDeLaPersona],
                Bloques = [Parrafo(Encabezado)],
                Firmas = FirmasBasicas("Recibe")
            })
    ];

    public static Modelo? Obtener(string? clave) =>
        string.IsNullOrWhiteSpace(clave)
            ? null
            : Todos.FirstOrDefault(x => string.Equals(x.Clave, clave, StringComparison.OrdinalIgnoreCase));
}
