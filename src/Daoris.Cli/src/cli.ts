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
import { commandQuest } from './quest.ts';
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
  quest <verb>         post work to ANOTHER repository's backlog, and track it:
                       post <repo> · take <id> · done <id> · decline <id> · list.
                       Repositories here do not develop across each other

Options:
  --dry-run            print the plan; write nothing
  --force              overwrite locally-drifted files (sync only)
  --all                promote every drifted file (upstream only)
  --title, --body      the quest (quest post); --reason (take/done/decline)
  --help, --version`;

/** Commands are registered here as they land. @returns {number} process exit code */
const commands: Record<string, (args: CommandArgs) => ExitCode> = {
  index: commandIndex,
  sync: commandSync,
  check: commandCheck,
  upstream: commandUpstream,
  init: commandInit,
  status: commandStatus,
  doctor: commandDoctor,
  analyze: commandAnalyze,
  quest: commandQuest,
};

export function runCli(
  argv: string[],
  cwd: string,
  write: (line: string) => void = console.log,
): ExitCode {
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
    return handler({ root: cwd, argv: argv.slice(1), write, packageRoot });
  } catch (error) {
    if (error instanceof DaorisError) {
      write(`daoris: ${error.message}`);
      return error.exitCode;
    }
    throw error;
  }
}

