import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { homeEntry } from '../../src/Daoris.Cli/src/platform/home.ts';
import { LAUNCHER, RETIRED_LAUNCHERS, SHELL_EXE, SHELL_HOME } from '../desktop-publish.mjs';
import { scratchPlan } from './environment.mjs';
import { run } from './run.mjs';

/**
 * A project's built executable — derived, never written down.
 *
 * The sibling's config file carries the target framework inside the path with a warning that it MUST
 * be updated whenever the project retargets, because getting it wrong launches the binary still
 * sitting in the old folder and every change appears to do nothing. Reading the assembly name from
 * the project and globbing for the build removes the thing that needed remembering: a retarget moves
 * the folder, the glob follows, and "not built" stays distinguishable from "built elsewhere".
 */
export function assemblyExe(projectDir, { flavours = ['Debug', 'Release'] } = {}) {
  const project = readdirSync(projectDir).find((file) => file.endsWith('.csproj'));
  if (!project) return null;

  const name = /<AssemblyName>([^<]+)<\/AssemblyName>/
    .exec(readFileSync(join(projectDir, project), 'utf8'))?.[1];
  if (!name) return null;

  // On the Chromium the shell ships, the exe is CEF's launcher, named from the assembly less
  // `.App`, and the build lands in a runtime identifier's folder. 🔴 The launcher is copied with CEF's
  // own file date, older than any build of ours, so it counts only with the assembly it starts beside
  // it, and is dated by that assembly: dated by itself, a stale build in the folder above would win.
  const launched = name.endsWith('.App');
  const exeName = launched ? name.slice(0, -'.App'.length) : name;
  const found = [];
  const consider = (folder) => {
    const exe = join(folder, `${exeName}.exe`);
    if (!existsSync(exe)) return;
    const dated = launched ? join(folder, `${name}.dll`) : exe;
    if (existsSync(dated)) found.push({ exe, at: statSync(dated).mtimeMs });
  };
  for (const flavour of flavours) {
    const flavourDir = join(projectDir, 'bin', flavour);
    if (!existsSync(flavourDir)) continue;
    for (const framework of readdirSync(flavourDir)) {
      const frameworkDir = join(flavourDir, framework);
      if (!statSync(frameworkDir).isDirectory()) continue;
      consider(frameworkDir);
      for (const rid of readdirSync(frameworkDir)) {
        const ridDir = join(frameworkDir, rid);
        if (statSync(ridDir).isDirectory()) consider(ridDir);
      }
    }
  }

  // Newest wins: a Release build made after a Debug one is what the person last asked for.
  found.sort((a, b) => b.at - a.at);
  return found[0]?.exe ?? null;
}

/**
 * The launcher inside a published install, or null when that folder is not one.
 *
 * @remarks
 * 🔴 **The way to look at a deployed shell.** `shot`, `eval` and `click` find a window by THIS
 * checkout's executable path, so a deployed shell — a different path, and no debug port — can
 * only be looked at by a person, unless a deliberate opt-in flag addresses the install:
 * **develop against the install**, because the desktop is where the capability is.
 *
 * **The launcher is at the ROOT, never guessed one level down.** An install shows one thing to
 * double-click and hides the rest under `app/` — so a folder with no launcher at its root is not an
 * install, and answering null beats launching something that happens to be nearby. The first
 * deployment's own folder is `<family>/app`, which is exactly the shape that makes "point it at the
 * folder you published to" easy to get wrong.
 */
export function installedExe(directory) {
  if (!directory) return null;
  // The application the launcher starts: what holds the window, the debug port and the files,
  // so what every instrument addresses. An older install has the single-file shell at
  // its root instead, until its next publish replaces it.
  if (existsSync(join(directory, LAUNCHER))) {
    const shell = join(directory, ...SHELL_HOME, SHELL_EXE);
    return existsSync(shell) ? shell : null;
  }
  return RETIRED_LAUNCHERS.map((name) => join(directory, name)).find(existsSync) ?? null;
}

