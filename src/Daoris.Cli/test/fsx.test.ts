import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { readFileSync } from 'node:fs';
import { makeFixture } from './_fixture.ts';
import { normalize, readText, writeTextAtomic, sha256, listMarkdown, onPath } from '../src/fsx.ts';

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

/**
 * npm installs an extensionless POSIX script beside every `.cmd` on Windows. Present, it counts; to
 * START, only the `.cmd` will do: found first, the bare file is one no Windows process can run. The
 * two PATH lookups this replaced each held one half (REV3 CLEAN1), as the driver's twin holds both.
 */
test('a command on PATH is found as present by its bare name, and as startable by its shim', {
  skip: process.platform !== 'win32' && 'a shim is a Windows shape',
}, () => {
  const fx = makeFixture('fsx-on-path');
  fx.write('bin/tool', '#!/bin/sh\n');
  fx.write('bin/tool.cmd', '@echo off\n');
  const env = { PATH: join(fx.root, 'bin'), PATHEXT: '.EXE;.CMD' };

  assert.equal(onPath('tool', { env }), join(fx.root, 'bin', 'tool'));
  // The extension is spelled as PATHEXT spells it; Windows reads a path in any case.
  assert.equal(onPath('tool', { env, startable: true })?.toLowerCase(), join(fx.root, 'bin', 'tool.cmd').toLowerCase());
  assert.equal(onPath('missing', { env }), null);
  assert.equal(onPath(join(fx.root, 'bin', 'tool.cmd'), { env }), join(fx.root, 'bin', 'tool.cmd'));
  fx.cleanup();
});
