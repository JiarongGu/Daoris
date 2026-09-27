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
import { releaseFetcher } from './service.ts';
import { commandDriver } from './driverconfig.ts';
import { commandPlugin } from './plugins.ts';
import { commandBrowser } from './browser.ts';
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
  index                say where the roster went: the AGENTS.md region sync writes
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
                       re-run, and it never re-points anyone's workspace —
                       unless --workspace W names the circle they land in
  remote [verb]        this machine's remotes, one per workspace:
                         list                      what is wired (keys redacted)
                         add <workspace> --url U   wire a workspace's deployment
                         remove <workspace>        unwire it here; the
                                                   deployment is untouched
  agent [verb]         this machine's agents — Claude Code, Codex, dsh — and the
                       accounts they run as. A sign-in stays the tool's:
                         list                      installed? version? accounts?
                         install|update <agent>    its OWN mechanism, never auto
                         login <agent> [--profile P]
                                                   its own login flow, run INTO
                                                   an account's directory
                         login <agent> --new       sign in to another account:
                                                   kept only if it finished;
                                                   list names who signed in
                         key <agent>               an account that is an API key,
                                                   read from stdin; Daoris keeps it
                         profile list|add|remove <agent> <profile>
                                                   remove deletes the account,
                                                   sign-in and all
                         profile default <agent> <profile> [--workspace W]
                         pin <agent> <version> [--workspace W]
                                                   install that version somewhere
                                                   Daoris owns, and run it. Claude
                                                   Code and Codex come from their
                                                   makers' own channels, verified
                         unpin <agent> [--workspace W]
                                                   back to whatever is on PATH
                         trust <agent> <folder> [--profile P] --yes
                                                   what the agent asks the first
                                                   time it runs in a folder, granted
                                                   in its own file; without --yes,
                                                   the question and nothing written
                         rules [allow|ask|deny|remove <rule>]
                               [--workspace W | --repository R]
                                                   what a session Daoris starts may
                                                   do; rules default <id> on|off
                         rules proposals|accept|decline <id>
                                                   what agents asked to change
  driver [verb]        what this machine drives ($DAORIS_HOME/driver.json):
                         list                      adapter, cap, what is opted in
                         drive|undrive <repo>      opt a repository in, or out
                         hold|resume <repo>        pause one, or release it
                         trees <repo> on|off       sessions there open their own
                                                   worktree (D51) — your dirty
                                                   root stops holding the driver
                         notify on|off             say so when a session parks,
                                                   or ends without you asking
                         intake <adapter>|off      answer an ask the declarations
                                                   do not settle with a session
                                                   on that harness, a login each
                         strikes <n>               park a quest after n failed
                                                   sessions; 0 never parks
                         retry <quest> [--at <n>]  start a parked quest again
                         timeout <minutes>         how long one session may run
                         cap <n> · adapter <name>
  plugin [verb]        this machine's plugins ($DAORIS_HOME/plugins/<id>/plugin.json):
                         list                      each one, what it declares and
                                                   speaks, why a refused one does not
                         add <folder>              copy one in under its id
                         remove <id>               take it out; what it kept stays
                         enable|disable <id>       a row, never a rename
  browser [verb]       the in-app browser's favorites ($DAORIS_HOME/browser/favorites.json),
                       the same the window's star keeps:
                         favorite list             what is kept, in the bar's order
                         favorite add <address> [--title T]
                                                   keep a page
                         favorite remove <address> stop keeping it

  connect, retire and import are the MANAGEMENT commands: opt-in, they talk to a
  service, and no gate ever runs them. remote, agent, driver, plugin and browser are
  management too — they edit files under the Daoris home ($DAORIS_HOME, the
  installed application's own data folder; nothing lives under your profile),
  and agent spawns each agent's own tooling. Of them only agent pin opens a
  connection itself: a maker's release channel, for Claude Code and Codex.
  Every doctrine command is offline.

Options:
  --dry-run            print the plan; write nothing
  --force              overwrite locally-drifted files (sync only)
  --all                promote every drifted file (upstream only)
  --machine            report this machine's wiring too (status only)
  --workspace <name>   which workspace this repo shares with, on THIS machine
                       (connect, import). Wiring, like a git remote: it is kept in
                       the machine's registry and written into no tracked file.
                       Omit to leave the existing wiring alone
  --url <url>          the deployment a workspace syncs with (remote add)
  --key <key>          its key; or DAORIS_REMOTE_KEY, or typed in (remote add).
                       Never printed back — only its audit prefix
  --profile <name>     which account to act on (agent login) — a named,
                       isolated configuration home for that agent
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
  // `pin` fetches a vendor's release (AGT2b) through the one module that may reach a network.
  agent: (args) => commandHarness(args, releaseFetcher()),
  driver: commandDriver,
  plugin: commandPlugin,
  browser: commandBrowser,
  analyze: commandAnalyze,
};

/** Verbs that were renamed, and what they are called now. */
const MOVED: Record<string, string> = {
  // The tools a session runs are agents to a person; `harness` stays the code's word (AGT1).
  harness: 'agent',
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
    if (!handler) {
      // A verb that moved says where to, once — it is not a second name for the new one (AGT1).
      const moved = MOVED[command];
      throw new DaorisError(moved
        ? `unknown command '${command}' — it is \`daoris ${moved}\` now`
        : `unknown command '${command}' — run 'daoris --help'`);
    }

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
    // 🔴 A failure nobody anticipated is a TOOL error. Rethrown, Node printed a stack trace and exited
    // 1 — the policy code, which a build gate reads as "the doctrine is wrong" (REV3).
    write(`daoris: ${error instanceof Error ? error.message : String(error)}`);
    return 2;
  }
}

