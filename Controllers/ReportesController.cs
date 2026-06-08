using System.Globalization;
using System.Text;
using IntranetPrueba.Data;
using IntranetPrueba.Models.ViewModels;
using IntranetPrueba.Models.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Controllers;

[Authorize(Policy = SystemPermissions.Reportes)]
public class ReportesController : Controller
{
    private static readonly string[] DashboardPalette =
    [
        "#0ea5e9",
        "#22c55e",
        "#f97316",
        "#e11d48",
        "#8b5cf6",
        "#06b6d4",
        "#f59e0b",
        "#64748b"
    ];

    private readonly ApplicationDbContext _context;

    public ReportesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var totalRegistrosCenso = await _context.Censos
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var totalEventosPendientesSinAutorizacion = await _context.Censos
            .AsNoTracking()
            .Where(x => x.AutorizacionEvento == null || x.AutorizacionEvento == string.Empty)
            .CountAsync(cancellationToken);

        var totalGestionesPendientes = await _context.Censos
            .AsNoTracking()
            .Where(x => x.GestionCompletaPendiente == "Pendiente")
            .CountAsync(cancellationToken);

        var totalPendientesCriticos = await _context.Censos
            .AsNoTracking()
            .Where(x => x.GestionCompletaPendiente == "Pendiente" && (x.AutorizacionEvento == null || x.AutorizacionEvento == string.Empty))
            .CountAsync(cancellationToken);

