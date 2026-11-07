#!/usr/bin/env node
/**
 * The desktop shell's dev loop: build it, run it on a machine of its own, look at it, ask it things.
 *
 * WHY THIS EXISTS. Every other surface in this repository has a loop that can see it — the CLI has
 * `npm test`, the service has its suite, the platform has vitest over a mocked bridge and Playwright
 * over the real bundle. The shell has neither: Playwright cannot reach it (the bridge is absent in a
 * browser, by construction — `docs/2026-09-19-frontend-architecture.md` §4), and until REV2 nothing
 * tested it at all, which is how 1,128 lines came to answer every refusal with a blank failure. This
 * is not a gate and does not pretend to be one; it is the instrument a person or an agent uses to
 * START the thing and SEE it, which is the step that was being done by hand or not at all.
 *
 * node tools/desktop.mjs doctor what this machine has, and what a run would use
 * node tools/desktop.mjs build the platform bundle, the host, then the shell
 * node tools/desktop.mjs run start it on a scratch machine, debug port attached
 * node tools/desktop.mjs run --install <dir> start the DEPLOYED one there, on your real machine
 * node tools/desktop.mjs shot overview capture the window
 * node tools/desktop.mjs eval "document.title" (or --file <script.js>)
 * node tools/desktop.mjs click "[data-nav=quests]"
 * node tools/desktop.mjs kill | restart
 *
 * TWO RULES SHAPE ALL OF IT.
 *
 * **A dev run gets its own machine.** The shell is not a viewer: it runs the driver loop, and the
 * driver SPAWNS AGENT SESSIONS in real repositories against real quests. Pointed at the machine's
 * Daoris home it is the person's actual Daoris, with their registry, their quests and their drivable
 * set — so `run` redirects every machine-local file, takes its own port, and copies `examples/` to
 * work over. The real one is `--real`, spelled out, because reaching it should be a sentence somebody
 * typed.
 *
 * **It only ever touches what this checkout built.** `kill` matches the executable's PATH, never the
 * process name; `shot` photographs that same path. An installed Daoris and this one are two programs
 * that happen to share a name, and every family sibling that skipped this lesson paid it twice — once
 * killing the wrong window, once photographing it.
 */
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';
import { applicationsAt, commandLinesAt, stopProcesses } from './processes.mjs';
import { SHOTS, pruneShots, readRun, startedHere, takeWindow, targetExe } from './desktop/session.mjs';
import { fail, run } from './desktop/run.mjs';
import { attach, build, doctor, evalExpression, start } from './desktop/commands.mjs';
import { shotTarget, withPageTheme } from './desktop/page.mjs';
import { repoRoot } from './desktop/paths.mjs';
import { clickScript } from './desktop/environment.mjs';
export { repoRoot, scratchRoot, scratchDaoris } from './desktop/paths.mjs';
export { scratchPlan, REDIRECTED, CLEARED, clickScript, debugEnvironment, installEnvironment, scratchEnvironment } from './desktop/environment.mjs';
export { assemblyExe, installedExe, prune, awaitDebugPort, engineLogOf, closedPortReport } from './desktop/executables.mjs';
export { startedHere } from './desktop/session.mjs';
export { SHELL_ORIGIN, isShell, PAGE_IDENTITY, THEME_KEY, PAGE_THEME, themeChoiceExpression, withPageTheme, shotTarget } from './desktop/page.mjs';
export { evalExpression } from './desktop/commands.mjs';

// Every home place is asked of the home layout's table, never joined here.
// The install's layout, from the script that makes it: one launcher at the root, the home in `data/`.

function usage() {
  console.log(readFileSync(fileURLToPath(import.meta.url), 'utf8')
    .split('\n')
    .slice(1, 21)
    .map((line) => line.replace(/^ \*\/?/, '').replace(/^ /, ''))
    .join('\n'));
}

/**
 * Only when this file is what runs. The helpers above are imported by the test suite, and a
 * module that dispatched on `process.argv` at import time would answer `node --test`'s arguments.
 */
