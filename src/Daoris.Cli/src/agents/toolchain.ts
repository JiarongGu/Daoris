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
//   5. An account made by signing in, or by a key, takes a fresh id, never reused (`newAccountId`, ACCT2); who it is, is
//      the tool's answer, offered as its name at the sign-in's end.
//   6. An account that is an API key keeps its key in `keys.json` under the home, beside the account.
//   7. A door's accounts, defaults and keys are its owner's (`accountOf`); its pin is its own.
//   8. The order rotation may use (`rotation`, `workspaceRotation`), and how each list is used (`rotationUse`,
//      `workspaceRotationUse`, TOOL6a), are read, resolved and edited by `rotation.ts`, and each writer keeps the other's
//      sections — for the same wiring both write the same bytes (TOOL4e).
//   9. An account's cool-off (`cooling.json`) is the driver's to write; here it is read, listed and ended — by
//      `profile ready`, a sign-in or a key into the account, and the account's removal (`cooling.ts`, TOOL4e).
//  10. An account's name is the person's, kept in `accounts.json` by its id, which is the folder's name; every verb takes
//      an account by its id or its name, and writes its id (`accountnames.ts`, ACCT2).
//  11. A sign-in into an account reaches one that is there, never a new folder; a new account's sign-in, and `profile
//      join`, put it in the lists the person names, and both say which lists and defaults hold it (`rotation.ts`, ACCT1).
//  12. Each account's sign-in this command reads, at `agent list` and at a sign-in's end, is kept in `reads.json` with
//      when, under its owner, where the driver keeps its own readings; a question never asked keeps nothing, and a
//      removed account's reading goes with it (`accountreads.ts`, AGENTREAD1).
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
import { THIS_INSTALL, daorisHome, homeEntry, homeOf, homePlaces, requireHomeFile } from '../platform/home.ts';
import { commandOnTheSystem, commandThroughTools, handTools, readTools } from '../tools/tools.ts';
import { pastLauncher } from '../tools/gitforwindows.ts';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import type { SpawnSyncReturns, StdioOptions } from 'node:child_process';
import { flagValue, operands } from '../platform/args.ts';

/** The `agent` flags that take a value — so that value is never read as an operand. */
const AGENT_VALUED: ReadonlySet<string> = new Set([
  '--profile', '--workspace', '--account', '--for', '--keep', '--switch', '--curve', '--early', '--near', '--name', '--join',
]);

/**
 * `agent profile use`'s flags (TOOL6a, D130 §16.6; D165 §6, SWITCH1a); any other is refused rather than read as a choice.
 * `--early` and `--near` are the old names of `--switch` and the session window's curve, taken as `UseChange` maps them.
 */
const USE_FLAGS: ReadonlySet<string> = new Set([
  '--keep', '--no-keep', '--switch', '--curve', '--no-curve', '--early', '--near', '--workspace', '--clear',
]);

/**
 * What the terminal says for each *use accounts* choice (D130 §16.6): its name, and what it costs where the screen says
 * so. A choice added to `USE_MODES` is a row here, and its step in `nextStartLines`.
 */
export const USE_WORDS: Record<UseMode, { name: string; cost: string | null }> = {
  goal: { name: 'make the most of them', cost: null },
  order: { name: 'one by one, in order', cost: 'one limit stops every session on that account' },
};

/** `agent profile use`'s shape, as a refusal names it. */
const USE_SHAPE = `[${USE_MODES.join('|')}] [--keep <account>|--no-keep] [--switch ${SWITCHES.join('|')}] `
  + '[--curve <window>=<elapsed>:<used>,…]… [--no-curve] [--workspace W], or --clear (--early on|off and --near <percent> '
  + 'are the old names)';

/** How a curve is given at the terminal (the switch design §5), as a refusal shows it. */
const CURVE_EXAMPLE = '`--curve weekly=0:30,50:70,100:100`';

/** A scope named in a sentence: this machine, or one workspace. */
function where(workspace: string | null): string {
  return workspace ? `in \`${workspace}\`` : 'on this machine';
}

/** A scope named in a command: nothing for the machine, `--workspace` for one workspace, spelled for a shell (ACCTQUOTE1). */
function scoped(workspace: string | null): string {
  return workspace ? ` --workspace ${shellWord(workspace, '<workspace>')}` : '';
}

/** An account in a command (ACCTQUOTE1): by its name where a shell can take it, else by its id, else a placeholder. */
function accountWord(shown: string, id: string): string {
  return shellWord(shown, shellWord(id, '<account>'));
}

/** Choices said as a person says them: `a, b or c`. */
function either(choices: readonly string[]): string {
  return choices.length > 1 ? `${choices.slice(0, -1).join(', ')} or ${choices.at(-1)}` : choices.join('');
}
import { DaorisError } from '../platform/errors.ts';
import { onPath, readJsonObject, writeJsonAtomic } from '../platform/fsx.ts';
import { writeKeeping } from '../platform/jsonkeep.ts';
import { normalizeWorkspace } from '../service/remotemap.ts';
// A cycle with `plugins.ts`, harmless because both sides read the other only inside functions:
// `harness list` shows the harnesses plugins declare, and the catalogue refuses the names this table has.
import { readPlugins, resolvable } from '../plugins/plugins.ts';
import { installFromChannel, latestVersion, refuseVersion } from './channels.ts';
import {
  CURVE_WINDOWS, NEAR_HIGHEST, NEAR_LOWEST, OLD_FIELDS, SWITCHES, USE_FIELDS, USE_MODES, curveProblem, joinProblem,
  joinRefusal, placesOf, readOrderCircles, readOrders, readUse, readUseCircles, readUses, resolveRotation, resolveScope,
  rotationProblem, rotationRefusal, scopeProblem, withJoined, withRotation, withUse, withoutAccount, writtenOrderCircles,
  usesAsRead, writtenOrders, writtenUseCircles, writtenUses,
} from '../accounts/rotation.ts';
import type {
  AccountPlace, Curve, CurvePoint, Orders, RotationUse, Scope, ScopeProblem, Switch, UseChange, UseMode, Uses,
} from '../accounts/rotation.ts';
import {
  accountNames, accountsPath, apartWords, forgetName, nameBeside, nameFor, nameProblem, newAccountId, offeredName, renameAccount,
  resolveAccount, shownAs, signedInSentence, signInRefusal, signInTarget, takenWayOut, AccountNameError,
} from '../accounts/accountnames.ts';
import { forgetRead, keepRead } from '../accounts/accountreads.ts';
import type { Login } from '../accounts/accountreads.ts';
import { atName, byName, findName, sameName } from '../platform/casefold.ts';
import { coolingLine, coolingOf, coolingWhen, endCooling, machineZone, readCooling } from '../accounts/cooling.ts';
import { shellWord, shellWords } from '../platform/shellword.ts';
import { OWN_WINDOWS, saidLine, saidOf } from '../accounts/windows.ts';
import { markSignedIn, probeLockPath, takeProbeLock } from '../accounts/probelock.ts';
import type { Channel, Fetcher } from './channels.ts';
import type { CommandArgs } from '../platform/types.ts';
import type { ExitCode } from '../platform/errors.ts';
import { AGENT_ROUTES, ALONE, type InstanceRoute, type VerbDoor } from '../platform/door.ts';

export const PATH_VARIABLE = 'DAORIS_HARNESS_CONFIG';

/** Where the wiring lives; the profile tree sits beside it. */
export function harnessesPath(env: Record<string, string | undefined> = process.env): string {
  return env[PATH_VARIABLE] ?? requireHomeFile(env, 'harnesses');
}

/**
 * The Daoris home the wiring is in (D63), which the profile tree is under: the home whose `harnesses` entry this file is, at its
 * new place or its old (HOMELAYOUT3, the driver's `HarnessSettings.HomeOf`).
 */
export function harnessHome(path = harnessesPath()): string {
  return homeOf(path, 'harnesses');
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
  /** Its own login flow, as arguments to the binary, run with a profile home in the environment. Offered first. */
  login?: string[];
  /**
   * Its sign-in by a device code, beside `login` (CODEXLOGIN1, D125's note): `agent login <agent> --device-code`. Declared
   * only where measured. The driver's `DeviceLoginArguments` is the twin.
   */
  deviceLogin?: string[];
  /**
   * The local ports `login` waits on for the browser's answer, in the order it tries them (CODEXLOGIN1: Codex 0.160.0 takes
   * 1455, else 1457). Where none can be taken the sign-in is refused before it starts, offering `--device-code`. The
   * driver's `LoginPorts` is the twin.
   */
  loginPorts?: number[];
  /**
   * How it answers "is this configuration home logged in?" — and the two shapes that answer takes.
   * Both supported harnesses EXIT 0 EITHER WAY, so the output is the answer and the exit code is
   * deliberately not consulted. Neither pattern matching means unknown, never logged-out.
   *
   * `account`, when given, is a pattern whose first group is WHO is signed in there (D66 §3) — the
   * name a person knows the account by. Read only on a yes.
   *
   * `org` and `plan`, when given, read the organisation and the plan the sign-in is in, on a yes only (ACCOUNTID1): one
   * email may sign in to two organisations, and they are what tells the two accounts apart. Read fresh, written nowhere,
   * as the driver's `LoginQuestion.Org` and `Plan` read them.
   */
  loginCheck?: { args: string[]; in: RegExp; out: RegExp; account?: RegExp; org?: RegExp; plan?: RegExp };
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
   * The tool's own variable for an API key (AGT3, D67 §1): what an account that is a key is handed
   * at spawn. Declared only where measured; absent means this agent takes no key from Daoris. The
   * driver's `KeyVariable` is the twin.
   */
  keyVariable?: string;
  /**
   * That this tool says how much of each window an account has used (TOOL6c, D130 §5.2; CODEXUSE1b): the driver reads its
   * frame on the door its sessions run on by its readings table, or asks its own server where no door carries one
   * (CODEXUSE1), and keeps what it said in `windows.json`, so *switch before the limit* and pace act on it. Declared only
   * where a frame or an answer was recorded (limit-signals evidence §1, Codex usage evidence §2); a door's are its owner's
   * (twin rule 7). The driver's `Speaks` is the twin: its `Windows` or its `Usage`. This command reads what was kept and
   * asks no server itself.
   */
  windows?: boolean;
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
    // It answers JSON — and volunteers an email, an organisation and a subscription tier with it. Daoris reads the
    // boolean, the email (who signed in, D66 §3), and the organisation and the plan, which tell two sign-ins of one email
    // apart (ACCOUNTID1); it keeps none of them. The driver's twin is `ClaudeCodeAdapter`'s `LoginCheck`.
    loginCheck: {
      args: ['auth', 'status'],
      in: /"loggedIn"\s*:\s*true/i,
      out: /"loggedIn"\s*:\s*false/i,
      account: /"email"\s*:\s*"([^"]+)"/i,
      org: /"orgName"\s*:\s*"([^"]+)"/i,
      plan: /"subscriptionType"\s*:\s*"([^"]+)"/i,
    },
    // 🔴 A pinned 2.1.270 reported its own auto-updates ENABLED (`claude doctor`, no login); with
    // this it reported them disabled, refused `claude update`, and stayed 2.1.270.
    pinnedEnv: { DISABLE_UPDATES: '1' },
    // Measured on 2.1.280 with an invalid key: `auth status` reads it, and `-p` takes it unprompted.
    keyVariable: 'ANTHROPIC_API_KEY',
    // Its trust record and its account's settings file are the driver's alone since `agent trust` and `agent settings` went to it
    // (PUSH1f2d): `HarnessToolchain.TrustFile` and `SettingsFile`.
    // Each window's use and reset on every `rate_limit_event`, which its ACP door forwards (TOOL6c, limit-signals evidence §1, §3).
    windows: true,
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
    // By its browser first, as ChatGPT's desktop app signs in (CODEXLOGIN1, D125's note): a ChatGPT workspace may refuse
    // device codes. It waits for the browser's answer on 127.0.0.1:1455, else 1457 (measured with 0.160.0), tried before it
    // starts. Its device code beside it needs no port (CODEXACCT2, which took it alone when a reserved range covered 1455).
    login: ['login'],
    deviceLogin: ['login', '--device-auth'],
    loginPorts: [1455, 1457],
    // ANCHORED, and that is load-bearing: this harness answers a sentence rather than a field, and
    // "Not logged in" contains "logged in". An unanchored pattern reported every logged-out profile
    // as logged in — found by a test, which is the only way a thing like this is ever found.
    loginCheck: { args: ['login', 'status'], in: /^\s*logged in/im, out: /^\s*not logged in/im },
    // Each window's use and reset, asked of its own app server under the account's home, since no door of Daoris's carries
    // them (CODEXUSE1b; the driver's `Usage`, declared on `codex-acp`; Codex usage evidence §1, §2).
    windows: true,
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
    // The name its maker publishes it under (AGENTS2): `dsh` beside DeepSeek still read as no DeepSeek agent. The binary
    // and this entry's name stay `dsh`, which is what a terminal types.
    product: 'DeepSeek Harness',
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
  /**
   * Which accounts rotation may use, per agent, in the person's order (TOOL4e, D125 §3.1): the machine's, and one
   * workspace's. `rotation.ts` reads, resolves and edits them; absent is no rotation.
   */
  rotation: Orders;
  /** @see rotation */
  workspaceRotation: Record<string, Orders>;
  /**
   * How each scope's list is used, per agent (TOOL6a, D130 §2): the machine's, and one workspace's. `rotation.ts` reads,
   * resolves and edits them, keeping what this build does not know; absent is today's default.
   */
  rotationUse: Uses;
  /** @see rotationUse */
  workspaceRotationUse: Record<string, Uses>;
  /** Everything else the file held, preserved — this is an editor, not the file's owner. */
  rest: Record<string, unknown>;
  /**
   * The file as `readHarnessSettings` understood it, written (`harnessesWritten`, each settings entry as the file holds it), or absent
   * for wiring built here (KEEPALL1b): what an edit is measured from.
   */
  understood?: string;
  /** Whether a file was there at the read, for `writeKeeping`. */
  found?: boolean;
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
    defaults: {}, workspaces: {}, versions: {}, workspaceVersions: {}, rotation: {}, workspaceRotation: {},
    rotationUse: {}, workspaceRotationUse: {}, rest: {},
  };
  // Unreadable reads as empty, for a session about to spawn; an EDIT over it is refused by the writer.
  const { value: parsed, problem } = readJsonObject(path);
  if (problem !== null) return empty;
  if (parsed === null) return { ...empty, understood: JSON.stringify(harnessesWritten(empty, true)), found: false };

  const {
    defaults, workspaces, versions, workspaceVersions, rotation, workspaceRotation, rotationUse, workspaceRotationUse, ...rest
  } = parsed;
  const settings: HarnessSettings = {
    defaults: stringMap(defaults),
    workspaces: circlesOf(workspaces),
    versions: stringMap(versions),
    workspaceVersions: circlesOf(workspaceVersions),
    rotation: readOrders(rotation),
    workspaceRotation: readOrderCircles(workspaceRotation),
    rotationUse: readUses(rotationUse),
    workspaceRotationUse: readUseCircles(workspaceRotationUse),
    rest,
  };
  return { ...settings, understood: JSON.stringify(harnessesWritten(settings, true)), found: true };
}

