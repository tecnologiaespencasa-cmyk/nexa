using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Nexa.Models.ViewModels;

/// <summary>
/// Paneles de detalle del tablero. Cada programa del censo tiene el suyo, más el del Portal
/// Administrativo. La clave viaja en la URL (<c>vista</c>) para que al aplicar un filtro la página
/// vuelva abierta en el mismo panel.
/// </summary>
public static class ReportesVistas
{
    public const string Agudos = "agudos";
    public const string Cronicos = "cronicos";
    public const string Heridas = "heridas";
    public const string Npt = "npt";
    public const string Terapia = "terapia";
    public const string Portal = "portal";

    public static readonly string[] Todas = [Agudos, Cronicos, Heridas, Npt, Terapia, Portal];

    public static string Normalizar(string? vista, string? programaHeredado)
    {
        var valor = (vista ?? string.Empty).Trim().ToLowerInvariant();
        if (Todas.Contains(valor, StringComparer.Ordinal))
        {
            return valor;
        }

        // Los enlaces anteriores filtraban con Programa=Agudos / Programa=Terapias ambulatorias.
        return (programaHeredado ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "terapias ambulatorias" or "terapia ambulatoria" => Terapia,
            "cronicos" or "crónicos" => Cronicos,
            "clinica de heridas" or "clínica de heridas" => Heridas,
            "npt" => Npt,
            _ => Agudos
        };
    }
}

public class ReportesDashboardViewModel
{
    public DateTime GeneradoLocal { get; init; }

    /// <summary>Fecha de hoy en Colombia: la de "activos hoy" y la de los rangos rápidos.</summary>
    public DateTime Hoy { get; init; }

    public ReportesFilterViewModel Filters { get; init; } = new();

    public ReportesFilterOptionsViewModel FilterOptions { get; init; } = new();

    public IReadOnlyList<ReportesPresetViewModel> Presets { get; init; } = [];

    public string PeriodoTexto { get; init; } = string.Empty;

    public int DiasPeriodo { get; init; }

    /// <summary>"día", "semana" o "mes": cómo se agrupan las series según el largo del periodo.</summary>
    public string Granularidad { get; init; } = "día";

    public string Vista { get; init; } = ReportesVistas.Agudos;

    /// <summary>Tarjetas de arriba: la foto de hoy, sin ningún filtro.</summary>
    public ReportesCensoHoyViewModel CensoHoy { get; init; } = new();

    public ReportesPortalHoyViewModel PortalHoy { get; init; } = new();

    public ReportesAgudosViewModel Agudos { get; init; } = new();

    public ReportesCronicosViewModel Cronicos { get; init; } = new();

    public ReportesHeridasViewModel Heridas { get; init; } = new();

    public ReportesNptViewModel Npt { get; init; } = new();

    public ReportesTerapiaViewModel Terapia { get; init; } = new();

    public ReportesPortalViewModel Portal { get; init; } = new();

    public IReadOnlyList<string> ActiveFilterLabels { get; init; } = [];

    /// <summary>
    /// Parámetros del tablero que deben sobrevivir cuando se navega el tabulado (y al revés), para que
    /// consultar el historial no devuelva el tablero al rango por omisión.
    /// </summary>
    public IReadOnlyDictionary<string, string> RutaTablero { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Tabulado unificado del censo. Vive al final de esta pantalla: es consulta de historial, no
    /// parte de la captura del paciente.
    /// </summary>
    public CensoUnificadoViewModel TabuladoCenso { get; init; } = new();

    public IEnumerable<ReportesProgramaViewModel> ProgramasCenso =>
        [Agudos, Cronicos, Heridas, Npt, Terapia];
}

/// <summary>
/// "Censo de hoy": activos de cada programa en este momento, sin filtros de fecha ni de municipio.
/// Las tarjetas cuentan atenciones (registros activos); <see cref="PacientesUnicos"/> cuenta personas.
/// </summary>
public sealed class ReportesCensoHoyViewModel
{
    public IReadOnlyList<ReportesCensoHoyProgramaViewModel> Programas { get; init; } = [];

    public int PacientesUnicos { get; init; }

    public int PacientesEnVariosProgramas { get; init; }

    /// <summary>Suma de las cinco tarjetas: da más que las personas cuando alguien está en varios programas.</summary>
    public int SumaDeTarjetas => Programas.Sum(x => x.Activos);

    /// <summary>
    /// Veces que un paciente cuenta en una tarjeta además de la primera (quien está en tres programas
    /// suma dos). Con las dos siguientes explica exactamente la diferencia:
    /// <see cref="SumaDeTarjetas"/> = <see cref="PacientesUnicos"/> + estas tres.
    /// </summary>
    public int CuentasEnOtrosProgramas { get; init; }

