using Nexa.Data.Entities;

namespace Nexa.Helpers;

/// <summary>
/// Arma la requisición de insumos de una atención de clínica de heridas a partir de lo que el
/// usuario diligenció en la sección 3: el tipo de atención decide la lista de insumos, y la duración
/// del tratamiento con la frecuencia de visita deciden cuántas aplicaciones (columnas) lleva.
/// </summary>
public static class ClinicaHeridasKardexBuilder
{
    public const string Titulo = "REQUISICION DE INSUMOS Y/O DISPOSITIVOS MEDICOS - CLINICA DE HERIDAS";

    /// <summary>
    /// Tope de columnas que se dibujan. Un tratamiento diario muy largo generaría una tabla
    /// inmanejable; al llegar aquí se avisa en el documento en vez de romper el formato.
    /// </summary>
    public const int MaximoAplicaciones = 60;

    // Insumos comunes a las dos atenciones que curan la herida.
    private static readonly string[] BaseCuracion =
    [
        "CLORURO DE SODIO 0.9% 100ML",
        "AGUJA HIPODERMICA 18X1",
        "GASA ESTÉRIL PAQUETE 10X10CM X5 UNIDADES",
        "GUANTE VINILO TALLA M",
        "GUANTE ESTERIL TALLA 7.0",
        "GASA ADHESIVA (ELECTOFIX) 10X10"
    ];

    private static readonly string[] InsumosNpt =
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

    private static readonly string[] InsumosPicc =
    [
        "AGUJA HIPODERMICA 18X1",
        "TEGADERM 10 CM X 12 CM",
        "BATA PACIENTE MANGA LARGA",
        "BIO-CONECTOR SIN AGUJA",
        "CLORHEXIDINA 2% 60ML- SOLUCION",
        "CLORURO DE SODIO 0.9% 100ml",
        "GASA ADHESIVA (ELECTOFIX) 10X10",
        "GORRO DESECHABLE",
        "GUANTE ESTERIL TALLA 7.0",
        "GUANTE VINILO TALLA M",
        "GASA ESTÉRIL PAQUETE 10X10CM X5 UNIDADES",
        "JERINGA 10ML"
    ];

    /// <summary>
    /// Solo manejo de herida y VAC arrastran los apósitos/medicamentos elegidos en la sección 3.
    /// NPT y PICC tienen lista fija.
    /// </summary>
    public static bool UsaApositosSeleccionados(string tipo) =>
        tipo is ClinicaHeridasKardexTipos.ManejoHerida or ClinicaHeridasKardexTipos.Vac;

    /// <summary>Cuántas veces se atiende al paciente en el tratamiento completo.</summary>
    public static int CalcularAplicaciones(int? duracionDias, string? frecuenciaVisita)
    {
        var dias = duracionDias.GetValueOrDefault();
        if (dias <= 0)
        {
            return 1;
        }

        var intervalo = IntervaloEnDias(frecuenciaVisita);
        // Division entera: 30 días una vez a la semana son 4 aplicaciones completas.
        var aplicaciones = dias / intervalo;
        return Math.Clamp(aplicaciones, 1, MaximoAplicaciones);
    }

    private static int IntervaloEnDias(string? frecuenciaVisita) => (frecuenciaVisita ?? string.Empty).Trim() switch
    {
        "Cada 24 horas" => 1,
        "Cada 48 horas" => 2,
        "Cada 72 horas" => 3,
        "Una vez a la semana" => 7,
        _ => 1
    };

    public static IReadOnlyList<string> Insumos(string tipo, IReadOnlyList<string> apositosSeleccionados)
    {
        return tipo switch
        {
            ClinicaHeridasKardexTipos.ManejoHerida => [.. apositosSeleccionados, .. BaseCuracion],
            ClinicaHeridasKardexTipos.Vac => [.. apositosSeleccionados, .. BaseCuracion, "HOJA DE BISTURI N°11"],
            ClinicaHeridasKardexTipos.Npt => InsumosNpt,
            ClinicaHeridasKardexTipos.Picc => InsumosPicc,
            _ => []
        };
    }

