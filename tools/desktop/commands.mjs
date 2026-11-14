import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { copyTree } from '../fsx.mjs';
import { homeEntry } from '../../src/Daoris.Cli/src/platform/home.ts';
import { HOME, LAUNCHER, layOffers } from '../desktop-publish.mjs';
import { applicationsAt, running, stopAll } from '../processes.mjs';
import { DESKTOP_PROJECT, DRIVER_HOST_PROJECT, HTTP_PROJECT, MCP_PROJECT, RUN_FILE, WEB, ago, readRun, takeInstall } from './session.mjs';
import { fail, run } from './run.mjs';
import { awaitSwap } from './swap-wait.mjs';
import { PAGE_IDENTITY, isShell } from './page.mjs';
import { assemblyExe, awaitDebugPort, closedPortReport, engineLogOf, installedExe } from './executables.mjs';
import { CLEARED, debugEnvironment, installEnvironment, scratchEnvironment, scratchPlan } from './environment.mjs';
import { repoRoot, scratchRoot } from './paths.mjs';
import { build as buildProject, guarded } from '../tree-base.mjs';

/**
 * Attach to one of the running shell's pages, having first established that it IS the shell.
 *
 * @param window - null for the application's own window, or a secondary window's name:
 * `monitor`, or `session:<id>`. Since the shell can hold more than one page, the caller says which.
 */
export async function attach(window = null) {
  const state = readRun();
  if (!state?.cdpPort) {
    fail('nothing to attach to — `node tools/desktop.mjs run` starts a shell with its debug port open.');
  }

  const { Cdp, pickPageTarget, targetsAt } = await import('../cdp.mjs');
  const targets = await targetsAt(state.cdpPort);
  if (!targets && state.debugPortOpen === false) {
    // `run` waited and said so; the same sentence here, rather than a guess at why.
    fail(`nothing is listening on the debug port ${state.cdpPort}: the window \`run\` started never opened it, `
      + 'as `run` said. `node tools/desktop.mjs restart` picks a port again.');
  }
  if (!targets) {
    fail(`nothing is listening on the debug port ${state.cdpPort} — the shell is not running, or was `
      + 'started without this tool. `node tools/desktop.mjs restart`.');
  }

  const target = pickPageTarget(targets, window);
  if (!target) {
    const which = window ? `no \`${window}\` window` : 'no main-window page';
    fail(`${which} on the debug port ${state.cdpPort}:\n${JSON.stringify(targets, null, 2)}`);
  }

  const cdp = await new Cdp(target.webSocketDebuggerUrl).open();

  /* IDENTIFY THE PAGE BEFORE REPORTING ITS ANSWER. The whole value of this instrument is that it
   * sees what nothing else can, so a reading taken in the wrong page is a claim about the desktop
   * that nobody can contradict. The bridge's mark exists only inside a Shenora host — it is what the
   * transport is built on — and the host the page reaches is the one this run started. */
  const page = await cdp.evaluate(PAGE_IDENTITY);
  if (!isShell(page, state.serviceUrl)) {
    cdp.close();
    const engine = page?.chromium ? 'a Chromium shell page' : page?.webview ? 'a WebView2' : 'a browser page';
    fail(
      `refusing: the page on ${state.cdpPort} is not this shell.\n`
      + `  expected the shell reaching ${state.serviceUrl ? new URL(state.serviceUrl).origin : '(any host)'}\n`
      + `  found     ${engine} at ${page?.origin ?? '(unknown)'}${page?.host ? ` reaching ${page.host}` : ''}`,
      1);
  }

  return cdp;
}

export async function build(args) {
  const configuration = args.includes('--release') ? 'Release' : 'Debug';

  // 🔴 An orphaned host from this checkout holds the assemblies this build has to overwrite, and
  // MSBuild says so in eleven lines ending `… Exceeded retry count of 10`. The trap is
  // already documented on `stopAll` below; naming it here is what makes the documentation reach the
  // person who hit it. NAMED, never killed: a host this tool did not start belongs to whoever did,
  // which is the same distinction the shell's own supervisor makes.
  const leftovers = running(assemblyExe(HTTP_PROJECT));
  if (leftovers.length) {
    fail(`${leftovers.length} service host(s) from this checkout are still running `
      + `(pid ${leftovers.join(', ')}), and they hold the assemblies this build overwrites.\n`
      + '  Close the shell — or `node tools/desktop.mjs kill`, which closes it so its own shutdown '
      + 'stops the host it owns.');
  }

  // The order is the dependency order and is not cosmetic: the platform builds INTO the host's
  // wwwroot, so a host built before the bundle serves the previous one, and the shell shows it.
  run('npm', ['--prefix', WEB, 'run', 'build'], { shell: true });
  buildProject(HTTP_PROJECT, configuration, { runner: run });
  // Daoris's browser is this same application started with the browser's argument, so
  // there is no second project to build.
  buildProject(DESKTOP_PROJECT, configuration, { runner: run });
  // The driver's program a scratch run's `daoris …` is handed to: its apphost, named by the run's environment.
  buildProject(DRIVER_HOST_PROJECT, configuration, { runner: run });
  buildProject(MCP_PROJECT, configuration, { runner: run });
  console.log('\nbuilt — `node tools/desktop.mjs run` to look at it.');
}

