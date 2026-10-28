/**
 * The machine's heavy-run admission, the queue's side of it (HEAVYQUEUE1, D180's HEAVYRUN1 notes): the merge queue's heavy gates and
 * a driven session's heavy runs take one place each before they start a heavy process, so the machine never runs more of them at
 * once than it admits, and each that waits is told what it waits for.
 *
 * One file in the Daoris home, `locks/admission/admission.json` (the home's `locks` entry, D172), read and written by two twins, this
 * and the driver's `MachineAdmission.cs`, which share no code. The file is the contract; `machine-admission.json`, the driver tests'
 * fixture, is the table both carry, case for case:
 *
 *   { "schema": 1, "holds": [Entry], "waits": [Entry] }
 *   Entry: { id, kind: "queue" | "session", pid, what, since, beat, session?, repository?, tree? }
 *
 * `holds` are the runs that may start or run now; `waits` are the runs asking, in the order they began to ask. An entry counts only
 * while its process runs (`pid`) AND it beat within `STALE_MS` (its writer rewrites `beat` every `BEAT_MS`), so a holder that dies
 * however it dies stops counting at once where its process is gone, and after two minutes where a reused process id says otherwise;
 * nothing is released by a timeout while the holder lives and beats. A run is admitted when the live holds, plus the live waits
 * ahead of it, are fewer than the capacity: first come, first served, a queue gate and a session's run alike.
 *
 * The file is changed under `admission.json.lock`, the keeping write's lock (`keeplock.ts`, the driver's `KeepLock.cs`: put in place
 * with no overwrite, `{ pid, since }`, stale when its process is gone or it is a minute old); a change takes milliseconds, and the
 * waiting is done outside it, by looking again every `POLL_MS`.
 *
 * Nothing here is a decision about what a gate checks: a gate admitted later runs the same, and a wait is never a timeout or a
 * failure. With no Daoris home (no `DAORIS_HOME`) nothing is shared and nothing waits.
 */
import { randomBytes } from 'node:crypto';
import {
  existsSync, readFileSync,
} from 'node:fs';
import { availableParallelism } from 'node:os';
import { homeEntry } from '../src/Daoris.Cli/src/platform/home.ts';
import { withKeepLock } from '../src/Daoris.Cli/src/platform/keeplock.ts';
import { writeAtomic } from './fsx.mjs';

/** How long an entry that has not beat still counts, and how often a live one beats (the twin's `StaleAfter` and `BeatEvery`). */
export const STALE_MS = 120_000;
export const BEAT_MS = 20_000;
/** How often a waiting run looks again, and how often it says that it still waits (the twin's `Poll` and `SayEvery`). */
export const POLL_MS = 2_000;
export const SAY_MS = 60_000;
/** The variable naming the hold a process runs inside: a run started by an admitted run is inside its hold, never a second one. */
export const INSIDE = 'DAORIS_ADMISSION_HOLD';
/** The variable that sets the capacity on a machine; else `capacityOf`. */
export const CAPACITY = 'DAORIS_ADMISSION_SLOTS';

/**
 * How many heavy runs the machine admits at once (the twin's `CapacityOf`): `DAORIS_ADMISSION_SLOTS` where it is a whole number
 * from 1 to 64, else one for each seven processors, two at least and six at most. On the 22-processor machine this was built for it
 * is three: a gate beside two others ran as it does alone, and beside six sessions' runs it took twice as long.
 */
export function capacityOf(cpus = availableParallelism(), env = process.env) {
  const said = String(env[CAPACITY] ?? '').trim();
  const set = /^\d{1,3}$/.test(said) ? Number(said) : 0;
  if (set >= 1 && set <= 64) return set;
  return Math.min(6, Math.max(2, Math.floor(cpus / 7)));
}

/** The admission file in a Daoris home, by the home's table of entries. */
export const admissionFile = (daorisHome) => homeEntry(daorisHome, 'locks', 'admission', 'admission.json');

/** Whether a process of that id runs; one that runs as someone else is there too. */
export function processAlive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error?.code === 'EPERM';
  }
}

const timeOf = (text) => {
  const at = Date.parse(text);
  return Number.isNaN(at) ? null : at;
};

