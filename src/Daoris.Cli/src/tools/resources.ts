// `resources.json`: where each version of a tool downloads from, and the merge of every list read (TOOLS3,
// D121; `docs/2026-10-01-tools-design.md` §3.1–§3.5). One list is built into the install, at
// `app/resources.json` beside the application; more are resource locations the person lists in `tools.json`,
// each fetched on their press and kept under `<home>/tools/locations/`. This module reads the copies and the
// list built in, and merges them. Fetching is TOOLS4's, through the fetcher the dispatcher hands in.
//
// 🔴 A TWIN of the driver's `ToolResources.cs`. The two share no code — the FILE is the contract — and each
// carries the same tables (`resources.test.ts` here, `ToolResourcesTests.cs` there), row for row. A rule
// changed here is changed there, in the same commit:
//
//   1. `schema` is read first: anything but 1 refuses the whole list, and so does a `tools` that is no object.
//   2. A tool this build does not declare is named and never offered.
//   3. A version is one to four numbers; a platform is one of the table's; a version names its `files`.
//   4. A file names its `url` (https://, or http:// to this machine), `sha256` (64 hex, kept lower case),
//      `size` (whole bytes above 0), `archive` (zip or tar.gz) and `exe` (a relative `/` path inside the
//      archive); `paths` defaults to the folder `exe` is in, `.` for the root.
//   5. What does not read is skipped and said, never guessed at.
//
// Then the merge (§3.4): the person's locations in order, then the list built in. One tool, version and
// platform is one download, whoever names it: lists that disagree refuse that version, naming both; lists that
// agree under other addresses are mirrors, tried in read order; the versions are the union, and the newest is
// the highest by number, never by any list's word.
//
// It stays PURE: it reads files and answers "what is offered". It spawns nothing and opens no connection, so
// no doctrine command could reach either through it (the dogfood tests hold both).

import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import {
  TOOLS, TOOLS_FOLDER, declaredTool, isAddress, isExactVersion, isInsidePath, isLoopbackHost, readTools, type ToolsRead,
} from './tools.ts';

/** The list's name, built in and fetched alike. The driver's `ToolResources.FileName`. */
export const RESOURCES_FILE = 'resources.json';

/**
 * Where an install carries the list built in, from its root: beside the application, in `app/`. A twin of the
 * publish's `RESOURCES` and the driver's `ToolResources.Layout`; `desktop-publish.test.ts` reads all three.
 */
export const BUILT_IN_LAYOUT: readonly string[] = Object.freeze(['app', RESOURCES_FILE]);

/** Where each location's fetched copy is kept, under the home. The driver's `ToolResources.LocationsFolder`. */
export const LOCATIONS_FOLDER: readonly string[] = Object.freeze([TOOLS_FOLDER, 'locations']);

/** The one schema this build reads. The driver's `ToolResources.Schema`. */
export const SCHEMA = 1;

/** The archives either reader unpacks; a third is a reviewed change to both (§3.2). */
export const ARCHIVES: readonly string[] = Object.freeze(['zip', 'tar.gz']);

/** How the list built in names itself in a sentence. The driver's `ToolResources.BuiltIn`. */
export const BUILT_IN = 'the built-in list';

/**
 * The platforms a list may name — .NET runtime identifiers — and Node's names for each (§3.2). 🔴 The driver's
 * `ToolResources.Platforms` holds the same ids in the same order, with .NET's names; the tests hold both
 * spellings in one table.
 */
const PLATFORM_TABLE: readonly { id: string; platform: string; arch: string }[] = [
  { id: 'win-x64', platform: 'win32', arch: 'x64' },
  { id: 'win-arm64', platform: 'win32', arch: 'arm64' },
  { id: 'linux-x64', platform: 'linux', arch: 'x64' },
  { id: 'linux-arm64', platform: 'linux', arch: 'arm64' },
  { id: 'osx-x64', platform: 'darwin', arch: 'x64' },
  { id: 'osx-arm64', platform: 'darwin', arch: 'arm64' },
];

export const PLATFORMS: readonly string[] = Object.freeze(PLATFORM_TABLE.map((row) => row.id));

