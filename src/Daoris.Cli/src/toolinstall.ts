// A managed version of a tool: downloaded, verified, unpacked and laid out (TOOLS4, D121;
// `docs/2026-10-01-tools-design.md` §3.6, §3.7), and `daoris tool`, the terminal's door onto all of it (§4.2).
//
// 🔴 A TWIN of the driver's `ToolInstall.cs`. The two share no code — the LAYOUT, the refusals and the plan are
// the contract — and each carries the same tables (`toolinstall.test.ts` here, `ToolInstallTests.cs` there), row
// for row. A rule changed here is changed there, in the same commit:
//
//   1. A download is staged at `<home>/tools/<tool>/<version>.part/`: the archive lands there and is held to the
//      list's size and SHA-256, it is unpacked into `package/`, the executable is found at `exe`, and `tool.json`
//      is written. Then the whole folder is renamed to `<version>/`. A refusal removes the staging.
//   2. Finding the record, naming a file that is there, is the proof: a version already downloaded fetches nothing.
//   3. Every address a list names is tried in read order, the person's first; each is held to https://, or
//      http:// to this machine, and so is every redirect. The bytes are the check, whichever address served them.
//   4. Which version: the one named, or the newest the lists named at the last look. `update` moves a managed tool
//      to the newest, never back; a tool run from PATH or a file is the machine's to update.
//   5. A version in use is never deleted.
//   6. A look fetches each location in order, each bounded, and keeps a copy only of a list that reads.
//
// NO NETWORK HERE, and nothing spawned. Every byte arrives through a `Fetcher` the caller hands in —
// `service.ts` is still the one module that may open a socket — so a doctrine command that somehow reached this
// file still could not fetch anything (the dogfood tests hold both).

