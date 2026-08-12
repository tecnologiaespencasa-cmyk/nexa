/**
 * Edge Function: sync-pacientes-heridas
 *
 * Unica puerta de entrada de escritura del puente. La intranet Nexa la invoca
 * por HTTPS con autenticacion bearer + firma HMAC de la peticion.
 *
 * Secretos requeridos (Supabase > Edge Functions > Secrets):
 *   BRIDGE_API_SECRET   autenticacion y firma de la peticion
 *   BRIDGE_HMAC_SECRET  derivacion de documento_hmac y nombre_hmac
 * SUPABASE_URL y SUPABASE_SERVICE_ROLE_KEY los inyecta la plataforma.
 *
 * La funcion se despliega con verify_jwt = false: no usamos JWT de Supabase,
 * la autenticacion propia (bearer + firma + timestamp + nonce) es la barrera.
 */

import { handleRequest, type UpsertResult } from "./handler.ts";

const SUPABASE_URL = Deno.env.get("SUPABASE_URL") ?? "";
const SERVICE_ROLE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? "";
const BRIDGE_API_SECRET = Deno.env.get("BRIDGE_API_SECRET") ?? "";
const BRIDGE_HMAC_SECRET = Deno.env.get("BRIDGE_HMAC_SECRET") ?? "";

/**
 * Ejecuta el upsert llamando por RPC a public.bridge_sync_pacientes_heridas
 * con la service_role key. Se usa fetch directo contra PostgREST para no
 * arrastrar dependencias externas.
 */
async function upsert(
  requestId: string,
  filas: Array<{ d: string; n: string }>,
): Promise<UpsertResult> {
  const response = await fetch(`${SUPABASE_URL}/rest/v1/rpc/bridge_sync_pacientes_heridas`, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      apikey: SERVICE_ROLE_KEY,
      authorization: `Bearer ${SERVICE_ROLE_KEY}`,
    },
    body: JSON.stringify({ p_request_id: requestId, p_pacientes: filas }),
  });

  if (!response.ok) {
    // El cuerpo de error de PostgREST no contiene datos personales (la funcion
    // solo recibe HMAC), pero se recorta por prudencia.
    const detalle = (await response.text()).slice(0, 200);
    throw new Error(`rpc_failed:${response.status}:${detalle}`);
  }

  return (await response.json()) as UpsertResult;
}

Deno.serve(async (request: Request) => {
  if (!BRIDGE_API_SECRET || !BRIDGE_HMAC_SECRET || !SUPABASE_URL || !SERVICE_ROLE_KEY) {
    console.error(JSON.stringify({ evt: "missing_configuration" }));
    return new Response(
      JSON.stringify({ success: false, error: "not_configured", message: "Faltan secretos en la Edge Function." }),
      { status: 500, headers: { "content-type": "application/json; charset=utf-8" } },
    );
  }

  try {
    return await handleRequest(request, {
      apiSecret: BRIDGE_API_SECRET,
      hmacSecret: BRIDGE_HMAC_SECRET,
      upsert,
      log: (entry) => console.log(JSON.stringify(entry)),
    });
  } catch (error) {
    const mensaje = error instanceof Error ? error.message : "error_desconocido";
    console.error(JSON.stringify({ evt: "unhandled_error", detail: mensaje.slice(0, 200) }));
    return new Response(
      JSON.stringify({ success: false, error: "internal_error", message: "Error interno procesando la sincronizacion." }),
      { status: 500, headers: { "content-type": "application/json; charset=utf-8" } },
    );
  }
});
