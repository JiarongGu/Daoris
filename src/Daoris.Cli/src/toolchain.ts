// `daoris harness` — this machine's agent harnesses and the accounts they run as (D49 §4, D50).
//
// NOT to be confused with `harness.ts`, which is about a harness's DOCTRINE LAYOUT — where an agent
// tool loads rules from. This file is about the harness as a TOOL: is it installed, at what version,
// and under which credential profile does a session spawn.
//
// Management parity, in the shape the driver proved: THE FILES ARE THE API and this is an editor over
// them. The home's `harnesses.json` holds which profile each harness runs as; `harnesses/<harness>/
// <profile>/` beside it IS the profile — a directory, so nothing has to agree with a register about
// which profiles exist. The desktop's roster edits the same file and the same directories. The home
// is `$DAORIS_HOME` (D63) — the installed application's own `data/` folder — and with none set this
// command refuses rather than writing under the user profile.
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

import { existsSync, mkdirSync, readdirSync, readFileSync, rmSync } from 'node:fs';
import { requireHomeFile } from './home.ts';
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
  return env[PATH_VARIABLE] ?? requireHomeFile(env, 'harnesses.json');
}

/** The directory profiles live under — the Daoris home (D63). */
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
  /**
   * The package a managed install fetches, when Daoris owns the binary (TOOL2/D57).
   *
   * @remarks
   * Declared rather than parsed out of `install`, because the two are different questions: `install`
   * is "how does this tool put itself on a machine", and this is "what do I fetch into a directory
   * I own". A harness that declares no package cannot be pinned, and says so.
   */
  package?: string;
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
  /**
   * The harness whose ACCOUNT this one runs as, when it has none of its own (ACP2).
   *
   * @remarks
   * 🔴 **Declared, so that a missing login flow is a fact rather than a gap.** The ACP adapter runs
   * `claude` and reads the configuration home `claude` logged into; it has no login of its own and
   * never will. Without this field its silence is indistinguishable from an omission — and an
   * unanswerable login question is PERMISSIVE (SES3), so an omission would quietly widen what may
   * spawn. `daoris harness login <accountOf>` is the verb the surfaces then name.
   */
  accountOf?: string;
  /**
   * That this harness has **no account at all** — not one of its own, and not one borrowed (ACP3).
   *
   * @remarks
   * 🔴 **The third state, and it is declared for the same reason the second one is.** dsh has no
   * notion of being logged in or out: which model answers is a route in its own `settings.yaml`, and
   * the credential for that route is the person's to place there. So there is nothing to ask and
   * nobody to ask it of — which is a fact about the harness, and is indistinguishable from "nobody
   * wrote the check yet" unless it is written down. Silence remains a failure; this is not silence.
   *
   * A harness declaring this must describe no login anywhere else, or it is saying two things.
   */
  noAccount?: boolean;
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
    package: '@anthropic-ai/claude-code',
    update: ['update'],
    login: ['auth', 'login'],
    // It answers JSON — and volunteers an email, an organisation and a subscription tier with it.
    // Daoris reads the boolean and keeps nothing else.
    loginCheck: { args: ['auth', 'status'], in: /"loggedIn"\s*:\s*true/i, out: /"loggedIn"\s*:\s*false/i },
  },
  // The supported harness over the PROTOCOL door (ACP2/D53). A separate toolchain entry from
  // `claude-code` on purpose: the ACP adapter and `claude` are different packages at different
  // versions, and one pin for both would install the wrong thing under a name somebody trusted.
  //
  // It declares no login flow and no login question. The ACCOUNT belongs to `claude`, and the
  // profile directory carries it — `daoris harness login claude-code` is still the verb, and this
  // adapter reads the home that produced. An unknown login state is permissive, by SES3's rule.
  'claude-code-acp': {
    // 🔴 The BINARY is `claude-agent-acp`, not the adapter's Daoris name — verified against the
    // installed package, after a guess was caught by `list` reporting the pin as not installed.
    binary: ['claude-agent-acp'],
    version: ['--version'],
    profileVariable: 'CLAUDE_CONFIG_DIR',
    install: ['npm', 'install', '-g', '@agentclientprotocol/claude-agent-acp'],
    package: '@agentclientprotocol/claude-agent-acp',
    // It has no login of its own: it runs `claude` and reads the home `claude` logged into.
    accountOf: 'claude-code',
  },
  codex: {
    binary: ['codex'],
    version: ['--version'],
    profileVariable: 'CODEX_HOME',
    install: ['npm', 'install', '-g', '@openai/codex'],
    package: '@openai/codex',
    update: ['update'],
    login: ['login'],
    // ANCHORED, and that is load-bearing: this harness answers a sentence rather than a field, and
    // "Not logged in" contains "logged in". An unanchored pattern reported every logged-out profile
    // as logged in — found by a test, which is the only way a thing like this is ever found.
    loginCheck: { args: ['login', 'status'], in: /^\s*logged in/im, out: /^\s*not logged in/im },
  },
  // Codex over the PROTOCOL door (ACP3/D53, closing HARNESS2). Its own entry beside `codex` for
  // exactly the reason `claude-code-acp` is one beside `claude-code`: the adapter and the harness
  // are different packages at different versions, and one pin for both installs the wrong thing
  // under a name somebody trusted.
  'codex-acp': {
    // 🔴 The BINARY is `codex-acp` — the adapter's own bin, not this entry's Daoris name and not
    // `codex`. Verified against the installed package's `bin` map.
    binary: ['codex-acp'],
    version: ['--version'],
    profileVariable: 'CODEX_HOME',
    install: ['npm', 'install', '-g', '@agentclientprotocol/codex-acp'],
    package: '@agentclientprotocol/codex-acp',
    // No login of its own: it runs `codex` and reads the home `codex` logged into. 🔴 Its
    // logged-out refusal arrives from the WIRE — `session/new` answers "Authentication required" —
    // rather than from a subcommand, so there is nothing here to ask.
    accountOf: 'codex',
  },
  // dsh over the protocol door (ACP3/D53). A profile IS a home here: `dsh --profile <name>` boots a
  // directory under `$DSH_HOME/profiles`, so one variable isolates credentials, settings and
  // sessions together.
  //
  // No login flow and no login question: dsh has no account to be out of, and only a definite *out*
  // refuses (SES3), so `unknown` is permissive and a session starts. No model is named (D24) —
  // which model answers is the profile's own `settings.yaml`.
  dsh: {
    binary: ['dsh'],
    version: ['--version'],
    profileVariable: 'DSH_HOME',
    // Pinned exact and vendored nowhere: 561 MB per machine, and a harness's own packaging is its
    // own problem — but the version the toolchain installs is asserted, not assumed (D53).
    install: ['npm', 'install', '-g', '@deepseek-ai/dsh'],
    package: '@deepseek-ai/dsh',
    noAccount: true,
  },
};

