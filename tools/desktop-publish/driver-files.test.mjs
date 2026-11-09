import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';
import { driverProblems } from './driver-files.mjs';
import { DRIVER_EXE, DRIVER_HOME } from './launchers.mjs';

const scratch = join(import.meta.dirname, '..', '..', '_fixtures', 'driver-files');

test('an install whose driver folder lacks the git library native file says so', () => {
  rmSync(scratch, { recursive: true, force: true });
  const folder = join(scratch, ...DRIVER_HOME);
  mkdirSync(folder, { recursive: true });
  writeFileSync(join(folder, DRIVER_EXE), '');
  assert.deepEqual(driverProblems(scratch), [`${DRIVER_HOME.join('/')}/git2-*.dll, the git library's native file, is not there`]);

  writeFileSync(join(folder, 'git2-5853918.dll'), '');
  assert.deepEqual(driverProblems(scratch), []);

  rmSync(join(folder, DRIVER_EXE));
  assert.deepEqual(driverProblems(scratch), [`${DRIVER_HOME.join('/')}/${DRIVER_EXE} is not there`]);
  rmSync(scratch, { recursive: true, force: true });
});
