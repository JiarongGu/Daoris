#!/usr/bin/env node
/**
 * The page's types, written by `tools/contracts/` from what `src/Daoris.Contracts` declares (the service's routes) and from the
 * window routes' marks in the desktop modules, so a route's name, path and wire names are written once, beside its handler, and
 * the page's typecheck finds one that is gone. The service types' file also carries the node kinds and the provider censuses' table, read
 * off the driver's kit and its provider registrations.
 *
 *   node tools/contracts.mjs              build and run the generator, writing both OUTPUTS
 *   node tools/contracts.mjs --if-stale   the same, only where a file is missing or stale (the web's pre-scripts)
 *   node tools/contracts.mjs --check      say whether one is missing or stale, building nothing (`npm run quick`, `verify`)
 *
 * The files are ignored and written in each checkout. Each one's first line stamps a hash of the declaring sources, the
 * contracts, the desktop modules and the generator, read here in Node with LF endings, so a check needs no .NET and a CRLF
 * checkout stamps what the runner does. A payload or answer record the window routes name is declared in the modules, so the
 * hash covers it.
 *
 * Exit codes: 0 written or fresh · 1 missing or stale · 2 a tool error (usage, no `dotnet`, the generator failed).
 */
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';
import { withRunLock } from './run-lock.mjs';

export const OUTPUTS = Object.freeze({
  service: 'src/Daoris.Web/src/data/generated/service.ts',
  routes: 'src/Daoris.Web/src/bridge/generated/routes.ts',
});
// The driver's settings list is read alone of the driver library, so a driver change stales no page file.
export const SOURCES = Object.freeze([
  'src/Daoris.Contracts/', 'src/Daoris.Desktop/Daoris.Desktop.Modules/', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Settings/List/', 'tools/contracts/',
]);
// The node kinds' list is read from the kit, by the files that declare a kind's reading, its step and its enums' words, named here so a
// change to a kind's door or its checks stales no page file.
export const KINDS_FOLDER = 'src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Kinds/';
const KIND_FILE = /(?:Node|Step|NodeGroup|NodeLook|PullRequestReading)\.cs$/;
// The provider censuses' table is read off the registrations: the list, its reader, and each provider's own files, whose constants its
// vocabulary names; a provider's layer, its stand-in's program and its pending cases stale no page file.
export const PROVIDER_FOLDERS = Object.freeze([
  'src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Providers/', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Registry/',
]);
const PROVIDER_FILE = /^src\/Daoris\.Desktop\/Daoris\.Desktop\.Driver\/Agents\/(?:Providers\/[^/]+\/[^/]+|Registry\/Providers|Registry\/ProviderVocabulary)\.cs$/;
const GENERATOR = 'tools/contracts';
const SOURCE_FILE = /\.(?:cs|csproj)$/;
const SKIPPED = new Set(['bin', 'obj']);

/** Whether a repository path is a declaring source: under a source folder, or a kind's or a provider's declaring file by its name. */
const declares = (file) => !file.split('/').some((part) => SKIPPED.has(part))
  && (SOURCES.some((folder) => file.startsWith(folder)) && SOURCE_FILE.test(file) || file.startsWith(KINDS_FOLDER) && KIND_FILE.test(file)
    || PROVIDER_FILE.test(file));
const STAMP = /^\/\/ contracts: ([0-9a-f]{64})\r?$/;
const WRITE = 'node tools/contracts.mjs';

/** Every declaring source under `root`, as repository paths with `/`, build output skipped. */
export function sourceFiles(root) {
  const files = [];
  const walk = (folder) => {
    let entries;
    try {
      entries = readdirSync(join(root, folder), { withFileTypes: true });
    } catch {
      return;
    }
    for (const entry of entries) {
      const path = `${folder}${entry.name}`;
      if (entry.isDirectory()) {
        if (!SKIPPED.has(entry.name)) walk(`${path}/`);
      } else if (declares(path)) {
        files.push(path);
      }
    }
  };
  for (const folder of [...SOURCES, KINDS_FOLDER, ...PROVIDER_FOLDERS]) walk(folder);
  return files;
}

/** The hash of the declaring sources: each path and its text with LF endings, in code-unit order; `read` is a test's. */
export function sourceHash(files, read) {
  const hash = createHash('sha256');
  for (const path of [...files].filter(declares).sort()) {
    hash.update(`${path}\0`).update(read(path).replace(/\r\n/g, '\n')).update('\0');
  }
  return hash.digest('hex');
}

