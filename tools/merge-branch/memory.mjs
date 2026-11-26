/**
 * The machine's free memory as the merge tool reads it before a heavy gate (`readMemory`), and whether a gate that failed died
 * of memory (`memoryDeath`). `tools/merge-branch.mjs` exports each again under its own name and holds the wait itself
 * (`memoryGate`), its *A heavy gate waits for memory* says why. The wait also keeps the person's reserve (`reserveGb`): a gate starts only
 * while each free memory stays at or above its need and the free commit at or above its need and the reserve together (`memoryLacking`), and
 * its wait says the reserve.
 */
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { homeEntry } from '../../src/Daoris.Cli/src/platform/home.ts';
import { freemem as osFreemem, totalmem as osTotalmem } from 'node:os';
import { freeMemory } from '../machine-read.mjs';

/**
 * What a gate that died of memory leaves in its log: .NET's `OutOfMemoryException` (the sensitive scan's), "cannot
 * allocate memory" (Go's runtime under esbuild, web-rules'; POSIX's ENOMEM), "out of memory" (V8's heap, a worker's
 * `DataCloneError`, cmd's and git's "Out of memory."), and Windows' "the paging file is too small" (its commit limit). A sign
 * directly inside quotes is a value shown, as an assertion shows what it expected, not a process that died of it.
 */
const MEMORY_SIGNS = [/OutOfMemoryException/, /cannot allocate memory/i, /\bout of memory\b/i, /the paging file is too small/i]
  .map((sign) => new RegExp(`(?<!["'\`])${sign.source}(?!["'\`])`, sign.flags));

/** Windows' statuses for an allocation the machine refused, as cmd and npm pass them on, unsigned. */
const MEMORY_EXITS = new Map([[0xC0000017, 'STATUS_NO_MEMORY'], [0xC000012D, 'STATUS_COMMITMENT_LIMIT']]);

const clip = (text, width = 120) => (text.length > width ? `${text.slice(0, width - 1)}…` : text);

/**
 * Whether a gate that failed died of memory: an exit of `MEMORY_EXITS`, signed or not, or a line of its log with a
 * sign of `MEMORY_SIGNS`. What said so, `{ said }`, or null. POSIX has no exit of its own for it: the kernel's killer sends the
 * SIGKILL any kill sends, so there a death of memory is read from what it printed.
 */
export function memoryDeath(log, ...codes) {
  for (const code of codes) {
    const status = typeof code === 'number' && code < 0 ? code + 2 ** 32 : code;
    if (MEMORY_EXITS.has(status)) return { said: `exit 0x${status.toString(16).toUpperCase()}, ${MEMORY_EXITS.get(status)}` };
  }
  for (const line of String(log ?? '').replace(/\r\n/g, '\n').split('\n')) {
    if (MEMORY_SIGNS.some((sign) => sign.test(line))) return { said: `"${clip(line.trim())}"` };
  }
  return null;
}

const GB = 2 ** 30;

export const gbOf = (bytes) => (bytes / GB).toFixed(1);
export const needOf = (gb) => (Number.isInteger(gb) ? String(gb) : gb.toFixed(1));

/** A reading's free commit and free physical memory, said, each that is read; `need` keeps those below it, in GB. */
export function memoryShown(reading, need = Infinity) {
  return [[reading.commit, 'free commit'], [reading.physical, 'free physical memory']]
    .filter(([bytes]) => bytes !== null && bytes < need * GB)
    .map(([bytes, what]) => `${gbOf(bytes)} GB ${what}`);
}

/**
 * What a reading lacks for a gate, said as `memoryShown` says it: each field below `need`, and the free commit (the free physical memory
 * where no commit is read) below `need` and `kept`, the reserve, together. The reserve is of free commit, as the driver's
 * `CheckAdmission.Judge` holds it.
 */
export function memoryLacking(reading, need, kept = 0) {
  const reserveOn = reading.commit !== null ? 'commit' : 'physical';
  const short = { ...reading };
  for (const field of ['commit', 'physical']) {
    const floor = field === reserveOn ? need + kept : need;
    if (short[field] !== null && short[field] >= floor * GB) short[field] = null;
  }
  return memoryShown(short);
}

/** The GB `reserve` keeps at a machine with `total` bytes, none where it throws or answers no positive number. */
export function keptFree(reserve, total) {
  try {
    const gb = Number(reserve(total));
    return Number.isFinite(gb) && gb > 0 ? gb : 0;
  } catch {
    return 0;
  }
}

/** The ten minutes a swing of the person's own use is read over, and the margin above it: `PersonMeasure` of the driver, twinned. */
const ROOM_WINDOW_MS = 10 * 60 * 1000;
const roomMarginGb = (total) => Math.max(1, typeof total === 'number' && total > 0 ? total / 50 / GB : 0);