/**
 * Write it back, preserving anything this build did not put there: the edit from what was read made onto the file as it stands
 * (KEEPALL1b, D173's note; `jsonkeep.ts`' `writeKeeping`), so a key this build has no field for stays where it is at every level, a
 * change another writer made since the read stays, and no edit writes nothing. Each settings entry was understood as the file holds
 * it, so a save writes an old or retired name as the new (D165 §6). Wiring built here is written whole.
 *
 * @remarks
 * 🔴 The driver's `HarnessSettings.Save` is the other writer of this file, and for the same wiring the two write the same
 * bytes (TOOL4e, TOOL6a; `rotation.test.ts`, `RotationTwinTests` and `home-files-keep.json` hold it).
 */
export function writeHarnessSettings(path: string, settings: HarnessSettings): void {
  if (settings.understood === undefined) writeJsonAtomic(path, harnessesWritten(settings, false));
  else writeKeeping(path, settings.found === true, JSON.parse(settings.understood) as Record<string, unknown>, harnessesWritten(settings, false));
}

/**
 * The wiring in the writer's layout, as the driver's `HarnessSettings.Layout`: what neither knows first, then the four sections, then
 * each order and its settings only where one is set; `asRead` writes each settings entry as the file held it.
 */
function harnessesWritten(settings: HarnessSettings, asRead: boolean): Record<string, unknown> {
  const circles = (map: Record<string, Record<string, string>>) => Object.fromEntries(
    Object.entries(map)
      .filter(([, inner]) => Object.keys(inner).length > 0)
      .sort(([a], [b]) => (a < b ? -1 : 1)));
  const rotation = writtenOrders(settings.rotation);
  const rotationUse = asRead ? usesAsRead(settings.rotationUse) : writtenUses(settings.rotationUse);
  const workspaceRotation = writtenOrderCircles(settings.workspaceRotation);
  const workspaceRotationUse = asRead
    ? Object.fromEntries(Object.entries(settings.workspaceRotationUse)
      .map(([workspace, uses]) => [workspace, usesAsRead(uses)] as const)
      .filter(([, uses]) => Object.keys(uses).length > 0)
      .sort(([a], [b]) => (a < b ? -1 : 1)))
    : writtenUseCircles(settings.workspaceRotationUse);

  return {
    ...settings.rest,
    defaults: sorted(settings.defaults),
    workspaces: circles(settings.workspaces),
    versions: sorted(settings.versions),
    workspaceVersions: circles(settings.workspaceVersions),
    ...(Object.keys(rotation).length > 0 ? { rotation } : {}),
    ...(Object.keys(rotationUse).length > 0 ? { rotationUse } : {}),
    ...(Object.keys(workspaceRotation).length > 0 ? { workspaceRotation } : {}),
    ...(Object.keys(workspaceRotationUse).length > 0 ? { workspaceRotationUse } : {}),
  };
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

  const circle = workspace?.trim() ? atName(settings.workspaces, workspace.trim()) : undefined;
  if (circle?.[harness]?.trim()) return circle[harness]!.trim();

  return settings.defaults[harness]?.trim() || null;
}

/**
 * An account's default set, or cleared with `null` (D49 §4, LEFT3): the machine's, or one workspace's.
 *
 * @remarks
 * 🔴 **A twin of the screen's write**, `HARNESS_ACTION`'s `profile-default` (`DriverModule.DefaultEdited`), which clears
 * by naming no account. Each side's table holds the same rows (`toolchain.test.ts`, `ProfileDefaultTwinTests`). A clear
 * removes one entry and nothing else, and a workspace left naming no account is dropped, as the driver drops it; so
 * absence then means what it always means, the workspace falls back to the machine's default, and that to the agent's
 * own configuration home.
 */
export function withDefault(
  settings: HarnessSettings, owner: string, profile: string | null, workspace: string | null,
): HarnessSettings {
  if (!workspace) {
    const defaults = { ...settings.defaults };
    if (profile) defaults[owner] = profile;
    else delete defaults[owner];
    return { ...settings, defaults };
  }

  // CASEFOLD1c: under the spelling the workspace was first written in, as the driver's `WithWorkspaceDefault` keeps it.
  const key = findName(Object.keys(settings.workspaces), workspace) ?? workspace;
  const circle = { ...settings.workspaces[key] };
  if (profile) circle[owner] = profile;
  else delete circle[owner];
  const workspaces = { ...settings.workspaces, [key]: circle };
  if (Object.keys(circle).length === 0) delete workspaces[key];
  return { ...settings, workspaces };
}

/** Where a named profile's configuration home is. Daoris owns this location and nothing inside it. */
export function profileHome(home: string, harness: string, profile: string): string {
  return homeEntry(home, 'agent-homes', safeName(harness, 'agent name'), safeName(profile, 'profile name'));
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

  const circle = workspace?.trim() ? atName(settings.workspaceVersions, workspace.trim()) : undefined;
  if (circle?.[harness]?.trim()) return circle[harness]!.trim();

  return settings.versions[harness]?.trim() || null;
}

/** Where a managed version of a harness lives. Daoris owns this location, binary and all. */
export function managedHome(home: string, harness: string, version: string): string {
  return homeEntry(home, 'toolchain', safeName(harness, 'agent name'), safeName(version, 'version'));
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

/**
 * The profiles that exist — the directories that exist, sorted. There is no second register. Both places of the account homes
 * are read, as a list of an entry's items is (D172 point 5, HOMELAYOUT2). A lock's folder or a hidden one is no account
 * (`notAnAccount`, HELPSTUCK1).
 */
export function profiles(home: string, harness: string): string[] {
  const names = homePlaces(home, 'agent-homes', safeName(harness, 'agent name'))
    .filter((root) => existsSync(root))
    .flatMap((root) => readdirSync(root, { withFileTypes: true }))
    .filter((entry) => entry.isDirectory() && !notAnAccount(entry.name))
    .map((entry) => entry.name);
  return [...new Set(names)].sort();
}

/**
 * Whether a folder beside the accounts is no account (HELPSTUCK1, twin rule 1): a lock's, its name ending `.lock` in any case,
 * since a lockfile library locks a folder by making `<folder>.lock` beside it, and Claude Code locks its configuration home so;
 * or a hidden one, its name starting `.`. Listed as an account, a lock's folder was signed in to and run in, and a lock of the
 * account it is named after then met a home. The driver's `HarnessSettings.NotAnAccount` is the twin.
 */
export function notAnAccount(name: string): boolean {
  return name.startsWith('.') || name.toLowerCase().endsWith('.lock');
}

/**
 * The id an account made by signing in, or by a key, gets (D66 §3, ACCT2): a fresh one, never reused
 * (`newAccountId`), where it was the first free `account-N`. Twin rule 5.
 *
 * @remarks
 * 🔴 **A neutral id, never who signed in.** It is needed before the sign-in starts, when nobody knows
 * whose it is; and renaming the directory afterwards would move a home a harness may have keyed its
 * credential to. What a person reads is the account's name, offered at the sign-in's end as who signed
 * in and kept only where the person keeps it (`accountnames.ts`).
 */
export function nextAccount(home: string, harness: string): string {
  return newAccountId(profiles(home, harness));
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
  // And with its cool-off (TOOL4e): an account made later by `profile add` under its name starts afresh. And its name (ACCT2),
  // and its last reading (AGENTREAD1), as the driver's `Removed` forgets one, so one made later under its name is never read.
  endCooling(home, harness, profile, new Date());
  forgetName(home, harness, profile);
  forgetRead(home, harness, profile);
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
  return homeEntry(home, 'keys');
}

function readKeys(home: string): Keys {
  // An unreadable file holds no key anyone can be handed; the account then asks for one. Writing a
  // new key over it is refused by `writeJsonAtomic`, or every other account's key would go with it.
  const { value: parsed } = readJsonObject(keysPath(home));
  if (parsed === null) return {};
  return Object.fromEntries(Object.entries(parsed).map(([agent, held]) => [agent, stringMap(held)]));
}

/**
 * The edit from `read` made onto the file as it stands (KEEPALL1, `jsonkeep.ts`' `writeKeeping`): a key this build has no field for
 * stays where it is, as written, at the top and inside an agent's keys, and a file that does not read is never written over. The
 * driver's `HarnessKeys` does the same.
 */
function writeKeys(home: string, keys: Keys, read: Keys): void {
  const written = (each: Keys) => Object.fromEntries(Object.entries(each)
    .filter(([, held]) => Object.keys(held).length > 0)
    .sort(([a], [b]) => (a < b ? -1 : 1))
    .map(([agent, held]) => [agent, sorted(held)]));
  const path = keysPath(home);
  writeKeeping(path, existsSync(path), written(read), written(keys));
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
  const { [profile]: _gone, ...held } = keys[harness]!;
  writeKeys(home, { ...keys, [harness]: held }, keys);
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
  // The key first: a `keys.json` it cannot read refuses here, before an account directory exists.
  writeKeys(home, { ...keys, [harness]: { ...keys[harness], [account]: key } }, keys);
  mkdirSync(profileHome(home, harness, account), { recursive: true });
  // A key made into an account ends a cool-off its name still carries (D125 §2.3), as the driver's `HarnessKeys.Add` does,
  // and is marked as a sign-in is (TOOL6g), so a name a removed account had carries no word that it is signed out.
  endCooling(home, harness, account, new Date());
  markSignedIn(home, harness, account, new Date());

  write(`daoris: \`${harness}\` account \`${account}\` is the API key ${keyHandle(key)}.`);
  write(`  Kept in ${keysPath(home)} — machine-local, tracked by nothing, shown back only as its last four.`);
  const id = shellWord(account, '<account>');
  write(`  \`daoris agent profile rename ${harness} ${id} <name>\` gives it a name of yours (ACCT2), and`);
  write(`  \`daoris agent profile join ${harness} ${id} <workspace>…|--machine\` puts it in a list a start runs on.`);
  return account;
}

/**
 * Write a new key into an account that is already a key (KEYREPLACE1a): the repair for a key its provider refused. The account
 * keeps its id, so its name, lists, defaults and order stand (ACCT2); what marked it refused is cleared as the driver's
 * `HarnessKeys.Replace` clears it. Said back by the handle only. The account is an id or a name; refused, before anything is
 * written, for an agent with no key variable, an account not here or not a key, and a blank or spaced key.
 */
export function replaceKeyAccount(
  home: string, door: string, given: string, raw: string, write: (line: string) => void,
): string {
  const harness = ownerOf(door);
  if (!TOOLCHAINS[harness]?.keyVariable) {
    throw new DaorisError(`\`${door}\` takes no API key from Daoris — sign in with its own login instead.`);
  }

  const account = resolveAccount(profiles(home, harness), accountNames(home, harness), given) ?? given.trim();
  if (!profiles(home, harness).includes(account)) {
    throw new DaorisError(
      `\`${harness}\` has no account \`${given.trim()}\` on this machine — accounts that exist: `
      + `${profiles(home, harness).join(', ') || '(none)'}`);
  }
  const keys = readKeys(home);
  if (!keys[harness]?.[account]) {
    throw new DaorisError(`\`${account}\` is not an API key account — it is a sign-in, so sign in again with \`daoris agent login ${harness}\`.`);
  }

  const key = raw.trim();
  if (!key || /\s/.test(key)) throw new DaorisError('that is not an API key — it is blank, or has spaces in it.');

  writeKeys(home, { ...keys, [harness]: { ...keys[harness], [account]: key } }, keys);
  // What marked the account refused: its last reading, its cool-off. Marked signed in, as a key made into an account is.
  forgetRead(home, harness, account);
  endCooling(home, harness, account, new Date());
  markSignedIn(home, harness, account, new Date());

  write(`daoris: \`${harness}\` account \`${account}\` is now the API key ${keyHandle(key)}; its name, lists and defaults are as they were.`);
  write(`  Kept in ${keysPath(home)} — machine-local, tracked by nothing, shown back only as its last four.`);
  write('  A running desktop reads the account again at its next press; this terminal cannot tell it sooner.');
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
   * account's handle (AGT3) — never the key — or null for a sign-in. `org` and `plan` are the organisation and the plan
   * the tool says beside who (ACCOUNTID1), read fresh and written nowhere; absent or null where it says none.
   */
  profiles: {
    name: string; home: string; login: Login; account: string | null; key: string | null;
    org?: string | null; plan?: string | null;
  }[];
}

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
  const base: Env = { ...process.env, ...(managed ? toolchain.pinnedEnv : {}), ...account };
  if (profile) {
    // Created as part of selecting it: at least one supported harness refuses to start when its home
    // variable names a path that does not exist.
    mkdirSync(profile, { recursive: true });
    base[toolchain.profileVariable] = profile;
  }

  // 🔴 Through `spawnable`, for the same reason `install` needs it: a managed pin's shim is a `.cmd`
  // on Windows, and a bare `spawnSync` answers ENOENT/EINVAL for one — which reads as "not installed"
  // about a binary that is installed and works. `startChild` is where that happens.
  const result = startChild([...command, ...args], base, { timeout: 20_000 });

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
      const said = loginAt([command!, ...toolchain.binary.slice(1)], toolchain, where, Boolean(pinned),
        key ? { [toolchain.keyVariable!]: key } : {}, probeLockPath(home, owner, name));
      // Kept where the driver keeps its own readings (AGENTREAD1), so the screen and the loop learn from this one too.
      if (said.asked) keepReading(home, owner, name, said.login);
      return { name, home: where, login: said.login, account: said.account, key: shown, org: said.org ?? null, plan: said.plan ?? null };
    }),
  };
}

