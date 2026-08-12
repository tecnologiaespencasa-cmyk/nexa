using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Nexa.Helpers;

/// <summary>
/// Reglas canonicas de normalizacion y HMAC del puente Nexa -> Supabase.
///
/// IMPORTANTE: estas reglas son un contrato. La Edge Function
/// (supabase/functions/sync-pacientes-heridas/normalize.ts) implementa las
/// mismas reglas en TypeScript y el portal Next.js de la fase 2 debera
/// implementarlas identicas para poder reconocer al mismo paciente.
/// Los vectores de prueba compartidos estan en
/// supabase/functions/sync-pacientes-heridas/test-vectors.json y los verifican
/// las dos implementaciones (tools/bridge-selftest.cs y normalize.test.ts).
///
/// NORMALIZACION DE DOCUMENTO
///   1. Descomponer en Unicode NFD y eliminar las marcas diacriticas
///      (categoria NonSpacingMark): "N" con virgulilla queda "N", "a" con
///      tilde queda "a".
///   2. Eliminar todo caracter que no sea [A-Za-z0-9]: espacios, puntos,
///      guiones, comas, barras, parentesis, etc.
///   3. Pasar a MAYUSCULAS (invariante).
///   Resultado: ^[A-Z0-9]+$  (cadena vacia = documento invalido)
///   Ejemplo: " 1.234.567-8 " -> "12345678"
///
/// NORMALIZACION DE NOMBRE
///   1. Descomponer en Unicode NFD y eliminar las marcas diacriticas.
///   2. Sustituir por un espacio todo caracter que no sea [A-Za-z0-9].
///   3. Colapsar espacios consecutivos y recortar los extremos.
///   4. Pasar a MAYUSCULAS (invariante).
///   Resultado: ^[A-Z0-9]+( [A-Z0-9]+)*$  (cadena vacia = nombre invalido)
///   Ejemplo: "  jose   perez  " -> "JOSE PEREZ"
///
/// HMAC
///   documento_hmac = HMAC-SHA256(BRIDGE_HMAC_SECRET, documento_normalizado)
///   nombre_hmac    = HMAC-SHA256(BRIDGE_HMAC_SECRET, nombre_normalizado)
///   Clave: el secreto en UTF-8 tal cual. Mensaje: la cadena normalizada en
///   UTF-8. Salida: hexadecimal en MINUSCULA de 64 caracteres.
///   No es SHA-256 simple: sin la clave el digest no se puede reproducir.
///
/// Estas funciones nunca modifican el dato original almacenado en la base de
/// datos de la intranet: trabajan sobre copias en memoria.
/// </summary>
public static class BridgeIdentityNormalizer
{
    /// <summary>Regla canonica de normalizacion de documento.</summary>
    public static string NormalizeDocument(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in StripDiacritics(value))
        {
            if (IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString();
    }

    /// <summary>Regla canonica de normalizacion de nombre.</summary>
    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSeparator = false;
        foreach (var character in StripDiacritics(value))
        {
            if (IsAsciiLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                pendingSeparator = false;
                builder.Append(char.ToUpperInvariant(character));
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return builder.ToString();
    }

    /// <summary>HMAC-SHA256(secreto, mensaje) en hexadecimal minuscula.</summary>
    public static string ComputeHmacHex(string secret, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        var key = Encoding.UTF8.GetBytes(secret);
        var digest = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(message));
        return Convert.ToHexStringLower(digest);
    }

    /// <summary>
    /// Firma canonica de una peticion al puente:
    /// HMAC-SHA256(BRIDGE_API_SECRET, timestamp + "." + requestId + "." + rawBody).
    /// </summary>
    public static string ComputeRequestSignature(string apiSecret, string timestamp, string requestId, string rawBody) =>
        ComputeHmacHex(apiSecret, $"{timestamp}.{requestId}.{rawBody}");

    private static bool IsAsciiLetterOrDigit(char character) =>
        character is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static string StripDiacritics(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
