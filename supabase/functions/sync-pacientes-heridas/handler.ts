/**
 * Logica pura de la Edge Function sync-pacientes-heridas.
 *
 * Esta separada de index.ts (que solo resuelve variables de entorno y hace el
 * RPC contra PostgREST) para poder ejecutarla en pruebas sin Deno ni Docker.
 *
 * REGLAS DE LOGGING: aqui NUNCA se escribe documento, nombre, documento_hmac,
 * nombre_hmac, payload ni secretos. Solo metadatos tecnicos.
 */

import {
  computeRequestSignature,
  hmacHex,
  importHmacKey,
  normalizeDocument,
  normalizeName,
  timingSafeEqual,
} from "./normalize.ts";
import { encryptName, importEncryptionKey } from "./crypto.ts";

export interface UpsertResult {
  replay: boolean;
  recibidos: number;
  unicos: number;
  insertados: number;
  actualizados: number;
  /** Ingresos al programa escritos. Ausente si la base aun no tiene la tabla. */
  ingresos?: number;
}

/**
 * Un ingreso del paciente al programa: numero de orden y si sigue abierto.
 * No lleva fechas ni nada clinico; solo lo que el portal necesita para saber
 * si puede registrar seguimientos y sobre cual ingreso.
 */
export interface IngresoPuente {
  n: number;
  s: "activo" | "cerrado";
}

/** Fila que se persiste: HMAC del documento, HMAC del nombre y nombre cifrado. */
export interface FilaPuente {
  d: string;
  n: string;
  e: string;
  /** Lista completa de ingresos. Ausente si la intranet aun no los envia. */
  i?: IngresoPuente[];
}

export interface HandlerDependencies {
  /** Secreto compartido intranet <-> Edge Function (autenticacion y firma). */
  apiSecret: string;
  /** Secreto exclusivo para derivar los HMAC de documento y nombre. */
  hmacSecret: string;
  /** Clave AES-256 para cifrar el nombre (BRIDGE_ENCRYPTION_KEY). */
  encryptionKey: string;
  /** Ejecuta el upsert en PostgreSQL (RPC public.bridge_sync_pacientes_heridas). */
  upsert(
    requestId: string,
    filas: FilaPuente[],
  ): Promise<UpsertResult>;
  /** Escribe una linea de log tecnica (sin datos personales). */
  log?(entry: Record<string, unknown>): void;
  /** Epoch en segundos; inyectable para pruebas. */
  now?(): number;
  maxRecords?: number;
  maxBodyBytes?: number;
  timestampToleranceSeconds?: number;
}

const DEFAULT_MAX_RECORDS = 500;
const DEFAULT_MAX_BODY_BYTES = 1_048_576; // 1 MiB
const DEFAULT_TIMESTAMP_TOLERANCE_SECONDS = 300; // +/- 5 minutos
const REQUEST_ID_PATTERN = /^[A-Za-z0-9-]{8,64}$/;

function jsonResponse(status: number, body: Record<string, unknown>): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8",
      "cache-control": "no-store",
    },
  });
}

function fail(status: number, error: string, message: string): Response {
  return jsonResponse(status, { success: false, error, message });
}

interface PayloadPaciente {
  document?: unknown;
  name?: unknown;
  admissions?: unknown;
}

const MAX_ADMISSIONS_PER_PATIENT = 100;
const ESTADOS_INGRESO = new Set(["activo", "cerrado"]);

/**
 * Valida los ingresos de un paciente. Devuelve la lista normalizada, o un
 * codigo de error si el formato no cumple. Se es estricto a proposito: esta
 * funcion es el borde del sistema y la tabla tiene CHECK equivalentes, asi que
 * un dato mal formado debe rechazarse aqui con un mensaje util y no morir en
 * la base con un error de restriccion.
 */
