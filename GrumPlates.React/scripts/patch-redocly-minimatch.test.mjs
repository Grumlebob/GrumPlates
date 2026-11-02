// @vitest-environment node
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import path from "node:path";
import { compileFunction } from "node:vm";
import { test } from "vitest";
import {
  installedRedocly,
  legacyImport,
  namedImport,
  originalHash,
  patchedHash,
  transformRedoclySource,
} from "./patch-redocly-minimatch.mjs";

const { utilityFile, utilityRequire } = installedRedocly();
const patched = readFileSync(utilityFile);
const original = Buffer.from(patched.toString("utf8").replace(namedImport, legacyImport));
const hash = (bytes) => createHash("sha256").update(bytes).digest("hex");

test("the exact legacy source transforms once and is idempotent", () => {
  assert.equal(hash(original), originalHash);
  const first = transformRedoclySource(original);
  assert.equal(first.changed, true);
  assert.equal(hash(first.bytes), patchedHash);
  const second = transformRedoclySource(first.bytes);
  assert.equal(second.changed, false);
  assert.deepEqual(second.bytes, first.bytes);
});

test("unexpected vendor source is rejected", () => {
  assert.throws(() => transformRedoclySource(Buffer.from("unexpected source")), /Unexpected Redocly matcher source/);
});

test("the Docker dependency stage copies the postinstall helper before npm ci", () => {
  const dockerfile = readFileSync(new URL("../../GrumPlates/Dockerfile", import.meta.url), "utf8");
  const helperCopy = "COPY GrumPlates.React/scripts/patch-redocly-minimatch.mjs ./scripts/patch-redocly-minimatch.mjs";
  const copiesHelperBeforeInstall = (source) => {
    const stage = source.replaceAll("\r\n", "\n").split(/^FROM /m).find((part) => part.startsWith("node:") && /^.* AS react-dependencies$/m.test(part));
    assert.ok(stage, "The frontend dependency stage must exist.");
    const copy = stage.indexOf(helperCopy);
    const install = stage.search(/^RUN .*\bnpm ci\b/m);
    return copy >= 0 && install > copy;
  };
  assert.equal(copiesHelperBeforeInstall(dockerfile.replace(helperCopy, "")), false);
  assert.equal(copiesHelperBeforeInstall(dockerfile), true);
});

test("the legacy callable import fails with the patched matcher", async () => {
  const module = { exports: {} };
  const load = compileFunction(original.toString("utf8"), ["exports", "require", "module", "__filename", "__dirname"], { filename: utilityFile });
  load(module.exports, utilityRequire, module, utilityFile, path.dirname(utilityFile));
  let calls = 0;
  await assert.rejects(() => module.exports.readFileFromUrl("https://example.test/spec.yaml", {
    headers: [{ matches: "example.test/**", name: "X-Fixture", value: "yes" }],
    customFetch: async () => { calls++; return new globalThis.Response("fixture"); },
  }), /minimatch is not a function/);
  assert.equal(calls, 0);
});

test("the real patched utility preserves matching headers and excludes nonmatches", async () => {
  const utility = utilityRequire(utilityFile);
  let calls = 0;
  const result = await utility.readFileFromUrl("https://example.test/spec.yaml", {
    headers: [
      { matches: "example.test/**", name: "X-Fixture", value: "yes" },
      { matches: "other.test/**", name: "X-Excluded", value: "no" },
    ],
    customFetch: async (url, { headers }) => {
      calls++;
      assert.equal(url, "https://example.test/spec.yaml");
      assert.deepEqual(headers, { "X-Fixture": "yes" });
      return new globalThis.Response("fixture", { headers: { "content-type": "application/yaml" } });
    },
  });
  assert.equal(calls, 1);
  assert.deepEqual(result, { body: "fixture", mimeType: "application/yaml" });
});
