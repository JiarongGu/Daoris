// `daoris harness` — this machine's agent harnesses and the accounts they run as (D49 §4, D50).
//
// NOT to be confused with `harness.ts`, which is about a harness's DOCTRINE LAYOUT — where an agent
// tool loads rules from. This file is about the harness as a TOOL: is it installed, at what version,
// and under which credential profile does a session spawn.
//
// Management parity, in the shape the driver proved: THE FILES ARE THE API and this is an editor over
// them. `~/.daoris/harnesses.json` holds which profile each harness runs as; `~/.daoris/harnesses/
// <harness>/<profile>/` IS the profile — a directory, so nothing has to agree with a register about
// which profiles exist. The desktop's roster edits the same file and the same directories.
//
// It is the CLI's twin of the driver's `Harnesses.cs`. Two copies exist because the two artefacts
// share no code — the FILE AND THE LAYOUT are the contract — so these rules must hold in both, and
// each carries a test table saying so:
//
//   1. A profile is a directory; the directories that exist are the profiles that exist.
//   2. Resolution is: the person's pick, then the workspace's default, then the machine's, then NONE.
//   3. None means the harness's OWN configuration home — the environment seam is not set at all.
//
// It is a MANAGEMENT command and it opens no socket. It does spawn processes — that is the whole
// point: install, update and login are each harness's OWN mechanism, run by Daoris rather than
// remembered by hand. `child_process` is a Node built-in, so the zero-dependency guarantee stands.
//
// DAORIS NEVER SEES, STORES OR COPIES A CREDENTIAL. It manages directories and names; login runs the
// harness's own flow INTO a profile directory, and whatever that obtains the harness stores itself.

