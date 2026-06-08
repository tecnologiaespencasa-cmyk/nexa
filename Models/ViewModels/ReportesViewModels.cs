using System.Globalization;

namespace IntranetPrueba.Models.ViewModels;

public class ReportesDashboardViewModel
{
    public DateTime GeneratedAtLocal { get; init; } = DateTime.Now;

    public int TotalRegistrosCenso { get; init; }

    public int TotalEventosPendientesSinAutorizacion { get; init; }

    public int TotalGestionesPendientes { get; init; }

    public int TotalGestionesCompletas { get; init; }

    public int TotalPendientesCriticos { get; init; }

    public double PorcentajeGestionPendiente { get; init; }

    public string PendientesDonutGradient { get; init; } = "conic-gradient(#dbe3f1 0 100%)";

    public IReadOnlyList<ReportesDonutSliceViewModel> PendientesDonutSlices { get; init; } = [];

    public IReadOnlyList<ReportesCategoryCountViewModel> EventosPendientesPorAuxiliar { get; init; } = [];

    public IReadOnlyList<ReportesCategoryCountViewModel> GestionPendientePorMunicipio { get; init; } = [];

    public IReadOnlyList<ReportesTrendPointViewModel> TendenciaIngresos7Dias { get; init; } = [];
}

public class ReportesDonutSliceViewModel
{
    public string Label { get; init; } = string.Empty;

    public int Value { get; init; }

    public double Percentage { get; init; }

    public string Color { get; init; } = "#dbe3f1";

    public bool IsTopPerformer { get; init; }
}

public class ReportesCategoryCountViewModel
{
    public string Label { get; init; } = string.Empty;

    public int Value { get; init; }

    public double Percentage { get; init; }

    public string BarWidthCss => $"{Percentage.ToString("0.##", CultureInfo.InvariantCulture)}%";
}

public class ReportesTrendPointViewModel
{
    public DateTime Date { get; init; }

    public int Value { get; init; }
}
