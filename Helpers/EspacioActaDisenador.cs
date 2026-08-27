using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Nexa.Data.Entities;
using Nexa.Models.EspacioCorporativo;

namespace Nexa.Helpers;

/// <summary>
/// Puerta de entrada de las plantillas que arma un administrador.
///
/// Todo lo que llega del diseñador pasa por <see cref="Normalizar"/>: ahí se
/// recortan longitudes, se generan las claves, se verifica que cada marcador
/// escrito en el pliego exista y se comprueba que las firmas apunten a campos
/// reales. Si algo no cuadra se devuelve la lista de errores y no se guarda nada.
/// </summary>
public static partial class EspacioActaDisenador
{
    public const int MaximoCampos = 40;
    public const int MaximoBloques = 60;
    public const int MaximoFirmas = 6;
    public const int MaximoTextoBloque = 4000;

    /// <summary>
    /// Un solo formato para la definición: la que viaja al navegador, la que se guarda
    /// en las columnas JSON y la que vuelve del diseñador. Los enums viajan por nombre
    /// para que la definición siga siendo legible dentro de la base de datos.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [GeneratedRegex(@"^[a-z][a-z0-9_]{0,39}$", RegexOptions.Compiled)]
    private static partial Regex ClavePattern { get; }

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NoAlfanumericoPattern { get; }

    public sealed record Resultado(EspacioActaPlantilla? Plantilla, IReadOnlyList<string> Errores)
    {
        public bool EsValida => Plantilla is not null && Errores.Count == 0;
    }

    /// <summary>
    /// Qué tan exigente es la validación.
    ///
    /// Un borrador se guarda a medio hacer: quien arma el acta puede parar y volver
    /// mañana. Solo al publicarla se exige que esté completa, porque desde ese momento
    /// cualquiera puede emitir actas con ella.
    /// </summary>
    public enum ModoDefinicion
    {
        Borrador,
        Publicacion
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Normalización y validación
    // ─────────────────────────────────────────────────────────────────────────

    public static Resultado Normalizar(
        EspacioActaDefinicionDto? dto,
        string? codigoExistente = null,
        ModoDefinicion modo = ModoDefinicion.Publicacion)
    {
        var errores = new List<string>();
        var publicando = modo == ModoDefinicion.Publicacion;

        if (dto is null)
        {
            return new Resultado(null, ["No se recibió la definición de la plantilla."]);
        }

        var nombre = Recortar(dto.Nombre, 200);
        if (string.IsNullOrWhiteSpace(nombre))
        {
            errores.Add("Ponle un nombre al acta para poder guardarla.");
        }

        var tituloActa = Recortar(dto.TituloActa, 200);
        if (publicando && string.IsNullOrWhiteSpace(tituloActa))
        {
            errores.Add("Escribe el título que va en el encabezado del documento.");
        }

        var campos = NormalizarCampos(dto.Campos, errores, publicando);
        var bloques = NormalizarBloques(dto.Bloques, campos, errores, publicando);
        var firmas = NormalizarFirmas(dto.Firmas, campos, errores, publicando);

        // Marcadores que quedaron apuntando a un dato borrado: al renderizar saldría un
        // hueco en el acta sin avisar. En borrador se toleran porque aún está a medias.
        if (publicando)
        {
            var desconocidos = EspacioActaRenderer.MarcadoresDesconocidos(bloques, campos);
            if (desconocidos.Count > 0)
            {
                errores.Add(
                    "En el texto quedaron datos que ya no existen. Bórralos del acta y vuelve a insertarlos.");
            }
        }

        // Quién es "la persona del acta" casi siempre se puede deducir: de la firma que
        // se traza en el momento, o del primer dato de texto. Solo se pregunta si no hay
        // por dónde deducirlo.
        var campoNombre = ResolverEnlace(dto.CampoNombre, campos)
            ?? firmas.FirstOrDefault(f => f.Origen == EspacioActaFirmaOrigen.EnVivo && f.CampoNombre is not null)?.CampoNombre
            ?? campos.FirstOrDefault(c => c.Tipo is EspacioActaTipoCampo.Texto)?.Clave;

        if (publicando && campoNombre is null)
        {
            errores.Add("Agrega un dato de tipo Texto con el nombre de la persona del acta.");
        }

        var campoCorreo = ResolverEnlace(dto.CampoCorreo, campos);
        if (campoCorreo is not null
            && campos.First(x => x.Clave == campoCorreo).Tipo != EspacioActaTipoCampo.Correo)
        {
            // No es motivo para frenar a nadie: simplemente no se envía copia.
            campoCorreo = null;
        }

        campoCorreo ??= campos.FirstOrDefault(c => c.Tipo == EspacioActaTipoCampo.Correo)?.Clave;

        // El documento alimenta la columna por la que se busca un acta después.
        // Se deduce igual que los demás enlaces para no preguntarlo.
        var campoDocumento = ResolverEnlace(dto.CampoDocumento, campos)
            ?? firmas.FirstOrDefault(f => f.CampoDocumento is not null)?.CampoDocumento
            ?? campos.FirstOrDefault(c => c.Tipo == EspacioActaTipoCampo.Documento)?.Clave;

        if (errores.Count > 0)
        {
            return new Resultado(null, errores);
        }

        var plantilla = new EspacioActaPlantilla
        {
            Codigo = codigoExistente ?? GenerarCodigo(nombre),
            Nombre = nombre!,
            Descripcion = Recortar(dto.Descripcion, 400) ?? string.Empty,
            Icono = EspacioActaPlantillas.EsIconoValido(dto.Icono)
                ? dto.Icono!
                : "bi-file-earmark-text-fill",
            TituloActa = tituloActa ?? string.Empty,
            Campos = campos,
            Bloques = bloques,
            Firmas = firmas,
            NumerarTitulos = dto.NumerarTitulos,
            CampoNombre = campoNombre ?? string.Empty,
            CampoDocumento = campoDocumento,
            CampoCorreo = campoCorreo,
            CampoUsuario = ResolverEnlace(dto.CampoUsuario, campos),
            RotuloRecibe = firmas
                .FirstOrDefault(f => f.Origen == EspacioActaFirmaOrigen.EnVivo)?.Rotulo ?? "Recibe"
        };

        return new Resultado(plantilla, []);
    }

