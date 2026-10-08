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
//
// And since PLUGUI2 (D140), with the driver's `PluginIcon.cs`, `PluginIconTests` holding the table:
//
//   7. A plugin's icon is the manifest's `icon`, a path inside its folder to an SVG or a PNG of at most 32 KiB.
//      Its problem is said, by the same rules in the same order and words, and never refuses the plugin.
//
// And since PLUGTOOL1a (D150 point 7), with the driver's `PluginTools.cs`, `PluginToolsTests` holding the table:
//
//   8. A plugin's tools are the manifest's `tools`: each an id, a range, why, and at most four checks. Each tool's
//      first problem is said, by the same rules in the same order and words, and never refuses the plugin. This side
//      reads and lists them; finding a tool and running its checks are the driver's, at a trial.
//
// And since XAGENT1b2 (D155 point 4), with `Plugins.cs`, `PluginCatalogTests` holding the row:
//
//   9. A declared harness may say its `product` and `maker`: text, trimmed, with blank or not text read as none and never
//      a refusal. A maker is the plugin's word, by which `opinions.ts` judges its agent's family.

import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, renameSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { basename, dirname, isAbsolute, join, resolve, sep } from 'node:path';
import { foldName, sameName } from './casefold.ts';
import { DaorisError } from './errors.ts';
import { onPath, readJsonObject, readText, renameHeld, writeJsonAtomic } from './fsx.ts';
import type { ExitCode } from './errors.ts';
import { daorisHome, HOME_SENTENCE } from './home.ts';
import { TOOLCHAINS } from './toolchain.ts';
import { TOOLS, isAddress } from './tools.ts';
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

// A plugin id's shape; the driver's `PluginCatalog.IsId` is its twin. A door that lowers an id first lowers it by
// `toLowerCase` where the driver's uses `ToLowerInvariant`, and the two agree on every code point for this check (CASEFOLD1b,
// measured on .NET 10 under ICU and in invariant mode): `İ` lowers to two letters here and keeps itself there, and is no
// letter of an id either way. And both end at the id's very end: this `$`, without the `m` flag, refuses a final line
// break, as the driver's `\z` does since CASEFOLD1e.
const ID_SHAPE = /^[a-z0-9][a-z0-9.-]*$/;

/**
 * Whether a name has a plugin id's shape: lowercase letters, digits, dots and dashes, never leading with a dot or a dash. The
 * one owner of the shape in this package (REFAC1): the catalogue's ids, a server's and a tool's names, `plugin`'s words and a
 * landing rule's plugin (`driverconfig.ts`) are all asked here, each caller trimming or lowering first as it always did.
 */
export function isPluginId(id: string): boolean {
  return ID_SHAPE.test(id);
}

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
  /**
   * What a person calls the tool it runs, as the plugin says (XAGENT1b2, D155 point 4): trimmed, and null where it says none,
   * says a blank or says something that is not text. Never a reason to refuse the plugin.
   */
  product: string | null;
  /**
   * Who makes that tool, as the plugin says, read as `product` is: the plugin's word, by which its agent may count as another
   * maker's for a second opinion (the second-agent design §3.1). Null is a maker not declared, never taken as independent.
   */
  maker: string | null;
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
  /**
   * Its icon (PLUGUI2, D140 §3.1): a path inside its folder to an SVG or a PNG, as the manifest writes it, once the
   * manifest's rules hold; null with none, and with one they refuse. `readIcon` judges the file.
   */
  icon: string | null;
  /** Why its declared icon is not drawn, by the manifest's rules; never a reason to refuse the plugin. */
  iconProblem: string | null;
  /** The tools its process runs (PLUGTOOL1a, D150 point 7), each read or saying its first problem; none for a refused plugin. */
  tools: PluginTool[];
  /** Why `tools` as a whole is not read, when it is not an array; never a reason to refuse the plugin. */
  toolsProblem: string | null;
}

/**
 * Whose a declared tool is (the UX6 design §7.2): `own`, one Daoris runs itself, whose way is set in Settings → Tools;
 * `known`, one Daoris's code names and runs for no one but a plugin; `other`, one Daoris does not know, found and never
 * downloaded. Twin: `PluginToolKind`.
 */
export type PluginToolKind = 'own' | 'known' | 'other';

