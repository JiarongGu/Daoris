import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import type { Fixture } from './_fixture.ts';
import { makeFixture, captureError } from './_fixture.ts';
import { readCanon } from '../src/canon.ts';
import { readManifest, readLock } from '../src/config.ts';
import { planSync, applySync } from '../src/materialize.ts';
import { upstreamFile, upstreamAll } from '../src/upstream.ts';
import { makeHeader, withHeader } from '../src/document.ts';
import { readText } from '../src/fsx.ts';
import { DaorisError } from '../src/errors.ts';

const doc = (name: string) => `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n\nBody of ${name}.\n`;

function synced() {
  const canonFx = makeFixture('up-canon');
  canonFx.write('canon.json', '{"version":"0.1.0"}');
  canonFx.write('core/rules/sensitive-info.md', doc('sensitive-info'));

  const repoFx = makeFixture('up-repo');
  repoFx.write('daoris.json', JSON.stringify({ source: 's', packs: [] }));
  repoFx.write('.claude/rules/house-style.md', doc('house-style'));
  const canon = readCanon(canonFx.root);
  const manifest = readManifest(repoFx.root);
  applySync({
    root: repoFx.root,
    manifest,
    canonVersion: canon.version,
    force: false,
    plan: planSync({ root: repoFx.root, manifest, canon, lock: null }),
  });
  return { canonFx, repoFx };
}

/** A canon fixture and a repository fixture, seeded together. */
interface Seeded { canonFx: Fixture; repoFx: Fixture }

const promote = ({ canonFx, repoFx }: Seeded, file: string) =>
  upstreamFile({
    root: repoFx.root,
    manifest: readManifest(repoFx.root),
    lock: readLock(repoFx.root),
    canonRoot: canonFx.root,
    file,
  });

const edited = () =>
  withHeader(
    makeHeader('core', 'core/rules/sensitive-info.md', '0.1.0'),
    `${doc('sensitive-info')}IMPROVED.\n`,
  );

test('a local edit lands in the canon without the provenance header', () => {
  const fx = synced();
  fx.repoFx.write('.claude/rules/sensitive-info.md', edited());
  const result = promote(fx, 'rules/sensitive-info.md');
  const canonText = readText(join(fx.canonFx.root, 'core/rules/sensitive-info.md'));
  assert.equal(result.source, 'core/rules/sensitive-info.md');
  assert.match(canonText, /IMPROVED\./);
  assert.equal(canonText.startsWith('---\n'), true);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('a bare filename resolves to its locked target', () => {
  const fx = synced();
  assert.equal(promote(fx, 'sensitive-info.md').target, 'rules/sensitive-info.md');
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('a target-dir-prefixed path resolves too', () => {
  const fx = synced();
  assert.equal(promote(fx, '.claude/rules/sensitive-info.md').target, 'rules/sensitive-info.md');
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('a local file has nothing to upstream and says so', () => {
  const fx = synced();
  const error = captureError(() => promote(fx, 'rules/house-style.md'));
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 2);
  assert.match(error.message, /local/i);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('upstreamAll promotes every drifted file and leaves clean ones alone', () => {
  const canonFx = makeFixture('up-all-canon');
  canonFx.write('canon.json', '{"version":"0.1.0"}');
  canonFx.write('core/rules/one.md', doc('one'));
  canonFx.write('core/rules/two.md', doc('two'));
  canonFx.write('core/rules/three.md', doc('three'));

  const repoFx = makeFixture('up-all-repo');
  repoFx.write('daoris.json', JSON.stringify({ source: 's', packs: [] }));
  const canon = readCanon(canonFx.root);
  const manifest = readManifest(repoFx.root);
  applySync({
    root: repoFx.root,
    manifest,
    canonVersion: canon.version,
    force: false,
    plan: planSync({ root: repoFx.root, manifest, canon, lock: null }),
  });

  // Edit two of the three.
  for (const name of ['one', 'three']) {
    repoFx.write(
      `.claude/rules/${name}.md`,
      withHeader(
        makeHeader('core', `core/rules/${name}.md`, '0.1.0'),
        `${doc(name)}IMPROVED ${name}.\n`,
      ),
    );
  }

  const promoted = upstreamAll({
    root: repoFx.root,
    manifest: readManifest(repoFx.root),
    lock: readLock(repoFx.root),
    canonRoot: canonFx.root,
  });

  assert.deepEqual(promoted.map((r) => r.source).sort(), [
    'core/rules/one.md',
    'core/rules/three.md',
  ]);
  assert.match(readText(join(canonFx.root, 'core/rules/one.md')), /IMPROVED one/);
  assert.match(readText(join(canonFx.root, 'core/rules/three.md')), /IMPROVED three/);
  assert.equal(/IMPROVED/.test(readText(join(canonFx.root, 'core/rules/two.md'))), false);

  canonFx.cleanup();
  repoFx.cleanup();
});

test('upstreamAll on a clean repo promotes nothing', () => {
  const fx = synced();
  assert.deepEqual(
    upstreamAll({
      root: fx.repoFx.root,
      manifest: readManifest(fx.repoFx.root),
      lock: readLock(fx.repoFx.root),
      canonRoot: fx.canonFx.root,
    }),
    [],
  );
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

/**
 * The return path has to close without --force. Once the edit is IN the canon,
 * the file on disk already is what the canon would write — there is nothing to
 * reconcile, only a stale lock hash. Demanding --force here would tell the
 * person who just contributed an improvement to "discard your local edit",
 * which is both wrong and the exact advice most likely to lose the work.
 */
/**
 * The realistic sequence, and the one the release rehearsal caught: an edit is
 * promoted, then the canon SHIPS as a new version. The repo's copy now holds the
 * canonical body under an old header, so comparing whole files makes it differ
 * from the lock and from the new content at once — and `sync` would refuse, and
 * advise promoting an edit that is already promoted. Bodies are what is
 * doctrine; the header is bookkeeping.
 */
test('a promoted edit survives a canon version bump on top of it', () => {
  const fx = synced();
  fx.repoFx.write('.claude/rules/sensitive-info.md', edited());
  promote(fx, 'rules/sensitive-info.md');
  fx.canonFx.write('canon.json', '{"version":"0.9.0"}');

  const canon = readCanon(fx.canonFx.root);
  const manifest = readManifest(fx.repoFx.root);
  const plan = planSync({ root: fx.repoFx.root, manifest, canon, lock: readLock(fx.repoFx.root) });
  assert.deepEqual(plan.drifted, [], 'the repo holds exactly what the canon says');

  applySync({ root: fx.repoFx.root, manifest, plan, canonVersion: canon.version, force: false });
  const after = fx.repoFx.read('.claude/rules/sensitive-info.md');
  assert.match(after, /IMPROVED\./);
  assert.match(after, /@ 0\.9\.0 /);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('after upstreaming, a re-sync closes the loop without --force', () => {
  const fx = synced();
  fx.repoFx.write('.claude/rules/sensitive-info.md', edited());
  promote(fx, 'rules/sensitive-info.md');
  const canon = readCanon(fx.canonFx.root);
  const manifest = readManifest(fx.repoFx.root);
  const plan = planSync({
    root: fx.repoFx.root,
    manifest,
    canon,
    lock: readLock(fx.repoFx.root),
  });
  assert.deepEqual(plan.drifted, [], 'a file already matching the canon is not drift');
  applySync({ root: fx.repoFx.root, manifest, plan, canonVersion: canon.version, force: false });
  const after = planSync({
    root: fx.repoFx.root,
    manifest,
    canon,
    lock: readLock(fx.repoFx.root),
  });
  assert.deepEqual(after.drifted, []);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});
