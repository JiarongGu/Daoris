import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { basename, join, relative, resolve } from 'node:path';
import { HOME_ENTRIES, INSTALL_DATA, INSTALL_MARKER, homePlaces } from '../../src/platform/home.ts';
import { DESKTOP } from '../_csharp.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * 🔴 HOMELAYOUT2's census (D172 point 5, the install layout design §6.2 and §11): no code joins a home path by hand. Each file
 * and folder under the Daoris home is an entry of D172's table, asked by name of its twin (`DaorisHome.Entry` in the driver
 * and the service, `homeEntry` here), so the move (HOMELAYOUT3) changes the table and no reader.
 *
 * A join by hand is a `Path.Combine(…)` (C#) or a `join(…)` or `resolve(…)` (the CLI's TypeScript) whose first part is a home:
 * a variable, field or property called `home`, `_home` or `Home`, alone or a member's. In the code every such join is one; in a
 * test, one that names an entry (a literal whose first part is a place the table holds, or a constant naming a folder or a
 * file), since a test's home is also its scratch. Each join kept is owed below with its reason; one fixed is a row this test
 * refuses until it is dropped. Scanned: every desktop and service project, the CLI's sources and tests, and the repository's
 * tools (HOMELAYOUT2b). A test's own names for the root it hands as `DAORIS_HOME` count as homes (HOMELAYOUT2c).
 */

const SRC = join(DESKTOP, '..');

/** The twins themselves, which alone join a home to a place: the table's own reading. */
const TWINS = new Set(['DaorisHome.cs', 'home.ts']);

/**
 * The joins kept, by file and join, each with why it is no entry of the table. A row the scan no longer finds is refused, so
 * the list holds only what is still there.
 */
const OWED: readonly { readonly file: string; readonly join: string; readonly why: string; readonly like?: boolean }[] = [
  {
    file: 'merge-branch.mjs',
    join: 'join(home, ',
    like: true,
    why: '`home` is the main checkout, where the gate tree and its record are kept, no Daoris home',
  },
  {
    file: 'desktop-publish.mjs',
    join: 'join(home, ...GATE_TREE',
    like: true,
    why: '`home` is the main checkout, where the gate tree and its record are kept, no Daoris home',
  },
  { file: 'HostRootTests.cs', join: 'Path.Combine(root, "bin"', like: true, why: 'a host’s build output, no home' },
  { file: 'deployment-rehearsal.test.ts', join: "join(root, 'bin', ", like: true, why: 'stub tools on a fixture’s PATH, no home' },
  { file: 'desktop-tool.test.ts', join: "join(root, 'bin', ", like: true, why: 'a project’s build output, no home' },
  { file: 'knowledge-server.test.ts', join: "join(fx.root, 'tools', 'knowledge-server.mjs')", why: 'a copy of the repository’s tools folder, no home' },
  { file: 'fsx.test.ts', join: "join(fx.root, 'bin'", like: true, why: 'a folder put on PATH, no home' },
  { file: 'landing-plugins.test.ts', join: "join(fx.root, 'bin'", like: true, why: 'a folder put on PATH, no home' },
  {
    file: 'InstallHome.cs',
    join: 'Path.Combine(home, name)',
    why: 'D63’s move-in lays each name of a `~/.daoris` from before D63 at the old home, name for name, before any table is read (the design §7.6)',
  },
  {
    file: 'EngineBrowserTests.cs',
    join: 'Path.Combine(Home, "browser", "profile")',
    why: 'an earlier build’s profile, no entry of the table (the design §3.6: stays as found); the test holds that the engine’s is not it',
  },
  {
    file: 'SystemSpace.cs',
    join: "Path.Combine(home, path.Split('/'))",
    why: 'what an earlier build left at the home’s top, `webview2/` and `browser/profile/` by exact path, no entry of the table (the cleanup design §1.6); the Disk space card only counts it (KEEP1c)',
  },
  {
    file: 'AgentsRoutes.cs',
    join: 'Path.Combine(profile.Home, settingsFile)',
    why: 'an account’s own home, the item `HomeEntry.AgentHomes` answered: the agent’s settings file is the agent’s, under the item',
  },
];

interface Join { readonly file: string; readonly line: number; readonly text: string; readonly parts: readonly string[] }

/** The code a file holds, comments and strings kept, so a join's parts are read as written. */
function joinsIn(file: string, source: string, csharp: boolean): Join[] {
  const found: Join[] = [];
  const call = csharp ? /\b(?:System\.IO\.)?Path\.Combine\(/g : /(?<![\w.$])(?:join|resolve)\(/g;
  for (const match of source.matchAll(call)) {
    const open = match.index! + match[0].length;
    let parts = argumentsOf(source, open);
    if (!parts) continue;
    // C#'s collection form, `Path.Combine([home, .. Folder, name])` (HOMELAYOUT3: `ToolResources.LocationCopy` hid a join so):
    // its parts are the collection's, a spread read as what it spreads.
    const collection = parts.length === 1 && /^\[[\s\S]*\]$/.test(parts[0]!) ? argumentsOf(`${parts[0]!.slice(1, -1)})`, 0) : null;
    if (collection) parts = collection.map((part) => part.replace(/^\.\.\s*/, ''));
    const line = source.slice(0, match.index).split('\n').length;
    found.push({ file, line, text: `${match[0]}${parts.join(', ')})`, parts });
  }
  return found;
}

/** A call's arguments from just after its `(`, split at its own commas; null where the text does not close it. */
function argumentsOf(source: string, open: number): string[] | null {
  const parts: string[] = [];
  let depth = 0;
  let start = open;
  for (let at = open; at < source.length; at++) {
    const c = source[at]!;
    if (c === '"' || c === "'" || c === '`') {
      const verbatim = c === '"' && source[at - 1] === '@';
      at += 1;
      while (at < source.length && source[at] !== c) {
        if (source[at] === '\\' && !verbatim) at += 1;
        at += 1;
      }
      continue;
    }
    if (c === '(' || c === '[' || c === '{') depth += 1;
    else if (c === ')' || c === ']' || c === '}') {
      if (depth === 0) {
        parts.push(source.slice(start, at).trim());
        return parts.filter((part) => part.length > 0);
      }
      depth -= 1;
    } else if (c === ',' && depth === 0) {
      parts.push(source.slice(start, at).trim());
      start = at + 1;
    }
  }
  return null;
}

/** A home: `home`, `_home` or `Home`, alone or as a member, `this.home` and `world.Home` among them. */
const isHome = (part: string): boolean => /^(?:[A-Za-z_][\w]*(?:\(\))?\.)*_?[Hh]ome$/.test(part);

/**
 * The names a test gives the root it hands a tool as its home: a fixture's root, `at`, `root`, a C# test's `DataHome`. A fixture's
 * own relative path (`fx.read('workflows/docs.json')`) is not read here: most name a file a per-file override hands over, so a
 * test reads a home's place through `fx.entry` or `homeEntry` by its own care (HOMELAYOUT3 found two such reads merged from main).
 */
const TEST_ROOT = /^(?:fx\.root|at|root|DataHome)$/;

/**
 * The top folders of the Daoris folder that are a category and no entry (HOMELAYOUT3): `data`, `profiles`, `work`, `cache` and
 * `run`. Alone each names nothing, so a test's own `data` (an install's shape) or a workspace it calls `work` is no entry; with
 * the part after it, it names the entry whose new place begins so. The logs are both their category and their entry.
 */
const CATEGORY_FOLDERS: ReadonlySet<string> = new Set(
  HOME_ENTRIES.map((entry) => entry.category as string).filter((category) => !HOME_ENTRIES.some((entry) => entry.old === category)));

/** A place's first part, or its first two where the first is a category's folder; none where a part is an item's. */
function head(place: string): string | null {
  const parts = place.split('/').filter((part) => part.length > 0);
  const taken = parts.slice(0, CATEGORY_FOLDERS.has(parts[0] ?? '') ? 2 : 1);
  return taken.length === 0 || taken.some((part) => part.includes('*')) ? null : taken.join('/');
}

/** The head of every place the table holds, and the names a place is known by: what a test's join may not name. */
const NAMED = new Set(HOME_ENTRIES.flatMap((entry) => [
  ...[entry.new, entry.old].map(head).filter((part): part is string => part !== null),
  ...entry.holds,
]));

/**
 * Whether a test's join names an entry: its literal parts after the root begin with a place the table holds, or its first part
 * is a constant naming a folder or a file.
 */
function namesEntry(parts: readonly string[]): boolean {
  const literals: string[] = [];
  for (const part of parts) {
    const literal = /^@?"([^"]*)"$|^'([^']*)'$/.exec(part);
    if (!literal) break;
    literals.push(...(literal[1] ?? literal[2])!.split(/[\\/]/).filter(Boolean));
  }
  if (literals.length > 0) {
    return CATEGORY_FOLDERS.has(literals[0]!) ? literals.length > 1 && NAMED.has(`${literals[0]}/${literals[1]}`) : NAMED.has(literals[0]!);
  }
  const part = parts[0];
  if (!part) return false;
  // An agent's own files sit under an account's home, the item `agent-homes` answers, never under Daoris's.
  if (/^(?:AgentSettings|ClaudeTrust)\./.test(part)) return false;
  return /^[A-Z][\w]*\.(?:Folder|FileName|[A-Z]\w*(?:File|Folder))$|^[A-Z][A-Z_]*_(?:FILE|FOLDER|DIR)$/.test(part);
}

/** Every source file under a folder, the build's output and the packages aside. */
function sources(folder: string, extension: string): string[] {
  const files: string[] = [];
  const walk = (at: string): void => {
    for (const entry of readdirSync(at, { withFileTypes: true })) {
      if (entry.isDirectory()) {
        if (!['bin', 'obj', 'node_modules', 'dist', 'wwwroot'].includes(entry.name)) walk(join(at, entry.name));
      } else if (entry.name.endsWith(extension)) {
        files.push(join(at, entry.name));
      }
    }
  };
  walk(folder);
  return files;
}

/** The scan: every join by hand, outside the twins, with the scanned files' count. */
function census(): { found: Join[]; scanned: number } {
  const roots: { folder: string; extension: string; csharp: boolean }[] = [
    { folder: join(SRC, 'Daoris.Desktop'), extension: '.cs', csharp: true },
    { folder: join(SRC, 'Daoris.Service'), extension: '.cs', csharp: true },
    { folder: join(SRC, 'Daoris.Cli', 'src'), extension: '.ts', csharp: false },
    { folder: join(SRC, 'Daoris.Cli', 'test'), extension: '.ts', csharp: false },
    // HOMELAYOUT2b: the tools ask the table too (they import the CLI's `homeEntry`).
    { folder: join(SRC, '..', 'tools'), extension: '.mjs', csharp: false },
  ];
  const found: Join[] = [];
  let scanned = 0;
  for (const root of roots) {
    for (const path of sources(root.folder, root.extension)) {
      if (TWINS.has(basename(path)) || basename(path) === 'home-census.test.ts') continue;
      scanned += 1;
      const name = relative(SRC, path).replaceAll('\\', '/');
      const testing = /\.Tests\//.test(name) || name.startsWith('Daoris.Cli/test/');
      for (const each of joinsIn(name, readFileSync(path, 'utf8'), root.csharp)) {
        // A join has a part under the home: `resolve(home)` alone is the home made whole, no place in it.
        if (each.parts.length < 2) continue;
        // A test hands its scratch root as `DAORIS_HOME` under names of its own (HOMELAYOUT2c): there too, a place the table
        // holds is asked of it.
        if (!isHome(each.parts[0]!) && !(testing && TEST_ROOT.test(each.parts[0]!))) continue;
        if (testing && !namesEntry(each.parts.slice(1))) continue;
        found.push(each);
      }
    }
  }
  return { found, scanned };
}

const owedMatches = (each: Join, owed: (typeof OWED)[number]): boolean =>
  basename(each.file) === owed.file && (owed.like ? each.text.startsWith(owed.join) : each.text === owed.join);

/**
 * HOMELAYOUT2c's test files still reading a scratch root's place by hand, each converted to `homeEntry` before the new places
 * were switched on (HOMELAYOUT3): empty, and a file the scan finds is refused, so a test never joins one again.
 */
const PENDING_TESTS: ReadonlySet<string> = new Set([]);

const owedHere = (each: Join): boolean =>
  OWED.some((owed) => owedMatches(each, owed)) || PENDING_TESTS.has(basename(each.file));

test('no code joins a home path by hand: each asks its twin for the entry by name (HOMELAYOUT2)', () => {
  const { found, scanned } = census();
  assert.ok(scanned >= 1500, `expected to scan the desktop’s, the service’s and the CLI’s sources, scanned ${scanned}`);
  const by = found.filter((each) => !owedHere(each)).map((each) => `${each.file}:${each.line}  ${each.text}`);
  assert.deepEqual(by, [], 'a join of a home by hand; ask the twin for the entry (DaorisHome.Entry, homeEntry) instead');
  for (const owed of OWED) {
    assert.ok(found.some((each) => owedMatches(each, owed)), `${owed.file}: ${owed.join} is gone; drop its row`);
  }
  for (const file of PENDING_TESTS) {
    const left = found.some((each) => basename(each.file) === file && !OWED.some((owed) => owedMatches(each, owed)));
    assert.ok(left, `${file} joins no home place by hand any more; drop it from PENDING_TESTS`);
  }
});

test('the connector locator’s own copy of `data` and `bin` is held to the table (HOMELAYOUT2b, HOMELAYOUT3)', async () => {
  // @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
  const { INSTALL_HOME, homeBins } = await import('../../../../tools/knowledge-server.mjs') as {
    INSTALL_HOME: string; homeBins: (home: string, executable: string) => string[];
  };
  assert.equal(INSTALL_HOME, INSTALL_DATA, 'the install’s home folder is the table’s `data`');
  const executable = process.platform === 'win32' ? 'daoris-knowledge.exe' : 'daoris-knowledge';
  // The locator’s `bin` candidates are the places the table gives the entry, new then old, for a scratch home, an install’s
  // `data/` and the install itself; the table changes, and this fails until the copy follows it.
  const fx = makeFixture('census-connector-bins');
  try {
    fx.write(join('install', INSTALL_MARKER), '# Daoris — installed desktop\n');
    for (const home of [join(fx.root, 'scratch'), join(fx.root, 'install', INSTALL_DATA), join(fx.root, 'install')]) {
      assert.deepEqual(homeBins(home, executable).map((path) => resolve(path)), homePlaces(home, 'bin', executable).map((path) => resolve(path)), home);
    }
  } finally {
    fx.cleanup();
  }
});

test('the census sees each way of joining a home, and never a join under an entry or another folder', () => {
  const read = (source: string, csharp = true): string[] =>
    joinsIn('x', source, csharp).filter((each) => isHome(each.parts[0] ?? '')).map((each) => each.text);
  assert.deepEqual(read('var a = Path.Combine(home, "sessions", $"{id}.log");'), ['Path.Combine(home, "sessions", $"{id}.log")']);
  assert.deepEqual(read('var a = System.IO.Path.Combine(_home, FileName);'), ['System.IO.Path.Combine(_home, FileName)']);
  assert.deepEqual(read('var a = Path.Combine(world.Home, Folder(x, ")"));'), ['Path.Combine(world.Home, Folder(x, ")"))']);
  assert.deepEqual(read("const a = join(home, 'keys.json');", false), ["join(home, 'keys.json')"]);
  assert.deepEqual(read('var a = Path.Combine(DaorisHome.Entry(home, HomeEntry.Sessions), "x.log");'), []);
  assert.deepEqual(read('var a = Path.Combine(profileHome, "usage-read");'), []);
  assert.deepEqual(read('var a = Path.Combine([home, .. LocationsFolder, name]);'), ['Path.Combine(home, LocationsFolder, name)']);
  assert.deepEqual(read("const a = path.join(root, 'driver.json');", false), []);

  assert.ok(namesEntry(['"sessions"']) && namesEntry(['"help/proposals"']) && namesEntry(['MachineLog.Folder']) && namesEntry(['COOLING_FILE']));
  assert.ok(['fx.root', 'at', 'root', 'DataHome'].every((name) => TEST_ROOT.test(name)) && !TEST_ROOT.test('repoRoot'));
  assert.ok(namesEntry(["'harnesses.json'"]) && namesEntry(['TOOLS_FOLDER']));
  assert.ok(!namesEntry(['"engine"']) && !namesEntry(['"agent.log"']) && !namesEntry(['name']));
  // HOMELAYOUT3: a category's folder names an entry only with the part after it, as its new place begins.
  assert.deepEqual([...CATEGORY_FOLDERS].sort(), ['cache', 'data', 'profiles', 'run', 'work']);
  assert.ok(namesEntry(["'data'", "'driver.json'"]) && namesEntry(['"run/home.lease"']) && namesEntry(['"work"', '"brainstorm-rooms"', '"b1"']));
  assert.ok(namesEntry(['"logs"']) && namesEntry(['"profiles"', '"agents"']));
  assert.ok(!namesEntry(["'data'"]) && !namesEntry(['"work"', '"engine"']) && !namesEntry(['"profiles"', 'adapter.Name']));
});
