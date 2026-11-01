/**
 * CONSOLEWINDOWS1: every child process the tools, the CLI and the web's scripts start hides its console window.
 *
 *   node tools/console-windows.mjs        check the scripts (exit 0 clean, 1 a start with no `windowsHide`, 2 tool error)
 *
 * A console program started from a parent with no console of its own (the merge queue's detached runner is one, and its gates run
 * `dotnet test`, git and node beneath it) is given a new visible window on the owner's screen unless the start says
 * `windowsHide: true`. The census reads every `spawn`, `spawnSync`, `execFile`, `execFileSync`, `exec` and `execSync` call, bare
 * or through a namespace, in each script, test and fixture source that names `child_process`, and refuses a call whose
 * arguments carry no `windowsHide`. A start that opens a window a person is meant to see marks its line, or the line above it,
 * `// console-window: <why>`, and the clean line counts the marks. A call inside a comment line is not one.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';

/** The folders read, against the repository root. */
export const FOLDERS = ['tools', 'src/Daoris.Cli', 'src/Daoris.Web', 'examples'];
const SKIPPED = new Set(['node_modules', 'dist', 'bin', 'obj', 'wwwroot', 'local', '_fixtures', '.git', 'test-results', 'playwright-report']);
/** The census's own files hold the calls it refuses, as the text of its cases. */
const OWN = new Set(['console-windows.mjs', 'console-windows.test.mjs']);
export const MARK = '// console-window:';
const CALL = /(?<![\w$.])(?:cp\.|childProcess\.)?(spawnSync|spawn|execFileSync|execFile|execSync|exec)\s*\(/g;

function* scripts(dir) {
  let entries;
  try {
    entries = readdirSync(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const entry of entries) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!SKIPPED.has(entry.name)) yield* scripts(full);
    } else if (/\.(?:mjs|cjs|js|ts|tsx)$/.test(entry.name) && !OWN.has(entry.name)) {
      yield full;
    }
  }
}

/** The index of the `)` closing the `(` at `open` in `src`, or the end of `src`. */
function closing(src, open) {
  let depth = 0;
  for (let i = open; i < src.length; i++) {
    if (src[i] === '(') depth++;
    else if (src[i] === ')' && --depth === 0) return i;
  }
  return src.length;
}

/**
 * Each child-process call in `src` whose arguments carry no `windowsHide` and whose line is not marked:
 * `{ line, fn }`, with the marked ones counted in `marked`.
 */
export function unhidden(src) {
  const found = [];
  let marked = 0;
  if (!src.includes('child_process')) return { found, marked };
  const lines = src.split('\n');
  for (const m of src.matchAll(CALL)) {
    const line = src.slice(0, m.index).split('\n').length;
    const text = lines[line - 1].trimStart();
    if (text.startsWith('//') || text.startsWith('*') || text.startsWith('/*')) continue;
    // A declaration (`function spawn(`, `async spawn(`) is no call.
    if (/\b(?:function|async)\s+$/.test(src.slice(Math.max(0, m.index - 12), m.index))) continue;
    const open = m.index + m[0].length - 1;
    if (src.slice(open, closing(src, open) + 1).includes('windowsHide')) continue;
    if (lines[line - 1].includes(MARK) || (lines[line - 2] ?? '').includes(MARK)) {
      marked++;
      continue;
    }
    found.push({ line, fn: m[1] });
  }
  return { found, marked };
}

/** The breaches under `root` (`{ problems, marked, calls }`): a child start with no `windowsHide`. */
export function checkConsoleWindows(root, folders = FOLDERS) {
  const problems = [];
  let marked = 0;
  for (const folder of folders) {
    for (const file of scripts(join(root, folder))) {
      const result = unhidden(readFileSync(file, 'utf8'));
      marked += result.marked;
      const rel = file.slice(root.length + 1).replaceAll('\\', '/');
      for (const { line, fn } of result.found) {
        problems.push(`${rel}:${line}: ${fn} without windowsHide: pass \`windowsHide: true\`, or mark the line \`${MARK} <why a window is meant>\``);
      }
    }
  }
  return { problems, marked };
}

function main() {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  try {
    const { problems, marked } = checkConsoleWindows(root);
    for (const p of problems) console.error(`console-windows: ${p}`);
    if (problems.length) return 1;
    console.log(`console-windows: clean (${marked} marked windows)`);
    return 0;
  } catch (e) {
    console.error(`console-windows: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main();
