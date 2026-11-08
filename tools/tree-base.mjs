/**
 * The guard of a kept build base (the tree-pool design §36.3, 3, §38.2, 6-7): a runner that reads a build output without building
 * reads `.daoris-base` in the folders from the one it reads up to the checkout's top, and refuses with the marker's sentence while
 * one stands; the door, `daoris tree build <path>`, is the one release. `build` is the one integrated builder: it builds a
 * project through the door where a marker stands under the configuration's output, and plainly where none does.
 *
 *   node tools/tree-base.mjs build <project folder> [--configuration Debug|Release]
 *
 * `sites` lists every call site of the tracked code that reads a build output (the census's two shapes), which
 * `tools/tree-base.test.mjs` holds each guarded or exempt for a reason.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { isMain } from './fsx.mjs';

export const MARKER = '.daoris-base';

/** The first marker in `path`'s folder or one above it, up to and including the checkout's top; null where none stands. */
export function baseAbove(path) {
  let folder = resolve(path);
  if (!isFolder(folder)) folder = dirname(folder);
  for (;;) {
    const marker = join(folder, MARKER);
    if (existsSync(marker)) return { folder, ...readMarker(marker) };
    if (existsSync(join(folder, '.git'))) return null;
    const up = dirname(folder);
    if (up === folder) return null;
    folder = up;
  }
}

/**
 * `path` where no marker stands above it; else the refusal, the marker's sentence, said and the run ended with 1. An empty or
 * missing path passes as it is, so an optional build still reads as not built.
 */
export function guarded(path, { refuse = refuseAndExit } = {}) {
  if (!path) return path;
  const standing = baseAbove(path);
  return standing ? refuse(`${path}: ${standing.says}`) : path;
}

/** The one integrated builder: the door where a marker stands under the project's configuration folder, else `dotnet build`. */
export function build(project, configuration = 'Debug', { runner = runOrExit } = {}) {
  const output = join(project, 'bin', configuration);
  if (existsSync(join(output, MARKER))) return runner('daoris', ['tree', 'build', output], { shell: process.platform === 'win32' });
  return runner('dotnet', ['build', project, '-c', configuration]);
}

/** What `build` runs, as one command line, for a runner that takes one. */
export function buildCommand(project, configuration = 'Debug') {
  const output = join(project, 'bin', configuration);
  return existsSync(join(output, MARKER)) ? `daoris tree build "${output}"` : `dotnet build "${project}" -c ${configuration}`;
}

/** The census's sites: every call that reads a build output, in the tracked code of `tools/` and `src/` and the root scripts. */
export function sites(root, files = trackedFiles(root)) {
  const found = [];
  const seen = new Map();
  const add = (file, text, shape, whole = text) => {
    const statement = text.trim().replace(/\s+/g, ' ');
    const key = `${file} :: ${statement}`;
    const count = (seen.get(key) ?? 0) + 1;
    seen.set(key, count);
    found.push({ file, statement: count === 1 ? statement : `${statement} #${count}`, shape, whole: whole.replace(/\s+/g, ' ') });
  };
  for (const file of files) {
    const lines = withoutComments(readFileSync(join(root, file), 'utf8'), file).split('\n');
    const test = /(\.test\.|[/\\]test[/\\]|Tests[/\\]|tool-test[/\\])/.test(file);
    for (const [at, line] of lines.entries()) {
      if (R1.some((shape) => shape.test(line))) add(file, line, 'R1', statementAt(lines, at));
      if (!test && /--no-build/.test(line)) add(file, line, 'R2', statementAt(lines, at));
    }
  }
  const scripts = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8')).scripts ?? {};
  for (const [name, script] of Object.entries(scripts)) {
    if (/--no-build/.test(script)) add('package.json', `scripts.${name}: --no-build`, 'R2');
    if (/\bdotnet build\b/.test(script)) add('package.json', `scripts.${name}: dotnet build`, 'R1');
  }
  return found;
}

/** A `bin` part followed by a Debug or Release literal or a name; a `bin/Debug` string; a call of a resolver that makes one. */
const R1 = [
  /['"]bin['"]\s*,\s*(?:['"](?:debug|release)['"]|[A-Za-z_][\w.]*(?![\w.]*\s*\()(?=\s*[,)]))/i,
  /bin[/\\]{1,2}(?:debug|release)\b/i,
  /(?<![\w.])(?:assemblyExe|targetExe|ownBuilt)\s*\((?!\s*projectDir\s*,\s*\{)/,
];

/** The line with the lines of its statement before it, so a guard opening a join broken over lines is read with it. */
function statementAt(lines, at) {
  let from = at;
  while (from > 0 && !/[;{}]\s*$/.test(lines[from - 1]) && at - from < 4) from--;
  return lines.slice(from, at + 1).join(' ');
}

/** The text with its comments blanked, line for line, so a site's text is its code alone. */
function withoutComments(text, file) {
  const hash = file.endsWith('.ps1');
  return text.replace(/\/\*[\s\S]*?\*\//g, (block) => block.replace(/[^\n]/g, ' '))
    .split('\n').map((line) => {
      const trimmed = line.trimStart();
      if (trimmed.startsWith('//') || trimmed.startsWith('*') || (hash && trimmed.startsWith('#'))) return '';
      return line.replace(/\s\/\/\s.*$/, '');
    }).join('\n');
}

function trackedFiles(root) {
  const listed = spawnSync('git', ['ls-files', '-z', '--', 'tools', 'src'], { cwd: root, encoding: 'utf8' });
  if (listed.status !== 0) throw new Error(`git could not list the tracked files: ${listed.stderr}`);
  return listed.stdout.split('\0').filter((file) => /\.(mjs|js|ts|cs|ps1)$/.test(file) && !file.includes('node_modules/'));
}

function readMarker(marker) {
  try {
    const { from, says } = JSON.parse(readFileSync(marker, 'utf8'));
    return { from: from ?? '', says: says ?? '' };
  } catch {
    return { from: '', says: "this output is the previous line's build; run `daoris tree build` first" };
  }
}

function isFolder(path) {
  try {
    return statSync(path).isDirectory();
  } catch {
    return false;
  }
}

function refuseAndExit(message) {
  console.error(message);
  process.exit(1);
}

function runOrExit(command, args, options = {}) {
  const ran = spawnSync(command, args, { stdio: 'inherit', ...options });
  if (ran.status !== 0) process.exit(ran.status ?? 2);
  return ran.status;
}

if (isMain(import.meta.url)) {
  const [verb, project, ...rest] = process.argv.slice(2);
  if (verb !== 'build' || !project) {
    console.error('usage: node tools/tree-base.mjs build <project folder> [--configuration Debug|Release]');
    process.exit(2);
  }
  const at = rest.indexOf('--configuration');
  build(project, at >= 0 ? rest[at + 1] : 'Debug');
}
