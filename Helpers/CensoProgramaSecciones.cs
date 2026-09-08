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
}
