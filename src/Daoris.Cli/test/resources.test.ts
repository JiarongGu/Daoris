import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  ARCHIVES, BUILT_IN, BUILT_IN_LAYOUT, LOCATIONS_FOLDER, PLATFORMS, RESOURCES_FILE, SCHEMA, builtInList, compareVersions,
  currentPlatform, integrityOf, locationCopy, mergeResources, parseResources, platformFor, readLists, readResourceFile,
  type MergedResources, type ResourceList,
} from '../src/resources.ts';
import { TOOLS, TOOLS_FILE, TOOLS_FOLDER } from '../src/tools.ts';
import { driverRows as csharpRows } from './_csharp.ts';
import { makeFixture } from './_fixture.ts';

/**
 * `resources.json`, schema 1, and the merge of every list read (TOOLS3, D121; the tools design §3.1–§3.5, §5):
 * the CLI's half of a TWIN with the driver's `ToolResources.cs`. 🔴 `ToolResourcesTests.cs` holds the same
 * tables, row for row and in the same order, and *the driver's tables are these tables*, below, reads that
 * class's theories and holds them to these, cell for cell. A row changed here is changed there, in the same
 * commit.
 *
 * The list's rules (§3.2), each a table below:
 *
 *   1. `schema` is read first: anything but 1 refuses the whole list, and so does a `tools` that is no object.
 *   2. A tool this build does not declare is named and never offered (§3.4 rule 1).
 *   3. A version is one to four numbers; a platform is one of the table's; a version names its `files`.
 *   4. A file names its `url` (https://, or http:// to this machine), its `sha256` (64 hex, kept in lower case),
 *      its `size` (whole bytes above 0), its `archive` (zip or tar.gz) and its `exe` (a relative `/` path inside
 *      the archive). `paths` defaults to the folder `exe` is in, `.` for the archive's root.
 *   5. What does not read is skipped and said, and never guessed at.
 *
 * Then the merge (§3.4): the person's locations in order, then the list built in; one tool, version and
 * platform is one download, so lists that disagree refuse that version, naming both; lists that agree under
 * other addresses are mirrors; the versions are the union, and the newest is the highest by number.
 *
 * Nothing here opens a connection: a list is text, or a file on disk.
 */

const here = dirname(fileURLToPath(import.meta.url));

/** A hash that reads: the placeholder `"SHA"` in a row. */
const HASH = 'a'.repeat(64);

/** The file every structural row uses, as the token `FILE`. */
const FILE = '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe"}';

/** A row's text with its tokens spelled out, as the driver's `Expand` spells them. */
const expand = (text: string): string => text.replaceAll('FILE', FILE).replaceAll('"SHA"', `"${HASH}"`);

/** One file of `gh` 2.62.0 for `win-x64`, in a list otherwise sound. */
const oneFile = (file: string): string => `{"schema":1,"tools":{"gh":{"versions":{"2.62.0":{"files":{"win-x64":${file}}}}}}}`;

/** What a list offers, in one line: each tool in ordinal order, its versions newest first, its platforms in the table's order. */
function summary(list: ResourceList): string {
  const parts: string[] = [];
  for (const id of [...list.tools.keys()].sort()) {
    const tool = list.tools.get(id)!;
    for (const version of [...tool.versions.keys()].sort((a, b) => compareVersions(b, a))) {
      const files = tool.versions.get(version)!;
      for (const platform of PLATFORMS) if (files.has(platform)) parts.push(`${id} ${version} ${platform}`);
    }
  }
  return parts.join('; ');
}

// ——— The constants both twins spell.

test('the list, where it is, and what it may name', () => {
  assert.equal(RESOURCES_FILE, 'resources.json');
  assert.deepEqual([...BUILT_IN_LAYOUT], ['app', 'resources.json']);
  assert.deepEqual([...LOCATIONS_FOLDER], [TOOLS_FOLDER, 'locations']);
  assert.equal(SCHEMA, 1);
  assert.deepEqual([...ARCHIVES], ['zip', 'tar.gz']);
  assert.equal(BUILT_IN, 'the built-in list');
});

// ——— The platform table (§3.2). The twin is `ToolResourcesTests.A_platform_is_named_as_the_cli_names_it`.

/** [the platform, Node's process.platform, Node's process.arch, .NET's OS, .NET's architecture]. */
const PLATFORM_ROWS: [string | null, string, string, string, string][] = [
  ['win-x64', 'win32', 'x64', 'Windows', 'X64'],
  ['win-arm64', 'win32', 'arm64', 'Windows', 'Arm64'],
  ['linux-x64', 'linux', 'x64', 'Linux', 'X64'],
  ['linux-arm64', 'linux', 'arm64', 'Linux', 'Arm64'],
  ['osx-x64', 'darwin', 'x64', 'OSX', 'X64'],
  ['osx-arm64', 'darwin', 'arm64', 'OSX', 'Arm64'],
  [null, 'win32', 'ia32', 'Windows', 'X86'],
  [null, 'linux', 'arm', 'Linux', 'Arm'],
  [null, 'freebsd', 'x64', 'FreeBSD', 'X64'],
];

