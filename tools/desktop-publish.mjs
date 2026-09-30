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
 * **Framework-dependent on purpose.** The shell requires a Windows desktop runtime; a self-contained
 * publish would add ~150 MB to carry a .NET that this machine has. It carries its own Chromium (D92,
 * CHR4), so it no longer needs WebView2. The service hosts are the opposite case and are
 * self-contained — they are what a *server* runs, possibly without .NET at all (D43).
 *
 * **The layout is a regular application's** (D93): `Daoris.exe` at the root is a small launcher and
 * the one thing to run; the application is `app/Daoris.Desktop.exe`, CEF's launcher, beside its
 * Chromium and its libraries, with the HTTP host in a folder of its own under `app/`; `data/` is the
 * home. Daoris's browser is the application itself since CHR8 (D99), started with the browser's
 * argument, so the install carries one Chromium. The names the shell's publish put in `app/` are
 * listed in `app/shell-files.txt`, so the next publish removes exactly those and nothing else. Daoris's
 * own example plugins sit beside them in `app/plugin-offers/`, offered and never installed (D103).
 *
 * **What a deployed shell finds.** Nothing is wired into it: with no `DAORIS_*` overrides it makes
 * the install's `data/` the Daoris home (D63) — the machine's registry, quests, drivable set and profiles
 * live there, and nothing under the user profile — and locates the HTTP host through
 * `ServiceHostLocator`, which looks beside the shell first. `--service` publishes a copy there, so
 * the folder is self-sufficient.
 */
import { execSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { copyTree, isMain } from './fsx.mjs';
import { running } from './processes.mjs';

// ---------------------------------------------------------------------------------------------
// Everything above the divider is the guard, exported so `src/Daoris.Cli/test/desktop-publish.test.ts`
// can hold every branch of it without a build; everything below it publishes.

/** What says this folder is ours to overwrite. Written on every publish; read before any. */
export const MARKER = 'INSTALLED.md';
export const MARKER_HEADER = '# Daoris — installed desktop';

/**
 * The install's layout, stated once (REV3 CLEAN1): one launcher at the root, the application and the
 * host under `app/`, the home in `data/` (D63, D93). The deployment gate, the dev loop and the testbed
 * read these rather than spelling the names again, which four of them did.
 *
 * `Daoris.exe` since D93: the one thing to run is named for the application, as a regular one is.
 */
export const LAUNCHER = 'Daoris.exe';

/**
 * Where the application sits, and its name there (D93): CEF's launcher, beside its Chromium. A twin
 * of `Launcher.AppFolder` and `Launcher.ShellExe` (`Daoris.Desktop.Launcher`), which starts it, and of
 * the app's assembly name, which names it; `desktop-publish.test.ts` reads both.
 */
export const SHELL_HOME = Object.freeze(['app']);
export const SHELL_EXE = 'Daoris.Desktop.exe';

/**
 * Launchers an earlier publish wrote at the root: the single-file shell before D93. A publish into
 * its own install removes them, and the dev loop still finds an install not yet republished.
 */
export const RETIRED_LAUNCHERS = Object.freeze(['daoris-desktop.exe']);

/**
 * Folders an earlier publish wrote under `app/` and this one no longer does: Daoris's browser before
 * CHR8, an executable of its own (`daoris-browser.exe`) with a second Chromium beside it. The
 * application is the browser now (D99). A publish into its own install removes them by name, as it
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
 * The names the shell's last publish put in `app/`, one per line (D93). What makes a republish
 * remove the previous engine's files and nothing it did not write.
 */
export const SHELL_FILES = Object.freeze(['app', 'shell-files.txt']);

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
 * The engine's locale files an install keeps, for the window and the browser alike, which share one
 * Chromium since CHR8: the two languages Daoris speaks, and the two `EngineBrowser.Locale` ever asks
 * for. The kit lays out only these languages (`ShenoraChromiumLocales` names zh-CN; en-US is its
 * fallback), with grammatical-gender stubs beside each that the publish leaves out; the deployment
 * gate reads the two back off the install.
 */
export const KEPT_LOCALES = Object.freeze(['en-US.pak', 'zh-CN.pak']);

/** The shell's own home, which it creates on first start (D63). */
export const HOME = 'data';

/**
 * Where an install carries Daoris's own example plugins as offers (PLUG9 d, D103), as path segments:
 * beside the application in `app/`, never under `data/plugins/`, so none is installed until a person
 * presses Install (or runs `daoris plugin add --offer <id>`). A twin of the CLI's `OFFERS_DIR` and the
 * driver's `PluginOffers.Layout`, which find them; `desktop-publish.test.ts` reads all three.
 */
export const PLUGIN_OFFERS = Object.freeze(['app', 'plugin-offers']);

/**
 * The tracked examples an install offers (D103): the two that land work (D100), and the one that hands a
 * session the install's own browser (D78). Not `hold-by-title`, which the rehearsals install as their
 * fixture, and not `browser`, which launches a browser of its own for a machine without the shell and
 * claims the same server name as `in-app-browser`, so the second installed would contribute nothing.
 */
export const OFFERED_PLUGINS = Object.freeze(['github-pull-request', 'azure-devops-pull-request', 'in-app-browser']);

/**
 * Lay the offers out in an install: each of {@link OFFERED_PLUGINS} copied whole from `examples` into
 * `<install>/app/plugin-offers/`, which is replaced wholesale — staged beside, then swapped — so a stale
 * file or an offer dropped since does not survive a republish. Nothing is written under the home.
 *
 * @returns the ids laid out, in the order offered.
 * @throws when an offered example has no `plugin.json`, before anything is replaced.
 */
export function layOffers(examples, install) {
  const missing = OFFERED_PLUGINS.filter((id) => !existsSync(join(examples, id, 'plugin.json')));
  if (missing.length > 0) {
    throw new Error(`desktop-publish: no plugin.json for the offered ${missing.join(', ')} under ${examples}`);
  }

  const target = join(install, ...PLUGIN_OFFERS);
  const parent = dirname(target);
  mkdirSync(parent, { recursive: true });
  const staged = join(parent, `.${PLUGIN_OFFERS.at(-1)}-staging`);
  rmSync(staged, { recursive: true, force: true });
  for (const id of OFFERED_PLUGINS) copyTree(join(examples, id), join(staged, id));
  rmSync(target, { recursive: true, force: true });
  renameSync(staged, target);
  return [...OFFERED_PLUGINS];
}

/**
 * Every name a publish writes at the root of an install — and the shell's own `data/`, which it
 * creates on first start. Nothing else in that folder is ever this script's to touch.
 */
export const OWN = Object.freeze([LAUNCHER, SHELL_HOME[0], HOME, MARKER]);

/** The names the last publish recorded in `app/shell-files.txt`, or none. */
export function recordedShellFiles(install) {
  const record = join(install, ...SHELL_FILES);
  return existsSync(record)
    ? readFileSync(record, 'utf8').split('\n').map((line) => line.trim()).filter(Boolean)
    : [];
}

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
  const LAUNCHER_PROJECT = 'src/Daoris.Desktop/Daoris.Desktop.Launcher';
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
  // The application and the single-file shell an install from before D93 still has: the launcher at
  // the root exits once it has started the application, so it is the application that holds files.
  const shells = [join(to, ...SHELL_HOME, SHELL_EXE), ...RETIRED_LAUNCHERS.map((name) => join(to, name))]
    .filter(existsSync);
  if (process.platform === 'win32' && shells.length > 0) {
    // Everything from the application's executable, its browser (CHR8) and its engine's processes
    // included; and the browser an install from before CHR8 still runs from its own folder, which
    // this publish removes. The browser follows the shell out, so a running one is a shell still
    // running, or just gone.
    const held = [
      ...shells.flatMap((shell) => running(shell)),
      ...RETIRED_IN_APP.flatMap((name) => running(join(to, ...SHELL_HOME, name, RETIRED_BROWSER_EXE))),
    ];
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
  // The first version published the default way: 24 entries, with the executable buried
  // alphabetically among DLLs, `.pdb`s and three stray WebView2 `.xml` doc files. The root holds one
  // small launcher now (D93), framework-dependent and single-file, wearing the application's icon, and
  // everything it starts sits under `app/`.
  const stages = join(repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.App', 'bin', 'publish-stage');
  rmSync(stages, { recursive: true, force: true });
  console.log('desktop-publish: publishing the launcher…');
  const launcherStage = join(stages, 'launcher');
  run(`dotnet publish "${LAUNCHER_PROJECT}" -c Release -r win-x64 --self-contained false `
    + '-p:PublishSingleFile=true -p:DebugType=none '
    + `-p:AllowedReferenceRelatedFileExtensions=none -o "${launcherStage}" --nologo`);

  // The application on its own Chromium (D92, D93): CEF's launcher beside the engine and the app's
  // libraries, and Daoris's browser too since CHR8 (D99), which is the same executable started with
  // the browser's argument — so there is one engine's `locales/` to keep, where there were two.
  // `AllowedReferenceRelatedFileExtensions=none` is what stops the package doc files; they come from
  // the WebView2 package rather than from this compile, so `GenerateDocumentationFile` does not reach
  // them. `DebugType=none` drops the symbols an installed app has no use for.
  console.log('desktop-publish: publishing the application…');
  const shellStage = join(stages, 'shell');
  run(`dotnet publish "${APP}" -c Release -r win-x64 --self-contained false `
    + `-p:DebugType=none -p:AllowedReferenceRelatedFileExtensions=none -o "${shellStage}" --nologo`);
  // The kit lays out only the languages the app project names (SHEN1), each with three 18-byte
  // grammatical-gender stubs beside it (`zh-CN_FEMININE.pak`, …); the install keeps the two plain
  // files, and refuses to ship without them.
  const locales = join(shellStage, 'locales');
  if (!KEPT_LOCALES.every((file) => existsSync(join(locales, file)))) {
    console.error(`desktop-publish: the application carries no ${KEPT_LOCALES.join(' or ')} — `
      + 'the engine would start with no language it is asked for.');
    process.exit(1);
  }
  for (const file of readdirSync(locales)) {
    if (!KEPT_LOCALES.includes(file)) rmSync(join(locales, file));
  }

  // What the last publish put in `app/` goes first, so an engine upgrade leaves none of its files
  // behind — only the recorded names, never the host beside them. Then what an earlier publish wrote
  // and this one no longer does, by name (D93): the single-file shell's launcher at the root, which
  // the launcher replaces, and the browser's own folder with its second engine (CHR8).
  const app = join(to, ...SHELL_HOME);
  for (const name of recordedShellFiles(to)) rmSync(join(app, name), { recursive: true, force: true });
  for (const path of retiredPaths(to)) rmSync(path, { recursive: true, force: true });
  mkdirSync(app, { recursive: true });
  const staged = readdirSync(shellStage);
  for (const name of staged) cpSync(join(shellStage, name), join(app, name), { recursive: true });
  writeFileSync(join(to, ...SHELL_FILES), `${staged.sort().join('\n')}\n`);
  cpSync(join(launcherStage, LAUNCHER), join(to, LAUNCHER));
  rmSync(stages, { recursive: true, force: true });

  // Daoris's own example plugins, as offers beside the application (PLUG9 d, D103): Settings → Plugins
  // lists them and installs one only when pressed, so the publish writes nothing under the home.
  const offered = layOffers(join(repoRoot, 'examples', 'plugins'), to);
  console.log(`desktop-publish: offering ${offered.join(', ')} in ${PLUGIN_OFFERS.join('/')}/ (none installed).`);

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
| \`${LAUNCHER}\` | **the application** — the only thing to run. A small launcher that starts \`${[...SHELL_HOME, SHELL_EXE].join('/')}\`. |
| \`${SHELL_HOME[0]}/\` | the application itself, on the Chromium it carries (its files are listed in \`${SHELL_FILES.join('/')}\`), which is also Daoris's own browser; the HTTP host in \`${HOST_HOME.slice(1).join('/')}/\` when published with \`--service\`; and Daoris's own example plugins in \`${PLUGIN_OFFERS.slice(1).join('/')}/\` (${OFFERED_PLUGINS.join(', ')}), offered in Settings → Plugins and by \`daoris plugin list\`, none installed until you install one. Nothing to open. |
| \`${HOME}/\` | **the Daoris home**: the registry, the quests, the drivable set, the harness profiles, the installed service binaries — and the window's engine profile (\`chromium/\`) and its geometry. |

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
