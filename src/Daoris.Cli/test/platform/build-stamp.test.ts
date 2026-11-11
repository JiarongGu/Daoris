import { test } from 'node:test';
import assert from 'node:assert/strict';
import { BUILD_STAMP, versionLine, writeBuildStamp } from '../../src/platform/build-stamp.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * `daoris --version` says the build it is: the package's version, and where the publish laid it into an install, the build's id and
 * its commit, as the stamp beside its package.json says them. A package with no stamp is the source tree's, or npm's.
 */

test('a package an install carries says its version, its build and the build\'s commit', (t) => {
  const fixture = makeFixture('build-stamp-laid');
  t.after(() => fixture.cleanup());
  fixture.write('package.json', JSON.stringify({ name: 'daoris', version: '0.0.1' }));
  writeBuildStamp(fixture.root, { id: '20261111T010203Z-0a1b2c3d', commit: '1930be1' });

  assert.equal(fixture.exists(BUILD_STAMP), true);
  assert.equal(versionLine(fixture.root), '0.0.1 (build 20261111T010203Z-0a1b2c3d, commit 1930be1)');
});

test('a build the publish could read no commit for says its id alone', (t) => {
  const fixture = makeFixture('build-stamp-no-commit');
  t.after(() => fixture.cleanup());
  fixture.write('package.json', JSON.stringify({ name: 'daoris', version: '0.0.1' }));
  writeBuildStamp(fixture.root, { id: '20261111T010203Z-0a1b2c3d', commit: null });

  assert.equal(versionLine(fixture.root), '0.0.1 (build 20261111T010203Z-0a1b2c3d)');
});

test('a package with no stamp, or one that does not read, says its version alone', (t) => {
  const fixture = makeFixture('build-stamp-none');
  t.after(() => fixture.cleanup());
  fixture.write('package.json', JSON.stringify({ name: 'daoris', version: '1.4.0' }));
  assert.equal(versionLine(fixture.root), '1.4.0');

  fixture.write(BUILD_STAMP, 'not json');
  assert.equal(versionLine(fixture.root), '1.4.0');
});