/**
 * Keep what one account's sign-in was just read as (AGENTREAD1, twin rule 12): in `reads.json`, under its owner, with now.
 * A reading not written costs the screen what this one learned, never the answer the terminal gives, as a reading the
 * driver could not write costs it a restart's knowledge.
 */
function keepReading(home: string, owner: string, account: string, login: Login): void {
  try {
    keepRead(home, owner, account, login, new Date());
  } catch (error) {
    if (typeof (error as NodeJS.ErrnoException).code !== 'string') throw error;
  }
}

/**
 * What the harness says about logging in to one home: the boolean, and — when the toolchain asks and
 * the answer is yes — who (D66 §3), with the organisation and the plan beside them (ACCOUNTID1). None of it is kept.
 *
 * @remarks
 * 🔴 Asked under the home's probe lock (TOOL6g, `probelock.ts`) where one is named: the agent may refresh an expired token
 * to answer, and the desktop asking the same account at the same moment would spend one single-use refresh token twice
 * and sign the account out. A lock another holds past its patience is not asked about: unknown.
 *
 * `asked` says whether the question was put at all (ROSTER1, AGENTREAD1): one never asked — no login question, or a lock
 * another kept — is no reading and is kept nowhere, as the driver's `LoginAnswer.Asked` says; one asked whose command did
 * not run is a reading of unknown.
 */
function loginAt(
  command: string[], toolchain: Toolchain, where: string, managed = false,
  account: Record<string, string> = {}, lock: string | null = null,
): LoginSaid {
  const check = toolchain.loginCheck;
  if (!check) return { login: 'unknown', account: null, asked: false };

  const release = lock === null ? null : takeProbeLock(lock);
  if (lock !== null && release === null) return { login: 'unknown', account: null, asked: false };
  let answer: ReturnType<typeof ask>;
  try {
    answer = ask(command, check.args, where, toolchain, managed, account);
  } finally {
    release?.();
  }
  if (!answer.ran) return { login: 'unknown', account: null, asked: true };
  if (check.in.test(answer.output)) {
    const read = (pattern: RegExp | undefined) => pattern?.exec(answer.output)?.[1]?.trim() || null;
    return { login: 'in', account: read(check.account), org: read(check.org), plan: read(check.plan), asked: true };
  }
  return { login: check.out.test(answer.output) ? 'out' : 'unknown', account: null, asked: true };
}

/**
 * One status answer, as `loginAt` read it: the state, and on a yes who with the organisation and the plan beside them
 * (ACCOUNTID1), each null or absent where the agent says none; and whether it was asked at all.
 */
interface LoginSaid {
  login: Login;
  account: string | null;
  org?: string | null;
  plan?: string | null;
  asked: boolean;
}

/**
 * What a sign-in does beside signing in (ACCT1, ACCT2): the name the person gave the account before it began, and the
 * lists it joins at its end — a workspace's name, or null for this machine's. `path` is the wiring file the joins are
 * written to and the end's report reads; none joins nothing and reports no list.
 */
export interface SignInOptions {
  name?: string | null;
  joins?: (string | null)[];
  path?: string | null;
  /** The binary the sign-in ran and its end's question asks (`signInBinary`); the toolchain's own where none is given. */
  binary?: SignInBinary;
}

/** The binary an agent's sign-in runs: its command, and whether that is its pin, which runs with the pin's switch (AGT2). */
export interface SignInBinary {
  command: string[];
  managed: boolean;
}

/**
 * The binary a sign-in runs (CODEXACCT1b, D57 rule 4): the agent's pin on this machine, else `PATH`'s, as `agent list`
 * asks it (`probe`), so the sign-in and the question at its end ask the program a session runs. The terminal's sign-in
 * ran `PATH`'s, and the owner's only `codex` is its pin. A pin with nothing installed at it is refused naming the pin,
 * before anything is made, never `PATH`'s instead; the driver's `HarnessActions.PrepareLogin` says the same words.
 */
export function signInBinary(
  harness: string, toolchain: Toolchain, home: string, settings: HarnessSettings,
): SignInBinary {
  const pinned = resolveVersion(settings, harness, null, null);
  if (!pinned) return { command: toolchain.binary, managed: false };

  const managed = managedBinary(home, harness, pinned, toolchain.binary);
  if (managed === null) {
    throw new DaorisError(
      `\`${harness}\` is pinned to ${pinned} on this machine, and nothing is installed at that version, so nothing was `
      + `signed in — \`daoris agent pin ${harness} ${pinned}\` installs it, and \`daoris agent unpin ${harness}\` goes `
      + 'back to PATH.');
  }
  return { command: [managed, ...toolchain.binary.slice(1)], managed: true };
}

/**
 * Whether a local port can be taken now (CODEXLOGIN1): `free` once taken and let go, `in-use` where another program holds it,
 * `reserved` where Windows refuses it (a range it reserves, os error 10013), `refused` for any other reason. The driver's
 * `PortHold` is the twin.
 */
export type PortHold = 'free' | 'in-use' | 'reserved' | 'refused';

/** How a port is tried: `service.ts`' `portHold`, the one module that holds a socket, unless a test's. */
export type PortProbe = (port: number) => Promise<PortHold>;

/**
 * Which of a browser sign-in's ports are held and by what, in plain words (CODEXLOGIN1, D125's note): the driver's
 * `SignInPorts.Held` says it to the byte. The way out after it is each door's own.
 */
export function portsHeld(agent: string, login: readonly string[], held: readonly { port: number; hold: PortHold }[]): string {
  const ports = held.map((each) => each.port).join(', else ');
  const none = held.length === 1 ? 'it cannot' : held.length === 2 ? 'neither can' : 'none can';
  const words = (hold: PortHold) => hold === 'in-use' ? 'held by another program'
    : hold === 'reserved' ? 'held by a range Windows reserves, which a restart may move' : 'refused to Daoris';
  const why = held.map((each) => `${each.port} is ${words(each.hold)}`).join('; ');
  return `\`${[agent, ...login].join(' ')}\` waits for your browser on port ${ports}, and ${none} be taken now: ${why}.`;
}

/**
 * The ports a browser sign-in waits on, each tried once before it starts (CODEXLOGIN1): null where one is free, since the tool
 * takes the next where the first is held (measured with Codex 0.160.0), else each port with what holds it. Nothing is tried
 * again in a loop.
 */
async function heldPorts(ports: readonly number[], probe: PortProbe): Promise<{ port: number; hold: PortHold }[] | null> {
  const held: { port: number; hold: PortHold }[] = [];
  for (const port of ports) {
    const hold = await probe(port);
    if (hold === 'free') return null;
    held.push({ port, hold });
  }
  return held;
}

/** One scope an account runs in, as a person reads it: `this machine's list, as its default`, `` `work`'s default ``. */
export function placeWords(place: AccountPlace): string {
  const scope = place.workspace === null ? 'this machine\'s' : `\`${place.workspace}\`'s`;
  if (place.list && place.default) return `${scope} list, as its default`;
  return `${scope} ${place.list ? 'list' : 'default'}`;
}

/**
 * Where an account runs, said at a sign-in's end and by `profile join` (ACCT1): each list and default that holds it, or
 * that none does — with the lists it could join and the command that joins them. Where the machine's scope names nothing,
 * none does and none names another, so Daoris takes its accounts in turn (OWNSPACE1d, D175; the modules' `PlacesSaid` says
 * it alike); only where it names another does the account run nowhere.
 */
export function placeLines(harness: string, account: string, settings: HarnessSettings, names: Record<string, string>): string[] {
  const places = placesOf(settings, harness, account);
  const shown = shownAs(names, account);
  if (places.length > 0) return [`  \`${shown}\` runs work in ${places.map(placeWords).join(', ')}.`];

  const lists = [
    ...(settings.rotation[harness] ? [`this machine's (${settings.rotation[harness]!.map((id) => shownAs(names, id)).join(', ')})`] : []),
    ...Object.entries(settings.workspaceRotation)
      .filter(([, orders]) => orders[harness])
      .map(([workspace, orders]) => `\`${workspace}\`'s own (${orders[harness]!.map((id) => shownAs(names, id)).join(', ')})`),
  ];
  const machine = resolveScope(settings, harness, null);
  if (machine.default === null && machine.list.length === 0) {
    return [
      `  no list and no default names \`${shown}\`, and none names another, so Daoris takes its accounts in turn.`,
      ...(lists.length > 0 ? [`  The lists here: ${lists.join('; ')}.`] : []),
    ];
  }
  return [
    `  no list and no default holds \`${shown}\`, so no start runs on it.`,
    ...(lists.length > 0 ? [`  The lists here: ${lists.join('; ')}.`] : []),
    `  \`daoris agent profile join ${harness} ${accountWord(shown, account)} <workspace>…|--machine\` puts it in one.`,
  ];
}

/**
 * The lists a sign-in is asked to join (ACCT1): each `--join <workspace>`, repeated or comma-separated, in the order named,
 * and `--join-machine` for this machine's list (null). A `--join` with no workspace after it is refused.
 */
export function joinsOf(argv: readonly string[]): (string | null)[] {
  const joins: (string | null)[] = [];
  for (let at = 0; at < argv.length; at += 1) {
    if (argv[at] === '--join-machine') joins.push(null);
    if (argv[at] !== '--join') continue;
    const value = argv[at + 1];
    if (!value || value.startsWith('--')) {
      throw new DaorisError('`--join` needs a workspace — e.g. `--join work`, or `--join-machine` for this machine\'s list.');
    }
    joins.push(...value.split(',').map((each) => each.trim()).filter((each) => each.length > 0).map(normalizeWorkspace));
    at += 1;
  }
  return joins.filter((join, index) => joins.indexOf(join) === index);
}

/**
 * The lists a sign-in or `profile join` was asked to join, each refused before anything starts where it cannot be joined
 * (a workspace that takes this machine's list, `joinRefusal`), so a refusal costs nothing.
 */
function refuseJoins(settings: HarnessSettings, harness: string, joins: (string | null)[]): void {
  for (const workspace of joins) {
    const problem = joinProblem(settings, harness, workspace);
    if (problem !== null) throw new DaorisError(joinRefusal(harness, problem.workspace));
  }
}

/** The lists joined, in the order named, written once; the wiring as it now stands. */
function joined(path: string, settings: HarnessSettings, harness: string, account: string, joins: (string | null)[]): HarnessSettings {
  if (joins.length === 0) return settings;
  const after = joinedSettings(settings, harness, account, joins);
  writeHarnessSettings(path, after);
  return after;
}

/** The wiring with the lists joined, in the order named, written nowhere: what `joined` writes, and what a sent join says. */
function joinedSettings(settings: HarnessSettings, harness: string, account: string, joins: (string | null)[]): HarnessSettings {
  return joins.reduce((next, workspace) => withJoined(next, harness, account, workspace), settings);
}

/** A route's answer read for its fields, an answer that is no object read as none. */
function fieldsOf(answer: unknown): Record<string, unknown> {
  return typeof answer === 'object' && answer !== null && !Array.isArray(answer) ? answer as Record<string, unknown> : {};
}

/** `joined` at a sign-in's end, where the sign-in stands whatever the write does: a list not written is said. */
function joinedAfterSignIn(
  path: string, settings: HarnessSettings, harness: string, account: string, joins: (string | null)[],
  write: (line: string) => void,
): HarnessSettings {
  try {
    return joined(path, settings, harness, account, joins);
  } catch (error) {
    if (!(error instanceof DaorisError)) throw error;
    write(`daoris: the account was kept, and joined no list — ${error.message}`);
    return settings;
  }
}

/** A wiring file's settings, or none where there is no file to read. */
function settingsAt(path: string | null | undefined): HarnessSettings {
  return path ? readHarnessSettings(path) : readHarnessSettings('');
}

/**
 * Sign in to a new account (D66 §3): the tool's own login flow into a folder of a fresh id (ACCT2), kept only when the
 * sign-in finished — the tool exited 0 and does not call that home signed out. At its end it is named, where the person
 * named it, joins the lists the person named, and says where it runs (ACCT1); where the person named it nothing, who
 * signed in is offered as its name, written only by the person's rename (D66 §3).
 *
 * @remarks
 * The spawn is a parameter so the judgement around it is testable with no account: `run` is the
 * relay in the verb, and a fixture in the tests. A sign-in that did not finish leaves nothing behind. A join or a name
 * that cannot be kept is refused before anything starts.
 */
