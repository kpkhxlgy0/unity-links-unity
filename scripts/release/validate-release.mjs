import { existsSync, readFileSync, readdirSync } from "node:fs";
import { extname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const STABLE_VERSION = /^[0-9]+\.[0-9]+\.[0-9]+$/;
const EXPECTED_NAME = "com.kpk.codex-unity-link";
const EXPECTED_LICENSE_URL =
  "https://github.com/kpkhxlgy0/unity-links-unity/blob/master/LICENSE";
const EXPECTED_UNITY = "2022.3";
const EXPECTED_COPYRIGHT = "Copyright (c) 2026 KPK";

function readJson(repositoryRoot, relativePath, errors) {
  try {
    return JSON.parse(readFileSync(resolve(repositoryRoot, relativePath), "utf8"));
  } catch (error) {
    errors.push(`${relativePath}: ${error instanceof Error ? error.message : String(error)}`);
    return null;
  }
}

function requirePath(repositoryRoot, relativePath, errors) {
  if (!existsSync(resolve(repositoryRoot, relativePath))) {
    errors.push(`${relativePath}: required Unity package file is missing`);
  }
}

function validateMetaCoverage(repositoryRoot, errors) {
  requirePath(repositoryRoot, "package.json.meta", errors);
  requirePath(repositoryRoot, "Editor.meta", errors);
  const editorRoot = resolve(repositoryRoot, "Editor");
  if (!existsSync(editorRoot)) {
    errors.push("Editor: required Unity package directory is missing");
    return;
  }

  function visit(directory) {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      if (entry.name.endsWith(".meta")) continue;
      const fullPath = join(directory, entry.name);
      const relativePath = relative(repositoryRoot, fullPath).replaceAll("\\", "/");
      if (entry.isDirectory()) {
        requirePath(repositoryRoot, `${relativePath}.meta`, errors);
        visit(fullPath);
      } else if ([".cs", ".asmdef"].includes(extname(entry.name))) {
        requirePath(repositoryRoot, `${relativePath}.meta`, errors);
      }
    }
  }

  visit(editorRoot);
}

export function validateRelease(repositoryRoot, requestedVersion) {
  const errors = [];
  if (!STABLE_VERSION.test(requestedVersion)) {
    errors.push(`version must be a stable MAJOR.MINOR.PATCH value without v: ${requestedVersion}`);
  }

  const unityPackage = readJson(repositoryRoot, "package.json", errors);
  if (unityPackage?.name !== EXPECTED_NAME) {
    errors.push(`package.json: name must be ${EXPECTED_NAME}`);
  }
  if (unityPackage?.version !== requestedVersion) {
    errors.push(
      `package.json: version must be ${requestedVersion}, got ${String(unityPackage?.version)}`,
    );
  }
  if (unityPackage?.license !== "MIT") {
    errors.push("package.json: license must be MIT");
  }
  if (unityPackage?.licensesUrl !== EXPECTED_LICENSE_URL) {
    errors.push(`package.json: licensesUrl must be ${EXPECTED_LICENSE_URL}`);
  }
  if (unityPackage?.unity !== EXPECTED_UNITY) {
    errors.push(`package.json: unity must be ${EXPECTED_UNITY}`);
  }

  try {
    const license = readFileSync(resolve(repositoryRoot, "LICENSE"), "utf8");
    if (!license.includes("MIT License")) errors.push("LICENSE: MIT License heading is missing");
    if (!license.includes(EXPECTED_COPYRIGHT)) errors.push(`LICENSE: ${EXPECTED_COPYRIGHT} is missing`);
  } catch (error) {
    errors.push(`LICENSE: ${error instanceof Error ? error.message : String(error)}`);
  }

  validateMetaCoverage(repositoryRoot, errors);

  if (errors.length > 0) throw new Error(errors.join("\n"));
  return { version: requestedVersion, tag: `v${requestedVersion}` };
}

const invokedPath = process.argv[1] ? resolve(process.argv[1]) : "";
if (invokedPath === fileURLToPath(import.meta.url)) {
  try {
    const repositoryRoot = process.argv[2];
    const requestedVersion = process.argv[3];
    if (!repositoryRoot || !requestedVersion) {
      throw new Error("usage: validate-release.mjs <repository-root> <version>");
    }
    const result = validateRelease(repositoryRoot, requestedVersion);
    console.log(`release-validation=passed version=${result.version} tag=${result.tag}`);
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  }
}
