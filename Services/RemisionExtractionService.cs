using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IntranetPrueba.Services.Interfaces;
using IntranetPrueba.Services.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IntranetPrueba.Services;

public class RemisionExtractionService : IRemisionExtractionService
{
    private const string OpenAiBaseUrl = "https://api.openai.com/";
    private const string ResponsesEndpoint = "v1/responses";
    private const string DefaultModel = "gpt-4.1-mini";
    private const int MaxDocumentCharacters = 24000;

    private const string SystemPrompt = """
Eres un asistente de extracción de datos clínicos para el censo del programa agudos de Especialistas En Casa (medicina domiciliaria).
Recibirás el texto plano de un documento de remisión, una hoja de Excel "Formato Remisión" o el cuerpo de un correo de remisión de pacientes. Debes extraer los datos solicitados en formato JSON.

Reglas estrictas:
- NO inventes ningún dato. Si un dato no aparece en el documento, retorna null en ese campo.
- No corrijas ni completes información: extrae los valores tal como aparecen (solo puedes normalizar mayúsculas, tildes y espacios).
- tipo_documento: una de [CC, TI, CE, PA, RC, PE, PPT] deducida del tipo de identificación (Cédula de ciudadanía -> CC, Tarjeta de identidad -> TI, Cédula de extranjería -> CE, Pasaporte -> PA, Registro civil -> RC, Permiso especial de permanencia -> PE, Permiso por protección temporal -> PPT). Si no aparece, null.
- documento: solo el número de identificación, sin puntos ni espacios.
- REGLA CRÍTICA DE IDENTIDAD: nombre, tipo_documento y documento corresponden EXCLUSIVAMENTE al paciente. Busca primero el bloque que esté rotulado como "Paciente", "Identificación del paciente", "Número de identificación" o equivalente. El tipo y número deben provenir del mismo bloque de datos del paciente.
- NUNCA uses para el paciente el documento de un médico, profesional, prescriptor, firmante, responsable, cuidador, familiar, acompañante o contacto. Ignora de forma estricta los documentos que aparezcan junto a expresiones como "Médico", "Medicina general", "Especialidad", "Firma", "Profesional", "Registro médico" o al final de una orden/prescripción. Si solo encuentras un documento de un profesional o hay duda sobre a quién pertenece, retorna documento y tipo_documento como null.
- Ejemplo: si el encabezado identifica a "Paciente" y más abajo aparece "MARIO... CC: 71744574 MEDICINA GENERAL", ese último documento pertenece al médico y NO puede usarse como documento del paciente.
- Los teléfonos deben contener solo dígitos, sin espacios ni guiones. telefono1 es el celular o teléfono principal del paciente; telefono2 es un teléfono alternativo distinto (puede aparecer dentro del resumen de historia clínica, por ejemplo el del cuidador). Si solo existe uno, telefono2 debe ser null. Nunca repitas el mismo número en ambos campos.
- fecha_nacimiento y fecha_consulta deben ir en formato YYYY-MM-DD. fecha_consulta es la fecha de admisión o de remisión que registre el documento.
- edad: edad del paciente en años (número entero) si aparece en el documento; si no, null.
- diagnostico: nombre del diagnóstico principal. cie10: código diagnóstico tal como aparece (ej: A46X, D440).
- ips_remite: la institución que remite al paciente.
- plan_salud: el plan de salud del paciente tal como aparece (ej: POS). tipo_asegurador: el tipo de asegurador si el documento lo registra; si está vacío, null.
- direccion: la dirección del paciente. barrio y ciudad en campos separados si existen.
- cuidador: nombre del cuidador o responsable del paciente.
- Para cada medicamento del plan de manejo / tratamientos:
  - nombre: nombre completo del medicamento tal como aparece.
  - dosis: valor numérico de la cantidad (ej: "900.0" -> 900).
  - unidad: una de [Miligramos, Gramos, Unidades, Gotas, Mililitros] si se puede deducir del texto (MG -> Miligramos, ML -> Mililitros); si no, null.
  - via: una de [Intravenosa, Intramuscular, Subcutánea, Nebulizada, Oral] SOLO si está explícita o es claramente deducible del documento (ej: "solución inyectable" junto con "antibiótico venoso" o "IV" -> Intravenosa); si no, null.
  - frecuencia: una de [INFUSION CONTINUA, CADA 4 HORAS, CADA 6 HORAS, CADA 8 HORAS, CADA 12 HORAS, CADA 24 HORAS, CADA 48 HORAS, CADA 72 HORAS] según el documento (ej: "8 Horas" -> CADA 8 HORAS); si no se puede determinar, null.
  - duracion_texto: la duración tal cual aparece en el documento (ej: "21 Dosis", "7 días").
  - duracion_dias: la duración convertida a días enteros. Si la duración está expresada en dosis, conviértela usando la frecuencia: dias = dosis / (24 / horas_de_frecuencia). Ejemplo: 21 dosis cada 8 horas = 21 / 3 = 7 días. Si ya está en días, usa ese valor. Si no se puede calcular, null.
- Si el documento no registra medicamentos, retorna una lista vacía en medicamentos.
- Responde ÚNICAMENTE con el JSON solicitado, sin texto adicional.
""";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RemisionExtractionService> _logger;