test('a platform is named as the driver names it, and one it does not know is none', () => {
  for (const [platform, os, arch] of PLATFORM_ROWS) assert.equal(platformFor(os, arch), platform, `${os} ${arch}`);
  assert.deepEqual([...PLATFORMS], PLATFORM_ROWS.map(([platform]) => platform).filter((platform) => platform !== null));
  assert.equal(currentPlatform(), platformFor(process.platform, process.arch));
});

// ——— Versions, by number (§3.4 rule 6). The twin is `ToolResourcesTests.Versions_compare_as_the_cli_compares_them`.

/** [a, b, the sign of a against b]. */
const VERSION_ROWS: [string, string, number][] = [
  ['2.10.0', '2.9.0', 1],
  ['2.9.0', '2.10.0', -1],
  ['2.62.0', '2.62.0', 0],
  ['24.21.0', '2.56.0', 1],
  ['1.0.0.10', '1.0.0.9', 1],
  ['3', '2.99.99.99', 1],
  ['99999999999999999999', '1', 1],
  ['2.62', '2.62.0', -1],
  ['007', '7', -1],
];

test('versions compare by number, and two spellings of one number still order', () => {
  for (const [a, b, sign] of VERSION_ROWS) assert.equal(Math.sign(compareVersions(a, b)), sign, `${a} against ${b}`);
});

// ——— Rules 1–3, 5: the list's shape. The twin is `ToolResourcesTests.A_list_reads_as_the_cli_reads_it`.

/** [case, the list's text (FILE and "SHA" are tokens), a fragment of its problem, a fragment of its one note, what it offers]. */
const LIST_ROWS: [string, string, string | null, string | null, string][] = [
  ['not JSON', 'not json', 'is not readable JSON', null, ''],
  ['a list', '[]', 'is not a JSON object', null, ''],
  ['no schema', '{"tools":{}}', 'names no `schema`', null, ''],
  ['a schema of null', '{"schema":null,"tools":{}}', 'names no `schema`', null, ''],
  ['a schema this build does not know', '{"schema":2,"tools":{}}', 'is schema 2, and this build reads schema 1', null, ''],
  ['a schema that is text', '{"schema":"1","tools":{}}', 'is schema "1", and this build reads schema 1', null, ''],
  ['tools that are a list', '{"schema":1,"tools":[]}', '`tools` is not an object', null, ''],
  ['no tools', '{"schema":1}', null, null, ''],
  ['one download', '{"schema":1,"tools":{"gh":{"versions":{"2.62.0":{"files":{"win-x64":FILE}}}}}}', null, null, 'gh 2.62.0 win-x64'],
  ['versions newest first, platforms in the table’s order', '{"schema":1,"tools":{"gh":{"versions":{"2.9.0":{"files":{"win-x64":FILE}},"2.10.0":{"files":{"linux-x64":FILE,"win-x64":FILE}}}}}}', null, null, 'gh 2.10.0 win-x64; gh 2.10.0 linux-x64; gh 2.9.0 win-x64'],
  ['two tools', '{"schema":1,"tools":{"node":{"versions":{"24.21.0":{"files":{"win-x64":FILE}}}},"gh":{"versions":{"2.62.0":{"files":{"win-x64":FILE}}}}}}', null, null, 'gh 2.62.0 win-x64; node 24.21.0 win-x64'],
  ['a tool this build does not run', '{"schema":1,"tools":{"bun":{"versions":{"1.2.0":{"files":{"win-x64":FILE}}}}}}', null, 'names `bun`, which is not a tool this build runs', ''],
  ['a tool that is not an object', '{"schema":1,"tools":{"gh":"2.62.0"}}', null, '`gh` is not an object', ''],
  ['a tool with no versions', '{"schema":1,"tools":{"gh":{}}}', null, null, ''],
  ['versions that are not an object', '{"schema":1,"tools":{"gh":{"versions":[]}}}', null, '`gh`\'s `versions` is not an object', ''],
  ['a version that is a tag', '{"schema":1,"tools":{"gh":{"versions":{"v2.62.0":{"files":{"win-x64":FILE}}}}}}', null, '`gh` `v2.62.0` is not an exact version', ''],
  ['a version of five numbers', '{"schema":1,"tools":{"gh":{"versions":{"2.62.0.1.1":{"files":{"win-x64":FILE}}}}}}', null, '`gh` `2.62.0.1.1` is not an exact version', ''],
  ['a version with no files', '{"schema":1,"tools":{"gh":{"versions":{"2.62.0":{}}}}}', null, '`gh` 2.62.0 has no `files` object', ''],
  ['files that are a list', '{"schema":1,"tools":{"gh":{"versions":{"2.62.0":{"files":[]}}}}}', null, '`gh` 2.62.0 has no `files` object', ''],
  ['a platform this build does not know', '{"schema":1,"tools":{"gh":{"versions":{"2.62.0":{"files":{"win-x86":FILE}}}}}}', null, 'names `win-x86`, which is not a platform this build knows', ''],
  ['a file that does not read', '{"schema":1,"tools":{"gh":{"versions":{"2.62.0":{"files":{"win-x64":{}}}}}}}', null, '`gh` 2.62.0 for win-x64 is skipped — ', ''],
  ['one file skipped beside one that reads', '{"schema":1,"tools":{"gh":{"versions":{"2.62.0":{"files":{"win-x64":FILE,"linux-x64":7}}}}}}', null, '`gh` 2.62.0 for linux-x64 is skipped — it is not an object', 'gh 2.62.0 win-x64'],
  ['a source that is no address', '{"schema":1,"tools":{"gh":{"source":"http://example.org/releases","versions":{"2.62.0":{"files":{"win-x64":FILE}}}}}}', null, '`gh`\'s `source` is not https://, or http:// to this machine, and is not shown', 'gh 2.62.0 win-x64'],
  ['a licence with no id', '{"schema":1,"tools":{"gh":{"licence":{"url":"https://example.org/licence"},"versions":{"2.62.0":{"files":{"win-x64":FILE}}}}}}', null, '`gh`\'s `licence` names no `id`, and is not shown', 'gh 2.62.0 win-x64'],
  ['a licence whose address is no address', '{"schema":1,"tools":{"gh":{"licence":{"id":"MIT","url":"ftp://example.org/licence"},"versions":{"2.62.0":{"files":{"win-x64":FILE}}}}}}', null, '`gh`\'s licence `url` is not https://, or http:// to this machine, and is not shown', 'gh 2.62.0 win-x64'],
];

