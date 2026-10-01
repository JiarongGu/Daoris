// `daoris plugin` — the plugins this machine has, from a terminal (D64, D50).
//
// A plugin is a FOLDER under the home's `plugins/` with a manifest: what it declares (configurations
// of the ACP door) and what it speaks (a process on named points). THE FOLDER IS THE API and this is
// an editor over it, the shape every management verb here has: the desktop's Plugins card edits the
// same folder and the same `plugins.json`, and hand-copying a folder in keeps working because the
// folder — not the surface — is the truth.
//
// It is the CLI's twin of the driver's `Plugins.cs`. Two copies exist because the two artefacts share
// no code — the FOLDER AND THE MANIFEST are the contract — so these rules hold in both, and each
// carries a test saying so:
//
//   1. The version is read before anything else; a newer one is refused naming both numbers.
//   2. A broken manifest is a named problem, never a crash — and a refused plugin contributes nothing.
//   3. A conflict is refused before anything loads, naming both sides: a harness this build carries,
//      or one an earlier plugin (by id) already declared.
//   4. Disabled is a row in `plugins.json`, never a rename.
//
// It is a MANAGEMENT command and it opens no socket and spawns nothing: it reads and writes files
// under the home. No code from a plugin is ever loaded here or anywhere (D64).
//
// Two more twins since PLUG9 (c) and (d), D103, with the driver's `PluginInstall.cs` and
// `PluginOffers.cs`, each side's tests holding the same table:
//
//   5. Where an installed plugin came from is `.daoris-source.json` in its install folder: the folder it
//      was added from, or the offer it was installed from. `update` re-reads that source by rules 1–3,
//      refuses in the same words, says what changes, and swaps the install folder whole. Since PLUGDIST1a
//      (D120) it may be the package the driver installed it from, which this side reads and lists, and
//      whose update it refuses as the driver does: a package is installed whole, by the driver alone.
//   6. Daoris's own example plugins are offered from the install's `app/plugin-offers/`, beside the
//      home; what each needs is its README's `## What it needs`.

