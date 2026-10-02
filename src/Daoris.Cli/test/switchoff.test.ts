import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { Fixture } from './_fixture.ts';
import { makeFixture, captureError } from './_fixture.ts';
import { readCanon, resolveSelection } from '../src/canon.ts';
import { readLock, readManifest } from '../src/config.ts';
import { planChanges, planSync, applySync, commandSync } from '../src/materialize.ts';
import { commandCheck } from '../src/drift.ts';
import { commandInit, commandStatus } from '../src/commands.ts';
import { analyze } from '../src/analyze.ts';
import { DaorisError } from '../src/errors.ts';

/**
 * 🔴 **A pack may switch a core row off, the repository confirms it, and it is never silent** (D71).
 *
 * The owner reopened D4 (2026-09-24). A pack offers, with a reason (`switchesOff` in `pack.json`);
 * the manifest accepts, naming the pack (`switchedOff` in `daoris.json`); the lock records it so the
 * offline `check` can say so. An offer nobody confirmed leaves the core row installed and is reported
 * — the safer default, and the owner's to reverse. D19's new cells are the `sync` tests below.
 */

const doc = (name: string, body = `Body of ${name}.`) =>
  `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n\n${body}\n`;

const REASON = 'its own ticket-lifecycle rule replaces the backlog discipline';

/** A canon whose `tracker` pack offers to switch off two core rows: a rule, and a skill. */
function canonFixture(name: string, offers: Record<string, string> = {
  'rules/task-lifecycle.md': REASON,
  'skills/fix-log': 'its own fix record replaces the fix log',
}): Fixture {
  const fx = makeFixture(`switch-canon-${name}`);
  fx.write('canon.json', '{"version":"0.1.0"}');
  fx.write('core/rules/task-lifecycle.md', doc('task-lifecycle', 'The backlog holds open work only.'));
  fx.write('core/rules/sensitive-info.md', doc('sensitive-info'));
  fx.write('core/knowledge/storage.md', doc('storage'));
  fx.write('core/skills/fix-log/SKILL.md', '---\nname: fix-log\ndescription: Record a fix.\n---\n\nSteps.\n');
  fx.write('core/skills/fix-log/template.md', 'A template the skill ships.\n');
  fx.write('packs/tracker/pack.json', JSON.stringify({
    name: 'tracker', apiVersion: 1, description: 'Work tracked in an external ticket system.',
    switchesOff: offers,
  }));
  fx.write('packs/tracker/rules/ticket-lifecycle.md', doc('ticket-lifecycle', 'The tracker holds open work.'));
  return fx;
}

function repoFixture(name: string, manifest: object): Fixture {
  const fx = makeFixture(`switch-repo-${name}`);
  fx.write('daoris.json', JSON.stringify({ source: 'github:OWNER/daoris#v0.1.0', ...manifest }));
  return fx;
}

function sync(canonFx: Fixture, repoFx: Fixture, force = false) {
  const canon = readCanon(canonFx.root);
  const manifest = readManifest(repoFx.root);
  const plan = planSync({ root: repoFx.root, manifest, canon, lock: readLock(repoFx.root) });
  applySync({ root: repoFx.root, manifest, plan, canonVersion: canon.version, force });
  return plan;
}

function setManifest(repoFx: Fixture, manifest: object): void {
  repoFx.write('daoris.json', JSON.stringify({ source: 'github:OWNER/daoris#v0.1.0', ...manifest }));
}

const done = (...fixtures: Fixture[]) => { for (const fx of fixtures) fx.cleanup(); };

// ——— The canon: what a pack may offer.

test('a pack may offer to switch a core row off, with its reason', () => {
  const canonFx = canonFixture('offer');
  const pack = readCanon(canonFx.root).packs.get('tracker')!;

  assert.equal(pack.switchesOff['rules/task-lifecycle.md'], REASON);
  assert.equal(readCanon(canonFx.root).packs.get('core')!.switchesOff['rules/task-lifecycle.md'], undefined);
  done(canonFx);
});

test('a switch naming no core document is refused, naming the pack and the row', () => {
  // `rules/ticket-lifecycle.md` is the pack's OWN row: another pack's rows are opt-in already, so
  // only core can be switched — and a typo is caught the same way.
  const canonFx = canonFixture('not-core', { 'rules/ticket-lifecycle.md': 'why' });

  const error = captureError(() => readCanon(canonFx.root));
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 2);
  assert.match(error.message, /tracker/);
  assert.match(error.message, /rules\/ticket-lifecycle\.md/);
  assert.match(error.message, /core/);
  done(canonFx);
});

