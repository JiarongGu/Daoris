import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import {
  daorisHome, ensureHomeId, HOME_ENTRIES, HOME_ID_FILE, HOME_TWICE, homeEntry, HomeEntryTwice, homeFile, homeId, homePlaces,
  HOME_VARIABLE, installOf, requireHomeFile, THIS_INSTALL, type HomeEntry,
} from '../../src/platform/home.ts';
import { DESKTOP, driverFile, driverRows, driverTestFile, type Cell } from '../_csharp.ts';
import { captureError, makeFixture } from '../_fixture.ts';

/**
 * Where every machine-local file lives (D63): one home, named by `DAORIS_HOME`, and no default under
 * the user profile. The CLI's copy of a three-way twin — the driver and the service hold the same
 * contract in C#, sharing no code, the way the remotes map's three copies do (WSP3).
 */

test('the home is the variable and nothing else', () => {
  assert.equal(HOME_VARIABLE, 'DAORIS_HOME');
  assert.equal(daorisHome({ DAORIS_HOME: 'D:/somewhere/data' }), 'D:/somewhere/data');
  assert.equal(daorisHome({}), null);
  assert.equal(daorisHome({ DAORIS_HOME: '   ' }), null);
});

/**
 * 🔴 FINDDAORIS1 (D171 point 1): the install's launchers set no variable, so a `daoris` the install carries
 * (`<install>/app/cli/node_modules/daoris/`) finds its home where it runs: the install's own `data` folder. The
 * variable still wins, and a checkout is no install.
 */
test('a daoris the install carries finds the install’s data folder, and the variable still wins', () => {
  const fx = makeFixture('home-install');
  try {
    fx.write('INSTALLED.md', '# Daoris — installed desktop\n');
    const pkg = join(fx.root, 'app', 'cli', 'node_modules', 'daoris', 'src', 'platform');
    assert.equal(installOf(pkg), fx.root);
    assert.equal(daorisHome({}, installOf(pkg)), join(fx.root, 'data'));
    assert.equal(daorisHome({ DAORIS_HOME: 'D:/somewhere/data' }, fx.root), 'D:/somewhere/data');
    assert.equal(homeFile({}, 'driver', fx.root), join(fx.root, 'data', 'driver.json'));
    assert.equal(requireHomeFile({}, 'driver', fx.root), join(fx.root, 'data', 'driver.json'));

    // An `app` folder with no marker beside it is no install.
    assert.equal(installOf(join(fx.root, 'elsewhere', 'app', 'cli')), null);
  } finally {
    fx.cleanup();
  }
});

test('this checkout is no install', () => {
  assert.equal(THIS_INSTALL, null);
});

// 🔴 No default under the user profile — that is the decision, not an omission.
test('absent, it is absent rather than the user profile', () => {
  assert.equal(daorisHome({ HOME: 'D:/somewhere/profile', USERPROFILE: 'C:/somewhere/profile' }), null);
});

test('an entry asked of the environment’s home is the entry, or null when there is none', () => {
  assert.equal(homeFile({ DAORIS_HOME: 'D:/somewhere/data' }, 'driver'), join('D:/somewhere/data', 'driver.json'));
  assert.equal(homeFile({}, 'driver'), null);
});

/**
 * A home's identity (HOMEID1, the terminal RPC design §3.1), the twin of the driver's and the service's `HomeIdentity`: read
 * from `home.json` alike by every path to the folder, and a file that does not read is no id (and is never replaced).
 */
test('a home’s id is read from home.json by every path to the folder, and a file that does not read is none', () => {
  const fx = makeFixture('home-id');
  try {
    const home = join(fx.root, 'data');
    assert.equal(homeId(home), null);
    fx.write(join('data', HOME_ID_FILE), JSON.stringify({ id: '0123456789abcdef0123456789abcdef', minted: '2026-10-16T08:00:00+00:00' }));
    fx.write(join('data', 'sub', '.keep'), '');
    assert.equal(homeId(home), '0123456789abcdef0123456789abcdef');
    assert.equal(homeId(join(home, 'sub', '..')), '0123456789abcdef0123456789abcdef');
    if (process.platform === 'win32') assert.equal(homeId(home.toUpperCase()), '0123456789abcdef0123456789abcdef');

    for (const text of ['not json', '[]', '{ "id": "0123" }', '{ "id": "0123456789ABCDEF0123456789ABCDEF" }']) {
      fx.write(join('data', HOME_ID_FILE), text);
      assert.equal(homeId(home), null, text);
      assert.equal(fx.read(join('data', HOME_ID_FILE)), text);
    }
  } finally {
    fx.cleanup();
  }
});

