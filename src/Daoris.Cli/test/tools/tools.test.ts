import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  GIT_SETTINGS, TOOLS, TOOLS_FILE, TOOLS_FOLDER, TOOL_PACKAGE, TOOL_RECORD, isWholePath, locationProblem, readTools, resolveTool,
} from '../../src/tools/tools.ts';
import { homeEntry } from '../../src/platform/home.ts';
import { driverRows as csharpRows, driverTestFile } from '../_csharp.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * `$DAORIS_HOME/tools.json` read and resolved (TOOLS2, D121; the tools design §2.1–§2.3, §5) — the CLI's half of a TWIN with the
 * driver's `Tools.cs`, which every child the CLI starts is handed through (TOOLS5). 🔴 `ToolsTests.cs` holds the same tables, row for
 * row and in the same order: a row changed here is changed there, in the same commit. *The driver's tables are these tables*, below,
 * reads that class's theories and holds them to these, cell for cell. The file's writes and `daoris tool` are the driver's program's
 * alone since PUSH1f2c (D194), so its write tables are `ToolsTests`' own.
 *
 * The file's rules (§2.2), each a table below:
 *
 *   1. No file, no entry, or `"use": "system"` is the system's.
 *   2. `managed` needs an exact version of one to four numbers; `file` needs a whole path. An entry that
 *      names another way's field, or lacks its own, is refused whole, and its tool is never run another way.
 *   3. A tool id this build does not declare is kept as written and never applied.
 *   4. `git` holds only keys on the allow-list: on a read kept, not applied, said.
 *   5. `locations` holds only https://, or http:// to this machine: skipped and said on a read.
 *
 * Then the resolution (§2.3): the way set decides which file starts, and a managed version nobody
 * downloaded, or a named file that is gone, refuses — never a fall back to PATH.
 *
 * Every case runs in a scratch home. Nothing here starts a program: presence is a file on disk.
 */

/** The placeholder a row writes where a whole path goes; each side spells its own. */
const WHOLE = 'WHOLE';

function home(name: string) {
  const fx = makeFixture(name);
  const at = join(fx.root, 'data');
  mkdirSync(at, { recursive: true });
  return { fx, home: at, whole: join(fx.root, 'bin', process.platform === 'win32' ? 'git.exe' : 'git') };
}

function writeFile(home: string, text: string): void {
  writeFileSync(homeEntry(home, 'tools'), text, 'utf8');
}

/**
 * A path as Windows compares it: PATH's match is spelled with PATHEXT's extension (`gh.EXE`), on both
 * sides, whatever case the file was made in.
 */
function spelled(path: string | null): string | null {
  return path !== null && process.platform === 'win32' ? path.toLowerCase() : path;
}

// ——— The declared tools (§2.1).

test('the tools are declared in code, in the order a child’s PATH takes them', () => {
  assert.deepEqual(TOOLS.map((tool) => [tool.id, tool.name, tool.answers, tool.version, tool.settings]), [
    ['git', 'Git', ['git'], ['--version'], ['core.sshCommand']],
    ['node', 'Node.js', ['node', 'npm', 'npx'], ['--version'], []],
    ['pwsh', 'PowerShell', ['pwsh'], ['--version'], []],
    ['gh', 'GitHub CLI', ['gh'], ['--version'], []],
    ['az', 'Azure CLI', ['az'], ['version'], []],
  ]);
  assert.deepEqual(GIT_SETTINGS, ['core.sshCommand']);
  assert.equal(TOOLS_FILE, 'tools.json');
  assert.deepEqual([TOOLS_FOLDER, TOOL_RECORD, TOOL_PACKAGE], ['tools', 'tool.json', 'package']);
});

// ——— Rules 1 and 2: one tool's entry. The twin is `ToolsTests.An_entry_reads_as_the_cli_reads_it`.

