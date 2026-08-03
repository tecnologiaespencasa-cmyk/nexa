using IntranetPrueba.Data.Entities;

namespace IntranetPrueba.Services.Interfaces;

public interface IEspacioCorporativoNotificationService
{
    /// <summary>
    /// Notifica al lider de tecnologia la creacion de una novedad con todos los datos del equipo y del responsable.
    /// </summary>
    Task<bool> NotifyNovedadCreadaAsync(
        EspacioActivoNovedad novedad,
        EspacioActivo? activo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Informa a quien reporto la novedad que su estado cambio.
    /// </summary>
    Task<bool> NotifyNovedadActualizadaAsync(
        EspacioActivoNovedad novedad,
        EspacioActivo? activo,
        string estadoAnterior,
        CancellationToken cancellationToken = default);
}
