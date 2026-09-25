// `daoris driver` — this machine's standing driving choices, from a terminal (D50).
//
// The file is the home's `driver.json` (`$DAORIS_HOME`, D63) and the driver re-reads it every tick (D46 §6), which is what
// makes management parity cheap here: the desktop's checkboxes and these verbs edit the same file,
// and hand-editing keeps working because the FILE — not the surface — is the truth. A headless
// machine running `daoris-driver` has no checkbox and still has to be told what it may drive.
//
// It is a MANAGEMENT command and it is entirely OFFLINE: it reads and writes one file under the
// home and talks to nothing. It does not even spawn, unlike its sibling `toolchain.ts`.
//
// EVERY EDIT PRESERVES WHAT IT DID NOT TOUCH. The driver writes fields this build has no verb for
// (`timeoutMinutes`, `pollSeconds`, the per-adapter `commands` map), and an editor that rewrote the
// file from its own idea of the shape would silently delete the command that makes the stub run.

import { requireHomeFile } from './home.ts';
import { DaorisError } from './errors.ts';
import { operands } from './args.ts';
import { readJsonObject, writeJsonAtomic } from './fsx.ts';
import { TOOLCHAINS } from './toolchain.ts';
import type { CommandArgs } from './types.ts';
import type { ExitCode } from './errors.ts';

export const PATH_VARIABLE = 'DAORIS_DRIVER_CONFIG';

/** The file's path: the override, or the conventional home beside the remotes map. */
export function driverConfigPath(env: Record<string, string | undefined> = process.env): string {
  return env[PATH_VARIABLE] ?? requireHomeFile(env, 'driver.json');
}

/** The choices, plus everything else the file held — this is an editor, not the file's owner. */
export interface DriverChoices {
  drivable: string[];
  holds: string[];
  /** Repositories whose sessions open their own worktree instead of the registered root (D51). */
  trees: string[];
  cap: number;
  adapter: string;
  /** Whether this machine says so when a session parks or ends unasked (SURF5b). */
  notify: boolean;
  /** How many failed sessions park a quest (DRV6). `0` never parks — the behaviour before it existed. */
  strikes: number;
  /** Quests the person restarted, and the failure count each was restarted at. */
  forgiven: Record<string, number>;
  /** The harness an ask's intake session runs on (INT4b, D65 §1b) — null, and no intake runs. */
  intakeAdapter: string | null;
  rest: Record<string, unknown>;
}

/** Drives nothing, holds nothing — the safe shape silence takes, matching the driver's own default. */
const EMPTY: DriverChoices = {
  drivable: [], holds: [], trees: [], cap: 2, adapter: 'claude-code', notify: true,
  strikes: 3, forgiven: {}, intakeAdapter: null, rest: {},
};

/**
 * The choices as they stand.
 *
 * @remarks
 * A missing file is a machine that has opted nothing in — the driver's own reading of it, held here
 * too because two artefacts disagreeing about what an absent file means is exactly how a machine ends
 * up driving something nobody opted in.
 */
export function readDriverChoices(path = driverConfigPath()): DriverChoices {
  const { value: parsed, problem } = readJsonObject(path);
  if (problem !== null) {
    // A torn or hand-mangled file is reported, never silently replaced: rewriting it would destroy
    // whatever the person was in the middle of typing, and the driver reads the same file.
    throw new DaorisError(`${problem}. Fix it, or delete it to start from nothing — `
      + 'this command will not overwrite a file it could not understand.');
  }
  if (parsed === null) return { ...EMPTY, rest: {} };

  const { drivable, holds, trees, cap, adapter, notify, strikes, forgiven, intakeAdapter, ...rest } = parsed;
  return {
    drivable: names(drivable),
    holds: names(holds),
    trees: names(trees),
    cap: typeof cap === 'number' && cap >= 1 ? Math.floor(cap) : EMPTY.cap,
    adapter: typeof adapter === 'string' && adapter.length > 0 ? adapter : EMPTY.adapter,
    // 🔴 Absent means the DEFAULT, and this is the OPPOSITE reading from `notify` directly above.
    // A machine whose file predates this field is the one that has been driving unattended longest,
    // so silence there must not mean "never park". Zero is settable and means exactly that.
    strikes: typeof strikes === 'number' && strikes >= 0 ? Math.floor(strikes) : EMPTY.strikes,
    forgiven: marks(forgiven),
    // 🔴 Absent means ON, the same reading the driver makes (SURF5b): every machine that already
    // has this file predates the field, and taking silence for "off" would ship the feature
    // switched off on exactly the machines that have been driving longest.
    notify: typeof notify === 'boolean' ? notify : EMPTY.notify,
    // 🔴 Absent means OFF — the opposite of `notify`, the driver's own reading (INT4b): an intake
    // spends a real login on every ask, so it runs only where a person named the harness for it.
    intakeAdapter: typeof intakeAdapter === 'string' && intakeAdapter.trim().length > 0
      ? intakeAdapter.trim() : null,
    rest,
  };
}

