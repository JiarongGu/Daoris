import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { DaorisError, type ExitCode } from './errors.ts';
import { commandIndex } from './indexgen.ts';
import { commandSync } from './materialize.ts';
import { commandCheck } from './drift.ts';
import { commandUpstream } from './upstream.ts';
import { commandInit, commandStatus } from './commands.ts';
import { commandDoctor } from './twins.ts';
import { commandAnalyze } from './analyze.ts';
import { commandConnect } from './connect.ts';
import type { CommandArgs } from './types.ts';

/** The package root — `src/` sits one level below it, `dist/` likewise once built. */
export const packageRoot = dirname(dirname(fileURLToPath(import.meta.url)));

const USAGE = `daoris <command> [options]

  analyze [packs...]   what adopting would do here: collisions, duplicates, budget.
                       Writes nothing; --json for an agent to act on
  init                 detect what this repo has, write daoris.json
  sync                 materialize the manifest's packs; write daoris.lock
  check                drift, staleness, index freshness, core budget (offline)
  upstream <file>      promote a locally-edited canonical file back to the canon
  index                regenerate RULES_INDEX.md from what is on disk
  status               human summary of packs, drift, and local files
  doctor               report local documents that look like canonical ones
                       under a different name (advisory; never fails)
  connect              register this repo with a knowledge service: what it owns
                       and what it accepts, so siblings know what to ask of it.
                       The ONLY command that uses the network, and it is opt-in

Options:
  --dry-run            print the plan; write nothing
  --force              overwrite locally-drifted files (sync only)
  --all                promote every drifted file (upstream only)
  --help, --version`;

/** Commands are registered here as they land. @returns {number} process exit code */
const commands: Record<string, (args: CommandArgs) => ExitCode | Promise<ExitCode>> = {
  index: commandIndex,
  sync: commandSync,
  check: commandCheck,
  upstream: commandUpstream,
  init: commandInit,
  status: commandStatus,
  doctor: commandDoctor,
  connect: commandConnect,
  analyze: commandAnalyze,
};

export function runCli(
  argv: string[],
  cwd: string,
  write: (line: string) => void = console.log,
): ExitCode | Promise<ExitCode> {
  try {
    if (argv.includes('--help')) {
      write(USAGE);
      return 0;
    }
    if (argv.includes('--version')) {
      write(JSON.parse(readFileSync(join(packageRoot, 'package.json'), 'utf8')).version);
      return 0;
    }

    const command = argv[0];
    if (!command) {
      write(USAGE);
      return 2;
    }

    const handler = commands[command];
    if (!handler) throw new DaorisError(`unknown command '${command}' — run 'daoris --help'`);

    const result = handler({ root: cwd, argv: argv.slice(1), write, packageRoot });

    // One command is async, and a `try` does not catch a rejected promise — so a DaorisError thrown
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
    throw error;
  }
}

