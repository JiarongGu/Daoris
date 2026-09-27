import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import {
  addFavorite, clearHistory, commandBrowser, favoriteAddress, favoritesFile, historyFile, readFavorites,
  readHistory, removeFavorite,
} from '../src/browser.ts';
import { captureError, makeFixture } from './_fixture.ts';

/**
 * `daoris browser favorite` — the CLI's half of the favorites TWIN (BRW5, the in-app browser design
 * §3a). 🔴 The modules' `BrowserFavoritesTests.cs` carries the same tables: a case changed here is
 * changed there, in the same commit.
 */

function write(home: string, json: string): void {
  mkdirSync(dirname(favoritesFile(home)), { recursive: true });
  writeFileSync(favoritesFile(home), json, 'utf8');
}

function run(argv: string[], home: string | null): { code: number; out: string } {
  const saved = process.env.DAORIS_HOME;
  if (home === null) delete process.env.DAORIS_HOME;
  else process.env.DAORIS_HOME = home;
  const lines: string[] = [];
  try {
    const code = commandBrowser({
      root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd(),
    }) as number;
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = saved;
  }
}

// The address rule — the bar's. The same cases, answer for answer, as the C# table.
const ADDRESSES: [string, string | null][] = [
  ['site.example/board', 'https://site.example/board'],
  ['  site.example  ', 'https://site.example/'],
  ['HTTPS://Site.Example', 'https://site.example/'],
  ['https://site.example:443/', 'https://site.example/'],
  ['site.example:8443/x', 'https://site.example:8443/x'],
  ['localhost:4200', 'http://localhost:4200/'],
  ['127.0.0.1:5231/popup', 'http://127.0.0.1:5231/popup'],
  ['http://127.0.0.1:5231/popup', 'http://127.0.0.1:5231/popup'],
  ['about:blank', null],
  ['javascript:alert(1)', null],
  ['mailto:a@b.example', null],
  ['file:///C:/secrets.txt', null],
  ['https://user:pw@site.example/', null],
  ['a b', null],
  ['', null],
];

test('an address is kept in the bar\'s form or not at all', () => {
  for (const [typed, kept] of ADDRESSES) assert.equal(favoriteAddress(typed), kept, `for ${JSON.stringify(typed)}`);
});

test('no file is no favorites', () => {
  const fx = makeFixture('favorites-none');
  assert.deepEqual(readFavorites(fx.root), { favorites: [], problem: null });
  fx.cleanup();
});

test('favorites come back in order with a title or the host', () => {
  const fx = makeFixture('favorites-order');
  write(fx.root, JSON.stringify({ favorites: [
    { url: 'https://site.example/board', title: 'Board' },
    { url: 'http://localhost:4200/' },
    { url: 'https://other.example/', title: '   ' },
  ] }));
  assert.deepEqual(readFavorites(fx.root).favorites, [
    { url: 'https://site.example/board', title: 'Board' },
    { url: 'http://localhost:4200/', title: 'localhost' },
    { url: 'https://other.example/', title: 'other.example' },
  ]);
  fx.cleanup();
});

test('a row that is not a page is skipped by a reader', () => {
  const fx = makeFixture('favorites-skip');
  write(fx.root, JSON.stringify({ favorites: [
    { url: 'javascript:alert(1)' }, 'a string', { title: 'no url' }, { url: 'https://site.example/' },
  ] }));
  assert.deepEqual(readFavorites(fx.root).favorites.map((f) => f.url), ['https://site.example/']);
  fx.cleanup();
});

test('a file that cannot be read shows none and is not written over', () => {
  for (const content of ['not json', '[1, 2]', '{ "favorites": "nope" }']) {
    const fx = makeFixture('favorites-unreadable');
    write(fx.root, content);
    const read = readFavorites(fx.root);
    assert.deepEqual(read.favorites, []);
    assert.ok(read.problem, `a problem for ${content}`);
    assert.ok(captureError(() => addFavorite(fx.root, 'site.example', null)));
    assert.ok(captureError(() => removeFavorite(fx.root, 'site.example')));
    assert.equal(readFileSync(favoritesFile(fx.root), 'utf8'), content);
    fx.cleanup();
  }
});

test('adding appends and adding again keeps its place', () => {
  const fx = makeFixture('favorites-add');
  addFavorite(fx.root, 'site.example/board', 'Board');
  addFavorite(fx.root, 'localhost:4200', null);
  addFavorite(fx.root, 'https://site.example/board', null);
  addFavorite(fx.root, 'site.example/board', 'The board');
  assert.deepEqual(readFavorites(fx.root).favorites, [
    { url: 'https://site.example/board', title: 'The board' },
    { url: 'http://localhost:4200/', title: 'localhost' },
  ]);
  fx.cleanup();
});

test('an address that is not a page is refused', () => {
  const fx = makeFixture('favorites-refuse');
  const error = captureError(() => addFavorite(fx.root, 'javascript:alert(1)', null));
  assert.match(String(error?.message), /not a web page/);
  assert.equal(existsSync(favoritesFile(fx.root)), false);
  fx.cleanup();
});

test('removing is by address under the same rule', () => {
  const fx = makeFixture('favorites-remove');
  addFavorite(fx.root, 'site.example/board', 'Board');
  addFavorite(fx.root, 'localhost:4200', null);
  assert.equal(removeFavorite(fx.root, 'HTTPS://SITE.EXAMPLE/board'), true);
  assert.equal(removeFavorite(fx.root, 'never.example'), false);
  assert.deepEqual(readFavorites(fx.root).favorites.map((f) => f.url), ['http://localhost:4200/']);
  fx.cleanup();
});

