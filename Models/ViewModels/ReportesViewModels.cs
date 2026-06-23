using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IntranetPrueba.Models.ViewModels;

public class ReportesDashboardViewModel
{
    public DateTime GeneratedAtLocal { get; init; } = DateTime.Now;

    public ReportesFilterViewModel Filters { get; init; } = new();

    public ReportesFilterOptionsViewModel FilterOptions { get; init; } = new();

    public int TotalRegistrosCenso { get; init; }

    public int TotalNovedades { get; init; }

    public int TotalEventosPendientesSinAutorizacion { get; init; }

    public int TotalGestionesPendientes { get; init; }

    public int TotalGestionesCompletas { get; init; }

    public int TotalPendientesCriticos { get; init; }

    public int TotalIngresosPeriodo { get; init; }

    public int PromedioNovedadesPorDia { get; init; }

    public int PromedioIngresosPorDia { get; init; }

    public int TotalNovedadesResueltas { get; init; }

    public double PorcentajeGestionPendiente { get; init; }

    public double PorcentajeResolucionNovedades { get; init; }

    public double? PromedioResolucionHoras { get; init; }

    public string PromedioResolucionDisplay => PromedioResolucionHoras.HasValue
        ? FormatHours(PromedioResolucionHoras.Value)
        : "Sin datos";

    public ReportesTrendSeriesViewModel NovedadesPorDia { get; init; } = new();

    public ReportesTrendSeriesViewModel IngresosPorDia { get; init; } = new();

    public IReadOnlyList<ReportesCategoryCountViewModel> NovedadesPorTipo { get; init; } = [];

    public IReadOnlyList<ReportesCategoryCountViewModel> EventosPendientesPorAuxiliar { get; init; } = [];

    public IReadOnlyList<ReportesCategoryCountViewModel> GestionPendientePorMunicipio { get; init; } = [];

    public IReadOnlyList<ReportesResolutionByTypeViewModel> ResolucionPorTipo { get; init; } = [];

    public IReadOnlyList<ReportesOperationalFocusViewModel> FocosOperativos { get; init; } = [];

    public IReadOnlyList<ReportesRecentRecordViewModel> RegistrosPrioritarios { get; init; } = [];

    public IReadOnlyList<string> ActiveFilterLabels { get; init; } = [];

    private static string FormatHours(double hours)
    {
        if (hours < 1)
        {
            return $"{Math.Round(hours * 60d, 0).ToString("N0", CultureInfo.GetCultureInfo("es-CO"))} min";
        }

        if (hours < 48)
        {
            return $"{hours.ToString("0.0", CultureInfo.GetCultureInfo("es-CO"))} h";
        }

        return $"{(hours / 24d).ToString("0.0", CultureInfo.GetCultureInfo("es-CO"))} d";
    }
}

public class ReportesFilterViewModel
{
    public DateTime? Desde { get; init; }

    public DateTime? Hasta { get; init; }

    public string? Municipio { get; init; }

    public string? Auxiliar { get; init; }

    public string? EstadoGestion { get; init; }

    public string? EstadoCenso { get; init; }

    public string? TipoNovedad { get; init; }
}

public class ReportesFilterOptionsViewModel
{
    public IReadOnlyList<SelectListItem> Municipios { get; init; } = [];

    public IReadOnlyList<SelectListItem> Auxiliares { get; init; } = [];

    public IReadOnlyList<SelectListItem> EstadosGestion { get; init; } = [];

    public IReadOnlyList<SelectListItem> EstadosCenso { get; init; } = [];

    public IReadOnlyList<SelectListItem> TiposNovedad { get; init; } = [];
}

public class ReportesCategoryCountViewModel
{
    public string Label { get; init; } = string.Empty;

    public int Value { get; init; }

    public double Percentage { get; init; }

    public string Color { get; init; } = "#2563eb";

    public string BarWidthCss => $"{Math.Max(Percentage, Value > 0 ? 4 : 0).ToString("0.##", CultureInfo.InvariantCulture)}%";
}

public class ReportesTrendPointViewModel
{
    public DateTime Date { get; init; }

    public string Label { get; init; } = string.Empty;

    public int Value { get; init; }

    public double Percentage { get; init; }
}

public class ReportesTrendSeriesViewModel
{
    public int ScaleMax { get; init; } = 100;

    public IReadOnlyList<int> ScaleTicks { get; init; } = [100, 80, 60, 40, 20, 0];

    public IReadOnlyList<ReportesTrendPointViewModel> Points { get; init; } = [];
}

public class ReportesResolutionByTypeViewModel
{
    public string Type { get; init; } = string.Empty;

    public int ResolvedCount { get; init; }

    public double? AverageHours { get; init; }

    public double Percentage { get; init; }

    public string BarWidthCss => AverageHours.HasValue
        ? $"{Math.Max(5, Percentage).ToString("0.##", CultureInfo.InvariantCulture)}%"
        : "0%";

    public string AverageDisplay => AverageHours.HasValue
        ? AverageHours.Value < 48
            ? $"{AverageHours.Value.ToString("0.0", CultureInfo.GetCultureInfo("es-CO"))} h"
            : $"{(AverageHours.Value / 24d).ToString("0.0", CultureInfo.GetCultureInfo("es-CO"))} d"
        : "Sin cierre";
}

public class ReportesOperationalFocusViewModel
{
    public string Label { get; init; } = string.Empty;

    public int Records { get; init; }

    public int PendingManagement { get; init; }

    public int WithoutAuthorization { get; init; }

    public double RiskScore { get; init; }

    public double Percentage { get; init; }
}

public class ReportesRecentRecordViewModel
{
    public long Id { get; init; }

    public string Paciente { get; init; } = string.Empty;

    public string Documento { get; init; } = string.Empty;

    public string Municipio { get; init; } = string.Empty;

    public string Auxiliar { get; init; } = string.Empty;

    public string EstadoGestion { get; init; } = string.Empty;

    public string Alerta { get; init; } = string.Empty;

    public DateTime FechaBase { get; init; }

    public bool SinAutorizacion { get; init; }
}
