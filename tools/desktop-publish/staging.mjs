import { createHash, randomBytes } from 'node:crypto';
import { copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import { join, relative } from 'node:path';
import { renameHeld, writeAtomic } from '../fsx.mjs';
import { HOST_EXE, HOST_HOME, LAUNCHER, MARKER, SHELL_EXE, SHELL_FILES, SHELL_HOME, carriedHosts, isInstall } from './names.mjs';
import { HOME } from './lay.mjs';

/**
 * Where `--stage` puts a build beside an install, as path segments: `update/`, which the stage and the
 * launcher's swap own, and `update/staged/` inside it, renamed into place whole. A twin of `StagedBuild.Folder`,
 * `StagedBuild.Staged`, `StagedBuild.Manifest`, `StagedBuild.Journal` and `StagedBuild.Schema`
 * (the driver's `StagedBuild.cs`, which the launcher compiles); `desktop-publish.test.ts` finds that file by its name and
 * reads it for every spelling here.
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
 * creates on first start, and `update/`, which `--stage` and the swap own. Nothing else in
 * that folder is ever this script's to touch.
 */
export const OWN = Object.freeze([LAUNCHER, SHELL_HOME[0], HOME, MARKER, STAGE[0]]);

/** A staged build's id: its moment in UTC and eight random hex digits, so two stages in one second differ. */
export function buildId(at = new Date()) {
  const stamp = at.toISOString().replace(/[-:]/g, '').replace(/\.\d+Z$/, 'Z');
  return `${stamp}-${randomBytes(4).toString('hex')}`;
}

/**
 * The manifest of the build under `root`: every file but the manifest itself, by its path with `/`, its size
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
  writeAtomic(target, `${JSON.stringify(stagedManifest(root, identity), null, 2)}\n`);
  return target;
}

/**
 * What stops `--stage`, as the sentence to print, or null: a folder that is no install this script published, a
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
 * The finished staging folder into `update/staged/`, replacing whatever is staged, whole. A folder of fresh
 * executables: Windows refuses its rename while something still holds a file in it, the scanner most often,
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

/** What is staged, removed: a publish in place supersedes it. The swap's journal stays to read. */
export function unstage(install) {
  rmSync(join(install, ...STAGED), { recursive: true, force: true });
}

/** The names the last publish recorded in `app/shell-files.txt`, or none; a path is no name, since each is removed under `app/`. */
export function recordedShellFiles(install) {
  const record = join(install, ...SHELL_FILES);
  if (!existsSync(record)) return [];
  const lines = readFileSync(record, 'utf8').split('\n').map((line) => line.trim());
  return lines.filter((name) => /^[^\\/:]+$/.test(name) && name !== '.' && name !== '..');
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
 * **Its own install, without `--service`, while it carries a service host.** Such a publish never touches
 * the hosts' folders, so the hosts an earlier `--service` laid under `app/` would stay, from that build, and both
 * locators run them ahead of the home's `bin/`: a stale binary, by another road. It is
 * refused, naming each and `--service`, as `--stage` refuses an install that carries its HTTP host. Removing them instead
 * would hand every session the home's `bin/` copy, which no publish refreshes: the same defect by the first road.
 *
 * @returns the refusal, as the sentence to print — or null when the folder may be written.
 */
export function refusal(to, { beside = false, service = false } = {}) {
  if (!existsSync(to)) return null;
  const held = readdirSync(to);
  if (held.length === 0) return null;
  if (isInstall(to)) {
    const carried = service ? [] : carriedHosts(to);
    if (carried.length === 0) return null;
    return `desktop-publish: \`${to}\` carries ${carried.join(' and ')} from an earlier publish, so this one must carry them too —\n`
      + '  publish it with --service, which lays this build\'s in their place. Without it the earlier build\'s would stay, and\n'
      + '  Daoris runs them ahead of any in the home\'s bin/. Refusing to write into it.';
  }

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
