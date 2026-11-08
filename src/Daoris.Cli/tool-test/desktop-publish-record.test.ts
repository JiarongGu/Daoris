import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
// @ts-expect-error — untyped workspace tooling; see desktop-publish.test.ts
import { SHELL_FILES, recordedShellFiles } from '../../../tools/desktop-publish.mjs';

test('a record line that is not one name inside app/ is never a name the republish removes', (t) => {
  const at = mkdtempSync(join(tmpdir(), 'daoris-shell-record-'));
  t.after(() => rmSync(at, { recursive: true, force: true }));
  mkdirSync(join(at, SHELL_FILES[0]));
  writeFileSync(join(at, ...SHELL_FILES), 'libcef.dll\n..\n.\n../data\nsub/inner\nsub\\inner\nC:\\Windows\n/etc\nlocales\n');
  assert.deepEqual(recordedShellFiles(at), ['libcef.dll', 'locales']);
});
