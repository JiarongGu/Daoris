import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { DaorisError, type ExitCode } from './errors.ts';
import type { CliCommand } from './types.ts';
import { command as analyze } from './cli/analyze.ts';
import { command as init } from './cli/init.ts';
import { command as sync } from './cli/sync.ts';
import { command as check } from './cli/check.ts';
import { command as upstream } from './cli/upstream.ts';
import { command as index } from './cli/index.ts';
import { command as status } from './cli/status.ts';
import { command as doctor } from './cli/doctor.ts';
import { command as connect } from './cli/connect.ts';
import { command as retire } from './cli/retire.ts';
import { command as importFolders } from './cli/import.ts';
import { command as remote } from './cli/remote.ts';
import { command as agent } from './cli/agent.ts';
import { command as driver } from './cli/driver.ts';
import { command as plugin } from './cli/plugin.ts';
import { command as browser } from './cli/browser.ts';

/** The package root — `src/` sits one level below it, `dist/` likewise once built. */
export const packageRoot = dirname(dirname(fileURLToPath(import.meta.url)));

/**
 * The commands, in the order `daoris --help` prints them (MOD7). Each is a module of its own under
 * `cli/` carrying its verb, its usage lines and its handler, so a new verb is a new file and one row
 * here. Only this file imports them: a command module that reached the table would reach every other
 * command, the management class's network and spawning included, through it.
 */
export const COMMANDS: readonly CliCommand[] = [
  analyze,
  init,
  sync,
  check,
  upstream,
  index,
  status,
  doctor,
  connect,
  retire,
  importFolders,
  remote,
  agent,
  driver,
  plugin,
  browser,
];

const USAGE = [
  'daoris <command> [options]',
  '',
  ...COMMANDS.flatMap((command) => command.usage),
  '',
  '  connect, retire and import are the MANAGEMENT commands: opt-in, they talk to a',
  '  service, and no gate ever runs them. remote, agent, driver, plugin and browser are',
  '  management too — they edit files under the Daoris home ($DAORIS_HOME, the',
  "  installed application's own data folder; nothing lives under your profile),",
  "  and agent spawns each agent's own tooling. Of them only agent pin and agent",
  "  update open a connection themselves: a maker's release channel, for Claude",
  '  Code and Codex.',
  '  Every doctrine command is offline.',
  '',
  'Options:',
  '  --dry-run            print the plan; write nothing',
  ...COMMANDS.flatMap((command) => command.options ?? []),
  '  --help, --version',
].join('\n');

const handlers: Record<string, CliCommand['run']> =
  Object.fromEntries(COMMANDS.map((command) => [command.name, command.run]));

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

    const handler = handlers[command];
    if (!handler) {
      // A verb that moved says where to, once — it is not a second name for the new one (AGT1).
      const moved = COMMANDS.find((entry) => entry.formerly?.includes(command));
      throw new DaorisError(moved
        ? `unknown command '${command}' — it is \`daoris ${moved.name}\` now`
        : `unknown command '${command}' — run 'daoris --help'`);
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
    // 1 — the policy code, which a build gate reads as "the doctrine is wrong" (REV3).
    write(`daoris: ${error instanceof Error ? error.message : String(error)}`);
    return 2;
  }
}
