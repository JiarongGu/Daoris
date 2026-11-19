import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, join, resolve } from 'node:path';
import {
  daorisHome, HOME_ENTRIES, HOME_ID_FILE, homeEntry, homeFile, homeId, homeOf,
  HOME_VARIABLE, INSTALL_DATA, INSTALL_MARKER, installOf, requireHomeFile, THIS_INSTALL, type HomeEntry, type HomeEntryName,
} from '../../src/platform/home.ts';
import { DESKTOP, driverFile, driverRows, driverTestFile, type Cell } from '../_csharp.ts';
import { captureError, makeFixture } from '../_fixture.ts';

/**
 * Where every machine-local file lives: one home, named by `DAORIS_HOME`, and no default under
 * the user profile. The CLI's copy of a three-way twin — the driver and the service hold the same
 * contract in C#, sharing no code, the way the remotes map's three copies do.
 */

test('the home is the variable and nothing else', () => {
  assert.equal(HOME_VARIABLE, 'DAORIS_HOME');
  assert.equal(daorisHome({ DAORIS_HOME: 'D:/somewhere/data' }), 'D:/somewhere/data');
  assert.equal(daorisHome({}), null);
  assert.equal(daorisHome({ DAORIS_HOME: '   ' }), null);
});

/**
 * 🔴 The install's launchers set no variable, so a `daoris` the install carries
 * (`<install>/app/cli/daoris.mjs`) finds its home where it runs: the install's own `data` folder. The
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
    // The install's data entries are in its `data/`, whichever spelling names the install.
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
 * A home's identity, the twin of the driver's and the service's `HomeIdentity`: read
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
 * 🔴 An entry is at its place under the Daoris folder, whatever is on the disk. The rows are the driver's
 * `An_entry_is_where_it_is`, whose summary says each column, and the service's holds the same rows: each is held to these, cell
 * for cell.
 */
const ENTRY_ROWS: readonly (readonly string[])[] = [
  ['data/a.json', '', '', 'data/a.json'],
  ['data/s', '', 'x.log', 'data/s/x.log'],
  ['work/t/*/*', '', 'a/one', 'work/t/a/one'],
  ['work/t/*/*', '', 'a/one/notes.md', 'work/t/a/one/notes.md'],
  ['work/t/*/*', '', 'a', 'work/t/a'],
  ['work/t/*/*', '', '', 'work/t'],
  ['work/f/*', '', 'one/b.png', 'work/f/one/b.png'],
  ['run/s/*.pid', '', 'one', 'run/s/one.pid'],
  ['work/rooms/help', 'CLAUDE.md', '', 'work/rooms/help'],
];

/**
 * 🔴 A home names its Daoris folder. The driver's `A_home_names_its_folder`, whose
 * summary says each column, and the service's hold the same rows: each is held to these, cell for cell.
 */
const FOLDER_ROWS: readonly (readonly string[])[] = [
  ['install', 'data', 'driver', '', 'data/driver.json'],
  ['install', '', 'driver', '', 'data/driver.json'],
  ['install', 'data', 'keys', '', 'profiles/keys.json'],
  ['install', 'data', 'logs', '', 'logs'],
  ['install', 'data', 'window', '', 'profiles/window'],
  ['install', 'data', 'agent-homes', 'claude-code/new', 'profiles/agents/claude-code/new'],
  ['install', 'data', 'home-lease', '', 'run/home.lease'],
  ['root', '', 'driver', '', 'data/driver.json'],
  ['root', '', 'running', '', 'run/running.json'],
  ['root', '', 'logs', '', 'logs'],
  ['root', 'data', 'driver', '', 'data/data/driver.json'],
];

/** The home a file of it is in: the driver's `A_home_is_found_from_its_entry`, and the service's. */
const HOME_OF_ROWS: readonly (readonly string[])[] = [
  ['install', 'data/driver.json', 'driver', 'data'],
  ['install', 'data/harnesses.json', 'harnesses', 'data'],
  ['root', 'data/driver.json', 'driver', ''],
  ['install', 'profiles/browser/engine', 'browser-engine', 'data'],
  ['root', 'profiles/browser/engine', 'browser-engine', ''],
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
    assert.deepEqual(asStrings(driverRows(source, 'A_home_names_its_folder', {}, owner)), FOLDER_ROWS, owner);
    assert.deepEqual(asStrings(driverRows(source, 'A_home_is_found_from_its_entry', {}, owner)), HOME_OF_ROWS, owner);
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
  for (const [shape, homeAs, name, asked, expected] of FOLDER_ROWS) {
    const fx = shaped('home-folder', shape!);
    try {
      const home = homeAs ? join(fx.root, homeAs) : fx.root;
      const asking = asked!.split('/').filter(Boolean);
      const why = `${shape} ${homeAs} ${name} ${asked}`;
      assert.equal(resolve(homeEntry(home, name as HomeEntryName, ...asking)), under(fx.root, expected!), why);
    } finally {
      fx.cleanup();
    }
  }
});