/** HOMELEASE1: a lease records the home's id, so the CLI mints one where the home has none, as a new file only. */
test('a home with no id is minted one once, and a file that does not read is never replaced', () => {
  const fx = makeFixture('home-id-mint');
  try {
    const home = join(fx.root, 'data');
    const id = ensureHomeId(home);
    assert.match(id ?? '', /^[0-9a-f]{32}$/);
    assert.equal(ensureHomeId(home), id);
    assert.equal(ensureHomeId(join(home, '.')), id);
    const text = fx.read(join('data', HOME_ID_FILE));
    assert.ok(!text.includes('\r') && text.includes('"minted"'), text);

    fx.write(join('data', HOME_ID_FILE), 'not json');
    assert.equal(ensureHomeId(home), null);
    assert.equal(fx.read(join('data', HOME_ID_FILE)), 'not json');
  } finally {
    fx.cleanup();
  }
});

/**
 * The management class REFUSES rather than guessing: a verb with no home and no per-file override
 * names what to set, in one sentence, instead of writing somewhere nobody pointed it.
 */
test('requiring an entry with no home is a refusal that names the variable and the entry’s place', () => {
  const error = captureError(() => requireHomeFile({}, 'harnesses'));
  assert.match(error.message, /DAORIS_HOME/);
  assert.match(error.message, /harnesses\.json/);
  assert.doesNotMatch(error.message, /\.daoris/);
});

/**
 * 🔴 D172 point 5 (HOMELAYOUT2, the install layout design §6.2) with its review note: an entry is read and written at its new
 * place if that holds what the entry is, else at its old place if that is there, else at its new place; where both hold it,
 * neither is chosen and the refusal names both. The rows are the driver's `An_entry_is_where_it_is`, whose summary says each
 * column, and the service's holds the same rows: each is held to these, cell for cell.
 */
const ENTRY_ROWS: readonly (readonly string[])[] = [
  ['a.json', 'a.json', '', '', '', 'a.json'],
  ['a.json', 'a.json', '', 'a.json', '', 'a.json'],
  ['data/a.json', 'a.json', '', '', '', 'data/a.json'],
  ['data/a.json', 'a.json', '', 'a.json', '', 'a.json'],
  ['data/a.json', 'a.json', '', 'data/a.json', '', 'data/a.json'],
  ['data/a.json', 'a.json', '', 'a.json,data/a.json', '', 'refused'],
  ['data/a.json', 'a.json', '', 'a.json,data/a.json=torn', '', 'a.json'],
  ['data/a.json', 'a.json', '', 'a.json=torn,data/a.json', '', 'data/a.json'],
  ['data/a.json', 'a.json', '', 'a.json=torn', '', 'a.json'],
  ['data/k.db', 'k.db', '', 'k.db,data/k.db=', '', 'k.db'],
  ['data/s', 's', '', 's/x.log', 'x.log', 's/x.log'],
  ['data/s', 's', '', 's/x.log', 'y.log', 's/y.log'],
  ['data/s', 's', '', 's/x.log,data/s/', 'y.log', 's/y.log'],
  ['data/s', 's', '', 's/x.log,data/s/z.log', '', 'refused'],
  ['work/t/*/*', 't/*/*', '', 't/a/one/HEAD', 'a/one', 't/a/one'],
  ['work/t/*/*', 't/*/*', '', 't/a/one/HEAD', 'a/two', 'work/t/a/two'],
  ['work/t/*/*', 't/*/*', '', 't/a/one/HEAD,work/t/a/one/HEAD', 'a/one', 'refused'],
  ['work/t/*/*', 't/*/*', '', 't/a/one/HEAD', 'a/one/notes.md', 't/a/one/notes.md'],
  ['work/t/*/*', 't/*/*', '', 't/a/one/HEAD', 'a', 't/a'],
  ['work/t/*/*', 't/*/*', '', 't/a/one/HEAD,work/t/a/two/HEAD', 'a', 'work/t/a'],
  ['work/f/*', 's/*/files', '', 's/one/files/a.png', 'one/b.png', 's/one/files/b.png'],
  ['work/f/*', 's/*/files', '', '', 'one', 'work/f/one'],
  ['run/s/*.pid', 's/*.pid', '', 's/one.pid', 'one', 's/one.pid'],
  ['work/rooms/help', 'help', 'CLAUDE.md', 'help/proposals/p.json', '', 'work/rooms/help'],
  ['work/rooms/help', 'help', 'CLAUDE.md', 'help/CLAUDE.md', '', 'help'],
  ['work/rooms/help', 'help', 'CLAUDE.md', 'help/CLAUDE.md,work/rooms/help/notes.md', '', 'help'],
  ['profiles/window', '', 'chromium|config', 'config/window-state.json', '', ''],
  ['profiles/window', '', 'chromium|config', '', '', 'profiles/window'],
];

