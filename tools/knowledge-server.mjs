#!/usr/bin/env node
/**
 * This workspace's knowledge server for agents (ORIENT1c): what `.mcp.json` starts, and the build it runs.
 *
 *   node tools/knowledge-server.mjs              serve over stdio, as `.mcp.json` names it
 *   node tools/knowledge-server.mjs build        publish the MCP host under local/, when its sources changed
 *   node tools/knowledge-server.mjs build --force
 *
 * ## Why
 *
 * `.mcp.json` named `dotnet run --project …Mcp`: a build at every session's start, once per worktree, failing
 * when two built at once, and with no Daoris home in a terminal's environment the host refused to start anyway.
 * The session that measured ORIENT1 had no server at all. So the server is built once, by `build` (which the
 * merge tool runs after a merge's gates pass), into `local/knowledge-server/builds/<when>-<digest>/`, and
 * `current.json` beside the builds names the one to run. Serving never builds.
 *
 * ## What it serves
 *
 * A session started by hand, or a subagent's, gets the workspace's own server: the build's home under
 * `local/knowledge-server/home/` (D63: never the profile, and never the install's home, whose store a workspace
 * build must not open), over this one checkout, its documents under `docs/` a section each and its index under
 * `docs/index/` a row each, by words only: each process re-reads the checkout once its reading is a minute old,
 * and embedding thousands of entries a minute is not a cost a workspace's instrument pays (D24: the deployment
 * chooses, and every answer says the tier). A worktree reaches its main checkout's build and serves the main
 * checkout, unless it built one of its own.
 *
 * A session a driver started (it names `DAORIS_REPOSITORY`) is the driver's: its environment is passed on whole,
 * so its connector speaks to the driver's store, as this file did for a pipe-door session before (D70). It runs
 * the machine's own host first, where the driver would find it for the protocol door (`DAORIS_MCP_HOST`, then
 * `bin/` under the home: `KnowledgeConnector.Candidates`), and this workspace's build after.
 *
 * With nothing to run, it answers the protocol itself: the handshake says why in the server's instructions,
 * no tool is listed, and the session works as it did before there was a server.
 *
 * Exit codes: 0 served or built (or nothing to build) · 1 a build failed · 2 a usage error.
 */
import { spawn, spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, readdirSync, readFileSync, rmSync, statSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';
import { isMain, writeAtomic } from './fsx.mjs';

/** Where a checkout keeps the server: its builds, the pointer to the current one, and its home. */
export const SERVER = 'local/knowledge-server';
/** What marks a session a driver started: set on every session its spawn shell makes, and by nobody else. */
export const DRIVEN = 'DAORIS_REPOSITORY';
/** The folders read beside the roles, a section each and a row each. */
export const DOCUMENTS = 'docs';
export const INDEX = 'docs/index';
/** The project built, the assembly it makes, and the projects its build reads from. */
export const PROJECT = 'src/Daoris.Service/Daoris.Service.Mcp';
export const ENTRY = 'daoris-knowledge.dll';
const SOURCES = ['Daoris.Service.Core', 'Daoris.Service.Shared', 'Daoris.Service.Mcp'];
const SOURCE_FILES = /\.(cs|csproj|props|targets)$/;
/** How many builds stay: a running session may still load from one a merge has replaced. */
const KEEP = 3;

/**
 * The machine's store, its remotes, its rules and its model: the install's to use, not a workspace build's. A
 * workspace build is the source of a later install, not the binary serving this one's store.
 */
const MACHINE = [
  'DAORIS_KNOWLEDGE_DB',
  'DAORIS_KNOWLEDGE_ROOT',
  'DAORIS_REMOTE_CONFIG',
  'DAORIS_REMOTE_URL',
  'DAORIS_REMOTE_KEY',
  'DAORIS_REMOTE_WORKSPACE',
  'DAORIS_RULES_HOME',
  'DAORIS_EMBED_MODEL',
  'DAORIS_EMBED_URL',
  'DAORIS_EMBED_WINDOW',
];

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));

/** The main checkout of a linked worktree, read from its `.git` file; any other checkout is its own. */
export function mainCheckout(checkout) {
  const dotGit = join(checkout, '.git');
  let stat;
  try {
    stat = statSync(dotGit);
  } catch {
    return checkout;
  }
  if (stat.isDirectory()) return checkout;
  const named = /^gitdir:\s*(.+)$/m.exec(readFileSync(dotGit, 'utf8'));
  if (!named) return checkout;
  const gitdir = resolve(checkout, named[1].trim());
  let common = gitdir;
  try {
    common = resolve(gitdir, readFileSync(join(gitdir, 'commondir'), 'utf8').trim());
  } catch {
    // No commondir: the gitdir is the repository's own.
  }
  // A bare repository's worktree has no checkout above its common folder.
  return basename(common) === '.git' ? dirname(common) : checkout;
}