test('a home is found from its entry, an install’s spelled as its data/ (HOMELAYOUT3)', () => {
  for (const [shape, file, name, expected] of HOME_OF_ROWS) {
    const fx = shaped('home-of', shape!);
    try {
      assert.equal(homeOf(join(fx.root, ...file!.split('/')), name as HomeEntryName), under(fx.root, expected!), `${shape} ${file}`);
    } finally {
      fx.cleanup();
    }
  }
});

const probe = (next: string, holds: string): HomeEntry =>
  ({ name: 'probe', category: 'data', new: next, holds: holds.split('|').filter(Boolean) });

test('an entry is where it is: its place under the Daoris folder, whatever is on the disk', () => {
  for (const [next, holds, asked, expected] of ENTRY_ROWS) {
    const fx = makeFixture('home-entry');
    try {
      const asking = asked!.split('/').filter(Boolean);
      assert.equal(homeEntry(fx.root, probe(next!, holds!), ...asking), join(fx.root, ...expected!.split('/').filter(Boolean)), `${next} ${asked}`);
    } finally {
      fx.cleanup();
    }
  }
});

/** A C# twin's table: each `new("name", HomeCategory.X, "place", "holds"…)` row of its `HomeEntry`, in order. */
function csharpTable(source: string): string[][] {
  return [...source.matchAll(/= new\("([^"]*)", HomeCategory\.(\w+), "([^"]*)"((?:, "[^"]*")*)\);/g)]
    .map((row) => [row[1]!, row[2]!.toLowerCase(), row[3]!, [...row[4]!.matchAll(/"([^"]*)"/g)].map((hold) => hold[1]).join('|')]);
}

/**
 * 🔴 The three home twins hold the same table, cell for cell and in order:
 * each entry's name, category, place and the names that make a place there.
 */
test('the driver’s and the service’s tables are this one, cell for cell (HOMELAYOUT2)', () => {
  const ours = HOME_ENTRIES.map((entry) => [entry.name, entry.category, entry.new, entry.holds.join('|')]);
  assert.deepEqual(csharpTable(readFileSync(driverFile('DaorisHome.cs'), 'utf8')), ours, 'the driver’s');
  assert.deepEqual(csharpTable(readFileSync(serviceFile('DaorisHome.cs'), 'utf8')), ours, 'the service’s');
});

/** The volume's clock is read from a mark the driver touches in the home's run folder, never in a tree or its git directory. */
test('the clock mark is a row of the run folder in all three tables (REPOCONFIG1a1)', () => {
  const mark = ['clock-mark', 'run', 'run/clock.mark', ''];
  const ours = HOME_ENTRIES.find((entry) => entry.name === 'clock-mark');
  assert.ok(ours, 'the CLI’s');
  assert.deepEqual([ours.name, ours.category, ours.new, ours.holds.join('|')], mark);
  for (const [owner, file] of [['the driver’s', driverFile('DaorisHome.cs')], ['the service’s', serviceFile('DaorisHome.cs')]] as const) {
    assert.deepEqual(csharpTable(readFileSync(file, 'utf8')).find((row) => row[0] === 'clock-mark'), mark, owner);
  }
});

/** A named workspace's Ask Daoris room (`help-<name>`) is an entry beside the default room's. */
test('the table names a named workspace’s help room beside the default one (HOMEROOMS1)', () => {
  const room = HOME_ENTRIES.find((entry) => entry.name === 'help-rooms');
  assert.ok(room);
  assert.deepEqual([room.category, room.new, [...room.holds]], ['work', 'work/rooms/help-*', ['CLAUDE.md']]);
});

test('each entry is named once and its place is relative', () => {
  assert.equal(new Set(HOME_ENTRIES.map((entry) => entry.name)).size, HOME_ENTRIES.length);
  for (const entry of HOME_ENTRIES) {
    const place = entry.new;
    assert.ok(!place.startsWith('/') && !place.split('/').includes('..') && !place.includes('\\'), `${entry.name}: ${place}`);
  }
});

/**
 * 🔴 Every new place is under the top folder its category names, and the logs are the `logs`
 * folder itself, so the Daoris folder holds the seven categories and nothing else of Daoris's at its top.
 */
test('each new place is under its category’s folder (HOMELAYOUT3)', () => {
  for (const entry of HOME_ENTRIES) {
    assert.ok(entry.new === entry.category || entry.new.startsWith(`${entry.category}/`), `${entry.name}: ${entry.new}`);
  }
});