/** One readiness check (§7.2): a command whose exit 0 means ready, what that means, and what a person runs when it is not. Daoris never runs `fix`. */
export interface ReadyCheck { run: string[]; says: string; fix: string | null }

/**
 * A tool a plugin declares (PLUGTOOL1a, D150 point 7), as its manifest writes it: a known id takes its name from Daoris and
 * its file from its way, so `command` is null for one; any other is found by `command`, the id when absent. A tool whose
 * entry breaks a rule keeps only its id, where it has one, and says its first problem. Twin: `PluginTool`.
 */
export interface PluginTool {
  id: string | null;
  kind: PluginToolKind | null;
  name: string | null;
  command: string | null;
  versionArguments: string[] | null;
  /** The range as written: `>=2.60`, `>=2.60 <3`, or one exact version; null for any. */
  versions: string | null;
  for: string | null;
  ready: ReadyCheck[];
  problem: string | null;
}

/** The tools Daoris runs itself (D150 point 7): their way stays Settings → Tools'. Twin: `PluginTools.DaorisOwn`. */
export const DAORIS_OWN_TOOLS: readonly string[] = Object.freeze(['git', 'node', 'pwsh']);

/** The most checks a tool may carry (§7.2). Twin: `PluginTools.MaxChecks`. */
export const MAX_TOOL_CHECKS = 4;

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

const empty = (id: string): PluginManifest => ({
  id, apiVersion: API_VERSION, name: id, version: '', description: '', harnesses: [], hooks: null, servers: [],
  icon: null, iconProblem: null, tools: [], toolsProblem: null,
});

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

// An id or a name compares as the driver's `PluginCatalog.Same` compares it, by `OrdinalIgnoreCase` (CASEFOLD1).
const same = sameName;

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

/** A harness's word about itself (XAGENT1b2): its text trimmed, and none where it is blank or not text, never a refusal. */
function ownWord(row: unknown, name: string): string | null {
  const value = text(row, name)?.trim();
  return value ? value : null;
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

  if (!isPluginId(id)) {
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
        // The plugin's word for its tool and its maker; a manifest written before says neither.
        product: ownWord(row, 'product'),
        maker: ownWord(row, 'maker'),
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
      if (!name || !isPluginId(name)) {
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

  // Its icon, last: an icon's problem is said and never refuses the plugin (D140 §3.1).
  const declaredIcon = (root as Record<string, unknown>).icon;
  const { icon, problem: iconProblem } = declaredIcon === undefined ? { icon: null, problem: null } : iconDeclared(declaredIcon);

  // Its tools, after: a tool's problem is that tool's sentence and never refuses the plugin (D150 point 7).
  const declaredTools = (root as Record<string, unknown>).tools;
  const { tools, problem: toolsProblem } = declaredTools === undefined ? { tools: [], problem: null } : toolsDeclared(declaredTools);

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
      icon,
      iconProblem,
      tools,
      toolsProblem,
    },
    problem: null,
  };
}

// ——— A plugin's tools (PLUGTOOL1a, D150 point 7; the UX6 design §7.2). Twin: the driver's `PluginTools.cs`, whose
// `PluginToolsTests` holds the table `plugin-tools.test.ts` parses and holds this reader to. A tool is something the
// plugin needs, never something it is, so a problem in `tools` is said beside the tool and never refuses the plugin.

/** One to four numbers, as `tools.json` and a resource list spell an exact version. Twin: `Tools.IsExactVersion`. */
const EXACT_VERSION = /^[0-9]+(?:\.[0-9]+){0,3}$/;

/** A range, read: a floor (`>=`), a floor and a top (`<`), or one exact version. Twin: `VersionRange`. */
interface VersionRange { lower: string | null; upper: string | null; exact: string | null }

/**
 * The range a manifest writes (§7.2), or null where it is not one: words parted by spaces, `>=V`, `>=V <V`, or `V` alone,
 * each `V` an exact version. Nothing else is a range, so a later reader never guesses at what one meant. Twin:
 * `VersionRange.Parse`.
 */
