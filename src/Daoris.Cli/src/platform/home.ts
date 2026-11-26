import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { DaorisError } from './errors.ts';

/**
 * Where every machine-local file Daoris owns lives: one home, named by `DAORIS_HOME`, and **no
 * default under the user profile**. The application's own folder is the home — the installed desktop
 * sets the variable for itself and everything it spawns — and this CLI reads the same variable from
 * the user's environment, or, run from an install with none, that install's own `data`. Absent
 * both, there is no home: the management class refuses with one sentence rather than writing
 * somewhere nobody pointed it; doctrine commands need no home.
 *
 * One of three twins — the driver's and the service's `DaorisHome` hold the same contract in C# and
 * share no code, the way the remotes map's three copies do. The environment is the contract.
 *
 * Each file and folder under the home is an entry of the table `HOME_ENTRIES`, and a reader asks for the
 * entry by name (`homeEntry`), never for a path it joined itself; `home-census.test.ts` fails a join by hand. The three
 * twins hold the same table, cell for cell (`home.test.ts`).
 */

export const HOME_VARIABLE = 'DAORIS_HOME';

/** The one sentence every refusal for a missing home carries. */
export const HOME_SENTENCE =
  'no Daoris home: set DAORIS_HOME to the application\'s data folder (the `data` directory beside '
  + 'Daoris.exe), or name the file\'s own variable. Daoris keeps nothing under the user profile.';

type Env = Record<string, string | undefined>;

/** The file `desktop-publish` writes at an install's root, and nothing else does (the driver's and the service's `DaorisHome.InstallMarker`). */
export const INSTALL_MARKER = 'INSTALLED.md';

/** The folder an install's programs run from, and its home beside it. */
export const INSTALL_APP = 'app';
export const INSTALL_DATA = 'data';

/**
 * The install a folder sits in: the folder above the `app/` it is in, however deep (the bundled CLI is
 * `app/cli/daoris.mjs`), where that folder holds the marker. Null for a checkout, which is no install.
 */
export function installOf(from: string): string | null {
  for (let directory = resolve(from); ;) {
    const parent = dirname(directory);
    if (parent === directory) return null;
    if (basename(directory).toLowerCase() === INSTALL_APP && existsSync(join(parent, INSTALL_MARKER))) return parent;
    directory = parent;
  }
}

export const THIS_INSTALL: string | null = installOf(dirname(fileURLToPath(import.meta.url)));

/**
 * The home, or null when there is none. Blank is none. The variable still wins (the dev loop, a rehearsal, a second home);
 * with none, a `daoris` the install carries finds the install's own `data` folder, as the driver host does,
 * since the install's launchers set nothing.
 */
export function daorisHome(env: Env = process.env, install: string | null = THIS_INSTALL): string | null {
  const home = env[HOME_VARIABLE]?.trim();
  if (home) return home;
  return install ? join(install, INSTALL_DATA) : null;
}

/** A top folder of the Daoris folder: the category an entry is in. */
export type HomeCategory = 'data' | 'profiles' | 'work' | 'cache' | 'run' | 'logs';

/**
 * An entry of the home table: its name, its category, its place under the Daoris folder,
 * `/`-separated, a `*` for each part of an item where the entry is kept per item, and the names that make
 * a place there where the entry is known by them (none: the place itself).
 */
export interface HomeEntry {
  readonly name: string;
  readonly category: HomeCategory;
  readonly new: string;
  readonly holds: readonly string[];
}

const row = <const N extends string>(name: N, category: HomeCategory, next: string, ...holds: string[]) =>
  ({ name, category, new: next, holds }) satisfies HomeEntry;

/**
 * The home table, in order: the driver's and the service's `HomeEntry` hold the same rows, which `home.test.ts` holds cell for
 * cell. 🔴 Each place is under the top folder its category names, under the Daoris folder, so an install's `data/` entries
 * stay where they are.
 */