export function signInNew(
  harness: string, toolchain: Toolchain, home: string,
  run: (where: string) => ExitCode, write: (line: string) => void, options: SignInOptions = {},
): ExitCode {
  const joins = options.joins ?? [];
  const before = settingsAt(options.path);
  refuseJoins(before, harness, joins);
  const id = nextAccount(home, harness);
  const wanted = options.name?.trim() || null;
  if (wanted !== null) {
    const problem = nameProblem([...profiles(home, harness), id], accountNames(home, harness), id, wanted);
    if (problem !== null) {
      throw new DaorisError(problem.kind === 'taken'
        ? `\`${wanted}\` already names \`${problem.other}\` — each \`${harness}\` account has a name of its own, so nothing was signed in — ${takenWayOut(wanted)}`
        : `\`${wanted}\` is not a name a terminal can type — a name is one word, with no space or backtick, not starting with a dash, at most 64 characters; nothing was signed in.`);
    }
  }

  const where = profileHome(home, harness, id);
  // Opened for the sign-in, and gone again below unless the sign-in finished.
  mkdirSync(where, { recursive: true });
  write(`daoris: signing in to a new \`${harness}\` account, with its own login flow.`);
  write(`  ${where}`);
  write('  Daoris chose the directory and nothing else: whatever you sign in with is stored by the');
  write('  agent, in its own store, under your OS account — Daoris never sees it.');

  let code: ExitCode = 2;
  try {
    code = run(where);
  } finally {
    // Asked of the binary the login ran, so the answer is about the sign-in that just happened.
    const binary = options.binary ?? { command: toolchain.binary, managed: false };
    const said = code === 0
      ? loginAt(binary.command, toolchain, where, binary.managed, {}, probeLockPath(home, harness, id))
      : { login: 'out' as const, account: null, asked: false };

    if (code !== 0 || said.login === 'out') {
      removeProfile(home, harness, id);
      write('daoris: nothing was signed in, so nothing was kept — the account opened for it is gone again.');
    } else {
      // A sign-in into an account ends its cool-off (D125 §2.3), as the driver's `LoginAsync` ends one: the directory
      // may hold another account now. And it is marked (TOOL6g), as the driver's `LoginAsync` marks one.
      endCooling(home, harness, id, new Date());
      markSignedIn(home, harness, id, new Date());
      // A sign-in's end reads its account (ROSTER1), and the reading is kept as the screen's sign-in keeps its own.
      if (said.asked) keepReading(home, harness, id, said.login);
      let called: string | null = null;
      if (wanted !== null) {
        try {
          called = renameAccount(home, harness, profiles(home, harness), id, wanted);
        } catch (error) {
          // The sign-in stands; the name is the person's to give again.
          if (!(error instanceof AccountNameError)) throw error;
          write(`daoris: the account was kept, and not named — ${error.message}`);
        }
      }
      const settings = options.path ? joinedAfterSignIn(options.path, before, harness, id, joins, write) : before;
      // ACCTNAME1: the email offered as the name is said once.
      // ACCOUNTID1c: where the email is another account's, the organisation (else the plan) is said after it.
      const apart = apartWords(profiles(home, harness), accountNames(home, harness), id, said.account, said.org ?? null, said.plan ?? null);
      write(`daoris: ${signedInSentence(said.account, harness, called, id, apart)}`);
      // ACCOUNTID1: the email another account already names is offered with its organisation beside it, or none.
      const offered = offeredName(profiles(home, harness), accountNames(home, harness), id, said.account, said.org ?? null, said.plan ?? null);
      for (const line of endLines(harness, id, called, offered, settings, home, Boolean(options.path))) write(line);
    }
  }

  return code === 0 ? 0 : 2;
}

/**
 * Sign back in to an account that is here (ACCT1): the one named, by its id or its name, else the machine's default —
 * never a new folder. The install's owner signed in to bring a signed-out account back and got a new account no list
 * held, so the work kept starting on the empty one. At its end it joins the lists the person named and says where it runs.
 */
export function signInTo(
  harness: string, toolchain: Toolchain, home: string, profile: string | null,
  run: (where: string) => ExitCode, write: (line: string) => void, options: SignInOptions = {},
): ExitCode {
  const accounts = profiles(home, harness);
  const names = accountNames(home, harness);
  const before = settingsAt(options.path);
  const target = signInTarget(accounts, names, before.defaults[harness], profile);
  if (target.account === null) throw new DaorisError(signInRefusal(harness, target, accounts, names));
  const account = target.account;
  const joins = options.joins ?? [];
  refuseJoins(before, harness, joins);

  const where = profileHome(home, harness, account);
  write(`daoris: running \`${harness}\`'s own login flow into the account \`${shownAs(names, account)}\`.`);
  write(`  ${where}`);
  write('  Daoris chose the directory and nothing else: whatever you sign in with is stored by');
  write('  the agent, in its own store, under your OS account — Daoris never sees it.');
  const signed = run(where);
  // A sign-in that finished ends that account's cool-off (D125 §2.3): the directory may hold another account now. And
  // it is marked (TOOL6g), so the desktop asks the account again at its next look rather than believing it signed out.
  if (signed === 0) {
    endCooling(home, harness, account, new Date());
    markSignedIn(home, harness, account, new Date());
    const settings = options.path ? joinedAfterSignIn(options.path, before, harness, account, joins, write) : before;
    const named = nameFor(names, account);
    // A sign-in's end reads its account (ROSTER1) and keeps the reading (AGENTREAD1), so an account the loop read signed out
    // reads signed in on the screen too; who signed in is offered as a name only where the account has none (D66 §3).
    const binary = options.binary ?? { command: toolchain.binary, managed: false };
    const said = loginAt(binary.command, toolchain, where, binary.managed, {}, probeLockPath(home, harness, account));
    if (said.asked) keepReading(home, harness, account, said.login);
    // ACCOUNTID1: the email another account already names is offered with its organisation beside it, or none.
    const offered = named === null
      ? offeredName(accounts, names, account, said.account, said.org ?? null, said.plan ?? null)
      : null;
    for (const line of endLines(harness, account, named, offered, settings, home, Boolean(options.path))) write(line);
  }
  return signed;
}

/**
 * What a sign-in's end says beside who signed in (ACCT1, ACCT2): where the account runs — each list and default that holds
 * it, or that none does — and, where it has no name of the person's, the name offered for it (`offeredName`: who signed in,
 * or with the organisation beside it where another account has the email, ACCOUNTID1) as a command the person runs to keep
 * it, or a name of theirs asked for where none is offered. Nothing of who signed in is written here.
 */
function endLines(
  harness: string, account: string, named: string | null, offered: string | null, settings: HarnessSettings, home: string,
  placed: boolean,
): string[] {
  const lines: string[] = [];
  if (named === null && offered) {
    lines.push(`  \`daoris agent profile rename ${harness} ${shellWord(account, '<account>')} ${shellWord(offered, '<name>')}\` names it `
      + `${offered}; it reads as \`${account}\` until it has a name.`);
  } else if (named === null) {
    lines.push(`  \`daoris agent profile rename ${harness} ${shellWord(account, '<account>')} <name>\` gives it a name of yours.`);
  }
  if (placed) lines.push(...placeLines(harness, account, settings, accountNames(home, harness)));
  return lines;
}

/** The switch's values as the page names them (`harness.use.switch.*`), for the terminal to say the same. */
const SWITCH_WORDS: Record<RotationUse['switch'], string> = { smart: 'Smart', curve: 'My own curve', off: 'Off' };

/**
 * How a scope's list is used, in the terminal's words (TOOL6a; D130 §3.2, §6, §16.6): each setting's label and value,
 * and a note for each thing the person should know — a default outside the list, a kept account that leaves driven work
 * none, and every value or setting this build does not know, said rather than dropped. `agent list` and `profile use`
 * both print it.
 *
 * @remarks
 * 🔴 It states each setting as chosen, and claims nothing about the walk that reads it: *make the most of them* is §16.3's
 * walk, the driver's, whose steps `profile use` names (`nextStartLines`). *Switch accounts* says what passes an
 * account at a start (D165 point 2, the switch design §3, SWITCH1b-iii): its limit reached, usage credits, or its use over
 * its curve, the scope's own under `curve` and Daoris's smart curve otherwise, `off` included. Only where the agent says how
 * much of each window is used, on its door or asked of its own server (`windows`, TOOL6c, CODEXUSE1b); for any other agent
 * it passes none, and that is said.
 *
 * @param says Whether the agent says how much of each window is used (the toolchain's `windows`).
 */
export function useLines(
  scope: Scope, product: string, says = false, shown: (account: string) => string = (account) => account,
): { rows: [label: string, value: string][]; notes: string[] } {
  const { use } = scope;
  const mode = USE_WORDS[use.use];
  const rows: [string, string][] = [
    ['use accounts', `${mode.name}${mode.cost ? ` — ${mode.cost}` : ''}`],
    ['kept for conversations', use.keep === null ? 'none' : shown(use.keep)],
    // What passes an account at a start since SWITCH1b-ii's walk (the switch design §3): the words say what it does today,
    // never the agent's own pace warning (USAGEREAD3a) or one fixed percentage.
    // SWITCHCLI1: the page's words (D165's SWITCH1d-c note): *Switch accounts*, with Smart, My own curve and Off, and the move
    // a session makes at a turn's end.
    ['switch accounts', !says
      ? `${SWITCH_WORDS[use.switch]} — ${product}'s sessions here do not say how much of each window is used, so a start passes no account by its use`
      : `${SWITCH_WORDS[use.switch]} — ${use.switch === 'off' ? 'no session moves at a turn\'s end; ' : 'a session moves at a turn\'s end when its use is over the curve; '}`
        + `a start ${use.switch === 'off' ? 'still ' : ''}passes an account whose limit is reached, that draws on usage `
        + `credits, or whose use is over ${use.switch === 'curve' ? 'your curve, Daoris\'s smart curve on a window yours leaves out' : 'Daoris\'s smart curve'}`],
  ];
  const curve = curveLine(use);
  if (curve !== null) rows.push(['curve', curve]);

  const notes: string[] = [];
  const problem = scopeProblem({ default: scope.default, list: scope.list, keep: use.keep });
  if (problem?.kind === 'default') {
    notes.push(`its default, \`${shown(problem.account)}\`, is not in this list: name it in the list, or make one of the list the default`);
  } else if (problem?.kind === 'alone') {
    notes.push(`\`${shown(problem.account)}\` is kept for conversations, and this list holds no other account for driven work`);
  }
  for (const name of scope.unknown) {
    notes.push((USE_FIELDS as readonly string[]).includes(name)
      ? `\`${name}\` holds a value this build does not know, so it reads as today's default; it is kept as written`
      : (OLD_FIELDS as readonly string[]).includes(name)
        ? `\`${name}\` is an old name of the switch, and holds a value this build does not read: nothing reads it, and the `
          + 'next write takes it out'
        : `\`${name}\` is a setting this build does not know: nothing here reads it, and it is kept as written`);
  }
  return { rows, notes };
}

/**
 * The person's curve in the terminal's words (D165 point 2): each window it names, its points as `elapsed:used`, and where
 * the switch is not `curve`, that it is kept and read only there. Null where there is no curve to show: none set, under a
 * switch other than `curve`.
 */
function curveLine(use: RotationUse): string | null {
  const windows = CURVE_WINDOWS.filter((window) => use.curve[window] !== undefined);
  if (windows.length === 0) return use.switch === 'curve' ? 'none of your own — each window takes Daoris\'s smart curve' : null;
  const said = windows
    .map((window) => `${window} ${use.curve[window]!.map(([elapsed, used]) => `${elapsed}:${used}`).join(', ')}`)
    .join('; ');
  return use.switch === 'curve' ? said : `${said} — kept, and read only under curve`;
}

/**
 * The step the next start of a scope would follow (TOOL6b, TOOL6c; D130 §16.3, §16.4, §16.6), in the terminal's words: the
 * goal's steps as the driver's walk takes them, or the list's order under `order`, and the kept account driven work passes.
 * Where the agent says how much of each window is used (`windows`), the curve's steps (D165 point 2, the switch design §3,
 * SWITCH1b-iii): an account whose limit is reached, that draws on usage credits or whose use is over its curve goes last,
 * under every switch and either use, the least over first under the goal; the goal's step 4 is what would lapse, within a
 * week's last day its room under the curve per hour to its reset (D165's USAGERESET1 note), said only where the agent says
 * its use, since nothing unread ranks there; step 5 is the most room under its curve, and under `off`, which reads the smart
 * curve for steps 2 and 4, D130's pace. With the goal and no account of the list having said anything, it says so.
 *
 * @remarks
 * Prose about the driver's walk (`AccountRotation.Order`), not a twin of a file: this command is offline and reads no
 * session record, so it names the steps rather than the account they would choose.
 *
 * @param says Whether the agent says how much of each window is used (the toolchain's `windows`).
 * @param anySaid Whether any account of the list has said what it has left (`windows.json`).
 */
export function nextStartLines(
  scope: Scope, says = false, anySaid = false, shown: (account: string) => string = (account) => account,
): { row: string; note: string | null } {
  if (scope.list.length === 1) return { row: `\`${shown(scope.list[0]!)}\`, the one account this list holds`, note: null };

  const begins = scope.begins === null ? null : shown(scope.begins);
  const from = `from \`${begins}\`${scope.default !== null && scope.default === scope.begins ? ', its default' : ''}`;
  const keep = scope.use.keep;
  const passes = keep !== null && scopeProblem({ default: null, list: scope.list, keep }) === null
    ? `; driven work passes \`${shown(keep)}\`, kept for conversations`
    : '';
  const off = scope.use.switch === 'off';
  const last = says
    ? `; one whose limit is reached, that draws on usage credits or whose use is over ${off ? 'Daoris\'s smart curve' : 'its curve'} goes last`
    : '';
  if (scope.use.use !== 'goal') {
    return { row: `the first ready account of this list, ${from}${passes}${last}`, note: null };
  }

  // Step 4 weighs what would lapse (D165's USAGERESET1 note): only a read week's room, so an agent that says nothing has no
  // such step, and a reset a limit told ranks nothing.
  const lapses = !says ? '' : 'then the one whose week would lapse the most: within its last day, its room under '
    + `${off ? 'Daoris\'s smart curve' : 'its curve'} per hour to its reset; `;
  const room = !says ? '' : off ? 'then the one furthest behind its week\'s pace; ' : 'then the one with the most room under its curve; ';
  return {
    row: `the ready account running the fewest of Daoris's sessions; ${lapses}`
      + `${room}then the one Daoris started on least recently; then this list's order, ${from}${passes}`
      + `${last}${says ? ', the least over first' : ''}`,
    note: anySaid
      ? null
      : 'no account has said what it has left yet: Daoris spreads starts across them by its own sessions',
  };
}

/**
 * What `agent list` says beneath a scope about its next start (TOOL6f; D130's TOOL6e note, §16.4): the walk's steps as
 * `profile use` names them, each account of the scope cooling now and until when, the wait where every account a start may
 * take is cooling (driven work's alone where the kept account is ready), and where the account it takes is named. A scope
 * with no list is its default alone, which the settings name and no step chooses.
 *
 * @remarks
 * 🔴 It never names the account a step would choose. Fewest running and least recently started read Daoris's session
 * records, which this command does not read, so a walk here without them would not be the driver's (D57: absent is never
 * zero). The agent's page asks the driver's own judgement (`AccountRotation.Next`), and no `daoris-driver` verb answers
 * it, so the pointer names the screen alone. A hold the roster alone knows (refused, signed out by the agent's last word)
 * is not said here; each account's own line says what its probe found.
 *
 * @param owner The agent whose accounts these are (AGT7).
 */