    /// <summary>Atenciones activas de más de un mismo paciente dentro de un mismo programa (p. ej. dos terapias).</summary>
    public int AtencionesRepetidasEnUnPrograma { get; init; }

    /// <summary>Atenciones activas sin número de documento: suman en su tarjeta pero no se pueden contar como persona.</summary>
    public int AtencionesSinDocumento { get; init; }

    /// <summary>Solo las combinaciones de dos o más programas, de la más frecuente a la menos.</summary>
    public IReadOnlyList<ReportesCombinacionViewModel> Combinaciones { get; init; } = [];
}

public sealed class ReportesCensoHoyProgramaViewModel
{
    /// <summary>Clave de <c>CensoProgramas</c>; pinta el color del programa.</summary>
    public string Programa { get; init; } = string.Empty;

    public string Vista { get; init; } = string.Empty;

    public string Nombre { get; init; } = string.Empty;

    public int Activos { get; init; }

    /// <summary>Personas activas en este programa que además están activas en otro.</summary>
    public int EnOtroPrograma { get; init; }

    /// <summary>Dato de estado de hoy (p. ej. "6 con VAC").</summary>
    public string? Destacado { get; init; }
}

public sealed class ReportesCombinacionViewModel
{
    /// <summary>Nombres de los programas, en el orden de las tarjetas.</summary>
    public IReadOnlyList<string> Programas { get; init; } = [];

    /// <summary>Claves de <c>CensoProgramas</c> de esos mismos programas, para pintar su color.</summary>
    public IReadOnlyList<string> Claves { get; init; } = [];

    public int Pacientes { get; init; }

    public string Texto => string.Join(" + ", Programas);
}

public sealed class ReportesPortalHoyViewModel
{
    /// <summary>Novedades sin resolver ahora mismo, sin importar cuándo se crearon.</summary>
    public int Pendientes { get; init; }

    /// <summary>Creación (hora de Colombia) de la pendiente más antigua.</summary>
    public DateTime? PendienteMasAntigua { get; init; }

    public bool Error { get; init; }
}

public class ReportesFilterViewModel
{
    public DateTime? Desde { get; init; }

    public DateTime? Hasta { get; init; }

    public string? Municipio { get; init; }

    /// <summary>Solo para enlaces anteriores; se traduce a <see cref="Vista"/>.</summary>
    public string? Programa { get; init; }

    public string? Vista { get; init; }

    /// <summary>Filtro propio del panel de agudos.</summary>
    public string? EstadoGestion { get; init; }

    /// <summary>Filtro propio del panel de agudos.</summary>
    public string? EstadoCenso { get; init; }

    /// <summary>Filtro propio del panel del portal.</summary>
    public string? TipoNovedad { get; init; }
}

public class ReportesFilterOptionsViewModel
{
    public IReadOnlyList<SelectListItem> Municipios { get; init; } = [];

    public IReadOnlyList<SelectListItem> EstadosGestion { get; init; } = [];

    public IReadOnlyList<SelectListItem> EstadosCenso { get; init; } = [];

    public IReadOnlyList<SelectListItem> TiposNovedad { get; init; } = [];
}

public class ReportesPresetViewModel
{
    public string Etiqueta { get; init; } = string.Empty;

    public DateTime Desde { get; init; }

    public DateTime Hasta { get; init; }

    public bool Activo { get; init; }
}

// ---------------------------------------------------------------------------------------------
// Piezas de gráfico. Todas llevan ya calculado lo que la vista dibuja: la vista no cuenta nada.
// ---------------------------------------------------------------------------------------------

public class ReportesSerieViewModel
{
    public int EscalaMaxima { get; init; }

    public IReadOnlyList<int> Marcas { get; init; } = [];

    public IReadOnlyList<ReportesPuntoViewModel> Puntos { get; init; } = [];

    /// <summary>"dia", "semana" o "mes": cómo se agrupó la serie.</summary>
    public string Vista { get; init; } = "dia";

    public bool EsDiaria => Vista == "dia";

    public int Total => Puntos.Sum(x => x.Valor);

    public int Maximo => Puntos.Count == 0 ? 0 : Puntos.Max(x => x.Valor);

    public string NombreTramo => Vista switch { "mes" => "mes", "semana" => "semana", _ => "día" };
}

public class ReportesPuntoViewModel
{
    public DateTime Inicio { get; init; }

    public string Etiqueta { get; init; } = string.Empty;

    /// <summary>Día de la semana abreviado; solo en la vista por día.</summary>
    public string? Dia { get; init; }

    /// <summary>Rango exacto que cubre la barra, recortado al periodo consultado.</summary>
    public string Rango { get; init; } = string.Empty;

    public bool FinDeSemana { get; init; }

    public int Valor { get; init; }

