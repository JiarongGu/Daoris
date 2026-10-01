import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { Fetcher } from '../src/channels.ts';
import { DaorisError, RefusalError } from '../src/errors.ts';
import {
  BUILT_IN, currentPlatform, locationCopy, mergeResources, parseResources, type ArchiveKind, type MergedResources, type OfferedVersion,
} from '../src/resources.ts';
import { releaseFetcher } from '../src/service.ts';
import {
  LOOK_BOUND_MS, RECORD_KEYS, REDIRECTS, STAGING, commandTool, deleteVersion, downloadVersion, downloadedVersions, lookLocations,
  planTool, unpackPackage, versionFolder, type ToolAction,
} from '../src/toolinstall.ts';
import { TOOLS_FILE, TOOLS_FOLDER, TOOL_PACKAGE, TOOL_RECORD, isAddress, readTools } from '../src/tools.ts';
import { buildArchive } from './_archives.ts';
import { driverRows as csharpRows } from './_csharp.ts';
import { makeFixture } from './_fixture.ts';

/**
 * A managed version, downloaded, verified, unpacked and laid out (TOOLS4, D121; the tools design §3.6, §3.7, §5):
 * the CLI's half of a TWIN with the driver's `ToolInstall.cs`. 🔴 `ToolInstallTests.cs` holds the same tables, row
 * for row and in the same order, and *the driver's tables are these tables*, below, reads that class's theories
 * and holds them to these, cell for cell. A row changed here is changed there, in the same commit.
 *
 * Every archive is built in the test from a row's words (`_archives.ts`), and nothing here reaches a network:
 * a download is served by a stand-in that answers as a host would.
 */

const here = dirname(fileURLToPath(import.meta.url));

// ——— An archive's refusals (§3.6). The twin is `ToolInstallTests.An_archive_unpacks_as_the_cli_unpacks_it`.

/** [case, the archive's kind, its entries, its shape, the executable the list names, the check that refuses it, or null]. */
const UNPACK_ROWS: [string, string, string, string, string, string | null][] = [
  ['a zip that unpacks', 'zip', 'bin/gh.exe=gh; bin/README=read me; docs/', '', 'bin/gh.exe', null],
  ['a zip of stored entries', 'zip', 'bin/gh.exe=gh!stored; docs/', '', 'bin/gh.exe', null],
  ['a zip written as zip64', 'zip', 'bin/gh.exe=gh; docs/', 'zip64', 'bin/gh.exe', null],
  ['a zip name from the root', 'zip', 'bin/gh.exe=gh; /gh.exe=x', '', 'bin/gh.exe', 'absolute'],
  ['a zip name on a drive', 'zip', 'C:/gh.exe=x', '', 'bin/gh.exe', 'absolute'],
  ['a zip name that climbs out', 'zip', 'bin/gh.exe=gh; ../gh.exe=x', '', 'bin/gh.exe', 'outside'],
  ['a zip name that climbs out partway', 'zip', 'bin/../../gh.exe=x', '', 'bin/gh.exe', 'outside'],
  ['a zip name that climbs out by a backslash', 'zip', '..\\gh.exe=x', '', 'bin/gh.exe', 'outside'],
  ['a zip stream name', 'zip', 'bin/gh.exe:hidden=x', '', 'bin/gh.exe', 'stream'],
  ['a zip symbolic link', 'zip', 'bin/gh.exe=/usr/bin/gh!symlink', '', 'bin/gh.exe', 'link'],
  ['a zip entry encrypted', 'zip', 'bin/gh.exe=gh!encrypted', '', 'bin/gh.exe', 'encrypted'],
  ['a zip method other than stored or deflate', 'zip', 'bin/gh.exe=gh!method12', '', 'bin/gh.exe', 'method'],
  ['a zip entry that fails its checksum', 'zip', 'bin/gh.exe=gh!badcrc', '', 'bin/gh.exe', 'checksum'],
  ['a zip with no end record', 'zip', 'bin/gh.exe=gh', 'cut-end', 'bin/gh.exe', 'truncated'],
  ['a zip cut in half', 'zip', 'bin/gh.exe=gh; bin/README=read me', 'cut-data', 'bin/gh.exe', 'truncated'],
  ['a zip without the executable', 'zip', 'bin/gh.exe=gh', '', 'bin/gh2.exe', 'exe'],
  ['a zip whose executable is a folder', 'zip', 'bin/gh.exe/', '', 'bin/gh.exe', 'exe'],
  ['a tar.gz that unpacks', 'tar.gz', 'bin/gh=gh; bin/README=read me; docs/', '', 'bin/gh', null],
  ['a tar.gz whose names are in extended headers', 'tar.gz', 'bin/gh=gh; docs/', 'pax', 'bin/gh', null],
  ['a tar.gz name from the root', 'tar.gz', '/gh=x', '', 'bin/gh', 'absolute'],
  ['a tar.gz name that climbs out', 'tar.gz', 'bin/gh=gh; ../gh=x', '', 'bin/gh', 'outside'],
  ['a tar.gz stream name', 'tar.gz', 'bin/gh:hidden=x', '', 'bin/gh', 'stream'],
  ['a tar.gz symbolic link', 'tar.gz', 'bin/gh=/usr/bin/gh!symlink', '', 'bin/gh', 'link'],
  ['a tar.gz hard link', 'tar.gz', 'bin/gh=bin/other!hardlink', '', 'bin/gh', 'link'],
  ['a tar.gz entry neither file nor folder', 'tar.gz', 'bin/gh=x!fifo', '', 'bin/gh', 'entry'],
  ['a tar.gz header that fails its checksum', 'tar.gz', 'bin/gh=gh!badcrc', '', 'bin/gh', 'checksum'],
  ['a tar.gz with no end', 'tar.gz', 'bin/gh=gh', 'cut-end', 'bin/gh', 'truncated'],
  ['a tar.gz cut inside an entry', 'tar.gz', 'bin/gh=gh', 'cut-data', 'bin/gh', 'truncated'],
  ['a tar.gz whose gzip is cut before its trailer', 'tar.gz', 'bin/gh=gh', 'cut-gzip', 'bin/gh', 'truncated'],
  ['a tar.gz without the executable', 'tar.gz', 'bin/gh=gh', '', 'bin/gh2', 'exe'],
  ['an archive kind nobody unpacks', '7z', 'bin/gh.exe=gh', '', 'bin/gh.exe', 'archive'],
];