/** The platform a Node process names, or null for one no list may name. */
export function platformFor(platform: string, arch: string): string | null {
  return PLATFORM_TABLE.find((row) => row.platform === platform && row.arch === arch)?.id ?? null;
}

/** This process's platform: the architecture it runs as, as the driver asks its own process. */
export function currentPlatform(): string | null {
  return platformFor(process.platform, process.arch);
}

export type ArchiveKind = 'zip' | 'tar.gz';

/** One download, as a list names it (§3.2). */
export interface ToolFile {
  url: string;
  /** Lower case. */
  sha256: string;
  size: number;
  archive: ArchiveKind;
  /** The executable inside the unpacked archive, a relative `/` path. */
  exe: string;
  /** The folders a child's PATH takes first; `.` is the archive's root. */
  paths: string[];
}

export interface Licence {
  id: string;
  url: string | null;
}

export interface ResourceTool {
  source: string | null;
  licence: Licence | null;
  /** Version → platform → file: only what reads, each in ordinal order. */
  versions: Map<string, Map<string, ToolFile>>;
}

/** One list as read. */
export interface ResourceList {
  /** How sentences name it: an address, or {@link BUILT_IN}. */
  origin: string;
  /** What vouches for it (§3.5): `built in`, `this machine`, or the host; null for text read on its own. */
  integrity: string | null;
  /** The file read; null for text. */
  path: string | null;
  exists: boolean;
  /** The sha256 of the bytes read, as the screen shows it; null when nothing was read. */
  sha256: string | null;
  /** The declared tools it names, in ordinal order. */
  tools: Map<string, ResourceTool>;
  /** The ids it names that this build does not run: never offered. */
  unknown: string[];
  /** What it skips, and why. */
  notes: string[];
  /** Why nothing in it is read. */
  problem: string | null;
}

/** One version of one tool for one platform, offered: one download, from every address that names it. */
export interface OfferedVersion {
  version: string;
  sha256: string;
  size: number;
  archive: ArchiveKind;
  exe: string;
  paths: string[];
  /** Every address, in read order, once: the person's first, then the maker's. */
  urls: string[];
  /** Every list that names it, in read order. */
  lists: string[];
}

/** A version two lists disagree on, and so refused. */
export interface RefusedVersion {
  version: string;
  /** The first field they disagree on. */
  field: string;
  /** The two lists, in read order. */
  lists: string[];
  problem: string;
}

export interface MergedTool {
  tool: string;
  source: string | null;
  sourceFrom: string | null;
  licence: Licence | null;
  licenceFrom: string | null;
  /** Every list naming the tool, in read order. */
  lists: string[];
  /** Newest first. */
  versions: OfferedVersion[];
  /** Newest first. */
  refused: RefusedVersion[];
  /** The highest offered version by number; never a refused one. */
  newest: string | null;
}

export interface MergedResources {
  platform: string | null;
  /** Every declared tool, in the declared order. */
  tools: MergedTool[];
  /** The ids lists name that this build does not run, with the lists that name them. */
  unknown: { tool: string; lists: string[] }[];
  /** Every list's problem and notes in read order, then each refusal. */
  notes: string[];
}

const HASH = /^[0-9A-Fa-f]{64}$/;
const ADDRESS_RULE = 'https://, or http:// to this machine';

const isObject = (value: unknown): value is Record<string, unknown> =>
  value !== null && typeof value === 'object' && !Array.isArray(value);

/** Present: JSON null is no field, as the driver's reader sees it. */
const present = (value: unknown): boolean => value !== undefined && value !== null;

/** The keys of an object, in ordinal order: JSON's own order is not one both runtimes keep. */
const ordinal = (value: Record<string, unknown>): string[] => Object.keys(value).sort();

/**
 * Compare two versions by number: each part as a whole number, a missing part as 0, then the spelling, so two
 * spellings of one number still sort one way. The driver's `ToolResources.CompareVersions`.
 */