    /// <summary>Altura de la barra en porcentaje de la escala.</summary>
    public double Altura { get; init; }
}

public class ReportesFlujoViewModel
{
    public string Vista { get; init; } = "dia";

    public int EscalaMaxima { get; init; }

    public IReadOnlyList<ReportesFlujoPuntoViewModel> Puntos { get; init; } = [];

    public int TotalIngresos => Puntos.Sum(x => x.Ingresos);

    public int TotalEgresos => Puntos.Sum(x => x.Egresos);
}

public class ReportesFlujoPuntoViewModel
{
    public string Etiqueta { get; init; } = string.Empty;

    public string? Dia { get; init; }

    public string Rango { get; init; } = string.Empty;

    public bool FinDeSemana { get; init; }

    public int Ingresos { get; init; }

    public int Egresos { get; init; }

    public double AlturaIngresos { get; init; }

    public double AlturaEgresos { get; init; }
}

/// <summary>Promedio de horas por tramo (día, semana o mes); nulo donde no hubo casos que promediar.</summary>
public class ReportesSerieHorasViewModel
{
    public string Vista { get; init; } = "dia";

    public IReadOnlyList<ReportesPuntoHorasViewModel> Puntos { get; init; } = [];

    /// <summary>Promedio de todo el periodo (sobre los casos, no sobre los días).</summary>
    public double? PromedioPeriodo { get; init; }
}

public class ReportesPuntoHorasViewModel
{
    public DateTime Inicio { get; init; }

    public string Etiqueta { get; init; } = string.Empty;

    public string? Dia { get; init; }

    public string Rango { get; init; } = string.Empty;

    public bool FinDeSemana { get; init; }

    public double? Horas { get; init; }

    public int Casos { get; init; }
}

public sealed record ReportesTramoViewModel(string Etiqueta, string? Dia, string Rango, bool FinDeSemana);

public sealed class ReportesLineaSerieViewModel
{
    public string Nombre { get; init; } = string.Empty;

    /// <summary>"principal" usa el color del programa; "secundaria", la pizarra.</summary>
    public string Clase { get; init; } = "principal";

    public IReadOnlyList<double?> Valores { get; init; } = [];

    public string? TotalTexto { get; init; }
}

/// <summary>
/// Gráfico de líneas sobre una sola escala. Se arma en el servidor: la posición de cada punto va en
/// porcentajes, así el dibujo se adapta al ancho sin JavaScript y las cifras quedan en el HTML (y en
/// la tabla "Ver las cifras") tal como salieron de la consulta.
/// </summary>
public sealed class ReportesLineaViewModel
{
    public string Id { get; init; } = string.Empty;

    public string Vista { get; init; } = "dia";

    public IReadOnlyList<ReportesTramoViewModel> Tramos { get; init; } = [];

    public IReadOnlyList<ReportesLineaSerieViewModel> Series { get; init; } = [];

    public double EscalaMaxima { get; init; }

    public IReadOnlyList<double> Marcas { get; init; } = [];

    public double? Referencia { get; init; }

    public string? ReferenciaTexto { get; init; }

    public bool EnHoras { get; init; }

    /// <summary>Columna adicional de la tabla (p. ej. cuántas novedades se promediaron).</summary>
    public string? ExtraTitulo { get; init; }

    public IReadOnlyList<string>? Extra { get; init; }

    public int N => Tramos.Count;

    public bool HayDatos => Series.Any(s => s.Valores.Any(v => v is > 0));

    public string NombreTramo => Vista switch { "mes" => "Mes", "semana" => "Semana", _ => "Día" };

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public string Formatear(double? valor) => valor is null
        ? "—"
        : EnHoras ? ReportesFormato.Horas(valor) : ReportesFormato.Entero((int)Math.Round(valor.Value));

    public string FormatearMarca(double valor) => EnHoras
        ? $"{valor.ToString(valor % 1 == 0 ? "0" : "0.#", CultureInfo.GetCultureInfo("es-CO"))} h"
        : ReportesFormato.Entero((int)Math.Round(valor));

    public double X(int indice) => N == 0 ? 0 : Math.Round((indice + 0.5) * 100d / N, 3);

    public double Y(double valor) => EscalaMaxima <= 0 ? 0 : Math.Round(valor * 100d / EscalaMaxima, 3);

    public static string Css(double valor) => valor.ToString("0.###", Inv);

    /// <summary>
    /// Con pocos tramos se rotulan todos. Con muchos días, solo los lunes (y el primero): la semana se lee
    /// de un vistazo y los rótulos no se montan. Con semanas o meses, uno de cada k.
    /// </summary>
    public bool RotuloVisible(int indice)
    {
        if (N <= 16)
        {
            return true;
        }

        if (Vista == "dia")
        {
            // El primer día se rotula solo si el primer lunes queda lejos; si no, los dos se montan.
            if (indice == 0)
            {
                var primerLunes = Tramos.ToList().FindIndex(t => t.Dia == "Lu");
                return Tramos[0].Dia == "Lu" || primerLunes < 0 || primerLunes >= 4;
            }

            return Tramos[indice].Dia == "Lu";
        }

        if (indice == 0)
        {
            return true;
        }

        var paso = (int)Math.Ceiling(N / 12d);
        return indice % paso == 0;
    }

