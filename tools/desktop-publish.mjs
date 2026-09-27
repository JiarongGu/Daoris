#!/usr/bin/env node
/**
 * Publish the desktop shell as an installed application — the thing a person runs, not a checkout
 * with a build step. The sibling of `service-publish.mjs`, and it exists for the same reason: a
 * workspace build is not a deployment, and the difference only shows up once you try.
 *
 *   node tools/desktop-publish.mjs --to <folder>            publish the shell into <folder>
 *   node tools/desktop-publish.mjs --to <folder> --service  …and the HTTP host beside it, with its bundle
 *   node tools/desktop-publish.mjs --to <folder> --beside   …into a folder that holds other things —
 *                                                           the repositories it drives, typically
 *
 * 🔴 **`--to` is required and has no default.** A machine path in a tracked file is exactly what
 * `sensitive-info` forbids, and a default would be one — the same rule `testbed.mjs` follows, for the
 * same reason.
 *
 * **Framework-dependent on purpose.** The shell already requires a Windows desktop runtime and the
 * WebView2 runtime; a self-contained publish would add ~150 MB to carry a .NET that this machine has
 * and would still not carry WebView2. The service hosts are the opposite case and are self-contained
 * — they are what a *server* runs, possibly without .NET at all (D43).
 *
 * **What a deployed shell finds.** Nothing is wired into it: with no `DAORIS_*` overrides it makes
 * its own `data/` the Daoris home (D63) — the machine's registry, quests, drivable set and profiles
 * live there, and nothing under the user profile — and locates the HTTP host through
 * `ServiceHostLocator`, which looks beside the shell first. `--service` publishes a copy there, so
 * the folder is self-sufficient.
 */
import { execSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';
import { running } from './processes.mjs';

// ---------------------------------------------------------------------------------------------
// Everything above the divider is the guard, exported so `src/Daoris.Cli/test/desktop-publish.test.ts`
// can hold every branch of it without a build; everything below it publishes.

/** What says this folder is ours to overwrite. Written on every publish; read before any. */
export const MARKER = 'INSTALLED.md';
export const MARKER_HEADER = '# Daoris — installed desktop';

/**
 * The install's layout, stated once (REV3 CLEAN1): one launcher at the root, supporting binaries
 * under `app/`, the home in `data/` (D63). The deployment gate, the dev loop and the testbed read
 * these rather than spelling the names again, which four of them did.
 */
export const LAUNCHER = 'daoris-desktop.exe';

/**
 * Where `--service` puts the HTTP host inside an install, as path segments.
 *
 * 🔴 One half of the counterpart set defect 2a was: this layout and `ServiceHostLocator`'s candidate
 * list must be the same layout, and until a deployment nothing read both.
 * `deployment-rehearsal.test.ts` reads the locator for the other half.
 */
export const HOST_HOME = Object.freeze(['app', 'daoris-knowledge-http']);

/** The service host's file name inside {@link HOST_HOME}. */
export const HOST_EXE = 'daoris-knowledge-http.exe';

/**
 * Where Daoris's own browser goes (D85, CHR3): a process of its own, carrying the engine's runtime
 * beside it. The other half of this counterpart set is `EngineBrowser.InstallHome`, which the shell
 * looks in first; `deployment-rehearsal.test.ts` reads both.
 */
export const BROWSER_HOME = Object.freeze(['app', 'daoris-browser']);

/** The browser's file name inside {@link BROWSER_HOME}. */
export const BROWSER_EXE = 'daoris-browser.exe';

/**
 * The engine's locale files an install keeps: the two languages Daoris speaks, and the two
 * `EngineBrowser.Locale` ever asks for. The other 218 are 48 MB nobody reads.
 */
export const BROWSER_LOCALES = Object.freeze(['en-US.pak', 'zh-CN.pak']);

/** The shell's own home, which it creates on first start (D63). */
export const HOME = 'data';

/**
 * Every name a publish writes at the root of an install — and the shell's own `data/`, which it
 * creates on first start. Nothing else in that folder is ever this script's to touch.
 */
export const OWN = Object.freeze([LAUNCHER, HOST_HOME[0], HOME, MARKER]);

/** Whether this script published here before: the marker, with its header — a file with that name proves nothing. */
export function isInstall(folder) {
  const marker = join(folder, MARKER);
  return existsSync(marker) && readFileSync(marker, 'utf8').startsWith(MARKER_HEADER);
}

/**
 * 🔴 The guard: this writes a whole directory tree, so it refuses a folder holding anything it did
 * not put there. A publish into somebody's documents folder and a publish into an install folder
 * look identical to `cpSync`.
 *
 * **Decided by a marker, not by a list of extensions.** The first version allowlisted the file types
 * a publish emits and refused its own second run, because `dotnet publish` also drops `.xml` doc
 * files — and an allowlist is a list people append to until it allows everything. A marker this
 * script writes is decidable: either we published here or we did not. It is the same provenance test
 * the dsh patch layer uses, for the same reason.
 *
 * **`--beside` is the door through it, not a weaker default.** The folder a person actually wants
 * may already hold the repositories the application drives (the second deployment did: the
 * application at the family's root, beside the testbed). This script writes exactly the names in
 * `OWN`, so beside other things it refuses only when one of THOSE names is already taken by
 * something it did not write — the one case where "install next to it" would be "install over it".
 *
 * @returns the refusal, as the sentence to print — or null when the folder may be written.
 */
export function refusal(to, { beside = false } = {}) {
  if (!existsSync(to)) return null;
  const held = readdirSync(to);
  if (held.length === 0 || isInstall(to)) return null;

  if (!beside) {
    // Naming what it found, not the first few things it listed: a refusal that points at the wrong
    // file sends you to look in the wrong place, which is worse than one that says nothing.
    return `desktop-publish: \`${to}\` is not a folder this script published — it holds `
      + `${held.length} item(s) and no \`${MARKER}\`: ${held.slice(0, 6).join(', ')}`
      + `${held.length > 6 ? ', …' : ''}\n  Refusing to write into it. Point --to at an empty folder, `
      + 'or one this script installed to before — or pass --beside to install next to what is there,\n'
      + '  which still refuses to write over a name it did not write.';
  }

  const taken = held.filter((entry) => OWN.includes(entry));
  if (taken.length > 0) {
    return `desktop-publish: \`${to}\` already holds ${taken.join(', ')}, and it is not a folder this `
      + 'script published — --beside writes next to other things, never over a name it did not write.\n'
      + '  Refusing to write into it.';
  }
  return null;
}

// ---------------------------------------------------------------------------------------------

function main() {
  const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
  const argv = process.argv.slice(2);
  const flag = (name) => argv.includes(name);
  const value = (name) => {
    const at = argv.indexOf(name);
    return at !== -1 && argv[at + 1] && !argv[at + 1].startsWith('--') ? argv[at + 1] : null;
  };

  const toArg = value('--to');
  if (!toArg) {
    console.error('desktop-publish: --to <folder> is required.\n'
      + '  It has no default on purpose: a machine path does not belong in a tracked file.');
    process.exit(2);
  }
  const to = isAbsolute(toArg) ? toArg : resolve(process.cwd(), toArg);

  const APP = 'src/Daoris.Desktop/Daoris.Desktop.App';
  const BROWSER = 'src/Daoris.Desktop/Daoris.Desktop.Browser';
  const HTTP = 'src/Daoris.Service/Daoris.Service.Http';
  const WEB = 'src/Daoris.Web';

  const run = (command) => execSync(command, { cwd: repoRoot, stdio: ['ignore', 'inherit', 'inherit'] });

  const refused = refusal(to, { beside: flag('--beside') });
  if (refused) {
    console.error(refused);
    process.exit(2);
  }

  /**
   * 🔴 A running install holds its own executable open, and a re-publish is exactly when it is running.
   *
   * Without this the failure is an MSBuild stack ending in
   * `System.UnauthorizedAccessException: Access to the path '…daoris-desktop.exe' is denied` — eleven
   * lines of `Microsoft.NET.HostModel.Bundle` internals for "close the app". Checked before anything
   * is built, so the answer arrives in a second rather than after the whole web bundle.
   */
  const installed = join(to, LAUNCHER);
  if (process.platform === 'win32' && existsSync(installed)) {
    // The browser follows the shell out, so a running one is a shell still running, or just gone.
    const held = [...running(installed), ...running(join(to, ...BROWSER_HOME, BROWSER_EXE))];
    if (held.length > 0) {
      console.error(`desktop-publish: the install at \`${to}\` is running (pid ${held.join(', ')}).`);
      console.error('  Close it and re-run — a running application holds its own executable open, and');
      console.error('  publishing over it fails halfway through, leaving the folder part-written.');
      process.exit(2);
    }
  }

  // The dependency order, and it is not cosmetic: the platform builds INTO the host's wwwroot, so a
  // host published before the bundle carries the previous one — and the window shows it.
  console.log('desktop-publish: building the platform bundle…');
  run(`npm --prefix ${WEB} run build`);

  // 🔴 ONE executable at the root, and nothing else that looks like one.
  //
  // The first version published the default way: 24 entries, with `daoris-desktop.exe` buried
  // alphabetically among DLLs, `.pdb`s and three stray WebView2 `.xml` doc files. A person opening the
  // folder could not see what to run. A single-file publish answers it completely — 2.8 MB, one file —
  // and is framework-dependent, so it carries no .NET it did not need to.
  //
  // `AllowedReferenceRelatedFileExtensions=none` is what stops the package doc files; they come from
  // the WebView2 package rather than from this compile, so `GenerateDocumentationFile` does not reach
  // them. `DebugType=none` drops the symbols an installed app has no use for.
  console.log('desktop-publish: publishing the shell…');
  run(`dotnet publish "${APP}" -c Release -r win-x64 --self-contained false `
    + '-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true '
    + `-p:DebugType=none -p:AllowedReferenceRelatedFileExtensions=none -o "${to}" --nologo`);

  // Daoris's own browser (D85, CHR3), always: the shell starts it, so a shell without it has a
  // browser menu that does nothing. Replaced rather than published over, as the host is below, so
  // an engine upgrade leaves no file of the last one beside it.
  console.log(`desktop-publish: publishing Daoris's browser under ${BROWSER_HOME.join('/')}/…`);
  const browser = join(to, ...BROWSER_HOME);
  rmSync(browser, { recursive: true, force: true });
  run(`dotnet publish "${BROWSER}" -c Release -r win-x64 --self-contained false `
    + `-p:DebugType=none -p:AllowedReferenceRelatedFileExtensions=none -o "${browser}" --nologo`);
  const locales = join(browser, 'locales');
  if (!BROWSER_LOCALES.every((file) => existsSync(join(locales, file)))) {
    console.error(`desktop-publish: the browser's publish carries no ${BROWSER_LOCALES.join(' or ')} — `
      + 'the engine would start with no language it is asked for.');
    process.exit(1);
  }
  for (const file of readdirSync(locales)) {
    if (!BROWSER_LOCALES.includes(file)) rmSync(join(locales, file));
  }

  if (flag('--service')) {
    // Supporting binaries go under `app/`, which is the shape the neighbouring applications on this
    // machine use: one launcher at the root, everything it needs out of sight, runtime state in `data/`.
    console.log(`desktop-publish: publishing the HTTP host under ${HOST_HOME[0]}/…`);
    const host = join(to, ...HOST_HOME);

    // 🔴 REPLACED, not published over. `dotnet publish` does not clear its output, so a re-publish
    // leaves every previous hashed bundle in `wwwroot/assets` — and `index.html` names only the
    // current one, which makes the folder correct and unreadable. `service-publish.mjs` already does
    // this, for this reason, and its sibling here did not: a real install reached SEVEN bundles, and
    // listing it gave a wrong answer about which build was live twice — once in the first-deployment
    // case study, and once while re-publishing to that same machine afterwards.
    rmSync(host, { recursive: true, force: true });

    run(`dotnet publish "${HTTP}" -c Release -r win-x64 --self-contained `
      + `-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "${host}"`);

    // The bundle travels BESIDE the executable — a host installed without its page answers every API
    // call and serves 404 for the UI, which reads as a broken app rather than a missing file.
    const bundle = join(host, 'wwwroot');
    if (!existsSync(bundle)) {
      console.error('desktop-publish: the HTTP publish carries no wwwroot — the shell would serve no page.');
      process.exit(1);
    }
  }

  // What this folder is, for whoever opens it in six months. Written here rather than tracked, because
  // it names a machine path — the file belongs to the install, not to the repository.
  mkdirSync(to, { recursive: true });
  writeFileSync(join(to, MARKER), `${MARKER_HEADER}

Published from a Daoris workspace by \`tools/desktop-publish.mjs\`.

## What is here

| | |
|---|---|
| \`${LAUNCHER}\` | **the application** — the only thing to run. One file. |
| \`${HOST_HOME[0]}/\` | supporting binaries: Daoris's own browser and the Chromium it runs on, and the HTTP host when published with \`--service\`. Nothing to open. |
| \`${HOME}/\` | **the Daoris home**: the registry, the quests, the drivable set, the harness profiles, the installed service binaries — and the WebView2 profile and the window's geometry. |

Anything else in this folder is not the application's — repositories it drives, typically — and a
re-publish never touches it.

## Where everything lives

In \`${HOME}/\`, and nowhere under your user profile. On first start the application sets
\`DAORIS_HOME\` to that folder for itself and — once, if your account has none — for your account,
which is how the \`daoris\` CLI on a terminal and the desktop are **two doors onto one machine**: what
one sets the other sees. A \`.daoris\` folder under your profile from an earlier version moves in on
that first start (its \`bin/\` stays; re-run \`publish:service --install\` to land the hosts here).
Deleting this folder removes the application and its machine — nothing else on the machine changes.

Starting it starts the driver loop, so **a drivable repository with an open quest gets a real agent
session.** \`daoris driver list\` shows what this machine will drive.

Re-publish over this folder to update it; nothing here is edited by hand.
`);

  console.log(`\ndesktop-publish: installed to ${to}`);
  console.log(`  Its home is ${join(to, HOME)} (D63) — starting it starts the driver loop.`);
}

// Guarded, because the guard above is imported by a unit test — and `node --test` importing this
// file must not publish anything. `desktop.mjs` and `deployment-rehearsal.mjs` guard the same way.
if (isMain(import.meta.url)) main();
