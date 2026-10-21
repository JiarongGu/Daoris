// The machine's remotes — `remotes.json` under the Daoris home (D63), a map of workspace → { url, key }
// (D48 §5).
//
// This module READS AND WRITES A FILE AND NOTHING ELSE. It opens no socket, which is why `status` may
// import it without touching the doctrine commands' offline guarantee (D8/D50): the wiring is a local
// fact about this machine, and reporting it is a local read.
//
// It is the CLI's twin of the service's `RemoteConfig` and the driver's `RemoteTarget`. Three copies
// exist because the three artefacts share no code — the FILE is the contract — so the three rules
// below must be identical in all of them, and each carries a test table saying so:
//
//   1. Either environment variable present means the environment IS the answer, for the WHOLE machine.
//   2. A half-set pair is no remote at all — never an env url with the file's key.
//   3. Absence is the default, and it is silent (D21).

import { findName } from '../platform/casefold.ts';
import { readJsonObject } from '../platform/fsx.ts';
import { homeFile, requireHomeFile } from '../platform/home.ts';
import { writeKeeping } from '../platform/jsonkeep.ts';

/** Where a workspace's shared deployment is, and the key this machine speaks to it with. */
export interface Remote {
  url: string;
  key: string;
}

/** What the machine's wiring is, and where the answer came from. */
export interface Wiring {
  /** `environment` when the env pair is set, `file` when the map named something, `none` otherwise. */
  source: 'environment' | 'file' | 'none';
  /**
   * The map's path — reported even when the environment outranks it, so the person can find it; null
   * on a machine with no Daoris home, which has no map to find.
   */
  path: string | null;
  remotes: Map<string, Remote>;
}

export const URL_VARIABLE = 'DAORIS_REMOTE_URL';
export const KEY_VARIABLE = 'DAORIS_REMOTE_KEY';
export const WORKSPACE_VARIABLE = 'DAORIS_REMOTE_WORKSPACE';
export const PATH_VARIABLE = 'DAORIS_REMOTE_CONFIG';

/** Where a repository lives when nobody has said otherwise — the service's `Workspaces.Default`. */
export const DEFAULT_WORKSPACE = 'default';

type Env = Record<string, string | undefined>;

/** A workspace name as it is stored and compared: trimmed, and the default when unstated. */
export function normalizeWorkspace(name?: string | null): string {
  return name?.trim() ? name.trim() : DEFAULT_WORKSPACE;
}

/**
 * The map's path: the override, or the file under the Daoris home (D63) — or null where there is no
 * home, which is a machine with no remotes: the documented default, and silent.
 */
export function remotesPath(env: Env = process.env): string | null {
  return env[PATH_VARIABLE] ?? homeFile(env, 'remotes');
}

/** The map's path for a verb that EDITS it: the override, or the home's file, or a refusal naming what to set. */
export function remotesPathRequired(env: Env = process.env): string {
  return env[PATH_VARIABLE] ?? requireHomeFile(env, 'remotes');
}

/**
 * A key as it may be shown: the audit prefix the deployment's own `keys list` prints, and nothing
 * else. Anything shorter than a prefix is shown as nothing at all — a "redaction" that leaks a short
 * key is worse than printing it, because it reads as safe.
 */
export function redactKey(key: string): string {
  return key.length > 11 ? `${key.slice(0, 11)}…` : '…';
}

/** The map as it sits on disk. */
function parse(path: string | null): Map<string, Remote> {
  // A file that will not parse names no remote. The sync loop, not this reader, is where "you
  // configured a remote and it does not work" gets said out loud — and the editor refuses to write
  // over it (`writeKeeping`). A BOM is not "will not parse": the C# twins strip it.
  return path === null ? new Map() : remotesIn(readJsonObject(path).value ?? {});
}

/** The remotes `entries` name. */
function remotesIn(entries: Record<string, unknown>): Map<string, Remote> {
  const remotes = new Map<string, Remote>();
  for (const [workspace, value] of Object.entries(entries)) {
    if (!whole(value)) continue;
    // An entry missing half its pair is one workspace with no remote, never a machine with none: a
    // typo in one circle must not silently unwire the others.
    remotes.set(normalizeWorkspace(workspace), { url: (value.url as string).replace(/\/+$/, ''), key: value.key as string });
  }

  return remotes;
}

/** Whether an entry is a whole pair, the only one any reader reads. */
function whole(value: unknown): value is Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
  const { url, key } = value as { url?: unknown; key?: unknown };
  return typeof url === 'string' && typeof key === 'string' && url.trim() !== '' && key.trim() !== '';
}