function parseRange(written: string): VersionRange | null {
  const words = written.split(' ').filter((word) => word !== '');
  const floor = (word: string) => word.startsWith('>=') && EXACT_VERSION.test(word.slice(2)) ? word.slice(2) : null;
  const top = (word: string) => word.startsWith('<') && EXACT_VERSION.test(word.slice(1)) ? word.slice(1) : null;
  if (words.length === 1) {
    if (EXACT_VERSION.test(words[0]!)) return { lower: null, upper: null, exact: words[0]! };
    const lower = floor(words[0]!);
    return lower === null ? null : { lower, upper: null, exact: null };
  }
  if (words.length !== 2) return null;
  const lower = floor(words[0]!);
  const upper = top(words[1]!);
  return lower !== null && upper !== null ? { lower, upper, exact: null } : null;
}

/** Two exact versions, number by number, a missing number 0: `2.60` and `2.60.0` are the same. Twin: `VersionRange.Compare`. */
function compareNumbers(a: string, b: string): number {
  const left = a.split('.');
  const right = b.split('.');
  for (let at = 0; at < Math.max(left.length, right.length); at += 1) {
    const x = (left[at] ?? '0').replace(/^0+/, '') || '0';
    const y = (right[at] ?? '0').replace(/^0+/, '') || '0';
    if (x.length !== y.length) return x.length - y.length;
    if (x !== y) return x < y ? -1 : 1;
  }
  return 0;
}

const filled = (value: unknown): value is string => typeof value === 'string' && value.trim() !== '';

const notRead = (id: string | null, problem: string): PluginTool => ({
  id, kind: null, name: null, command: null, versionArguments: null, versions: null, for: null, ready: [], problem,
});

/** The manifest's `tools`, by the table: none for `null`; an array, each entry read on its own. Twin: `PluginTools.Read`. */
function toolsDeclared(value: unknown): { tools: PluginTool[]; problem: string | null } {
  if (value === null) return { tools: [], problem: null };
  if (!Array.isArray(value)) {
    return { tools: [], problem: '`tools` must be an array of the tools the plugin runs, each an object with an `id`.' };
  }
  const seen = new Set<string>();
  return { tools: value.map((row, at) => toolDeclared(row, at + 1, seen)), problem: null };
}

/**
 * One entry, by the rules in order, the first broken said: an object; an `id` of a tool id's shape, once; a known id
 * declaring none of the three fields that are Daoris's; another id's `name`, `command` (a name on the PATH, no folder)
 * and `versionArguments`; `versions` a range that holds a version; `for` a sentence; at most four checks, each a `run`
 * whose first word is not blank, its `says`, and a `fix` that is text where it is given. `null` is no field.
 */
