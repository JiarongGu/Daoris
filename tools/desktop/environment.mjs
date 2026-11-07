import { join } from 'node:path';
import { homeEntry } from '../../src/Daoris.Cli/src/platform/home.ts';
import { PLUGIN_OFFERS } from '../desktop-publish.mjs';
import { scratchDaoris } from './paths.mjs';

/** The folders of a Daoris folder besides its files, in the order the design §2 gives them. */
const DAORIS_FOLDERS = ['app', 'data', 'profiles', 'work', 'cache', 'run', 'logs'];

/** What `run` lays out for a scratch run: the one root, and the places the readers will ask of it. Pure; `run` makes the folders. */
export function scratchPlan(root = scratchDaoris) {
  return {
    root,
    home: root,
    appRoot: root,
    folders: DAORIS_FOLDERS.map((folder) => join(root, folder)),
    window: homeEntry(root, 'window'),
    offers: join(root, ...PLUGIN_OFFERS),
  };
}

/**
 * The machine-local files the desktop reads, and the variable that moves each one.
 *
 * ⚠ THIS LIST IS HALF OF A PAIR. The other half is the code that reads them — `DriverConfig`,
 * `RemoteTarget`, `Harnesses`, the store and the index root — and a name missing here does not fail:
 * it silently writes into the person's real Daoris home. A test asserts the two agree, because the
 * failure this prevents is invisible until somebody's driver config has a repository in it they never
 * opted in.
 */
export const REDIRECTED = [
  // The home itself: every machine-local default derives from it, so a scratch run points it
  // at scratch before anything else — a new file under the home is then scratch by construction.
  'DAORIS_HOME',
  'DAORIS_KNOWLEDGE_DB',
  'DAORIS_KNOWLEDGE_ROOT',
  'DAORIS_REMOTE_CONFIG',
  'DAORIS_HARNESS_CONFIG',
];

/**
 * Variables a scratch run must UNSET, not override.
 *
 * The remote trio is an environment PAIR that replaces the whole machine's remotes map — that is what
 * makes the rehearsals hermetic, and inherited by a dev run it does the opposite: the scratch
 * shell's sync loop would feed a real deployment from a scratch store. A key is the same shape of
 * mistake one layer down. Redirecting them is not enough, because the file only governs when the
 * environment is silent.
 *
 * The rules home is where a session's proposal to change the rules is written. The driver
 * sets it on every connector it hands over, to its own home. A connector it did not hand, such as a
 * repository's own `.mcp.json` in a pipe-door session, inherits the environment instead, and an inherited
 * one would file a scratch session's proposal among the real machine's. Unset, it falls back to the
 * redirected `DAORIS_HOME`.
 *
 * The instance names the running Daoris's pipe that a verb sends its
 * press to. Inherited, a `daoris` run from the scratch shell's terminal would press the real
 * instance's routes; unset, a workspace build reads no address file and acts alone on the scratch home.
 *
 * The machine's heavy-run admission is read from two more. Its capacity override, inherited,
 * would size the scratch home's admission by the real machine's setting; the held place a driver hands a
 * heavy run's children, inherited, would let the scratch desktop's runs count themselves inside a place
 * the real machine's admission holds, and wait for nothing.
 */
export const CLEARED = [
  'DAORIS_REMOTE_URL',
  'DAORIS_REMOTE_KEY',
  'DAORIS_REMOTE_WORKSPACE',
  'DAORIS_SERVICE_KEY',
  'DAORIS_RULES_HOME',
  'DAORIS_INSTANCE',
  'DAORIS_ADMISSION_SLOTS',
  'DAORIS_ADMISSION_HOLD',
];

/**
 * The environment a scratch run needs, whole.
 *
 * ⚠ `DAORIS_HTTP_HOST` is not an optimisation. `ServiceHostLocator` prefers an INSTALLED host in
 * the home's `bin/` over this workspace's build, so a dev shell on a machine that has ever run
 * `npm run publish:service -- --install` brings up yesterday's binary serving yesterday's bundle, and
 * every change appears to do nothing. Naming the built host is what makes the loop honest.
 *
 * ⚠ The port is not the default one either. `HostSupervisor` ADOPTS a host already answering rather
 * than double-starting it (correctly — killing someone else's server is the process version of
 * writing into their tree), so a scratch run on :5177 would quietly adopt the person's real host and
 * show their real store while claiming to be a scratch machine.
 */
