import { test } from 'vitest';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, utimesSync } from 'node:fs';
import { join } from 'node:path';
import { fixtureFolder } from './fixture-home.ts';
import { DRIFT_BATCH, DRIFT_SCHEMA, driftFile, readPairs, treeDrifts } from './tree-drift.ts';
import type { DriftGit } from './tree-drift.ts';

const id = (char: string): string => char.repeat(40);
const [A, B, C, GONE, TO, OTHER] = ['a', 'b', 'c', 'd', 'e', 'f'].map(id) as [string, string, string, string, string, string];

/** A git that knows `paths` of each tree (or commit, `commits`) from `TO`, lacks `GONE`, and counts what it is asked. */
function fakeGit(paths: Record<string, string[]>, commits: string[] = []): { git: DriftGit; asked: string[][] } {
  const asked: string[][] = [];
  const git: DriftGit = (args, input = '') => {
    asked.push(args);
    const lines = input.split('\n').filter(Boolean);
    const kind = (name: string): string => (commits.includes(name) ? 'commit' : paths[name] ? 'tree' : 'missing');
    if (args.includes('--batch-check')) return { status: 0, out: lines.map((line) => `${line} ${kind(line)}${kind(line) === 'missing' ? '' : ' 10'}\n`).join('') };
    if (args.includes('diff-tree')) {
      // As git answers: each pair echoed, then its paths; a tree git lacks is passed over without a word.
      return { status: 0, out: lines.filter((line) => paths[line.split(' ')[0]!]).map((line) => `${line}\n${paths[line.split(' ')[0]!]!.map((path) => `${path}\0`).join('')}`).join('') };
    }
    const from = args.at(-2)!;
    return paths[from] ? { status: 0, out: paths[from].map((path) => `${path}\0`).join('') } : { status: 128, out: '' };
  };
  return { git, asked };
}

const STORE = { [A]: ['docs/a.md', 'src/b.ts'], [B]: ['café notes.md'], [C]: [], [TO]: [] };

test("a pair's drift is kept by the judged tree and the schema: the same tree is the same file, any other tree or schema another", () => {
  assert.equal(driftFile(TO), driftFile(TO));
  assert.notEqual(driftFile(TO), driftFile(OTHER));
  assert.notEqual(driftFile(TO, DRIFT_SCHEMA), driftFile(TO, DRIFT_SCHEMA + 1));
});

test('the batched answer is read pair by pair: each echoed pair its paths in order, a name with spaces whole, an empty pair empty and a pair git passed over absent', () => {
  const out = `${A} ${TO}\ndocs/a.md\0src/b.ts\0${C} ${TO}\n${B} ${TO}\ncafé notes.md\0`;
  const pairs = readPairs(out);
  assert.deepEqual(pairs.get(`${A} ${TO}`), ['docs/a.md', 'src/b.ts']);
  assert.deepEqual(pairs.get(`${C} ${TO}`), []);
  assert.deepEqual(pairs.get(`${B} ${TO}`), ['café notes.md']);
  assert.equal(pairs.has(`${GONE} ${TO}`), false);
  assert.equal(readPairs('').size, 0);
});

