/**
 * What the real-run proofs share — `acp2-proof`, `int4f-proof` and `perm2-proof` (REV3 CLEAN1). Each
 * carried its own copy of the readiness report, the scratch host, the adoption loop and the fixtures,
 * about 150 lines apiece, and each lesson in them (confine the host, unref the child, commit the
 * adoption) had been written down three times. What each proof PROVES stays in its own script.
 *
 * A proof is not a gate: it spends a login, and it is run by a person on a machine set up for it.
 * Without `--drive` it answers only what the machine has and what is left to do.
 */
import { execFileSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';
import { capture, makeChecker, openTranscript } from './rehearsal-kit.mjs';
// The real resolution, not a second copy of it (TOOL2/D57): a pinned harness is found exactly the way
// the CLI and the driver find one, so a proof cannot disagree with them about what would run.
import {
  harnessHome, harnessesPath, managedBinary, readHarnessSettings, resolveVersion,
} from '../src/Daoris.Cli/src/toolchain.ts';

export const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
export const cliBin = join(repoRoot, 'src', 'Daoris.Cli', 'bin', 'daoris.mjs');
const httpDll = join(
  repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Http',
  'bin', 'Debug', 'net10.0', 'daoris-knowledge-http.dll');
const driverDll = join(
  repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Host',
  'bin', 'Debug', 'net10.0', 'daoris-driver.dll');
// The connector a session publishes and proposes through. The driver finds it by walking up from its
// own binary to this workspace's build (`KnowledgeConnector.Candidates`), so without it a session has
// no voice, and a run would prove nothing about the one thing it exists to prove.
const mcpHost = join(
  repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Mcp',
  'bin', 'Debug', 'net10.0', process.platform === 'win32' ? 'daoris-knowledge.exe' : 'daoris-knowledge');

/** The harness every proof drives: Claude Code over the protocol door. */
export const ADAPTER = 'claude-code-acp';
// 🔴 The adapter's Daoris name and its BINARY are different strings — the package ships
// `claude-agent-acp`. Guessing the second from the first is how a pin reports itself missing.
export const BINARY = 'claude-agent-acp';
export const PACKAGE = '@agentclientprotocol/claude-agent-acp';

/**
 * Open a proof: its transcript, its checker, its readiness list, and — once it drives — its scratch
 * host on `port`, which is its own so that two proofs can share a machine.
 *
 * @param name the script's short name (`acp2`): the transcript, the scratch folder, the `--drive` hint.
 * @param label the item it proves (`ACP2`), as its scratch repositories and commits name it.
 */
export function openProof(name, { label, port }) {
  const scratch = join(repoRoot, '_fixtures', `${name}-proof`);
  const base = `http://localhost:${port}`;

  // 🔴 Held in this closure rather than declared by each script: a `let` further down a script sat in
  // its temporal dead zone when `beforeExit` reached for it, and failed as "Cannot access 'host'
  // before initialization" long after the line that caused it.
  let host = null;
  const stopHost = () => {
    try { host?.kill(); } catch { /* already gone */ }
    host = null;
  };

  openTranscript(repoRoot, name, { beforeExit: stopHost });
  const { totals, check, section } = makeChecker();

  /**
   * What the person still has to do. Collected rather than thrown, so they get the whole list once.
   *
   * 🔴 **A readiness item is not a check.** A machine that has not installed the adapter yet has
   * failed nothing — it is simply not set up — so these report as `todo` and never touch the failure
   * count. Mixing the two produced a run that said "FAIL" and "nothing above failed" in the same
   * breath, which is worse than either.
   */
  const missing = [];
  const needs = (what, command) => missing.push({ what, command });

  /** A readiness fact: true, or true-with-a-todo. Never a failure. */
  const ready = (label, satisfied, detail = '') => {
    console.log(satisfied
      ? `  ok    ${label}`
      : `  todo  ${label}${detail ? `\n          ${detail}` : ''}`);
    return satisfied;
  };

  /**
   * Which binary this machine would actually run for a harness: the managed pin, else `PATH` — the
   * order a spawn resolves (TOOL2's twin rule, minus the explicit command, a driver-config choice no
   * proof makes). Read when asked, so a machine with no Daoris home has already been told what is
   * built before the home's own refusal says what to set.
   */
  const managed = (harness, binary) => {
    const settings = readHarnessSettings(harnessesPath());
    return managedBinary(
      harnessHome(harnessesPath()), harness, resolveVersion(settings, harness, null, null), [binary]);
  };

  /** Ask a harness about itself the way `daoris agent list` does, tolerating absence. */
  const present = (harness, binary, args = ['--version']) => {
    const where = managed(harness, binary) ?? binary;
    const found = capture(`"${where}" ${args.join(' ')}`, repoRoot, { timeout: 30_000 });
    return found.code === 0
      ? { version: found.out.trim().split('\n')[0] ?? '', where }
      : null;
  };

  /**
   * The readiness every proof reports, in the order a person would fix it — each a fact about this
   * machine, each naming the one command that changes it.
   *
   * @param connector why this proof needs the MCP host built, or null when it does not.
   */
  const readiness = ({ connector = null } = {}) => {
    section('Readiness — what this machine has, and what is left');

    const built = ready('the host and the driver are built', existsSync(httpDll) && existsSync(driverDll));
    if (!built) needs('the host and driver binaries', 'npm run desktop -- build');

    if (connector) {
      const connectorBuilt = ready('the connector (the MCP host) is built', existsSync(mcpHost), connector);
      if (!connectorBuilt) needs('the MCP host', 'dotnet build src/Daoris.Service/Daoris.Service.Mcp');
    }

    const claude = present('claude-code', 'claude');
    ready('`claude` is on this machine', Boolean(claude),
      claude ? `${claude.version}  (${claude.where})` : 'not pinned, and not on PATH');
    if (!claude) {
      needs('the Claude Code CLI', 'daoris agent install claude-code   (or pin one: daoris agent pin claude-code <version>)');
    }

    const adapter = present(ADAPTER, BINARY);
    ready(`\`${ADAPTER}\` is on this machine`, Boolean(adapter), adapter?.version ?? 'not on PATH');
    if (!adapter) {
      needs(`the ACP adapter (${PACKAGE})`, `daoris agent pin ${ADAPTER} 0.79.0   — the version ACP2 was proven on`);
    }

    // The account. `claude auth status` answers JSON and exits 0 either way, so the OUTPUT is the
    // answer — the same reading the toolchain's own login check makes.
    const loggedIn = /"loggedIn"\s*:\s*true/i.test(capture('claude auth status', repoRoot, { timeout: 30_000 }).out);
    ready('a Claude account is logged in', loggedIn,
      loggedIn ? '' : 'the profile this run would use reports logged out');
    if (!loggedIn) {
      needs('a logged-in account',
        'daoris agent login claude-code [--profile <name>]   — runs the agent\'s own flow, into a directory Daoris owns');
    }

    return { claude, adapter, loggedIn };
  };

  /** One GET against the scratch host, bounded: a bare `fetch` can hang the run with nothing on screen. */
  const ask = async (path) => {
    try {
      const answered = await fetch(`${base}${path}`, { signal: AbortSignal.timeout(30_000) });
      return await answered.json();
    } catch {
      return null;
    }
  };

  /** One POST against the scratch host, bounded; the answer's JSON, or an empty object. */
  const post = (path, body) => fetch(`${base}${path}`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    signal: AbortSignal.timeout(30_000),
    body: JSON.stringify(body),
  }).then((r) => r.json()).catch(() => ({}));

  /** A fresh git repository with one commit in it — the starting point a session is measured from. */
  const born = (name, summary) => {
    const where = join(scratch, name);
    mkdirSync(where, { recursive: true });
    const git = (command) => execFileSync('git', command, { cwd: where, encoding: 'utf8' });
    git(['init', '-q', '-b', 'main']);
    git(['config', 'user.email', 'proof@example.com']);
    git(['config', 'user.name', `${label} proof`]);
    writeFileSync(join(where, 'README.md'), `# ${name}\n\n${summary}\n`);
    git(['add', '-A']);
    git(['commit', '-qm', 'the starting point']);
    return where;
  };

  /** The declaration of a repository that owns nothing anybody depends on, and says so. */
  const scratchDomain = (name) => ({
    summary: `A scratch repository, born for ${label}'s proof run. Not real work.`,
    owns: [`everything inside \`${name}\`, which is nothing anybody depends on`],
    accepts: ['a small, reversible change to its own files'],
  });

  /**
   * Say what a repository is, which `connect` requires before it will register one.
   *
   * 🔴 Not a formality, and the refusal says so: *"that declaration is how siblings know whether a
   * quest is yours"*. A fixture that skipped it was refused by name — the system defending its own
   * premise against a script that had not read it.
   */
  const declare = (where, domain) => {
    const manifest = join(where, 'daoris.json');
    const held = JSON.parse(readFileSync(manifest, 'utf8'));
    held.domain = domain;
    writeFileSync(manifest, `${JSON.stringify(held, null, 2)}\n`);
  };

  /** Empty the scratch folder: a drive starts from nothing a previous one left. */
  const freshScratch = () => {
    rmSync(scratch, { recursive: true, force: true });
    mkdirSync(scratch, { recursive: true });
  };

  /**
   * The host, started over the scratch folder and confined to it — the environment every later step
   * runs under, or null when it did not come up or can see beyond it.
   */
  const startHost = async () => {
    // 🔴 CONFINE THE HOST TO THIS SCRATCH FAMILY. Without `DAORIS_KNOWLEDGE_ROOT` the host falls back
    // to a parent-of-CWD heuristic and indexes every repository beside this one — measured: a run
    // without it read the developer's neighbouring projects into its registry. Nothing was written to
    // them, and nothing should have been read either. `DAORIS_KNOWLEDGE_DB` keeps the index here too,
    // so a scratch run never touches the machine's real one.
    const env = {
      DAORIS_SERVICE_URL: base,
      ASPNETCORE_URLS: base,
      DAORIS_KNOWLEDGE_ROOT: scratch,
      DAORIS_KNOWLEDGE_DB: join(scratch, 'knowledge.db'),
      DAORIS_REMOTE_CONFIG: join(scratch, 'no-remote.json'),
    };

    host = spawn('dotnet', [httpDll], { cwd: scratch, env: { ...process.env, ...env }, stdio: 'ignore' });
    // 🔴 A live child keeps Node's event loop alive, and the only thing that kills this one runs on
    // `beforeExit` — which therefore never fires. Every early `return` then sat forever with its last
    // check printed and nothing following it, which is indistinguishable from a hung driver.
    host.unref();
    let up = false;
    for (let attempt = 0; attempt < 40 && !up; attempt++) {
      await sleep(500);
      up = await fetch(`${base}/api/status`, { signal: AbortSignal.timeout(5_000) })
        .then((r) => r.ok).catch(() => false);
    }
    check('the scratch host answers', up);
    if (!up) return null;

    // 🔴 The guard that would have caught the scan escaping. A scratch host that can see a repository
    // this run did not create is pointed at the wrong world, and everything after it is meaningless —
    // so this refuses BEFORE anything is published or a model is spent.
    const seen = await ask('/api/registry');
    const strangers = (seen ?? []).map((r) => r.repository).filter((n) => !n.startsWith('proof-'));
    check('the host sees this scratch family and nothing else', strangers.length === 0,
      strangers.length ? `it also indexed: ${strangers.join(', ')}` : '');
    if (strangers.length > 0) {
      console.log('        Refusing to go further: DAORIS_KNOWLEDGE_ROOT is not confining the scan.');
      return null;
    }
    return env;
  };

  /** Adopt, declare and register each repository — true when every one of them did. */
  const adopt = (repos, env) => {
    let joined = true;
    for (const { name, where, domain } of repos) {
      const adopted = capture(`node "${cliBin}" init --name ${name}`, where, { env });
      declare(where, domain);
      capture(`node "${cliBin}" sync`, where, { env });
      // 🔴 `connect` has NO `--service` flag — the address is `DAORIS_SERVICE_URL`, and an unknown flag
      // is ignored in silence. Passing `env` is what points it at the scratch host; without it the
      // command answered "no DAORIS_SERVICE_URL" while the script had every appearance of having told it.
      const connected = capture(`node "${cliBin}" connect`, where, { env });
      // 🔴 COMMIT THE ADOPTION. `init` and `sync` leave `daoris.json`, `daoris.lock` and `.claude/`
      // uncommitted, and the driver refuses a tree with work in flight — *"somebody's work in flight;
      // the driver holds rather than entangling a session with it"*. It was right and the fixture was
      // wrong: a repository is adopted in a commit, not left dirty.
      execFileSync('git', ['add', '-A'], { cwd: where, encoding: 'utf8' });
      execFileSync('git', ['commit', '-qm', 'adopt daoris'], { cwd: where, encoding: 'utf8' });
      const ok = adopted.code === 0 && connected.code === 0;
      check(`\`${name}\` adopts, declares its domain, and registers`, ok, `${adopted.out}\n${connected.out}`);
      joined &&= ok;
    }
    return joined;
  };

  /**
   * Write `config` as the driver's `driver.json` in the scratch folder — its home, so the proposals,
   * the rules and the transcripts land there too — and run it until idle. This is the step that
   * spends the login, and it says so before it starts.
   */
  const runDriver = (env, config, saying) => {
    const configPath = join(scratch, 'driver.json');
    writeFileSync(configPath, `${JSON.stringify(config, null, 2)}\n`);
    console.log(`  ..    ${saying} — this is the step that spends the login`);
    const run = capture(`dotnet "${driverDll}" drive --until-idle`, scratch, {
      env: { ...env, DAORIS_DRIVER_CONFIG: configPath },
      timeout: 15 * 60_000,
    });
    console.log(run.out.split('\n').map((line) => `        ${line}`).join('\n'));
    return run;
  };

  /** The verdict: what is left to do when the machine is not ready, then the count, as the exit code. */
  const verdict = (what) => {
    console.log('');
    if (missing.length > 0) {
      console.log(`This machine is not ready for the real ${what} yet. What is left:`);
      for (const { what: item, command } of missing) {
        console.log(`\n  ${item}`);
        console.log(`    ${command}`);
      }
      console.log(`\nThen: node tools/${name}-proof.mjs --drive`);
      console.log('\nNothing above failed — this is a readiness report, not a gate.');
    }
    console.log(`\n  ${totals.checks - totals.failures}/${totals.checks} checks passed`);
    process.exitCode = totals.failures > 0 ? 1 : 0;
  };

  return {
    scratch, totals, check, section, missing, readiness, ask, post, born, scratchDomain, declare,
    freshScratch, startHost, stopHost, adopt, runDriver, verdict,
  };
}