export function nextStartBeneath(
  scope: Scope, owner: string, home: string, now: Date, zone: string,
  shown: (account: string) => string = (account) => account,
): string[] {
  const uses = scope.list.length > 0 ? scope.list : scope.default !== null ? [scope.default] : [];
  const lines: string[] = [];
  if (scope.list.length > 0) {
    const anySaid = scope.list.some((account) => saidOf(home, owner, account, now) !== null);
    const next = nextStartLines(scope, TOOLCHAINS[owner]?.windows === true, anySaid, shown);
    lines.push(`next start: ${next.row}`);
    if (next.note) lines.push(next.note);
  }

  // Each account as the list spells it, with its cool-off's end where `cooling.json` holds one (names compare without case).
  const held = uses.flatMap((account) => {
    const entry = coolingOf(home, owner, account, now);
    return entry ? [{ account, until: entry.until }] : [];
  });
  const soonest = (of: { until: Date }[]) =>
    coolingWhen(new Date(Math.min(...of.map((entry) => entry.until.getTime()))), zone);
  if (held.length > 0) {
    const each = held.map((entry) => `\`${shown(entry.account)}\` is cooling until ${coolingWhen(entry.until, zone)}`).join('; ');
    // Driven work passes a kept account that leaves it another (§4.6), so it waits once every other account cools.
    const keep = scope.use.keep !== null && scopeProblem({ default: null, list: scope.list, keep: scope.use.keep }) === null
      ? scope.use.keep
      : null;
    const driven = held.filter((entry) => entry.account !== keep);
    if (uses.length === 1) {
      lines.push(`held now: ${each}, so the next start waits until then`);
    } else {
      lines.push(`held now: ${each}`);
      if (held.length === uses.length) {
        lines.push(`every account of this list is cooling, so the next start waits until ${soonest(held)}`);
      } else if (keep !== null && driven.length === uses.length - 1) {
        lines.push(`every account but \`${shown(keep)}\`, kept for conversations, is cooling, so driven work waits until ${soonest(driven)}`);
      }
    }
  }

  // UX6e2: the screen that names it is the agent's page in the Agents place since UX6e (D150 §5.2).
  if (scope.list.length > 1) {
    lines.push('Agents → the agent\'s page → How accounts are used names the account it takes, from the sessions Daoris '
      + 'runs and its last starts, which this terminal does not read');
  }
  return lines;
}

type ProfileListed = HarnessReport['profiles'][number];

/**
 * Who signed in, as `agent list` says it after an account's name and state (D66 §3): dropped where it is the name already
 * (ACCTNAME1, as the page's `accountWho` does); and where another account signed in with the same email (ACCOUNTID1), what
 * tells this one apart after it (`apartBy`), each said once, so a name that is the email with it says neither again.
 */
function whoWords(profile: ProfileListed, name: string, profiles: readonly ProfileListed[]): string {
  const apart = apartBy(profile, profiles);
  const said = apart !== null && profile.account ? nameBeside(profile.account, apart) : null;
  const named = said !== null && sameName(said, name);
  const words = [
    profile.account && !sameName(profile.account, name) && !named ? profile.account : null,
    apart !== null && !named ? apart : null,
  ].filter((word): word is string => word !== null);
  return words.length > 0 ? ` ${words.join(' · ')}` : '';
}

/**
 * What tells an account apart from another signed in with its email (ACCOUNTID1): its organisation, where no such other
 * account has it, else its plan likewise; null where no other account has its email, or where neither tells them apart.
 */
function apartBy(profile: ProfileListed, profiles: readonly ProfileListed[]): string | null {
  const email = profile.account;
  if (!email) return null;
  const twins = profiles.filter((other) => other !== profile && other.account && sameName(other.account, email));
  if (twins.length === 0) return null;
  for (const part of ['org', 'plan'] as const) {
    const mine = profile[part];
    if (mine && twins.every((other) => !other[part] || !sameName(other[part], mine))) return mine;
  }
  return null;
}

/** An agent with no Daoris account (OWNSPACE1d, D175): its starts wait for a sign-in, never run in the tool's own home. */
function noAccountsWords(agent: string): string {
  return `no accounts — Daoris's sessions wait until you sign in: \`daoris agent login ${agent} --new\``;
}

/**
 * What `agent list` says under one agent about its accounts (D49 §4, D66 §3, TOOL4e): each account and what marks it,
 * each one's cool-off under it, the tool's own sign-in's cool-off and what it last said of its windows (CODEXUSE3), the
 * order rotation may use with how it is used and its next start (`nextStartBeneath`, TOOL6f), and — where a start would
 * run on the person's own sign-in — that it does, and how to give Daoris an account of its own (D125 §2.4, §3.7).
 *
 * @remarks
 * A door's accounts, defaults, orders and cool-offs are its owner's (twin rule 7). The own sign-in's line is said only for
 * an agent that is here and signs in by its own flow: an agent that is not installed starts nothing.
 */
