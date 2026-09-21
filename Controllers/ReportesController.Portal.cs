using System.Globalization;
using Nexa.Data.Repositories.Models;
using Nexa.Helpers;
using Nexa.Models.ViewModels;

namespace Nexa.Controllers;

/// <summary>
/// Panel de novedades del Portal Administrativo (tabla "Novedad" en Neon).
///
/// El portal guarda las fechas en UTC. El periodo se pide con sus límites de Colombia convertidos a
/// UTC y cada novedad se ubica en su día de Colombia: antes se agrupaba por el día UTC y las novedades
/// creadas entre las 7 p. m. y la medianoche aparecían al día siguiente.
///
/// El portal no guarda la hora exacta en que se resolvió una novedad. El tiempo "hasta el cierre" se
/// mide hasta su última actualización ("updatedAt"), que para una novedad resuelta es el cierre o
/// algo posterior; el panel lo dice con esas palabras.
/// </summary>
public partial class ReportesController
{
    private static readonly IReadOnlyDictionary<string, string> CategoriaNovedadLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PACIENTE"] = "Paciente",
            ["RUTA"] = "Ruta",
            ["PROCESO_FARMACEUTICO"] = "Proceso farmacéutico",
            ["LLAMADA_URGENTE"] = "Llamada urgente",
            ["TERAPIAS_AMBULATORIAS"] = "Terapias ambulatorias"
        };

    private async Task<ReportesPortalViewModel> ConstruirPortalAsync(
        ReportesFilterViewModel f,
        Periodo p,
        DateTime ahoraColombia,
        CancellationToken ct)
    {
        IReadOnlyList<PortalNovedadRow> filas;
        IReadOnlyList<string> categorias;
        try
        {
            filas = await _portalNovedadRepository.GetNovedadesAsync(
                ColombiaTime.ConvertToUtc(p.Desde),
                ColombiaTime.ConvertToUtc(p.HastaExclusivo),
                f.TipoNovedad,
                null,
                ct);
            categorias = await _portalNovedadRepository.GetCategoriasAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Si el portal no responde, los paneles del censo se muestran igual.
            _logger.LogError(ex, "No se pudieron leer las novedades del Portal Administrativo.");
            return new ReportesPortalViewModel
            {
                Error = "No se pudo consultar el Portal Administrativo. Las cifras del censo no dependen de él; vuelva a cargar la página en unos minutos."
            };
        }

        var novedades = filas
            .Select(x => new
            {
                Fila = x,
                Creada = ColombiaTime.Convert(x.CreatedAt)
            })
            .ToList();

        var resueltas = novedades.Where(x => EsIgual(x.Fila.Estado, "RESUELTA")).ToList();
        var pendientes = novedades.Where(x => !EsIgual(x.Fila.Estado, "RESUELTA")).ToList();
        var cerradas = resueltas.Where(x => x.Fila.UpdatedAt > x.Fila.CreatedAt).ToList();

        // Todas las categorías que existen en el portal, incluidas las que no tuvieron novedades: un
        // cero también es información. Con un tipo filtrado solo se muestra ese.
        var visibles = (string.IsNullOrWhiteSpace(f.TipoNovedad) ? categorias : [f.TipoNovedad!])
            .Concat(novedades.Select(x => x.Fila.Categoria))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var porTipo = visibles
            .Select(c => (Etiqueta: EtiquetaCategoriaNovedad(c), Valor: novedades.Count(x => EsIgual(x.Fila.Categoria, c))))
            .OrderByDescending(x => x.Valor)
            .ThenBy(x => x.Etiqueta, StringComparer.Create(Cultura, ignoreCase: true))
            .ToList();
        var mayorTipo = porTipo.Count == 0 ? 0 : porTipo.Max(x => x.Valor);

        var tiempos = visibles
            .Select(c =>
            {
                var grupo = cerradas.Where(x => EsIgual(x.Fila.Categoria, c)).ToList();
                return new
                {
                    Tipo = EtiquetaCategoriaNovedad(c),
                    Resueltas = grupo.Count,
                    Promedio = grupo.Count == 0
                        ? (double?)null
                        : grupo.Average(x => (x.Fila.UpdatedAt - x.Fila.CreatedAt).TotalHours)
                };
            })
            .OrderByDescending(x => x.Resueltas)
            .ThenBy(x => x.Tipo, StringComparer.Create(Cultura, ignoreCase: true))
            .ToList();
        var mayorTiempo = tiempos.Where(x => x.Promedio.HasValue).Select(x => x.Promedio!.Value).DefaultIfEmpty(0).Max();

        // Tiempo de resolución por tramo, según el día (de Colombia) en que se creó cada novedad
        // resuelta. Un tramo sin novedades resueltas queda vacío: no es cero horas, es sin dato.
        var resolucionPorTramo = cerradas
            .GroupBy(x => InicioDeTramo(x.Creada.Date, p.Vista))
            .ToDictionary(
                g => g.Key,
                g => (Horas: g.Average(x => (x.Fila.UpdatedAt - x.Fila.CreatedAt).TotalHours), Casos: g.Count()));

        return new ReportesPortalViewModel
        {
            Total = novedades.Count,
            Resueltas = resueltas.Count,
            Pendientes = pendientes.Count,
            PorcentajeResueltas = novedades.Count == 0 ? 0 : Math.Round(resueltas.Count * 100d / novedades.Count, 1),
            PromedioHorasHastaCierre = cerradas.Count == 0
                ? null
                : cerradas.Average(x => (x.Fila.UpdatedAt - x.Fila.CreatedAt).TotalHours),
            PromedioPorDia = Promedio(novedades.Count, p.Dias),
            DiasPendienteMasAntigua = pendientes.Count == 0
                ? null
                : pendientes.Max(x => (ahoraColombia.Date - x.Creada.Date).Days),
            FiltradoPorTipo = !string.IsNullOrWhiteSpace(f.TipoNovedad),
            PorDia = ConstruirSerie(novedades.Select(x => x.Creada), p),
            ResolucionPorDia = new ReportesSerieHorasViewModel
            {
                Vista = p.Vista,
                PromedioPeriodo = cerradas.Count == 0
                    ? null
                    : cerradas.Average(x => (x.Fila.UpdatedAt - x.Fila.CreatedAt).TotalHours),
                Puntos = EnumerarTramos(p)
                    .Select(t => new ReportesPuntoHorasViewModel
                    {
                        Inicio = t.Inicio,
                        Etiqueta = t.Etiqueta,
                        Dia = t.Dia,
                        Rango = t.Rango,
                        FinDeSemana = t.FinDeSemana,
                        Horas = resolucionPorTramo.TryGetValue(t.Inicio, out var r) ? r.Horas : null,
                        Casos = resolucionPorTramo.TryGetValue(t.Inicio, out var c) ? c.Casos : 0
                    })
                    .ToList()
            },
            PorTipo = porTipo
                .Select(x => new ReportesCategoriaViewModel
                {
                    Etiqueta = x.Etiqueta,
                    Valor = x.Valor,
                    Porcentaje = novedades.Count == 0 ? 0 : Math.Round(x.Valor * 100d / novedades.Count, 1),
                    Barra = mayorTipo == 0 ? 0 : Math.Round(x.Valor * 100d / mayorTipo, 2)
                })
                .ToList(),
            TiempoPorTipo = tiempos
                .Select(x => new ReportesTiempoTipoViewModel
                {
                    Tipo = x.Tipo,
                    Resueltas = x.Resueltas,
                    PromedioHoras = x.Promedio,
                    Barra = x.Promedio.HasValue && mayorTiempo > 0 ? Math.Round(x.Promedio.Value * 100d / mayorTiempo, 2) : 0
                })
                .ToList(),
            Prioridad = ConstruirSegmentos(
                ("Alta", novedades.Count(x => EsIgual(x.Fila.Prioridad, "ALTA")), "n1"),
                ("Media", novedades.Count(x => EsIgual(x.Fila.Prioridad, "MEDIA")), "n2"),
                ("Baja", novedades.Count(x => EsIgual(x.Fila.Prioridad, "BAJA")), "n3"),
                ("Sin dato", novedades.Count(x => !EsIgual(x.Fila.Prioridad, "ALTA")
                    && !EsIgual(x.Fila.Prioridad, "MEDIA")
                    && !EsIgual(x.Fila.Prioridad, "BAJA")), "neutro"))
        };
    }

    private static string EtiquetaCategoriaNovedad(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Sin tipo";
        }

        return CategoriaNovedadLabels.TryGetValue(value, out var label)
            ? label
            : FraseEnMinuscula(value.Replace('_', ' ').ToUpper(CultureInfo.InvariantCulture));
    }
}
