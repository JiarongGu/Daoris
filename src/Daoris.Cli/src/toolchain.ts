// `daoris agent` — this machine's agents and the accounts they run as (D49 §4, D50). The code calls
// the program a session runs a HARNESS; a person reads *agent* (AGT1), and that is the verb.
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
//   (4 is the binary — see `resolveVersion`.)
//   5. An account made by signing in takes the first free `account-N`; who it is, is the tool's answer.
//   6. An account that is an API key keeps its key in `keys.json` under the home, beside the account.
//   7. A door's accounts, defaults and keys are its owner's (`accountOf`); its pin is its own.
//
// It is a MANAGEMENT command and it opens no socket. It does spawn processes — that is the whole
// point: install, update and login are each harness's OWN mechanism, run by Daoris rather than
// remembered by hand. `child_process` is a Node built-in, so the zero-dependency guarantee stands.
// `pin` fetches a vendor's release (AGT2b) through a fetcher the dispatcher hands in — the network
// stays in `service.ts`, and nothing here can open one of its own.
//
// DAORIS NEVER SEES, STORES OR COPIES A SIGN-IN. An API key is the one exception, and only when a
// person gives one (`agent key`, D67 §1). It manages directories and names; login runs the
// harness's own flow INTO a profile directory, and whatever that obtains the harness stores itself.

import { existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync } from 'node:fs';
import { homedir } from 'node:os';
import { requireHomeFile } from './home.ts';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { flagValue, operands } from './args.ts';

/** The `agent` flags that take a value — so that value is never read as an operand. */
const AGENT_VALUED: ReadonlySet<string> = new Set(['--profile', '--workspace']);
import { DaorisError } from './errors.ts';
import { onPath, readJsonObject, writeJsonAtomic } from './fsx.ts';
import { normalizeWorkspace } from './remotemap.ts';
// A cycle with `plugins.ts`, harmless because both sides read the other only inside functions:
// `harness list` shows the harnesses plugins declare, and the catalogue refuses the names this table has.
import { readPlugins, resolvable } from './plugins.ts';
import { installFromChannel, latestVersion, refuseVersion } from './channels.ts';
import { commandRules } from './permissions.ts';
import { PROPOSAL_VERBS, commandProposals } from './ruleproposals.ts';
import { grantTrust, TRUST_FILE } from './trust.ts';
import type { Channel, Fetcher } from './channels.ts';
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
  /**
   * What a person calls the tool this runs, and who makes it (AGT1) — `dsh` meant nothing to the
   * owner until it said whose. A door onto a tool names that tool, not itself. The driver's
   * `Product` and `Maker` are the twin.
   */
  product?: string;
  maker?: string;
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
  /**
   * The maker's OWN release channel a managed install fetches from (AGT2b), when the maker publishes
   * one — verified end to end in `channels.ts`, never through npm. A harness declares this or
   * `package`, never both: which source a pin came from is not left for anyone to work out.
   */
  channel?: Channel;
  /** Its own updater, as arguments to the binary. */
  update?: string[];
  /** Its own login flow, as arguments to the binary, run with a profile home in the environment. */
  login?: string[];
  /**
   * How it answers "is this configuration home logged in?" — and the two shapes that answer takes.
   * Both supported harnesses EXIT 0 EITHER WAY, so the output is the answer and the exit code is
   * deliberately not consulted. Neither pattern matching means unknown, never logged-out.
   *
   * `account`, when given, is a pattern whose first group is WHO is signed in there (D66 §3) — the
   * name a person knows the account by. Read only on a yes, and the one thing besides the boolean
   * taken from the answer.
   */
  loginCheck?: { args: string[]; in: RegExp; out: RegExp; account?: RegExp };
  /**
   * The harness whose ACCOUNT this one runs as, when it has none of its own (ACP2).
   *
   * @remarks
   * 🔴 **Declared, so that a missing login flow is a fact rather than a gap.** The ACP adapter runs
   * `claude` and reads the configuration home `claude` logged into; it has no login of its own and
   * never will. Without this field its silence is indistinguishable from an omission — and an
   * unanswerable login question is PERMISSIVE (SES3), so an omission would quietly widen what may
   * spawn. `daoris agent login <accountOf>` is the verb the surfaces then name.
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
  /**
   * What a PINNED binary runs with so it stays the version pinned (AGT2) — the tool's own switch,
   * measured, and applied to a managed binary only: one off `PATH` is the machine's, and so are its
   * updates. The driver's `PinnedEnvironment` is the twin.
   */
  pinnedEnv?: Record<string, string>;
  /**
   * The tool's own file that records which folders a person has trusted it in (DEPLOY1), in the
   * account's configuration home — declared where the tool ignores a folder's own permissions until
   * then. `daoris agent trust` writes it, on the person's word (D73). The driver's `TrustFile` is the
   * twin.
   */
  trustFile?: string;
  /**
   * The tool's own variable for an API key (AGT3, D67 §1): what an account that is a key is handed
   * at spawn. Declared only where measured; absent means this agent takes no key from Daoris. The
   * driver's `KeyVariable` is the twin.
   */
  keyVariable?: string;
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
    product: 'Claude Code',
    maker: 'Anthropic',
    binary: ['claude'],
    version: ['--version'],
    profileVariable: 'CLAUDE_CONFIG_DIR',
    install: ['npm', 'install', '-g', '@anthropic-ai/claude-code'],
    // A pin comes from the release bucket, against its SIGNED manifest (AGT2b) — the npm package
    // installs the same native binary, with nothing but npm's own integrity check behind it.
    channel: 'claude-code-releases',
    update: ['update'],
    login: ['auth', 'login'],
    // It answers JSON — and volunteers an email, an organisation and a subscription tier with it.
    // Daoris reads the boolean and the email (who signed in, D66 §3), and keeps neither of the rest.
    loginCheck: {
      args: ['auth', 'status'],
      in: /"loggedIn"\s*:\s*true/i,
      out: /"loggedIn"\s*:\s*false/i,
      account: /"email"\s*:\s*"([^"]+)"/i,
    },
    // 🔴 A pinned 2.1.270 reported its own auto-updates ENABLED (`claude doctor`, no login); with
    // this it reported them disabled, refused `claude update`, and stayed 2.1.270.
    pinnedEnv: { DISABLE_UPDATES: '1' },
    // Measured on 2.1.280 with an invalid key: `auth status` reads it, and `-p` takes it unprompted.
    keyVariable: 'ANTHROPIC_API_KEY',
    // Its trust record: until a folder is accepted here, the folder's own allow-list is ignored.
    trustFile: TRUST_FILE,
  },
  // The supported harness over the PROTOCOL door (ACP2/D53). A separate toolchain entry from
  // `claude-code` on purpose: the ACP adapter and `claude` are different packages at different
  // versions, and one pin for both would install the wrong thing under a name somebody trusted.
  //
  // It declares no login flow and no login question. The ACCOUNT belongs to `claude`, and the
  // profile directory carries it — `daoris agent login claude-code` is still the verb, and this
  // adapter reads the home that produced. An unknown login state is permissive, by SES3's rule.
  'claude-code-acp': {
    product: 'Claude Code',
    maker: 'Anthropic',
    // 🔴 The BINARY is `claude-agent-acp`, not the adapter's Daoris name — verified against the
    // installed package, after a guess was caught by `list` reporting the pin as not installed.
    binary: ['claude-agent-acp'],
    version: ['--version'],
    profileVariable: 'CLAUDE_CONFIG_DIR',
    install: ['npm', 'install', '-g', '@agentclientprotocol/claude-agent-acp'],
    package: '@agentclientprotocol/claude-agent-acp',
    // It has no login of its own: it runs `claude` and reads the home `claude` logged into.
    accountOf: 'claude-code',
    // And the same trust record: measured, it ignores an untrusted folder's allow-list too (DEPLOY1).
    trustFile: TRUST_FILE,
  },
  codex: {
    product: 'Codex',
    maker: 'OpenAI',
    binary: ['codex'],
    version: ['--version'],
    profileVariable: 'CODEX_HOME',
    install: ['npm', 'install', '-g', '@openai/codex'],
    // A pin comes from the release's own package, by both of its published hashes (AGT2b). No
    // `pinnedEnv`: an executable outside Codex's own standalone layout gets no update action, so a
    // pin stays the version pinned without a switch (the channel evidence, §2).
    channel: 'codex-releases',
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
    product: 'Codex',
    maker: 'OpenAI',
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
    product: 'dsh',
    maker: 'DeepSeek',
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
  // Unreadable reads as empty, for a session about to spawn; an EDIT over it is refused by the writer.
  const { value: parsed } = readJsonObject(path);
  if (parsed === null) return empty;

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
}

