/**
 * Pruebas de la Edge Function estado-paciente-heridas.
 *
 * El portal falla cerrado: bloquea el registro ante cualquier respuesta que no
 * sea un permiso explicito. Estas pruebas fijan justamente eso, que la funcion
 * solo diga "puedes registrar" cuando de verdad hay un ingreso activo, y que
 * nunca devuelva ni registre el documento del paciente.
 *
 * Ejecutar:  node --test supabase/functions/estado-paciente-heridas/
 */
import test from "node:test";
import assert from "node:assert/strict";

import { type ConsultaDependencies, type EstadoPaciente, handleRequest } from "./handler.ts";
import { computeRequestSignature, hmacHex, importHmacKey, normalizeDocument } from "../sync-pacientes-heridas/normalize.ts";

const QUERY_SECRET = "TEST_ONLY_query_api_secret_0123";
const WRITE_SECRET = "TEST_ONLY_api_secret_0123456789";
const HMAC_SECRET = "TEST_ONLY_bridge_hmac_secret";
const AHORA = 1_760_000_000;

const SIN_INGRESOS: EstadoPaciente = {
  encontrado: false,
  puede_registrar: false,
  ingreso_actual: null,
  ingresos: [],
};

/** Reproduce en memoria la semantica de public.bridge_estado_paciente_heridas. */
function crearBaseFalsa(estados: Record<string, EstadoPaciente> = {}) {
  const consultados: string[] = [];
  let fallarCon: Error | null = null;

  const consultar = async (documentoHmac: string): Promise<EstadoPaciente> => {
    if (fallarCon) throw fallarCon;
    consultados.push(documentoHmac);
    return estados[documentoHmac] ?? SIN_INGRESOS;
  };

  return {
    consultados,
    consultar,
    romper: (error: Error) => {
      fallarCon = error;
    },
  };
}

function crearDeps(
  base: ReturnType<typeof crearBaseFalsa>,
  logs: Record<string, unknown>[],
): ConsultaDependencies {
  return {
    apiSecret: QUERY_SECRET,
    hmacSecret: HMAC_SECRET,
    consultar: base.consultar,
    log: (entry) => logs.push(entry),
    now: () => AHORA,
  };
}

interface OpcionesPeticion {
  document?: unknown;
  requestId?: string;
  timestamp?: number;
  timestampHeader?: string;
  firmarCon?: string;
  firma?: string;
  bearer?: string;
  metodo?: string;
  cuerpoCrudo?: string;
}

async function construirPeticion(opciones: OpcionesPeticion = {}): Promise<Request> {
  const requestId = opciones.requestId ?? crypto.randomUUID();
  const timestamp = opciones.timestamp ?? AHORA;
  const cuerpo = opciones.cuerpoCrudo ??
    JSON.stringify({ requestId, timestamp, document: opciones.document ?? "71234567" });
  const timestampHeader = opciones.timestampHeader ?? String(timestamp);

  const firma = opciones.firma ??
    await computeRequestSignature(
      await importHmacKey(opciones.firmarCon ?? QUERY_SECRET),
      timestampHeader,
      requestId,
      cuerpo,
    );

  return new Request("https://proyecto.supabase.co/functions/v1/estado-paciente-heridas", {
    method: opciones.metodo ?? "POST",
    headers: {
      authorization: `Bearer ${opciones.bearer ?? QUERY_SECRET}`,
      "content-type": "application/json",
      "x-bridge-timestamp": timestampHeader,
      "x-bridge-request-id": requestId,
      "x-bridge-signature": firma,
    },
    body: opciones.metodo === "GET" ? undefined : cuerpo,
  });
}

async function ejecutar(opciones: OpcionesPeticion = {}, contexto?: {
  base: ReturnType<typeof crearBaseFalsa>;
  logs: Record<string, unknown>[];
}) {
  const base = contexto?.base ?? crearBaseFalsa();
  const logs = contexto?.logs ?? [];
  const respuesta = await handleRequest(await construirPeticion(opciones), crearDeps(base, logs));
  return { respuesta, cuerpo: await respuesta.json(), base, logs };
}

/** HMAC del documento tal como lo calcularia la funcion. */
async function hmacDe(documento: string): Promise<string> {
  return await hmacHex(await importHmacKey(HMAC_SECRET), normalizeDocument(documento));
}