function toolDeclared(row: unknown, at: number, seen: Set<string>): PluginTool {
  if (typeof row !== 'object' || row === null || Array.isArray(row)) return notRead(null, `tool ${at} in \`tools\` is not an object with an \`id\`.`);
  const fields = row as Record<string, unknown>;
  const given = (name: string) => fields[name] !== undefined && fields[name] !== null;

  const id = fields.id;
  if (!filled(id)) return notRead(null, `tool ${at} in \`tools\` needs an \`id\`: a tool's name in lowercase, like \`az\`.`);
  if (!isPluginId(id)) {
    return notRead(null, `tool ${at} in \`tools\` has the \`id\` \`${id}\`, which is not one: lowercase letters, digits, dots and dashes, like \`az\`.`);
  }
  if (seen.has(id)) return notRead(id, `tool \`${id}\` is declared twice in \`tools\`; the first is read.`);
  seen.add(id);
  const tool = `tool \`${id}\``;

  const known = TOOLS.find((declared) => declared.id === id);
  if (known) {
    const field = ['name', 'command', 'versionArguments'].find(given);
    if (field !== undefined) {
      return notRead(id, `${tool} is ${known.name}, which Daoris knows: its name, its file and how its version is asked are `
        + `Daoris's, so \`${field}\` is not a plugin's to declare.`);
    }
  } else {
    if (given('name') && !filled(fields.name)) return notRead(id, `${tool}'s \`name\` must be text: what a person calls it.`);
    if (given('command') && (!filled(fields.command) || ['/', '\\', ':'].some((part) => (fields.command as string).includes(part)))) {
      return notRead(id, `${tool}'s \`command\` must be the name of a program found on the PATH, with no folder in it.`);
    }
    if (given('versionArguments')
      && !(Array.isArray(fields.versionArguments) && fields.versionArguments.every((word) => typeof word === 'string'))) {
      return notRead(id, `${tool}'s \`versionArguments\` must be an array of text: what prints its version.`);
    }
  }

  let versions: string | null = null;
  if (given('versions')) {
    const range = typeof fields.versions === 'string' ? parseRange(fields.versions) : null;
    if (range === null) return notRead(id, `${tool}'s \`versions\` must be a range: \`>=2.60\`, \`>=2.60 <3\`, or one exact version.`);
    if (range.lower !== null && range.upper !== null && compareNumbers(range.upper, range.lower) <= 0) {
      return notRead(id, `${tool}'s \`versions\` \`${fields.versions as string}\` holds no version: \`<${range.upper}\` is not above \`>=${range.lower}\`.`);
    }
    versions = fields.versions as string;
  }

  if (given('for') && !filled(fields.for)) return notRead(id, `${tool}'s \`for\` must be one sentence: why the plugin runs it.`);

  const ready: ReadyCheck[] = [];
  if (given('ready')) {
    if (!Array.isArray(fields.ready)) return notRead(id, `${tool}'s \`ready\` must be an array of checks.`);
    if (fields.ready.length > MAX_TOOL_CHECKS) {
      return notRead(id, `${tool} has ${fields.ready.length} checks in \`ready\`, and a tool has at most four.`);
    }
    for (const [index, check] of (fields.ready as unknown[]).entries()) {
      const said = typeof check === 'object' && check !== null && !Array.isArray(check) ? check as Record<string, unknown> : {};
      const run = said.run;
      if (!Array.isArray(run) || run.length === 0 || !run.every((word) => typeof word === 'string') || !filled(run[0])) {
        return notRead(id, `${tool}'s check ${index + 1} needs a \`run\`: the command whose exit 0 means ready, as an array.`);
      }
      if (!filled(said.says)) return notRead(id, `${tool}'s check ${index + 1} needs \`says\`: what it means when it passes.`);
      if (said.fix !== undefined && said.fix !== null && !filled(said.fix)) {
        return notRead(id, `${tool}'s check ${index + 1} has a \`fix\` that is not text: the command a person runs when it does not pass.`);
      }
      ready.push({ run: [...(run as string[])], says: said.says, fix: (said.fix as string | null | undefined) ?? null });
    }
  }

  return {
    id,
    kind: known ? (DAORIS_OWN_TOOLS.includes(id) ? 'own' : 'known') : 'other',
    name: known ? known.name : (fields.name as string | null | undefined) ?? id,
    command: known ? null : (fields.command as string | null | undefined) ?? id,
    versionArguments: known || !given('versionArguments') ? null : [...(fields.versionArguments as string[])],
    versions,
    for: (fields.for as string | null | undefined) ?? null,
    ready,
    problem: null,
  };
}

/** What `plugin list` says of a plugin's tools: the ones it runs, then each problem, never a refusal. */
function toolLines(manifest: PluginManifest): string[] {
  const lines: string[] = [];
  const read = manifest.tools.filter((tool) => tool.problem === null);
  if (read.length > 0) {
    lines.push(`runs ${read.map((tool) => [tool.name, ...(tool.versions ?? '').split(' ').filter((word) => word !== '')].join(' ')).join(', ')}`);
  }
  if (manifest.toolsProblem !== null) lines.push(`tool not read: ${manifest.toolsProblem}`);
  for (const tool of manifest.tools) if (tool.problem !== null) lines.push(`tool not read: ${tool.problem}`);
  return lines;
}

// ——— A plugin's icon (PLUGUI2, D140; the catalogue design §3.1). Twin: the driver's `PluginIcon.cs`, whose
// `PluginIconTests` holds the table `plugin-icons.test.ts` parses and holds this reader to. An icon is how a plugin is
// recognised, never what it does, so its problem is said and never refuses the plugin.

/** The most an icon file may be: it is drawn at 48 px at most, and the screen's list carries every plugin's. Twin: `PluginIcon.MaxBytes`. */
export const ICON_MAX_BYTES = 32 * 1024;

/** The most a PNG icon may be on a side, which also bounds what a small file inflates to. Twin: `PluginIcon.MaxPixels`. */
export const ICON_MAX_PIXELS = 512;

const PNG_SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