/** An entry as the file may hold it, or null for one that does not read as an entry (it counts for nothing and is dropped at the next write). */
function entryOf(raw) {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return null;
  const { id, kind, pid, what, since, beat } = raw;
  if (typeof id !== 'string' || !id || !['queue', 'session'].includes(kind) || !Number.isInteger(pid)) return null;
  if (typeof what !== 'string' || timeOf(since) === null || timeOf(beat) === null) return null;
  const text = (value) => (typeof value === 'string' && value.trim() ? value : undefined);
  const entry = { id, kind, pid, what, since, beat };
  for (const [name, value] of [['session', raw.session], ['repository', raw.repository], ['tree', raw.tree]]) {
    if (text(value)) entry[name] = value;
  }
  return entry;
}

/** The file read: both lists, each entry that reads, in the order kept. A file that is missing or does not read is empty. */
export function readState(file) {
  try {
    const parsed = JSON.parse(readFileSync(file, 'utf8'));
    const list = (value) => (Array.isArray(value) ? value.map(entryOf).filter(Boolean) : []);
    return { holds: list(parsed?.holds), waits: list(parsed?.waits) };
  } catch {
    return { holds: [], waits: [] };
  }
}

/** Whether an entry still counts: its process runs and it beat within `STALE_MS` (the twin's `Live`). */
export const live = (entry, { alive = processAlive, now = Date.now() } = {}) => alive(entry.pid) && now - timeOf(entry.beat) < STALE_MS;

/** The state with the entries that no longer count taken out; `waits` in the order they began to ask. */
export function pruned(state, seam = {}) {
  const keep = (list) => list.filter((entry) => live(entry, seam));
  const byAsk = (a, b) => (timeOf(a.since) - timeOf(b.since)) || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0);
  return { holds: keep(state.holds), waits: keep(state.waits).sort(byAsk) };
}

/**
 * Whether the run `id` is admitted (the twin's `Judge`), over a pruned state: `{ admitted, ahead, holds }`. It is when the holds, plus
 * the waits before it, are fewer than `capacity`. A run not among the waits is last in line.
 */
export function judge({ holds, waits }, id, capacity) {
  const at = waits.findIndex((entry) => entry.id === id);
  const ahead = at < 0 ? waits.length : at;
  return { admitted: holds.length + ahead < capacity, ahead, holds };
}

/** What a run waits for, as a sentence: the holds it waits on, each who and what and since when, and the runs before it. */
export function waitingSentence({ holds, ahead }, { capacity, now = Date.now() }) {
  const span = (since) => {
    const seconds = Math.max(0, Math.round((now - timeOf(since)) / 1000));
    return seconds >= 3600 ? `${Math.floor(seconds / 3600)}h ${Math.floor((seconds % 3600) / 60)}m` : seconds >= 60 ? `${Math.floor(seconds / 60)}m ${seconds % 60}s` : `${seconds}s`;
  };
  const who = (entry) => `${entry.kind === 'queue' ? 'the merge queue' : entry.session ? `session ${entry.session}` : 'a driven run'}'s ${entry.what} (${span(entry.since)})`;
  return `the machine runs ${holds.length} of ${capacity} heavy runs: ${holds.map(who).join('; ') || 'none'}${ahead ? `; ${ahead} ${ahead === 1 ? 'run asked' : 'runs asked'} before this one` : ''}`;
}

/**
 * The admission of one process. `take` waits its turn and answers the hold, or null where it has nothing to take (no home, a gate
 * that is not heavy) or `halted` ended the wait; the hold's `release` lets it go and is safe to call twice.
 *
 * `daorisHome` null admits everything at once. The seams (`alive`, `now`, `sleep`, `say`) are a test's.
 */