// ---------------------------------------------------------------------------
// Paciente con ingreso activo
// ---------------------------------------------------------------------------
test("un paciente con ingreso activo autoriza y dice cual es", async () => {
  const hmac = await hmacDe("71234567");
  const base = crearBaseFalsa({
    [hmac]: {
      encontrado: true,
      puede_registrar: true,
      ingreso_actual: 2,
      ingresos: [{ numero: 1, estado: "cerrado" }, { numero: 2, estado: "activo" }],
    },
  });

  const { respuesta, cuerpo } = await ejecutar({ document: "71234567" }, { base, logs: [] });

  assert.equal(respuesta.status, 200);
  assert.equal(cuerpo.found, true);
  assert.equal(cuerpo.canRegister, true);
  assert.equal(cuerpo.currentAdmission, 2);
  assert.deepEqual(cuerpo.admissions, [
    { number: 1, state: "cerrado" },
    { number: 2, state: "activo" },
  ]);
});

// ---------------------------------------------------------------------------
// Paciente dado de alta
// ---------------------------------------------------------------------------
test("un paciente sin ingreso activo no autoriza", async () => {
  const hmac = await hmacDe("71234567");
  const base = crearBaseFalsa({
    [hmac]: {
      encontrado: true,
      puede_registrar: false,
      ingreso_actual: null,
      ingresos: [{ numero: 1, estado: "cerrado" }],
    },
  });

  const { cuerpo } = await ejecutar({ document: "71234567" }, { base, logs: [] });

  assert.equal(cuerpo.found, true);
  assert.equal(cuerpo.canRegister, false);
  assert.equal(cuerpo.currentAdmission, null);
});

test("un documento que no esta en el puente no autoriza", async () => {
  const { cuerpo } = await ejecutar({ document: "99999999" });

  assert.equal(cuerpo.found, false);
  assert.equal(cuerpo.canRegister, false);
  assert.equal(cuerpo.currentAdmission, null);
  assert.deepEqual(cuerpo.admissions, []);
});

// ---------------------------------------------------------------------------
// El documento se normaliza antes de calcular el HMAC
// ---------------------------------------------------------------------------
test("el documento se normaliza igual que en la escritura", async () => {
  const hmac = await hmacDe("71234567");
  const base = crearBaseFalsa({
    [hmac]: { encontrado: true, puede_registrar: true, ingreso_actual: 1, ingresos: [] },
  });

  // Con puntos y espacios debe llegar al mismo HMAC que "71234567".
  const { cuerpo } = await ejecutar({ document: " 71.234.567 " }, { base, logs: [] });

  assert.equal(cuerpo.canRegister, true);
  assert.equal(base.consultados[0], hmac);
});

// ---------------------------------------------------------------------------
// Autenticacion: el secreto de escritura NO sirve aqui
// ---------------------------------------------------------------------------
test("el secreto de escritura no autoriza a consultar", async () => {
  const { respuesta, cuerpo } = await ejecutar({ bearer: WRITE_SECRET, firmarCon: WRITE_SECRET });

  assert.equal(respuesta.status, 401);
  assert.equal(cuerpo.error, "unauthorized");
});

test("sin credencial se rechaza", async () => {
  const { respuesta, cuerpo } = await ejecutar({ bearer: "" });

  assert.equal(respuesta.status, 401);
  assert.equal(cuerpo.error, "unauthorized");
});

test("una firma que no corresponde se rechaza", async () => {
  const { respuesta, cuerpo } = await ejecutar({ firmarCon: "otro_secreto_cualquiera" });

  assert.equal(respuesta.status, 401);
  assert.equal(cuerpo.error, "invalid_signature");
});

test("un cuerpo alterado despues de firmar se rechaza", async () => {
  const requestId = crypto.randomUUID();
  const cuerpoFirmado = JSON.stringify({ requestId, timestamp: AHORA, document: "71234567" });
  const firma = await computeRequestSignature(
    await importHmacKey(QUERY_SECRET),
    String(AHORA),
    requestId,
    cuerpoFirmado,
  );

  const { respuesta, cuerpo } = await ejecutar({
    requestId,
    firma,
    // Se cambia el documento conservando la firma del original.
    cuerpoCrudo: JSON.stringify({ requestId, timestamp: AHORA, document: "99999999" }),
  });

  assert.equal(respuesta.status, 401);
  assert.equal(cuerpo.error, "invalid_signature");
});

