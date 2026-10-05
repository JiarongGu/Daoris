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
 *   node tools/desktop-publish.mjs --to <install> --service --stage
 *                                                           …beside a running install, in update/staged/,
 *                                                           which the desktop installs when idle (D139)
 *   … --force-ungated                                       …a tree the full set of gates has not passed
 *                                                           (GATE3, below): the person's call
 *
 * 🔴 **`--to` is required and has no default.** A machine path in a tracked file is exactly what
 * `sensitive-info` forbids, and a default would be one — the same rule `testbed.mjs` follows, for the
 * same reason.
 *
 * 🔴 **It publishes only a tree the full set of gates passed** (GATE3). A merge runs only the gates its
 * lanes reach (`tools/merge-branch.mjs`), so this is where the rest is owed: before anything is built it
 * asks `merge-branch --passed`, and refuses, naming each gate that has not passed and the smallest command
 * that would pass it: `--stale`, `--rerun <gate>…`, or `--full`, which runs them all (GATE6b).
 * `--force-ungated` is the person's explicit override, and the publish says it is ungated. A folder under
 * the workspace's `_fixtures` is never asked: the deployment rehearsal publishes there, and it is one of the
 * gates being run.
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
 * own example plugins sit beside them in `app/plugin-offers/`, offered and never installed (D103), and so
 * does the list of where each tool's versions download from, `app/resources.json` (D121). And Daoris's
 * doctrine tool (WSSETUP2, D124 §1.2): the CLI packed as the release packs it, unpacked under `app/cli/`,
 * with a launcher for each shell in `app/bin/`.
 *
 * **What a deployed shell finds.** Nothing is wired into it: with no `DAORIS_*` overrides it makes
 * the install's `data/` the Daoris home (D63) — the machine's registry, quests, drivable set and profiles
 * live there, and nothing under the user profile — and locates the HTTP host through
 * `ServiceHostLocator`, which looks beside the shell first. `--service` publishes a copy there, so
 * the folder is self-sufficient.
 */
