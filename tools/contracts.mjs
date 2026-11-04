#!/usr/bin/env node
/**
 * The page's types for the service's routes, written from what `src/Daoris.Contracts` declares by `tools/contracts/`, so a
 * route's path and wire names are written once, in its record, and the page's typecheck finds a field that is gone.
 *
 *   node tools/contracts.mjs              build and run the generator, writing src/Daoris.Web/src/data/generated/service.ts
 *   node tools/contracts.mjs --if-stale   the same, only where the file is missing or stale (the web's pre-scripts)
 *   node tools/contracts.mjs --check      say whether it is missing or stale, building nothing (`npm run quick`, `verify`)
 *
 * The file is ignored and written in each checkout. Its first line stamps a hash of the declaring sources, the contracts and
 * the generator, read here in Node with LF endings, so a check needs no .NET and a CRLF checkout stamps what the runner does.
 *
 * Exit codes: 0 written or fresh · 1 missing or stale · 2 a tool error (usage, no `dotnet`, the generator failed).
 */
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';

export const OUTPUT = 'src/Daoris.Web/src/data/generated/service.ts';
export const SOURCES = Object.freeze(['src/Daoris.Contracts/', 'tools/contracts/']);
const GENERATOR = 'tools/contracts';
const SOURCE_FILE = /\.(?:cs|csproj)$/;
const SKIPPED = new Set(['bin', 'obj']);
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
      } else if (SOURCE_FILE.test(entry.name)) {
        files.push(path);
      }
    }
  };
  for (const folder of SOURCES) walk(folder);
  return files;
}

/** The hash of the declaring sources: each path and its text with LF endings, in code-unit order; `read` is a test's. */
export function sourceHash(files, read) {
  const hash = createHash('sha256');
  for (const path of [...files].filter((file) => SOURCES.some((folder) => file.startsWith(folder))
    && !file.split('/').some((part) => SKIPPED.has(part)) && SOURCE_FILE.test(file)).sort()) {
    hash.update(`${path}\0`).update(read(path).replace(/\r\n/g, '\n')).update('\0');
  }
  return hash.digest('hex');
}

/** Why the written file is not the sources' (`text` null for none), or null where it is. */
export function staleness(text, hash) {
  if (text === null) return `${OUTPUT} is not written: run \`${WRITE}\``;
  const stamped = STAMP.exec(text.slice(0, text.indexOf('\n') < 0 ? text.length : text.indexOf('\n')))?.[1];
  if (stamped === hash) return null;
  return `${OUTPUT} is stale: the contracts changed since it was written. Run \`${WRITE}\``;
}

function standing(root) {
  const hash = sourceHash(sourceFiles(root), (path) => readFileSync(join(root, path), 'utf8'));
  const output = join(root, OUTPUT);
  return { hash, why: staleness(existsSync(output) ? readFileSync(output, 'utf8') : null, hash) };
}

/** Build and run the generator. No build server outlives it, so a pre-script leaves nothing running. */
function generate(root, hash) {
  try {
    execFileSync('dotnet', ['run', '--project', GENERATOR, '--', '--out', join(root, OUTPUT), '--stamp', hash], {
      cwd: root, stdio: ['ignore', 'pipe', 'inherit'], windowsHide: true, maxBuffer: 64 * 1024 * 1024,
      env: { ...process.env, MSBUILDDISABLENODEREUSE: '1', DOTNET_CLI_USE_MSBUILD_SERVER: '0', UseSharedCompilation: 'false' },
    });
    return true;
  } catch (error) {
    process.stdout.write(error.stdout ?? '');
    console.error(error.code === 'ENOENT'
      ? 'contracts: no `dotnet` on the PATH, and the page\'s types are written by a .NET tool: install the .NET SDK'
      : 'contracts: the generator failed; its own words are above');
    return false;
  }
}

function main(argv) {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  const mode = argv[0] ?? '--write';
  if (argv.length > 1 || !['--write', '--if-stale', '--check'].includes(mode)) {
    console.error('usage: node tools/contracts.mjs [--if-stale | --check]');
    return 2;
  }
  const { hash, why } = standing(root);
  if (mode === '--check') {
    if (why) console.error(`contracts: ${why}`);
    return why ? 1 : 0;
  }
  if (mode === '--if-stale' && !why) return 0;
  if (!generate(root, hash)) return 2;
  console.log(`contracts: wrote ${OUTPUT}`);
  return 0;
}

if (isMain(import.meta.url)) process.exit(main(process.argv.slice(2)));
