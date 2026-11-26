import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  HELPER_SOURCES, freeMemory, helperStanding, parseMemory, parseProcesses, processStartOf, readProcesses, sourceHash,
} from './machine-read.mjs';
import { FOREIGN_EVERY } from './time-load.mjs';
import { MEMORY_POLL_MS, MEMORY_WAIT_MS } from './merge-branch.mjs';

// The process table and the free memory are read by one in-house reader, which starts no PowerShell and asks no WMI.
// Its real run, on this machine, is `src/Daoris.Cli/tool-test/machine-read.e2e.ts`.

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..');

const helperRows = JSON.stringify([
  { pid: 4, ppid: 0, created: 1700000000000, name: 'System', path: null, command: null },
  { pid: 20, ppid: 4, created: 1700000005250, name: 'node.exe', path: 'C:\\Program Files\\nodejs\\node.exe', command: '"C:\\Program Files\\nodejs\\node.exe" a.mjs "x|y"' },
]);

test('the helper\'s rows are read as pid, parent, creation time in milliseconds, name, path and command, an unread command being empty', () => {
  assert.deepEqual(parseProcesses(helperRows), [
    { pid: 4, ppid: 0, created: 1700000000000, name: 'System', path: null, command: '' },
    { pid: 20, ppid: 4, created: 1700000005250, name: 'node.exe', path: 'C:\\Program Files\\nodejs\\node.exe', command: '"C:\\Program Files\\nodejs\\node.exe" a.mjs "x|y"' },
  ]);
  assert.throws(() => parseProcesses('not json'));
  assert.throws(() => parseProcesses('{}'), /list/);
});

test('the helper\'s memory is read in bytes, and one it leaves out is refused', () => {
  assert.deepEqual(parseMemory('{"physical":1073741824,"commit":2147483648,"total":4294967296}'), { physical: 1073741824, commit: 2147483648, total: 4294967296 });
  assert.throws(() => parseMemory('{"physical":1}'), /commit/);
});

test('on Windows the table is asked of the helper alone, with the pids named, and never of a PowerShell', () => {
  const asked = [];
  const run = (command, args) => { asked.push([command, ...args]); return { status: 0, stdout: helperRows }; };
  const all = readProcesses({ platform: 'win32', run, helper: 'H:\\machine-read.exe' });
  assert.equal(all.length, 2);
  readProcesses({ platform: 'win32', run, helper: () => 'H:\\machine-read.exe', pids: [20, 4] });
  assert.deepEqual(asked, [['H:\\machine-read.exe', 'processes'], ['H:\\machine-read.exe', 'processes', '20', '4']]);
  assert.ok(asked.every(([command]) => !/powershell|pwsh/i.test(command)));
});

test('a helper that fails, will not start or answers what is not a list is an error that says so, never an empty machine', () => {
  const helper = 'H:\\machine-read.exe';
  assert.throws(() => readProcesses({ platform: 'win32', helper, run: () => ({ status: 1, stdout: '', stderr: 'machine-read: denied\n' }) }), /failed: machine-read: denied/);
  assert.throws(() => readProcesses({ platform: 'win32', helper, run: () => ({ error: new Error('ENOENT'), status: null }) }), /could not start: ENOENT/);
  assert.throws(() => readProcesses({ platform: 'win32', helper, run: () => ({ status: 0, stdout: '' }) }), /could not be read/);
});

test('elsewhere the table is ps\'s: pid, parent, start (UTC) and arguments, the name the program\'s file name', () => {
  const asked = [];
  const text = '    1     0 Thu Nov 26 05:47:01 2026 /sbin/init splash\n  200     1 Thu Nov 26 05:47:09 2026 node /srv/a b.mjs\nrubbish\n';
  const rows = readProcesses({ platform: 'linux', run: (command, args) => { asked.push([command, ...args]); return { status: 0, stdout: text }; } });
  assert.equal(asked[0][0], 'ps');
  assert.deepEqual(rows.map(({ pid, ppid, name, command }) => ({ pid, ppid, name, command })), [
    { pid: 1, ppid: 0, name: 'init', command: '/sbin/init splash' },
    { pid: 200, ppid: 1, name: 'node', command: 'node /srv/a b.mjs' },
  ]);
  assert.equal(rows[0].created, Date.parse('2026-11-26T05:47:01Z'));
});

test('free memory is the helper\'s on Windows, and an error says why where it cannot be read', () => {
  const run = () => ({ status: 0, stdout: '{"physical":1,"commit":2,"total":3}' });
  assert.deepEqual(freeMemory({ platform: 'win32', run, helper: 'H' }), { physical: 1, commit: 2, total: 3 });
  assert.throws(() => freeMemory({ platform: 'win32', run: () => ({ status: 1, stderr: 'no' }), helper: 'H' }), /failed: no/);
});

test('one process\'s start is its row\'s creation time, null where it does not run', () => {
  const run = () => ({ status: 0, stdout: helperRows });
  assert.equal(processStartOf(20, { platform: 'win32', run, helper: 'H' }), 1700000005250);
  assert.equal(processStartOf(99, { platform: 'win32', run, helper: 'H' }), null);
});

test('the helper is rebuilt when a source of it changed, and only then', () => {
  const files = new Map(HELPER_SOURCES.map((path) => [path, `// ${path}\r\n`]));
  const read = (path) => files.get(relative(REPO, path).replaceAll('\\', '/'));
  const one = sourceHash(REPO, read);
  assert.equal(sourceHash(REPO, read), one);
  files.set('tools/machine-read/Program.cs', '// changed\n');
  assert.notEqual(sourceHash(REPO, read), one);
  files.set('tools/machine-read/Program.cs', '// tools/machine-read/Program.cs\n');
  assert.equal(sourceHash(REPO, read), one, 'line endings are not a change');
  assert.equal(helperStanding(REPO, { read, stamp: () => one, exists: () => true }).stale, false);
  assert.equal(helperStanding(REPO, { read, stamp: () => 'old', exists: () => true }).stale, true);
  assert.equal(helperStanding(REPO, { read, stamp: () => one, exists: () => false }).stale, true);
  assert.equal(helperStanding(REPO, { read, stamp: () => null, exists: () => true }).stale, true);
});

test('no tool asks Windows for its processes or its memory through a PowerShell or WMI', () => {
  const sources = [];
  const walk = (folder) => {
    for (const name of readdirSync(folder)) {
      const path = join(folder, name);
      if (name === 'node_modules' || name === 'machine-read') continue;
      if (statSync(path).isDirectory()) walk(path);
      else if (/\.(mjs|ts)$/.test(name) && !/\.test\./.test(name)) sources.push(path);
    }
  };
  walk(join(REPO, 'tools'));
  const asking = /Get-CimInstance|Get-WmiObject|Get-NetTCPConnection|Win32_Process|Win32_OperatingSystem|\bwmic\b/;
  const found = sources.filter((path) => asking.test(readFileSync(path, 'utf8'))).map((path) => relative(REPO, path).replaceAll('\\', '/'));
  assert.deepEqual(found, [], 'these read the process table or the memory by PowerShell or WMI: use tools/machine-read.mjs');
});

test('each caller that samples says how often: the foreign-tree read at most every 15 s, the memory gate every 5 s for 10 minutes at most', () => {
  assert.ok(FOREIGN_EVERY >= 15_000);
  assert.ok(MEMORY_POLL_MS >= 5_000);
  assert.ok(MEMORY_WAIT_MS <= 10 * 60_000);
});