/* The script `click` sends. A script's element.click() moves no focus, while a pointer's click in
 * Chromium focuses the element or its nearest focusable ancestor first — so a focus judged after a
 * bare click is not a person's. Focus first, click, then say
 * where the focus is. `eval` stays raw: it runs any script, and a click inside it moves no focus. */
export function clickScript(selector) {
  return `(() => {
    const found = [...document.querySelectorAll(${JSON.stringify(selector)})];
    if (found.length !== 1) return { matched: found.length };
    const element = found[0];
    const label = (node) => (node.innerText || node.value || '').trim().slice(0, 80);
    element.scrollIntoView({ block: 'center' });
    const focusable = element.closest('a[href],button,input,select,textarea,summary,[tabindex],[contenteditable="true"]');
    if (focusable) focusable.focus();
    element.click();
    const active = document.activeElement;
    return {
      matched: 1,
      clicked: (label(element) || element.tagName).trim(),
      focus: active ? (active.tagName + (label(active) ? ' "' + label(active) + '"' : '')) : 'none',
    };
  })()`;
}

export function debugEnvironment(cdpPort) {
  if (!cdpPort) return {};
  return {
    // Both halves are needed. The Chromium the shell ships opens the DevTools port the shell
    // hands it only in development, which is how a shipped window has nothing to attach to. So dev
    // mode is not a preference here: it is the switch that lets the port through.
    DOTNET_ENVIRONMENT: 'Development',
    DAORIS_DEVTOOLS_PORT: String(cdpPort),
  };
}

/**
 * A run of a deployed install: the debug port's two halves, and the host held in production. The port needs the kit's
 * development switch, and the host the shell starts inherits it; in development ASP.NET answers a bad request with its
 * exception page, a build machine's source paths in it. An installed host runs in production, as the deployment rehearsal
 * keeps it (FIX-LOG 2026-10-04).
 */
export function installEnvironment(cdpPort) {
  return { ...debugEnvironment(cdpPort), ASPNETCORE_ENVIRONMENT: 'Production' };
}

export function scratchEnvironment({ home, family, serviceUrl, httpHost, mcpHost, cdpPort, driverProgram }) {
  return {
    DAORIS_SERVICE_URL: serviceUrl,
    // The other half of the port, and it is not optional. The shell PROBES `DAORIS_SERVICE_URL` and
    // the host BINDS `ASPNETCORE_URLS` (defaulting to :5177) — nothing passes one to the other, so a
    // scratch run that moved only the probe waits on a host answering somewhere else and sits on the
    // splash forever. Found on this tool's first real run.
    ASPNETCORE_URLS: serviceUrl,
    DAORIS_HOME: home,
    DAORIS_KNOWLEDGE_DB: homeEntry(home, 'knowledge'),
    DAORIS_KNOWLEDGE_ROOT: family,
    DAORIS_REMOTE_CONFIG: homeEntry(home, 'remotes'),
    DAORIS_HARNESS_CONFIG: homeEntry(home, 'harnesses'),
    DAORIS_HTTP_HOST: httpHost,
    // The MCP host a driven session is handed over the protocol door. Pinned for the same
    // reason the HTTP one is: the locator prefers an INSTALLED binary, so a scratch run would hand
    // the session the real machine every time this one is behind it.
    DAORIS_MCP_HOST: mcpHost,
    // The driver's program a `daoris …` typed in this run's terminal is handed to: this checkout's
    // build, never one the parent shell named; with none built, none, and the CLI says where the program is.
    DAORIS_DRIVER_PROGRAM: driverProgram ?? '',
    // Nothing in this repository reads these: the debug port is the runtime's and the loader's, which
    // is the point — the shipped app gains no debug surface, and a window nobody started this way has
    // nothing to attach to. Loopback only, on a port picked for this run.
    ...debugEnvironment(cdpPort),
  };
}