function parseAdmissions(
  valor: unknown,
): { ok: true; ingresos?: IngresoPuente[] } | { ok: false; error: string; message: string } {
  if (valor === undefined || valor === null) {
    return { ok: true };
  }
  if (!Array.isArray(valor)) {
    return { ok: false, error: "invalid_admissions", message: "admissions debe ser un arreglo." };
  }
  if (valor.length > MAX_ADMISSIONS_PER_PATIENT) {
    return {
      ok: false,
      error: "too_many_admissions",
      message: `admissions admite maximo ${MAX_ADMISSIONS_PER_PATIENT} ingresos por paciente.`,
    };
  }

  const ingresos: IngresoPuente[] = [];
  const vistos = new Set<number>();

  for (const item of valor) {
    if (typeof item !== "object" || item === null) {
      return { ok: false, error: "invalid_admissions", message: "Cada ingreso debe ser un objeto." };
    }

    const numero = (item as { number?: unknown }).number;
    const estado = (item as { state?: unknown }).state;

    if (typeof numero !== "number" || !Number.isInteger(numero) || numero < 1 || numero > 999) {
      return {
        ok: false,
        error: "invalid_admission_number",
        message: "El numero de ingreso debe ser un entero entre 1 y 999.",
      };
    }
    if (typeof estado !== "string" || !ESTADOS_INGRESO.has(estado)) {
      return {
        ok: false,
        error: "invalid_admission_state",
        message: "El estado del ingreso debe ser activo o cerrado.",
      };
    }
    if (vistos.has(numero)) {
      return {
        ok: false,
        error: "duplicate_admission",
        message: "Un mismo ingreso llego dos veces para el mismo paciente.",
      };
    }

    vistos.add(numero);
    ingresos.push({ n: numero, s: estado as "activo" | "cerrado" });
  }

  return { ok: true, ingresos };
}