/** Write it back, preserving anything this build did not put there. */
export function writeHarnessSettings(path: string, settings: HarnessSettings): void {
  const circles = (map: Record<string, Record<string, string>>) => Object.fromEntries(
    Object.entries(map)
      .filter(([, inner]) => Object.keys(inner).length > 0)
      .sort(([a], [b]) => (a < b ? -1 : 1)));

  writeJsonAtomic(path, {
    ...settings.rest,
    defaults: sorted(settings.defaults),
    workspaces: circles(settings.workspaces),
    versions: sorted(settings.versions),
    workspaceVersions: circles(settings.workspaceVersions),
  });
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
  return join(home, 'harnesses', safeName(harness, 'agent name'), safeName(profile, 'profile name'));
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
  return join(home, 'toolchain', safeName(harness, 'agent name'), safeName(version, 'version'));
}

/**
 * The executable inside a managed install, or null when there is no pin or nothing installed at it.
 *
 * @remarks
 * **Two layouts, the vendor's first** (AGT2b). A pin from a maker's own channel lands as
 * `<dir>/bin/<binary>` — `.exe` on Windows — which is Codex's package layout and the same shape for
 * Claude Code's one file; it is moved into place only once it verified, so finding it is the proof.
 * Then **npm's layout, for what ships only there**: `--prefix <dir>` puts the package under
 * `<dir>/node_modules` and its shims in `<dir>/node_modules/.bin`, with a `.cmd` beside the shell
 * script on Windows. A pin npm made before AGT2b still resolves — it is an install somebody made.
 * The driver's `ManagedBinary` is the twin.
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

  const managed = managedHome(home, harness, version);
  const vendor = join(managed, 'bin', process.platform === 'win32' ? `${name}.exe` : name);
  if (existsSync(vendor)) return vendor;

  const bin = join(managed, 'node_modules', '.bin');
  for (const candidate of [join(bin, `${name}.cmd`), join(bin, name)]) {
    if (existsSync(candidate)) return candidate;
  }

  return null;
}

/**
 * Whose accounts an agent runs as (AGT7, twin rule 7): `accountOf` for a door onto another agent,
 * else itself. Accounts, their defaults and their keys live under this name; a pin does not.
 */
export function ownerOf(harness: string): string {
  return TOOLCHAINS[harness]?.accountOf ?? harness;
}