    private static List<EspacioActaCampo> NormalizarCampos(
        IReadOnlyList<EspacioActaDefinicionDto.CampoDto> entrada,
        List<string> errores,
        bool publicando)
    {
        var campos = new List<EspacioActaCampo>();
        var clavesUsadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (entrada.Count == 0)
        {
            if (publicando)
            {
                errores.Add("Agrega al menos un dato que se llene al emitir el acta.");
            }

            return campos;
        }

        if (entrada.Count > MaximoCampos)
        {
            errores.Add($"Una plantilla admite hasta {MaximoCampos} campos.");
            return campos;
        }

        foreach (var dto in entrada)
        {
            var etiqueta = Recortar(dto.Etiqueta, 120);
            if (string.IsNullOrWhiteSpace(etiqueta))
            {
                if (publicando)
                {
                    errores.Add("Hay un dato sin nombre. Ponle uno o quítalo.");
                }

                continue;
            }

            if (!Enum.TryParse<EspacioActaTipoCampo>(dto.Tipo, ignoreCase: true, out var tipo))
            {
                errores.Add($"El campo '{etiqueta}' tiene un tipo que no se reconoce.");
                continue;
            }

            var clave = Recortar(dto.Clave, 40)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(clave) || !ClavePattern.IsMatch(clave))
            {
                clave = GenerarClave(etiqueta, clavesUsadas);
            }

            if (!clavesUsadas.Add(clave))
            {
                clave = GenerarClave(clave, clavesUsadas);
                clavesUsadas.Add(clave);
            }

            var opciones = new List<EspacioActaOpcion>();
            if (tipo == EspacioActaTipoCampo.Seleccion)
            {
                foreach (var opcion in dto.Opciones.Take(30))
                {
                    var textoOpcion = Recortar(opcion.Etiqueta, 120) ?? Recortar(opcion.Valor, 120);
                    if (string.IsNullOrWhiteSpace(textoOpcion))
                    {
                        continue;
                    }

                    opciones.Add(new EspacioActaOpcion(
                        Recortar(opcion.Valor, 120) ?? textoOpcion,
                        textoOpcion));
                }

                if (opciones.Count == 0)
                {
                    if (publicando)
                    {
                        errores.Add($"La lista '{etiqueta}' necesita al menos una opción.");
                    }

                    continue;
                }
            }

            campos.Add(new EspacioActaCampo
            {
                Clave = clave,
                Etiqueta = etiqueta!,
                Tipo = tipo,
                Requerido = dto.Requerido,
                Placeholder = Recortar(dto.Placeholder, 120),
                Ayuda = Recortar(dto.Ayuda, 200),
                VisibleEnActa = dto.VisibleEnActa,
                Opciones = opciones,
                MaxLength = LongitudPorTipo(tipo)
            });
        }

