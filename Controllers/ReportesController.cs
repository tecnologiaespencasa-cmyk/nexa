using System.Globalization;
using IntranetPrueba.Data;
using IntranetPrueba.Data.Repositories.Interfaces;
using IntranetPrueba.Data.Repositories.Models;
using IntranetPrueba.Helpers;
using IntranetPrueba.Models.Reports;
using IntranetPrueba.Models.Security;
using IntranetPrueba.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Controllers;

[Authorize(Policy = SystemPermissions.Reportes)]
public class ReportesController : Controller
{
    private const string VistaDia = "dia";
    private const string VistaSemana = "semana";
    private const string VistaMes = "mes";

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
        var censoRows = await ApplyBaseFilters(_context.Censos.AsNoTracking(), normalizedFilters)
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
                Estado = x.Estado,
                AutorizacionEvento = x.AutorizacionEvento,
                GestionCompletaPendiente = x.GestionCompletaPendiente,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var portalRows = await _portalNovedadRepository.GetNovedadesAsync(
            normalizedFilters.Desde!.Value,
            normalizedFilters.Hasta!.Value,
            normalizedFilters.TipoNovedad,
            normalizedFilters.Auxiliar,
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
            PromedioNovedadesPorDia = portalRows.Count / totalDiasPeriodo,
            PromedioIngresosPorDia = censoRows.Count / totalDiasPeriodo,
            TotalNovedadesResueltas = resolvedPortalRows.Count,
            PorcentajeGestionPendiente = totalRegistrosCenso == 0 ? 0 : Math.Round(totalGestionesPendientes * 100d / totalRegistrosCenso, 2),
            PorcentajeResolucionNovedades = portalRows.Count == 0 ? 0 : Math.Round(resolvedPortalRows.Count * 100d / portalRows.Count, 2),
            PromedioResolucionHoras = promedioResolucionHoras,
            NovedadesPorDia = BuildTrend(portalRows.Select(x => x.CreatedAt), normalizedFilters),
            IngresosPorDia = BuildTrend(censoRows.Select(x => x.FechaIngreso), normalizedFilters),
            NovedadesPorTipo = BuildPortalCategoryCounts(
                portalRows,
                portalRows.Count,
                normalizedFilters.TipoNovedad),
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
            RegistrosPrioritarios = BuildPriorityRecords(censoRows),
            ActiveFilterLabels = BuildActiveFilterLabels(normalizedFilters)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportarPacientesActivos(CancellationToken cancellationToken)
    {
        var currentDate = ColombiaTime.Convert(DateTime.UtcNow).Date;
        var candidates = await _context.Censos
            .AsNoTracking()
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

        var rows = candidates
            .GroupBy(x => x.NumeroIdentificacion, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(x => x.FechaIngreso)
                .ThenByDescending(x => x.Id)
                .First())
            .OrderBy(x => x.NombrePaciente)
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

        var workbook = ExcelWorkbookWriter.BuildActivePatientsWorkbook(rows, DateTime.UtcNow);
        var fileName = $"Informe_pacientes_activos_{currentDate:yyyyMMdd}.xlsx";
        return File(
            workbook,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private static IQueryable<Data.Entities.CensoRecord> ApplyBaseFilters(
        IQueryable<Data.Entities.CensoRecord> query,
        ReportesFilterViewModel filters)
    {
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

        if (!string.IsNullOrWhiteSpace(filters.Auxiliar))
        {
            query = query.Where(x => x.AuxiliarAsignado == filters.Auxiliar || x.NombreRealizaKardex == filters.Auxiliar);
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

    private async Task<ReportesFilterOptionsViewModel> BuildFilterOptionsAsync(
        ReportesFilterViewModel filters,
        CancellationToken cancellationToken)
    {
        var municipios = await _context.Censos
            .AsNoTracking()
            .Where(x => x.MunicipioResidencia != string.Empty)
            .Select(x => x.MunicipioResidencia)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var estadosCenso = await ExcludeCancelledAndRejected(_context.Censos.AsNoTracking())
            .Where(x => x.Estado != null && x.Estado != string.Empty)
            .Select(x => x.Estado!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var auxiliaresCensoRaw = await _context.Censos
            .AsNoTracking()
            .Select(x => new { x.AuxiliarAsignado, x.NombreRealizaKardex })
            .ToListAsync(cancellationToken);

        var auxiliaresPortal = await _portalNovedadRepository.GetAuxiliaresAsync(cancellationToken);
        var categoriasPortal = await _portalNovedadRepository.GetCategoriasAsync(cancellationToken);

        var auxiliares = auxiliaresCensoRaw
            .SelectMany(x => new[] { x.AuxiliarAsignado, x.NombreRealizaKardex })
            .Concat(auxiliaresPortal)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        return new ReportesFilterOptionsViewModel
        {
            Municipios = BuildSelectOptions(municipios, filters.Municipio, "Todos"),
            Auxiliares = BuildSelectOptions(auxiliares, filters.Auxiliar, "Todos"),
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
            Auxiliar = NormalizeText(filters.Auxiliar),
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

    private static IReadOnlyList<string> GetVisiblePortalCategories(string? selectedCategory)
    {
        return string.IsNullOrWhiteSpace(selectedCategory)
            ? CategoriaNovedadLabels.Keys.ToList()
            : [selectedCategory];
    }

    private static List<ReportesOperationalFocusViewModel> BuildOperationalFocus(IReadOnlyList<ReportesCensoRow> rows)
    {
        var focus = rows
            .GroupBy(x => NormalizeLabel(x.MunicipioResidencia, "Sin municipio"))
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

        if (!string.IsNullOrWhiteSpace(filters.Auxiliar))
        {
            labels.Add(filters.Auxiliar);
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
        public string? Estado { get; init; }
        public string? AutorizacionEvento { get; init; }
        public string GestionCompletaPendiente { get; init; } = string.Empty;
        public DateTime CreatedAtUtc { get; init; }
    }
}