test('a switch names one document or one whole skill — never a tier, never a file inside a skill', () => {
  // REV3 CLI F15: the key was matched as a prefix, so `rules` took every core rule out, and one file
  // of a skill could go while its SKILL.md stayed — a skill shipped without what it uses.
  for (const key of ['rules', 'skills', 'skills/fix-log/template.md', 'skills/fix-log/SKILL.md']) {
    const canonFx = canonFixture(`shape-${key.replace(/\W/g, '-')}`, { [key]: 'why' });
    const error = captureError(() => readCanon(canonFx.root));
    assert.ok(error instanceof DaorisError, `${key}: ${String(error)}`);
    assert.ok(error.message.includes(key), error.message);
    done(canonFx);
  }
});

test('a switch without its reason is refused — the reason is what status prints', () => {
  const canonFx = canonFixture('no-reason', { 'rules/task-lifecycle.md': '  ' });

  const error = captureError(() => readCanon(canonFx.root));
  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /reason/);
  done(canonFx);
});

test('two selected packs shipping one target are refused, naming both', () => {
  const canonFx = canonFixture('dup');
  canonFx.write('packs/other/pack.json', '{"name":"other","description":"d"}');
  canonFx.write('packs/other/rules/ticket-lifecycle.md', doc('ticket-lifecycle'));
  const canon = readCanon(canonFx.root);

  const error = captureError(() => resolveSelection(canon, ['tracker', 'other'], {}));
  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /rules\/ticket-lifecycle\.md/);
  assert.match(error.message, /tracker/);
  assert.match(error.message, /other/);
  done(canonFx);
});

// ——— The selection: the pack offers, the repository confirms.

test('an unconfirmed offer leaves the core row installed, and is reported as an offer', () => {
  const canonFx = canonFixture('unconfirmed');
  const selection = resolveSelection(readCanon(canonFx.root), ['tracker'], {});

  assert.ok(selection.files.some((file) => file.target === 'rules/task-lifecycle.md'));
  assert.deepEqual(selection.switchedOff, []);
  assert.deepEqual(
    selection.offers.map((offer) => [offer.target, offer.by]).sort(),
    [['rules/task-lifecycle.md', 'tracker'], ['skills/fix-log', 'tracker']],
  );
  done(canonFx);
});

test('a confirmed switch takes the core row out — a skill with every file it ships', () => {
  const canonFx = canonFixture('confirmed');
  const selection = resolveSelection(readCanon(canonFx.root), ['tracker'], {
    'rules/task-lifecycle.md': 'tracker',
    'skills/fix-log': 'tracker',
  });

  const targets = selection.files.map((file) => file.target);
  assert.equal(targets.includes('rules/task-lifecycle.md'), false);
  assert.equal(targets.some((target) => target.startsWith('skills/fix-log/')), false);
  assert.ok(targets.includes('rules/ticket-lifecycle.md'), 'the pack still installs its own');
  assert.deepEqual(
    selection.switchedOff.map((row) => [row.target, row.by, row.because]),
    [['rules/task-lifecycle.md', 'tracker', REASON], ['skills/fix-log', 'tracker', 'its own fix record replaces the fix log']],
  );
  assert.deepEqual(selection.offers, []);
  done(canonFx);
});

/**
 * D4's reason survives: a repository ALONE cannot drop a core row. A confirmation no selected pack
 * offers — the pack not selected, or never offering it — is a manifest defect, so a tool error.
 */
test('a confirmation no selected pack offers is a tool error, naming the row', () => {
  const canonFx = canonFixture('orphan');
  const canon = readCanon(canonFx.root);

  const unselected = captureError(() => resolveSelection(canon, [], { 'rules/task-lifecycle.md': 'tracker' }));
  assert.ok(unselected instanceof DaorisError);
  assert.equal(unselected.exitCode, 2);
  assert.match(unselected.message, /rules\/task-lifecycle\.md/);
  assert.match(unselected.message, /tracker/);

  const alone = captureError(() => resolveSelection(canon, ['tracker'], { 'rules/sensitive-info.md': 'tracker' }));
  assert.ok(alone instanceof DaorisError);
  assert.match(alone.message, /rules\/sensitive-info\.md/);
  done(canonFx);
});

