import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  GIT_SETTINGS, TOOLS, TOOLS_FILE, TOOLS_FOLDER, TOOL_PACKAGE, TOOL_RECORD, addLocation, gitKeyProblem,
  isWholePath, locationProblem, readTools, removeLocation, resolveTool, useFile, useManaged, useSystem,
} from '../src/tools.ts';
import { commandTool } from '../src/toolinstall.ts';
import { driverRows as csharpRows } from './_csharp.ts';
import { captureError, makeFixture } from './_fixture.ts';

/**
 * `$DAORIS_HOME/tools.json` and its resolution (TOOLS2, D121; the tools design §2.1–§2.3, §5) — the CLI's
 * half of a TWIN with the driver's `Tools.cs`. 🔴 `ToolsTests.cs` holds the same tables, row for row and in
 * the same order: a row changed here is changed there, in the same commit. *The driver's tables are these
 * tables*, below, reads that class's theories and holds them to these, cell for cell.
 *
 * The file's rules (§2.2), each a table below:
 *
 *   1. No file, no entry, or `"use": "system"` is the system's.
 *   2. `managed` needs an exact version of one to four numbers; `file` needs a whole path. An entry that
 *      names another way's field, or lacks its own, is refused whole, and its tool is never run another way.
 *   3. A tool id this build does not declare is kept as written and never applied.
 *   4. `git` holds only keys on the allow-list: off it, refused on a write; on a read kept, not applied, said.
 *   5. `locations` holds only https://, or http:// to this machine: refused on a write; skipped and said on a read.
 *   6. Setting one way clears the others, and a writer keeps what it has no field for.
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
  writeFileSync(join(home, TOOLS_FILE), text, 'utf8');
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
    else assert.equal(existsSync(join(at, TOOLS_FILE)), false, `${name}: a row with no file comes before any row that writes one`);

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
  assert.equal(problem, `the entry for \`git\` in ${join(at, TOOLS_FILE)} does not read (managed needs a \`version\`, one to four numbers), `
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
  assert.equal(existsSync(join(at, TOOLS_FILE)), false);
  assert.equal(existsSync(join(at, TOOLS_FOLDER)), false);
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

const OFF_THE_LIST = ['credential.helper', 'core.sshcommand', 'core.autocrlf', ''];

test('a key off the allow-list is refused on a write', () => {
  assert.equal(gitKeyProblem('core.sshCommand'), null);
  for (const key of OFF_THE_LIST) {
    assert.equal(gitKeyProblem(key),
      `\`${key}\` is not a setting Daoris's git carries — the one it carries is core.sshCommand`, key);
  }
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
    const version = join(at, TOOLS_FOLDER, 'gh', '2.62.0');
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
    problem: `Git is managed at 2.51.0, and that version is not downloaded (${join(at, TOOLS_FOLDER, 'git', '2.51.0')}) `
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

// ——— Rule 6: a write. The twin is `ToolsTests.A_write_sets_one_way_and_keeps_the_rest`.

test('setting one way clears the others, and keeps what the writer has no field for', () => {
  const { fx, home: at, whole } = home('tools-write');
  mkdirSync(join(fx.root, 'bin'), { recursive: true });
  writeFileSync(whole, '');
  const file = join(at, TOOLS_FILE);

  useSystem(at, 'git');
  assert.deepEqual(JSON.parse(readFileSync(file, 'utf8')), { tools: { git: { use: 'system' } } });

  writeFile(at, JSON.stringify({
    note: 'mine',
    tools: { git: { use: 'managed', version: '2.51.0', keep: 'this' }, bun: { use: 'system' }, gh: { use: 'managed' } },
    git: { 'core.sshCommand': 'ssh', 'credential.helper': 'manager' },
    locations: ['https://example.org/r.json', 'ftp://kept.example/r.json'],
  }));
  useFile(at, 'git', whole);
  assert.deepEqual(JSON.parse(readFileSync(file, 'utf8')), {
    note: 'mine',
    tools: { git: { use: 'file', keep: 'this', file: whole }, bun: { use: 'system' }, gh: { use: 'managed' } },
    git: { 'core.sshCommand': 'ssh', 'credential.helper': 'manager' },
    locations: ['https://example.org/r.json', 'ftp://kept.example/r.json'],
  });
  assert.equal(readFileSync(file, 'utf8').endsWith('}\n'), true, 'two-space JSON and a final newline');

  useSystem(at, 'git');
  assert.deepEqual(JSON.parse(readFileSync(file, 'utf8')).tools.git, { use: 'system', keep: 'this' });

  writeFile(at, '{"tools":{"git":"managed"}}');
  useSystem(at, 'git');
  assert.deepEqual(JSON.parse(readFileSync(file, 'utf8')), { tools: { git: { use: 'system' } } }, 'an entry that was no object is replaced');
  fx.cleanup();
});

/** [case, the file's text before, the way written, the tool, the file (GONE: one not there), a fragment of the refusal]. */
const REFUSED_WRITES: [string, string, 'system' | 'file', string, string | null, string][] = [
  ['a file that is not JSON', 'not json', 'system', 'git', null, 'is not readable JSON'],
  ['tools that are a list', '{"tools":[]}', 'system', 'git', null, '`tools` is not an object'],
  ['a tool nobody declared', '{}', 'system', 'bun', null, '`bun` is not a tool this build runs — one of: git, node, pwsh, gh, az'],
  ['a file that is not whole', '{}', 'file', 'git', 'bin/git', '`bin/git` is not a whole path'],
  ['a file that is not there', '{}', 'file', 'git', 'GONE', 'no file at'],
];

