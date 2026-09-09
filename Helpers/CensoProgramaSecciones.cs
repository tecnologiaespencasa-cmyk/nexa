using Nexa.Data.Entities;

namespace Nexa.Helpers;

/// <summary>Una sección navegable dentro de un programa.</summary>
public sealed record CensoSeccion(string Id, string Eyebrow, string Titulo, string Descripcion);

/// <summary>
/// Catálogo de las secciones de cada programa. Es la fuente única del navegador lateral de la
/// pantalla unificada: la barra se arma desde aquí y cada parcial de programa solo renderiza los
/// paneles con estos mismos identificadores.
///
/// Los identificadores llevan el prefijo del programa a propósito. Antes de la unificación cada
/// censo vivía en su propia página y varios repetían ids como "tab-datos-basicos" o
/// "tab-gestion-alta"; al convivir en un solo documento esos ids chocarían.
/// </summary>
public static class CensoProgramaSecciones
{
    /// <summary>Recepción y datos básicos: se capturan una vez para todo paciente.</summary>
    public static readonly IReadOnlyList<CensoSeccion> Paciente =
    [
        new("tab-paciente-recepcion", "Recepción",
            "Recepción del paciente",
            "Ingreso, tiempos de respuesta y responsables del caso."),
        new("tab-paciente-datos-basicos", "Paciente",
            "Datos básicos del paciente",
            "Identificación, diagnóstico, dirección, ubicación y contacto.")
    ];

    private static readonly IReadOnlyList<CensoSeccion> Agudos =
    [
        // Agudos es el censo de referencia: su recepción y sus datos básicos son los que subieron al
        // maestro, así que no tiene sección de datos específicos.
        new("tab-agudos-plan-manejo", "Plan", "Plan de manejo",
            "Riesgo, tratamiento, promesa de inicio, apoyos complementarios y seguimiento."),
        new("tab-agudos-gestion-alta", "Alta", "Gestión de alta",
            "Fecha de alta y responsable que cierra el servicio."),
        new("tab-agudos-seguimiento-alta-tardia", "Seguimiento", "Seguimiento alta tardía",
            "Seguimiento a 24, 48 y 72 horas con observaciones."),
        new("tab-agudos-seguimiento-hospitalizacion", "Hospitalización", "Seguimiento hospitalización",
            "Rehospitalización, motivos, IPS implicadas y tiempos."),
        new("tab-agudos-devolucion-productos", "Devolución", "Devolución de productos",
            "Novedades, notificaciones y estado con servicio farmacéutico."),
        new("tab-agudos-prorroga", "Prórroga", "Prórroga o cambio",
            "Tratamiento de la prórroga, independiente de la atención original.")
    ];

    private static readonly IReadOnlyList<CensoSeccion> Cronicos =
    [
        new("tab-cronicos-datos-especificos", "Específicos", "Datos específicos",
            "Lo que crónicos pide además de los datos básicos del paciente."),
        new("tab-cronicos-gestion-caso", "Caso", "Gestión del caso",
            "Clasificación, patologías y escalas de valoración."),
        new("tab-cronicos-agudizaciones", "Agudizaciones", "Agudizaciones",
            "Episodios agudos con kardex y requisición propios."),
        new("tab-cronicos-hospitalizacion", "Hospitalización", "Hospitalización y seguimiento",
            "Episodios de hospitalización y sus seguimientos."),
        new("tab-cronicos-gestion-alta", "Alta", "Gestión del alta",
            "Egreso del programa crónico y su motivo.")
    ];

    private static readonly IReadOnlyList<CensoSeccion> ClinicaHeridas =
    [
        new("tab-heridas-datos-especificos", "Específicos", "Datos específicos",
            "Lo que clínica de heridas pide además de los datos básicos."),
        new("tab-heridas-manejo", "Manejo", "Manejo de la herida",
            "Apósitos, duración y frecuencia; origen de las requisiciones."),
        new("tab-heridas-caracteristicas", "Herida", "Características de la herida",
            "Seguimientos que registra la aplicación de clínica de heridas."),
        new("tab-heridas-activo-fijo", "Activo fijo", "Activo fijo",
            "Equipos en comodato entregados al paciente."),
        new("tab-heridas-seguimiento-hospitalizado", "Hospitalización", "Seguimiento hospitalizado",
            "Hospitalización, motivo, IPS y seguimientos."),
        new("tab-heridas-devolucion-productos", "Devolución", "Devolución de productos",
            "Novedades y estado con servicio farmacéutico."),
        new("tab-heridas-alta-programa", "Alta", "Alta del programa",
            "Egreso del programa de clínica de heridas.")
    ];

    private static readonly IReadOnlyList<CensoSeccion> Npt =
    [
        new("tab-npt-datos-especificos", "Específicos", "Datos específicos",
            "Lo que NPT pide además de los datos básicos del paciente."),
        new("tab-npt-manejo", "Manejo", "Manejo de la NPT",
            "Fechas, horas de conexión y días de tratamiento."),
        new("tab-npt-cargue-servicios", "Servicios", "Cargue de servicios",
            "Laboratorios, glucometría y servicios complementarios."),
        new("tab-npt-activo-fijo", "Activo fijo", "Activo fijo",
            "Equipos en comodato entregados al paciente."),
        new("tab-npt-seguimiento-hospitalizado", "Hospitalización", "Seguimiento hospitalizado",
            "Hospitalización, motivo, IPS y seguimientos."),
        new("tab-npt-devolucion-productos", "Devolución", "Devolución de productos",
            "Novedades y estado con servicio farmacéutico."),
        new("tab-npt-alta-programa", "Alta", "Alta del programa",
            "Egreso del programa de NPT.")
    ];

