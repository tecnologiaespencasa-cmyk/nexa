/**
 * Pruebas de la Edge Function sync-pacientes-heridas.
 * Cubren el escenario 1-17 acordado, en la parte que corresponde a Supabase.
 *
 * Ejecutar:  node --test supabase/functions/sync-pacientes-heridas/
 */
import test from "node:test";
import assert from "node:assert/strict";

import { type FilaPuente, handleRequest, type HandlerDependencies, type UpsertResult } from "./handler.ts";
import { computeRequestSignature, importHmacKey } from "./normalize.ts";
import { decryptName, ENVELOPE_PATTERN, importEncryptionKey } from "./crypto.ts";

const API_SECRET = "TEST_ONLY_api_secret_0123456789";
const HMAC_SECRET = "TEST_ONLY_bridge_hmac_secret";
// 32 bytes en hexadecimal: clave de PRUEBA, no es la BRIDGE_ENCRYPTION_KEY real.
const ENCRYPTION_KEY = "0123456789abcdef".repeat(4);
const AHORA = 1_760_000_000;

/** Fila almacenada: nombre_hmac y nombre_encrypted. */
interface FilaAlmacenada {
  n: string;
  e: string;
}

/** Reproduce en memoria la semantica de public.bridge_sync_pacientes_heridas. */
function crearBaseFalsa() {
  const tabla = new Map<string, FilaAlmacenada>();
  const nonces = new Set<string>();
  const recibidoEnCrudo: FilaPuente[][] = [];
  let fallarCon: Error | null = null;

  const upsert = async (
    requestId: string,
    filas: FilaPuente[],
  ): Promise<UpsertResult> => {
    if (fallarCon) throw fallarCon;
    recibidoEnCrudo.push(filas);

    if (nonces.has(requestId)) {
      return { replay: true, recibidos: 0, unicos: 0, insertados: 0, actualizados: 0 };
    }
    nonces.add(requestId);

    const deduplicado = new Map<string, FilaAlmacenada>();
    for (const fila of filas) deduplicado.set(fila.d, { n: fila.n, e: fila.e }); // gana el ultimo

    let insertados = 0;
    let actualizados = 0;
    for (const [d, fila] of deduplicado) {
      const actual = tabla.get(d);
      if (!actual) {
        tabla.set(d, fila);
        insertados++;
      } else if (actual.n !== fila.n) {
        // Igual que en SQL: solo se reescribe cuando cambia nombre_hmac, que es
        // determinista; el ciphertext cambia siempre por el nonce aleatorio.
        tabla.set(d, fila);
        actualizados++;
      }
    }

    return {
      replay: false,
      recibidos: filas.length,
      unicos: deduplicado.size,
      insertados,
      actualizados,
    };
  };

  return {
    tabla,
    recibidoEnCrudo,
    upsert,
    romper: (error: Error) => {
      fallarCon = error;
    },
  };
}

function crearDeps(base: ReturnType<typeof crearBaseFalsa>, logs: Record<string, unknown>[]): HandlerDependencies {
  return {
    apiSecret: API_SECRET,
    hmacSecret: HMAC_SECRET,
    encryptionKey: ENCRYPTION_KEY,
    upsert: base.upsert,
    log: (entry) => logs.push(entry),
    now: () => AHORA,
  };
}

interface OpcionesPeticion {
  patients?: unknown;
  requestId?: string;
  timestamp?: number;
  timestampHeader?: string;
  firmarCon?: string;
  firma?: string;
  bearer?: string;
  metodo?: string;
  contentType?: string;
  cuerpoCrudo?: string;
}

