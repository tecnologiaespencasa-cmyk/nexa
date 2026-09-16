namespace Nexa.Data.Repositories.Models;

/// <summary>
/// Una novedad del Portal Administrativo (Neon, tabla "Novedad") que nombra al paciente por su
/// documento. La intranet solo lee estos datos.
/// </summary>
public class PortalNovedadPacienteRow
{
    public string Id { get; set; } = string.Empty;

    public string PacienteNombre { get; set; } = string.Empty;

    /// <summary>Momento en que se reportó. El portal lo guarda en UTC.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Última modificación (gestión, respuesta o cambio de estado). En UTC.</summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Valor del enum CategoriaNovedad: PACIENTE, RUTA, PROCESO_FARMACEUTICO…</summary>
    public string Categoria { get; set; } = string.Empty;

    /// <summary>El tipo concreto según la categoría (TipoNovedadPaciente, TipoNovedadFarmacia…).</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>PENDIENTE o RESUELTA.</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>BAJA, MEDIA o ALTA.</summary>
    public string Prioridad { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public string RespuestaPrestador { get; set; } = string.Empty;

    public string ResponsableGestion { get; set; } = string.Empty;

    public string AsignadoA { get; set; } = string.Empty;

    public string PrestadorNombre { get; set; } = string.Empty;

    public string PrestadorProfesion { get; set; } = string.Empty;

    public bool EsClinicaHeridas { get; set; }

    public IReadOnlyList<string> Medicamentos { get; set; } = [];
}

/// <summary>
/// Un reporte de ronda intramural del Portal Administrativo (Neon, "RondaIntramural"): el médico
/// de ronda identifica en la IPS a un paciente candidato a atención domiciliaria.
/// </summary>
public class PortalRondaPacienteRow
{
    public string Id { get; set; } = string.Empty;

    public string PacienteNombre { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Fecha de ingreso del paciente a la IPS donde se hizo la ronda (solo fecha).</summary>
    public DateTime FechaIngresoIps { get; set; }

    public string Ips { get; set; } = string.Empty;

    public string Cie10Codigo { get; set; } = string.Empty;

    public string DiagnosticoDescriptivo { get; set; } = string.Empty;

    /// <summary>Null mientras nadie ha confirmado si el paciente ingresó.</summary>
    public bool? IngresoEfectivo { get; set; }

    public string CausaNoIngreso { get; set; } = string.Empty;

    public string ObservacionNoIngreso { get; set; } = string.Empty;

    public string Otros { get; set; } = string.Empty;

    public string ReportadoPor { get; set; } = string.Empty;

    public IReadOnlyList<string> Medicamentos { get; set; } = [];
}
