/**
 * The fabric probe's launchers (FABRIC-PROBE1; the C0 audit §2, `docs/2026-10-17-fabric-contract-audit.md`).
 *
 *   node tools/fabric-probe/launch.mjs claude [--scenario child|family|kill] [--run <name>] [--model <id>]
 *   node tools/fabric-probe/launch.mjs codex --codex <codex executable> [--scenario …] [--model <id>] [--effort low]
 *
 * One run is one folder, `_fixtures/fabric-probe/<run>/` (gitignored): a scratch repository with one empty commit, a
 * scratch Daoris home, the stand-in server's log, the hook log, the harness's own event stream, and `run.json`. Then the
 * receipt (`receipt.mjs`) is printed.
 *
 * 🔴 Every process it starts gets its own `DAORIS_HOME`, the run's `home/`: every `DAORIS_*` variable this process
 * inherited is dropped first (on the owner's machine one names the live install's home), and so is every `CLAUDE*`
 * variable of the session that ran it, so the harness starts as a root and not as a child of this one. Never the
 * account's Daoris home, never an install. The harness's own sign-in is its own: Claude Code's account, and Codex's
 * `CODEX_HOME` where the person set one, else its default.
 *
 * A run spends the account's usage. Each scenario is a few short turns; `--timeout` (default 240 s) ends the tree.
 * The scratch repository sits under this checkout, so Claude Code reads this checkout's `CLAUDE.md` as a driven session
 * in a repository would; `--setting-sources local` keeps the user tier out, as the audit's runs did.
 */
