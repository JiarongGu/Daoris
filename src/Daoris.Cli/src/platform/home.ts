import { existsSync, linkSync, mkdirSync, readdirSync, readFileSync, statSync, unlinkSync, writeFileSync } from 'node:fs';
import { randomBytes } from 'node:crypto';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { DaorisError } from './errors.ts';

/**
 * Where every machine-local file Daoris owns lives (D63): one home, named by `DAORIS_HOME`, and **no
 * default under the user profile**. The application's own folder is the home — the installed desktop
 * sets the variable for itself and everything it spawns — and this CLI reads the same variable from
 * the user's environment, or, run from an install with none, that install's own `data` (D171). Absent
 * both, there is no home: the management class refuses with one sentence rather than writing
 * somewhere nobody pointed it; doctrine commands never needed a home.
 *
 * One of three twins — the driver's and the service's `DaorisHome` hold the same contract in C# and
 * share no code, the way the remotes map's three copies do (WSP3). The environment is the contract.
 *
 * Each file and folder under the home is an entry of D172's table (`HOME_ENTRIES`, HOMELAYOUT2), and a reader asks for the
 * entry by name (`homeEntry`), never for a path it joined itself; `home-census.test.ts` fails a join by hand. The three
 * twins hold the same table, cell for cell (`home.test.ts`).
 */

export const HOME_VARIABLE = 'DAORIS_HOME';

/** The one sentence every refusal for a missing home carries. */
export const HOME_SENTENCE =
  'no Daoris home: set DAORIS_HOME to the application\'s data folder (the `data` directory beside '
  + 'Daoris.exe), or name the file\'s own variable. Daoris keeps nothing under the user profile.';

/**
 * The refusal for an entry found in both its places (D172 point 5's review note): the entry's name, its new place and its old
 * one. The driver's and the service's `DaorisHome.Twice` are the same words.
 */
export const HOME_TWICE =
  '{0} is in two places, {1} and {2}, and Daoris will not choose between them: move away or remove the one you do not '
  + 'mean (D172 point 5).';

type Env = Record<string, string | undefined>;

/** The file `desktop-publish` writes at an install's root, and nothing else does (the driver's and the service's `DaorisHome.InstallMarker`). */
export const INSTALL_MARKER = 'INSTALLED.md';

/** The folder an install's programs run from (D93), and its home beside it (D63). */
export const INSTALL_APP = 'app';
export const INSTALL_DATA = 'data';

/**
 * The install a folder sits in (FINDDAORIS1, D171 point 1): the folder above the `app/` it is in, however deep (this package is
 * `app/cli/node_modules/daoris/`), where that folder holds the marker. Null for a checkout, which is no install.
 */
export function installOf(from: string): string | null {
  for (let directory = resolve(from); ;) {
    const parent = dirname(directory);
    if (parent === directory) return null;
    if (basename(directory).toLowerCase() === INSTALL_APP && existsSync(join(parent, INSTALL_MARKER))) return parent;
    directory = parent;
  }
}

/** The install this package runs from, or null for a checkout. */
export const THIS_INSTALL: string | null = installOf(dirname(fileURLToPath(import.meta.url)));

/**
 * The home, or null when there is none. Blank is none. The variable still wins (the dev loop, a rehearsal, a second home);
 * with none, a `daoris` the install carries finds the install's own `data` folder, as the driver host does (FINDDAORIS1),
 * since the install's launchers set nothing.
 */
export function daorisHome(env: Env = process.env, install: string | null = THIS_INSTALL): string | null {
  const home = env[HOME_VARIABLE]?.trim();
  if (home) return home;
  return install ? join(install, INSTALL_DATA) : null;
}

/** A top folder of the Daoris folder (D172 point 1): the category an entry is in. */
export type HomeCategory = 'data' | 'profiles' | 'work' | 'cache' | 'run' | 'logs';

/**
 * An entry of D172's table (the install layout design §3, HOMELAYOUT2): its name, its category, its new place and its old one,
 * each `/`-separated under the home, a `*` for each part of an item where the entry is kept per item, and the names that make
 * a place there where the entry is known by them (none: the place itself).
 */
export interface HomeEntry {
  readonly name: string;
  readonly category: HomeCategory;
  readonly new: string;
  readonly old: string;
  readonly holds: readonly string[];
}

