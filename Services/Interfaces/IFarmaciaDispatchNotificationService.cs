using Nexa.Data.Entities;

namespace Nexa.Services.Interfaces;

public interface IFarmaciaDispatchNotificationService
{
    Task<IReadOnlyList<string>> NotifyDispatchSentAsync(CensoRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> NotifyAssistantAssignedAsync(CensoRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> NotifyDespachadoAsync(CensoRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> NotifyEmpacadoPendienteAuxiliarAsync(CensoRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> NotifyEmpacadoPorVencerGerenciaAsync(CensoRecord record, CancellationToken cancellationToken = default);

    // Clínica de heridas: mismo flujo de avisos, con la requisición como adjunto.
    /// <summary>Requisición de NPT recién enviada a farmacia: se le manda al auxiliar asignado.</summary>
    Task<IReadOnlyList<string>> NotifyNptRequisicionEnviadaAsync(
        CensoNptKardex kardex,
        CancellationToken cancellationToken = default);

    /// <summary>Bolsa de NPT lista para reclamar.</summary>
    Task<IReadOnlyList<string>> NotifyNptDespachadoAsync(
        CensoNptKardex kardex,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> NotifyClinicaHeridasRequisicionEnviadaAsync(
        CensoClinicaHeridasKardex kardex,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> NotifyClinicaHeridasDespachadoAsync(
        CensoClinicaHeridasKardex kardex,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> NotifyClinicaHeridasEmpacadoPendienteAuxiliarAsync(
        CensoClinicaHeridasKardex kardex,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> NotifyClinicaHeridasEmpacadoPorVencerGerenciaAsync(
        CensoClinicaHeridasKardex kardex,
        CancellationToken cancellationToken = default);
}
