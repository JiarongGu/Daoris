import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { makeFixture, type Fixture } from '../test/_fixture.ts';
import { runCli } from '../src/cli.ts';
import { commit, git, history } from './support/family-git.ts';

/**
 * `daoris family check` with no service: the family file in the folder the members' checkouts share,
 * each member's `uses` from its `daoris.json`, each consumer's pins from its `daoris.family.lock`, and each pin read against the
 * producer's checkout by git's own files. Green on beta to alpha; red on a planted cycle and a planted branch pin.
 */

function run(cwd: string, ...argv: string[]): { code: number; out: string } {
  const lines: string[] = [];
  const code = runCli(['family', ...argv], cwd, (line) => lines.push(line), () => 2, null) as number;
  return { code, out: lines.join('\n') };
}

const manifest = (uses: string[]) => JSON.stringify({ source: 'daoris@0.0.1', packs: [], domain: { summary: 's', owns: [], accepts: [], uses } });

/** alpha and beta side by side under the family's folder, alpha a repository on `main` with two commits; beta pins nothing yet. */
function family(name: string): { fixture: Fixture; alpha: string; beta: string; first: string; head: string } {
  const fixture = makeFixture(`family-check/${name}`);
  fixture.write('daoris.family.json', JSON.stringify({ layers: [['alpha'], ['beta']] }, null, 2));
  fixture.write('alpha/daoris.json', manifest([]));
  fixture.write('beta/daoris.json', manifest(['alpha']));
  const alpha = join(fixture.root, 'alpha');
  const [first, head] = history(alpha, 2) as [string, string];
  return { fixture, alpha, beta: join(fixture.root, 'beta'), first, head };
}

const pin = (fixture: Fixture, commitOrName: string) =>
  fixture.write('beta/daoris.family.lock', JSON.stringify({ pins: [{ producer: 'alpha', commit: commitOrName }] }, null, 2));

test('beta pinned to a landed alpha commit is green, from the consumer\'s checkout', () => {
  const { fixture, beta, first } = family('green');
  pin(fixture, first);

  const { code, out } = run(beta, 'check');

  assert.equal(code, 0, out);
  assert.match(out, /2 members in 2 layers, 1 edge/);
  assert.match(out, new RegExp(`ok +beta → alpha at ${first.slice(0, 12)}, on alpha's line main`));
  fixture.cleanup();
});

test('a planted cycle is refused with its path', () => {
  const { fixture, beta, first } = family('cycle');
  pin(fixture, first);
  fixture.write('alpha/daoris.json', manifest(['beta']));

  const { code, out } = run(beta, 'check');

  assert.equal(code, 1, out);
  assert.match(out, /refused +a cycle: (alpha → beta → alpha|beta → alpha → beta)/);
  assert.match(out, /refused +alpha → beta: alpha is in layer 1 and beta in layer 2/);
  fixture.cleanup();
});

test('a planted branch pin is refused, and so is a tag, an abbreviated commit and a moving name', () => {
  const { fixture, alpha, beta, first } = family('names');
  git(alpha, 'tag', 'v1', first);
  for (const [name, said] of [
    ['main', /the pin `main` is a branch/],
    ['v1', /the pin `v1` is a tag/],
    [first.slice(0, 12), new RegExp(`the pin \`${first.slice(0, 12)}\` is not a full commit`)],
    ['latest', /the pin `latest` is not a full commit/],
  ] as const) {
    pin(fixture, name);
    const { code, out } = run(beta, 'check');
    assert.equal(code, 1, `${name}: ${out}`);
    assert.match(out, said, name);
    assert.match(out, /a pin is one full commit on alpha's line/, name);
  }
  fixture.cleanup();
});

test('a pin off the producer\'s line is refused, and a commit alpha does not hold is refused as unknown', () => {
  const { fixture, alpha, beta } = family('off-line');
  git(alpha, 'switch', '-q', '-c', 'topic');
  const topic = commit(alpha, 'on a branch, never landed');
  git(alpha, 'switch', '-q', 'main');

  pin(fixture, topic);
  const off = run(beta, 'check');
  assert.equal(off.code, 1, off.out);
  assert.match(off.out, new RegExp(`the pin ${topic.slice(0, 12)} is not on alpha's line main`));

  pin(fixture, 'c'.repeat(40));
  const unknown = run(beta, 'check');
  assert.equal(unknown.code, 1, unknown.out);
  assert.match(unknown.out, /alpha's checkout holds no commit cccccccccccc/);
  fixture.cleanup();
});

test('a pin is read through git\'s packs as well as its loose objects', () => {
  const { fixture, alpha, beta, first } = family('packed');
  git(alpha, 'gc', '-q');
  pin(fixture, first);

  const { code, out } = run(beta, 'check');

  assert.equal(code, 0, out);
  assert.match(out, new RegExp(`ok +beta → alpha at ${first.slice(0, 12)}`));
  fixture.cleanup();
});

test('the line is origin\'s HEAD where the producer\'s checkout is a clone', () => {
  const { fixture, alpha, beta, head } = family('clone');
  git(alpha, 'branch', '-q', '-m', 'main', 'trunk');
  const clone = join(fixture.root, 'alpha-clone');
  git(fixture.root, 'clone', '-q', alpha, clone);
  git(clone, 'switch', '-q', '-c', 'local-work');
  fixture.write('daoris.family.json', JSON.stringify({ layers: [['alpha-clone'], ['beta']] }));
  fixture.write('alpha-clone/daoris.json', manifest([]));
  fixture.write('beta/daoris.json', manifest(['alpha-clone']));
  fixture.write('beta/daoris.family.lock', JSON.stringify({ pins: [{ producer: 'alpha-clone', commit: head }] }));

  const { code, out } = run(beta, 'check');

  assert.equal(code, 0, out);
  assert.match(out, /on alpha-clone's line trunk/);
  fixture.cleanup();
});

test('a uses with no pin is warned and the check stays green', () => {
  const { fixture, beta } = family('unpinned');

  const { code, out } = run(beta, 'check');

  assert.equal(code, 0, out);
  assert.match(out, /warned +beta uses alpha and pins no commit of it/);
  fixture.cleanup();
});

test('with no family file beside the repository there is no family, and the check says so and passes', () => {
  const fixture = makeFixture('family-check/none');
  fixture.write('alone/daoris.json', manifest([]));

  const { code, out } = run(join(fixture.root, 'alone'), 'check');

  assert.equal(code, 0, out);
  assert.match(out, /no family file at .*daoris\.family\.json: no family, nothing to check/);
  fixture.cleanup();
});

test('--family names the file from anywhere', () => {
  const { fixture, first } = family('named');
  pin(fixture, first);

  const { code, out } = run(fixture.root, 'check', '--family', join(fixture.root, 'daoris.family.json'));

  assert.equal(code, 0, out);
  assert.match(out, /ok +beta → alpha/);
  fixture.cleanup();
});

test('family with no verb, or another verb, is a usage error', () => {
  const fixture = makeFixture('family-check/usage');
  assert.equal(run(fixture.root).code, 2);
  assert.equal(run(fixture.root, 'move').code, 2);
  fixture.cleanup();
});