// ---------------------------------------------------------------------------
// Ventana de tiempo y formato
// ---------------------------------------------------------------------------
test("un timestamp fuera de la ventana se rechaza", async () => {
  const { respuesta, cuerpo } = await ejecutar({ timestamp: AHORA - 3600 });

  assert.equal(respuesta.status, 401);
  assert.equal(cuerpo.error, "timestamp_out_of_window");
});

test("el timestamp del cuerpo debe coincidir con el de la cabecera", async () => {
  const requestId = crypto.randomUUID();
  const cuerpo = JSON.stringify({ requestId, timestamp: AHORA - 1, document: "71234567" });
  const firma = await computeRequestSignature(
    await importHmacKey(QUERY_SECRET),
    String(AHORA),
    requestId,
    cuerpo,
  );

  const resultado = await ejecutar({ requestId, cuerpoCrudo: cuerpo, firma });

  assert.equal(resultado.respuesta.status, 400);
  assert.equal(resultado.cuerpo.error, "timestamp_mismatch");
});

test("el requestId del cuerpo debe coincidir con el de la cabecera", async () => {
  const requestId = crypto.randomUUID();
  const cuerpo = JSON.stringify({ requestId: crypto.randomUUID(), timestamp: AHORA, document: "71234567" });
  const firma = await computeRequestSignature(
    await importHmacKey(QUERY_SECRET),
    String(AHORA),
    requestId,
    cuerpo,
  );

  const resultado = await ejecutar({ requestId, cuerpoCrudo: cuerpo, firma });

  assert.equal(resultado.respuesta.status, 400);
  assert.equal(resultado.cuerpo.error, "request_id_mismatch");
});

test("solo se admite POST", async () => {
  const { respuesta, cuerpo } = await ejecutar({ metodo: "GET" });

  assert.equal(respuesta.status, 405);
  assert.equal(cuerpo.error, "method_not_allowed");
});

test("un documento vacio se rechaza y no se consulta la base", async () => {
  const base = crearBaseFalsa();
  const { respuesta, cuerpo } = await ejecutar({ document: "" }, { base, logs: [] });

  assert.equal(respuesta.status, 422);
  assert.equal(cuerpo.error, "empty_document");
  assert.equal(base.consultados.length, 0);
});

test("un documento que solo trae signos se rechaza", async () => {
  const { respuesta, cuerpo } = await ejecutar({ document: "---" });

  assert.equal(respuesta.status, 422);
  assert.equal(cuerpo.error, "empty_document");
});

// ---------------------------------------------------------------------------
// Nunca se filtra el documento
// ---------------------------------------------------------------------------
test("la respuesta no contiene el documento ni su HMAC", async () => {
  const hmac = await hmacDe("71234567");
  const base = crearBaseFalsa({
    [hmac]: { encontrado: true, puede_registrar: true, ingreso_actual: 1, ingresos: [] },
  });

  const { cuerpo } = await ejecutar({ document: "71234567" }, { base, logs: [] });

  const texto = JSON.stringify(cuerpo);
  assert.ok(!texto.includes("71234567"));
  assert.ok(!texto.includes(hmac));
});

test("el log no contiene el documento ni su HMAC", async () => {
  const hmac = await hmacDe("71234567");
  const base = crearBaseFalsa({
    [hmac]: { encontrado: true, puede_registrar: true, ingreso_actual: 1, ingresos: [] },
  });
  const logs: Record<string, unknown>[] = [];

  await ejecutar({ document: "71234567" }, { base, logs });

  const texto = JSON.stringify(logs);
  assert.ok(logs.length > 0);
  assert.ok(!texto.includes("71234567"));
  assert.ok(!texto.includes(hmac));
});

// ---------------------------------------------------------------------------
// Fallo de la base: se propaga como error, nunca como permiso
// ---------------------------------------------------------------------------
test("si la consulta a la base falla, la excepcion sube y no se autoriza", async () => {
  const base = crearBaseFalsa();
  base.romper(new Error("rpc_failed:404:function does not exist"));

  const peticion = await construirPeticion();
  await assert.rejects(
    () => handleRequest(peticion, crearDeps(base, [])),
    /rpc_failed/,
  );
});