    /// <summary>Trazo SVG de una serie en coordenadas 0–100; se corta donde falta el dato.</summary>
    public string Trazo(int serie)
    {
        var sb = new System.Text.StringBuilder();
        var abierto = false;
        var valores = Series[serie].Valores;
        for (var i = 0; i < valores.Count; i++)
        {
            if (valores[i] is not { } v)
            {
                abierto = false;
                continue;
            }

            sb.Append(abierto ? " L" : " M").Append(Css(X(i))).Append(' ').Append(Css(100 - Y(v)));
            abierto = true;
        }

        return sb.ToString().Trim();
    }

    /// <summary>Área bajo la serie principal (solo si no tiene huecos).</summary>
    public string? Area()
    {
        // Solo con una serie: con dos, el relleno de una tapaba la otra y se leía como su área.
        if (Series.Count != 1 || Series[0].Valores.Any(v => v is null) || N == 0)
        {
            return null;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append("M").Append(Css(X(0))).Append(" 100");
        for (var i = 0; i < N; i++)
        {
            sb.Append(" L").Append(Css(X(i))).Append(' ').Append(Css(100 - Y(Series[0].Valores[i]!.Value)));
        }

        sb.Append(" L").Append(Css(X(N - 1))).Append(" 100 Z");
        return sb.ToString();
    }

    /// <summary>Tramos seguidos de fin de semana, para sombrearlos como una sola banda.</summary>
    public IEnumerable<(double Izquierda, double Ancho)> BandasFinDeSemana()
    {
        for (var i = 0; i < N; i++)
        {
            if (!Tramos[i].FinDeSemana)
            {
                continue;
            }

            var inicio = i;
            while (i + 1 < N && Tramos[i + 1].FinDeSemana)
            {
                i++;
            }

            yield return (Math.Round(inicio * 100d / N, 3), Math.Round((i - inicio + 1) * 100d / N, 3));
        }
    }

    /// <summary>Puntos que llevan su cifra escrita: todos con pocos tramos; si no, el mayor y el último.</summary>
    public bool CifraVisible(int serie, int indice)
    {
        var valores = Series[serie].Valores;
        if (valores[indice] is null)
        {
            return false;
        }

        if (Series.Count == 1 && N <= 16)
        {
            return true;
        }

        var maximo = valores.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(0).Max();
        var indiceMaximo = valores.ToList().FindIndex(v => v == maximo);
        var ultimo = valores.ToList().FindLastIndex(v => v.HasValue);
        return indice == indiceMaximo || (serie == 0 && Series.Count == 1 && indice == ultimo);
    }

    public string Rotulo(int indice) =>
        string.Join(" · ", new[] { Tramos[indice].Rango }
            .Concat(Series.Select(s => $"{s.Nombre}: {Formatear(s.Valores[indice])}"))
            .Concat(Extra is null ? [] : [$"{ExtraTitulo}: {Extra[indice]}"]));

    // --------------------------------------------------------------------------------- fábricas

    public static ReportesLineaViewModel DeSerie(string id, ReportesSerieViewModel serie, string nombre, string unidadPorDia)
    {
        var escala = Escala(serie.Maximo, entera: true);
        var total = serie.Total;
        var promedio = serie.EsDiaria && serie.Puntos.Count > 0 ? total / (double)serie.Puntos.Count : (double?)null;
        return new ReportesLineaViewModel
        {
            Id = id,
            Vista = serie.Vista,
            Tramos = serie.Puntos.Select(p => new ReportesTramoViewModel(p.Etiqueta, p.Dia, p.Rango, p.FinDeSemana)).ToList(),
            Series = [new ReportesLineaSerieViewModel { Nombre = nombre, Valores = serie.Puntos.Select(p => (double?)p.Valor).ToList(), TotalTexto = ReportesFormato.Entero(total) }],
            EscalaMaxima = escala.Maximo,
            Marcas = escala.Marcas,
            Referencia = promedio,
            ReferenciaTexto = promedio is null ? null : $"Promedio: {ReportesFormato.Decimal(Math.Round(promedio.Value, 1))} {unidadPorDia}"
        };
    }

    public static ReportesLineaViewModel DeFlujo(string id, ReportesFlujoViewModel flujo)
    {
        var maximo = flujo.Puntos.Count == 0 ? 0 : flujo.Puntos.Max(p => Math.Max(p.Ingresos, p.Egresos));
        var escala = Escala(maximo, entera: true);
        return new ReportesLineaViewModel
        {
            Id = id,
            Vista = flujo.Vista,
            Tramos = flujo.Puntos.Select(p => new ReportesTramoViewModel(p.Etiqueta, p.Dia, p.Rango, p.FinDeSemana)).ToList(),
            Series =
            [
                new ReportesLineaSerieViewModel { Nombre = "Ingresan", Valores = flujo.Puntos.Select(p => (double?)p.Ingresos).ToList(), TotalTexto = ReportesFormato.Entero(flujo.TotalIngresos) },
                new ReportesLineaSerieViewModel { Nombre = "Egresan", Clase = "secundaria", Valores = flujo.Puntos.Select(p => (double?)p.Egresos).ToList(), TotalTexto = ReportesFormato.Entero(flujo.TotalEgresos) }
            ],
            EscalaMaxima = escala.Maximo,
            Marcas = escala.Marcas
        };
    }

    public static ReportesLineaViewModel DeHoras(string id, ReportesSerieHorasViewModel serie, string nombre)
    {
        var maximo = serie.Puntos.Where(p => p.Horas.HasValue).Select(p => p.Horas!.Value).DefaultIfEmpty(0).Max();
        var escala = Escala(maximo, entera: false);
        return new ReportesLineaViewModel
        {
            Id = id,
            Vista = serie.Vista,
            EnHoras = true,
            Tramos = serie.Puntos.Select(p => new ReportesTramoViewModel(p.Etiqueta, p.Dia, p.Rango, p.FinDeSemana)).ToList(),
            Series = [new ReportesLineaSerieViewModel { Nombre = nombre, Valores = serie.Puntos.Select(p => p.Horas).ToList() }],
            EscalaMaxima = escala.Maximo,
            Marcas = escala.Marcas,
            Referencia = serie.PromedioPeriodo,
            ReferenciaTexto = serie.PromedioPeriodo is null ? null : $"Promedio del periodo: {ReportesFormato.Horas(serie.PromedioPeriodo)}",
            ExtraTitulo = "Resueltas",
            Extra = serie.Puntos.Select(p => ReportesFormato.Entero(p.Casos)).ToList()
        };
    }

    /// <summary>Tope redondo con unas cinco marcas; enteras cuando la serie cuenta casos.</summary>
    public static (double Maximo, IReadOnlyList<double> Marcas) Escala(double maximo, bool entera)
    {
        if (maximo <= 0)
        {
            return (1, [1, 0]);
        }

        var crudo = maximo / 5d;
        var magnitud = Math.Pow(10, Math.Floor(Math.Log10(crudo)));
        var normal = crudo / magnitud;
        var factor = normal <= 1 ? 1d : normal <= 2 ? 2d : normal <= 2.5 ? 2.5d : normal <= 5 ? 5d : 10d;
        var paso = factor * magnitud;
        if (entera)
        {
            paso = Math.Max(1, Math.Round(paso));
        }

        var tope = Math.Ceiling(Math.Round(maximo / paso, 9)) * paso;
        var marcas = new List<double>();
        for (var valor = tope; valor > -paso / 2; valor -= paso)
        {
            marcas.Add(Math.Round(Math.Max(0, valor), 6));
        }

        return (Math.Round(tope, 6), marcas);
    }
}

/// <summary>Parte de un todo en forma de dona, con su total al centro.</summary>
public sealed record ReportesDonaViewModel(IReadOnlyList<ReportesSegmentoViewModel> Segmentos, int Total, string Rotulo)
{
    /// <summary>
    /// Dona a partir de una lista de categorías ya ordenada de mayor a menor: cada categoría toma un tono
    /// de pizarra por su puesto, "Otros (N)" el último tono y "Sin dato" el neutro. Pensada para listas
    /// de hasta cuatro categorías principales (<c>maximoFilas: 4</c>).
    /// </summary>
    public static ReportesDonaViewModel DeCategorias(IReadOnlyList<ReportesCategoriaViewModel> categorias, int total, string rotulo)
    {
        string[] tonos = ["n1", "n2", "n3", "n4"];
        var puesto = 0;
        var segmentos = categorias
            .Where(x => x.Valor > 0)
            .Select(x => new ReportesSegmentoViewModel
            {
                Etiqueta = x.Etiqueta,
                Valor = x.Valor,
                Porcentaje = x.Porcentaje,
                Tono = x.Etiqueta.StartsWith("Otros (", StringComparison.Ordinal) ? "n4"
                    : x.Secundaria ? "neutro"
                    : tonos[Math.Min(puesto++, tonos.Length - 1)]
            })
            .ToList();
        return new ReportesDonaViewModel(segmentos, total, rotulo);
    }
}

/// <summary>
/// Calendario de ingresos con forma de calendario: un bloque por mes, semanas de lunes a domingo.
/// Cada día del periodo lleva su número de ingresos y uno de tres tonos fijos (no relativos al
/// periodo, para que el mismo tono signifique lo mismo cualquier semana).
/// </summary>
public sealed class ReportesCalendarioViewModel
{
    /// <summary>Menos de este número de ingresos en el día: tono suave.</summary>
    public const int UmbralMedio = 20;

