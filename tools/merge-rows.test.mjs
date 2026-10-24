/**
 * ROUTERMERGE1: the router's merge driver merges table rows by their first cell three ways, and prose as text.
 *
 *   node --test tools/merge-rows.test.mjs
 *
 * Run by `npm run verify`. Each case is a base and two sides; the last merges two branches of a scratch repository under
 * `_fixtures/` through git itself, with the driver configured there as `--install` configures it here.
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { DRIVER, main, mergeRows } from './merge-rows.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const scratch = join(here, '..', '_fixtures', 'merge-rows-test');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const lines = (...each) => `${each.join('\n')}\n`;
const router = (first, second) => lines(
  '# Router', '', 'Intro prose.', '', '## Guides', '', '| File | Purpose |', '|---|---|', ...first, '',
  '## Designs', '', 'Designs, newest last.', '', '| File | Kind | Rows |', '|---|---|---|', ...second, '', 'Closing prose.',
);
const guides = ['| `a.md` | A guide |', '| `b.md` | B guide |', '| `c.md` | C guide |'];
const designs = ['| `x.md` | Proposed | X1 |', '| `y.md` | Proposed | Y1 |', '| `z.md` | Proposed | Z1 |'];
const base = router(guides, designs);
const swap = (rows, at, row) => rows.map((each, index) => (index === at ? row : each));
const count = (text, needle) => text.split(needle).length - 1;

test('a row one side changed takes that side, whichever side it is', () => {
  const changed = router(guides, swap(designs, 1, '| `y.md` | Accepted (D9) | Y1 |'));
  for (const [ours, theirs] of [[changed, base], [base, changed]]) {
    const merged = mergeRows(base, ours, theirs);
    assert.equal(merged.conflicts, 0);
    assert.equal(merged.text, changed);
  }
});

test('a branch carrying a row main has since replaced leaves main\'s row, once', () => {
  // The incident: main replaced y's row; the branch, cut before, changed a neighbouring row and still carries the old y.
  const main = router(guides, swap(designs, 1, '| `y.md` | Accepted (D9) | Y1 built |'));
  const branch = router(guides, swap(designs, 2, '| `z.md` | Accepted (D10) | Z1 |'));
  const merged = mergeRows(base, main, branch);
  assert.equal(merged.conflicts, 0);
  assert.equal(merged.text, router(guides, [designs[0], '| `y.md` | Accepted (D9) | Y1 built |', '| `z.md` | Accepted (D10) | Z1 |']));
  assert.equal(count(merged.text, '| `y.md`'), 1);
});

test('a row both sides changed differently is a conflict marked in git\'s own form', () => {
  const ours = router(guides, swap(designs, 1, '| `y.md` | Accepted (D9) | Y1 |'));
  const theirs = router(guides, swap(designs, 1, '| `y.md` | Declined | Y1 |'));
  const merged = mergeRows(base, ours, theirs);
  assert.equal(merged.conflicts, 1);
  assert.match(merged.text, /\n<{7} ours\n\| `y\.md` \| Accepted \(D9\) \| Y1 \|\n={7}\n\| `y\.md` \| Declined \| Y1 \|\n>{7} theirs\n/);
  // The marked rows stand where the row stood, between its neighbours.
  assert.ok(merged.text.indexOf('| `x.md`') < merged.text.indexOf('<<<<<<<'));
  assert.ok(merged.text.indexOf('>>>>>>>') < merged.text.indexOf('| `z.md`'));
  assert.match(mergeRows(base, ours, theirs, { markerSize: 9 }).text, /\n<{9} ours\n/);
});

test('a row both sides changed the same way is kept once', () => {
  const both = router(guides, swap(designs, 1, '| `y.md` | Accepted (D9) | Y1 |'));
  const merged = mergeRows(base, both, both);
  assert.equal(merged.conflicts, 0);
  assert.equal(merged.text, both);
});

test('a row one side removed and the other left alone goes', () => {
  const removed = router(guides, [designs[0], designs[2]]);
  for (const [ours, theirs] of [[removed, base], [base, removed]]) {
    const merged = mergeRows(base, ours, theirs);
    assert.equal(merged.conflicts, 0);
    assert.equal(merged.text, removed);
  }
});

test('a row one side removed and the other changed is a conflict', () => {
  const removed = router(guides, [designs[0], designs[2]]);
  const changed = router(guides, swap(designs, 1, '| `y.md` | Accepted (D9) | Y1 |'));
  const merged = mergeRows(base, removed, changed);
  assert.equal(merged.conflicts, 1);
  assert.match(merged.text, /\n<{7} ours\n={7}\n\| `y\.md` \| Accepted \(D9\) \| Y1 \|\n>{7} theirs\n/);
});

test('a row both sides added identically is kept once', () => {
  const added = router(guides, [...designs, '| `w.md` | Proposed | W1 |']);
  const merged = mergeRows(base, added, added);
  assert.equal(merged.conflicts, 0);
  assert.equal(merged.text, added);
  assert.equal(count(merged.text, '| `w.md`'), 1);
});

test('a row both sides added differently is a conflict', () => {
  const merged = mergeRows(base, router(guides, [...designs, '| `w.md` | Proposed | W1 |']),
    router(guides, [...designs, '| `w.md` | Proposed | W2 |']));
  assert.equal(merged.conflicts, 1);
  assert.equal(count(merged.text, '| `w.md`'), 2);
});

test('rows keep their table and their order', () => {
  // Ours adds a guide after a and removes c; theirs adds a design after x and one at the end of the designs.
  const ours = router([guides[0], '| `a2.md` | A2 guide |', guides[1]], designs);
  const theirs = router(guides, [designs[0], '| `x2.md` | Proposed | X2 |', designs[1], designs[2], '| `zz.md` | Proposed | ZZ |']);
  const merged = mergeRows(base, ours, theirs);
  assert.equal(merged.conflicts, 0);
  assert.equal(merged.text, router([guides[0], '| `a2.md` | A2 guide |', guides[1]],
    [designs[0], '| `x2.md` | Proposed | X2 |', designs[1], designs[2], '| `zz.md` | Proposed | ZZ |']));
});

test('adjacent rows each side changed both land, where a text merge conflicts', () => {
  const ours = router(guides, swap(designs, 0, '| `x.md` | Accepted | X1 |'));
  const theirs = router(guides, swap(designs, 1, '| `y.md` | Accepted | Y1 |'));
  const merged = mergeRows(base, ours, theirs);
  assert.equal(merged.conflicts, 0);
  assert.equal(merged.text, router(guides, ['| `x.md` | Accepted | X1 |', '| `y.md` | Accepted | Y1 |', designs[2]]));
});

test('prose outside the tables merges as text, conflicting where both changed it', () => {
  const ours = base.replace('Intro prose.', 'Intro prose, ours.');
  const theirs = base.replace('Closing prose.', 'Closing prose, theirs.');
  const clean = mergeRows(base, ours, theirs);
  assert.equal(clean.conflicts, 0);
  assert.equal(clean.text, base.replace('Intro prose.', 'Intro prose, ours.').replace('Closing prose.', 'Closing prose, theirs.'));
  const both = mergeRows(base, ours, base.replace('Intro prose.', 'Intro prose, theirs.'));
  assert.equal(both.conflicts, 1);
  assert.match(both.text, /\n<{7} ours\nIntro prose, ours\.\n={7}\nIntro prose, theirs\.\n>{7} theirs\n/);
});

test('a table one side added comes with its rows', () => {
  const ours = `${base}\n## New\n\n| Key | Value |\n|---|---|\n| k | v |\n`;
  const theirs = router(guides, swap(designs, 2, '| `z.md` | Accepted | Z1 |'));
  const merged = mergeRows(base, ours, theirs);
  assert.equal(merged.conflicts, 0);
  assert.equal(merged.text, `${theirs}\n## New\n\n| Key | Value |\n|---|---|\n| k | v |\n`);
});

test('the driver writes the result over ours and exits 0 clean, 1 conflicted, 2 on a tool error', () => {
  const dir = join(scratch, 'driver');
  mkdirSync(dir, { recursive: true });
  const file = (name, text) => { writeFileSync(join(dir, name), text); return join(dir, name); };
  const quiet = { out: () => {}, err: () => {} };
  const O = file('O', base);
  const A = file('A', router(guides, swap(designs, 0, '| `x.md` | Accepted | X1 |')));
  const B = file('B', router(guides, swap(designs, 1, '| `y.md` | Accepted | Y1 |')));
  assert.equal(main([O, A, B, '7', 'docs/README.md'], quiet), 0);
  assert.equal(readFileSync(A, 'utf8'), router(guides, ['| `x.md` | Accepted | X1 |', '| `y.md` | Accepted | Y1 |', designs[2]]));
  const C = file('C', router(guides, swap(designs, 1, '| `y.md` | Declined | Y1 |')));
  assert.equal(main([O, A, C, '7', 'docs/README.md'], quiet), 1);
  assert.match(readFileSync(A, 'utf8'), /^<{7} ours$/m);
  assert.equal(main([O, join(dir, 'missing'), B], quiet), 2);
  assert.equal(main([], quiet), 2);
});

test('a CRLF ours keeps its line endings', () => {
  const crlf = (text) => text.replace(/\n/g, '\r\n');
  const merged = mergeRows(base, crlf(router(guides, swap(designs, 0, '| `x.md` | Accepted | X1 |'))), base);
  assert.equal(merged.text, crlf(router(guides, swap(designs, 0, '| `x.md` | Accepted | X1 |'))));
});

test('two branches that each edited one router row merge with each row once (a scratch repository)', () => {
  const repo = join(scratch, 'repo');
  mkdirSync(join(repo, 'docs'), { recursive: true });
  const env = Object.fromEntries(Object.entries(process.env)
    .filter(([key]) => !/^GIT_(DIR|WORK_TREE|INDEX_FILE|COMMON_DIR|OBJECT_DIRECTORY)$/i.test(key)));
  const git = (...args) => {
    const run = spawnSync('git', ['-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid', '-c', 'core.autocrlf=false', ...args],
      { cwd: repo, encoding: 'utf8', env, windowsHide: true });
    return run;
  };
  const ok = (...args) => { const run = git(...args); assert.equal(run.status, 0, `${args.join(' ')}: ${run.stderr}${run.stdout}`); return run.stdout; };
  ok('init', '-q', '-b', 'main');
  // The driver as `--install` sets it, but naming this checkout's tool, since the scratch repository has no tools/.
  assert.equal(main(['--install'], { out: () => {}, err: () => {}, cwd: repo, env }), 0);
  assert.equal(ok('config', '--get', 'merge.rows.driver').trim(), DRIVER);
  ok('config', 'merge.rows.driver', DRIVER.replace('tools/merge-rows.mjs', JSON.stringify(join(here, 'merge-rows.mjs').replace(/\\/g, '/'))));
  writeFileSync(join(repo, '.gitattributes'), '* text=auto eol=lf\ndocs/README.md merge=rows\n');
  writeFileSync(join(repo, 'docs', 'README.md'), base);
  ok('add', '-A');
  ok('commit', '-q', '-m', 'base');
  ok('checkout', '-q', '-b', 'one');
  writeFileSync(join(repo, 'docs', 'README.md'), router(guides, swap(designs, 0, '| `x.md` | Accepted (D1) | X1 |')));
  ok('commit', '-q', '-am', 'one');
  ok('checkout', '-q', '-b', 'two', 'main');
  writeFileSync(join(repo, 'docs', 'README.md'), router(guides, swap(designs, 1, '| `y.md` | Accepted (D2) | Y1 |')));
  ok('commit', '-q', '-am', 'two');
  ok('checkout', '-q', 'main');
  ok('merge', '-q', '--no-ff', '-m', 'merge one', 'one');
  ok('merge', '-q', '--no-ff', '-m', 'merge two', 'two');
  const merged = readFileSync(join(repo, 'docs', 'README.md'), 'utf8');
  assert.equal(merged, router(guides, ['| `x.md` | Accepted (D1) | X1 |', '| `y.md` | Accepted (D2) | Y1 |', designs[2]]));
  for (const key of ['| `x.md`', '| `y.md`', '| `z.md`']) assert.equal(count(merged, key), 1, key);
  // A branch still carrying y's old row, with one change of its own elsewhere, leaves y once.
  ok('checkout', '-q', '-b', 'stale', 'one');
  writeFileSync(join(repo, 'docs', 'README.md'), router(swap(guides, 2, '| `c.md` | C guide, revised |'), swap(designs, 0, '| `x.md` | Accepted (D1) | X1 |')));
  ok('commit', '-q', '-am', 'stale');
  ok('checkout', '-q', 'main');
  ok('merge', '-q', '--no-ff', '-m', 'merge stale', 'stale');
  const again = readFileSync(join(repo, 'docs', 'README.md'), 'utf8');
  assert.equal(count(again, '| `y.md`'), 1);
  assert.match(again, /\| `y\.md` \| Accepted \(D2\) \| Y1 \|/);
  assert.match(again, /C guide, revised/);
});
