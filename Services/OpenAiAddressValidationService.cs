using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Nexa.Services;

/// <summary>
/// Ubica las direcciones del censo con el mismo modelo de OpenAI que usa el extractor de remisiones.
/// Reemplazó a Google Maps el 2026-11-17: Google cobraba pasadas 10.000 consultas al mes y se cayó
/// cuando venció la tarjeta de la cuenta de facturación.
///
/// Ojo con lo que esto es y lo que no: el modelo NO tiene un mapa ni busca en internet. Normaliza
/// la nomenclatura, separa el detalle (apto, torre…), deduce el municipio y el área y dice si la
/// dirección está incompleta o fuera de Antioquia. El barrio NO lo adivina: probado el 2026-11-17,
/// sus barrios y comunas para Medellín fallaban casi siempre (Calle 50 # 45-20 lo ponía en Belén).
/// El barrio y la zona los pone el censo con las direcciones vecinas ya validadas
/// (CensoController.Cuadricula.cs); aquí solo se acepta el barrio si la persona lo escribió.
/// Una dirección se da por válida solo si el modelo responde con confianza alta y conserva intactos
/// los números escritos; si no, se muestran sugerencias para que alguien las confirme.
/// </summary>
public class OpenAiAddressValidationService : IAddressValidationService
{
    private const string OpenAiBaseUrl = "https://api.openai.com/";
    private const string ResponsesEndpoint = "v1/responses";
    private const string DefaultModel = "gpt-4.1-mini";
    private const int MaxCandidatos = 3;
    private const int MaxLargoDireccion = 300;
    private static readonly TimeSpan VigenciaMemoria = TimeSpan.FromHours(12);

    /// <summary>
    /// Lo que ya se preguntó, para no volver a pagar la misma dirección: el botón «Validar
    /// dirección» y el guardado que la sigue preguntan lo mismo, y una sugerencia que la persona
    /// eligió queda guardada como válida para que el servidor no la vuelva a poner en duda.
    /// </summary>
    private static readonly ConcurrentDictionary<string, (DateTimeOffset Vence, AddressValidationResult Resultado)> Memoria = new();

    /// <summary>Los mismos municipios (y corregimientos de Medellín) que ofrece el censo.</summary>
    private static readonly string[] MunicipiosAtendidos =
    [
        "MEDELLIN", "BELLO", "ENVIGADO", "ITAGÜÍ", "SABANETA", "LA ESTRELLA", "AMAGA", "BARBOSA", "CALDAS",
        "COPACABANA", "DON MATIAS", "EL CARMEN DE VIBORAL", "EL SANTUARIO", "GIRARDOTA", "GUARNE", "GUATAPE",
        "LA CEJA", "LA UNION", "MARINILLA", "PEÑOL", "RETIRO", "SAN PEDRO DE LOS MILAGROS", "SAN VICENTE DE FERRER",
        "SANTA ROSA DE OSOS", "RIONEGRO", "SAN FELIX", "SAN ANTONIO DE PRADO", "SAN CRISTOBAL", "SANTA ELENA",
        "ALTAVISTA", "SAN SEBASTIAN DE PALMITAS"
    ];

