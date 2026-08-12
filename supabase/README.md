# Proyecto Supabase puente

Artefactos del proyecto de Supabase que recibe los pacientes de clínica de heridas.
La explicación completa (arquitectura, normalización, secretos, rotación, riesgos)
está en [`docs/puente-supabase.md`](../docs/puente-supabase.md).

```
supabase/
├── config.toml                                  verify_jwt = false para la función
├── migrations/
│   └── 20260812180000_bridge_pacientes_heridas.sql
└── functions/sync-pacientes-heridas/
    ├── index.ts            arranque (Deno.serve, secretos, RPC a PostgREST)
    ├── handler.ts          validaciones, normalización, HMAC  (lógica pura)
    ├── normalize.ts        reglas canónicas de normalización y HMAC
    ├── test-vectors.json   vectores compartidos con la intranet
    ├── handler.test.ts     pruebas del contrato HTTP
    └── normalize.test.ts   pruebas de normalización y HMAC
```

## Pruebas (no necesitan Docker ni Deno)

```bash
node --test supabase/functions/sync-pacientes-heridas/normalize.test.ts supabase/functions/sync-pacientes-heridas/handler.test.ts
```

Contra la función ya desplegada (inserta un paciente ficticio que después borras):

```bash
BRIDGE_API_SECRET=... SUPABASE_PROJECT_URL=https://qlmglhygiyykyhyzjczr.supabase.co node supabase/functions/sync-pacientes-heridas/smoke-test.mjs
```

## Despliegue

Requiere un **Personal Access Token** de Supabase solo durante la configuración
(`export SUPABASE_ACCESS_TOKEN=sbp_...`). Ese token **no** se usa en tiempo de
ejecución y debe revocarse al terminar.

1. Aplicar el SQL: pegar `migrations/20260812180000_bridge_pacientes_heridas.sql`
   en el SQL Editor del proyecto, o

   ```bash
   npx supabase db push --project-ref <ref>
   ```

2. Cargar los secretos de la función:

   ```bash
   npx supabase secrets set BRIDGE_API_SECRET=<valor> BRIDGE_HMAC_SECRET=<valor> --project-ref <ref>
   ```

3. Desplegar la función:

   ```bash
   npx supabase functions deploy sync-pacientes-heridas --project-ref <ref> --no-verify-jwt
   ```

   > En este equipo no hay CLI de Supabase ni Docker, así que el despliegue inicial se
   > hizo con la Management API, que no necesita ninguno de los dos:
   >
   > ```bash
   > curl -X POST "https://api.supabase.com/v1/projects/<ref>/functions/deploy?slug=sync-pacientes-heridas" -H "Authorization: Bearer $SUPABASE_ACCESS_TOKEN" -F 'metadata={"name":"sync-pacientes-heridas","entrypoint_path":"index.ts","verify_jwt":false};type=application/json' -F "file=@index.ts" -F "file=@handler.ts" -F "file=@normalize.ts"
   > ```
   >
   > El SQL y los secretos se aplicaron igual, con `POST /v1/projects/<ref>/database/query`
   > y `POST /v1/projects/<ref>/secrets`.

4. Comprobar que responde y que rechaza lo que debe rechazar:

   ```bash
   curl -i -X POST https://<ref>.supabase.co/functions/v1/sync-pacientes-heridas
   ```

   Debe devolver `401` con `{"error":"missing_headers"}`: la función está viva y no
   acepta peticiones sin firmar.