/** Which profile each harness runs as: per machine, and optionally per workspace. */
export interface HarnessSettings {
  defaults: Record<string, string>;
  workspaces: Record<string, Record<string, string>>;
  /**
   * Which VERSION each harness runs at, when Daoris manages the binary (TOOL2/D57) — per machine,
   * and optionally per workspace, resolved by exactly the rule profiles use.
   */
  versions: Record<string, string>;
  /** @see versions */
  workspaceVersions: Record<string, Record<string, string>>;
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
  const empty: HarnessSettings = {
    defaults: {}, workspaces: {}, versions: {}, workspaceVersions: {}, rest: {},
  };
  if (!existsSync(path)) return empty;

  try {
    const parsed = JSON.parse(readFileSync(path, 'utf8')) as Record<string, unknown>;
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return empty;

    const { defaults, workspaces, versions, workspaceVersions, ...rest } = parsed;
    return {
      defaults: stringMap(defaults),
      workspaces: Object.fromEntries(
        Object.entries(asObject(workspaces)).map(([circle, map]) => [circle, stringMap(map)])),
      versions: stringMap(versions),
      workspaceVersions: Object.fromEntries(
        Object.entries(asObject(workspaceVersions)).map(([circle, map]) => [circle, stringMap(map)])),
      rest,
    };
  } catch {
    return empty;
  }
}