    /// <summary>Más de este número de ingresos en el día: tono fuerte. De 20 a 30 inclusive: tono medio.</summary>
    public const int UmbralAlto = 30;

    /// <summary>Solo se arma para periodos de hasta 120 días (cuatro o cinco meses).</summary>
    public bool Disponible { get; init; }

    public IReadOnlyList<ReportesCalendarioMesViewModel> Meses { get; init; } = [];

    public int Total => Meses.Sum(m => m.TotalPeriodo);

    /// <summary>1 suave (menos de 20), 2 medio (20 a 30), 3 fuerte (más de 30).</summary>
    public static int NivelDe(int ingresos) =>
        ingresos < UmbralMedio ? 1 : ingresos <= UmbralAlto ? 2 : 3;
}

public sealed class ReportesCalendarioMesViewModel
{
    public DateTime Mes { get; init; }

    public string Nombre { get; init; } = string.Empty;

    /// <summary>Semanas completas de lunes a domingo que cubren el mes.</summary>
    public IReadOnlyList<IReadOnlyList<ReportesCalendarioDiaViewModel>> Semanas { get; init; } = [];

    /// <summary>Ingresos del mes que caen dentro del periodo consultado.</summary>
    public int TotalPeriodo => Semanas.SelectMany(s => s).Where(d => d.EnMes && d.EnPeriodo).Sum(d => d.Valor);
}

public sealed class ReportesCalendarioDiaViewModel
{
    public DateTime Fecha { get; init; }

