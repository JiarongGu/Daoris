import { packageRootOf } from './platform/package-root.ts';
import { versionLine } from './platform/build-stamp.ts';
import { DaorisError, type ExitCode } from './platform/errors.ts';
import type { CliCommand } from './platform/types.ts';
import { command as analyze } from './cli/analyze.ts';
import { command as init } from './cli/init.ts';
import { command as sync } from './cli/sync.ts';
import { command as check } from './cli/check.ts';
import { command as upstream } from './cli/upstream.ts';
import { command as index } from './cli/index.ts';
import { command as status } from './cli/status.ts';
import { command as reconcile } from './cli/reconcile.ts';
import { command as doctor } from './cli/doctor.ts';
import { HANDED_VARIABLE, runDriverProgram, type DriverProgramSeam } from './platform/driverprogram.ts';

export { HANDED_VARIABLE };

/** The package root — `src/` sits one level below it, `dist/` likewise once built; an install's bundle is its own folder. */
export const packageRoot = packageRootOf(import.meta.url);

/**
 * The doctrine commands, in the order `daoris --help` prints them, and nothing else: these rows are the one declaration of them. The
 * tree's other groups are the desktop's program's, declared by its own commands, which this dispatcher hands every other word to,
 * whole. Each row is a module of its own under `cli/` carrying its verb, its usage lines and its handler, and its lines in the golden
 * usage (`test/fixtures/cli-usage/help.txt`); `cli-tree.test.ts` holds that each row runs here.
 *
 * Only this file imports the rows, and no row imports this file: a row that reached the table would reach the hand-off through it,
 * and a doctrine command would then reach the one spawn. `cli.test.ts` and `dogfood.test.ts` hold both halves.
 */
export const COMMANDS: readonly CliCommand[] = [
  analyze,
  init,
  sync,
  check,
  upstream,
  index,
  status,
  reconcile,
  doctor,
];

const USAGE = [
  'daoris <command> [options]',
  '',
  ...COMMANDS.flatMap((command) => command.usage),
  '',
  '  These are the doctrine commands, and every one is offline. Every other command',
  "  (`daoris help` names them) is the desktop's program's: this daoris hands it the",
  '  words whole, the one an install carries in app/daoris-driver/ or the one',
  '  DAORIS_DRIVER_PROGRAM names; with neither it says so and does nothing.',
  '',
  'Options:',
  '  --dry-run            print the plan; write nothing',
  ...COMMANDS.flatMap((command) => command.options ?? []),
  '  --help, --version',
].join('\n');

/** Who handed this process its words, read once and taken out of what it passes on, so a word handed once is never handed back. */
function takeHanded(): string | null {
  const handed = process.env[HANDED_VARIABLE] ?? null;
  delete process.env[HANDED_VARIABLE];
  return handed;
}

/**
 * @param driverProgram the desktop's program, which every word outside the doctrine commands is handed to:
 * the real one, or a test's stand-in, which records what it is handed.
 * @param handedBy the program that handed these words here, `driver` where the desktop's program did; read from the environment.
 */
export function runCli(
  argv: string[],
  cwd: string,
  write: (line: string) => void = console.log,
  driverProgram: DriverProgramSeam = (words) => runDriverProgram(words),
  handedBy: string | null = takeHanded(),
): ExitCode | Promise<ExitCode> {
  try {
    const command = argv[0];
    if (command === undefined) {
      write(USAGE);
      return 2;
    }

    // By the table's own verbs: an object lookup also answered for `toString` and `constructor`, Object's members, and ran them.
    const handler = COMMANDS.find((entry) => entry.name === command)?.run;
    if (!handler && command !== '--help' && command !== '--version') {
      // A word handed once is never handed back (the design §11): the desktop's program sent it, and it is no doctrine command.
      if (handedBy === 'driver') {
        throw new DaorisError(`\`${command}\` is no doctrine command, and the desktop's program that handed it here has none either — `
          + "`daoris help` names each.");
      }
      return driverProgram(argv);
    }

    if (argv.includes('--help')) {
      write(USAGE);
      return 0;
    }
    if (!handler || argv.includes('--version')) {
      write(versionLine(packageRoot));
      return 0;
    }

    const result = handler({ root: cwd, argv: argv.slice(1), write, packageRoot });

    // A command may be async, and a `try` does not catch a rejected promise — so a DaorisError thrown
    // inside it escaped as an unhandled rejection and printed a stack trace instead of its message.
    // Exit codes are the contract here; a stack trace is neither the code nor the message.
    return result instanceof Promise ? result.catch(report) : result;
  } catch (error) {
    return report(error);
  }

  function report(error: unknown): ExitCode {
    if (error instanceof DaorisError) {
      write(`daoris: ${error.message}`);
      return error.exitCode;
    }
    // 🔴 A failure nobody anticipated is a TOOL error. Rethrown, Node printed a stack trace and exited
    // 1 — the policy code, which a build gate reads as "the doctrine is wrong".
    write(`daoris: ${error instanceof Error ? error.message : String(error)}`);
    return 2;
  }
}
