import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import type { Fixture } from './_fixture.ts';
import { makeFixture, captureError } from './_fixture.ts';
import { readCanon } from '../src/canon.ts';
import { readText } from '../src/fsx.ts';
import { readManifest, readLock } from '../src/config.ts';
import { planSync, applySync, commandSync } from '../src/materialize.ts';
import { DaorisError } from '../src/errors.ts';

const doc = (name: string) => `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n\nBody of ${name}.\n`;

/** A canon fixture and a repository fixture, seeded together. */
interface Seeded { canonFx: Fixture; repoFx: Fixture }

function seed(packs: string[] = []) {
  const canonFx = makeFixture('sync-canon');
  canonFx.write('canon.json', '{"version":"0.1.0"}');
  canonFx.write('core/rules/sensitive-info.md', doc('sensitive-info'));
  // A knowledge document, because it is still a FILE — which is where collisions and path-level
  // refusals still live now that the always-loaded tier is a span (D59).
  canonFx.write('core/knowledge/storage.md', doc('storage'));
  canonFx.write('packs/win/pack.json', '{"name":"win","description":"Windows"}');
  canonFx.write('packs/win/rules/gotchas.md', doc('gotchas'));

  const repoFx = makeFixture('sync-repo');
  repoFx.write('daoris.json', JSON.stringify({ source: 'github:OWNER/daoris#v0.1.0', packs }));
  repoFx.write('.claude/rules/house-style.md', doc('house-style'));
  return { canonFx, repoFx };
}

function run({ canonFx, repoFx }: Seeded, force = false) {
  const canon = readCanon(canonFx.root);
  const manifest = readManifest(repoFx.root);
  const plan = planSync({ root: repoFx.root, manifest, canon, lock: readLock(repoFx.root) });
  return applySync({ root: repoFx.root, manifest, plan, canonVersion: canon.version, force });
}

/**
 * The always-loaded tier is a SPAN in the file every harness reads (D59), so a fresh sync writes a
 * region rather than a directory — and the pointer that carries it to the one harness reading the
 * other name.
 */