import { chmodSync, existsSync, mkdirSync, readdirSync, rmSync, statSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { operands } from './args.ts';
import type { Fetcher } from './channels.ts';
import { DaorisError, RefusalError, type ExitCode } from './errors.ts';
import { renameHeld, writeBytesAtomic, writeJsonAtomic } from './fsx.ts';
import { HOME_SENTENCE, daorisHome } from './home.ts';
import {
  ARCHIVES, BUILT_IN, builtInList, compareVersions, currentPlatform, locationCopy, mergeResources, parseResources, readLists,
  readResourceFile, type Licence, type MergedResources, type OfferedVersion, type ResourceList,
} from './resources.ts';
import { extractTarGz } from './tarball.ts';
import {
  TOOLS, TOOLS_FILE, TOOLS_FOLDER, TOOL_PACKAGE, TOOL_RECORD, addLocation, declaredTool, isAddress, isExactVersion, isWholePath,
  managedExecutable, readTools, removeLocation, resolveFrom, resolveTool, toolsFile, undeclaredTool, useFile, useManaged, useSystem,
  wayWords, type ToolDeclaration, type ToolResolution,
} from './tools.ts';
import type { CommandArgs } from './types.ts';
import { extractZip } from './zipfile.ts';

type Env = Record<string, string | undefined>;

/** Where a download is staged, beside the folder it becomes. The driver's `ToolInstall.Staging`. */
export const STAGING = '.part';

/** A record's keys, in the order both twins write them (§3.6). The driver's `ToolInstall.RecordKeys`. */
export const RECORD_KEYS: readonly string[] = Object.freeze([
  'tool', 'version', 'platform', 'sha256', 'size', 'archive', 'url', 'lists', 'exe', 'paths', 'at',
]);

/** How many redirects one fetch follows. The driver's `ToolInstall.Redirects`. */
export const REDIRECTS = 10;

/** How long one location may take to answer a look (§3.7). The driver's `ToolInstall.LookBound`. */
export const LOOK_BOUND_MS = 30_000;

/** What a person may ask of a managed version (§4.2). */
export type ToolAction = 'download' | 'use' | 'update';

/** Which version, whether it is fetched, or why not (§3.7): planned without touching the disk or a network. */
export interface ToolPlan {
  tool: string;
  action: ToolAction;
  /** The version downloaded or used; null when there is nothing to do, or a refusal. */
  version: string | null;
  /** The version the tool is managed at now, or null. */
  current: string | null;
  /** True when the version is not downloaded and is fetched from what the lists offer. */
  fetch: boolean;
  /** What the lists offer for it, when it is fetched. */
  offered: OfferedVersion | null;
  source: string | null;
  licence: Licence | null;
  /** Why there is nothing to do, when there is not. */
  nothing: string | null;
  /** The check that refuses it, and why. */
  check: string | null;
  problem: string | null;
}

/** A version's folder under the home: `<home>/tools/<tool>/<version>`. */
export function versionFolder(home: string, tool: string, version: string): string {
  return join(home, TOOLS_FOLDER, tool, version);
}

/** The versions of a tool downloaded and verified — a record naming a file that is there — newest first. */
export function downloadedVersions(home: string, tool: string): string[] {
  let names: string[];
  try {
    names = readdirSync(join(home, TOOLS_FOLDER, tool));
  } catch {
    return [];
  }
  return names.filter((name) => isExactVersion(name) && managedExecutable(home, tool, name).file !== null)
    .sort((a, b) => compareVersions(b, a));
}

function declared(id: string): ToolDeclaration {
  const tool = declaredTool(id);
  if (!tool) throw new DaorisError(undeclaredTool(id));
  return tool;
}

const NO_PLATFORM = `this machine (${process.platform} ${process.arch}) is not a platform a list may name, so no version is offered here`;

/**
 * Which version a download, a use or an update means, and whether it is fetched (§3.7, rule 4). It reads the
 * home's file and what is downloaded, and the lists as last fetched; it fetches nothing.
 */
export function planTool(home: string, merged: MergedResources, id: string, action: ToolAction, asked: string | null): ToolPlan {
  const tool = declared(id);
  const entry = readTools(home).entries[id]!;
  const current = entry.way === 'managed' ? entry.version : null;
  const offers = merged.tools.find((each) => each.tool === id)!;
  const platform = merged.platform;
  const base = { tool: id, action, current, source: offers.source, licence: offers.licence };
  const refuse = (check: string, problem: string): ToolPlan =>
    ({ ...base, version: null, fetch: false, offered: null, nothing: null, check, problem });
  const done = (nothing: string): ToolPlan => ({ ...base, version: null, fetch: false, offered: null, nothing, check: null, problem: null });
  const noneNamed = `no list names a version of ${tool.name} for ${platform} — \`daoris tool look\` fetches the locations, and `
    + '`daoris tool locations add <address>` adds one';
  const here = (version: string): boolean => managedExecutable(home, id, version).file !== null;

  let version: string;
  if (action === 'update') {
    if (entry.problem !== null) return refuse('file', entry.problem);
    if (entry.way !== 'managed') {
      return refuse('machine', `${tool.name} runs ${entry.way === 'file' ? 'a file you name' : "the system's"}: its updates are the `
        + `machine's, not Daoris's — \`daoris tool use ${id} managed\` keeps a version in the home`);
    }
    if (platform === null) return refuse('platform', NO_PLATFORM);
    if (offers.newest === null) return refuse('unknown', noneNamed);
    if (compareVersions(offers.newest, current!) < 0) {
      return done(`${tool.name} is managed at ${current}, newer than the newest the lists name (${offers.newest}) — update never `
        + 'moves a tool back');
    }
    if (offers.newest === current && here(current)) {
      return done(`${tool.name} ${current} is the newest the lists name, and it is downloaded — nothing to do`);
    }
    version = offers.newest;
  } else if (asked !== null) {
    if (!isExactVersion(asked)) return refuse('version', `\`${asked}\` is not an exact version — one to four numbers, like 2.51.0`);
    version = asked;
  } else {
    if (platform === null) return refuse('platform', NO_PLATFORM);
    if (offers.newest === null) return refuse('unknown', noneNamed);
    version = offers.newest;
  }

  if (here(version)) return { ...base, version, fetch: false, offered: null, nothing: null, check: null, problem: null };
  if (platform === null) return refuse('platform', NO_PLATFORM);
  const conflict = offers.refused.find((each) => each.version === version);
  if (conflict) return refuse('conflict', conflict.problem);
  const offered = offers.versions.find((each) => each.version === version);
  if (!offered) return refuse('unknown', `no list names ${tool.name} ${version} for ${platform}`);
  return { ...base, version, fetch: true, offered, nothing: null, check: null, problem: null };
}

/** One download, as `downloadVersion` takes it. */
export interface DownloadOptions {
  home: string;
  tool: string;
  offered: OfferedVersion;
  platform: string;
  fetcher: Fetcher;
  write: (line: string) => void;
}

/**
 * Download one version, verify it, unpack it and lay it out (§3.6, rules 1–3). @returns the executable. Nothing
 * switches: using it is `useManaged`'s, once this has answered.
 *
 * @throws RefusalError naming its check: `archive`, `address`, `unreachable`, `size` or `hash` (the last address
 * tried says which), or the archive's own.
 */
export async function downloadVersion({ home, tool: id, offered, platform, fetcher, write }: DownloadOptions): Promise<string> {
  const tool = declared(id);
  const { version } = offered;
  if (!isExactVersion(version)) throw new RefusalError('version', `\`${version}\` is not an exact version — nothing was fetched`);

  const folder = versionFolder(home, id, version);
  const present = managedExecutable(home, id, version).file;
  if (present !== null) {
    write(`  ${tool.name} ${version} is already downloaded — nothing was fetched.`);
    write(`  ${present}`);
    return present;
  }
  if (!ARCHIVES.includes(offered.archive)) {
    throw new RefusalError('archive', `\`${offered.archive}\` is not an archive this build unpacks — zip or tar.gz; nothing was fetched`);
  }

  const staging = `${folder}${STAGING}`;
  rmSync(staging, { recursive: true, force: true });
  mkdirSync(staging, { recursive: true });
  try {
    const archive = join(staging, 'archive');
    const url = await fetchVerified(tool, offered, archive, fetcher, write);
    write(`  its size and SHA-256 match the list: ${offered.sha256}`);

    await unpackPackage(archive, offered.archive, join(staging, TOOL_PACKAGE), offered.exe);
    rmSync(archive, { force: true });
    const record: Record<string, unknown> = {
      tool: id, version, platform, sha256: offered.sha256, size: offered.size, archive: offered.archive, url,
      lists: [...offered.lists], exe: offered.exe, paths: [...offered.paths], at: new Date().toISOString(),
    };
    writeJsonAtomic(join(staging, TOOL_RECORD), Object.fromEntries(RECORD_KEYS.map((key) => [key, record[key]])));

    if (existsSync(folder)) {
      // Daoris's own folder, holding nothing that runs (asked above): a download that stopped, or a record that went.
      write(`  replacing ${folder}, which held nothing that runs`);
      rmSync(folder, { recursive: true, force: true });
    }
    // The scanner opens the executable just unpacked, and the folder cannot move until it lets go (FIX-LOG 2026-10-07).
    renameHeld(staging, folder);
    write(`  unpacked into ${join(folder, TOOL_PACKAGE)}`);
    return join(folder, TOOL_PACKAGE, ...offered.exe.split('/'));
  } finally {
    rmSync(staging, { recursive: true, force: true });
    // A refusal leaves nothing under the tool's folder, not even the folder the staging made.
    const parent = join(home, TOOLS_FOLDER, id);
    try {
      if (readdirSync(parent).length === 0) rmSync(parent, { recursive: true, force: true });
    } catch {
      // Gone already, or never made.
    }
  }
}

/** The archive, from the first address whose bytes are the list's (rule 3). @returns that address. */
async function fetchVerified(tool: ToolDeclaration, offered: OfferedVersion, to: string, fetcher: Fetcher, write: (line: string) => void): Promise<string> {
  const reasons: string[] = [];
  let last: RefusalError | null = null;
  for (const [at, url] of offered.urls.entries()) {
    let failed: RefusalError;
    try {
      if (!isAddress(url)) throw new RefusalError('address', `${url} is not https://, or http:// to this machine — nothing was fetched from it`);
      write(`  downloading ${url} (${megabytes(offered.size)})`);
      const saved = await fetcher.save(url, to);
      if (saved === null) throw new RefusalError('unreachable', `${url} has nothing there`);
      if (saved.size !== offered.size) throw new RefusalError('size', `${url} served ${saved.size} bytes, and the list says ${offered.size}`);
      if (saved.sha256 !== offered.sha256) {
        throw new RefusalError('hash', `${url} served bytes whose SHA-256 is ${saved.sha256}, and the list says ${offered.sha256}`);
      }
      return url;
    } catch (error) {
      if (!(error instanceof RefusalError)) throw error;
      failed = error;
    }

    rmSync(to, { force: true });
    write(`  ${failed.message}${at < offered.urls.length - 1 ? ' — trying the next address' : ''}`);
    reasons.push(failed.message);
    last = failed;
  }

  throw new RefusalError(last?.check ?? 'unreachable', `${tool.name} ${offered.version} was not downloaded: `
    + `${reasons.length > 0 ? reasons.join('; ') : 'no address names it'}. Nothing was kept`);
}

/**
 * Unpack a verified archive whole into `into`, and find the executable its list names there (§3.6). @returns the
 * executable's path.
 *
 * @throws RefusalError naming its check: the archive's own (`zipfile.ts`, `tarball.ts`), `archive` for a kind this
 * build does not unpack, or `exe` when the executable is not a file in it.
 */
export async function unpackPackage(archive: string, kind: string, into: string, exe: string): Promise<string> {
  if (kind === 'zip') await extractZip(archive, into);
  else if (kind === 'tar.gz') await extractTarGz(archive, into);
  else throw new RefusalError('archive', `\`${kind}\` is not an archive this build unpacks — zip or tar.gz`);

  const file = join(into, ...exe.split('/'));
  if (!isFile(file)) {
    throw new RefusalError('exe', `the archive holds no file at \`${exe}\`, the executable its list names — nothing of it is kept`);
  }
  // Nothing is patched (§3.6): the executable bit off Windows is the one change, and it is the file system's.
  if (process.platform !== 'win32') chmodSync(file, 0o755);
  return file;
}

/**
 * Delete a downloaded version nothing uses (§3.6, rule 5). @returns the folder deleted.
 *
 * @throws RefusalError: `version`, `missing`, `file` (which version runs cannot be read), `in-use`, or `held` with the
 * system's reason when something still holds a file in it.
 */
export function deleteVersion(home: string, id: string, version: string): string {
  const tool = declared(id);
  if (!isExactVersion(version)) {
    throw new RefusalError('version', `\`${version}\` is not an exact version — one to four numbers, like 2.51.0 — so nothing was deleted`);
  }
  const folder = versionFolder(home, id, version);
  if (!existsSync(folder)) throw new RefusalError('missing', `${tool.name} ${version} is not downloaded (${folder}) — nothing was deleted`);

  const read = readTools(home);
  if (read.problem !== null) {
    throw new RefusalError('file', `${read.problem} — nothing was deleted, since which version runs cannot be read`);
  }
  const entry = read.entries[id]!;
  if (entry.way === 'managed' && entry.version === version) {
    throw new RefusalError('in-use', `${tool.name} runs ${version} — \`daoris tool use ${id} system\`, or another version, first; `
      + 'nothing was deleted');
  }

  try {
    rmSync(folder, { recursive: true });
  } catch (error) {
    throw new RefusalError('held', `${folder} could not be deleted — ${(error as Error).message}. A program may be running from it: stop `
      + 'it and delete again; part of it may already be gone');
  }
  return folder;
}

/** One location, looked at (§3.7). */
export interface LocationLook {
  address: string;
  /** `fetched` (its copy is the list just fetched), `unread` (it answered no list this build reads) or `failed`. */
  outcome: 'fetched' | 'unread' | 'failed';
  sentence: string;
  /** What the copy names now that the one before did not, for this platform: `<tool> <version>`. */
  added: string[];
  dropped: string[];
}

/**
 * Fetch each resource location, in order, each bounded by the fetcher (§3.7, rule 6). A list that reads replaces
 * its copy, the bytes as fetched; anything else keeps the last copy and says its age. Downloads nothing.
 */
export async function lookLocations(home: string, fetcher: Fetcher, platform: string | null, now: number = Date.now()): Promise<LocationLook[]> {
  const looks: LocationLook[] = [];
  for (const address of readTools(home).locations) {
    const copy = locationCopy(home, address);
    const before = readResourceFile(copy, address);
    const kept = (): string => (before.exists ? `its copy from ${age(copy, now)} ago is kept` : 'it has never been fetched, so it names nothing');
    const failed = (why: string): LocationLook =>
      ({ address, outcome: 'failed', sentence: `${address} could not be fetched (${why}); ${kept()}`, added: [], dropped: [] });

    let bytes: Buffer | null;
    try {
      bytes = await fetcher.bytes(address);
    } catch (error) {
      if (!(error instanceof DaorisError)) throw error;
      looks.push(failed(error.message.replace(/\.$/, '')));
      continue;
    }
    if (bytes === null) {
      looks.push(failed('nothing there'));
      continue;
    }

    const list = parseResources(bytes.toString('utf8'), address);
    if (list.problem !== null) {
      looks.push({ address, outcome: 'unread', sentence: `${address} answered a list this build does not read (${list.problem}); ${kept()}`, added: [], dropped: [] });
      continue;
    }

    writeBytesAtomic(copy, bytes);
    const was = offeredHere(before, platform);
    const is = offeredHere(list, platform);
    const added = is.filter((each) => !was.includes(each));
    const dropped = was.filter((each) => !is.includes(each));
    const parts = [`fetched — ${is.length} version${is.length === 1 ? '' : 's'} for ${platform ?? 'no platform'}`];
    if (added.length > 0) parts.push(`added ${added.join(', ')}`);
    if (dropped.length > 0) parts.push(`dropped ${dropped.join(', ')}`);
    if (before.exists && added.length === 0 && dropped.length === 0) parts.push('nothing changed since the copy before');
    looks.push({ address, outcome: 'fetched', sentence: `${address}: ${parts.join('; ')}`, added, dropped });
  }
  return looks;
}

/** What a list offers for one platform, `<tool> <version>`, in the declared order and newest first. */
function offeredHere(list: ResourceList, platform: string | null): string[] {
  if (platform === null) return [];
  return TOOLS.flatMap((tool) => [...(list.tools.get(tool.id)?.versions ?? new Map())]
    .filter(([, files]) => files.has(platform))
    .map(([version]) => version)
    .sort((a, b) => compareVersions(b, a))
    .map((version) => `${tool.id} ${version}`));
}

/** How long ago a file was written, in the largest whole unit. */
function age(file: string, now: number): string {
  const seconds = Math.max(0, Math.round((now - statSync(file).mtimeMs) / 1000));
  const [count, unit] = seconds < 120 ? [seconds, 'second'] : seconds < 7200 ? [Math.round(seconds / 60), 'minute']
    : seconds < 172800 ? [Math.round(seconds / 3600), 'hour'] : [Math.round(seconds / 86400), 'day'];
  return `${count} ${unit}${count === 1 ? '' : 's'}`;
}

function megabytes(bytes: number): string {
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

/** A file, as .NET's `File.Exists` answers: a folder is not one. */
function isFile(path: string): boolean {
  try {
    return statSync(path).isFile();
  } catch {
    return false;
  }
}

// ——— The verb.

/** The home, or the refusal every management verb gives without one (D63). */
function requireHome(env: Env): string {
  const home = daorisHome(env);
  if (!home) throw new DaorisError(`${HOME_SENTENCE} (wanted: ${TOOLS_FILE})`);
  return home;
}

/** How one tool reads on a row of `list`. */
function row(tool: ToolDeclaration, resolution: ToolResolution): string {
  const lead = `  ${tool.id.padEnd(5)} ${tool.name.padEnd(10)} `;
  const how = resolution.way === 'managed' ? `managed ${resolution.version}` : resolution.way ? wayWords(resolution.way) : null;
  if (how === null) return `${lead}refused — ${resolution.problem}`;
  if (resolution.file !== null) return `${lead}${how}: ${resolution.file}`;
  return `${lead}${how}: ${resolution.refused ? 'refused' : 'not found'} — ${resolution.problem}`;
}

const VERBS = 'list, path, use, download, update, delete, locations, look';

/**
 * `daoris tool` — the terminal's door onto `tools.json`, the lists and the versions kept in the home (D50, §4.2).
 * Management class. `download`, `use … managed`, `update` and `look` reach a network, and only through the fetcher
 * the dispatcher hands in; every other verb answers at once and opens nothing.
 */
export function commandTool({ root, argv, write }: CommandArgs, fetcher: Fetcher | null = null, env: Env = process.env): ExitCode | Promise<ExitCode> {
  const [verb = 'list', id, way, named] = operands(argv, new Set());
  const ids = TOOLS.map((tool) => tool.id).join(', ');
  const needTool = (said: string): ToolDeclaration => {
    if (!id) throw new DaorisError(`\`tool ${said}\` needs a tool — one of: ${ids}`);
    return declared(id);
  };

  switch (verb) {
    case 'list': {
      const home = requireHome(env);
      const read = readTools(home);
      write(read.exists ? `daoris: ${read.path}` : `daoris: ${read.path} — no file, so every tool is the system's, from PATH.`);
      let refused = false;
      for (const tool of TOOLS) {
        const resolution = resolveFrom(read, home, tool.id, env);
        refused ||= resolution.refused;
        write(row(tool, resolution));
        const kept = downloadedVersions(home, tool.id);
        if (kept.length > 0) write(`        downloaded: ${kept.join(', ')}`);
      }
      for (const note of read.notes) write(`  ${note}`);
      write('');
      write('  `daoris tool use <tool> system|managed [<version>]|file <path>` sets one; nothing switches on its own.');
      return refused ? 1 : 0;
    }

    case 'path': {
      const tool = needTool('path');
      const resolution = resolveTool(requireHome(env), tool.id, env);
      if (resolution.file === null) {
        write(`daoris: ${resolution.problem}`);
        return 1;
      }
      // Alone on its line, for a script to take.
      write(resolution.file);
      return 0;
    }

    case 'use': {
      const tool = needTool('use');
      const home = requireHome(env);
      const written = toolsFile(home);

      if (way === 'system') {
        useSystem(home, tool.id);
        const resolution = resolveTool(home, tool.id, env);
        write(`daoris: ${tool.name} is set to the system's, from PATH — written to ${written}.`);
        write(resolution.file !== null
          ? `  \`daoris tool path ${tool.id}\` answers ${resolution.file}.`
          : `  \`daoris tool path ${tool.id}\` answers: ${resolution.problem}.`);
        return 0;
      }

      if (way === 'file') {
        if (!named) throw new DaorisError(`\`tool use ${tool.id} file\` needs a path — the executable to run`);
        const file = isWholePath(named) ? named : resolve(root, named);
        useFile(home, tool.id, file);
        write(`daoris: ${tool.name} is set to the file ${file} — written to ${written}.`);
        write(`  \`daoris tool path ${tool.id}\` answers ${file}; \`daoris tool use ${tool.id} system\` puts it back on PATH.`);
        return 0;
      }

      if (way === 'managed') return managed(home, tool, 'use', named ?? null);
      throw new DaorisError(`\`tool use ${tool.id}\` takes \`system\`, \`managed [<version>]\` or \`file <path>\``);
    }

    case 'download':
      return managed(requireHome(env), needTool('download'), 'download', way ?? null);

    case 'update':
      return managed(requireHome(env), needTool('update'), 'update', null);

    case 'delete': {
      const tool = needTool('delete');
      if (!way) throw new DaorisError(`\`tool delete ${tool.id}\` needs a version — \`daoris tool list\` names the ones downloaded`);
      const folder = refusing(() => deleteVersion(requireHome(env), tool.id, way));
      write(`daoris: ${tool.name} ${way} is deleted — ${folder}.`);
      return 0;
    }

    case 'locations':
      return locations(requireHome(env), id ?? null, way ?? null);

    case 'look':
      return look(requireHome(env));

    default:
      throw new DaorisError(`unknown tool verb '${verb}' — one of: ${VERBS}`);
  }

  /** Download, use or update a managed version (§3.6, §3.7): planned first, so a refusal fetches nothing. */
  function managed(home: string, tool: ToolDeclaration, action: ToolAction, asked: string | null): ExitCode | Promise<ExitCode> {
    const platform = currentPlatform();
    const plan = planTool(home, mergeResources(readLists(home), platform), tool.id, action, asked);
    if (plan.problem !== null) throw new RefusalError(plan.check!, plan.problem, 1);
    if (plan.nothing !== null) {
      write(`daoris: ${plan.nothing}.`);
      return 0;
    }
    if (!plan.fetch) return finish(home, tool, plan, null);
    if (!fetcher) throw new DaorisError(`this build was given no way to reach a network, so ${tool.name} ${plan.version} was not downloaded.`);

    const offered = plan.offered!;
    const licence = plan.licence ? `${plan.licence.id}${plan.licence.url ? ` (${plan.licence.url})` : ''}` : 'no licence named';
    write(`daoris: ${tool.name} ${offered.version} for ${platform} — ${megabytes(offered.size)}, ${licence}${plan.source ? `, from ${plan.source}` : ''}.`);
    write('  Daoris redistributes nothing: it downloads from the addresses the lists name, as you, and checks the bytes.');
    return downloadVersion({ home, tool: tool.id, offered, platform: platform!, fetcher, write })
      .catch((error: unknown) => {
        if (error instanceof RefusalError) throw new RefusalError(error.check, error.message, 1);
        throw error;
      })
      .then((exe) => finish(home, tool, plan, exe));
  }

  /** What a download, a use or an update ends with: `use` and `update` switch to the version, `download` never does. */
  function finish(home: string, tool: ToolDeclaration, plan: ToolPlan, fetched: string | null): ExitCode {
    const version = plan.version!;
    const exe = fetched ?? managedExecutable(home, tool.id, version).file!;
    if (plan.action === 'download') {
      write(fetched === null
        ? `daoris: ${tool.name} ${version} is already downloaded — nothing was fetched, and nothing switches.`
        : `daoris: ${tool.name} ${version} is downloaded and verified — nothing switches.`);
      write(`  ${exe}`);
      write(`  \`daoris tool use ${tool.id} managed ${version}\` runs it.`);
      return 0;
    }

    useManaged(home, tool.id, version);
    write(`daoris: ${tool.name} is set to managed ${version}${plan.current && plan.current !== version ? ` (it was ${plan.current})` : ''} — `
      + `written to ${toolsFile(home)}.`);
    write(`  \`daoris tool path ${tool.id}\` answers ${exe}; \`daoris tool use ${tool.id} system\` puts it back on PATH.`);
    return 0;
  }

  /** `locations` alone lists them in read order; `add` and `remove` edit the file (§3.3). */
  function locations(home: string, sub: string | null, address: string | null): ExitCode {
    if (sub === 'add' || sub === 'remove') {
      if (!address) throw new DaorisError(`\`tool locations ${sub}\` needs an address — https://, or http:// to this machine`);
      if (sub === 'add') {
        write(addLocation(home, address)
          ? `daoris: ${address} is added, read before the list built in — \`daoris tool look\` fetches it.`
          : `daoris: ${address} is already a location — nothing was written.`);
      } else {
        write(removeLocation(home, address)
          ? `daoris: ${address} is removed: its versions are no longer offered, and a version already downloaded stays.`
          : `daoris: ${address} is not a location — nothing was written.`);
      }
      return 0;
    }
    if (sub !== null) throw new DaorisError(`\`tool locations\` takes \`add <address>\` or \`remove <address>\`, or nothing to list them`);

    const read = readTools(home);
    write(`daoris: ${read.path} — the lists versions come from, read in this order:`);
    const now = Date.now();
    read.locations.forEach((each, at) => {
      const list = readResourceFile(locationCopy(home, each), each);
      write(list.exists
        ? `  ${at + 1}. ${each} — fetched ${age(list.path!, now)} ago, vouched for by ${list.integrity}, sha256 ${list.sha256}`
        : `  ${at + 1}. ${each} — never fetched, so it names nothing yet`);
    });
    const built = readResourceFile(builtInList(home), BUILT_IN);
    write(built.exists
      ? `  ${read.locations.length + 1}. ${BUILT_IN} — ${built.path}, sha256 ${built.sha256}`
      : `  ${read.locations.length + 1}. ${BUILT_IN} — none beside this home (${built.path})`);
    for (const note of read.notes) write(`  ${note}`);
    return 0;
  }

  /** Fetch every location, then say for each tool what it runs and the newest the lists name (§3.7). */
  async function look(home: string): Promise<ExitCode> {
    if (!fetcher) throw new DaorisError('this build was given no way to reach a network, so no location was fetched.');
    const platform = currentPlatform();
    const looks = await lookLocations(home, fetcher, platform);
    write(looks.length === 0
      ? 'daoris: no resource location is listed — only the list built in is read. `daoris tool locations add <address>` adds one.'
      : `daoris: looked at ${looks.length} location${looks.length === 1 ? '' : 's'}:`);
    for (const each of looks) write(`  ${each.sentence}`);

    const read = readTools(home);
    const merged = mergeResources(readLists(home, read), platform);
    write('');
    for (const tool of TOOLS) {
      const entry = read.entries[tool.id]!;
      const runs = entry.problem !== null ? 'refused' : entry.way === 'managed' ? `managed ${entry.version}` : wayWords(entry.way!);
      const offers = merged.tools.find((each) => each.tool === tool.id)!;
      const newest = offers.versions[0];
      const newer = newest && entry.way === 'managed' && entry.version !== null && compareVersions(newest.version, entry.version) > 0;
      write(`  ${tool.id.padEnd(5)} ${tool.name.padEnd(10)} runs ${runs}; ${newest
        ? `the lists name ${newest.version} (${newest.lists[0]})${newer ? ` — \`daoris tool update ${tool.id}\` moves to it` : ''}`
        : `no list names a version for ${platform ?? 'this machine'}`}`);
    }
    for (const note of merged.notes) write(`  ${note}`);
    return looks.every((each) => each.outcome === 'fetched') ? 0 : 1;
  }
}

/** A refusal by a check is exit 1 (§4.2): the file's rules or a check said no, which is not a tool error. */
function refusing<T>(work: () => T): T {
  try {
    return work();
  } catch (error) {
    if (error instanceof RefusalError) throw new RefusalError(error.check, error.message, 1);
    throw error;
  }
}
