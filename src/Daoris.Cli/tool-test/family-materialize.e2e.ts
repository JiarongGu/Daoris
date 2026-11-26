import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture, type Fixture } from '../test/_fixture.ts';
import { runCli } from '../src/cli.ts';
import { commitFiles, git } from './support/family-git.ts';

/**
 * `daoris family materialize`: each pin of this repository's own `daoris.family.lock` written as its producer's tree at the pinned
 * commit, read from the producer's git store beside the family file and never from its working tree, into the folder the
 * repository's build reads. Git lays each repository out; the command reads git's files and starts no process.
 */

function run(cwd: string, ...argv: string[]): { code: number; out: string } {
  const lines: string[] = [];
  const code = runCli(['family', ...argv], cwd, (line) => lines.push(line), () => 2, null) as number;
  return { code, out: lines.join('\n') };
}

const manifest = (uses: string[]) => JSON.stringify({ source: 'daoris@0.0.1', packs: [], domain: { summary: 's', owns: [], accepts: [], uses } });

const FIRST_API = '{ "calls": ["advance"] }\n';
const HEAD_API = '{ "calls": [] }\n';

/** alpha, a repository on `main` of two commits, and beta, which uses it, side by side under the family's folder. */
function family(name: string): { fixture: Fixture; alpha: string; beta: string; first: string; head: string } {
  const fixture = makeFixture(`family-materialize/${name}`);
  fixture.write('daoris.family.json', JSON.stringify({ layers: [['alpha'], ['beta']] }, null, 2));
  fixture.write('beta/daoris.json', manifest(['alpha']));
  const alpha = join(fixture.root, 'alpha');
  fixture.write('alpha/.keep', '');
  git(alpha, 'init', '-q', '-b', 'main');
  const first = commitFiles(alpha, {
    'daoris.json': manifest([]), 'api.json': FIRST_API, 'src/deep/run.sh*': '#!/bin/sh\necho run\n', 'gone.txt': 'only at the first\n',
  }, 'first');
  const head = commitFiles(alpha, { 'api.json': HEAD_API, 'gone.txt': null }, 'second');
  return { fixture, alpha, beta: join(fixture.root, 'beta'), first, head };
}

const pin = (fixture: Fixture, folder: string, commitOrName: string, producer = 'alpha') =>
  fixture.write(`${folder}/daoris.family.lock`, JSON.stringify({ pins: [{ producer, commit: commitOrName }] }, null, 2));

/** Every file under `root` with its size and modification time: a file touched at all reads as changed. */
function snapshot(root: string): string[] {
  const files: string[] = [];
  const walk = (dir: string, prefix: string): void => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const abs = join(dir, entry.name);
      if (entry.isDirectory()) walk(abs, `${prefix}${entry.name}/`);
      else files.push(`${prefix}${entry.name} ${statSync(abs).size} ${statSync(abs).mtimeMs}`);
    }
  };
  walk(root, '');
  return files.sort();
}

test('a pin is written as its producer\'s tree at the pinned commit, never the producer\'s checkout as it stands', () => {
  const { fixture, alpha, beta, first } = family('written');
  pin(fixture, 'beta', first);
  fixture.write('alpha/api.json', 'an uncommitted edit in the sibling checkout\n');
  const before = snapshot(alpha);

  const { code, out } = run(beta, 'materialize');

  assert.equal(code, 0, out);
  assert.match(out, new RegExp(`wrote +alpha at ${first.slice(0, 12)} into pinned/alpha \\(5 files\\)`));
  assert.match(out, /materialized 1 pin into pinned/);
  assert.equal(readFileSync(join(beta, 'pinned/alpha/api.json'), 'utf8'), FIRST_API);
  assert.equal(readFileSync(join(beta, 'pinned/alpha/gone.txt'), 'utf8'), 'only at the first\n');
  assert.equal(readFileSync(join(beta, 'pinned/alpha/src/deep/run.sh'), 'utf8'), '#!/bin/sh\necho run\n');
  if (process.platform !== 'win32') assert.notEqual(statSync(join(beta, 'pinned/alpha/src/deep/run.sh')).mode & 0o111, 0);
  assert.equal(existsSync(join(beta, 'pinned/alpha/.git')), false);
  assert.deepEqual(JSON.parse(readFileSync(join(beta, 'pinned/alpha/.daoris-pin'), 'utf8')), { producer: 'alpha', commit: first });
  assert.deepEqual(snapshot(alpha), before, 'materialize touched the producer\'s checkout');
  fixture.cleanup();
});