/** [case, the file's text or null for none, tool, way, version, file, a fragment of its problem]. */
const ENTRY_ROWS: [string, string | null, string, string | null, string | null, string | null, string | null][] = [
  ['no file', null, 'git', 'system', null, null, null],
  ['an empty object', '{}', 'git', 'system', null, null, null],
  ['no entry', '{"tools":{}}', 'git', 'system', null, null, null],
  ['the system\'s, said', '{"tools":{"git":{"use":"system"}}}', 'git', 'system', null, null, null],
  ['a key it has no field for', '{"tools":{"git":{"use":"system","note":"mine"}}}', 'git', 'system', null, null, null],
  ['managed at three numbers', '{"tools":{"git":{"use":"managed","version":"2.51.0"}}}', 'git', 'managed', '2.51.0', null, null],
  ['managed at four numbers', '{"tools":{"git":{"use":"managed","version":"2.51.0.2"}}}', 'git', 'managed', '2.51.0.2', null, null],
  ['managed at one number', '{"tools":{"git":{"use":"managed","version":"2"}}}', 'git', 'managed', '2', null, null],
  ['a file', '{"tools":{"git":{"use":"file","file":"WHOLE"}}}', 'git', 'file', null, WHOLE, null],
  ['another tool\'s entry', '{"tools":{"gh":{"use":"managed"}}}', 'git', 'system', null, null, null],
  ['not an object', '{"tools":{"git":"managed"}}', 'git', null, null, null, 'it is not an object'],
  ['no way', '{"tools":{"git":{"version":"2.51.0"}}}', 'git', null, null, null, 'it names no way'],
  ['a way nobody declared', '{"tools":{"git":{"use":"portable"}}}', 'git', null, null, null, '`use` is `portable`, not system, managed or file'],
  ['a way in the wrong case', '{"tools":{"git":{"use":"System"}}}', 'git', null, null, null, '`use` is `System`, not system, managed or file'],
  ['managed with no version', '{"tools":{"git":{"use":"managed"}}}', 'git', null, null, null, 'managed needs a `version`'],
  ['managed at a word', '{"tools":{"git":{"use":"managed","version":"latest"}}}', 'git', null, null, null, '`latest` is not an exact version'],
  ['managed at five numbers', '{"tools":{"git":{"use":"managed","version":"2.51.0.1.1"}}}', 'git', null, null, null, '`2.51.0.1.1` is not an exact version'],
  ['managed at a tag', '{"tools":{"git":{"use":"managed","version":"v2.51.0"}}}', 'git', null, null, null, '`v2.51.0` is not an exact version'],
  ['managed at a number', '{"tools":{"git":{"use":"managed","version":2.51}}}', 'git', null, null, null, 'its `version` is not text'],
  ['managed and a file', '{"tools":{"git":{"use":"managed","version":"2.51.0","file":"WHOLE"}}}', 'git', null, null, null, 'it is managed, and names a file too'],
  ['a file and a version', '{"tools":{"git":{"use":"file","file":"WHOLE","version":"2.51.0"}}}', 'git', null, null, null, 'it is a file, and names a version too'],
  ['the system\'s and a version', '{"tools":{"git":{"use":"system","version":"2.51.0"}}}', 'git', null, null, null, 'it is the system\'s, and names a version too'],
  ['the system\'s and a file', '{"tools":{"git":{"use":"system","file":"WHOLE"}}}', 'git', null, null, null, 'it is the system\'s, and names a file too'],
  ['a file with no path', '{"tools":{"git":{"use":"file"}}}', 'git', null, null, null, 'a file needs its `file`'],
  ['a file that is not text', '{"tools":{"git":{"use":"file","file":7}}}', 'git', null, null, null, 'its `file` is not text'],
  ['a file that is not whole', '{"tools":{"git":{"use":"file","file":"bin/git"}}}', 'git', null, null, null, '`bin/git` is not a whole path'],
  ['a file on a drive with no root', '{"tools":{"git":{"use":"file","file":"C:git.exe"}}}', 'git', null, null, null, '`C:git.exe` is not a whole path'],
  ['a file that is not JSON', 'not json', 'git', null, null, null, 'is not readable JSON'],
  ['a file that is a list', '[]', 'git', null, null, null, 'is not a JSON object'],
  ['tools that are a list', '{"tools":[]}', 'git', null, null, null, '`tools` is not an object'],
];