import { execSync, spawnSync } from 'node:child_process';
import { createHash, randomBytes } from 'node:crypto';
import { copyFileSync, cpSync, existsSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join, posix, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
// The tar reader the CLI carries (AGT2b): what unpacks the doctrine tool's package (D124 §1.2).
import { extractTarGz } from '../src/Daoris.Cli/src/tarball.ts';
import { copyTree, isMain, renameHeld } from './fsx.mjs';
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
 * Where an install carries the list built in (TOOLS3, D121 §3.1), as path segments: beside the application
 * in `app/`, where the driver reads it from its own folder, never under `data/`, since a republish
 * replaces it whole and a newer list arrives as a resource location. A twin of the CLI's
 * `BUILT_IN_LAYOUT`, which finds it beside the home, and the driver's `ToolResources.Layout`;
 * `desktop-publish.test.ts` reads all three.
 */
export const RESOURCES = Object.freeze(['app', 'resources.json']);

/** The list's one source in the workspace: the driver's own, which every build carries beside itself. */
export const RESOURCES_SOURCE = Object.freeze(['src', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'resources.json']);

/**
 * Lay the list built in out in an install: `source` copied byte for byte to `<install>/app/resources.json`,
 * written beside and renamed, so a republish replaces it whole and a stopped one leaves the last. The
 * bytes are the tracked file's, so the hash the screen shows for the list built in is the list's own.
 *
 * @returns the path written.
 * @throws when `source` does not read as a schema 1 list, before anything is written: an install never
 *   carries a list its own readers refuse whole.
 */
export function layResources(source, install) {
  let list;
  try {
    list = JSON.parse(readFileSync(source, 'utf8').replace(/^﻿/, ''));
  } catch (error) {
    throw new Error(`desktop-publish: ${source} is not readable JSON (${error.message})`);
  }
  if (list === null || typeof list !== 'object' || Array.isArray(list) || list.schema !== 1) {
    throw new Error(`desktop-publish: ${source} is not a schema 1 resources.json, which every reader of the install refuses`);
  }

  const target = join(install, ...RESOURCES);
  mkdirSync(dirname(target), { recursive: true });
  const staged = `${target}.staging`;
  copyFileSync(source, staged);
  renameSync(staged, target);
  return target;
}

/**
 * Where the install carries its doctrine tool (WSSETUP2, D124 §1.2), as path segments: the CLI package the
 * release publishes, unpacked the way npm lays a package out under a prefix, `<prefix>/node_modules/daoris`.
 *
 * 🔴 The `node_modules` folder is load-bearing. The CLI reads the canon it ships only when its own folder sits
 * under one (`resolveCanonRoot`, `src/Daoris.Cli/src/canon.ts`); anywhere else it takes itself for a development
 * checkout and reads `../../canon`, which in an install is `<install>/canon`: a folder nothing publishes, or a
 * neighbour's under `--beside`. Laid out as npm would, the package finds its canon by the same rule wherever
 * it is installed, and that rule needs no second case. `desktop-publish.test.ts` holds the two together.
 */
export const CLI_HOME = Object.freeze(['app', 'cli']);
export const CLI_PACKAGE = Object.freeze([...CLI_HOME, 'node_modules', 'daoris']);

/** The package's entry inside it, as its `package.json`'s `bin` names it. */
export const CLI_ENTRY = Object.freeze(['bin', 'daoris.mjs']);

/**
 * The launchers' folder, beside the application, and the launchers in it (D124 §1.2): `daoris`, a shell
 * script, for Git Bash, and `daoris.cmd` for Command Prompt, which PowerShell finds too by `PATHEXT`. The
 * shapes npm writes for a global `bin`, less its `daoris.ps1`, which a machine's execution policy may refuse.
 * Nothing here puts the folder on any `PATH`: the account's stays the person's (D124 §10).
 */
export const CLI_BIN = Object.freeze(['app', 'bin']);
export const CLI_LAUNCHERS = Object.freeze(['daoris', 'daoris.cmd']);

/**
 * The text of each launcher, by name. Each runs the package's bin entry, found from the launcher's own
 * folder, on the bare `node` the caller's `PATH` finds: in a child Daoris starts, the one Tools resolves
 * (D124 §1.3). No machine path, so the files are the same in every install.
 *
 * The batch file is CRLF and both are ASCII: cmd.exe reads a batch file a line at a time in the console's
 * code page, which is how npm's own shims are written. The shell script turns its folder into a Windows
 * path with `cygpath` where one exists, as npm's does, so Git Bash hands `node.exe` a path it can open.
 */
export function cliLaunchers() {
  const entry = posix.relative(CLI_BIN.join('/'), [...CLI_PACKAGE, ...CLI_ENTRY].join('/'));
  const noNode = 'daoris: no node on PATH; the doctrine tool runs on Node.js 22 or later.';
  const sh = [
    '#!/bin/sh',
    '# Daoris\'s doctrine tool, the package this install carries (WSSETUP2, D124), run on the node PATH finds.',
    '# Written by the publish, and replaced by the next one.',
    'basedir=$(dirname "$(echo "$0" | sed -e \'s,\\\\,/,g\')")',
    'if command -v cygpath >/dev/null 2>&1; then basedir=$(cygpath -w "$basedir"); fi',
    'if ! command -v node >/dev/null 2>&1; then',
    `  echo '${noNode}' >&2`,
    '  exit 2',
    'fi',
    `exec node "$basedir/${entry}" "$@"`,
    '',
  ].join('\n');
  const cmd = [
    '@echo off',
    'rem Daoris\'s doctrine tool, the package this install carries (WSSETUP2, D124), run on the node PATH finds.',
    'rem Written by the publish, and replaced by the next one.',
    `where node >nul 2>nul || (echo ${noNode} 1>&2 & exit /b 2)`,
    `node "%~dp0${entry.replace(/\//g, '\\')}" %*`,
    '',
  ].join('\r\n');
  return { [CLI_LAUNCHERS[0]]: sh, [CLI_LAUNCHERS[1]]: cmd };
}

/**
 * What is wrong with an unpacked package as the doctrine tool an install carries, or null when it is the
 * release's `daoris`: named `daoris`, its bin entry, its built dispatcher (without `dist/` the entry falls
 * back to sources a package does not ship), its canon at the package's own version (`release-prep` holds the
 * two together in the workspace, and this holds them on the artefact), and no TypeScript sources, which only
 * the source tree has: the release's `files` leave them out.
 */
function cliProblem(unpacked) {
  const manifestPath = join(unpacked, 'package.json');
  if (!existsSync(manifestPath)) return 'holds no package/package.json';
  let manifest;
  try {
    manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
  } catch (error) {
    return `holds a package/package.json that is not JSON (${error.message})`;
  }
  if (manifest?.name !== CLI_PACKAGE.at(-1)) return `is the package \`${manifest?.name}\`, not \`${CLI_PACKAGE.at(-1)}\``;
  for (const file of [CLI_ENTRY.join('/'), 'dist/cli.js', 'canon/canon.json']) {
    if (!existsSync(join(unpacked, ...file.split('/')))) return `carries no package/${file}`;
  }
  if (existsSync(join(unpacked, 'src'))) {
    return 'carries package/src/, the TypeScript sources: it is the source tree, not the package the release publishes';
  }
  let canon;
  try {
    canon = JSON.parse(readFileSync(join(unpacked, 'canon', 'canon.json'), 'utf8'));
  } catch (error) {
    return `holds a package/canon/canon.json that is not JSON (${error.message})`;
  }
  if (canon?.version !== manifest.version) {
    return `carries canon ${canon?.version} in package ${manifest.version}; a release's package and its canon are one version`;
  }
  return null;
}

/**
 * Lay the doctrine tool out in an install (WSSETUP2, D124 §1.2): the tarball `npm pack` made of
 * `src/Daoris.Cli`, unpacked by the CLI's own tar reader into `<install>/app/cli/node_modules/daoris/`, and
 * the launchers written into `<install>/app/bin/`. Both folders are staged beside and swapped in whole, so a
 * file an earlier publish wrote does not survive a republish, and a refused package leaves the last one
 * standing. Nothing is written under the home.
 *
 * @returns the package's version, which is the canon's.
 * @throws naming the tarball and what is wrong, when it is not the release's `daoris` as npm packs it (every
 *   entry under `package/`), before anything is replaced.
 */
export async function layCli(tarball, install) {
  const app = join(install, SHELL_HOME[0]);
  mkdirSync(app, { recursive: true });
  const stagedCli = join(app, `.${CLI_HOME.at(-1)}-staging`);
  const stagedBin = join(app, `.${CLI_BIN.at(-1)}-staging`);
  const unstage = () => {
    rmSync(stagedCli, { recursive: true, force: true });
    rmSync(stagedBin, { recursive: true, force: true });
  };
  unstage();

  let version;
  try {
    // `npm pack` puts every entry under `package/`, so the archive unpacks into the folder that holds the
    // package and is then renamed to the package's name, as npm's own install does.
    const modules = join(stagedCli, ...CLI_PACKAGE.slice(CLI_HOME.length, -1));
    await extractTarGz(tarball, modules);
    const held = readdirSync(modules);
    if (held.length !== 1 || held[0] !== 'package') {
      throw new Error(`holds ${held.join(', ')} at its root, where npm's pack puts one folder, package/`);
    }
    const unpacked = join(modules, 'package');
    const problem = cliProblem(unpacked);
    if (problem) throw new Error(problem);
    version = JSON.parse(readFileSync(join(unpacked, 'package.json'), 'utf8')).version;
    renameSync(unpacked, join(modules, CLI_PACKAGE.at(-1)));

    mkdirSync(stagedBin, { recursive: true });
    for (const [name, text] of Object.entries(cliLaunchers())) {
      writeFileSync(join(stagedBin, name), text, { mode: 0o755 });
    }
  } catch (error) {
    unstage();
    throw new Error(`desktop-publish: the doctrine tool's package ${tarball} ${error.message}`);
  }

  for (const [staged, segments] of [[stagedCli, CLI_HOME], [stagedBin, CLI_BIN]]) {
    const target = join(install, ...segments);
    rmSync(target, { recursive: true, force: true });
    renameSync(staged, target);
  }
  return { version };
}

/**
 * Where `--stage` puts a build beside an install (UPDATE1, D139 §1), as path segments: `update/`, which the stage and the
 * launcher's swap own, and `update/staged/` inside it, renamed into place whole. A twin of `StagedBuild.Folder`,
 * `StagedBuild.Staged`, `StagedBuild.Manifest`, `StagedBuild.Journal` and `StagedBuild.Schema`
 * (`src/Daoris.Desktop/Daoris.Desktop.Driver/StagedBuild.cs`, which the launcher compiles); `desktop-publish.test.ts`
 * reads that file for every spelling here.
 */
export const STAGE = Object.freeze(['update']);
export const STAGED = Object.freeze([...STAGE, 'staged']);
export const BUILD_MANIFEST = 'build.json';
export const SWAP_JOURNAL = 'swap.json';
export const MANIFEST_SCHEMA = 1;

/** What every staged build must carry, by its manifest paths: `StagedBuild.Required`, in its order. */
export const STAGED_REQUIRED = Object.freeze([LAUNCHER, MARKER, `${SHELL_HOME[0]}/${SHELL_EXE}`, `${SHELL_HOME[0]}/Daoris.Desktop.App.dll`]);

/** A swap's phases that mean it is under way: a stage then would move files out from under the launcher. */
const SWAPPING = Object.freeze(['swapping', 'started', 'confirmed']);

/**
 * Every name a publish writes at the root of an install — and the shell's own `data/`, which it
 * creates on first start, and `update/`, which `--stage` and the swap own (D139). Nothing else in
 * that folder is ever this script's to touch.
 */
export const OWN = Object.freeze([LAUNCHER, SHELL_HOME[0], HOME, MARKER, STAGE[0]]);

/** A staged build's id: its moment in UTC and eight random hex digits, so two stages in one second differ. */
export function buildId(at = new Date()) {
  const stamp = at.toISOString().replace(/[-:]/g, '').replace(/\.\d+Z$/, 'Z');
  return `${stamp}-${randomBytes(4).toString('hex')}`;
}

/**
 * The manifest of the build under `root` (D139 §1, §4): every file but the manifest itself, by its path with `/`, its size
 * and its SHA-256, in one order, under the build's identity. What the application and the launcher check before anything
 * is replaced.
 */
export function stagedManifest(root, { id, version, commit = null, at }) {
  const files = [];
  const walk = (folder, prefix) => {
    for (const entry of readdirSync(folder, { withFileTypes: true }).sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0))) {
      const relative = prefix ? `${prefix}/${entry.name}` : entry.name;
      const full = join(folder, entry.name);
      if (entry.isDirectory()) {
        walk(full, relative);
      } else if (relative !== BUILD_MANIFEST) {
        const bytes = readFileSync(full);
        files.push({ path: relative, size: bytes.length, sha256: createHash('sha256').update(bytes).digest('hex') });
      }
    }
  };
  walk(root, '');
  files.sort((a, b) => (a.path < b.path ? -1 : a.path > b.path ? 1 : 0));
  return { schema: MANIFEST_SCHEMA, id, version, commit, at, files };
}

