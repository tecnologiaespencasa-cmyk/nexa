/**
 * Prueba de humo contra la Edge Function ya desplegada.
 *
 * Comprueba, en este orden:
 *   1. Sin cabeceras            -> 401
 *   2. Bearer incorrecto        -> 401
 *   3. Firma incorrecta         -> 401
 *   4. Timestamp fuera de rango -> 401
 *   5. Documento vacio          -> 422
 *   6. Peticion valida          -> 200  (inserta un paciente de prueba)
 *   7. Reenvio del mismo requestId -> 409
 *   8. Mismo paciente otra vez  -> 200 sin insertar (idempotencia)
 *
 * El paciente de prueba usa un documento ficticio; borralo despues con:
 *   delete from bridge.pacientes_heridas where documento_hmac = '<el que imprime el script>';
 *
 * Uso:
 *   BRIDGE_API_SECRET=... SUPABASE_PROJECT_URL=https://<ref>.supabase.co \
 *     node supabase/functions/sync-pacientes-heridas/smoke-test.mjs
 */
import { createHmac, randomUUID } from "node:crypto";

const PROJECT_URL = process.env.SUPABASE_PROJECT_URL;
const API_SECRET = process.env.BRIDGE_API_SECRET;

if (!PROJECT_URL || !API_SECRET) {
  console.error("Define SUPABASE_PROJECT_URL y BRIDGE_API_SECRET en el entorno.");
  process.exit(2);
}

const ENDPOINT = `${PROJECT_URL.replace(/\/$/, "")}/functions/v1/sync-pacientes-heridas`;
const sign = (secret, ts, requestId, body) =>
  createHmac("sha256", Buffer.from(secret, "utf8")).update(`${ts}.${requestId}.${body}`).digest("hex");

let fallos = 0;

async function comprobar(nombre, esperado, opciones) {
  const {
    patients = [],
    requestId = randomUUID(),
    timestamp = Math.floor(Date.now() / 1000),
    secreto = API_SECRET,
    firma,
    bearer = API_SECRET,
    cabeceras = true,
  } = opciones ?? {};

  const cuerpo = JSON.stringify({ requestId, timestamp, patients });
  const headers = { "content-type": "application/json" };
  if (cabeceras) {
    headers.authorization = `Bearer ${bearer}`;
    headers["x-bridge-timestamp"] = String(timestamp);
    headers["x-bridge-request-id"] = requestId;
    headers["x-bridge-signature"] = firma ?? sign(secreto, timestamp, requestId, cuerpo);
  }

  const respuesta = await fetch(ENDPOINT, { method: "POST", headers, body: cuerpo });
  const datos = await respuesta.json().catch(() => ({}));
  const ok = respuesta.status === esperado;
  if (!ok) fallos++;
  console.log(
    `  ${ok ? "ok   " : "FALLO"} ${nombre}: HTTP ${respuesta.status}` +
      (datos.error ? ` (${datos.error})` : "") +
      (datos.success ? ` processed=${datos.processed} inserted=${datos.inserted} updated=${datos.updated}` : "") +
      (ok ? "" : ` -- esperado ${esperado}`),
  );
  return { respuesta, datos, requestId };
}

const pacientePrueba = [{ document: "PRUEBAPUENTE0001", name: "PACIENTE DE PRUEBA PUENTE" }];

console.log(`Endpoint: ${ENDPOINT}\n`);

console.log("Rechazos esperados");
await comprobar("sin cabeceras de firma", 401, { cabeceras: false });
await comprobar("bearer incorrecto", 401, { bearer: "no-es-el-secreto", patients: pacientePrueba });
await comprobar("firma incorrecta", 401, { firma: "0".repeat(64), patients: pacientePrueba });
await comprobar("timestamp expirado", 401, {
  timestamp: Math.floor(Date.now() / 1000) - 3600,
  patients: pacientePrueba,
});
await comprobar("documento vacio", 422, { patients: [{ document: "   ", name: "ALGUIEN" }] });
await comprobar("payload sin pacientes", 400, { patients: [] });

console.log("\nPeticiones validas");
const primera = await comprobar("paciente de prueba nuevo", 200, { patients: pacientePrueba });
await comprobar("reenvio del mismo requestId (replay)", 409, {
  patients: pacientePrueba,
  requestId: primera.requestId,
});
await comprobar("mismo paciente, requestId nuevo (idempotente)", 200, { patients: pacientePrueba });

const documentoHmac = createHmac("sha256", Buffer.from(process.env.BRIDGE_HMAC_SECRET ?? "", "utf8"))
  .update("PRUEBAPUENTE0001")
  .digest("hex");

console.log(
  process.env.BRIDGE_HMAC_SECRET
    ? `\nBorra el paciente de prueba con:\n  delete from bridge.pacientes_heridas where documento_hmac = '${documentoHmac}';`
    : "\nPara conocer el documento_hmac del paciente de prueba y borrarlo, define tambien BRIDGE_HMAC_SECRET.",
);

console.log(fallos === 0 ? "\nTodas las comprobaciones pasaron." : `\n${fallos} comprobacion(es) fallaron.`);
process.exit(fallos === 0 ? 0 : 1);