export const HOME_ENTRIES = [
  row('driver', 'data', 'data/driver.json'),
  row('harnesses', 'data', 'data/harnesses.json'),
  row('accounts', 'data', 'data/accounts.json'),
  row('cooling', 'data', 'data/cooling.json'),
  row('windows', 'data', 'data/windows.json'),
  row('reads', 'data', 'data/reads.json'),
  row('permissions', 'data', 'data/permissions.json'),
  row('files', 'data', 'data/files.json'),
  row('files-found', 'data', 'data/files-found.json'),
  row('files-asks', 'data', 'data/files-asks.json'),
  row('rule-proposals', 'data', 'data/proposals'),
  row('tools', 'data', 'data/tools.json'),
  row('plugin-state', 'data', 'data/plugins.json'),
  row('plugins', 'data', 'data/plugins'),
  row('plugin-approvals', 'data', 'data/plugin-approvals.json'),
  row('usage', 'data', 'data/usage.json'),
  row('landings', 'data', 'data/landings.json'),
  row('session-branches', 'data', 'data/session-branches.json'),
  row('lines-moved', 'data', 'data/lines-moved.json'),
  row('overlaps-told', 'data', 'data/overlaps-told.json'),
  row('registry-followed', 'data', 'data/registry-followed.json'),
  row('abandoned', 'data', 'data/abandoned.json'),
  row('update', 'data', 'data/update.json'),
  row('knowledge', 'data', 'data/knowledge.db'),
  row('desktop-db', 'data', 'data/desktop.db'),
  row('memory', 'data', 'data/memory.db'),
  row('sessions', 'data', 'data/sessions'),
  row('helpers', 'data', 'data/helpers'),
  row('partners', 'data', 'data/partners'),
  row('checks', 'data', 'data/checks'),
  row('handed', 'data', 'data/handed'),
  row('setup', 'data', 'data/setup'),
  row('workflows', 'data', 'data/workflows'),
  row('configured-access', 'data', 'data/configured-access.json'),
  row('opinion-gates', 'data', 'data/opinions/gates'),
  row('opinion-dues', 'data', 'data/opinions/due.json'),
  row('browser-settings', 'data', 'data/browser/settings.json'),
  row('browser-favorites', 'data', 'data/browser/favorites.json'),
  row('home-id', 'data', 'data/home.json'),
  row('brainstorms', 'data', 'data/brainstorms'),
  row('usage-history', 'data', 'data/usage-history'),
  row('keeping', 'data', 'data/keeping.json'),
  row('notices', 'data', 'data/notices.json'),
  row('check-budgets', 'data', 'data/check-budgets.json'),
  row('git-identities', 'data', 'data/git-identities.json'),
  // Named here, never read by the CLI.
  row('agent-offers', 'data', 'data/agent-offers.json'),
  // A terminal's presses waiting for the person, kept across a restart; named here,
  // never read by the CLI.
  row('pipe-waiting', 'data', 'data/pipe-waiting'),
  // An act's file step's bytes, one folder per act; named here, never read by the CLI.
  row('act-files', 'data', 'data/act-files/*'),
  // A place's apply, written before its first effect; named here, never read by the CLI.
  row('place-receipts', 'data', 'data/place-receipts'),
  // The steward desk's requests kept across a restart; named here, never read by the CLI.
  row('steward-desk', 'data', 'data/steward-desk.json'),
  // The newest build that has run on the home, which the launcher refuses an older one by; named here, never read by the CLI.
  row('last-build', 'data', 'data/last-build.json'),
  row('terminal-acts', 'data', 'data/terminal-acts.json'),
  row('ux-trail', 'data', 'data/ux-trail'),
  row('priority', 'data', 'data/priority.json'),
  row('keys', 'profiles', 'profiles/keys.json'),
  row('remotes', 'profiles', 'profiles/remotes.json'),
  row('agent-homes', 'profiles', 'profiles/agents/*/*'),
  row('browser-engine', 'profiles', 'profiles/browser/engine'),
  row('browser-edge', 'profiles', 'profiles/browser/edge'),
  row('edge-cookies', 'profiles', 'profiles/browser/edge-session-cookies.bin'),
  row('git-keys', 'profiles', 'profiles/git/keys'),
  row('git-tokens', 'profiles', 'profiles/git/tokens'),
  row('plugin-private', 'profiles', 'profiles/plugins'),
  row('git-ssh-config', 'profiles', 'profiles/git/ssh_config'),
  row('git-known-hosts', 'profiles', 'profiles/git/known_hosts'),
  row('git-config', 'profiles', 'profiles/git/global.gitconfig'),
  row('window', 'profiles', 'profiles/window', 'chromium', 'config'),
  // The keys of a terminal's sent presses, kept as long as a press's card lives; named
  // here, never read by the CLI.
  row('pipe-sent', 'profiles', 'profiles/pipe-sent'),
  // Named here, never read by the CLI.
  row('deps-secret', 'profiles', 'profiles/deps-secret'),
  row('trees', 'work', 'work/trees/*/*/*'),
  row('help-room', 'work', 'work/rooms/help', 'CLAUDE.md'),
  row('help-rooms', 'work', 'work/rooms/help-*', 'CLAUDE.md'),
  row('intake', 'work', 'work/rooms/intake/*'),
  row('opinion-packets', 'work', 'work/opinions/*'),
  row('quest-files', 'work', 'work/files/quests/*'),
  row('ask-files', 'work', 'work/files/asks/*'),
  row('session-files', 'work', 'work/files/sessions/*'),
  row('proof', 'work', 'work/files/proof/*'),
  row('usage-read', 'work', 'work/usage-read'),
  row('brainstorm-rooms', 'work', 'work/brainstorm-rooms/*'),
  // Named here, never read by the CLI.
  row('role-rooms', 'work', 'work/rooms/roles/*'),
  row('deps', 'work', 'work/deps/*'),
  row('tree-files', 'work', 'work/files/trees/*'),
  row('places', 'work', 'work/places/*/*'),
  row('plans', 'work', 'work/plans/*'),
  row('evidence', 'work', 'work/evidence/*'),
  row('toolchain', 'cache', 'cache/toolchain'),
  row('tool-versions', 'cache', 'cache/tools'),
  row('bin', 'cache', 'cache/bin'),
  row('home-lease', 'run', 'run/home.lease'),
  row('running', 'run', 'run/running.json'),
  row('locks', 'run', 'run/locks'),
  row('spawn', 'run', 'run/spawn'),
  row('starts', 'run', 'run/starts'),
  row('hooks', 'run', 'run/hooks'),
  row('probing', 'run', 'run/probing'),
  row('session-pids', 'run', 'run/sessions/*.pid'),
  row('requests', 'run', 'run/requests'),
  row('edge-record', 'run', 'run/edge.json'),
  row('stop-ask', 'run', 'run/stop-ask.json'),
  row('pipe', 'run', 'run/pipe'),
  row('git-held', 'run', 'run/git/held.json'),
  row('git-grants', 'run', 'run/git/grants'),
  row('git-used', 'run', 'run/git/used'),
  row('git-asks', 'run', 'run/git/asks'),
  row('git-checkouts', 'run', 'run/git/checkouts.json'),
  row('git-library', 'run', 'run/git/library.json'),
  row('git-library-leases', 'run', 'run/git/library-leases'),
  row('git-home', 'run', 'run/git/home'),
  row('git-sessions', 'run', 'run/git/sessions'),
  row('clock-mark', 'run', 'run/clock.mark'),
  row('logs', 'logs', 'logs'),
] as const;

