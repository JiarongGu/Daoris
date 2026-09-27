import { mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { flagValue, operands } from './args.ts';
import { DaorisError, type ExitCode } from './errors.ts';
import { readJsonObject, writeJsonAtomic } from './fsx.ts';
import { HOME_SENTENCE, daorisHome } from './home.ts';
import type { CommandArgs } from './types.ts';

/**
 * The in-app browser's favorites (BRW5): `<home>/browser/favorites.json`, the person's and never a
 * session's, kept from a terminal as the window keeps them (D50: two doors).
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

// BRW6: the history — `<home>/browser/history.json`, which the window records as pages load. This side
// lists and clears it, by the reading rules `BrowserHistory.cs` holds (the design, §3b).

export const HISTORY_FILE = join('browser', 'history.json');

export interface Visit { url: string; title: string; last: string | null; count: number }

export function historyFile(home: string): string {
  return join(home, HISTORY_FILE);
}

function historyRows(home: string): { file: Record<string, unknown> | null; rows: unknown[] | null; problem: string | null } {
  const path = historyFile(home);
  const { value, problem } = readJsonObject(path);
  if (problem !== null) return { file: null, rows: null, problem };
  if (value === null) return { file: null, rows: null, problem: null };
  if (!('visits' in value)) return { file: value, rows: [], problem: null };
  if (!Array.isArray(value.visits)) return { file: null, rows: null, problem: `${path}'s visits is not a list` };
  return { file: value, rows: value.visits, problem: null };
}

/** When it was last visited, as an ISO time, or null when the row does not say in one — the earliest. */
function lastOf(row: Record<string, unknown>): string | null {
  const last = text(row, 'last');
  if (last === null) return null;
  const at = new Date(last);
  return Number.isNaN(at.getTime()) ? null : at.toISOString();
}

/** How often, or once when the row does not say in a positive whole number. */
function countOf(row: Record<string, unknown>): number {
  return Number.isInteger(row.count) && (row.count as number) > 0 ? row.count as number : 1;
}

/** The history, most recent first; a row that is not a page skipped, as a reader skips it in the shell. */
export function readHistory(home: string): { visits: Visit[]; problem: string | null } {
  const { rows, problem } = historyRows(home);
  const visits: Visit[] = [];
  for (const row of rows ?? []) {
    if (!isRow(row)) continue;
    const url = text(row, 'url');
    const page = url === null ? null : favoriteAddress(url);
    if (page) visits.push({ url: page, title: titleOf(row, page), last: lastOf(row), count: countOf(row) });
  }

  const time = (visit: Visit) => (visit.last === null ? -Infinity : Date.parse(visit.last));
  visits.sort((a, b) => time(b) - time(a));
  return { visits, problem };
}

/** Forget every page, keeping what an editor has no field for. Refused over a file it could not read. */
export function clearHistory(home: string): number {
  const { file, rows, problem } = historyRows(home);
  if (problem !== null) {
    throw new DaorisError(`${problem}. Fix it, or delete it to start from nothing — `
      + 'the history will not write over a file it could not read.');
  }
  if (file === null) return 0;

  const forgot = (rows ?? []).length;
  writeJsonAtomic(historyFile(home), { ...file, visits: [] });
  return forgot;
}

/** The home, or the refusal every management verb gives without one (D63). */
function requireHome(): string {
  const home = daorisHome();
  if (!home) throw new DaorisError(`${HOME_SENTENCE} (wanted: ${FAVORITES_FILE})`);
  return home;
}

const USAGE = '`daoris browser favorite list`, `daoris browser favorite add <address> [--title T]`, '
  + '`daoris browser favorite remove <address>`, `daoris browser history list [--limit N]` '
  + 'or `daoris browser history clear`';

/** `browser history list|clear`: what the window recorded, most recent first, or forget it all. */
function commandHistory(verb: string, argv: string[], write: (line: string) => void): ExitCode {
  const home = requireHome();
  switch (verb) {
    case 'list': {
      const { visits, problem } = readHistory(home);
      if (problem) {
        write(`daoris: ⚠ ${problem}`);
        return 1;
      }
      if (visits.length === 0) {
        write('daoris: no history yet — the in-app browser records the pages it loads.');
        return 0;
      }
      const limit = Number(flagValue(argv, '--limit') ?? 20);
      const shown = visits.slice(0, Number.isInteger(limit) && limit > 0 ? limit : 20);
      write(`daoris: ${historyFile(home)} — the ${shown.length} most recent of ${visits.length}`);
      const width = Math.max(...shown.map((v) => v.title.length));
      for (const visit of shown) {
        write(`  ${visit.title.padEnd(width)}  ${visit.url}  ${visit.count}×${visit.last ? `  ${visit.last.slice(0, 10)}` : ''}`);
      }
      return 0;
    }

    case 'clear': {
      const forgot = clearHistory(home);
      write(`daoris: forgot ${forgot} ${forgot === 1 ? 'page' : 'pages'}. The window forgets its own history `
        + 'the next time it is cleared from there.');
      return 0;
    }

    default:
      throw new DaorisError(`\`browser history\` does not know \`${verb}\` — it takes ${USAGE}.`);
  }
}

/**
 * The in-app browser from a terminal: its favorites and its history, the same files the window keeps
 * (D50). Management class: it edits files under the home and talks to nothing.
 */
export function commandBrowser({ argv, write }: CommandArgs): ExitCode {
  const [area, verb = 'list', address] = operands(argv, new Set(['--title', '--limit']));
  if (area === 'history') return commandHistory(verb, argv, write);
  if (area !== 'favorite') throw new DaorisError(`\`browser\` takes ${USAGE}.`);

  const home = requireHome();
  switch (verb) {
    case 'list': {
      const { favorites, problem } = readFavorites(home);
      if (problem) {
        write(`daoris: ⚠ ${problem}`);
        return 1;
      }
      if (favorites.length === 0) {
        write('daoris: no favorites yet — the star in the in-app browser\'s bar keeps a page,');
        write('  or `daoris browser favorite add <address>`.');
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