export function accountLines(
  name: string, toolchain: Toolchain, report: HarnessReport, settings: HarnessSettings, home: string, now: Date, zone: string,
): string[] {
  const indent = `  ${''.padEnd(14)} `;
  const owner = toolchain.accountOf ?? name;
  const lines: string[] = [];
  // Each account by the person's name for it, else its id (ACCT2); the id is said beside a name, as records name it.
  const names = accountNames(home, owner);
  const shown = (account: string) => shownAs(names, account);
  // ACCTNAME1: the name column fits the longest name shown (floor 16, so short lists keep their look).
  const col = Math.max(16, ...report.profiles.map((profile) => shown(profile.name).length));
  const gap = ''.padEnd(col);

  // OWNSPACE1d (D175): Daoris starts nothing in the tool's own home, so with no account its starts wait for a sign-in.
  if (report.profiles.length === 0) lines.push(`${indent}${noAccountsWords(owner)}`);

  for (const profile of report.profiles) {
    const marks = [
      shown(profile.name) !== profile.name ? `id ${profile.name}` : null,
      report.machineDefault === profile.name ? 'machine default' : null,
      ...Object.entries(settings.workspaces)
        .filter(([, map]) => map[owner] === profile.name)
        .map(([circle]) => `default in ${circle}`),
    ].filter(Boolean);

    // 🔴 A key account is never "in": the tool says so for any key, a wrong one included (AGT3).
    lines.push(
      `${indent}${shown(profile.name).padEnd(col)} ${(profile.key ? 'unchecked' : profile.login).padEnd(9)}`
      + `${whoWords(profile, shown(profile.name), report.profiles)}`
      + `${profile.key ? ` API key ${profile.key}` : ''}`
      + `${marks.length ? ` (${marks.join(', ')})` : ''}`);

    const cooled = coolingOf(home, owner, profile.name, now);
    if (cooled) lines.push(`${indent}${gap} ${coolingLine(cooled, now, zone)}`);
    // What its agent last said about its windows, and how long ago (TOOL6c, D130 §3.2): a reading, never a judgement.
    const said = saidOf(home, owner, profile.name, now);
    if (said) lines.push(`${indent}${gap} ${saidLine(said, now, zone)}`);
    // ACCT1: an account no list and no default holds runs nowhere, and says so where it is listed — the install's new
    // account sat in no list while the work kept starting on an empty one. Where the machine names nothing, Daoris takes its
    // accounts in turn, so this one runs (OWNSPACE1d, D175).
    const machineScope = resolveScope(settings, owner, null);
    if (placesOf(settings, owner, profile.name).length === 0 && (machineScope.default !== null || machineScope.list.length > 0)) {
      lines.push(`${indent}${gap} no workspace: no list and no default holds it, so no start runs on it — `
        + `\`daoris agent profile join ${owner} ${accountWord(shown(profile.name), profile.name)} <workspace>…|--machine\` puts it in one`);
    }
  }

  const ownCooling = coolingOf(home, owner, null, now);
  if (ownCooling) lines.push(`${indent}its own sign-in: ${coolingLine(ownCooling, now, zone)}`);
  // What the tool's own sign-in last said, read by its agent's server at a person's press (CODEXUSE3): a reading beside
  // the accounts, never one of them.
  const ownSaid = saidOf(home, owner, OWN_WINDOWS, now);
  if (ownSaid) lines.push(`${indent}its own sign-in: ${saidLine(ownSaid, now, zone)}`);

  // Each list, how it is used beneath it (TOOL6a, D130 §3.2) and its next start (TOOL6f): the machine's, then each
  // workspace's own. The machine with no list says its next start where its default names the one account it uses.
  const product = TOOLCHAINS[owner]?.product ?? owner;
  const beneath = (scope: Scope) => {
    const { rows, notes } = useLines(scope, product, TOOLCHAINS[owner]?.windows === true, shown);
    for (const [label, value] of rows) lines.push(`${indent}${gap} ${label}: ${value}`);
    for (const note of notes) lines.push(`${indent}${gap} ${note}`);
    for (const line of nextStartBeneath(scope, owner, home, now, zone, shown)) lines.push(`${indent}${gap} ${line}`);
  };
  const order = resolveRotation(settings, owner, null);
  if (order.from === 'machine') {
    lines.push(`${indent}${'rotation'.padEnd(col)} ${order.order.map(shown).join(', then ')}`);
    beneath(resolveScope(settings, owner, null));
  } else {
    const machine = resolveScope(settings, owner, null);
    if (machine.default !== null) {
      lines.push(`${indent}${'next start'.padEnd(col)} \`${shown(machine.default)}\`, this machine's default — with no list, the one account its `
        + 'starts run on');
      for (const line of nextStartBeneath(machine, owner, home, now, zone, shown)) lines.push(`${indent}${gap} ${line}`);
    }
  }
  for (const [circle, orders] of Object.entries(settings.workspaceRotation)) {
    if (!orders[owner]) continue;
    lines.push(`${indent}${`rotation in ${circle}`.padEnd(col)} ${orders[owner].map(shown).join(', then ')}`);
    beneath(resolveScope(settings, owner, circle));
  }

  if (!toolchain.accountOf && toolchain.login && report.present && !report.machineDefault) {
    const named = Object.entries(settings.workspaces).filter(([, map]) => map[owner]).map(([circle]) => circle);
    // OWNSPACE1d (D175): Daoris never starts in it; what began there is read where it is and goes on there.
    lines.push(`${indent}Daoris starts no session on your own sign-in, the account \`${name}\` uses at your own terminal`
      + `${named.length > 0 ? ` (outside ${named.join(', ')})` : ''}:`);
    lines.push(`${indent}a conversation begun there goes on there, and new starts take Daoris's accounts in turn —`);
    lines.push(`${indent}\`daoris agent login ${name} --new\` signs in to one`);
  }

  return lines;
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
 *
 * `door` is where an edit of the home goes (RPC1f): the Agents or Accounts place's route while Daoris runs on this home, which its
 * own handler judges, writes and publishes; this verb's own write under the home's lease where none does. A verb that needs this
 * terminal (an installer, a sign-in, a key on stdin, a pin's download) acts alone, as `door.ts`'s `AGENT_VERBS` says.
 */
export function commandHarness(
  { argv, write }: CommandArgs, releases: Fetcher | null = null, npm: string[] = ['npm'], door: VerbDoor = ALONE,
  ports: PortProbe | null = null,
): ExitCode | Promise<ExitCode> {
  const verb = argv[0] ?? 'list';
  const path = harnessesPath();
  const home = harnessHome(path);
  // The verb as a lease names it: `profile`'s own verb with it, never an argument.
  const what = verb === 'profile' ? `agent profile ${argv[1] ?? 'list'}` : `agent ${verb}`;

  /** An edit through the door: the route's payload, this verb's own write, and its words for either path. */
  const press = (route: InstanceRoute, payload: unknown, edit: () => void, said: (answer?: unknown) => ExitCode) => door({
    what, route, payload,
    alone: () => {
      edit();
      return said();
    },
    words: (answer) => said(answer),
  });

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
      // 🔴 On the system's npm (TOOLS5, D121 §2.7), whatever the tools run node as: an install is the agent's own
      // installer, into the machine. A managed npm's global folder may be its node version's, which the next loses.
      return relay(commandOnTheSystem(toolchain.install), null, toolchain, write);
    }

    // USE1a: update does what it says. A pinned agent with a package or a channel moves its pin to
    // the newest release, since running its updater would change a different copy than the one
    // sessions run. Unpinned, the agent's own updater runs, as it always did.
    case 'update': {
      const { name, toolchain } = required(argv, 'update');
      const workspace = flagValue(argv, '--workspace');
      const circle = workspace?.trim() ? normalizeWorkspace(workspace) : null;
      const settings = readHarnessSettings(path);
      const pin = (circle ? atName(settings.workspaceVersions, circle)?.[name] : settings.versions[name])?.trim() || null;

      if (pin && (toolchain.package || toolchain.channel)) return updatePin(name, toolchain, pin, circle);
      if (circle) {
        throw new DaorisError(
          `the \`${circle}\` workspace pins no version of \`${name}\`, so there is no pin to move — `
          + `\`daoris agent pin ${name} <version>${scoped(circle)}\` sets one.`);
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
      if (!toolchain.login) {
        throw new DaorisError(
          `\`${name}\` declares no login flow — log in with its own tooling, pointing `
          + `${toolchain.profileVariable} at the profile directory.`);
      }
      // CODEXLOGIN1: the tool's own sign-in first, its device code where asked; the driver's `SignInWays.Arguments` words it.
      const byCode = argv.includes('--device-code');
      if (byCode && !toolchain.deviceLogin) {
        throw new DaorisError(
          `\`${name}\` signs in by no device code, so nothing was signed in — its own sign-in is its one way.`);
      }
      const login = byCode ? toolchain.deviceLogin! : toolchain.login;

      // ACCT1: the lists a sign-in joins at its end, `--join <workspace>` (repeated, or comma-separated) and
      // `--join-machine`, refused before anything starts where one cannot be joined.
      const joins = joinsOf(argv);
      if (argv.includes('--new') && argv.includes('--profile')) {
        throw new DaorisError(
          `\`--new\` signs in to a new account and \`--profile\` to one that is here — \`daoris agent login ${name} --new\`, or `
          + `\`daoris agent login ${name} --profile ${shellWord(flagValue(argv, '--profile') ?? '', '<account>')}\`.`);
      }
      if (!argv.includes('--new') && argv.includes('--name')) {
        throw new DaorisError(
          `\`--name\` names a new account as it is made (\`--new\`); an account that is here is named with \`daoris agent `
          + `profile rename ${name} <account> <name>\`.`);
      }

      // CODEXACCT1b: the binary `agent list` asks, the pin before PATH's, refused before anything is made where the pin
      // has nothing installed; the sign-in and its end's question both run it.
      const binary = signInBinary(name, toolchain, home, readHarnessSettings(path));
      const relayed = (where: string) => relay([...binary.command, ...login], where, toolchain, write, binary.managed);
      // The same verb by the other way, said where one way did not sign in (CODEXLOGIN1): a workspace may refuse either.
      const typed = ['daoris agent login', name, ...argv.slice(2).filter((word) => word !== '--device-code')].join(' ');
      const otherWay = !toolchain.deviceLogin ? null : byCode
        ? `\`${typed}\` signs in through your browser instead.`
        : `\`${typed} --device-code\` signs in with a device code instead.`;

      const signIn = (): ExitCode => {
        // A new account (D66 §3): made by the sign-in, offered who signed in as its name (ACCT2) — the desktop's
        // *Add an account…*, from a terminal (D50). An account that is here, by its id or its name, else the machine's
        // default: never a new folder (ACCT1).
        const code = argv.includes('--new')
          ? signInNew(name, toolchain, home, relayed, write, { name: flagValue(argv, '--name') ?? null, joins, path, binary })
          : signInTo(name, toolchain, home, flagValue(argv, '--profile') ?? null, relayed, write, { joins, path, binary });
        if (code !== 0 && otherWay) write(`daoris: ${otherWay}`);
        return code;
      };

      // The browser's ports, each tried once before anything is made, where this verb was handed a way to try them
      // (`agent.ts` hands it `service.ts`' own): where none can be taken, refused in the driver's words.
      if (byCode || !toolchain.loginPorts || !ports) return signIn();
      return heldPorts(toolchain.loginPorts, ports).then((held) => {
        if (held === null) return signIn();
        throw new DaorisError(`${portsHeld(name, toolchain.login!, held)} Nothing was signed in — ${otherWay
          ?? 'close what holds it, or restart, and try again.'}`);
      });
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
      const said = (): ExitCode => {
        write(workspace
          ? `daoris: the \`${workspace}\` workspace no longer pins \`${name}\` on this machine.`
          : `daoris: \`${name}\` runs from PATH again on this machine.`);
        write('  Nothing was deleted — the managed install stays where it is, and re-pinning that');
        write('  version needs no download.');
        return 0;
      };
      const unpinned = () => pinTo(name, null, workspace);
      // HARNESS_ACTION's unpin is the machine's pin alone: a workspace's is this verb's.
      return workspace
        ? door({ what, route: null, alone: () => { unpinned(); return said(); }, words: said })
        : press(AGENT_ROUTES.harness, { action: 'unpin', harness: name }, unpinned, said);
    }

    case 'profile':
      return profileVerb();

    // An account that is an API key (AGT3, D67 §1). 🔴 Read from STDIN, never from an argument: an
    // argument is visible in the process list and saved in the shell's history.
    case 'key': {
      const { name } = required(argv, 'key');
      // `--account` names the account whose key is replaced (KEYREPLACE1a); its value is an operand to nothing else.
      const replacing = flagValue(argv, '--account');
      const extra = operands(argv, AGENT_VALUED).slice(2);
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
      if (replacing === undefined) addKeyAccount(home, name, raw, write);
      else replaceKeyAccount(home, name, replacing, raw, write);
      return 0;
    }

    default:
      throw new DaorisError(
        `unknown agent verb '${verb}' — one of: list, install, update, login, key, pin, unpin, profile, settings, trust, rules`);
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
    // The npm the tools resolve (TOOLS5, D121 §2.7): its prefix is named, so what it installs runs on the tools' node.
    const installed = relay(throughTools([...npm, 'install', '--prefix', where, `${toolchain.package}@${version}`]), null, toolchain, write);
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
    const stays = `Nothing was fetched or pinned, and the pin stays at ${pin}.`;
    let command: string[];
    try {
      // The npm the tools resolve (TOOLS5, D121 §2.7), as the pin it would make runs it.
      command = throughTools([...npm, 'view', pkg, 'version']);
    } catch (error) {
      if (!(error instanceof DaorisError)) throw error;
      throw new DaorisError(`${error.message} ${stays}`, error.exitCode);
    }
    write(`  $ ${command.join(' ')}`);
    const result = startChild(command, { ...process.env }, { stdio: ['ignore', 'pipe', 'pipe'] });

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
      // CASEFOLD1c: under the spelling the workspace was first written in, as the driver's `WithWorkspaceVersion` keeps it.
      const key = findName(Object.keys(settings.workspaceVersions), circle) ?? circle;
      const held = { ...settings.workspaceVersions[key] };
      if (version) held[name] = version;
      else delete held[name];
      writeHarnessSettings(path, {
        ...settings,
        workspaceVersions: { ...settings.workspaceVersions, [key]: held },
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

      // One account list per tool (TOOL6g): a door onto another agent's accounts lists them nowhere of its own, since its
      // owner listed them above with what the agent said of each, and naming them twice read as two sets of accounts.
      const owner = toolchain.accountOf ? TOOLCHAINS[toolchain.accountOf] : undefined;
      if (toolchain.accountOf && owner) {
        const product = owner.product ?? toolchain.accountOf;
        write(`  ${''.padEnd(14)} its accounts are ${product}'s, listed under \`${toolchain.accountOf}\`: this is another way `
          + `${product} runs on them`);
        continue;
      }
      for (const line of accountLines(name, toolchain, report, settings, home, new Date(), machineZone())) write(line);
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
        write(`  ${''.padEnd(14)} ${existing.length > 0 ? `accounts: ${existing.join(', ')}` : noAccountsWords(harness.name)}`);
      }
    }

    write('');
    write('  An account is a directory Daoris owns the location of. The credential inside it belongs to');
    write("  the agent's own store. An API key is the one secret Daoris keeps: `agent key`, in keys.json.");
    return 0;
  }

  function profileVerb(): ExitCode | Promise<ExitCode> {
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
        return press(AGENT_ROUTES.harness, { action: 'profile-add', harness: name, profile }, () => mkdirSync(where, { recursive: true }), () => {
          write(existed
            ? `daoris: \`${name}\` profile \`${profile}\` already exists — ${where}`
            : `daoris: \`${name}\` profile \`${profile}\` — ${where}`);
          write(`  It is empty until you log into it: \`daoris agent login ${name} --profile ${shellWord(profile, '<account>')}\`.`);
          return 0;
        });
      }

      case 'remove': {
        const name = accountsOf(operand(argv, 2), 'profile remove');
        const profile = accountNamed(name, bare(argv, 3, 'profile remove', '<agent> <profile>'));
        const where = profileHome(home, name, profile);
        const existed = existsSync(where);
        const said = (removed: boolean): ExitCode => {
          write(removed
            ? `daoris: removed the \`${name}\` account \`${profile}\` — the directory and the sign-in in it are gone: ${where}`
            : `daoris: \`${name}\` has no account \`${profile}\` on this machine — there was nothing to remove.`);
          write(`  It is no longer a default for \`${name}\` anywhere on this machine: no default and no order names it.`);
          return 0;
        };

        return door({
          what, route: AGENT_ROUTES.harness, payload: { action: 'profile-remove', harness: name, profile },
          alone: () => {
            // 🔴 The account goes, directory and sign-in both (D66 §3, amending SES3's "deletes
            // nothing"): an account a person removes should not still be signed in on disk. The old
            // rule kept any directory the harness would not call signed out, so a removed account
            // stayed listed and signed in. The desktop's Remove is the same verb.
            const removed = removeProfile(home, name, profile);
            // No default and no order names it afterwards (TOOL4e), as the driver's `WithoutAccount` leaves the wiring.
            writeHarnessSettings(path, withoutAccount(settings, name, profile));
            return said(removed);
          },
          // The instance says a delete that could not finish on its own console, and answers exit 1: said here too.
          words: (answer) => {
            if (fieldsOf(answer).exitCode === 1) {
              throw new DaorisError(`${where} was not removed — a session running as this account may hold a file open in it; stop it `
                + 'and remove it again. Part of it may already be gone.', 1);
            }
            return said(existed);
          },
        });
      }

      // The accounts rotation may use, in order (TOOL4e, D125 §3.1, §6): the machine's, or with `--workspace` one
      // workspace's. An account not listed never rotates, into or away; `--clear` names none again.
      case 'order': {
        const name = accountsOf(operand(argv, 2), 'profile order');
        const flagged = flagValue(argv, '--workspace');
        const workspace = flagged ? normalizeWorkspace(flagged) : null;
        // An account by its id or its name (ACCT2); the list holds ids, so a rename leaves it standing.
        const named = operands(argv, AGENT_VALUED).slice(3).map((given) => accountNamed(name, given));
        if (argv.includes('--clear')) {
          if (named.length > 0) {
            throw new DaorisError(
              `\`--clear\` names no account — \`daoris agent profile order ${name} --clear\` clears the order, and `
              + `\`daoris agent profile order ${name} ${shellWords(named, '<account>')}\` sets it.`);
          }
          return clearOrder(name, workspace);
        }

        if (named.length === 0) {
          throw new DaorisError(
            `\`agent profile order\` needs the accounts, in order, or --clear — e.g. \`daoris agent profile order ${name} `
            + 'account-1 account-2`.');
        }

        // Refused rather than written, as `profile default` refuses a name that does not exist (§3.1): a typo in an
        // order is an account rotation would walk past without saying why.
        const existing = profiles(home, name);
        const problem = rotationProblem(existing, named);
        if (problem !== null) throw new DaorisError(rotationRefusal(name, problem, existing));

        // D130 §3.1, §4.6 (TOOL6a): the list is every account the scope's starts may run on, so it holds the scope's
        // default and its kept account, and leaves driven work another beside the kept one.
        const list = named.map((account) => account.trim());
        const bound = scopeProblem({ default: ownDefault(name, workspace), list, keep: ownKeep(name, workspace) });
        if (bound !== null) throw new DaorisError(orderRefusal(name, workspace, bound, list));

        return press(AGENT_ROUTES.account, { action: 'order', harness: name, accounts: list, ...(workspace ? { workspace } : {}) },
          () => writeHarnessSettings(path, withRotation(settings, name, named, workspace)), () => {
            const spelled = named.map((account) => account.trim()).join(', then ');
            write(workspace
              ? `daoris: in \`${workspace}\`, \`${name}\`'s rotation is ${spelled}.`
              : `daoris: on this machine, \`${name}\`'s rotation is ${spelled}.`);
            write('  An account not in it is never rotated into, and work resolved to it never moves (D125 §3.1).');
            write(`  Written to ${path} — machine-local, tracked by nothing, like every wiring file here.`);
            return 0;
          });
      }

      // The terminal's *Try now* (TOOL4e, D125 §2.3, §6): an account's cool-off ended early, since the person may know
      // its limit was raised. `--own` is the tool's own sign-in.
      case 'ready': {
        const name = accountsOf(operand(argv, 2), 'profile ready');
        const own = argv.includes('--own');
        const given = operand(argv, 3);
        const account = given === undefined ? null : accountNamed(name, given);
        if (own && account !== null) {
          throw new DaorisError(
            `\`--own\` names no account — \`daoris agent profile ready ${name} --own\` is the tool's own sign-in, and `
            + `\`daoris agent profile ready ${name} ${shellWord(account, '<account>')}\` is that account.`);
        }
        if (!own && account === null) {
          throw new DaorisError(
            `\`agent profile ready\` needs <agent> <profile>|--own — e.g. \`daoris agent profile ready ${name} account-1\`, `
            + 'or `--own` for the tool\'s own sign-in.');
        }

        const now = new Date();
        const who = account === null ? `\`${name}\`'s own sign-in` : `the \`${name}\` account \`${account}\``;
        const said = (ended: boolean): ExitCode => {
          if (ended) {
            write(`daoris: ${who} is offered again: no start waits on its limit now.`);
            write('  If the limit still holds, the agent refuses the next start at once, and the account cools again until');
            write('  the time it names.');
            return 0;
          }

          const others = readCooling(home, now).filter((entry) => sameName(entry.agent, name));
          write(`daoris: ${who} is not cooling, so nothing changed — `
            + (others.length > 0
              ? `cooling now: ${others.map((entry) => entry.account ?? 'its own sign-in').join(', ')}.`
              : `no account of \`${name}\` is cooling.`));
          return 0;
        };
        // *Try now* while Daoris runs: the instance's roster ends the cool-off it holds, as well as the file's.
        return door({
          what, route: AGENT_ROUTES.account, payload: { action: 'ready', harness: name, ...(account === null ? { own: true } : { profile: account }) },
          alone: () => said(endCooling(home, name, account, now)),
          words: (answer) => said(fieldsOf(answer).ended === true),
        });
      }

      case 'default': {
        const name = accountsOf(operand(argv, 2), 'profile default');
        const flagged = flagValue(argv, '--workspace');
        const workspace = flagged ? normalizeWorkspace(flagged) : null;
        // D50's second door onto the screen's own clear (LEFT3): its *Make default* on the tool's own row names no
        // account, and so does this.
        if (argv.includes('--clear')) return clearDefault(name, workspace);

        const profile = accountNamed(name, bare(argv, 3, 'profile default', '<agent> <profile>|--clear [--workspace <name>]'));
        // Refused rather than created: naming a default that does not exist is a typo with a silent
        // wrong answer available — every spawn in that circle would refuse, and the message would be
        // about logging in rather than about the name.
        if (!profiles(home, name).includes(profile)) {
          throw new DaorisError(
            `\`${name}\` has no profile \`${profile}\` on this machine — \`daoris agent profile add `
            + `${name} ${shellWord(profile, '<account>')}\` creates it. Profiles that exist: `
            + `${profiles(home, name).join(', ') || '(none)'}`);
        }

        // D130 §3.1 (TOOL6a): a scope's default is where its starts begin within its list, so one the list does not hold
        // is refused. A workspace with no list of its own takes any account, and is then that account alone.
        const list = ownList(name, workspace);
        if (scopeProblem({ default: profile, list, keep: null }) !== null) {
          throw new DaorisError(
            `\`${name}\`'s list ${where(workspace)} is ${list.join(', then ')}, and \`${profile}\` is not in it — the list is `
            + 'every account its starts may run on, and the default is where they begin within it. '
            + `\`daoris agent profile order ${name} ${shellWords([...list, profile], '<account>')}${scoped(workspace)}\` adds it, or make one `
            + 'of the list the default.');
        }

        return press(AGENT_ROUTES.harness, { action: 'profile-default', harness: name, profile, ...(workspace ? { workspace } : {}) },
          () => writeHarnessSettings(path, withDefault(settings, name, profile, workspace)), () => {
            write(workspace
              ? `daoris: sessions in \`${workspace}\` run \`${name}\` as \`${profile}\`.`
              : `daoris: this machine runs \`${name}\` as \`${profile}\` by default.`);
            write(`  Written to ${path} — machine-local, tracked by nothing, like every wiring file here.`);
            return 0;
          });
      }

      // How a scope's list is used (TOOL6a, D130 §2, §9): the machine's, or with `--workspace` one workspace's. Each choice
      // is written as made; with no flag it prints the scope's settings, the step the next start would follow (TOOL6b) and
      // what each account last said.
      case 'use':
        return useVerb();

      // An account's name (ACCT2): the person's word, in `accounts.json` by the account's id, so every list, default,
      // reading and record naming the id keeps working. Naming it its own id gives it none. The screen's twin is
      // `HARNESS_ACTION`'s `profile-rename`.
      case 'rename': {
        const name = accountsOf(operand(argv, 2), 'profile rename');
        const account = accountNamed(name, bare(argv, 3, 'profile rename', '<agent> <account> <name>'));
        const wanted = operand(argv, 4);
        if (wanted === undefined) {
          throw new DaorisError(
            `\`agent profile rename\` needs <agent> <account> <name> — e.g. \`daoris agent profile rename ${name} `
            + `${shellWord(account, '<account>')} work\`; `
            + `naming it \`${account}\` gives it none.`);
        }
        const said = (now: string | null): ExitCode => {
          write(now !== null
            ? `daoris: \`${name}\`'s account \`${account}\` is called \`${now}\` now.`
            : `daoris: \`${name}\`'s account \`${account}\` has no name of yours now: it reads as its id.`);
          write('  Its id stays, so every list, default, reading and record that names it still does.');
          write(`  Written to ${accountsPath(home)} — machine-local, tracked by nothing.`);
          return 0;
        };
        return door({
          what, route: AGENT_ROUTES.harness, payload: { action: 'profile-rename', harness: name, profile: account, name: wanted },
          alone: () => said(renameAccount(home, name, profiles(home, name), account, wanted)),
          words: (answer) => {
            const now = fieldsOf(answer).name;
            return said(typeof now === 'string' && now.length > 0 ? now : null);
          },
        });
      }

      // An account put into lists a start runs on (ACCT1): each workspace's own list named, and with `--machine` this
      // machine's, at its end; a workspace that takes this machine's list is refused, naming it. The screen's twin is
      // `HARNESS_ACTION`'s `profile-join`.
      case 'join': {
        const name = accountsOf(operand(argv, 2), 'profile join');
        const account = accountNamed(name, bare(argv, 3, 'profile join', '<agent> <account> <workspace>…|--machine'));
        const joins: (string | null)[] = [
          ...(argv.includes('--machine') ? [null] : []),
          ...operands(argv, AGENT_VALUED).slice(4).map(normalizeWorkspace),
        ];
        if (!profiles(home, name).includes(account)) {
          throw new DaorisError(
            `\`${name}\` has no account \`${account}\` on this machine — accounts that exist: `
            + `${profiles(home, name).map((id) => shownAs(accountNames(home, name), id)).join(', ') || '(none)'}`);
        }
        if (joins.length === 0) {
          throw new DaorisError(
            `\`agent profile join\` needs the lists — a workspace's name, or \`--machine\` for this machine's: e.g. `
            + `\`daoris agent profile join ${name} ${shellWord(account, '<account>')} work\`.`);
        }
        refuseJoins(settings, name, joins);

        const after = joinedSettings(settings, name, account, joins);
        return press(AGENT_ROUTES.harness, { action: 'profile-join', harness: name, profile: account, join: joins },
          () => writeHarnessSettings(path, after), () => {
            write(`daoris: \`${name}\`'s account \`${shownAs(accountNames(home, name), account)}\` is in `
              + `${joins.map((workspace) => (workspace === null ? 'this machine\'s list' : `\`${workspace}\`'s list`)).join(', ')}.`);
            for (const line of placeLines(name, account, after, accountNames(home, name))) write(line);
            write(`  Written to ${path} — machine-local, tracked by nothing, like every wiring file here.`);
            return 0;
          });
      }

      default:
        throw new DaorisError(
          `unknown agent profile verb '${action}' — one of: list, add, remove, default, order, ready, use, rename, join`);
    }

    /** A scope's own list: the machine's, or the workspace's own — never the machine's standing in for it. */
    function ownList(name: string, workspace: string | null): string[] {
      return (workspace ? atName(settings.workspaceRotation, workspace)?.[name] : settings.rotation[name]) ?? [];
    }

    /** A scope's own default: the machine's, or the workspace's own. */
    function ownDefault(name: string, workspace: string | null): string | null {
      return (workspace ? atName(settings.workspaces, workspace)?.[name] : settings.defaults[name])?.trim() || null;
    }

    /** A scope's kept account, as its settings name it, whether or not its list holds it. */
    function ownKeep(name: string, workspace: string | null): string | null {
      return readUse(workspace ? atName(settings.workspaceRotationUse, workspace)?.[name] : settings.rotationUse[name]).use.keep;
    }

    /** `profile order`'s refusal for a list that leaves out its scope's default or kept account (§3.1, §4.6). */
    function orderRefusal(name: string, workspace: string | null, problem: ScopeProblem, list: string[]): string {
      const named = `\`daoris agent profile order ${name} ${shellWords([...list, problem.account], '<account>')}${scoped(workspace)}\``;
      const noKeep = `\`daoris agent profile use ${name} --no-keep${scoped(workspace)}\``;
      switch (problem.kind) {
        case 'default':
          return `${workspace ? `\`${workspace}\`'s` : 'this machine\'s'} default for \`${name}\` is \`${problem.account}\`, and `
            + 'this list does not hold it — the list is every account its starts may run on, and the default is where they '
            + `begin within it. Name it in the list (${named}), or first make one of the list the default `
            + `(\`daoris agent profile default ${name} ${shellWord(list[0]!, '<account>')}${scoped(workspace)}\`).`;
        case 'keep':
          return `\`${problem.account}\` is kept for conversations ${where(workspace)}, and this list does not hold it — the `
            + `kept account is one of the list. Name it in the list (${named}), or first keep none (${noKeep}).`;
        case 'alone':
          return `\`${problem.account}\` is kept for conversations, and this list holds no other account, so driven work `
            + `would have none — name another account in it, or first keep none (${noKeep}).`;
      }
    }

    /**
     * `profile use <agent> [goal|order] [--keep <account>|--no-keep] [--switch smart|curve|off]
     * [--curve <window>=<elapsed>:<used>,…]… [--no-curve] [--workspace <name>]`, or `--clear` (D130 §16.6; D165 §6, the
     * switch design §5); `--early on|off` and `--near <percent>`, the old names, are taken as `UseChange` maps them. A scope
     * with no list of its own has nothing to use and is refused, naming the door that gives it one; a kept account is one
     * of the list and leaves driven work another (§4.6); a curve is refused in the sentence the driver says.
     */
    function useVerb(): ExitCode | Promise<ExitCode> {
      const name = accountsOf(operand(argv, 2), 'profile use');
      const flagged = flagValue(argv, '--workspace');
      const workspace = flagged ? normalizeWorkspace(flagged) : null;

      const stray = argv.slice(2).find((token) => token.startsWith('--') && !USE_FLAGS.has(token));
      if (stray) throw new DaorisError(`\`${stray}\` is not a flag of \`agent profile use\` — it takes ${USE_SHAPE}.`);
      const chosen = operands(argv, AGENT_VALUED).slice(3);
      const modes = USE_MODES.map((mode) => `${mode} (${USE_WORDS[mode].name})`);
      if (chosen.length > 1) {
        throw new DaorisError(`\`agent profile use\` takes one way to use accounts, and \`${chosen.join(' ')}\` names more — `
          + `${either(modes)}.`);
      }
      if (chosen.length === 1 && !(USE_MODES as readonly string[]).includes(chosen[0]!)) {
        throw new DaorisError(`\`${chosen[0]}\` is not a way to use accounts — ${either(modes)}. An account is kept for `
          + `conversations with \`--keep <account>\`.`);
      }

      const change = useChangeOf(name);
      if (chosen.length === 1) change.use = chosen[0] as UseMode;
      const clear = argv.includes('--clear');
      if (clear && Object.keys(change).length > 0) {
        throw new DaorisError(`\`--clear\` sets no setting beside it — \`daoris agent profile use ${name} --clear\` returns `
          + 'the scope to today\'s defaults, and the setting is set on its own.');
      }

      const scope = workspace ? { workspace } : {};
      if (clear) {
        return press(AGENT_ROUTES.account, { action: 'use', harness: name, ...scope, clear: true },
          () => writeHarnessSettings(path, withUse(settings, name, null, workspace)), () => {
            write(`daoris: ${where(workspace)}, \`${name}\` sets nothing of its own about how its list is used: today's defaults apply.`);
            write(`  Written to ${path} — machine-local, tracked by nothing, like every wiring file here.`);
            return 0;
          });
      }

      if (Object.keys(change).length === 0) return printUse(name, workspace, resolveScope(settings, name, workspace));

      const list = ownList(name, workspace);
      if (list.length === 0) {
        const borrowed = workspace !== null && resolveScope(settings, name, workspace).from === 'machine';
        throw new DaorisError(workspace
          ? `\`${workspace}\` has no list of its own for \`${name}\`, so there is nothing to use there — \`daoris agent profile `
            + `order ${name} <account>…${scoped(workspace)}\` gives it one`
            + (borrowed ? ', and until then it uses this machine\'s, which is set without --workspace.' : '.')
          : `\`${name}\` has no list on this machine, so there is nothing to use yet — \`daoris agent profile order ${name} `
            + '<account>…` sets one, and how it is used is set beside it.');
      }

      if (typeof change.keep === 'string') {
        const problem = scopeProblem({ default: null, list, keep: change.keep });
        if (problem?.kind === 'keep') {
          throw new DaorisError(
            `\`${problem.account}\` is not in \`${name}\`'s list ${where(workspace)} (${list.join(', then ')}) — the kept account `
            + `is one of the list. Name it in the list first (\`daoris agent profile order ${name} `
            + `${shellWords([...list, problem.account], '<account>')}${scoped(workspace)}\`), or keep one of it.`);
        }
        if (problem?.kind === 'alone') {
          throw new DaorisError(
            `\`${name}\`'s list ${where(workspace)} holds no account but \`${problem.account}\`, so keeping it for conversations `
            + `would leave driven work none — \`daoris agent profile order ${name} ${shellWords(list, '<account>')} <account>…`
            + `${scoped(workspace)}\` adds one.`);
        }
      }

      // As the Accounts place sends a change (`ACCOUNT_USE`'s `UseChangeOf`): a kept account or a curve taken out is its own flag.
      const sent = {
        ...(change.use !== undefined ? { use: change.use } : {}),
        ...(change.keep === null ? { noKeep: true } : change.keep !== undefined ? { keep: change.keep } : {}),
        ...(change.switch !== undefined ? { switch: change.switch } : {}),
        ...(change.curve === null ? { noCurve: true } : change.curve !== undefined ? { curve: change.curve } : {}),
        ...(change.early !== undefined ? { early: change.early } : {}),
        ...(change.near !== undefined ? { near: change.near } : {}),
      };
      const after = withUse(settings, name, change, workspace);
      return press(AGENT_ROUTES.account, { action: 'use', harness: name, ...scope, ...sent }, () => writeHarnessSettings(path, after), () => {
        printUse(name, workspace, resolveScope(after, name, workspace));
        write(`  Written to ${path} — machine-local, tracked by nothing, like every wiring file here.`);
        return 0;
      });
    }

    /** The settings `profile use`'s flags name, each refused unless it is one; none named is an empty change. */
    function useChangeOf(name: string): UseChange {
      const change: UseChange = {};
      const switched = useValue('--switch');
      if (switched !== undefined) {
        if (!(SWITCHES as readonly string[]).includes(switched)) {
          throw new DaorisError('`--switch` is smart, curve or off — smart, Daoris\'s curve; curve, your own (`--curve`); off, '
            + 'no switch while a session runs.');
        }
        change.switch = switched as Switch;
      }

      const curves = useValues('--curve');
      if (curves.length > 0 && argv.includes('--no-curve')) {
        throw new DaorisError('`--curve` and `--no-curve` together say two things — set a window of your curve, or take the '
          + 'curve out.');
      }
      if (curves.length > 0) change.curve = curveOf(curves);
      if (argv.includes('--no-curve')) change.curve = null;

      const early = onOff('--early', 'switch before the limit, or not');
      if (early !== undefined) change.early = early;

      const near = useValue('--near');
      if (near !== undefined) {
        const percent = /^\d+$/.test(near) ? Number(near) : Number.NaN;
        if (!(percent >= NEAR_LOWEST && percent <= NEAR_HIGHEST)) {
          throw new DaorisError(`\`--near\` is a whole percent from ${NEAR_LOWEST} to ${NEAR_HIGHEST} — e.g. \`--near 85\`: `
            + 'the old name of your curve\'s five-hour window, flat at that share; a start passes an account whose use of that '
            + 'window is over it.');
        }
        change.near = percent;
      }

      const keep = useValue('--keep');
      if (keep !== undefined && argv.includes('--no-keep')) {
        throw new DaorisError('`--keep` and `--no-keep` together say two things — keep one account for conversations, or none.');
      }
      if (keep !== undefined) change.keep = accountNamed(name, keep);
      if (argv.includes('--no-keep')) change.keep = null;
      return change;
    }

    /** A `profile use` flag's value, refusing the flag with none after it. */
    function useValue(flag: string): string | undefined {
      return useValues(flag)[0];
    }

    /** Each value a repeatable `profile use` flag is given, in order, refusing the flag with none after it. */
    function useValues(flag: string): string[] {
      const values: string[] = [];
      for (const [at, token] of argv.entries()) {
        if (token !== flag) continue;
        const value = argv[at + 1];
        if (value === undefined || value.startsWith('--') || value.trim() === '') {
          throw new DaorisError(`\`${flag}\` needs a value — it takes ${USE_SHAPE}.`);
        }
        values.push(value);
      }
      return values;
    }

    /**
     * The curve `--curve <window>=<elapsed>:<used>,…` gives, once per window (the switch design §5): each window's points as
     * whole numbers, then held to the curve's rules in the sentence the driver says (`curveProblem`).
     */
    function curveOf(given: string[]): Curve {
      const windows: [string, CurvePoint[]][] = [];
      for (const value of given) {
        const match = /^([^=]+)=(\d+:\d+(?:,\d+:\d+)*)$/.exec(value);
        if (!match) {
          throw new DaorisError(`\`--curve ${value}\` is not a window and its points — it takes \`<window>=<elapsed>:<used>,…\`, `
            + `each a whole percent, e.g. ${CURVE_EXAMPLE}.`);
        }
        const [, window, points] = match as unknown as [string, string, string];
        if (windows.some(([named]) => named === window)) {
          throw new DaorisError(`\`--curve\` names \`${window}\` twice — give each window once.`);
        }
        windows.push([window, points.split(',').map((point) => point.split(':').map(Number) as CurvePoint)]);
      }
      const curve = Object.fromEntries(windows);
      const problem = curveProblem(curve);
      if (problem !== null) {
        throw new DaorisError(`${problem} \`--curve\` takes <window>=<elapsed>:<used>,…, e.g. ${CURVE_EXAMPLE}.`);
      }
      return curve as Curve;
    }

    function onOff(flag: string, meaning: string): boolean | undefined {
      const value = useValue(flag);
      if (value === undefined) return undefined;
      if (value !== 'on' && value !== 'off') throw new DaorisError(`\`${flag}\` is on or off — ${meaning}.`);
      return value === 'on';
    }

    /**
     * `profile use`'s print: whose scope it is, its list, how it is used, the step the next start would follow, and what
     * each of its accounts last said.
     */
    function printUse(name: string, workspace: string | null, scope: Scope): ExitCode {
      const borrowed = workspace !== null && scope.from === 'machine';
      if (scope.list.length === 0) {
        write(!workspace
          ? `daoris: \`${name}\` has no list on this machine, so it has no settings — \`daoris agent profile order ${name} `
            + '<account>…` sets one.'
          : borrowed
            ? `daoris: \`${workspace}\` names no account of its own for \`${name}\`, and this machine has no list, so there are `
              + `no settings — \`daoris agent profile order ${name} <account>…\` sets the machine's.`
            : `daoris: \`${workspace}\` names its own account for \`${name}\`, \`${scope.default}\`, and no list of its own, so it `
              + `has no settings — \`daoris agent profile order ${name} ${shellWord(scope.default ?? '', '<account>')} <account>…`
              + `${scoped(workspace)}\` `
              + 'gives it a list.');
        return 0;
      }

      // Each account by the person's name for it, else its id (ACCT2).
      const names = accountNames(home, name);
      const shown = (account: string) => shownAs(names, account);
      write(borrowed
        ? `daoris: \`${workspace}\` names no account of its own for \`${name}\`, so it uses this machine's: `
          + `${scope.list.map(shown).join(', then ')}.`
        : `daoris: ${where(workspace)}, \`${name}\`'s list is ${scope.list.map(shown).join(', then ')}.`);

      const says = TOOLCHAINS[name]?.windows === true;
      const now = new Date();
      const zone = machineZone();
      const said = new Map(scope.list.map((account) => [account, saidOf(home, name, account, now)] as const));

      const { rows, notes } = useLines(scope, TOOLCHAINS[name]?.product ?? name, says, shown);
      for (const [label, value] of rows) write(`  ${label.padEnd(24)} ${value}`);
      for (const note of notes) write(`  ${note}`);
      const next = nextStartLines(scope, says, [...said.values()].some((windows) => windows !== null), shown);
      write(`  ${'next start'.padEnd(24)} ${next.row}`);
      if (next.note) write(`  ${next.note}`);

      // Each account's cool-off, else its last reading and its age (TOOL6c), else that it has said nothing: unknown. A
      // reading over the scope's curve says so, as the start's line does (SWITCH1b-iii).
      write('  what each account last said:');
      for (const account of scope.list) {
        const cooled = coolingOf(home, name, account, now);
        const reading = said.get(account);
        write(`    ${shown(account).padEnd(14)} ${cooled
          ? coolingLine(cooled, now, zone)
          : reading
            ? saidLine(reading, now, zone, scope.use)
            : 'nothing said about what it has left'}`);
      }
      return 0;
    }

    /**
     * `profile order <agent> --clear [--workspace <name>]`: the order gone, the machine's or one workspace's, saying which
     * order applies now, since a workspace with none takes the machine's.
     */
    function clearOrder(name: string, workspace: string | null): ExitCode | Promise<ExitCode> {
      const after = withRotation(settings, name, null, workspace);
      // An order of none clears it, as the Accounts place sends it.
      return press(AGENT_ROUTES.account, { action: 'order', harness: name, accounts: [], ...(workspace ? { workspace } : {}) },
        () => writeHarnessSettings(path, after), () => {
          if (workspace) {
            const machine = resolveRotation(after, name, null).order;
            write(machine.length > 0
              ? `daoris: \`${workspace}\` names no order for \`${name}\` now: the machine's applies there, ${machine.join(', then ')}.`
              : `daoris: \`${workspace}\` names no order for \`${name}\` now, and neither does this machine.`);
          } else {
            write(`daoris: \`${name}\` has no order on this machine now.`);
            const scoped = Object.entries(after.workspaceRotation).filter(([, orders]) => orders[name]).map(([circle]) => circle);
            if (scoped.length > 0) write(`  A workspace's own still applies there: ${scoped.join(', ')}.`);
          }

          write(`  No account was deleted. Written to ${path} — machine-local, tracked by nothing, like every wiring file here.`);
          return 0;
        });
    }

    /**
     * `profile default <agent> --clear [--workspace <name>]` (LEFT3): the default gone, the machine's or one
     * workspace's, as the screen's own clear writes it. Nothing is deleted, and it says what sessions run as now, since
     * a workspace with none falls back to the machine's default.
     */
    function clearDefault(name: string, workspace: string | null): ExitCode | Promise<ExitCode> {
      if (operand(argv, 3) !== undefined) {
        throw new DaorisError(
          `\`--clear\` names no account — \`daoris agent profile default ${name} --clear\` clears the default, `
          + `and \`daoris agent profile default ${name} ${shellWord(operand(argv, 3)!, '<account>')}\` sets it.`);
      }

      const after = withDefault(settings, name, null, workspace);
      // No account named clears it, as the Accounts place's own clear sends it.
      return press(AGENT_ROUTES.harness, { action: 'profile-default', harness: name, ...(workspace ? { workspace } : {}) },
        () => writeHarnessSettings(path, after), () => {
          const fallback = resolveProfile(after, name, workspace, null);
          write(!workspace
            ? `daoris: this machine names no default for \`${name}\` now: Daoris takes its accounts in turn.`
            : fallback
              ? `daoris: \`${workspace}\` names no account for \`${name}\` now: its sessions run as the machine's default, \`${fallback}\`.`
              : `daoris: \`${workspace}\` names no account for \`${name}\` now: its sessions take Daoris's accounts in turn.`);
          write(`  No account was deleted. Written to ${path} — machine-local, tracked by nothing, like every wiring file here.`);
          return 0;
        });
    }
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

  /** An account by its id or its name (ACCT2): its id, or what was given where it names none, for the verb to refuse. */
  function accountNamed(owner: string, given: string): string {
    return resolveAccount(profiles(home, owner), accountNames(home, owner), given) ?? given.trim();
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
   *
   * `managed` says the command is a pin's, which runs with the pin's own switch (AGT2), as a probe asks it.
   */
  function relay(
    command: string[], profile: string | null, toolchain: Toolchain, out: (line: string) => void, managed = false,
  ): ExitCode {
    out(`  $ ${command.join(' ')}`);
    out('');

    const env: Env = { ...process.env, ...(managed ? toolchain.pinnedEnv : {}) };
    if (profile) {
      mkdirSync(profile, { recursive: true });
      env[toolchain.profileVariable] = profile;
    }

    const result = startChild(command, env, { stdio: 'inherit' });

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
 * @param env The child's environment: a bare name is found on ITS PATH, the tools' (TOOLS5), as the driver's
 * `WindowsShim` finds an agent's on the PATH a session is handed.
 * @returns the file to spawn, its arguments, and whether they are a verbatim Windows command line.
 */
function spawnable(command: string[], env: Env): [string, string[], boolean] {
  const [name, ...rest] = command as [string, ...string[]];
  if (process.platform !== 'win32') return [name, rest, false];

  const resolved = windowsExecutable(name, env);
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
function windowsExecutable(command: string, env: Env): string {
  if (/[\\/]/.test(command) || /\.[a-z]+$/i.test(command)) return command;

  // Not found is not this function's decision to make: spawning reports it, with its own sentence.
  return onPath(command, { env, startable: true }) ?? command;
}

type Env = Record<string, string | undefined>;

/**
 * 🔴 Every child this module starts is started here (TOOLS5, D121 §2.4): handed the tools' environment, read now
 * from `$DAORIS_HOME/tools.json` through `tools.ts`, and its file found on that PATH. So an agent's probe, its
 * installer, its login and a pin's npm see the git and node a session of the desktop's would. With no home, or every
 * tool the system's, the environment is `base` exactly. Held by the dogfood test: the one `spawnSync` is here.
 *
 * GITSTART4: the system's git, where it is Git for Windows' launcher, starts as the git behind it with the launcher's
 * variables, one process where it was two (`gitforwindows.ts`, the driver's `WorkingTree.GitStart`). A git the person
 * named or a managed one starts as it is.
 */
function startChild(
  command: string[], base: Env, options: { stdio?: StdioOptions; timeout?: number; exact?: boolean },
): SpawnSyncReturns<string> {
  const { exact = false, ...started } = options;
  // 🔴 One exception (PUSH1f2a, D194 point 1): Daoris's driver program is handed the terminal's environment exactly. It hands its own
  // children the tools itself, and its `agent install` finds the machine's npm on the PATH it inherited; a managed node's first
  // would install the agent into the managed node's folder. Only `runDriverProgram` asks it, which the dogfood test holds.
  // GITSTART4's way past the launcher is a git's alone, and the driver's program is none.
  const home = exact ? null : daorisHome(base);
  const handed = exact ? base : handTools(base, home);
  const [named, argv, verbatim] = spawnable(command, handed);
  const past = verbatim || exact ? null : pastLauncher(named, handed);
  const { file, env } = past !== null && systemsGit(home) ? past : { file: named, env: handed };
  return spawnSync(file, argv, {
    ...started, env, encoding: 'utf8', shell: false, windowsHide: true,
    ...(verbatim ? { windowsVerbatimArguments: true } : {}),
  });
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

/** How the driver's program is started: the toolchain's one spawn site, or a test's stand-in. */
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
  const start: DriverStart = seam.start ?? ((command, given) => startChild(command, given, { stdio: 'inherit', exact: true }));
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

/**
 * git's answer in a checkout, read and never written (FABRIC-PIPELINE1a: `driver workflow use … --landed` reads the line's tip and
 * its declaration's blob as git objects): started here, as every child is, with the tools' git. A git that would not start answers
 * no status.
 */
export function gitAnswer(root: string, args: readonly string[]): { status: number | null; stdout: string } {
  const ran = startChild(throughTools(['git', '-C', root, ...args]), process.env, { timeout: 30_000 });
  return { status: ran.error ? null : ran.status, stdout: ran.stdout ?? '' };
}

/** Whether Daoris's git is the system's: with no home it is, and with one, where its entry reads so (§2.3). */
function systemsGit(home: string | null): boolean {
  if (home === null) return true;
  const entry = readTools(home).entries.git!;
  return entry.problem === null && entry.way === 'system';
}

/**
 * A command whose first word a tool answers for, with that word the file the tools resolve (TOOLS5): with no home,
 * as named, which is today's.
 */
function throughTools(command: string[]): string[] {
  const home = daorisHome();
  return home === null ? command : commandThroughTools(home, command);
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

/**
 * One workspace → agent → value map, as the driver's `ReadCircles` reads one (CASEFOLD1c): a workspace that is not an object
 * skipped, and one written twice in any case read once, as first written, holding the later's, a later naming none included.
 */
function circlesOf(value: unknown): Record<string, Record<string, string>> {
  return byName(Object.entries(asObject(value))
    .filter(([, map]) => map !== null && typeof map === 'object' && !Array.isArray(map))
    .map(([circle, map]) => [circle, stringMap(map)] as const));
}

function stringMap(value: unknown): Record<string, string> {
  return Object.fromEntries(
    Object.entries(asObject(value)).filter(([, v]) => typeof v === 'string' && v.length > 0),
  ) as Record<string, string>;
}

function sorted(map: Record<string, string>): Record<string, string> {
  return Object.fromEntries(Object.entries(map).sort(([a], [b]) => (a < b ? -1 : 1)));
}