/** An entry's name, as the table holds it. */
export type HomeEntryName = (typeof HOME_ENTRIES)[number]['name'];

const BY_NAME: ReadonlyMap<string, HomeEntry> = new Map(HOME_ENTRIES.map((entry) => [entry.name, entry]));

const entryOf = (entry: HomeEntryName | HomeEntry): HomeEntry => (typeof entry === 'string' ? BY_NAME.get(entry)! : entry);

/** A place under the home: its `/` parts, each `*` taking the next part asked; the rest asked is what lies under it. */
function expand(home: string, place: string, asked: readonly string[]): { place: string; under: string[] } {
  const parts = [home];
  let at = 0;
  for (const part of place.split('/').filter(Boolean)) {
    if (part.includes('*')) {
      if (at >= asked.length) return { place: join(...parts), under: [] };
      parts.push(part.replaceAll('*', asked[at++]!));
    } else {
      parts.push(part);
    }
  }
  return { place: join(...parts), under: asked.slice(at) };
}

/**
 * Where an entry is in a home: its place under the Daoris folder (`homeFolder`).
 *
 * For an entry kept per item, `asked` is the item, one part for each `*` of its place; asked short of its parts, the folder
 * holding them. What is asked beyond the item, or under an entry kept whole, is joined under the place: it is the entry's own.
 */