test('every pair a judgement lacks is asked of git at once, in batches; the same tree and none are answered without git, and a tree git lacks is none', () => {
  const { dir, remove } = fixtureFolder('tree-drift-ask');
  try {
    const { git, asked } = fakeGit(STORE);
    const drifts = treeDrifts({ to: TO, git, dir, writable: () => true });
    drifts.ask([A, B, A, C, GONE, TO, null, undefined]);
    assert.deepEqual(asked.filter((args) => args.includes('diff-tree')).length, 1, 'one git start for every pair');
    assert.deepEqual(asked.filter((args) => args.includes('--batch-check')).length, 1, 'the tree git passed over is looked for once');
    assert.deepEqual(drifts.drift(A), ['docs/a.md', 'src/b.ts']);
    assert.deepEqual(drifts.drift(B), ['café notes.md']);
    assert.deepEqual(drifts.drift(C), []);
    assert.equal(drifts.drift(GONE), null);
    assert.deepEqual(drifts.drift(TO), []);
    assert.equal(drifts.drift(null), null);
    assert.equal(asked.length, 2, 'what was asked is not asked again');

    // A commit, which the batch would read as a commit and its parent, is asked alone, as `git diff` of the pair asks it.
    const COMMIT = id('9');
    const mixed = fakeGit({ ...STORE, [COMMIT]: ['by/commit.md'] }, [COMMIT]);
    const both = treeDrifts({ to: TO, git: mixed.git, dir: join(dir, 'none kept'), writable: () => false });
    both.ask([A, COMMIT, 'not-an-id']);
    assert.deepEqual([both.drift(A), both.drift(COMMIT), both.drift('not-an-id')], [['docs/a.md', 'src/b.ts'], ['by/commit.md'], null]);
    assert.deepEqual(mixed.asked.map((args) => args.find((arg) => ['cat-file', 'diff-tree', 'diff'].includes(arg))), ['cat-file', 'diff-tree', 'diff', 'diff']);

    const many = Object.fromEntries(Array.from({ length: DRIFT_BATCH + 1 }, (_, i) => [i.toString(16).padStart(40, '0'), [`p${i}`]]));
    const wide = fakeGit(many);
    treeDrifts({ to: TO, git: wide.git, dir, writable: () => false }).ask(Object.keys(many));
    assert.equal(wide.asked.filter((args) => args.includes('diff-tree')).length, 2, `${DRIFT_BATCH} pairs a start`);
  } finally {
    remove();
  }
});

test('a pair asked once is read back from its file on the same judged tree, git asked nothing; another tree asks again', () => {
  const { dir, remove } = fixtureFolder('tree-drift-cache');
  try {
    const first = fakeGit(STORE);
    treeDrifts({ to: TO, git: first.git, dir, writable: () => true }).ask([A, B, C, GONE]);
    assert.ok(existsSync(join(dir, driftFile(TO))));

    const again = fakeGit(STORE);
    const hit = treeDrifts({ to: TO, git: again.git, dir, writable: () => true });
    hit.ask([A, B, C]);
    assert.equal(again.asked.length, 0, 'a hit asks git nothing');
    assert.deepEqual([hit.drift(A), hit.drift(B), hit.drift(C)], [['docs/a.md', 'src/b.ts'], ['café notes.md'], []]);
    hit.ask([GONE]);
    assert.deepEqual(again.asked.map((args) => args[1]), ['diff'], 'a tree git lacked is not kept: one alone is asked as `git diff` asks it');
    assert.equal(hit.drift(GONE), null);

    const other = fakeGit({ [A]: ['x'] });
    const miss = treeDrifts({ to: OTHER, git: other.git, dir, writable: () => true });
    assert.deepEqual(miss.drift(A), ['x'], 'a pair of another judged tree is its own');
    assert.equal(other.asked.length, 1);
    assert.ok(existsSync(join(dir, driftFile(OTHER))), 'a pair asked alone is kept too');
  } finally {
    remove();
  }
});

test('nothing is kept where the folder may not be written, and only the newest few judged trees stay', () => {
  const { dir, remove } = fixtureFolder('tree-drift-keep');
  try {
    const { git } = fakeGit(STORE);
    treeDrifts({ to: TO, git, dir: join(dir, 'unignored'), writable: () => false }).ask([A]);
    assert.equal(existsSync(join(dir, 'unignored')), false);

    const trees = Array.from({ length: 10 }, (_, i) => String(i).repeat(40));
    trees.forEach((to, day) => {
      treeDrifts({ to, git: fakeGit({ [A]: [`p${day}`] }).git, dir, writable: () => true }).ask([A]);
      utimesSync(join(dir, driftFile(to)), new Date(Date.UTC(2020, 0, day + 1)), new Date(Date.UTC(2020, 0, day + 1)));
    });
    treeDrifts({ to: TO, git, dir, writable: () => true }).ask([A]);
    const kept = readdirSync(dir).filter((name) => name.startsWith('drift-'));
    assert.equal(kept.length, 8);
    assert.ok(kept.includes(driftFile(TO)) && kept.includes(driftFile(trees[9]!)) && !kept.includes(driftFile(trees[0]!)), 'the oldest go');
  } finally {
    remove();
  }
});
