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
import { commandImport, commandRetire } from './manage.ts';
import { commandRemote } from './remotes.ts';
import { commandHarness } from './toolchain.ts';
import { commandDriver } from './driverconfig.ts';
import { commandPlugin } from './plugins.ts';
import type { CommandArgs } from './types.ts';

/** The package root — `src/` sits one level below it, `dist/` likewise once built. */
export const packageRoot = dirname(dirname(fileURLToPath(import.meta.url)));

const USAGE = `daoris <command> [options]

  analyze [packs...]   what adopting would do here: collisions, duplicates, budget.
                       Writes nothing; --json for an agent to act on
  init                 detect what this repo has, write daoris.json
  sync                 materialize the manifest's packs; write daoris.lock
  check                drift, staleness, index freshness (offline); the core budget is reported,
                       never enforced
  upstream <file>      promote a locally-edited canonical file back to the canon
  index                regenerate RULES_INDEX.md from what is on disk
  status               summary of packs, drift, local files, and any pending
                       canon update; --machine adds this machine's wiring;
                       --json for an agent to act on
  doctor               report local documents that look like canonical ones
                       under a different name (advisory; never fails)
  connect              register this repo with a knowledge service: what it owns
                       and what it accepts, so siblings know what to ask of it
  retire [name]        take a repository off this machine's registry. Ends the
                       registration ONLY — no file, history or doctrine is touched
  import [folder]      register a folder's subdirectories in one go; safe to
                       re-run, and it never re-points anyone's workspace
  remote [verb]        this machine's remotes, one per workspace:
                         list                      what is wired (keys redacted)
                         add <workspace> --url U   wire a workspace's deployment
                         remove <workspace>        unwire it here; the
                                                   deployment is untouched
  harness [verb]       this machine's agent harnesses and the accounts they run
                       as. Daoris manages directories and names, never secrets:
                         list                      installed? version? profiles?
                         install|update <harness>  its OWN mechanism, never auto
                         login <harness> [--profile P]
                                                   its own login flow, run INTO
                                                   a profile directory
                         profile list|add|remove <harness> <profile>
                         profile default <harness> <profile> [--workspace W]
                         pin <harness> <version> [--workspace W]
                                                   install that version somewhere
                                                   Daoris owns, and run it
                         unpin <harness> [--workspace W]
                                                   back to whatever is on PATH
  driver [verb]        what this machine drives ($DAORIS_HOME/driver.json):
                         list                      adapter, cap, what is opted in
                         drive|undrive <repo>      opt a repository in, or out
                         hold|resume <repo>        pause one, or release it
                         trees <repo> on|off       sessions there open their own
                                                   worktree (D51) — your dirty
                                                   root stops holding the driver
                         notify on|off             say so when a session parks,
                                                   or ends without you asking
                         cap <n> · adapter <name>
  plugin [verb]        this machine's plugins ($DAORIS_HOME/plugins/<id>/plugin.json):
                         list                      each one, what it declares and
                                                   speaks, why a refused one does not
                         add <folder>              copy one in under its id
                         remove <id>               take it out; what it kept stays
                         enable|disable <id>       a row, never a rename

  connect, retire and import are the MANAGEMENT commands: opt-in, they talk to a
  service, and no gate ever runs them. remote, harness, driver and plugin are
  management too and reach no network — they edit files under the Daoris home
  ($DAORIS_HOME, the installed application's own data folder; nothing lives under
  your profile), and harness spawns each harness's own tooling. Every doctrine
  command is offline.

Options:
  --dry-run            print the plan; write nothing
  --force              overwrite locally-drifted files (sync only)
  --all                promote every drifted file (upstream only)
  --machine            report this machine's wiring too (status only)
  --workspace <name>   which workspace this repo shares with, on THIS machine
                       (connect only). Wiring, like a git remote: it is kept in
                       the machine's registry and written into no tracked file.
                       Omit to leave the existing wiring alone
  --url <url>          the deployment a workspace syncs with (remote add)
  --key <key>          its key; or DAORIS_REMOTE_KEY, or typed in (remote add).
                       Never printed back — only its audit prefix
  --profile <name>     which credential profile to act on (harness login) —
                       a named, isolated configuration home for that harness
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
  retire: commandRetire,
  import: commandImport,
  remote: commandRemote,
  harness: commandHarness,
  driver: commandDriver,
  plugin: commandPlugin,
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

