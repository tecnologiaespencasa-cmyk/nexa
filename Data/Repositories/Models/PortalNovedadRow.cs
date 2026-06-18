namespace IntranetPrueba.Data.Repositories.Models;

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
