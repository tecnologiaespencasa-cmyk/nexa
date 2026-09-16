using Nexa.Models.ViewModels;

namespace Nexa.Services.Interfaces;

public interface IHojaVidaPacienteService
{
    /// <summary>
    /// Arma la hoja de vida del paciente con el documento indicado. Es de solo lectura: no
    /// reconcilia episodios ni escribe en ninguna base. Si el Portal Administrativo o SharePoint no
    /// responden a tiempo, la hoja se entrega igual con el aviso correspondiente.
    /// </summary>
    Task<HojaVidaPacienteViewModel> ConstruirAsync(string? documento, CancellationToken cancellationToken);
}
