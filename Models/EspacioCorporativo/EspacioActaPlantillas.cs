namespace Nexa.Models.EspacioCorporativo;

/// <summary>
/// Catálogo de plantillas de fábrica y metadatos que consume el diseñador de actas.
///
/// Las plantillas que arma un administrador se guardan en base de datos; estas
/// viven en código porque su cuerpo es HTML de confianza y no se edita desde la
/// interfaz.
/// </summary>
public static class EspacioActaPlantillas
{
    public const string CodigoAccesosTecnologicos = "ACCESOS_TECNOLOGICOS";

    /// <summary>Prefijo de las plantillas creadas desde el diseñador.</summary>
    public const string PrefijoPersonalizada = "PZ_";

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

    /// <summary>Plantillas que vienen con el sistema.</summary>
    public static readonly IReadOnlyList<EspacioActaPlantilla> DeFabrica = [AccesosTecnologicos];

    public static EspacioActaPlantilla? Obtener(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo)
            ? null
            : DeFabrica.FirstOrDefault(x => string.Equals(x.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

    // ─────────────────────────────────────────────────────────────────────────
    // Metadatos para el diseñador
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Tipos de campo que ofrece el diseñador, en el orden en que se muestran.</summary>
    public static readonly IReadOnlyList<EspacioActaTipoCampoInfo> TiposDeCampo =
    [
        new(EspacioActaTipoCampo.Texto, "Texto", "bi-fonts", "Una línea: nombres, cargos, referencias."),
        new(EspacioActaTipoCampo.TextoLargo, "Texto largo", "bi-text-paragraph", "Varias líneas: observaciones, motivos."),
        new(EspacioActaTipoCampo.Seleccion, "Lista de opciones", "bi-ui-radios", "Quien diligencia elige una opción.", AdmiteOpciones: true),
        new(EspacioActaTipoCampo.Numero, "Número", "bi-123", "Cantidades enteras."),
        new(EspacioActaTipoCampo.Decimal, "Decimal", "bi-percent", "Cifras con decimales."),
        new(EspacioActaTipoCampo.Moneda, "Dinero", "bi-cash-coin", "Importes en pesos."),
        new(EspacioActaTipoCampo.Fecha, "Fecha", "bi-calendar-event", "Se imprime en letras: 12 de agosto de 2026."),
        new(EspacioActaTipoCampo.Hora, "Hora", "bi-clock", "Se imprime como 02:30 p. m."),
        new(EspacioActaTipoCampo.Casilla, "Sí / No", "bi-check2-square", "Casilla que se imprime como Sí o No."),
        new(EspacioActaTipoCampo.Correo, "Correo", "bi-envelope-at", "Se valida el formato."),
        new(EspacioActaTipoCampo.Telefono, "Teléfono", "bi-telephone", "Fijo o celular."),
        new(EspacioActaTipoCampo.Documento, "Documento de identidad", "bi-person-vcard", "Cédula o NIT."),
        new(EspacioActaTipoCampo.Enlaces, "Enlaces", "bi-link-45deg", "Una URL por línea; se imprimen como enlaces."),
        new(EspacioActaTipoCampo.Credencial, "Contraseña", "bi-shield-lock", "Se oculta al escribirla.")
    ];

    public static EspacioActaTipoCampoInfo InfoDeTipo(EspacioActaTipoCampo tipo) =>
        TiposDeCampo.FirstOrDefault(x => x.Tipo == tipo)
        ?? new EspacioActaTipoCampoInfo(tipo, tipo.ToString(), "bi-input-cursor-text", string.Empty);

    /// <summary>
    /// Marcadores que el sistema resuelve solo (fecha, ciudad y datos de quien emite).
    /// Se ofrecen en el diseñador junto a los campos de la plantilla.
    /// </summary>
    public static readonly IReadOnlyList<EspacioActaOpcion> MarcadoresDelSistema =
    [
        new("__fecha_completa", "Fecha completa"),
        new("__fecha_dia", "Día"),
        new("__fecha_mes", "Mes"),
        new("__fecha_anio", "Año"),
        new("__ciudad", "Ciudad"),
        new("__firmante_nombre", "Nombre de quien emite"),
        new("__firmante_documento", "Documento de quien emite"),
        new("__firmante_cargo", "Cargo de quien emite")
    ];

    public static bool EsMarcadorDelSistema(string clave) =>
        MarcadoresDelSistema.Any(x => string.Equals(x.Valor, clave, StringComparison.OrdinalIgnoreCase));

    /// <summary>Íconos disponibles para identificar la plantilla en el listado.</summary>
    public static readonly IReadOnlyList<EspacioActaOpcion> Iconos =
    [
        new("bi-file-earmark-text-fill", "Documento"),
        new("bi-key-fill", "Llave"),
        new("bi-laptop", "Equipo"),
        new("bi-shield-check", "Confidencialidad"),
        new("bi-people-fill", "Reunión"),
        new("bi-clipboard-check-fill", "Compromiso"),
        new("bi-box-seam", "Entrega"),
        new("bi-arrow-return-left", "Devolución"),
        new("bi-cash-coin", "Dinero"),
        new("bi-hospital", "Asistencial"),
        new("bi-mortarboard-fill", "Capacitación"),
        new("bi-exclamation-octagon-fill", "Llamado de atención")
    ];

    public static bool EsIconoValido(string? icono) =>
        !string.IsNullOrWhiteSpace(icono)
        && Iconos.Any(x => string.Equals(x.Valor, icono, StringComparison.Ordinal));
}
