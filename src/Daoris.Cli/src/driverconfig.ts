// `daoris driver` — this machine's standing driving choices, from a terminal (D50).
//
// The file is `~/.daoris/driver.json` and the driver re-reads it every tick (D46 §6), which is what
// makes management parity cheap here: the desktop's checkboxes and these verbs edit the same file,
// and hand-editing keeps working because the FILE — not the surface — is the truth. A headless
// machine running `daoris-driver` has no checkbox and still has to be told what it may drive.
//
// It is a MANAGEMENT command and it is entirely OFFLINE: it reads and writes one file under the
// profile and talks to nothing. It does not even spawn, unlike its sibling `toolchain.ts`.
//
// EVERY EDIT PRESERVES WHAT IT DID NOT TOUCH. The driver writes fields this build has no verb for
// (`timeoutMinutes`, `pollSeconds`, the per-adapter `commands` map), and an editor that rewrote the
// file from its own idea of the shape would silently delete the command that makes the stub run.

import { existsSync, readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';
import { DaorisError } from './errors.ts';
import { writeTextAtomic } from './fsx.ts';
import { TOOLCHAINS } from './toolchain.ts';
import type { CommandArgs } from './types.ts';
import type { ExitCode } from './errors.ts';

export const PATH_VARIABLE = 'DAORIS_DRIVER_CONFIG';

/** The file's path: the override, or the conventional home beside the remotes map. */
export function driverConfigPath(env: Record<string, string | undefined> = process.env): string {
  return env[PATH_VARIABLE] ?? join(homedir(), '.daoris', 'driver.json');
}

/** The choices, plus everything else the file held — this is an editor, not the file's owner. */
export interface DriverChoices {
  drivable: string[];
  holds: string[];
  cap: number;
  adapter: string;
  rest: Record<string, unknown>;
}

/** Drives nothing, holds nothing — the safe shape silence takes, matching the driver's own default. */
const EMPTY: DriverChoices = { drivable: [], holds: [], cap: 2, adapter: 'claude-code', rest: {} };

/**
 * The choices as they stand.
 *
 * @remarks
 * A missing file is a machine that has opted nothing in — the driver's own reading of it, held here
 * too because two artefacts disagreeing about what an absent file means is exactly how a machine ends
 * up driving something nobody opted in.
 */
export function readDriverChoices(path = driverConfigPath()): DriverChoices {
  if (!existsSync(path)) return { ...EMPTY, rest: {} };

  try {
    const parsed = JSON.parse(readFileSync(path, 'utf8')) as Record<string, unknown>;
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return { ...EMPTY, rest: {} };

    const { drivable, holds, cap, adapter, ...rest } = parsed;
    return {
      drivable: names(drivable),
      holds: names(holds),
      cap: typeof cap === 'number' && cap >= 1 ? Math.floor(cap) : EMPTY.cap,
      adapter: typeof adapter === 'string' && adapter.length > 0 ? adapter : EMPTY.adapter,
      rest,
    };
  } catch {
    // A torn or hand-mangled file is reported, never silently replaced: rewriting it would destroy
    // whatever the person was in the middle of typing, and the driver reads the same file.
    throw new DaorisError(
      `${path} is not readable JSON. Fix it, or delete it to start from nothing — `
      + 'this command will not overwrite a file it could not understand.');
  }
}

/** Write them back, preserving anything this build did not put there. */
export function writeDriverChoices(path: string, choices: DriverChoices): void {
  writeTextAtomic(path, `${JSON.stringify({
    ...choices.rest,
    drivable: choices.drivable,
    holds: choices.holds,
    cap: choices.cap,
    adapter: choices.adapter,
  }, null, 2)}\n`);
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

    case 'adapter': {
      const adapter = named(argv, 'adapter');
      // Not refused against a list: the driver's adapter set is the driver's, and the CLI naming a
      // shorter list than it has would refuse a harness that actually works. The driver errors
      // naming what exists (D23), which is the answer that comes from the side that knows.
      writeDriverChoices(path, { ...choices, adapter });
      write(`daoris: sessions on this machine spawn via \`${adapter}\`.`);
      if (!(adapter in TOOLCHAINS)) {
        write('  Daoris manages no toolchain for that name — `daoris harness list` shows the ones it does.');
      }

      return 0;
    }

    default:
      throw new DaorisError(
        `unknown driver verb '${verb}' — one of: list, drive, undrive, hold, resume, cap, adapter`);
  }

  function list(): ExitCode {
    write(`daoris: ${path}`);
    write(`  adapter    ${choices.adapter}`);
    write(`  cap        ${choices.cap} concurrent session(s)`);

    if (choices.drivable.length === 0) {
      write('  drivable   nothing — this machine drives no repository, which is the default (D46 §2).');
      write('             `daoris driver drive <repository>` opts one in.');
    }

    for (const repository of choices.drivable) {
      const held = choices.holds.some((name) => name.toLowerCase() === repository.toLowerCase());
      write(`  drivable   ${repository}${held ? '  (held by you — `daoris driver resume` releases it)' : ''}`);
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
    for (let at = 1; at < args.length; at += 1) {
      const token = args[at]!;
      if (!token.startsWith('--')) return token;
    }

    throw new DaorisError(
      `\`driver ${verb}\` needs a name — e.g. \`daoris driver ${verb} aurora-engine\`.`);
  }
}

function names(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((name): name is string => typeof name === 'string' && name.length > 0) : [];
}