/**
 * What the person's own work is measured to take back, in GB: how far their use (the machine's in use less what Daoris's own
 * processes hold) stood above its present in the last ten minutes of the samples the driver's admission keeps (`room.json` under the
 * home's `locks`), and a margin of 2 % of `total` (1 GB at least). The margin alone where there are no samples; 0 where `total` is not known.
 */
export function measuredHeadroomGb({ total = null, home = null, now = Date.now(), read = (path) => readFileSync(path, 'utf8') } = {}) {
  if (!(typeof total === 'number' && total > 0)) return 0;
  let swing = 0;
  if (home) {
    try {
      const samples = JSON.parse(read(join(homeEntry(home, 'locks'), 'admission', 'room.json')).replace(/^﻿/, '')).samples
        .map((sample) => ({ at: Date.parse(sample.at), person: Math.max(0, Number(sample.used) - Number(sample.daoris)) }))
        .filter((sample) => Number.isFinite(sample.at) && Number.isFinite(sample.person) && now - sample.at <= ROOM_WINDOW_MS)
        .sort((a, b) => a.at - b.at);
      if (samples.length > 0) swing = (Math.max(...samples.map((sample) => sample.person)) - samples[samples.length - 1].person) / GB;
    } catch {
      // No samples that read: the margin alone, as the driver's ledger reads a file that does not.
    }
  }
  return swing + roomMarginGb(total);
}

/**
 * The GB the machine keeps free for the person's own work: the larger of the person's floor (`reserveGb` of the Daoris home's
 * `driver.json`, the file the driver's `ReserveSetting` reads, none where it says none or does not read) and what their own work is
 * measured to take back (`measuredHeadroomGb`), so the merge tool keeps what the driver's admission keeps.
 */
export function reserveGb({ total = null, home = null, now = Date.now(), read = (path) => readFileSync(path, 'utf8') } = {}) {
  let floor = 0;
  if (home) {
    try {
      const stored = JSON.parse(read(homeEntry(home, 'driver')).replace(/^﻿/, '')).reserveGb;
      if (Number.isInteger(stored) && stored >= 0 && stored <= 1024) floor = stored;
    } catch {
      // A file that does not read is no floor, as the driver's reader treats it.
    }
  }
  return Math.max(floor, measuredHeadroomGb({ total, home, now, read }));
}

/** Linux's free commit from `/proc/meminfo`: `CommitLimit` less `Committed_AS`, in bytes, or null when either is missing. */
export function parseMeminfo(text) {
  const field = (name) => {
    const found = new RegExp(`^${name}:\\s+(\\d+)\\s*kB`, 'm').exec(text);
    return found ? Number(found[1]) * 1024 : null;
  };
  const limit = field('CommitLimit');
  const committed = field('Committed_AS');
  return { commit: limit === null || committed === null ? null : Math.max(0, limit - committed) };
}

/**
 * The machine's free memory, read once, in bytes: `physical`, `commit` (null where it cannot be read, with `why`),
 * and `total`, its physical memory in all. On Windows the machine reader (`tools/machine-read.mjs`, no PowerShell and no WMI) reads
 * `GlobalMemoryStatusEx`'s free physical memory and free commit (the commit limit less the commit charge: what WMI's FreeVirtualMemory
 * said); a read that fails leaves Node's free memory. Linux holds a
 * process to a commit limit only under strict overcommit (`vm.overcommit_memory` 2), so only there is it read; elsewhere there
 * is none to read, and the free physical memory judges alone. `helper` is the reader's path, a test's.
 */
export function readMemory({ platform = process.platform, run = spawnSync, helper = undefined, freemem = osFreemem, totalmem = osTotalmem, read = (path) => readFileSync(path, 'utf8') } = {}) {
  const total = totalmem();
  const without = (why, physical = null) => ({ physical: physical ?? freemem(), commit: null, total, why });
  if (platform === 'win32') {
    try {
      const { physical, commit, total: whole } = freeMemory({ platform, run, ...(helper === undefined ? {} : { helper }) });
      return { physical, commit, total: whole > 0 ? whole : total };
    } catch (error) {
      return without(`the free memory could not be read: ${error?.message ?? error}`);
    }
  }
  if (platform === 'linux') {
    try {
      if (read('/proc/sys/vm/overcommit_memory').trim() !== '2') return without('Linux holds no process to a commit limit unless vm.overcommit_memory is 2');
      const { commit } = parseMeminfo(read('/proc/meminfo'));
      return commit === null ? without('/proc/meminfo names no commit limit') : { physical: freemem(), commit, total };
    } catch (error) {
      return without(`its commit could not be read: ${error?.message ?? error}`);
    }
  }
  return without(`${platform} keeps no commit limit to read`);
}
