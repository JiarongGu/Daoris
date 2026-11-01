/**
 * CONSOLEWINDOWS1: the console-windows census.
 *
 *   node --test tools/console-windows.test.mjs
 *
 * Each case holds the verdict on a small source: a child start with no `windowsHide` is refused, naming its line; one with it, a
 * marked one, a call in a comment, a regular expression's `exec` and a file that starts nothing pass. The last case holds the
 * repository's own scripts clean.
 */
import assert from 'node:assert/strict';
import { dirname } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkConsoleWindows, unhidden } from './console-windows.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const head = "import { spawnSync, spawn } from 'node:child_process';\n";

test('a start with no windowsHide is refused, naming its line and its function', () => {
  const { found } = unhidden(`${head}spawnSync('git', ['status'], { cwd });\n`);
  assert.deepEqual(found, [{ line: 2, fn: 'spawnSync' }]);
});

test('every shape of a start is read: spawn, exec, execFile, a namespace, a call over lines, and no options at all', () => {
  for (const call of [
    "spawn('node', ['x.mjs'], { stdio: 'ignore' })",
    "execSync('npm pack', { cwd })",
    "execFileSync('git', ['log'])",
    "cp.exec('dir', () => {})",
    "spawnSync(process.execPath, [script],\n  { encoding: 'utf8' })",
    "spawn(\n  'dotnet',\n  [dll],\n)",
  ]) {
    assert.equal(unhidden(`${head}${call};\n`).found.length, 1, call);
  }
});

test('a start that says windowsHide passes, wherever the option sits in its arguments', () => {
  for (const call of [
    "spawnSync('git', ['status'], { cwd, windowsHide: true })",
    "spawn('node', [x], {\n  stdio: 'ignore',\n  windowsHide: true,\n  cwd,\n})",
    "execFileSync('git', args, { windowsHide: true, ...options })",
  ]) {
    assert.deepEqual(unhidden(`${head}${call};\n`).found, [], call);
  }
});

test('a start marked as a window on purpose passes and is counted, on its line or the one above', () => {
  const above = unhidden(`${head}// console-window: the application's own window\nspawn(exe, [], { detached: true });\n`);
  assert.deepEqual(above, { found: [], marked: 1 });
  const beside = unhidden(`${head}spawn(exe); // console-window: the application's own window\n`);
  assert.deepEqual(beside, { found: [], marked: 1 });
});

test('a call in a comment, a regular expression match, a declaration and a file with no child_process pass', () => {
  assert.deepEqual(unhidden(`${head}// spawnSync('git', args)\n * spawn('node')\nconst m = /x/.exec(text);\nfunction spawn(a) {}\n`).found, []);
  assert.deepEqual(unhidden("const run = spawn('node');\n").found, []);
});

test("the repository's own scripts start every child with its window hidden or a mark saying why not", () => {
  const { problems, marked } = checkConsoleWindows(root);
  assert.deepEqual(problems, []);
  assert.ok(marked >= 4, `expected the windows kept on purpose to be marked, found ${marked}`);
});