    /// <summary>
    /// Documento recién generado a partir del censo. Es el punto de partida: si el kardex ya tiene
    /// una versión editada guardada, esa manda sobre esta.
    /// </summary>
    public static ClinicaHeridasKardexDocumento Generar(
        CensoClinicaHeridasRecord record,
        string tipo,
        string? elaboradoPor,
        DateTime fechaColombia)
    {
        var apositos = new[]
            {
                record.ApositoMedicamento1,
                record.ApositoMedicamento2,
                record.ApositoMedicamento3,
                record.ApositoMedicamento4
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();

        var aplicaciones = CalcularAplicaciones(record.DuracionTratamientoDias, record.FrecuenciaVisita);
        var insumos = Insumos(tipo, UsaApositosSeleccionados(tipo) ? apositos : []);

        var telefonos = string.Join(" / ", new[]
            {
                record.TelefonoPrincipal,
                record.TelefonoAdicional1,
                record.TelefonoAdicional2
            }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        return new ClinicaHeridasKardexDocumento
        {
            Tipo = tipo,
            TipoNombre = ClinicaHeridasKardexTipos.Nombre(tipo),
            Titulo = Titulo,
            CodigoFormato = "FO-SEF-07",
            VersionFormato = "01",
            PaginaFormato = "1 de 1",
            FechaFormato = fechaColombia.ToString("dd/MM/yyyy"),
            Paciente = record.NombrePaciente,
            Documento = $"{record.TipoIdentificacion} {record.NumeroIdentificacion}".Trim(),
            Asegurador = record.Asegurador,
            Edad = record.Edad.ToString(),
            Direccion = string.Join(" ", new[] { record.Direccion, record.DetalleDireccion }
                .Where(x => !string.IsNullOrWhiteSpace(x))),
            Telefonos = telefonos,
            CodigoCie10 = record.CodigoCie10,
            Diagnostico = record.DiagnosticoDescriptivo,
            AuxiliarAsignado = record.AuxiliarEnfermeriaAsignado ?? string.Empty,
            ElaboradoPor = elaboradoPor ?? string.Empty,
            FechaSolicitud = fechaColombia.ToString("yyyy-MM-dd"),
            DuracionDias = record.DuracionTratamientoDias.GetValueOrDefault(),
            Frecuencia = record.FrecuenciaVisita ?? string.Empty,
            Aplicaciones = aplicaciones,
            Encabezados = EncabezadosPorDefecto(aplicaciones),
            Observaciones = string.Empty,
            Insumos = insumos
                .Select((descripcion, indice) => new ClinicaHeridasKardexInsumo
                {
                    Item = indice + 1,
                    Descripcion = descripcion,
                    // Una unidad por atención: es lo que se consume en cada curación.
                    Cantidades = Enumerable.Repeat(1, aplicaciones).ToList()
                })
                .ToList()
        };
    }

    /// <summary>
    /// Documento que corresponde a un kardex ya guardado: la version editada si existe, y si no, la
    /// generada con los apositos y el tratamiento del plan (no con los que el censo tenga hoy).
    /// </summary>
    public static ClinicaHeridasKardexDocumento Resolver(
        CensoClinicaHeridasRecord record,
        CensoClinicaHeridasPlan plan,
        string tipo,
        string? kardexJson,
        string? elaboradoPor,
        DateTime fecha)
    {
        if (!string.IsNullOrWhiteSpace(kardexJson))
        {
            try
            {
                var guardado = System.Text.Json.JsonSerializer.Deserialize<ClinicaHeridasKardexDocumento>(
                    kardexJson,
                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

                if (guardado is not null)
                {
                    guardado.Tipo = tipo;
                    guardado.TipoNombre = ClinicaHeridasKardexTipos.Nombre(tipo);
                    guardado.Titulo = Titulo;
                    guardado.Encabezados = NormalizarEncabezados(guardado.Encabezados, guardado.Aplicaciones);
                    return guardado;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Un JSON ilegible no debe dejar sin documento: se regenera.
            }
        }

        var origen = new CensoClinicaHeridasRecord
        {
            Id = record.Id,
            NombrePaciente = record.NombrePaciente,
            TipoIdentificacion = record.TipoIdentificacion,
            NumeroIdentificacion = record.NumeroIdentificacion,
            Asegurador = record.Asegurador,
            Edad = record.Edad,
            Direccion = record.Direccion,
            DetalleDireccion = record.DetalleDireccion,
            TelefonoPrincipal = record.TelefonoPrincipal,
            TelefonoAdicional1 = record.TelefonoAdicional1,
            TelefonoAdicional2 = record.TelefonoAdicional2,
            CodigoCie10 = record.CodigoCie10,
            DiagnosticoDescriptivo = record.DiagnosticoDescriptivo,
            AuxiliarEnfermeriaAsignado = record.AuxiliarEnfermeriaAsignado,
            ApositoMedicamento1 = plan.ApositoMedicamento1,
            ApositoMedicamento2 = plan.ApositoMedicamento2,
            ApositoMedicamento3 = plan.ApositoMedicamento3,
            ApositoMedicamento4 = plan.ApositoMedicamento4,
            DuracionTratamientoDias = plan.DuracionTratamientoDias,
            FrecuenciaVisita = plan.FrecuenciaVisita
        };

        return Generar(origen, tipo, elaboradoPor, fecha);
    }

    /// <summary>
    /// Titulos de las columnas de aplicacion. Son editables: el usuario suele reemplazarlos por la
    /// fecha real de cada visita, asi que viajan guardados con el documento.
    /// </summary>
    public static List<string> EncabezadosPorDefecto(int aplicaciones) =>
        Enumerable.Range(1, Math.Max(1, aplicaciones))
            .Select(numero => "Aplicación " + numero)
            .ToList();

    /// <summary>
    /// Ajusta la lista de encabezados al numero de columnas: conserva los que el usuario ya escribio
    /// y completa con los de por defecto. Un documento guardado antes de este campo llega sin ellos.
    /// </summary>
    public static List<string> NormalizarEncabezados(List<string>? encabezados, int aplicaciones)
    {
        var total = Math.Max(1, aplicaciones);
        var pordefecto = EncabezadosPorDefecto(total);
        if (encabezados is null || encabezados.Count == 0)
        {
            return pordefecto;
        }

        return Enumerable.Range(0, total)
            .Select(indice =>
            {
                var actual = indice < encabezados.Count ? encabezados[indice]?.Trim() : null;
                return string.IsNullOrWhiteSpace(actual) ? pordefecto[indice] : actual!;
            })
            .ToList();
    }
}

public sealed class ClinicaHeridasKardexDocumento
{
    public string Tipo { get; set; } = string.Empty;
    public string TipoNombre { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string CodigoFormato { get; set; } = string.Empty;
    public string VersionFormato { get; set; } = string.Empty;
    public string PaginaFormato { get; set; } = string.Empty;
    public string FechaFormato { get; set; } = string.Empty;
    public string Paciente { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string Asegurador { get; set; } = string.Empty;
    public string Edad { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Telefonos { get; set; } = string.Empty;
    public string CodigoCie10 { get; set; } = string.Empty;
    public string Diagnostico { get; set; } = string.Empty;
    public string AuxiliarAsignado { get; set; } = string.Empty;
    public string ElaboradoPor { get; set; } = string.Empty;
    public string FechaSolicitud { get; set; } = string.Empty;
    public int DuracionDias { get; set; }
    public string Frecuencia { get; set; } = string.Empty;
    public int Aplicaciones { get; set; }
    public List<string> Encabezados { get; set; } = [];
    public string Observaciones { get; set; } = string.Empty;
    public List<ClinicaHeridasKardexInsumo> Insumos { get; set; } = [];
}

public sealed class ClinicaHeridasKardexInsumo
{
    public int Item { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public List<int> Cantidades { get; set; } = [];
    public int Total => Cantidades.Sum();
}
