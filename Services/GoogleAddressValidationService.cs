using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using IntranetPrueba.Services.Interfaces;
using IntranetPrueba.Services.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IntranetPrueba.Services;

public class GoogleAddressValidationService : IAddressValidationService
{
    private const string GeocodingBaseUrl = "https://maps.googleapis.com/maps/api/geocode/";
    private const string AntioquiaComponents = "administrative_area:Antioquia|country:CO";
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
    private readonly ILogger<GoogleAddressValidationService> _logger;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _neighborhoodCatalog;

    public GoogleAddressValidationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GoogleAddressValidationService> logger,
        IWebHostEnvironment webHostEnvironment)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _neighborhoodCatalog = LoadNeighborhoodCatalog(webHostEnvironment.ContentRootPath, logger);
        _httpClient.BaseAddress = new Uri(GeocodingBaseUrl);
    }

    public async Task<AddressValidationResult> ValidateAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        var normalizedAddress = address?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return new AddressValidationResult
            {
                Outcome = AddressValidationOutcome.Invalid,
                Message = "Debes escribir una direccion para validarla."
            };
        }

        var apiKey = _configuration["GoogleMaps:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AddressValidationResult
            {
                Outcome = AddressValidationOutcome.Unavailable,
                Message = "No se pudo validar la direccion porque no hay una ApiKey de Google configurada."
            };
        }

        try
        {
            var requestUri =
                $"json?address={Uri.EscapeDataString(normalizedAddress)}" +
                $"&components={Uri.EscapeDataString(AntioquiaComponents)}" +
                $"&key={Uri.EscapeDataString(apiKey)}&language=es&region=co";
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new AddressValidationResult
                {
                    Outcome = AddressValidationOutcome.Unavailable,
                    Message = "No fue posible validar la direccion en este momento. Intenta nuevamente."
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var status = root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString()
                : null;

            if (string.Equals(status, "ZERO_RESULTS", StringComparison.OrdinalIgnoreCase))
            {
                return new AddressValidationResult
                {
                    Outcome = AddressValidationOutcome.Invalid,
                    Message = "La direccion no se encontro en Google Maps."
                };
            }

            if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                return new AddressValidationResult
                {
                    Outcome = AddressValidationOutcome.Unavailable,
                    Message = $"Google no pudo validar la direccion (estado: {status ?? "desconocido"})."
                };
            }

            if (!root.TryGetProperty("results", out var resultsElement) || resultsElement.GetArrayLength() == 0)
            {
                return new AddressValidationResult
                {
                    Outcome = AddressValidationOutcome.Invalid,
                    Message = "No se encontro una coincidencia valida para la direccion."
                };
            }

            var candidates = BuildAddressCandidates(resultsElement);
            if (candidates.Count == 0)
            {
                return new AddressValidationResult
                {
                    Outcome = AddressValidationOutcome.Invalid,
                    Message = "La direccion no pertenece a Antioquia, Colombia. Solo se permiten direcciones en Antioquia."
                };
            }

            var bestCandidate = candidates[0];
            if (candidates.Count > 1)
            {
                return new AddressValidationResult
                {
                    Outcome = AddressValidationOutcome.Doubtful,
                    FormattedAddress = bestCandidate.FormattedAddress,
                    SuggestedAddress = bestCandidate.FormattedAddress,
                    Municipality = bestCandidate.Municipality,
                    Neighborhood = bestCandidate.Neighborhood,
                    District = bestCandidate.District,
                    RequiresSelection = true,
                    Candidates = candidates,
                    Message = "Se encontraron varias direcciones coincidentes. Selecciona la direccion correcta."
                };
            }

            if (bestCandidate.IsReliable)
            {
                return new AddressValidationResult
                {
                    Outcome = AddressValidationOutcome.Valid,
                    FormattedAddress = bestCandidate.FormattedAddress,
                    SuggestedAddress = bestCandidate.FormattedAddress,
                    Municipality = bestCandidate.Municipality,
                    Neighborhood = bestCandidate.Neighborhood,
                    District = bestCandidate.District,
                    Candidates = candidates,
                    Message = "Direccion validada correctamente."
                };
            }

            return new AddressValidationResult
            {
                Outcome = AddressValidationOutcome.Doubtful,
                FormattedAddress = bestCandidate.FormattedAddress,
                SuggestedAddress = bestCandidate.FormattedAddress,
                Municipality = bestCandidate.Municipality,
                Neighborhood = bestCandidate.Neighborhood,
                District = bestCandidate.District,
                Candidates = candidates,
                Message = "La direccion parece incompleta o dudosa. Revisa la sugerencia."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando direccion con Google.");
            return new AddressValidationResult
            {
                Outcome = AddressValidationOutcome.Unavailable,
                Message = "No fue posible validar la direccion por un error de conexion."
            };
        }
    }

    private static IReadOnlyList<AddressValidationCandidate> BuildAddressCandidates(JsonElement resultsElement)
    {
        var candidates = new List<AddressValidationCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in resultsElement.EnumerateArray())
        {
            if (!IsAntioquiaColombiaResult(result))
            {
                continue;
            }

            var formattedAddress = result.TryGetProperty("formatted_address", out var formattedAddressElement)
                ? formattedAddressElement.GetString()?.Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(formattedAddress))
            {
                continue;
            }

            var normalizedAddress = NormalizeLookup(formattedAddress);
            if (!seen.Add(normalizedAddress))
            {
                continue;
            }

            var municipality = GetAddressComponent(result, "locality")
                               ?? GetAddressComponent(result, "administrative_area_level_2");
            var district = GetAddressComponent(result, "sublocality_level_1")
                           ?? GetAddressComponent(result, "sublocality");
            var neighborhood = GetAddressComponent(result, "neighborhood")
                               ?? GetAddressComponent(result, "sublocality_level_2")
                               ?? district;

            var partialMatch = result.TryGetProperty("partial_match", out var partialMatchElement)
                && partialMatchElement.ValueKind == JsonValueKind.True;

            var locationType = result.TryGetProperty("geometry", out var geometryElement)
                               && geometryElement.TryGetProperty("location_type", out var locationTypeElement)
                ? locationTypeElement.GetString()
                : string.Empty;

            candidates.Add(new AddressValidationCandidate
            {
                FormattedAddress = formattedAddress,
                Municipality = municipality,
                Neighborhood = neighborhood,
                District = district,
                IsReliable = !partialMatch && IsReliableLocationType(locationType)
            });
        }

        return candidates
            .Take(8)
            .ToList();
    }

    private static bool IsReliableLocationType(string? locationType)
    {
        return string.Equals(locationType, "ROOFTOP", StringComparison.OrdinalIgnoreCase)
               || string.Equals(locationType, "RANGE_INTERPOLATED", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<string>> SearchNeighborhoodsAsync(
        string municipality,
        string term,
        CancellationToken cancellationToken = default)
    {
        var municipalityNormalized = municipality?.Trim();
        var termNormalized = term?.Trim();
        if (string.IsNullOrWhiteSpace(municipalityNormalized))
        {
            return [];
        }

        var staticNeighborhoods = GetStaticNeighborhoods(municipalityNormalized, termNormalized);
        var shouldReturnStaticOnly = !string.IsNullOrWhiteSpace(termNormalized) && termNormalized.Length <= 1;
        if (staticNeighborhoods.Count > 0 && shouldReturnStaticOnly)
        {
            return staticNeighborhoods;
        }

        var apiKey = _configuration["GoogleMaps:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return staticNeighborhoods;
        }

        try
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queries = BuildNeighborhoodQueries(municipalityNormalized, termNormalized);
            foreach (var query in queries)
            {
                var requestUri =
                    $"json?address={Uri.EscapeDataString(query)}" +
                    $"&components={Uri.EscapeDataString(AntioquiaComponents)}" +
                    $"&key={Uri.EscapeDataString(apiKey)}&language=es&region=co";

                using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = document.RootElement;
                if (!root.TryGetProperty("status", out var statusElement)
                    || !string.Equals(statusElement.GetString(), "OK", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!root.TryGetProperty("results", out var resultsElement))
                {
                    continue;
                }

                foreach (var result in resultsElement.EnumerateArray())
                {
                    if (!IsAntioquiaColombiaResult(result) || !IsMunicipalityMatch(result, municipalityNormalized))
                    {
                        continue;
                    }

                    var neighborhood = GetAddressComponent(result, "sublocality_level_1")
                                       ?? GetAddressComponent(result, "neighborhood")
                                       ?? GetAddressComponent(result, "sublocality");

                    if (string.IsNullOrWhiteSpace(neighborhood))
                    {
                        continue;
                    }

                    if (string.Equals(
                        NormalizeLookup(neighborhood),
                        NormalizeLookup(municipalityNormalized),
                        StringComparison.Ordinal))
                    {
                        continue;
                    }

                    set.Add(neighborhood.Trim());
                }
            }

            if (set.Count == 0)
            {
                return staticNeighborhoods;
            }

            if (staticNeighborhoods.Count == 0)
            {
                return set.OrderBy(x => x).Take(60).ToList();
            }

            return staticNeighborhoods
                .Concat(set)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .Take(80)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando barrios por municipio en Google.");
            return staticNeighborhoods;
        }
    }

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
        ILogger<GoogleAddressValidationService> logger)
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

    private static IReadOnlyList<string> BuildNeighborhoodQueries(string municipality, string? term)
    {
        var normalizedTerm = term?.Trim() ?? string.Empty;
        var broadQuery = normalizedTerm.Length <= 1;

        if (broadQuery)
        {
            return
            [
                $"barrios {municipality}, Antioquia, Colombia",
                $"barrios en {municipality}, Antioquia, Colombia",
                $"{municipality}, Antioquia, Colombia"
            ];
        }

        return
        [
            $"{normalizedTerm}, {municipality}, Antioquia, Colombia",
            $"barrio {normalizedTerm}, {municipality}, Antioquia, Colombia",
            $"barrios {municipality}, Antioquia, Colombia"
        ];
    }

    private static bool IsAntioquiaColombiaResult(JsonElement result)
    {
        var department = GetAddressComponent(result, "administrative_area_level_1");
        var countryShort = GetAddressComponent(result, "country", useShortName: true);
        var countryLong = GetAddressComponent(result, "country");

        var normalizedDepartment = NormalizeLookup(department);
        var normalizedCountryShort = NormalizeLookup(countryShort);
        var normalizedCountryLong = NormalizeLookup(countryLong);

        var isAntioquia = normalizedDepartment.Contains("ANTIOQUIA", StringComparison.Ordinal);
        var isColombia = string.Equals(normalizedCountryShort, "CO", StringComparison.Ordinal)
                         || normalizedCountryLong.Contains("COLOMBIA", StringComparison.Ordinal);

        return isAntioquia && isColombia;
    }

    private static bool IsMunicipalityMatch(JsonElement result, string municipality)
    {
        var municipalityLookup = NormalizeLookup(municipality);
        if (string.IsNullOrWhiteSpace(municipalityLookup))
        {
            return false;
        }

        var candidates = new[]
        {
            GetAddressComponent(result, "locality"),
            GetAddressComponent(result, "administrative_area_level_2"),
            GetAddressComponent(result, "administrative_area_level_3"),
            GetAddressComponent(result, "sublocality_level_1"),
            GetAddressComponent(result, "sublocality")
        };

        foreach (var candidate in candidates)
        {
            if (string.Equals(NormalizeLookup(candidate), municipalityLookup, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (result.TryGetProperty("formatted_address", out var formattedAddressElement))
        {
            var formattedAddress = formattedAddressElement.GetString();
            if (NormalizeLookup(formattedAddress).Contains(municipalityLookup, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeLookup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }

    private static string? GetAddressComponent(JsonElement result, string componentType, bool useShortName = false)
    {
        if (!result.TryGetProperty("address_components", out var componentsElement))
        {
            return null;
        }

        foreach (var component in componentsElement.EnumerateArray())
        {
            if (!component.TryGetProperty("types", out var typesElement))
            {
                continue;
            }

            var hasType = false;
            foreach (var type in typesElement.EnumerateArray())
            {
                if (string.Equals(type.GetString(), componentType, StringComparison.OrdinalIgnoreCase))
                {
                    hasType = true;
                    break;
                }
            }

            if (!hasType)
            {
                continue;
            }

            var primaryProperty = useShortName ? "short_name" : "long_name";
            if (component.TryGetProperty(primaryProperty, out var primaryValue))
            {
                var value = primaryValue.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            var fallbackProperty = useShortName ? "long_name" : "short_name";
            if (component.TryGetProperty(fallbackProperty, out var fallbackValue))
            {
                var value = fallbackValue.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }
}