export function compareVersions(a: string, b: string): number {
  const left = a.split('.');
  const right = b.split('.');
  const number = (part: string | undefined): string => (part ?? '0').replace(/^0+/, '') || '0';
  for (let at = 0; at < Math.max(left.length, right.length); at += 1) {
    const x = number(left[at]);
    const y = number(right[at]);
    if (x.length !== y.length) return x.length - y.length;
    if (x !== y) return x < y ? -1 : 1;
  }
  return a === b ? 0 : a < b ? -1 : 1;
}

/** A file of the list, judged (rule 4): what it downloads, or why it is skipped. */
function judgeFile(value: unknown): ToolFile | string {
  if (!isObject(value)) return 'it is not an object';
  if (typeof value.url !== 'string') return 'it names no `url`';
  if (!isAddress(value.url)) return `\`url\` is not ${ADDRESS_RULE}`;
  if (typeof value.sha256 !== 'string' || !HASH.test(value.sha256)) return '`sha256` is not 64 hex digits';
  if (typeof value.size !== 'number' || !Number.isSafeInteger(value.size) || value.size <= 0) {
    return '`size` is not a whole number of bytes above 0';
  }
  if (typeof value.archive !== 'string' || !ARCHIVES.includes(value.archive)) return '`archive` is not zip or tar.gz';
  if (typeof value.exe !== 'string') return 'it names no `exe`';
  if (!isInsidePath(value.exe)) return '`exe` is not a relative path inside the archive';

  let paths: string[];
  if (!present(value.paths)) {
    const at = value.exe.lastIndexOf('/');
    paths = [at < 0 ? '.' : value.exe.slice(0, at)];
  } else if (Array.isArray(value.paths) && value.paths.length > 0
    && value.paths.every((path) => typeof path === 'string' && (path === '.' || isInsidePath(path)))) {
    paths = [...value.paths as string[]];
  } else {
    return '`paths` is not a list of folders inside the archive';
  }

  return {
    url: value.url, sha256: value.sha256.toLowerCase(), size: value.size, archive: value.archive as ArchiveKind, exe: value.exe, paths,
  };
}

/** A tool's entry (rules 3–5): what it offers, with each thing skipped said. */
function readTool(origin: string, id: string, entry: Record<string, unknown>, notes: string[]): ResourceTool {
  const tool: ResourceTool = { source: null, licence: null, versions: new Map() };

  if (present(entry.source)) {
    if (typeof entry.source === 'string' && isAddress(entry.source)) tool.source = entry.source;
    else notes.push(`${origin}: \`${id}\`'s \`source\` is not ${ADDRESS_RULE}, and is not shown`);
  }

  if (present(entry.licence)) {
    const licence = entry.licence;
    if (!isObject(licence) || typeof licence.id !== 'string' || licence.id === '') {
      notes.push(`${origin}: \`${id}\`'s \`licence\` names no \`id\`, and is not shown`);
    } else if (present(licence.url) && (typeof licence.url !== 'string' || !isAddress(licence.url))) {
      notes.push(`${origin}: \`${id}\`'s licence \`url\` is not ${ADDRESS_RULE}, and is not shown`);
      tool.licence = { id: licence.id, url: null };
    } else {
      tool.licence = { id: licence.id, url: typeof licence.url === 'string' ? licence.url : null };
    }
  }

  if (!present(entry.versions)) return tool;
  if (!isObject(entry.versions)) {
    notes.push(`${origin}: \`${id}\`'s \`versions\` is not an object, and nothing of it is offered`);
    return tool;
  }

  for (const version of ordinal(entry.versions)) {
    if (!isExactVersion(version)) {
      notes.push(`${origin}: \`${id}\` \`${version}\` is not an exact version — one to four numbers — and is skipped`);
      continue;
    }
    const held = entry.versions[version];
    if (!isObject(held) || !isObject(held.files)) {
      notes.push(`${origin}: \`${id}\` ${version} has no \`files\` object, and is skipped`);
      continue;
    }

    const files = new Map<string, ToolFile>();
    for (const platform of ordinal(held.files)) {
      if (!PLATFORMS.includes(platform)) {
        notes.push(`${origin}: \`${id}\` ${version} names \`${platform}\`, which is not a platform this build knows, and is skipped`);
        continue;
      }
      const judged = judgeFile(held.files[platform]);
      if (typeof judged === 'string') notes.push(`${origin}: \`${id}\` ${version} for ${platform} is skipped — ${judged}`);
      else files.set(platform, judged);
    }
    if (files.size > 0) tool.versions.set(version, files);
  }
  return tool;
}