/**
 * Which captures a prune would drop: keep the newest `keep`, and stay under `maxBytes`.
 *
 * A window capture is a full-resolution lossless PNG — several megabytes each — and the sibling's
 * folder had quietly reached 294 MB before anyone measured it. Every capture prunes afterwards, so
 * the policy applies whether or not someone remembers it.
 */
export function prune(entries, { keep = 25, maxBytes = 150 * 1024 * 1024 } = {}) {
  const newestFirst = [...entries].sort((a, b) => b.at - a.at);
  const drop = newestFirst.slice(keep);
  let kept = 0;
  for (const entry of newestFirst.slice(0, keep)) {
    kept += entry.size;
    if (kept > maxBytes) drop.push(entry);
  }

  return drop;
}

/**
 * Wait for the debug port a run asked for, and say how the wait ended.
 *
 * 🔴 The engine opens its port only in development, and when it cannot bind the port it runs on without
 * one. So a started window is no evidence of a port: printing the port asked for would leave
 * `shot` to find nothing listening there. The application ending before the port answered is said at
 * once, since nothing is left to wait for.
 *
 * @param answers - whether anything answers on the port now.
 * @param gone - null while the application runs, `{ code, signal }` once it has ended.
 * @returns `{ open, waitedMs, gone }`, where `gone` is null unless the application ended.
 */
export async function awaitDebugPort({
  answers, gone, limitMs = 30_000, stepMs = 500,
  now = Date.now, sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms)),
}) {
  const started = now();
  for (;;) {
    if (await answers()) return { open: true, waitedMs: now() - started, gone: null };
    const ended = gone();
    if (ended) return { open: false, waitedMs: now() - started, gone: ended };
    if (now() - started >= limitMs) return { open: false, waitedMs: now() - started, gone: null };
    await sleep(stepMs);
  }
}

/**
 * Where the engine's own log is for a run: CEF keeps it in the kit's `chromium` data area. For an install that is the
 * window's memory: the install's home's `window` entry, which `Program.cs` hands the kit as its
 * data area: `profiles/window/` once moved, the install's `data/` while an older build's engine profile is still there.
 * A scratch run's is the same entry of its one root (`scratchPlan`), handed the kit the same way. Otherwise, for `--real`,
 * the kit's own `data/` under the built executable's folder.
 */
export function engineLogOf({ install, real, exe }) {
  if (install) return join(homeEntry(install, 'window'), 'chromium', 'cef.log');
  if (!real) return join(scratchPlan().window, 'chromium', 'cef.log');
  return join(dirname(exe), 'data', 'chromium', 'cef.log');
}

/** What `run` says when the debug port it asked for never answered, from `awaitDebugPort`'s outcome. */
export function closedPortReport({ port, waitedMs, gone, pid, engineLog, home }) {
  if (gone) {
    const how = Number.isInteger(gone.code) ? `exited ${gone.code}` : `ended (${gone.signal ?? 'no exit code'})`;
    return [
      `⚠ the application (pid ${pid}) ${how} before its debug port ${port} opened, so this run has no window.`,
      '  A Daoris already running from the same folder takes a later start and ends it, and that window has',
      '  no debug port: close it (`kill` closes one this tool started), then `run` again. Otherwise the machine',
      `  log in ${home ? homeEntry(home, 'logs') : join('(the home)', 'logs')} says why it ended.`,
    ].join('\n');
  }
  return [
    `⚠ the window started WITHOUT its debug port: nothing listens on ${port} (127.0.0.1 or [::1]) `
      + `${Math.round(waitedMs / 1000)}s after the start,`,
    '  so `shot`, `eval` and `click` cannot reach it. The engine opens the port only in development, and when',
    '  it cannot bind the port it runs on without one. Try:',
    '    node tools/desktop.mjs restart   to pick a port again and start a new window',
    `    the engine's log, ${engineLog}, for why it did not listen`,
    '    netsh interface ipv4 show excludedportrange protocol=tcp   for whether Windows reserves the port:',
    '      nobody can bind a reserved port, and `run` now passes one over (LOOK4)',
  ].join('\n');
}

// ---------------------------------------------------------------------------------------------
// Everything below runs; everything above is asserted.