test('an archive unpacks, or is refused by its check, as the driver unpacks it (§3.6)', async () => {
  for (const [index, [name, kind, entries, shape, exe, check]] of UNPACK_ROWS.entries()) {
    const fx = makeFixture(`toolinstall-unpack-${index}`);
    const archive = join(fx.root, 'download');
    writeFileSync(archive, buildArchive(kind, entries, shape));
    // Two folders down, so a name that climbs out one or two lands somewhere this can look.
    const into = join(fx.root, 'a', 'b', 'package');

    let refused: unknown = null;
    let file: string | null = null;
    try {
      file = await unpackPackage(archive, kind, into, exe);
    } catch (error) {
      refused = error;
    }

    if (check === null) {
      assert.equal(refused, null, `${name}: ${(refused as Error | null)?.message}`);
      assert.equal(file, join(into, ...exe.split('/')), name);
      assert.equal(readFileSync(file!, 'utf8'), 'gh', `${name}: the executable's bytes as the archive holds them`);
    } else {
      assert.ok(refused instanceof RefusalError, `${name}: ${String(refused)}`);
      assert.equal(refused.check, check, `${name}: ${refused.message}`);
    }
    for (const outside of [join(fx.root, 'gh.exe'), join(fx.root, 'a', 'gh.exe'), join(fx.root, 'a', 'b', 'gh.exe'), join(fx.root, 'a', 'b', 'gh')]) {
      assert.equal(existsSync(outside), false, `${name}: nothing lands outside the folder (${outside})`);
    }
    fx.cleanup();
  }
});

test('a zip unpacks whole, top folder included, and what may run stays runnable', async () => {
  const fx = makeFixture('toolinstall-zip-whole');
  const archive = join(fx.root, 'download');
  writeFileSync(archive, buildArchive('zip', 'gh_2.62.0/; gh_2.62.0/bin/gh.exe=gh; gh_2.62.0/LICENSE=MIT', ''));

  const into = join(fx.root, 'package');
  const file = await unpackPackage(archive, 'zip', into, 'gh_2.62.0/bin/gh.exe');
  assert.equal(file, join(into, 'gh_2.62.0', 'bin', 'gh.exe'));
  assert.equal(readFileSync(join(into, 'gh_2.62.0', 'LICENSE'), 'utf8'), 'MIT');
  if (process.platform !== 'win32') assert.ok(statSync(file).mode & 0o100, 'the executable is runnable');
  fx.cleanup();
});