/** Why the file written at `output` is not the sources' (`text` null for none), or null where it is. */
export function staleness(text, hash, output) {
  if (text === null) return `${output} is not written: run \`${WRITE}\``;
  const stamped = STAMP.exec(text.slice(0, text.indexOf('\n') < 0 ? text.length : text.indexOf('\n')))?.[1];
  if (stamped === hash) return null;
  return `${output} is stale: the contracts changed since it was written. Run \`${WRITE}\``;
}

function standing(root) {
  const hash = sourceHash(sourceFiles(root), (path) => readFileSync(join(root, path), 'utf8'));
  const why = Object.values(OUTPUTS)
    .map((output) => staleness(existsSync(join(root, output)) ? readFileSync(join(root, output), 'utf8') : null, hash, output))
    .find((reason) => reason !== null) ?? null;
  return { hash, why };
}

/**
 * Where the generator builds: an output of its own, `_fixtures/contracts-build/`, by the SDK's `--artifacts-path`, which
 * puts every project the build reaches, the contracts and the desktop modules it references too, under it and none under their
 * folders. The web gates' pre-scripts build it beside the service's and the desktop's gates, which build those projects into
 * their own `obj/`; two builds of one project there fight over its files.
 */
export const OWN_OUTPUT = '_fixtures/contracts-build';

/** The generator's build and the program it built, under `output`: the SDK's artifacts layout, `bin/<project>/debug/`. */
export const generatorBuild = (output) => ({
  build: ['build', GENERATOR, '--artifacts-path', output, '-v', 'q', '-nologo'],
  built: join(output, 'bin', 'ContractsGen', 'debug', 'contracts-gen.dll'),
});

/** Build and run the generator. No build server outlives it, so a pre-script leaves nothing running. */
function generate(root, hash) {
  const options = {
    cwd: root, stdio: ['ignore', 'pipe', 'inherit'], maxBuffer: 64 * 1024 * 1024,
    env: { ...process.env, MSBUILDDISABLENODEREUSE: '1', DOTNET_CLI_USE_MSBUILD_SERVER: '0', UseSharedCompilation: 'false' },
  };
  try {
    const { build, built } = generatorBuild(join(root, OWN_OUTPUT));
    execFileSync('dotnet', build, { ...options, windowsHide: true });
    execFileSync('dotnet', [built, '--out', join(root, OUTPUTS.service), '--routes', join(root, OUTPUTS.routes), '--stamp', hash], { ...options, windowsHide: true });
    return true;
  } catch (error) {
    process.stdout.write(error.stdout ?? '');
    console.error(error.code === 'ENOENT'
      ? 'contracts: no `dotnet` on the PATH, and the page\'s types are written by a .NET tool: install the .NET SDK'
      : 'contracts: the generator failed; its own words are above');
    return false;
  }
}

/** Where the generator's runs take their turns, beside its output (`tools/run-lock.mjs`). */
export const LOCK = `${OWN_OUTPUT}.lock`;

/**
 * Write the files, one run at a time: the web gates' pre-scripts start together, and two builds into the one output fight over its
 * files (CS2012), as two generators would over the files they write. A run that waited reads the files again, and builds nothing
 * where the run before it wrote them. `build` and `look` are a test's.
 */
export function writeContracts(root, mode, { build = generate, look = standing } = {}) {
  return withRunLock(join(root, LOCK), () => {
    const { hash, why } = look(root);
    if (mode === '--if-stale' && !why) return 0;
    if (!build(root, hash)) return 2;
    console.log(`contracts: wrote ${Object.values(OUTPUTS).join(' and ')}`);
    return 0;
  });
}

function main(argv) {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  const mode = argv[0] ?? '--write';
  if (argv.length > 1 || !['--write', '--if-stale', '--check'].includes(mode)) {
    console.error('usage: node tools/contracts.mjs [--if-stale | --check]');
    return 2;
  }
  const { why } = standing(root);
  if (mode === '--check') {
    if (why) console.error(`contracts: ${why}`);
    return why ? 1 : 0;
  }
  if (mode === '--if-stale' && !why) return 0;
  return writeContracts(root, mode);
}

if (isMain(import.meta.url)) process.exit(main(process.argv.slice(2)));