test('an entry reads as the driver reads it (rules 1 and 2)', () => {
  const { fx, home: at, whole } = home('tools-entries');
  for (const [name, text, tool, way, version, file, problem] of ENTRY_ROWS) {
    if (text !== null) writeFile(at, text.replace(WHOLE, JSON.stringify(whole).slice(1, -1)));
    // `no file` is the first row, so nothing is there yet.
    else assert.equal(existsSync(homeEntry(at, 'tools')), false, `${name}: a row with no file comes before any row that writes one`);

    const entry = readTools(at).entries[tool]!;
    assert.equal(entry.way, way, name);
    assert.equal(entry.version, version, name);
    assert.equal(entry.file, file === WHOLE ? whole : file, name);
    if (problem === null) assert.equal(entry.problem, null, `${name}: ${entry.problem}`);
    else assert.ok(entry.problem?.includes(problem), `${name}: ${entry.problem}`);
  }
  fx.cleanup();
});

test('a refused entry names the file and the tool, and says it is never run another way', () => {
  const { fx, home: at } = home('tools-entry-sentence');
  writeFile(at, '{"tools":{"git":{"use":"managed"}}}');

  const { problem } = readTools(at).entries.git!;
  assert.equal(problem, `the entry for \`git\` in ${homeEntry(at, 'tools')} does not read (managed needs a \`version\`, one to four numbers), `
    + 'and Git is never run another way: fix the entry, or `daoris tool use git system` writes a new one');
  fx.cleanup();
});

test('no file is every tool the system’s, and reading it creates nothing', () => {
  const { fx, home: at } = home('tools-absent');

  const read = readTools(at);
  assert.equal(read.exists, false);
  assert.equal(read.problem, null);
  assert.deepEqual(Object.keys(read.entries), TOOLS.map((tool) => tool.id));
  for (const entry of Object.values(read.entries)) assert.deepEqual(entry, { way: 'system', version: null, file: null, problem: null });
  assert.deepEqual([read.unknown, read.git, read.locations, read.notes], [[], {}, [], []]);
  assert.equal(existsSync(homeEntry(at, 'tools')), false);
  assert.equal(existsSync(homeEntry(at, 'tool-versions')), false);
  fx.cleanup();
});

// ——— Rule 2's whole path: .NET's `Path.IsPathFullyQualified`, spelled here.

/** [the path, whole on Windows, whole elsewhere]. */
const WHOLE_ROWS: [string, boolean, boolean][] = [
  ['C:\\Tools\\git.exe', true, false],
  ['C:/Tools/git.exe', true, false],
  ['\\\\server\\share\\git.exe', true, false],
  ['/usr/bin/git', false, true],
  ['C:git.exe', false, false],
  ['bin/git', false, false],
  ['git', false, false],
  ['', false, false],
];

test('a whole path is one .NET calls fully qualified, on this platform', () => {
  for (const [path, onWindows, elsewhere] of WHOLE_ROWS) {
    assert.equal(isWholePath(path), process.platform === 'win32' ? onWindows : elsewhere, path);
  }
});

// ——— Rule 3: a tool this build does not declare. The twin is `An_undeclared_tool_is_kept_and_never_applied`.

test('a tool this build does not declare is kept as written, never applied, and said', () => {
  const { fx, home: at } = home('tools-unknown');
  writeFile(at, '{"tools":{"bun":{"use":"file","file":"anything"},"git":{"use":"system"}}}');

  const read = readTools(at);
  assert.deepEqual(read.unknown, ['bun']);
  assert.equal('bun' in read.entries, false);
  assert.deepEqual(read.notes, ['`bun` in `tools` is not a tool this build runs: it is kept as written, and never applied']);
  assert.equal(resolveTool(at, 'git', { PATH: '' }).problem?.includes('PATH'), true);
  fx.cleanup();
});

