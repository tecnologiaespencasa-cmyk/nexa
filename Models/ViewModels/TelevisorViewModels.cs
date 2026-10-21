namespace Nexa.Models.ViewModels;

// Modelos del Televisor. El tablero completo (TelevisorTablero) se guarda tal cual como JSON en
// televisor_cortes para que un turno o un día cerrado se vuelva a pintar igual que terminó: por eso
// son clases planas con propiedades públicas y valores por omisión. Si se agrega un campo, una foto
// vieja simplemente lo trae vacío.

public static class TelevisorBloques
{
    public const string Manana = "manana";
    public const string Tarde = "tarde";
    public const string Dia = "dia";
}

public static class TelevisorTurnos
{
    public const string Manana = "manana";
    public const string Tarde = "tarde";
    public const string Unico = "unico";
    public const string FinDeSemana = "fin-de-semana";
    public const string NoTrabaja = "no-trabaja";
    public const string SinConexion = "sin-conexion";

    public static readonly string[] Ajustables = [Manana, Tarde, Unico, FinDeSemana, NoTrabaja];

    public static string Etiqueta(string? turno) => turno switch
    {
        Manana => "Mañana · 6:00 a 14:00",
        Tarde => "Tarde · 14:00 a 22:00",
        Unico => "Único · 8:00 a 17:00",
        FinDeSemana => "Único · 7:00 a 19:00",
        NoTrabaja => "No trabaja hoy",
        _ => "Sin conexión hoy"
    };

    /// <summary>Para las tarjetas, donde no cabe la etiqueta larga.</summary>
    public static string Chip(string? turno) => turno switch
    {
        Manana => "Mañana 6–14",
        Tarde => "Tarde 14–22",
        Unico => "Único 8–17",
        FinDeSemana => "Único 7–19",
        NoTrabaja => "No trabaja",
        _ => "Sin conexión"
    };

    public static string Corta(string? turno) => turno switch
    {
        Manana => "Mañana",
        Tarde => "Tarde",
        Unico => "Único",
        FinDeSemana => "Único",
        NoTrabaja => "No trabaja",
        _ => "Sin conexión"
    };
}

/// <summary>Resultado de un ingreso frente a la promesa de 15 minutos.</summary>
public static class TelevisorPromesa
{
    public const int Minutos = 15;

    public const string Cumple = "cumple";
    public const string NoCumple = "no-cumple";
    public const string Nocturno = "nocturno";
    public const string Revisar = "revisar";
    public const string SinDato = "sin-dato";

    /// <summary>
    /// El ingreso no es de Admisiones (lo recepcionó la líder u otra área, que no llenan la hora del
    /// correo ni la de la respuesta): se muestra, pero ni se mide ni se marca como dato por corregir.
    /// </summary>
    public const string NoAplica = "no-aplica";

    /// <summary>Solo estos dos suman al porcentaje y al balance de minutos.</summary>
    public static bool Cuenta(string? promesa) => promesa is Cumple or NoCumple;
}

/// <summary>
/// Resultado de un ingreso de agudos frente a la meta de creación: desde que se empieza a crear el
/// paciente en el censo hasta que queda con gestión completa, máximo 10 minutos.
/// </summary>
public static class TelevisorCreacion
{
    public const int Minutos = 10;

    public const string Cumple = "cumple";
    public const string NoCumple = "no-cumple";

    /// <summary>Sigue con la gestión pendiente y ya pasaron los 10 minutos: cuenta como fuera de tiempo.</summary>
    public const string Vencida = "vencida";

    /// <summary>Gestión pendiente, todavía dentro de los 10 minutos: aún no cuenta.</summary>
    public const string EnCurso = "en-curso";

    /// <summary>Gestión completa marcada antes de que se empezara a guardar la hora: no se puede medir.</summary>
    public const string SinHora = "sin-hora";

    public static bool Cuenta(string? creacion) => creacion is Cumple or NoCumple or Vencida;
}

public static class TelevisorGestion
{
    public const string Completa = "completa";
    public const string Pendiente = "pendiente";
    public const string NoAplica = "no-aplica";
}

public class TelevisorTablero
{
    public int Version { get; set; } = 1;