/** The profiles that exist — the directories that exist, sorted. There is no second register. */
export function profiles(home: string, harness: string): string[] {
  const root = join(home, 'harnesses', safeName(harness, 'agent name'));
  if (!existsSync(root)) return [];

  return readdirSync(root, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort();
}

/**
 * The name an account made by signing in gets (D66 §3): the first free `account-N`. Twin rule 5.
 *
 * @remarks
 * 🔴 **A neutral name, never who signed in.** It is needed before the sign-in starts, when nobody knows
 * whose it is; and renaming the directory afterwards would move a home a harness may have keyed its
 * credential to. Who it is stays the tool's answer, read by `list`.
 */
export function nextAccount(home: string, harness: string): string {
  const taken = new Set(profiles(home, harness).map((name) => name.toLowerCase()));
  let number = 1;
  while (taken.has(`account-${number}`)) number += 1;
  return `account-${number}`;
}

/**
 * Delete an account: its directory, credentials included (D66 §3). True when there was one.
 *
 * @remarks
 * The name is judged first (`profileHome`), so a name pointing anywhere else is refused before
 * anything is touched — the tool's own configuration home is not under here and no name reaches it.
 * Node's own recursive remove clears a read-only file on Windows and does not follow links.
 */
export function removeProfile(home: string, harness: string, profile: string): boolean {
  const where = profileHome(home, harness, profile);
  // An account that was a key goes with its key (AGT3), even when its directory went by hand.
  removeKey(home, harness, profile);
  if (!existsSync(where)) return false;
  rmSync(where, { recursive: true, force: true, maxRetries: 3 });
  return true;
}

// ——— Accounts that are API keys (AGT3, D67 §1). `keys.json` under the home, keyed by agent and then
// account: beside the account, never inside the directory that is the tool's (D49 §4). Plaintext at
// rest, like `remotes.json`'s deployment keys and the tools' own credential files. The driver's
// `HarnessKeys` is the twin, over the same file.

type Keys = Record<string, Record<string, string>>;

function keysPath(home: string): string {
  return join(home, 'keys.json');
}

function readKeys(home: string): Keys {
  // An unreadable file holds no key anyone can be handed; the account then asks for one. Writing a
  // new key over it is refused by `writeJsonAtomic`, or every other account's key would go with it.
  const { value: parsed } = readJsonObject(keysPath(home));
  if (parsed === null) return {};
  return Object.fromEntries(Object.entries(parsed).map(([agent, held]) => [agent, stringMap(held)]));
}

function writeKeys(home: string, keys: Keys): void {
  const kept = Object.fromEntries(Object.entries(keys)
    .filter(([, held]) => Object.keys(held).length > 0)
    .sort(([a], [b]) => (a < b ? -1 : 1))
    .map(([agent, held]) => [agent, sorted(held)]));
  writeJsonAtomic(keysPath(home), kept);
}

/** The key's last four characters — every Anthropic key begins the same way. */
export function keyHandle(key: string): string {
  return key.length > 4 ? `…${key.slice(-4)}` : '…';
}

/** The key an account is, or null for a sign-in. */
export function keyOf(home: string, harness: string, profile: string): string | null {
  return readKeys(home)[harness]?.[profile] ?? null;
}

function removeKey(home: string, harness: string, profile: string): void {
  const keys = readKeys(home);
  if (!keys[harness]?.[profile]) return;
  delete keys[harness]![profile];
  writeKeys(home, keys);
}

/**
 * Make an account that is this key: the next free `account-N`, its directory, and the key kept
 * beside it. Said back by its handle only. Refused, before anything is made, for a blank key or an
 * agent that declares no key variable.
 */
export function addKeyAccount(
  home: string, door: string, raw: string, write: (line: string) => void,
): string {
  // A key given for a door is its owner's account (AGT7), and it takes the owner's variable.
  const harness = ownerOf(door);
  const toolchain = TOOLCHAINS[harness];
  if (!toolchain?.keyVariable) {
    throw new DaorisError(`\`${door}\` takes no API key from Daoris — sign in with its own login instead.`);
  }

  const key = raw.trim();
  if (!key || /\s/.test(key)) throw new DaorisError('that is not an API key — it is blank, or has spaces in it.');

  const account = nextAccount(home, harness);
  const keys = readKeys(home);
  keys[harness] = { ...keys[harness], [account]: key };
  // The key first: a `keys.json` it cannot read refuses here, before an account directory exists.
  writeKeys(home, keys);
  mkdirSync(profileHome(home, harness, account), { recursive: true });

  write(`daoris: \`${harness}\` account \`${account}\` is the API key ${keyHandle(key)}.`);
  write(`  Kept in ${keysPath(home)} — machine-local, tracked by nothing, shown back only as its last four.`);
  write(`  \`daoris agent profile default ${harness} ${account}\` makes sessions run as it.`);
  return account;
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
  /**
   * `account` is who the tool says is signed in there (D66 §3), or null; `key` is an API-key
   * account's handle (AGT3) — never the key — or null for a sign-in.
   */
  profiles: { name: string; home: string; login: Login; account: string | null; key: string | null }[];
}

type Login = 'in' | 'out' | 'unknown';

/**
 * Run one of the harness's own reporting commands and collect what it said.
 *
 * @remarks
 * Synchronous on purpose: this is a person at a terminal waiting for one answer, and `spawnSync`
 * keeps the whole command a straight line with no lifetime to get wrong.
 */
function ask(
  command: string[], args: string[], profile: string | null, toolchain: Toolchain, managed = false,
  account: Record<string, string> = {},
): { ran: boolean; output: string; problem: string | null } {
  // A pinned binary is asked the way a session runs it (AGT2), so asking is not when it moves; and
  // an account that is a key is asked with its key (AGT3).
  const env = { ...process.env, ...(managed ? toolchain.pinnedEnv : {}), ...account };
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
        + `\`daoris agent pin ${harness} ${pinned}\` installs it, and \`daoris agent unpin `
        + `${harness}\` goes back to PATH`,
    }
    : ask([command, ...toolchain.binary.slice(1)], toolchain.version, null, toolchain, Boolean(pinned));

  return {
    harness,
    present: version.ran,
    version: version.ran ? firstLine(version.output) : null,
    problem: version.problem,
    // A door's accounts and default are its owner's (AGT7, twin rule 7).
    machineDefault: settings.defaults[toolchain.accountOf ?? harness] ?? null,
    profiles: profiles(home, toolchain.accountOf ?? harness).map((name) => {
      const owner = toolchain.accountOf ?? harness;
      const where = profileHome(home, owner, name);
      // An account that is a key is asked WITH its key (AGT3), as a session would run it.
      const held = keyOf(home, owner, name);
      const key = toolchain.keyVariable ? held : null;
      const shown = held ? keyHandle(held) : null;
      if (!version.ran) return { name, home: where, login: 'unknown' as const, account: null, key: shown };

      // The SAME binary the version came from. Asking the pin whether it runs and then asking PATH
      // whether it is logged in would answer about two different installs.
      return {
        name, home: where,
        ...loginAt([command!, ...toolchain.binary.slice(1)], toolchain, where, Boolean(pinned),
          key ? { [toolchain.keyVariable!]: key } : {}),
        key: shown,
      };
    }),
  };
}

