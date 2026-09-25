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

import { copyFileSync, existsSync, mkdirSync, readdirSync, renameSync, rmSync, statSync } from 'node:fs';
import { isAbsolute, join, resolve } from 'node:path';
import { DaorisError } from './errors.ts';
import { onPath, readJsonObject, readText, writeJsonAtomic } from './fsx.ts';
import type { ExitCode } from './errors.ts';
import { daorisHome, HOME_SENTENCE } from './home.ts';
import { TOOLCHAINS } from './toolchain.ts';
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

function expand(value: string, folder: string): string {
  return value.includes(PLACEHOLDER) ? resolve(value.split(PLACEHOLDER).join(folder)) : value;
}

/** A string array, with the plugin placeholder expanded to the install folder in every entry; null when absent or not one. */
function strings(row: unknown, name: string, folder: string): string[] | null {
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
 */
export function readManifest(folderName: string, folder: string): { manifest: PluginManifest; problem: string | null } {
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
      const command = strings(row, 'command', folder);
      if (!command || command.length === 0) {
        return { manifest: empty(id), problem: `harness \`${name}\` needs a \`command\` — what to run.` };
      }
      harnesses.push({
        name,
        command,
        posture: text(row, 'posture'),
        profileVariable: text(row, 'profileVariable'),
        package: text(row, 'package'),
        install: strings(row, 'install', folder),
        versionArguments: strings(row, 'versionArguments', folder),
        accountOf: text(row, 'accountOf'),
      });
    }
  }

  let hooks: PluginHooks | null = null;
  const spoken = (root as Record<string, unknown>).hooks;
  if (spoken !== undefined) {
    const command = strings(spoken, 'command', folder);
    const points = strings(spoken, 'points', folder);
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
      const command = strings(row, 'command', folder);
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
        for (const [key, value] of Object.entries(declared as Record<string, string>)) env[key] = expand(value, folder);
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
  if (!ID_SHAPE.test(id.toLowerCase())) {
    throw new DaorisError(`\`${id}\` is not a plugin id — one is lowercase letters, digits, dots and dashes, `
      + 'like `acme.quiet-hours`; `daoris plugin list` shows what there is.');
  }
  return id;
}

function describe(entry: PluginEntry): string {
  const parts: string[] = [];
  if (entry.manifest.harnesses.length > 0) {
    parts.push(`declares ${entry.manifest.harnesses.map((h) => h.name).join(', ')}`);
  }
  if (entry.manifest.hooks) parts.push(`speaks on ${entry.manifest.hooks.points.join(', ')}`);
  if (entry.manifest.servers.length > 0) {
    parts.push(`hands sessions ${entry.manifest.servers.map((s) => s.name).join(', ')}`);
  }
  return parts.length > 0 ? parts.join('; ') : 'declares nothing and speaks nothing';
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
      if (catalog.plugins.length === 0) {
        write(`daoris: no plugins on this machine — a plugin is a folder under ${pluginsRoot(home)}`);
        write('  with a `plugin.json`; `daoris plugin add <folder>` copies one in.');
        return 0;
      }

      write(`daoris: ${pluginsRoot(home)}`);
      for (const entry of catalog.plugins) {
        const version = entry.manifest.version ? ` ${entry.manifest.version}` : '';
        const state = entry.enabled ? '' : '  (off)';
        write(`  ${entry.manifest.id.padEnd(24)} ${entry.manifest.name}${version}${state}`);
        write(`  ${''.padEnd(24)} ${entry.problem ? `⚠ ${entry.problem}` : describe(entry)}`);
      }
      return 0;
    }

    case 'add': {
      const home = requireHome();
      const source = argv[1];
      if (!source || source.startsWith('--')) {
        throw new DaorisError('`plugin add` needs a folder holding a `plugin.json` — e.g. `daoris plugin add ./acme.quiet-hours`.');
      }
      const from = isAbsolute(source) ? source : resolve(process.cwd(), source);
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
      write(`  ${describe({ manifest, folder: target, data: dataFolder(home, manifest.id), enabled: true, problem: null })}.`);
      if (existsSync(dataFolder(home, manifest.id))) write(`  ${dataFolder(home, manifest.id)} kept, untouched.`);
      write('  It takes effect at the driver\'s next look — a harness on the roster, a hook at the next tick.');
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

    default:
      throw new DaorisError(`unknown plugin verb '${verb}' — one of: list, add, remove, enable, disable`);
  }
}