/** Write the build's manifest into `root`: beside, then renamed, LF. */
export function writeManifest(root, identity) {
  const target = join(root, BUILD_MANIFEST);
  const staging = `${target}.writing`;
  writeFileSync(staging, `${JSON.stringify(stagedManifest(root, identity), null, 2)}\n`);
  renameSync(staging, target);
  return target;
}

/**
 * What stops `--stage` (D139 §1), as the sentence to print, or null: a folder that is no install this script published, a
 * swap the launcher has under way, and an install carrying its HTTP host staged without `--service`, which would leave the
 * install without one. A running install is not refused: staging beside it is what the flag is for.
 */
export function stageRefusal(to, { service = false } = {}) {
  if (!existsSync(to) || !isInstall(to)) {
    return `desktop-publish: --stage stages a build beside an install this script published, and \`${to}\` is not an install.\n`
      + '  Publish into it first, or point --to at the install the desktop runs from.';
  }

  const journal = join(to, ...STAGE, SWAP_JOURNAL);
  if (existsSync(journal)) {
    let phase = null;
    try {
      phase = JSON.parse(readFileSync(journal, 'utf8'))?.phase ?? null;
    } catch {
      // A journal that does not read is no swap under way: the launcher reads it the same way.
    }
    if (SWAPPING.includes(phase)) {
      return `desktop-publish: the launcher has a swap under way in \`${to}\` (${phase}); stage again once Daoris has started on it.`;
    }
  }

  if (!service && existsSync(join(to, ...HOST_HOME, HOST_EXE))) {
    return `desktop-publish: \`${to}\` carries its HTTP host, so the staged build must too — stage it with --service, or the update\n`
      + '  would leave the install without one.';
  }
  return null;
}