// ——— Rule 4: what Daoris's git carries. The twin is `ToolsTests.Git_settings_read_as_the_cli_reads_them`.

/** [case, the `git` value's JSON, the settings applied, the notes]. */
const GIT_ROWS: [string, string, Record<string, string>, string[]][] = [
  ['the one key', '{"core.sshCommand":"C:/Windows/System32/OpenSSH/ssh.exe"}', { 'core.sshCommand': 'C:/Windows/System32/OpenSSH/ssh.exe' }, []],
  ['a key off the list', '{"core.sshCommand":"ssh","credential.helper":"manager"}', { 'core.sshCommand': 'ssh' },
    ['`credential.helper` in `git` is not a setting Daoris\'s git carries: it is kept, and not applied']],
  ['a key that is not text', '{"core.sshCommand":42}', {},
    ['`core.sshCommand` in `git` is not text: it is kept, and not applied']],
  ['not an object', '"core.sshCommand"', {}, ['`git` is not an object: it is kept, and nothing in it is applied']],
];

test('git’s settings read as the driver reads them (rule 4)', () => {
  const { fx, home: at } = home('tools-git');
  for (const [name, git, applied, notes] of GIT_ROWS) {
    writeFile(at, `{"git":${git}}`);
    const read = readTools(at);
    assert.deepEqual(read.git, applied, name);
    assert.deepEqual(read.notes, notes, name);
    assert.equal(read.entries.git!.way, 'system', `${name}: a setting is not a way`);
  }
  fx.cleanup();
});

// ——— Rule 5: where versions come from. The twin is `ToolsTests.A_location_is_judged_as_the_cli_judges_it`.

const LOCATION_ROWS: [string, boolean][] = [
  ['https://example.org/daoris/resources.json', true],
  ['HTTPS://EXAMPLE.ORG/resources.json', true],
  ['http://localhost:8080/resources.json', true],
  ['http://127.0.0.1/resources.json', true],
  ['http://[::1]:5177/resources.json', true],
  ['http://example.org/resources.json', false],
  ['http://localhost.example.com/resources.json', false],
  ['http://127.0.0.2/resources.json', false],
  ['ftp://example.org/resources.json', false],
  ['file:///C:/resources.json', false],
  ['https://', false],
  ['not an address', false],
  ['', false],
];

test('a location is judged as the driver judges it (rule 5)', () => {
  for (const [address, fine] of LOCATION_ROWS) {
    assert.equal(locationProblem(address), fine ? null
      : `\`${address}\` is not a resource location — an address is https://, or http:// to this machine `
        + '(localhost, 127.0.0.1 or [::1])', address);
  }
});

test('locations read in order; one that is not an address is skipped and said', () => {
  const { fx, home: at } = home('tools-locations');
  writeFile(at, JSON.stringify({
    locations: ['https://example.org/r.json', 'http://example.org/r.json', 7, 'http://localhost:8080/r.json'],
  }));
  let read = readTools(at);
  assert.deepEqual(read.locations, ['https://example.org/r.json', 'http://localhost:8080/r.json']);
  assert.deepEqual(read.notes, [
    '`http://example.org/r.json` in `locations` is skipped: an address is https://, or http:// to this machine',
    'an entry in `locations` is not text, and is skipped',
  ]);

  writeFile(at, '{"locations":"https://example.org/r.json"}');
  read = readTools(at);
  assert.deepEqual(read.locations, []);
  assert.deepEqual(read.notes, ['`locations` is not a list: it is kept, and no location is read']);
  fx.cleanup();
});

// ——— The resolution (§2.3). The twin is `ToolsTests.A_tool_resolves_as_the_cli_resolves_it`.

/**
 * Every row is `gh`. [case, the file's text or null, the managed folder (null: none; '': no record; else the
 * record's text), whether the record's executable is there, what is at FILE ('none' | 'file' | 'folder'),
 * whether PATH holds a `gh`, what resolves ('PATH' | 'MANAGED' | 'FILE' | null), refused, a fragment].
 */