test('a list reads as the driver reads it (rules 1–3 and 5)', () => {
  for (const [name, text, problem, note, offers] of LIST_ROWS) {
    const list = parseResources(expand(text), 'a list');
    if (problem === null) assert.equal(list.problem, null, `${name}: ${list.problem}`);
    else assert.ok(list.problem?.includes(problem), `${name}: ${list.problem}`);
    if (note === null) assert.deepEqual(list.notes, [], name);
    else {
      assert.equal(list.notes.length, 1, `${name}: ${list.notes.join(' / ')}`);
      assert.ok(list.notes[0]!.includes(note), `${name}: ${list.notes[0]}`);
    }
    assert.equal(summary(list), offers, name);
  }
});

test('a list that does not read names itself, offers nothing, and says a newer Daoris may read it', () => {
  const list = parseResources('{"schema":2,"tools":{"gh":{}}}', 'https://example.org/r.json');
  assert.equal(list.problem, 'https://example.org/r.json is schema 2, and this build reads schema 1: nothing in it is read, '
    + 'and a newer Daoris may read it');
  assert.equal(list.tools.size, 0);
  assert.deepEqual(list.unknown, []);
});

test('a tool this build does not run is kept by name, and nothing of it is read', () => {
  const list = parseResources(expand('{"schema":1,"tools":{"bun":{"versions":{"1.2.0":{"files":{"win-x64":FILE}}}},"gh":{}}}'), 'first');
  assert.deepEqual(list.unknown, ['bun']);
  assert.deepEqual([...list.tools.keys()], ['gh']);
  assert.deepEqual(list.notes, ['first names `bun`, which is not a tool this build runs: nothing of it is offered']);
});

// ——— Rule 4: one file. The twin is `ToolResourcesTests.A_file_reads_as_the_cli_reads_it`.

