import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
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

/** The file `desktop-publish` writes at an install's root, and nothing else does (the driver's `DaorisHome.InstallMarker`). */
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
 * cell. 🔴 Every new place is still the old one (HOMELAYOUT2): HOMELAYOUT3 switches the new places on with the move.
 */
export const HOME_ENTRIES = [
  row('driver', 'data', 'driver.json', 'driver.json'),
  row('harnesses', 'data', 'harnesses.json', 'harnesses.json'),
  row('accounts', 'data', 'accounts.json', 'accounts.json'),
  row('cooling', 'data', 'cooling.json', 'cooling.json'),
  row('windows', 'data', 'windows.json', 'windows.json'),
  row('reads', 'data', 'reads.json', 'reads.json'),
  row('permissions', 'data', 'permissions.json', 'permissions.json'),
  row('rule-proposals', 'data', 'proposals', 'proposals'),
  row('tools', 'data', 'tools.json', 'tools.json'),
  row('plugin-state', 'data', 'plugins.json', 'plugins.json'),
  row('plugins', 'data', 'plugins', 'plugins'),
  row('usage', 'data', 'usage.json', 'usage.json'),
  row('landings', 'data', 'landings.json', 'landings.json'),
  row('session-branches', 'data', 'session-branches.json', 'session-branches.json'),
  row('lines-moved', 'data', 'lines-moved.json', 'lines-moved.json'),
  row('registry-followed', 'data', 'registry-followed.json', 'registry-followed.json'),
  row('abandoned', 'data', 'abandoned.json', 'abandoned.json'),
  row('update', 'data', 'update.json', 'update.json'),
  row('knowledge', 'data', 'knowledge.db', 'knowledge.db'),
  row('sessions', 'data', 'sessions', 'sessions'),
  row('helpers', 'data', 'helpers', 'helpers'),
  row('handed', 'data', 'handed', 'handed'),
  row('setup', 'data', 'setup', 'setup'),
  row('workflows', 'data', 'workflows', 'workflows'),
  row('help-proposals', 'data', 'help/proposals', 'help/proposals'),
  row('opinion-gates', 'data', 'opinions/gates', 'opinions/gates'),
  row('opinion-dues', 'data', 'opinions/due.json', 'opinions/due.json'),
  row('browser-settings', 'data', 'browser/settings.json', 'browser/settings.json'),
  row('browser-favorites', 'data', 'browser/favorites.json', 'browser/favorites.json'),
  row('keys', 'profiles', 'keys.json', 'keys.json'),
  row('remotes', 'profiles', 'remotes.json', 'remotes.json'),
  row('agent-homes', 'profiles', 'harnesses/*/*', 'harnesses/*/*'),
  row('browser-engine', 'profiles', 'browser/engine', 'browser/engine'),
  row('browser-edge', 'profiles', 'browser/edge', 'browser/edge'),
  row('edge-cookies', 'profiles', 'browser/edge-session-cookies.bin', 'browser/edge-session-cookies.bin'),
  row('window', 'profiles', '', '', 'chromium', 'config'),
  row('trees', 'work', 'trees/*/*/*', 'trees/*/*/*'),
  row('help-room', 'work', 'help', 'help', 'CLAUDE.md'),
  row('intake', 'work', 'intake/*', 'intake/*'),
  row('opinion-packets', 'work', 'opinions/*', 'opinions/*'),
  row('quest-files', 'work', 'quests/*', 'quests/*'),
  row('ask-files', 'work', 'asks/*', 'asks/*'),
  row('session-files', 'work', 'sessions/*/files', 'sessions/*/files'),
  row('proof', 'work', 'proof/*', 'proof/*'),
  row('usage-read', 'work', 'usage-read', 'usage-read'),
  row('toolchain', 'cache', 'toolchain', 'toolchain'),
  row('tool-versions', 'cache', 'tools', 'tools'),
  row('bin', 'cache', 'bin', 'bin'),
  row('driver-lock', 'run', 'driver.lock', 'driver.lock'),
  row('running', 'run', 'running.json', 'running.json'),
  row('locks', 'run', 'locks', 'locks'),
  row('spawn', 'run', 'spawn', 'spawn'),
  row('starts', 'run', 'starts', 'starts'),
  row('hooks', 'run', 'hooks', 'hooks'),
  row('probing', 'run', 'harnesses/.probing', 'harnesses/.probing'),
  row('session-pids', 'run', 'sessions/*.pid', 'sessions/*.pid'),
  row('requests', 'run', 'sessions/requests', 'sessions/requests'),
  row('edge-record', 'run', 'browser/edge.json', 'browser/edge.json'),
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
  const next = expand(home, entry.new, asked);
  let place = next.place;
  if (entry.new !== entry.old) {
    const old = expand(home, entry.old, asked).place;
    const held = holds(entry, next.place);
    if (held && next.whole && holds(entry, old)) throw new HomeEntryTwice(entry.name, next.place, old);
    place = held ? next.place : there(entry, old) ? old : next.place;
  }
  return next.under.length === 0 ? place : join(place, ...next.under);
}

/**
 * Both places of an entry, asked as `homeEntry` is, without the disk: for a list of its items, which reads both (the design
 * §6.2). The new place first, then the old, once each.
 */
export function homePlaces(home: string, name: HomeEntryName | HomeEntry, ...asked: string[]): string[] {
  const entry = entryOf(name);
  const next = expand(home, entry.new, asked);
  const old = expand(home, entry.old, asked);
  const places = [next.place, old.place].map((place) => (next.under.length === 0 ? place : join(place, ...next.under)));
  return [...new Set(places)];
}

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
