/**
 * Verifica que la normalizacion y el HMAC de la Edge Function coinciden con
 * los vectores canonicos (test-vectors.json). La intranet corre exactamente
 * las mismas comprobaciones en tools/bridge-selftest.cs.
 *
 * Ejecutar:  node --test supabase/functions/sync-pacientes-heridas/
 */
import test from "node:test";
import assert from "node:assert/strict";
import { createHmac } from "node:crypto";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { hmacHex, importHmacKey, normalizeDocument, normalizeName } from "./normalize.ts";

interface Vector {
  input: string;
  normalized: string;
  hmac: string;
}

const vectors = JSON.parse(
  readFileSync(fileURLToPath(new URL("./test-vectors.json", import.meta.url)), "utf8"),
) as { secret: string; documents: Vector[]; names: Vector[] };

test("normalizeDocument sigue la regla canonica", () => {
  for (const vector of vectors.documents) {
    assert.equal(normalizeDocument(vector.input), vector.normalized, `documento: ${vector.input}`);
  }
});

test("normalizeName sigue la regla canonica", () => {
  for (const vector of vectors.names) {
    assert.equal(normalizeName(vector.input), vector.normalized, `nombre: ${vector.input}`);
  }
});

test("documento y nombre vacios o solo simbolos se normalizan a cadena vacia", () => {
  assert.equal(normalizeDocument("   "), "");
  assert.equal(normalizeDocument("...---"), "");
  assert.equal(normalizeDocument(null), "");
  assert.equal(normalizeName("   "), "");
  assert.equal(normalizeName("-- .. --"), "");
  assert.equal(normalizeName(undefined), "");
});

test("el HMAC coincide con los vectores canonicos", async () => {
  const key = await importHmacKey(vectors.secret);
  for (const vector of [...vectors.documents, ...vectors.names]) {
    assert.equal(await hmacHex(key, vector.normalized), vector.hmac, `hmac: ${vector.input}`);
  }
});

test("el HMAC de WebCrypto coincide con node:crypto (implementacion independiente)", async () => {
  const key = await importHmacKey(vectors.secret);
  for (const vector of vectors.documents) {
    const esperado = createHmac("sha256", Buffer.from(vectors.secret, "utf8"))
      .update(Buffer.from(vector.normalized, "utf8"))
      .digest("hex");
    assert.equal(await hmacHex(key, vector.normalized), esperado);
  }
});

test("el mismo paciente escrito distinto produce el mismo HMAC (idempotencia)", async () => {
  const key = await importHmacKey(vectors.secret);
  const a = await hmacHex(key, normalizeDocument(" 1.234.567-8 "));
  const b = await hmacHex(key, normalizeDocument("1234 5678"));
  assert.equal(a, b);

  const n1 = await hmacHex(key, normalizeName("juan perez"));
  const n2 = await hmacHex(key, normalizeName("  JUAN   PEREZ "));
  assert.equal(n1, n2);
});

test("no es SHA-256 simple: al cambiar el secreto cambia el digest", async () => {
  const conSecretoA = await hmacHex(await importHmacKey("secreto-a"), "12345678");
  const conSecretoB = await hmacHex(await importHmacKey("secreto-b"), "12345678");
  assert.notEqual(conSecretoA, conSecretoB);

  // SHA-256("12345678") sin clave, para confirmar que el resultado es distinto.
  const sha256Simple = "ef797c8118f02dfb649607dd5d3f8c7623048c9c063d532cc95c5ed7a898a64f";
  assert.notEqual(conSecretoA, sha256Simple);
});