test('a fresh sync writes the region, the pointer, and the lock', () => {
  const fx = seed(['win']);
  run(fx);

  const region = fx.repoFx.region();
  assert.ok(region, 'the region must exist');
  // The provenance line is still per rule, which is what keeps drift and `upstream` per rule.
  assert.match(region, /<!-- daoris: core\/core\/rules\/sensitive-info\.md @ 0\.1\.0 /);
  assert.match(region, /Body of sensitive-info/);
  assert.match(region, /Body of gotchas/);
  // 🔴 No frontmatter fence: seven of them mid-document is what stripping exists to prevent.
  assert.doesNotMatch(region, /^---\s*$/m);
  // The rules no longer land as files of their own.
  assert.equal(fx.repoFx.exists('.claude/rules/sensitive-info.md'), false);

  assert.match(fx.repoFx.read('CLAUDE.md'), /@AGENTS\.md/);
  assert.deepEqual(
    readLock(fx.repoFx.root)!.entries.map((e) => e.target).sort(),
    ['knowledge/storage.md', 'rules/gotchas.md', 'rules/sensitive-info.md'],
  );
  // The identity stays the canonical path; `in` is what says where to look, and only rules have it.
  const entries = readLock(fx.repoFx.root)!.entries;
  assert.equal(entries.find((e) => e.target === 'rules/sensitive-info.md')!.in, 'AGENTS.md');
  assert.equal(entries.find((e) => e.target === 'knowledge/storage.md')!.in, undefined);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('a local file is never touched and never enters the lock', () => {
  const fx = seed();
  const before = fx.repoFx.read('.claude/rules/house-style.md');
  run(fx);
  assert.equal(fx.repoFx.read('.claude/rules/house-style.md'), before);
  assert.equal(readLock(fx.repoFx.root)!.entries.some((e) => e.target.includes('house-style')), false);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('retiring a canonical file removes it from the repo on the next sync', () => {
  const fx = seed(['win']);
  run(fx);
  rmSync(join(fx.canonFx.root, 'packs/win/rules/gotchas.md'));
  run(fx);
  assert.equal(fx.repoFx.exists('.claude/rules/gotchas.md'), false);
  assert.equal(readLock(fx.repoFx.root)!.entries.some((e) => e.target.includes('gotchas')), false);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

/** Editing a rule now means editing the region it lives in — drift is measured per rule inside it. */
function handEdit(fx: Seeded, from: string, to: string): void {
  fx.repoFx.write('AGENTS.md', fx.repoFx.read('AGENTS.md').replace(from, to));
}

test('a locally-drifted rule is refused without --force and overwritten with it', () => {
  const fx = seed();
  run(fx);
  handEdit(fx, 'Body of sensitive-info.', 'hand-edited');

  const error = captureError(() => run(fx));
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /sensitive-info/);
  assert.match(error.message, /--force/);

  run(fx, true);
  assert.match(fx.repoFx.rule('core/core/rules/sensitive-info.md')!, /Body of sensitive-info/);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('a canon improvement reaches an untouched repo without --force', () => {
  const fx = seed();
  run(fx);
  // The rule got better upstream. The repo did nothing at all.
  fx.canonFx.write('core/rules/sensitive-info.md', doc('sensitive-info').replace('Body of', 'IMPROVED body of'));
  run(fx);
  assert.match(fx.repoFx.rule('core/core/rules/sensitive-info.md')!, /IMPROVED body of sensitive-info/);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('a canon version bump alone is not drift', () => {
  const fx = seed();
  run(fx);
  fx.canonFx.write('canon.json', '{"version":"0.2.0"}');
  const canon = readCanon(fx.canonFx.root);
  const plan = planSync({
    root: fx.repoFx.root,
    manifest: readManifest(fx.repoFx.root),
    canon,
    lock: readLock(fx.repoFx.root),
  });
  // Only the provenance header moved. Reporting that as "you edited this" would
  // accuse every consumer of an edit nobody made.
  assert.deepEqual(plan.drifted, []);
  assert.deepEqual(plan.collisions, []);
  run(fx);
  assert.match(fx.repoFx.region()!, /@ 0\.2\.0 /);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

/**
 * A canonical file renamed upstream reaches consumers as a delete plus an add,
 * which loses nothing and explains nothing. Detected by content rather than
 * declared in metadata: a ledger can claim a rename that never happened, and
 * this cannot — it is reading what actually moved, the way version control does.
 */
test('a renamed canonical file is reported as a rename, not a delete plus an add', () => {
  const fx = seed(['win']);
  run(fx);
  const body = readText(join(fx.canonFx.root, 'packs/win/rules/gotchas.md'));
  rmSync(join(fx.canonFx.root, 'packs/win/rules/gotchas.md'));
  fx.canonFx.write('packs/win/rules/windows-traps.md', body);

  const canon = readCanon(fx.canonFx.root);
  const plan = planSync({
    root: fx.repoFx.root,
    manifest: readManifest(fx.repoFx.root),
    canon,
    lock: readLock(fx.repoFx.root),
  });
  assert.deepEqual(plan.renames, [{ from: 'rules/gotchas.md', to: 'rules/windows-traps.md' }]);

  // The outcome is unchanged — only the explanation improves.
  run(fx);
  assert.equal(fx.repoFx.rule('win/packs/win/rules/gotchas.md'), null);
  assert.ok(fx.repoFx.rule('win/packs/win/rules/windows-traps.md'));
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('an unrelated retirement and addition are not called a rename', () => {
  const fx = seed(['win']);
  run(fx);
  rmSync(join(fx.canonFx.root, 'packs/win/rules/gotchas.md'));
  fx.canonFx.write('packs/win/rules/something-else.md', doc('something-else'));

  const plan = planSync({
    root: fx.repoFx.root,
    manifest: readManifest(fx.repoFx.root),
    canon: readCanon(fx.canonFx.root),
    lock: readLock(fx.repoFx.root),
  });
  assert.deepEqual(plan.renames, []);
  assert.deepEqual(plan.deletes, ['rules/gotchas.md']);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

/**
 * D5 says anything absent from the lock is invisible to the tool. The complement
 * was never enforced: everything the tool touches must sit INSIDE the target
 * directory. A lock entry containing `..` escaped it, and `sync` deleted files
 * daoris never wrote — silently, since retirement reports a count rather than a
 * path resolution.
 *
 * The lock is generated, so nobody reads it closely in review; a merge-mangled
 * entry or a crafted one in a pull request both reach the same rmSync.
 */
/**
 * The state space is lock x disk x canon, and this is the corner that was
 * missed: a rule the repo had improved, retired upstream in the same cycle.
 * Drift on a RETAINED file refuses and points at `upstream`; drift on a RETIRED
 * one deleted it without a word — the more destructive path had the weaker
 * guard, and it destroys the edit at exactly the moment it can no longer be
 * promoted, because the canonical file it belonged to is gone.
 */
/**
 * 🔴 Span-aware, and asserted because it stops working SILENTLY otherwise. A retired rule that lives
 * in the region has no file at its target, so a guard that only stats a path finds nothing, calls it
 * an untouched retirement and deletes the edit without a word — which is the fourth bug D19's last
 * row was written from, arriving again through the tier moving.
 */
test('retiring a rule the repo edited refuses rather than destroying the edit', () => {
  const fx = seed(['win']);
  run(fx);
  handEdit(fx, 'Body of gotchas.', 'Body of gotchas.\n\nHARD-WON LOCAL IMPROVEMENT.');
  rmSync(join(fx.canonFx.root, 'packs/win/rules/gotchas.md'));

  const error = captureError(() => run(fx));
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /gotchas/);
  assert.match(fx.repoFx.region()!, /HARD-WON LOCAL IMPROVEMENT/, 'the edit must survive the refusal');

  // --force is the deliberate "yes, I have what I need; drop it".
  run(fx, true);
  assert.equal(fx.repoFx.rule('win/packs/win/rules/gotchas.md'), null);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('retiring an untouched rule needs no ceremony', () => {
  const fx = seed(['win']);
  run(fx);
  rmSync(join(fx.canonFx.root, 'packs/win/rules/gotchas.md'));
  run(fx);
  assert.equal(fx.repoFx.rule('win/packs/win/rules/gotchas.md'), null);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

/**
 * 🔴 A retired SPAN leaves with the region's rewrite — there is no file of Daoris's at its old path.
 * A file there now is the repository's own (the test above makes that legal), and deleting it was the
 * one thing `sync` does without `--force` that lost work (REV3).
 */
test('retiring a span never deletes the repository\'s own file at the old path', () => {
  const fx = seed(['win']);
  run(fx);
  fx.repoFx.write('.claude/rules/gotchas.md', 'our own gotchas, written after adopting\n');
  rmSync(join(fx.canonFx.root, 'packs/win/rules/gotchas.md'));

  run(fx);
  assert.equal(fx.repoFx.rule('win/packs/win/rules/gotchas.md'), null, 'the span left the region');
  assert.equal(fx.repoFx.read('.claude/rules/gotchas.md'), 'our own gotchas, written after adopting\n');
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

/**
 * "Everything outside the region survives byte for byte" (`writeRegion`). It did not through `sync`:
 * the file was normalized before it reached the writer, so the line-ending detection always answered
 * LF and every line of an adopter's CRLF file changed on its first sync, BOM gone too (REV3).
 */
test('an adopter\'s CRLF instruction files keep their line endings and BOM through a sync', () => {
  const fx = seed();
  const theirs = '﻿# Our agents\r\n\r\nHouse notes that are ours.\r\n';
  const pointer = '﻿# Claude\r\n\r\nOur own notes.\r\n';
  writeFileSync(join(fx.repoFx.root, 'AGENTS.md'), theirs, 'utf8');
  writeFileSync(join(fx.repoFx.root, 'CLAUDE.md'), pointer, 'utf8');

  run(fx);
  const agents = readFileSync(join(fx.repoFx.root, 'AGENTS.md'), 'utf8');
  const claude = readFileSync(join(fx.repoFx.root, 'CLAUDE.md'), 'utf8');
  assert.ok(agents.startsWith(theirs.trimEnd()), 'their text is untouched, BOM and CRLF included');
  assert.equal(/(?<!\r)\n/.test(agents), false, 'no bare LF anywhere: the region takes the file\'s ending');
  assert.ok(claude.startsWith(pointer.trimEnd()));
  assert.match(claude, /@AGENTS\.md\r\n/);

  // And a second sync is a no-op on the bytes.
  run(fx);
  assert.equal(readFileSync(join(fx.repoFx.root, 'AGENTS.md'), 'utf8'), agents);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('a lock entry cannot reach outside the target directory', () => {
  const fx = seed();
  run(fx);
  const outside = join(fx.repoFx.root, 'IMPORTANT.md');
  writeFileSync(outside, 'a file daoris does not own\n');

  const plan = {
    writes: [],
    deletes: ['../IMPORTANT.md'],
    drifted: [],
    collisions: [],
    renames: [],
    editedRetirements: [],
    switchedOff: [],
    offers: [],
    editedSwitchedOff: [],
  };
  const error = captureError(() =>
    applySync({
      root: fx.repoFx.root,
      manifest: readManifest(fx.repoFx.root),
      plan,
      canonVersion: '0.1.0',
      force: false,
    }),
  );
  assert.ok(error instanceof DaorisError, 'escaping the target must be refused');
  assert.match(error.message, /IMPORTANT\.md/);
  assert.equal(existsSync(outside), true, 'and the file must still be there');

  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

/**
 * --force is the only way to lose work with this tool, and it was silent about
 * doing so. A refusal names the file; the override that overrules the refusal
 * should name it too, or the record of what was destroyed exists nowhere.
 */
test('--force names what it destroys', () => {
  const fx = seed(['win']);
  process.env.DAORIS_CANON = fx.canonFx.root;
  const out: string[] = [];
  const write = (s: string) => out.push(s);
  commandSync({ root: fx.repoFx.root, argv: [], write, packageRoot: '' });

  handEdit(fx, 'Body of sensitive-info.', 'hand-edited');
  rmSync(join(fx.canonFx.root, 'packs/win/rules/gotchas.md'));
  handEdit(fx, 'Body of gotchas.', 'edited, and being retired');

  out.length = 0;
  assert.equal(commandSync({ root: fx.repoFx.root, argv: ['--force'], write, packageRoot: '' }), 0);
  const text = out.join('\n');
  assert.match(text, /overwrote\s+rules\/sensitive-info\.md/, text);
  assert.match(text, /discarded\s+rules\/gotchas\.md/, text);

  delete process.env.DAORIS_CANON;
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('an unchanged file is planned as unchanged, not rewritten', () => {
  const fx = seed();
  run(fx);
  const canon = readCanon(fx.canonFx.root);
  const plan = planSync({
    root: fx.repoFx.root,
    manifest: readManifest(fx.repoFx.root),
    canon,
    lock: readLock(fx.repoFx.root),
  });
  assert.ok(plan.writes.every((w) => w.state === 'unchanged'));
  assert.deepEqual(plan.deletes, []);
  assert.deepEqual(plan.drifted, []);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('adopting a repo that already owns a canonical filename refuses rather than clobbering', () => {
  const fx = seed();
  // The repo wrote its own storage.md long before it ever heard of daoris.
  fx.repoFx.write('.claude/knowledge/storage.md', 'our own hard-won document\n');

  const error = captureError(() => run(fx));
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /storage/);
  assert.match(error.message, /already/i);
  assert.equal(fx.repoFx.read('.claude/knowledge/storage.md'), 'our own hard-won document\n');

  run(fx, true); // --force is the deliberate "yes, take the canonical one"
  assert.match(fx.repoFx.read('.claude/knowledge/storage.md'), /Body of storage/);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

/**
 * 🔴 A consequence of the move, asserted so it is a decision rather than a discovery: **a rule can no
 * longer collide.** The canonical rules land in a region Daoris owns, so a repository's own
 * `.claude/rules/x.md` is no longer at a path the canon claims — it is simply a local document, kept
 * and invisible (D5), whatever it happens to be called. D12's refusal still guards every tier that is
 * still files, and `doctor` is what reports a local rule that restates a canonical one.
 */
test('a rule the repo wrote itself is local now, not a collision', () => {
  const fx = seed();
  fx.repoFx.write('.claude/rules/sensitive-info.md', 'our own hard-won rule\n');

  const plan = planSync({
    root: fx.repoFx.root,
    manifest: readManifest(fx.repoFx.root),
    canon: readCanon(fx.canonFx.root),
    lock: readLock(fx.repoFx.root),
  });

  assert.deepEqual(plan.collisions, []);
  run(fx);
  assert.equal(fx.repoFx.read('.claude/rules/sensitive-info.md'), 'our own hard-won rule\n');
  assert.match(fx.repoFx.rule('core/core/rules/sensitive-info.md')!, /Body of sensitive-info/);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('an adopted file identical to the canon is not a collision', () => {
  const fx = seed();
  run(fx);
  const vendored = fx.repoFx.read('.claude/knowledge/storage.md');
  const fresh = seed();
  fresh.repoFx.write('.claude/knowledge/storage.md', vendored);
  run(fresh); // byte-identical: nothing to warn about
  assert.deepEqual(
    readLock(fresh.repoFx.root)!.entries.map((e) => e.target).sort(),
    ['knowledge/storage.md', 'rules/sensitive-info.md'],
  );
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
  fresh.canonFx.cleanup();
  fresh.repoFx.cleanup();
});

/**
 * The roster is part of the region now, not a file beside it — so it is loaded rather than merely
 * present, which is the whole reason the tier moved.
 */
test('sync writes the roster into the region, so a synced repo is immediately consistent', () => {
  const fx = seed();
  fx.repoFx.write('.claude/knowledge/ours.md', doc('ours'));
  run(fx);

  const region = fx.repoFx.region()!;
  assert.match(region, /sensitive-info/);
  assert.match(region, /## Read on demand/);
  assert.match(region, /storage/);
  // A document the repository wrote itself is listed and marked, never synced.
  assert.match(region, /ours.*\(local\)/);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});
