import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { readFileSync } from 'node:fs';
import { makeFixture } from './_fixture.mjs';
import { normalize, readText, writeTextAtomic, sha256, listMarkdown } from '../src/fsx.mjs';

test('normalize strips a BOM and converts CRLF to LF', () => {
  assert.equal(normalize('﻿a\r\nb\r\n'), 'a\nb\n');
});

test('readText normalizes on the way in', () => {
  const fx = makeFixture('fsx-read');
  fx.write('a.md', '﻿line\r\n');
  assert.equal(readText(join(fx.root, 'a.md')), 'line\n');
  fx.cleanup();
});

test('writeTextAtomic writes BOM-less UTF-8 and leaves no temp file', () => {
  const fx = makeFixture('fsx-write');
  writeTextAtomic(join(fx.root, 'deep/b.md'), 'x — 灵台\n');
  assert.equal(readFileSync(join(fx.root, 'deep/b.md'), 'utf8'), 'x — 灵台\n');
  assert.equal(readFileSync(join(fx.root, 'deep/b.md'))[0], 0x78); // 'x', no BOM
  assert.equal(fx.exists('deep/b.md.daoris-tmp'), false);
  fx.cleanup();
});

test('sha256 ignores line-ending differences', () => {
  assert.equal(sha256('a\r\nb'), sha256('a\nb'));
  assert.notEqual(sha256('a'), sha256('b'));
});

test('listMarkdown returns sorted relative paths and ignores non-markdown', () => {
  const fx = makeFixture('fsx-list');
  fx.write('z.md', '');
  fx.write('a.md', '');
  fx.write('sub/m.md', '');
  fx.write('notes.txt', '');
  assert.deepEqual(listMarkdown(fx.root), ['a.md', 'sub/m.md', 'z.md']);
  assert.deepEqual(listMarkdown(join(fx.root, 'nope')), []);
  fx.cleanup();
});