test('a write that cannot be made writes nothing', () => {
  const { fx, home: at, whole } = home('tools-refused-writes');
  mkdirSync(join(fx.root, 'bin'), { recursive: true });
  writeFileSync(whole, '');
  for (const [name, before, way, tool, file, fragment] of REFUSED_WRITES) {
    writeFile(at, before);
    const error = captureError(() => (way === 'system'
      ? useSystem(at, tool)
      : useFile(at, tool, file === 'GONE' ? `${whole}.gone` : file!)));
    assert.ok(error.message.includes(fragment), `${name}: ${error.message}`);
    assert.equal(readFileSync(join(at, TOOLS_FILE), 'utf8'), before, `${name}: the file is as it was`);
  }
  fx.cleanup();
});

// ——— Managed, written (TOOLS4, rule 6): only over a version that is downloaded. The twin is
// `ToolsTests.Managed_is_written_as_the_cli_writes_it`.

/** [case, the file's text before (null: none; FILE: a whole path), the version downloaded (null: none), the version asked, the entry after, a fragment of the refusal]. */
const USE_MANAGED_ROWS: [string, string | null, string | null, string, string | null, string | null][] = [
  ['a version downloaded', null, '2.62.0', '2.62.0', '{"use":"managed","version":"2.62.0"}', null],
  ['a version not downloaded', null, null, '2.62.0', null, 'is not downloaded'],
  ['another version downloaded', null, '2.61.0', '2.62.0', null, 'is not downloaded'],
  ['a version that is not exact', null, null, 'latest', null, '`latest` is not an exact version'],
  ['a version that climbs out', null, null, '../2.62.0', null, '`../2.62.0` is not an exact version'],
  ['over a file it names', '{"tools":{"gh":{"use":"file","file":"FILE","keep":"this"}}}', '2.62.0', '2.62.0', '{"use":"managed","keep":"this","version":"2.62.0"}', null],
  ['over a file that does not read', 'not json', '2.62.0', '2.62.0', null, 'is not readable JSON'],
];

