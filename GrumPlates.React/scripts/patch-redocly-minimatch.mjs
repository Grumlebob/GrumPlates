import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFileSync, realpathSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";

// openapi-typescript 7 needs Redocly 1's deep imports. Keep that API while using
// the patched matcher's named export: GHSA-g3hv-75wg-m249. Refuse source drift.
export const originalHash = "896a9acce92e3e5905aa267e895985d57888a87f500d58c29da9c8f9d488ff1c";
export const patchedHash = "825736f13fb67dc4a1e2157bb51396209c7343cfd00376ed5d923e3e9ff82a18";
export const legacyImport = 'const minimatch = require("minimatch");';
export const namedImport = 'const { minimatch } = require("minimatch");';
const hash = (bytes) => createHash("sha256").update(bytes).digest("hex");

export function transformRedoclySource(bytes) {
  if (hash(bytes) === patchedHash) return { bytes, changed: false };
  assert.equal(hash(bytes), originalHash, "Unexpected Redocly matcher source; review the compatibility patch.");
  const text = bytes.toString("utf8");
  assert.equal(text.split(legacyImport).length, 2, "Expected one legacy matcher import.");
  const patched = Buffer.from(text.replace(legacyImport, namedImport), "utf8");
  assert.equal(hash(patched), patchedHash, "Unexpected patched matcher source.");
  return { bytes: patched, changed: true };
}

export function installedRedocly() {
  const require = createRequire(import.meta.url);
  const generatorRequire = createRequire(require.resolve("openapi-typescript"));
  const packageFile = generatorRequire.resolve("@redocly/openapi-core/package.json");
  const packageRoot = path.dirname(packageFile);
  const utilityFile = path.join(packageRoot, "lib/utils.js");
  const utilityRequire = createRequire(utilityFile);
  assert.equal(JSON.parse(readFileSync(packageFile, "utf8")).version, "1.34.20", "Review the Redocly version before patching.");
  assert.equal(utilityRequire("minimatch/package.json").version, "11.0.1", "The patched matcher version is required.");
  return { utilityFile, utilityRequire };
}

export function patchInstalledRedocly() {
  const { utilityFile } = installedRedocly();
  const result = transformRedoclySource(readFileSync(utilityFile));
  if (result.changed) writeFileSync(utilityFile, result.bytes);
  return result.changed;
}

if (process.argv[1] && realpathSync(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const changed = patchInstalledRedocly();
  console.log(`Redocly matcher compatibility patch ${changed ? "applied" : "already applied"}.`);
}