import { copyFileSync, existsSync, mkdirSync, readdirSync, renameSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { basename, dirname, isAbsolute, join, resolve, sep } from 'node:path';
import { DaorisError } from './errors.ts';
import { onPath, readJsonObject, readText, writeJsonAtomic } from './fsx.ts';
import type { ExitCode } from './errors.ts';
import { daorisHome, HOME_SENTENCE } from './home.ts';
import { TOOLCHAINS } from './toolchain.ts';
import { isAddress } from './tools.ts';
import type { CommandArgs } from './types.ts';

/** The plugin API this build speaks. Raised only when a plugin written for the new shape cannot work on the old one. */
export const API_VERSION = 1;
export const MANIFEST = 'plugin.json';
export const PLUGINS_DIR = 'plugins';
/** Where a plugin keeps what survives an update: `plugins/.data/<id>`. */
export const DATA_DIR = '.data';
export const STATE_FILE = 'plugins.json';
/** The host telling a plugin where it is — a plugin cannot work out its own folder. */
export const PLACEHOLDER = '${plugin}';
/**
 * The host telling a plugin where it KEEPS things (D77): its data folder, which an update never
 * touches — a browser's signed-in profile, which otherwise lands under the user's profile (D63).
 * Twin: `Plugins.cs`.
 */
export const DATA_PLACEHOLDER = '${data}';

/**
 * Where an installed plugin came from (PLUG9 c, D103): a file in its install folder, written into the
 * staged copy before the swap, so it is replaced with the install, goes with it, and is absent from a
 * folder copied in by hand. The catalogue reads nothing but the manifest. Twin: `PluginSource.FileName`.
 */
export const SOURCE_FILE = '.daoris-source.json';

/**
 * Where an install keeps Daoris's own example plugins (PLUG9 d, D103), as path segments from the
 * install's root: beside `app/`'s application, never under `data/plugins/`, so none is installed until
 * a press. The home is the install's `data/`, so the offers are the home's sibling `app/plugin-offers/`.
 * Twins: `tools/desktop-publish.mjs`'s `PLUGIN_OFFERS`, which lays them out, and `PluginOffers.Folder`.
 */
export const OFFERS_DIR: readonly string[] = Object.freeze(['app', 'plugin-offers']);

/** The heading in an offer's README whose bullets say what it needs on the machine. Twin: `PluginOffers.NeedsHeading`. */
export const NEEDS_HEADING = '## What it needs';

const ID_SHAPE = /^[a-z0-9][a-z0-9.-]*$/;

/**
 * The harness names this build carries, which a plugin may not declare. The toolchain table plus the
 * two gate stubs that have no toolchain — the same set the driver's `AdapterSet.Built()` names.
 *
 * A function rather than a constant: `toolchain.ts` imports this module for `harness list`, and this
 * module imports its table — read at call time, the cycle costs nothing; read at load time, whichever
 * module loaded second would see an empty table.
 */
export function reservedHarnesses(): ReadonlySet<string> {
  return new Set([...Object.keys(TOOLCHAINS), 'stub', 'acp-stub', 'dsh']);
}

/**
 * Whether a command would start, without starting it — the twin of the driver's `CommandPresence`.
 * A declared harness with no version question is asked this rather than run: an ACP agent started
 * bare waits on its stdin.
 */
export function resolvable(command: string, env: Record<string, string | undefined> = process.env): boolean {
  return onPath(command, { env }) !== null;
}

export interface PluginHarness {
  name: string;
  command: string[];
  posture: string | null;
  profileVariable: string | null;
  package: string | null;
  install: string[] | null;
  versionArguments: string[] | null;
  accountOf: string | null;
}

export interface PluginHooks { command: string[]; points: string[] }

/** An MCP server a plugin hands to every session (D65 §1f) — beside the knowledge host, never in its place. */
export interface PluginServer { name: string; command: string[]; env: Record<string, string> }

export interface PluginManifest {
  id: string;
  apiVersion: number;
  name: string;
  version: string;
  description: string;
  harnesses: PluginHarness[];
  hooks: PluginHooks | null;
  servers: PluginServer[];
}

/** The knowledge host's server name — Daoris's own, which a plugin may not claim. */
export const KNOWLEDGE_SERVER = 'daoris-knowledge';

export interface PluginEntry {
  manifest: PluginManifest;
  folder: string;
  data: string;
  /** The person's word (`plugins.json`), independent of whether the plugin is sound. */
  enabled: boolean;
  /** Why it contributes nothing, in a sentence; null when sound. */
  problem: string | null;
}

export interface PluginCatalog {
  plugins: PluginEntry[];
  /** Enabled and sound: what the host drives. */
  contributing: PluginEntry[];
}

export function pluginsRoot(home: string): string {
  return join(home, PLUGINS_DIR);
}

/**
 * A folder copied file by file with explicit reads and writes, never `fs.cpSync` (the contract §8: a
 * documented crash on the Node version in use). Links are not followed or copied (D3).
 */
function copyTree(from: string, to: string): void {
  mkdirSync(to, { recursive: true });
  for (const entry of readdirSync(from, { withFileTypes: true })) {
    const source = join(from, entry.name);
    const target = join(to, entry.name);
    if (entry.isDirectory()) copyTree(source, target);
    else if (entry.isFile()) copyFileSync(source, target);
  }
}

export function dataFolder(home: string, id: string): string {
  return join(pluginsRoot(home), DATA_DIR, id);
}

const empty = (id: string): PluginManifest =>
  ({ id, apiVersion: API_VERSION, name: id, version: '', description: '', harnesses: [], hooks: null, servers: [] });

/** `plugins.json`: which plugins are disabled. An unreadable file disables nothing — the safe direction. */
export function readPluginState(home: string): { disabled: string[] } {
  const { value: parsed } = readJsonObject(join(home, STATE_FILE));
  const rows = Array.isArray(parsed?.disabled) ? parsed.disabled : [];
  return { disabled: rows.filter((row): row is string => typeof row === 'string' && row.length > 0) };
}

/**
 * Written beside and renamed over, like every file Daoris owns — and never over a file it could not
 * read, which would switch back on every plugin the person had switched off.
 */
export function writePluginState(home: string, state: { disabled: string[] }): void {
  const disabled = [...new Set(state.disabled)].sort();
  writeJsonAtomic(join(home, STATE_FILE), { disabled });
}

const same = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();

export function disablePlugin(home: string, id: string): void {
  writePluginState(home, { disabled: [...readPluginState(home).disabled.filter((d) => !same(d, id)), id] });
}

export function enablePlugin(home: string, id: string): void {
  writePluginState(home, { disabled: readPluginState(home).disabled.filter((d) => !same(d, id)) });
}

function text(row: unknown, name: string): string | null {
  if (typeof row !== 'object' || row === null) return null;
  const value = (row as Record<string, unknown>)[name];
  return typeof value === 'string' ? value : null;
}

/**
 * The data folder is the install folder's sibling under `.data`, by the same name: a plugin's folder IS
 * its id. A null folder leaves the placeholders as written (PLUG9: what a person is shown, and what an
 * update compares, is the manifest's own words, never a path on this machine).
 */
function expand(value: string, folder: string | null): string {
  if (folder === null) return value;
  if (!value.includes(PLACEHOLDER) && !value.includes(DATA_PLACEHOLDER)) return value;
  const install = resolve(folder);
  const kept = join(dirname(install), DATA_DIR, basename(install));
  return resolve(value.split(PLACEHOLDER).join(install).split(DATA_PLACEHOLDER).join(kept));
}

/** A string array, with the plugin placeholder expanded to the install folder in every entry; null when absent or not one. */
function strings(row: unknown, name: string, folder: string | null): string[] | null {
  if (typeof row !== 'object' || row === null) return null;
  const value = (row as Record<string, unknown>)[name];
  if (!Array.isArray(value) || !value.every((item) => typeof item === 'string')) return null;
  return (value as string[]).map((item) => expand(item, folder));
}

/**
 * One manifest, read whole — or an empty manifest and the sentence saying why nothing of it is taken.
 *
 * 🔴 The version before anything else: a plugin from a newer API is refused with both numbers, and
 * nothing below it is read.
 *
 * @param asWritten leave `${plugin}` and `${data}` as the manifest writes them — the twin of the
 *   driver's `PluginCatalog.ReadAsWritten` (PLUG9).
 */
export function readManifest(folderName: string, folder: string, asWritten = false): { manifest: PluginManifest; problem: string | null } {
  const base = asWritten ? null : folder;
  let root: unknown;
  try {
    root = JSON.parse(readText(join(folder, MANIFEST)));
  } catch (error) {
    return { manifest: empty(folderName), problem: `\`${MANIFEST}\` could not be read: ${(error as Error).message}` };
  }
  if (typeof root !== 'object' || root === null || Array.isArray(root)) {
    return { manifest: empty(folderName), problem: `\`${MANIFEST}\` is not an object.` };
  }

  const id = text(root, 'id') ?? folderName;
  const declaredVersion = (root as Record<string, unknown>).apiVersion;
  let apiVersion = 1;
  if (declaredVersion !== undefined) {
    if (typeof declaredVersion !== 'number' || !Number.isInteger(declaredVersion)) {
      return { manifest: empty(id), problem: '`apiVersion` must be an integer — this manifest is malformed, not old.' };
    }
    apiVersion = declaredVersion;
    if (apiVersion > API_VERSION) {
      return {
        manifest: { ...empty(id), apiVersion },
        problem: `needs plugin API ${apiVersion}, and this build speaks ${API_VERSION} — update Daoris, `
          + `or use a plugin written for ${API_VERSION}.`,
      };
    }
  }

  if (!ID_SHAPE.test(id)) {
    return { manifest: empty(folderName), problem: `\`id\` must be lowercase letters, digits, dots and dashes — \`${id}\` is not.` };
  }
  if (id !== folderName) {
    return { manifest: empty(id), problem: `\`id\` is \`${id}\` but the folder is \`${folderName}\` — a plugin's folder is its id.` };
  }

  const harnesses: PluginHarness[] = [];
  const declared = (root as Record<string, unknown>).harnesses;
  if (declared !== undefined) {
    if (!Array.isArray(declared)) return { manifest: empty(id), problem: '`harnesses` must be an array.' };
    for (const row of declared) {
      const name = text(row, 'name')?.trim();
      if (!name) return { manifest: empty(id), problem: 'a declared harness needs a `name`.' };
      const command = strings(row, 'command', base);
      if (!command || command.length === 0) {
        return { manifest: empty(id), problem: `harness \`${name}\` needs a \`command\` — what to run.` };
      }
      harnesses.push({
        name,
        command,
        posture: text(row, 'posture'),
        profileVariable: text(row, 'profileVariable'),
        package: text(row, 'package'),
        install: strings(row, 'install', base),
        versionArguments: strings(row, 'versionArguments', base),
        accountOf: text(row, 'accountOf'),
      });
    }
  }

  let hooks: PluginHooks | null = null;
  const spoken = (root as Record<string, unknown>).hooks;
  if (spoken !== undefined) {
    const command = strings(spoken, 'command', base);
    const points = strings(spoken, 'points', base);
    if (!command || command.length === 0 || !points || points.length === 0) {
      return { manifest: empty(id), problem: '`hooks` needs a `command` and the `points` it listens on.' };
    }
    hooks = { command, points };
  }

  const servers: PluginServer[] = [];
  const handed = (root as Record<string, unknown>).servers;
  if (handed !== undefined) {
    if (!Array.isArray(handed)) return { manifest: empty(id), problem: '`servers` must be an array.' };
    for (const row of handed) {
      const name = text(row, 'name')?.trim();
      if (!name || !ID_SHAPE.test(name)) {
        return { manifest: empty(id), problem: 'a declared server needs a `name` — lowercase letters, digits, dots and dashes; it is what the agent calls it.' };
      }
      const command = strings(row, 'command', base);
      if (!command || command.length === 0) {
        return { manifest: empty(id), problem: `server \`${name}\` needs a \`command\` — what to run.` };
      }
      const env: Record<string, string> = {};
      const declared = typeof row === 'object' && row !== null ? (row as Record<string, unknown>).env : undefined;
      if (declared !== undefined) {
        if (typeof declared !== 'object' || declared === null || Array.isArray(declared)
          || !Object.values(declared).every((value) => typeof value === 'string')) {
          return { manifest: empty(id), problem: `server \`${name}\`'s \`env\` must be an object of strings.` };
        }
        for (const [key, value] of Object.entries(declared as Record<string, string>)) env[key] = expand(value, base);
      }
      servers.push({ name, command, env });
    }
  }

  return {
    manifest: {
      id,
      apiVersion,
      name: text(root, 'name') ?? id,
      version: text(root, 'version') ?? '',
      description: text(root, 'description') ?? '',
      harnesses,
      hooks,
      servers,
    },
    problem: null,
  };
}

/**
 * What in a manifest this build refuses, whatever else is installed: a harness it already carries, or
 * a server under the knowledge host's name. Case-blind, as every name here is. The catalogue and
 * `plugin add` ask this one question, so they refuse the same plugin in the same words (REV3 CLEAN1:
 * `add` compared harness names case-sensitively, and copied in a plugin the catalogue then refused).
 */
function refusedByThisBuild(manifest: PluginManifest, reserved: Iterable<string>): string | null {
  const taken = new Set([...reserved].map((name) => name.toLowerCase()));
  const harness = manifest.harnesses.find((declared) => taken.has(declared.name.toLowerCase()));
  if (harness) {
    return `declares harness \`${harness.name}\`, which this build already carries — `
      + 'a plugin adds a harness and never replaces one.';
  }
  const server = manifest.servers.find((declared) => declared.name.toLowerCase() === KNOWLEDGE_SERVER);
  if (server) {
    return `declares server \`${server.name}\`, which is Daoris's own knowledge host — `
      + 'a plugin hands a session servers beside it, never in its place.';
  }
  return null;
}

/**
 * The home's plugins, in folder order by id — the host drives whatever is here and names no plugin.
 *
 * @param reserved the harness names this build carries; a plugin declaring one is refused naming both.
 */
export function readPlugins(home: string, reserved: Iterable<string> = reservedHarnesses()): PluginCatalog {
  const root = pluginsRoot(home);
  const plugins: PluginEntry[] = [];
  if (!existsSync(root)) return { plugins, contributing: [] };

  const disabled = new Set(readPluginState(home).disabled.map((d) => d.toLowerCase()));
  const declaredBy = new Map<string, string>();
  const servedBy = new Map<string, string>();

  for (const folderName of readdirSync(root).sort()) {
    // `.data/` and any other dot-folder is the catalogue's own, never a plugin.
    if (folderName.startsWith('.')) continue;
    const folder = join(root, folderName);
    if (!statSync(folder).isDirectory() || !existsSync(join(folder, MANIFEST))) continue;

    let { manifest, problem } = readManifest(folderName, folder);
    const enabled = !disabled.has(manifest.id.toLowerCase());

    if (problem === null && enabled) {
      problem = refusedByThisBuild(manifest, reserved);
      for (const harness of problem === null ? manifest.harnesses : []) {
        const key = harness.name.toLowerCase();
        const other = declaredBy.get(key);
        if (other) {
          problem = `declares harness \`${harness.name}\`, which plugin \`${other}\` already declares — `
            + 'the first by id keeps it, and this plugin contributes nothing.';
          break;
        }
      }
      // A server's name is what the agent calls it; two plugins claiming one would give a session
      // two tools under one name. The knowledge host's name is Daoris's own.
      for (const server of problem === null ? manifest.servers : []) {
        const key = server.name.toLowerCase();
        const other = servedBy.get(key);
        if (other) {
          problem = `declares server \`${server.name}\`, which plugin \`${other}\` already declares — `
            + 'the first by id keeps it, and this plugin contributes nothing.';
          break;
        }
      }
      if (problem === null) {
        for (const harness of manifest.harnesses) declaredBy.set(harness.name.toLowerCase(), manifest.id);
        for (const server of manifest.servers) servedBy.set(server.name.toLowerCase(), manifest.id);
      }
    }

    // Nothing of a refused plugin is taken — not a harness, not a hook, not a server.
    if (problem !== null) manifest = { ...manifest, harnesses: [], hooks: null, servers: [] };

    plugins.push({ manifest, folder, data: dataFolder(home, manifest.id), enabled, problem });
  }

  return { plugins, contributing: plugins.filter((p) => p.enabled && p.problem === null) };
}

// ——— Where an installed plugin came from (PLUG9 c, D103). Twin: `PluginSource` in `PluginInstall.cs`, whose
// `PluginSourceTests` holds the table `plugin-sources.test.ts` parses and holds this reader to.

/**
 * The package a plugin was installed from (PLUGDIST1a, D120 §4, the distribution design §5.7 step 5): its package
 * id, its version, the SHA-512 of the package file in standard base64, and the source it came from — a package
 * source's index address, or the whole path of the folder that held the file. Only the driver reads a package
 * (D120 §4); this side reads and lists the record it leaves. Twin: `PluginPackageOrigin`.
 */
export interface PackageOrigin { package: string; version: string; sha512: string; source: string }

/** The folder a plugin was added from (a whole path), the offer of this install it was installed from (an id), or its package. */
export type PluginSource = { folder: string } | { offer: string } | PackageOrigin;

/** A NuGet package id: words of letters, digits and underscores joined by dots or dashes, at most 100 characters. Twin: `PluginSource.IsPackageId`. */
const PACKAGE_ID = /^[A-Za-z0-9_]+(?:[.-][A-Za-z0-9_]+)*$/;

/** A package's version: one to four numbers, then a prerelease label and build metadata, each optional. Twin: `PluginSource.IsPackageVersion`. */
const PACKAGE_VERSION = /^[0-9]+(?:\.[0-9]+){0,3}(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/;

/** A SHA-512 in standard base64: 64 bytes are 86 characters and two of padding. */
const SHA512 = /^[A-Za-z0-9+/]{86}==$/;

export const isPackageId = (id: string): boolean => id.length <= 100 && PACKAGE_ID.test(id);

export const isPackageVersion = (version: string): boolean => PACKAGE_VERSION.test(version);

/**
 * An installed plugin's record of where it came from: none for a plugin added before Daoris kept one or
 * copied in by hand, which is said and never guessed; a named problem for a record that does not read.
 */
export function readPluginSource(installFolder: string): { source: PluginSource | null; problem: string | null } {
  const path = join(installFolder, SOURCE_FILE);
  if (!existsSync(path)) return { source: null, problem: null };
  const unread = (why: string) => ({ source: null, problem: `\`${SOURCE_FILE}\` does not read (${why}).` });
  let root: unknown;
  try {
    root = JSON.parse(readText(path));
  } catch (error) {
    return unread(`it is not JSON: ${(error as Error).message}`);
  }
  if (typeof root !== 'object' || root === null || Array.isArray(root)) return unread('it is not a JSON object');
  const folder = text(root, 'folder');
  const offer = text(root, 'offer');
  const pkg = text(root, 'package');
  if (pkg !== null && folder !== null) return unread('it names both a package and a folder');
  if (pkg !== null && offer !== null) return unread('it names both a package and an offer');
  if (folder !== null && offer !== null) return unread('it names both a folder and an offer');
  if (folder !== null) return isAbsolute(folder) ? { source: { folder }, problem: null } : unread('its folder is not a whole path');
  if (offer !== null) return ID_SHAPE.test(offer) ? { source: { offer }, problem: null } : unread('its offer is not a plugin id');
  if (pkg === null) return unread('it names no folder, offer or package');

  // A package's record (PLUGDIST1a), each field judged in the driver's order and words.
  if (!isPackageId(pkg)) return unread(`its package \`${pkg}\` is not a package id`);
  const version = text(root, 'version');
  if (version === null) return unread('a package needs its `version`');
  if (!isPackageVersion(version)) return unread(`its version \`${version}\` is not a package version`);
  const sha512 = text(root, 'sha512');
  if (sha512 === null) return unread('a package needs its `sha512`');
  if (!SHA512.test(sha512)) return unread('its sha512 is not a SHA-512 hash in base64');
  const source = text(root, 'source');
  if (source === null) return unread('a package needs its `source`');
  if (!isAddress(source) && !isAbsolute(source)) {
    return unread(`its source \`${source}\` is neither a whole path nor an address — https://, or http:// to this machine`);
  }
  return { source: { package: pkg, version, sha512, source }, problem: null };
}

/** Written into a staged copy before it is renamed into place, so the record and the install move together. */
function writePluginSource(installFolder: string, source: PluginSource): void {
  writeFileSync(join(installFolder, SOURCE_FILE), `${JSON.stringify(source, null, 2)}\n`, 'utf8');
}

/** Where a plugin came from, said for a person: a package as the distribution design §6.2 says it. */
function sourceSaid(source: PluginSource): string {
  if ('package' in source) return `${source.source}, package \`${source.package}\` ${source.version}`;
  return 'offer' in source
    ? `Daoris's own plugins, offered by this install (\`${source.offer}\`)`
    : source.folder;
}

// ——— Daoris's own plugins, offered by the install (PLUG9 d, D103). Twin: `PluginOffers.cs`.

/** The install's offers: `app/plugin-offers/` beside the home, which an install makes its `data/`. */
export function offersFolder(home: string): string {
  return join(dirname(resolve(home)), ...OFFERS_DIR);
}

/** One of Daoris's own plugins the install offers, read as the catalogue reads one, placeholders as written. */
export interface PluginOffer {
  id: string;
  folder: string;
  manifest: PluginManifest;
  /** Why it could not be installed as it stands, in the catalogue's words; null when sound. */
  problem: string | null;
  /** What it needs on the machine: its README's own requirement lines. */
  needs: string[];
  /** Whether a plugin of this id is installed here already. */
  installed: boolean;
}

/**
 * What a plugin needs on the machine, in its README's words: the bullets under `## What it needs`, a
 * wrapped bullet joined into one line. Emphasis is dropped, since neither door draws it; code is kept.
 */
export function readNeeds(folder: string): string[] {
  const path = join(folder, 'README.md');
  if (!existsSync(path)) return [];
  const lines = readText(path).split('\n');
  const start = lines.findIndex((line) => line.trimEnd() === NEEDS_HEADING);
  if (start < 0) return [];
  const needs: string[] = [];
  for (const raw of lines.slice(start + 1)) {
    if (raw.startsWith('#')) break;
    const line = raw.trim();
    if (line.length === 0) continue;
    if (/^[-*] /.test(line)) needs.push(line.slice(2).trim());
    else if (needs.length > 0 && /^\s/.test(raw)) needs[needs.length - 1] += ` ${line}`;
  }
  return needs.map((need) => need.replace(/\*\*([^*]+)\*\*/g, '$1'));
}

/** The install's offers, by folder name; an unsound one is listed with why, as the catalogue lists a plugin. */
export function readOffers(home: string, reserved: Iterable<string> = reservedHarnesses()): PluginOffer[] {
  const root = offersFolder(home);
  if (!existsSync(root)) return [];
  const offers: PluginOffer[] = [];
  for (const name of readdirSync(root).sort()) {
    if (name.startsWith('.')) continue;
    const folder = join(root, name);
    if (!statSync(folder).isDirectory() || !existsSync(join(folder, MANIFEST))) continue;
    const read = readManifest(name, folder, true);
    const problem = read.problem ?? refusedByThisBuild(read.manifest, reserved);
    offers.push({
      id: name,
      folder,
      manifest: read.manifest,
      problem,
      needs: readNeeds(folder),
      installed: existsSync(join(pluginsRoot(home), name, MANIFEST)),
    });
  }
  return offers;
}

// ——— Update (PLUG9 c, D103). Twin: `PluginInstall.PlanUpdate` and `PluginInstall.Update`.

/** One thing an update changes, each side said as its manifest writes it; empty is none. */
export interface PluginChange { what: 'version' | 'command' | 'points' | 'harnesses' | 'servers'; was: string; now: string }

export interface UpdatePlan {
  id: string;
  source: PluginSource;
  /** The folder the update copies from, resolved on this machine. */
  from: string;
  changes: PluginChange[];
}

const within = (path: string, folder: string): boolean => {
  const [a, b] = process.platform === 'win32' ? [path.toLowerCase(), folder.toLowerCase()] : [path, folder];
  return a === b || a.startsWith(b + sep);
};

/**
 * Why a folder is no place to take a plugin from, as it stands to the home — the driver's
 * `PluginInstall.Placement`, word for word: inside it is Daoris's own, and one holding it would be
 * copied into itself. Null when neither. `resolve` leaves no trailing separator but a root's.
 */
export function placement(home: string, folder: string): string | null {
  const from = resolve(folder);
  const own = resolve(home);
  if (within(from, own)) {
    return `${folder} is inside Daoris's home, which holds what Daoris installed and kept — add a plugin from the repository that holds it.`;
  }
  if (within(own, from)) {
    return `${folder} holds Daoris's home, and copying it would copy the home into itself — name the plugin's own folder.`;
  }
  return null;
}

/** A command as a terminal takes it: a word holding a space quoted, as the driver spells it. */
const line = (command: readonly string[] | null | undefined): string =>
  (command ?? []).map((word) => (word.includes(' ') ? `"${word}"` : word)).join(' ');

/** What an update changes, from what is installed to what the source now holds, each as written. */
export function changesBetween(was: PluginManifest, now: PluginManifest): PluginChange[] {
  const said = (manifest: PluginManifest): Record<PluginChange['what'], string> => ({
    version: manifest.version,
    command: line(manifest.hooks?.command),
    points: (manifest.hooks?.points ?? []).join(', '),
    harnesses: manifest.harnesses.map((harness) => `${harness.name} (${line(harness.command)})`).join('; '),
    servers: manifest.servers.map((server) => `${server.name} (${line(server.command)})`).join('; '),
  });
  const before = said(was);
  const after = said(now);
  return (['version', 'command', 'points', 'harnesses', 'servers'] as const)
    .filter((what) => before[what] !== after[what])
    .map((what) => ({ what, was: before[what], now: after[what] }));
}

const NOT_AN_ID = (id: string) => `\`${id}\` is not a plugin id — one is lowercase letters, digits, dots and dashes, `
  + 'like `acme.quiet-hours`; `daoris plugin list` shows what there is.';

/**
 * What updating an installed plugin would do, or why it cannot: its record, the source it names read by
 * the catalogue's own reader, the same plugin by id, nothing this build refuses — and what changes.
 * The refusals come in the driver's order and words (twin rule 5), and nothing is written.
 */
export function planUpdate(home: string, id: string, reserved: Iterable<string> = reservedHarnesses()):
  { plan: UpdatePlan | null; refusal: string | null } {
  const refuse = (refusal: string) => ({ plan: null, refusal });
  if (!ID_SHAPE.test(id.toLowerCase())) return refuse(NOT_AN_ID(id));
  const entry = readPlugins(home, reserved).plugins.find((plugin) => same(basename(plugin.folder), id));
  if (!entry) return refuse(`no plugin \`${id}\` on this machine — \`daoris plugin list\` shows what there is.`);
  const installed = basename(entry.folder);

  const record = readPluginSource(entry.folder);
  if (record.problem !== null) {
    return refuse(`plugin \`${installed}\`'s record of where it came from does not read, so there is nothing to update `
      + `it from: ${record.problem} \`daoris plugin add <folder>\` replaces it and records it again.`);
  }
  if (record.source === null) {
    return refuse(`plugin \`${installed}\` has no record of where it came from: it was added before Daoris kept one, or `
      + 'copied in by hand, so there is nothing to update it from. `daoris plugin add <folder>` replaces it wholesale '
      + 'and records where it came from.');
  }
  const source = record.source;

  // PLUGDIST1a: a package is installed whole, and only the driver reads one (D120 §4); the driver's
  // `PluginInstall.PlanUpdate` says the same, word for word.
  if ('package' in source) {
    return refuse(`plugin \`${installed}\` came from ${sourceSaid(source)}, and a plugin from a package is installed whole. `
      + `A newer package takes its place: \`daoris plugin remove ${installed}\`, then `
      + '`daoris-driver plugins install <file.nupkg>`, and what it kept stays where it is.');
  }

  let from: string;
  if ('offer' in source) {
    from = join(offersFolder(home), source.offer);
    if (!existsSync(join(from, MANIFEST))) {
      return refuse(`Daoris's own \`${source.offer}\` is not offered by this install any more, so there is nothing to `
        + `update \`${installed}\` from. It stays as it is; \`daoris plugin remove ${installed}\` takes it out.`);
    }
  } else {
    from = source.folder;
  }

  const misplaced = placement(home, from);
  if (misplaced !== null) return refuse(misplaced);
  if (!existsSync(from) || !statSync(from).isDirectory()) {
    return refuse(`the folder \`${installed}\` was added from is not there any more: ${from}. \`daoris plugin add <folder>\` `
      + 'from where it is now replaces it and records the new place.');
  }
  if (!existsSync(join(from, MANIFEST))) {
    return refuse(`no \`${MANIFEST}\` in ${from} — a plugin is a folder with a manifest at its root.`);
  }

  // Read as the catalogue would, named by its own id, which is then held to the installed one.
  const probe = readJsonObject(join(from, MANIFEST));
  const named = typeof probe.value?.id === 'string' ? probe.value.id : '';
  const next = readManifest(named, from, true);
  if (next.problem !== null) return refuse(`\`${MANIFEST}\` in ${from}: ${next.problem}`);
  if (!same(next.manifest.id, installed)) {
    return refuse(`${from} now holds plugin \`${next.manifest.id}\`, not \`${installed}\` — an update replaces a plugin `
      + 'with its own next version, never with another plugin.');
  }
  const refused = refusedByThisBuild(next.manifest, reserved);
  if (refused !== null) return refuse(`plugin \`${installed}\` ${refused}`);

  const current = readManifest(installed, entry.folder, true).manifest;
  return { plan: { id: installed, source, from, changes: changesBetween(current, next.manifest) }, refusal: null };
}

/**
 * Update an installed plugin from its source: judged again, copied beside, the record written into the
 * copy, the installed folder moved aside WHOLE and the copy renamed into place. `.data/` is its sibling
 * and never touched. A folder something holds open is refused whole, the installed version untouched.
 */
export function applyUpdate(home: string, id: string, reserved: Iterable<string> = reservedHarnesses()): UpdatePlan {
  const { plan, refusal } = planUpdate(home, id, reserved);
  if (plan === null) throw new DaorisError(`${refusal} Nothing was replaced.`);

  const root = pluginsRoot(home);
  const target = join(root, plan.id);
  const stamp = `${process.pid}-${Date.now()}`;
  const staging = join(root, `.updating-${plan.id}-${stamp}`);
  copyTree(plan.from, staging);
  writePluginSource(staging, plan.source);
  const aside = join(root, `.removing-${plan.id}-${stamp}`);
  try {
    renameSync(target, aside);
  } catch {
    rmSync(staging, { recursive: true, force: true });
    throw new DaorisError(
      `\`${plan.id}\` was not updated: something on this machine still has its folder open — a running desktop's `
      + `hook process, most likely. \`daoris plugin disable ${plan.id}\`, give the desktop a moment to stop it, then `
      + 'update it again. The installed version is untouched.');
  }
  try {
    renameSync(staging, target);
  } catch (error) {
    renameSync(aside, target);
    rmSync(staging, { recursive: true, force: true });
    throw new DaorisError(`\`${plan.id}\` was not updated: ${(error as Error).message} The installed version is untouched.`);
  }
  try {
    rmSync(aside, { recursive: true, force: true });
  } catch {
    // A dot-folder is never read as a plugin; one that cannot be deleted now is nobody's.
  }
  return plan;
}

/** The home, or the refusal every management verb here gives without one (D63). */
function requireHome(): string {
  const home = daorisHome();
  if (!home) throw new DaorisError(`${HOME_SENTENCE} (wanted: ${PLUGINS_DIR}/)`);
  return home;
}

function requireId(argv: string[], verb: string): string {
  const id = argv[1];
  if (!id || id.startsWith('--')) {
    throw new DaorisError(`\`plugin ${verb}\` needs a plugin id — e.g. \`daoris plugin ${verb} acme.quiet-hours\`.`);
  }
  // 🔴 Before the id becomes a path: `remove` joins it under plugins/ and deletes recursively, so `..`
  // was the whole home and `.` every plugin's kept data (REV3). An installed id always has this shape.
  if (!ID_SHAPE.test(id.toLowerCase())) throw new DaorisError(NOT_AN_ID(id));
  return id;
}

function describe(manifest: PluginManifest): string {
  const parts: string[] = [];
  if (manifest.harnesses.length > 0) {
    parts.push(`declares ${manifest.harnesses.map((h) => h.name).join(', ')}`);
  }
  if (manifest.hooks) parts.push(`speaks on ${manifest.hooks.points.join(', ')}`);
  if (manifest.servers.length > 0) {
    parts.push(`hands sessions ${manifest.servers.map((s) => s.name).join(', ')}`);
  }
  return parts.length > 0 ? parts.join('; ') : 'declares nothing and speaks nothing';
}

/** Where an installed plugin came from, for its line in `list`: never a guess where there is no record. */
function sourceLine(installFolder: string): string {
  const { source, problem } = readPluginSource(installFolder);
  if (problem !== null) return `⚠ ${problem}`;
  if (source === null) {
    return 'no record of where it came from — added before Daoris kept one, or copied in by hand; '
      + '`daoris plugin add <folder>` records it';
  }
  // A package is installed whole, so `update` is not offered for one it would refuse (PLUGDIST1a).
  if ('package' in source) return `from ${sourceSaid(source)}`;
  return `from ${sourceSaid(source)}; \`daoris plugin update ${basename(installFolder)}\` takes a newer one`;
}

/**
 * Read, install, remove, or switch this machine's plugins.
 *
 * @remarks
 * `add` copies a folder in under its manifest's id, replacing the install wholesale and leaving
 * `.data/<id>` exactly where it was — an update never touches what a plugin keeps. `remove` takes the
 * install folder and NAMES the data folder rather than deleting it: what a plugin kept is the person's
 * to throw away, the same judgement Forget makes for an account.
 */
export function commandPlugin({ argv, write }: CommandArgs): ExitCode {
  const verb = argv[0] ?? 'list';

  switch (verb) {
    case 'list': {
      const home = requireHome();
      const catalog = readPlugins(home);
      // The install's own plugins not installed here (PLUG9 d), listed where the screen lists them.
      const offered = readOffers(home).filter((offer) => !offer.installed);
      if (catalog.plugins.length === 0) {
        write(`daoris: no plugins on this machine — a plugin is a folder under ${pluginsRoot(home)}`);
        write('  with a `plugin.json`; `daoris plugin add <folder>` copies one in.');
      } else {
        write(`daoris: ${pluginsRoot(home)}`);
        for (const entry of catalog.plugins) {
          const version = entry.manifest.version ? ` ${entry.manifest.version}` : '';
          const state = entry.enabled ? '' : '  (off)';
          write(`  ${entry.manifest.id.padEnd(24)} ${entry.manifest.name}${version}${state}`);
          write(`  ${''.padEnd(24)} ${entry.problem ? `⚠ ${entry.problem}` : describe(entry.manifest)}`);
          write(`  ${''.padEnd(24)} ${sourceLine(entry.folder)}`);
        }
      }

      if (offered.length > 0) {
        write(`daoris: Daoris's own plugins, offered by this install, not installed (${offersFolder(home)}):`);
        for (const offer of offered) {
          const version = offer.manifest.version ? ` ${offer.manifest.version}` : '';
          write(`  ${offer.id.padEnd(24)} ${offer.manifest.name}${version}`);
          write(`  ${''.padEnd(24)} ${offer.problem ? `⚠ ${offer.problem}` : describe(offer.manifest)}`);
          if (offer.needs.length > 0) write(`  ${''.padEnd(24)} needs: ${offer.needs.join('; ')}`);
          if (offer.problem === null) {
            write(`  ${''.padEnd(24)} \`daoris plugin add --offer ${offer.id}\` installs it; nothing runs before that.`);
          }
        }
      }
      return 0;
    }

    case 'add': {
      // Twin: the driver's `PluginInstall.cs`, which Ask Daoris's plugin card applies (PLUG9). Same
      // refusals, same staged copy; it adds and never replaces, where this replaces wholesale.
      const home = requireHome();
      let from: string;
      let recorded: PluginSource;
      const offerAt = argv.indexOf('--offer');
      if (offerAt !== -1) {
        // One of Daoris's own, by its id (PLUG9 d): the same copy as a folder's, the offer recorded.
        const wanted = argv[offerAt + 1];
        if (!wanted || wanted.startsWith('--')) {
          throw new DaorisError('`--offer` names one of Daoris\'s own plugins by its id — `daoris plugin list` shows what this install offers.');
        }
        if (!ID_SHAPE.test(wanted.toLowerCase())) throw new DaorisError(NOT_AN_ID(wanted));
        const offers = readOffers(home);
        const offer = offers.find((each) => same(each.id, wanted));
        if (!offer) {
          throw new DaorisError(`this install offers no plugin \`${wanted}\` — `
            + (offers.length > 0 ? `it offers ${offers.map((each) => `\`${each.id}\``).join(', ')}.` : 'it offers none.'));
        }
        from = offer.folder;
        recorded = { offer: offer.id };
      } else {
        const source = argv[1];
        if (!source || source.startsWith('--')) {
          throw new DaorisError('`plugin add` needs a folder holding a `plugin.json` — e.g. `daoris plugin add ./acme.quiet-hours`.');
        }
        from = isAbsolute(source) ? source : resolve(process.cwd(), source);
        recorded = { folder: resolve(from) };
      }
      if (!existsSync(join(from, MANIFEST))) {
        throw new DaorisError(`no \`${MANIFEST}\` in ${from} — a plugin is a folder with a manifest at its root.`);
      }

      // Read as the catalogue would, with the target folder's name as the folder — the id decides
      // where it lands, so the source folder may be called anything.
      // A broken manifest is a named problem, never a crash (plugin design, rule 2) — a bare parse here
      // threw past the dispatcher as a stack trace (REV3).
      const probe = readJsonObject(join(from, MANIFEST));
      if (probe.problem !== null) throw new DaorisError(`${probe.problem}. Nothing was copied.`);
      const id = typeof probe.value?.id === 'string' ? probe.value.id : '';
      const { manifest, problem } = readManifest(id, from);
      if (problem !== null) {
        throw new DaorisError(`${MANIFEST} in ${from}: ${problem} Nothing was copied.`);
      }
      const refused = refusedByThisBuild(manifest, reservedHarnesses());
      if (refused !== null) throw new DaorisError(`plugin \`${manifest.id}\` ${refused} Nothing was copied.`);

      const target = join(pluginsRoot(home), manifest.id);
      if (resolve(from) === resolve(target)) {
        throw new DaorisError(`${from} is already the installed folder for \`${manifest.id}\`.`);
      }
      const replacing = existsSync(target);
      // 🔴 Replaced wholesale, never merged: a stale file from the previous install is exactly the
      // kind of thing that makes "which version is running" unanswerable. `.data/` is beside it.
      // Copied in beside first, and the old folder moved aside WHOLE (REV3): deleting it first left
      // no plugin at all when its folder was held, or when the copy then failed.
      mkdirSync(pluginsRoot(home), { recursive: true });
      const staging = join(pluginsRoot(home), `.adding-${manifest.id}-${process.pid}-${Date.now()}`);
      copyTree(from, staging);
      // Where it came from, in the copy before it is renamed in (PLUG9 c): the record and the install
      // are one move, so neither can outlive the other.
      writePluginSource(staging, recorded);
      if (replacing) {
        const aside = join(pluginsRoot(home), `.removing-${manifest.id}-${process.pid}-${Date.now()}`);
        try {
          renameSync(target, aside);
        } catch {
          rmSync(staging, { recursive: true, force: true });
          throw new DaorisError(
            `\`${manifest.id}\` was not replaced: something on this machine still has its folder open — a `
            + `running desktop's hook process, most likely. \`daoris plugin disable ${manifest.id}\`, give the `
            + 'desktop a moment to stop it, then add it again. The installed version is untouched.');
        }
        renameSync(staging, target);
        try {
          rmSync(aside, { recursive: true, force: true });
        } catch {
          // A dot-folder is never read as a plugin; one that cannot be deleted now is nobody's.
        }
      } else {
        renameSync(staging, target);
      }

      write(`daoris: ${replacing ? 'replaced' : 'added'} plugin \`${manifest.id}\` at ${target}`);
      write(`  ${describe(manifest)}.`);
      write(`  From ${sourceSaid(recorded)}; \`daoris plugin update ${manifest.id}\` takes a newer one from there.`);
      if (existsSync(dataFolder(home, manifest.id))) write(`  ${dataFolder(home, manifest.id)} kept, untouched.`);
      write('  It takes effect at the driver\'s next look — a harness on the roster, a hook at the next tick.');
      return 0;
    }

    case 'update': {
      // PLUG9 (c): the question is the command without --yes, as `agent trust` asks one. A terminal
      // cannot prompt, and replacing on the first keystroke would show nothing before the press.
      const home = requireHome();
      const id = requireId(argv, 'update');
      const { plan, refusal } = planUpdate(home, id);
      if (plan === null) throw new DaorisError(`${refusal} Nothing was replaced.`);

      write(`daoris: plugin \`${plan.id}\`, from ${sourceSaid(plan.source)}:`);
      if (plan.changes.length === 0) {
        write('  What it declares does not change; its files are replaced from there.');
      } else {
        for (const change of plan.changes) {
          write(`  ${change.what.padEnd(10)} ${change.was || '(none)'} → ${change.now || '(none)'}`);
        }
      }
      if (!argv.includes('--yes')) {
        write('  Not updated: this is what would change. Run it again with --yes to update it.');
        return 1;
      }

      applyUpdate(home, id);
      write(`daoris: updated plugin \`${plan.id}\` from ${plan.from}.`);
      if (existsSync(dataFolder(home, plan.id))) write(`  ${dataFolder(home, plan.id)} kept, untouched.`);
      write('  It takes effect at the driver\'s next look — its hook, if it speaks, restarted at the next tick.');
      return 0;
    }

    case 'remove': {
      const home = requireHome();
      const id = requireId(argv, 'remove');
      const target = join(pluginsRoot(home), id);
      if (!existsSync(target)) {
        write(`daoris: no plugin \`${id}\` on this machine — nothing to remove.`);
        return 0;
      }
      // 🔴 Moved aside WHOLE, then deleted (REV3): a running desktop's hook process holds its plugin's
      // folder on Windows, and a recursive delete took every file it could before failing — a plugin
      // with no manifest, neither there nor gone. A dot-folder is never read as a plugin.
      const aside = join(pluginsRoot(home), `.removing-${id}-${process.pid}-${Date.now()}`);
      try {
        renameSync(target, aside);
      } catch {
        throw new DaorisError(
          `\`${id}\` was not removed: something on this machine still has its folder open — a running `
          + `desktop's hook process, most likely. \`daoris plugin disable ${id}\`, give the desktop a moment `
          + 'to stop it, then remove it again. Nothing was taken.');
      }
      try {
        rmSync(aside, { recursive: true, force: true });
      } catch {
        // Aside is invisible to the catalogue; a delete that cannot finish leaves no plugin behind.
      }
      enablePlugin(home, id);
      write(`daoris: plugin \`${id}\` removed from this machine.`);
      const data = dataFolder(home, id);
      if (existsSync(data)) {
        write(`  What it kept is still at ${data} — yours to delete, never Daoris's.`);
      }
      return 0;
    }

    case 'enable':
    case 'disable': {
      const home = requireHome();
      const id = requireId(argv, verb);
      const entry = readPlugins(home).plugins.find((p) => same(p.manifest.id, id));
      if (!entry) {
        throw new DaorisError(`no plugin \`${id}\` on this machine — \`daoris plugin list\` shows what there is.`);
      }
      if (verb === 'enable') enablePlugin(home, entry.manifest.id);
      else disablePlugin(home, entry.manifest.id);
      write(`daoris: plugin \`${entry.manifest.id}\` is ${verb === 'enable' ? 'on' : 'off'} on this machine.`);
      if (verb === 'disable') {
        write('  It stays where it is, and so does what it kept; the driver stops asking it at its next look.');
      } else if (entry.problem) {
        write(`  ⚠ ${entry.problem}`);
      }
      return 0;
    }

    // The kit a plugin is made with (PLUG8, D101) lives with the code that starts plugins and speaks
    // their wire, so what `try` checks is what the driver does: this module spawns nothing, and the
    // samples a new plugin carries are the driver's own frames. Asked for here, it says where it is.
    case 'new':
    case 'try':
      throw new DaorisError(
        `the plugin kit is \`daoris-driver plugins ${verb}\`, or Settings → Plugins — it starts a plugin as the `
        + 'driver would, so it lives with the driver. `daoris-driver plugins` says what each takes.');

    // A plugin package (PLUGDIST1a, D120 §4) is read by the driver alone: this side reads and lists the
    // record a package leaves, and says where one is installed.
    case 'install':
      throw new DaorisError(
        'a plugin package is installed by `daoris-driver plugins install <file.nupkg>` — the driver reads a package '
        + 'and checks it before anything is extracted. `daoris plugin add <folder>` installs a plugin\'s folder.');

    default:
      throw new DaorisError(`unknown plugin verb '${verb}' — one of: list, add, update, remove, enable, disable (and new, try: the kit)`);
  }
}