test('a switchedOff that is not a map of rows to packs is a tool error', () => {
  const repoFx = repoFixture('malformed', { switchedOff: ['rules/task-lifecycle.md'] });

  const error = captureError(() => readManifest(repoFx.root));
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 2);
  assert.match(error.message, /switchedOff/);
  done(repoFx);
});

// ——— `sync`: D19's new cells.

test('confirmed, locked and untouched: the row goes off, the lock records it, sync names it', () => {
  const canonFx = canonFixture('off');
  const repoFx = repoFixture('off', { packs: ['tracker'] });
  sync(canonFx, repoFx);
  assert.ok(repoFx.rule('core/core/rules/task-lifecycle.md'), 'on, before the confirmation');
  assert.ok(repoFx.exists('.claude/skills/fix-log/template.md'));

  setManifest(repoFx, { packs: ['tracker'], switchedOff: {
    'rules/task-lifecycle.md': 'tracker', 'skills/fix-log': 'tracker',
  } });
  const out: string[] = [];
  process.env.DAORIS_CANON = canonFx.root;
  assert.equal(commandSync({ root: repoFx.root, argv: [], write: (line) => out.push(line), packageRoot: '' }), 0);
  delete process.env.DAORIS_CANON;

  assert.equal(repoFx.rule('core/core/rules/task-lifecycle.md'), null, 'its span left the region');
  assert.equal(repoFx.exists('.claude/skills/fix-log/SKILL.md'), false);
  assert.equal(repoFx.exists('.claude/skills/fix-log/template.md'), false);
  const lock = readLock(repoFx.root)!;
  assert.equal(lock.entries.some((entry) => entry.target === 'rules/task-lifecycle.md'), false);
  assert.deepEqual(lock.switchedOff, [
    { target: 'rules/task-lifecycle.md', by: 'tracker' },
    { target: 'skills/fix-log', by: 'tracker' },
  ]);
  assert.match(out.join('\n'), /off\s+rules\/task-lifecycle\.md.*tracker/);
  // 🔴 Never a rename, however alike the pack's own rule reads: a switch is a decision, not a move.
  assert.doesNotMatch(out.join('\n'), /renamed/);
  // A switch is not a retirement, and is not counted as one.
  assert.match(out.join('\n'), /retired 0\b/);
  done(canonFx, repoFx);
});

test('the region says which core rows are off and which pack switched them', () => {
  const canonFx = canonFixture('roster');
  const repoFx = repoFixture('roster', { packs: ['tracker'], switchedOff: { 'rules/task-lifecycle.md': 'tracker' } });
  sync(canonFx, repoFx);

  const region = repoFx.region()!;
  assert.match(region, /Switched off here/);
  assert.match(region, /`task-lifecycle`[^\n]*`tracker`/);
  done(canonFx, repoFx);
});

test('confirmed, locked and edited here: refused like drift, and --force discards', () => {
  const canonFx = canonFixture('edited');
  const repoFx = repoFixture('edited', { packs: ['tracker'] });
  sync(canonFx, repoFx);
  repoFx.write('AGENTS.md', repoFx.read('AGENTS.md').replace('The backlog holds open work only.', 'hand-edited'));
  setManifest(repoFx, { packs: ['tracker'], switchedOff: { 'rules/task-lifecycle.md': 'tracker' } });

  const error = captureError(() => sync(canonFx, repoFx));
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /rules\/task-lifecycle\.md/);
  assert.match(error.message, /tracker/);
  // The canonical file still EXISTS, so upstream is a real route — unlike an edited retirement.
  assert.match(error.message, /upstream/);
  assert.doesNotMatch(error.message, /cannot save/);

  sync(canonFx, repoFx, true);
  assert.equal(repoFx.rule('core/core/rules/task-lifecycle.md'), null);
  done(canonFx, repoFx);
});

test("confirmed, never locked: the repository's own file at that path is untouched", () => {
  const canonFx = canonFixture('own', { 'knowledge/storage.md': 'its own storage guide replaces it' });
  const repoFx = repoFixture('own', { packs: ['tracker'], switchedOff: { 'knowledge/storage.md': 'tracker' } });
  repoFx.write('.claude/knowledge/storage.md', '# Our own storage guide\n');
  sync(canonFx, repoFx);

  assert.equal(repoFx.read('.claude/knowledge/storage.md'), '# Our own storage guide\n');
  assert.equal(readLock(repoFx.root)!.entries.some((entry) => entry.target === 'knowledge/storage.md'), false);
  assert.deepEqual(readLock(repoFx.root)!.switchedOff, [{ target: 'knowledge/storage.md', by: 'tracker' }]);
  done(canonFx, repoFx);
});