/** A list of an entry's items reads both places: the driver's `A_list_reads_both_places`, and the service's. */
const PLACES_ROWS: readonly (readonly string[])[] = [
  ['a.json', 'a.json', '', 'a.json'],
  ['data/a.json', 'a.json', '', 'data/a.json|a.json'],
  ['work/t/*/*', 't/*/*', '', 'work/t|t'],
  ['work/t/*/*', 't/*/*', 'a', 'work/t/a|t/a'],
  ['work/f/*', 's/*/files', 'one', 'work/f/one|s/one/files'],
  ['run/s/*.pid', 's/*.pid', '', 'run/s|s'],
];

/** The service's copy of a file, found by its name under the service's projects, the build's output aside. */
function serviceFile(name: string): string {
  const found: string[] = [];
  const walk = (folder: string, top: boolean): void => {
    for (const entry of readdirSync(folder, { withFileTypes: true })) {
      if (!entry.isDirectory()) {
        if (entry.name === name) found.push(join(folder, entry.name));
      } else if (!top || (entry.name !== 'bin' && entry.name !== 'obj')) {
        walk(join(folder, entry.name), entry.name.startsWith('Daoris.Service'));
      }
    }
  };
  walk(join(DESKTOP, '..', 'Daoris.Service'), false);
  assert.equal(found.length, 1, `${name} is under the service ${found.length} times`);
  return found[0]!;
}

const DRIVER_TESTS = (): string => readFileSync(driverTestFile('DaorisHomeTests.cs'), 'utf8');
const SERVICE_TESTS = (): string => readFileSync(serviceFile('DaorisHomeTests.cs'), 'utf8');

const asStrings = (rows: Cell[][]): string[][] => rows.map((row) => row.map(String));

test('the driver’s and the service’s rows of an entry’s place are these, cell for cell (HOMELAYOUT2)', () => {
  for (const [owner, source] of [['the driver’s tests', DRIVER_TESTS()], ['the service’s tests', SERVICE_TESTS()]] as const) {
    assert.deepEqual(asStrings(driverRows(source, 'An_entry_is_where_it_is', {}, owner)), ENTRY_ROWS, owner);
    assert.deepEqual(asStrings(driverRows(source, 'A_list_reads_both_places', {}, owner)), PLACES_ROWS, owner);
  }
});

/** A row's disk, laid out as the C# rows lay it: `{}` for JSON and a letter otherwise, what follows `=`, a trailing `/` a folder. */
function lay(root: string, present: string): void {
  for (const item of present.split(',').filter(Boolean)) {
    const at = item.indexOf('=');
    const file = at >= 0 ? item.slice(0, at) : item;
    const text = at >= 0 ? item.slice(at + 1) : file.endsWith('.json') ? '{}' : 'x';
    const path = join(root, ...file.split('/').filter(Boolean));
    if (file.endsWith('/')) {
      mkdirSync(path, { recursive: true });
      continue;
    }
    mkdirSync(dirname(path), { recursive: true });
    writeFileSync(path, text);
  }
}

const probe = (next: string, old: string, holds: string): HomeEntry =>
  ({ name: 'probe', category: 'data', new: next, old, holds: holds.split('|').filter(Boolean) });