export async function handleRequest(
  request: Request,
  deps: HandlerDependencies,
): Promise<Response> {
  const log = deps.log ?? (() => {});
  const now = deps.now ?? (() => Math.floor(Date.now() / 1000));
  const maxRecords = deps.maxRecords ?? DEFAULT_MAX_RECORDS;
  const maxBodyBytes = deps.maxBodyBytes ?? DEFAULT_MAX_BODY_BYTES;
  const tolerance = deps.timestampToleranceSeconds ??
    DEFAULT_TIMESTAMP_TOLERANCE_SECONDS;
  const startedAt = Date.now();

  // 1. Metodo -------------------------------------------------------------
  if (request.method !== "POST") {
    return fail(405, "method_not_allowed", "Solo se acepta POST.");
  }

  // 2. Content-Type -------------------------------------------------------
  const contentType = request.headers.get("content-type") ?? "";
  if (!contentType.toLowerCase().includes("application/json")) {
    return fail(415, "unsupported_media_type", "Content-Type debe ser application/json.");
  }

  // 3. Cabeceras obligatorias --------------------------------------------
  const authorization = request.headers.get("authorization") ?? "";
  const timestampHeader = request.headers.get("x-bridge-timestamp") ?? "";
  const requestIdHeader = request.headers.get("x-bridge-request-id") ?? "";
  const signatureHeader = (request.headers.get("x-bridge-signature") ?? "").toLowerCase();

  if (!authorization || !timestampHeader || !requestIdHeader || !signatureHeader) {
    return fail(401, "missing_headers", "Faltan cabeceras de autenticacion o firma.");
  }

  if (!REQUEST_ID_PATTERN.test(requestIdHeader)) {
    return fail(400, "invalid_request_id", "X-Bridge-Request-Id no tiene un formato valido.");
  }

  // 4. Autenticacion (bearer con comparacion en tiempo constante) --------
  const bearer = authorization.startsWith("Bearer ") ? authorization.slice(7) : "";
  if (!bearer || !(await timingSafeEqual(bearer, deps.apiSecret))) {
    log({ evt: "auth_failed", requestId: requestIdHeader });
    return fail(401, "unauthorized", "Credenciales invalidas.");
  }

  // 5. Timestamp (anti-replay por ventana) --------------------------------
  const timestamp = Number(timestampHeader);
  if (!Number.isInteger(timestamp)) {
    return fail(400, "invalid_timestamp", "X-Bridge-Timestamp debe ser epoch en segundos.");
  }
  const skew = Math.abs(now() - timestamp);
  if (skew > tolerance) {
    log({ evt: "timestamp_rejected", requestId: requestIdHeader, skew });
    return fail(401, "timestamp_out_of_window", "El timestamp esta fuera de la ventana permitida.");
  }

  // 6. Cuerpo y firma -----------------------------------------------------
  const rawBody = await request.text();
  if (new TextEncoder().encode(rawBody).length > maxBodyBytes) {
    return fail(413, "payload_too_large", "El cuerpo de la peticion excede el maximo permitido.");
  }

  const apiKey = await importHmacKey(deps.apiSecret);
  const expectedSignature = await computeRequestSignature(
    apiKey,
    timestampHeader,
    requestIdHeader,
    rawBody,
  );
  if (!(await timingSafeEqual(signatureHeader, expectedSignature))) {
    log({ evt: "signature_rejected", requestId: requestIdHeader });
    return fail(401, "invalid_signature", "La firma de la peticion no es valida.");
  }

  // 7. Estructura del payload --------------------------------------------
  let payload: { requestId?: unknown; timestamp?: unknown; patients?: unknown };
  try {
    payload = JSON.parse(rawBody);
  } catch {
    return fail(400, "invalid_json", "El cuerpo no es JSON valido.");
  }

  if (typeof payload !== "object" || payload === null) {
    return fail(400, "invalid_payload", "El cuerpo debe ser un objeto JSON.");
  }
  if (payload.requestId !== requestIdHeader) {
    return fail(400, "request_id_mismatch", "requestId del cuerpo y de la cabecera no coinciden.");
  }
  if (payload.timestamp !== timestamp) {
    return fail(400, "timestamp_mismatch", "timestamp del cuerpo y de la cabecera no coinciden.");
  }
  if (!Array.isArray(payload.patients)) {
    return fail(400, "invalid_payload", "patients debe ser un arreglo.");
  }
  if (payload.patients.length === 0) {
    return fail(400, "empty_batch", "patients no puede estar vacio.");
  }
  if (payload.patients.length > maxRecords) {
    return fail(413, "batch_too_large", `patients admite maximo ${maxRecords} registros por peticion.`);
  }

  // 8. Normalizacion + HMAC + cifrado del nombre --------------------------
  //
  // La intranet envia documento y nombre reales por HTTPS. Aqui, y solo en
  // memoria durante esta peticion, se derivan los tres valores que si se
  // persisten: los dos HMAC (no reversibles, sirven para buscar) y el nombre
  // cifrado con AES-256-GCM (reversible solo con BRIDGE_ENCRYPTION_KEY).
  const hmacKey = await importHmacKey(deps.hmacSecret);
  const encryptionKey = await importEncryptionKey(deps.encryptionKey);
  const filas: FilaPuente[] = [];

  for (let i = 0; i < payload.patients.length; i++) {
    const item = payload.patients[i] as PayloadPaciente;
    if (typeof item !== "object" || item === null) {
      return fail(422, "invalid_record", `El registro en la posicion ${i} no es un objeto.`);
    }

    const nombreOriginal = typeof item.name === "string" ? item.name.trim() : "";
    const documento = normalizeDocument(
      typeof item.document === "string" ? item.document : "",
    );
    const nombre = normalizeName(nombreOriginal);

    if (documento.length === 0) {
      return fail(422, "empty_document", `El documento esta vacio en la posicion ${i}.`);
    }
    if (nombre.length === 0) {
      return fail(422, "empty_name", `El nombre esta vacio en la posicion ${i}.`);
    }

    const ingresos = parseAdmissions(item.admissions);
    if (!ingresos.ok) {
      return fail(422, ingresos.error, `${ingresos.message} (posicion ${i})`);
    }

    const documentoHmac = await hmacHex(hmacKey, documento);
    const nombreHmac = await hmacHex(hmacKey, nombre);
    // El AAD ata el ciphertext a su fila: no se puede mover a otro paciente.
    const nombreEncrypted = await encryptName(encryptionKey, nombreOriginal, documentoHmac);

    const fila: FilaPuente = { d: documentoHmac, n: nombreHmac, e: nombreEncrypted };
    // Solo se envia la clave cuando la intranet mando ingresos: sin ella, la
    // funcion de base de datos no reconcilia y no borra los que ya existan.
    if (ingresos.ingresos !== undefined) {
      fila.i = ingresos.ingresos;
    }
    filas.push(fila);
  }

  // 9. Escritura ----------------------------------------------------------
  const resultado = await deps.upsert(requestIdHeader, filas);
  const elapsedMs = Date.now() - startedAt;

  if (resultado.replay) {
    log({ evt: "replay_rejected", requestId: requestIdHeader, elapsedMs });
    return fail(409, "replay_detected", "El requestId ya fue procesado.");
  }

  log({
    evt: "sync_ok",
    requestId: requestIdHeader,
    recibidos: resultado.recibidos,
    unicos: resultado.unicos,
    insertados: resultado.insertados,
    actualizados: resultado.actualizados,
    ingresos: resultado.ingresos ?? 0,
    elapsedMs,
  });

  return jsonResponse(200, {
    success: true,
    processed: resultado.unicos,
    inserted: resultado.insertados,
    updated: resultado.actualizados,
    admissions: resultado.ingresos ?? 0,
    requestId: requestIdHeader,
  });
}

/** Reexportado para las pruebas y para index.ts. */
export { hmacHex, importHmacKey, normalizeDocument, normalizeName };
export { decryptName, encryptName, importEncryptionKey } from "./crypto.ts";
