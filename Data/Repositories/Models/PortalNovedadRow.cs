namespace Nexa.Data.Repositories.Models;

public class PortalNovedadRow
{
    public string Id { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public string Categoria { get; init; } = string.Empty;

    public string Estado { get; init; } = string.Empty;

    public string Prioridad { get; init; } = string.Empty;

    public string AsignadoA { get; init; } = string.Empty;

    public string PrestadorNombre { get; init; } = string.Empty;

    public string PacienteNombre { get; init; } = string.Empty;

    public string ResponsableGestion { get; init; } = string.Empty;
}

public class PortalNovedadesResumen
{
    public List<PortalNovedadesArea> Areas { get; init; } = [];

    /// <summary>"updatedAt" (UTC) de cada novedad resuelta dentro del día.</summary>
    public List<DateTime> ResueltasUtc { get; init; } = [];
}

public class PortalNovedadesArea
{
    /// <summary>Valor de "responsableGestion" (vacío si la novedad no tiene).</summary>
    public string Area { get; init; } = string.Empty;

    /// <summary>Sin resolver al final del rango.</summary>
    public int Pendientes { get; init; }

    public int Ingresadas { get; init; }

    public int Resueltas { get; init; }

    /// <summary>Sin resolver a las 8:00 a. m., deducido de las fechas.</summary>
    public int Saldo8am { get; init; }

    public DateTime? MasAntiguaPendienteUtc { get; init; }
}
