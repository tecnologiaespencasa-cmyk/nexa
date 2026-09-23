using System.Globalization;

namespace Nexa.Helpers;

/// <summary>
/// Cómo se escriben fechas, duraciones y valores del Portal Administrativo en la hoja de vida del
/// paciente. Todo en palabras de quien lee, no de quien programó: "12 ago 2026", "3 días",
/// "Probable reacción alérgica" en lugar de "PROBABLE_REACCION_ALERGICA".
/// </summary>
public static class HojaVidaFormato
{
    // Abreviaturas propias y no las de la cultura es-CO, que en .NET llevan punto ("ago.") y
    // cambian según la versión del sistema operativo.
    private static readonly string[] Meses =
        ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    private static readonly string[] MesesLargos =
        ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    public static string MesCorto(int mes) => Meses[mes - 1];

    public static string MesLargo(int mes) => MesesLargos[mes - 1];

    /// <summary>"12 ago 2026".</summary>
    public static string Fecha(DateTime? fecha) =>
        fecha is { } f ? $"{f.Day} {Meses[f.Month - 1]} {f.Year}" : "Sin fecha";

    /// <summary>"12 ago", con el año solo si no es el actual.</summary>
    public static string FechaCorta(DateTime? fecha, DateTime hoy) =>
        fecha is not { } f
            ? "Sin fecha"
            : f.Year == hoy.Year ? $"{f.Day} {Meses[f.Month - 1]}" : $"{f.Day} {Meses[f.Month - 1]} {f.Year}";

    /// <summary>"12 de agosto de 2026", para las frases del resumen.</summary>
    public static string FechaLarga(DateTime fecha) =>
        $"{fecha.Day} de {MesesLargos[fecha.Month - 1]} de {fecha.Year}";

    /// <summary>"agosto de 2026".</summary>
    public static string MesAnio(DateTime fecha) => $"{MesesLargos[fecha.Month - 1]} de {fecha.Year}";

    /// <summary>"12 ago 2026, 3:40 p. m.".</summary>
    public static string FechaHora(DateTime fecha)
    {
        var hora = fecha.Hour % 12 == 0 ? 12 : fecha.Hour % 12;
        var meridiano = fecha.Hour < 12 ? "a. m." : "p. m.";
        return $"{Fecha(fecha)}, {hora}:{fecha.Minute:00} {meridiano}";
    }

    public static string Hora(TimeSpan? hora)
    {
        if (hora is not { } h)
        {
            return string.Empty;
        }

        var hh = h.Hours % 12 == 0 ? 12 : h.Hours % 12;
        return $"{hh}:{h.Minutes:00} {(h.Hours < 12 ? "a. m." : "p. m.")}";
    }

    /// <summary>"1 día", "12 días", "Mismo día".</summary>
    public static string Dias(int? dias) => dias switch
    {
        null => "Sin dato",
        0 => "Mismo día",
        1 => "1 día",
        _ => $"{dias.Value.ToString("N0", Colombia)} días"
    };

    /// <summary>"45 min", "2 h 15 min", "3 días 4 h".</summary>
    public static string Minutos(double minutos)
    {
        var total = (int)Math.Round(minutos);
        if (total < 60)
        {
            return $"{total} min";
        }

        if (total < 60 * 24)
        {
            var h = total / 60;
            var m = total % 60;
            return m == 0 ? $"{h} h" : $"{h} h {m} min";
        }

        var d = total / (60 * 24);
        var hr = (total % (60 * 24)) / 60;
        return hr == 0 ? Dias(d) : $"{Dias(d)} {hr} h";
    }

    /// <summary>
    /// Cuánto tardó algo, contado en horas mientras sean pocas ("6 h 25 min") y en días con su
    /// equivalente en horas cuando ya son muchas ("8 días · 196 h").
    /// </summary>
    public static string Transcurrido(int minutos)
    {
        if (minutos < 0)
        {
            minutos = 0;
        }

        if (minutos < 60)
        {
            return $"{minutos} min";
        }

        var horas = minutos / 60;
        if (horas < 72)
        {
            var resto = minutos % 60;
            return resto == 0 ? $"{horas} h" : $"{horas} h {resto} min";
        }

        return $"{Dias(minutos / (60 * 24)).ToLowerInvariant()} · {horas.ToString("N0", Colombia)} h";
    }

    public static string Numero(double valor, int decimales = 1) =>
        valor.ToString(decimales == 0 ? "N0" : "0." + new string('#', decimales), Colombia);

    /// <summary>Documento con separador de miles cuando es solo numérico: 43.123.456.</summary>
    public static string Documento(string? documento)
    {
        if (string.IsNullOrWhiteSpace(documento))
        {
            return string.Empty;
        }

        return documento.All(char.IsDigit) && documento.Length <= 15 && long.TryParse(documento, out var n)
            ? n.ToString("N0", Colombia)
            : documento;
    }