/**
 * The finished staging folder into `update/staged/`, replacing whatever was staged before, whole. A folder of fresh
 * executables: Windows refuses its rename while something still holds a file in it, the scanner most often (UPDATE1),
 * so the rename is tried for half a minute, and then the build is copied into place with its manifest written last.
 * Whatever reads `update/staged/` reads the manifest first, so a half copy is nothing staged, never a broken build.
 */
export function promoteStage(staging, install, { rename, tries = 150, waitMs = 200, copied = () => {} } = {}) {
  const staged = join(install, ...STAGED);
  rmSync(staged, { recursive: true, force: true });
  try {
    renameHeld(staging, staged, { tries, waitMs, ...(rename ? { rename } : {}) });
    return staged;
  } catch (error) {
    if (!['EPERM', 'EACCES', 'EBUSY'].includes(error?.code)) throw error;
  }

  const copyInto = (from, to, at) => {
    mkdirSync(to, { recursive: true });
    for (const entry of readdirSync(from, { withFileTypes: true })) {
      const path = at ? `${at}/${entry.name}` : entry.name;
      if (!at && entry.name === BUILD_MANIFEST) continue;
      if (entry.isDirectory()) copyInto(join(from, entry.name), join(to, entry.name), path);
      else {
        copyFileSync(join(from, entry.name), join(to, entry.name));
        copied(path);
      }
    }
  };
  copyInto(staging, staged, '');
  copyFileSync(join(staging, BUILD_MANIFEST), join(staged, BUILD_MANIFEST));
  copied(BUILD_MANIFEST);
  // The staging folder is spent either way; one still held is cleared by the next stage, which starts by removing it.
  try {
    rmSync(staging, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 });
  } catch {
    // Left for the next stage.
  }
  return staged;
}