async function main(command, args) {
  if (process.platform !== 'win32' && command !== 'doctor') {
    fail('the shell is a Windows application (net10.0-windows) — this loop only runs there.');
  }

  switch (command) {
    case 'build':
      await build(args);
      break;

    case 'run':
    case 'restart':
      await start(command, args);
      break;

    case 'kill': {
      const live = startedHere(applicationsAt(targetExe()), readRun());
      if (!live.length) {
        console.log('no shell this loop started is running.');
        break;
      }

      stopProcesses(live);
      console.log(`stopped pid ${live.join(', ')}`);
      break;
    }

    case 'shot': {
      const exe = targetExe();
      if (!exe) fail('the shell is not built — nothing to photograph.');

      /* `--theme light|dark` photographs the OTHER theme without touching the machine's setting.
       *
       * It exists because the window's own chrome — the DWM border and the caption buttons — is
       * painted NATIVELY from whatever the page last pushed over SET_THEME, so it is
       * invisible to every other instrument: the page cannot see it, and a CSS-level check cannot
       * either. Emulating the media query makes the page push the other theme, and the window
       * repaints for real.
       *
       * The page follows the emulation only while the viewer's choice is System. A viewer who chose
       * light or dark has theirs set for the capture and put back after it, and a page that still
       * is not in the theme is refused rather than photographed (`withPageTheme`). It holds
       * for `--page` too.
       *
       * 🔴 The emulation is scoped to the CDP SESSION and is reverted the moment it closes. A probe
       * that set it, closed, and then captured reported dark and photographed a light window — so
       * the capture happens HERE, while the connection is still open. */
      const themeFlag = args.indexOf('--theme');
      const theme = themeFlag === -1 ? null : args[themeFlag + 1];
      if (themeFlag !== -1) {
        if (!['light', 'dark'].includes(theme)) fail('usage: shot [name] --theme <light|dark>');
        args.splice(themeFlag, 2);
      }

      /* `--window <name>` photographs a SECONDARY window — `monitor`, or `session:<id>` —
       * rather than the main one, or `browser`, Daoris's own browser beside the application.
       *
       * It exists because `Process.MainWindowHandle` answers for exactly one window and Windows
       * chooses which, so with the monitor open a capture silently photographs whichever the OS
       * calls main. A name that matches no open window is refused rather than falling back: the
       * whole point of asking is that the main window is not the one wanted. Which PROCESS holds the
       * window is chosen before that, below, and never by Windows. */
      const window = takeWindow(args);

      /* `--page [--size WxH]` asks Chromium for the PAGE over the debug port instead of photographing
       * the window. A minimized window photographs as its 314 × 50 caption, and restoring an
       * installed one puts it in front of its owner; the page renders without either. `--size` lays it
       * out at a width and height for the capture and puts it back after. It is the page alone: the
       * native frame and caption buttons are not in it, which is what the window capture is for. */
      const page = args.indexOf('--page');
      const sizeFlag = args.indexOf('--size');
      const size = sizeFlag === -1 ? null : /^(\d+)x(\d+)$/.exec(args[sizeFlag + 1] ?? '');
      if (sizeFlag !== -1) {
        if (!size) fail('usage: shot [name] --page [--size <width>x<height>]');
        args.splice(sizeFlag, 2);
      }
      if (page !== -1) args.splice(args.indexOf('--page'), 1);

      const name = (args[0] ?? `shell-${new Date().toISOString().slice(11, 19).replaceAll(':', '')}`)
        .replace(/[^\w.-]/g, '-');

      if (page !== -1) {
        const cdp = await attach(window);
        let refusal = null;
        try {
          if (size) {
            await cdp.send('Emulation.setDeviceMetricsOverride', {
              width: Number(size[1]), height: Number(size[2]), deviceScaleFactor: 1, mobile: false,
            });
            await new Promise((resolve) => setTimeout(resolve, 600));
          }
          const capturePage = async () => {
            const shot = await cdp.send('Page.captureScreenshot', { format: 'png' });
            mkdirSync(SHOTS, { recursive: true });
            writeFileSync(join(SHOTS, `${name}.png`), Buffer.from(shot.data, 'base64'));
            console.log(`captured the page -> ${join(SHOTS, `${name}.png`)}`);
          };
          if (theme) await withPageTheme(cdp, theme, capturePage);
          else await capturePage();
        } catch (error) {
          refusal = error.message;
        } finally {
          if (size) await cdp.send('Emulation.clearDeviceMetricsOverride').catch(() => {});
          cdp.close();
        }
        if (refusal) fail(refusal, 1);
        pruneShots();
        break;
      }

      // The browser runs from the application's own executable, with windows of its
      // own, so the path and the process name are both of theirs: the process is chosen here, by its
      // command line, and handed to the script by id. Left to the script, the first of the
      // two with a window is Windows' choice, and on an install that is the browser.
      const target = shotTarget(commandLinesAt(exe), { window, record: readRun(), exe });
      if (target.refusal) fail(target.refusal);
      if (target.others) {
        console.log(`${target.others.length + 1} applications run from ${exe} and the last run recorded none of `
          + `them; photographing pid ${target.pid}, not ${target.others.join(', ')}.`);
      }
      const whose = ['-ProcessName', basename(exe, '.exe'), '-ExePath', exe, '-ProcessId', String(target.pid)];
      const captureArgs = ['-NoProfile', '-ExecutionPolicy', 'Bypass',
        '-File', join(repoRoot, 'tools', 'shot-window.ps1'),
        ...whose,
        ...(target.title ? ['-WindowTitle', target.title] : []),
        '-OutFile', join(SHOTS, `${name}.png`)];

      if (theme) {
        // Thrown, not exited: `run` would end the process inside the capture and skip putting the
        // viewer's theme choice back.
        const capture = async () => {
          const result = spawnSync('powershell', captureArgs, { stdio: 'inherit', shell: false, windowsHide: true });
          if (result.error) throw new Error(`powershell did not start: ${result.error.message}`);
          if (result.status !== 0) throw new Error(`the capture exited ${result.status}`);
        };
        const cdp = await attach(window);
        let refusal = null;
        try {
          await withPageTheme(cdp, theme, capture);
        } catch (error) {
          refusal = error.message;
        } finally {
          cdp.close();
        }
        if (refusal) fail(refusal, 1);
      } else {
        run('powershell', captureArgs);
      }

      pruneShots();
      break;
    }

    case 'eval': {
      const window = takeWindow(args);
      let expression;
      try {
        expression = evalExpression(args);
      } catch (error) {
        fail(error.message);
      }
      if (!expression) fail('usage: node tools/desktop.mjs eval [--window <name>] ("<js expression>" | --file <script.js>)');

      const cdp = await attach(window);
      try {
        console.log(JSON.stringify(await cdp.evaluate(expression), null, 2));
      } catch (error) {
        cdp.close();
        fail(`the expression threw: ${error.message}`, 1);
      }

      cdp.close();
      break;
    }

    case 'click': {
      const window = takeWindow(args);
      const selector = args.join(' ');
      if (!selector) fail('usage: node tools/desktop.mjs click [--window <name>] "<css selector>"');

      const cdp = await attach(window);
      /* One element or none. A selector matching three things and clicking the first is how a loop
       * reports success for an interaction that never happened — so the COUNT is the answer, and a
       * miss is a refusal rather than a silent first-match. The click is the page's own, dispatched
       * where the page's handlers are; nothing here simulates a cursor. */
      const outcome = await cdp.evaluate(clickScript(selector));
      cdp.close();

      if (outcome.matched !== 1) {
        fail(`${selector} matched ${outcome.matched} elements — a click needs exactly one.`, 1);
      }

      console.log(`clicked: ${outcome.clicked}`);
      console.log(`focus after: ${outcome.focus}`);
      break;
    }

    case 'doctor':
      doctor();
      break;

    default:
      usage();
      if (command) process.exit(2);
  }
}

if (isMain(import.meta.url)) {
  await main(process.argv[2], process.argv.slice(3));
}