    public static string Categoria(string? valor) => valor switch
    {
        "PACIENTE" => "Paciente",
        "RUTA" => "Ruta",
        "PROCESO_FARMACEUTICO" => "Proceso farmacéutico",
        "LLAMADA_URGENTE" => "Llamada urgente",
        "TERAPIAS_AMBULATORIAS" => "Terapias ambulatorias",
        _ => Legible(valor)
    };

    public static string TipoNovedad(string? valor) => valor switch
    {
        "ERCA" => "ERCA",
        "CATETER_PICC" => "Catéter PICC",
        "DATOS_ERRADOS" => "Datos errados",
        "ACTUALIZACION_DATOS" => "Actualización de datos",
        "AGENDAMIENTO" => "Agendamiento",
        "FALLECIMIENTO" => "Fallecimiento",
        "HOSPITALIZACION" => "Hospitalización",
        "ALTA_TARDIA" => "Alta tardía",
        "INICIO_TRATAMIENTO_PRIORITARIO" => "Inicio de tratamiento prioritario",
        "RETRASO_INICIO_TRATAMIENTO" => "Retraso en el inicio del tratamiento",
        "PROBABLE_REACCION_ALERGICA" => "Probable reacción alérgica",
        "DOBLE_PRESTADOR" => "Doble prestador",
        "RELACIONAMIENTO" => "Relacionamiento",
        "IMPOSIBILIDAD_CONTACTAR_PACIENTE" => "No se pudo contactar al paciente",
        "IMPOSIBILIDAD_INGRESAR_DOMICILIO" => "No se pudo ingresar al domicilio",
        "PRORROGA_CAMBIO_ADICION_TRATAMIENTO" => "Prórroga, cambio o adición de tratamiento",
        "OTRA" => "Otra",
        "INCAPACIDAD" => "Incapacidad",
        "ACCIDENTE" => "Accidente",
        "CIERRE_VIAL" => "Cierre vial",
        "NO_REALIZO_RUTA" => "No realizó la ruta",
        "ERROR_KARDEX" => "Error en el kardex",
        "ERROR_REQUISICION" => "Error en la requisición",
        "ERROR_AUTORIZACION" => "Error en la autorización",
        "ERROR_AUXILIAR_ASIGNADO" => "Error en el auxiliar asignado",
        "ERROR_FORMULA" => "Error en la fórmula",
        "ERROR_TODOS_LOS_DOCUMENTOS" => "Error en todos los documentos",
        "PACIENTE_TERAPIA_AMBULATORIA" => "Paciente de terapia ambulatoria",
        "VALIDACION_PERTINENCIA_TERAPIAS" => "Validación de pertinencia de terapias",
        "CONSIDERACION_INGRESO_PROGRAMA_CRONICO" => "Considerar ingreso a crónicos",
        "PROBABLE_AGUDIZACION" => "Probable agudización",
        "SOLICITUD_EXTENSION_TERAPIAS" => "Solicitud de extensión de terapias",
        "CAMBIO_FRECUENCIA_TERAPIAS" => "Cambio de frecuencia de terapias",
        "VISITA_FALLIDA" => "Visita fallida",
        _ => Legible(valor)
    };

    public static string Responsable(string? valor) => valor switch
    {
        "ADMISIONES" => "Admisiones",
        "ANALISTA_ASISTENCIAL" => "Analista asistencial",
        "CLINICA_HERIDAS" => "Clínica de heridas",
        "DIRECCION_ASISTENCIAL" => "Dirección asistencial",
        _ => Legible(valor)
    };

    public static string Profesion(string? valor) => valor switch
    {
        "AUXILIAR_ENFERMERIA" => "Auxiliar de enfermería",
        "ENFERMERIA" => "Enfermería",
        "MEDICO" => "Médico",
        "FISIOTERAPIA" => "Fisioterapia",
        "FONOAUDIOLOGIA" => "Fonoaudiología",
        "NUTRICION" => "Nutrición",
        "OTRO" => "Otro",
        _ => Legible(valor)
    };

    public static string Prioridad(string? valor) => valor switch
    {
        "ALTA" => "Prioridad alta",
        "MEDIA" => "Prioridad media",
        "BAJA" => "Prioridad baja",
        _ => string.Empty
    };

    /// <summary>Estados de la bandeja de farmacia dichos para quien no la conoce.</summary>
    public static string EstadoFarmacia(string? estado) => estado switch
    {
        "Nuevo" => "Enviado, sin recibir en farmacia",
        "Recepcionado" => "Recibido en farmacia",
        "Facturado" => "Facturado",
        "Empacado" => "Empacado, listo para entregar",
        "PorDesempacar" => "Devuelto, por desempacar",
        "Despachado" => "Entregado",
        null or "" => "Sin enviar",
        _ => estado
    };

    /// <summary>"TEXTO_EN_MAYUSCULAS" → "Texto en mayusculas", para valores que no están en las tablas.</summary>
    private static string Legible(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var texto = valor.Replace('_', ' ').Trim().ToLowerInvariant();
        return char.ToUpperInvariant(texto[0]) + texto[1..];
    }
}