/** [case, the file's JSON ("SHA" is a token), a fragment of why it is skipped, its folders for PATH joined by `,`]. */
const FILE_ROWS: [string, string, string | null, string | null][] = [
  ['the whole file', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe"}', null, 'bin'],
  ['its folders named', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe","paths":["bin","."]}', null, 'bin,.'],
  ['folders of null', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe","paths":null}', null, 'bin'],
  ['an executable at the root', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"gh.exe"}', null, '.'],
  ['an executable two folders down', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"gh_2.62.0/bin/gh.exe"}', null, 'gh_2.62.0/bin'],
  ['a tar.gz', '{"url":"https://example.org/gh.tar.gz","sha256":"SHA","size":10,"archive":"tar.gz","exe":"bin/gh"}', null, 'bin'],
  ['an address on this machine', '{"url":"http://127.0.0.1:8080/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe"}', null, 'bin'],
  ['a hash in capitals', '{"url":"https://example.org/gh.zip","sha256":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","size":10,"archive":"zip","exe":"bin/gh.exe"}', null, 'bin'],
  ['not an object', '"gh.zip"', 'it is not an object', null],
  ['no address', '{"sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe"}', 'it names no `url`', null],
  ['an address that is not text', '{"url":7,"sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe"}', 'it names no `url`', null],
  ['an address over http to another host', '{"url":"http://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe"}', '`url` is not https://, or http:// to this machine', null],
  ['an address that is no address', '{"url":"gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe"}', '`url` is not https://, or http:// to this machine', null],
  ['no hash', '{"url":"https://example.org/gh.zip","size":10,"archive":"zip","exe":"bin/gh.exe"}', '`sha256` is not 64 hex digits', null],
  ['a short hash', '{"url":"https://example.org/gh.zip","sha256":"abc","size":10,"archive":"zip","exe":"bin/gh.exe"}', '`sha256` is not 64 hex digits', null],
  ['a hash that is not hex', '{"url":"https://example.org/gh.zip","sha256":"gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg","size":10,"archive":"zip","exe":"bin/gh.exe"}', '`sha256` is not 64 hex digits', null],
  ['no size', '{"url":"https://example.org/gh.zip","sha256":"SHA","archive":"zip","exe":"bin/gh.exe"}', '`size` is not a whole number of bytes above 0', null],
  ['a size of nothing', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":0,"archive":"zip","exe":"bin/gh.exe"}', '`size` is not a whole number of bytes above 0', null],
  ['a size below nothing', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":-10,"archive":"zip","exe":"bin/gh.exe"}', '`size` is not a whole number of bytes above 0', null],
  ['a size with a fraction', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10.5,"archive":"zip","exe":"bin/gh.exe"}', '`size` is not a whole number of bytes above 0', null],
  ['a size that is text', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":"10","archive":"zip","exe":"bin/gh.exe"}', '`size` is not a whole number of bytes above 0', null],
  ['no archive', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"exe":"bin/gh.exe"}', '`archive` is not zip or tar.gz', null],
  ['an archive nobody reads', '{"url":"https://example.org/gh.7z","sha256":"SHA","size":10,"archive":"7z","exe":"bin/gh.exe"}', '`archive` is not zip or tar.gz', null],
  ['an archive in capitals', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"ZIP","exe":"bin/gh.exe"}', '`archive` is not zip or tar.gz', null],
  ['no executable', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip"}', 'it names no `exe`', null],
  ['an executable that climbs out', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"../gh.exe"}', '`exe` is not a relative path inside the archive', null],
  ['an executable with a backslash', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin\\\\gh.exe"}', '`exe` is not a relative path inside the archive', null],
  ['an executable from the root', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"/gh.exe"}', '`exe` is not a relative path inside the archive', null],
  ['an executable on a drive', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"C:/gh.exe"}', '`exe` is not a relative path inside the archive', null],
  ['an executable that is a folder', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/"}', '`exe` is not a relative path inside the archive', null],
  ['folders that climb out', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe","paths":["../bin"]}', '`paths` is not a list of folders inside the archive', null],
  ['no folders', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe","paths":[]}', '`paths` is not a list of folders inside the archive', null],
  ['folders that are text', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe","paths":"bin"}', '`paths` is not a list of folders inside the archive', null],
  ['a folder that is a number', '{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe","paths":["bin",7]}', '`paths` is not a list of folders inside the archive', null],
];

test('a file reads as the driver reads it (rule 4)', () => {
  for (const [name, file, why, paths] of FILE_ROWS) {
    const list = parseResources(expand(oneFile(file)), 'a list');
    assert.equal(list.problem, null, name);
    const read = list.tools.get('gh')?.versions.get('2.62.0')?.get('win-x64') ?? null;
    if (why === null) {
      assert.deepEqual(list.notes, [], name);
      assert.equal(read?.paths.join(','), paths, name);
    } else {
      assert.equal(read, null, `${name}: a file that does not read is skipped`);
      assert.equal(list.notes.length, 1, `${name}: ${list.notes.join(' / ')}`);
      assert.ok(list.notes[0]!.startsWith('a list: `gh` 2.62.0 for win-x64 is skipped — '), `${name}: ${list.notes[0]}`);
      assert.ok(list.notes[0]!.includes(why), `${name}: ${list.notes[0]}`);
    }
  }
});

test('a file as read: the hash kept in lower case, and every field as the list names it', () => {
  const list = parseResources(oneFile(`{"url":"https://example.org/gh.zip","sha256":"${'AB'.repeat(32)}","size":15512013,`
    + '"archive":"zip","exe":"bin/gh.exe"}'), 'a list');
  assert.deepEqual(list.tools.get('gh')!.versions.get('2.62.0')!.get('win-x64'), {
    url: 'https://example.org/gh.zip', sha256: 'ab'.repeat(32), size: 15512013, archive: 'zip', exe: 'bin/gh.exe', paths: ['bin'],
  });
});

test('a list’s source and licence read with the tool, and a byte-order mark is not part of the text', () => {
  const list = parseResources(`\uFEFF${expand('{"schema":1,"tools":{"git":{"source":"https://github.com/git-for-windows/git/releases",'
    + '"licence":{"id":"GPL-2.0-only","url":"https://example.org/COPYING"},"versions":{}},'
    + '"gh":{"licence":{"id":"MIT"},"versions":{"2.62.0":{"files":{"win-x64":FILE}}}}}}')}`, 'a list');
  assert.equal(list.problem, null);
  assert.deepEqual(list.tools.get('git'), {
    source: 'https://github.com/git-for-windows/git/releases', licence: { id: 'GPL-2.0-only', url: 'https://example.org/COPYING' },
    versions: new Map(),
  });
  assert.deepEqual(list.tools.get('gh')!.licence, { id: 'MIT', url: null });
  assert.equal(list.tools.get('gh')!.source, null);
});

// ——— The merge (§3.4). The twin is `ToolResourcesTests.Lists_merge_as_the_cli_merges_them`.

/**
 * A list, spelled short: downloads separated by `; `, each `<tool> <version> <platform> <hash letter> <host>`
 * and any of `size=`, `archive=`, `exe=`, `paths=a,b` it differs by; `<tool> licence <id>` names a licence; and
 * `!` is a list that is not JSON. The driver's `ListText` spells the same.
 */
function listText(spec: string): string {
  if (spec === '!') return 'not json';
  const tools: Record<string, { licence?: { id: string }; versions: Record<string, { files: Record<string, Record<string, unknown>> }> }> = {};
  for (const entry of spec.split('; ').filter(Boolean)) {
    const [tool, ...rest] = entry.split(' ') as [string, ...string[]];
    const held = (tools[tool] ??= { versions: {} });
    if (rest[0] === 'licence') {
      held.licence = { id: rest[1]! };
      continue;
    }
    const [version, platform, letter, host, ...differs] = rest as [string, string, string, string, ...string[]];
    const file: Record<string, unknown> = {
      url: `https://${host}/${tool}-${version}.zip`, sha256: letter.repeat(64), size: 10, archive: 'zip', exe: `bin/${tool}.exe`,
    };
    for (const differ of differs) {
      const [key, value] = differ.split('=') as [string, string];
      file[key] = key === 'size' ? Number(value) : key === 'paths' ? value.split(',') : value;
    }
    ((held.versions[version] ??= { files: {} }).files)[platform] = file;
  }
  return JSON.stringify({ schema: 1, tools });
}

/** What a merge offers, in one line: each tool with the lists naming it, what each version downloads from, what is refused, the newest. */
function render(merged: MergedResources): string {
  const parts: string[] = [];
  for (const tool of merged.tools) {
    if (tool.lists.length === 0) continue;
    let text = `${tool.tool} [${tool.lists.join(', ')}]`;
    for (const version of tool.versions) text += ` ${version.version}=${version.urls.map((url) => new URL(url).host).join('+')}`;
    for (const refused of tool.refused) text += ` refused ${refused.version}(${refused.field})`;
    text += ` newest ${tool.newest ?? '-'}`;
    if (tool.licence) text += ` licence ${tool.licence.id} from ${tool.licenceFrom}`;
    parts.push(text);
  }
  for (const unknown of merged.unknown) parts.push(`${unknown.tool}? [${unknown.lists.join(', ')}]`);
  return parts.join(' | ');
}

/** [case, the first location, the second, the list built in (null: none), the platform, what the merge offers]. */
const MERGE_ROWS: [string, string | null, string | null, string | null, string | null, string][] = [
  ['nothing read', null, null, null, 'win-x64', ''],
  ['the list built in, alone', null, null, 'gh 2.62.0 win-x64 a maker.example', 'win-x64', 'gh [built in] 2.62.0=maker.example newest 2.62.0'],
  ['a location adds a newer version', 'gh 2.63.0 win-x64 b mirror.example', null, 'gh 2.62.0 win-x64 a maker.example', 'win-x64', 'gh [first, built in] 2.63.0=mirror.example 2.62.0=maker.example newest 2.63.0'],
  ['lists that agree under other addresses are mirrors, the person’s first', 'gh 2.62.0 win-x64 a mirror.example', null, 'gh 2.62.0 win-x64 a maker.example', 'win-x64', 'gh [first, built in] 2.62.0=mirror.example+maker.example newest 2.62.0'],
  ['one address named twice is tried once', 'gh 2.62.0 win-x64 a maker.example', null, 'gh 2.62.0 win-x64 a maker.example', 'win-x64', 'gh [first, built in] 2.62.0=maker.example newest 2.62.0'],
  ['lists that disagree on the hash refuse that version, and only that one', 'gh 2.62.0 win-x64 b other.example; gh 2.63.0 win-x64 c other.example', null, 'gh 2.62.0 win-x64 a maker.example', 'win-x64', 'gh [first, built in] 2.63.0=other.example refused 2.62.0(sha256) newest 2.63.0'],
  ['lists that disagree on the size', 'gh 2.62.0 win-x64 a other.example size=11', null, 'gh 2.62.0 win-x64 a maker.example', 'win-x64', 'gh [first, built in] refused 2.62.0(size) newest -'],
  ['lists that disagree on the archive', 'gh 2.62.0 win-x64 a other.example archive=tar.gz', null, 'gh 2.62.0 win-x64 a maker.example', 'win-x64', 'gh [first, built in] refused 2.62.0(archive) newest -'],
  ['lists that disagree on the executable', 'gh 2.62.0 win-x64 a other.example exe=gh.exe', null, 'gh 2.62.0 win-x64 a maker.example', 'win-x64', 'gh [first, built in] refused 2.62.0(exe) newest -'],
  ['lists that disagree on the folders for PATH', 'gh 2.62.0 win-x64 a other.example paths=bin,.', null, 'gh 2.62.0 win-x64 a maker.example', 'win-x64', 'gh [first, built in] refused 2.62.0(paths) newest -'],
  ['a third list refuses what two agreed on', 'gh 2.62.0 win-x64 a mirror.example', 'gh 2.62.0 win-x64 a other.example', 'gh 2.62.0 win-x64 b maker.example', 'win-x64', 'gh [first, second, built in] refused 2.62.0(sha256) newest -'],
  ['a disagreement on another platform refuses nothing here', 'gh 2.62.0 linux-x64 b other.example', null, 'gh 2.62.0 win-x64 a maker.example; gh 2.62.0 linux-x64 a maker.example', 'win-x64', 'gh [first, built in] 2.62.0=maker.example newest 2.62.0'],
  ['and refuses it on its own platform', 'gh 2.62.0 linux-x64 b other.example', null, 'gh 2.62.0 win-x64 a maker.example; gh 2.62.0 linux-x64 a maker.example', 'linux-x64', 'gh [first, built in] refused 2.62.0(sha256) newest -'],
  ['a version for another platform is not offered here', null, null, 'gh 2.62.0 linux-x64 a maker.example', 'win-x64', 'gh [built in] newest -'],
  ['the newest is by number, whichever list names it', 'gh 2.9.0 win-x64 a m.example', null, 'gh 2.10.0 win-x64 b m.example', 'win-x64', 'gh [first, built in] 2.10.0=m.example 2.9.0=m.example newest 2.10.0'],
  ['a refused version is never the newest', 'gh 2.63.0 win-x64 b x.example', 'gh 2.63.0 win-x64 c y.example', 'gh 2.62.0 win-x64 a maker.example', 'win-x64', 'gh [first, second, built in] 2.62.0=maker.example refused 2.63.0(sha256) newest 2.62.0'],
  ['a tool this build does not run offers nothing, and is named', 'bun 1.2.0 win-x64 a x.example', null, 'gh 2.62.0 win-x64 a maker.example', 'win-x64', 'gh [built in] 2.62.0=maker.example newest 2.62.0 | bun? [first]'],
  ['a list that does not read offers nothing', '!', null, 'gh 2.62.0 win-x64 a maker.example', 'win-x64', 'gh [built in] 2.62.0=maker.example newest 2.62.0'],
  ['the licence is the first list’s that names one', 'gh licence Apache-2.0; gh 2.63.0 win-x64 b m.example', null, 'gh licence MIT; gh 2.62.0 win-x64 a maker.example', 'win-x64', 'gh [first, built in] 2.63.0=m.example 2.62.0=maker.example newest 2.63.0 licence Apache-2.0 from first'],
  ['a list naming no licence leaves it to the next', 'gh 2.63.0 win-x64 b m.example', null, 'gh licence MIT; gh 2.62.0 win-x64 a maker.example', 'win-x64', 'gh [first, built in] 2.63.0=m.example 2.62.0=maker.example newest 2.63.0 licence MIT from built in'],
  ['no platform offers nothing', null, null, 'gh 2.62.0 win-x64 a maker.example', null, 'gh [built in] newest -'],
  ['every tool in the declared order', null, null, 'gh 2.62.0 win-x64 a m.example; git 2.56.0 win-x64 b m.example', 'win-x64', 'git [built in] 2.56.0=m.example newest 2.56.0 | gh [built in] 2.62.0=m.example newest 2.62.0'],
];

function merged(first: string | null, second: string | null, builtIn: string | null, platform: string | null): MergedResources {
  const lists = ([['first', first], ['second', second], ['built in', builtIn]] as const)
    .filter(([, spec]) => spec !== null)
    .map(([origin, spec]) => parseResources(listText(spec!), origin));
  return mergeResources(lists, platform);
}

test('lists merge as the driver merges them (§3.4)', () => {
  for (const [name, first, second, builtIn, platform, offers] of MERGE_ROWS) {
    assert.equal(render(merged(first, second, builtIn, platform)), offers, name);
  }
});

test('a refusal names both lists and both hashes, and nothing is fetched until one changes', () => {
  const result = merged('gh 2.62.0 win-x64 b other.example', null, 'gh 2.62.0 win-x64 a maker.example', 'win-x64');
  const gh = result.tools.find((tool) => tool.tool === 'gh')!;
  assert.deepEqual(gh.refused, [{
    version: '2.62.0', field: 'sha256', lists: ['first', 'built in'],
    problem: `GitHub CLI 2.62.0 for win-x64 is refused: first names its sha256 as ${'b'.repeat(64)}, and built in as `
      + `${'a'.repeat(64)}. Two lists that disagree on one download refuse it, and nothing is fetched until one of them changes`,
  }]);
  assert.deepEqual(result.notes, [gh.refused[0]!.problem]);
});

test('an offered version carries every address and every list that names it, and its file once', () => {
  const result = merged('gh 2.62.0 win-x64 a mirror.example', null, 'gh 2.62.0 win-x64 a maker.example', 'win-x64');
  assert.deepEqual(result.tools.find((tool) => tool.tool === 'gh')!.versions, [{
    version: '2.62.0', sha256: HASH, size: 10, archive: 'zip', exe: 'bin/gh.exe', paths: ['bin'],
    urls: ['https://mirror.example/gh-2.62.0.zip', 'https://maker.example/gh-2.62.0.zip'], lists: ['first', 'built in'],
  }]);
  assert.deepEqual(result.tools.map((tool) => tool.tool), TOOLS.map((tool) => tool.id), 'every declared tool, named or not');
});

test('a merge carries every list’s problem and notes, in read order', () => {
  const result = mergeResources([
    parseResources('not json', 'first'),
    parseResources(expand('{"schema":1,"tools":{"bun":{}}}'), 'built in'),
  ], 'win-x64');
  assert.equal(result.notes.length, 2);
  assert.ok(result.notes[0]!.startsWith('first is not readable JSON'), result.notes[0]);
  assert.equal(result.notes[1], 'built in names `bun`, which is not a tool this build runs: nothing of it is offered');
});

// ——— Where the lists are (§3.1, §3.3). Twins: `ToolResourcesTests.A_location_s_copy_is_named_as_the_cli_names_it`
// and `A_list_is_vouched_for_as_the_cli_says`.

/** [the address, its fetched copy's file name under `<home>/tools/locations/`]. */
const COPY_ROWS: [string, string][] = [
  ['https://example.org/daoris/resources.json', '83e03ee627324bedbd8aaea591e2a14753db2074954ef09fc29f5417fb769fe8.json'],
  ['HTTPS://EXAMPLE.ORG/resources.json', '8ef852c6746bfde60ad9bf0f202e88b307be50a9bd8007665d05c16a72ef0da8.json'],
  ['http://localhost:8080/resources.json', '676166cf6d6760078d2ef005ed1c378e9e067a9b97106e3e9b6bd8bbb5b4c703.json'],
  ['https://例え.jp/r.json', 'e2e2d45ac6cca9317d9ebea32cf02ce29eb338fe0926e0e2eecdd7111a125f9b.json'],
];

test('a location’s copy is named by the hash of its address, as the driver names it', () => {
  for (const [address, name] of COPY_ROWS) assert.equal(locationCopy('HOME', address), join('HOME', ...LOCATIONS_FOLDER, name), address);
});

/** [the address, what vouches for a list from it (§3.5)]. */
const INTEGRITY_ROWS: [string, string][] = [
  ['https://example.org/daoris/resources.json', 'example.org'],
  ['HTTPS://EXAMPLE.ORG/resources.json', 'example.org'],
  ['https://example.org:8443/resources.json', 'example.org:8443'],
  ['https://例え.jp/r.json', 'xn--r8jz45g.jp'],
  ['http://localhost:8080/resources.json', 'this machine'],
  ['http://127.0.0.1/resources.json', 'this machine'],
  ['http://[::1]:5177/resources.json', 'this machine'],
  ['https://localhost:8443/resources.json', 'this machine'],
];

test('a list is vouched for by its host, or by this machine, as the driver says', () => {
  for (const [address, integrity] of INTEGRITY_ROWS) assert.equal(integrityOf(address), integrity, address);
});

function scratchHome(name: string) {
  const fx = makeFixture(name);
  const home = join(fx.root, 'data');
  mkdirSync(home, { recursive: true });
  return { fx, home };
}

test('the lists are the person’s locations in order, then the one built in beside the home', () => {
  const { fx, home } = scratchHome('resources-lists');
  const first = 'https://mirror.example/resources.json';
  const second = 'http://localhost:5177/resources.json';
  writeFileSync(join(home, TOOLS_FILE), JSON.stringify({ locations: [first, second] }));
  const firstText = listText('gh 2.63.0 win-x64 b mirror.example');
  mkdirSync(join(home, ...LOCATIONS_FOLDER), { recursive: true });
  writeFileSync(locationCopy(home, first), firstText);
  mkdirSync(join(fx.root, 'app'), { recursive: true });
  const builtInText = listText('gh 2.62.0 win-x64 a maker.example');
  writeFileSync(join(fx.root, ...BUILT_IN_LAYOUT), builtInText);

  const lists = readLists(home);
  assert.deepEqual(lists.map((list) => [list.origin, list.integrity, list.exists]), [
    [first, 'mirror.example', true],
    [second, 'this machine', false],
    [BUILT_IN, 'built in', true],
  ]);
  assert.equal(lists[0]!.path, locationCopy(home, first));
  assert.equal(lists[0]!.sha256, createHash('sha256').update(firstText).digest('hex'));
  assert.deepEqual(lists[1]!.notes, [`${second} has not been fetched yet, so it names nothing`]);
  assert.equal(lists[2]!.path, builtInList(home));
  assert.equal(render(mergeResources(lists, 'win-x64')),
    `gh [${first}, ${BUILT_IN}] 2.63.0=mirror.example 2.62.0=maker.example newest 2.63.0`);
  fx.cleanup();
});

test('a home with no install beside it has no list built in, and says so', () => {
  const { fx, home } = scratchHome('resources-no-install');

  const lists = readLists(home);
  assert.deepEqual(lists.map((list) => [list.origin, list.exists, list.problem]), [[BUILT_IN, false, null]]);
  assert.deepEqual(lists[0]!.notes, [`no list is built in beside this home (${builtInList(home)}), so only the locations are read`]);
  assert.equal(builtInList(home), join(fx.root, ...BUILT_IN_LAYOUT));
  fx.cleanup();
});

test('a copy is hashed as its bytes, and read as its text', () => {
  const { fx, home } = scratchHome('resources-copy');
  const path = join(home, 'copy.json');
  writeFileSync(path, Buffer.concat([Buffer.from([0xef, 0xbb, 0xbf]), Buffer.from('{"schema":1,"tools":{}}\n')]));

  const list = readResourceFile(path, 'https://example.org/r.json');
  assert.deepEqual([list.exists, list.problem, list.integrity, list.sha256],
    [true, null, 'example.org', 'b649957df9c285c1ab0d01306767157f8a6a9e74d4718da94a7ea5c7bebfcd3c']);
  writeFileSync(path, '{"schema":1,"tools":{}}\n');
  assert.equal(readResourceFile(path, 'https://example.org/r.json').sha256, 'c5dd286d689d840e89162ec6c963a65cac731a88e08d4b8389c416d88185ac3b');
  fx.cleanup();
});

// ——— The list built in (§3.1, §3.2), and the evidence it was read from.

const BUILT_IN_SOURCE = join(here, '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver', RESOURCES_FILE);
const EVIDENCE = join(here, '..', '..', '..', 'docs', '2026-10-01-tools-resources-evidence.md');

test('the list built in reads whole, and names one version of every declared tool for win-x64', () => {
  const list = parseResources(readFileSync(BUILT_IN_SOURCE, 'utf8'), BUILT_IN);
  assert.equal(list.problem, null);
  assert.deepEqual(list.notes, []);
  assert.deepEqual([...list.tools.keys()].sort(), TOOLS.map((tool) => tool.id).sort());

  const result = mergeResources([list], 'win-x64');
  for (const tool of result.tools) {
    assert.equal(tool.versions.length, 1, `${tool.tool}: one version`);
    assert.deepEqual(tool.refused, [], tool.tool);
    assert.equal(tool.newest, tool.versions[0]!.version, tool.tool);
    assert.ok(tool.source?.startsWith('https://'), `${tool.tool}: its source`);
    assert.ok(tool.licence?.id && tool.licence.url?.startsWith('https://'), `${tool.tool}: its licence`);
    assert.equal(tool.versions[0]!.urls.length, 1, `${tool.tool}: the maker's one address`);
  }
});

test('every file the list built in names is recorded in its evidence, with its hash, size and address', () => {
  const list = parseResources(readFileSync(BUILT_IN_SOURCE, 'utf8'), BUILT_IN);
  const evidence = readFileSync(EVIDENCE, 'utf8');
  let files = 0;
  for (const [id, tool] of list.tools) {
    for (const [version, platforms] of tool.versions) {
      for (const [platform, file] of platforms) {
        files += 1;
        for (const fact of [file.url, file.sha256, String(file.size), file.exe]) {
          assert.ok(evidence.includes(fact), `${id} ${version} ${platform}: the evidence does not carry ${fact}`);
        }
      }
    }
  }
  assert.equal(files, TOOLS.length, 'one file for each tool, so the walk proved something');
});

// ——— The twin, held: the driver's tables are these tables, row for row and in this order.

const DRIVER_TABLES = join(here, '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'ToolResourcesTests.cs');

test('the driver’s tables are these tables, row for row and in this order', () => {
  const source = readFileSync(DRIVER_TABLES, 'utf8').replace(/\r\n/g, '\n');
  const rows = (method: string) => csharpRows(source, method, {}, 'ToolResourcesTests');

  assert.deepEqual(rows('A_platform_is_named_as_the_cli_names_it'), PLATFORM_ROWS);
  assert.deepEqual(rows('Versions_compare_as_the_cli_compares_them'), VERSION_ROWS);
  assert.deepEqual(rows('A_list_reads_as_the_cli_reads_it'), LIST_ROWS);
  assert.deepEqual(rows('A_file_reads_as_the_cli_reads_it'), FILE_ROWS);
  assert.deepEqual(rows('Lists_merge_as_the_cli_merges_them'), MERGE_ROWS);
  assert.deepEqual(rows('A_location_s_copy_is_named_as_the_cli_names_it'), COPY_ROWS);
  assert.deepEqual(rows('A_list_is_vouched_for_as_the_cli_says'), INTEGRITY_ROWS);
});
