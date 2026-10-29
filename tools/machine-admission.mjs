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

/**
 * The file read strictly, for the whole admission's readings (INTEGRATE1b, design §4.5 *Inconclusive*): both lists of entries that
 * read, and `unread`, how many holds do not read as an entry. A missing file is empty only where `missing` allows it; one that is
 * missing otherwise, does not read as JSON, is not an object or keeps a list that is not a list throws, since its holds are unknown.
 */
export function readStrict(file, { missing = false, read = (path) => readFileSync(path, 'utf8') } = {}) {
  let text;
  try {
    text = read(file);
  } catch (error) {
    if (missing && error?.code === 'ENOENT') return { holds: [], waits: [], unread: 0 };
    throw error;
  }
  const parsed = JSON.parse(text);
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error('the record is not an object');
  for (const name of ['holds', 'waits']) if (parsed[name] !== undefined && !Array.isArray(parsed[name])) throw new Error(`its ${name} is not a list`);
  const holds = (parsed.holds ?? []).map(entryOf);
  return { holds: holds.filter(Boolean), waits: (parsed.waits ?? []).map(entryOf).filter(Boolean), unread: holds.filter((each) => !each).length };
}

/** Who holds or asks, as `--queue` and the waiting sentence name them. */
const whoOf = (entry) => `${entry.kind === 'queue' ? 'the merge queue' : entry.session ? `session ${entry.session}` : 'a driven run'}'s ${entry.what}`;

/**
 * What doubts a whole admission at one reading (INTEGRATE1b, design §4.5 *Inconclusive*, F34), over the record pruned and read
 * strictly: each of its own places (`ids`) not held, any live hold not its own (a writer of a larger capacity, a run that took a
 * place while the record could not be read), and any hold that does not read as an entry. None is a reading that shows the
 * machine held for it alone. The asks behind it are no doubt: they wait for it.
 */
