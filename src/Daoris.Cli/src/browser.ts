import { mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { flagValue, operands } from './args.ts';
import { DaorisError, type ExitCode } from './errors.ts';
import { readJsonObject, writeJsonAtomic } from './fsx.ts';
import { HOME_SENTENCE, daorisHome } from './home.ts';
import type { CommandArgs } from './types.ts';

/**
 * The in-app browser's favorites (BRW5, CHR5): `<home>/browser/favorites.json`, the person's and never a
 * session's, kept from a terminal and from the Settings screen (D50: two doors). `daoris-browser` puts
 * them in a *Daoris* folder on its bookmarks bar each time it starts.
 *
 * 🔴 A TWIN file (the in-app browser design, §3a): the desktop modules' `BrowserFavorites.cs` reads and
 * edits it with its own code, and each side carries the same test table (`browser.test.ts` here,
 * `BrowserFavoritesTests.cs` there). A rule changed here is changed there, in the same commit.
 */

export const FAVORITES_FILE = join('browser', 'favorites.json');

export interface Favorite { url: string; title: string }

export function favoritesFile(home: string): string {
  return join(home, FAVORITES_FILE);
}

/** `host:port`, `host:port/path` — a colon that is a port, not a scheme. `InAppBrowser.LooksLikeHostAndPort`'s twin. */
function looksLikeHostAndPort(text: string): boolean {
  const rest = text.slice(text.indexOf(':') + 1);
  const digits = /^\d*/.exec(rest)![0].length;
  return digits > 0 && (digits === rest.length || rest[digits] === '/');
}

/**
 * A page's address as a favorite keeps it — the bar's rule, `InAppBrowser.Address` in the shell — or
 * null when it is no web page. A host with no scheme is HTTPS unless it is this machine; only `http`
 * and `https`, with a host and no credentials; `about:blank` is no page to keep.
 */
export function favoriteAddress(typed: string): string | null {
  const text = typed.trim();
  if (text.length === 0 || text.includes(' ')) return null;
  if (text.toLowerCase() === 'about:blank') return null;

  let candidate = text;
  if (!text.includes('://')) {
    // No scheme: a web address, if anything. `javascript:`, `mailto:` and the like all carry one.
    if (text.includes(':') && !looksLikeHostAndPort(text)) return null;
    const local = /^localhost/i.test(text) || text.startsWith('127.0.0.1') || text.startsWith('[::1]');
    candidate = (local ? 'http://' : 'https://') + text;
  }

  let url: URL;
  try {
    url = new URL(candidate);
  } catch {
    return null;
  }

  if (url.protocol !== 'http:' && url.protocol !== 'https:') return null;
  if (url.username || url.password || !url.hostname) return null;
  return url.href;
}

function text(row: Record<string, unknown>, name: string): string | null {
  return typeof row[name] === 'string' ? row[name] : null;
}

function titleOf(row: Record<string, unknown>, page: string): string {
  const title = text(row, 'title');
  return title && title.trim() ? title.trim() : new URL(page).hostname;
}

const isRow = (row: unknown): row is Record<string, unknown> =>
  row !== null && typeof row === 'object' && !Array.isArray(row);

/** The rows, or why a file that was there gave none. */
function rowsOf(home: string): { file: Record<string, unknown> | null; rows: unknown[] | null; problem: string | null } {
  const path = favoritesFile(home);
  const { value, problem } = readJsonObject(path);
  if (problem !== null) return { file: null, rows: null, problem };
  if (value === null) return { file: null, rows: null, problem: null };
  if (!('favorites' in value)) return { file: value, rows: [], problem: null };
  if (!Array.isArray(value.favorites)) return { file: null, rows: null, problem: `${path}'s favorites is not a list` };
  return { file: value, rows: value.favorites, problem: null };
}

export function readFavorites(home: string): { favorites: Favorite[]; problem: string | null } {
  const { rows, problem } = rowsOf(home);
  const favorites: Favorite[] = [];
  for (const row of rows ?? []) {
    if (!isRow(row)) continue;
    const url = text(row, 'url');
    const page = url === null ? null : favoriteAddress(url);
    if (page) favorites.push({ url: page, title: titleOf(row, page) });
  }

  return { favorites, problem };
}

/** The file as an object to edit — refused when there is one this could not read. */
function editable(home: string): { file: Record<string, unknown>; rows: unknown[] } {
  const { file, rows, problem } = rowsOf(home);
  if (problem !== null) {
    throw new DaorisError(`${problem}. Fix it, or delete it to start from nothing — `
      + 'favorites will not write over a file they could not read.');
  }

  if (file === null) {
    const fresh: unknown[] = [];
    return { file: { favorites: fresh }, rows: fresh };
  }

  file.favorites = rows ?? [];
  return { file, rows: file.favorites as unknown[] };
}

function save(home: string, file: Record<string, unknown>): void {
  mkdirSync(dirname(favoritesFile(home)), { recursive: true });
  writeJsonAtomic(favoritesFile(home), file);
}

const kept = (row: unknown, page: string): row is Record<string, unknown> => {
  if (!isRow(row)) return false;
  const url = text(row, 'url');
  return url !== null && favoriteAddress(url) === page;
};

/**
 * Keep a page: appended, or, when it is kept already, left in its place and given `title` if one was
 * given. An editor keeps what it has no field for, on the file and on each row.
 */
export function addFavorite(home: string, typed: string, title: string | null): Favorite {
  const page = favoriteAddress(typed);
  if (!page) throw new DaorisError(`\`${typed}\` is not a web page, so it cannot be a favorite.`);

  const { file, rows } = editable(home);
  let row = rows.find((candidate) => kept(candidate, page)) as Record<string, unknown> | undefined;
  if (!row) {
    row = { url: page };
    rows.push(row);
  }

  if (title && title.trim()) row.title = title.trim();
  save(home, file);
  return { url: page, title: titleOf(row, page) };
}

/** Stop keeping a page, by address under the same rule. False when it was not kept. */
export function removeFavorite(home: string, typed: string): boolean {
  const page = favoriteAddress(typed);
  if (!page) return false;

  const { file, rows } = editable(home);
  const left = rows.filter((row) => !kept(row, page));
  if (left.length === rows.length) return false;
  file.favorites = left;
  save(home, file);
  return true;
}

// CHR7: the browser's settings — `<home>/browser/settings.json`, which `daoris-browser` reads each time
// it starts, and whose `links` the page reads at each click (BRW7). 🔴 A TWIN file as the favorites
// are: `BrowserSettings.cs` in the desktop modules reads and edits it with its own code, and
// `BrowserSettingsTests.cs` carries this side's table.

export const SETTINGS_FILE = join('browser', 'settings.json');

/**
 * What the browser does with extensions other software registered for Chrome on this machine: `offer`
 * them for the person's approval, as the engine does, or `refuse` them before it starts.
 */
export type ExtensionsSetting = 'offer' | 'refuse';

/**
 * Which browser a session drives and the person opens (BRW12, D84): Daoris's own, the engine it ships,
 * or the person's Edge on a profile of Daoris's.
 */
export type BrowserChoice = 'daoris' | 'edge';

/**
 * Where a link on Daoris's page opens (BRW7): in the `system`'s browser, as a link always has, or in
 * `daoris`'s browser — whichever `browser` chooses, Daoris's own or the person's Edge. The page reads it,
 * not `daoris-browser`, so a change needs no restart of either.
 */
export type LinksSetting = 'system' | 'daoris';

export function settingsFile(home: string): string {
  return join(home, SETTINGS_FILE);
}

/** The settings, or the defaults with why a file that was there gave none. */
export function readBrowserSettings(home: string): {
  extensions: ExtensionsSetting; browser: BrowserChoice; links: LinksSetting; problem: string | null;
} {
  const { value, problem } = readJsonObject(settingsFile(home));
  const extensions = value?.extensions === 'refuse' ? 'refuse' : 'offer';
  const browser = value?.browser === 'edge' ? 'edge' : 'daoris';
  const links = value?.links === 'daoris' ? 'daoris' : 'system';
  return { extensions, browser, links, problem };
}

/** Set one field, keeping what an editor has no field for. Refused over a file it could not read. */
function setField(home: string, field: 'extensions' | 'browser' | 'links', choice: string): void {
  const { value, problem } = readJsonObject(settingsFile(home));
  if (problem !== null) {
    throw new DaorisError(`${problem}. Fix it, or delete it to start from the defaults — `
      + 'the browser\'s settings will not write over a file they could not read.');
  }

  mkdirSync(dirname(settingsFile(home)), { recursive: true });
  writeJsonAtomic(settingsFile(home), { ...(value ?? {}), [field]: choice });
}

export function setExtensions(home: string, extensions: ExtensionsSetting): void {
  setField(home, 'extensions', extensions);
}

export function setBrowser(home: string, browser: BrowserChoice): void {
  setField(home, 'browser', browser);
}

export function setLinks(home: string, links: LinksSetting): void {
  setField(home, 'links', links);
}

/** The home, or the refusal every management verb gives without one (D63). */
function requireHome(): string {
  const home = daorisHome();
  if (!home) throw new DaorisError(`${HOME_SENTENCE} (wanted: ${FAVORITES_FILE})`);
  return home;
}

const USAGE = '`daoris browser favorite list`, `daoris browser favorite add <address> [--title T]`, '
  + '`daoris browser favorite remove <address>`, `daoris browser extensions [offer|refuse]`, '
  + '`daoris browser use [daoris|edge]`, or `daoris browser links [system|daoris]`';

/** `browser links [system|daoris]`: say where the page's links open, or choose it (BRW7). */
function commandLinks(value: string | undefined, write: (line: string) => void): ExitCode {
  const home = requireHome();
  if (value === undefined) {
    const { links, problem } = readBrowserSettings(home);
    if (problem) write(`daoris: ⚠ ${problem}; the default holds.`);
    write(links === 'daoris'
      ? 'daoris: links on Daoris\'s page open in Daoris\'s browser — its own, or your Edge where `browser use edge` chose it.'
      : 'daoris: links on Daoris\'s page open in the system\'s browser.');
    return problem ? 1 : 0;
  }

  if (value !== 'system' && value !== 'daoris') {
    throw new DaorisError(`\`browser links\` takes \`system\` or \`daoris\`, not \`${value}\`.`);
  }

  setLinks(home, value);
  write(value === 'daoris'
    ? 'daoris: links on Daoris\'s page open in Daoris\'s browser. No restart: an open window takes it up when '
      + 'it is next brought to the front. A sign-in link still opens in the system\'s browser, where your own '
      + 'sign-ins are.'
    : 'daoris: links on Daoris\'s page open in the system\'s browser. No restart: an open window takes it up '
      + 'when it is next brought to the front.');
  return 0;
}

/** `browser use [daoris|edge]`: say which browser, or choose it for the next time one is opened. */
function commandUse(value: string | undefined, write: (line: string) => void): ExitCode {
  const home = requireHome();
  if (value === undefined) {
    const { browser, problem } = readBrowserSettings(home);
    if (problem) write(`daoris: ⚠ ${problem}; the default holds.`);
    write(browser === 'edge'
      ? 'daoris: sessions and you use your Edge, on a profile of Daoris\'s under the home.'
      : 'daoris: sessions and you use Daoris\'s own browser, the engine it ships.');
    return problem ? 1 : 0;
  }

  if (value !== 'daoris' && value !== 'edge') {
    throw new DaorisError(`\`browser use\` takes \`daoris\` or \`edge\`, not \`${value}\`.`);
  }

  setBrowser(home, value);
  write(value === 'edge'
    ? 'daoris: Edge, from the next time the browser is opened. Edge signs its profile in to your Microsoft '
      + 'account on its own, and its sync and extensions are yours to turn on. Your default Edge profile '
      + 'cannot be driven: Chromium refuses a debug port there, so Daoris gives Edge a profile of its own.'
    : 'daoris: Daoris\'s own browser, from the next time the browser is opened.');
  return 0;
}

/** `browser extensions [offer|refuse]`: say the setting, or set it for the browser's next start. */
function commandExtensions(value: string | undefined, write: (line: string) => void): ExitCode {
  const home = requireHome();
  if (value === undefined) {
    const { extensions, problem } = readBrowserSettings(home);
    if (problem) write(`daoris: ⚠ ${problem}; the default holds.`);
    write(extensions === 'refuse'
      ? 'daoris: extensions other software registered for Chrome are refused by Daoris\'s browser.'
      : 'daoris: extensions other software registered for Chrome are offered by Daoris\'s browser, '
        + 'each for your approval.');
    return problem ? 1 : 0;
  }

  if (value !== 'offer' && value !== 'refuse') {
    throw new DaorisError(`\`browser extensions\` takes \`offer\` or \`refuse\`, not \`${value}\`.`);
  }

  setExtensions(home, value);
  write(`daoris: extensions other software registered for Chrome will be ${value === 'refuse' ? 'refused' : 'offered'} `
    + 'the next time Daoris\'s browser starts.');
  return 0;
}

/**
 * The in-app browser from a terminal: its favorites, which it shows in a *Daoris* folder on its
 * bookmarks bar, and its settings — the same files the Settings screen keeps (D50). Management class:
 * it edits files under the home and talks to nothing.
 */
export function commandBrowser({ argv, write }: CommandArgs): ExitCode {
  const [area, verb, address] = operands(argv, new Set(['--title']));
  if (area === 'extensions') return commandExtensions(verb, write);
  if (area === 'use') return commandUse(verb, write);
  if (area === 'links') return commandLinks(verb, write);
  if (area === 'history') {
    throw new DaorisError('`browser history` is retired: Daoris\'s browser keeps the browser\'s own history '
      + 'now, on its History page, where it is cleared as well.');
  }
  if (area !== 'favorite') throw new DaorisError(`\`browser\` takes ${USAGE}.`);

  const home = requireHome();
  switch (verb ?? 'list') {
    case 'list': {
      const { favorites, problem } = readFavorites(home);
      if (problem) {
        write(`daoris: ⚠ ${problem}`);
        return 1;
      }
      if (favorites.length === 0) {
        write('daoris: no favorites yet — `daoris browser favorite add <address>` keeps a page, and');
        write('  Daoris\'s browser shows them in a Daoris folder on its bookmarks bar.');
        return 0;
      }
      write(`daoris: ${favoritesFile(home)}`);
      const width = Math.max(...favorites.map((f) => f.title.length));
      for (const favorite of favorites) write(`  ${favorite.title.padEnd(width)}  ${favorite.url}`);
      return 0;
    }

    case 'add': {
      if (!address) throw new DaorisError(`\`browser favorite add\` needs an address — ${USAGE}.`);
      const favorite = addFavorite(home, address, flagValue(argv, '--title') ?? null);
      write(`daoris: ${favorite.title} (${favorite.url}) is a favorite.`);
      return 0;
    }

    case 'remove': {
      if (!address) throw new DaorisError(`\`browser favorite remove\` needs an address — ${USAGE}.`);
      const page = favoriteAddress(address) ?? address;
      write(removeFavorite(home, address)
        ? `daoris: ${page} is no longer a favorite.`
        : `daoris: ${page} was not a favorite; nothing changed.`);
      return 0;
    }

    default:
      throw new DaorisError(`\`browser favorite\` does not know \`${verb}\` — it takes ${USAGE}.`);
  }
}
