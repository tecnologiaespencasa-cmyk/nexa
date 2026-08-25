using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

/// <summary>
/// Un plan de requisiciones agrupa las requisiciones de todas las atenciones del paciente en un
/// momento dado. Al abrir un plan nuevo, el anterior se cierra completo y queda de consulta: sus
/// requisiciones y los apósitos con los que se armaron se conservan tal como estaban.
/// </summary>
public class CensoClinicaHeridasPlan
{
    public long Id { get; set; }

    public long CensoClinicaHeridasRecordId { get; set; }

    public CensoClinicaHeridasRecord CensoClinicaHeridasRecord { get; set; } = null!;

    /// <summary>Consecutivo dentro del paciente: 1, 2, 3…</summary>
    public int Numero { get; set; }

    /// <summary>Perfil que abrió el plan.</summary>
    [StringLength(200)]
    public string? CreadoPor { get; set; }

    public DateTime CreadoAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Null mientras es el plan vigente. Solo puede haber uno abierto por paciente.</summary>
    public DateTime? CerradoAtUtc { get; set; }

    [StringLength(200)]
    public string? CerradoPor { get; set; }

    // Copia de los apósitos/medicamentos vigentes cuando se armó el plan. Mientras el plan está
    // abierto se sincroniza con lo que el usuario guarda en la sección 3; al cerrarse queda fijo,
    // que es lo que permite consultar un plan viejo con los insumos que realmente llevaba.
    [StringLength(200)]
    public string? ApositoMedicamento1 { get; set; }

    [StringLength(200)]
    public string? ApositoMedicamento2 { get; set; }

    [StringLength(200)]
    public string? ApositoMedicamento3 { get; set; }

    [StringLength(200)]
    public string? ApositoMedicamento4 { get; set; }

    public int? DuracionTratamientoDias { get; set; }

    [StringLength(160)]
    public string? FrecuenciaVisita { get; set; }

    public ICollection<CensoClinicaHeridasKardex> Kardex { get; set; } = [];

    public bool EstaVigente => CerradoAtUtc is null;

    public IReadOnlyList<string> Apositos =>
    [
        .. new[] { ApositoMedicamento1, ApositoMedicamento2, ApositoMedicamento3, ApositoMedicamento4 }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
    ];
}
