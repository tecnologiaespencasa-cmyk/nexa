/**
 * ============================================================================
 * CIFRADO AUTENTICADO DEL NOMBRE (AES-256-GCM)
 * ============================================================================
 *
 * El HMAC no es reversible, asi que no sirve para devolverle el nombre real al
 * portal administrativo. Por eso, ademas de nombre_hmac, se guarda el nombre
 * cifrado con AES-256-GCM en la columna nombre_encrypted.
 *
 * La clave (BRIDGE_ENCRYPTION_KEY) vive SOLO como secreto de la Edge Function.
 * No esta en la intranet, ni en la tabla, ni en el repositorio, ni en los logs.
 *
 * ---------------------------------------------------------------------------
 * FORMATO ALMACENADO  (contrato con la fase 2)
 * ---------------------------------------------------------------------------
 *
 *     v1.<nonce>.<ciphertext>
 *
 *   v1          version del sobre; permite rotar clave o algoritmo mas adelante
 *   nonce       12 bytes aleatorios, base64url sin relleno (16 caracteres)
 *   ciphertext  texto cifrado + tag de autenticacion de 16 bytes, concatenados
 *               por WebCrypto, en base64url sin relleno
 *
 * Todo lo necesario para descifrar viaja en el sobre EXCEPTO la clave.
 *
 * ---------------------------------------------------------------------------
 * DATOS AUTENTICADOS ADICIONALES (AAD)
 * ---------------------------------------------------------------------------
 * Se usa documento_hmac como AAD. Consecuencias:
 *   - Un ciphertext solo descifra en la fila a la que pertenece: quien tuviera
 *     acceso de escritura a la tabla no puede intercambiar nombres entre
 *     pacientes sin que el descifrado falle.
 *   - Para descifrar hay que pasar el mismo documento_hmac de la fila.
 *
 * ---------------------------------------------------------------------------
 * QUE SE CIFRA
 * ---------------------------------------------------------------------------
 * El nombre tal como lo envia la intranet, solo recortado de espacios extremos:
 * conserva mayusculas y tildes ("JOSE PEREZ" con tilde sigue con tilde). NO se
 * cifra la version normalizada, porque esa pierde tildes y es solo para el HMAC.
 * ============================================================================
 */

const NONCE_BYTES = 12;
const KEY_BYTES = 32; // AES-256
const ENVELOPE_VERSION = "v1";

/** Patron que debe cumplir nombre_encrypted; el mismo que valida la base de datos. */
export const ENVELOPE_PATTERN = /^v1\.[A-Za-z0-9_-]{16}\.[A-Za-z0-9_-]+$/;

const encoder = new TextEncoder();
const decoder = new TextDecoder();

function base64UrlEncode(bytes: Uint8Array): string {
  let binario = "";
  for (const byte of bytes) binario += String.fromCharCode(byte);
  return btoa(binario).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function base64UrlDecode(value: string): Uint8Array {
  const relleno = value.replace(/-/g, "+").replace(/_/g, "/");
  const binario = atob(relleno + "=".repeat((4 - (relleno.length % 4)) % 4));
  const bytes = new Uint8Array(binario.length);
  for (let i = 0; i < binario.length; i++) bytes[i] = binario.charCodeAt(i);
  return bytes;
}

/** Acepta la clave en base64 (con o sin relleno) o en hexadecimal. */
function decodeKeyMaterial(secret: string): Uint8Array {
  const limpio = secret.trim();
  if (/^[0-9a-fA-F]{64}$/.test(limpio)) {
    const bytes = new Uint8Array(KEY_BYTES);
    for (let i = 0; i < KEY_BYTES; i++) bytes[i] = parseInt(limpio.substr(i * 2, 2), 16);
    return bytes;
  }
  return base64UrlDecode(limpio);
}

/**
 * Importa BRIDGE_ENCRYPTION_KEY. Falla si no son exactamente 32 bytes: una
 * clave corta degradaria el cifrado en silencio, asi que se cierra el paso.
 */
export async function importEncryptionKey(secret: string): Promise<CryptoKey> {
  const material = decodeKeyMaterial(secret);
  if (material.length !== KEY_BYTES) {
    throw new Error(`encryption_key_invalid_length:${material.length}`);
  }
  return await crypto.subtle.importKey("raw", material, { name: "AES-GCM" }, false, [
    "encrypt",
    "decrypt",
  ]);
}

/** Cifra el nombre y devuelve el sobre v1.<nonce>.<ciphertext>. */
export async function encryptName(
  key: CryptoKey,
  nombre: string,
  documentoHmacAad: string,
): Promise<string> {
  const nonce = crypto.getRandomValues(new Uint8Array(NONCE_BYTES));
  const cifrado = await crypto.subtle.encrypt(
    {
      name: "AES-GCM",
      iv: nonce,
      tagLength: 128,
      additionalData: encoder.encode(documentoHmacAad),
    },
    key,
    encoder.encode(nombre),
  );
  return `${ENVELOPE_VERSION}.${base64UrlEncode(nonce)}.${base64UrlEncode(new Uint8Array(cifrado))}`;
}

/**
 * Descifra un sobre. Lanza si la version no se reconoce, si el sobre esta mal
 * formado, si la clave no es la correcta o si el AAD (documento_hmac) no
 * coincide: AES-GCM verifica el tag antes de devolver nada.
 *
 * Hoy solo lo usan las pruebas; la fase 2 lo reutilizara desde la Edge Function
 * de consulta del portal administrativo.
 */
export async function decryptName(
  key: CryptoKey,
  envelope: string,
  documentoHmacAad: string,
): Promise<string> {
  // Se valida el sobre completo antes de decodificar nada: asi una entrada
  // corrupta produce un error controlado y no una excepcion de base64.
  if (!ENVELOPE_PATTERN.test(envelope)) {
    throw new Error("envelope_invalid");
  }

  const partes = envelope.split(".");
  const nonce = base64UrlDecode(partes[1]);
  if (nonce.length !== NONCE_BYTES) {
    throw new Error("envelope_invalid_nonce");
  }

  const descifrado = await crypto.subtle.decrypt(
    {
      name: "AES-GCM",
      iv: nonce,
      tagLength: 128,
      additionalData: encoder.encode(documentoHmacAad),
    },
    key,
    base64UrlDecode(partes[2]),
  );
  return decoder.decode(descifrado);
}