test('an entry is where it is: its new place where that holds it, else its old place where that is there, else new', () => {
  for (const [next, old, holds, present, asked, expected] of ENTRY_ROWS) {
    const fx = makeFixture('home-entry');
    try {
      lay(fx.root, present!);
      const entry = probe(next!, old!, holds!);
      const asking = asked!.split('/').filter(Boolean);
      if (expected === 'refused') {
        const error = captureError(() => homeEntry(fx.root, entry, ...asking));
        assert.ok(error instanceof HomeEntryTwice, `${present}: refused`);
        const twice = error as HomeEntryTwice;
        assert.deepEqual([twice.next, twice.old], homePlaces(fx.root, entry, ...asking));
        assert.equal(twice.message, HOME_TWICE.replace('{0}', 'probe').replace('{1}', twice.next).replace('{2}', twice.old));
        continue;
      }
      assert.equal(homeEntry(fx.root, entry, ...asking), join(fx.root, ...expected!.split('/').filter(Boolean)), `${next} ${old} ${present} ${asked}`);
    } finally {
      fx.cleanup();
    }
  }
});

test('a list of an entry’s items reads both places, new first, once each, without the disk', () => {
  const home = join('D:', 'somewhere', 'data');
  for (const [next, old, asked, expected] of PLACES_ROWS) {
    assert.deepEqual(
      homePlaces(home, probe(next!, old!, ''), ...asked!.split('/').filter(Boolean)),
      expected!.split('|').map((place) => join(home, ...place.split('/'))));
  }
});

/** A C# twin's table: each `new("name", HomeCategory.X, "new", "old", "holds"…)` row of its `HomeEntry`, in order. */
function csharpTable(source: string): string[][] {
  return [...source.matchAll(/= new\("([^"]*)", HomeCategory\.(\w+), "([^"]*)", "([^"]*)"((?:, "[^"]*")*)\);/g)]
    .map((row) => [row[1]!, row[2]!.toLowerCase(), row[3]!, row[4]!, [...row[5]!.matchAll(/"([^"]*)"/g)].map((hold) => hold[1]).join('|')]);
}

/** A C# twin's refusal for an entry in two places, its `Twice` constant's literals joined. */
function csharpTwice(source: string): string {
  const at = source.indexOf('public const string Twice =');
  const end = source.indexOf(';', at);
  return [...source.slice(at, end).matchAll(/"((?:[^"\\]|\\.)*)"/g)].map((part) => part[1]).join('');
}

/**
 * 🔴 D172 point 5: the three home twins hold the same table (the install layout design §3), cell for cell and in order:
 * each entry's name, category, new place, old place and the names that make a place there.
 */
test('the driver’s and the service’s tables are this one, cell for cell (HOMELAYOUT2)', () => {
  const ours = HOME_ENTRIES.map((entry) => [entry.name, entry.category, entry.new, entry.old, entry.holds.join('|')]);
  assert.deepEqual(csharpTable(readFileSync(driverFile('DaorisHome.cs'), 'utf8')), ours, 'the driver’s');
  assert.deepEqual(csharpTable(readFileSync(serviceFile('DaorisHome.cs'), 'utf8')), ours, 'the service’s');
  assert.equal(csharpTwice(readFileSync(driverFile('DaorisHome.cs'), 'utf8')), HOME_TWICE, 'the driver’s refusal');
  assert.equal(csharpTwice(readFileSync(serviceFile('DaorisHome.cs'), 'utf8')), HOME_TWICE, 'the service’s refusal');
});

/** HOMEROOMS1: a named workspace's Ask Daoris room (`help-<name>`, ASKSCOPE1b) is an entry beside the default room's. */
test('the table names a named workspace’s help room beside the default one (HOMEROOMS1)', () => {
  const room = HOME_ENTRIES.find((entry) => entry.name === 'help-rooms');
  assert.ok(room);
  assert.deepEqual([room.category, room.new, room.old, [...room.holds]], ['work', 'help-*', 'help-*', ['CLAUDE.md']]);
});

test('each entry is named once, its places are relative, and an item’s parts are the same in both places', () => {
  assert.equal(new Set(HOME_ENTRIES.map((entry) => entry.name)).size, HOME_ENTRIES.length);
  for (const entry of HOME_ENTRIES) {
    for (const place of [entry.new, entry.old]) {
      assert.ok(!place.startsWith('/') && !place.split('/').includes('..') && !place.includes('\\'), `${entry.name}: ${place}`);
    }
    assert.equal(entry.new.split('*').length, entry.old.split('*').length, `${entry.name}: the same items in both places`);
  }
});

/** 🔴 HOMELAYOUT2 names the entries and moves nothing: HOMELAYOUT3 switches the new places on with the move (the design §10). */
test('every new place is still the old one (HOMELAYOUT2)', () => {
  for (const entry of HOME_ENTRIES) assert.equal(entry.new, entry.old, entry.name);
});
