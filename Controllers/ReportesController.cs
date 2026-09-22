using System.Globalization;
using System.Text;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Data.Repositories.Interfaces;
using Nexa.Helpers;
using Nexa.Models.Security;
using Nexa.Models.ViewModels;
using Nexa.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

/// <summary>
/// Tablero de reportes: los cinco censos más las novedades del Portal Administrativo.
///
/// Cada programa se arma en su propio archivo parcial (ReportesController.Agudos.cs, .Cronicos.cs,
/// .Heridas.cs, .Npt.cs, .Terapia.cs, .Portal.cs). Este archivo solo resuelve el periodo, los filtros
/// y las piezas comunes de gráfico.
///
/// Reglas que valen para todo el tablero:
/// - "Hoy" es la fecha de Colombia, no la del reloj del servidor.
/// - Los periodos son por fecha calendario, cerrados en ambos extremos: del Desde al Hasta, ambos incluidos.
/// - "Activos hoy" usa la misma regla de cada censo que el informe de pacientes activos
///   (CensoController.ExportarPacientesActivos), para que el tablero y el informe no se contradigan.
/// - Cada lista suma exactamente la base que declara su panel; lo que no cabe se pliega en "Otros".
/// </summary>
[Authorize(Policy = SystemPermissions.Reportes)]
public partial class ReportesController : Controller
{
    private const string VistaDia = "dia";
    private const string VistaSemana = "semana";
    private const string VistaMes = "mes";
    private const string MunicipioNoParametrizado = "NO PARAMETRIZADO";
    private const string SinDato = "Sin dato";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    private static readonly string[] DiasCortos = ["Do", "Lu", "Ma", "Mi", "Ju", "Vi", "Sá"];

    private readonly DbContextOptions<ApplicationDbContext> _opcionesContexto;
    private readonly IPortalNovedadRepository _portalNovedadRepository;
    private readonly ICensoTabuladoService _censoTabuladoService;
    private readonly ILogger<ReportesController> _logger;

