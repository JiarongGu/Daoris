import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { RefusalError } from '../src/errors.ts';
import { unpackPackage } from '../src/toolinstall.ts';
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

// ——— The twin, held: the driver's tables are these tables, row for row and in this order.

const DRIVER_TABLES = join(here, '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'ToolInstallTests.cs');

test('the driver’s tables are these tables, row for row and in this order', () => {
  const source = readFileSync(DRIVER_TABLES, 'utf8').replace(/\r\n/g, '\n');
  const rows = (method: string) => csharpRows(source, method, {}, 'ToolInstallTests');

  assert.deepEqual(rows('An_archive_unpacks_as_the_cli_unpacks_it'), UNPACK_ROWS);
});
