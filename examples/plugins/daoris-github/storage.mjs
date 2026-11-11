// The kit's storage: a JSON file of records, written one record at a time under a lock, so two processes of this plugin (one per
// place, or a command beside a work process) never lose each other's writes. Daoris's plugin kit wrote this file, and
// `daoris plugin new` writes it again for a newer build: use it, do not change it. `node --test` here checks it (storage.test.mjs).
//
//   const kept = records(join(process.env.DAORIS_PLUGIN_DATA, 'state.json'));
//   kept.put('runs', run.id, { state: 'begun' });   // this one record written; every other on disk kept as it is
//   kept.get('runs', run.id);                       // read fresh, so what another process wrote is seen
//   kept.all('runs');                                // every record of a section, by key
//   kept.remove('runs', run.id);
//
// The file is `{ "<section>": { "<key>": <record> } }`, replaced whole by a rename, so a reader never sees half of a write. A file
// that does not read is refused and kept as it is: a store started afresh over it is how a record is forgotten.
import { closeSync, mkdirSync, openSync, readFileSync, renameSync, statSync, unlinkSync, writeFileSync, writeSync } from 'node:fs';
import { dirname } from 'node:path';

/** How long a write waits for another process's, in milliseconds, before it is refused. A record is held for a millisecond or two. */
const PATIENCE = 10000;

/** A lock older than this, in milliseconds, whose holder cannot be told, is left by a process that is gone. */
const ABANDONED = 30000;

const pause = (ms) => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);

/** The records kept in `file`, made with its folder at the first write. */
export function records(file) {
  const lock = `${file}.lock`;
  const read = () => {
    let text;
    try {
      text = readFileSync(file, 'utf8');
    } catch (error) {
      if (error.code === 'ENOENT') return {};
      throw error;
    }
    let kept;
    try {
      kept = JSON.parse(text);
    } catch (error) {
      throw new Error(`${file} does not read (${error.message}); it is kept as it is, and nothing is written to it until it reads`);
    }
    if (kept === null || typeof kept !== 'object' || Array.isArray(kept)) {
      throw new Error(`${file} holds no object of sections; it is kept as it is, and nothing is written to it until it does`);
    }
    return kept;
  };

  const held = (act) => {
    mkdirSync(dirname(file), { recursive: true });
    const until = Date.now() + PATIENCE;
    for (let wait = 1; ; wait = Math.min(wait * 2, 25)) {
      let handle;
      try {
        handle = openSync(lock, 'wx');
      } catch (error) {
        // Windows answers EPERM for a lock file another process is deleting.
        if (!['EEXIST', 'EPERM', 'EBUSY'].includes(error.code)) throw error;
        if (abandoned(lock)) continue;
        if (Date.now() > until) throw new Error(`${lock} was held by another process for ${PATIENCE / 1000}s; nothing was written`);
        pause(wait);
        continue;
      }
      try {
        writeSync(handle, `${process.pid}\n`);
      } finally {
        closeSync(handle);
      }
      try {
        return act();
      } finally {
        retried(() => {
          try {
            unlinkSync(lock);
          } catch (error) {
            if (error.code !== 'ENOENT') throw error;
          }
        });
      }
    }
  };

  const write = (kept) => {
    const written = `${file}.${process.pid}.tmp`;
    writeFileSync(written, `${JSON.stringify(kept, null, 2)}\n`);
    retried(() => renameSync(written, file));
  };

  return {
    /** The whole file, read fresh. */
    read: () => held(read),
    /** Every record of `section`, by key. */
    all: (section) => held(() => read()[section] ?? {}),
    /** One record, or undefined. */
    get: (section, key) => held(() => read()[section]?.[key]),
    /** One record written, every other record in the file as it is on disk now. */
    put: (section, key, value) => held(() => {
      const kept = read();
      kept[section] = { ...(kept[section] ?? {}), [key]: value };
      write(kept);
    }),
    /** One record removed, if it is there. */
    remove: (section, key) => held(() => {
      const kept = read();
      if (kept[section]?.[key] === undefined) return;
      delete kept[section][key];
      write(kept);
    }),
  };
}

/**
 * Whether the lock is left by a process that is gone, and is removed so: its holder's pid no longer running, or the lock older than
 * {@link ABANDONED}, which a pid used again by another process cannot outlast. Read again just before it is removed, so a lock taken
 * meanwhile is never removed.
 */
function abandoned(lock) {
  let holder;
  let since;
  try {
    holder = readFileSync(lock, 'utf8');
    since = statSync(lock).mtimeMs;
  } catch {
    return false;
  }
  const pid = Number.parseInt(holder, 10);
  const gone = Date.now() - since > ABANDONED || (Number.isInteger(pid) && pid > 0 && !running(pid));
  if (!gone) return false;
  try {
    if (readFileSync(lock, 'utf8') !== holder) return false;
    unlinkSync(lock);
  } catch {
    return false;
  }
  return true;
}

function running(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error.code === 'EPERM';
  }
}

/** A file operation another process's open handle can refuse for a moment on Windows, tried again for up to a second. */
function retried(act) {
  for (let attempt = 0; ; attempt++) {
    try {
      return act();
    } catch (error) {
      if (!['EPERM', 'EBUSY', 'EACCES'].includes(error.code) || attempt >= 40) throw error;
      pause(25);
    }
  }
}