export function wholeDoubts(state, ids) {
  const mine = new Set(ids);
  const held = new Set(state.holds.filter((entry) => mine.has(entry.id)).map((entry) => entry.id));
  const lost = ids.filter((id) => !held.has(id));
  const others = state.holds.filter((entry) => !mine.has(entry.id));
  const doubts = [];
  if (lost.length) doubts.push(`${lost.length} of its ${ids.length} places ${lost.length === 1 ? 'is' : 'are'} no longer held (${lost.join(', ')})`);
  if (others.length) doubts.push(`another live hold stands beside it: ${others.map(whoOf).join('; ')}`);
  if (state.unread) doubts.push(`the record keeps ${state.unread} ${state.unread === 1 ? 'hold that does' : 'holds that do'} not read as an entry`);
  return doubts;
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
 * `takeWhole` takes every place at once, for a confirm run (INTEGRATE1b): see its own contract below.
 *
 * `daorisHome` null admits everything at once. The seams (`alive`, `now`, `sleep`, `say`, and `readText` and `writeText`, which the
 * whole admission alone reads and writes through) are a test's.
 */
export function createAdmission({
  daorisHome = process.env.DAORIS_HOME ?? null, kind = 'queue', capacity = null, alive = processAlive, now = Date.now,
  sleep = (ms) => new Promise((done) => { setTimeout(done, ms); }), say = console.log, pollMs = POLL_MS, sayMs = SAY_MS, beatMs = BEAT_MS, env = process.env,
  readText = (path) => readFileSync(path, 'utf8'), writeText = writeAtomic,
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

  /**
   * A change read strictly (`readStrict`): the record pruned, with `unread`, handed to `fn`, which answers the lists to write or
   * null to write nothing. Throws where the lock, the read or the write fails, which the whole admission reads as a doubt.
   */
  const changeStrict = (fn, { missing = false } = {}) => withKeepLock(file, () => {
    const raw = readStrict(file, { missing, read: readText });
    const state = { ...pruned(raw, seam()), unread: raw.unread };
    const next = fn(state);
    if (next) writeText(file, `${JSON.stringify({ schema: 1, holds: next.holds, waits: next.waits }, null, 2)}\n`);
    return next ?? state;
  });

  /**
   * The whole admission, for a confirm run (INTEGRATE1b, design §4.5, C7, F34): every place the machine admits, taken as that many
   * ordinary entries of today's schema, so a writer of any build reads, counts and keeps each one.
   *
   * - **The take of N.** N is `capacityOf`'s count, the one the driver's twin reads. The N entries share one `since`, each its own
   *   id under this process (`<pid>-<hex>-<nn>`, so they sort together), and go into `waits` in **one** locked write, where they
   *   stand together in first-come order and no other ask can fall between them. Each is admitted by today's first-come rule
   *   (`judge`); the hold answers once every one holds. While it waits, the holds it sees are the runs it waits for, never a doubt.
   * - **Its caller's contract.** The failing gate's own place, and every other place this process holds or asks, is let go before
   *   the take: the take would otherwise wait on itself, so it refuses at once, writing nothing, where its own process holds or
   *   asks a live place. A run the confirm run starts asks no place of its own (or runs inside one of its ids, `INSIDE`).
   * - **Its beat.** One write beats every entry, every `beatMs`, and `beat()` beats it at once.
   * - **Its readings.** At its start (a strict read once every entry holds), at each beat (in the beat's own change) and at its end
   *   (in the change that lets every entry go, one write), each reading's `wholeDoubts`, and any lock, read or write that fails
   *   there or while it waits, is a doubt. A doubt while it waits ends the wait with every place let go.
   *
   * Answers `{ held: true, ids, capacity, beat(), end() }`, where `end()` lets every place go and answers the evidence (twice, the
   * same); or `{ held: false, dropped, why, evidence }`: `dropped` where `halted()` ended the wait, else inconclusive at the take (no
   * home, its own process or an outer hold holding a place, a hold that does not read, a record it could not write) or while it
   * waited. The evidence: `{ pid, what, capacity, ids, asked, started, ended, waitedMs, beats, doubts: [{ at, when, why }],
   * conclusive }`, `at` one of `take`, `wait`, `start`, `beat` and `end`; conclusive only with no doubt from its take to its end.
   */
  async function takeWhole({ what, session, repository, tree, halted = () => false } = {}) {
    const n = size();
    const asked = now();
    const name = String(what ?? 'a confirm run');
    const evidence = { pid: process.pid, what: name, capacity: n, ids: [], asked: new Date(asked).toISOString(), started: null, ended: null, waitedMs: null, beats: 0, doubts: [] };
    const doubt = (at, why) => evidence.doubts.push({ at, when: stamp(), why });
    const seal = () => ({ ...evidence, ids: [...evidence.ids], doubts: [...evidence.doubts], conclusive: !evidence.doubts.length && !!evidence.started && !!evidence.ended });
    const refuse = (at, why) => {
      doubt(at, why);
      return { held: false, dropped: false, why, evidence: seal() };
    };
    const failed = (error) => error?.message ?? String(error);
    if (!file) return refuse('take', 'there is no Daoris home whose locks can be placed, so the machine admission cannot be shown');
    const inside = env[INSIDE];
    if (inside && readState(file).holds.some((entry) => entry.id === inside && live(entry, seam()))) {
      return refuse('take', `it runs inside the hold ${inside}, which holds a place it cannot take`);
    }
    const tag = `${process.pid}-${randomBytes(4).toString('hex')}`;
    const ids = Array.from({ length: n }, (_, i) => `${tag}-${String(i + 1).padStart(2, '0')}`);
    evidence.ids = ids;
    const mine = new Set(ids);
    const since = stamp();
    const entries = ids.map((id, i) => {
      const entry = { id, kind, pid: process.pid, what: `${name} (place ${i + 1} of ${n})`, since, beat: since };
      for (const [field, value] of [['session', session], ['repository', repository], ['tree', tree]]) if (value) entry[field] = value;
      return entry;
    });
    let refused = null;
    try {
      changeStrict((state) => {
        const own = [...state.holds, ...state.waits].filter((entry) => entry.pid === process.pid);
        if (state.unread) refused = `the record keeps ${state.unread} ${state.unread === 1 ? 'hold that does' : 'holds that do'} not read as an entry, so who holds the machine is unknown`;
        else if (own.length) refused = `its own process holds or asks a place already (${own.map((entry) => entry.what).join('; ')}): the failing gate's own place is let go before the whole admission is taken, which would otherwise wait on itself`;
        return refused ? null : { holds: state.holds, waits: [...state.waits, ...entries] };
      }, { missing: true });
    } catch (error) {
      return refuse('take', `the machine's record could not be written (${failed(error)})`);
    }
    if (refused) return refuse('take', refused);

    let started = false;
    const drop = (state) => ({ holds: state.holds.filter((entry) => !mine.has(entry.id)), waits: state.waits.filter((entry) => !mine.has(entry.id)) });
    const letGo = () => {
      try {
        changeStrict(drop);
      } catch {
        try {
          change(drop);
        } catch {
          // Left: they stop counting when this process ends, or two minutes on.
        }
      }
    };
    const beat = () => {
      try {
        changeStrict((state) => {
          if (started) for (const why of wholeDoubts(state, ids)) doubt('beat', why);
          const touch = (entry) => (mine.has(entry.id) ? { ...entry, beat: stamp() } : entry);
          return { holds: state.holds.map(touch), waits: state.waits.map(touch) };
        });
        if (started) evidence.beats += 1;
      } catch (error) {
        doubt(started ? 'beat' : 'wait', `the machine's record could not be read or written at a beat (${failed(error)})`);
      }
    };
    const timer = setInterval(beat, beatMs);
    timer.unref?.();
    const stop = () => {
      clearInterval(timer);
      letGo();
    };
    let saidAt = null;
    let saidOf = '';
    for (;;) {
      if (evidence.doubts.length) {
        stop();
        return { held: false, dropped: false, why: evidence.doubts[0].why, evidence: seal() };
      }
      let gone = [];
      let next;
      try {
        next = changeStrict((state) => {
          gone = ids.filter((id) => ![...state.holds, ...state.waits].some((entry) => entry.id === id));
          if (gone.length) return null;
          let { holds, waits } = state;
          for (const id of ids) {
            if (holds.some((entry) => entry.id === id)) continue;
            if (!judge({ holds, waits }, id, n).admitted) break;
            holds = [...holds, { ...waits.find((entry) => entry.id === id), beat: stamp() }];
            waits = waits.filter((entry) => entry.id !== id);
          }
          return { holds, waits };
        });
      } catch (error) {
        doubt('wait', `the machine's record could not be read or written while it waited (${failed(error)})`);
        continue;
      }
      if (gone.length) {
        doubt('wait', `${gone.length} of its ${n} places left the record while it waited (${gone.join(', ')})`);
        continue;
      }
      if (ids.every((id) => next.holds.some((entry) => entry.id === id))) break;
      if (halted()) {
        stop();
        return { held: false, dropped: true, why: `the wait for the whole admission before ${name} was ended`, evidence: seal() };
      }
      const first = ids.find((id) => !next.holds.some((entry) => entry.id === id));
      const judged = judge(next, first, n);
      const key = `${judged.holds.map((entry) => entry.id).join(',')}/${judged.ahead}`;
      if (saidAt === null || now() - saidAt >= sayMs || key !== saidOf) {
        say(`  waiting for the machine's whole heavy-run admission before ${name}, all ${n} places: ${waitingSentence(judged, { capacity: n, now: now() })}; it never times out, and looks again every ${Math.round(pollMs / 1000)}s`);
        saidAt = now();
        saidOf = key;
      }
      await sleep(pollMs);
    }

    started = true;
    evidence.started = stamp();
    evidence.waitedMs = now() - asked;
    if (saidAt !== null) say(`  admission: ${name} holds every place after waiting ${Math.round(evidence.waitedMs / 1000)}s`);
    try {
      const raw = readStrict(file, { read: readText });
      for (const why of wholeDoubts({ ...pruned(raw, seam()), unread: raw.unread }, ids)) doubt('start', why);
    } catch (error) {
      doubt('start', `the machine's record could not be read or written at its start (${failed(error)})`);
    }

    let ended = null;
    const end = () => {
      if (ended) return ended;
      clearInterval(timer);
      try {
        changeStrict((state) => {
          for (const why of wholeDoubts(state, ids)) doubt('end', why);
          return drop(state);
        });
      } catch (error) {
        doubt('end', `the machine's record could not be read or written at its end (${failed(error)})`);
        letGo();
      }
      evidence.ended = stamp();
      ended = seal();
      return ended;
    };
    return { held: true, ids, capacity: n, beat, end };
  }

  return { take, takeWhole, file, capacity: size };
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
