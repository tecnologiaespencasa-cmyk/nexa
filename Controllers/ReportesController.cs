using System.Globalization;
using IntranetPrueba.Data;
using IntranetPrueba.Data.Repositories.Interfaces;
using IntranetPrueba.Data.Repositories.Models;
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
            TotalNovedadesResueltas = resolvedPortalRows.Count,
            PorcentajeGestionPendiente = totalRegistrosCenso == 0 ? 0 : Math.Round(totalGestionesPendientes * 100d / totalRegistrosCenso, 2),
            PorcentajeResolucionNovedades = portalRows.Count == 0 ? 0 : Math.Round(resolvedPortalRows.Count * 100d / portalRows.Count, 2),
            PromedioResolucionHoras = promedioResolucionHoras,
            NovedadesPorDia = BuildTrend(portalRows.Select(x => x.CreatedAt), normalizedFilters),
            IngresosPorDia = BuildTrend(censoRows.Select(x => x.FechaIngreso), normalizedFilters),
            NovedadesPorTipo = BuildCategoryCounts(
                portalRows
                    .GroupBy(x => GetCategoriaLabel(x.Categoria))
                    .Select(x => (x.Key, x.Count())),
                portalRows.Count),
            EventosPendientesPorAuxiliar = BuildCategoryCounts(
                censoRows
                    .Where(IsWithoutAuthorization)
                    .GroupBy(x => NormalizeLabel(FirstNonEmpty(x.AuxiliarAsignado, x.NombreRealizaKardex), "Sin auxiliar asignado"))
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
            ResolucionPorTipo = BuildResolutionByType(resolvedPortalRows),
            FocosOperativos = BuildOperationalFocus(censoRows),
            RegistrosPrioritarios = BuildPriorityRecords(censoRows),
            ActiveFilterLabels = BuildActiveFilterLabels(normalizedFilters)
        };

        return View(model);
    }

    private static IQueryable<Data.Entities.CensoRecord> ApplyBaseFilters(
        IQueryable<Data.Entities.CensoRecord> query,
        ReportesFilterViewModel filters)
    {
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
            TiposNovedad = BuildSelectOptions(
                categoriasPortal.Select(x => (x, GetCategoriaLabel(x))),
                filters.TipoNovedad,
                "Todas"),
            Vistas = BuildSelectOptions(
                [
                    ("dia", "Día"),
                    ("semana", "Semana"),
                    ("mes", "Mes")
                ],
                filters.Vista ?? VistaDia)
        };
    }

    private static ReportesFilterViewModel NormalizeFilters(ReportesFilterViewModel filters)
    {
        var today = DateTime.Today;
        var desde = filters.Desde?.Date ?? today.AddDays(-29);
        var hasta = filters.Hasta?.Date ?? today;

        if (desde > hasta)
        {
            (desde, hasta) = (hasta, desde);
        }

        var vista = NormalizeText(filters.Vista);
        if (string.IsNullOrWhiteSpace(vista))
        {
            var totalDays = Math.Max(1, (hasta - desde).TotalDays);
            vista = totalDays > 120 ? VistaMes : totalDays > 45 ? VistaSemana : VistaDia;
        }

        if (!string.Equals(vista, VistaDia, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(vista, VistaSemana, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(vista, VistaMes, StringComparison.OrdinalIgnoreCase))
        {
            vista = VistaDia;
        }

        return new ReportesFilterViewModel
        {
            Desde = desde,
            Hasta = hasta,
            Municipio = NormalizeText(filters.Municipio),
            Auxiliar = NormalizeText(filters.Auxiliar),
            EstadoGestion = NormalizeText(filters.EstadoGestion),
            TipoNovedad = NormalizeText(filters.TipoNovedad),
            Vista = vista
        };
    }

    private static List<ReportesTrendPointViewModel> BuildTrend(IEnumerable<DateTime> dates, ReportesFilterViewModel filters)
    {
        var desde = filters.Desde ?? DateTime.Today.AddDays(-29);
        var hasta = filters.Hasta ?? DateTime.Today;
        var vista = filters.Vista ?? VistaDia;
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

        var max = Math.Max(1, points.Max(x => x.Value));
        return points
            .Select(x => new ReportesTrendPointViewModel
            {
                Date = x.Date,
                Label = x.Label,
                Value = x.Value,
                Percentage = Math.Round(x.Value * 100d / max, 2)
            })
            .ToList();
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

    private static List<ReportesResolutionByTypeViewModel> BuildResolutionByType(IReadOnlyList<PortalNovedadRow> resolvedRows)
    {
        var resolvedByType = resolvedRows
            .GroupBy(x => GetCategoriaLabel(x.Categoria))
            .Select(x => new
            {
                Type = x.Key,
                Count = x.Count(),
                Average = x.Average(item => (item.UpdatedAt - item.CreatedAt).TotalHours)
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        var maxAverage = Math.Max(1, resolvedByType.Count == 0 ? 1 : resolvedByType.Max(x => x.Average));
        return resolvedByType
            .Select(x => new ReportesResolutionByTypeViewModel
            {
                Type = x.Type,
                ResolvedCount = x.Count,
                AverageHours = x.Average,
                Percentage = Math.Round(x.Average * 100d / maxAverage, 2)
            })
            .ToList();
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

    private static IReadOnlyList<SelectListItem> BuildSelectOptions(
        IEnumerable<(string Value, string Text)> values,
        string selected)
    {
        return values
            .Select(x => new SelectListItem
            {
                Value = x.Value,
                Text = x.Text,
                Selected = string.Equals(x.Value, selected, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();
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
        public string NombreRealizaKardex { get; init; } = string.Empty;
        public string? AuxiliarAsignado { get; init; }
        public string MunicipioResidencia { get; init; } = string.Empty;
        public string? Estado { get; init; }
        public string? AutorizacionEvento { get; init; }
        public string GestionCompletaPendiente { get; init; } = string.Empty;
        public DateTime CreatedAtUtc { get; init; }
    }
}