/** What was staged, removed: a publish in place supersedes it (D139 §1). The swap's journal stays to read. */
export function unstage(install) {
  rmSync(join(install, ...STAGED), { recursive: true, force: true });
}

/** The names the last publish recorded in `app/shell-files.txt`, or none. */
export function recordedShellFiles(install) {
  const record = join(install, ...SHELL_FILES);
  return existsSync(record)
    ? readFileSync(record, 'utf8').split('\n').map((line) => line.trim()).filter(Boolean)
    : [];
}

/**
 * What an install's `INSTALLED.md` says: what this folder is, for whoever opens it in six months. It is
 * also the marker {@link isInstall} reads, so it opens with {@link MARKER_HEADER}. Written by the publish
 * rather than tracked, because the file belongs to the install; it names no machine path.
 *
 * The pinning section is D108's §2 (LEFT1): the window names Daoris's taskbar id, so a pin made from
 * it starts the launcher here, and a pin made on the launcher in Explorer carries no id and stays a
 * button apart from the running window.
 */
export function installedNote() {
  return `${MARKER_HEADER}

Published from a Daoris workspace by \`tools/desktop-publish.mjs\`.

## What is here

| | |
|---|---|
| \`${LAUNCHER}\` | **the application** — the only thing to run. A small launcher that starts \`${[...SHELL_HOME, SHELL_EXE].join('/')}\`. |
| \`${SHELL_HOME[0]}/\` | the application itself, on the Chromium it carries (its files are listed in \`${SHELL_FILES.join('/')}\`), which is also Daoris's own browser; the HTTP host in \`${HOST_HOME.slice(1).join('/')}/\` when published with \`--service\`; and Daoris's own example plugins in \`${PLUGIN_OFFERS.slice(1).join('/')}/\` (${OFFERED_PLUGINS.join(', ')}), offered in Settings → Plugins and by \`daoris plugin list\`, none installed until you install one; and \`${RESOURCES.slice(1).join('/')}\`, the list of where each version of the tools Daoris runs downloads from, read and never rewritten; and the doctrine tool, in \`${CLI_HOME.slice(1).join('/')}/\` and \`${CLI_BIN.slice(1).join('/')}/\` (below). Nothing to open. |
| \`${HOME}/\` | **the Daoris home**: the registry, the quests, the drivable set, the harness profiles, the installed service binaries — and the window's engine profile (\`chromium/\`) and its geometry. |
| \`${STAGE[0]}/\` | an update: a build staged with \`--stage\` in \`${STAGED.slice(1).join('/')}/\`, which the application installs once its work allows, and the launcher's record of the last swap, \`${SWAP_JOURNAL}\` (D139). Present only once something was staged. |

Anything else in this folder is not the application's — repositories it drives, typically — and a
re-publish never touches it.

## Where everything lives

In \`${HOME}/\`, and nowhere under your user profile. On first start the application sets
\`DAORIS_HOME\` to that folder for itself and — once, if your account has none — for your account,
which is how the \`daoris\` CLI on a terminal and the desktop are **two doors onto one machine**: what
one sets the other sees. When your account's \`DAORIS_HOME\` already names another folder — another
install's \`${HOME}/\`, or this one's before it moved — the application still runs on its own, leaves
that variable as it is, and says so on Settings' home row (D105): a terminal reads the folder the
variable names. A \`.daoris\` folder under your profile from an earlier version moves in on
that first start (its \`bin/\` stays; re-run \`publish:service --install\` to land the hosts here).
Deleting this folder removes the application and its machine — nothing else on the machine changes.

Starting it starts the driver loop, so **a drivable repository with an open quest gets a real agent
session.** \`daoris driver list\` shows what this machine will drive.

## The doctrine tool

\`${CLI_BIN.join('/')}/\` holds \`${CLI_LAUNCHERS[0]}\`, for Git Bash, and \`${CLI_LAUNCHERS[1]}\`, for Command Prompt and
PowerShell. Each runs the \`daoris\` package this install carries in \`${CLI_HOME.join('/')}/\`, packed from the same
build as the application, as the release packs the one it publishes to npm (\`daoris --version\` says
which), on the \`node\` your PATH finds: it needs Node.js 22 or later.
Every program Daoris starts finds it by its bare name: a driven session, a conversation, an intake, a hook,
a landing plugin and every shell of its terminal panel begin their PATH with \`${CLI_BIN.join('/')}/\`, because
this install's home, \`${HOME}/\`, sits beside \`${CLI_BIN[0]}/\` (D124).
Nothing puts \`${CLI_BIN.join('/')}/\` on your account's PATH, so a terminal of yours outside Daoris still runs whatever
\`daoris\` you installed; run this one by its path there, from the repository it should look at. A re-publish
replaces both folders whole.

## Pinning it to the taskbar

Pin Daoris from its running window: right-click its button on the taskbar, then *Pin to taskbar*.
The window names Daoris's taskbar id (D108), so that pin starts \`${LAUNCHER}\` here and every later
Daoris window joins it as one button. A pin made on \`${LAUNCHER}\` itself, from Explorer, carries no id:
the launcher starts \`${[...SHELL_HOME, SHELL_EXE].join('/')}\` and exits, and Windows cannot tell that
the window belongs to that pin, so the running window shows as a second button beside it. If you have
a pin of that kind, unpin it, start Daoris, and pin its running window once.

## Updating it

Stage a new build beside it while it runs: \`npm run publish:desktop -- --to <this folder> --service --stage\`. The
application then starts nothing new, lets the running sessions end or park, closes, and \`${LAUNCHER}\` checks the
staged build, swaps \`${SHELL_HOME[0]}/\` and starts it again, putting the build before it back if the new one does not
come up (D139). The banner in the window and \`daoris-driver update --when-idle|--now|--cancel\` say when.
Re-publishing over this folder with the application closed still works; nothing here is edited by hand.
`;
}

