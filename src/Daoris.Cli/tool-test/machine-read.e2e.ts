import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { freemem } from 'node:os';
import { mkdtempSync, readFileSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { freeMemory, processStartOf, readProcesses } from '../../../tools/machine-read.mjs';

// test-map: the machine reader, built and run on this machine: the table, the memory and the programs a read starts
// reaches: tool-e2e — the reader is a real .NET program, started for each read

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const win = process.platform === 'win32';
const { startedFrom } = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../tools/processes.mjs') as { startedFrom: (exe: string) => { pid: number }[] };

test('the reader answers the table with this process in it, and the memory the platform reports', { skip: !win, timeout: 300_000 }, () => {
  const rows = readProcesses();
  const own = rows.find((row) => row.pid === process.pid);
  assert.ok(own, 'this process is in the table');
  assert.equal(own.name.toLowerCase(), 'node.exe');
  assert.equal(own.path?.toLowerCase(), realpathSync.native(process.execPath).toLowerCase(), 'the path the system holds, a link resolved');
  assert.ok(own.command.includes('machine-read.e2e.ts') || own.command.includes('--test'), own.command);
  assert.ok(Math.abs(own.created - (Date.now() - process.uptime() * 1000)) < 5_000, 'its creation time is when it began');
  assert.equal(rows.find((row) => row.pid === own.ppid)?.pid, own.ppid, 'its parent is in the table');
  assert.deepEqual(readProcesses({ pids: [process.pid] }).map((row) => row.pid), [process.pid]);
  assert.notEqual(processStartOf(process.pid), null);
  assert.ok(startedFrom(process.execPath).some((row) => row.pid === process.pid), 'a program is found by the path it was started by, a junction on the way or not');

  const memory = freeMemory();
  assert.ok(memory.total > 2 ** 30 && memory.physical > 0 && memory.physical <= memory.total && memory.commit > 0);
  assert.ok(Math.abs(memory.physical - freemem()) < 2 * 2 ** 30, 'the free physical memory is the one Node reports');
});

test('a read of the table and of the memory starts the reader and nothing else: no PowerShell, counted by the programs a run starts', { skip: !win, timeout: 300_000 }, () => {
  const scratch = mkdtempSync(join(REPO, '_fixtures', 'machine-read-test-'));
  try {
    const counted = join(scratch, 'started.txt');
    // Every program the run starts goes through child_process: a preload records each one's name before it runs.
    writeFileSync(join(scratch, 'count.cjs'), [
      "const cp = require('node:child_process');",
      "const fs = require('node:fs');",
      "for (const name of ['spawnSync', 'spawn', 'execFileSync', 'execSync', 'execFile', 'exec']) {",
      '  const real = cp[name];',
      "  cp[name] = function (command, ...rest) { fs.appendFileSync(process.env.STARTED_FILE, `${name} ${String(command).split(/[\\\\/]/).pop()}\\n`); return real.call(this, command, ...rest); };",
      '}',
    ].join('\n'));
    const reader = new URL('../../../tools/machine-read.mjs', import.meta.url).href;
    const ran = spawnSync(process.execPath, ['--require', join(scratch, 'count.cjs'), '--input-type=module', '-e',
      `import { readProcesses, freeMemory } from ${JSON.stringify(reader)}; readProcesses(); freeMemory();`],
    { encoding: 'utf8', windowsHide: true, env: { ...process.env, STARTED_FILE: counted } });
    assert.equal(ran.status, 0, ran.stderr);
    const started = readFileSync(counted, 'utf8').split('\n').filter(Boolean);
    assert.deepEqual(started.filter((line) => /powershell|pwsh|wmic|cscript/i.test(line)), [], started.join('\n'));
    // A build, when the reader is stale, is the SDK's; the reads themselves are two starts of the reader.
    assert.deepEqual(started.filter((line) => !/ dotnet(?:\.exe)?$/i.test(line)), ['spawnSync machine-read.exe', 'spawnSync machine-read.exe']);
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
});