/** Write it back, preserving anything this build did not put there. */
export function writeHarnessSettings(path: string, settings: HarnessSettings): void {
  const circles = (map: Record<string, Record<string, string>>) => Object.fromEntries(
    Object.entries(map)
      .filter(([, inner]) => Object.keys(inner).length > 0)
      .sort(([a], [b]) => (a < b ? -1 : 1)));

  writeTextAtomic(path, `${JSON.stringify({
    ...settings.rest,
    defaults: sorted(settings.defaults),
    workspaces: circles(settings.workspaces),
    versions: sorted(settings.versions),
    workspaceVersions: circles(settings.workspaceVersions),
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

/**
 * Which version a spawn runs at: the person's pick, the workspace's pin, the machine's, or none
 * (TOOL2/D57).
 *
 * @remarks
 * 🔴 **None means `PATH`** — whatever the machine has, which is what happened before any of this
 * existed. Not an empty managed directory and not a refusal: a machine that installed `claude`
 * itself, and a contributor who never ran Daoris, both have to keep working (D48 §2a). It is the
 * exact twin of "no profile means the harness's own configuration home", and it regresses the same
 * way if anybody ever makes absence mean something.
 */
export function resolveVersion(
  settings: HarnessSettings, harness: string, workspace?: string | null, chosen?: string | null,
): string | null {
  if (chosen?.trim()) return chosen.trim();

  const circle = workspace?.trim() ? settings.workspaceVersions[workspace.trim()] : undefined;
  if (circle?.[harness]?.trim()) return circle[harness]!.trim();

  return settings.versions[harness]?.trim() || null;
}

/** Where a managed version of a harness lives. Daoris owns this location, binary and all. */
export function managedHome(home: string, harness: string, version: string): string {
  return join(home, 'toolchain', safeName(harness, 'harness name'), safeName(version, 'version'));
}

/**
 * The executable inside a managed install, or null when there is no pin or nothing installed at it.
 *
 * @remarks
 * **npm's layout, because npm is how these harnesses ship**: `--prefix <dir>` puts the package under
 * `<dir>/node_modules` and its shims in `<dir>/node_modules/.bin`. Windows gets a `.cmd` beside the
 * shell script, so whichever exists is the answer.
 *
 * 🔴 **A pin whose directory is not there answers null, and the caller must say so** rather than
 * quietly falling back to `PATH` — that would run a different tool than the one the person asked for
 * and report success.
 */
export function managedBinary(
  home: string, harness: string, version: string | null, binary: string[],
): string | null {
  if (!version) return null;

  const name = binary[0];
  if (!name) return null;

  const bin = join(managedHome(home, harness, version), 'node_modules', '.bin');
  for (const candidate of [join(bin, `${name}.cmd`), join(bin, name)]) {
    if (existsSync(candidate)) return candidate;
  }

  return null;
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
      `\`${value}\` is not a usable ${what} — letters, digits, dashes and dots. It becomes a `
      + 'directory Daoris owns the location of, so it may not point anywhere else.');
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

  // 🔴 Through `spawnable`, for the same reason `install` needs it: a managed pin's shim is a `.cmd`
  // on Windows, and a bare `spawnSync` answers ENOENT/EINVAL for one — which reads as "not installed"
  // about a binary that is installed and works.
  const [file, argv, verbatim] = spawnable([...command, ...args]);
  const result = spawnSync(file, argv, {
    env, encoding: 'utf8', shell: false, timeout: 20_000, windowsHide: true,
    ...(verbatim ? { windowsVerbatimArguments: true } : {}),
  });

  if (result.error) {
    return { ran: false, output: '', problem: `\`${command[0]}\` is not on this machine's PATH` };
  }

  // Both streams: a harness that reports its version on stderr is not an absent harness. The exit
  // code is deliberately not consulted — see `Toolchain.loginCheck`.
  return { ran: true, output: `${result.stdout ?? ''}\n${result.stderr ?? ''}`, problem: null };
}

/**
 * Probe one harness: present, version, and every profile's login state.
 *
 * @remarks
 * 🔴 **It asks about the binary a spawn would actually run** — rule 4 of the twin contract, applied
 * here and not only where a session starts. A probe that asks `PATH` while a pin is set answers about
 * a different program: it reported the machine's own `claude` as the pinned one, and reported a
 * working pin as "not on this machine's PATH". Both are the silent substitution the pin exists to
 * prevent, arriving through the presence question instead of through the spawn.
 */