export async function start(command, args) {
  /* 🔴 The DEPLOYED shell, addressed by the folder it is published to. It implies `--real` and
     cannot mean anything else: an install has no `DAORIS_*` overrides and makes its own `data/` the
     Daoris home by construction — that is what makes it a deployment rather than a preview.
     What this adds is the debug port, and it adds it AT LAUNCH through the environment: the shipped
     application still exposes nothing. */
  const install = takeInstall(args);
  const exe = install ? installedExe(install) : guarded(assemblyExe(DESKTOP_PROJECT));
  if (install && !exe) {
    fail(`no \`${LAUNCHER}\` at the root of \`${install}\` — that is not an install.\n`
      + '  Point --install at the folder you published to (the one holding the launcher, `app/`\n'
      + '  and `data/`), not at its parent.');
  }
  if (!exe) fail('the shell is not built — `node tools/desktop.mjs build`.');

  // An update's swap replaces `app/` while nothing runs from it, and refuses a build it finds in use: a start waits it out.
  if (install) {
    try {
      await awaitSwap(install);
    } catch (error) {
      fail(error.message, 1);
    }
  }

  const live = applicationsAt(exe);
  if (live.length && command === 'restart') {
    stopAll(exe);
    console.log(`stopped pid ${live.join(', ')}`);
  } else if (live.length) {
    fail(`a shell from this checkout is already running (pid ${live.join(', ')}). `
      + '`node tools/desktop.mjs restart` replaces it.');
  }

  const real = args.includes('--real') || Boolean(install);
  const { answersOn, freePort } = await import('../cdp.mjs');
  const cdpPort = await freePort(9333);

  let environment = debugEnvironment(cdpPort);
  let serviceUrl = process.env.DAORIS_SERVICE_URL ?? 'http://localhost:5177';
  const extra = [];

  if (real) {
    console.log(install
      ? `⚠ --install: the DEPLOYED application, on its own home (${join(install, HOME)}).`
      : `⚠ --real: your own Daoris home (${process.env.DAORIS_HOME ?? 'DAORIS_HOME is not set — '
        + 'the shell will refuse'}) — your registry, your quests, your drivable set.`);
    console.log('  The driver loop starts with the app, so a drivable repository with an open quest');
    console.log('  gets a real agent session. This is the instance you use, not a copy of it.');
    if (install) {
      // Said plainly, because it is the one way this differs from double-clicking the launcher.
      console.log('  Started with a debug port so `shot`, `eval` and `click` can reach it. Nothing');
      console.log('  in the published application opens one — this run does, and only this run.');
      environment = installEnvironment(cdpPort);
    }
  } else {
    const plan = scratchPlan();
    const { home, appRoot } = plan;
    const family = join(scratchRoot, 'family');
    for (const folder of plan.folders) mkdirSync(folder, { recursive: true });

    if (args.includes('--fresh')) rmSync(family, { recursive: true, force: true });
    if (!existsSync(family)) {
      // The example family is the fixture: two adopters the store's bootstrap import registers
      // on first sight, so the window has something in it. COPIED rather than pointed at, because
      // the shell can write a repository's own `daoris.json` from Projects — and the tracked
      // examples are a gate's fixture, not a scratchpad. The MEMBERS, not the folder: `examples/`
      // also holds the example plugin, which is not a project and must not be imported as one.
      mkdirSync(family, { recursive: true });
      for (const name of ['engine', 'game']) copyTree(join(repoRoot, 'examples', name), join(family, name));
      console.log(`copied examples/{engine,game} -> ${family}`);
    }
    // The install's offers, laid out in this run's Daoris folder as a publish lays them in an install's, so the
    // Plugins place shows them here too. None is installed by this.
    layOffers(join(repoRoot, 'examples', 'plugins'), plan.root);

    const httpHost = guarded(assemblyExe(HTTP_PROJECT));
    if (!httpHost) fail('the service host is not built — `node tools/desktop.mjs build`.');
    // Optional: a run without it drives, and a session simply has no connector.
    const mcpHost = guarded(assemblyExe(MCP_PROJECT)) ?? '';
    // Optional too: a run without it is the window, and its terminal's `daoris …` says where the program is.
    const driverProgram = guarded(assemblyExe(DRIVER_HOST_PROJECT)) ?? '';

    serviceUrl = `http://127.0.0.1:${await freePort(5188)}`;
    environment = scratchEnvironment({ home, family, serviceUrl, httpHost, mcpHost, cdpPort, driverProgram });
    // Its own root, the same folder as the home, so the engine's profile, the window geometry and the runtime's
    // single-instance scope all belong to this run — a scratch shell and a real one never contend for either.
    extra.push('--app-root', appRoot);
  }

  const env = { ...process.env, ...environment };
  if (!real) for (const name of CLEARED) delete env[name];

  // console-window: the desktop's own window, which a person is meant to see
  const child = spawn(exe, extra, { env, detached: true, stdio: 'ignore' });
  // Heard while `run` waits for the port below, so an application that ended is said at once.
  let ended = null;
  child.on('exit', (code, signal) => { ended = { code, signal }; });
  child.unref();

  // Written before the wait, so `kill` and `restart` reach a window whose port never opens.
  mkdirSync(scratchRoot, { recursive: true });
  const record = { pid: child.pid, exe, serviceUrl, cdpPort, real, started: new Date().toISOString() };
  writeFileSync(RUN_FILE, `${JSON.stringify(record, null, 2)}\n`);

  const home = install ? join(install, HOME) : real ? process.env.DAORIS_HOME : scratchPlan().home;
  console.log(`shell started (pid ${child.pid})`);
  console.log(`  platform   ${serviceUrl}`);
  console.log(`  debug port ${cdpPort}`);
  console.log(`  machine    ${real ? `${home ?? '(no DAORIS_HOME)'} — YOUR OWN` : home}`);

  // The port asked for is not a port opened: wait until it answers, or say why it did not.
  const outcome = await awaitDebugPort({ answers: () => answersOn(cdpPort), gone: () => ended });
  writeFileSync(RUN_FILE, `${JSON.stringify({ ...record, debugPortOpen: outcome.open }, null, 2)}\n`);
  if (!outcome.open) {
    fail(closedPortReport({
      port: cdpPort, waitedMs: outcome.waitedMs, gone: outcome.gone, pid: child.pid,
      engineLog: engineLogOf({ install, real, exe }), home,
    }));
  }
  console.log(`  debug port answering after ${Math.round(outcome.waitedMs / 1000)}s`);
}

