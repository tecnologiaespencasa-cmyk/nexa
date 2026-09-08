using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

/// <summary>
/// Catálogo de los programas del censo. La clave que se guarda en base de datos es la constante,
/// no el nombre visible, para poder cambiar el rótulo sin migrar datos.
/// </summary>
public static class CensoProgramas
{
    public const string Agudos = "AGUDOS";
    public const string Cronicos = "CRONICOS";
    public const string ClinicaHeridas = "CLINICA_HERIDAS";
    public const string Npt = "NPT";
    public const string TerapiaAmbulatoria = "TERAPIA_AMBULATORIA";

    /// <summary>
    /// VAC no es un programa que se agregue al paciente: es un estado del censo de clínica de
    /// heridas (sección "Manejo de la herida", campo VAC en Sí). Existe aquí solo para el informe
    /// de pacientes activos, donde pesa más que clínica de heridas. Por eso queda deliberadamente
    /// fuera de <see cref="Todos"/>: no se puede agregar ni abrir como episodio.
    /// </summary>
    public const string Vac = "VAC";

    public static readonly string[] Todos =
    [
        Agudos,
        Cronicos,
        ClinicaHeridas,
        Npt,
        TerapiaAmbulatoria
    ];

    public static bool EsValido(string? programa) =>
        !string.IsNullOrWhiteSpace(programa) && Todos.Contains(programa, StringComparer.Ordinal);

    public static string Nombre(string? programa) => programa switch
    {
        Agudos => "Programa agudos",
        Cronicos => "Programa crónicos",
        Vac => "VAC",
        ClinicaHeridas => "Clínica de heridas",
        Npt => "NPT",
        TerapiaAmbulatoria => "Terapia ambulatoria",
        _ => programa ?? string.Empty
    };

    /// <summary>Rótulo corto para las etiquetas del tabulado unificado.</summary>
    public static string NombreCorto(string? programa) => programa switch
    {
        Agudos => "Agudos",
        Cronicos => "Crónicos",
        Vac => "VAC",
        ClinicaHeridas => "Heridas",
        Npt => "NPT",
        TerapiaAmbulatoria => "Terapia",
        _ => programa ?? string.Empty
    };

    /// <summary>
    /// Jerarquía para el informe de pacientes activos: cuando un paciente está activo en varios
    /// programas se reporta una sola vez, con el programa de menor número. La define la operación:
    /// 1 NPT, 2 crónicos, 3 VAC, 4 clínica de heridas, 5 agudos, 6 terapia ambulatoria.
    ///
    /// <see cref="Vac"/> solo aparece en el informe: un paciente de clínica de heridas con VAC en
    /// Sí se reporta como VAC, y vuelve a reportarse como clínica de heridas en cuanto lo pasan a
    /// No, porque el informe lee el estado del momento y no guarda nada.
    ///
    /// También ordena los programas en el carril del paciente y en el navegador lateral, para que
    /// el orden de la pantalla y el del informe sean el mismo.
    /// </summary>
    public static int Jerarquia(string? programa) => programa switch
    {
        Npt => 1,
        Cronicos => 2,
        Vac => 3,
        ClinicaHeridas => 4,
        Agudos => 5,
        TerapiaAmbulatoria => 6,
        _ => 99
    };

    /// <summary>
    /// Agudos y crónicos no pueden estar abiertos al mismo tiempo para el mismo paciente. Sí puede
    /// haber sido agudo antes y ser crónico ahora: la exclusión solo aplica entre programas abiertos.
    /// </summary>
    /// <summary>
    /// Programas base: el paciente solo puede tener uno abierto a la vez. Los demás
    /// (clínica de heridas y NPT) se suman al base que corresponda.
    /// </summary>
    public static readonly string[] Base =
    [
        Agudos,
        Cronicos,
        TerapiaAmbulatoria
    ];

    public static bool EsBase(string? programa) =>
        Base.Contains(programa, StringComparer.Ordinal);

    /// <summary>
    /// Dos programas no pueden estar abiertos a la vez si son dos bases distintos, o si
    /// alguno es terapia ambulatoria.
    ///
    /// Terapia ambulatoria es exclusiva con **todo**, incluidos los adicionales: es una
    /// modalidad ambulatoria completa, no una atención que se sume a un domiciliario.
    /// </summary>
    public static bool SonExcluyentes(string? a, string? b) =>
        a is not null
        && b is not null
        && !string.Equals(a, b, StringComparison.Ordinal)
        && (a == TerapiaAmbulatoria || b == TerapiaAmbulatoria || (EsBase(a) && EsBase(b)));

    /// <summary>
    /// De los programas abiertos del paciente, el primero que impide abrir <paramref name="programa"/>.
    /// Devuelve null si no hay ninguno. Se resuelve contra lo que el paciente tiene de verdad y no
    /// contra una tabla fija de parejas, porque terapia ambulatoria choca con los cuatro restantes.
    /// </summary>
    public static string? PrimeroQueBloquea(string? programa, IEnumerable<string?> abiertos) =>
        abiertos.FirstOrDefault(abierto => SonExcluyentes(programa, abierto));
}

/// <summary>
/// Un episodio del paciente en un programa. Es la fuente de verdad de "qué programas tiene este
/// paciente": el selector de la pantalla, la validación de exclusión agudos/crónicos y la jerarquía
/// del informe de activos leen de aquí.
///
/// <see cref="RegistroId"/> apunta a la fila de la tabla propia del programa (censo, censo_cronicos,
/// censo_clinica_heridas, censo_npt, censo_terapias_ambulatorias). Queda nulo entre que el usuario
/// añade el programa y guarda por primera vez su formulario.
///
/// Un paciente puede tener varios episodios cerrados del mismo programa (agudos genera uno por
/// atención y terapia ambulatoria uno por tratamiento), pero solo uno abierto a la vez.
/// </summary>
public class CensoPacientePrograma
{
    [Key]
    public long Id { get; set; }

    public long CensoPacienteId { get; set; }

    public CensoPaciente CensoPaciente { get; set; } = null!;

    [Required]
    [StringLength(30)]
    public string Programa { get; set; } = string.Empty;

    public long? RegistroId { get; set; }

    public DateTime AgregadoAtUtc { get; set; } = DateTime.UtcNow;

    [StringLength(200)]
    public string? AgregadoPor { get; set; }

    public DateTime? CerradoAtUtc { get; set; }

    [StringLength(200)]
    public string? CerradoPor { get; set; }

    /// <summary>Motivo con el que se cerró el episodio (alta, egreso, cancelación...).</summary>
    [StringLength(120)]
    public string? MotivoCierre { get; set; }

    public bool EstaAbierto => CerradoAtUtc is null;
}