/** A checkout's current build, or null: none built, or a pointer to a build that is gone. */
export function readBuild(checkout) {
  const folder = join(checkout, SERVER);
  let current;
  try {
    current = JSON.parse(readFileSync(join(folder, 'current.json'), 'utf8'));
  } catch {
    return null;
  }
  if (typeof current?.build !== 'string' || typeof current?.entry !== 'string') return null;
  const entry = join(folder, current.build, current.entry);
  return existsSync(entry)
    ? { folder, build: current.build, entry, digest: String(current.digest ?? ''), builtAt: String(current.builtAt ?? '') }
    : null;
}

/** The build a session in this checkout runs, and the checkout it serves: its own, else its main checkout's. */
export function locateServer(checkout) {
  const own = readBuild(checkout);
  if (own) return { served: checkout, ...own };
  const main = mainCheckout(checkout);
  if (main === checkout) return null;
  const shared = readBuild(main);
  return shared ? { served: main, ...shared } : null;
}

/** The environment the server is started with, and whose session it serves. */
export function serverEnvironment(env, located) {
  if (env[DRIVEN]?.trim()) return { mode: 'driven', env: { ...env } };
  const out = { ...env };
  if (!located) return { mode: 'workspace', env: out };
  for (const name of MACHINE) delete out[name];
  out.DAORIS_HOME = join(located.folder, 'home');
  out.DAORIS_KNOWLEDGE_REPOSITORY = located.served;
  out.DAORIS_KNOWLEDGE_DOCUMENTS = DOCUMENTS;
  out.DAORIS_KNOWLEDGE_INDEX = INDEX;
  return { mode: 'workspace', env: out };
}

/** What to run, or null when there is nothing: the machine's host first for a driven session, then the build. */
export function serverCommand(mode, env, located, platform = process.platform) {
  if (mode === 'driven') {
    const executable = platform === 'win32' ? 'daoris-knowledge.exe' : 'daoris-knowledge';
    const machine = [env.DAORIS_MCP_HOST?.trim(), env.DAORIS_HOME?.trim() ? join(env.DAORIS_HOME.trim(), 'bin', executable) : null]
      .filter(Boolean)
      .find((path) => existsSync(path));
    if (machine) return { command: machine, args: [], what: `the machine's host, ${machine}` };
  }
  return located
    ? { command: 'dotnet', args: [located.entry], what: `this workspace's build of ${located.builtAt}` }
    : null;
}

/** Why there is no server, in the words the session reads. */
export function absenceSentence(checkout, mode) {
  return mode === 'driven'
    ? 'no knowledge server for this session: no host at DAORIS_MCP_HOST, none under bin/ in DAORIS_HOME, and no build '
      + `of this workspace's (\`npm run knowledge:build\` in ${mainCheckout(checkout)} makes one). The session works `
      + 'without its connector.'
    : `the workspace's knowledge server is not built: \`npm run knowledge:build\` in ${mainCheckout(checkout)} builds it, `
      + 'and the merge tool rebuilds it after a merge. Until then, read docs/index/ and search the files as before; '
      + 'reconnect this server once it is built.';
}

/** The answer to one message while there is no server: the handshake says why, no tool is listed, the rest refused. */
export function absenceReply(message, sentence) {
  if (!message || typeof message !== 'object' || message.id === undefined || message.id === null) return null;
  const reply = (result) => ({ jsonrpc: '2.0', id: message.id, result });
  switch (message.method) {
    case 'initialize':
      return reply({
        protocolVersion: message.params?.protocolVersion ?? '2025-06-18',
        capabilities: { tools: {} },
        serverInfo: { name: 'daoris-knowledge', version: 'not built' },
        instructions: sentence,
      });
    case 'ping':
      return reply({});
    case 'tools/list':
      return reply({ tools: [] });
    default:
      return { jsonrpc: '2.0', id: message.id, error: { code: -32601, message: sentence } };
  }
}

/** The protocol spoken with nothing behind it, a message a line, until the input ends. */
function serveAbsence(input, output, sentence) {
  process.stderr.write(`daoris-knowledge: ${sentence}\n`);
  const lines = createInterface({ input, crlfDelay: Infinity });
  lines.on('line', (line) => {
    if (!line.trim()) return;
    let message;
    try {
      message = JSON.parse(line);
    } catch {
      return;
    }
    const reply = absenceReply(message, sentence);
    if (reply) output.write(`${JSON.stringify(reply)}\n`);
  });
  return new Promise((done) => lines.on('close', done));
}

/** Serve: the build, with the environment its session needs, its standard streams the client's own. */
async function serve(checkout) {
  const located = locateServer(checkout);
  const { mode, env } = serverEnvironment(process.env, located);
  const command = serverCommand(mode, env, located);
  if (!command) {
    await serveAbsence(process.stdin, process.stdout, absenceSentence(checkout, mode));
    process.exit(0);
  }
  const child = spawn(command.command, command.args, { stdio: 'inherit', env, windowsHide: true });
  child.on('error', async (error) => {
    // The child never started, so the input is still this process's to answer on.
    await serveAbsence(process.stdin, process.stdout,
      `the knowledge server could not start (${command.what}: ${error.code ?? error.message}). Is \`dotnet\` on the PATH?`);
    process.exit(0);
  });
  for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => child.kill(signal));
  child.on('exit', (code) => process.exit(code ?? 1));
}