/**
 * What the harness says about logging in to one home: the boolean, and — when the toolchain asks and
 * the answer is yes — who (D66 §3). Nothing else it volunteers is kept.
 */
function loginAt(
  command: string[], toolchain: Toolchain, where: string, managed = false,
  account: Record<string, string> = {},
): { login: Login; account: string | null } {
  const check = toolchain.loginCheck;
  if (!check) return { login: 'unknown', account: null };

  const answer = ask(command, check.args, where, toolchain, managed, account);
  if (!answer.ran) return { login: 'unknown', account: null };
  if (check.in.test(answer.output)) {
    return { login: 'in', account: check.account?.exec(answer.output)?.[1]?.trim() || null };
  }
  return { login: check.out.test(answer.output) ? 'out' : 'unknown', account: null };
}

/**
 * Sign in to another account (D66 §3): the tool's own login flow into the next free `account-N`,
 * kept only when the sign-in finished — the tool exited 0 and does not call that home signed out.
 *
 * @remarks
 * The spawn is a parameter so the judgement around it is testable with no account: `run` is the
 * relay in the verb, and a fixture in the tests. A sign-in that did not finish leaves nothing behind.
 */
export function signInNew(
  harness: string, toolchain: Toolchain, home: string,
  run: (where: string) => ExitCode, write: (line: string) => void,
): ExitCode {
  const name = nextAccount(home, harness);
  const where = profileHome(home, harness, name);
  // Opened for the sign-in, and gone again below unless the sign-in finished.
  mkdirSync(where, { recursive: true });
  write(`daoris: signing in to another \`${harness}\` account, with its own login flow.`);
  write(`  ${where}`);
  write('  Daoris chose the directory and nothing else: whatever you sign in with is stored by the');
  write('  agent, in its own store, under your OS account — Daoris never sees it.');

  let code: ExitCode = 2;
  try {
    code = run(where);
  } finally {
    // Asked of the binary the login ran, so the answer is about the sign-in that just happened.
    const said = code === 0
      ? loginAt(toolchain.binary, toolchain, where)
      : { login: 'out' as const, account: null };

    if (code !== 0 || said.login === 'out') {
      removeProfile(home, harness, name);
      write('daoris: nothing was signed in, so nothing was kept — the account opened for it is gone again.');
    } else {
      write(said.account
        ? `daoris: signed in as ${said.account} — this machine lists it as \`${name}\`.`
        : `daoris: signed in — \`${harness}\` did not say who, so this machine lists it as \`${name}\`.`);
      write(`  \`daoris agent profile default ${harness} ${name}\` makes sessions run as it.`);
    }
  }

  return code === 0 ? 0 : 2;
}

/**
 * Read or change this machine's harnesses and the accounts they run as.
 *
 * @remarks
 * Verbs, not flags: `install` and `login` do entirely different things to different parts of the
 * machine, and a boolean distinguishing them is the shape that eventually gets defaulted wrong.
 *
 * `releases` is how `pin` reaches a vendor's channel (AGT2b), and how `update` asks one for its
 * newest release (USE1a): handed in by the dispatcher from `service.ts`, so this module never holds a
 * socket of its own. Every other verb never touches it.
 *
 * `npm` is the package manager a package pin, and a package pin's update, runs — npm itself, unless a
 * test hands in a stand-in.
 */
