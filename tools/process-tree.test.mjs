import { test } from 'node:test';
import assert from 'node:assert/strict';
import { census, installOf, said } from './process-tree.mjs';

const install = 'D:\\Programs\\Daoris';
const row = (pid, ppid, name, created, line = name, path = null) => ({ pid, ppid, name, path, line, created });

/** A machine: the launcher that started the application, its tree, and a gate run whose shell has gone. */
const machine = () => [
  row(10, 1, 'Daoris.exe', 100, 'Daoris.exe', `${install}\\Daoris.exe`),
  row(20, 10, 'Daoris.Desktop.exe', 110, 'Daoris.Desktop.exe', `${install}\\app\\Daoris.Desktop.exe`),
  row(21, 20, 'Daoris.Desktop.exe', 120, 'Daoris.Desktop.exe --type=renderer', `${install}\\app\\Daoris.Desktop.exe`),
  row(22, 20, 'git.exe', 130),
  row(23, 20, 'Daoris.Desktop.PieceStart.exe', 130),
  row(24, 23, 'claude.exe', 140),
  row(25, 24, 'bash.exe', 150),
  row(26, 25, 'daoris-driver.exe', 160, `"${install}\\app\\daoris-driver\\daoris-driver.exe" gate run`),
  row(40, 999, 'daoris-driver.exe', 200, `"${install}\\app\\daoris-driver\\daoris-driver.exe" gate run`),
  row(41, 40, 'cmd.exe', 210),
  row(42, 41, 'dotnet.exe', 220),
  row(43, 42, 'dotnet.exe', 230),
  row(44, 43, 'testhost.exe', 240),
  row(50, 1, 'explorer.exe', 50),
  row(60, 50, 'notepad.exe', 300),
];

test('an install is the folder above its app folder', () => {
  assert.equal(installOf(`${install}\\app\\Daoris.Desktop.exe`), install);
  assert.equal(installOf('C:\\Program Files\\git\\bin\\git.exe'), null);
});

test('the application and what descends from it are inside; its launcher is neither inside nor outside', () => {
  const read = census(machine());
  assert.deepEqual(read.applications, [{ pid: 20, install, inside: 7 }]);
  assert.deepEqual(read.outside.map((root) => root.pid), [40]);
});

test('a run whose shell has gone is outside, under what started it, with what runs under it', () => {
  const [run] = census(machine()).outside;
  assert.deepEqual(run.startedBy, { pid: 999, name: null });
  assert.deepEqual(run.under, [{ name: 'cmd.exe', count: 1 }, { name: 'dotnet.exe', count: 2 }, { name: 'testhost.exe', count: 1 }]);
  assert.deepEqual(said(census(machine())).slice(1), [
    'process-tree: 5 Daoris processes outside it, by what started them:',
    `  daoris-driver.exe (pid 40), started by pid 999, which no longer runs: "${install}\\app\\daoris-driver\\daoris-driver.exe" gate run; `
      + 'under it: cmd.exe, dotnet.exe ×2, testhost.exe',
  ]);
});

test('a process id used again is no parent: a process older than the one named its parent is not under it', () => {
  const rows = [...machine(), row(70, 20, 'node.exe', 90, `node "${install}\\app\\tools\\serve.mjs"`)];
  const read = census(rows);
  assert.equal(read.applications[0].inside, 7);
  assert.deepEqual(read.outside.find((root) => root.pid === 70)?.startedBy, { pid: 20, name: null });
});

test('a folder that only starts with the install folder\'s name is no install: what runs from it is no Daoris process', () => {
  const rows = [...machine(), row(80, 999, 'node.exe', 400, 'node D:\\Programs\\DaorisNotes\\serve.mjs'),
    row(81, 999, 'node.exe', 410, `node "${install}\\tools\\serve.mjs"`)];
  assert.deepEqual(census(rows).outside.map((root) => root.pid), [40, 81]);
});

test('nothing outside is said so, and no application is said so', () => {
  const inside = machine().filter((process) => process.pid < 40);
  assert.deepEqual(said(census(inside)).slice(1), ['process-tree: no Daoris process runs outside it.']);
  assert.deepEqual(said(census([])), ['process-tree: no Daoris application runs here.', 'process-tree: no Daoris process runs outside it.']);
});
