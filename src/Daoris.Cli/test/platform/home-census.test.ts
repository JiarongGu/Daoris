import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { basename, join, relative } from 'node:path';
import { HOME_ENTRIES } from '../../src/platform/home.ts';
import { DESKTOP } from '../_csharp.ts';

/**
 * 🔴 HOMELAYOUT2's census (D172 point 5, the install layout design §6.2 and §11): no code joins a home path by hand. Each file
 * and folder under the Daoris home is an entry of D172's table, asked by name of its twin (`DaorisHome.Entry` in the driver
 * and the service, `homeEntry` here), so the move (HOMELAYOUT3) changes the table and no reader.
 *
 * A join by hand is a `Path.Combine(…)` (C#) or a `join(…)` or `resolve(…)` (the CLI's TypeScript) whose first part is a home:
 * a variable, field or property called `home`, `_home` or `Home`, alone or a member's. In the code every such join is one; in a
 * test, one that names an entry (a literal whose first part is a place the table holds, or a constant naming a folder or a
 * file), since a test's home is also its scratch. Each join kept is owed below with its reason; one fixed is a row this test
 * refuses until it is dropped. Scanned: every desktop and service project, and the CLI's sources and tests.
 */

const SRC = join(DESKTOP, '..');

/** The twins themselves, which alone join a home to a place: the table's own reading. */
const TWINS = new Set(['DaorisHome.cs', 'home.ts']);

/**
 * The joins kept, by file and join, each with why it is no entry of the table. A row the scan no longer finds is refused, so
 * the list holds only what is still there.
 */
const OWED: readonly { readonly file: string; readonly join: string; readonly why: string }[] = [
  {
    file: 'InstallHome.cs',
    join: 'Path.Combine(home, name)',
    why: 'D63’s move-in lays each name of a `~/.daoris` from before D63 at the old home, name for name, before any table is read (the design §7.6)',
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
    const parts = argumentsOf(source, open);
    if (!parts) continue;
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

/** The first part of every place the table holds, and the names a place is known by: what a test's join may not name. */
const NAMED = new Set(HOME_ENTRIES.flatMap((entry) => [
  ...[entry.new, entry.old].map((place) => place.split('/')[0]!).filter((part) => part.length > 0 && !part.includes('*')),
  ...entry.holds,
]));

/** Whether a test's join names an entry: a literal of a place the table holds, or a constant naming a folder or a file. */
function namesEntry(part: string | undefined): boolean {
  if (!part) return false;
  const literal = /^@?"([^"]*)"$|^'([^']*)'$/.exec(part);
  if (literal) return NAMED.has((literal[1] ?? literal[2])!.split(/[\\/]/)[0]!);
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
        if (each.parts.length < 2 || !isHome(each.parts[0]!)) continue;
        if (testing && !namesEntry(each.parts[1])) continue;
        found.push(each);
      }
    }
  }
  return { found, scanned };
}

const owedHere = (each: Join): boolean =>
  OWED.some((owed) => basename(each.file) === owed.file && each.text === owed.join);

test('no code joins a home path by hand: each asks its twin for the entry by name (HOMELAYOUT2)', () => {
  const { found, scanned } = census();
  assert.ok(scanned >= 1500, `expected to scan the desktop’s, the service’s and the CLI’s sources, scanned ${scanned}`);
  const by = found.filter((each) => !owedHere(each)).map((each) => `${each.file}:${each.line}  ${each.text}`);
  assert.deepEqual(by, [], 'a join of a home by hand; ask the twin for the entry (DaorisHome.Entry, homeEntry) instead');
  for (const owed of OWED) {
    assert.ok(found.some((each) => basename(each.file) === owed.file && each.text === owed.join), `${owed.file}: ${owed.join} is gone; drop its row`);
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
  assert.deepEqual(read("const a = path.join(root, 'driver.json');", false), []);

  assert.ok(namesEntry('"sessions"') && namesEntry('"help/proposals"') && namesEntry('MachineLog.Folder') && namesEntry('COOLING_FILE'));
  assert.ok(!namesEntry('"engine"') && !namesEntry('"agent.log"') && !namesEntry('name'));
});