        return campos;
    }

    private static List<EspacioActaBloque> NormalizarBloques(
        IReadOnlyList<EspacioActaDefinicionDto.BloqueDto> entrada,
        IReadOnlyList<EspacioActaCampo> campos,
        List<string> errores,
        bool publicando)
    {
        var bloques = new List<EspacioActaBloque>();

        if (entrada.Count > MaximoBloques)
        {
            errores.Add($"El pliego admite hasta {MaximoBloques} bloques.");
            return bloques;
        }

        foreach (var dto in entrada)
        {
            if (!Enum.TryParse<EspacioActaTipoBloque>(dto.Tipo, ignoreCase: true, out var tipo))
            {
                errores.Add("Hay un bloque del pliego con un tipo que no se reconoce.");
                continue;
            }

            var texto = Recortar(dto.Texto, MaximoTextoBloque) ?? string.Empty;

            if (tipo == EspacioActaTipoBloque.Datos)
            {
                var claves = dto.Campos
                    .Select(clave => campos.FirstOrDefault(c =>
                        string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase))?.Clave)
                    .Where(clave => clave is not null)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(clave => clave!)
                    .ToList();

                if (claves.Count == 0)
                {
                    // Un cuadro sin datos no imprime nada: se descarta en vez de frenar.
                    continue;
                }

                bloques.Add(new EspacioActaBloque { Tipo = tipo, Campos = claves });
                continue;
            }

            if (tipo != EspacioActaTipoBloque.Separador && string.IsNullOrWhiteSpace(texto))
            {
                // Un bloque vacío no es un error: se descarta al guardar.
                continue;
            }

            bloques.Add(new EspacioActaBloque { Tipo = tipo, Texto = texto });
        }

        if (publicando && bloques.Count == 0)
        {
            errores.Add("El acta está en blanco. Escribe al menos un párrafo.");
        }