const iconType = (icon: string): 'svg' | 'png' | null =>
  /\.svg$/i.test(icon) ? 'svg' : /\.png$/i.test(icon) ? 'png' : null;

/**
 * The manifest's `icon` by the manifest's rules, in order: none for `null`; text that is not blank; a path inside the
 * folder, names joined by `/` with none empty, `.` or `..`, and no `\` or `:`; an `.svg` or a `.png`, case aside.
 */
function iconDeclared(value: unknown): { icon: string | null; problem: string | null } {
  if (value === null) return { icon: null, problem: null };
  if (typeof value !== 'string' || value.trim() === '') {
    return { icon: null, problem: "`icon` must be the path of an .svg or .png file in the plugin's folder." };
  }
  if (value.includes('\\') || value.includes(':') || value.split('/').some((name) => name === '' || name === '.' || name === '..')) {
    return {
      icon: null,
      problem: `\`icon\` \`${value}\` is not a path inside the plugin's folder: it is written from the folder, with \`/\` `
        + 'between names and no `.` or `..`.',
    };
  }
  return iconType(value) === null
    ? { icon: null, problem: `\`icon\` \`${value}\` is neither an .svg nor a .png file.` }
    : { icon: value, problem: null };
}

/**
 * The icon a manifest declares, judged as a file in its folder, by the file's rules in order: there and a file; at most
 * 32 KiB; a PNG that starts with its signature and header chunk, at most 512 pixels on a side; an SVG that is UTF-8 text
 * holding an `<svg` element and declaring no entity. A manifest's own problem is said first. Twin: `PluginIcon.Read`,
 * which also hands the page the bytes; this side lists only whether it draws.
 */
export function readIcon(folder: string, manifest: PluginManifest): { type: 'svg' | 'png' | null; problem: string | null } {
  if (manifest.iconProblem !== null) return { type: null, problem: manifest.iconProblem };
  if (manifest.icon === null) return { type: null, problem: null };
  const icon = manifest.icon;
  const refused = (why: string) => ({ type: null, problem: `\`icon\` \`${icon}\` ${why}` });

  const path = join(folder, ...icon.split('/'));
  let bytes: Buffer;
  try {
    if (!statSync(path).isFile()) return refused("is not a file in the plugin's folder.");
    if (statSync(path).size > ICON_MAX_BYTES) return refused('is larger than 32 KiB, the most an icon may be.');
    bytes = readFileSync(path);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') return refused("is not a file in the plugin's folder.");
    // Daoris's words, never the system's: its message names the file's whole path, which the page must not learn.
    return refused('could not be read.');
  }
  // Read once, so the bytes judged are the bytes drawn.
  if (bytes.length > ICON_MAX_BYTES) return refused('is larger than 32 KiB, the most an icon may be.');

  if (iconType(icon) === 'png') {
    if (bytes.length < 24 || !bytes.subarray(0, 8).equals(PNG_SIGNATURE) || bytes.toString('ascii', 12, 16) !== 'IHDR') {
      return refused('is not a PNG image.');
    }
    const width = bytes.readUInt32BE(16);
    const height = bytes.readUInt32BE(20);
    if (width === 0 || height === 0) return refused('is not a PNG image.');
    if (width > ICON_MAX_PIXELS || height > ICON_MAX_PIXELS) {
      return refused(`is ${width}×${height} pixels, and an icon is at most ${ICON_MAX_PIXELS} on a side.`);
    }
    return { type: 'png', problem: null };
  }

  let text: string;
  try {
    text = new TextDecoder('utf-8', { fatal: true }).decode(bytes);
  } catch {
    return refused('is not an SVG image.');
  }
  if (text.includes('\0') || !/<svg[\s>/]/.test(text)) return refused('is not an SVG image.');
  if (text.includes('<!ENTITY')) return refused('declares an XML entity, which an icon may not.');
  return { type: 'svg', problem: null };
}

/**
 * What in a manifest this build refuses, whatever else is installed: a harness it already carries, or
 * a server under the knowledge host's name. Case-blind, as every name here is. The catalogue and
 * `plugin add` ask this one question, so they refuse the same plugin in the same words (REV3 CLEAN1:
 * `add` compared harness names case-sensitively, and copied in a plugin the catalogue then refused).
 */