test('the same pin again is left as it is, and a moved pin replaces the producer\'s folder whole', () => {
  const { fixture, beta, first, head } = family('moved');
  pin(fixture, 'beta', first);
  assert.equal(run(beta, 'materialize').code, 0);

  const again = run(beta, 'materialize');
  assert.equal(again.code, 0, again.out);
  assert.match(again.out, new RegExp(`unchanged +alpha at ${first.slice(0, 12)} in pinned/alpha`));

  pin(fixture, 'beta', head);
  const moved = run(beta, 'materialize');
  assert.equal(moved.code, 0, moved.out);
  assert.equal(readFileSync(join(beta, 'pinned/alpha/api.json'), 'utf8'), HEAD_API);
  assert.equal(existsSync(join(beta, 'pinned/alpha/gone.txt')), false);
  fixture.cleanup();
});

test('a producer whose objects are packed, deltas included, is written the same', () => {
  const { fixture, alpha, beta, first } = family('packed');
  git(alpha, 'gc', '-q', '--aggressive');
  assert.equal(readdirSync(join(alpha, '.git/objects/pack')).some((name) => name.endsWith('.pack')), true);
  pin(fixture, 'beta', first);

  const { code, out } = run(beta, 'materialize');

  assert.equal(code, 0, out);
  assert.equal(readFileSync(join(beta, 'pinned/alpha/api.json'), 'utf8'), FIRST_API);
  fixture.cleanup();
});

test('a pin the family check refuses is said and nothing is written', () => {
  const { fixture, beta } = family('refused');
  pin(fixture, 'beta', 'main');

  const branch = run(beta, 'materialize');
  assert.equal(branch.code, 1, branch.out);
  assert.match(branch.out, /refused +beta → alpha: the pin `main` is a branch/);
  assert.match(branch.out, /nothing materialized: 1 refused/);
  assert.equal(existsSync(join(beta, 'pinned')), false);

  pin(fixture, 'beta', 'f'.repeat(40), 'gamma');
  const stranger = run(beta, 'materialize');
  assert.equal(stranger.code, 1, stranger.out);
  assert.match(stranger.out, /refused +beta pins gamma, which is no member of the family/);
  fixture.cleanup();
});

test('a producer\'s folder that materialize did not write is refused and left as it is', () => {
  const { fixture, beta, first } = family('foreign');
  fixture.write('beta/pinned/alpha/mine.txt', 'the repository\'s own file\n');
  pin(fixture, 'beta', first);

  const { code, out } = run(beta, 'materialize');

  assert.equal(code, 1, out);
  assert.match(out, /pinned\/alpha holds files materialize did not write/);
  assert.equal(readFileSync(join(beta, 'pinned/alpha/mine.txt'), 'utf8'), 'the repository\'s own file\n');
  assert.equal(existsSync(join(beta, 'pinned/alpha/api.json')), false);
  fixture.cleanup();
});

test('from a session\'s tree the family is found through the tree\'s checkout, and the tree\'s own lock is read', () => {
  const { fixture, beta, first, head } = family('tree');
  pin(fixture, 'beta', head);
  git(beta, 'init', '-q', '-b', 'main');
  commitFiles(beta, {}, 'beta pins alpha at its head');
  const tree = join(fixture.root, 'trees', 's-1');
  git(beta, 'worktree', 'add', '-q', '-b', 'session', tree);
  pin(fixture, 'trees/s-1', first);

  const { code, out } = run(tree, 'materialize');

  assert.equal(code, 0, out);
  assert.equal(readFileSync(join(tree, 'pinned/alpha/api.json'), 'utf8'), FIRST_API);
  assert.equal(existsSync(join(beta, 'pinned')), false);
  const check = run(tree, 'check');
  assert.equal(check.code, 0, check.out);
  assert.match(check.out, /the family at .*daoris\.family\.json: 2 members/);
  fixture.cleanup();
});

test('--into names another folder of the repository, and one outside it is refused', () => {
  const { fixture, beta, first } = family('into');
  pin(fixture, 'beta', first);

  const inside = run(beta, 'materialize', '--into', 'build/deps');
  assert.equal(inside.code, 0, inside.out);
  assert.equal(readFileSync(join(beta, 'build/deps/alpha/api.json'), 'utf8'), FIRST_API);

  const outside = run(beta, 'materialize', '--into', '../elsewhere');
  assert.equal(outside.code, 2, outside.out);
  assert.equal(existsSync(join(fixture.root, 'elsewhere')), false);
  fixture.cleanup();
});

test('a repository that pins nothing has nothing to materialize', () => {
  const { fixture, beta } = family('unpinned');

  const { code, out } = run(beta, 'materialize');

  assert.equal(code, 0, out);
  assert.match(out, /no daoris\.family\.lock here: nothing pinned, nothing to materialize/);
  fixture.cleanup();
});