/** A downloaded version as the resolution finds it: its record, naming an executable that is there. */
function downloaded(at: string, tool: string, version: string): void {
  const folder = join(at, TOOLS_FOLDER, tool, version);
  mkdirSync(join(folder, TOOL_PACKAGE, 'bin'), { recursive: true });
  writeFileSync(join(folder, TOOL_PACKAGE, 'bin', `${tool}.exe`), tool);
  writeFileSync(join(folder, TOOL_RECORD), JSON.stringify({ exe: `bin/${tool}.exe` }));
}

test('managed is written only over a version that is downloaded, as the driver writes it', () => {
  for (const [index, [name, before, has, version, after, refusal]] of USE_MANAGED_ROWS.entries()) {
    const { fx, home: at, whole } = home(`tools-use-managed-${index}`);
    if (before !== null) writeFile(at, before.replace('FILE', JSON.stringify(whole).slice(1, -1)));
    if (has !== null) downloaded(at, 'gh', has);
    const was = existsSync(join(at, TOOLS_FILE)) ? readFileSync(join(at, TOOLS_FILE), 'utf8') : null;

    if (refusal === null) {
      useManaged(at, 'gh', version);
      assert.deepEqual(JSON.parse(readFileSync(join(at, TOOLS_FILE), 'utf8')).tools.gh, JSON.parse(after!), name);
      assert.equal(resolveTool(at, 'gh', { PATH: '' }).file, join(at, TOOLS_FOLDER, 'gh', version, TOOL_PACKAGE, 'bin', 'gh.exe'), name);
    } else {
      const error = captureError(() => useManaged(at, 'gh', version));
      assert.ok(error.message.includes(refusal), `${name}: ${error.message}`);
      assert.equal(existsSync(join(at, TOOLS_FILE)) ? readFileSync(join(at, TOOLS_FILE), 'utf8') : null, was, `${name}: nothing written`);
    }
    fx.cleanup();
  }
});

// ——— Locations, written (rule 5's write side, TOOLS4). The twin is `ToolsTests.A_location_is_written_as_the_cli_writes_it`.

/** [case, the file's text before (null: none), add or remove, the address, the locations after (JSON), whether it changed, a fragment of the refusal]. */
const LOCATION_WRITES: [string, string | null, string, string, string | null, boolean, string | null][] = [
  ['add to no file', null, 'add', 'https://a.example/r.json', '["https://a.example/r.json"]', true, null],
  ['add after another', '{"locations":["https://a.example/r.json"]}', 'add', 'http://localhost:8080/r.json', '["https://a.example/r.json","http://localhost:8080/r.json"]', true, null],
  ['add one already listed', '{"locations":["https://a.example/r.json"]}', 'add', 'https://a.example/r.json', '["https://a.example/r.json"]', false, null],
  ['add over http to another host', null, 'add', 'http://a.example/r.json', null, false, 'is not a resource location'],
  ['add over locations that are not a list', '{"locations":"https://a.example/r.json"}', 'add', 'https://b.example/r.json', null, false, '`locations` is not a list'],
  ['add over a file that does not read', 'not json', 'add', 'https://a.example/r.json', null, false, 'is not readable JSON'],
  ['remove one listed', '{"locations":["https://a.example/r.json","https://b.example/r.json"]}', 'remove', 'https://a.example/r.json', '["https://b.example/r.json"]', true, null],
  ['remove one not listed', '{"locations":["https://a.example/r.json"]}', 'remove', 'https://b.example/r.json', '["https://a.example/r.json"]', false, null],
  ['remove beside one that is not an address', '{"locations":["ftp://kept.example/r.json","https://a.example/r.json"]}', 'remove', 'https://a.example/r.json', '["ftp://kept.example/r.json"]', true, null],
  ['remove from no file', null, 'remove', 'https://a.example/r.json', null, false, null],
];