const row = <const N extends string>(name: N, category: HomeCategory, next: string, old: string, ...holds: string[]) =>
  ({ name, category, new: next, old, holds }) satisfies HomeEntry;

/**
 * D172's table, in order: the driver's and the service's `HomeEntry` hold the same rows, which `home.test.ts` holds cell for
 * cell. 🔴 The new places are switched on (HOMELAYOUT3): each under the top folder its category names, under the Daoris folder,
 * and each old place under the old home (`homeFolder`), so an install's `data/` entries are where they were. An entry made by
 * HOMELAYOUT3 itself had no old place: its old column repeats its new, a place under the old home nothing writes.
 */
export const HOME_ENTRIES = [
  row('driver', 'data', 'data/driver.json', 'driver.json'),
  row('harnesses', 'data', 'data/harnesses.json', 'harnesses.json'),
  row('accounts', 'data', 'data/accounts.json', 'accounts.json'),
  row('cooling', 'data', 'data/cooling.json', 'cooling.json'),
  row('windows', 'data', 'data/windows.json', 'windows.json'),
  row('reads', 'data', 'data/reads.json', 'reads.json'),
  row('permissions', 'data', 'data/permissions.json', 'permissions.json'),
  row('rule-proposals', 'data', 'data/proposals', 'proposals'),
  row('tools', 'data', 'data/tools.json', 'tools.json'),
  row('plugin-state', 'data', 'data/plugins.json', 'plugins.json'),
  row('plugins', 'data', 'data/plugins', 'plugins'),
  row('plugin-approvals', 'data', 'data/plugin-approvals.json', 'plugin-approvals.json'),
  row('usage', 'data', 'data/usage.json', 'usage.json'),
  row('landings', 'data', 'data/landings.json', 'landings.json'),
  row('session-branches', 'data', 'data/session-branches.json', 'session-branches.json'),
  row('lines-moved', 'data', 'data/lines-moved.json', 'lines-moved.json'),
  row('registry-followed', 'data', 'data/registry-followed.json', 'registry-followed.json'),
  row('abandoned', 'data', 'data/abandoned.json', 'abandoned.json'),
  row('update', 'data', 'data/update.json', 'update.json'),
  row('knowledge', 'data', 'data/knowledge.db', 'knowledge.db'),
  row('sessions', 'data', 'data/sessions', 'sessions'),
  row('helpers', 'data', 'data/helpers', 'helpers'),
  row('handed', 'data', 'data/handed', 'handed'),
  row('setup', 'data', 'data/setup', 'setup'),
  row('workflows', 'data', 'data/workflows', 'workflows'),
  row('help-proposals', 'data', 'data/help/proposals', 'help/proposals'),
  row('configured-access', 'data', 'data/configured-access.json', 'configured-access.json'),
  row('opinion-gates', 'data', 'data/opinions/gates', 'opinions/gates'),
  row('opinion-dues', 'data', 'data/opinions/due.json', 'opinions/due.json'),
  row('browser-settings', 'data', 'data/browser/settings.json', 'browser/settings.json'),
  row('browser-favorites', 'data', 'data/browser/favorites.json', 'browser/favorites.json'),
  row('home-id', 'data', 'data/home.json', 'home.json'),
  row('brainstorms', 'data', 'data/brainstorms', 'brainstorms'),
  row('usage-history', 'data', 'data/usage-history', 'usage-history'),
  row('keys', 'profiles', 'profiles/keys.json', 'keys.json'),
  row('remotes', 'profiles', 'profiles/remotes.json', 'remotes.json'),
  row('agent-homes', 'profiles', 'profiles/agents/*/*', 'harnesses/*/*'),
  row('browser-engine', 'profiles', 'profiles/browser/engine', 'browser/engine'),
  row('browser-edge', 'profiles', 'profiles/browser/edge', 'browser/edge'),
  row('edge-cookies', 'profiles', 'profiles/browser/edge-session-cookies.bin', 'browser/edge-session-cookies.bin'),
  row('window', 'profiles', 'profiles/window', '', 'chromium', 'config'),
  row('trees', 'work', 'work/trees/*/*/*', 'trees/*/*/*'),
  row('help-room', 'work', 'work/rooms/help', 'help', 'CLAUDE.md'),
  row('help-rooms', 'work', 'work/rooms/help-*', 'help-*', 'CLAUDE.md'),
  row('intake', 'work', 'work/rooms/intake/*', 'intake/*'),
  row('opinion-packets', 'work', 'work/opinions/*', 'opinions/*'),
  row('quest-files', 'work', 'work/files/quests/*', 'quests/*'),
  row('ask-files', 'work', 'work/files/asks/*', 'asks/*'),
  row('session-files', 'work', 'work/files/sessions/*', 'sessions/*/files'),
  row('proof', 'work', 'work/files/proof/*', 'proof/*'),
  row('usage-read', 'work', 'work/usage-read', 'usage-read'),
  row('brainstorm-rooms', 'work', 'work/brainstorm-rooms/*', 'brainstorm-rooms/*'),
  row('toolchain', 'cache', 'cache/toolchain', 'toolchain'),
  row('tool-versions', 'cache', 'cache/tools', 'tools'),
  row('bin', 'cache', 'cache/bin', 'bin'),
  row('home-lease', 'run', 'run/home.lease', 'home.lease'),
  row('driver-lock', 'run', 'run/driver.lock', 'driver.lock'),
  row('running', 'run', 'run/running.json', 'running.json'),
  row('locks', 'run', 'run/locks', 'locks'),
  row('spawn', 'run', 'run/spawn', 'spawn'),
  row('starts', 'run', 'run/starts', 'starts'),
  row('hooks', 'run', 'run/hooks', 'hooks'),
  row('probing', 'run', 'run/probing', 'harnesses/.probing'),
  row('session-pids', 'run', 'run/sessions/*.pid', 'sessions/*.pid'),
  row('requests', 'run', 'run/requests', 'sessions/requests'),
  row('edge-record', 'run', 'run/edge.json', 'browser/edge.json'),
  row('pipe', 'run', 'run/pipe', 'pipe'),
  row('home-move', 'run', 'run/home-move.json', 'run/home-move.json'),
  row('logs', 'logs', 'logs', 'logs'),
] as const;