    /// <summary>Día en hora Colombia (solo fecha).</summary>
    public DateTime Dia { get; set; }

    public bool EsHabil { get; set; }

    public string? Festivo { get; set; }

    /// <summary>Hasta qué momento (hora Colombia) llegan los datos: la hora actual o la del corte.</summary>
    public DateTime EstadoAl { get; set; }

    public DateTime GeneradoAtUtc { get; set; }

    /// <summary>"vivo", "foto" (corte guardado a tiempo), "recuperada" (corte guardado tarde) o "reconstruido" (sin foto).</summary>
    public string Origen { get; set; } = "vivo";

    public List<TelevisorBloque> Bloques { get; set; } = [];

    public List<TelevisorPersona> Personal { get; set; } = [];

    public TelevisorNovedades Novedades { get; set; } = new();

    /// <summary>Todos los ingresos del día en la cinta, con la hora en que se registraron.</summary>
    public List<TelevisorMarca> Cinta { get; set; } = [];

    public TelevisorBloque? Bloque(string clave) =>
        Bloques.FirstOrDefault(b => string.Equals(b.Clave, clave, StringComparison.Ordinal));
}

public class TelevisorBloque
{
    public string Clave { get; set; } = TelevisorBloques.Dia;

    public string Titulo { get; set; } = string.Empty;

    public DateTime Desde { get; set; }

    public DateTime Hasta { get; set; }

    public TelevisorAdmisiones Admisiones { get; set; } = new();

    public TelevisorAnalistas Analistas { get; set; } = new();

    /// <summary>Novedades del portal resueltas dentro de la ventana del bloque.</summary>
    public int NovedadesResueltas { get; set; }

    public List<TelevisorAlerta> Calidad { get; set; } = [];
}

public class TelevisorAdmisiones
{
    public int Ingresos { get; set; }

    public int Medibles { get; set; }

    public int Cumplen { get; set; }

    public int NoCumplen { get; set; }

    public int Nocturnos { get; set; }

    public int PorRevisar { get; set; }

    /// <summary>Agudos cancelados o rechazados del bloque: se listan, no se cuentan.</summary>
    public int Cancelados { get; set; }

    public double? Porcentaje { get; set; }

    /// <summary>Suma de minutos reales de respuesta de los ingresos medibles.</summary>
    public int MinutosReales { get; set; }

    /// <summary>Lo que deberían sumar: 15 minutos por ingreso medible.</summary>
    public int MinutosPromesa { get; set; }

    public int Balance => MinutosReales - MinutosPromesa;

    public double? PromedioRespuesta { get; set; }

    public int GestionCompletas { get; set; }

    public int GestionPendientes { get; set; }

    public int GestionNoAplica { get; set; }

    /// <summary>Ingresos de agudos que ya se pueden medir contra la meta de creación de 10 minutos.</summary>
    public int CreacionMedibles { get; set; }

    public int CreacionCumplen { get; set; }

    /// <summary>Null mientras no haya ninguno medible (o el día sea anterior a que se guardara la hora).</summary>
    public double? CreacionPorcentaje { get; set; }

    /// <summary>Promedio, en minutos, de los que ya quedaron con gestión completa.</summary>
    public double? CreacionPromedio { get; set; }

    /// <summary>Con la gestión pendiente y todavía dentro de los 10 minutos.</summary>
    public int CreacionEnCurso { get; set; }

    /// <summary>Mediana de minutos entre la respuesta al asegurador y el registro en Nexa.</summary>
    public int? MedianaDemoraRegistro { get; set; }

    public List<TelevisorConteoTurno> PorTurno { get; set; } = [];

    public List<TelevisorPersonaAdmisiones> Personas { get; set; } = [];

    /// <summary>Personas de admisiones sin ingresos ni conexión en el bloque.</summary>
    public List<string> SinActividad { get; set; } = [];

    /// <summary>
    /// En turno pero todavía sin ingresos: van en una línea y no en una tarjeta vacía, que le
    /// quitaría ancho a las listas de quienes sí tienen.
    /// </summary>
    public List<string> EnTurnoSinIngresos { get; set; } = [];
}

public class TelevisorConteoTurno
{
    public string Turno { get; set; } = string.Empty;

    public string Etiqueta { get; set; } = string.Empty;

