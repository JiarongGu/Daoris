import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { gzipSync } from 'node:zlib';
import { extractTarGz } from '../src/tarball.ts';
import { makeFixture } from './_fixture.ts';
import { TAR_END as END, pax, tarEntry as entry } from './_tar.ts';

/**
 * A vendor's package, unpacked whole (AGT2b) — the only archive Daoris opens, and one it opens only
 * after its hash matched the vendor's published one. The hash says the bytes are the vendor's; it says
 * nothing about where the entries inside point, so the extraction refuses every entry that would land
 * outside the directory it was given, whoever wrote the archive.
 */

test('a package unpacks whole: directories, files, and what may run stays runnable', async () => {
  const fx = makeFixture('tarball-whole');
  const archive = join(fx.root, 'package.tar.gz');
  writeFileSync(archive, gzipSync(Buffer.concat([
    entry('./', '', '5'),
    entry('./codex-package.json', '{"layout":1}\n'),
    entry('./bin/', '', '5'),
    entry('./bin/codex', '#!/bin/sh\necho codex\n', '0', 0o755),
    entry('codex-resources/notes.txt', 'plain\n'),
    END,
  ])));

  const into = join(fx.root, 'out');
  const written = await extractTarGz(archive, into);

  assert.deepEqual(written.sort(), ['bin/codex', 'codex-package.json', 'codex-resources/notes.txt']);
  assert.equal(readFileSync(join(into, 'codex-package.json'), 'utf8'), '{"layout":1}\n');
  assert.equal(readFileSync(join(into, 'bin', 'codex'), 'utf8'), '#!/bin/sh\necho codex\n');
  if (process.platform !== 'win32') {
    assert.ok(statSync(join(into, 'bin', 'codex')).mode & 0o100, 'the executable lost its mode');
    assert.equal(statSync(join(into, 'codex-resources', 'notes.txt')).mode & 0o111, 0);
  }
  fx.cleanup();
});

test('a name longer than the header holds arrives whole, by either long-name convention', async () => {
  const fx = makeFixture('tarball-long');
  const deep = `${'d'.repeat(60)}/${'e'.repeat(60)}/file.txt`;
  const deeper = `${'p'.repeat(70)}/${'q'.repeat(70)}/other.txt`;
  const archive = join(fx.root, 'long.tar.gz');
  writeFileSync(archive, gzipSync(Buffer.concat([
    // POSIX: an extended header carries `path`.
    entry('PaxHeader', pax({ path: deep }), 'x'),
    entry('truncated-name', 'pax\n'),
    // GNU: a `././@LongLink` entry carries the name as its body.
    entry('././@LongLink', `${deeper}\0`, 'L'),
    entry('truncated-too', 'gnu\n'),
    END,
  ])));

  const into = join(fx.root, 'out');
  await extractTarGz(archive, into);
  assert.equal(readFileSync(join(into, ...deep.split('/')), 'utf8'), 'pax\n');
  assert.equal(readFileSync(join(into, ...deeper.split('/')), 'utf8'), 'gnu\n');
  fx.cleanup();
});

for (const [what, name] of [
  ['a parent reference', '../escaped.txt'],
  ['a parent reference inside the path', 'bin/../../escaped.txt'],
  ['an absolute path', '/escaped.txt'],
  ['a drive-letter path', 'C:/escaped.txt'],
  ['a backslash path', '..\\escaped.txt'],
] as const) {
  test(`an entry naming ${what} is refused, and nothing lands outside`, async () => {
    const fx = makeFixture(`tarball-escape-${what.replace(/\W+/g, '-')}`);
    const archive = join(fx.root, 'evil.tar.gz');
    writeFileSync(archive, gzipSync(Buffer.concat([entry('fine.txt', 'ok\n'), entry(name, 'no\n'), END])));

    const into = join(fx.root, 'nested', 'out');
    await assert.rejects(extractTarGz(archive, into), /outside|absolute/);
    assert.equal(existsSync(join(fx.root, 'nested', 'escaped.txt')), false);
    assert.equal(existsSync(join(fx.root, 'escaped.txt')), false);
    fx.cleanup();
  });
}

test('a link is refused rather than followed — a package that needs one is a package this cannot vouch for', async () => {
  const fx = makeFixture('tarball-links');
  for (const [type, label] of [['2', 'symbolic'], ['1', 'hard']] as const) {
    const archive = join(fx.root, `${label}.tar.gz`);
    writeFileSync(archive, gzipSync(Buffer.concat([entry('bin/rg', '', type, 0o755, '../../outside'), END])));
    await assert.rejects(extractTarGz(archive, join(fx.root, label)), /link/);
  }
  fx.cleanup();
});

test('an archive that stops mid-entry is refused as damaged, not unpacked in part as if whole', async () => {
  const fx = makeFixture('tarball-truncated');
  const whole = Buffer.concat([entry('bin/codex', 'x'.repeat(2000)), END]);
  const archive = join(fx.root, 'short.tar.gz');
  writeFileSync(archive, gzipSync(whole.subarray(0, 1024)));

  await assert.rejects(extractTarGz(archive, join(fx.root, 'out')), /ends|truncated|damaged/);
  fx.cleanup();
});

test('a header whose checksum is wrong is refused — a tarball is not guessed at', async () => {
  const fx = makeFixture('tarball-checksum');
  const bad = entry('bin/codex', 'x\n');
  bad[0] = 'c'.charCodeAt(0) + 1;
  const archive = join(fx.root, 'bad.tar.gz');
  writeFileSync(archive, gzipSync(Buffer.concat([bad, END])));

  await assert.rejects(extractTarGz(archive, join(fx.root, 'out')), /checksum/);
  assert.deepEqual(existsSync(join(fx.root, 'out')) ? readdirSync(join(fx.root, 'out')) : [], []);
  fx.cleanup();
});
