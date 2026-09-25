import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { existsSync } from 'node:fs';
import type { Fixture } from './_fixture.ts';
import { makeFixture, captureError } from './_fixture.ts';
import { readCanon } from '../src/canon.ts';
import { readManifest, readLock } from '../src/config.ts';
import { planSync, applySync } from '../src/materialize.ts';
import { upstreamFile, upstreamAll } from '../src/upstream.ts';
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

/** A rule is edited where it lives now: inside the region (D59). */
const edit = (fx: { repoFx: Fixture }, name: string, how: (body: string) => string) => {
  const region = fx.repoFx.read('AGENTS.md');
  fx.repoFx.write('AGENTS.md', region.replace(`Body of ${name}.`, how(`Body of ${name}.`)));
};

/**
 * 🔴 The frontmatter stays the CANON's. A span carries prose only — the frontmatter is stripped on
 * the way in — so promoting from a region replaces the body and leaves `name`, `applies_when` and
 * `enforces` alone. That is the honest limit of the move, and it is asserted rather than discovered:
 * improving an `enforces` line is a canon edit, not something a repository pushes from its
 * instruction file.
 */
test('a local edit lands in the canon, keeping the canon\'s own frontmatter', () => {
  const fx = synced();
  edit(fx, 'sensitive-info', (body) => `${body}\n\nIMPROVED.`);

  const result = promote(fx, 'rules/sensitive-info.md');
  const canonText = readText(join(fx.canonFx.root, 'core/rules/sensitive-info.md'));

  assert.equal(result.source, 'core/rules/sensitive-info.md');
  assert.match(canonText, /IMPROVED\./);
  assert.equal(canonText.startsWith('---\n'), true);
  assert.match(canonText, /applies_when: w/);
  // The provenance header never goes back up — it is materialization's, not the canon's.
  assert.doesNotMatch(canonText, /<!-- daoris:/);
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

/**
 * D18's containment, on the other half of the loop. `sync` refuses a lock entry that leaves the
 * target directory; `upstream` wrote wherever an entry's `source` pointed, and the lock is the
 * generated file nobody reads closely in review (REV3).
 */
test('a lock entry whose source leaves the canon is refused, and nothing is written outside it', () => {
  const fx = synced();
  edit(fx, 'sensitive-info', (body) => `${body}\n\nIMPROVED.`);
  const lock = JSON.parse(fx.repoFx.read('daoris.lock'));
  lock.entries.find((e: { target: string }) => e.target === 'rules/sensitive-info.md').source = '../escaped.md';
  fx.repoFx.write('daoris.lock', JSON.stringify(lock));
  const outside = join(fx.canonFx.root, '..', 'escaped.md');

  const error = captureError(() => promote(fx, 'rules/sensitive-info.md'));
  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /outside the canon/);
  assert.equal(existsSync(outside), false);
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

  // Edit two of the three, where they live now.
  let region = repoFx.read('AGENTS.md');
  for (const name of ['one', 'three']) {
    region = region.replace(`Body of ${name}.`, `Body of ${name}.\n\nIMPROVED ${name}.`);
  }
  repoFx.write('AGENTS.md', region);

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
  edit(fx, 'sensitive-info', (body) => `${body}\n\nIMPROVED.`);
  promote(fx, 'rules/sensitive-info.md');
  fx.canonFx.write('canon.json', '{"version":"0.9.0"}');

  const canon = readCanon(fx.canonFx.root);
  const manifest = readManifest(fx.repoFx.root);
  const plan = planSync({ root: fx.repoFx.root, manifest, canon, lock: readLock(fx.repoFx.root) });
  assert.deepEqual(plan.drifted, [], 'the repo holds exactly what the canon says');

  applySync({ root: fx.repoFx.root, manifest, plan, canonVersion: canon.version, force: false });
  const after = fx.repoFx.region()!;
  assert.match(after, /IMPROVED\./);
  assert.match(after, /@ 0\.9\.0 /);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('after upstreaming, a re-sync closes the loop without --force', () => {
  const fx = synced();
  edit(fx, 'sensitive-info', (body) => `${body}\n\nIMPROVED.`);
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