export function doctor() {
  const shell = assemblyExe(DESKTOP_PROJECT);
  const host = assemblyExe(HTTP_PROJECT);
  const bundle = join(HTTP_PROJECT, 'wwwroot', 'index.html');
  // Where `publish:service --install` lands one — and no home means none installed.
  const installed = process.env.DAORIS_HOME
    ? homeEntry(process.env.DAORIS_HOME, 'bin', 'daoris-knowledge-http', 'daoris-knowledge-http.exe')
    : null;
  const state = readRun();

  console.log('built');
  console.log(`  platform bundle  ${existsSync(bundle) ? ago(bundle) : 'absent — `build`'}`);
  console.log(`  service host     ${host ? ago(host) : 'absent — `build`'}`);
  console.log(`  shell            ${shell ? ago(shell) : 'absent — `build`'}`);

  console.log('\nrunning');
  const live = process.platform === 'win32' ? applicationsAt(shell) : [];
  console.log(`  this checkout    ${live.length ? `pid ${live.join(', ')}` : 'nothing'}`);
  // A host with no shell is usually an orphan — a shell that died without its teardown. It is NOT
  // killed here: this tool did not start it, and it may be a gate's. Named, so the person can act.
  const hosts = process.platform === 'win32' ? running(host) : [];
  if (hosts.length && !live.length) {
    console.log(`  ⚠ service host   pid ${hosts.join(', ')} — running with no shell of this checkout.`);
    console.log('    It holds the port and a lock on the assemblies `build` overwrites. Stop it if it');
    console.log('    is yours: Get-Process daoris-knowledge-http | Stop-Process');
  }
  if (state) {
    console.log(`  last run         ${state.started} · ${state.serviceUrl} · debug ${state.cdpPort}`
      + `${state.real ? ' · --real' : ''}`);
  }

  console.log('\nwhat a scratch run would use');
  console.log(`  machine          ${scratchPlan().home} (also its --app-root)`);
  console.log(`  family           ${join(scratchRoot, 'family')}`
    + `${existsSync(join(scratchRoot, 'family')) ? '' : ' (copied from examples/ on first run)'}`);
  console.log(`  service host     ${host ?? '(none built)'}`);
  if (installed && existsSync(installed)) {
    console.log('  ⚠ an installed service host exists on this machine.');
    console.log('    ServiceHostLocator prefers it over this workspace, which is why `run` names the');
    console.log('    built one explicitly — a shell started any other way serves that one instead.');
  }
}

/**
 * What `eval` evaluates: its words joined, or with `--file <script>` that file's text, whole. The screen
 * counter's script (`ux-count.mjs --window`) is longer than the line `npm run` hands cmd.exe holds, and its quotes
 * do not survive the shells between, so a script travels as a file. A byte-order mark is dropped. Null when there
 * is nothing to evaluate, or `--file` is not the only thing said; a file that cannot be read throws, naming it.
 */
export function evalExpression(args) {
  const at = args.indexOf('--file');
  if (at === -1) return args.join(' ') || null;
  if (args.length !== 2 || at !== 0) return null;
  let text;
  try {
    text = readFileSync(args[1], 'utf8');
  } catch (error) {
    throw new Error(`cannot read ${args[1]}: ${error.message}`);
  }
  return text.replace(/^﻿/, '') || null;
}
