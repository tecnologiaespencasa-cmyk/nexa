using System.Text.Json;
using Nexa.Data.Entities;
using Nexa.Models.EspacioCorporativo;

namespace Nexa.Helpers;

/// <summary>
/// Lectura de las firmas de un acta emitida.
///
/// Las actas nuevas guardan todas sus firmas en FirmasJson. Las anteriores al
/// disenador solo tienen las dos columnas de siempre: aqui se reconstruyen con la
/// misma forma, de modo que las vistas y el correo trabajen con una sola lista.
/// </summary>
public static class EspacioActaFirmas
{
    private const string RotuloEntrega = "Entrega";

    public static IReadOnlyList<EspacioActaFirmaEmitida> Leer(EspacioActaDocumental acta)
    {
        if (!string.IsNullOrWhiteSpace(acta.FirmasJson))
        {
            try
            {
                var firmas = JsonSerializer.Deserialize<List<EspacioActaFirmaEmitida>>(
                    acta.FirmasJson,
                    EspacioActaDisenador.JsonOptions);

                if (firmas is { Count: > 0 })
                {
                    return firmas;
                }
            }
            catch (JsonException)
            {
                // Un JSON corrupto no debe impedir ver el acta: se cae al formato heredado.
            }
        }

        var rotuloRecibe = EspacioActaPlantillas.Obtener(acta.PlantillaCodigo)?.RotuloRecibe ?? "Recibe";

        return
        [
            new EspacioActaFirmaEmitida
            {
                Clave = EspacioActaFirma.ClaveEmisor,
                Rotulo = RotuloEntrega,
                Nombre = acta.EmitidaPorNombre,
                Documento = acta.EmitidaPorDocumento,
                Cargo = string.IsNullOrWhiteSpace(acta.EmitidaPorCargo)
                    ? "Área de Tecnología"
                    : acta.EmitidaPorCargo,
                DataUrl = acta.FirmaEmiteDataUrl
            },
            new EspacioActaFirmaEmitida
            {
                Clave = EspacioActaFirma.ClaveRecibe,
                Rotulo = rotuloRecibe,
                Nombre = acta.NombreRecibe,
                Documento = acta.DocumentoRecibe,
                DataUrl = acta.FirmaRecibeDataUrl
            }
        ];
    }
}
