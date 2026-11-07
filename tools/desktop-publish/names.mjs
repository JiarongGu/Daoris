import { existsSync, readFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { HOSTS, executableFor, publishCommand } from '../service-publish.mjs';

/** What says this folder is ours to overwrite. Written on every publish; read before any. */
export const MARKER = 'INSTALLED.md';
export const MARKER_HEADER = '# Daoris — installed desktop';

/**
 * The install's layout, stated once: one launcher at the root, the application and the
 * host under `app/`, the home in `data/`. The deployment gate, the dev loop and the testbed
 * read these rather than spelling the names again.
 *
 * `Daoris.exe`: the one thing to run is named for the application, as a regular one is.
 */
export const LAUNCHER = 'Daoris.exe';

/**
 * Where the application sits, and its name there: CEF's launcher, beside its Chromium. A twin
 * of `Launcher.AppFolder` and `Launcher.ShellExe` (`Daoris.Desktop.Launcher`), which starts it, and of
 * the app's assembly name, which names it; `desktop-publish.test.ts` reads both.
 */
export const SHELL_HOME = Object.freeze(['app']);
export const SHELL_EXE = 'Daoris.Desktop.exe';

/**
 * Launchers an earlier publish wrote at the root: the single-file shell. A publish into
 * its own install removes them, and the dev loop still finds an install not yet republished.
 */
export const RETIRED_LAUNCHERS = Object.freeze(['daoris-desktop.exe']);

/**
 * Folders an earlier publish wrote under `app/` and this one does not: Daoris's browser as
 * an executable of its own (`daoris-browser.exe`) with a second Chromium beside it. The
 * application is the browser. A publish into its own install removes them by name, as it
 * removes {@link RETIRED_LAUNCHERS}: exactly what it once wrote, never a folder it did not.
 */
export const RETIRED_IN_APP = Object.freeze(['daoris-browser']);

/** The retired browser's executable inside its folder: a running one holds the folder a republish removes. */
export const RETIRED_BROWSER_EXE = 'daoris-browser.exe';

/**
 * What a publish into `install` removes before it writes, because an earlier publish wrote it and
 * this one does not: {@link RETIRED_LAUNCHERS} at the root and {@link RETIRED_IN_APP} under `app/`.
 * None in a folder this script never published, which holds nothing of its to remove.
 */
export function retiredPaths(install) {
  if (!isInstall(install)) return [];
  return [
    ...RETIRED_LAUNCHERS.map((name) => join(install, name)),
    ...RETIRED_IN_APP.map((name) => join(install, ...SHELL_HOME, name)),
  ];
}

/**
 * The names the shell's last publish put in `app/`, one per line. What makes a republish
 * remove the previous engine's files and nothing it did not write.
 */
export const SHELL_FILES = Object.freeze(['app', 'shell-files.txt']);

/**
 * Where `--service` puts the HTTP host inside an install, as path segments.
 *
 * 🔴 One half of a counterpart set: this layout and `ServiceHostLocator`'s candidate
 * list must be the same layout, and only a deployment reads both.
 * `deployment-rehearsal.test.ts` reads the locator for the other half.
 */
export const HOST_HOME = Object.freeze(['app', 'daoris-knowledge-http']);

/** The service host's file name inside {@link HOST_HOME}. */
export const HOST_EXE = 'daoris-knowledge-http.exe';

/**
 * Where `--service` puts the knowledge connector inside an install, as path segments: the MCP host every
 * protocol-door session is handed, beside the HTTP host and built with it.
 *
 * 🔴 An install that carried only its HTTP host handed every session the home's `bin/` copy, which one
 * `publish:service --install` laid down and no republish refreshed: eight days old on the install that found it, it
 * rebuilt the shared store at its own older schema. A folder of its own under `app/`, for three reasons: it is the
 * folder `service-publish` publishes this host into, so one recipe lays both out; the publish replaces it whole, as it
 * does the HTTP host's; and `app/bin/`, the one other folder of executables, is the doctrine tool's and goes first on
 * every child's PATH. The other half of this counterpart set is `KnowledgeConnector.Candidates`, which
 * `deployment-rehearsal.test.ts` reads.
 */
export const CONNECTOR_HOME = Object.freeze(['app', 'daoris-knowledge']);

/** The connector's file name inside {@link CONNECTOR_HOME}. */
export const CONNECTOR_EXE = 'daoris-knowledge.exe';

/** The runtime an install is published for: the shell is a Windows application. */
export const INSTALL_RID = 'win-x64';

/**
 * What `--service` lays out, as a plan: each host `service-publish` makes, the folder under `app/` named for
 * its binary, its executable there, and the command that builds it, which is that script's one recipe.
 */
export function servicePlan(root, rid = INSTALL_RID) {
  return HOSTS.map((host) => {
    const folder = join(root, SHELL_HOME[0], host.binary);
    return { host, folder, executable: join(folder, executableFor(host.binary, rid)), command: publishCommand(host, rid, folder) };
  });
}

/**
 * The service hosts `install` carries under `app/`, as each one's folder relative to the install with `/`:
 * every host {@link servicePlan} lays out whose executable is there. Both locators run these ahead of the home's `bin/`,
 * so a publish in place that leaves them is one whose sessions run the build before it. A folder without its executable is
 * nothing either locator finds, and is not counted.
 */
export function carriedHosts(install) {
  return servicePlan(install)
    .filter((step) => existsSync(step.executable))
    .map((step) => `${relative(install, step.folder).split(sep).join('/')}/`);
}

/** Whether this script published here before: the marker, with its header — a file with that name proves nothing. */
export function isInstall(folder) {
  const marker = join(folder, MARKER);
  return existsSync(marker) && readFileSync(marker, 'utf8').startsWith(MARKER_HEADER);
}