export function createAdmission({
  daorisHome = process.env.DAORIS_HOME ?? null, kind = 'queue', capacity = null, alive = processAlive, now = Date.now,
  sleep = (ms) => new Promise((done) => { setTimeout(done, ms); }), say = console.log, pollMs = POLL_MS, sayMs = SAY_MS, beatMs = BEAT_MS, env = process.env,
} = {}) {
  let file = null;
  try {
    file = daorisHome ? admissionFile(daorisHome) : null;
  } catch (error) {
    say(`  admission: the home's locks cannot be placed (${error?.message ?? error}); the heavy gates run without waiting for the machine`);
  }
  const size = () => capacity ?? capacityOf(undefined, env);
  const stamp = () => new Date(now()).toISOString();
  const seam = () => ({ alive, now: now() });
  const change = (fn) => withKeepLock(file, () => {
    const state = pruned(readState(file), seam());
    const next = fn(state) ?? state;
    writeAtomic(file, `${JSON.stringify({ schema: 1, holds: next.holds, waits: next.waits }, null, 2)}\n`);
    return next;
  });

  async function take({ what, session, repository, tree, halted = () => false } = {}) {
    if (!file) return null;
    // A run an admitted run started is inside its hold: it takes no second place, which would wait on the run that started it.
    const inside = env[INSIDE];
    if (inside && readState(file).holds.some((entry) => entry.id === inside && live(entry, seam()))) return { id: inside, inside: true, release() {} };
    const id = `${process.pid}-${randomBytes(4).toString('hex')}`;
    const entry = { id, kind, pid: process.pid, what: String(what ?? 'a heavy run'), since: stamp(), beat: stamp() };
    for (const [name, value] of [['session', session], ['repository', repository], ['tree', tree]]) if (value) entry[name] = value;
    try {
      change((state) => ({ holds: state.holds, waits: [...state.waits, entry] }));
    } catch (error) {
      say(`  admission: the machine's record could not be written (${error?.message ?? error}); ${entry.what} runs without waiting for it`);
      return null;
    }
    let timer = null;
    const beat = () => {
      try {
        change((state) => ({
          holds: state.holds.map((each) => (each.id === id ? { ...each, beat: stamp() } : each)),
          waits: state.waits.map((each) => (each.id === id ? { ...each, beat: stamp() } : each)),
        }));
      } catch {
        // A beat missed is the next one's: the entry stays until it is stale.
      }
    };
    timer = setInterval(beat, beatMs);
    timer.unref?.();
    let released = false;
    const release = () => {
      if (released) return;
      released = true;
      clearInterval(timer);
      try {
        change((state) => ({ holds: state.holds.filter((each) => each.id !== id), waits: state.waits.filter((each) => each.id !== id) }));
      } catch {
        // Left: it stops counting when its process ends, or two minutes on.
      }
    };
    let saidAt = null;
    let saidOf = '';
    const waitedFrom = now();
    for (;;) {
      let verdict;
      try {
        verdict = change((state) => {
          const judged = judge(state, id, size());
          if (!judged.admitted) return state;
          return {
            holds: [...state.holds, { ...state.waits.find((each) => each.id === id) ?? entry, beat: stamp() }],
            waits: state.waits.filter((each) => each.id !== id),
          };
        });
      } catch (error) {
        say(`  admission: the machine's record could not be read (${error?.message ?? error}); ${entry.what} runs without waiting for it`);
        release();
        return null;
      }
      if (verdict.holds.some((each) => each.id === id)) {
        if (saidAt !== null) say(`  admission: ${entry.what} has its place after waiting ${Math.round((now() - waitedFrom) / 1000)}s`);
        return { id, inside: false, release };
      }
      if (halted()) {
        release();
        return null;
      }
      const judged = judge(verdict, id, size());
      const key = `${judged.holds.map((each) => each.id).join(',')}/${judged.ahead}`;
      if (saidAt === null || now() - saidAt >= sayMs || key !== saidOf) {
        say(`  waiting for the machine's heavy-run admission before ${entry.what}: ${waitingSentence(judged, { capacity: size(), now: now() })}; it never times out, and looks again every ${Math.round(pollMs / 1000)}s`);
        saidAt = now();
        saidOf = key;
      }
      await sleep(pollMs);
    }
  }

  return { take, file, capacity: size };
}

/** What `--queue` says of the machine's admission (HEAVYQUEUE1): its places held, whose and for how long, and how many ask. */
export function admissionLines(look, now = Date.now()) {
  const who = (entry) => `${entry.kind === 'queue' ? 'the merge queue' : entry.session ? `session ${entry.session}` : 'a driven run'}'s ${entry.what}`;
  const lines = [`machine heavy-run admission (the Daoris home): ${look.holds.length} of ${look.capacity} places held, ${look.waits.length} asking`];
  for (const entry of look.holds) lines.push(`  holds: ${who(entry)}, since ${entry.since} (${Math.round((now - timeOf(entry.since)) / 60_000)} min)`);
  for (const entry of look.waits) lines.push(`  asks: ${who(entry)}, since ${entry.since}`);
  return lines;
}

/** The machine's admission as it stands, read only: the live holds and waits, and whether the file is there. */
export function lookAt(daorisHome, seam = {}) {
  const file = admissionFile(daorisHome);
  const state = pruned(readState(file), { alive: seam.alive, now: seam.now ?? Date.now() });
  return { ...state, capacity: seam.capacity ?? capacityOf(undefined, seam.env ?? process.env), present: existsSync(file) };
}
