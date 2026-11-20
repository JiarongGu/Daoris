/**
 * Every test file is in a declared test set, every set holds one and is run by a declared gate, and no script names a test file.
 *
 *   node tools/tool-test-census.mjs     check every test file and set (exit 0 clean, 1 a problem, 2 tool error)
 *
 * The files held are those named as tests (`*.test.mjs`, `*.test.ts`, `*.test.tsx`, `*.e2e.ts`, `*.spec.ts`) under `tools/`, the
 * CLI package and the page, wherever they sit; the sets are `daoris.gates.json`'s `tests` (`tools/test-sets.mjs`). A test file is
 * placed by a set's pattern, so a new one is run with no list to edit; a script or a gate's command that names one would be a
 * list again, and is refused. A set whose runner runs its own config (Vitest, Playwright) runs every file its patterns hold, so
 * one that holds an earlier set's file would run it twice; a pick of another set (`of`) names files that set holds.
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