    public int Pacientes { get; set; }
}

public class TelevisorPersonaAdmisiones
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>"Otros" agrupa a quien no está en Admisiones (la líder, un nombre fuera de la lista o vacío).</summary>
    public bool EsOtros { get; set; }

    public string Turno { get; set; } = TelevisorTurnos.SinConexion;

    public bool Conectada { get; set; }

    /// <summary>El nombre de la tabla de personal no corresponde a ningún usuario de Nexa (p. ej. "AUX DE APOYO").</summary>
    public bool SinUsuario { get; set; }

    /// <summary>Ingresos que figuran a su nombre en "Nombre de quien recepciona el caso".</summary>
    public int Ingresos { get; set; }

    /// <summary>
    /// Ingresos que registró con su usuario, sin importar a nombre de quién quedaron. Null si la
    /// persona no tiene usuario de Nexa (no se puede saber).
    /// </summary>
    public int? Registrados { get; set; }

    public int Medibles { get; set; }

    public int Cumplen { get; set; }

    public double? Porcentaje { get; set; }

    public int Balance { get; set; }

    /// <summary>
    /// Meta de creación de 10 minutos, sobre los pacientes de agudos que la persona REGISTRÓ (no
    /// los que recepcionó): crear el paciente es trabajo de quien lo digita.
    /// </summary>
    public int CreacionMedibles { get; set; }

    public int CreacionCumplen { get; set; }

    public double? CreacionPorcentaje { get; set; }

    /// <summary>
    /// Promedio de minutos entre el correo y la respuesta, en lo que recepcionó y se puede medir
    /// (sin correos de noche ni datos por revisar).
    /// </summary>
    public double? PromedioRespuesta { get; set; }

    /// <summary>Promedio de minutos de creación de los que registró y ya quedaron con gestión completa.</summary>
    public double? CreacionPromedio { get; set; }

    public int CreacionTerminados { get; set; }

    public List<TelevisorIngreso> Lista { get; set; } = [];
}

public class TelevisorIngreso
{
    public long EpisodioId { get; set; }

    public string Programa { get; set; } = string.Empty;

    public string ProgramaCorto { get; set; } = string.Empty;

    /// <summary>Nombre del paciente tal como está en el maestro (en mayúscula inicial).</summary>
    public string Paciente { get; set; } = string.Empty;

    public DateTime Registrado { get; set; }

    public string Bloque { get; set; } = string.Empty;

    public DateTime? Correo { get; set; }

    public DateTime? Respuesta { get; set; }

    public int? MinutosRespuesta { get; set; }

    public string Promesa { get; set; } = TelevisorPromesa.SinDato;

    /// <summary>Por qué hay que revisar el dato, cuando Promesa es "revisar" o "sin-dato".</summary>
    public string? Revision { get; set; }

    public string Recepciona { get; set; } = string.Empty;

    /// <summary>
    /// Quién lo registró en Nexa (el usuario que agregó el programa). No siempre es quien figura
    /// en "recepciona": la tarde suele registrar lo que la mañana respondió. Null si lo creó la
    /// sincronización.
    /// </summary>
    public string? RegistradoPor { get; set; }

    /// <summary>Lo registró una persona distinta de la que lo recepcionó.</summary>
    public bool RegistradoPorOtro { get; set; }

    /// <summary>
    /// Agudos cancelado o rechazado. Se muestra en la lista (un ingreso que desaparece parece un
    /// error del tablero) pero no cuenta como ingreso ni para la promesa, igual que en Reportes.
    /// </summary>
    public bool Cancelado { get; set; }

    public string Gestion { get; set; } = TelevisorGestion.NoAplica;

    /// <summary>Uno de <see cref="TelevisorCreacion"/>; null cuando no aplica (no es agudos o no es de Admisiones).</summary>
    public string? Creacion { get; set; }

    /// <summary>
    /// Minutos desde que se empezó a crear el paciente hasta la gestión completa; si sigue
    /// pendiente, los que lleva hasta el momento del tablero.
    /// </summary>
    public int? MinutosCreacion { get; set; }

    /// <summary>null cuando no aplica (el check solo existe en agudos).</summary>
    public bool? Analista { get; set; }

    public DateTime? AnalistaAt { get; set; }

