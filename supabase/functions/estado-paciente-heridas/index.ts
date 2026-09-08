/**
 * Edge Function: estado-paciente-heridas
 *
 * Unica puerta de lectura del puente. La usa el Portal Administrativo antes de
 * dejar registrar un seguimiento de clinica de heridas: le dice si el paciente
 * sigue en el programa y sobre cual ingreso debe cargarlo.
 *
 * Secretos requeridos (Supabase > Edge Functions > Secrets):
 *   BRIDGE_QUERY_API_SECRET  autenticacion y firma de la peticion (secreto de LECTURA)
 *   BRIDGE_HMAC_SECRET       derivacion de documento_hmac (el MISMO de la escritura)
 * SUPABASE_URL y SUPABASE_SERVICE_ROLE_KEY los inyecta la plataforma.
 *
 * POR QUE NO BRIDGE_API_SECRET: ese es el secreto de ESCRITURA, el que autoriza a
 * sincronizar pacientes en sync-pacientes-heridas. Dárselo al portal, que solo
 * necesita consultar, le daria de paso permiso para escribir en el puente. Con
 * BRIDGE_QUERY_API_SECRET los dos roles quedan separados: quien consulta no puede
 * sincronizar, y rotar uno no obliga a rotar el otro.
 *
 * BRIDGE_HMAC_SECRET si es el mismo de la escritura, y no puede ser otro: el HMAC
 * del documento tiene que coincidir con el que se guardo. Nunca sale de Supabase,
 * asi que el portal no lo ve.
 *
 * No necesita BRIDGE_ENCRYPTION_KEY: aqui no se descifra ningun nombre.
 *
 * Se despliega con verify_jwt = false, igual que la de escritura: la barrera es
 * la autenticacion propia (bearer + firma + ventana de tiempo).
 */

import { type EstadoPaciente, handleRequest } from "./handler.ts";

const SUPABASE_URL = Deno.env.get("SUPABASE_URL") ?? "";
const SERVICE_ROLE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? "";
const BRIDGE_QUERY_API_SECRET = Deno.env.get("BRIDGE_QUERY_API_SECRET") ?? "";
const BRIDGE_HMAC_SECRET = Deno.env.get("BRIDGE_HMAC_SECRET") ?? "";

/**
 * Consulta por RPC a public.bridge_estado_paciente_heridas con la service_role
 * key. Mismo patron que la funcion de escritura: fetch directo contra PostgREST
 * para no arrastrar dependencias externas.
 */
async function consultar(documentoHmac: string): Promise<EstadoPaciente> {
  const response = await fetch(`${SUPABASE_URL}/rest/v1/rpc/bridge_estado_paciente_heridas`, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      apikey: SERVICE_ROLE_KEY,
      authorization: `Bearer ${SERVICE_ROLE_KEY}`,
    },
    body: JSON.stringify({ p_documento_hmac: documentoHmac }),
  });

  if (!response.ok) {
    const detalle = (await response.text()).slice(0, 200);
    throw new Error(`rpc_failed:${response.status}:${detalle}`);
  }

  return (await response.json()) as EstadoPaciente;
}

Deno.serve(async (request: Request) => {
  if (!BRIDGE_QUERY_API_SECRET || !BRIDGE_HMAC_SECRET || !SUPABASE_URL || !SERVICE_ROLE_KEY) {
    console.error(JSON.stringify({ evt: "missing_configuration" }));
    return new Response(
      JSON.stringify({ success: false, error: "not_configured", message: "Faltan secretos en la Edge Function." }),
      { status: 500, headers: { "content-type": "application/json; charset=utf-8" } },
    );
  }

  try {
    return await handleRequest(request, {
      apiSecret: BRIDGE_QUERY_API_SECRET,
      hmacSecret: BRIDGE_HMAC_SECRET,
      consultar,
      log: (entry) => console.log(JSON.stringify(entry)),
    });
  } catch (error) {
    const mensaje = error instanceof Error ? error.message : "error_desconocido";
    console.error(JSON.stringify({ evt: "unhandled_error", detail: mensaje.slice(0, 200) }));
    return new Response(
      JSON.stringify({ success: false, error: "internal_error", message: "Error interno consultando el estado." }),
      { status: 500, headers: { "content-type": "application/json; charset=utf-8" } },
    );
  }
});