export function commandHarness(
  { argv, write }: CommandArgs, releases: Fetcher | null = null, npm: string[] = ['npm'],
): ExitCode | Promise<ExitCode> {
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
          + 'it with its own tooling; `daoris agent list` will find it afterwards.');
      }

      write(`daoris: installing \`${name}\` with its own installer — nothing here is automatic (D49 §4).`);
      return relay(toolchain.install, null, toolchain, write);
    }

    // USE1a: update does what it says. A pinned agent with a package or a channel moves its pin to
    // the newest release, since running its updater would change a different copy than the one
    // sessions run. Unpinned, the agent's own updater runs, as it always did.
    case 'update': {
      const { name, toolchain } = required(argv, 'update');
      const workspace = flagValue(argv, '--workspace');
      const circle = workspace?.trim() ? normalizeWorkspace(workspace) : null;
      const settings = readHarnessSettings(path);
      const pin = (circle ? settings.workspaceVersions[circle]?.[name] : settings.versions[name])?.trim() || null;

      if (pin && (toolchain.package || toolchain.channel)) return updatePin(name, toolchain, pin, circle);
      if (circle) {
        throw new DaorisError(
          `the \`${circle}\` workspace pins no version of \`${name}\`, so there is no pin to move — `
          + `\`daoris agent pin ${name} <version> --workspace ${circle}\` sets one.`);
      }
      if (!toolchain.update) {
        throw new DaorisError(`\`${name}\` declares no updater — it updates itself, or its package manager does.`);
      }

      write(`daoris: updating \`${name}\`. Never mid-session and never unasked: a tool that changed`);
      write('  under a running loop is a moving target nobody diffed.');
      return relay([...toolchain.binary, ...toolchain.update], null, toolchain, write);
    }

    case 'login': {
      const { name, toolchain } = required(argv, 'login');
      const login = toolchain.login;
      if (!login) {
        throw new DaorisError(
          `\`${name}\` declares no login flow — log in with its own tooling, pointing `
          + `${toolchain.profileVariable} at the profile directory.`);
      }

      // Another account (D66 §3): made by the sign-in, named by who signed in — the desktop's
      // "Sign in to another account", from a terminal (D50).
      if (argv.includes('--new')) {
        return signInNew(
          name, toolchain, home,
          (where) => relay([...toolchain.binary, ...login], where, toolchain, write),
          write);
      }

      const profile = flagValue(argv, '--profile')
        ?? readHarnessSettings(path).defaults[name]
        ?? 'default';
      const where = profileHome(home, name, profile);

      write(`daoris: running \`${name}\`'s own login flow into the account \`${profile}\`.`);
      write(`  ${where}`);
      write('  Daoris chose the directory and nothing else: whatever you sign in with is stored by');
      write('  the agent, in its own store, under your OS account — Daoris never sees it.');
      return relay([...toolchain.binary, ...login], where, toolchain, write);
    }

    // The managed toolchain (TOOL2/D57): Daoris owns where this version lives and which one runs.
    case 'pin': {
      const { name, toolchain } = required(argv, 'pin');
      if (!toolchain.package && !toolchain.channel) {
        throw new DaorisError(
          `\`${name}\` declares no package, so Daoris has no sanctioned way to fetch a version of `
          + 'it. Install it with its own tooling and Daoris will find it on PATH.');
      }

      const version = bare(argv, 2, 'pin', '<agent> <version>');
      const where = managedHome(home, name, version);
      const workspace = flagValue(argv, '--workspace');

      // The maker's own channel (AGT2b). A version it cannot verify is refused HERE, before a
      // single byte is fetched — and never handed to npm instead, which would be the same trust by
      // another road.
      if (toolchain.channel) {
        refuseVersion(toolchain.channel, version);
        return pinFromChannel(name, toolchain, toolchain.channel, version, where, workspace);
      }

      return pinFromPackage(name, toolchain, version, where, workspace);
    }

    case 'unpin': {
      const { name } = required(argv, 'unpin');
      const workspace = flagValue(argv, '--workspace');

      pinTo(name, null, workspace);
      write(workspace
        ? `daoris: the \`${workspace}\` workspace no longer pins \`${name}\` on this machine.`
        : `daoris: \`${name}\` runs from PATH again on this machine.`);
      write('  Nothing was deleted — the managed install stays where it is, and re-pinning that');
      write('  version needs no download.');
      return 0;
    }

    case 'profile':
      return profileVerb();

    // What an agent Daoris starts may do (PERM1, D72): Claude Code's own rules in Daoris's scopes,
    // one file under the home, handed over at spawn. It spawns nothing and opens nothing.
    // An agent's proposals to change them (PERM2, D74) are answered here too: a widening never applies
    // without the person.
    case 'rules':
      return PROPOSAL_VERBS.includes(argv[1] ?? '')
        ? commandProposals({ argv: argv.slice(1), write, root: '', packageRoot: '' })
        : commandRules({ argv: argv.slice(1), write, root: '', packageRoot: '' });

    // The harness's trust in a folder (D73): the person's grant, asked and then written — the
    // terminal's door onto the screen's *trust this folder…*, and the one that names any folder.
    case 'trust':
      return trust();

    // An account that is an API key (AGT3, D67 §1). 🔴 Read from STDIN, never from an argument: an
    // argument is visible in the process list and saved in the shell's history.
    case 'key': {
      const { name } = required(argv, 'key');
      const extra = argv.slice(1).filter((token) => !token.startsWith('--') && token !== name);
      if (extra.length > 0) {
        throw new DaorisError(
          'the key goes on stdin, not on the command line — pipe it in, or type it and end the input. '
          + 'One given as an argument is now in your shell history; revoke it if that matters.');
      }

      let raw = '';
      try {
        raw = readFileSync(0, 'utf8');
      } catch {
        // No stdin at all: the same as an empty one, which is refused as not a key.
      }
      addKeyAccount(home, name, raw, write);
      return 0;
    }

    default:
      throw new DaorisError(
        `unknown agent verb '${verb}' — one of: list, install, update, login, key, pin, unpin, profile, trust, rules`);
  }

  /**
   * Install one version from its maker's own channel, verified, and only then pin to it (AGT2b).
   *
   * @remarks
   * An install already in place is the proof it verified — the channel moves it there only after
   * every check — so re-pinning it fetches nothing, which is what `unpin` promises.
   */
  async function pinFromChannel(
    name: string, toolchain: Toolchain, channel: Channel, version: string, where: string,
    workspace: string | undefined,
  ): Promise<ExitCode> {
    const present = managedBinary(home, name, version, toolchain.binary);
    if (present) {
      write(`daoris: \`${name}\` ${version} is already installed where Daoris keeps it — nothing was downloaded.`);
      write(`  ${present}`);
      return pinned(name, version, workspace);
    }

    if (!releases) {
      throw new DaorisError(`this build was given no way to reach ${toolchain.maker ?? 'the maker'}'s release channel, so nothing was fetched or pinned.`);
    }

    write(`daoris: installing ${toolchain.product ?? name} ${version} from ${toolchain.maker ?? 'its maker'}'s own `
      + 'release channel, into a directory Daoris owns.');
    write(`  ${where}`);
    try {
      await installFromChannel({ channel, version, where, fetcher: releases, write });
    } catch (error) {
      if (!(error instanceof DaorisError)) throw error;
      throw new DaorisError(`${error.message}\n  Nothing was pinned: a pin naming a version that is not `
        + 'there would run a different tool than the one you asked for.', error.exitCode);
    }

    return pinned(name, version, workspace);
  }

  /** Install one version from npm into a directory Daoris owns, and only then pin to it (TOOL2/D57). */
  function pinFromPackage(
    name: string, toolchain: Toolchain, version: string, where: string, workspace: string | undefined,
  ): ExitCode {
    write(`daoris: installing \`${toolchain.package}@${version}\` into a directory Daoris owns.`);
    write(`  ${where}`);
    const installed = relay([...npm, 'install', '--prefix', where, `${toolchain.package}@${version}`], null, toolchain, write);
    if (installed !== 0) {
      write('  Nothing was pinned: a pin naming a version that is not there would run a different');
      write('  tool than the one you asked for.');
      return installed;
    }

    return pinned(name, version, workspace);
  }

  /**
   * Move a pin to the newest release (USE1a): resolve the newest to one exact version, then pin it
   * exactly as `pin` does — the channel's verified install, or npm's. The pin never names a pointer.
   */
  function updatePin(
    name: string, toolchain: Toolchain, pin: string, workspace: string | null,
  ): ExitCode | Promise<ExitCode> {
    const scope = workspace ? ` for the \`${workspace}\` workspace` : '';
    const channel = toolchain.channel;
    if (channel) {
      write(`daoris: \`${name}\` is pinned at ${pin}${scope}. Asking ${toolchain.maker ?? 'its maker'}'s own release `
        + 'channel which release is newest.');
      return (async () => {
        if (!releases) {
          throw new DaorisError(`this build was given no way to reach ${toolchain.maker ?? 'the maker'}'s release channel, so nothing was fetched or pinned.`);
        }
        let newest: string;
        try {
          newest = await latestVersion(channel, releases);
        } catch (error) {
          if (!(error instanceof DaorisError)) throw error;
          throw new DaorisError(`${error.message} The pin stays at ${pin}.`, error.exitCode);
        }
        return settle(name, toolchain, pin, newest)
          ?? pinFromChannel(name, toolchain, channel, newest, managedHome(home, name, newest), workspace ?? undefined);
      })();
    }

    write(`daoris: \`${name}\` is pinned at ${pin}${scope}. Asking npm which release of \`${toolchain.package}\` is newest.`);
    const newest = newestOnNpm(toolchain.package!, pin);
    return settle(name, toolchain, pin, newest)
      ?? pinFromPackage(name, toolchain, newest, managedHome(home, name, newest), workspace ?? undefined);
  }

  /**
   * The answer when the newest release does not move the pin — already there and installed, or older
   * than a pin someone chose ahead of it — or null when it does, having said from what to what.
   */
  function settle(name: string, toolchain: Toolchain, pin: string, newest: string): ExitCode | null {
    const order = compareReleases(newest, pin);
    if (newest === pin && managedBinary(home, name, pin, toolchain.binary)) {
      write(`daoris: \`${name}\` ${pin} is already the newest release — nothing was fetched, and the pin stays.`);
      return 0;
    }
    if (order !== null && order < 0) {
      write(`daoris: \`${name}\` is pinned at ${pin}, newer than the newest release (${newest}) — nothing was`);
      write('  fetched, and the pin stays. Update never moves a pin backwards.');
      return 0;
    }

    write(newest === pin
      ? `daoris: \`${name}\` ${pin} is the newest release and is not installed here — installing it.`
      : `daoris: \`${name}\` ${pin} → ${newest}`);
    return null;
  }

  /**
   * The newest version npm publishes of a package, asked with `npm view <package> version` — a spawn
   * of the same kind the pin's `npm install` is. Anything but one version is a refusal that says so.
   */
  function newestOnNpm(pkg: string, pin: string): string {
    const command = [...npm, 'view', pkg, 'version'];
    write(`  $ ${command.join(' ')}`);
    const [file, args, verbatim] = spawnable(command);
    const result = spawnSync(file, args, {
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
      shell: false,
      windowsHide: true,
      ...(verbatim ? { windowsVerbatimArguments: true } : {}),
    });
    const stays = `Nothing was fetched or pinned, and the pin stays at ${pin}.`;

    if (result.error) {
      throw new DaorisError(`\`${command[0]}\` could not be run — ${result.error.message}. ${stays}`);
    }
    if (result.status !== 0) {
      const said = firstLine(result.stderr ?? '') ?? firstLine(result.stdout ?? '');
      throw new DaorisError(
        `npm could not say which release of \`${pkg}\` is newest — it exited ${result.status ?? 'on a signal'}`
        + `${said ? ` (${clip(said)})` : ''}. ${stays}`);
    }

    const version = versionFromNpm(result.stdout ?? '');
    if (!version) {
      const said = firstLine(result.stdout ?? '');
      throw new DaorisError(
        `npm answered no single version of \`${pkg}\`${said ? ` (it said \`${clip(said)}\`)` : ''}. ${stays}`);
    }
    return version;
  }

  /** Write the pin, and say what it now means. */
  function pinned(name: string, version: string, workspace: string | undefined): ExitCode {
    pinTo(name, version, workspace);
    write(workspace
      ? `daoris: \`${name}\` runs at ${version} for the \`${workspace}\` workspace on this machine.`
      : `daoris: \`${name}\` runs at ${version} on this machine.`);
    write('  Sessions spawn this binary rather than whatever is on PATH. `daoris agent unpin`');
    write('  puts it back, and the version is on every session record either way.');
    return 0;
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
      // What it IS, before what state it is in: `dsh` meant nothing to the owner until it said whose.
      if (toolchain.product) {
        write(`  ${name.padEnd(14)} ${toolchain.product}${toolchain.maker ? `, by ${toolchain.maker}` : ''}`);
        write(`  ${''.padEnd(14)} ${report.present ? report.version : `absent — ${report.problem}`}`);
      } else {
        write(`  ${name.padEnd(14)} ${report.present ? report.version : `absent — ${report.problem}`}`);
      }
      if (!report.present && toolchain.install) {
        write(`  ${''.padEnd(14)} \`daoris agent install ${name}\` installs it, with its own installer`);
      }

      // The pin, and whether it is actually in force (TOOL2/D57). A pin whose directory is not there
      // is reported as MISSING, with what the driver does about it: it refuses to start a session,
      // because running whatever is on PATH would be a different tool than the one asked for.
      const pinned = resolveVersion(settings, name, null, null);
      if (pinned) {
        const binary = managedBinary(home, name, pinned, toolchain.binary);
        write(`  ${''.padEnd(14)} pinned ${pinned} — `
          + (binary ? `managed: ${binary}` : 'NOT INSTALLED, so the driver refuses to start a session on it'));
        if (!binary) {
          write(`  ${''.padEnd(14)} \`daoris agent pin ${name} ${pinned}\` installs it`);
        }
      }

      for (const [circle, map] of Object.entries(settings.workspaceVersions)) {
        if (map[name]) write(`  ${''.padEnd(14)} pinned ${map[name]} for the \`${circle}\` workspace`);
      }

      if (report.profiles.length === 0) {
        write(`  ${''.padEnd(14)} no accounts — sessions run in the agent's own configuration home`);
        continue;
      }

      for (const profile of report.profiles) {
        const marks = [
          report.machineDefault === profile.name ? 'machine default' : null,
          ...Object.entries(settings.workspaces)
            .filter(([, map]) => map[name] === profile.name)
            .map(([circle]) => `default in ${circle}`),
        ].filter(Boolean);

        // 🔴 A key account is never "in": the tool says so for any key, a wrong one included (AGT3).
        write(
          `  ${''.padEnd(14)} ${profile.name.padEnd(16)} ${(profile.key ? 'unchecked' : profile.login).padEnd(9)}`
          + `${profile.account ? ` ${profile.account}` : ''}`
          + `${profile.key ? ` API key ${profile.key}` : ''}`
          + `${marks.length ? ` (${marks.join(', ')})` : ''}`);
      }
    }

    // The harnesses this machine's plugins declare (D64): configurations of the ACP door, listed
    // beside the build's own with the plugin they came from. One with no version question is asked
    // whether it is THERE rather than run, the same rule the driver's roster applies — an ACP agent
    // started bare waits on its stdin.
    for (const plugin of readPlugins(home).contributing) {
      for (const harness of plugin.manifest.harnesses) {
        write('');
        const present = harness.versionArguments
          ? probe(harness.name, {
            binary: harness.command,
            version: harness.versionArguments,
            profileVariable: harness.profileVariable ?? '',
            ...(harness.install ? { install: harness.install } : {}),
            ...(harness.package ? { package: harness.package } : {}),
            ...(harness.accountOf ? { accountOf: harness.accountOf } : {}),
          }, home, settings)
          : { present: resolvable(harness.command[0]!), version: 'present (no version question declared)', problem: `\`${harness.command[0]}\` is not there`, profiles: [] };
        write(`  ${harness.name.padEnd(14)} ${present.present ? present.version : `absent — ${present.problem}`}`
          + `  (declared by plugin \`${plugin.manifest.id}\`)`);
        const existing = profiles(home, harness.name);
        write(`  ${''.padEnd(14)} ${existing.length > 0 ? `accounts: ${existing.join(', ')}` : 'no accounts — sessions run in the agent\'s own configuration home'}`);
      }
    }

    write('');
    write('  An account is a directory Daoris owns the location of. The credential inside it belongs to');
    write("  the agent's own store. An API key is the one secret Daoris keeps: `agent key`, in keys.json.");
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
        const name = accountsOf(operand(argv, 2), 'profile add');
        const profile = bare(argv, 3, 'profile add', '<agent> <profile>');
        const where = profileHome(home, name, profile);
        const existed = existsSync(where);
        mkdirSync(where, { recursive: true });

        write(existed
          ? `daoris: \`${name}\` profile \`${profile}\` already exists — ${where}`
          : `daoris: \`${name}\` profile \`${profile}\` — ${where}`);
        write(`  It is empty until you log into it: \`daoris agent login ${name} --profile ${profile}\`.`);
        return 0;
      }

      case 'remove': {
        const name = accountsOf(operand(argv, 2), 'profile remove');
        const profile = bare(argv, 3, 'profile remove', '<agent> <profile>');
        const where = profileHome(home, name, profile);

        // 🔴 The account goes, directory and sign-in both (D66 §3, amending SES3's "deletes
        // nothing"): an account a person removes should not still be signed in on disk. The old
        // rule kept any directory the harness would not call signed out, so a removed account
        // stayed listed and signed in. The desktop's Remove is the same verb.
        const removed = removeProfile(home, name, profile);
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

        write(removed
          ? `daoris: removed the \`${name}\` account \`${profile}\` — the directory and the sign-in in it are gone: ${where}`
          : `daoris: \`${name}\` has no account \`${profile}\` on this machine — there was nothing to remove.`);
        write(`  It is no longer a default for \`${name}\` anywhere on this machine.`);
        return 0;
      }

      case 'default': {
        const name = accountsOf(operand(argv, 2), 'profile default');
        const profile = bare(argv, 3, 'profile default', '<agent> <profile> [--workspace <name>]');
        const workspace = flagValue(argv, '--workspace');
        // Refused rather than created: naming a default that does not exist is a typo with a silent
        // wrong answer available — every spawn in that circle would refuse, and the message would be
        // about logging in rather than about the name.
        if (!profiles(home, name).includes(profile)) {
          throw new DaorisError(
            `\`${name}\` has no profile \`${profile}\` on this machine — \`daoris agent profile add `
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
          `unknown agent profile verb '${action}' — one of: list, add, remove, default`);
    }
  }

  /**
   * `agent trust <agent> <folder> [--profile <name>] --yes` — the harness's trust in a folder (D73).
   *
   * @remarks
   * 🔴 **Asked, then written.** A terminal command cannot prompt: a gate runs it with stdin closed. So
   * the question IS the command without `--yes`. It names the folder, the account's file and what
   * trusting means, grants nothing, and exits 1. `--yes` is the person's answer. Granting on the first
   * keystroke would be the silent option with one more word in front of it.
   *
   * The account is the one a session there would run as: the profile named, else the machine's
   * default, else the agent's OWN configuration home (twin rule 3). A door's account is its owner's.
   */
  function trust(): ExitCode {
    const { name, toolchain } = required(argv, 'trust');
    if (!toolchain.trustFile) {
      throw new DaorisError(
        `\`${name}\` has no trust question Daoris knows of, so there is nothing to grant: its own `
        + 'permissions apply as they are.');
    }

    const folder = resolve(bare(argv, 2, 'trust', '<agent> <folder>'));
    if (!existsSync(folder) || !statSync(folder).isDirectory()) {
      throw new DaorisError(`no folder at \`${folder}\` — trust is granted to a folder a session would run in.`);
    }

    const owner = ownerOf(name);
    if (owner !== name) write(`daoris: \`${name}\` runs as \`${owner}\`'s accounts — this is one of them.`);
    const profile = flagValue(argv, '--profile') ?? readHarnessSettings(path).defaults[owner] ?? null;
    const file = join(profile ? profileHome(home, owner, profile) : homedir(), toolchain.trustFile);

    write(`daoris: trusting \`${folder}\` for \`${owner}\`, `
      + `${profile ? `as the account \`${profile}\`` : 'as its own account'} — in its own file:`);
    write(`  ${file}`);
    write('  This is what the agent asks the first time it runs in a folder. Once it is granted, the');
    write('  folder\'s own settings and `permissions.allow` apply there, and the agent stops asking.');
    write('  Daoris writes that one flag and nothing else in the file.');

    if (argv.includes('--dry-run')) return 0;
    if (!argv.includes('--yes')) {
      write('  Not granted: this is the question. Run it again with --yes to grant it.');
      return 1;
    }

    const grant = grantTrust(file, folder);
    if (!grant.changed) {
      write('  Already trusted, so nothing was written.');
    } else if (grant.verified) {
      write('  Granted.');
    } else {
      write('  Written, but reading the file back does not show it — a running Claude Code may have saved');
      write('  over it. Run this again once it has exited.');
      return 1;
    }

    return 0;
  }

  /**
   * The agent whose ACCOUNTS a verb on this name touches (AGT7): a door's are its owner's, and the
   * verb says so rather than quietly landing somewhere else than was typed.
   */
  function accountsOf(value: string | undefined, verb: string): string {
    const { name } = namedHarness(value, verb);
    const owner = ownerOf(name);
    if (owner !== name) write(`daoris: \`${name}\` runs as \`${owner}\`'s accounts — this is one of them.`);
    return owner;
  }

  /** The agent named after the verb, refused rather than defaulted. */
  function required(args: string[], verb: string): { name: string; toolchain: Toolchain } {
    return namedHarness(bare(args, 1, verb, '<agent>'), verb);
  }

  function namedHarness(value: string | undefined, verb: string): { name: string; toolchain: Toolchain } {
    if (!value) {
      throw new DaorisError(
        `\`agent ${verb}\` needs an agent — one of: ${Object.keys(TOOLCHAINS).join(', ')}`);
    }

    const toolchain = TOOLCHAINS[value];
    if (!toolchain) {
      // Never a silent fallback: a machine that installed a different harness than the person named
      // is the same failure as a repository that asked for one layout and received another (D23).
      throw new DaorisError(
        `unknown agent '${value}' — one of: ${Object.keys(TOOLCHAINS).join(', ')}. `
        + 'An agent is added deliberately, never guessed.');
    }

    return { name: value, toolchain };
  }

  /** The operand at `index`, counting the verb as 0 — flags and their values are never one. */
  function operand(args: string[], index: number): string | undefined {
    return operands(args, AGENT_VALUED)[index];
  }

  function bare(args: string[], index: number, verb: string, shape: string): string {
    const found = operand(args, index);
    if (found !== undefined) return found;

    throw new DaorisError(`\`agent ${verb}\` needs ${shape} — e.g. \`daoris agent ${verb} claude-code\``);
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
        + 'Daoris spawns the agent\'s own tooling; it does not vendor a copy of it.');
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
        `\`${token}\` cannot be passed to a Windows command shim safely. Run the agent's own `
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

  // Not found is not this function's decision to make: spawning reports it, with its own sentence.
  return onPath(command, { startable: true }) ?? command;
}