/** This machine's remotes, by workspace, and which source decided. */
export function readRemotes(env: Env = process.env): Wiring {
  const path = remotesPath(env);
  const url = env[URL_VARIABLE];
  const key = env[KEY_VARIABLE];

  if (url?.trim() || key?.trim()) {
    const remotes = new Map<string, Remote>();
    if (url?.trim() && key?.trim()) {
      remotes.set(normalizeWorkspace(env[WORKSPACE_VARIABLE]), { url: url.replace(/\/+$/, ''), key });
    }

    // Still `environment` when the pair is half-set: the environment answered, and its answer was
    // "no remote". Falling back to the file here is the mix rule 2 forbids.
    return { source: 'environment', path, remotes };
  }

  const remotes = parse(path);
  return { source: remotes.size > 0 ? 'file' : 'none', path, remotes };
}

/**
 * The map's file as an editor reads it (KEEPALL1e, D173's KEEPALL1e note), the driver's `RemoteFile`: every entry as the file holds it,
 * in its order, whole or not, and whether a file was there. An edit sets or takes one workspace's entry, and `writeRemoteFile` makes
 * only that onto the file as it stands (`writeKeeping`), so another key, an entry's own unknown keys, an entry missing half its pair,
 * numbers as written and CRLF all stay. Rebuilt from the remotes read, the file lost each of them. Both twins are held to
 * `test/fixtures/home-files-keep.json`, which the service's reader (`RemoteConfig`) reads too.
 */
export interface RemoteFile {
  found: boolean;
  read: Record<string, unknown>;
  entries: Record<string, unknown>;
}

/**
 * The file at `path` as it stands; no entry for none, or for a text that does not read, which `writeRemoteFile` then refuses to write
 * over. A key written twice is read as `JSON.parse` reads it, its last value in its first place.
 */
export function readRemoteFile(path: string): RemoteFile {
  const { value, problem, text } = readJsonObject(path);
  const entries = value ?? {};
  return { found: text !== undefined || problem !== null, read: entries, entries };
}

/** The remotes a file's entries name, as every reader reads them. */
export function remotesOf(file: RemoteFile): Map<string, Remote> {
  return remotesIn(file.entries);
}

/** The names in `file` held as `workspace`, compared as a person compares names, in the file's order. */
function holding(file: RemoteFile, workspace: string): string[] {
  const wanted = normalizeWorkspace(workspace);
  return Object.keys(file.entries).filter((name) => findName([normalizeWorkspace(name)], wanted) !== null);
}

/**
 * `workspace` wired to `remote`: the entry the readers read for it changed where it stands, else the last one held under its name in any
 * case (an entry missing half its pair, completed), else a new entry, last. Its own other keys stay.
 */
export function withRemote(file: RemoteFile, workspace: string, remote: Remote): RemoteFile {
  const held = holding(file, workspace);
  const name = held.filter((each) => whole(file.entries[each])).at(-1) ?? held.at(-1);
  const was = name === undefined ? undefined : file.entries[name];
  const own = was !== null && typeof was === 'object' && !Array.isArray(was) ? was as Record<string, unknown> : {};
  return { ...file, entries: { ...file.entries, [name ?? normalizeWorkspace(workspace)]: { ...own, url: remote.url, key: remote.key } } };
}

/** `workspace` no longer wired: every entry the readers read under its name in any case taken, whole. */
export function withoutRemote(file: RemoteFile, workspace: string): RemoteFile {
  const taken = holding(file, workspace).filter((name) => whole(file.entries[name]));
  if (taken.length === 0) return file;
  return { ...file, entries: Object.fromEntries(Object.entries(file.entries).filter(([name]) => !taken.includes(name))) };
}

/**
 * The edit made onto the file as it stands (`writeKeeping`): beside-then-rename, BOM-less UTF-8, its lines ended as the file's are, and
 * no edit nothing written. A driver tick may read it at any moment, and a torn read must never be what it finds.
 *
 * @throws DaorisError The file there now does not read or holds a key twice; nothing was written.
 */
export function writeRemoteFile(path: string, file: RemoteFile): void {
  writeKeeping(path, file.found, file.read, file.entries);
}

/**
 * The key a workspace is held under in `remotes`, compared as a person compares names — trimmed and
 * case-insensitive, the rule both C# twins read the file by (`OrdinalIgnoreCase`, which `casefold.ts`
 * compares by, CASEFOLD1). Without it `remote remove aurora` said "not wired" over an `Aurora` the
 * driver kept syncing to (REV3).
 */
export function heldAs(remotes: Map<string, Remote>, workspace: string): string | undefined {
  return findName(remotes.keys(), normalizeWorkspace(workspace)) ?? undefined;
}
