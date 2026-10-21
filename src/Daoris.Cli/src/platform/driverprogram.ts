// The seam: `daoris driver|agent|plugin|tool …` run by Daoris's driver program (D194, the alone-path design §1.1, §4).
//
// Every verb of the four management families is the driver's program's (`daoris-driver driver|agent|plugin|tool …`): this module
// starts it, with the same words, through the one spawn site, `startChild`. The families' TypeScript left row by row, PUSH1f2a to
// PUSH1f2f; the agents' declarations went with the plugins, the last of them, whose reserved names are the driver's
// (`AdapterSet.Reserved`, `fixtures/reserved-harnesses.json`).
//
// It is a MANAGEMENT module and it opens no socket. It spawns one program, the driver's; `child_process` is a Node built-in, so the
// zero-dependency guarantee stands.

import { existsSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import type { SpawnSyncReturns, StdioOptions } from 'node:child_process';
import { THIS_INSTALL } from './home.ts';
import { DaorisError } from './errors.ts';
import type { ExitCode } from './errors.ts';

type Env = Record<string, string | undefined>;

/**
 * 🔴 Every child this package starts is started here, and there is one: Daoris's driver program (PUSH1f2a, D194 point 1), handed the
 * terminal's environment exactly. It hands its own children the tools itself, and its `agent install` finds the machine's npm on the
 * PATH it inherited; a managed node's first would install the agent into the managed node's folder. The program is an apphost, never
 * a `.cmd` (`driverProgram` refuses one), so nothing here goes through `cmd.exe`. Held by the dogfood test: the one `spawnSync` is here.
 */
function startChild(command: string[], env: Env, options: { stdio?: StdioOptions }): SpawnSyncReturns<string> {
  const [file, ...argv] = command as [string, ...string[]];
  return spawnSync(file, argv, { ...options, env, encoding: 'utf8', shell: false, windowsHide: true });
}

/** The variable naming Daoris's driver program: the dev loop's and a rehearsal's own build, as `DAORIS_INSTANCE` names their pipe. */
export const DRIVER_PROGRAM_VARIABLE = 'DAORIS_DRIVER_PROGRAM';

/** Where an install carries the driver's program, under its root: the publish's `DRIVER_HOME` (`desktop-publish.test.ts` holds the twin). */
export const DRIVER_PROGRAM_HOME: readonly string[] = Object.freeze(['app', 'daoris-driver']);

/** The driver program's apphost, which a build writes beside its DLL (the host's `AssemblyName`): the publish's `DRIVER_EXE` on Windows. */
export const DRIVER_PROGRAM_FILE = process.platform === 'win32' ? 'daoris-driver.exe' : 'daoris-driver';

/**
 * Daoris's driver program for this `daoris` (D194 point 1, the alone-path design §1.1): the apphost `DAORIS_DRIVER_PROGRAM` names, else
 * the one this package's install carries; else the refusal saying where it is. Never `PATH`, never a checkout's build output found by
 * itself, never the install that holds the home: each is a guess at which build acts. A variable naming anything but an existing file
 * of the apphost's name is refused naming it: a `.dll` no start runs, a `.cmd` or `.bat` Node would hand to `cmd.exe`, where the
 * person's words would be read again, a quoted value, a folder.
 */
export function driverProgram(env: Env, install: string | null): { program: string } | { refusal: string } {
  const named = env[DRIVER_PROGRAM_VARIABLE];
  if (named !== undefined && named.length > 0) {
    const same = process.platform === 'win32' ? named.split(/[\\/]/).at(-1)?.toLowerCase() === DRIVER_PROGRAM_FILE
      : named.split('/').at(-1) === DRIVER_PROGRAM_FILE;
    const file = !named.includes('"') && existsSync(named) && statSync(named).isFile();
    return same && file ? { program: named } : {
      refusal: `\`${DRIVER_PROGRAM_VARIABLE}\` names \`${named}\`, which is not Daoris's driver program: it names the apphost a build `
        + `writes beside its DLL, \`${DRIVER_PROGRAM_FILE}\`, an existing file, never a \`.dll\`, a launcher or a folder; nothing was done.`,
    };
  }

  const carried = install === null ? null : join(install, ...DRIVER_PROGRAM_HOME, DRIVER_PROGRAM_FILE);
  if (carried !== null && existsSync(carried) && statSync(carried).isFile()) return { program: carried };
  return {
    refusal: '`daoris driver` is run by Daoris\'s own driver program, which this `daoris` cannot find: it is not the one a Daoris '
      + 'install carries, and `DAORIS_DRIVER_PROGRAM` names none. Run the install\'s `daoris` (its `app/bin/`), or `daoris-driver driver …` '
      + 'where it is installed; nothing was done.',
  };
}

/** How the driver's program is started: the one spawn site, or a test's stand-in. */
export type DriverStart = (command: string[], env: Env) => { status: number | null; signal: NodeJS.Signals | null; error?: Error };

/** The seam a family's verb is handed, as a family is handed its door: the real one, or the dispatcher's test's stand-in. */
export type DriverProgramSeam = (family: string, words: readonly string[]) => ExitCode;

/** The statuses a Ctrl+C ends a Windows process with, `STATUS_CONTROL_C_EXIT`, as Node reads it unsigned and signed. */
const CTRL_C_STATUSES: ReadonlySet<number> = new Set([0xC000013A, -1073741510]);

/**
 * `daoris <family> …` run by Daoris's driver program (PUSH1f2a, D194 point 1, the alone-path design §1.1): `<program> <family> <words…>`,
 * the words whole, through the one spawn site with the terminal's own streams, so the program reads the person's typing and its
 * prompts reach the person with no copy held here, and with the terminal's environment exactly. Nothing is written before the start.
 * Its exit is the run's: its own 0, 1 and 2 as they are; a start that fails is 2, said with the program and the error; a Ctrl+C's
 * end is 2 with nothing said, the person pressed it; any other status or signal is 2, naming it.
 *
 * @remarks
 * Ctrl+C reaches every process on the console. A listener for `SIGINT` and one for `SIGBREAK` (Windows' Ctrl+Break), each doing nothing,
 * are set before the synchronous start, so neither ends this process while the program runs and its status is the run's; the program
 * takes the key in its families' one handler.
 */
export function runDriverProgram(
  family: string, words: readonly string[],
  seam: { env?: Env; install?: string | null; start?: DriverStart } = {},
): ExitCode {
  const env = seam.env ?? process.env;
  const found = driverProgram(env, seam.install === undefined ? THIS_INSTALL : seam.install);
  if ('refusal' in found) throw new DaorisError(found.refusal, 2);

  const waiting = () => {};
  process.on('SIGINT', waiting);
  process.on('SIGBREAK', waiting);
  const start: DriverStart = seam.start ?? ((command, given) => startChild(command, given, { stdio: 'inherit' }));
  let ran: ReturnType<DriverStart>;
  try {
    ran = start([found.program, family, ...words], env);
  } finally {
    // A key pressed while the program ran was taken by these listeners' handle and is dropped with it: the program had it too.
    process.off('SIGINT', waiting);
    process.off('SIGBREAK', waiting);
  }

  if (ran.error) throw new DaorisError(`\`${found.program}\` could not be started — ${ran.error.message}; nothing was done here.`, 2);
  if (ran.status === 0 || ran.status === 1 || ran.status === 2) return ran.status;
  if (ran.signal === 'SIGINT' || (ran.status !== null && CTRL_C_STATUSES.has(ran.status))) return 2;
  throw new DaorisError(`\`${found.program}\` ended ${ran.signal !== null ? `on ${ran.signal}` : `with status ${ran.status}`}, which is `
    + 'none of its own answers: a program that found no .NET runtime to run on, or one that stopped unexpectedly, ends so.', 2);
}