    /// <summary>El día pertenece al mes del bloque (los de relleno de la primera y última semana, no).</summary>
    public bool EnMes { get; init; }

    public bool EnPeriodo { get; init; }

    public int Valor { get; init; }

    /// <summary>0 fuera del periodo; 1 a 3 según <see cref="ReportesCalendarioViewModel.NivelDe"/>.</summary>
    public int Nivel { get; init; }
}

public class ReportesCategoriaViewModel
{
    public string Etiqueta { get; init; } = string.Empty;

    /// <summary>Texto secundario (p. ej. la descripción de un código CIE-10).</summary>
    public string? Detalle { get; init; }

    public int Valor { get; init; }

    /// <summary>Porcentaje sobre la base declarada del panel.</summary>
    public double Porcentaje { get; init; }

    /// <summary>Largo de la barra, relativo a la categoría mayor de la lista.</summary>
    public double Barra { get; init; }

    /// <summary>"Sin dato", "Otros": se pintan apagados para no competir con los datos reales.</summary>
    public bool Secundaria { get; init; }

    public string BarraCss => $"{Math.Max(Barra, Valor > 0 ? 1.5 : 0).ToString("0.##", CultureInfo.InvariantCulture)}%";
}

public class ReportesSegmentoViewModel
{
    public string Etiqueta { get; init; } = string.Empty;

    public int Valor { get; init; }

    public double Porcentaje { get; init; }

    /// <summary>Clase de color: el significado del segmento, no su posición.</summary>
    public string Tono { get; init; } = "neutro";

    public string AnchoCss => $"{Porcentaje.ToString("0.###", CultureInfo.InvariantCulture)}%";
}

public class ReportesPacienteFilaViewModel
{
    public string Paciente { get; init; } = string.Empty;

    public string Documento { get; init; } = string.Empty;

    public string NumeroIdentificacion { get; init; } = string.Empty;

    public DateTime Fecha { get; init; }

    public int? Dias { get; init; }

    public string? Diagnostico { get; init; }

    public string? Auxiliar { get; init; }

    public string? Municipio { get; init; }

    public string? Detalle { get; init; }
}

// ---------------------------------------------------------------------------------------------
// Programas.
// ---------------------------------------------------------------------------------------------

public abstract class ReportesProgramaViewModel
{
    /// <summary>Clave de <c>CensoProgramas</c>; pinta el color del programa.</summary>
    public abstract string Programa { get; }

    public abstract string Vista { get; }

    public string Nombre { get; init; } = string.Empty;

    public int ActivosHoy { get; init; }

    public int IngresosPeriodo { get; init; }

    public int IngresosPeriodoAnterior { get; init; }

    public double PromedioIngresosDia { get; init; }

    public ReportesSerieViewModel Ingresos { get; init; } = new();