async function construirPeticion(opciones: OpcionesPeticion = {}): Promise<Request> {
  const requestId = opciones.requestId ?? crypto.randomUUID();
  const timestamp = opciones.timestamp ?? AHORA;
  const cuerpo = opciones.cuerpoCrudo ??
    JSON.stringify({ requestId, timestamp, patients: opciones.patients ?? [] });
  const timestampHeader = opciones.timestampHeader ?? String(timestamp);

  const firma = opciones.firma ??
    await computeRequestSignature(
      await importHmacKey(opciones.firmarCon ?? API_SECRET),
      timestampHeader,
      requestId,
      cuerpo,
    );

  const metodo = opciones.metodo ?? "POST";
  return new Request("https://proyecto.supabase.co/functions/v1/sync-pacientes-heridas", {
    method: metodo,
    headers: {
      authorization: `Bearer ${opciones.bearer ?? API_SECRET}`,
      "content-type": opciones.contentType ?? "application/json",
      "x-bridge-timestamp": timestampHeader,
      "x-bridge-request-id": requestId,
      "x-bridge-signature": firma,
    },
    // GET/HEAD no admiten cuerpo en la API Request estandar.
    body: metodo === "GET" || metodo === "HEAD" ? undefined : cuerpo,
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

// ---------------------------------------------------------------------------
// 1. Paciente nuevo
// ---------------------------------------------------------------------------
test("1. paciente nuevo se inserta", async () => {
  const { respuesta, cuerpo, base } = await ejecutar({
    patients: [{ document: "1234567", name: "JUAN PEREZ" }],
  });
  assert.equal(respuesta.status, 200);
  assert.equal(cuerpo.success, true);
  assert.equal(cuerpo.processed, 1);
  assert.equal(cuerpo.inserted, 1);
  assert.equal(base.tabla.size, 1);
});

// ---------------------------------------------------------------------------
// 2 y 3. Paciente existente / mismo paciente enviado dos veces
// ---------------------------------------------------------------------------
test("2-3. reenviar el mismo paciente es idempotente (no duplica)", async () => {
  const base = crearBaseFalsa();
  const logs: Record<string, unknown>[] = [];
  const paciente = [{ document: "1.234.567", name: "Juan Perez" }];

  const primera = await ejecutar({ patients: paciente }, { base, logs });
  const segunda = await ejecutar({ patients: paciente }, { base, logs });

  assert.equal(primera.cuerpo.inserted, 1);
  assert.equal(segunda.cuerpo.inserted, 0);
  assert.equal(segunda.cuerpo.updated, 0);
  assert.equal(base.tabla.size, 1, "no se crean duplicados");
});

test("3b. el mismo documento repetido dentro del lote se colapsa en una fila", async () => {
  const { cuerpo, base } = await ejecutar({
    patients: [
      { document: "555", name: "ANA GOMEZ" },
      { document: "555", name: "ANA GOMEZ" },
    ],
  });
  assert.equal(cuerpo.processed, 1);
  assert.equal(base.tabla.size, 1);
});

// ---------------------------------------------------------------------------
// 4. Nombre modificado
// ---------------------------------------------------------------------------
test("4. si cambia el nombre se actualiza nombre_hmac sin duplicar documento", async () => {
  const base = crearBaseFalsa();
  const logs: Record<string, unknown>[] = [];

  await ejecutar({ patients: [{ document: "999", name: "ANA GOMEZ" }] }, { base, logs });
  const clave = [...base.tabla.keys()][0];
  const inicial = { ...base.tabla.get(clave)! };

  const segunda = await ejecutar(
    { patients: [{ document: "999", name: "ANA GOMEZ RUIZ" }] },
    { base, logs },
  );

  assert.equal(segunda.cuerpo.updated, 1);
  assert.equal(base.tabla.size, 1);
  assert.notEqual(base.tabla.get(clave)!.n, inicial.n, "cambia nombre_hmac");
  assert.notEqual(base.tabla.get(clave)!.e, inicial.e, "cambia nombre_encrypted");

  // El nombre nuevo es el que queda cifrado.
  const key = await importEncryptionKey(ENCRYPTION_KEY);
  assert.equal(await decryptName(key, base.tabla.get(clave)!.e, clave), "ANA GOMEZ RUIZ");
});

test("4b. una variacion solo de espacios o tildes NO genera actualizacion", async () => {
  const base = crearBaseFalsa();
  const logs: Record<string, unknown>[] = [];

  await ejecutar({ patients: [{ document: "77", name: "JOSE PEREZ" }] }, { base, logs });
  const segunda = await ejecutar(
    { patients: [{ document: "77", name: "  josé   pérez " }] },
    { base, logs },
  );

  assert.equal(segunda.cuerpo.inserted, 0);
  assert.equal(segunda.cuerpo.updated, 0);
});

// ---------------------------------------------------------------------------
// 5 y 6. Documento vacio / nombre vacio
// ---------------------------------------------------------------------------
test("5. documento vacio se rechaza con 422", async () => {
  const { respuesta, cuerpo, base } = await ejecutar({
    patients: [{ document: "  ..-- ", name: "JUAN PEREZ" }],
  });
  assert.equal(respuesta.status, 422);
  assert.equal(cuerpo.error, "empty_document");
  assert.equal(base.tabla.size, 0);
});

test("6. nombre vacio se rechaza con 422", async () => {
  const { respuesta, cuerpo, base } = await ejecutar({
    patients: [{ document: "1234567", name: "   " }],
  });
  assert.equal(respuesta.status, 422);
  assert.equal(cuerpo.error, "empty_name");
  assert.equal(base.tabla.size, 0);
});

// ---------------------------------------------------------------------------
// 7. Payload invalido
// ---------------------------------------------------------------------------
test("7. payload invalido se rechaza con 400", async () => {
  const base = crearBaseFalsa();
  const logs: Record<string, unknown>[] = [];

  const sinArreglo = await ejecutar({ patients: { document: "1" } as unknown }, { base, logs });
  assert.equal(sinArreglo.respuesta.status, 400);
  assert.equal(sinArreglo.cuerpo.error, "invalid_payload");

  const vacio = await ejecutar({ patients: [] }, { base, logs });
  assert.equal(vacio.respuesta.status, 400);
  assert.equal(vacio.cuerpo.error, "empty_batch");

  const requestId = crypto.randomUUID();
  const cuerpoCrudo = "{ esto no es json";
  const jsonRoto = await ejecutar({ requestId, cuerpoCrudo }, { base, logs });
  assert.equal(jsonRoto.respuesta.status, 400);
  assert.equal(jsonRoto.cuerpo.error, "invalid_json");

  assert.equal(base.tabla.size, 0);
});

test("7b. requestId o timestamp del cuerpo que no coinciden con la cabecera se rechazan", async () => {
  const requestId = crypto.randomUUID();
  const cuerpoCrudo = JSON.stringify({
    requestId: crypto.randomUUID(),
    timestamp: AHORA,
    patients: [{ document: "1", name: "A" }],
  });
  const { respuesta, cuerpo } = await ejecutar({ requestId, cuerpoCrudo });
  assert.equal(respuesta.status, 400);
  assert.equal(cuerpo.error, "request_id_mismatch");
});

test("7c. metodo y Content-Type incorrectos se rechazan", async () => {
  const base = crearBaseFalsa();
  const logs: Record<string, unknown>[] = [];

  const get = await ejecutar({ metodo: "GET", patients: [] }, { base, logs });
  assert.equal(get.respuesta.status, 405);

  const texto = await ejecutar({ contentType: "text/plain", patients: [] }, { base, logs });
  assert.equal(texto.respuesta.status, 415);
});

test("7d. un lote mayor al maximo permitido se rechaza con 413", async () => {
  const base = crearBaseFalsa();
  const logs: Record<string, unknown>[] = [];
  const muchos = Array.from({ length: 501 }, (_, i) => ({ document: String(i), name: `PACIENTE ${i}` }));
  const { respuesta, cuerpo } = await ejecutar({ patients: muchos }, { base, logs });
  assert.equal(respuesta.status, 413);
  assert.equal(cuerpo.error, "batch_too_large");
  assert.equal(base.tabla.size, 0);
});

// ---------------------------------------------------------------------------
// 8 y 9. Firma invalida / secret incorrecto
// ---------------------------------------------------------------------------
test("8. firma invalida se rechaza con 401", async () => {
  const { respuesta, cuerpo, base } = await ejecutar({
    patients: [{ document: "1234567", name: "JUAN PEREZ" }],
    firma: "0".repeat(64),
  });
  assert.equal(respuesta.status, 401);
  assert.equal(cuerpo.error, "invalid_signature");
  assert.equal(base.tabla.size, 0);
});

test("8b. alterar el cuerpo despues de firmar invalida la firma", async () => {
  const requestId = crypto.randomUUID();
  const cuerpoOriginal = JSON.stringify({
    requestId,
    timestamp: AHORA,
    patients: [{ document: "1", name: "A" }],
  });
  const firma = await computeRequestSignature(
    await importHmacKey(API_SECRET),
    String(AHORA),
    requestId,
    cuerpoOriginal,
  );
  const cuerpoAlterado = JSON.stringify({
    requestId,
    timestamp: AHORA,
    patients: [{ document: "2", name: "B" }],
  });

  const { respuesta, cuerpo } = await ejecutar({ requestId, cuerpoCrudo: cuerpoAlterado, firma });
  assert.equal(respuesta.status, 401);
  assert.equal(cuerpo.error, "invalid_signature");
});

test("9. secret incorrecto se rechaza con 401", async () => {
  const bearerMalo = await ejecutar({
    patients: [{ document: "1", name: "A" }],
    bearer: "secreto-que-no-es",
  });
  assert.equal(bearerMalo.respuesta.status, 401);
  assert.equal(bearerMalo.cuerpo.error, "unauthorized");

  const firmadoConOtroSecreto = await ejecutar({
    patients: [{ document: "1", name: "A" }],
    firmarCon: "otro-secreto",
  });
  assert.equal(firmadoConOtroSecreto.respuesta.status, 401);
  assert.equal(firmadoConOtroSecreto.cuerpo.error, "invalid_signature");
});

// ---------------------------------------------------------------------------
// 10. Timestamp expirado
// ---------------------------------------------------------------------------
test("10. timestamp fuera de la ventana de 5 minutos se rechaza", async () => {
  const viejo = await ejecutar({
    patients: [{ document: "1", name: "A" }],
    timestamp: AHORA - 301,
  });
  assert.equal(viejo.respuesta.status, 401);
  assert.equal(viejo.cuerpo.error, "timestamp_out_of_window");

  const futuro = await ejecutar({
    patients: [{ document: "1", name: "A" }],
    timestamp: AHORA + 301,
  });
  assert.equal(futuro.respuesta.status, 401);

  const dentro = await ejecutar({
    patients: [{ document: "1", name: "A" }],
    timestamp: AHORA - 299,
  });
  assert.equal(dentro.respuesta.status, 200);
});

// ---------------------------------------------------------------------------
// 11. Request ID repetido
// ---------------------------------------------------------------------------
test("11. reenviar la misma peticion firmada (mismo requestId) se rechaza con 409", async () => {
  const base = crearBaseFalsa();
  const logs: Record<string, unknown>[] = [];
  const requestId = crypto.randomUUID();
  const patients = [{ document: "1234567", name: "JUAN PEREZ" }];

  const primera = await ejecutar({ requestId, patients }, { base, logs });
  const replay = await ejecutar({ requestId, patients }, { base, logs });

  assert.equal(primera.respuesta.status, 200);
  assert.equal(replay.respuesta.status, 409);
  assert.equal(replay.cuerpo.error, "replay_detected");
});

test("11b. requestId con formato invalido se rechaza con 400", async () => {
  const { respuesta, cuerpo } = await ejecutar({ requestId: "x", patients: [] });
  assert.equal(respuesta.status, 400);
  assert.equal(cuerpo.error, "invalid_request_id");
});

// ---------------------------------------------------------------------------
// 15. Error de base de datos -> 500 controlado (lo envuelve index.ts)
// ---------------------------------------------------------------------------
test("15. si el upsert falla, el error se propaga para responder 500", async () => {
  const base = crearBaseFalsa();
  base.romper(new Error("rpc_failed:500:boom"));
  await assert.rejects(
    () => ejecutar({ patients: [{ document: "1", name: "A" }] }, { base, logs: [] }),
    /rpc_failed/,
  );
});

// ---------------------------------------------------------------------------
// 16 y 17. Nada de datos reales en la base ni en los logs
// ---------------------------------------------------------------------------
test("16. a la base solo llegan digest hex de 64 caracteres, nunca el dato real", async () => {
  const { base } = await ejecutar({
    patients: [
      { document: "71234567", name: "CARLOS ANDRES MEJIA" },
      { document: "52.998.111", name: "Luz Marina Ríos" },
    ],
  });

  const enviado = JSON.stringify(base.recibidoEnCrudo);
  assert.ok(!enviado.includes("71234567"), "el documento real no debe viajar a la base");
  assert.ok(!enviado.includes("CARLOS"), "el nombre real no debe viajar a la base");
  assert.ok(!enviado.includes("52998111"));
  assert.ok(!enviado.includes("Luz Marina"), "el nombre tampoco viaja dentro del ciphertext");

  for (const [documentoHmac, fila] of base.tabla) {
    assert.match(documentoHmac, /^[0-9a-f]{64}$/);
    assert.match(fila.n, /^[0-9a-f]{64}$/);
    assert.match(fila.e, ENVELOPE_PATTERN);
  }
});

test("17. los logs no contienen documento, nombre, HMAC, payload ni secretos", async () => {
  const base = crearBaseFalsa();
  const logs: Record<string, unknown>[] = [];

  await ejecutar({ patients: [{ document: "71234567", name: "CARLOS ANDRES MEJIA" }] }, { base, logs });
  await ejecutar({ patients: [{ document: "1", name: "A" }], bearer: "secreto-malo" }, { base, logs });
  await ejecutar({ patients: [{ document: "1", name: "A" }], firma: "0".repeat(64) }, { base, logs });

  const texto = JSON.stringify(logs);
  assert.ok(logs.length > 0, "deben registrarse eventos tecnicos");
  assert.ok(!texto.includes("71234567"));
  assert.ok(!texto.includes("CARLOS"));
  assert.ok(!texto.includes(API_SECRET));
  assert.ok(!texto.includes(HMAC_SECRET));
  assert.ok(!texto.includes(ENCRYPTION_KEY), "la clave de cifrado no se registra");
  assert.ok(!texto.includes("secreto-malo"));
  for (const [, fila] of base.tabla) {
    assert.ok(!texto.includes(fila.n), "los HMAC tampoco se registran");
    assert.ok(!texto.includes(fila.e), "ni el nombre cifrado");
  }
});

// ---------------------------------------------------------------------------
// nombre_encrypted (AES-256-GCM)
// ---------------------------------------------------------------------------
test("nombre_encrypted se genera con el formato del sobre y descifra al nombre real", async () => {
  const { base } = await ejecutar({
    patients: [{ document: "71234567", name: "José Pérez Gómez" }],
  });

  const [documentoHmac, fila] = [...base.tabla.entries()][0];
  assert.match(fila.e, ENVELOPE_PATTERN);

  const key = await importEncryptionKey(ENCRYPTION_KEY);
  assert.equal(
    await decryptName(key, fila.e, documentoHmac),
    "José Pérez Gómez",
    "conserva tildes y mayusculas del nombre original",
  );
});

test("sin la clave no se recupera el nombre desde nombre_encrypted", async () => {
  const { base } = await ejecutar({
    patients: [{ document: "71234567", name: "CARLOS ANDRES MEJIA" }],
  });

  const [documentoHmac, fila] = [...base.tabla.entries()][0];
  const otraClave = await importEncryptionKey("f".repeat(64));
  await assert.rejects(() => decryptName(otraClave, fila.e, documentoHmac));
  assert.ok(!fila.e.includes("CARLOS"));
});

test("el mismo nombre en dos pacientes produce ciphertext distinto", async () => {
  const { base } = await ejecutar({
    patients: [
      { document: "111", name: "JUAN PEREZ" },
      { document: "222", name: "JUAN PEREZ" },
    ],
  });

  const cifrados = [...base.tabla.values()].map((fila) => fila.e);
  assert.equal(cifrados.length, 2);
  assert.notEqual(cifrados[0], cifrados[1]);
  // Y el nombre_hmac si coincide: sigue sirviendo para comparar.
  const hmacs = [...base.tabla.values()].map((fila) => fila.n);
  assert.equal(hmacs[0], hmacs[1]);
});

test("una clave de cifrado que no mide 32 bytes hace fallar la peticion", async () => {
  const base = crearBaseFalsa();
  const peticion = await construirPeticion({ patients: [{ document: "1", name: "A" }] });
  await assert.rejects(
    () => handleRequest(peticion, { ...crearDeps(base, []), encryptionKey: "clave-corta" }),
    /encryption_key_invalid_length/,
  );
  assert.equal(base.tabla.size, 0);
});

// ---------------------------------------------------------------------------
// La respuesta nunca devuelve pacientes
// ---------------------------------------------------------------------------
test("la respuesta solo contiene informacion tecnica", async () => {
  const { cuerpo } = await ejecutar({
    patients: [{ document: "71234567", name: "CARLOS ANDRES MEJIA" }],
  });
  assert.deepEqual(
    Object.keys(cuerpo).sort(),
    ["inserted", "processed", "requestId", "success", "updated"],
  );
  const texto = JSON.stringify(cuerpo);
  assert.ok(!texto.includes("71234567"));
  assert.ok(!texto.includes("CARLOS"));
});