test('a withdrawn confirmation brings the core row back', () => {
  const canonFx = canonFixture('withdrawn');
  const repoFx = repoFixture('withdrawn', { packs: ['tracker'], switchedOff: { 'rules/task-lifecycle.md': 'tracker' } });
  sync(canonFx, repoFx);
  assert.equal(repoFx.rule('core/core/rules/task-lifecycle.md'), null);

  setManifest(repoFx, { packs: ['tracker'] });
  sync(canonFx, repoFx);
  assert.match(repoFx.rule('core/core/rules/task-lifecycle.md')!, /open work only/);
  assert.equal(readLock(repoFx.root)!.switchedOff, undefined, 'nothing off, nothing recorded');
  done(canonFx, repoFx);
});

test('an unconfirmed offer is named by sync, with the line that would confirm it', () => {
  const canonFx = canonFixture('sync-offer');
  const repoFx = repoFixture('sync-offer', { packs: ['tracker'] });

  const out: string[] = [];
  process.env.DAORIS_CANON = canonFx.root;
  assert.equal(commandSync({ root: repoFx.root, argv: [], write: (line) => out.push(line), packageRoot: '' }), 0);
  delete process.env.DAORIS_CANON;

  assert.ok(repoFx.rule('core/core/rules/task-lifecycle.md'), 'an offer switches nothing');
  assert.match(out.join('\n'), /offered\s+rules\/task-lifecycle\.md/);
  assert.match(out.join('\n'), /"switchedOff"/);
  done(canonFx, repoFx);
});

test('a switched-off row is not reported as retired by the update report', () => {
  const canonFx = canonFixture('changes');
  const repoFx = repoFixture('changes', { packs: ['tracker'] });
  sync(canonFx, repoFx);
  setManifest(repoFx, { packs: ['tracker'], switchedOff: { 'rules/task-lifecycle.md': 'tracker' } });

  const changes = planChanges({
    root: repoFx.root, manifest: readManifest(repoFx.root), canon: readCanon(canonFx.root), lock: readLock(repoFx.root),
  });
  assert.equal(changes.retired.includes('rules/task-lifecycle.md'), false);
  done(canonFx, repoFx);
});

// ——— `check`: offline, from the lock.

test('check names every switched-off row on every run, and stays clean', () => {
  const canonFx = canonFixture('check');
  const repoFx = repoFixture('check', { packs: ['tracker'], switchedOff: { 'rules/task-lifecycle.md': 'tracker' } });
  sync(canonFx, repoFx);
  canonFx.cleanup(); // D8: check never reads the canon

  const out: string[] = [];
  assert.equal(commandCheck({ root: repoFx.root, write: (line) => out.push(line) }), 0, out.join('\n'));
  assert.match(out.join('\n'), /off\s+rules\/task-lifecycle\.md.*tracker/);
  done(repoFx);
});

test('check fails when the manifest and the lock disagree about a switch', () => {
  const canonFx = canonFixture('check-stale');
  const repoFx = repoFixture('check-stale', { packs: ['tracker'] });
  sync(canonFx, repoFx);

  setManifest(repoFx, { packs: ['tracker'], switchedOff: { 'rules/task-lifecycle.md': 'tracker' } });
  const confirmed: string[] = [];
  assert.equal(commandCheck({ root: repoFx.root, write: (line) => confirmed.push(line) }), 1);
  assert.match(confirmed.join('\n'), /stale\s+.*rules\/task-lifecycle\.md/);

  sync(canonFx, repoFx);
  setManifest(repoFx, { packs: ['tracker'] });
  const withdrawn: string[] = [];
  assert.equal(commandCheck({ root: repoFx.root, write: (line) => withdrawn.push(line) }), 1);
  assert.match(withdrawn.join('\n'), /stale\s+.*rules\/task-lifecycle\.md/);
  done(canonFx, repoFx);
});

// ——— `status` and `init`: the owner-facing half.

