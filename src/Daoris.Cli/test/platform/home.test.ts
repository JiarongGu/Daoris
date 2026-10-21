import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';
import {
  daorisHome, HOME_ENTRIES, HOME_ID_FILE, HOME_TWICE, homeEntry, HomeEntryTwice, homeFile, homeId, homeOf, homePlaces,
  HOME_VARIABLE, INSTALL_DATA, INSTALL_MARKER, installOf, requireHomeFile, THIS_INSTALL, type HomeEntry, type HomeEntryName,
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
    const data = join(fx.root, INSTALL_DATA);
    assert.equal(daorisHome({}, installOf(pkg)), data);
    assert.equal(daorisHome({ DAORIS_HOME: 'D:/somewhere/data' }, fx.root), 'D:/somewhere/data');
    // The install's data entries are where they were (HOMELAYOUT3), in its `data/`, whichever spelling names the install.
    assert.equal(homeFile({}, 'driver', fx.root), join(data, 'driver.json'));
    assert.equal(requireHomeFile({}, 'driver', fx.root), join(data, 'driver.json'));
    assert.equal(resolve(homeFile({ DAORIS_HOME: fx.root }, 'driver')!), resolve(data, 'driver.json'));

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
  assert.equal(homeFile({ DAORIS_HOME: 'X:/daoris' }, 'driver'), join('X:/daoris', 'data', 'driver.json'));
  assert.equal(homeFile({}, 'driver'), null);
});

/**
 * A home's identity (HOMEID1, the terminal RPC design §3.1), the twin of the driver's and the service's `HomeIdentity`: read
 * from `home.json` alike by every path to the folder, and a file that does not read is no id (and is never replaced).
 */
