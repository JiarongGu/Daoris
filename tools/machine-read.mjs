/**
 * The machine's process table and free memory, read by one in-house reader that starts no PowerShell and asks no WMI.
 *
 * On Windows the reader is `tools/machine-read/`, a small .NET program built the first time a tool asks and again when a source of it
 * changed (under `_fixtures/machine-read-build/`, one build at a time by a lock beside it): it reads the table with the system's own
 * calls, as the driver reads its memory (`MachineMemory`). Where it cannot run (no .NET SDK, a build that fails) the read throws, saying
 * why: an empty machine is never the answer. Elsewhere the table is `ps`'s and the memory Node's and `/proc`'s, as each caller read it
 * before, so nothing here is Windows' alone.
 *
 * Each call is one start of the reader, about half a second on a loaded machine (the PowerShell and CIM read it replaces took two to five,
 * and the WMI service paid for each: 177 CPU-hours in 15.8 days). How often a caller asks is its own bound, said where it is made: the
 * memory gate every 5 s for 10 minutes at most (`memoryGate`), the foreign-tree count at most every 15 s (`foreignTrees`), every other
 * caller once per question asked (a census, an end of strays, a lock's holder).
 */
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { withRunLock } from './run-lock.mjs';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');

/** The files the reader is built from: its hash says whether the built one is the sources'. */
export const HELPER_SOURCES = [
  'tools/machine-read/MachineRead.csproj', 'tools/machine-read/Program.cs',
  'tools/machine-read/Directory.Build.props', 'tools/machine-read/Directory.Packages.props',
];

/** Where it is built, by the SDK's `--artifacts-path`: its own output, so no other build of this tree fights it for a file. */
export const BUILD_FOLDER = '_fixtures/machine-read-build';
const PROJECT = HELPER_SOURCES[0];
const EXE = ['bin', 'MachineRead', 'debug', 'machine-read.exe'];
const STAMP = 'stamp.txt';

/** The longest a read of the table or of the memory may take, and the longest a build of the reader may. */
export const READ_TIMEOUT_MS = 90_000;
export const BUILD_TIMEOUT_MS = 5 * 60_000;

/** The hash of the sources, each path and its text with LF endings; `read` is a test's. */
export function sourceHash(root = ROOT, read = (path) => readFileSync(path, 'utf8')) {
  const hash = createHash('sha256');
  for (const source of HELPER_SOURCES) hash.update(`${source}\0`).update(String(read(join(root, source))).replace(/\r\n/g, '\n')).update('\0');
  return hash.digest('hex');
}

/** Whether the reader built under `root` is stale (missing, or not the sources'), with the hash the sources have now. */
export function helperStanding(root = ROOT, {
  read = (path) => readFileSync(path, 'utf8'),
  stamp = (path) => (existsSync(path) ? readFileSync(path, 'utf8').trim() : null),
  exists = existsSync,
} = {}) {
  const hash = sourceHash(root, read);
  const exe = join(root, BUILD_FOLDER, ...EXE);
  return { hash, exe, stale: !exists(exe) || stamp(join(root, BUILD_FOLDER, STAMP)) !== hash };
}

/** What a failed command said: its first line, else no reason. */
const said = (text) => String(text ?? '').replace(/\r\n/g, '\n').split('\n').map((line) => line.trim()).find(Boolean) ?? 'no reason given';

function buildHelper(root, hash, run) {
  const out = join(root, BUILD_FOLDER);
  const ran = run('dotnet', ['build', PROJECT, '--artifacts-path', out, '-v', 'q', '-nologo'], {
    cwd: root, encoding: 'utf8', windowsHide: true, timeout: BUILD_TIMEOUT_MS, maxBuffer: 16 * 1024 * 1024,
    env: { ...process.env, MSBUILDDISABLENODEREUSE: '1', DOTNET_CLI_USE_MSBUILD_SERVER: '0', UseSharedCompilation: 'false', DOTNET_NOLOGO: '1' },
  });
  if (ran.error) throw new Error(`the machine reader could not be built: ${ran.error.code === 'ENOENT' ? 'no `dotnet` on the PATH' : ran.error.message}`);
  if (ran.status !== 0) throw new Error(`the machine reader could not be built: ${said(ran.stdout || ran.stderr)}`);
  mkdirSync(out, { recursive: true });
  writeFileSync(join(out, STAMP), `${hash}\n`);
}

const built = new Map();

/** The built reader of the tree at `root`, built first where it is missing or stale; `run` and `look` are a test's. Throws where it cannot be. */
export function helperPath(root = ROOT, { run = spawnSync, look = helperStanding } = {}) {
  if (built.has(root)) return built.get(root);
  let standing = look(root);
  if (standing.stale) {
    withRunLock(join(root, `${BUILD_FOLDER}.lock`), () => {
      standing = look(root);
      if (standing.stale) buildHelper(root, standing.hash, run);
    });
    standing = look(root);
    if (standing.stale) throw new Error('the machine reader was built and is still not there');
  }
  built.set(root, standing.exe);
  return standing.exe;
}