        return bloques;
    }

    private static List<EspacioActaFirma> NormalizarFirmas(
        IReadOnlyList<EspacioActaDefinicionDto.FirmaDto> entrada,
        IReadOnlyList<EspacioActaCampo> campos,
        List<string> errores,
        bool publicando)
    {
        var firmas = new List<EspacioActaFirma>();
        var clavesUsadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (entrada.Count == 0)
        {
            if (publicando)
            {
                errores.Add("Un acta necesita al menos una firma.");
            }

            return firmas;
        }

        if (entrada.Count > MaximoFirmas)
        {
            errores.Add($"Un acta admite hasta {MaximoFirmas} firmas.");
            return firmas;
        }

        foreach (var dto in entrada)
        {
            var rotulo = Recortar(dto.Rotulo, 80);
            if (string.IsNullOrWhiteSpace(rotulo))
            {
                if (publicando)
                {
                    errores.Add("Hay una firma sin rótulo. Escribe qué dice debajo de la raya.");
                }

                continue;
            }

            var origen = Enum.TryParse<EspacioActaFirmaOrigen>(dto.Origen, ignoreCase: true, out var parseado)
                ? parseado
                : EspacioActaFirmaOrigen.EnVivo;

            var clave = Recortar(dto.Clave, 40)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(clave) || !ClavePattern.IsMatch(clave))
            {
                clave = GenerarClave(rotulo!, clavesUsadas);
            }

            if (!clavesUsadas.Add(clave))
            {
                clave = GenerarClave(clave, clavesUsadas);
                clavesUsadas.Add(clave);
            }

            var campoNombre = ResolverEnlace(dto.CampoNombre, campos);
            var nombreFijo = Recortar(dto.NombreFijo, 160);

            if (publicando && origen == EspacioActaFirmaOrigen.EnVivo && campoNombre is null && nombreFijo is null)
            {
                errores.Add(
                    $"La firma '{rotulo}' no sabe a nombre de quién va: elige un dato o escribe el nombre.");
                continue;
            }

            firmas.Add(new EspacioActaFirma
            {
                Clave = clave,
                Rotulo = rotulo!,
                Origen = origen,
                CampoNombre = campoNombre,
                CampoDocumento = ResolverEnlace(dto.CampoDocumento, campos),
                NombreFijo = nombreFijo,
                CargoFijo = Recortar(dto.CargoFijo, 120),
                Requerida = dto.Requerida
            });
        }

        if (publicando && firmas.Count > 0 && firmas.All(x => x.Origen == EspacioActaFirmaOrigen.Emisor))
        {
            errores.Add("Agrega al menos una firma que trace la otra persona en el momento.");
        }

        return firmas;
    }

    /// <summary>Devuelve la clave real del campo referenciado, o nulo si no existe.</summary>
    private static string? ResolverEnlace(string? clave, IReadOnlyList<EspacioActaCampo> campos) =>
        string.IsNullOrWhiteSpace(clave)
            ? null
            : campos.FirstOrDefault(x => string.Equals(x.Clave, clave.Trim(), StringComparison.OrdinalIgnoreCase))?.Clave;

    // ─────────────────────────────────────────────────────────────────────────
    // Entidad ↔ dominio
    // ─────────────────────────────────────────────────────────────────────────

    public static EspacioActaPlantilla ADominio(EspacioActaPlantillaPersonalizada entidad) =>
        new()
        {
            Id = entidad.Id,
            Codigo = entidad.Codigo,
            Nombre = entidad.Nombre,
            Descripcion = entidad.Descripcion,
            Icono = entidad.Icono,
            TituloActa = entidad.TituloActa,
            Campos = Deserializar<EspacioActaCampo>(entidad.CamposJson),
            Bloques = Deserializar<EspacioActaBloque>(entidad.BloquesJson),
            Firmas = Deserializar<EspacioActaFirma>(entidad.FirmasJson),
            NumerarTitulos = entidad.NumerarTitulos,
            CampoNombre = entidad.CampoNombre,
            CampoDocumento = entidad.CampoDocumento,
            CampoCorreo = entidad.CampoCorreo,
            CampoUsuario = entidad.CampoUsuario,
            Activa = entidad.Activa,
            CreadaPorNombre = entidad.CreadaPorNombre,
            ActualizadaAtUtc = entidad.ActualizadaAtUtc ?? entidad.CreadaAtUtc
        };

    public static void Volcar(EspacioActaPlantilla plantilla, EspacioActaPlantillaPersonalizada entidad)
    {
        entidad.Nombre = plantilla.Nombre;
        entidad.Descripcion = plantilla.Descripcion;
        entidad.Icono = plantilla.Icono;
        entidad.TituloActa = plantilla.TituloActa;
        entidad.CamposJson = JsonSerializer.Serialize(plantilla.Campos, JsonOptions);
        entidad.BloquesJson = JsonSerializer.Serialize(plantilla.Bloques, JsonOptions);
        entidad.FirmasJson = JsonSerializer.Serialize(plantilla.Firmas, JsonOptions);
        entidad.NumerarTitulos = plantilla.NumerarTitulos;
        entidad.CampoNombre = plantilla.CampoNombre;
        entidad.CampoDocumento = plantilla.CampoDocumento;
        entidad.CampoCorreo = plantilla.CampoCorreo;
        entidad.CampoUsuario = plantilla.CampoUsuario;
    }

    /// <summary>Definición lista para rehidratar el diseñador al editar una plantilla.</summary>
    public static EspacioActaDefinicionDto ADto(EspacioActaPlantilla plantilla) =>
        new()
        {
            Id = plantilla.Id,
            Nombre = plantilla.Nombre,
            Descripcion = plantilla.Descripcion,
            Icono = plantilla.Icono,
            TituloActa = plantilla.TituloActa,
            NumerarTitulos = plantilla.NumerarTitulos,
            CampoNombre = plantilla.CampoNombre,
            CampoDocumento = plantilla.CampoDocumento,
            CampoCorreo = plantilla.CampoCorreo,
            CampoUsuario = plantilla.CampoUsuario,
            Campos = plantilla.Campos
                .Select(campo => new EspacioActaDefinicionDto.CampoDto
                {
                    Clave = campo.Clave,
                    Etiqueta = campo.Etiqueta,
                    Tipo = campo.Tipo.ToString(),
                    Requerido = campo.Requerido,
                    Placeholder = campo.Placeholder,
                    Ayuda = campo.Ayuda,
                    VisibleEnActa = campo.VisibleEnActa,
                    Opciones = campo.Opciones
                        .Select(opcion => new EspacioActaDefinicionDto.OpcionDto
                        {
                            Valor = opcion.Valor,
                            Etiqueta = opcion.Etiqueta
                        })
                        .ToList()
                })
                .ToList(),
            Bloques = plantilla.Bloques
                .Select(bloque => new EspacioActaDefinicionDto.BloqueDto
                {
                    Tipo = bloque.Tipo.ToString(),
                    Texto = bloque.Texto,
                    Campos = bloque.Campos.ToList()
                })
                .ToList(),
            Firmas = plantilla.FirmasEfectivas
                .Select(firma => new EspacioActaDefinicionDto.FirmaDto
                {
                    Clave = firma.Clave,
                    Rotulo = firma.Rotulo,
                    Origen = firma.Origen.ToString(),
                    CampoNombre = firma.CampoNombre,
                    CampoDocumento = firma.CampoDocumento,
                    NombreFijo = firma.NombreFijo,
                    CargoFijo = firma.CargoFijo,
                    Requerida = firma.Requerida
                })
                .ToList()
        };

    private static List<T> Deserializar<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Claves y códigos
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Convierte un rótulo en una clave usable dentro de {{...}}.</summary>
    public static string GenerarClave(string origen, ICollection<string> yaUsadas)
    {
        var baseClave = Simplificar(origen);

        if (string.IsNullOrWhiteSpace(baseClave) || !char.IsLetter(baseClave[0]))
        {
            baseClave = $"campo_{baseClave}".TrimEnd('_');
        }

        if (baseClave.Length > 36)
        {
            baseClave = baseClave[..36].TrimEnd('_');
        }

        var candidata = baseClave;
        var sufijo = 2;

        while (yaUsadas.Contains(candidata))
        {
            candidata = $"{baseClave}_{sufijo.ToString(CultureInfo.InvariantCulture)}";
            sufijo++;
        }

        return candidata;
    }

    public static string GenerarCodigo(string? nombre)
    {
        var baseCodigo = Simplificar(nombre ?? string.Empty).ToUpperInvariant();

        if (baseCodigo.Length > 32)
        {
            baseCodigo = baseCodigo[..32].TrimEnd('_');
        }

        if (string.IsNullOrWhiteSpace(baseCodigo))
        {
            baseCodigo = "ACTA";
        }

        // El sufijo evita choques entre plantillas con nombres parecidos sin obligar
        // a consultar la tabla desde el normalizador.
        var sufijo = DateTime.UtcNow.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture);
        return $"{EspacioActaPlantillas.PrefijoPersonalizada}{baseCodigo}_{sufijo}";
    }

    /// <summary>Quita tildes, pasa a minúsculas y deja solo letras, dígitos y guion bajo.</summary>
    private static string Simplificar(string valor)
    {
        var normalizado = valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalizado.Length);

        foreach (var caracter in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(caracter);
            }
        }

        return NoAlfanumericoPattern
            .Replace(builder.ToString().Normalize(NormalizationForm.FormC), "_")
            .Trim('_');
    }

    private static string? Recortar(string? valor, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var limpio = valor.Trim();
        return limpio.Length <= maxLength ? limpio : limpio[..maxLength];
    }

    private static int LongitudPorTipo(EspacioActaTipoCampo tipo) => tipo switch
    {
        EspacioActaTipoCampo.TextoLargo => 2000,
        EspacioActaTipoCampo.Enlaces => 1000,
        EspacioActaTipoCampo.Correo => 150,
        EspacioActaTipoCampo.Credencial => 120,
        EspacioActaTipoCampo.Seleccion => 120,
        EspacioActaTipoCampo.Documento => 30,
        EspacioActaTipoCampo.Telefono => 40,
        EspacioActaTipoCampo.Numero or EspacioActaTipoCampo.Decimal or EspacioActaTipoCampo.Moneda => 20,
        EspacioActaTipoCampo.Fecha => 10,
        EspacioActaTipoCampo.Hora => 5,
        EspacioActaTipoCampo.Casilla => 10,
        _ => 300
    };
}