test('a home’s id is read from home.json by every path to the folder, and a file that does not read is none', () => {
  const fx = makeFixture('home-id');
  try {
    const home = fx.root;
    assert.equal(homeId(home), null);
    const file = fx.entry('home-id');
    assert.equal(basename(file), HOME_ID_FILE);
    writeFileSync(file, JSON.stringify({ id: '0123456789abcdef0123456789abcdef', minted: '2026-10-16T08:00:00+00:00' }));
    fx.write(join('sub', '.keep'), '');
    assert.equal(homeId(home), '0123456789abcdef0123456789abcdef');
    assert.equal(homeId(join(home, 'sub', '..')), '0123456789abcdef0123456789abcdef');
    if (process.platform === 'win32') assert.equal(homeId(home.toUpperCase()), '0123456789abcdef0123456789abcdef');

    for (const text of ['not json', '[]', '{ "id": "0123" }', '{ "id": "0123456789ABCDEF0123456789ABCDEF" }']) {
      writeFileSync(file, text);
      assert.equal(homeId(home), null, text);
      assert.equal(readFileSync(file, 'utf8'), text);
    }
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

/**
 * 🔴 D172 point 2 (HOMELAYOUT3): a home names its Daoris folder and the old home. The driver's `A_home_names_its_folder`, whose
 * summary says each column, and the service's hold the same rows: each is held to these, cell for cell.
 */
const FOLDER_ROWS: readonly (readonly string[])[] = [
  ['install', 'data', '', 'driver', '', 'data/driver.json'],
  ['install', '', '', 'driver', '', 'data/driver.json'],
  ['install', 'data', '', 'keys', '', 'profiles/keys.json'],
  ['install', 'data', 'data/keys.json', 'keys', '', 'data/keys.json'],
  ['install', '', 'data/keys.json', 'keys', '', 'data/keys.json'],
  ['install', 'data', 'data/keys.json,profiles/keys.json', 'keys', '', 'refused'],
  ['install', 'data', 'data/logs/desktop.log', 'logs', '', 'data/logs'],
  ['install', 'data', '', 'logs', '', 'logs'],
  ['install', 'data', 'data/chromium/', 'window', '', 'data'],
  ['install', 'data', '', 'window', '', 'profiles/window'],
  ['install', 'data', 'data/harnesses/claude-code/work/x', 'agent-homes', 'claude-code/work', 'data/harnesses/claude-code/work'],
  ['install', 'data', 'data/harnesses/claude-code/work/x', 'agent-homes', 'claude-code/new', 'profiles/agents/claude-code/new'],
  ['install', 'data', '', 'home-lease', '', 'run/home.lease'],
  ['root', '', '', 'driver', '', 'data/driver.json'],
  ['root', '', 'driver.json', 'driver', '', 'driver.json'],
  ['root', '', '', 'running', '', 'run/running.json'],
  ['root', '', '', 'logs', '', 'logs'],
  ['root', 'data', '', 'driver', '', 'data/data/driver.json'],
];

/** The home a file of it is in: the driver's `A_home_is_found_from_its_entry_at_either_place`, and the service's. */
const HOME_OF_ROWS: readonly (readonly string[])[] = [
  ['install', 'data/driver.json', 'driver', 'data'],
  ['install', 'data/harnesses.json', 'harnesses', 'data'],
  ['root', 'data/driver.json', 'driver', ''],
  ['root', 'driver.json', 'driver', ''],
  ['install', 'profiles/browser/engine', 'browser-engine', 'data'],
  ['install', 'data/browser/engine', 'browser-engine', 'data'],
  ['root', 'profiles/browser/engine', 'browser-engine', ''],
  ['root', 'browser/engine', 'browser-engine', ''],
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
    assert.deepEqual(asStrings(driverRows(source, 'A_home_names_its_folder', {}, owner)), FOLDER_ROWS, owner);
    assert.deepEqual(asStrings(driverRows(source, 'A_home_is_found_from_its_entry_at_either_place', {}, owner)), HOME_OF_ROWS, owner);
  }
});

/** A row's root: the marker at it where the row is an install's. */
function shaped(name: string, shape: string): ReturnType<typeof makeFixture> {
  const fx = makeFixture(name);
  if (shape === 'install') fx.write(INSTALL_MARKER, '# Daoris — installed desktop\n');
  return fx;
}

const under = (root: string, place: string): string => resolve(join(root, ...place.split('/').filter(Boolean)));

test('a home names its folder: an install by its folder or its data/, any other home itself (HOMELAYOUT3)', () => {
  for (const [shape, homeAs, present, name, asked, expected] of FOLDER_ROWS) {
    const fx = shaped('home-folder', shape!);
    try {
      lay(fx.root, present!);
      const home = homeAs ? join(fx.root, homeAs) : fx.root;
      const asking = asked!.split('/').filter(Boolean);
      const why = `${shape} ${homeAs} ${present} ${name} ${asked}`;
      if (expected === 'refused') {
        const error = captureError(() => homeEntry(home, name as HomeEntryName, ...asking));
        assert.ok(error instanceof HomeEntryTwice, why);
        assert.deepEqual([(error as HomeEntryTwice).next, (error as HomeEntryTwice).old], homePlaces(home, name as HomeEntryName, ...asking), why);
        continue;
      }
      assert.equal(resolve(homeEntry(home, name as HomeEntryName, ...asking)), under(fx.root, expected!), why);
    } finally {
      fx.cleanup();
    }
  }
});

test('a home is found from its entry at either place, an install’s spelled as its data/ (HOMELAYOUT3)', () => {
  for (const [shape, file, name, expected] of HOME_OF_ROWS) {
    const fx = shaped('home-of', shape!);
    try {
      assert.equal(homeOf(join(fx.root, ...file!.split('/')), name as HomeEntryName), under(fx.root, expected!), `${shape} ${file}`);
    } finally {
      fx.cleanup();
    }
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
  assert.deepEqual([room.category, room.new, room.old, [...room.holds]], ['work', 'work/rooms/help-*', 'help-*', ['CLAUDE.md']]);
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

/**
 * 🔴 D172 §3 switched on (HOMELAYOUT3): every new place is under the top folder its category names, and the logs are the `logs`
 * folder itself, so the Daoris folder holds the seven categories and nothing else of Daoris's at its top.
 */
test('each new place is under its category’s folder (HOMELAYOUT3)', () => {
  for (const entry of HOME_ENTRIES) {
    assert.ok(entry.new === entry.category || entry.new.startsWith(`${entry.category}/`), `${entry.name}: ${entry.new}`);
  }
});