import { execFileSync, spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from '../fsx.mjs';
import { receipt, receiptText } from './receipt.mjs';

const here = dirname(fileURLToPath(import.meta.url));
export const ROOT = resolve(here, '..', '..');
export const SERVER = join(here, 'server.mjs');
export const HOOK = join(here, 'hook.mjs');

/** Where runs are kept: the checkout's gitignored scratch. */
export const RUNS = join(ROOT, '_fixtures', 'fabric-probe');

/** The stand-in's server name, so its tools are `mcp__probe__*` on Claude Code. */
export const SERVER_NAME = 'probe';

/**
 * The environment a probe's process starts with: this one's, without the session's `CLAUDE*` variables or any
 * `DAORIS_*` it inherited, then the run's own home.
 */
export function childEnvironment(env, home) {
  const child = {};
  for (const [name, value] of Object.entries(env)) {
    if (/^claude/i.test(name) || /^daoris_/i.test(name)) continue;
    child[name] = value;
  }
  child.DAORIS_HOME = home;
  return child;
}

/** The run's files, by role. */
export function runFiles(folder) {
  return {
    folder,
    repo: join(folder, 'repo'),
    home: join(folder, 'home'),
    serverLog: join(folder, 'server.log'),
    hookLog: join(folder, 'hooks.log'),
    stream: join(folder, 'stream.jsonl'),
    stderr: join(folder, 'stderr.log'),
    mcp: join(folder, 'mcp.json'),
    settings: join(folder, 'settings.json'),
    run: join(folder, 'run.json'),
  };
}

/** The root session id the stand-in's entry names, as a driver names a session's. */
export const rootSession = (run) => `probe-root-${run}`;

/** What each scenario asks, in each harness's words for a subagent and for the probe's tools. */
export function prompt(scenario, harness) {
  const spawnWord = harness === 'claude'
    ? 'use the Agent tool to launch a general-purpose subagent'
    : 'spawn a sub-agent with your spawn_agent tool';
  const tool = harness === 'claude' ? 'mcp__probe__probe_read' : 'the probe server\'s probe_read tool';
  const wait = harness === 'claude' ? 'mcp__probe__probe_wait' : 'the probe server\'s probe_wait tool';
  const call = (note) => `call ${tool} once with note "${note}"`;
  const head = 'This is a short probe of tool calls; do nothing else, read no files and run no commands. ';
  switch (scenario) {
    case 'child':
      return `${head}First ${call('root')}. Then ${spawnWord} whose task is exactly: "${call('child')} and reply with the `
        + 'token it returned." Finally reply with every token you saw.';
    case 'family':
      return `${head}First ${call('root')}. Then ${spawnWord} named sibling-a whose task is exactly: "${call('sibling-a')}, `
        + `then ${spawnWord} of your own whose task is: ${call('grandchild')} and reply with its token. `
        + 'Reply with both tokens." After it answered, '
        + `${spawnWord} named sibling-b whose task is exactly: "${call('sibling-b')} and reply with its token." `
        + 'Finally reply with every token you saw.';
    case 'kill':
      return `${head}First ${call('root')}. Then ${spawnWord} whose task is exactly: "${call('child')}, then call `
        + `${wait} once with seconds 120 and note "child", and reply with its token." Wait for it, then reply.`;
    default:
      throw new Error(`no scenario ${scenario}: child, family or kill`);
  }
}

/** The stand-in's MCP entry: node and the server, logging to the run's log, named as the root session. */
export function probeServer(files, run, node = process.execPath) {
  return {
    command: node,
    args: [SERVER, '--log', files.serverLog],
    env: { DAORIS_SESSION_ID: rootSession(run), DAORIS_HOME: files.home },
  };
}

/**
 * Claude Code's launch, the pipe door's shape (`claude -p`, stream-json): the servers from `--mcp-config` alone, rules
 * and hooks from `--settings` with the user tier left out. `probe_read` and `probe_wait` allowed, `probe_write` denied,
 * `probe_unlisted` named by no rule. The hook logger on every `PreToolUse`, `SubagentStart` and `SubagentStop`.
 */
export function claudeLaunch({ files, run, text, model, node = process.execPath, servers = {}, hooks = {} }) {
  const mcp = { mcpServers: { [SERVER_NAME]: { type: 'stdio', ...probeServer(files, run, node) }, ...servers } };
  const logger = { type: 'command', command: node, args: [HOOK, files.hookLog], timeout: 30 };
  const settings = {
    permissions: {
      allow: ['mcp__probe__probe_read', 'mcp__probe__probe_wait'],
      ask: [],
      deny: ['mcp__probe__probe_write'],
    },
    hooks: {
      PreToolUse: [{ matcher: '*', hooks: [logger] }, ...(hooks.PreToolUse ?? [])],
      SubagentStart: [{ hooks: [logger] }, ...(hooks.SubagentStart ?? [])],
      SubagentStop: [{ hooks: [logger] }, ...(hooks.SubagentStop ?? [])],
    },
  };
  const args = [
    '-p', text,
    '--output-format', 'stream-json', '--verbose',
    '--mcp-config', files.mcp, '--strict-mcp-config',
    '--settings', files.settings, '--setting-sources', 'local',
    '--permission-mode', 'acceptEdits',
    ...(model ? ['--model', model] : []),
  ];
  return { mcp, settings, args };
}

/** A TOML value, for Codex's `-c key=value`: strings as basic strings, arrays and tables inline. */
export function toml(value) {
  if (Array.isArray(value)) return `[${value.map(toml).join(', ')}]`;
  if (value !== null && typeof value === 'object') {
    return `{ ${Object.entries(value).map(([key, item]) => `${key} = ${toml(item)}`).join(', ')} }`;
  }
  return typeof value === 'string' ? JSON.stringify(value) : String(value);
}

/**
 * Codex's launch, `codex exec --json` read-only, with the user's configuration ignored and the stand-in set by `-c`:
 * `probe_write` disabled, and its tools approved, since Codex refuses every MCP call by default (the audit's run E).
 * Never `--ephemeral`, which breaks a spawn.
 */
export function codexLaunch({ files, run, text, model, effort, node = process.execPath, servers = {} }) {
  const entry = { ...probeServer(files, run, node), disabled_tools: ['probe_write'], default_tools_approval_mode: 'approve' };
  const set = (name, server) => Object.entries(server).flatMap(([key, value]) => ['-c', `mcp_servers.${name}.${key}=${toml(value)}`]);
  return {
    args: [
      'exec', '--json', '--sandbox', 'read-only', '--ignore-user-config',
      ...set(SERVER_NAME, entry),
      ...Object.entries(servers).flatMap(([name, server]) => set(name, server)),
      ...(model ? ['-m', model] : []),
      ...(effort ? ['-c', `model_reasoning_effort=${toml(effort)}`] : []),
      text,
    ],
  };
}

/** The scratch repository: a folder with one empty commit, made fresh. */
function scratchRepository(repo, env) {
  mkdirSync(repo, { recursive: true });
  const git = (...args) => execFileSync('git', args, { cwd: repo, env, stdio: 'ignore', windowsHide: true });
  git('init', '-q');
  git('-c', 'user.name=probe', '-c', 'user.email=probe@example.invalid', 'commit', '-q', '--allow-empty', '-m', 'probe');
}

/** The process ids a server log says started. */
function serverPids(log) {
  if (!existsSync(log)) return [];
  return readFileSync(log, 'utf8').split('\n').filter(Boolean).flatMap((line) => {
    try {
      const entry = JSON.parse(line);
      return entry.event === 'start' ? [entry.pid] : [];
    } catch {
      return [];
    }
  });
}

/** Whether the server log has a `probe_wait` call in it yet. */
function waitCalled(log) {
  if (!existsSync(log)) return false;
  return readFileSync(log, 'utf8').split('\n').some((line) => line.includes('"tools/call"') && line.includes('"probe_wait"'));
}

function alive(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

/** The root alone (the audit's `taskkill /F`, not the tree), or the whole tree. */
function kill(pid, tree) {
  if (process.platform === 'win32') {
    spawnSync('taskkill', ['/F', ...(tree ? ['/T'] : []), '/PID', String(pid)], { stdio: 'ignore', windowsHide: true });
  } else {
    try {
      process.kill(tree ? -pid : pid, 'SIGKILL');
    } catch {
      // Gone already.
    }
  }
}

const sleep = (ms) => new Promise((done) => setTimeout(done, ms));

/** One run, start to receipt. */
export async function launch(options) {
  const { harness, scenario = 'child', model, effort, timeout = 240, killAfter = 90, servers, hooks } = options;
  const run = options.run ?? `${harness}-${scenario}-${new Date().toISOString().replace(/[-:]/g, '').slice(0, 15)}`;
  const files = runFiles(join(RUNS, run));
  rmSync(files.folder, { recursive: true, force: true });
  mkdirSync(files.home, { recursive: true });
  const env = childEnvironment(process.env, files.home);
  scratchRepository(files.repo, env);

  const text = prompt(scenario, harness);
  let command;
  let args;
  if (harness === 'claude') {
    command = options.claude ?? 'claude';
    const launched = claudeLaunch({ files, run, text, model, servers: servers?.(files, run) ?? {}, hooks: hooks?.(files, run) ?? {} });
    writeFileSync(files.mcp, `${JSON.stringify(launched.mcp, null, 2)}\n`);
    writeFileSync(files.settings, `${JSON.stringify(launched.settings, null, 2)}\n`);
    args = launched.args;
  } else if (harness === 'codex') {
    if (!options.codex) throw new Error('codex needs --codex <executable>: the install\'s managed copy or one on PATH');
    command = options.codex;
    args = codexLaunch({ files, run, text, model, effort, servers: servers?.(files, run) ?? {} }).args;
  } else {
    throw new Error(`no harness ${harness}: claude or codex`);
  }

  const version = spawnSync(command, ['--version'], { env, encoding: 'utf8', windowsHide: true }).stdout?.trim() || null;
  const started = Date.now();
  const child = spawn(command, args, { cwd: files.repo, env, windowsHide: true, detached: process.platform !== 'win32' });
  child.stdout.on('data', (chunk) => writeFileSync(files.stream, chunk, { flag: 'a' }));
  child.stderr.on('data', (chunk) => writeFileSync(files.stderr, chunk, { flag: 'a' }));
  child.stdin.end();
  const exited = new Promise((done) => child.on('exit', (code, signal) => done({ code, signal })));

  let killed = null;
  let ended = null;
  const deadline = started + timeout * 1000;
  while (ended === null) {
    ended = await Promise.race([exited, sleep(500).then(() => null)]);
    if (ended !== null) break;
    if (scenario === 'kill' && killed === null
      && (waitCalled(files.serverLog) || Date.now() - started > killAfter * 1000)) {
      // Once the child's call is held open: the root process alone, as the audit's run B and G killed it.
      await sleep(2000);
      kill(child.pid, false);
      killed = { seconds: Math.round((Date.now() - started) / 1000), mode: 'root alone' };
    } else if (Date.now() > deadline) {
      kill(child.pid, true);
      killed = { seconds: timeout, mode: 'tree, at the timeout' };
    }
  }

  // What outlived the root: each server process, looked at again after the audit's six to eight seconds.
  if (killed) await sleep(8000);
  const servers_ = serverPids(files.serverLog).map((pid) => ({ pid, alive: alive(pid) }));
  const record = {
    harness, version, scenario, model: model ?? null, effort: effort ?? null, run,
    seconds: Math.round((Date.now() - started) / 1000), exit: ended.code, signal: ended.signal, killed,
    serversAfter: servers_,
  };
  writeFileSync(files.run, `${JSON.stringify(record, null, 2)}\n`);
  return { files, record };
}

/** `--name value` pairs, after the harness. */
export function parseArguments(argv) {
  const [harness, ...rest] = argv;
  const options = { harness };
  for (let at = 0; at < rest.length; at += 2) {
    const name = rest[at];
    if (!name?.startsWith('--') || rest[at + 1] === undefined) throw new Error(`expected --name value, found ${name}`);
    const key = name.slice(2).replace(/-([a-z])/g, (_, letter) => letter.toUpperCase());
    const value = rest[at + 1];
    options[key] = ['timeout', 'killAfter'].includes(key) ? Number(value) : value;
  }
  return options;
}

if (isMain(import.meta.url)) {
  try {
    const { files } = await launch(parseArguments(process.argv.slice(2)));
    process.stdout.write(receiptText(receipt(files.folder)));
  } catch (error) {
    process.stderr.write(`fabric-probe: ${error.message}\n`);
    process.exit(2);
  }
}