    public int? MinutosAnalista { get; set; }

    public string? AnalistaPor { get; set; }

    /// <summary>Desde cuándo espera la gestión de la analista (respuesta o, si falta, registro).</summary>
    public DateTime EsperaDesde { get; set; }

    /// <summary>Se registró entre las 6:00 y las 22:00: entra en la meta de las analistas.</summary>
    public bool EnFranjaAnalistas { get; set; }
}

public class TelevisorAnalistas
{
    public int Universo { get; set; }

    public int ConCheck { get; set; }

    public double? Porcentaje { get; set; }

    /// <summary>Ingresos de agudos registrados fuera de la franja de 6:00 a 22:00.</summary>
    public int FueraDeFranja { get; set; }

    public double? PromedioMinutos { get; set; }

    public int ConHora { get; set; }

    public List<TelevisorPersonaAnalista> Personas { get; set; } = [];

    /// <summary>Analistas sin gestiones ni turno en el bloque.</summary>
    public List<string> SinActividad { get; set; } = [];

    public List<TelevisorIngreso> Pendientes { get; set; } = [];
}

public class TelevisorPersonaAnalista
{
    public string Nombre { get; set; } = string.Empty;

    public bool EsOtros { get; set; }

    public string Turno { get; set; } = TelevisorTurnos.SinConexion;

    public bool Conectada { get; set; }

    public int Gestionados { get; set; }

    public double? PromedioMinutos { get; set; }

    public List<TelevisorIngreso> Lista { get; set; } = [];
}

public class TelevisorNovedades
{
    public bool Disponible { get; set; } = true;

    public string? Error { get; set; }

    public int Meta { get; set; } = 48;

    public int? Saldo8am { get; set; }

    /// <summary>"foto" si se tomó a las 8:00, "reconstruido" si se dedujo de las fechas del portal.</summary>
    public string? Saldo8amOrigen { get; set; }

    public int Ingresadas { get; set; }

    public int Resueltas { get; set; }

    public int PendientesAlCorte { get; set; }

    public int? DiasMasAntigua { get; set; }

    public List<TelevisorNovedadArea> PorArea { get; set; } = [];

    /// <summary>Hora (Colombia) de cada novedad resuelta en el día, para repartirlas por bloque.</summary>
    public List<DateTime> ResueltasAt { get; set; } = [];
}

public class TelevisorNovedadArea
{
    public string Area { get; set; } = string.Empty;

    public string Etiqueta { get; set; } = string.Empty;

    public int Pendientes { get; set; }

    public int Ingresadas { get; set; }

    public int Resueltas { get; set; }
}

public class TelevisorPersona
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Rol { get; set; } = string.Empty;

    public string? Usuario { get; set; }

    public string Turno { get; set; } = TelevisorTurnos.SinConexion;

    public bool TurnoAjustado { get; set; }

    public DateTime? PrimeraConexion { get; set; }

    public DateTime? UltimaActividad { get; set; }

    public bool Conectada { get; set; }
}

public class TelevisorMarca
{
    public DateTime Registrado { get; set; }

    public string Promesa { get; set; } = TelevisorPromesa.SinDato;
}

public class TelevisorAlerta
{
    public string Tipo { get; set; } = string.Empty;

    public string Texto { get; set; } = string.Empty;

    public string? Paciente { get; set; }

    public string? Persona { get; set; }
}

/// <summary>Lo que recibe la vista del Televisor.</summary>
public class TelevisorPantallaViewModel
{
    public TelevisorTablero Tablero { get; set; } = new();

    public string BloqueClave { get; set; } = TelevisorBloques.Dia;

    /// <summary>El bloque lo eligió el servidor según la hora (mañana antes del corte, tarde después).</summary>
    public bool BloqueAutomatico { get; set; }

    public bool EsHoy { get; set; }

    public DateTime AhoraColombia { get; set; }

    public long ServidorUtcMs { get; set; }

    /// <summary>Si la mañana se está mostrando desde su foto de la 1:45 p. m.</summary>
    public bool MananaCongelada { get; set; }

    public string? Error { get; set; }

    public TelevisorBloque? Bloque => Tablero.Bloque(BloqueClave) ?? Tablero.Bloques.LastOrDefault();
}