/** A list that names nothing. */
const emptyList = (origin: string): ResourceList => ({
  origin, integrity: null, path: null, exists: true, sha256: null, tools: new Map(), unknown: [], notes: [], problem: null,
});

/** A list's text (rules 1–5), as the list named `origin`. A byte-order mark is not part of it. */
export function parseResources(text: string, origin: string): ResourceList {
  const list = emptyList(origin);

  let parsed: unknown;
  try {
    parsed = JSON.parse(text.replace(/^﻿/, ''));
  } catch (error) {
    list.problem = `${origin} is not readable JSON (${(error as Error).message})`;
    return list;
  }
  if (!isObject(parsed)) {
    list.problem = `${origin} is not a JSON object`;
    return list;
  }

  // Rule 1: the schema, before anything else.
  if (!present(parsed.schema)) {
    list.problem = `${origin} names no \`schema\`: nothing in it is read`;
    return list;
  }
  if (parsed.schema !== SCHEMA) {
    list.problem = `${origin} is schema ${JSON.stringify(parsed.schema)}, and this build reads schema ${SCHEMA}: nothing in it is `
      + 'read, and a newer Daoris may read it';
    return list;
  }
  if (!present(parsed.tools)) return list;
  if (!isObject(parsed.tools)) {
    list.problem = `${origin}'s \`tools\` is not an object: nothing in it is read`;
    return list;
  }

  for (const id of ordinal(parsed.tools)) {
    // Rule 2: a list offers versions of the tools this build declares, and can never add one.
    if (!declaredTool(id)) {
      list.unknown.push(id);
      list.notes.push(`${origin} names \`${id}\`, which is not a tool this build runs: nothing of it is offered`);
      continue;
    }
    const entry = parsed.tools[id];
    if (!isObject(entry)) {
      list.notes.push(`${origin}: \`${id}\` is not an object, and nothing of it is offered`);
      continue;
    }
    list.tools.set(id, readTool(origin, id, entry, list.notes));
  }
  return list;
}

/** What vouches for a list from an address (§3.5): this machine, or the host the person chose to trust. */
export function integrityOf(address: string): string {
  const url = new URL(address);
  return isLoopbackHost(url.hostname) ? 'this machine' : url.host;
}

/**
 * A list on disk, hashed as its bytes and read as its text. Absent is `exists: false` and no problem: the
 * caller says what absence means.
 */
export function readResourceFile(path: string, origin: string): ResourceList {
  const integrity = origin === BUILT_IN ? 'built in' : integrityOf(origin);
  if (!existsSync(path)) return { ...emptyList(origin), integrity, path, exists: false };
  const bytes = readFileSync(path);
  return {
    ...parseResources(bytes.toString('utf8'), origin),
    integrity, path, sha256: createHash('sha256').update(bytes).digest('hex'),
  };
}

/** The list built in, beside the home as the offers are (`OFFERS_DIR`): in an install, the two are one folder. */
export function builtInList(home: string): string {
  return join(dirname(resolve(home)), ...BUILT_IN_LAYOUT);
}

/** Where a location's fetched copy is kept: named by the sha256 of its address as written. */
export function locationCopy(home: string, address: string): string {
  return join(home, ...LOCATIONS_FOLDER, `${createHash('sha256').update(address, 'utf8').digest('hex')}.json`);
}

/**
 * Every list, in read order (§3.3): the person's locations top to bottom, each from its fetched copy, then the
 * list built in, always last. A location never fetched, and a home with no install beside it, are lists that
 * name nothing and say why.
 */
export function readLists(home: string, read: ToolsRead = readTools(home)): ResourceList[] {
  const lists = read.locations.map((address) => {
    const list = readResourceFile(locationCopy(home, address), address);
    if (!list.exists) list.notes.push(`${address} has not been fetched yet, so it names nothing`);
    return list;
  });

  const builtIn = readResourceFile(builtInList(home), BUILT_IN);
  if (!builtIn.exists) builtIn.notes.push(`no list is built in beside this home (${builtIn.path}), so only the locations are read`);
  return [...lists, builtIn];
}

