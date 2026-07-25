import assert from "node:assert/strict";
import { afterEach, test } from "node:test";
import {
  mkdtempSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { validateRelease } from "./validate-release.mjs";

const fixtureRoots = [];

function writeJson(root, relativePath, value) {
  const target = join(root, relativePath);
  mkdirSync(dirname(target), { recursive: true });
  writeFileSync(target, `${JSON.stringify(value, null, 2)}\n`);
}

function fixtureRoot() {
  const root = mkdtempSync(join(tmpdir(), "unity-links-unity-release-"));
  fixtureRoots.push(root);
  writeJson(root, "package.json", {
    name: "com.kpk.codex-unity-link",
    version: "0.2.0",
    license: "MIT",
    licensesUrl: "https://github.com/kpkhxlgy0/unity-links-unity/blob/master/LICENSE",
    unity: "2022.3",
  });
  writeFileSync(join(root, "package.json.meta"), "fileFormatVersion: 2\n");
  writeFileSync(join(root, "LICENSE"), "MIT License\n\nCopyright (c) 2026 KPK\n");
  mkdirSync(join(root, "Editor/Sub"), { recursive: true });
  writeFileSync(join(root, "Editor.meta"), "fileFormatVersion: 2\n");
  writeFileSync(join(root, "Editor/Foo.cs"), "internal sealed class Foo {}\n");
  writeFileSync(join(root, "Editor/Foo.cs.meta"), "fileFormatVersion: 2\n");
  writeFileSync(join(root, "Editor/Sub.meta"), "fileFormatVersion: 2\n");
  writeFileSync(join(root, "Editor/Sub/Bar.asmdef"), "{}\n");
  writeFileSync(join(root, "Editor/Sub/Bar.asmdef.meta"), "fileFormatVersion: 2\n");
  return root;
}

function updateJson(root, relativePath, patch) {
  const target = join(root, relativePath);
  const current = JSON.parse(readFileSync(target, "utf8"));
  writeJson(root, relativePath, { ...current, ...patch });
}

afterEach(() => {
  for (const root of fixtureRoots.splice(0)) rmSync(root, { recursive: true, force: true });
});

test("accepts a valid stable Unity package release", () => {
  assert.deepEqual(validateRelease(fixtureRoot(), "0.2.0"), {
    version: "0.2.0",
    tag: "v0.2.0",
  });
});

test("rejects non-stable versions", () => {
  for (const version of ["v0.2.0", "0.2", "0.2.0-preview.1", "latest"]) {
    assert.throws(() => validateRelease(fixtureRoot(), version), /stable MAJOR\.MINOR\.PATCH/);
  }
});

test("rejects incorrect UPM identity and compatibility metadata", () => {
  const cases = [
    [{ name: "com.example.wrong" }, /com\.kpk\.codex-unity-link/],
    [{ version: "0.2.1" }, /version must be 0\.2\.0/],
    [{ license: "Apache-2.0" }, /license must be MIT/],
    [{ licensesUrl: "https://example.com/LICENSE" }, /unity-links-unity/],
    [{ unity: "2021.3" }, /2022\.3/],
  ];
  for (const [patch, pattern] of cases) {
    const root = fixtureRoot();
    updateJson(root, "package.json", patch);
    assert.throws(() => validateRelease(root, "0.2.0"), pattern);
  }
});

test("rejects missing or incorrect MIT metadata", () => {
  const missingLicense = fixtureRoot();
  rmSync(join(missingLicense, "LICENSE"));
  assert.throws(() => validateRelease(missingLicense, "0.2.0"), /LICENSE/);

  const wrongCopyright = fixtureRoot();
  writeFileSync(join(wrongCopyright, "LICENSE"), "MIT License\nCopyright (c) 2026 Someone Else\n");
  assert.throws(() => validateRelease(wrongCopyright, "0.2.0"), /Copyright \(c\) 2026 KPK/);
});

test("rejects missing Unity meta files", () => {
  const missingRootMeta = fixtureRoot();
  rmSync(join(missingRootMeta, "Editor.meta"));
  assert.throws(() => validateRelease(missingRootMeta, "0.2.0"), /Editor\.meta/);

  const missingPackageMeta = fixtureRoot();
  rmSync(join(missingPackageMeta, "package.json.meta"));
  assert.throws(() => validateRelease(missingPackageMeta, "0.2.0"), /package\.json\.meta/);

  const missingScriptMeta = fixtureRoot();
  rmSync(join(missingScriptMeta, "Editor/Foo.cs.meta"));
  assert.throws(() => validateRelease(missingScriptMeta, "0.2.0"), /Foo\.cs\.meta/);

  const missingDirectoryMeta = fixtureRoot();
  rmSync(join(missingDirectoryMeta, "Editor/Sub.meta"));
  assert.throws(() => validateRelease(missingDirectoryMeta, "0.2.0"), /Sub\.meta/);

  const missingAsmdefMeta = fixtureRoot();
  rmSync(join(missingAsmdefMeta, "Editor/Sub/Bar.asmdef.meta"));
  assert.throws(() => validateRelease(missingAsmdefMeta, "0.2.0"), /Bar\.asmdef\.meta/);
});