const RESOLVE_ROWS: [string, string | null, string | null, boolean, 'none' | 'file' | 'folder', boolean, 'PATH' | 'MANAGED' | 'FILE' | null, boolean, string | null][] = [
  ['no file, on PATH', null, null, false, 'none', true, 'PATH', false, null],
  ['no file, not on PATH', null, null, false, 'none', false, null, false, '`gh` is not on this machine\'s PATH'],
  ['the system\'s, on PATH', '{"tools":{"gh":{"use":"system"}}}', null, false, 'none', true, 'PATH', false, null],
  ['managed, downloaded', '{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}', '{"exe":"gh_2.62.0/bin/gh.exe"}', true, 'none', true, 'MANAGED', false, null],
  ['managed, not downloaded', '{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}', null, false, 'none', true, null, true, 'that version is not downloaded'],
  ['managed, a folder with no record', '{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}', '', false, 'none', true, null, true, 'that version is not downloaded'],
  ['managed, a record that is not JSON', '{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}', 'not json', false, 'none', true, null, true, 'is not readable JSON'],
  ['managed, a record naming no executable', '{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}', '{}', false, 'none', true, null, true, 'it names no `exe`'],
  ['managed, a record that climbs out', '{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}', '{"exe":"../gh.exe"}', false, 'none', true, null, true, 'its `exe` is not a relative path inside the package'],
  ['managed, a record with a backslash', '{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}', '{"exe":"bin\\\\gh.exe"}', false, 'none', true, null, true, 'its `exe` is not a relative path inside the package'],
  ['managed, the executable gone', '{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}', '{"exe":"gh_2.62.0/bin/gh.exe"}', false, 'none', true, null, true, 'and there is no file there'],
  ['a file, there', '{"tools":{"gh":{"use":"file","file":"FILE"}}}', null, false, 'file', true, 'FILE', false, null],
  ['a file, gone', '{"tools":{"gh":{"use":"file","file":"FILE"}}}', null, false, 'none', true, null, true, 'and there is no file there'],
  ['a file that is a folder', '{"tools":{"gh":{"use":"file","file":"FILE"}}}', null, false, 'folder', true, null, true, 'and there is no file there'],
  ['an entry that does not read', '{"tools":{"gh":{"use":"managed"}}}', null, false, 'none', true, null, true, 'does not read'],
  ['a file that does not read', 'not json', null, false, 'none', true, null, true, 'is not readable JSON'],
];

test('a tool resolves as the driver resolves it: the way set decides, and never falls back to PATH', () => {
  const windows = process.platform === 'win32';
  for (const [index, [name, text, record, exe, atFile, onPath, resolves, refused, fragment]] of RESOLVE_ROWS.entries()) {
    const fx = makeFixture(`tools-resolve-${index}`);
    const at = join(fx.root, 'data');
    const pathDir = join(fx.root, 'path');
    mkdirSync(at, { recursive: true });
    mkdirSync(pathDir, { recursive: true });
    const onPathFile = join(pathDir, windows ? 'gh.exe' : 'gh');
    if (onPath) writeFileSync(onPathFile, '');
    const named = join(fx.root, 'named', windows ? 'gh.exe' : 'gh');
    if (atFile === 'file') {
      mkdirSync(join(fx.root, 'named'), { recursive: true });
      writeFileSync(named, '');
    } else if (atFile === 'folder') {
      mkdirSync(named, { recursive: true });
    }
    const version = homeEntry(at, 'tool-versions', 'gh', '2.62.0');
    const managed = join(version, TOOL_PACKAGE, 'gh_2.62.0', 'bin', 'gh.exe');
    if (record !== null) {
      mkdirSync(version, { recursive: true });
      if (record !== '') writeFileSync(join(version, TOOL_RECORD), record);
    }
    if (exe) {
      mkdirSync(join(version, TOOL_PACKAGE, 'gh_2.62.0', 'bin'), { recursive: true });
      writeFileSync(managed, '');
    }
    if (text !== null) writeFile(at, text.replace('FILE', JSON.stringify(named).slice(1, -1)));

    const resolution = resolveTool(at, 'gh', { PATH: pathDir });
    const expected = resolves === 'PATH' ? onPathFile : resolves === 'MANAGED' ? managed : resolves === 'FILE' ? named : null;
    assert.equal(spelled(resolution.file), spelled(expected), name);
    assert.equal(resolution.refused, refused, name);
    if (fragment === null) assert.equal(resolution.problem, null, `${name}: ${resolution.problem}`);
    else assert.ok(resolution.problem?.includes(fragment), `${name}: ${resolution.problem}`);
    if (refused) assert.ok(resolution.problem?.includes('never'), `${name} says it is never run another way: ${resolution.problem}`);
    fx.cleanup();
  }
});