    public RemisionExtractionService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<RemisionExtractionService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(OpenAiBaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(90);
    }

    public async Task<RemisionExtractionResult> ExtractRemisionDataAsync(string documentText, CancellationToken cancellationToken = default)
    {
        var normalizedText = documentText?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return RemisionExtractionResult.Failure("El documento no contiene texto para analizar.");
        }

        var apiKey = _configuration["OpenAI:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return RemisionExtractionResult.Failure(
                "No hay una ApiKey de OpenAI configurada. Define la variable de entorno OpenAI__ApiKey.");
        }

        if (normalizedText.Length > MaxDocumentCharacters)
        {
            normalizedText = normalizedText[..MaxDocumentCharacters];
        }

        var model = _configuration["OpenAI:Model"]?.Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            model = DefaultModel;
        }

        var payload = new
        {
            model,
            temperature = 0,
            max_output_tokens = 3000,
            input = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = normalizedText }
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "remision_extraccion",
                    strict = true,
                    schema = BuildExtractionSchema()
                }
            }
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ResponsesEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "OpenAI respondió {StatusCode} al extraer datos de remisión: {Body}",
                    (int)response.StatusCode,
                    Truncate(body, 2000));
                return RemisionExtractionResult.Failure(
                    "El servicio de extracción no está disponible en este momento. Intenta nuevamente.");
            }

            return ParseResponsesApiBody(body, normalizedText);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RemisionExtractionResult.Failure("La extracción tardó demasiado y fue cancelada. Intenta nuevamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error de conexión con OpenAI al extraer datos de remisión.");
            return RemisionExtractionResult.Failure("No fue posible conectarse al servicio de extracción.");
        }
    }

    private RemisionExtractionResult ParseResponsesApiBody(string body, string documentText)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var errorElement)
                && errorElement.ValueKind == JsonValueKind.Object)
            {
                var errorMessage = errorElement.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString()
                    : null;
                _logger.LogError("OpenAI retornó un error en la extracción: {Error}", errorMessage);
                return RemisionExtractionResult.Failure("El servicio de extracción reportó un error. Intenta nuevamente.");
            }

            if (!root.TryGetProperty("output", out var outputElement)
                || outputElement.ValueKind != JsonValueKind.Array)
            {
                return RemisionExtractionResult.Failure("El servicio de extracción devolvió una respuesta inesperada.");
            }

            foreach (var item in outputElement.EnumerateArray())
            {
                if (!item.TryGetProperty("type", out var typeElement)
                    || !string.Equals(typeElement.GetString(), "message", StringComparison.OrdinalIgnoreCase)
                    || !item.TryGetProperty("content", out var contentElement)
                    || contentElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var contentItem in contentElement.EnumerateArray())
                {
                    var contentType = contentItem.TryGetProperty("type", out var contentTypeElement)
                        ? contentTypeElement.GetString()
                        : null;

                    if (string.Equals(contentType, "refusal", StringComparison.OrdinalIgnoreCase))
                    {
                        return RemisionExtractionResult.Failure("El modelo rechazó analizar el documento.");
                    }

                    if (!string.Equals(contentType, "output_text", StringComparison.OrdinalIgnoreCase)
                        || !contentItem.TryGetProperty("text", out var textElement))
                    {
                        continue;
                    }

                    var jsonText = textElement.GetString();
                    if (string.IsNullOrWhiteSpace(jsonText))
                    {
                        continue;
                    }

                    // Valida que la salida del modelo sea JSON antes de entregarla al cliente.
                    using (JsonDocument.Parse(jsonText))
                    {
                    }

                    return ApplyPatientIdentityGuard(jsonText, documentText);
                }
            }

            var status = root.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;
            _logger.LogError("OpenAI no devolvió texto de salida. Estado: {Status}", status);
            return RemisionExtractionResult.Failure("El servicio de extracción no devolvió datos. Intenta nuevamente.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "La respuesta de OpenAI no es un JSON válido.");
            return RemisionExtractionResult.Failure("El servicio de extracción devolvió una respuesta inválida.");
        }
    }

    private static object BuildExtractionSchema()
    {
        var medicamentoSchema = new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "nombre", "dosis", "unidad", "via", "frecuencia", "duracion_texto", "duracion_dias" },
            properties = new Dictionary<string, object>
            {
                ["nombre"] = new { type = "string", description = "Nombre completo del medicamento tal como aparece en el documento." },
                ["dosis"] = NullableType("number"),
                ["unidad"] = NullableType("string"),
                ["via"] = NullableType("string"),
                ["frecuencia"] = NullableType("string"),
                ["duracion_texto"] = NullableType("string"),
                ["duracion_dias"] = NullableType("integer")
            }
        };

        return new
        {
            type = "object",
            additionalProperties = false,
            required = new[]
            {
                "nombre", "tipo_documento", "documento", "fecha_nacimiento", "edad", "diagnostico", "cie10",
                "fecha_consulta", "ips_remite", "plan_salud", "tipo_asegurador", "telefono1", "telefono2",
                "cuidador", "direccion", "barrio", "ciudad", "medicamentos"
            },
            properties = new Dictionary<string, object>
            {
                ["nombre"] = NullableType("string"),
                ["tipo_documento"] = NullableType("string"),
                ["documento"] = NullableType("string"),
                ["fecha_nacimiento"] = NullableType("string"),
                ["edad"] = NullableType("integer"),
                ["diagnostico"] = NullableType("string"),
                ["cie10"] = NullableType("string"),
                ["fecha_consulta"] = NullableType("string"),
                ["ips_remite"] = NullableType("string"),
                ["plan_salud"] = NullableType("string"),
                ["tipo_asegurador"] = NullableType("string"),
                ["telefono1"] = NullableType("string"),
                ["telefono2"] = NullableType("string"),
                ["cuidador"] = NullableType("string"),
                ["direccion"] = NullableType("string"),
                ["barrio"] = NullableType("string"),
                ["ciudad"] = NullableType("string"),
                ["medicamentos"] = new { type = "array", items = medicamentoSchema }
            }
        };
    }

    private static object NullableType(string type) => new { type = new[] { type, "null" } };

    private RemisionExtractionResult ApplyPatientIdentityGuard(string jsonText, string documentText)
    {
        try
        {
            var result = JsonNode.Parse(jsonText)?.AsObject();
            var documentNumber = result?["documento"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(documentNumber)
                || !AppearsOnlyInProfessionalContext(documentText, documentNumber))
            {
                return RemisionExtractionResult.Success(jsonText);
            }

            result!["documento"] = null;
            _logger.LogWarning(
                "Se descartó una identificación extraída porque solo aparece en un contexto de profesional o firma.");
            return RemisionExtractionResult.Success(result.ToJsonString());
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "No fue posible aplicar la validación adicional de identidad del paciente.");
            return RemisionExtractionResult.Success(jsonText);
        }
    }

    private static bool AppearsOnlyInProfessionalContext(string documentText, string documentNumber)
    {
        var digits = Regex.Replace(documentNumber, "\\D", string.Empty);
        if (digits.Length < 5)
        {
            return false;
        }

        var numberPattern = string.Join("\\D*", digits.Select(digit => Regex.Escape(digit.ToString())));
        var matches = Regex.Matches(documentText, $"(?<!\\d){numberPattern}(?!\\d)", RegexOptions.CultureInvariant);
        if (matches.Count == 0)
        {
            return false;
        }

        var isOnlyProfessional = true;
        foreach (Match match in matches)
        {
            var start = Math.Max(0, match.Index - 260);
            var length = Math.Min(documentText.Length - start, match.Length + 520);
            var context = documentText.Substring(start, length);
            var hasPatientMarker = Regex.IsMatch(context,
                "paciente|identificaci[oó]n|documento de identidad|historia cl[ií]nica|n[uú]mero de identificaci[oó]n",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var hasProfessionalMarker = Regex.IsMatch(context,
                "m[eé]dico|medicina general|profesional|prescriptor|firma|firmado|especialidad|registro m[eé]dico|doctor|doctora",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (hasPatientMarker || !hasProfessionalMarker)
            {
                isOnlyProfessional = false;
                break;
            }
        }

        return isOnlyProfessional;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
