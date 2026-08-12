/**
 * ============================================================================
 * REGLAS CANONICAS DE NORMALIZACION + HMAC DEL PUENTE
 * ============================================================================
 *
 * Estas reglas son la definicion de referencia y DEBEN reproducirse identicas
 * en cualquier otro consumidor (portal Next.js de la fase 2). La version en C#
 * vive en Helpers/BridgeIdentityNormalizer.cs de la intranet Nexa y esta
 * verificada contra los mismos vectores de prueba (normalize.test.ts /
 * tools/bridge-selftest.cs).
 *
 * ---------------------------------------------------------------------------
 * normalizeDocument(documento)
 * ---------------------------------------------------------------------------
 *   1. Descomponer en Unicode NFD y eliminar las marcas diacriticas
 *      (\p{M}) -> "Ñ" se convierte en "N", "á" en "a".
 *   2. Eliminar TODO caracter que no sea [A-Za-z0-9]: espacios, puntos,
 *      guiones, comas, barras, parentesis, etc.
 *   3. Pasar a MAYUSCULAS.
 *   Resultado: /^[A-Z0-9]+$/  (cadena vacia = documento invalido)
 *
 *   Ejemplos: " 1.234.567-8 " -> "12345678"
 *             "cc 71-234.567" -> "CC71234567"
 *
 * ---------------------------------------------------------------------------
 * normalizeName(nombre)
 * ---------------------------------------------------------------------------
 *   1. Descomponer en Unicode NFD y eliminar las marcas diacriticas.
 *   2. Sustituir por un espacio TODO caracter que no sea [A-Za-z0-9].
 *   3. Colapsar espacios consecutivos en uno solo y recortar los extremos.
 *   4. Pasar a MAYUSCULAS.
 *   Resultado: /^[A-Z0-9]+( [A-Z0-9]+)*$/  (cadena vacia = nombre invalido)
 *
 *   Ejemplos: "  josé   pérez  " -> "JOSE PEREZ"
 *             "MARIA-JOSE  NUÑEZ" -> "MARIA JOSE NUNEZ"
 *
 * ---------------------------------------------------------------------------
 * HMAC
 * ---------------------------------------------------------------------------
 *   documento_hmac = HMAC-SHA256(BRIDGE_HMAC_SECRET, normalizeDocument(doc))
 *   nombre_hmac    = HMAC-SHA256(BRIDGE_HMAC_SECRET, normalizeName(nombre))
 *
 *   - Clave: BRIDGE_HMAC_SECRET en UTF-8, tal cual (sin decodificar base64).
 *   - Mensaje: la cadena normalizada en UTF-8.
 *   - Salida: hexadecimal EN MINUSCULA, 64 caracteres.
 *   - El mismo secreto se usa para documento y para nombre.
 *   - NO es SHA-256 simple: sin la clave no se puede reproducir el digest.
 * ============================================================================
 */

const DIACRITICS = /\p{M}/gu;

/** Quita acentos/diacriticos de forma determinista (NFD + eliminar marcas). */
function stripDiacritics(value: string): string {
  return value.normalize("NFD").replace(DIACRITICS, "");
}

/** Regla canonica de normalizacion de documento. Ver cabecera del archivo. */
export function normalizeDocument(value: string | null | undefined): string {
  if (!value) return "";
  return stripDiacritics(value).replace(/[^A-Za-z0-9]/g, "").toUpperCase();
}

/** Regla canonica de normalizacion de nombre. Ver cabecera del archivo. */
export function normalizeName(value: string | null | undefined): string {
  if (!value) return "";
  return stripDiacritics(value)
    .replace(/[^A-Za-z0-9]/g, " ")
    .trim()
    .replace(/\s+/g, " ")
    .toUpperCase();
}

const encoder = new TextEncoder();

/** Importa el secreto UTF-8 como clave HMAC-SHA256 reutilizable. */
export async function importHmacKey(secret: string): Promise<CryptoKey> {
  return await crypto.subtle.importKey(
    "raw",
    encoder.encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
}

function toHex(buffer: ArrayBuffer): string {
  return Array.from(new Uint8Array(buffer))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

/** HMAC-SHA256(clave, mensaje) en hex minuscula. */
export async function hmacHex(key: CryptoKey, message: string): Promise<string> {
  return toHex(await crypto.subtle.sign("HMAC", key, encoder.encode(message)));
}

/**
 * Comparacion en tiempo constante de dos cadenas hex.
 * Se aplica HMAC a ambos lados con una clave efimera aleatoria antes de
 * comparar, de modo que ni la longitud ni el punto de divergencia son
 * observables por temporizacion.
 */
export async function timingSafeEqual(a: string, b: string): Promise<boolean> {
  const nonce = crypto.getRandomValues(new Uint8Array(32));
  const key = await crypto.subtle.importKey(
    "raw",
    nonce,
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const [ha, hb] = await Promise.all([hmacHex(key, a), hmacHex(key, b)]);
  let diff = 0;
  for (let i = 0; i < ha.length; i++) {
    diff |= ha.charCodeAt(i) ^ hb.charCodeAt(i);
  }
  return diff === 0;
}

/** Firma canonica de una peticion: HMAC(API_SECRET, "ts.requestId.rawBody"). */
export async function computeRequestSignature(
  apiSecretKey: CryptoKey,
  timestamp: string,
  requestId: string,
  rawBody: string,
): Promise<string> {
  return await hmacHex(apiSecretKey, `${timestamp}.${requestId}.${rawBody}`);
}