/** Write them back, preserving anything this build did not put there. */
export function writeDriverChoices(path: string, choices: DriverChoices): void {
  writeJsonAtomic(path, {
    ...choices.rest,
    drivable: choices.drivable,
    holds: choices.holds,
    trees: choices.trees,
    cap: choices.cap,
    adapter: choices.adapter,
    notify: choices.notify,
    strikes: choices.strikes,
    forgiven: choices.forgiven,
    // Written only when named — absent IS off, and the driver writes it the same way.
    ...(choices.intakeAdapter ? { intakeAdapter: choices.intakeAdapter } : {}),
  });
}

/**
 * Read or change what this machine drives.
 *
 * @remarks
 * `drive`/`undrive` and `hold`/`resume` are pairs of verbs rather than one verb with a boolean,
 * because they mean different things: opting a repository IN is a standing decision, and holding one
 * is a pause on something already opted in. A surface that collapsed them would lose that a held
 * repository is still drivable.
 */
export function commandDriver({ argv, write }: CommandArgs): ExitCode {
  const verb = argv[0] ?? 'list';
  const path = driverConfigPath();
  const choices = readDriverChoices(path);

  switch (verb) {
    case 'list':
      return list();

    case 'drive':
      return toggle('drivable', named(argv, 'drive'), true);

    case 'undrive':
      return toggle('drivable', named(argv, 'undrive'), false);

    case 'hold':
      return toggle('holds', named(argv, 'hold'), true);

    case 'resume':
      return toggle('holds', named(argv, 'resume'), false);

    // One flag with two directions rather than a verb pair, unlike drive/undrive and hold/resume:
    // those pairs mean different things (a standing decision vs a pause on one), where this is a
    // single standing switch and inventing two words for it would imply a difference that is not there.
    case 'trees': {
      const repository = named(argv, 'trees');
      const direction = argv.slice(1).find((token) => token === 'on' || token === 'off');
      if (!direction) {
        throw new DaorisError(
          '`driver trees` needs on|off — e.g. `daoris driver trees aurora-engine on`.');
      }

      const on = direction === 'on';
      const kept = choices.trees.filter((name) => name.toLowerCase() !== repository.toLowerCase());
      writeDriverChoices(path, { ...choices, trees: on ? [...kept, repository] : kept });

      write(on
        ? `daoris: sessions in \`${repository}\` open their own worktree (D51).`
        : `daoris: sessions in \`${repository}\` run in its registered root again.`);
      if (on) {
        write('  A fresh tree holds nothing git does not track — no installed dependencies, no build');
        write('  outputs — so a session pays that repository\'s own setup cost per tree. In exchange,');
        write('  your uncommitted work in the root no longer holds the driver.');
      }

      write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
      return 0;
    }

    // One flag with two directions, like `trees` and for the same reason. The desktop has a
    // checkbox for this; a machine with no screen has this verb (D50's two doors, SURF5b).
    case 'notify': {
      const direction = argv.slice(1).find((token) => token === 'on' || token === 'off');
      if (!direction) {
        throw new DaorisError('`driver notify` needs on|off — e.g. `daoris driver notify off`.');
      }

      const on = direction === 'on';
      writeDriverChoices(path, { ...choices, notify: on });

      write(on
        ? 'daoris: this machine says so when a session parks or ends without you asking.'
        : 'daoris: this machine will not interrupt you about sessions.');
      if (!on) {
        write('  The records still say everything they said — this is about being TOLD, not about');
        write('  what is recorded. A parked session waits for you either way.');
      }

      write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
      return 0;
    }

    case 'cap': {
      const value = Number(named(argv, 'cap'));
      if (!Number.isInteger(value) || value < 1) {
        throw new DaorisError(
          '`driver cap` needs a whole number of concurrent sessions, at least 1 — e.g. `daoris driver cap 3`.');
      }

      writeDriverChoices(path, { ...choices, cap: value });
      write(`daoris: this machine runs at most ${value} session(s) at once.`);
      return 0;
    }

    // DRV6. A number rather than on|off, because the useful question is not "should it give up" but
    // "after how many" — and 0 is a real answer to it, not the absence of one.
    case 'strikes': {
      const value = Number(named(argv, 'strikes'));
      if (!Number.isInteger(value) || value < 0) {
        throw new DaorisError(
          '`driver strikes` needs a whole number of failed sessions, 0 or more — '
          + 'e.g. `daoris driver strikes 3`, or `0` to never park a quest.');
      }

      writeDriverChoices(path, { ...choices, strikes: value });
      write(value === 0
        ? 'daoris: this machine never parks a quest — it keeps trying, as it did before this setting.'
        : `daoris: a quest is parked after ${value} failed session(s), and waits for you.`);
      if (value === 0) {
        write('  🔴 A quest that fails for a reason no retry can fix will be started again every tick,');
        write('     and every start spends a real login. That is the behaviour this setting exists for.');
      } else {
        write('  A failure is a session that ended without landing anything. A stand-down, a decline,');
        write('  a stop and a held repository are not failures and never count toward this.');
      }

      write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
      return 0;
    }

    // Not `unpark` or `forgive`: the person is saying "try this again", and the mark records where
    // to count from rather than erasing what happened — the records still say it.
    case 'retry': {
      const quest = named(argv, 'retry').replace(/^#/, '');
      const at = argv.indexOf('--at');
      const mark = at !== -1 && argv[at + 1] ? Number(argv[at + 1]) : choices.strikes;
      if (!Number.isInteger(mark) || mark < 0) {
        throw new DaorisError('`driver retry --at` needs a whole number of failures to count from.');
      }

      writeDriverChoices(path, { ...choices, forgiven: { ...choices.forgiven, [quest]: mark } });
      write(`daoris: quest \`#${quest}\` may be started again.`);
      write(`  Counting from ${mark} failure(s) — what already happened is still in the records, and`);
      write(`  ${choices.strikes || 'no'} more failure(s) will park it again.`);
      write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
      return 0;
    }

    case 'adapter': {
      const adapter = named(argv, 'adapter');
      // Not refused against a list: the driver's adapter set is the driver's, and the CLI naming a
      // shorter list than it has would refuse a harness that actually works. The driver errors
      // naming what exists (D23), which is the answer that comes from the side that knows.
      writeDriverChoices(path, { ...choices, adapter });
      write(`daoris: sessions on this machine spawn via \`${adapter}\`.`);
      if (!(adapter in TOOLCHAINS)) {
        write('  Daoris manages no toolchain for that name — `daoris agent list` shows the ones it does.');
      }

      return 0;
    }

    // Which harness answers an ask with an intake session (INT4b, D65 §1b), or `off`. A NAME rather
    // than on|off: which harness answers asks and which does the work are two choices, and changing
    // the second must not quietly move the first. The desktop's `SET_INTAKE` is the other door (D50).
    case 'intake': {
      const named = argv.slice(1).find((token) => !token.startsWith('--'));
      if (!named) {
        throw new DaorisError(
          '`driver intake` needs <adapter>|off — e.g. `daoris driver intake claude-code-acp`, or `off` '
          + 'to answer asks by declarations only.');
      }

      const off = named === 'off';
      writeDriverChoices(path, { ...choices, intakeAdapter: off ? null : named });
      if (off) {
        write('daoris: asks on this machine are answered by declarations only — proposed, never published unasked.');
        write('  An intake already waiting on you still waits; answering its ask ends it.');
      } else {
        write(`daoris: an ask the declarations do not settle opens an intake session via \`${named}\`.`);
        write('  It reads the workspace\'s declarations, publishes the quests itself, and asks you where they do not');
        write('  settle it. 🔴 Every intake spends a real login on that harness\'s account.');
        if (!(named in TOOLCHAINS)) {
          write('  Daoris manages no toolchain for that name — `daoris agent list` shows the ones it does.');
        }
      }

      write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
      return 0;
    }

    default:
      throw new DaorisError(
        `unknown driver verb '${verb}' — one of: list, drive, undrive, hold, resume, trees, notify, `
        + 'strikes, retry, cap, adapter, intake');
  }

  function list(): ExitCode {
    write(`daoris: ${path}`);
    write(`  adapter    ${choices.adapter}`);
    write(`  cap        ${choices.cap} concurrent session(s)`);
    write(`  strikes    ${choices.strikes === 0
      ? 'off — a quest is never parked, and it keeps trying at the cost of a login each time'
      : `${choices.strikes} failed session(s) park a quest`}`);
    for (const [quest, mark] of Object.entries(choices.forgiven)) {
      write(`  retried    #${quest}  (counting from ${mark} failure(s))`);
    }

    write(`  notify     ${choices.notify ? 'on' : 'off'}`
      + `  (a session parking, or ending without you asking${choices.notify ? '' : ' — not said'})`);
    write(choices.intakeAdapter
      ? `  intake     ${choices.intakeAdapter}  (an ask the declarations do not settle opens a session — a login each)`
      : '  intake     off — asks are answered by declarations only; `daoris driver intake <adapter>` names a harness');

    if (choices.drivable.length === 0) {
      write('  drivable   nothing — this machine drives no repository, which is the default (D46 §2).');
      write('             `daoris driver drive <repository>` opts one in.');
    }

    for (const repository of choices.drivable) {
      const held = choices.holds.some((name) => name.toLowerCase() === repository.toLowerCase());
      const trees = choices.trees.some((name) => name.toLowerCase() === repository.toLowerCase());
      write(`  drivable   ${repository}`
        + `${held ? '  (held by you — `daoris driver resume` releases it)' : ''}`
        + `${trees ? '  (sessions open their own tree — D51)' : ''}`);
    }

    // Trees on something not drivable is standing configuration, not an error — the desktop's chat
    // door reads it too — but naming it keeps the list the whole truth.
    for (const repository of choices.trees) {
      if (!choices.drivable.some((name) => name.toLowerCase() === repository.toLowerCase())) {
        write(`  trees      ${repository}  (sessions there open their own tree when anything spawns one)`);
      }
    }

    // A hold on something not opted in is inert, and saying so is the point: it reads as protection
    // and is not. Reported even when NOTHING is drivable — which is exactly the machine where a
    // person is most likely to believe a hold is what is stopping things.
    for (const held of choices.holds) {
      if (!choices.drivable.some((name) => name.toLowerCase() === held.toLowerCase())) {
        write(`  held       ${held}  (not drivable anyway — the hold changes nothing)`);
      }
    }

    return 0;
  }

  function toggle(field: 'drivable' | 'holds', repository: string, present: boolean): ExitCode {
    const kept = choices[field].filter((name) => name.toLowerCase() !== repository.toLowerCase());
    const next = present ? [...kept, repository] : kept;
    writeDriverChoices(path, { ...choices, [field]: next });

    if (field === 'drivable') {
      write(present
        ? `daoris: this machine may drive \`${repository}\`.`
        : `daoris: this machine no longer drives \`${repository}\`.`);
      if (present) {
        write('  Driving is additive (D46 §2): outside sessions and hand work in that repository are');
        write('  untouched, and a quest somebody else already took is never the driver\'s to start.');
      }
    } else {
      write(present
        ? `daoris: \`${repository}\` is held — still drivable, not now.`
        : `daoris: \`${repository}\` is released; the driver may start it again.`);
    }

    write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
    return 0;
  }

  function named(args: string[], verb: string): string {
    // A flag's value is never the name (REV3): `retry --at 2 42` read `2` as the quest.
    const name = operands(args, new Set(['--at']))[1];
    if (name !== undefined) return name;

    throw new DaorisError(
      `\`driver ${verb}\` needs a name — e.g. \`daoris driver ${verb} aurora-engine\`.`);
  }
}

/** The forgiveness marks, as a map of quest id to the failure count it was restarted at. */
function marks(value: unknown): Record<string, number> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const held: Record<string, number> = {};
  for (const [quest, mark] of Object.entries(value as Record<string, unknown>)) {
    if (typeof mark === 'number' && Number.isInteger(mark) && mark > 0) held[quest] = mark;
  }

  return held;
}

function names(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((name): name is string => typeof name === 'string' && name.length > 0) : [];
}