test('a version nobody downloaded refuses, names the download and the way back to PATH, and never guesses', () => {
  const { fx, home: at } = home('tools-not-downloaded');
  writeFile(at, '{"tools":{"git":{"use":"managed","version":"2.51.0"}}}');

  const resolution = resolveTool(at, 'git', { PATH: '' });
  assert.deepEqual(resolution, {
    tool: 'git', way: 'managed', version: '2.51.0', file: null, refused: true,
    problem: `Git is managed at 2.51.0, and that version is not downloaded (${homeEntry(at, 'tool-versions', 'git', '2.51.0')}) `
      + '— it never falls back to PATH. `daoris tool use git managed 2.51.0` downloads it, and `daoris tool use git system` '
      + 'runs the one on PATH',
  });
  fx.cleanup();
});

test('a named file that is gone refuses, naming the file', () => {
  const { fx, home: at, whole } = home('tools-file-gone');
  writeFile(at, JSON.stringify({ tools: { node: { use: 'file', file: whole } } }));

  const resolution = resolveTool(at, 'node', { PATH: '' });
  assert.equal(resolution.problem, `Node.js runs the file ${whole}, and there is no file there — it never falls back to PATH. `
    + '`daoris tool use node file <path>` names another, and `daoris tool use node system` runs the one on PATH');
  fx.cleanup();
});

test('a tool PATH does not find is said, naming the three ways, and is not a refusal', () => {
  const { fx, home: at } = home('tools-not-on-path');

  const resolution = resolveTool(at, 'az', { PATH: '' });
  assert.deepEqual(resolution, {
    tool: 'az', way: 'system', version: null, file: null, refused: false,
    problem: '`az` is not on this machine\'s PATH. A tool is run as the system\'s, managed, or from a file you name: '
      + '`daoris tool use az file <path>` names one',
  });
  fx.cleanup();
});

// ——— The twin, held: the driver's tables are these tables, row for row and in this order.

const DRIVER_TABLES = driverTestFile('ToolsTests.cs');

/** One of the driver's theories, its `Whole` placeholder read as `WHOLE` here. */
const driverRows = (source: string, method: string) => csharpRows(source, method, { Whole: WHOLE }, 'ToolsTests');

test('the driver’s tables are these tables, row for row and in this order', () => {
  const source = readFileSync(DRIVER_TABLES, 'utf8').replace(/\r\n/g, '\n');

  assert.deepEqual(driverRows(source, 'An_entry_reads_as_the_cli_reads_it'), ENTRY_ROWS);
  assert.deepEqual(driverRows(source, 'A_whole_path_is_one_dotnet_calls_fully_qualified'), WHOLE_ROWS);
  assert.deepEqual(driverRows(source, 'A_location_is_judged_as_the_cli_judges_it'), LOCATION_ROWS);
  assert.deepEqual(driverRows(source, 'A_tool_resolves_as_the_cli_resolves_it'), RESOLVE_ROWS);

  // The git rows are member data, one `{ "case", …` per row.
  const git = source.slice(source.indexOf('GitRows =>'), source.indexOf('[MemberData(nameof(GitRows))]'));
  assert.deepEqual([...git.matchAll(/^\s*\{ "([^"]+)",/gm)].map((match) => match[1]), GIT_ROWS.map(([name]) => name));
});