test('status names each switched-off row with its pack and reason, and each pending offer', () => {
  const canonFx = canonFixture('status');
  const repoFx = repoFixture('status', { packs: ['tracker'], switchedOff: { 'rules/task-lifecycle.md': 'tracker' } });
  sync(canonFx, repoFx);
  process.env.DAORIS_CANON = canonFx.root;

  const out: string[] = [];
  assert.equal(commandStatus({ root: repoFx.root, write: (line) => out.push(line), packageRoot: '' }), 0);
  const text = out.join('\n');
  assert.match(text, /switched off\s+rules\/task-lifecycle\.md.*tracker/);
  assert.ok(text.includes(REASON), text);
  assert.match(text, /offered\s+skills\/fix-log.*tracker/);

  const json: string[] = [];
  commandStatus({ root: repoFx.root, argv: ['--json'], write: (line) => json.push(line), packageRoot: '' });
  const status = JSON.parse(json.join('\n'));
  assert.deepEqual(status.switchedOff, [{ target: 'rules/task-lifecycle.md', by: 'tracker', because: REASON }]);
  assert.deepEqual(status.offers.map((offer: { target: string }) => offer.target), ['skills/fix-log']);

  delete process.env.DAORIS_CANON;
  done(canonFx, repoFx);
});

test('status names what check would fail on: a switch never synced, and an index behind the disk', () => {
  // REV3 CLI F11: `status` rendered drifted, missing and stale packs, and dropped the other two facts
  // `inspect` computes — so the one command a person asks "why is check red?" said nothing about them.
  const canonFx = canonFixture('status-stale');
  const repoFx = repoFixture('status-stale', { packs: ['tracker'] });
  sync(canonFx, repoFx);
  process.env.DAORIS_CANON = canonFx.root;

  setManifest(repoFx, { packs: ['tracker'], switchedOff: { 'rules/task-lifecycle.md': 'tracker' } });
  repoFx.write('.claude/knowledge/our-own.md', doc('our-own'));

  const out: string[] = [];
  commandStatus({ root: repoFx.root, write: (line) => out.push(line), packageRoot: '' });
  const text = out.join('\n');
  assert.match(text, /stale\s+rules\/task-lifecycle\.md is switched off by 'tracker' in the manifest and still on here/, text);
  assert.match(text, /index\s+\.claude\/INDEX\.md is out of date/, text);

  const json: string[] = [];
  commandStatus({ root: repoFx.root, argv: ['--json'], write: (line) => json.push(line), packageRoot: '' });
  const status = JSON.parse(json.join('\n'));
  assert.deepEqual(status.staleSwitches, [
    "rules/task-lifecycle.md is switched off by 'tracker' in the manifest and still on here",
  ]);
  assert.equal(status.indexStale, true);

  delete process.env.DAORIS_CANON;
  done(canonFx, repoFx);
});

test('analyze projects without a switched-off row, and reports the offers still waiting', () => {
  const canonFx = canonFixture('analyze');
  const repoFx = makeFixture('switch-repo-analyze');
  const canon = readCanon(canonFx.root);
  const report = (switchedOff: Record<string, string>) => analyze({
    root: repoFx.root, canon, packs: ['tracker'], switchedOff, target: '.claude', budgetLimit: 30000,
  });

  const on = report({});
  const off = report({ 'rules/task-lifecycle.md': 'tracker' });
  // The budget the adoption would add is smaller by exactly the row that will not load.
  assert.ok(off.budget.projected < on.budget.projected, `${off.budget.projected} < ${on.budget.projected}`);
  assert.deepEqual(on.offers.map((offer) => offer.target), ['rules/task-lifecycle.md', 'skills/fix-log']);
  assert.deepEqual(off.offers.map((offer) => offer.target), ['skills/fix-log']);
  assert.deepEqual(off.switchedOff.map((row) => row.target), ['rules/task-lifecycle.md']);
  done(canonFx, repoFx);
});

test("init names a pack's switches beside its description", () => {
  const canonFx = canonFixture('init');
  const repoFx = makeFixture('switch-repo-init');
  process.env.DAORIS_CANON = canonFx.root;

  const out: string[] = [];
  assert.equal(commandInit({ root: repoFx.root, write: (line) => out.push(line), packageRoot: '' }), 0);
  assert.match(out.join('\n'), /switches off core rules\/task-lifecycle\.md/);

  delete process.env.DAORIS_CANON;
  done(canonFx, repoFx);
});
