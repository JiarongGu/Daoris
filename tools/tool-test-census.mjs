/**
 * TOOLTESTCENSUS1: every tool test file is run by exactly one declared gate.
 *
 *   node tools/tool-test-census.mjs     check every tool test file (exit 0 clean, 1 an orphan or a double, 2 tool error)
 *
 * The files held are `tools/*.test.mjs` and every `*.test.ts` under `src/Daoris.Cli/tool-test/`. A gate in
 * `daoris.gates.json` owns a file when its command, with each `npm run` and `npm test` followed into the package script it
 * names, lists the file's path or globs it (`node --test "tool-test/**\/*.ts"`). `as-merged.test.mjs` rotted for weeks
 * outside every gate's list: the verify script names its files one by one, so a new test file that nobody adds stays unrun
 * and nothing says so. This census names each file no gate runs, each run by two gates, and each listed file that is gone.
 */
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join, posix } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';

const CLI = 'src/Daoris.Cli';
const TOOL_TESTS = `${CLI}/tool-test`;

const readJson = (path) => JSON.parse(readFileSync(path, 'utf8'));

/** The package scripts a command may name: the root's, and the CLI package's behind `-w daoris`. */
function scriptsOf(root) {
  return { root: readJson(join(root, 'package.json')).scripts ?? {}, cli: readJson(join(root, CLI, 'package.json')).scripts ?? {} };
}

/**
 * A command with each `npm run <script>` and `npm test` replaced by the script's own text, to a fixed depth. Each piece
 * carries the folder its relative paths are read from: the root, or the CLI package for a script run `-w daoris`.
 */
export function expand(command, scripts, base = '', depth = 0) {
  const pieces = [{ base, text: command }];
  if (depth > 4) return pieces;
  const found = /\bnpm\s+(?:run\s+([\w:-]+)|(test))((?:\s+-w\s+daoris)?)/g;
  for (const m of command.matchAll(found)) {
    const inCli = m[3] !== '';
    const name = m[1] ?? m[2];
    const script = (inCli ? scripts.cli : scripts.root)[name];
    if (script !== undefined) pieces.push(...expand(script, scripts, inCli ? CLI : base, depth + 1));
  }
  return pieces;
}

const globPattern = (glob) => new RegExp(`^${glob
  .replace(/[.+^${}()|[\]\\]/g, '\\$&')
  .replace(/\*\*\//g, '\u0000')
  .replace(/\*/g, '[^/]*')
  .replace(/\u0000/g, '(?:.*/)?')}$`);

/** The repository-relative paths a command's pieces list (`tools/x.test.mjs`) or glob (`"tool-test/**\/*.ts"`). */
export function coverage(pieces) {
  const listed = new Set();
  const globs = [];
  for (const { base, text } of pieces) {
    // A script run in the CLI package may name a root file through `..` (`../../tools/x.test.mjs`): read it as the root's.
    const at = (path) => posix.normalize(base ? `${base}/${path}` : path);
    for (const m of text.matchAll(/(?<![\w./-])((?:[\w.*-]+\/)+[\w.*-]+\.(?:mjs|ts))\b/g)) {
      if (m[1].includes('*')) globs.push(globPattern(at(m[1])));
      else listed.add(at(m[1]));
    }
  }
  return { listed, globs };
}

/** Every tool test file, repository-relative. */
export function toolTestFiles(root) {
  const files = readdirSync(join(root, 'tools')).filter((n) => n.endsWith('.test.mjs')).map((n) => `tools/${n}`);
  const walk = (dir) => {
    for (const entry of readdirSync(join(root, dir), { withFileTypes: true })) {
      if (entry.isDirectory()) walk(`${dir}/${entry.name}`);
      else if (entry.name.endsWith('.test.ts')) files.push(`${dir}/${entry.name}`);
    }
  };
  if (existsSync(join(root, TOOL_TESTS))) walk(TOOL_TESTS);
  return files.sort();
}

/** The census over the tree at `root`: `{ problems, count }`, one problem for each orphan, double or stale entry. */
export function checkToolTests(root) {
  const scripts = scriptsOf(root);
  const gates = readJson(join(root, 'daoris.gates.json')).gates ?? [];
  const covers = gates.map((gate) => ({ name: gate.name, ...coverage(expand(gate.run, scripts)) }));
  const files = toolTestFiles(root);
  const problems = [];
  for (const file of files) {
    const owners = covers.filter((c) => c.listed.has(file) || c.globs.some((g) => g.test(file))).map((c) => c.name);
    if (owners.length === 0) problems.push(`${file}: no gate runs it (add it to the verify script's \`node --test\` list, or to a gate that globs it)`);
    else if (owners.length > 1) problems.push(`${file}: run by ${owners.length} gates (${owners.join(', ')}); keep it in one`);
  }
  const known = new Set(files);
  for (const c of covers) {
    for (const path of c.listed) {
      const ours = path.startsWith('tools/') || path.startsWith(`${TOOL_TESTS}/`);
      if (ours && /\.test\.(mjs|ts)$/.test(path) && !known.has(path) && !existsSync(join(root, path))) {
        problems.push(`${path}: listed by gate ${c.name} and not there`);
      }
    }
  }
  return { problems, count: files.length };
}

function main() {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  try {
    const { problems, count } = checkToolTests(root);
    for (const p of problems) console.error(`tool-test-census: ${p}`);
    if (problems.length) return 1;
    console.log(`tool-test-census: clean (${count} tool test files, each in one gate)`);
    return 0;
  } catch (e) {
    console.error(`tool-test-census: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main();