// ---------------------------------------------------------------------------------------------
// GATE3: the full set before the install

/** The command that runs every gate on the checkout as it stands: what a refusal names. `merge-branch`'s `FULL_COMMAND`. */
export const GATE_COMMAND = 'node tools/merge-branch.mjs --full';

/**
 * The line `merge-branch --passed` ends a refusal with (GATE6b): what to run and the smallest command that runs it,
 * `--stale`, `--rerun <gate>…` or `--full`. Read from its output, as the rest is: the publish never imports the tool.
 */
const REMEDY = /^\s*Run (.+?) on it: (node tools\/merge-branch\.mjs .+?)\s*$/;

/**
 * Whether `to` is the workspace's scratch, `_fixtures` or a folder inside it: where the deployment rehearsal
 * publishes. Compared by path, so a sibling whose name starts the same is not inside; case-blind on Windows.
 */
export function insideFixtures(to, repoRoot) {
  const rel = relative(join(resolve(repoRoot), '_fixtures'), resolve(to));
  return rel !== '..' && !rel.startsWith(`..${sep}`) && !isAbsolute(rel);
}

/**
 * Ask the merge tool whether the full set passed the checkout as it stands: its exit (0 passed, 1 not,
 * anything else could not tell) and what it printed, gate by gate. A process rather than an import, so
 * nothing the deployment rehearsal runs imports the merge tool.
 */
export function askGates(repoRoot, tool = join(dirname(fileURLToPath(import.meta.url)), 'merge-branch.mjs')) {
  const asked = spawnSync(process.execPath, [tool, '--passed'], { cwd: repoRoot, encoding: 'utf8', windowsHide: true });
  if (asked.error) return { code: 2, out: `node could not start: ${asked.error.message}` };
  return { code: asked.status ?? 2, out: `${asked.stdout ?? ''}${asked.stderr ?? ''}`.trim() };
}

const indented = (text) => text.split(/\r?\n/).filter((line) => line.trim()).map((line) => `  ${line.trimEnd()}`).join('\n');

/**
 * What stops a publish into `to` for want of gates (GATE3), as the sentence to print, and what to say when it
 * goes ahead: nothing is asked for a folder under `_fixtures`; a tree the full set passed publishes; any other
 * is refused, naming what has not passed and the smallest command the tool names that would pass it (GATE6b), or
 * the one that runs it all, unless `force` (`--force-ungated`, the person's explicit override), when it publishes
 * and says it is ungated.
 */
