// Regenerates the OpenAPI contract from the C# project, then the TypeScript API client from that contract.
//   node scripts/api-generate.mjs            write web/openapi + web/src/api/generated
//   node scripts/api-generate.mjs --check    regenerate into scratch folders and fail if the committed files differ
// Optional: --no-restore (used when called from inside `dotnet publish`, which already restored).
import { spawnSync } from "node:child_process";
import {
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  rmSync,
} from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const webDir = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const serverProject = resolve(webDir, "..", "server", "Afterpelago.csproj");
const check = process.argv.includes("--check");
const noRestore = process.argv.includes("--no-restore");

const committedSpec = join(webDir, "openapi", "afterpelago.json");
const committedClient = join(webDir, "src", "api", "generated");
const scratchRoot = join(webDir, ".api-check");
const scratchClientRel = "src/api/.generated-check";
const scratchClient = join(webDir, ...scratchClientRel.split("/"));

function run(command, args, env = {}) {
  const result = spawnSync(command, args, {
    cwd: webDir,
    stdio: "inherit",
    env: { ...process.env, ...env },
  });
  if (result.error) throw result.error;
  if (result.status !== 0) {
    console.error(
      `\n${command} ${args.join(" ")} failed with exit code ${result.status}`,
    );
    process.exit(result.status ?? 1);
  }
}

function listFiles(dir) {
  if (!existsSync(dir)) return [];
  return readdirSync(dir, { recursive: true, withFileTypes: true })
    .filter((e) => e.isFile())
    .map((e) => relative(dir, join(e.parentPath, e.name)).replaceAll("\\", "/"))
    .sort();
}

const normalize = (file) => readFileSync(file, "utf8").replaceAll("\r\n", "\n");

function compare(label, expectedDir, actualDir) {
  const expected = listFiles(expectedDir);
  const actual = listFiles(actualDir);
  const problems = [];
  for (const f of new Set([...expected, ...actual])) {
    if (!expected.includes(f))
      problems.push(`${label}: unexpected generated file ${f}`);
    else if (!actual.includes(f))
      problems.push(`${label}: missing generated file ${f}`);
    else if (normalize(join(expectedDir, f)) !== normalize(join(actualDir, f)))
      problems.push(`${label}: ${f} is out of date`);
  }
  return problems;
}

function buildSpec(outputDir) {
  // --no-incremental forces the generator to run even when the assembly is unchanged.
  const args = [
    "build",
    serverProject,
    "-c",
    "Release",
    "--no-incremental",
    "--nologo",
    "-v",
    "q",
    "-p:OpenApiGenerateDocumentsOnBuild=true",
    `-p:OpenApiDocumentsDirectory=${outputDir}`,
  ];
  if (noRestore) args.push("--no-restore");
  run("dotnet", args);
}

function runOrval(input, out) {
  const orval = fileURLToPath(import.meta.resolve("orval/bin/orval"));
  run(process.execPath, [orval, "--config", "orval.config.ts"], {
    ORVAL_INPUT: input,
    ORVAL_OUT: out,
  });
}

if (!check) {
  console.log("==> Generating OpenAPI contract from C#");
  buildSpec(join(webDir, "openapi"));
  console.log("==> Generating TypeScript client with Orval");
  runOrval("./openapi/afterpelago.json", "src/api/generated");
  process.exit(0);
}

rmSync(scratchRoot, { recursive: true, force: true });
rmSync(scratchClient, { recursive: true, force: true });
try {
  console.log("==> Checking contract and client are up to date");
  const scratchSpecDir = join(scratchRoot, "openapi");
  mkdirSync(scratchSpecDir, { recursive: true });
  buildSpec(scratchSpecDir);
  runOrval(`./.api-check/openapi/afterpelago.json`, scratchClientRel);

  const problems = [
    ...compare("openapi", join(webDir, "openapi"), scratchSpecDir),
    ...compare("client", committedClient, scratchClient),
  ];
  if (problems.length > 0) {
    console.error(
      "\nGenerated API files are stale. Run `npm run api:generate` and commit the result.\n" +
        problems.map((p) => `  - ${p}`).join("\n"),
    );
    process.exit(1);
  }
  console.log("Contract and generated client are up to date.");
} finally {
  rmSync(scratchRoot, { recursive: true, force: true });
  rmSync(scratchClient, { recursive: true, force: true });
}