        var eventosPendientesPorAuxiliarRaw = await _context.Censos
            .AsNoTracking()
            .Where(x => x.AutorizacionEvento == null || x.AutorizacionEvento == string.Empty)
            .GroupBy(x => x.NombreRealizaKardex)
            .Select(group => new
            {
                Auxiliar = group.Key,
                Total = group.Count()
            })
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.Auxiliar)
            .ToListAsync(cancellationToken);

        var eventosPendientesPorAuxiliar = BuildCategoryCounts(
            eventosPendientesPorAuxiliarRaw.Select(x => (NormalizeLabel(x.Auxiliar, "Sin auxiliar asignado"), x.Total)).ToList(),
            totalEventosPendientesSinAutorizacion);

        var donutSlices = BuildDonutSlices(eventosPendientesPorAuxiliar);
        var donutGradient = BuildDonutGradient(donutSlices);

        var totalGestionesCompletas = Math.Max(totalRegistrosCenso - totalGestionesPendientes, 0);
        var porcentajeGestionPendiente = totalRegistrosCenso == 0
            ? 0
            : Math.Round((double)totalGestionesPendientes * 100d / totalRegistrosCenso, 2);

        var gestionPendientePorMunicipioRaw = await _context.Censos
            .AsNoTracking()
            .Where(x => x.GestionCompletaPendiente == "Pendiente")
            .GroupBy(x => x.MunicipioResidencia)
            .Select(group => new
            {
                Municipio = group.Key,
                Total = group.Count()
            })
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.Municipio)
            .Take(6)
            .ToListAsync(cancellationToken);

        var totalMunicipiosPendiente = gestionPendientePorMunicipioRaw.Sum(x => x.Total);
        var gestionPendientePorMunicipio = BuildCategoryCounts(
            gestionPendientePorMunicipioRaw.Select(x => (NormalizeLabel(x.Municipio, "Sin municipio"), x.Total)).ToList(),
            totalMunicipiosPendiente);

        var today = DateTime.Today;
        var startDate = today.AddDays(-6);

        var ingresosPorDiaRaw = await _context.Censos
            .AsNoTracking()
            .Where(x => x.FechaIngreso >= startDate && x.FechaIngreso <= today)
            .GroupBy(x => x.FechaIngreso)
            .Select(group => new
            {
                Fecha = group.Key,
                Total = group.Count()
            })
            .ToListAsync(cancellationToken);

        var tendenciaMap = ingresosPorDiaRaw.ToDictionary(x => x.Fecha.Date, x => x.Total);
        var tendenciaIngresos7Dias = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var date = startDate.AddDays(offset);
                return new ReportesTrendPointViewModel
                {
                    Date = date,
                    Value = tendenciaMap.TryGetValue(date, out var value) ? value : 0
                };
            })
            .ToList();

        var model = new ReportesDashboardViewModel
        {
            GeneratedAtLocal = DateTime.Now,
            TotalRegistrosCenso = totalRegistrosCenso,
            TotalEventosPendientesSinAutorizacion = totalEventosPendientesSinAutorizacion,
            TotalGestionesPendientes = totalGestionesPendientes,
            TotalGestionesCompletas = totalGestionesCompletas,
            TotalPendientesCriticos = totalPendientesCriticos,
            PorcentajeGestionPendiente = porcentajeGestionPendiente,
            PendientesDonutGradient = donutGradient,
            PendientesDonutSlices = donutSlices,
            EventosPendientesPorAuxiliar = eventosPendientesPorAuxiliar,
            GestionPendientePorMunicipio = gestionPendientePorMunicipio,
            TendenciaIngresos7Dias = tendenciaIngresos7Dias
        };

        return View(model);
    }

    private static List<ReportesDonutSliceViewModel> BuildDonutSlices(IReadOnlyList<ReportesCategoryCountViewModel> ranking)
    {
        if (ranking.Count == 0)
        {
            return [];
        }

        const int maxSlices = 7;
        var topValue = ranking.Max(x => x.Value);

        var topEntries = ranking
            .Select(x => (x.Label, x.Value))
            .Take(maxSlices - 1)
            .ToList();

        if (ranking.Count > maxSlices - 1)
        {
            var others = ranking.Skip(maxSlices - 1).Sum(x => x.Value);
            if (others > 0)
            {
                topEntries.Add(("Otros", others));
            }
        }
        else
        {
            topEntries = ranking
                .Select(x => (x.Label, x.Value))
                .ToList();
        }

        var total = topEntries.Sum(x => x.Value);

        return topEntries
            .Select((x, index) => new ReportesDonutSliceViewModel
            {
                Label = x.Label,
                Value = x.Value,
                Percentage = total == 0 ? 0 : Math.Round((double)x.Value * 100d / total, 2),
                Color = DashboardPalette[index % DashboardPalette.Length],
                IsTopPerformer = x.Value == topValue && x.Value > 0
            })
            .ToList();
    }

    private static string BuildDonutGradient(IReadOnlyList<ReportesDonutSliceViewModel> slices)
    {
        if (slices.Count == 0)
        {
            return "conic-gradient(#dbe3f1 0 100%)";
        }

        var current = 0d;
        var segments = new StringBuilder();

        for (var i = 0; i < slices.Count; i++)
        {
            var slice = slices[i];
            var end = i == slices.Count - 1 ? 100 : Math.Min(100, current + slice.Percentage);

            if (segments.Length > 0)
            {
                segments.Append(", ");
            }

            segments.Append(slice.Color);
            segments.Append(' ');
            segments.Append(current.ToString("0.##", CultureInfo.InvariantCulture));
            segments.Append("% ");
            segments.Append(end.ToString("0.##", CultureInfo.InvariantCulture));
            segments.Append('%');

            current = end;
        }

        if (current < 100)
        {
            segments.Append(", #dbe3f1 ");
            segments.Append(current.ToString("0.##", CultureInfo.InvariantCulture));
            segments.Append("% 100%");
        }

        return $"conic-gradient({segments})";
    }

    private static List<ReportesCategoryCountViewModel> BuildCategoryCounts(
        IReadOnlyList<(string Label, int Value)> rawValues,
        int total)
    {
        return rawValues
            .Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Label)
            .Select(x => new ReportesCategoryCountViewModel
            {
                Label = x.Label,
                Value = x.Value,
                Percentage = total == 0 ? 0 : Math.Round((double)x.Value * 100d / total, 2)
            })
            .ToList();
    }

    private static string NormalizeLabel(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