function refusedByThisBuild(manifest: PluginManifest, reserved: Iterable<string>): string | null {
  const taken = new Set([...reserved].map(foldName));
  const harness = manifest.harnesses.find((declared) => taken.has(foldName(declared.name)));
  if (harness) {
    return `declares harness \`${harness.name}\`, which this build already carries — `
      + 'a plugin adds a harness and never replaces one.';
  }
  const server = manifest.servers.find((declared) => same(declared.name, KNOWLEDGE_SERVER));
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

  const disabled = new Set(readPluginState(home).disabled.map(foldName));
  const declaredBy = new Map<string, string>();
  const servedBy = new Map<string, string>();

  for (const folderName of readdirSync(root).sort()) {
    // `.data/` and any other dot-folder is the catalogue's own, never a plugin.
    if (folderName.startsWith('.')) continue;
    const folder = join(root, folderName);
    if (!statSync(folder).isDirectory() || !existsSync(join(folder, MANIFEST))) continue;

    let { manifest, problem } = readManifest(folderName, folder);
    const enabled = !disabled.has(foldName(manifest.id));

    if (problem === null && enabled) {
      problem = refusedByThisBuild(manifest, reserved);
      for (const harness of problem === null ? manifest.harnesses : []) {
        const key = foldName(harness.name);
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
        const key = foldName(server.name);
        const other = servedBy.get(key);
        if (other) {
          problem = `declares server \`${server.name}\`, which plugin \`${other}\` already declares — `
            + 'the first by id keeps it, and this plugin contributes nothing.';
          break;
        }
      }
      if (problem === null) {
        for (const harness of manifest.harnesses) declaredBy.set(foldName(harness.name), manifest.id);
        for (const server of manifest.servers) servedBy.set(foldName(server.name), manifest.id);
      }
    }

    // Nothing of a refused plugin is taken — not a harness, not a hook, not a server, not a tool it would run (PLUGTOOL1a).
    if (problem !== null) manifest = { ...manifest, harnesses: [], hooks: null, servers: [], tools: [], toolsProblem: null };

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
  if (offer !== null) return isPluginId(offer) ? { source: { offer }, problem: null } : unread('its offer is not a plugin id');
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
  if (!isPluginId(id.toLowerCase())) return refuse(NOT_AN_ID(id));
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
    renameHeld(staging, target);
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
  if (!isPluginId(id.toLowerCase())) throw new DaorisError(NOT_AN_ID(id));
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
          // The tools its process runs, and each problem in them, which never refuses it (PLUGTOOL1a).
          for (const line of toolLines(entry.manifest)) write(`  ${''.padEnd(24)} ${line}`);
          write(`  ${''.padEnd(24)} ${sourceLine(entry.folder)}`);
          // Its author's to fix, and never a reason it is refused (D140 §3.1): the screen draws its monogram.
          const icon = readIcon(entry.folder, entry.manifest);
          if (icon.problem !== null) write(`  ${''.padEnd(24)} icon not drawn: ${icon.problem}`);
        }
      }

      if (offered.length > 0) {
        write(`daoris: Daoris's own plugins, offered by this install, not installed (${offersFolder(home)}):`);
        for (const offer of offered) {
          const version = offer.manifest.version ? ` ${offer.manifest.version}` : '';
          write(`  ${offer.id.padEnd(24)} ${offer.manifest.name}${version}`);
          write(`  ${''.padEnd(24)} ${offer.problem ? `⚠ ${offer.problem}` : describe(offer.manifest)}`);
          for (const line of toolLines(offer.manifest)) write(`  ${''.padEnd(24)} ${line}`);
          if (offer.needs.length > 0) write(`  ${''.padEnd(24)} needs: ${offer.needs.join('; ')}`);
          const icon = readIcon(offer.folder, offer.manifest);
          if (icon.problem !== null) write(`  ${''.padEnd(24)} icon not drawn: ${icon.problem}`);
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
        if (!isPluginId(wanted.toLowerCase())) throw new DaorisError(NOT_AN_ID(wanted));
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
        renameHeld(staging, target);
        try {
          rmSync(aside, { recursive: true, force: true });
        } catch {
          // A dot-folder is never read as a plugin; one that cannot be deleted now is nobody's.
        }
      } else {
        renameHeld(staging, target);
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