test('a refusal says what it found, in a sentence', async () => {
  const fx = makeFixture('toolinstall-refusal-words');
  const archive = join(fx.root, 'download');
  writeFileSync(archive, buildArchive('zip', 'bin/gh.exe=gh!badcrc', ''));
  mkdirSync(join(fx.root, 'package'), { recursive: true });

  await assert.rejects(unpackPackage(archive, 'zip', join(fx.root, 'package'), 'bin/gh.exe'),
    /^RefusalError: the archive's `bin\/gh\.exe` fails its own check — 2 bytes with CRC-32 [0-9a-f]{8}, where it states 2 with [0-9a-f]{8}$/);
  await assert.rejects(unpackPackage(archive, 'rar', join(fx.root, 'package'), 'bin/gh.exe'),
    /`rar` is not an archive this build unpacks — zip or tar\.gz/);
  fx.cleanup();
});

// ——— A scratch home, its lists, and a host that answers as a stand-in.

/** The placeholder a row writes where a whole path goes; each side spells its own. */
const WHOLE = 'WHOLE';

function scratch(name: string) {
  const fx = makeFixture(name);
  const home = join(fx.root, 'data');
  mkdirSync(home, { recursive: true });
  return { fx, home, whole: join(fx.root, 'bin', process.platform === 'win32' ? 'gh.exe' : 'gh') };
}

/** A downloaded version as the resolution finds it: its record, naming an executable that is there. */
function downloaded(home: string, tool: string, version: string): void {
  const folder = versionFolder(home, tool, version);
  mkdirSync(join(folder, TOOL_PACKAGE, 'bin'), { recursive: true });
  writeFileSync(join(folder, TOOL_PACKAGE, 'bin', `${tool}.exe`), tool);
  writeFileSync(join(folder, TOOL_RECORD), JSON.stringify({ exe: `bin/${tool}.exe` }));
}

/**
 * A list, spelled short — the resources tests' `listText`, and the driver's `ListText`: downloads separated by `; `,
 * each `<tool> <version> <platform> <hash letter> <host>`.
 */
function listText(spec: string): string {
  const tools: Record<string, { versions: Record<string, { files: Record<string, Record<string, unknown>> }> }> = {};
  for (const entry of spec.split('; ').filter(Boolean)) {
    const [tool, version, platform, letter, host] = entry.split(' ') as [string, string, string, string, string];
    ((tools[tool] ??= { versions: {} }).versions[version] ??= { files: {} }).files[platform] = {
      url: `https://${host}/${tool}-${version}.zip`, sha256: letter.repeat(64), size: 10, archive: 'zip', exe: `bin/${tool}.exe`,
    };
  }
  return JSON.stringify({ schema: 1, tools });
}

/** The lists a row names, separated by ` | `: a location called `first`, then the list built in, always last. */
function mergedOf(spec: string): MergedResources {
  const specs = spec === '' ? [] : spec.split(' | ');
  return mergeResources(specs.map((one, at) => parseResources(listText(one), at === specs.length - 1 ? BUILT_IN : 'first')), 'win-x64');
}

type Answer = Buffer | number | { location: string };

/**
 * A host, as a stand-in: each address answers bytes, a status, or a redirect. It is handed to the one fetcher
 * `service.ts` builds, so a hop is held to the rule exactly as a real download's is — and no socket is opened.
 */
function host(answers: Map<string, Answer>): Fetcher {
  return releaseFetcher({ hop: isAddress, redirects: REDIRECTS }, async (url) => {
    const answer = answers.get(url);
    if (answer === undefined) return new Response(null, { status: 404 });
    if (typeof answer === 'number') return new Response(null, { status: answer });
    if (Buffer.isBuffer(answer)) return new Response(answer);
    return new Response(null, { status: 302, headers: { location: answer.location } });
  });
}

test('the fetcher holds every hop to the rule, follows ten redirects and no more, and bounds a small read', async () => {
  const loop = releaseFetcher({ hop: isAddress, redirects: REDIRECTS },
    async (url) => new Response(null, { status: 302, headers: { location: `${url}x` } }));
  await assert.rejects(loop.bytes('https://a.example/r'),
    (error) => error instanceof RefusalError && error.check === 'unreachable' && /redirected more than 10 times/.test(error.message));

  const slow = releaseFetcher({ hop: isAddress, bound: 20 }, (_url, init) => new Promise((_resolve, reject) => {
    init.signal!.addEventListener('abort', () => reject(init.signal!.reason));
  }));
  await assert.rejects(slow.bytes('https://a.example/r'),
    (error) => error instanceof RefusalError && error.check === 'unreachable' && /a\.example did not answer within/.test(error.message));

  const seen: RequestInit[] = [];
  const plain = releaseFetcher({}, async (_url, init) => {
    seen.push(init);
    return new Response('ok');
  });
  assert.equal((await plain.bytes('http://example.org/anything'))?.toString(), 'ok', 'with no rule, nothing is held to one');
  assert.equal(seen[0]!.redirect, 'follow', 'with no rule, a channel is fetched as it always was');
});

// ——— Which version, and whether anything is fetched (§3.7). The twin is `ToolInstallTests.A_plan_is_made_as_the_cli_makes_it`.

const LISTS = 'gh 2.62.0 win-x64 a m.example; gh 2.63.0 win-x64 b m.example';

/**
 * Every row is `gh`. [case, tools.json (null: none), the lists, the versions downloaded, the action, the version
 * asked, the version planned, whether it is fetched, a fragment of why there is nothing to do, the check that refuses it].
 */
const PLAN_ROWS: [string, string | null, string, string, string, string | null, string | null, boolean, string | null, string | null][] = [
  ['download the newest the lists name', null, LISTS, '', 'download', null, '2.63.0', true, null, null],
  ['download a version named', null, LISTS, '', 'download', '2.62.0', '2.62.0', true, null, null],
  ['download a version already downloaded', null, LISTS, '2.62.0', 'download', '2.62.0', '2.62.0', false, null, null],
  ['download a version no list names', null, LISTS, '', 'download', '2.64.0', null, false, null, 'unknown'],
  ['download a version that is not exact', null, LISTS, '', 'download', 'latest', null, false, null, 'version'],
  ['download with no list naming any', null, '', '', 'download', null, null, false, null, 'unknown'],
  ['download a version two lists disagree on', null, 'gh 2.62.0 win-x64 b o.example | gh 2.62.0 win-x64 a m.example', '', 'download', '2.62.0', null, false, null, 'conflict'],
  ['use a version downloaded that no list names', null, '', '2.61.0', 'use', '2.61.0', '2.61.0', false, null, null],
  ['use the newest, not downloaded', null, LISTS, '', 'use', null, '2.63.0', true, null, null],
  ['update a managed tool to the newest', '{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}', LISTS, '2.62.0', 'update', null, '2.63.0', true, null, null],
  ['update at the newest, downloaded', '{"tools":{"gh":{"use":"managed","version":"2.63.0"}}}', LISTS, '2.63.0', 'update', null, null, false, 'nothing to do', null],
  ['update at the newest, not downloaded', '{"tools":{"gh":{"use":"managed","version":"2.63.0"}}}', LISTS, '', 'update', null, '2.63.0', true, null, null],
  ['update never moves a tool back', '{"tools":{"gh":{"use":"managed","version":"2.64.0"}}}', LISTS, '2.64.0', 'update', null, null, false, 'never moves a tool back', null],
  ['update the system’s', null, LISTS, '', 'update', null, null, false, null, 'machine'],
  ['update a file you name', '{"tools":{"gh":{"use":"file","file":"WHOLE"}}}', LISTS, '', 'update', null, null, false, null, 'machine'],
  ['update an entry that does not read', '{"tools":{"gh":{"use":"managed"}}}', LISTS, '', 'update', null, null, false, null, 'file'],
  ['update with no list naming any', '{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}', '', '2.62.0', 'update', null, null, false, null, 'unknown'],
];

test('a plan says which version, and whether it is fetched, as the driver plans it (§3.7)', () => {
  for (const [index, [name, tools, lists, has, action, version, planned, fetch, nothing, check]] of PLAN_ROWS.entries()) {
    const { fx, home, whole } = scratch(`toolinstall-plan-${index}`);
    if (tools !== null) writeFileSync(join(home, TOOLS_FILE), tools.replace(WHOLE, JSON.stringify(whole).slice(1, -1)));
    for (const each of has.split(',').filter(Boolean)) downloaded(home, 'gh', each);

    const plan = planTool(home, mergedOf(lists), 'gh', action as ToolAction, version);
    assert.equal(plan.version, planned, `${name}: ${plan.problem ?? plan.nothing}`);
    assert.equal(plan.fetch, fetch, name);
    assert.equal(plan.offered !== null, fetch, `${name}: what is fetched is offered`);
    if (nothing === null) assert.equal(plan.nothing, null, `${name}: ${plan.nothing}`);
    else assert.ok(plan.nothing?.includes(nothing), `${name}: ${plan.nothing}`);
    assert.equal(plan.check, check, `${name}: ${plan.problem}`);
    assert.equal(plan.problem !== null, check !== null, name);
    fx.cleanup();
  }
});

test('a plan’s refusals say what to do next', () => {
  const { fx, home } = scratch('toolinstall-plan-words');
  assert.equal(planTool(home, mergedOf(''), 'gh', 'download', null).problem,
    'no list names a version of GitHub CLI for win-x64 — `daoris tool look` fetches the locations, and '
    + '`daoris tool locations add <address>` adds one');
  assert.equal(planTool(home, mergedOf(LISTS), 'gh', 'update', null).problem,
    'GitHub CLI runs the system\'s: its updates are the machine\'s, not Daoris\'s — `daoris tool use gh managed` keeps a '
    + 'version in the home');
  assert.equal(planTool(home, mergedOf(LISTS), 'gh', 'download', '2.64.0').problem, 'no list names GitHub CLI 2.64.0 for win-x64');
  fx.cleanup();
});

// ——— A download (§3.6). The twin is `ToolInstallTests.A_download_is_verified_as_the_cli_verifies_it`.

/**
 * [case, the archive's kind, the addresses the lists name and how each answers, the check that refuses it, the host
 * that served it]. An address is `<base>=<answer>`, the file at `<base>/gh.<kind>`; an answer is `ok` (the bytes the
 * list names), `other` (as many bytes, one changed), `longer` (one byte more), `missing` (404), `to-https` (a
 * redirect to `https://cdn.example`, which serves it) or `to-http` (a redirect to `http://elsewhere.example`, which
 * would serve it).
 */
const DOWNLOAD_ROWS: [string, string, string, string | null, string | null][] = [
  ['one address, the bytes it names', 'zip', 'https://maker.example=ok', null, 'maker.example'],
  ['a tar.gz from one address', 'tar.gz', 'https://maker.example=ok', null, 'maker.example'],
  ['bytes of another hash', 'zip', 'https://maker.example=other', 'hash', null],
  ['bytes of another size', 'zip', 'https://maker.example=longer', 'size', null],
  ['nothing at the address', 'zip', 'https://maker.example=missing', 'unreachable', null],
  ['an address over http to another host', 'zip', 'http://maker.example=ok', 'address', null],
  ['an address over http to this machine', 'zip', 'http://127.0.0.1:8080=ok', null, '127.0.0.1:8080'],
  ['a redirect over https', 'zip', 'https://maker.example=to-https', null, 'maker.example'],
  ['a redirect to http on another host', 'zip', 'https://maker.example=to-http', 'address', null],
  ['a mirror with other bytes, then the maker', 'zip', 'https://mirror.example=other https://maker.example=ok', null, 'maker.example'],
  ['a mirror with nothing, then the maker', 'zip', 'https://mirror.example=missing https://maker.example=ok', null, 'maker.example'],
  ['every address fails, and the last says why', 'zip', 'https://mirror.example=missing https://maker.example=other', 'hash', null],
  ['an archive kind nobody unpacks, before a byte is fetched', '7z', 'https://maker.example=ok', 'archive', null],
];

/** The archive a download row serves, and the offer a list makes of it. */
function offerOf(kind: string, addresses: string): { offered: OfferedVersion; answers: Map<string, Answer> } {
  const bytes = buildArchive(kind, kind === 'tar.gz' ? 'bin/gh=gh' : 'bin/gh.exe=gh', '');
  const file = `gh.${kind}`;
  const answers = new Map<string, Answer>();
  const urls: string[] = [];
  for (const address of addresses.split(' ')) {
    const [base, answer] = address.split('=') as [string, string];
    const url = `${base}/${file}`;
    urls.push(url);
    const changed = Buffer.from(bytes);
    changed.writeUInt8(changed.readUInt8(changed.length - 1) ^ 0xff, changed.length - 1);
    answers.set(url, answer === 'ok' ? bytes : answer === 'other' ? changed : answer === 'longer' ? Buffer.concat([bytes, Buffer.from([0])])
      : answer === 'missing' ? 404 : { location: answer === 'to-https' ? `https://cdn.example/${file}` : `http://elsewhere.example/${file}` });
  }
  answers.set(`https://cdn.example/${file}`, bytes);
  answers.set(`http://elsewhere.example/${file}`, bytes);

  return {
    offered: {
      version: '2.62.0', sha256: createHash('sha256').update(bytes).digest('hex'), size: bytes.length, archive: kind as ArchiveKind,
      exe: kind === 'tar.gz' ? 'bin/gh' : 'bin/gh.exe', paths: ['bin'], urls, lists: [BUILT_IN],
    },
    answers,
  };
}

test('a download is verified, staged and laid out, or refused leaving nothing, as the driver downloads it (§3.6)', async () => {
  for (const [index, [name, kind, addresses, check, servedBy]] of DOWNLOAD_ROWS.entries()) {
    const { fx, home } = scratch(`toolinstall-download-${index}`);
    const { offered, answers } = offerOf(kind, addresses);
    const folder = versionFolder(home, 'gh', '2.62.0');

    let refused: unknown = null;
    try {
      await downloadVersion({ home, tool: 'gh', offered, platform: 'win-x64', fetcher: host(answers), write: () => {} });
    } catch (error) {
      refused = error;
    }

    if (check === null) {
      assert.equal(refused, null, `${name}: ${(refused as Error | null)?.message}`);
      const record = JSON.parse(readFileSync(join(folder, TOOL_RECORD), 'utf8')) as Record<string, unknown>;
      assert.equal(new URL(record.url as string).host, servedBy, name);
      assert.equal(readFileSync(join(folder, TOOL_PACKAGE, ...offered.exe.split('/')), 'utf8'), 'gh', name);
      assert.equal(existsSync(join(home, TOOLS_FILE)), false, `${name}: nothing switches`);
    } else {
      assert.ok(refused instanceof RefusalError, `${name}: ${String(refused)}`);
      assert.equal(refused.check, check, `${name}: ${refused.message}`);
      const tool = join(home, TOOLS_FOLDER, 'gh');
      assert.deepEqual(existsSync(tool) ? readdirSync(tool) : [], [], `${name}: nothing under tools/gh`);
    }
    assert.equal(existsSync(`${folder}${STAGING}`), false, `${name}: no staging is left`);
    fx.cleanup();
  }
});

test('the record names what the list named, where it came from, and when', async () => {
  const { fx, home } = scratch('toolinstall-record');
  const { offered, answers } = offerOf('zip', 'https://mirror.example=missing https://maker.example=ok');
  offered.lists = ['https://lists.example/r.json', BUILT_IN];
  const lines: string[] = [];
  const exe = await downloadVersion({ home, tool: 'gh', offered, platform: 'win-x64', fetcher: host(answers), write: (line) => lines.push(line) });

  const folder = versionFolder(home, 'gh', '2.62.0');
  assert.equal(exe, join(folder, TOOL_PACKAGE, 'bin', 'gh.exe'));
  const text = readFileSync(join(folder, TOOL_RECORD), 'utf8');
  const record = JSON.parse(text) as Record<string, unknown>;
  assert.deepEqual(Object.keys(record), [...RECORD_KEYS]);
  assert.deepEqual({ ...record, at: null }, {
    tool: 'gh', version: '2.62.0', platform: 'win-x64', sha256: offered.sha256, size: offered.size, archive: 'zip',
    url: 'https://maker.example/gh.zip', lists: ['https://lists.example/r.json', BUILT_IN], exe: 'bin/gh.exe', paths: ['bin'], at: null,
  });
  assert.match(record.at as string, /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$/);
  assert.ok(text.endsWith('}\n') && !text.includes('\r'), 'two-space JSON, LF, a final newline');
  assert.deepEqual(readdirSync(folder).sort(), [TOOL_PACKAGE, TOOL_RECORD], 'the archive itself is not kept');
  assert.ok(lines.some((line) => line.includes('https://mirror.example/gh.zip') && line.includes('nothing there')), lines.join('\n'));
  assert.ok(lines.some((line) => line.includes(offered.sha256)), lines.join('\n'));
  assert.deepEqual(downloadedVersions(home, 'gh'), ['2.62.0']);
  fx.cleanup();
});

test('a version already downloaded fetches nothing, and one that runs nothing is replaced', async () => {
  const { fx, home } = scratch('toolinstall-already');
  const { offered, answers } = offerOf('zip', 'https://maker.example=ok');
  await downloadVersion({ home, tool: 'gh', offered, platform: 'win-x64', fetcher: host(answers), write: () => {} });

  const silent = host(new Map());
  const lines: string[] = [];
  await downloadVersion({ home, tool: 'gh', offered, platform: 'win-x64', fetcher: silent, write: (line) => lines.push(line) });
  assert.ok(lines.some((line) => line.includes('already downloaded')), lines.join('\n'));

  // A folder whose executable went is no proof: it is replaced by a verified one.
  const folder = versionFolder(home, 'gh', '2.62.0');
  writeFileSync(join(folder, TOOL_RECORD), '{"exe":"bin/missing.exe"}');
  await downloadVersion({ home, tool: 'gh', offered, platform: 'win-x64', fetcher: host(answers), write: () => {} });
  assert.equal(JSON.parse(readFileSync(join(folder, TOOL_RECORD), 'utf8')).exe, 'bin/gh.exe');
  fx.cleanup();
});

// ——— Deleting a version (§3.6). The twin is `ToolInstallTests.A_version_is_deleted_as_the_cli_deletes_it`.

/** [case, tools.json (null: none), whether the version is downloaded, the version asked, the check that refuses it]. */
const DELETE_ROWS: [string, string | null, boolean, string, string | null][] = [
  ['a version nothing uses', null, true, '2.62.0', null],
  ['a version not downloaded', null, false, '2.62.0', 'missing'],
  ['the version the tool runs', '{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}', true, '2.62.0', 'in-use'],
  ['another version than the one it runs', '{"tools":{"gh":{"use":"managed","version":"2.63.0"}}}', true, '2.62.0', null],
  ['beside a file that does not read', 'not json', true, '2.62.0', 'file'],
  ['a version that climbs out', null, false, '../2.62.0', 'version'],
];

test('a version is deleted only when nothing uses it, as the driver deletes it', () => {
  for (const [index, [name, tools, has, version, check]] of DELETE_ROWS.entries()) {
    const { fx, home } = scratch(`toolinstall-delete-${index}`);
    if (tools !== null) writeFileSync(join(home, TOOLS_FILE), tools);
    if (has) downloaded(home, 'gh', '2.62.0');

    let refused: unknown = null;
    try {
      deleteVersion(home, 'gh', version);
    } catch (error) {
      refused = error;
    }
    if (check === null) {
      assert.equal(refused, null, `${name}: ${(refused as Error | null)?.message}`);
      assert.equal(existsSync(versionFolder(home, 'gh', version)), false, name);
    } else {
      assert.ok(refused instanceof RefusalError, `${name}: ${String(refused)}`);
      assert.equal(refused.check, check, `${name}: ${refused.message}`);
      if (has) assert.ok(existsSync(versionFolder(home, 'gh', '2.62.0')), `${name}: the version stays`);
    }
    fx.cleanup();
  }
});

// ——— Look for updates (§3.7). The twin is `ToolInstallTests.A_location_is_looked_at_as_the_cli_looks`.

const LOCATION = 'https://lists.example/resources.json';
const OLD_LIST = listText('gh 2.62.0 win-x64 a m.example');
const NEW_LIST = listText('gh 2.63.0 win-x64 b m.example');

/**
 * [case, what the location answers (`list`, `unread`: schema 2, `not json`, `missing`: 404, `to-http`: a redirect to
 * http on another host), whether a copy was kept before, the outcome, the copy after (`new`, `old`, `none`)].
 */
const LOOK_ROWS: [string, string, boolean, string, string][] = [
  ['a list, never fetched before', 'list', false, 'fetched', 'new'],
  ['a list, over an older copy', 'list', true, 'fetched', 'new'],
  ['a list this build does not read, over a copy', 'unread', true, 'unread', 'old'],
  ['a list this build does not read, never fetched', 'unread', false, 'unread', 'none'],
  ['text that is not JSON, over a copy', 'not json', true, 'unread', 'old'],
  ['nothing there, over a copy', 'missing', true, 'failed', 'old'],
  ['nothing there, never fetched', 'missing', false, 'failed', 'none'],
  ['a redirect to http on another host', 'to-http', true, 'failed', 'old'],
];

test('a location is looked at as the driver looks: a list that reads is kept, anything else keeps the last copy', async () => {
  for (const [index, [name, answers, before, outcome, after]] of LOOK_ROWS.entries()) {
    const { fx, home } = scratch(`toolinstall-look-${index}`);
    writeFileSync(join(home, TOOLS_FILE), JSON.stringify({ locations: [LOCATION] }));
    const copy = locationCopy(home, LOCATION);
    if (before) {
      mkdirSync(dirname(copy), { recursive: true });
      writeFileSync(copy, OLD_LIST);
    }
    const served = new Map<string, Answer>([['http://elsewhere.example/resources.json', Buffer.from(NEW_LIST)]]);
    if (answers !== 'missing') {
      served.set(LOCATION, answers === 'list' ? Buffer.from(NEW_LIST) : answers === 'unread' ? Buffer.from('{"schema":2}')
        : answers === 'not json' ? Buffer.from('not json') : { location: 'http://elsewhere.example/resources.json' });
    }

    const looks = await lookLocations(home, host(served), 'win-x64');
    assert.deepEqual(looks.map((look) => [look.address, look.outcome]), [[LOCATION, outcome]], `${name}: ${looks[0]?.sentence}`);
    const kept = existsSync(copy) ? readFileSync(copy, 'utf8') : null;
    assert.equal(kept, after === 'new' ? NEW_LIST : after === 'old' ? OLD_LIST : null, name);
    fx.cleanup();
  }
});

test('a look says what a list added and dropped, and the age of a copy it kept', async () => {
  const { fx, home } = scratch('toolinstall-look-words');
  writeFileSync(join(home, TOOLS_FILE), JSON.stringify({ locations: [LOCATION] }));
  const copy = locationCopy(home, LOCATION);
  mkdirSync(dirname(copy), { recursive: true });
  writeFileSync(copy, OLD_LIST);

  const [fetched] = await lookLocations(home, host(new Map([[LOCATION, Buffer.from(NEW_LIST)]])), 'win-x64');
  assert.deepEqual([fetched!.added, fetched!.dropped], [['gh 2.63.0'], ['gh 2.62.0']]);
  assert.match(fetched!.sentence, /^https:\/\/lists\.example\/resources\.json: fetched — 1 version for win-x64; added gh 2\.63\.0; dropped gh 2\.62\.0$/);

  const [failed] = await lookLocations(home, host(new Map()), 'win-x64');
  assert.match(failed!.sentence, /^https:\/\/lists\.example\/resources\.json could not be fetched \(nothing there\); its copy from .+ ago is kept$/);
  fx.cleanup();
});

// ——— The verb: `daoris tool download|use … managed|update|delete|locations|look`, against a stand-in host.

/** One verb, as the dispatcher runs it, with the home and the host given; a refusal is caught with its exit code. */
async function verb(argv: string[], home: string, fetcher: Fetcher | null): Promise<{ code: number; out: string[] }> {
  const out: string[] = [];
  try {
    const code = await commandTool({ root: process.cwd(), argv, write: (line) => out.push(line), packageRoot: process.cwd() }, fetcher,
      { ...process.env, DAORIS_HOME: home, PATH: '' });
    return { code, out };
  } catch (error) {
    assert.ok(error instanceof DaorisError, String(error));
    return { code: error.exitCode, out: [...out, `daoris: ${error.message}`] };
  }
}

test('the verb: a location added and looked at, a version used, updated, deleted and downloaded again', async (t) => {
  const platform = currentPlatform();
  if (platform === null) {
    t.skip('this machine is not a platform a list may name');
    return;
  }
  const { fx, home } = scratch('toolinstall-verb');
  const bytes = buildArchive('zip', 'bin/gh.exe=gh', '');
  const sha256 = createHash('sha256').update(bytes).digest('hex');
  const listOf = (...versions: string[]) => Buffer.from(JSON.stringify({
    schema: 1,
    tools: {
      gh: {
        source: 'https://maker.example/releases', licence: { id: 'MIT', url: 'https://maker.example/LICENSE' },
        versions: Object.fromEntries(versions.map((version) => [version, {
          files: { [platform]: { url: `https://maker.example/gh-${version}.zip`, sha256, size: bytes.length, archive: 'zip', exe: 'bin/gh.exe' } },
        }])),
      },
    },
  }));
  const served = new Map<string, Answer>([
    ['https://maker.example/gh-2.62.0.zip', bytes], ['https://maker.example/gh-2.63.0.zip', bytes], [LOCATION, listOf('2.62.0')],
  ]);
  const fetcher = host(served);
  const said = (result: { out: string[] }, text: string) => assert.ok(result.out.some((line) => line.includes(text)), `${text}:\n${result.out.join('\n')}`);

  let result = await verb(['locations', 'add', LOCATION], home, null);
  assert.equal(result.code, 0);
  said(result, 'is added, read before the list built in');

  result = await verb(['look'], home, fetcher);
  assert.equal(result.code, 0, result.out.join('\n'));
  said(result, `${LOCATION}: fetched — 1 version for ${platform}; added gh 2.62.0`);
  said(result, `gh    GitHub CLI runs the system's; the lists name 2.62.0 (${LOCATION})`);

  result = await verb(['use', 'gh', 'managed'], home, fetcher);
  assert.equal(result.code, 0, result.out.join('\n'));
  said(result, `GitHub CLI 2.62.0 for ${platform} — `);
  said(result, 'MIT (https://maker.example/LICENSE), from https://maker.example/releases');
  said(result, 'GitHub CLI is set to managed 2.62.0');
  const first = join(versionFolder(home, 'gh', '2.62.0'), TOOL_PACKAGE, 'bin', 'gh.exe');
  assert.deepEqual(await verb(['path', 'gh'], home, null), { code: 0, out: [first] });

  served.set(LOCATION, listOf('2.62.0', '2.63.0'));
  result = await verb(['look'], home, fetcher);
  said(result, 'added gh 2.63.0');
  said(result, 'runs managed 2.62.0; the lists name 2.63.0');
  said(result, '`daoris tool update gh` moves to it');

  result = await verb(['update', 'gh'], home, fetcher);
  assert.equal(result.code, 0, result.out.join('\n'));
  said(result, 'GitHub CLI is set to managed 2.63.0 (it was 2.62.0)');
  result = await verb(['update', 'gh'], home, fetcher);
  assert.equal(result.code, 0);
  said(result, 'GitHub CLI 2.63.0 is the newest the lists name, and it is downloaded — nothing to do');

  result = await verb(['delete', 'gh', '2.63.0'], home, null);
  assert.equal(result.code, 1, 'the version in use is refused');
  said(result, 'GitHub CLI runs 2.63.0');
  result = await verb(['delete', 'gh', '2.62.0'], home, null);
  assert.equal(result.code, 0);
  assert.equal(existsSync(versionFolder(home, 'gh', '2.62.0')), false);

  result = await verb(['download', 'gh', '2.62.0'], home, fetcher);
  assert.equal(result.code, 0, result.out.join('\n'));
  said(result, 'GitHub CLI 2.62.0 is downloaded and verified — nothing switches');
  assert.equal(readTools(home).entries.gh!.version, '2.63.0', 'a download switches nothing');
  said(await verb(['list'], home, null), '        downloaded: 2.63.0, 2.62.0');
  said(await verb(['locations'], home, null), `  1. ${LOCATION} — fetched `);

  result = await verb(['locations', 'remove', LOCATION], home, null);
  said(result, 'its versions are no longer offered, and a version already downloaded stays');
  result = await verb(['use', 'gh', 'managed', '2.62.0'], home, null);
  assert.equal(result.code, 0, `a version downloaded is used with no list and no network:\n${result.out.join('\n')}`);
  fx.cleanup();
});

test('a download whose bytes are not the list’s is exit 1, and writes nothing', async (t) => {
  const platform = currentPlatform();
  if (platform === null) {
    t.skip('this machine is not a platform a list may name');
    return;
  }
  const { fx, home } = scratch('toolinstall-verb-refused');
  writeFileSync(join(home, TOOLS_FILE), JSON.stringify({ locations: [LOCATION] }));
  mkdirSync(dirname(locationCopy(home, LOCATION)), { recursive: true });
  writeFileSync(locationCopy(home, LOCATION), JSON.stringify({
    schema: 1,
    tools: { gh: { versions: { '2.62.0': { files: { [platform]: { url: 'https://maker.example/gh.zip', sha256: 'a'.repeat(64), size: 3, archive: 'zip', exe: 'bin/gh.exe' } } } } } },
  }));
  const before = readFileSync(join(home, TOOLS_FILE), 'utf8');

  const result = await verb(['use', 'gh', 'managed', '2.62.0'], home, host(new Map([['https://maker.example/gh.zip', Buffer.from('abc')]])));
  assert.equal(result.code, 1, result.out.join('\n'));
  assert.match(result.out.at(-1)!, /^daoris: GitHub CLI 2\.62\.0 was not downloaded: https:\/\/maker\.example\/gh\.zip served bytes whose SHA-256 is [0-9a-f]{64}, and the list says a{64}\. Nothing was kept$/);
  assert.equal(readFileSync(join(home, TOOLS_FILE), 'utf8'), before, 'nothing was written');
  assert.deepEqual(readdirSync(join(home, TOOLS_FOLDER)).sort(), ['locations'], 'nothing under tools/gh');
  fx.cleanup();
});

// ——— The twin, held: the driver's tables are these tables, row for row and in this order.

const DRIVER_TABLES = join(here, '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'ToolInstallTests.cs');

test('the driver’s tables are these tables, row for row and in this order', () => {
  const source = readFileSync(DRIVER_TABLES, 'utf8').replace(/\r\n/g, '\n');
  const rows = (method: string) => csharpRows(source, method, { Whole: WHOLE, Lists: LISTS }, 'ToolInstallTests');

  assert.deepEqual(rows('An_archive_unpacks_as_the_cli_unpacks_it'), UNPACK_ROWS);
  assert.deepEqual(rows('A_plan_is_made_as_the_cli_makes_it'), PLAN_ROWS);
  assert.deepEqual(rows('A_download_is_verified_as_the_cli_verifies_it'), DOWNLOAD_ROWS);
  assert.deepEqual(rows('A_version_is_deleted_as_the_cli_deletes_it'), DELETE_ROWS);
  assert.deepEqual(rows('A_location_is_looked_at_as_the_cli_looks'), LOOK_ROWS);
  assert.deepEqual(rows('The_record_names_what_the_cli_names'), [[RECORD_KEYS.join(',')]]);
  assert.deepEqual(rows('The_bounds_are_the_cli_s'), [[STAGING, REDIRECTS, LOOK_BOUND_MS]]);
});