    private static readonly IReadOnlyList<CensoSeccion> TerapiaAmbulatoria =
    [
        new("tab-terapia-datos-especificos", "Específicos", "Datos específicos",
            "Tratamientos, autorización, fisioterapeuta y estado del paciente."),
        new("tab-terapia-prorroga", "Prórroga", "Prórroga",
            "Prórrogas de terapia con su autorización y frecuencia."),
        new("tab-terapia-gestion-alta", "Alta", "Gestión alta",
            "Alta del paciente y motivo.")
    ];

    public static IReadOnlyList<CensoSeccion> De(string? programa) => programa switch
    {
        CensoProgramas.Agudos => Agudos,
        CensoProgramas.Cronicos => Cronicos,
        CensoProgramas.ClinicaHeridas => ClinicaHeridas,
        CensoProgramas.Npt => Npt,
        CensoProgramas.TerapiaAmbulatoria => TerapiaAmbulatoria,
        _ => []
    };

    /// <summary>Primera sección del programa: la que se abre al entrar en él.</summary>
    public static string? PrimeraSeccion(string? programa) => De(programa).FirstOrDefault()?.Id;

    // ==========================================================================================
    // Qué se puede seguir diligenciando después del alta
    //
    // Una atención cerrada se puede abrir para consultarla, pero sus datos clínicos ya no se
    // editan: reescribirlos sin querer fue el error que se corrigió el 2026-09-09. Lo que sí
    // sigue vivo son las secciones cuyo trabajo ocurre, por diseño, DESPUÉS del alta: al
    // paciente se le recogen los productos y el equipo en comodato días más tarde, y si
    // reingresa a un hospital hay que registrarlo contra la atención que lo generó.
    //
    // Esta lista es el candado. Lo aplican tanto la vista —que no dibuja el formulario ni el
    // botón de guardar de una sección bloqueada— como el servidor, en
    // CensoController.Unificado.AtencionCerradaBloquea.
    // ==========================================================================================
    private static readonly HashSet<string> EditablesTrasElAlta = new(StringComparer.Ordinal)
    {
        // Agudos
        "tab-agudos-seguimiento-alta-tardia",   // el seguimiento a 24/48/72 horas es posterior al alta
        "tab-agudos-seguimiento-hospitalizacion",
        "tab-agudos-devolucion-productos",

        // Crónicos
        "tab-cronicos-hospitalizacion",

        // Clínica de heridas
        "tab-heridas-activo-fijo",              // la devolución del equipo en comodato se registra aquí
        "tab-heridas-seguimiento-hospitalizado",
        "tab-heridas-devolucion-productos",

        // NPT
        "tab-npt-activo-fijo",
        "tab-npt-seguimiento-hospitalizado",
        "tab-npt-devolucion-productos"

        // Terapia ambulatoria no tiene ninguna: sus tres secciones son la atención misma.
    };

    /// <summary>
    /// True si la sección se puede seguir diligenciando cuando la atención ya está cerrada.
    /// Todo lo demás queda de solo lectura, incluida la gestión del alta: cambiarla cambiaría
    /// el estado por el que la atención está cerrada, y eso es reabrirla, no editarla.
    /// </summary>
    public static bool SeEditaTrasElAlta(string? seccionId) =>
        seccionId is not null && EditablesTrasElAlta.Contains(seccionId);

    /// <summary>
    /// Campos de agudos que pertenecen a las tres secciones posteriores al alta.
    ///
    /// Los otros cuatro programas guardan sección por sección —cada botón lleva su propio
    /// formaction y la acción escribe únicamente sus campos—, así que allí basta con dejar pasar
    /// o no la acción entera. Agudos no: su formulario entero viaja en un solo POST a
    /// ProgramaAgudos, de modo que sobre una atención cerrada hay que dejar entrar estos campos y
    /// devolver todos los demás a su valor guardado. Lo hace
    /// CensoController.RevertirCamposBloqueadosDeAgudos, comparando contra lo que EF tiene como
    /// valor original.
    ///
    /// Si algún día se agrega un campo a "Seguimiento alta tardía", "Seguimiento hospitalización"
    /// o "Devolución de productos", tiene que aparecer aquí o quedará congelado tras el alta.
    /// </summary>
    public static readonly IReadOnlySet<string> CamposDeAgudosTrasElAlta = new HashSet<string>(StringComparer.Ordinal)
    {
        // Seguimiento alta tardía
        "AltaTardia",
        "NombreQuienRealizaSeguimientoAltaTardia",
        "FechaPrimerSeguimiento24Horas",
        "FechaSegundoSeguimiento48Horas",
        "FechaTercerSeguimiento72Horas",
        "ObservacionAltaTardia",

        // Seguimiento hospitalización
        "PacienteRehospitalizado",
        "FechaRehospitalizacion",
        "MotivoRehospitalizacion",
        "AmpliacionMotivoRehospitalizacion",
        "IpsIntramuralRehospitalizacion",
        "RemitidoPorRehospitalizacion",
        "FechaRegistroReporteRehospitalizacion",
        "FechaAltaHospitalizacion",
        "FechaPrimerSeguimientoRehospitalizacion",
        "FechaSegundoSeguimientoRehospitalizacion",
        "FechaTercerSeguimientoRehospitalizacion",
        "ObservacionRehospitalizacion",

        // Devolución de productos
        "FechaNovedadDevolucionProductos",
        "MotivoNovedadDevolucionProductos",
        "NotificacionAuxiliarDevolucionProductos",
        "FechaMaximaDevolucionProductos",
        "EstadoDevolucionServicioFarmaceutico"

        // No lleva marca de auditoría: censo_paciente no tiene columna de última actualización,
        // a diferencia de las tablas de los otros programas. Quién guardó y cuándo queda en la
        // bitácora que escribe _auditService.
    };
}