/** An entry's name, as the table holds it. */
export type HomeEntryName = (typeof HOME_ENTRIES)[number]['name'];

const BY_NAME: ReadonlyMap<string, HomeEntry> = new Map(HOME_ENTRIES.map((entry) => [entry.name, entry]));

/** An entry found in both its places (D172 point 5's review note): neither is chosen, and the refusal names both. */
export class HomeEntryTwice extends DaorisError {
  /** The entry's new place, holding it. */
  readonly next: string;
  /** Its old place, holding it too. */
  readonly old: string;

  constructor(name: string, next: string, old: string) {
    super(HOME_TWICE.replace('{0}', name).replace('{1}', next).replace('{2}', old));
    this.name = 'HomeEntryTwice';
    this.next = next;
    this.old = old;
  }
}

const entryOf = (entry: HomeEntryName | HomeEntry): HomeEntry => (typeof entry === 'string' ? BY_NAME.get(entry)! : entry);

/**
 * A place under the home: its `/` parts, each `*` taking the next part asked. Whole where every item part was asked; the rest
 * asked is what lies under it.
 */
function expand(home: string, place: string, asked: readonly string[]): { place: string; under: string[]; whole: boolean } {
  const parts = [home];
  let at = 0;
  for (const part of place.split('/').filter(Boolean)) {
    if (part.includes('*')) {
      if (at >= asked.length) return { place: join(...parts), under: [], whole: false };
      parts.push(part.replaceAll('*', asked[at++]!));
    } else {
      parts.push(part);
    }
  }
  return { place: join(...parts), under: asked.slice(at), whole: true };
}

/** A place is there where it is on the disk, or, for an entry known by the names it holds, where it holds one. */
const there = (entry: HomeEntry, place: string): boolean =>
  entry.holds.length > 0 ? entry.holds.some((name) => existsSync(join(place, name))) : existsSync(place);

/**
 * A place holds what the entry is: one of its names, where it is known by them; else a folder that is not empty, or a file
 * that is not empty and, for JSON, parses (strict, a byte-order mark aside, as the C# twins read it).
 */