    private static readonly string Instrucciones = $"""
Eres el asistente de direcciones del censo de Especialistas En Casa, una empresa de medicina domiciliaria que atiende pacientes en Medellín, su área metropolitana y algunos municipios de Antioquia (Colombia). Recibes la dirección de la casa de un paciente tal como la escribió el personal y debes ubicarla y normalizarla.

Municipios atendidos (usa EXACTAMENTE uno de estos valores en "municipio"): {string.Join(", ", MunicipiosAtendidos)}.
San Antonio de Prado, San Cristóbal, Santa Elena, Altavista y San Sebastián de Palmitas son corregimientos de Medellín, pero en el censo se registran como municipio: si la dirección está en uno de ellos, usa ese valor y no MEDELLIN.

Nomenclatura de Medellín y el Valle de Aburrá:
- Las calles van de oriente a occidente y su número crece de sur a norte (Calle 10 en El Poblado, Calle 30 en Belén, Calle 50 en el centro, Calle 100 en el norte). Las calles con sufijo SUR quedan al sur (Envigado, Sabaneta, Itagüí, sur de El Poblado).
- Las carreras van de sur a norte y su número crece de oriente a occidente (Carrera 43A en El Poblado, Carrera 52 en el centro, Carrera 70 en Laureles, Carrera 80 y más en el occidente). Las carreras con sufijo ESTE quedan al oriente (laderas de Buenos Aires, Villa Hermosa, Santa Elena).
- "CALLE 32EE # 80-39" es la casa sobre la Calle 32EE, a 39 metros de la esquina con la Carrera 80. Las letras (A, B, AA, EE, BIS) son parte del número de la vía.

Reglas:
1. NUNCA cambies, agregues ni quites números o letras de la vía ni de la placa. Los separadores "#", "No", "N°", "Nro", el guion y los espacios son equivalentes: "TV 39B 74 12" es "TRANSVERSAL 39B # 74-12" y "CL 32 EE 80 39" es "CALLE 32EE # 80-39". La dirección solo es "incompleta" si falta el número de la vía o el número de la placa (por ejemplo "Calle 50" o "Carrera 80 # 33"). "Cra 80 # 33-45 int 201 torre 2" está COMPLETA: vía CARRERA 80, placa 33-45, y "INTERIOR 201 TORRE 2" es detalle.
2. Escribe la dirección en MAYÚSCULAS y con la forma estándar: tipo de vía completo (CALLE, CARRERA, TRANSVERSAL, DIAGONAL, CIRCULAR, AVENIDA, AUTOPISTA, VARIANTE), número, "#", y la placa con guion. Ejemplos: "Cra 43a #1sur-100" -> "CARRERA 43A # 1 SUR-100"; "Tv 39b No 74-12" -> "TRANSVERSAL 39B # 74-12". No agregues el municipio ni el barrio dentro de "direccion".
3. Lo que no es la vía y la placa (apartamento, torre, interior, bloque, casa, piso, unidad o edificio, local, puntos de referencia como "frente a la iglesia") va en "detalle", en mayúsculas y abreviado de forma clara (APTO 202, TORRE 3, INTERIOR 5). Si no hay, detalle es null.
4. "area" es "Rural" si es vereda, finca, kilómetro de una vía, parcelación o sector rural de un corregimiento; si no, "Urbana". En direcciones rurales sin nomenclatura urbana (por ejemplo "VEREDA EL PLAN, FINCA LA ESPERANZA") la regla 1 no aplica: normaliza lo escrito, y el estado es "encontrada" si se reconoce la vereda o el sector.
5. MUNICIPIO: la gran mayoría de los pacientes viven en MEDELLIN. Si la dirección no dice el municipio, NO es incompleta: asume MEDELLIN cuando la nomenclatura existe en Medellín (números de calle entre 1 y 130 y de carrera entre 1 y 130 sin sufijo SUR, letras como A, B, AA, EE, BB), y usa confianza "alta" si el número de calle y de carrera ubican claramente un sector urbano de Medellín. Usa "varias" SOLO cuando haya un indicio concreto de que puede ser otro municipio (por ejemplo calles SUR, que pueden ser Envigado, Itagüí, Sabaneta o el sur de El Poblado); en ese caso da hasta {MaxCandidatos} candidatos del más probable al menos probable. Si la dirección menciona el municipio, úsalo.
6. "barrio": SOLO si el barrio viene escrito en el texto (por ejemplo "CALLE 32EE # 80-39 BELÉN LAS PLAYAS"); en ese caso sácalo de la dirección. Si no está escrito, null: no lo deduzcas.
7. Si la dirección es de otro departamento (Bogotá, Cali, Pereira…), el estado es "fuera_de_antioquia". Si es de Antioquia pero de un municipio que no está en la lista (Antioquia tiene 125 municipios: Caucasia, Apartadó, Turbo, Yarumal, Sonsón, Santa Fe de Antioquia y muchos más), el estado es "municipio_no_atendido" y el mensaje dice que ese municipio de Antioquia no lo atiende el censo. Si el texto no es una dirección, el estado es "no_es_direccion". En esos casos y en "incompleta", candidatos es una lista vacía.
8. "confianza": "alta" si el municipio es claro (escrito o deducido con seguridad por la regla 5) y la vía y la placa están completas; "media" o "baja" si tienes dudas reales sobre el municipio o la dirección.
9. "mensaje": una frase corta en español para el personal: qué encontraste, qué falta o por qué no se pudo ubicar. Sin tecnicismos.
Responde ÚNICAMENTE con el JSON solicitado.
""";

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> StaticNeighborhoodCatalog =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["MEDELLIN"] =
            [
                "Popular", "Santo Domingo", "Granizal", "Aranjuez", "Manrique", "Prado", "Boston", "La Candelaria",
                "Villahermosa", "Buenos Aires", "Castilla", "Doce de Octubre", "Robledo", "Laureles", "Estadio",
                "La America", "San Javier", "Belen", "Guayabal", "El Poblado", "Los Colores", "San Joaquin",
                "La Floresta", "Santa Monica", "Alfonso Lopez", "La Pradera", "NO PARAMETRIZADO"
            ],
            ["BELLO"] =
            [
                "Niquia", "Cabanas", "Santa Ana", "Perez", "Madera", "Mesa", "Suarez", "Paris", "Trapiche",
                "Navarra", "Bello Horizonte", "Andalucia", "La Cumbre", "Pachelly", "Niquia Camacol",
                "Valadares", "La Gabriela", "Fontidueno", "San Jose Obrero", "NO PARAMETRIZADO"
            ],
            ["ENVIGADO"] =
            [
                "Zona Centro", "El Dorado", "La Magnolia", "San Marcos", "Alcala", "Mesa", "Milan",
                "Las Flores", "Pontevedra", "El Portal", "Loma del Escobero", "Loma del Esmeraldal",
                "Loma de las Brujas", "Loma de los Benedictinos", "El Chocho", "Las Orquideas", "La Mina",
                "Zuniga", "Bosques de Zuniga", "Uribe Angel", "NO PARAMETRIZADO"
            ],
            ["ITAGUI"] =
            [
                "Centro", "Ditaires", "El Carmelo", "San Francisco", "Santa Maria", "La Gloria", "Las Mercedes",
                "Terranova", "Triana", "Samaria", "Fatima", "Yarumito", "El Rosario", "Los Naranjos",
                "Playa Rica", "Simon Bolivar", "San Pio", "Camparola", "Calatrava", "Guayabal",
                "Asturias", "Viviendas del Sur", "El Porvenir", "Ferrara", "La Independencia",
                "Santa Catalina", "NO PARAMETRIZADO"
            ],
            ["SABANETA"] =
            [
                "Centro", "Entreamigos", "San Joaquin", "Prados de Sabaneta", "Vegas de la Dona Maria",
                "Maria Auxiliadora", "La Barquena", "Aliadas del Sur", "Loma Linda", "Los Arias", "NO PARAMETRIZADO"
            ],
            ["LAESTRELLA"] =
            [
                "Centro", "La Ferreria", "Toledo", "La Chinca", "San Agustin", "Bellavista", "Ancon Sur",
                "Suramerica", "Primavera", "NO PARAMETRIZADO"
            ],
            ["AMAGA"] = ["Centro", "Camilo C", "Pueblo Nuevo", "La Ferreria", "Morro Azul", "NO PARAMETRIZADO"],
            ["BARBOSA"] = ["Centro", "Aguas Claras", "El Hatillo", "La Tolda", "Popalito", "NO PARAMETRIZADO"],
            ["CALDAS"] = ["Centro", "La Inmaculada", "Las Margaritas", "Primavera", "La Corrala", "NO PARAMETRIZADO"],
            ["COPACABANA"] = ["Centro", "Machado", "La Misericordia", "Simon Bolivar", "Villa Nueva", "NO PARAMETRIZADO"],
            ["DONMATIAS"] = ["Centro", "Bellavista", "La Torre", "Los Aguacates", "NO PARAMETRIZADO"],
            ["ELCARMENDEVIBORAL"] = ["Centro", "La Alhambra", "Cimarronas", "Campo Alegre", "NO PARAMETRIZADO"],
            ["ELSANTUARIO"] = ["Centro", "Alto del Aire", "La Judea", "Portachuelo", "NO PARAMETRIZADO"],
            ["GIRARDOTA"] = ["Centro", "El Totumo", "San Andres", "Portachuelo", "Naranjal", "NO PARAMETRIZADO"],
            ["GUARNE"] = ["Centro", "San Antonio", "La Inmaculada", "Comfama", "Horizontes", "NO PARAMETRIZADO"],
            ["GUATAPE"] = ["Centro", "El Roble", "La Piedra", "Quebrada Arriba", "NO PARAMETRIZADO"],
            ["LACEJA"] = ["Centro", "Payuco", "Montanas del Tambo", "San Cayetano", "NO PARAMETRIZADO"],
            ["LAUNION"] = ["Centro", "Carlos E Restrepo", "La Almeria", "San Juan", "NO PARAMETRIZADO"],
            ["MARINILLA"] = ["Centro", "Alto de la Virgen", "Belen", "Rosales", "NO PARAMETRIZADO"],
            ["PENOL"] = ["Centro", "Horizonte", "Santa Ines", "El Uvital", "NO PARAMETRIZADO"],
            ["RETIRO"] = ["Centro", "El Portento", "Martin Pescador", "Don Diego", "NO PARAMETRIZADO"],
            ["SANPEDRODELOSMILAGROS"] = ["Centro", "La Union", "Belmira", "Santa Barbara", "NO PARAMETRIZADO"],
            ["SANVICENTEDEFERRER"] = ["Centro", "El Vergel", "Santa Ana", "La Magdalena", "NO PARAMETRIZADO"],
            ["SANTAROSADEOSOS"] = ["Centro", "Altos de la Montana", "La Granja", "Jesus Nazareno", "NO PARAMETRIZADO"],
            ["RIONEGRO"] =
            [
                "Centro", "Porvenir", "La Aldea", "El Hospital", "San Antonio", "Belchite", "Santa Ana",
                "Cuatro Esquinas", "La Convencion", "NO PARAMETRIZADO"
            ],
            ["SANFELIX"] = ["Centro", "NO PARAMETRIZADO"],
            ["NOPARAMETRIZADO"] = ["NO PARAMETRIZADO"]
        };
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiAddressValidationService> _logger;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _neighborhoodCatalog;

    public OpenAiAddressValidationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenAiAddressValidationService> logger,
        IWebHostEnvironment webHostEnvironment)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _neighborhoodCatalog = LoadNeighborhoodCatalog(webHostEnvironment.ContentRootPath, logger);
        _httpClient.BaseAddress = new Uri(OpenAiBaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<AddressValidationResult> ValidateAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        var direccion = address?.Trim();
        if (string.IsNullOrWhiteSpace(direccion))
        {
            return new AddressValidationResult
            {
                Outcome = AddressValidationOutcome.Invalid,
                Message = "Escribe una dirección para validarla."
            };
        }

        if (direccion.Length > MaxLargoDireccion)
        {
            direccion = direccion[..MaxLargoDireccion];
        }

        var clave = ClaveMemoria(direccion);
        if (Memoria.TryGetValue(clave, out var recordado) && recordado.Vence > DateTimeOffset.UtcNow)
        {
            return recordado.Resultado;
        }

        var apiKey = _configuration["OpenAI:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AddressValidationResult
            {
                Outcome = AddressValidationOutcome.Unavailable,
                Message = "La validación de direcciones no está configurada. Marca \"Asumir dirección errada y continuar\" y elige el municipio y el barrio."
            };
        }

        var model = _configuration["OpenAI:Model"]?.Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            model = DefaultModel;
        }

        try
        {
            var respuesta = await PreguntarAsync(apiKey, model, direccion, cancellationToken);
            if (respuesta is null)
            {
                return NoDisponible();
            }

            // El modelo a veces dice "incompleta" a una dirección que sí trae vía y placa (le pasa
            // con detalles largos como "int 201 torre 2"). Si el texto las trae, se le pregunta una
            // vez más diciéndoselo; si insiste, queda su respuesta.
            if (Texto(respuesta.Value, "estado") == "incompleta" && TieneViaYPlaca(direccion))
            {
                var segunda = await PreguntarAsync(apiKey, model,
                    $"{direccion}\n(La vía y la placa están completas; lo que sigue a la placa es detalle.)", cancellationToken);
                if (segunda is not null)
                {
                    respuesta = segunda;
                }
            }

            var resultado = Interpretar(direccion, respuesta.Value);
            Recordar(clave, resultado);
            return resultado;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("La validación de dirección con OpenAI tardó demasiado.");
            return NoDisponible();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando una dirección con OpenAI.");
            return NoDisponible();
        }
    }

    /// <summary>
    /// Barrios del catálogo local del municipio (desde 2026-11 ya no se le preguntaban a Google).
    /// Los barrios que el censo ya tiene guardados los suma el controlador.
    /// </summary>
    public Task<IReadOnlyList<string>> SearchNeighborhoodsAsync(
        string municipality,
        string term,
        CancellationToken cancellationToken = default)
    {
        var municipalityNormalized = municipality?.Trim();
        if (string.IsNullOrWhiteSpace(municipalityNormalized))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        return Task.FromResult(GetStaticNeighborhoods(municipalityNormalized, term?.Trim()));
    }

    /// <summary>Una consulta al modelo: el JSON de su respuesta, o null si no respondió algo útil.</summary>
    private async Task<JsonElement?> PreguntarAsync(string apiKey, string model, string texto, CancellationToken cancellationToken)
    {
        var payload = new
        {
            model,
            temperature = 0,
            max_output_tokens = 800,
            input = new object[]
            {
                new { role = "system", content = Instrucciones },
                new { role = "user", content = texto }
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "direccion_censo",
                    strict = true,
                    schema = EsquemaRespuesta()
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, ResponsesEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "OpenAI respondió {StatusCode} al validar una dirección: {Body}",
                (int)response.StatusCode,
                body.Length > 1000 ? body[..1000] : body);
            return null;
        }

        return LeerRespuesta(body);
    }

    /// <summary>¿El texto trae tipo de vía, número de vía y placa completa (# 33-45)?</summary>
    private static bool TieneViaYPlaca(string direccion)
    {
        var nucleo = TryParseColombianAddressCore(direccion);
        return nucleo is not null
               && !string.IsNullOrEmpty(nucleo.RouteNumber)
               && !string.IsNullOrEmpty(nucleo.PrimaryNumber)
               && !string.IsNullOrEmpty(nucleo.SecondaryNumber);
    }

    private static AddressValidationResult NoDisponible() => new()
    {
        Outcome = AddressValidationOutcome.Unavailable,
        Message = "No fue posible validar la dirección en este momento. Intenta de nuevo o marca \"Asumir dirección errada y continuar\"."
    };

    private JsonElement? LeerRespuesta(string body)
    {
        using var documento = JsonDocument.Parse(body);
        var raiz = documento.RootElement;
        if (raiz.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
        {
            _logger.LogWarning("OpenAI devolvió un error al validar una dirección: {Error}",
                error.TryGetProperty("message", out var m) ? m.GetString() : null);
            return null;
        }

        if (!raiz.TryGetProperty("output", out var salida) || salida.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in salida.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var contenido) || contenido.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var parte in contenido.EnumerateArray())
            {
                if (parte.TryGetProperty("type", out var tipo)
                    && string.Equals(tipo.GetString(), "output_text", StringComparison.Ordinal)
                    && parte.TryGetProperty("text", out var texto)
                    && !string.IsNullOrWhiteSpace(texto.GetString()))
                {
                    // Se clona: el documento se libera al salir de aquí.
                    using var json = JsonDocument.Parse(texto.GetString()!);
                    return json.RootElement.Clone();
                }
            }
        }

        _logger.LogWarning("OpenAI no devolvió texto al validar una dirección.");
        return null;
    }

    private AddressValidationResult Interpretar(string escrita, JsonElement respuesta)
    {
        var estado = Texto(respuesta, "estado") ?? string.Empty;
        var mensaje = Texto(respuesta, "mensaje");
        var candidatos = new List<AddressValidationCandidate>();
        if (respuesta.TryGetProperty("candidatos", out var lista) && lista.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in lista.EnumerateArray())
            {
                var candidato = ArmarCandidato(c, escrita);
                if (candidato is not null
                    && candidatos.All(x => !string.Equals(ClaveMemoria(x.FormattedAddress + x.Municipality), ClaveMemoria(candidato.FormattedAddress + candidato.Municipality), StringComparison.Ordinal)))
                {
                    candidatos.Add(candidato);
                }
            }
        }

        candidatos = candidatos.Take(MaxCandidatos).ToList();

        AddressValidationResult Sin(string porDefecto) => new()
        {
            Outcome = AddressValidationOutcome.Invalid,
            Message = string.IsNullOrWhiteSpace(mensaje) ? porDefecto : mensaje
        };

        switch (estado)
        {
            case "fuera_de_antioquia":
                return Sin("La dirección no es de Antioquia. El censo solo atiende pacientes en Antioquia.");
            case "municipio_no_atendido":
                return Sin("La dirección es de un municipio que el censo no atiende.");
            case "no_es_direccion":
                return Sin("El texto no parece una dirección. Escribe la vía y la placa, por ejemplo CALLE 32EE # 80-39.");
            case "incompleta":
                return Sin("A la dirección le falta información (por ejemplo el número de la placa).");
        }

        if (candidatos.Count == 0)
        {
            return Sin("No se pudo ubicar la dirección. Revísala o marca \"Asumir dirección errada y continuar\".");
        }

        var mejor = candidatos[0];
        // El modelo no puede cambiar los números que escribió la persona: si lo hizo, no se acepta
        // sola, se muestra como sugerencia para que alguien la compare con lo escrito.
        var conservaNumeros = DoesFormattedAddressPreserveInputHouseNumber(escrita, mejor.FormattedAddress);
        if (estado == "encontrada" && candidatos.Count == 1 && mejor.IsReliable && conservaNumeros)
        {
            return Resultado(AddressValidationOutcome.Valid, mejor, candidatos, requiereEleccion: false,
                mensaje: DescribirUbicacion(mejor, "Dirección ubicada"));
        }

        var aviso = estado == "varias" || candidatos.Count > 1
            ? "Puede estar en varios lugares. Elige la correcta:"
            : !conservaNumeros
                ? "La IA cambió números de la dirección. Revisa la sugerencia:"
                : "La IA no está segura. Confirma la sugerencia:";
        return Resultado(AddressValidationOutcome.Doubtful, mejor, candidatos, requiereEleccion: true, mensaje: aviso);
    }

    private AddressValidationCandidate? ArmarCandidato(JsonElement c, string escrita)
    {
        var direccion = Texto(c, "direccion");
        var municipio = Texto(c, "municipio");
        if (string.IsNullOrWhiteSpace(direccion) || string.IsNullOrWhiteSpace(municipio))
        {
            return null;
        }

        return new AddressValidationCandidate
        {
            FormattedAddress = direccion.Trim().ToUpper(CultureInfo.GetCultureInfo("es-CO")),
            Municipality = municipio.Trim(),
            Neighborhood = AjustarBarrio(municipio, Texto(c, "barrio"), escrita),
            District = null,
            Detail = Texto(c, "detalle")?.Trim().ToUpper(CultureInfo.GetCultureInfo("es-CO")),
            Area = string.Equals(Texto(c, "area"), "Rural", StringComparison.OrdinalIgnoreCase) ? "Rural" : "Urbana",
            IsReliable = string.Equals(Texto(c, "confianza"), "alta", StringComparison.OrdinalIgnoreCase)
        };
    }

    /// <summary>
    /// El barrio solo cuenta si la persona lo escribió en la dirección (el modelo a veces lo
    /// deduce aunque se le pida que no). Si está en el catálogo, se usa la forma del catálogo.
    /// </summary>
    private string? AjustarBarrio(string municipio, string? barrio, string escrita)
    {
        if (string.IsNullOrWhiteSpace(barrio))
        {
            return null;
        }

        var limpio = barrio.Trim();
        if (limpio.StartsWith("BARRIO ", StringComparison.OrdinalIgnoreCase))
        {
            limpio = limpio[7..].Trim();
        }

        if (!NormalizeLookup(escrita).Contains(NormalizeLookup(limpio), StringComparison.Ordinal))
        {
            return null;
        }

        if (_neighborhoodCatalog.TryGetValue(NormalizeLookup(municipio), out var opciones))
        {
            var igual = opciones.FirstOrDefault(x => string.Equals(NormalizeLookup(x), NormalizeLookup(limpio), StringComparison.Ordinal));
            if (igual is not null)
            {
                return igual.ToUpper(CultureInfo.GetCultureInfo("es-CO"));
            }
        }

        return limpio.ToUpper(CultureInfo.GetCultureInfo("es-CO"));
    }

    private static string DescribirUbicacion(AddressValidationCandidate c, string inicio)
    {
        var partes = new[] { c.Neighborhood, c.Municipality }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        return partes.Count == 0 ? $"{inicio}." : $"{inicio}: {string.Join(", ", partes)}.";
    }

    private static AddressValidationResult Resultado(
        AddressValidationOutcome outcome,
        AddressValidationCandidate mejor,
        IReadOnlyList<AddressValidationCandidate> candidatos,
        bool requiereEleccion,
        string mensaje) => new()
        {
            Outcome = outcome,
            FormattedAddress = mejor.FormattedAddress,
            SuggestedAddress = mejor.FormattedAddress,
            Municipality = mejor.Municipality,
            Neighborhood = mejor.Neighborhood,
            District = mejor.District,
            Detail = mejor.Detail,
            Area = mejor.Area,
            RequiresSelection = requiereEleccion,
            Candidates = candidatos,
            Message = mensaje
        };

    /// <summary>
    /// Guarda la respuesta con lo escrito y también con cada dirección propuesta: al elegir una
    /// sugerencia (o al guardar la que ya quedó normalizada) el servidor vuelve a preguntar por ese
    /// texto, y debe recibir la misma ubicación sin volver a pagar ni cambiar de opinión.
    /// </summary>
    private static void Recordar(string clave, AddressValidationResult resultado)
    {
        if (Memoria.Count > 5000)
        {
            Memoria.Clear();
        }

        var vence = DateTimeOffset.UtcNow.Add(VigenciaMemoria);
        if (resultado.Outcome == AddressValidationOutcome.Unavailable)
        {
            return;
        }

        Memoria[clave] = (vence, resultado);
        foreach (var mismoTexto in resultado.Candidates.GroupBy(x => ClaveMemoria(x.FormattedAddress)))
        {
            // "CALLE 9 SUR # 50-20" puede ser de Medellín, Envigado o Itagüí: el texto solo no dice
            // cuál eligió la persona, así que se recuerda como válida pero sin municipio ni barrio,
            // para que el guardado no le cambie el municipio que quedó en el formulario.
            // El detalle no va: ese texto ya es la dirección sin el apto, y si se devolviera, el
            // guardado de un programa lo pegaría otra vez al final de la dirección.
            var primero = mismoTexto.First();
            var ambigua = mismoTexto.Count() > 1;
            var candidato = new AddressValidationCandidate
            {
                FormattedAddress = primero.FormattedAddress,
                Municipality = ambigua ? null : primero.Municipality,
                Neighborhood = ambigua ? null : primero.Neighborhood,
                Area = primero.Area,
                IsReliable = true
            };

            var elegida = Resultado(AddressValidationOutcome.Valid, candidato, [candidato], requiereEleccion: false,
                mensaje: DescribirUbicacion(candidato, "Dirección confirmada"));
            Memoria[mismoTexto.Key] = (vence, elegida);
        }
    }

    private static string ClaveMemoria(string? texto) =>
        new string(RemoveDiacritics(texto ?? string.Empty).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string? Texto(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static object EsquemaRespuesta()
    {
        object Anulable(string tipo) => new { type = new[] { tipo, "null" } };
        var candidato = new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "direccion", "detalle", "municipio", "barrio", "area", "confianza" },
            properties = new Dictionary<string, object>
            {
                ["direccion"] = new { type = "string", description = "Vía y placa normalizadas, en mayúsculas, sin municipio ni barrio." },
                ["detalle"] = Anulable("string"),
                ["municipio"] = new { type = "string", @enum = MunicipiosAtendidos },
                ["barrio"] = Anulable("string"),
                ["area"] = new { type = "string", @enum = new[] { "Urbana", "Rural" } },
                ["confianza"] = new { type = "string", @enum = new[] { "alta", "media", "baja" } }
            }
        };

        return new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "estado", "mensaje", "candidatos" },
            properties = new Dictionary<string, object>
            {
                ["estado"] = new
                {
                    type = "string",
                    @enum = new[] { "encontrada", "varias", "incompleta", "fuera_de_antioquia", "municipio_no_atendido", "no_es_direccion" }
                },
                ["mensaje"] = new { type = "string" },
                ["candidatos"] = new { type = "array", items = candidato }
            }
        };
    }

    private static bool DoesFormattedAddressPreserveInputHouseNumber(string originalAddress, string formattedAddress)
    {
        var originalCore = TryParseColombianAddressCore(originalAddress);
        if (originalCore is null)
        {
            return true;
        }

        var formattedCore = TryParseColombianAddressCore(formattedAddress);
        if (formattedCore is null)
        {
            return false;
        }

        return AddressPartMatches(originalCore.RouteNumber, formattedCore.RouteNumber)
               && AddressPartMatches(originalCore.PrimaryNumber, formattedCore.PrimaryNumber)
               && AddressPartMatches(originalCore.SecondaryNumber, formattedCore.SecondaryNumber);
    }

    private static ColombianAddressCore? TryParseColombianAddressCore(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = RemoveDiacritics(value).ToUpperInvariant();
        var routeMatch = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"\b(?:CARRERA|CRA|KR|KRA|CALLE|CLL|CL|AVENIDA|AV|TRANSVERSAL|TV|DIAGONAL|DG|CIRCULAR|CIR)\.?\s*(\d+[A-Z]?)\b",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var plateMatch = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"#\s*(\d+[A-Z]?)\s*-?\s*(\d+[A-Z]?)",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        if (!routeMatch.Success && !plateMatch.Success)
        {
            return null;
        }

        return new ColombianAddressCore(
            routeMatch.Success ? NormalizeAddressPart(routeMatch.Groups[1].Value) : null,
            plateMatch.Success ? NormalizeAddressPart(plateMatch.Groups[1].Value) : null,
            plateMatch.Success ? NormalizeAddressPart(plateMatch.Groups[2].Value) : null);
    }

    private static bool AddressPartMatches(string? expected, string? actual)
    {
        return string.IsNullOrWhiteSpace(expected)
               || (!string.IsNullOrWhiteSpace(actual)
                   && string.Equals(expected, actual, StringComparison.Ordinal));
    }

    private static string NormalizeAddressPart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = RemoveDiacritics(value).ToUpperInvariant();
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        var result = builder.ToString().TrimStart('0');
        return string.IsNullOrWhiteSpace(result) ? "0" : result;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed record ColombianAddressCore(string? RouteNumber, string? PrimaryNumber, string? SecondaryNumber);

    private IReadOnlyList<string> GetStaticNeighborhoods(string municipality, string? term)
    {
        var key = NormalizeLookup(municipality);
        if (!_neighborhoodCatalog.TryGetValue(key, out var options) || options.Count == 0)
        {
            return [];
        }

        var termKey = NormalizeLookup(term);
        if (string.IsNullOrWhiteSpace(termKey) || termKey.Length <= 1)
        {
            return options
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }

        var filtered = options
            .Where(x => NormalizeLookup(x).Contains(termKey, StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        if (filtered.Count > 0)
        {
            return filtered;
        }

        return options
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> LoadNeighborhoodCatalog(
        string contentRootPath,
        ILogger<OpenAiAddressValidationService> logger)
    {
        try
        {
            var catalogPath = Path.Combine(contentRootPath, "Data", "Seed", "neighborhood_catalog.json");
            if (!File.Exists(catalogPath))
            {
                logger.LogWarning(
                    "No se encontro el catalogo local de barrios en {CatalogPath}. Se usa el catalogo interno.",
                    catalogPath);
                return StaticNeighborhoodCatalog;
            }

            using var stream = File.OpenRead(catalogPath);
            var raw = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(stream);
            if (raw is null || raw.Count == 0)
            {
                return StaticNeighborhoodCatalog;
            }

            var normalized = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var (municipalityKey, values) in raw)
            {
                var normalizedMunicipality = NormalizeLookup(municipalityKey);
                if (string.IsNullOrWhiteSpace(normalizedMunicipality))
                {
                    continue;
                }

                var items = (values ?? [])
                    .Select(x => x?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .Cast<string>()
                    .ToList();

                if (items.Count == 0)
                {
                    continue;
                }

                normalized[normalizedMunicipality] = items;
            }

            return normalized.Count == 0 ? StaticNeighborhoodCatalog : normalized;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cargando catalogo local de barrios. Se usa el catalogo interno.");
            return StaticNeighborhoodCatalog;
        }
    }

    private static string NormalizeLookup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return RemoveDiacritics(value)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }

}