test('a location is added or removed as the driver writes it, and a refusal writes nothing', () => {
  for (const [index, [name, before, verb, address, after, changed, refusal]] of LOCATION_WRITES.entries()) {
    const { fx, home: at } = home(`tools-locations-write-${index}`);
    if (before !== null) writeFile(at, before);
    const write = () => (verb === 'add' ? addLocation(at, address) : removeLocation(at, address));

    if (refusal === null) {
      assert.equal(write(), changed, name);
      const file = join(at, TOOLS_FILE);
      assert.deepEqual(existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')).locations : null, after === null ? null : JSON.parse(after), name);
    } else {
      const error = captureError(write);
      assert.ok(error.message.includes(refusal), `${name}: ${error.message}`);
      assert.equal(existsSync(join(at, TOOLS_FILE)) ? readFileSync(join(at, TOOLS_FILE), 'utf8') : null, before, `${name}: nothing written`);
    }
    fx.cleanup();
  }
});

test('a location written keeps every key the writer has no field for', () => {
  const { fx, home: at } = home('tools-locations-keep');
  writeFile(at, '{"note":"mine","tools":{"git":{"use":"system"}},"git":{"core.sshCommand":"ssh"}}');
  addLocation(at, 'https://a.example/r.json');
  assert.deepEqual(JSON.parse(readFileSync(join(at, TOOLS_FILE), 'utf8')), {
    note: 'mine', tools: { git: { use: 'system' } }, git: { 'core.sshCommand': 'ssh' }, locations: ['https://a.example/r.json'],
  });
  fx.cleanup();
});

// ——— The twin, held: the driver's tables are these tables, row for row and in this order.

const DRIVER_TABLES = join(
  dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'ToolsTests.cs');

/** One of the driver's theories, its `Whole` placeholder read as `WHOLE` here. */
const driverRows = (source: string, method: string) => csharpRows(source, method, { Whole: WHOLE }, 'ToolsTests');

test('the driver’s tables are these tables, row for row and in this order', () => {
  const source = readFileSync(DRIVER_TABLES, 'utf8').replace(/\r\n/g, '\n');

  assert.deepEqual(driverRows(source, 'An_entry_reads_as_the_cli_reads_it'), ENTRY_ROWS);
  assert.deepEqual(driverRows(source, 'A_whole_path_is_one_dotnet_calls_fully_qualified'), WHOLE_ROWS);
  assert.deepEqual(driverRows(source, 'A_key_off_the_allow_list_is_refused_on_a_write'), OFF_THE_LIST.map((key) => [key]));
  assert.deepEqual(driverRows(source, 'A_location_is_judged_as_the_cli_judges_it'), LOCATION_ROWS);
  assert.deepEqual(driverRows(source, 'A_tool_resolves_as_the_cli_resolves_it'), RESOLVE_ROWS);
  assert.deepEqual(driverRows(source, 'A_write_that_cannot_be_made_writes_nothing'), REFUSED_WRITES);
  assert.deepEqual(driverRows(source, 'Managed_is_written_as_the_cli_writes_it'), USE_MANAGED_ROWS);
  assert.deepEqual(driverRows(source, 'A_location_is_written_as_the_cli_writes_it'), LOCATION_WRITES);

  // The git rows are member data, one `{ "case", …` per row.
  const git = source.slice(source.indexOf('GitRows =>'), source.indexOf('[MemberData(nameof(GitRows))]'));
  assert.deepEqual([...git.matchAll(/^\s*\{ "([^"]+)",/gm)].map((match) => match[1]), GIT_ROWS.map(([name]) => name));
});

// ——— The verb: `daoris tool list|path|use`.

/**
 * An environment whose PATH is the case's, under no other spelling (TEST2). A copy of `process.env` keeps the
 * spelling the shell that ran the suite gave it (`Path` from PowerShell, `PATH` from Git Bash), and on Windows
 * `onPath` reads the first key that is PATH in any case (TOOLS5), so a `PATH` set beside an inherited `Path`
 * was not the one read, and the system's git and gh were found.
 */
function withOnlyPath(path: string, inherited: NodeJS.ProcessEnv): Record<string, string | undefined> {
  const env: Record<string, string | undefined> = {};
  for (const [key, value] of Object.entries(inherited)) if (key.toUpperCase() !== 'PATH') env[key] = value;
  env.PATH = path;
  return env;
}

/** The verb, given no way to reach a network: these verbs need none. */
function run(argv: string[], at: string | null, path = '', inherited: NodeJS.ProcessEnv = process.env): { code: number; out: string[] } {
  const out: string[] = [];
  const env = withOnlyPath(path, inherited);
  if (at === null) delete env.DAORIS_HOME;
  else env.DAORIS_HOME = at;
  const code = commandTool({ root: process.cwd(), argv, write: (line) => out.push(line), packageRoot: process.cwd() }, null, env);
  assert.equal(typeof code, 'number', `${argv.join(' ')} answers at once: it reaches no network`);
  return { code: code as number, out };
}

test('a case’s PATH is the one read, whatever the shell that ran the suite spells it (TEST2)', () => {
  const { fx, home: at } = home('tools-path-spelling');
  const shell = join(fx.root, 'shell');
  mkdirSync(shell, { recursive: true });
  writeFileSync(join(shell, process.platform === 'win32' ? 'gh.exe' : 'gh'), '');
  // PowerShell hands its children `Path`, Git Bash `PATH`: this is PowerShell's, from whichever shell runs it.
  const inherited: NodeJS.ProcessEnv = withOnlyPath(shell, process.env);
  inherited.Path = inherited.PATH;
  delete inherited.PATH;

  const gh = run(['path', 'gh'], at, '', inherited);
  assert.equal(gh.code, 1, `the case's empty PATH finds no gh, and the shell's was not read: ${gh.out.join('\n')}`);
  fx.cleanup();
});

test('tool list on a home with no file says every tool is the system’s, and writes nothing', () => {
  const { fx, home: at } = home('tools-list-empty');

  const { code, out } = run(['list'], at);
  assert.equal(code, 0, out.join('\n'));
  assert.equal(out[0], `daoris: ${join(at, TOOLS_FILE)} — no file, so every tool is the system's, from PATH.`);
  for (const tool of TOOLS) {
    const line = out.find((row) => row.startsWith(`  ${tool.id.padEnd(5)} ${tool.name}`));
    assert.ok(line, `no row for ${tool.id}:\n${out.join('\n')}`);
    assert.match(line, /the system's: not found — /, line);
  }
  assert.equal(existsSync(join(at, TOOLS_FILE)), false);
  assert.deepEqual(run([], at).out, out, 'no verb is list');
  fx.cleanup();
});

test('tool list names each way, a refusal, and what the file keeps without applying it', () => {
  const { fx, home: at, whole } = home('tools-list');
  mkdirSync(join(fx.root, 'bin'), { recursive: true });
  writeFileSync(whole, '');
  const pathDir = join(fx.root, 'path');
  mkdirSync(pathDir, { recursive: true });
  const onPathGh = join(pathDir, process.platform === 'win32' ? 'gh.exe' : 'gh');
  writeFileSync(onPathGh, '');
  writeFile(at, JSON.stringify({
    tools: { git: { use: 'file', file: whole }, node: { use: 'managed', version: '22.20.0' }, pwsh: { use: 'managed' }, bun: { use: 'system' } },
  }));

  const { code, out } = run(['list'], at, pathDir);
  assert.equal(code, 1, 'a refused tool is a refusal');
  assert.equal(out[0], `daoris: ${join(at, TOOLS_FILE)}`);
  assert.ok(out.includes(`  git   Git        a file: ${whole}`), out.join('\n'));
  assert.ok(out.some((row) => row.startsWith('  node  Node.js    managed 22.20.0: refused — Node.js is managed at 22.20.0')), out.join('\n'));
  assert.ok(out.some((row) => row.startsWith('  pwsh  PowerShell refused — the entry for `pwsh`')), out.join('\n'));
  assert.ok(out.map(spelled).includes(spelled(`  gh    GitHub CLI the system's: ${onPathGh}`)), out.join('\n'));
  assert.ok(out.includes('  `bun` in `tools` is not a tool this build runs: it is kept as written, and never applied'), out.join('\n'));
  fx.cleanup();
});

test('tool path prints the file alone, or says why there is none and exits 1', () => {
  const { fx, home: at, whole } = home('tools-path');
  mkdirSync(join(fx.root, 'bin'), { recursive: true });
  writeFileSync(whole, '');
  writeFile(at, JSON.stringify({ tools: { git: { use: 'file', file: whole }, gh: { use: 'managed', version: '2.62.0' } } }));

  assert.deepEqual(run(['path', 'git'], at), { code: 0, out: [whole] });
  const gh = run(['path', 'gh'], at);
  assert.equal(gh.code, 1);
  assert.match(gh.out[0]!, /^daoris: GitHub CLI is managed at 2\.62\.0, and that version is not downloaded/);
  assert.deepEqual(run(['path', 'az'], at).code, 1, 'not on PATH is no file to print');
  fx.cleanup();
});

test('tool use writes the file and says what the tool now resolves to', () => {
  const { fx, home: at, whole } = home('tools-use');
  mkdirSync(join(fx.root, 'bin'), { recursive: true });
  writeFileSync(whole, '');

  const file = run(['use', 'git', 'file', whole], at);
  assert.equal(file.code, 0, file.out.join('\n'));
  assert.deepEqual(file.out, [
    `daoris: Git is set to the file ${whole} — written to ${join(at, TOOLS_FILE)}.`,
    `  \`daoris tool path git\` answers ${whole}; \`daoris tool use git system\` puts it back on PATH.`,
  ]);

  const system = run(['use', 'git', 'system'], at);
  assert.equal(system.code, 0);
  assert.equal(system.out[0], `daoris: Git is set to the system's, from PATH — written to ${join(at, TOOLS_FILE)}.`);
  assert.match(system.out[1]!, /^ {2}`daoris tool path git` answers: `git` is not on this machine's PATH/);
  assert.deepEqual(JSON.parse(readFileSync(join(at, TOOLS_FILE), 'utf8')), { tools: { git: { use: 'system' } } });
  fx.cleanup();
});

test('a relative file is named from where the command runs', () => {
  const { fx, home: at, whole } = home('tools-use-relative');
  mkdirSync(join(fx.root, 'bin'), { recursive: true });
  writeFileSync(whole, '');
  const out: string[] = [];
  const code = commandTool({
    root: fx.root, argv: ['use', 'git', 'file', join('bin', process.platform === 'win32' ? 'git.exe' : 'git')],
    write: (line) => out.push(line), packageRoot: fx.root,
  }, null, { ...process.env, DAORIS_HOME: at });
  assert.equal(code, 0, out.join('\n'));
  assert.equal(readTools(at).entries.git!.file, whole);
  fx.cleanup();
});

test('the verb refuses what it does not set, with no file written', () => {
  const { fx, home: at } = home('tools-verb-refusals');
  const rows: [string[], RegExp][] = [
    [['frobnicate'], /^unknown tool verb 'frobnicate' — one of: list, path, use, download, update, delete, locations, look$/],
    [['path'], /^`tool path` needs a tool — one of: git, node, pwsh, gh, az$/],
    [['path', 'bun'], /^`bun` is not a tool this build runs — one of: git, node, pwsh, gh, az/],
    [['use', 'git'], /^`tool use git` takes `system`, `managed \[<version>\]` or `file <path>`$/],
    [['use', 'git', 'file'], /^`tool use git file` needs a path — the executable to run$/],
  ];
  for (const [argv, said] of rows) {
    const error = captureError(() => run(argv, at));
    assert.match(error.message, said, argv.join(' '));
    assert.equal(error.exitCode, 2, argv.join(' '));
  }
  assert.equal(existsSync(join(at, TOOLS_FILE)), false);
  assert.match(captureError(() => run(['list'], null)).message, /no Daoris home.*\(wanted: tools\.json\)/);
  fx.cleanup();
});
