using System.Globalization;
using System.Text;
using Nexa.Data;
using Nexa.Data.Repositories.Interfaces;
using Nexa.Data.Repositories.Models;
using Nexa.Helpers;
using Nexa.Models.Reports;
using Nexa.Models.Security;
using Nexa.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

[Authorize(Policy = SystemPermissions.Reportes)]
public class ReportesController : Controller
{
    private const string VistaDia = "dia";
    private const string VistaSemana = "semana";
    private const string VistaMes = "mes";
    private const string ProgramaAgudos = "Agudos";
    private const string ProgramaTerapiasAmbulatorias = "Terapias ambulatorias";
    private const string TerapiaAmbulatoriaEstadoGestionCompleta = "Gestión completa";
    private const string MunicipioNoParametrizado = "NO PARAMETRIZADO";

    private static readonly IReadOnlyDictionary<string, string> UnparameterizedLocationAliasValues =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ALTAVISTA"] = "Altavista",
            ["PALMITAS"] = "Palmitas",
            ["SANANTONIODEPRADO"] = "San Antonio de Prado",
            ["SANCRISTOBAL"] = "San Cristóbal",
            ["SANTAELENA"] = "Santa Elena"
        };

    private static readonly string[] DashboardPalette =
    [
        "#2563eb",
        "#0f766e",
        "#ea580c",
        "#db2777",
        "#7c3aed",
        "#0891b2",
        "#ca8a04",
        "#475569"
    ];

    private static readonly IReadOnlyDictionary<string, string> CategoriaNovedadLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PACIENTE"] = "Paciente",
            ["RUTA"] = "Ruta",
            ["PROCESO_FARMACEUTICO"] = "Proceso farmacéutico",
            ["LLAMADA_URGENTE"] = "Llamada urgente"
        };

    private readonly ApplicationDbContext _context;
    private readonly IPortalNovedadRepository _portalNovedadRepository;

    public ReportesController(ApplicationDbContext context, IPortalNovedadRepository portalNovedadRepository)
    {
        _context = context;
        _portalNovedadRepository = portalNovedadRepository;
    }

    public async Task<IActionResult> Index(ReportesFilterViewModel filters, CancellationToken cancellationToken)
    {
        var normalizedFilters = NormalizeFilters(filters);
        var censoRows = ShouldIncludeAgudos(normalizedFilters.Programa)
            ? await ApplyBaseFilters(_context.Censos.AsNoTracking(), normalizedFilters, _context)
                .Select(x => new ReportesCensoRow
                {
                    Id = x.Id,
                    FechaIngreso = x.FechaIngreso,
                    NombrePaciente = x.NombrePaciente,
                    TipoIdentificacion = x.TipoIdentificacion,
                    NumeroIdentificacion = x.NumeroIdentificacion,
                    NombreRecepcionaCaso = x.NombreRecepcionaCaso,
                    NombreRealizaKardex = x.NombreRealizaKardex,
                    AuxiliarAsignado = x.AuxiliarAsignado,
                    MunicipioResidencia = x.MunicipioResidencia,
                    Barrio = x.Barrio,
                    Direccion = x.Direccion,
                    Estado = x.Estado,
                    AutorizacionEvento = x.AutorizacionEvento,
                    GestionCompletaPendiente = x.GestionCompletaPendiente,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToListAsync(cancellationToken)
            : [];

        var terapiaRows = ShouldIncludeTerapiasAmbulatorias(normalizedFilters.Programa)
            ? await ApplyTerapiaAmbulatoriaFilters(_context.CensoTerapiasAmbulatorias.AsNoTracking(), normalizedFilters)
                .Select(x => new ReportesTerapiaAmbulatoriaRow
                {
                    Id = x.Id,
                    FechaInicio = x.FechaInicio,
                    NombrePaciente = x.NombrePaciente,
                    TipoIdentificacion = x.TipoIdentificacion,
                    NumeroIdentificacion = x.NumeroIdentificacion,
                    MunicipioResidencia = x.MunicipioResidencia,
                    TipoTerapia = x.TipoTerapia,
                    SegundoTratamientoTipoTerapia = x.SegundoTratamientoTipoTerapia,
                    TercerTratamientoTipoTerapia = x.TercerTratamientoTipoTerapia,
                    EstadoGestion = x.EstadoGestion,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToListAsync(cancellationToken)
            : [];

        var portalRows = await _portalNovedadRepository.GetNovedadesAsync(
            normalizedFilters.Desde!.Value,
            normalizedFilters.Hasta!.Value,
            normalizedFilters.TipoNovedad,
            null,
            cancellationToken);

        var totalRegistrosCenso = censoRows.Count;
        var totalEventosPendientesSinAutorizacion = censoRows.Count(IsWithoutAuthorization);
        var totalGestionesPendientes = censoRows.Count(IsPendingManagement);
        var totalGestionesCompletas = Math.Max(totalRegistrosCenso - totalGestionesPendientes, 0);
        var totalPendientesCriticos = censoRows.Count(x => IsPendingManagement(x) && IsWithoutAuthorization(x));
        var resolvedPortalRows = portalRows
            .Where(IsResolved)
            .Where(x => x.UpdatedAt > x.CreatedAt)
            .ToList();

        var promedioResolucionHoras = resolvedPortalRows.Count == 0
            ? (double?)null
            : resolvedPortalRows.Average(x => (x.UpdatedAt - x.CreatedAt).TotalHours);
        var totalDiasPeriodo = Math.Max(
            1,
            (normalizedFilters.Hasta!.Value.Date - normalizedFilters.Desde!.Value.Date).Days + 1);

        var filterOptions = await BuildFilterOptionsAsync(normalizedFilters, cancellationToken);

        var model = new ReportesDashboardViewModel
        {
            GeneratedAtLocal = DateTime.Now,
            Filters = normalizedFilters,
            FilterOptions = filterOptions,
            TotalRegistrosCenso = totalRegistrosCenso,
            TotalNovedades = portalRows.Count,
            TotalEventosPendientesSinAutorizacion = totalEventosPendientesSinAutorizacion,
            TotalGestionesPendientes = totalGestionesPendientes,
            TotalGestionesCompletas = totalGestionesCompletas,
            TotalPendientesCriticos = totalPendientesCriticos,
            TotalIngresosPeriodo = censoRows.Count,
            TotalIngresosTerapiaAmbulatoriaPeriodo = terapiaRows.Count,
            PromedioNovedadesPorDia = portalRows.Count / totalDiasPeriodo,
            PromedioIngresosPorDia = censoRows.Count / totalDiasPeriodo,
            PromedioIngresosTerapiaAmbulatoriaPorDia = terapiaRows.Count / totalDiasPeriodo,
            TotalNovedadesResueltas = resolvedPortalRows.Count,
            PorcentajeGestionPendiente = totalRegistrosCenso == 0 ? 0 : Math.Round(totalGestionesPendientes * 100d / totalRegistrosCenso, 2),
            PorcentajeResolucionNovedades = portalRows.Count == 0 ? 0 : Math.Round(resolvedPortalRows.Count * 100d / portalRows.Count, 2),
            PromedioResolucionHoras = promedioResolucionHoras,
            NovedadesPorDia = BuildTrend(portalRows.Select(x => x.CreatedAt), normalizedFilters),
            IngresosPorDia = BuildTrend(censoRows.Select(x => x.FechaIngreso), normalizedFilters),
            IngresosTerapiaAmbulatoriaPorDia = BuildTrend(terapiaRows.Select(x => x.FechaInicio), normalizedFilters),
            NovedadesPorTipo = BuildPortalCategoryCounts(
                portalRows,
                portalRows.Count,
                normalizedFilters.TipoNovedad),
            IngresosTerapiaAmbulatoriaPorTipo = BuildTerapiaAmbulatoriaTypeCounts(terapiaRows),
            EventosPendientesPorAuxiliar = BuildCategoryCounts(
                censoRows
                    .Where(IsWithoutAuthorization)
                    .GroupBy(x => NormalizeLabel(x.NombreRecepcionaCaso, "Sin responsable de recepción"))
                    .Select(x => (x.Key, x.Count())),
                totalEventosPendientesSinAutorizacion),
            GestionPendientePorMunicipio = BuildCategoryCounts(
                censoRows
                    .Where(IsPendingManagement)
                    .GroupBy(x => NormalizeLabel(x.MunicipioResidencia, "Sin municipio"))
                    .Select(x => (x.Key, x.Count())),
                totalGestionesPendientes)
                .Take(8)
                .ToList(),
            ResolucionPorTipo = BuildResolutionByType(resolvedPortalRows, normalizedFilters.TipoNovedad),
            FocosOperativos = BuildOperationalFocus(censoRows),
            FocosNoParametrizados = BuildUnparameterizedOperationalFocus(censoRows),
            RegistrosPrioritarios = BuildPriorityRecords(censoRows),
            ActiveFilterLabels = BuildActiveFilterLabels(normalizedFilters)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportarPacientesActivos(CancellationToken cancellationToken)
    {
        var currentDate = ColombiaTime.Convert(DateTime.UtcNow).Date;
        // Mismo criterio de visibilidad que la tabla del censo en pantalla: si una fila no se puede ver ni
        // abrir en el censo, tampoco puede reportarse aquí como paciente activo. Sin este filtro una copia
        // interna de despacho a farmacia con Estado contaminado hace que el paciente salga en el exportable
        // pero no aparezca al filtrar su cédula en el censo.
        var censoCandidates = await _context.Censos
            .AsNoTracking()
            .Where(CensoVisibility.EditableRecord(_context))
            .Where(x => x.Estado != null
                && (EF.Functions.ILike(x.Estado, "Aceptado activo")
                    || EF.Functions.ILike(x.Estado, "Aceptado cronico")
                    || EF.Functions.ILike(x.Estado, "Aceptado crónico")
                    || EF.Functions.ILike(x.Estado, "Activo Estancia prolongada")
                    || EF.Functions.ILike(x.Estado, "Aceptado estancia prolongada")))
            .Select(x => new
            {
                x.Id,
                x.FechaIngreso,
                x.NombrePaciente,
                x.TipoIdentificacion,
                x.NumeroIdentificacion,
                x.ClasificacionZonaSura,
                x.DiagnosticoDescriptivo,
                x.Programa,
                x.Asegurador,
                x.Estado
            })
            .ToListAsync(cancellationToken);

        var censoRows = censoCandidates
            .GroupBy(x => x.NumeroIdentificacion, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(x => x.FechaIngreso)
                .ThenByDescending(x => x.Id)
                .First())
            .Select(x => new ActivePatientReportRow
            {
                CurrentDate = currentDate,
                FullName = x.NombrePaciente,
                IdentificationType = x.TipoIdentificacion,
                IdentificationNumber = x.NumeroIdentificacion,
                Zone = x.ClasificacionZonaSura,
                AdmissionDate = x.FechaIngreso.Date,
                LengthOfStayDays = Math.Max(0, (currentDate - x.FechaIngreso.Date).Days),
                Diagnosis = x.DiagnosticoDescriptivo,
                Program = NormalizeActivePatientProgram(x.Programa, x.Estado),
                Insurer = x.Asegurador
            })
            .ToList();

        var terapiaCandidates = await _context.CensoTerapiasAmbulatorias
            .AsNoTracking()
            .Where(x => EF.Functions.ILike(x.EstadoPaciente, "Activo")
                && !EF.Functions.ILike(x.EstadoAlta, "Cerrado"))
            .Select(x => new
            {
                x.Id,
                x.FechaInicio,
                x.NombrePaciente,
                x.TipoIdentificacion,
                x.NumeroIdentificacion,
                x.ClasificacionZonaSura,
                x.DiagnosticoDescriptivo
            })
            .ToListAsync(cancellationToken);

        var terapiaRows = terapiaCandidates
            .GroupBy(x => x.NumeroIdentificacion, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(x => x.FechaInicio)
                .ThenByDescending(x => x.Id)
                .First())
            .Select(x => new ActivePatientReportRow
            {
                CurrentDate = currentDate,
                FullName = x.NombrePaciente,
                IdentificationType = x.TipoIdentificacion,
                IdentificationNumber = x.NumeroIdentificacion,
                Zone = x.ClasificacionZonaSura ?? string.Empty,
                AdmissionDate = x.FechaInicio.Date,
                LengthOfStayDays = Math.Max(0, (currentDate - x.FechaInicio.Date).Days),
                Diagnosis = x.DiagnosticoDescriptivo,
                Program = "Terapia ambulatoria",
                Insurer = "EPS SURA"
            })
            .ToList();

        var rows = censoRows
            .Concat(terapiaRows)
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.Program)
            .ToList();

        var workbook = ExcelWorkbookWriter.BuildActivePatientsWorkbook(rows, DateTime.UtcNow);
        var fileName = $"Informe_pacientes_activos_{currentDate:yyyyMMdd}.xlsx";
        return File(
            workbook,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private static IQueryable<Data.Entities.CensoRecord> ApplyBaseFilters(
        IQueryable<Data.Entities.CensoRecord> query,
        ReportesFilterViewModel filters,
        ApplicationDbContext context)
    {
        // Mismo criterio de visibilidad que la pantalla del censo y sus exportables: las copias internas de
        // despacho a farmacia no son atenciones reales y contarlas infla todos los indicadores del tablero.
        query = query.Where(CensoVisibility.EditableRecord(context));
        query = ExcludeCancelledAndRejected(query);

        if (filters.Desde.HasValue)
        {
            query = query.Where(x => x.FechaIngreso >= filters.Desde.Value.Date);
        }

        if (filters.Hasta.HasValue)
        {
            query = query.Where(x => x.FechaIngreso <= filters.Hasta.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(filters.Municipio))
        {
            query = query.Where(x => x.MunicipioResidencia == filters.Municipio);
        }

        if (!string.IsNullOrWhiteSpace(filters.EstadoGestion))
        {
            query = query.Where(x => x.GestionCompletaPendiente == filters.EstadoGestion);
        }

        if (!string.IsNullOrWhiteSpace(filters.EstadoCenso))
        {
            query = query.Where(x => x.Estado == filters.EstadoCenso);
        }

        return query;
    }

    private static IQueryable<Data.Entities.CensoTerapiaAmbulatoriaRecord> ApplyTerapiaAmbulatoriaFilters(
        IQueryable<Data.Entities.CensoTerapiaAmbulatoriaRecord> query,
        ReportesFilterViewModel filters)
    {
        if (filters.Desde.HasValue)
        {
            query = query.Where(x => x.FechaInicio >= filters.Desde.Value.Date);
        }

        if (filters.Hasta.HasValue)
        {
            query = query.Where(x => x.FechaInicio <= filters.Hasta.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(filters.Municipio))
        {
            query = query.Where(x => x.MunicipioResidencia == filters.Municipio);
        }

        if (string.Equals(filters.EstadoGestion, "Completa", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.EstadoGestion == TerapiaAmbulatoriaEstadoGestionCompleta);
        }
        else if (string.Equals(filters.EstadoGestion, "Pendiente", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.EstadoGestion != TerapiaAmbulatoriaEstadoGestionCompleta);
        }

        return query;
    }

    private async Task<ReportesFilterOptionsViewModel> BuildFilterOptionsAsync(
        ReportesFilterViewModel filters,
        CancellationToken cancellationToken)
    {
        // Las opciones de filtro se calculan sobre el mismo universo que los indicadores para no ofrecer
        // valores que solo existen en copias internas de despacho a farmacia (filtrarlos daría cero).
        var municipiosCenso = await _context.Censos
            .AsNoTracking()
            .Where(CensoVisibility.EditableRecord(_context))
            .Where(x => x.MunicipioResidencia != string.Empty)
            .Select(x => x.MunicipioResidencia)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var municipiosTerapiaAmbulatoria = await _context.CensoTerapiasAmbulatorias
            .AsNoTracking()
            .Where(x => x.MunicipioResidencia != null && x.MunicipioResidencia != string.Empty)
            .Select(x => x.MunicipioResidencia!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var municipios = municipiosCenso
            .Concat(municipiosTerapiaAmbulatoria)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var estadosCenso = await ExcludeCancelledAndRejected(
                _context.Censos.AsNoTracking().Where(CensoVisibility.EditableRecord(_context)))
            .Where(x => x.Estado != null && x.Estado != string.Empty)
            .Select(x => x.Estado!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var categoriasPortal = await _portalNovedadRepository.GetCategoriasAsync(cancellationToken);

        return new ReportesFilterOptionsViewModel
        {
            Municipios = BuildSelectOptions(municipios, filters.Municipio, "Todos"),
            Programas = BuildSelectOptions(
                [
                    (ProgramaAgudos, ProgramaAgudos),
                    (ProgramaTerapiasAmbulatorias, ProgramaTerapiasAmbulatorias)
                ],
                filters.Programa,
                "Todos"),
            EstadosGestion = BuildSelectOptions(["Pendiente", "Completa"], filters.EstadoGestion, "Todos"),
            EstadosCenso = BuildSelectOptions(estadosCenso, filters.EstadoCenso, "Todos"),
            TiposNovedad = BuildSelectOptions(
                categoriasPortal.Select(x => (x, GetCategoriaLabel(x))),
                filters.TipoNovedad,
                "Todas")
        };
    }

    private static IQueryable<Data.Entities.CensoRecord> ExcludeCancelledAndRejected(
        IQueryable<Data.Entities.CensoRecord> query)
    {
        return query.Where(x => x.Estado == null
            || (!EF.Functions.ILike(x.Estado, "%cancelado%")
                && !EF.Functions.ILike(x.Estado, "%rechazado%")));
    }

    private static ReportesFilterViewModel NormalizeFilters(ReportesFilterViewModel filters)
    {
        var today = DateTime.Today;
        var desde = filters.Desde?.Date ?? today.AddDays(-13);
        var hasta = filters.Hasta?.Date ?? today;

        if (desde > hasta)
        {
            (desde, hasta) = (hasta, desde);
        }

        return new ReportesFilterViewModel
        {
            Desde = desde,
            Hasta = hasta,
            Municipio = NormalizeText(filters.Municipio),
            Programa = NormalizeProgramFilter(filters.Programa),
            EstadoGestion = NormalizeText(filters.EstadoGestion),
            EstadoCenso = NormalizeText(filters.EstadoCenso),
            TipoNovedad = NormalizeText(filters.TipoNovedad),
        };
    }

    private static ReportesTrendSeriesViewModel BuildTrend(IEnumerable<DateTime> dates, ReportesFilterViewModel filters)
    {
        var desde = filters.Desde ?? DateTime.Today.AddDays(-13);
        var hasta = filters.Hasta ?? DateTime.Today;
        var vista = ResolveTrendView(desde, hasta);
        var grouped = dates
            .Where(x => x.Date >= desde.Date && x.Date <= hasta.Date)
            .GroupBy(x => GetPeriodStart(x.Date, vista))
            .ToDictionary(x => x.Key, x => x.Count());

        var points = EnumeratePeriods(desde.Date, hasta.Date, vista)
            .Select(period =>
            {
                grouped.TryGetValue(period, out var value);
                return new ReportesTrendPointViewModel
                {
                    Date = period,
                    Label = FormatPeriodLabel(period, vista),
                    Value = value
                };
            })
            .ToList();

        var scaleMax = CalculateTrendScaleMax(points.Max(x => x.Value));
        return new ReportesTrendSeriesViewModel
        {
            ScaleMax = scaleMax,
            ScaleTicks = Enumerable.Range(0, 6)
                .Select(index => scaleMax - (scaleMax / 5 * index))
                .ToList(),
            Points = points
                .Select(x => new ReportesTrendPointViewModel
                {
                    Date = x.Date,
                    Label = x.Label,
                    Value = x.Value,
                    Percentage = Math.Round(x.Value * 100d / scaleMax, 2)
                })
                .ToList()
        };
    }

    private static int CalculateTrendScaleMax(int highestValue)
    {
        if (highestValue <= 100)
        {
            return 100;
        }

        var rawStep = highestValue / 5d;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var normalizedStep = rawStep / magnitude;
        var niceStep = normalizedStep <= 1
            ? 1
            : normalizedStep <= 2
                ? 2
                : normalizedStep <= 5
                    ? 5
                    : 10;
        var step = Math.Max(1, (int)(niceStep * magnitude));

        return Math.Max(100, (int)Math.Ceiling(highestValue / (double)step) * step);
    }

    private static List<ReportesCategoryCountViewModel> BuildCategoryCounts(
        IEnumerable<(string Label, int Value)> values,
        int total)
    {
        return values
            .Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Label)
            .Select((x, index) => new ReportesCategoryCountViewModel
            {
                Label = NormalizeLabel(x.Label, "Sin dato"),
                Value = x.Value,
                Percentage = total == 0 ? 0 : Math.Round(x.Value * 100d / total, 2),
                Color = DashboardPalette[index % DashboardPalette.Length]
            })
            .ToList();
    }

    private static List<ReportesCategoryCountViewModel> BuildPortalCategoryCounts(
        IReadOnlyList<PortalNovedadRow> rows,
        int total,
        string? selectedCategory)
    {
        var categories = GetVisiblePortalCategories(selectedCategory);
        var counts = rows
            .GroupBy(x => x.Categoria, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        return categories
            .Select(category => new
            {
                Label = GetCategoriaLabel(category),
                Value = counts.GetValueOrDefault(category)
            })
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Label)
            .Select((x, index) => new ReportesCategoryCountViewModel
            {
                Label = x.Label,
                Value = x.Value,
                Percentage = total == 0 ? 0 : Math.Round(x.Value * 100d / total, 2),
                Color = DashboardPalette[index % DashboardPalette.Length]
            })
            .ToList();
    }

    private static List<ReportesResolutionByTypeViewModel> BuildResolutionByType(
        IReadOnlyList<PortalNovedadRow> resolvedRows,
        string? selectedCategory)
    {
        var groupedRows = resolvedRows
            .GroupBy(x => x.Categoria, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var resolvedByType = GetVisiblePortalCategories(selectedCategory)
            .Select(category =>
            {
                groupedRows.TryGetValue(category, out var categoryRows);
                categoryRows ??= [];
                return new
                {
                    Type = GetCategoriaLabel(category),
                    Count = categoryRows.Count,
                    Average = categoryRows.Count == 0
                        ? (double?)null
                        : categoryRows.Average(item => (item.UpdatedAt - item.CreatedAt).TotalHours)
                };
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Type)
            .ToList();

        var maxAverage = Math.Max(1, resolvedByType.Max(x => x.Average ?? 0));
        return resolvedByType
            .Select(x => new ReportesResolutionByTypeViewModel
            {
                Type = x.Type,
                ResolvedCount = x.Count,
                AverageHours = x.Average,
                Percentage = x.Average.HasValue
                    ? Math.Round(x.Average.Value * 100d / maxAverage, 2)
                    : 0
            })
            .ToList();
    }

    private static List<ReportesCategoryCountViewModel> BuildTerapiaAmbulatoriaTypeCounts(
        IReadOnlyList<ReportesTerapiaAmbulatoriaRow> rows)
    {
        var types = rows
            .SelectMany(row => EnumerateTherapyTypes(
                row.TipoTerapia,
                row.SegundoTratamientoTipoTerapia,
                row.TercerTratamientoTipoTerapia))
            .ToList();

        return BuildCategoryCounts(
            types
                .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(x => (x.Key, x.Count())),
            types.Count);
    }

    private static IEnumerable<string> EnumerateTherapyTypes(params string?[] values)
    {
        return values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(x => x!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static IReadOnlyList<string> GetVisiblePortalCategories(string? selectedCategory)
    {
        return string.IsNullOrWhiteSpace(selectedCategory)
            ? CategoriaNovedadLabels.Keys.ToList()
            : [selectedCategory];
    }

    private static List<ReportesOperationalFocusViewModel> BuildOperationalFocus(IReadOnlyList<ReportesCensoRow> rows)
    {
        var focus = rows
            .Where(row => !IsNoParametrizado(row.MunicipioResidencia))
            .GroupBy(row => NormalizeLabel(row.MunicipioResidencia, "Sin municipio"), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var records = group.ToList();
                var pending = records.Count(IsPendingManagement);
                var withoutAuthorization = records.Count(IsWithoutAuthorization);
                var score = pending * 3d + withoutAuthorization * 4d + records.Count * 0.25d;

                return new ReportesOperationalFocusViewModel
                {
                    Label = group.Key,
                    Records = records.Count,
                    PendingManagement = pending,
                    WithoutAuthorization = withoutAuthorization,
                    RiskScore = score
                };
            })
            .OrderByDescending(x => x.RiskScore)
            .ThenBy(x => x.Label)
            .Take(8)
            .ToList();

        var max = Math.Max(1, focus.Count == 0 ? 1 : focus.Max(x => x.RiskScore));
        return focus
            .Select(x => new ReportesOperationalFocusViewModel
            {
                Label = x.Label,
                Records = x.Records,
                PendingManagement = x.PendingManagement,
                WithoutAuthorization = x.WithoutAuthorization,
                RiskScore = x.RiskScore,
                Percentage = Math.Round(x.RiskScore * 100d / max, 2)
            })
            .ToList();
    }

    private static List<ReportesOperationalFocusViewModel> BuildUnparameterizedOperationalFocus(IReadOnlyList<ReportesCensoRow> rows)
    {
        var focus = rows
            .Where(row => IsNoParametrizado(row.MunicipioResidencia))
            .GroupBy(ResolveUnparameterizedLocation, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var records = group.ToList();
                var pending = records.Count(IsPendingManagement);
                var withoutAuthorization = records.Count(IsWithoutAuthorization);
                var score = pending * 3d + withoutAuthorization * 4d + records.Count * 0.25d;

                return new ReportesOperationalFocusViewModel
                {
                    Label = group.Key,
                    Records = records.Count,
                    PendingManagement = pending,
                    WithoutAuthorization = withoutAuthorization,
                    RiskScore = score
                };
            })
            .OrderByDescending(x => x.RiskScore)
            .ThenBy(x => x.Label)
            .Take(8)
            .ToList();

        var max = Math.Max(1, focus.Count == 0 ? 1 : focus.Max(x => x.RiskScore));
        return focus
            .Select(x => new ReportesOperationalFocusViewModel
            {
                Label = x.Label,
                Records = x.Records,
                PendingManagement = x.PendingManagement,
                WithoutAuthorization = x.WithoutAuthorization,
                RiskScore = x.RiskScore,
                Percentage = Math.Round(x.RiskScore * 100d / max, 2)
            })
            .ToList();
    }

    private static string ResolveUnparameterizedLocation(ReportesCensoRow row)
    {
        return ResolveUnparameterizedLocationAlias(row.Barrio)
            ?? ResolveUnparameterizedLocationAlias(row.Direccion)
            ?? "No parametrizado";
    }

    private static string? ResolveUnparameterizedLocationAlias(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var key = NormalizeMunicipalityKey(value);
        foreach (var alias in UnparameterizedLocationAliasValues)
        {
            if (key.Contains(alias.Key, StringComparison.Ordinal))
            {
                return alias.Value;
            }
        }

        return null;
    }

    private static bool IsNoParametrizado(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || string.Equals(NormalizeMunicipalityKey(value), NormalizeMunicipalityKey(MunicipioNoParametrizado), StringComparison.Ordinal);
    }

    private static List<ReportesRecentRecordViewModel> BuildPriorityRecords(IReadOnlyList<ReportesCensoRow> rows)
    {
        return rows
            .Select(row =>
            {
                var score = (IsWithoutAuthorization(row) ? 5 : 0)
                    + (IsPendingManagement(row) ? 4 : 0)
                    + (string.Equals(row.Estado, "Aceptado activo", StringComparison.OrdinalIgnoreCase) ? 1 : 0);

                return new
                {
                    Row = row,
                    Score = score
                };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Row.CreatedAtUtc)
            .Take(12)
            .Select(x => new ReportesRecentRecordViewModel
            {
                Id = x.Row.Id,
                Paciente = NormalizeLabel(x.Row.NombrePaciente, "Sin paciente"),
                Documento = $"{x.Row.TipoIdentificacion} {x.Row.NumeroIdentificacion}".Trim(),
                Municipio = NormalizeLabel(x.Row.MunicipioResidencia, "Sin municipio"),
                Auxiliar = NormalizeLabel(FirstNonEmpty(x.Row.AuxiliarAsignado, x.Row.NombreRealizaKardex), "Sin auxiliar"),
                EstadoGestion = NormalizeLabel(x.Row.GestionCompletaPendiente, "Sin estado"),
                Alerta = BuildCensoAlert(x.Row),
                FechaBase = x.Row.FechaIngreso,
                SinAutorizacion = IsWithoutAuthorization(x.Row)
            })
            .ToList();
    }

    private static IReadOnlyList<string> BuildActiveFilterLabels(ReportesFilterViewModel filters)
    {
        var labels = new List<string>();

        if (filters.Desde.HasValue && filters.Hasta.HasValue)
        {
            labels.Add($"{filters.Desde:dd/MM/yyyy} - {filters.Hasta:dd/MM/yyyy}");
        }

        if (!string.IsNullOrWhiteSpace(filters.Municipio))
        {
            labels.Add(filters.Municipio);
        }

        if (!string.IsNullOrWhiteSpace(filters.Programa))
        {
            labels.Add($"Programa: {filters.Programa}");
        }

        if (!string.IsNullOrWhiteSpace(filters.EstadoGestion))
        {
            labels.Add(filters.EstadoGestion);
        }

        if (!string.IsNullOrWhiteSpace(filters.EstadoCenso))
        {
            labels.Add(filters.EstadoCenso);
        }

        if (!string.IsNullOrWhiteSpace(filters.TipoNovedad))
        {
            labels.Add(GetCategoriaLabel(filters.TipoNovedad));
        }

        return labels;
    }

    private static IReadOnlyList<SelectListItem> BuildSelectOptions(
        IEnumerable<string> values,
        string? selected,
        string emptyText)
    {
        var options = new List<SelectListItem>
        {
            new() { Value = string.Empty, Text = emptyText, Selected = string.IsNullOrWhiteSpace(selected) }
        };

        options.AddRange(values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .Select(x => new SelectListItem
            {
                Value = x,
                Text = x,
                Selected = string.Equals(x, selected, StringComparison.OrdinalIgnoreCase)
            }));

        return options;
    }

    private static IReadOnlyList<SelectListItem> BuildSelectOptions(
        IEnumerable<(string Value, string Text)> values,
        string? selected,
        string emptyText)
    {
        var options = new List<SelectListItem>
        {
            new() { Value = string.Empty, Text = emptyText, Selected = string.IsNullOrWhiteSpace(selected) }
        };

        options.AddRange(values
            .OrderBy(x => x.Text)
            .Select(x => new SelectListItem
            {
                Value = x.Value,
                Text = x.Text,
                Selected = string.Equals(x.Value, selected, StringComparison.OrdinalIgnoreCase)
            }));

        return options;
    }

    private static IEnumerable<DateTime> EnumeratePeriods(DateTime desde, DateTime hasta, string vista)
    {
        var current = GetPeriodStart(desde, vista);
        var final = GetPeriodStart(hasta, vista);

        while (current <= final)
        {
            yield return current;
            current = vista switch
            {
                VistaMes => current.AddMonths(1),
                VistaSemana => current.AddDays(7),
                _ => current.AddDays(1)
            };
        }
    }

    private static DateTime GetPeriodStart(DateTime date, string vista)
    {
        return vista switch
        {
            VistaMes => new DateTime(date.Year, date.Month, 1),
            VistaSemana => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)).Date,
            _ => date.Date
        };
    }

    private static string ResolveTrendView(DateTime desde, DateTime hasta)
    {
        var totalDays = Math.Max(1, (hasta.Date - desde.Date).TotalDays + 1);
        return totalDays > 120
            ? VistaMes
            : totalDays > 45
                ? VistaSemana
                : VistaDia;
    }

    private static string FormatPeriodLabel(DateTime date, string vista)
    {
        var culture = CultureInfo.GetCultureInfo("es-CO");
        return vista switch
        {
            VistaMes => date.ToString("MMM yy", culture),
            VistaSemana => $"Sem {date:dd/MM}",
            _ => date.ToString("dd/MM", culture)
        };
    }

    private static string GetCategoriaLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Sin tipo";
        }

        return CategoriaNovedadLabels.TryGetValue(value, out var label)
            ? label
            : CultureInfo.GetCultureInfo("es-CO").TextInfo.ToTitleCase(value.Replace('_', ' ').ToLowerInvariant());
    }

    private static string BuildCensoAlert(ReportesCensoRow row)
    {
        var withoutAuthorization = IsWithoutAuthorization(row);
        var pending = IsPendingManagement(row);

        return (withoutAuthorization, pending) switch
        {
            (true, true) => "Sin autorización y pendiente",
            (true, false) => "Sin autorización",
            (false, true) => "Gestión pendiente",
            _ => "Revisar caso"
        };
    }

    private static string NormalizeActivePatientProgram(string? program, string? state)
    {
        if (!string.IsNullOrWhiteSpace(program))
        {
            return program.Contains("cron", StringComparison.OrdinalIgnoreCase)
                ? "Cronico"
                : "Agudo";
        }

        return state?.Contains("cron", StringComparison.OrdinalIgnoreCase) == true
            ? "Cronico"
            : "Agudo";
    }

    private static bool IsResolved(PortalNovedadRow row)
    {
        return string.Equals(row.Estado, "RESUELTA", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWithoutAuthorization(ReportesCensoRow row)
    {
        return string.IsNullOrWhiteSpace(row.AutorizacionEvento);
    }

    private static bool IsPendingManagement(ReportesCensoRow row)
    {
        return string.Equals(row.GestionCompletaPendiente, "Pendiente", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    private static string NormalizeLabel(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeMunicipalityKey(string value)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
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

    private static string? NormalizeProgramFilter(string? value)
    {
        var normalized = NormalizeText(value);
        if (normalized is null)
        {
            return null;
        }

        if (string.Equals(normalized, ProgramaAgudos, StringComparison.OrdinalIgnoreCase))
        {
            return ProgramaAgudos;
        }

        return string.Equals(normalized, ProgramaTerapiasAmbulatorias, StringComparison.OrdinalIgnoreCase)
            ? ProgramaTerapiasAmbulatorias
            : null;
    }

    private static bool ShouldIncludeAgudos(string? programa)
    {
        return string.IsNullOrWhiteSpace(programa)
            || string.Equals(programa, ProgramaAgudos, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldIncludeTerapiasAmbulatorias(string? programa)
    {
        return string.IsNullOrWhiteSpace(programa)
            || string.Equals(programa, ProgramaTerapiasAmbulatorias, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ReportesCensoRow
    {
        public long Id { get; init; }
        public DateTime FechaIngreso { get; init; }
        public string NombrePaciente { get; init; } = string.Empty;
        public string TipoIdentificacion { get; init; } = string.Empty;
        public string NumeroIdentificacion { get; init; } = string.Empty;
        public string NombreRecepcionaCaso { get; init; } = string.Empty;
        public string NombreRealizaKardex { get; init; } = string.Empty;
        public string? AuxiliarAsignado { get; init; }
        public string MunicipioResidencia { get; init; } = string.Empty;
        public string Barrio { get; init; } = string.Empty;
        public string Direccion { get; init; } = string.Empty;
        public string? Estado { get; init; }
        public string? AutorizacionEvento { get; init; }
        public string GestionCompletaPendiente { get; init; } = string.Empty;
        public DateTime CreatedAtUtc { get; init; }
    }

    private sealed class ReportesTerapiaAmbulatoriaRow
    {
        public long Id { get; init; }
        public DateTime FechaInicio { get; init; }
        public string NombrePaciente { get; init; } = string.Empty;
        public string TipoIdentificacion { get; init; } = string.Empty;
        public string NumeroIdentificacion { get; init; } = string.Empty;
        public string? MunicipioResidencia { get; init; }
        public string TipoTerapia { get; init; } = string.Empty;
        public string? SegundoTratamientoTipoTerapia { get; init; }
        public string? TercerTratamientoTipoTerapia { get; init; }
        public string EstadoGestion { get; init; } = string.Empty;
        public DateTime CreatedAtUtc { get; init; }
    }
}
