import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import {
  addFavorite, commandBrowser, favoriteAddress, favoritesFile, readBrowserSettings, readFavorites,
  removeFavorite, setExtensions, setLinks, settingsFile,
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

// CHR7: the browser's settings, `<home>/browser/settings.json` — the same table as
// `BrowserSettingsTests.cs`. `daoris-browser` reads it each time it starts.

function writeSettings(home: string, json: string): void {
  mkdirSync(dirname(settingsFile(home)), { recursive: true });
  writeFileSync(settingsFile(home), json, 'utf8');
}

test('no settings file offers other software\'s extensions, as the engine does, uses Daoris\'s browser, and opens links in the system\'s', () => {
  const fx = makeFixture('settings-none');
  assert.deepEqual(readBrowserSettings(fx.root), { extensions: 'offer', browser: 'daoris', links: 'system', problem: null });
  fx.cleanup();
});

// BRW12: which browser, answer for answer, as the C# table.
const BROWSERS: [unknown, 'daoris' | 'edge'][] = [
  ['daoris', 'daoris'],
  ['edge', 'edge'],
  ['Edge', 'daoris'],
  ['chrome', 'daoris'],
  [1, 'daoris'],
];

test('the browser is daoris or edge, and anything else is Daoris\'s own', () => {
  for (const [value, expected] of BROWSERS) {
    const fx = makeFixture('settings-browser');
    writeSettings(fx.root, JSON.stringify({ browser: value, extensions: 'refuse' }));
    assert.deepEqual(readBrowserSettings(fx.root), { extensions: 'refuse', browser: expected, links: 'system', problem: null }, JSON.stringify(value));
    fx.cleanup();
  }
});

test('`browser use` says which browser and sets it, keeping the rest', () => {
  const fx = makeFixture('settings-use');
  assert.match(run(['use'], fx.root).out, /Daoris's own browser/);
  writeSettings(fx.root, JSON.stringify({ extensions: 'refuse' }));
  assert.match(run(['use', 'edge'], fx.root).out, /Edge.*Microsoft account/s);
  assert.deepEqual(readBrowserSettings(fx.root), { extensions: 'refuse', browser: 'edge', links: 'system', problem: null });
  assert.match(String(captureError(() => run(['use', 'firefox'], fx.root))?.message), /daoris.*edge/);
  fx.cleanup();
});

// BRW7: where the page's links open, answer for answer, as the C# table. `daoris` is whichever browser
// `browser` chooses: Daoris's own, or the person's Edge on Daoris's profile.
const LINKS: [unknown, 'system' | 'daoris'][] = [
  ['system', 'system'],
  ['daoris', 'daoris'],
  ['Daoris', 'system'],
  ['edge', 'system'],
  [true, 'system'],
];

test('links open in the system\'s browser or Daoris\'s, and anything else is the system\'s', () => {
  for (const [value, expected] of LINKS) {
    const fx = makeFixture('settings-links');
    writeSettings(fx.root, JSON.stringify({ links: value, browser: 'edge' }));
    assert.deepEqual(readBrowserSettings(fx.root), { extensions: 'offer', browser: 'edge', links: expected, problem: null }, JSON.stringify(value));
    fx.cleanup();
  }
});

test('setting the links keeps what it has no field for, and is refused over a file it could not read', () => {
  const fx = makeFixture('settings-links-keep');
  writeSettings(fx.root, JSON.stringify({ extensions: 'refuse', theirs: { kept: true } }));
  setLinks(fx.root, 'daoris');
  const file = JSON.parse(readFileSync(settingsFile(fx.root), 'utf8'));
  assert.deepEqual(file.theirs, { kept: true });
  assert.deepEqual(readBrowserSettings(fx.root), { extensions: 'refuse', browser: 'daoris', links: 'daoris', problem: null });
  fx.cleanup();

  const broken = makeFixture('settings-links-unreadable');
  writeSettings(broken.root, 'not json');
  assert.ok(captureError(() => setLinks(broken.root, 'daoris')));
  assert.equal(readFileSync(settingsFile(broken.root), 'utf8'), 'not json');
  broken.cleanup();
});

test('`browser links` says where links open and sets it, and refuses anything else', () => {
  const fx = makeFixture('settings-links-command');
  assert.match(run(['links'], fx.root).out, /system's browser/);
  const set = run(['links', 'daoris'], fx.root);
  assert.equal(set.code, 0);
  assert.match(set.out, /Daoris's browser.*No restart/s);
  assert.match(run(['links'], fx.root).out, /open in Daoris's browser/);
  assert.equal(readBrowserSettings(fx.root).links, 'daoris');
  assert.match(String(captureError(() => run(['links', 'chrome'], fx.root))?.message), /system.*daoris/);
  fx.cleanup();
});

// The extensions setting, answer for answer, as the C# table.
const EXTENSIONS: [unknown, 'offer' | 'refuse'][] = [
  ['offer', 'offer'],
  ['refuse', 'refuse'],
  ['REFUSE', 'offer'],
  ['block', 'offer'],
  [true, 'offer'],
  [null, 'offer'],
];

test('the extensions setting is offer or refuse, and anything else is the default', () => {
  for (const [value, expected] of EXTENSIONS) {
    const fx = makeFixture('settings-value');
    writeSettings(fx.root, JSON.stringify({ extensions: value }));
    assert.deepEqual(readBrowserSettings(fx.root), { extensions: expected, browser: 'daoris', links: 'system', problem: null }, JSON.stringify(value));
    fx.cleanup();
  }
});

test('a settings file that cannot be read shows the default and is not written over', () => {
  for (const content of ['not json', '[1]']) {
    const fx = makeFixture('settings-unreadable');
    writeSettings(fx.root, content);
    const read = readBrowserSettings(fx.root);
    assert.equal(read.extensions, 'offer');
    assert.ok(read.problem);
    assert.ok(captureError(() => setExtensions(fx.root, 'refuse')));
    assert.equal(readFileSync(settingsFile(fx.root), 'utf8'), content);
    fx.cleanup();
  }
});

test('setting the extensions keeps what it has no field for', () => {
  const fx = makeFixture('settings-keep');
  writeSettings(fx.root, JSON.stringify({ version: 2, extensions: 'offer', theirs: { kept: true } }));
  setExtensions(fx.root, 'refuse');
  const file = JSON.parse(readFileSync(settingsFile(fx.root), 'utf8'));
  assert.equal(file.version, 2);
  assert.deepEqual(file.theirs, { kept: true });
  assert.equal(readBrowserSettings(fx.root).extensions, 'refuse');
  fx.cleanup();
});

test('`browser extensions` says the setting and sets it', () => {
  const fx = makeFixture('settings-command');
  assert.match(run(['extensions'], fx.root).out, /offered/);
  assert.match(run(['extensions', 'refuse'], fx.root).out, /refused.*next time/s);
  assert.match(run(['extensions'], fx.root).out, /refused/);
  assert.match(String(captureError(() => run(['extensions', 'maybe'], fx.root))?.message), /offer.*refuse/);
  fx.cleanup();
});

/** The browser keeps its own history now (CHR5), and a person who typed the old command is told where. */
test('`browser history` is retired, and says where the history is', () => {
  const fx = makeFixture('history-retired');
  assert.match(String(captureError(() => run(['history', 'list'], fx.root))?.message), /browser's own history/);
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

test('`browser` knows `favorite` and `extensions`, and says what it takes', () => {
  const fx = makeFixture('favorites-usage');
  assert.match(String(captureError(() => run(['tabs'], fx.root))?.message), /browser favorite.*browser extensions.*browser use.*browser links/s);
  assert.match(String(captureError(() => run(['favorite', 'add'], fx.root))?.message), /needs an address/);
  fx.cleanup();
});