function holds(entry: HomeEntry, place: string): boolean {
  if (entry.holds.length > 0) return there(entry, place);
  try {
    const stat = statSync(place);
    if (stat.isDirectory()) return readdirSync(place).length > 0;
    if (stat.size === 0) return false;
    if (!place.toLowerCase().endsWith('.json')) return true;
    JSON.parse(readFileSync(place, 'utf8').replace(/^﻿/, ''));
    return true;
  } catch {
    return false;
  }
}

/**
 * Where an entry is in a home, by D172 point 5 and its review note (the install layout design §6.2): its new place where that
 * holds what the entry is, else its old place where that is there, else its new place, where a writer makes it. Where both
 * hold it, neither is chosen: `HomeEntryTwice` names both, for the caller to say.
 *
 * For an entry kept per item, `asked` is the item, one part for each `*` of its place, asked per item so an old one is read
 * where it is and a new one made in the new place; asked short of its parts, the folder holding them, which both places hold
 * by design, so it is never refused. What is asked beyond the item, or under an entry kept whole, is joined under the place
 * found: it is the entry's own.
 */
export function homeEntry(home: string, name: HomeEntryName | HomeEntry, ...asked: string[]): string {
  const entry = entryOf(name);
  const { folder, old: oldHome } = homeFolder(home);
  const next = expand(folder, entry.new, asked);
  const old = expand(oldHome, entry.old, asked).place;
  let place: string;
  if (samePlace(next.place, old)) {
    // One place by two spellings (an install's data entries, a root's logs) is the old one, spelled under the home as named.
    place = old;
  } else {
    const held = holds(entry, next.place);
    if (held && next.whole && holds(entry, old)) throw new HomeEntryTwice(entry.name, next.place, old);
    place = held ? next.place : there(entry, old) ? old : next.place;
    if (place === next.place) makeCategory(folder, entry);
  }
  return next.under.length === 0 ? place : join(place, ...next.under);
}

/**
 * The folder of an entry's category, made where its new place is answered and the Daoris folder is there (HOMELAYOUT3), as the
 * C# twins make it: a writer of a file the home's top held before now writes into its category's folder, which a new home has not
 * made yet. Nothing else is made: an entry's own folders are its writer's, and a home that is not there stays not there.
 */
function makeCategory(folder: string, entry: HomeEntry): void {
  const parts = entry.new.split('/').filter(Boolean);
  if (parts.length < 2) return;
  const category = join(folder, parts[0]!);
  if (existsSync(category) || !existsSync(folder)) return;
  try {
    mkdirSync(category);
  } catch {
    // The writer that follows meets the same refusal and says it.
  }
}

/**
 * Both places of an entry, asked as `homeEntry` is, without reading them: for a list of its items, which reads both (the design
 * §6.2). The new place first, then the old, once each. Only the marker that says which folder the home names is read
 * (`homeFolder`).
 */
export function homePlaces(home: string, name: HomeEntryName | HomeEntry, ...asked: string[]): string[] {
  const entry = entryOf(name);
  const { folder, old: oldHome } = homeFolder(home);
  const next = expand(folder, entry.new, asked);
  const old = expand(oldHome, entry.old, asked);
  const places = samePlace(next.place, old.place) ? [old.place] : [next.place, old.place];
  return places.map((place) => (next.under.length === 0 ? place : join(place, ...next.under)));
}

/**
 * The Daoris folder a home names, and the old home its entries were under before D172's places (D172 point 2, HOMELAYOUT3): an
 * install's folder (it holds the marker), whose old home is its `data/`; that `data/`, which names the same install and is its
 * old home, as every account that met D63 names it; and any other home, which is both. The C# twins' `DaorisHome.FolderOf`.
 */
export function homeFolder(home: string): { folder: string; old: string } {
  const trimmed = home.length > 1 ? home.replace(/[\\/]+$/, '') || home : home;
  if (existsSync(join(trimmed, INSTALL_MARKER))) return { folder: trimmed, old: join(trimmed, INSTALL_DATA) };
  const parent = dirname(trimmed);
  if (basename(trimmed).toLowerCase() === INSTALL_DATA && parent !== trimmed && existsSync(join(parent, INSTALL_MARKER))) {
    return { folder: parent, old: trimmed };
  }
  return { folder: trimmed, old: trimmed };
}