export function ungatedRefusal(to, repoRoot, { force = false, ask = askGates } = {}) {
  if (insideFixtures(to, repoRoot)) return { refusal: null, note: null };
  const { code, out } = ask(repoRoot);
  if (code === 0) return { refusal: null, note: `desktop-publish: ${out.split(/\r?\n/)[0].replace(/^merge-branch: /, '')}` };
  const why = code === 1
    ? 'the full set of gates has not passed this checkout'
    : 'could not tell whether the full set passed this checkout';
  if (force) {
    return { refusal: null, note: `desktop-publish: publishing ungated (--force-ungated): ${why}.\n${indented(out)}` };
  }
  // The tool's own last line names the command too; it is said once, below. A tool that names none, or cannot tell, gets the full set.
  const lines = out.split(/\r?\n/);
  const named = lines.map((line) => REMEDY.exec(line)).find(Boolean);
  const [what, command] = named ? [named[1], named[2]] : ['every gate', GATE_COMMAND];
  const said = lines.filter((line) => !REMEDY.test(line) && !line.includes(GATE_COMMAND)).join('\n');
  return {
    refusal: `desktop-publish: ${why}, so it is not published to \`${to}\`.\n${indented(said)}\n`
      + `  A merge runs only the gates its lanes reach (GATE3); run ${what} on this checkout: ${command}\n`
      + '  Or, as your explicit call, publish it ungated with --force-ungated.',
    note: null,
  };
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

async function main() {
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

  // `--stage` (UPDATE1, D139 §1): the same build, written beside the install into `update/staged/` instead of over it, for
  // the desktop to install when its work allows. Everything below writes under `root`, which is the install or the staging.
  const stage = flag('--stage');
  const refused = stage ? stageRefusal(to, { service: flag('--service') }) : refusal(to, { beside: flag('--beside') });
  if (refused) {
    console.error(refused);
    process.exit(2);
  }
  // GATE3: only a tree the full set passed is built into an install, before anything is built.
  const gated = ungatedRefusal(to, repoRoot, { force: flag('--force-ungated') });
  if (gated.refusal) {
    console.error(gated.refusal);
    process.exit(2);
  }
  if (gated.note) console.log(gated.note);
  const root = stage ? join(to, ...STAGE, '.staging') : to;
  if (stage) {
    rmSync(root, { recursive: true, force: true });
    mkdirSync(root, { recursive: true });
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
  // A stage writes nothing the running application holds, so it is never refused for one running (D139 §1).
  if (!stage && process.platform === 'win32' && shells.length > 0) {
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
      console.error('  publishing over it fails halfway through, leaving the folder part-written. Or stage the');
      console.error('  build beside it with --stage, which the desktop installs once its work allows (D139).');
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

  // The doctrine tool (WSSETUP2, D124 §1.2): `src/Daoris.Cli` packed as the release packs it, `npm pack` in
  // its own folder, whose `prepack` builds `dist/` and stages the canon, the licence and the readme. The
  // tarball is the artefact the release rehearsal installs and the release workflow publishes, never the
  // source tree. Packed before anything is published, so a package that will not pack stops the publish
  // while the install is still as it was.
  console.log('desktop-publish: packing the doctrine tool…');
  const cliStage = join(stages, 'cli');
  mkdirSync(cliStage, { recursive: true });
  // `postpack` removes what the pack staged, and this removes it again whatever happened, as the release
  // rehearsal does: a `dist/` outliving a pack shadows the sources for every later bin-driven run (FIX-LOG
  // 2026-09-20).
  const unstagePackage = () => execSync(`node "${join(repoRoot, 'tools', 'stage-package.mjs')}" --clean`, { stdio: 'ignore' });
  let cliTarball;
  try {
    // The tarball's name is the last line npm prints; the build `prepack` runs prints above it.
    const packed = execSync(`npm pack --pack-destination "${cliStage}"`, {
      cwd: join(repoRoot, 'src', 'Daoris.Cli'), encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'],
    }).trim().split('\n').pop().trim();
    cliTarball = join(cliStage, packed);
  } catch (error) {
    unstagePackage();
    console.error(`${error.stdout ?? ''}${error.stderr ?? ''}`);
    console.error('desktop-publish: `npm pack` of src/Daoris.Cli failed, so the install would carry no doctrine tool.');
    process.exit(1);
  }
  unstagePackage();
  if (!existsSync(cliTarball)) {
    console.error(`desktop-publish: \`npm pack\` named ${cliTarball}, and there is no such file.`);
    process.exit(1);
  }

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
  // The list built in is laid out below, by its one writer (TOOLS3). Whatever the build's publish carried
  // of it leaves the stage, so `shell-files.txt` never records it as the application's.
  rmSync(join(shellStage, RESOURCES.at(-1)), { force: true });

  // What the last publish put in `app/` goes first, so an engine upgrade leaves none of its files
  // behind — only the recorded names, never the host beside them. Then what an earlier publish wrote
  // and this one no longer does, by name (D93): the single-file shell's launcher at the root, which
  // the launcher replaces, and the browser's own folder with its second engine (CHR8).
  const app = join(root, ...SHELL_HOME);
  const staged = readdirSync(shellStage);
  // The doctrine tool's two folders are laid out whole below, so an application file of either name would
  // be replaced by them, and lost without a word.
  const clash = staged.filter((name) => name === CLI_HOME.at(-1) || name === CLI_BIN.at(-1));
  if (clash.length > 0) {
    console.error(`desktop-publish: the application's publish carries ${clash.join(' and ')}, where the install `
      + 'keeps the doctrine tool (WSSETUP2).');
    process.exit(1);
  }
  // A staging folder starts empty and is no install, so neither line below removes anything there.
  for (const name of recordedShellFiles(root)) rmSync(join(app, name), { recursive: true, force: true });
  for (const path of retiredPaths(root)) rmSync(path, { recursive: true, force: true });
  mkdirSync(app, { recursive: true });
  for (const name of staged) cpSync(join(shellStage, name), join(app, name), { recursive: true });
  writeFileSync(join(root, ...SHELL_FILES), `${staged.sort().join('\n')}\n`);
  cpSync(join(launcherStage, LAUNCHER), join(root, LAUNCHER));

  // The doctrine tool, beside the application (WSSETUP2, D124 §1.2): the package under `app/cli/`, as npm
  // lays one out, and a launcher for each shell in `app/bin/`. Nothing goes on any PATH from here.
  try {
    const tool = await layCli(cliTarball, root);
    console.log(`desktop-publish: the doctrine tool is daoris ${tool.version} in ${CLI_HOME.join('/')}/, `
      + `run by ${CLI_LAUNCHERS.map((name) => `${CLI_BIN.join('/')}/${name}`).join(' or ')}.`);
  } catch (error) {
    console.error(error.message);
    process.exit(1);
  }
  rmSync(stages, { recursive: true, force: true });

  // Daoris's own example plugins, as offers beside the application (PLUG9 d, D103): Settings → Plugins
  // lists them and installs one only when pressed, so the publish writes nothing under the home.
  const offered = layOffers(join(repoRoot, 'examples', 'plugins'), root);
  console.log(`desktop-publish: offering ${offered.join(', ')} in ${PLUGIN_OFFERS.join('/')}/ (none installed).`);

  // The list of where each tool's versions download from (TOOLS3, D121 §3.1), beside the application.
  // The driver's build carries it too, and the stage was cleared of it above: this is the one writer.
  layResources(join(repoRoot, ...RESOURCES_SOURCE), root);
  console.log(`desktop-publish: the list built in is ${RESOURCES.join('/')}.`);

  if (flag('--service')) {
    // Supporting binaries go under `app/`, which is the shape the neighbouring applications on this
    // machine use: one launcher at the root, everything it needs out of sight, runtime state in `data/`.
    console.log(`desktop-publish: publishing the HTTP host under ${HOST_HOME[0]}/…`);
    const host = join(root, ...HOST_HOME);

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

  mkdirSync(root, { recursive: true });
  writeFileSync(join(root, MARKER), installedNote());

  if (stage) {
    // The build's manifest, which the application and the launcher check before anything is replaced (D139 §4), then
    // the folder swapped into `update/staged/` whole, so the desktop never meets half a stage.
    const identity = {
      id: buildId(),
      version: JSON.parse(readFileSync(join(repoRoot, 'canon', 'canon.json'), 'utf8')).version,
      commit: commitOf(repoRoot),
      at: new Date().toISOString().replace(/\.\d+Z$/, 'Z'),
    };
    writeManifest(root, identity);
    promoteStage(root, to);
    console.log(`\ndesktop-publish: staged build ${identity.id} (${identity.version}${identity.commit ? `, ${identity.commit}` : ''}) `
      + `beside ${to}, in ${STAGED.join('/')}/`);
    console.log('  The desktop installs it once no driven session runs and no turn is in flight, and starts again (D139);');
    console.log('  `daoris-driver update` says where it stands, and --now or --cancel says otherwise.');
    return;
  }

  // A publish in place supersedes anything staged (D139 §1): left, it would replace this build at the next start.
  unstage(to);
  console.log(`\ndesktop-publish: installed to ${to}`);
  console.log(`  Its home is ${join(to, HOME)} (D63) — starting it starts the driver loop.`);
}

/** The commit this workspace is at, short, or null where git cannot say: what tells two builds at one version apart. */
function commitOf(repoRoot) {
  try {
    return execSync('git rev-parse --short HEAD', { cwd: repoRoot, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim() || null;
  } catch {
    return null;
  }
}

// Guarded, because the guard above is imported by a unit test — and `node --test` importing this
// file must not publish anything. `desktop.mjs` and `deployment-rehearsal.mjs` guard the same way.
if (isMain(import.meta.url)) await main();