import { existsSync, mkdirSync, readdirSync, readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { flagValue } from './args.ts';
import { DaorisError } from './errors.ts';
import { writeTextAtomic } from './fsx.ts';
import { normalizeWorkspace } from './remotemap.ts';
import type { CommandArgs } from './types.ts';
import type { ExitCode } from './errors.ts';

export const PATH_VARIABLE = 'DAORIS_HARNESS_CONFIG';

/** Where the wiring lives; the profile tree sits beside it. */
export function harnessesPath(env: Record<string, string | undefined> = process.env): string {
  return env[PATH_VARIABLE] ?? join(homedir(), '.daoris', 'harnesses.json');
}

/** The directory profiles live under — `~/.daoris` on an ordinary machine. */
export function harnessHome(path = harnessesPath()): string {
  return join(path, '..');
}

/**
 * What Daoris knows about a harness as a TOOL.
 *
 * @remarks
 * Every field is a claim about somebody else's program, so every one of them was verified against the
 * real binary before it was written here. `install` is a whole command because a machine without the
 * harness cannot run the harness; `update` and `login` are its own subcommands, because a present
 * harness updates and authenticates itself.
 */
export interface Toolchain {
  /** The default command, when nothing on this machine names another. */
  binary: string[];
  /** How to ask its version. */
  version: string[];
  /** The environment seam: the variable naming this harness's configuration home. */
  profileVariable: string;
  /** Its own installer — a whole command; the harness may not exist yet. */
  install?: string[];
  /** Its own updater, as arguments to the binary. */
  update?: string[];
  /** Its own login flow, as arguments to the binary, run with a profile home in the environment. */
  login?: string[];
  /**
   * How it answers "is this configuration home logged in?" — and the two shapes that answer takes.
   * Both supported harnesses EXIT 0 EITHER WAY, so the output is the answer and the exit code is
   * deliberately not consulted. Neither pattern matching means unknown, never logged-out.
   */
  loginCheck?: { args: string[]; in: RegExp; out: RegExp };
}

/**
 * The harnesses this build knows about.
 *
 * @remarks
 * `codex` is here even though no session adapter spawns it yet (D23: an adapter arrives deliberately,
 * never guessed) — because managing the TOOL and spawning sessions on it are different questions, and
 * a person setting a machine up wants to see what is installed before anything drives it. Its
 * mechanisms were verified the same way the supported harness's were.
 */
export const TOOLCHAINS: Record<string, Toolchain> = {
  'claude-code': {
    binary: ['claude'],
    version: ['--version'],
    profileVariable: 'CLAUDE_CONFIG_DIR',
    install: ['npm', 'install', '-g', '@anthropic-ai/claude-code'],
    update: ['update'],
    login: ['auth', 'login'],
    // It answers JSON — and volunteers an email, an organisation and a subscription tier with it.
    // Daoris reads the boolean and keeps nothing else.
    loginCheck: { args: ['auth', 'status'], in: /"loggedIn"\s*:\s*true/i, out: /"loggedIn"\s*:\s*false/i },
  },
  codex: {
    binary: ['codex'],
    version: ['--version'],
    profileVariable: 'CODEX_HOME',
    install: ['npm', 'install', '-g', '@openai/codex'],
    update: ['update'],
    login: ['login'],
    // ANCHORED, and that is load-bearing: this harness answers a sentence rather than a field, and
    // "Not logged in" contains "logged in". An unanchored pattern reported every logged-out profile
    // as logged in — found by a test, which is the only way a thing like this is ever found.
    loginCheck: { args: ['login', 'status'], in: /^\s*logged in/im, out: /^\s*not logged in/im },
  },
};

/** Which profile each harness runs as: per machine, and optionally per workspace. */
export interface HarnessSettings {
  defaults: Record<string, string>;
  workspaces: Record<string, Record<string, string>>;
  /** Everything else the file held, preserved — this is an editor, not the file's owner. */
  rest: Record<string, unknown>;
}

/**
 * The wiring as it stands.
 *
 * @remarks
 * A missing file is a machine that named no profiles; so is an unreadable one. This is wiring, and
 * absent wiring is the documented default (D21) — a hand-mangled file must never be the thing that
 * stops a session spawning, and the person is told rather than stopped.
 */
export function readHarnessSettings(path = harnessesPath()): HarnessSettings {
  const empty: HarnessSettings = { defaults: {}, workspaces: {}, rest: {} };
  if (!existsSync(path)) return empty;

  try {
    const parsed = JSON.parse(readFileSync(path, 'utf8')) as Record<string, unknown>;
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return empty;

    const { defaults, workspaces, ...rest } = parsed;
    return {
      defaults: stringMap(defaults),
      workspaces: Object.fromEntries(
        Object.entries(asObject(workspaces)).map(([circle, map]) => [circle, stringMap(map)])),
      rest,
    };
  } catch {
    return empty;
  }
}

/** Write it back, preserving anything this build did not put there. */
export function writeHarnessSettings(path: string, settings: HarnessSettings): void {
  const workspaces = Object.fromEntries(
    Object.entries(settings.workspaces)
      .filter(([, map]) => Object.keys(map).length > 0)
      .sort(([a], [b]) => (a < b ? -1 : 1)));

  writeTextAtomic(path, `${JSON.stringify({
    ...settings.rest,
    defaults: sorted(settings.defaults),
    workspaces,
  }, null, 2)}\n`);
}

/**
 * Which profile a spawn runs as: the person's pick, the workspace's default, the machine's, or none.
 *
 * @remarks
 * None is the important one. It means the harness's OWN configuration home, untouched — not an empty
 * profile directory. Pointing someone who never asked for profiles at a fresh configuration home
 * would log them out of their own tool, which is the loudest possible way to break "Daoris works
 * alone" (D48 §2a).
 */
export function resolveProfile(
  settings: HarnessSettings, harness: string, workspace?: string | null, chosen?: string | null,
): string | null {
  if (chosen?.trim()) return chosen.trim();

  const circle = workspace?.trim() ? settings.workspaces[workspace.trim()] : undefined;
  if (circle?.[harness]?.trim()) return circle[harness]!.trim();

  return settings.defaults[harness]?.trim() || null;
}

/** Where a named profile's configuration home is. Daoris owns this location and nothing inside it. */
export function profileHome(home: string, harness: string, profile: string): string {
  return join(home, 'harnesses', safeName(harness, 'harness name'), safeName(profile, 'profile name'));
}

/** The profiles that exist — the directories that exist, sorted. There is no second register. */
export function profiles(home: string, harness: string): string[] {
  const root = join(home, 'harnesses', safeName(harness, 'harness name'));
  if (!existsSync(root)) return [];

  return readdirSync(root, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort();
}

/**
 * A name that will become a directory. Refused rather than normalized: a name carrying a separator or
 * a traversal is a request to own a location somewhere else, and quietly rewriting it would put a
 * profile where nobody would look for it.
 */
function safeName(value: string, what: string): string {
  const trimmed = value?.trim() ?? '';
  const bad = trimmed.length === 0 || trimmed === '.' || trimmed === '..'
    || /[\\/:*?"<>|]/.test(trimmed);

  if (bad) {
    throw new DaorisError(
      `\`${value}\` is not a usable ${what} — letters, digits, dashes. A profile is a directory `
      + 'Daoris owns the location of, so its name may not point anywhere else.');
  }

  return trimmed;
}

/** What one harness looks like on this machine right now. */
export interface HarnessReport {
  harness: string;
  present: boolean;
  version: string | null;
  problem: string | null;
  machineDefault: string | null;
  profiles: { name: string; home: string; login: 'in' | 'out' | 'unknown' }[];
}

/**
 * Run one of the harness's own reporting commands and collect what it said.
 *
 * @remarks
 * Synchronous on purpose: this is a person at a terminal waiting for one answer, and `spawnSync`
 * keeps the whole command a straight line with no lifetime to get wrong.
 */
function ask(
  command: string[], args: string[], profile: string | null, toolchain: Toolchain,
): { ran: boolean; output: string; problem: string | null } {
  const env = { ...process.env };
  if (profile) {
    // Created as part of selecting it: at least one supported harness refuses to start when its home
    // variable names a path that does not exist.
    mkdirSync(profile, { recursive: true });
    env[toolchain.profileVariable] = profile;
  }

  const result = spawnSync(command[0]!, [...command.slice(1), ...args], {
    env, encoding: 'utf8', shell: false, timeout: 20_000, windowsHide: true,
  });

  if (result.error) {
    return { ran: false, output: '', problem: `\`${command[0]}\` is not on this machine's PATH` };
  }

  // Both streams: a harness that reports its version on stderr is not an absent harness. The exit
  // code is deliberately not consulted — see `Toolchain.loginCheck`.
  return { ran: true, output: `${result.stdout ?? ''}\n${result.stderr ?? ''}`, problem: null };
}

/** Probe one harness: present, version, and every profile's login state. */
export function probe(
  harness: string, toolchain: Toolchain,
  home = harnessHome(), settings = readHarnessSettings(),
): HarnessReport {
  const version = ask(toolchain.binary, toolchain.version, null, toolchain);

  return {
    harness,
    present: version.ran,
    version: version.ran ? firstLine(version.output) : null,
    problem: version.problem,
    machineDefault: settings.defaults[harness] ?? null,
    profiles: profiles(home, harness).map((name) => {
      const where = profileHome(home, harness, name);
      if (!version.ran || !toolchain.loginCheck) return { name, home: where, login: 'unknown' as const };

      const answer = ask(toolchain.binary, toolchain.loginCheck.args, where, toolchain);
      if (!answer.ran) return { name, home: where, login: 'unknown' as const };
      if (toolchain.loginCheck.in.test(answer.output)) return { name, home: where, login: 'in' as const };
      return {
        name,
        home: where,
        login: toolchain.loginCheck.out.test(answer.output) ? ('out' as const) : ('unknown' as const),
      };
    }),
  };
}

/**
 * Read or change this machine's harnesses and the accounts they run as.
 *
 * @remarks
 * Verbs, not flags: `install` and `login` do entirely different things to different parts of the
 * machine, and a boolean distinguishing them is the shape that eventually gets defaulted wrong.
 */
export function commandHarness({ argv, write }: CommandArgs): ExitCode {
  const verb = argv[0] ?? 'list';
  const path = harnessesPath();
  const home = harnessHome(path);

  switch (verb) {
    case 'list':
      return list();

    case 'install': {
      const { name, toolchain } = required(argv, 'install');
      if (!toolchain.install) {
        throw new DaorisError(
          `\`${name}\` declares no installer, so Daoris has no sanctioned way to install it. Install `
          + 'it with its own tooling; `daoris harness list` will find it afterwards.');
      }

      write(`daoris: installing \`${name}\` with its own installer — nothing here is automatic (D49 §4).`);
      return relay(toolchain.install, null, toolchain, write);
    }

    case 'update': {
      const { name, toolchain } = required(argv, 'update');
      if (!toolchain.update) {
        throw new DaorisError(`\`${name}\` declares no updater — it updates itself, or its package manager does.`);
      }

      write(`daoris: updating \`${name}\`. Never mid-session and never unasked: a tool that changed`);
      write('  under a running loop is a moving target nobody diffed.');
      return relay([...toolchain.binary, ...toolchain.update], null, toolchain, write);
    }

    case 'login': {
      const { name, toolchain } = required(argv, 'login');
      if (!toolchain.login) {
        throw new DaorisError(
          `\`${name}\` declares no login flow — log in with its own tooling, pointing `
          + `${toolchain.profileVariable} at the profile directory.`);
      }

      const profile = flagValue(argv, '--profile')
        ?? readHarnessSettings(path).defaults[name]
        ?? 'default';
      const where = profileHome(home, name, profile);

      write(`daoris: running \`${name}\`'s own login flow into the profile \`${profile}\`.`);
      write(`  ${where}`);
      write('  Daoris chose the directory and nothing else: whatever you sign in with is stored by');
      write('  the harness, in its own store, under your OS account — Daoris never sees it.');
      return relay([...toolchain.binary, ...toolchain.login], where, toolchain, write);
    }

    case 'profile':
      return profileVerb();

    default:
      throw new DaorisError(
        `unknown harness verb '${verb}' — one of: list, install, update, login, profile`);
  }

  function list(): ExitCode {
    const settings = readHarnessSettings(path);
    write(`daoris: ${path}`);
    for (const [name, toolchain] of Object.entries(TOOLCHAINS)) {
      const report = probe(name, toolchain, home, settings);
      write('');
      write(`  ${name.padEnd(14)} ${report.present ? report.version : `absent — ${report.problem}`}`);
      if (!report.present && toolchain.install) {
        write(`  ${''.padEnd(14)} \`daoris harness install ${name}\` installs it, with its own installer`);
      }

      if (report.profiles.length === 0) {
        write(`  ${''.padEnd(14)} no profiles — sessions run in the harness's own configuration home`);
        continue;
      }

      for (const profile of report.profiles) {
        const marks = [
          report.machineDefault === profile.name ? 'machine default' : null,
          ...Object.entries(settings.workspaces)
            .filter(([, map]) => map[name] === profile.name)
            .map(([circle]) => `default in ${circle}`),
        ].filter(Boolean);

        write(
          `  ${''.padEnd(14)} ${profile.name.padEnd(16)} ${profile.login.padEnd(8)}`
          + `${marks.length ? ` (${marks.join(', ')})` : ''}`);
      }
    }

    write('');
    write('  A profile is a directory Daoris owns the location of. The credential inside it belongs to');
    write("  the harness's own store — Daoris manages directories and names, never secrets.");
    return 0;
  }

  function profileVerb(): ExitCode {
    const action = argv[1] ?? 'list';
    const settings = readHarnessSettings(path);

    switch (action) {
      case 'list': {
        for (const name of Object.keys(TOOLCHAINS)) {
          const existing = profiles(home, name);
          write(`  ${name.padEnd(14)} ${existing.length ? existing.join(', ') : '(none)'}`);
        }

        return 0;
      }

      case 'add': {
        const { name } = namedHarness(argv[2], 'profile add');
        const profile = bare(argv, 3, 'profile add', '<harness> <profile>');
        const where = profileHome(home, name, profile);
        const existed = existsSync(where);
        mkdirSync(where, { recursive: true });

        write(existed
          ? `daoris: \`${name}\` profile \`${profile}\` already exists — ${where}`
          : `daoris: \`${name}\` profile \`${profile}\` — ${where}`);
        write(`  It is empty until you log into it: \`daoris harness login ${name} --profile ${profile}\`.`);
        return 0;
      }

      case 'remove': {
        const { name } = namedHarness(argv[2], 'profile remove');
        const profile = bare(argv, 3, 'profile remove', '<harness> <profile>');
        const where = profileHome(home, name, profile);

        // Deliberately NOT deleted. The directory holds a credential the harness put there, and a
        // verb that quietly destroyed one would be the kind of irreversible act this family never
        // does silently. Un-defaulting is the reversible half, and it is what was asked for.
        const cleared = { ...settings, defaults: { ...settings.defaults } };
        if (cleared.defaults[name] === profile) delete cleared.defaults[name];
        cleared.workspaces = Object.fromEntries(
          Object.entries(settings.workspaces).map(([circle, map]) => {
            if (map[name] !== profile) return [circle, map];
            const copy = { ...map };
            delete copy[name];
            return [circle, copy];
          }));
        writeHarnessSettings(path, cleared);

        write(`daoris: \`${profile}\` is no longer a default for \`${name}\` anywhere on this machine.`);
        write(`  The directory is untouched — ${where}`);
        write('  It holds a credential the harness put there; deleting it is yours to do, deliberately.');
        return 0;
      }

      case 'default': {
        const { name } = namedHarness(argv[2], 'profile default');
        const profile = bare(argv, 3, 'profile default', '<harness> <profile> [--workspace <name>]');
        const workspace = flagValue(argv, '--workspace');
        // Refused rather than created: naming a default that does not exist is a typo with a silent
        // wrong answer available — every spawn in that circle would refuse, and the message would be
        // about logging in rather than about the name.
        if (!profiles(home, name).includes(profile)) {
          throw new DaorisError(
            `\`${name}\` has no profile \`${profile}\` on this machine — \`daoris harness profile add `
            + `${name} ${profile}\` creates it. Profiles that exist: `
            + `${profiles(home, name).join(', ') || '(none)'}`);
        }

        writeHarnessSettings(path, workspace
          ? {
            ...settings,
            workspaces: {
              ...settings.workspaces,
              [normalizeWorkspace(workspace)]: {
                ...settings.workspaces[normalizeWorkspace(workspace)], [name]: profile,
              },
            },
          }
          : { ...settings, defaults: { ...settings.defaults, [name]: profile } });

        write(workspace
          ? `daoris: sessions in \`${normalizeWorkspace(workspace)}\` run \`${name}\` as \`${profile}\`.`
          : `daoris: this machine runs \`${name}\` as \`${profile}\` by default.`);
        write(`  Written to ${path} — machine-local, tracked by nothing, like every wiring file here.`);
        return 0;
      }

      default:
        throw new DaorisError(
          `unknown harness profile verb '${action}' — one of: list, add, remove, default`);
    }
  }

  /** The harness named after the verb, refused rather than defaulted. */
  function required(args: string[], verb: string): { name: string; toolchain: Toolchain } {
    return namedHarness(bare(args, 1, verb, '<harness>'), verb);
  }

  function namedHarness(value: string | undefined, verb: string): { name: string; toolchain: Toolchain } {
    if (!value) {
      throw new DaorisError(
        `\`harness ${verb}\` needs a harness — one of: ${Object.keys(TOOLCHAINS).join(', ')}`);
    }

    const toolchain = TOOLCHAINS[value];
    if (!toolchain) {
      // Never a silent fallback: a machine that installed a different harness than the person named
      // is the same failure as a repository that asked for one layout and received another (D23).
      throw new DaorisError(
        `unknown harness '${value}' — one of: ${Object.keys(TOOLCHAINS).join(', ')}. `
        + 'A harness is added deliberately, never guessed.');
    }

    return { name: value, toolchain };
  }

  function bare(args: string[], from: number, verb: string, shape: string): string {
    for (let at = from; at < args.length; at += 1) {
      const token = args[at]!;
      if (token === '--profile' || token === '--workspace') at += 1;
      else if (!token.startsWith('--')) return token;
    }

    throw new DaorisError(`\`harness ${verb}\` needs ${shape} — e.g. \`daoris harness ${verb} claude-code\``);
  }

  /**
   * Spawn one of the harness's own commands with the terminal attached.
   *
   * @remarks
   * Inherited stdio rather than captured: a login flow asks questions, opens a browser and waits for
   * a code, and a person needs to answer it. Capturing the stream to pretty-print it would turn a
   * working login into a hung one — the desktop's roster relays these through the session console
   * instead, which is the same process wired for a surface that has no terminal.
   */
  function relay(
    command: string[], profile: string | null, toolchain: Toolchain, out: (line: string) => void,
  ): ExitCode {
    out(`  $ ${command.join(' ')}`);
    out('');

    const env = { ...process.env };
    if (profile) {
      mkdirSync(profile, { recursive: true });
      env[toolchain.profileVariable] = profile;
    }

    const result = spawnSync(command[0]!, command.slice(1), {
      env, stdio: 'inherit', shell: false, windowsHide: true,
    });

    if (result.error) {
      throw new DaorisError(
        `\`${command[0]}\` could not be run — ${result.error.message}. `
        + 'Daoris spawns the harness\'s own tooling; it does not vendor a copy of it.');
    }

    // The harness's exit code is the answer, mapped onto this family's contract: anything non-zero is
    // a tool error, because a failed install is not a policy decision.
    return result.status === 0 ? 0 : 2;
  }
}

function firstLine(output: string): string | null {
  for (const line of output.split('\n')) {
    if (line.trim()) return line.trim();
  }

  return null;
}

function asObject(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' && !Array.isArray(value)
    ? value as Record<string, unknown>
    : {};
}

function stringMap(value: unknown): Record<string, string> {
  return Object.fromEntries(
    Object.entries(asObject(value)).filter(([, v]) => typeof v === 'string' && v.length > 0),
  ) as Record<string, string>;
}

function sorted(map: Record<string, string>): Record<string, string> {
  return Object.fromEntries(Object.entries(map).sort(([a], [b]) => (a < b ? -1 : 1)));
}