export function homeEntry(home: string, name: HomeEntryName | HomeEntry, ...asked: string[]): string {
  const entry = entryOf(name);
  const folder = homeFolder(home);
  const { place, under } = expand(folder, entry.new, asked);
  makeCategory(folder, entry);
  return under.length === 0 ? place : join(place, ...under);
}

/**
 * The folder of an entry's category, made where its place is answered and the Daoris folder is there, as the
 * C# twins make it: a writer of a file the home's top holds writes into its category's folder, which a new home has not
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
 * The Daoris folder a home names: an install's folder (it holds the marker); the `data/` of an install, which names the same
 * install; and any other home, which is the folder itself. The C# twins' `DaorisHome.FolderOf`.
 */
export function homeFolder(home: string): string {
  const trimmed = home.length > 1 ? home.replace(/[\\/]+$/, '') || home : home;
  if (existsSync(join(trimmed, INSTALL_MARKER))) return trimmed;
  const parent = dirname(trimmed);
  if (basename(trimmed).toLowerCase() === INSTALL_DATA && parent !== trimmed && existsSync(join(parent, INSTALL_MARKER))) {
    return parent;
  }
  return trimmed;
}

/**
 * The home a file or folder of it is in, the entry it is named: undone from the entry's place, the Daoris
 * folder, spelled as its `data/` where that folder is an install, as every install's home is; a path the entry does not end,
 * its own folder. For a reader that holds a file rather than its home (the harnesses' wiring, the driver's
 * choices). The C# twins' `DaorisHome.HomeOf`. An entry kept per item is not asked here.
 */
export function homeOf(path: string, name: HomeEntryName | HomeEntry): string {
  const entry = entryOf(name);
  const full = resolve(path).replace(/[\\/]+$/, '') || resolve(path);
  const folder = undone(full, entry.new);
  if (folder !== null) return existsSync(join(folder, INSTALL_MARKER)) ? join(folder, INSTALL_DATA) : folder;
  return dirname(full);
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
 * A home's identity: `<home>/home.json`, the entry `home-id`,
 * `{ "id": "<32 lower-case hex>", "minted": "…" }`, which every path to the folder reads alike, so a running service is told to
 * be this home's by it and never by a path. The driver's and the service's `HomeIdentity` hold the same file in C# and share no
 * code (`twins.md`).
 */
export const HOME_ID_FILE = 'home.json';

/** Whether a value is an id as `home.json` holds one. */
export function isHomeId(value: unknown): value is string {
  return typeof value === 'string' && /^[0-9a-f]{32}$/.test(value);
}

/**
 * The home's id, or null where it has none or its file does not read. A read: discovery mints nothing, and nothing here mints one; the driver's `HomeIdentity.Ensure` does.
 */
export function homeId(home: string): string | null {
  try {
    const file: unknown = JSON.parse(readFileSync(homeEntry(home, 'home-id'), 'utf8'));
    const id = typeof file === 'object' && file !== null ? (file as Record<string, unknown>).id : null;
    return isHomeId(id) ? id : null;
  } catch {
    return null;
  }
}
