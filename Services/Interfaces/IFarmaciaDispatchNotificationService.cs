using IntranetPrueba.Data.Entities;

namespace IntranetPrueba.Services.Interfaces;

public interface IFarmaciaDispatchNotificationService
{
    Task<IReadOnlyList<string>> NotifyDispatchSentAsync(CensoRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> NotifyAssistantAssignedAsync(CensoRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> NotifyDespachadoAsync(CensoRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> NotifyEmpacadoPendienteAuxiliarAsync(CensoRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> NotifyEmpacadoPorVencerGerenciaAsync(CensoRecord record, CancellationToken cancellationToken = default);
}