function firstLine(output: string): string | null {
  for (const line of output.split('\n')) {
    if (line.trim()) return line.trim();
  }

  return null;
}

/** One exact release, as npm and the channels write it — a prerelease or build suffix included. */
const RELEASE = /^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$/;

/**
 * The one version `npm view <package> version` answered, or null when it answered none or several
 * (USE1a). A warning npm prints beside it is not a version, and quotes around one are npm's own.
 * The driver's `VersionFromNpm` is the twin.
 */
export function versionFromNpm(output: string): string | null {
  const found = new Set(output.split(/\r?\n/)
    .map((line) => line.trim().replace(/^['"]+|['"]+$/g, ''))
    .filter((line) => RELEASE.test(line)));
  return found.size === 1 ? [...found][0]! : null;
}

/**
 * Which of two releases is newer: negative when `a` is older, positive when newer, and null when
 * either is not a version this can order. A release outranks its own prereleases.
 */
function compareReleases(a: string, b: string): number | null {
  const left = RELEASE.exec(a);
  const right = RELEASE.exec(b);
  if (!left || !right) return null;

  for (let at = 1; at <= 3; at++) {
    // Compared as digit strings, so a number too long for a double still orders exactly.
    const x = left[at]!.replace(/^0+/, '');
    const y = right[at]!.replace(/^0+/, '');
    if (x.length !== y.length) return x.length - y.length;
    if (x !== y) return x < y ? -1 : 1;
  }
  if (left[4] === right[4]) return 0;
  if (left[4] === undefined) return 1;
  if (right[4] === undefined) return -1;
  return left[4] < right[4] ? -1 : 1;
}

/** A line somebody else's tool printed, short enough to sit inside a sentence. */
function clip(text: string): string {
  return text.length > 160 ? `${text.slice(0, 160)}…` : text;
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