/** What the host is built from, as one digest: the projects it compiles and the tree's build settings. */
export function sourceDigest(checkout) {
  const service = join(checkout, 'src', 'Daoris.Service');
  const files = [];
  const walk = (folder, at) => {
    let entries;
    try {
      entries = readdirSync(folder, { withFileTypes: true });
    } catch {
      return;
    }
    for (const entry of entries) {
      const path = `${at}/${entry.name}`;
      if (entry.isDirectory()) {
        if (entry.name !== 'bin' && entry.name !== 'obj') walk(join(folder, entry.name), path);
      } else if (SOURCE_FILES.test(entry.name)) {
        files.push(path);
      }
    }
  };
  for (const project of SOURCES) walk(join(service, project), project);
  for (const name of ['Directory.Build.props', 'Directory.Packages.props']) {
    if (existsSync(join(service, name))) files.push(`/${name}`);
  }
  const hash = createHash('sha256');
  for (const path of files.sort()) {
    hash.update(`${path}\0`).update(readFileSync(join(service, path.replace(/^\//, ''))));
    hash.update('\0');
  }
  return hash.digest('hex');
}

/** Whether a build is owed, and why. */
export function buildPlan(checkout, digest) {
  const current = readBuild(checkout);
  if (!current) return { needed: true, why: 'no build yet' };
  if (current.digest !== digest) return { needed: true, why: "the service's sources changed" };
  return { needed: false, why: `built ${current.builtAt} from these sources` };
}

/** The newest builds stay; each older one goes, unless something holds it, which is kept and said. */
export function pruneBuilds(builds, keep, remove = (path) => rmSync(path, { recursive: true, force: true })) {
  let names;
  try {
    names = readdirSync(builds, { withFileTypes: true }).filter((entry) => entry.isDirectory()).map((entry) => entry.name).sort();
  } catch {
    return { removed: [], kept: [] };
  }
  const removed = [];
  const kept = [];
  for (const name of names.slice(0, Math.max(0, names.length - keep))) {
    try {
      remove(join(builds, name));
      removed.push(name);
    } catch (error) {
      if (!['EBUSY', 'EPERM', 'EACCES'].includes(error?.code)) throw error;
      kept.push({ name, why: 'in use' });
    }
  }
  return { removed, kept };
}

const stamp = () => new Date().toISOString().replace(/[-:]/g, '').replace(/\.\d+Z$/, 'Z');

/**
 * This machine's runtime identifier. A build for it alone carries one platform's SQLite library rather than
 * every platform's: 9 MB rather than 59, measured on the first build.
 */
const rid = () => {
  const arch = process.arch === 'arm64' ? 'arm64' : 'x64';
  return `${process.platform === 'win32' ? 'win' : process.platform === 'darwin' ? 'osx' : 'linux'}-${arch}`;
};

/** Build when owed: publish into a new folder, point at it, and prune the old. */
function build(checkout, { force }) {
  const started = Date.now();
  const digest = sourceDigest(checkout);
  const plan = buildPlan(checkout, digest);
  if (!plan.needed && !force) {
    console.log(`knowledge-server: current (${plan.why})`);
    return 0;
  }
  const why = plan.needed ? plan.why : 'forced';
  const folder = join(checkout, SERVER);
  const name = `${stamp()}-${digest.slice(0, 8)}`;
  const out = join(folder, 'builds', name);
  // stdout kept and shown on failure: MSBuild writes its errors there (service-publish's lesson, REV3).
  // Framework-dependent: a machine that builds the workspace has the runtime, and `dotnet` runs the result.
  const run = spawnSync('dotnet', ['publish', PROJECT, '-c', 'Release', '-r', rid(), '--self-contained', 'false', '-o', out, '--nologo'],
    { cwd: checkout, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  if (run.status !== 0 || !existsSync(join(out, ENTRY))) {
    process.stdout.write(run.stdout ?? '');
    process.stderr.write(run.stderr ?? (run.error ? String(run.error) : ''));
    rmSync(out, { recursive: true, force: true });
    console.error(`knowledge-server: the build failed (${why}); sessions keep the build they had`);
    return 1;
  }
  const pointer = join(folder, 'current.json');
  writeAtomic(pointer, `${JSON.stringify({ build: `builds/${name}`, entry: ENTRY, digest, builtAt: new Date().toISOString() }, null, 2)}\n`);
  const pruned = pruneBuilds(join(folder, 'builds'), KEEP);
  const kept = pruned.kept.length ? `; ${pruned.kept.length} older kept, in use` : '';
  console.log(`knowledge-server: built ${name} (${why}) in ${Math.round((Date.now() - started) / 1000)} s; `
    + `${pruned.removed.length} older removed${kept}`);
  return 0;
}

if (isMain(import.meta.url)) {
  const [verb, ...rest] = process.argv.slice(2);
  if (verb === 'build' && rest.every((arg) => arg === '--force')) {
    process.exit(build(repoRoot, { force: rest.includes('--force') }));
  } else if (verb === undefined) {
    await serve(repoRoot);
  } else {
    console.error('usage: node tools/knowledge-server.mjs [build [--force]]');
    process.exit(2);
  }
}
