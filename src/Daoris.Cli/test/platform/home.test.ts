import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { daorisHome, homeFile, HOME_VARIABLE, installOf, requireHomeFile, THIS_INSTALL } from '../../src/platform/home.ts';
import { captureError, makeFixture } from '../_fixture.ts';

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

/**
 * 🔴 FINDDAORIS1 (D171 point 1): the install's launchers set no variable, so a `daoris` the install carries
 * (`<install>/app/cli/node_modules/daoris/`) finds its home where it runs: the install's own `data` folder. The
 * variable still wins, and a checkout is no install.
 */
test('a daoris the install carries finds the install’s data folder, and the variable still wins', () => {
  const fx = makeFixture('home-install');
  try {
    fx.write('INSTALLED.md', '# Daoris — installed desktop\n');
    const pkg = join(fx.root, 'app', 'cli', 'node_modules', 'daoris', 'src', 'platform');
    assert.equal(installOf(pkg), fx.root);
    assert.equal(daorisHome({}, installOf(pkg)), join(fx.root, 'data'));
    assert.equal(daorisHome({ DAORIS_HOME: 'D:/somewhere/data' }, fx.root), 'D:/somewhere/data');
    assert.equal(homeFile({}, 'driver.json', fx.root), join(fx.root, 'data', 'driver.json'));
    assert.equal(requireHomeFile({}, 'driver.json', fx.root), join(fx.root, 'data', 'driver.json'));

    // An `app` folder with no marker beside it is no install.
    assert.equal(installOf(join(fx.root, 'elsewhere', 'app', 'cli')), null);
  } finally {
    fx.cleanup();
  }
});

test('this checkout is no install', () => {
  assert.equal(THIS_INSTALL, null);
});

// 🔴 No default under the user profile — that is the decision, not an omission.
test('absent, it is absent rather than the user profile', () => {
  assert.equal(daorisHome({ HOME: 'D:/somewhere/profile', USERPROFILE: 'C:/somewhere/profile' }), null);
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