const asked = ({ run, helper, args, timeoutMs }) => {
  const exe = typeof helper === 'function' ? helper() : helper;
  const ran = run(exe, args, { encoding: 'utf8', windowsHide: true, maxBuffer: 64 * 1024 * 1024, timeout: timeoutMs });
  if (ran.error) throw new Error(`the machine reader could not start: ${ran.error.message}`);
  if (ran.status !== 0) throw new Error(`the machine reader failed: ${said(ran.stderr || ran.stdout)}`);
  return String(ran.stdout ?? '');
};

/** The helper's table as rows `{ pid, ppid, created, name, path, command }`: `created` in epoch milliseconds, `command` empty where unread. */
export function parseProcesses(text) {
  let rows;
  try {
    rows = JSON.parse(String(text).replace(/^﻿/, '').trim());
  } catch (error) {
    throw new Error(`the machine reader's list of processes could not be read: ${error.message}`);
  }
  if (!Array.isArray(rows)) throw new Error('the machine reader did not answer a list of processes');
  return rows.map((row) => ({
    pid: Number(row.pid), ppid: Number(row.ppid), created: Number(row.created),
    name: String(row.name ?? ''), path: typeof row.path === 'string' ? row.path : null, command: typeof row.command === 'string' ? row.command : '',
  }));
}

/** The helper's memory, in bytes. */
export function parseMemory(text) {
  let read;
  try {
    read = JSON.parse(String(text).replace(/^﻿/, '').trim());
  } catch (error) {
    throw new Error(`the machine reader's memory could not be read: ${error.message}`);
  }
  for (const field of ['physical', 'commit', 'total']) {
    if (!Number.isFinite(read?.[field])) throw new Error(`the machine reader named no ${field} memory`);
  }
  return { physical: read.physical, commit: read.commit, total: read.total };
}

/** `ps`'s rows (`pid ppid lstart args`, started in UTC) as the same rows. */
function parsePs(text) {
  return String(text).split('\n').flatMap((line) => {
    const found = /^\s*(\d+)\s+(\d+)\s+(\w{3} \w{3}\s+\d+ [\d:]{8} \d{4})\s+(.*)$/.exec(line);
    if (!found) return [];
    const command = (found[4] ?? '').trim();
    const program = command.startsWith('"') ? command.slice(1, Math.max(1, command.indexOf('"', 1))) : command.split(/\s+/)[0] ?? '';
    return [{ pid: Number(found[1]), ppid: Number(found[2]), created: Date.parse(`${found[3]} UTC`), name: basename(program), path: null, command }];
  });
}

/**
 * Every process on the machine (or those of `pids`) as `{ pid, ppid, created, name, path, command }`, read and changing nothing. Throws,
 * saying why, where the table could not be read. `run` and `helper` (a path, or a function that answers it) are a test's.
 */
export function readProcesses({ platform = process.platform, run = spawnSync, helper = helperPath, pids = [], timeoutMs = READ_TIMEOUT_MS } = {}) {
  if (platform === 'win32') return parseProcesses(asked({ run, helper, args: ['processes', ...pids.map(String)], timeoutMs }));
  const ran = run('ps', ['-A', '-o', 'pid=', '-o', 'ppid=', '-o', 'lstart=', '-o', 'args='], { encoding: 'utf8', windowsHide: true, maxBuffer: 64 * 1024 * 1024, timeout: timeoutMs, env: { ...process.env, LC_ALL: 'C' } });
  if (ran.error) throw new Error(`ps could not start: ${ran.error.message}`);
  if (ran.status !== 0) throw new Error(`ps failed: ${said(ran.stderr || ran.stdout)}`);
  const rows = parsePs(ran.stdout);
  return pids.length === 0 ? rows : rows.filter((row) => pids.includes(row.pid));
}

/** The machine's free physical memory, free commit and physical memory in all, in bytes: Windows' own, read by the reader. Throws where it cannot be. */
export function freeMemory({ platform = process.platform, run = spawnSync, helper = helperPath, timeoutMs = READ_TIMEOUT_MS } = {}) {
  if (platform !== 'win32') throw new Error(`${platform} has no reader of the free commit here`);
  return parseMemory(asked({ run, helper, args: ['memory'], timeoutMs }));
}

/** When the process `pid` began, in epoch milliseconds; null where it does not run. Throws where the table could not be read. */
export function processStartOf(pid, options = {}) {
  return readProcesses({ ...options, pids: [pid] }).find((row) => row.pid === pid)?.created ?? null;
}