/** The fields one download is, whoever names it (§3.4 rule 2), in the order a refusal names the first that differs. */
const FILE_FIELDS = ['sha256', 'size', 'archive', 'exe', 'paths'] as const;

const fieldText = (file: ToolFile, field: (typeof FILE_FIELDS)[number]): string =>
  field === 'paths' ? `[${file.paths.join(', ')}]` : String(file[field]);

/**
 * Merge the lists, read in order, for one platform (§3.4). A list with a problem offers nothing; its problem
 * is carried into the notes, as each list's notes are.
 */
export function mergeResources(lists: readonly ResourceList[], platform: string | null): MergedResources {
  const notes: string[] = [];
  for (const list of lists) {
    if (list.problem !== null) notes.push(list.problem);
    notes.push(...list.notes);
  }

  const unknown: { tool: string; lists: string[] }[] = [];
  for (const list of lists) {
    for (const id of list.unknown) {
      const held = unknown.find((entry) => entry.tool === id);
      if (held) held.lists.push(list.origin);
      else unknown.push({ tool: id, lists: [list.origin] });
    }
  }

  const refusals: string[] = [];
  const tools = TOOLS.map((declaration): MergedTool => {
    const merged: MergedTool = {
      tool: declaration.id, source: null, sourceFrom: null, licence: null, licenceFrom: null, lists: [], versions: [], refused: [], newest: null,
    };
    const offered = new Map<string, { file: ToolFile; version: OfferedVersion }>();
    const refused = new Map<string, RefusedVersion>();

    for (const list of lists) {
      const tool = list.tools.get(declaration.id);
      if (!tool) continue;
      merged.lists.push(list.origin);
      // Rule 7: the first list that names them, in read order.
      if (merged.source === null && tool.source !== null) [merged.source, merged.sourceFrom] = [tool.source, list.origin];
      if (merged.licence === null && tool.licence !== null) [merged.licence, merged.licenceFrom] = [tool.licence, list.origin];
      if (platform === null) continue;

      for (const [version, files] of tool.versions) {
        const file = files.get(platform);
        if (!file || refused.has(version)) continue;
        const held = offered.get(version);
        if (!held) {
          const { url, sha256, size, archive, exe, paths } = file;
          offered.set(version, {
            file,
            version: { version, sha256, size, archive, exe, paths: [...paths], urls: [url], lists: [list.origin] },
          });
          continue;
        }

        // Rules 2 and 3: one download, whoever names it; a disagreement refuses this version, naming both.
        const field = FILE_FIELDS.find((name) => fieldText(held.file, name) !== fieldText(file, name));
        if (field !== undefined) {
          const first = held.version.lists[0]!;
          refused.set(version, {
            version, field, lists: [first, list.origin],
            problem: `${declaration.name} ${version} for ${platform} is refused: ${first} names its ${field} as `
              + `${fieldText(held.file, field)}, and ${list.origin} as ${fieldText(file, field)}. Two lists that disagree on one `
              + 'download refuse it, and nothing is fetched until one of them changes',
          });
          offered.delete(version);
          continue;
        }

        // Rule 4: the same bytes under another address are a mirror, tried in read order.
        if (!held.version.urls.includes(file.url)) held.version.urls.push(file.url);
        if (!held.version.lists.includes(list.origin)) held.version.lists.push(list.origin);
      }
    }

    // Rules 5 and 6: the union, newest first by number; a refused version is never the newest.
    merged.versions = [...offered.values()].map((held) => held.version).sort((a, b) => compareVersions(b.version, a.version));
    merged.refused = [...refused.values()].sort((a, b) => compareVersions(b.version, a.version));
    merged.newest = merged.versions[0]?.version ?? null;
    refusals.push(...merged.refused.map((entry) => entry.problem));
    return merged;
  });

  return { platform, tools, unknown, notes: [...notes, ...refusals] };
}
