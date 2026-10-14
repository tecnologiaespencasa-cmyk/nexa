using Nexa.Models.ViewModels;

namespace Nexa.Services.Interfaces;

public interface ITelevisorIndicadoresService
{
    /// <summary>
    /// Tablero de <paramref name="dia"/> (hora Colombia) con el estado que tenía en
    /// <paramref name="estadoAl"/>: los ingresos registrados hasta ese momento y las marcas de
    /// gestión puestas hasta entonces.
    /// </summary>
    Task<TelevisorTablero> ConstruirAsync(DateTime dia, DateTime estadoAl, CancellationToken ct);
}

public interface ITelevisorCortesService
{
    /// <summary>Lo que muestra la pantalla: el día en vivo, o un día anterior desde sus fotos.</summary>
    Task<TelevisorPantallaViewModel> PantallaAsync(DateTime? dia, string? bloque, CancellationToken ct);

    /// <summary>Toma las fotos que falten: saldo de las 8:00, corte de la 1:45 y cierres de días anteriores.</summary>
    Task TomarCortesPendientesAsync(DateTime ahoraUtc, CancellationToken ct);

    /// <summary>Fija (o devuelve a automático con "auto") el turno de una persona en un día.</summary>
    Task<bool> AjustarTurnoAsync(DateTime dia, int personaId, string turno, string? usuario, CancellationToken ct);
}
