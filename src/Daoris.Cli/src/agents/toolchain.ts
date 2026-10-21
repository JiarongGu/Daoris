// The seam and the agents' declarations: what is left of `daoris agent`'s TypeScript (PUSH1f2e, D194, the alone-path design §4).
//
// Every verb of the families `driver`, `tool` and `agent` is Daoris's driver program's (`daoris-driver driver|tool|agent …`): this
// module starts it, with the same words, through the one spawn site, `startChild`. The accounts, the pins, the sign-ins, the keys and
// a maker's release channel went with the agent family's last verbs, PUSH1f2e; their rules are the driver's (`Harnesses.cs`,
// `ReleaseChannels`) and their tables its tests'. `TOOLCHAINS` stays for the names no plugin may declare (`plugins.ts`), until PUSH1f2f
// takes it and moves the seam to `platform/driverprogram.ts`.
//
// It is a MANAGEMENT module and it opens no socket. It spawns one program, the driver's; `child_process` is a Node built-in, so the
// zero-dependency guarantee stands.

import { existsSync, statSync } from 'node:fs';
import { THIS_INSTALL, daorisHome } from '../platform/home.ts';
import { handTools, readTools } from '../tools/tools.ts';
import { pastLauncher } from '../tools/gitforwindows.ts';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import type { SpawnSyncReturns, StdioOptions } from 'node:child_process';
import { DaorisError } from '../platform/errors.ts';
import { onPath } from '../platform/fsx.ts';
import type { ExitCode } from '../platform/errors.ts';

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
   * one — verified end to end by the driver's `ReleaseChannels` (PUSH1f2e), never through npm. A harness declares this or
   * `package`, never both: which source a pin came from is not left for anyone to work out.
   */
  channel?: 'claude-code-releases' | 'codex-releases';
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
  // Whether a tool says how much of each window an account has used is the driver's `Speaks` alone since `agent list`, the last
  // verb here that read `windows.json`, went to its program (PUSH1f2e4).
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

/** Whether Daoris's git is the system's: with no home it is, and with one, where its entry reads so (§2.3). */
function systemsGit(home: string | null): boolean {
  if (home === null) return true;
  const entry = readTools(home).entries.git!;
  return entry.problem === null && entry.way === 'system';
}