    public ReportesController(
        DbContextOptions<ApplicationDbContext> opcionesContexto,
        IPortalNovedadRepository portalNovedadRepository,
        ICensoTabuladoService censoTabuladoService,
        ILogger<ReportesController> logger)
    {
        _opcionesContexto = opcionesContexto;
        _portalNovedadRepository = portalNovedadRepository;
        _censoTabuladoService = censoTabuladoService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(
        ReportesFilterViewModel filters,
        string? cedulaPaciente,
        string? programaFiltro,
        DateTime? fechaIngresoDesde,
        DateTime? fechaIngresoHasta,
        CancellationToken cancellationToken)
    {
        // El tabulado del censo trae sus propios parametros: se consulta por documento, por programa
        // y por rango de ingreso, al margen de los filtros del tablero. Los nombres son los que envía
        // su formulario (_CensoTabulado.cshtml): fechaIngresoDesde / fechaIngresoHasta.
        var tabulado = new CensoUnificadoViewModel
        {
            CedulaFiltro = cedulaPaciente,
            ProgramaFiltro = programaFiltro,
            FechaIngresoFiltroDesde = fechaIngresoDesde?.Date,
            FechaIngresoFiltroHasta = fechaIngresoHasta?.Date
        };
        var ahora = ColombiaTime.Convert(DateTime.UtcNow);
        var hoy = ahora.Date;
        var f = NormalizeFilters(filters, hoy);
        var periodo = new Periodo(f.Desde!.Value, f.Hasta!.Value);

        // Las consultas no dependen unas de otras y casi todo su tiempo es la ida y vuelta a la base:
        // cada programa corre en paralelo con su propio contexto (un DbContext no admite consultas
        // simultáneas). El tabulado usa el contexto de la petición, que nadie más toca aquí.
        var tabuladoTask = _censoTabuladoService.ConstruirAsync(tabulado, cancellationToken);
        var agudosTask = ConContextoPropioAsync(c => ConstruirAgudosAsync(c, f, periodo, hoy, cancellationToken));
        var cronicosTask = ConContextoPropioAsync(c => ConstruirCronicosAsync(c, f, periodo, hoy, cancellationToken));
        var heridasTask = ConContextoPropioAsync(c => ConstruirHeridasAsync(c, f, periodo, hoy, cancellationToken));
        var nptTask = ConContextoPropioAsync(c => ConstruirNptAsync(c, f, periodo, hoy, cancellationToken));
        var terapiaTask = ConContextoPropioAsync(c => ConstruirTerapiaAsync(c, f, periodo, hoy, cancellationToken));
        var opcionesTask = ConContextoPropioAsync(c => BuildFilterOptionsAsync(c, f, cancellationToken));
        var portalTask = ConstruirPortalAsync(f, periodo, ahora, cancellationToken);
        var censoHoyTask = ConContextoPropioAsync(c => ConstruirCensoHoyAsync(c, cancellationToken));
        var portalHoyTask = ConstruirPortalHoyAsync(cancellationToken);

        await Task.WhenAll(tabuladoTask, agudosTask, cronicosTask, heridasTask, nptTask, terapiaTask, opcionesTask, portalTask,
            censoHoyTask, portalHoyTask);

        var model = new ReportesDashboardViewModel
        {
            GeneradoLocal = ahora,
            Hoy = hoy,
            Filters = f,
            FilterOptions = opcionesTask.Result,
            Presets = BuildPresets(hoy, periodo),
            PeriodoTexto = FormatearPeriodo(periodo),
            DiasPeriodo = periodo.Dias,
            Granularidad = periodo.Vista switch { VistaMes => "mes", VistaSemana => "semana", _ => "día" },
            Vista = f.Vista!,
            CensoHoy = censoHoyTask.Result,
            PortalHoy = portalHoyTask.Result,
            Agudos = agudosTask.Result,
            Cronicos = cronicosTask.Result,
            Heridas = heridasTask.Result,
            Npt = nptTask.Result,
            Terapia = terapiaTask.Result,
            Portal = portalTask.Result,
            ActiveFilterLabels = BuildActiveFilterLabels(f),
            RutaTablero = BuildRutaTablero(f),
            TabuladoCenso = tabulado
        };

        return View(model);
    }

    private async Task<T> ConContextoPropioAsync<T>(Func<ApplicationDbContext, Task<T>> consulta)
    {
        await using var contexto = new ApplicationDbContext(_opcionesContexto);
        return await consulta(contexto);
    }

    // ==========================================================================================
    // Periodo
    // ==========================================================================================

    /// <summary>Periodo del tablero: fechas calendario, ambos extremos incluidos.</summary>
    private readonly record struct Periodo(DateTime Desde, DateTime Hasta)
    {
        /// <summary>Límite superior abierto para las consultas: el día siguiente al Hasta.</summary>
        public DateTime HastaExclusivo => Hasta.AddDays(1);

        public int Dias => (Hasta - Desde).Days + 1;

        public string Vista => Dias > 120 ? VistaMes : Dias > 45 ? VistaSemana : VistaDia;

        public bool Contiene(DateTime fecha) => fecha.Date >= Desde && fecha.Date <= Hasta;

        /// <summary>El periodo inmediatamente anterior, del mismo largo.</summary>
        public Periodo Anterior => new(Desde.AddDays(-Dias), Desde.AddDays(-1));
    }

    private static ReportesFilterViewModel NormalizeFilters(ReportesFilterViewModel filters, DateTime hoy)
    {
        var desde = filters.Desde?.Date ?? hoy.AddDays(-13);
        var hasta = filters.Hasta?.Date ?? hoy;

        if (desde > hasta)
        {
            (desde, hasta) = (hasta, desde);
        }

        return new ReportesFilterViewModel
        {
            Desde = desde,
            Hasta = hasta,
            Municipio = NormalizeText(filters.Municipio),
            Vista = ReportesVistas.Normalizar(filters.Vista, filters.Programa),
            EstadoGestion = NormalizeText(filters.EstadoGestion),
            EstadoCenso = NormalizeText(filters.EstadoCenso),
            TipoNovedad = NormalizeText(filters.TipoNovedad)
        };
    }

    private static IReadOnlyList<ReportesPresetViewModel> BuildPresets(DateTime hoy, Periodo actual)
    {
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var rangos = new (string Etiqueta, DateTime Desde, DateTime Hasta)[]
        {
            ("Hoy", hoy, hoy),
            ("7 días", hoy.AddDays(-6), hoy),
            ("14 días", hoy.AddDays(-13), hoy),
            ("30 días", hoy.AddDays(-29), hoy),
            ("Este mes", inicioMes, hoy),
            ("Mes anterior", inicioMes.AddMonths(-1), inicioMes.AddDays(-1))
        };

        return rangos
            .Select(x => new ReportesPresetViewModel
            {
                Etiqueta = x.Etiqueta,
                Desde = x.Desde,
                Hasta = x.Hasta,
                Activo = x.Desde == actual.Desde && x.Hasta == actual.Hasta
            })
            .ToList();
    }

    private static string FormatearPeriodo(Periodo p)
    {
        if (p.Desde == p.Hasta)
        {
            return p.Desde.ToString("dddd d 'de' MMMM 'de' yyyy", Cultura);
        }

        var desde = p.Desde.Year == p.Hasta.Year
            ? p.Desde.Month == p.Hasta.Month
                ? p.Desde.ToString("%d", Cultura)
                : p.Desde.ToString("d 'de' MMMM", Cultura)
            : p.Desde.ToString("d 'de' MMMM 'de' yyyy", Cultura);

        return $"Del {desde} al {p.Hasta.ToString("d 'de' MMMM 'de' yyyy", Cultura)}";
    }

    // ==========================================================================================
    // Filtros
    // ==========================================================================================

    private async Task<ReportesFilterOptionsViewModel> BuildFilterOptionsAsync(
        ApplicationDbContext contexto,
        ReportesFilterViewModel filters,
        CancellationToken cancellationToken)
    {
        // Municipios de los cinco censos. Las opciones de agudos se toman sobre el mismo universo que
        // los indicadores para no ofrecer valores que solo existen en copias internas de despacho a
        // farmacia (filtrarlos daría cero).
        var municipios = new List<string>();
        municipios.AddRange(await contexto.Censos.AsNoTracking()
            .Where(CensoVisibility.EditableRecord(contexto))
            .Where(x => x.MunicipioResidencia != string.Empty)
            .Select(x => x.MunicipioResidencia)
            .Distinct()
            .ToListAsync(cancellationToken));
        municipios.AddRange(await contexto.CensoCronicos.AsNoTracking()
            .Where(x => x.MunicipioResidencia != null && x.MunicipioResidencia != string.Empty)
            .Select(x => x.MunicipioResidencia!)
            .Distinct()
            .ToListAsync(cancellationToken));
        municipios.AddRange(await contexto.CensoClinicaHeridas.AsNoTracking()
            .Where(x => x.MunicipioResidencia != null && x.MunicipioResidencia != string.Empty)
            .Select(x => x.MunicipioResidencia!)
            .Distinct()
            .ToListAsync(cancellationToken));
        municipios.AddRange(await contexto.CensoNpt.AsNoTracking()
            .Where(x => x.MunicipioResidencia != null && x.MunicipioResidencia != string.Empty)
            .Select(x => x.MunicipioResidencia!)
            .Distinct()
            .ToListAsync(cancellationToken));
        municipios.AddRange(await contexto.CensoTerapiasAmbulatorias.AsNoTracking()
            .Where(x => x.MunicipioResidencia != null && x.MunicipioResidencia != string.Empty)
            .Select(x => x.MunicipioResidencia!)
            .Distinct()
            .ToListAsync(cancellationToken));

        var estadosCenso = await ExcludeCancelledAndRejected(
                contexto.Censos.AsNoTracking().Where(CensoVisibility.EditableRecord(contexto)))
            .Where(x => x.Estado != null && x.Estado != string.Empty)
            .Select(x => x.Estado!)
            .Distinct()
            .ToListAsync(cancellationToken);

        IReadOnlyList<string> categoriasPortal;
        try
        {
            categoriasPortal = await _portalNovedadRepository.GetCategoriasAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "No se pudieron leer las categorías de novedades del portal.");
            categoriasPortal = [];
        }

        return new ReportesFilterOptionsViewModel
        {
            Municipios = BuildSelectOptions(municipios.Select(x => (x, EtiquetaMunicipio(x))), filters.Municipio, "Todos"),
            EstadosGestion = BuildSelectOptions([("Pendiente", "Pendiente"), ("Completa", "Completa")], filters.EstadoGestion, "Todas", ordenar: false),
            EstadosCenso = BuildSelectOptions(estadosCenso.Select(x => (x, EtiquetaEstadoAgudos(x))), filters.EstadoCenso, "Todos"),
            TiposNovedad = BuildSelectOptions(categoriasPortal.Select(x => (x, EtiquetaCategoriaNovedad(x))), filters.TipoNovedad, "Todos", ordenar: false)
        };
    }