test('an editor keeps what it has no field for', () => {
  const fx = makeFixture('favorites-keep');
  write(fx.root, JSON.stringify({ version: 7, favorites: [
    { url: 'https://site.example/', title: 'Site', icon: 'star.png' },
    { url: 'javascript:alert(1)', note: 'the shell wrote this' },
  ] }));
  addFavorite(fx.root, 'localhost:4200', null);
  const file = JSON.parse(readFileSync(favoritesFile(fx.root), 'utf8'));
  assert.equal(file.version, 7);
  assert.equal(file.favorites.length, 3);
  assert.equal(file.favorites[0].icon, 'star.png');
  assert.equal(file.favorites[1].note, 'the shell wrote this');
  assert.equal(file.favorites[2].url, 'http://localhost:4200/');
  fx.cleanup();
});

// BRW6: the history's reading and clearing — the same table as `BrowserHistoryTests.cs`. Recording a
// visit is the window's alone.

function writeHistory(home: string, json: string): void {
  mkdirSync(dirname(historyFile(home)), { recursive: true });
  writeFileSync(historyFile(home), json, 'utf8');
}

test('no history file is no history', () => {
  const fx = makeFixture('history-none');
  assert.deepEqual(readHistory(fx.root), { visits: [], problem: null });
  fx.cleanup();
});

test('visits come back most recent first with a title or the host', () => {
  const fx = makeFixture('history-order');
  writeHistory(fx.root, JSON.stringify({ visits: [
    { url: 'https://site.example/board', title: 'Board', last: '2026-09-28T09:00:00Z', count: 3 },
    { url: 'javascript:alert(1)', last: '2026-09-28T12:00:00Z', count: 1 },
    { url: 'http://localhost:4200/', last: '2026-09-28T10:00:00Z' },
    { url: 'https://old.example/', title: 'Old', last: 'not a time', count: 9 },
  ] }));
  assert.deepEqual(readHistory(fx.root).visits, [
    { url: 'http://localhost:4200/', title: 'localhost', last: '2026-09-28T10:00:00.000Z', count: 1 },
    { url: 'https://site.example/board', title: 'Board', last: '2026-09-28T09:00:00.000Z', count: 3 },
    { url: 'https://old.example/', title: 'Old', last: null, count: 9 },
  ]);
  fx.cleanup();
});

test('a history file that cannot be read shows none and is not cleared over', () => {
  for (const content of ['not json', '{ "visits": "nope" }']) {
    const fx = makeFixture('history-unreadable');
    writeHistory(fx.root, content);
    assert.ok(readHistory(fx.root).problem);
    assert.ok(captureError(() => clearHistory(fx.root)));
    assert.equal(readFileSync(historyFile(fx.root), 'utf8'), content);
    fx.cleanup();
  }
});

test('clearing empties the history and keeps what it has no field for', () => {
  const fx = makeFixture('history-clear');
  writeHistory(fx.root, JSON.stringify({ version: 2, visits: [{ url: 'https://site.example/', last: '2026-09-28T09:00:00Z' }] }));
  clearHistory(fx.root);
  const file = JSON.parse(readFileSync(historyFile(fx.root), 'utf8'));
  assert.equal(file.version, 2);
  assert.deepEqual(file.visits, []);
  assert.deepEqual(readHistory(fx.root).visits, []);
  fx.cleanup();
});

test('`browser history` lists the most recent and clears', () => {
  const fx = makeFixture('history-command');
  assert.match(run(['history', 'list'], fx.root).out, /no history yet/);
  writeHistory(fx.root, JSON.stringify({ visits: [
    { url: 'https://site.example/board', title: 'Board', last: '2026-09-28T09:00:00Z', count: 3 },
  ] }));
  assert.match(run(['history', 'list'], fx.root).out, /Board\s+https:\/\/site\.example\/board\s+3×/);
  assert.match(run(['history', 'clear'], fx.root).out, /forgot 1 page/);
  assert.match(run(['history', 'list'], fx.root).out, /no history yet/);
  fx.cleanup();
});

// The command itself.

test('`browser favorite` lists, adds and removes, in sentences', () => {
  const fx = makeFixture('favorites-command');
  assert.match(run(['favorite', 'list'], fx.root).out, /no favorites/);
  const added = run(['favorite', 'add', 'site.example/board', '--title', 'Board'], fx.root);
  assert.equal(added.code, 0);
  assert.match(added.out, /Board.*https:\/\/site\.example\/board/);
  assert.match(run(['favorite', 'list'], fx.root).out, /Board\s+https:\/\/site\.example\/board/);
  assert.match(run(['favorite', 'remove', 'site.example/board'], fx.root).out, /no longer a favorite/);
  assert.match(run(['favorite', 'remove', 'site.example/board'], fx.root).out, /was not a favorite/);
  fx.cleanup();
});

test('`browser favorite` with no home refuses, naming the variable (D63)', () => {
  const error = captureError(() => run(['favorite', 'list'], null));
  assert.match(String(error?.message), /DAORIS_HOME/);
});

test('`browser` knows `favorite` and `history`, and says what it takes', () => {
  const fx = makeFixture('favorites-usage');
  assert.match(String(captureError(() => run(['tabs'], fx.root))?.message), /browser favorite.*browser history/s);
  assert.match(String(captureError(() => run(['favorite', 'add'], fx.root))?.message), /needs an address/);
  fx.cleanup();
});
