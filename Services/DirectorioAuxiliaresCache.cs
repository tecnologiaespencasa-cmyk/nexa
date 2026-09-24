using System.Collections.Concurrent;

namespace Nexa.Services;

/// <summary>
/// Nombres del directorio de personal (Portal Administrativo, en Neon) que ofrecen los campos de
/// auxiliar del censo, guardados unos minutos entre consultas (2026-09-24).
///
/// Cada ficha los pedía a otra base, en otra nube: la primera conexión tardaba hasta 1,2 s. El
/// directorio cambia poco (149 personas de enfermería, se agregan o retiran de vez en cuando) y
/// la tabla no guarda fechas de cambio que permitan invalidar, así que se usa una vigencia corta:
/// alguien que se agrega en el portal aparece en el censo a más tardar en <see cref="Vigencia"/>.
/// Cada despliegue reinicia la aplicación y vacía la caché.
///
/// Por seguridad guarda solo los nombres, que es lo único que muestra la ficha, y no correos,
/// teléfonos ni cédulas. Las claves las arma el código con listas fijas de profesiones, nunca con
/// datos que mande el usuario, así que no se puede llenar ni envenenar desde afuera. Cuando vence,
/// una sola petición consulta a Neon y las que llegan a la vez esperan esa misma respuesta, en vez
/// de salir todas a consultar (avalancha de caché). Si la consulta falla no se guarda nada y el
/// error sigue su curso, igual que sin caché.
/// </summary>
public sealed class DirectorioAuxiliaresCache
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, Entrada> _entradas = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _candados = new(StringComparer.Ordinal);
    private readonly TimeProvider _reloj;
    private readonly ILogger<DirectorioAuxiliaresCache> _logger;

    public DirectorioAuxiliaresCache(TimeProvider reloj, ILogger<DirectorioAuxiliaresCache> logger)
    {
        _reloj = reloj;
        _logger = logger;
    }

    private sealed record Entrada(IReadOnlyList<string> Nombres, DateTimeOffset VenceEn);

    public async Task<IReadOnlyList<string>> ObtenerAsync(
        string clave,
        Func<CancellationToken, Task<IReadOnlyList<string>>> consultar,
        CancellationToken cancellationToken)
    {
        if (Vigente(clave, out var nombres))
        {
            return nombres;
        }

        var candado = _candados.GetOrAdd(clave, _ => new SemaphoreSlim(1, 1));
        await candado.WaitAsync(cancellationToken);
        try
        {
            // Mientras se esperaba el turno, otra petición pudo haberla llenado.
            if (Vigente(clave, out nombres))
            {
                return nombres;
            }

            nombres = await consultar(cancellationToken);
            _entradas[clave] = new Entrada(nombres, _reloj.GetUtcNow() + Vigencia);
            _logger.LogInformation(
                "Directorio de auxiliares ({Clave}) consultado en Neon: {Cantidad} nombres, vigentes {Minutos} min.",
                clave, nombres.Count, Vigencia.TotalMinutes);
            return nombres;
        }
        finally
        {
            candado.Release();
        }
    }

    private bool Vigente(string clave, out IReadOnlyList<string> nombres)
    {
        if (_entradas.TryGetValue(clave, out var entrada) && entrada.VenceEn > _reloj.GetUtcNow())
        {
            nombres = entrada.Nombres;
            return true;
        }

        nombres = [];
        return false;
    }
}