    /// <summary>El panel tiene filtros propios aplicados, así que su conteo de ingresos está recortado.</summary>
    public bool FiltrosPropios { get; init; }
}

public class ReportesAgudosViewModel : ReportesProgramaViewModel
{
    public override string Programa => "AGUDOS";

    public override string Vista => ReportesVistas.Agudos;

    public int CanceladosRechazadosPeriodo { get; init; }

    public ReportesCalendarioViewModel Calendario { get; init; } = new();

    public int GestionPendiente { get; init; }

    public int GestionCompleta { get; init; }

    public int SinAutorizacion { get; init; }

    public int Criticos { get; init; }

    public IReadOnlyList<ReportesSegmentoViewModel> Gestion { get; init; } = [];

    public IReadOnlyList<ReportesSegmentoViewModel> Autorizacion { get; init; } = [];

    public IReadOnlyList<ReportesCategoriaViewModel> EstadoActual { get; init; } = [];

    public IReadOnlyList<ReportesSegmentoViewModel> Aseguradora { get; init; } = [];

    public IReadOnlyList<ReportesSegmentoViewModel> Riesgo { get; init; } = [];

    public IReadOnlyList<ReportesMunicipioFilaViewModel> Municipios { get; init; } = [];

    public IReadOnlyList<ReportesCategoriaViewModel> SinAutorizacionPorRecepcion { get; init; } = [];

    public IReadOnlyList<ReportesCategoriaViewModel> ActivosPorAuxiliar { get; init; } = [];

    public int ActivosSinAuxiliar { get; init; }

    public int AuxiliaresConPacientes { get; init; }

    /// <summary>Ingresos del periodo según el auxiliar asignado hoy en cada registro.</summary>
    public IReadOnlyList<ReportesCategoriaViewModel> IngresosPorAuxiliar { get; init; } = [];

    public int IngresosSinAuxiliar { get; init; }

    public int AuxiliaresConIngresos { get; init; }

    public IReadOnlyList<ReportesRegistroRevisarViewModel> RegistrosPrioritarios { get; init; } = [];

    public int TotalConAlerta { get; init; }
}

public class ReportesCronicosViewModel : ReportesProgramaViewModel
{
    public override string Programa => "CRONICOS";

    public override string Vista => ReportesVistas.Cronicos;

    public int EgresosPeriodo { get; init; }

    public int InactivosSinFechaEgreso { get; init; }

    public ReportesFlujoViewModel Flujo { get; init; } = new();

    public IReadOnlyList<ReportesCategoriaViewModel> Antiguedad { get; init; } = [];

    public IReadOnlyList<ReportesCategoriaViewModel> Patologia { get; init; } = [];

    public IReadOnlyList<ReportesCategoriaViewModel> MunicipiosActivos { get; init; } = [];

    public IReadOnlyList<ReportesCategoriaViewModel> MotivosEgreso { get; init; } = [];

    public int AgudizacionesRegistradas { get; init; }

    public int HospitalizacionesRegistradas { get; init; }
}

public class ReportesHeridasViewModel : ReportesProgramaViewModel
{
    public override string Programa => "CLINICA_HERIDAS";

    public override string Vista => ReportesVistas.Heridas;

    public int ActivosConVac { get; init; }

    public int EgresosPeriodo { get; init; }

    public int InactivosSinFechaEgreso { get; init; }

    public ReportesFlujoViewModel Flujo { get; init; } = new();

    public IReadOnlyList<ReportesCategoriaViewModel> Diagnosticos { get; init; } = [];

    public IReadOnlyList<ReportesCategoriaViewModel> ActivosPorAuxiliar { get; init; } = [];

    public int ActivosSinAuxiliar { get; init; }

    public IReadOnlyList<ReportesCategoriaViewModel> MotivosEgreso { get; init; } = [];

    public IReadOnlyList<ReportesSegmentoViewModel> FuenteIngreso { get; init; } = [];

    public IReadOnlyList<ReportesCategoriaViewModel> FrecuenciaVisita { get; init; } = [];
}

public class ReportesNptViewModel : ReportesProgramaViewModel
{
    public override string Programa => "NPT";

    public override string Vista => ReportesVistas.Npt;

    public int EgresosPeriodo { get; init; }

    public IReadOnlyList<ReportesPacienteFilaViewModel> PacientesActivos { get; init; } = [];

    public IReadOnlyList<ReportesPacienteFilaViewModel> IngresosDelPeriodo { get; init; } = [];

    public IReadOnlyList<ReportesPacienteFilaViewModel> EgresosDelPeriodo { get; init; } = [];
}

public class ReportesTerapiaViewModel : ReportesProgramaViewModel
{
    public override string Programa => "TERAPIA_AMBULATORIA";

    public override string Vista => ReportesVistas.Terapia;

    public IReadOnlyList<ReportesCategoriaViewModel> TerapiasSolicitadas { get; init; } = [];

