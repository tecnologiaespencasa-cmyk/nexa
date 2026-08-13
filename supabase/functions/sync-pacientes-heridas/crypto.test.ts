/**
 * Pruebas del cifrado autenticado del nombre (AES-256-GCM).
 *
 * Ejecutar:
 *   node --test supabase/functions/sync-pacientes-heridas/crypto.test.ts
 */
import test from "node:test";
import assert from "node:assert/strict";
import { randomBytes } from "node:crypto";

import {
  decryptName,
  encryptName,
  ENVELOPE_PATTERN,
  importEncryptionKey,
} from "./crypto.ts";

const CLAVE = randomBytes(32).toString("base64");
const OTRA_CLAVE = randomBytes(32).toString("base64");
const DOCUMENTO_HMAC = "a".repeat(64);

test("la clave debe medir exactamente 32 bytes (AES-256)", async () => {
  await assert.rejects(() => importEncryptionKey(randomBytes(16).toString("base64")), /invalid_length/);
  await assert.rejects(() => importEncryptionKey(randomBytes(31).toString("base64")), /invalid_length/);
  await assert.doesNotReject(() => importEncryptionKey(CLAVE));
  // Tambien se acepta en hexadecimal de 64 caracteres.
  await assert.doesNotReject(() => importEncryptionKey(randomBytes(32).toString("hex")));
});

test("cifrar y descifrar devuelve el nombre original, con tildes y mayusculas", async () => {
  const key = await importEncryptionKey(CLAVE);
  for (const nombre of ["JOSÉ PÉREZ", "María Ángela Gómez", "JUAN PEREZ", "Ñoño Muñoz"]) {
    const sobre = await encryptName(key, nombre, DOCUMENTO_HMAC);
    assert.equal(await decryptName(key, sobre, DOCUMENTO_HMAC), nombre);
  }
});

test("el sobre cumple el formato v1.<nonce>.<ciphertext> que valida la base de datos", async () => {
  const key = await importEncryptionKey(CLAVE);
  const sobre = await encryptName(key, "JOSE PEREZ", DOCUMENTO_HMAC);
  assert.match(sobre, ENVELOPE_PATTERN);
  assert.equal(sobre.split(".").length, 3);
  assert.equal(sobre.split(".")[0], "v1");
  assert.equal(sobre.split(".")[1].length, 16, "nonce de 12 bytes en base64url");
});

test("el mismo nombre cifrado dos veces produce ciphertext distinto (nonce unico)", async () => {
  const key = await importEncryptionKey(CLAVE);
  const a = await encryptName(key, "JOSE PEREZ", DOCUMENTO_HMAC);
  const b = await encryptName(key, "JOSE PEREZ", DOCUMENTO_HMAC);
  assert.notEqual(a, b);
  assert.notEqual(a.split(".")[1], b.split(".")[1], "el nonce no se repite");
  // Aun asi los dos descifran al mismo nombre.
  assert.equal(await decryptName(key, a, DOCUMENTO_HMAC), "JOSE PEREZ");
  assert.equal(await decryptName(key, b, DOCUMENTO_HMAC), "JOSE PEREZ");
});

test("el nombre no aparece en claro dentro del sobre", async () => {
  const key = await importEncryptionKey(CLAVE);
  const sobre = await encryptName(key, "CARLOS ANDRES MEJIA", DOCUMENTO_HMAC);
  assert.ok(!sobre.includes("CARLOS"));
  assert.ok(!sobre.includes("MEJIA"));
  // Ni decodificando el base64url del ciphertext.
  const crudo = Buffer.from(sobre.split(".")[2].replace(/-/g, "+").replace(/_/g, "/"), "base64").toString("latin1");
  assert.ok(!crudo.includes("CARLOS"));
  assert.ok(!crudo.includes("MEJIA"));
});

test("sin la clave correcta no se puede descifrar", async () => {
  const key = await importEncryptionKey(CLAVE);
  const otra = await importEncryptionKey(OTRA_CLAVE);
  const sobre = await encryptName(key, "CARLOS ANDRES MEJIA", DOCUMENTO_HMAC);
  await assert.rejects(() => decryptName(otra, sobre, DOCUMENTO_HMAC));
});

test("el ciphertext esta atado a su fila: con otro documento_hmac falla", async () => {
  const key = await importEncryptionKey(CLAVE);
  const sobre = await encryptName(key, "CARLOS ANDRES MEJIA", DOCUMENTO_HMAC);
  await assert.rejects(() => decryptName(key, sobre, "b".repeat(64)));
});

test("alterar el ciphertext invalida el tag de autenticacion", async () => {
  const key = await importEncryptionKey(CLAVE);
  const sobre = await encryptName(key, "JOSE PEREZ", DOCUMENTO_HMAC);
  const [version, nonce, ciphertext] = sobre.split(".");
  const alterado = ciphertext.slice(0, -2) + (ciphertext.endsWith("AA") ? "BB" : "AA");
  await assert.rejects(() => decryptName(key, `${version}.${nonce}.${alterado}`, DOCUMENTO_HMAC));
});

test("un sobre mal formado o de otra version se rechaza", async () => {
  const key = await importEncryptionKey(CLAVE);
  await assert.rejects(() => decryptName(key, "no-es-un-sobre", DOCUMENTO_HMAC), /envelope_invalid/);
  await assert.rejects(() => decryptName(key, "v2.AAAAAAAAAAAAAAAA.AAAA", DOCUMENTO_HMAC), /envelope_invalid/);
  await assert.rejects(() => decryptName(key, "v1.corto.AAAA", DOCUMENTO_HMAC), /envelope_invalid/);
});
