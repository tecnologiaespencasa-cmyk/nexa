/**
 * Logica pura de la Edge Function estado-paciente-heridas.
 *
 * Responde una sola pregunta del portal: para este documento, ¿se pueden
 * registrar seguimientos y sobre cual ingreso?
 *
 * Se separa de index.ts igual que en sync-pacientes-heridas, para poder
 * ejecutarla en pruebas sin Deno ni Docker.
 *
 * REGLAS DE LOGGING: aqui NUNCA se escribe documento, documento_hmac, payload
 * ni secretos. Solo metadatos tecnicos.
 */

import {
  computeRequestSignature,
  hmacHex,
  importHmacKey,
  normalizeDocument,
  timingSafeEqual,
} from "../sync-pacientes-heridas/normalize.ts";

/** Un ingreso tal como lo devuelve la funcion de base de datos. */
export interface IngresoConsultado {
  numero: number;
  estado: "activo" | "cerrado";
}

export interface EstadoPaciente {
  encontrado: boolean;
  puede_registrar: boolean;
  ingreso_actual: number | null;
  ingresos: IngresoConsultado[];
}

export interface ConsultaDependencies {
  /**
   * Secreto compartido portal <-> Edge Function (autenticacion y firma).
   * Es el secreto de LECTURA (BRIDGE_QUERY_API_SECRET), no el de escritura:
   * consultar el estado no debe habilitar a sincronizar pacientes.
   */
  apiSecret: string;
  /** Secreto exclusivo para derivar documento_hmac. El mismo de la escritura. */
  hmacSecret: string;
  /** Ejecuta la consulta (RPC public.bridge_estado_paciente_heridas). */
  consultar(documentoHmac: string): Promise<EstadoPaciente>;
  log?(entry: Record<string, unknown>): void;
  now?(): number;
  maxBodyBytes?: number;
  timestampToleranceSeconds?: number;
}

const DEFAULT_MAX_BODY_BYTES = 8_192;
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

export async function handleRequest(
  request: Request,
  deps: ConsultaDependencies,
): Promise<Response> {
  const log = deps.log ?? (() => {});
  const now = deps.now ?? (() => Math.floor(Date.now() / 1000));
  const maxBodyBytes = deps.maxBodyBytes ?? DEFAULT_MAX_BODY_BYTES;
  const tolerance = deps.timestampToleranceSeconds ?? DEFAULT_TIMESTAMP_TOLERANCE_SECONDS;
  const startedAt = Date.now();

  if (request.method !== "POST") {
    return fail(405, "method_not_allowed", "Solo se admite POST.");
  }

  // 1. Autenticacion ------------------------------------------------------
  const authorization = request.headers.get("authorization") ?? "";
  const bearer = authorization.startsWith("Bearer ") ? authorization.slice(7) : "";
  if (!await timingSafeEqual(bearer, deps.apiSecret)) {
    log({ evt: "auth_failed" });
    return fail(401, "unauthorized", "Credencial invalida.");
  }

  // 2. Cabeceras de firma -------------------------------------------------
  const timestampHeader = request.headers.get("x-bridge-timestamp") ?? "";
  const requestIdHeader = request.headers.get("x-bridge-request-id") ?? "";
  const signatureHeader = request.headers.get("x-bridge-signature") ?? "";

  if (!timestampHeader || !requestIdHeader || !signatureHeader) {
    return fail(400, "missing_headers", "Faltan cabeceras de firma.");
  }
  if (!REQUEST_ID_PATTERN.test(requestIdHeader)) {
    return fail(400, "invalid_request_id", "requestId con formato invalido.");
  }

  const timestamp = Number(timestampHeader);
  if (!Number.isInteger(timestamp)) {
    return fail(400, "invalid_timestamp", "timestamp invalido.");
  }
  if (Math.abs(now() - timestamp) > tolerance) {
    log({ evt: "timestamp_out_of_window" });
    return fail(401, "timestamp_out_of_window", "La peticion esta fuera de la ventana permitida.");
  }

  // 3. Cuerpo -------------------------------------------------------------
  const rawBody = await request.text();
  if (new TextEncoder().encode(rawBody).length > maxBodyBytes) {
    return fail(413, "body_too_large", "El cuerpo excede el tamano permitido.");
  }

  const apiKey = await importHmacKey(deps.apiSecret);
  const esperada = await computeRequestSignature(apiKey, timestampHeader, requestIdHeader, rawBody);
  if (!await timingSafeEqual(signatureHeader, esperada)) {
    log({ evt: "signature_mismatch", requestId: requestIdHeader });
    return fail(401, "invalid_signature", "Firma invalida.");
  }

  // No se consume nonce anti-replay: esta funcion solo lee y devuelve siempre
  // lo mismo para la misma entrada, asi que reenviarla no cambia nada. Anotar
  // cada consulta llenaria la tabla de nonces sin aportar proteccion.

  let payload: { requestId?: unknown; timestamp?: unknown; document?: unknown };
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

  const documento = normalizeDocument(
    typeof payload.document === "string" ? payload.document : "",
  );
  if (documento.length === 0) {
    return fail(422, "empty_document", "El documento esta vacio.");
  }

  // 4. Consulta -----------------------------------------------------------
  // El documento se convierte a HMAC aqui: el secreto no sale de Supabase, ni
  // siquiera para consultar, igual que en la escritura.
  const hmacKey = await importHmacKey(deps.hmacSecret);
  const documentoHmac = await hmacHex(hmacKey, documento);
  const estado = await deps.consultar(documentoHmac);
  const elapsedMs = Date.now() - startedAt;

  log({
    evt: "estado_ok",
    requestId: requestIdHeader,
    encontrado: estado.encontrado,
    puedeRegistrar: estado.puede_registrar,
    ingresos: estado.ingresos.length,
    elapsedMs,
  });

  return jsonResponse(200, {
    success: true,
    found: estado.encontrado,
    canRegister: estado.puede_registrar,
    currentAdmission: estado.ingreso_actual,
    admissions: estado.ingresos.map((x) => ({ number: x.numero, state: x.estado })),
    requestId: requestIdHeader,
  });
}