    public IReadOnlyList<ReportesSegmentoViewModel> EstadoGestion { get; init; } = [];

    public IReadOnlyList<ReportesCategoriaViewModel> ActivosPorFisioterapeuta { get; init; } = [];

    public int ActivosSinFisioterapeuta { get; init; }
}

public class ReportesPortalViewModel
{
    public string Vista => ReportesVistas.Portal;

    /// <summary>Mensaje cuando el portal no respondió; los paneles del censo se muestran igual.</summary>
    public string? Error { get; init; }

    public int Total { get; init; }

    public int Resueltas { get; init; }

    public int Pendientes { get; init; }

    public double PorcentajeResueltas { get; init; }

    public double? PromedioHorasHastaCierre { get; init; }

    public double PromedioPorDia { get; init; }

    public int? DiasPendienteMasAntigua { get; init; }

    public bool FiltradoPorTipo { get; init; }

    public ReportesSerieViewModel PorDia { get; init; } = new();

    /// <summary>Tiempo de resolución promedio según el día en que se creó la novedad.</summary>
    public ReportesSerieHorasViewModel ResolucionPorDia { get; init; } = new();

    public IReadOnlyList<ReportesCategoriaViewModel> PorTipo { get; init; } = [];

    public IReadOnlyList<ReportesTiempoTipoViewModel> TiempoPorTipo { get; init; } = [];

    public IReadOnlyList<ReportesSegmentoViewModel> Prioridad { get; init; } = [];

    public string PromedioHastaCierreTexto => ReportesFormato.Horas(PromedioHorasHastaCierre);
}

public class ReportesTiempoTipoViewModel
{
    public string Tipo { get; init; } = string.Empty;

    public int Resueltas { get; init; }

    public double? PromedioHoras { get; init; }

    public double Barra { get; init; }

    public string PromedioTexto => ReportesFormato.HorasConDias(PromedioHoras);

    public string BarraCss => PromedioHoras.HasValue
        ? $"{Math.Max(2, Barra).ToString("0.##", CultureInfo.InvariantCulture)}%"
        : "0%";
}

public class ReportesMunicipioFilaViewModel
{
    public string Municipio { get; init; } = string.Empty;

    public bool NoParametrizado { get; init; }

    public int Ingresos { get; init; }

    public int GestionPendiente { get; init; }

    public int SinAutorizacion { get; init; }

    public double Barra { get; init; }
}

public class ReportesRegistroRevisarViewModel
{
    public long Id { get; init; }

    public string Paciente { get; init; } = string.Empty;

    public string Documento { get; init; } = string.Empty;

    public string NumeroIdentificacion { get; init; } = string.Empty;

    public string Municipio { get; init; } = string.Empty;

    /// <summary>
    /// Auxiliar asignado en el censo (directorio de Neon). Nulo cuando el registro no lo tiene: nunca
    /// se reemplaza por quien hizo el kardex o recibió el caso, que son personal administrativo.
    /// </summary>
    public string? AuxiliarAsignado { get; init; }

    public string Estado { get; init; } = string.Empty;

    public string Alerta { get; init; } = string.Empty;

    public bool SinAutorizacion { get; init; }

    public bool GestionPendiente { get; init; }

    public DateTime FechaIngreso { get; init; }
}

public static class ReportesFormato
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    public static string Entero(int valor) => valor.ToString("N0", Cultura);

    /// <summary>La palabra en singular o plural según la cifra: "1 egreso", "4 egresos".</summary>
    public static string Plural(int valor, string uno, string varios) => valor == 1 ? uno : varios;

    /// <summary>Cifra más la palabra concordada: "1 ingreso del periodo", "417 ingresos del periodo".</summary>
    public static string Cantidad(int valor, string uno, string varios) => $"{Entero(valor)} {Plural(valor, uno, varios)}";

    public static string Decimal(double valor) => valor.ToString(valor % 1 == 0 ? "N0" : "N1", Cultura);

    public static string Porcentaje(double valor) => $"{valor.ToString(valor % 1 == 0 ? "N0" : "N1", Cultura)} %";

    /// <summary>
    /// Siempre en horas, con un decimal: mezclar minutos, horas y días en la misma pantalla obligaba a
    /// convertir para comparar ("41,4 h" contra "2,4 días").
    /// </summary>
    public static string Horas(double? horas) =>
        horas.HasValue ? $"{horas.Value.ToString("N1", Cultura)} h" : "Sin datos";

    /// <summary>Horas, y entre paréntesis los días cuando pasa de dos días: "57,6 h (2,4 días)".</summary>
    public static string HorasConDias(double? horas) =>
        horas is >= 48
            ? $"{Horas(horas)} ({(horas.Value / 24d).ToString("N1", Cultura)} días)"
            : Horas(horas);
}