    private static IReadOnlyList<SelectListItem> BuildSelectOptions(
        IEnumerable<(string Value, string Text)> values,
        string? selected,
        string emptyText,
        bool ordenar = true)
    {
        var options = new List<SelectListItem>
        {
            new() { Value = string.Empty, Text = emptyText, Selected = string.IsNullOrWhiteSpace(selected) }
        };

        var items = values
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => (Value: x.Value.Trim(), x.Text))
            .DistinctBy(x => x.Value, StringComparer.OrdinalIgnoreCase);
        if (ordenar)
        {
            items = items.OrderBy(x => x.Text, StringComparer.Create(Cultura, ignoreCase: true));
        }

        options.AddRange(items.Select(x => new SelectListItem
        {
            Value = x.Value,
            Text = x.Text,
            Selected = string.Equals(x.Value, selected, StringComparison.OrdinalIgnoreCase)
        }));

        return options;
    }

    private static IReadOnlyList<string> BuildActiveFilterLabels(ReportesFilterViewModel filters)
    {
        var labels = new List<string>();

        if (!string.IsNullOrWhiteSpace(filters.Municipio))
        {
            labels.Add($"Municipio: {filters.Municipio}");
        }

        if (!string.IsNullOrWhiteSpace(filters.EstadoGestion))
        {
            labels.Add($"Agudos · gestión {filters.EstadoGestion.ToLowerInvariant()}");
        }

        if (!string.IsNullOrWhiteSpace(filters.EstadoCenso))
        {
            labels.Add($"Agudos · {EtiquetaEstadoAgudos(filters.EstadoCenso)}");
        }

        if (!string.IsNullOrWhiteSpace(filters.TipoNovedad))
        {
            labels.Add($"Portal · {EtiquetaCategoriaNovedad(filters.TipoNovedad)}");
        }

        return labels;
    }

    private static IReadOnlyDictionary<string, string> BuildRutaTablero(ReportesFilterViewModel f)
    {
        var ruta = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Desde"] = f.Desde!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["Hasta"] = f.Hasta!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["Vista"] = f.Vista!
        };
        void Agregar(string clave, string? valor)
        {
            if (!string.IsNullOrWhiteSpace(valor))
            {
                ruta[clave] = valor;
            }
        }

        Agregar("Municipio", f.Municipio);
        Agregar("EstadoGestion", f.EstadoGestion);
        Agregar("EstadoCenso", f.EstadoCenso);
        Agregar("TipoNovedad", f.TipoNovedad);
        return ruta;
    }

    // ==========================================================================================
    // Series
    // ==========================================================================================

    /// <summary>
    /// Serie de conteos por día, semana o mes. Las semanas empiezan el lunes y, como los meses, se
    /// recortan al periodo: la primera barra de "Sem 01/09" en un periodo que empieza el 03/09 cuenta
    /// solo del 03 en adelante, y así lo dice su rótulo emergente.
    /// </summary>
    private static ReportesSerieViewModel ConstruirSerie(IEnumerable<DateTime> fechas, Periodo p)
    {
        var agrupado = fechas
            .Where(p.Contiene)
            .GroupBy(x => InicioDeTramo(x.Date, p.Vista))
            .ToDictionary(x => x.Key, x => x.Count());

        var tramos = EnumerarTramos(p).ToList();
        var maximo = tramos.Count == 0 ? 0 : tramos.Max(t => agrupado.GetValueOrDefault(t.Inicio));
        var escala = EscalaAmigable(maximo);

        return new ReportesSerieViewModel
        {
            Vista = p.Vista,
            EscalaMaxima = escala.Maximo,
            Marcas = escala.Marcas,
            Puntos = tramos
                .Select(t =>
                {
                    var valor = agrupado.GetValueOrDefault(t.Inicio);
                    return new ReportesPuntoViewModel
                    {
                        Inicio = t.Inicio,
                        Etiqueta = t.Etiqueta,
                        Dia = t.Dia,
                        Rango = t.Rango,
                        FinDeSemana = t.FinDeSemana,
                        Valor = valor,
                        Altura = escala.Maximo == 0 ? 0 : Math.Round(valor * 100d / escala.Maximo, 2)
                    };
                })
                .ToList()
        };
    }

    /// <summary>Ingresos (hacia arriba) y egresos (hacia abajo) sobre una sola escala simétrica.</summary>
    private static ReportesFlujoViewModel ConstruirFlujo(IEnumerable<DateTime> ingresos, IEnumerable<DateTime> egresos, Periodo p)
    {
        var entradas = ingresos.Where(p.Contiene)
            .GroupBy(x => InicioDeTramo(x.Date, p.Vista))
            .ToDictionary(x => x.Key, x => x.Count());
        var salidas = egresos.Where(p.Contiene)
            .GroupBy(x => InicioDeTramo(x.Date, p.Vista))
            .ToDictionary(x => x.Key, x => x.Count());

        var tramos = EnumerarTramos(p).ToList();
        var maximo = tramos.Count == 0
            ? 0
            : tramos.Max(t => Math.Max(entradas.GetValueOrDefault(t.Inicio), salidas.GetValueOrDefault(t.Inicio)));
        var escala = EscalaAmigable(maximo).Maximo;

        return new ReportesFlujoViewModel
        {
            Vista = p.Vista,
            EscalaMaxima = escala,
            Puntos = tramos
                .Select(t =>
                {
                    var i = entradas.GetValueOrDefault(t.Inicio);
                    var e = salidas.GetValueOrDefault(t.Inicio);
                    return new ReportesFlujoPuntoViewModel
                    {
                        Etiqueta = t.Etiqueta,
                        Dia = t.Dia,
                        Rango = t.Rango,
                        FinDeSemana = t.FinDeSemana,
                        Ingresos = i,
                        Egresos = e,
                        AlturaIngresos = escala == 0 ? 0 : Math.Round(i * 100d / escala, 2),
                        AlturaEgresos = escala == 0 ? 0 : Math.Round(e * 100d / escala, 2)
                    };
                })
                .ToList()
        };
    }

    /// <summary>
    /// Calendario de un periodo de hasta 120 días: un bloque por cada mes que toca el periodo, con sus
    /// semanas de lunes a domingo. Solo los días dentro del periodo llevan cifra y tono; los demás días
    /// del mes se muestran como en cualquier calendario, sin datos.
    /// </summary>
    private static ReportesCalendarioViewModel ConstruirCalendario(IEnumerable<DateTime> fechas, Periodo p)
    {
        if (p.Dias > 120)
        {
            return new ReportesCalendarioViewModel { Disponible = false };
        }

        var porDia = fechas
            .Where(p.Contiene)
            .GroupBy(x => x.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var meses = new List<ReportesCalendarioMesViewModel>();
        for (var mes = new DateTime(p.Desde.Year, p.Desde.Month, 1); mes <= p.Hasta; mes = mes.AddMonths(1))
        {
            var finMes = mes.AddMonths(1).AddDays(-1);
            var semanas = new List<IReadOnlyList<ReportesCalendarioDiaViewModel>>();
            for (var lunes = InicioDeTramo(mes, VistaSemana); lunes <= finMes; lunes = lunes.AddDays(7))
            {
                semanas.Add(Enumerable.Range(0, 7)
                    .Select(i =>
                    {
                        var fecha = lunes.AddDays(i);
                        var enMes = fecha.Month == mes.Month && fecha.Year == mes.Year;
                        var enPeriodo = enMes && p.Contiene(fecha);
                        var valor = enPeriodo ? porDia.GetValueOrDefault(fecha) : 0;
                        return new ReportesCalendarioDiaViewModel
                        {
                            Fecha = fecha,
                            EnMes = enMes,
                            EnPeriodo = enPeriodo,
                            Valor = valor,
                            Nivel = enPeriodo ? ReportesCalendarioViewModel.NivelDe(valor) : 0
                        };
                    })
                    .ToList());
            }

            meses.Add(new ReportesCalendarioMesViewModel
            {
                Mes = mes,
                Nombre = Capitalizar(mes.ToString("MMMM 'de' yyyy", Cultura)),
                Semanas = semanas
            });
        }

        return new ReportesCalendarioViewModel
        {
            Disponible = true,
            Meses = meses
        };
    }

    private sealed record Tramo(DateTime Inicio, string Etiqueta, string? Dia, string Rango, bool FinDeSemana);

    private static IEnumerable<Tramo> EnumerarTramos(Periodo p)
    {
        var actual = InicioDeTramo(p.Desde, p.Vista);
        var final = InicioDeTramo(p.Hasta, p.Vista);

        while (actual <= final)
        {
            var siguiente = p.Vista switch
            {
                VistaMes => actual.AddMonths(1),
                VistaSemana => actual.AddDays(7),
                _ => actual.AddDays(1)
            };

            // Rango real que cubre la barra dentro del periodo consultado.
            var desde = actual < p.Desde ? p.Desde : actual;
            var hasta = siguiente.AddDays(-1) > p.Hasta ? p.Hasta : siguiente.AddDays(-1);

            yield return p.Vista switch
            {
                VistaMes => new Tramo(
                    actual,
                    Capitalizar(actual.ToString("MMM yy", Cultura).Replace(".", string.Empty)),
                    null,
                    $"{desde:dd/MM/yyyy} – {hasta:dd/MM/yyyy}",
                    false),
                VistaSemana => new Tramo(
                    actual,
                    $"Sem {actual:dd/MM}",
                    null,
                    $"{desde:dd/MM/yyyy} – {hasta:dd/MM/yyyy}",
                    false),
                _ => new Tramo(
                    actual,
                    actual.ToString("dd/MM", CultureInfo.InvariantCulture),
                    DiasCortos[(int)actual.DayOfWeek],
                    actual.ToString("dddd d 'de' MMMM", Cultura),
                    actual.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            };

            actual = siguiente;
        }
    }

    private static DateTime InicioDeTramo(DateTime fecha, string vista) => vista switch
    {
        VistaMes => new DateTime(fecha.Year, fecha.Month, 1),
        VistaSemana => fecha.AddDays(-(((int)fecha.DayOfWeek + 6) % 7)).Date,
        _ => fecha.Date
    };

    /// <summary>
    /// Escala con tope redondo y pocas marcas enteras. Antes la escala nunca bajaba de 100, así que
    /// una serie de 0 a 8 ingresos se dibujaba pegada al piso.
    /// </summary>
    private static (int Maximo, IReadOnlyList<int> Marcas) EscalaAmigable(int maximo)
    {
        if (maximo <= 0)
        {
            return (0, [0]);
        }

        var pasoCrudo = maximo / 5d;
        var magnitud = Math.Pow(10, Math.Floor(Math.Log10(pasoCrudo)));
        var normalizado = pasoCrudo / magnitud;
        var factor = normalizado <= 1 ? 1d
            : normalizado <= 2 ? 2d
            : normalizado <= 2.5 && magnitud >= 10 ? 2.5d
            : normalizado <= 5 ? 5d
            : 10d;
        var paso = Math.Max(1, (int)Math.Round(factor * magnitud));
        var tope = (int)Math.Ceiling(maximo / (double)paso) * paso;

        var marcas = new List<int>();
        for (var valor = tope; valor >= 0; valor -= paso)
        {
            marcas.Add(valor);
        }

        return (tope, marcas);
    }

    // ==========================================================================================
    // Listas de categorías
    // ==========================================================================================

    /// <summary>
    /// Convierte conteos en filas de barras. Ordena de mayor a menor, deja al final las categorías
    /// secundarias ("Sin dato") y pliega lo que pase de <paramref name="maximoFilas"/> en una fila
    /// "Otros" para que la lista siempre sume lo mismo que su base.
    /// </summary>
    private static List<ReportesCategoriaViewModel> ConstruirCategorias(
        IEnumerable<(string Etiqueta, int Valor)> conteos,
        int baseTotal,
        int maximoFilas = 8,
        Func<string, string?>? detalle = null,
        Func<string, bool>? esSecundaria = null,
        string? etiquetaOtros = null,
        bool conservarOrden = false)
    {
        var filas = conteos
            .Where(x => x.Valor > 0)
            .Select(x => (x.Etiqueta, x.Valor, Secundaria: esSecundaria?.Invoke(x.Etiqueta) ?? x.Etiqueta == SinDato))
            .ToList();

        if (!conservarOrden)
        {
            filas = filas
                .OrderBy(x => x.Secundaria)
                .ThenByDescending(x => x.Valor)
                .ThenBy(x => x.Etiqueta, StringComparer.Create(Cultura, ignoreCase: true))
                .ToList();
        }

        var principales = filas.Where(x => !x.Secundaria).ToList();
        var secundarias = filas.Where(x => x.Secundaria).ToList();

        var resultado = new List<(string Etiqueta, int Valor, bool Secundaria, string? Detalle)>();
        if (principales.Count > maximoFilas)
        {
            var visibles = principales.Take(maximoFilas - 1).ToList();
            var resto = principales.Skip(maximoFilas - 1).ToList();
            resultado.AddRange(visibles.Select(x => (x.Etiqueta, x.Valor, false, detalle?.Invoke(x.Etiqueta))));
            resultado.Add((
                etiquetaOtros ?? $"Otros ({resto.Count})",
                resto.Sum(x => x.Valor),
                true,
                string.Join(", ", resto.Select(x => x.Etiqueta))));
        }
        else
        {
            resultado.AddRange(principales.Select(x => (x.Etiqueta, x.Valor, false, detalle?.Invoke(x.Etiqueta))));
        }

        resultado.AddRange(secundarias.Select(x => (x.Etiqueta, x.Valor, true, detalle?.Invoke(x.Etiqueta))));

        var mayor = resultado.Count == 0 ? 0 : resultado.Max(x => x.Valor);
        return resultado
            .Select(x => new ReportesCategoriaViewModel
            {
                Etiqueta = x.Etiqueta,
                Detalle = x.Detalle,
                Valor = x.Valor,
                Porcentaje = baseTotal == 0 ? 0 : Math.Round(x.Valor * 100d / baseTotal, 1),
                Barra = mayor == 0 ? 0 : Math.Round(x.Valor * 100d / mayor, 2),
                Secundaria = x.Secundaria
            })
            .ToList();
    }

    /// <summary>Segmentos de una barra apilada. Los segmentos en cero no se dibujan.</summary>
    private static List<ReportesSegmentoViewModel> ConstruirSegmentos(params (string Etiqueta, int Valor, string Tono)[] partes)
    {
        var total = partes.Sum(x => x.Valor);
        return partes
            .Where(x => x.Valor > 0)
            .Select(x => new ReportesSegmentoViewModel
            {
                Etiqueta = x.Etiqueta,
                Valor = x.Valor,
                Tono = x.Tono,
                Porcentaje = total == 0 ? 0 : Math.Round(x.Valor * 100d / total, 1)
            })
            .ToList();
    }

    /// <summary>Agrupa sin distinguir mayúsculas ni espacios sobrantes y rotula los vacíos como <see cref="SinDato"/>.</summary>
    private static IEnumerable<(string Etiqueta, int Valor)> Contar(
        IEnumerable<string?> valores,
        Func<string, string>? etiquetar = null,
        string vacio = SinDato)
    {
        return valores
            .Select(x => string.IsNullOrWhiteSpace(x) ? null : x.Trim())
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(g => (
                g.Key is null ? vacio : etiquetar?.Invoke(g.First()!) ?? g.First()!,
                g.Count()));
    }

    // ==========================================================================================
    // Utilidades
    // ==========================================================================================

    private static double Promedio(int total, int dias) =>
        dias <= 0 ? 0 : Math.Round(total / (double)dias, 1);

    private static string NormalizarDocumento(string? documento) =>
        (documento ?? string.Empty).Trim().ToUpperInvariant();

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Capitalizar(string valor) =>
        string.IsNullOrEmpty(valor) ? valor : char.ToUpper(valor[0], Cultura) + valor[1..];

    private static string Documento(string tipo, string numero) => $"{tipo} {numero}".Trim();

    private static bool EsIgual(string? valor, string esperado) =>
        string.Equals(valor?.Trim(), esperado, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeMunicipalityKey(string value)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }

    /// <summary>Etiqueta con tildes de un municipio en mayúsculas sostenidas (los censos lo guardan sin tilde).</summary>
    private static string EtiquetaMunicipio(string municipio) => municipio.Trim().ToUpperInvariant() switch
    {
        "MEDELLIN" => "MEDELLÍN",
        "SAN CRISTOBAL" => "SAN CRISTÓBAL",
        "AMAGA" => "AMAGÁ",
        "DON MATIAS" => "DON MATÍAS",
        "GUATAPE" => "GUATAPÉ",
        "LA UNION" => "LA UNIÓN",
        _ => municipio.Trim()
    };
}