/**
 * The home a file or folder of it is in, the entry it is named (HOMELAYOUT3): undone from the entry's new place, the Daoris
 * folder, spelled as its `data/` where that folder is an install, as every install's home has been since D63; else undone from
 * its old place, the old home. For a reader that holds a file rather than its home (the harnesses' wiring, the driver's
 * choices). The C# twins' `DaorisHome.HomeOf`. An entry kept per item is not asked here.
 */
export function homeOf(path: string, name: HomeEntryName | HomeEntry): string {
  const entry = entryOf(name);
  const full = resolve(path).replace(/[\\/]+$/, '') || resolve(path);
  const folder = undone(full, entry.new);
  if (folder !== null) return existsSync(join(folder, INSTALL_MARKER)) ? join(folder, INSTALL_DATA) : folder;
  return undone(full, entry.old) ?? dirname(full);
}

/** The folder a place's parts sit under in `full`, or null where it does not end with them. */
function undone(full: string, place: string): string | null {
  let at = full;
  for (const part of place.split('/').filter(Boolean).reverse()) {
    const parent = dirname(at);
    if (!sameName(basename(at), part) || parent === at) return null;
    at = parent;
  }
  return at;
}

const WINDOWS = process.platform === 'win32';
const sameName = (a: string, b: string): boolean => (WINDOWS ? a.toLowerCase() === b.toLowerCase() : a === b);

/** Two spellings of one place: whole, and without regard to case where the file system has none. */
const samePlace = (a: string, b: string): boolean => sameName(resolve(a), resolve(b));

/** An entry under the environment's home, or null when there is no home. */
export function homeFile(env: Env, name: HomeEntryName, install: string | null = THIS_INSTALL): string | null {
  const home = daorisHome(env, install);
  return home ? homeEntry(home, name) : null;
}

/** An entry under the home, or a refusal naming the variable and the entry's place. */
export function requireHomeFile(env: Env, name: HomeEntryName, install: string | null = THIS_INSTALL): string {
  const file = homeFile(env, name, install);
  if (!file) throw new DaorisError(`${HOME_SENTENCE} (wanted: ${entryOf(name).new})`);
  return file;
}

/**
 * A home's identity (HOMEID1, the terminal RPC design §3.1): `<home>/home.json`, the entry `home-id`,
 * `{ "id": "<32 lower-case hex>", "minted": "…" }`, which every path to the folder reads alike, so a running service is told to
 * be this home's by it and never by a path. The driver's and the service's `HomeIdentity` hold the same file in C# and share no
 * code (`twins.md`).
 */
export const HOME_ID_FILE = 'home.json';

/** Whether a value is an id as `home.json` holds one. */
export function isHomeId(value: unknown): value is string {
  return typeof value === 'string' && /^[0-9a-f]{32}$/.test(value);
}

/** The home's id, or null where it has none or its file does not read. A read: discovery mints nothing. */
export function homeId(home: string): string | null {
  try {
    const file: unknown = JSON.parse(readFileSync(homeEntry(home, 'home-id'), 'utf8'));
    const id = typeof file === 'object' && file !== null ? (file as Record<string, unknown>).id : null;
    return isHomeId(id) ? id : null;
  } catch {
    return null;
  }
}

/**
 * The home's id, minted where it has none (HOMELEASE1: a lease records it): written beside, then linked into place, which
 * refuses where a file is, so of two minting at once one stands and both read it. Null where its file does not read, which is
 * never replaced, or none could be written.
 */
export function ensureHomeId(home: string): string | null {
  const path = homeEntry(home, 'home-id');
  if (existsSync(path)) return homeId(home);
  const beside = `${path}.${randomBytes(8).toString('hex')}.writing`;
  try {
    mkdirSync(dirname(path), { recursive: true });
    const minted = { id: randomBytes(16).toString('hex'), minted: new Date().toISOString() };
    writeFileSync(beside, `${JSON.stringify(minted, null, 2)}\n`, 'utf8');
    linkSync(beside, path);
  } catch {
    // Almost always: another process minted it first. Its file is the home's id, read below like any other.
  } finally {
    try { unlinkSync(beside); } catch { /* never written */ }
  }
  return homeId(home);
}
