using System.Text.Json;
using Nexa.Data.Entities;

namespace Nexa.Helpers;

/// <summary>
/// Arma la requisición de insumos de una atención de NPT.
///
/// Estuvo dentro de <see cref="ClinicaHeridasKardexBuilder"/> como un tipo más, porque ahí vivía la
/// maquinaria de requisiciones; se trajo a su programa. La lista de insumos es fija —NPT no elige
/// apósitos— y el número de columnas sale de los días de tratamiento: la NPT se conecta y se
/// desconecta todos los días, así que hay una aplicación por día.
///
/// Reutiliza a propósito <see cref="ClinicaHeridasKardexDocumento"/>: es la forma del documento que
/// ya sabe pintar el modal de requisiciones, y tener dos tipos idénticos solo para cambiarles el
/// nombre obligaría a duplicar también esa vista.
/// </summary>
public static class NptKardexBuilder
{
    public const string Titulo = "REQUISICION DE INSUMOS Y/O DISPOSITIVOS MEDICOS - NPT";

    /// <summary>Etiqueta con la que la requisición viaja a farmacia.</summary>
    public const string TipoNombre = "NPT";

    /// <summary>Mismo tope que en heridas: una tabla más larga se vuelve ilegible.</summary>
    public const int MaximoAplicaciones = 60;

    private static readonly string[] Insumos =
    [
        "BATA ESTERIL",
        "CLORURO DE SODIO AL 0.9% 50ML",
        "GORRO DESECHABLE",
        "BOLSA ROJA PEQUEÑA",
        "BOLSA GRIS PEQUEÑA",
        "GUANTE VINILO TALLA M",
        "GUANTE ESTERIL TALLA 7.0",
        "CLORHEXIDINA 2% 60ML- SOLUCION",
        "EQUIPO FOTOSENSIBLE FRESENUIS KABI",
        "JERINGA DE 10 ML",
        "BIOCONECTOR",
        "APOSITO TRANPARENTE",
        "GASA ADHESIVA (ELECTOFIX) 10X10",
        "GASA ESTÉRIL PAQUETE 10X10CM X5 UNIDADES",
        "TAPABOCAS",
        "TIRAS",
        "LANCETA",
        "FILTRO PARA NPT"
    ];

    /// <summary>
    /// Una aplicación por día de tratamiento. En heridas el intervalo lo decide la frecuencia de
    /// visita; la NPT no tiene frecuencia porque se administra a diario.
    /// </summary>
    public static int CalcularAplicaciones(int? diasTratamiento)
    {
        var dias = diasTratamiento.GetValueOrDefault();
        return dias <= 0 ? 1 : Math.Clamp(dias, 1, MaximoAplicaciones);
    }

    /// <summary>
    /// Documento recién generado a partir del censo de NPT. Es el punto de partida: si la
    /// requisición ya tiene una versión editada guardada, esa manda sobre esta.
    /// </summary>
    public static ClinicaHeridasKardexDocumento Generar(
        CensoNptRecord record,
        string? elaboradoPor,
        DateTime fechaColombia)
    {
        var aplicaciones = CalcularAplicaciones(record.DiasTratamiento);

        var telefonos = string.Join(" / ", new[]
            {
                record.TelefonoPrincipal,
                record.TelefonoAdicional1,
                record.TelefonoAdicional2
            }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        return new ClinicaHeridasKardexDocumento
        {
            Tipo = TipoNombre,
            TipoNombre = TipoNombre,
            Titulo = Titulo,
            CodigoFormato = "FO-SEF-07",
            VersionFormato = "01",
            PaginaFormato = "1 de 1",
            FechaFormato = fechaColombia.ToString("dd/MM/yyyy"),
            Paciente = record.NombrePaciente,
            Documento = $"{record.TipoIdentificacion} {record.NumeroIdentificacion}".Trim(),
            Asegurador = record.Asegurador,
            Edad = record.Edad.ToString(),
            Direccion = record.Direccion ?? string.Empty,
            Telefonos = telefonos,
            CodigoCie10 = record.CodigoCie10,
            Diagnostico = record.DiagnosticoDescriptivo,
            AuxiliarAsignado = record.AuxiliarEnfermeriaAsignado ?? string.Empty,
            ElaboradoPor = elaboradoPor ?? string.Empty,
            FechaSolicitud = fechaColombia.ToString("yyyy-MM-dd"),
            DuracionDias = record.DiasTratamiento.GetValueOrDefault(),
            // La NPT se administra todos los días; se deja dicho en el documento porque el formato
            // tiene la casilla y en blanco se leería como que nadie la diligenció.
            Frecuencia = "Cada 24 horas",
            Aplicaciones = aplicaciones,
            Encabezados = ClinicaHeridasKardexBuilder.EncabezadosPorDefecto(aplicaciones),
            Observaciones = string.Empty,
            Insumos = Insumos
                .Select((descripcion, indice) => new ClinicaHeridasKardexInsumo
                {
                    Item = indice + 1,
                    Descripcion = descripcion,
                    Cantidades = Enumerable.Repeat(1, aplicaciones).ToList()
                })
                .ToList()
        };
    }

    /// <summary>
    /// Documento que corresponde a una requisición ya guardada: la versión editada si existe, y si
    /// no, la generada con lo que el censo de NPT tenga hoy.
    /// </summary>
    public static ClinicaHeridasKardexDocumento Resolver(
        CensoNptRecord record,
        string? kardexJson,
        string? elaboradoPor,
        DateTime fecha)
    {
        if (!string.IsNullOrWhiteSpace(kardexJson))
        {
            try
            {
                var guardado = JsonSerializer.Deserialize<ClinicaHeridasKardexDocumento>(
                    kardexJson,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));

                if (guardado is not null)
                {
                    // El tipo y el título los pone siempre el código: un documento guardado antes
                    // del traslado traería los de clínica de heridas.
                    guardado.Tipo = TipoNombre;
                    guardado.TipoNombre = TipoNombre;
                    guardado.Titulo = Titulo;
                    guardado.Encabezados = ClinicaHeridasKardexBuilder.NormalizarEncabezados(
                        guardado.Encabezados, guardado.Aplicaciones);
                    return guardado;
                }
            }
            catch (JsonException)
            {
                // Un JSON ilegible no debe dejar sin documento: se regenera.
            }
        }

        return Generar(record, elaboradoPor, fecha);
    }
}