export function probe(
  harness: string, toolchain: Toolchain,
  home = harnessHome(), settings = readHarnessSettings(),
): HarnessReport {
  const pinned = resolveVersion(settings, harness, null, null);
  const managed = managedBinary(home, harness, pinned, toolchain.binary);

  // Pinned and nothing installed at it: absent, saying which version — never a fall back to `PATH`.
  const command = pinned ? managed : toolchain.binary[0] ?? null;
  const version = command === null
    ? {
      ran: false,
      output: '',
      problem: `pinned to ${pinned} on this machine, and nothing is installed at that version — `
        + `\`daoris harness pin ${harness} ${pinned}\` installs it, and \`daoris harness unpin `
        + `${harness}\` goes back to PATH`,
    }
    : ask([command, ...toolchain.binary.slice(1)], toolchain.version, null, toolchain);

  return {
    harness,
    present: version.ran,
    version: version.ran ? firstLine(version.output) : null,
    problem: version.problem,
    machineDefault: settings.defaults[harness] ?? null,
    profiles: profiles(home, harness).map((name) => {
      const where = profileHome(home, harness, name);
      if (!version.ran || !toolchain.loginCheck) return { name, home: where, login: 'unknown' as const };

      // The SAME binary the version came from. Asking the pin whether it runs and then asking PATH
      // whether it is logged in would answer about two different installs.
      const answer = ask(
        [command!, ...toolchain.binary.slice(1)], toolchain.loginCheck.args, where, toolchain);
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

    // The managed toolchain (TOOL2/D57): Daoris owns where this version lives and which one runs.
    case 'pin': {
      const { name, toolchain } = required(argv, 'pin');
      if (!toolchain.package) {
        throw new DaorisError(
          `\`${name}\` declares no package, so Daoris has no sanctioned way to fetch a version of `
          + 'it. Install it with its own tooling and Daoris will find it on PATH.');
      }

      const version = bare(argv, 2, 'pin', '<harness> <version>');
      const where = managedHome(home, name, version);
      const workspace = flagValue(argv, '--workspace');

      write(`daoris: installing \`${toolchain.package}@${version}\` into a directory Daoris owns.`);
      write(`  ${where}`);
      const installed = relay(
        ['npm', 'install', '--prefix', where, `${toolchain.package}@${version}`],
        null, toolchain, write);
      if (installed !== 0) {
        write('  Nothing was pinned: a pin naming a version that is not there would run a different');
        write('  tool than the one you asked for.');
        return installed;
      }

      pinTo(name, version, workspace);
      write(workspace
        ? `daoris: \`${name}\` runs at ${version} for the \`${workspace}\` circle on this machine.`
        : `daoris: \`${name}\` runs at ${version} on this machine.`);
      write('  Sessions spawn this binary rather than whatever is on PATH. `daoris harness unpin`');
      write('  puts it back, and the version is on every session record either way.');
      return 0;
    }

    case 'unpin': {
      const { name } = required(argv, 'unpin');
      const workspace = flagValue(argv, '--workspace');

      pinTo(name, null, workspace);
      write(workspace
        ? `daoris: the \`${workspace}\` circle no longer pins \`${name}\` on this machine.`
        : `daoris: \`${name}\` runs from PATH again on this machine.`);
      write('  Nothing was deleted — the managed install stays where it is, and re-pinning that');
      write('  version needs no download.');
      return 0;
    }

    case 'profile':
      return profileVerb();

    default:
      throw new DaorisError(
        `unknown harness verb '${verb}' — one of: list, install, update, login, pin, unpin, profile`);
  }

  /** Write one pin, machine-wide or for one circle. Null takes it off. */
  function pinTo(name: string, version: string | null, workspace?: string | null): void {
    const settings = readHarnessSettings(path);
    const circle = workspace?.trim() ? normalizeWorkspace(workspace) : null;

    if (circle) {
      const held = { ...settings.workspaceVersions[circle] };
      if (version) held[name] = version;
      else delete held[name];
      writeHarnessSettings(path, {
        ...settings,
        workspaceVersions: { ...settings.workspaceVersions, [circle]: held },
      });
      return;
    }

    const versions = { ...settings.versions };
    if (version) versions[name] = version;
    else delete versions[name];
    writeHarnessSettings(path, { ...settings, versions });
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

      // The pin, and whether it is actually in force (TOOL2/D57). A pin whose directory is not there
      // is reported as MISSING rather than silently ignored: sessions would run whatever is on PATH,
      // which is a different tool than the one the person asked for.
      const pinned = resolveVersion(settings, name, null, null);
      if (pinned) {
        const binary = managedBinary(home, name, pinned, toolchain.binary);
        write(`  ${''.padEnd(14)} pinned ${pinned} — `
          + (binary ? `managed: ${binary}` : 'NOT INSTALLED, so sessions fall back to PATH'));
        if (!binary) {
          write(`  ${''.padEnd(14)} \`daoris harness pin ${name} ${pinned}\` installs it`);
        }
      }

      for (const [circle, map] of Object.entries(settings.workspaceVersions)) {
        if (map[name]) write(`  ${''.padEnd(14)} pinned ${map[name]} for the \`${circle}\` circle`);
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

        // Never a credential. Un-defaulting is the reversible half and is what "remove" means; a
        // verb that quietly destroyed a credential would be the irreversible act this family never
        // does silently.
        //
        // 🔴 But "deletes nothing" made a remove nobody could see: the directory IS the profile, so
        // a profile stayed listed after being removed — and a harness scaffolds a fresh home the
        // first time it is asked about it, so "empty" alone did not cover a real one. The directory
        // goes when there is nothing signed-in to destroy, by the only evidence Daoris takes: it is
        // empty, or the harness itself, asked as `list` asks, reports that profile signed OUT.
        // Signed in, or unanswerable, and it stays — said out loud, with the path. The desktop's
        // Forget draws the same line.
        const empty = existsSync(where) && readdirSync(where).length === 0;
        const login = !existsSync(where) || empty
          ? 'unknown'
          : probe(name, TOOLCHAINS[name]!, home, settings).profiles.find((p) => p.name === profile)?.login ?? 'unknown';
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
        if (empty) {
          rmSync(where, { recursive: true, force: true });
          write(`  The empty directory Daoris made is gone with it — nothing was ever put in it: ${where}`);
          return 0;
        }
        if (login === 'out') {
          rmSync(where, { recursive: true, force: true });
          write(`  The directory is gone with it — \`${name}\` reports that profile signed out, so nothing`);
          write(`  signed-in was in it: ${where}`);
          return 0;
        }

        write(`  The directory is untouched — ${where}`);
        write(login === 'in'
          ? `  \`${name}\` reports it signed in, and Daoris never deletes a credential: sign out with the`
          : `  \`${name}\` could not say whether it is signed in, so Daoris leaves it: remove the`);
        write(login === 'in'
          ? '  tool, then remove it — or delete the directory yourself, deliberately.'
          : '  directory yourself if you are sure.');
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

    const [file, argv, verbatim] = spawnable(command);
    const result = spawnSync(file, argv, {
      env,
      stdio: 'inherit',
      shell: false,
      windowsHide: true,
      ...(verbatim ? { windowsVerbatimArguments: true } : {}),
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

/**
 * How to actually spawn a harness's own tooling, which on Windows is usually a `.cmd` shim.
 *
 * @remarks
 * 🔴 **Two Windows facts, in order, each of which broke this silently.** `npm` is `npm.cmd`, and
 * `spawnSync` with `shell: false` does not find it — it answers `ENOENT`, which reads as "npm is not
 * installed" on a machine where it plainly is. Resolve the extension and the next one lands:
 * **Node refuses to spawn a `.cmd` or `.bat` without a shell at all** (the 2024 argument-injection
 * fix), answering `EINVAL`. Between them these broke every npm-shaped mechanism this module declares
 * — `install`, `update`, and TOOL2's `pin` — for as long as they have existed.
 *
 * **`shell: true` is the wrong fix**, twice over. Node does not quote for it on Windows, so a
 * configuration home under `C:\Users\<a name with a space>\` breaks; and it would put a version string a person
 * typed onto a command line `cmd.exe` parses. So this builds the `cmd.exe /d /s /c` invocation
 * itself, quotes every token, and passes it verbatim — the quoting is ours, which is the only way it
 * is anybody's.
 *
 * @returns the file to spawn, its arguments, and whether they are a verbatim Windows command line.
 */
function spawnable(command: string[]): [string, string[], boolean] {
  const [name, ...rest] = command as [string, ...string[]];
  if (process.platform !== 'win32') return [name, rest, false];

  const resolved = windowsExecutable(name);
  if (!/\.(cmd|bat)$/i.test(resolved)) return [resolved, rest, false];

  // A token cmd.exe would reinterpret is refused rather than escaped. Every argument here is a path
  // or a `package@version`, so none of them legitimately contains one — and "escaped correctly for
  // cmd" is a claim nobody should have to verify.
  for (const token of [resolved, ...rest]) {
    if (/["%\r\n]/.test(token)) {
      throw new DaorisError(
        `\`${token}\` cannot be passed to a Windows command shim safely. Run the harness's own `
        + 'tooling directly, or move it somewhere without quotes or percent signs in the path.');
    }
  }

  const quote = (token: string) => (/[\s&()[\]{}^=;!'+,`~]/.test(token) ? `"${token}"` : token);
  const line = [resolved, ...rest].map(quote).join(' ');
  return [process.env.ComSpec ?? 'cmd.exe', ['/d', '/s', '/c', `"${line}"`], true];
}

/** Where Windows keeps the shim for a bare command name, or the name itself when it is not one. */
function windowsExecutable(command: string): string {
  if (/[\\/]/.test(command) || /\.[a-z]+$/i.test(command)) return command;

  const pathExt = (process.env.PATHEXT ?? '.COM;.EXE;.BAT;.CMD').split(';').filter(Boolean);
  for (const directory of (process.env.PATH ?? '').split(';')) {
    if (!directory) continue;
    for (const extension of pathExt) {
      const candidate = join(directory, command + extension.toLowerCase());
      if (existsSync(candidate)) return candidate;
    }
  }

  // Not found is not this function's decision to make: spawning reports it, with its own sentence.
  return command;
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
