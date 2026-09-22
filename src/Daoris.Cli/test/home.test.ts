import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { daorisHome, homeFile, HOME_VARIABLE, requireHomeFile } from '../src/home.ts';
import { captureError } from './_fixture.ts';

/**
 * Where every machine-local file lives (D63): one home, named by `DAORIS_HOME`, and no default under
 * the user profile. The CLI's copy of a three-way twin — the driver and the service hold the same
 * contract in C#, sharing no code, the way the remotes map's three copies do (WSP3).
 */

test('the home is the variable and nothing else', () => {
  assert.equal(HOME_VARIABLE, 'DAORIS_HOME');
  assert.equal(daorisHome({ DAORIS_HOME: 'D:/somewhere/data' }), 'D:/somewhere/data');
  assert.equal(daorisHome({}), null);
  assert.equal(daorisHome({ DAORIS_HOME: '   ' }), null);
});

// 🔴 No default under the user profile — that is the decision, not an omission.
test('absent, it is absent rather than the user profile', () => {
  assert.equal(daorisHome({ HOME: '/home/someone', USERPROFILE: 'C:/Users/someone' }), null);
});

test('a file under the home is the home joined, or null when there is none', () => {
  assert.equal(homeFile({ DAORIS_HOME: 'D:/somewhere/data' }, 'driver.json'), join('D:/somewhere/data', 'driver.json'));
  assert.equal(homeFile({}, 'driver.json'), null);
});

/**
 * The management class REFUSES rather than guessing: a verb with no home and no per-file override
 * names what to set, in one sentence, instead of writing somewhere nobody pointed it.
 */
test('requiring a file with no home is a refusal that names the variable and the file', () => {
  const error = captureError(() => requireHomeFile({}, 'harnesses.json'));
  assert.match(error.message, /DAORIS_HOME/);
  assert.match(error.message, /harnesses\.json/);
  assert.doesNotMatch(error.message, /\.daoris/);
});
