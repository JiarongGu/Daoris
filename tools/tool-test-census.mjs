/**
 * Every test file is in a declared test set, every set holds one and is run by a declared gate, and no script names a test file.
 *
 *   node tools/tool-test-census.mjs     check every test file and set (exit 0 clean, 1 a problem, 2 tool error)
 *
 * The files held are those named as tests (`*.test.mjs`, `*.test.ts`, `*.test.tsx`, `*.e2e.ts`, `*.spec.ts`) under `tools/`, the
 * CLI package and the page, wherever they sit; the sets are `daoris.gates.json`'s `tests` (`tools/test-sets.mjs`). A test file is
 * placed by a set's pattern, so a new one is run with no list to edit; a script or a gate's command that names one would be a
 * list again, and is refused. A set whose runner runs its own config (Vitest, Playwright) runs every file its patterns hold, so
 * one that holds an earlier set's file would run it twice; a pick of another set (`of`) names files that set holds. No test under
 * `tools/` reads the live machine's load (its heavy-run admission or its process list): a call to a reader with no seam is named.
 */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';
import { declaredSets, filesBySet, listFiles, setOf } from './test-sets.mjs';

const TEST_FILE = /\.(?:test\.(?:mjs|ts|tsx)|e2e\.ts|spec\.ts)$/;
const HELD = [{ files: ['tools/**', 'src/Daoris.Cli/**', 'src/Daoris.Web/**'] }];
const SCRIPTS = ['package.json', 'src/Daoris.Cli/package.json', 'src/Daoris.Web/package.json'];

const readJson = (root, path) => {
  try {
    return JSON.parse(readFileSync(join(root, path), 'utf8'));
  } catch {
    return {};
  }
};

/** The test files a command names one by one: a word that is a test file's path, a glob not counted. */
export const namedTests = (command) => command.split(/[\s"'&|;]+/).filter((word) => !word.includes('*') && TEST_FILE.test(word));

const LIVE_HOME = /process\.env|DAORIS_HOME/;
const ADMISSION = { fixture: (args) => args !== '' && !LIVE_HOME.test(args), why: 'the live admission of the Daoris home; a test names a home of its own' };
/** The readers of the machine's load as it stands, each with what makes a call a fixture's: a test's words change with the load it runs beside. */
const LIVE_READS = [
  { name: 'loadSeam', fixture: () => false, why: 'the merge\'s own reading of the machine; a test hands the flow a seam of its own (`load`)' },
  { name: 'readProcesses', fixture: () => false, why: 'the process list of the machine; a test hands `foreignTrees` its `processes`' },
  { name: 'heldNow', ...ADMISSION },
  { name: 'lookAt', ...ADMISSION },
  { name: 'holdersSentence', ...ADMISSION },
  { name: 'foreignTrees', fixture: (args) => /seam|processes/i.test(args), why: 'the process list of the machine; a test hands it `processes`' },
  { name: 'loadSampler', fixture: (args) => /\bcount\b/.test(args), why: 'the live admission and process list; a test hands it its `count`' },
];

/** The text between the parenthesis at `at` and its pair. */
function argumentsAt(text, at) {
  let depth = 0;
  for (let i = at; i < text.length; i++) {
    if (text[i] === '(') depth++;
    else if (text[i] === ')' && --depth === 0) return text.slice(at + 1, i).trim();
  }
  return text.slice(at + 1).trim();
}

/** The calls in a test's `text` that read the live machine's load, each `{ line, name, why }`. */
export function liveLoadReads(text) {
  const found = [];
  for (const { name, fixture, why } of LIVE_READS) {
    for (const match of text.matchAll(new RegExp(`(?<![.\\w$])${name}\\s*\\(`, 'g'))) {
      const start = text.lastIndexOf('\n', match.index) + 1;
      const before = text.slice(start, match.index).trim();
      if (/^(?:\/\/|\*|\/\*)/.test(before) || /(?:^|\s)function\*?$/.test(before)) continue;
      if (!fixture(argumentsAt(text, match.index + match[0].length - 1))) found.push({ line: text.slice(0, match.index).split('\n').length, name, why });
    }
  }
  return found.sort((a, b) => a.line - b.line);
}

/** The census over the tree at `root`: `{ problems, count }`, one problem for each file no set holds and each set or script at fault. */
export function checkToolTests(root) {
  const declaration = readJson(root, 'daoris.gates.json');
  const sets = declaredSets(root);
  const gates = new Set((declaration.gates ?? []).map((gate) => gate.name));
  const files = listFiles(root, [...HELD, ...sets]);
  const tests = files.filter((file) => TEST_FILE.test(file));
  const held = filesBySet(sets, files);
  const problems = [];
  for (const file of tests) if (!setOf(sets, file)) problems.push(`${file}: no test set holds it (add a pattern to a set in daoris.gates.json's tests)`);
  for (const file of tests.filter((each) => each.startsWith('tools/'))) {
    for (const read of liveLoadReads(readFileSync(join(root, file), 'utf8'))) problems.push(`${file}:${read.line}: ${read.name}( reads the live machine, so the test's words change with the load it runs beside: ${read.why}`);
  }
  sets.forEach((set, at) => {
    if (!gates.has(set.gate)) problems.push(`set ${set.name}: run by gate ${set.gate}, which daoris.gates.json does not declare`);
    if (set.of) {
      for (const file of set.files.filter((entry) => !/[*?{[]/.test(entry))) {
        if (setOf(sets, files.includes(file) ? file : '')?.name !== set.of) problems.push(`set ${set.name}: picks ${file}, which set ${set.of} does not hold`);
      }
      return;
    }
    if (held.get(set.name).length === 0) problems.push(`set ${set.name}: holds no test file (no file matches its patterns, or an earlier set holds each)`);
    if (set.runner === 'node') return;
    for (const earlier of sets.slice(0, at).filter((each) => !each.of)) {
      for (const file of held.get(earlier.name)) {
        if (set.patterns.some((pattern) => pattern.test(file))) problems.push(`set ${set.name}: runs by its config, which also runs ${file} of set ${earlier.name}`);
      }
    }
  });
  for (const path of SCRIPTS) {
    for (const [name, command] of Object.entries(readJson(root, path).scripts ?? {})) {
      for (const file of namedTests(command)) problems.push(`${path}: script ${name} names ${file}; place a test file by a set's pattern in daoris.gates.json's tests`);
    }
  }
  for (const gate of declaration.gates ?? []) {
    for (const file of namedTests(gate.run ?? '')) problems.push(`daoris.gates.json: gate ${gate.name} names ${file}; place a test file by a set's pattern in its tests`);
  }
  return { problems, count: tests.length };
}

function main() {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  try {
    const { problems, count } = checkToolTests(root);
    for (const p of problems) console.error(`tool-test-census: ${p}`);
    if (problems.length) return 1;
    console.log(`tool-test-census: clean (${count} test files, each in one declared set)`);
    return 0;
  } catch (e) {
    console.error(`tool-test-census: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main();
