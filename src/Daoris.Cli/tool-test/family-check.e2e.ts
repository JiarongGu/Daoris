import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { makeFixture, type Fixture } from '../test/_fixture.ts';
import { runCli } from '../src/cli.ts';
import { commit, git, history } from './support/family-git.ts';

/**
 * `daoris family check` with no service: the family file in the folder the members' checkouts share,
 * each member's `uses` from its `daoris.json`, each consumer's pins from its `daoris.family.lock`, and each pin read against the
 * producer's checkout by git's own files. Green on game to engine; red on a planted cycle and a planted branch pin.
 */

function run(cwd: string, ...argv: string[]): { code: number; out: string } {
  const lines: string[] = [];
  const code = runCli(['family', ...argv], cwd, (line) => lines.push(line), () => 2, null) as number;
  return { code, out: lines.join('\n') };
}

const manifest = (uses: string[]) => JSON.stringify({ source: 'daoris@0.0.1', packs: [], domain: { summary: 's', owns: [], accepts: [], uses } });

/** engine and game side by side under the family's folder, engine a repository on `main` with two commits; game pins nothing yet. */
function family(name: string): { fixture: Fixture; engine: string; game: string; first: string; head: string } {
  const fixture = makeFixture(`family-check/${name}`);
  fixture.write('daoris.family.json', JSON.stringify({ layers: [['engine'], ['game']] }, null, 2));
  fixture.write('engine/daoris.json', manifest([]));
  fixture.write('game/daoris.json', manifest(['engine']));
  const engine = join(fixture.root, 'engine');
  const [first, head] = history(engine, 2) as [string, string];
  return { fixture, engine, game: join(fixture.root, 'game'), first, head };
}

const pin = (fixture: Fixture, commitOrName: string) =>
  fixture.write('game/daoris.family.lock', JSON.stringify({ pins: [{ producer: 'engine', commit: commitOrName }] }, null, 2));

test('game pinned to a landed engine commit is green, from the consumer\'s checkout', () => {
  const { fixture, game, first } = family('green');
  pin(fixture, first);

  const { code, out } = run(game, 'check');

  assert.equal(code, 0, out);
  assert.match(out, /2 members in 2 layers, 1 edge/);
  assert.match(out, new RegExp(`ok +game → engine at ${first.slice(0, 12)}, on engine's line main`));
  fixture.cleanup();
});

test('a planted cycle is refused with its path', () => {
  const { fixture, game, first } = family('cycle');
  pin(fixture, first);
  fixture.write('engine/daoris.json', manifest(['game']));

  const { code, out } = run(game, 'check');

  assert.equal(code, 1, out);
  assert.match(out, /refused +a cycle: (engine → game → engine|game → engine → game)/);
  assert.match(out, /refused +engine → game: engine is in layer 1 and game in layer 2/);
  fixture.cleanup();
});

test('a planted branch pin is refused, and so is a tag, an abbreviated commit and a moving name', () => {
  const { fixture, engine, game, first } = family('names');
  git(engine, 'tag', 'v1', first);
  for (const [name, said] of [
    ['main', /the pin `main` is a branch/],
    ['v1', /the pin `v1` is a tag/],
    [first.slice(0, 12), new RegExp(`the pin \`${first.slice(0, 12)}\` is not a full commit`)],
    ['latest', /the pin `latest` is not a full commit/],
  ] as const) {
    pin(fixture, name);
    const { code, out } = run(game, 'check');
    assert.equal(code, 1, `${name}: ${out}`);
    assert.match(out, said, name);
    assert.match(out, /a pin is one full commit on engine's line/, name);
  }
  fixture.cleanup();
});

test('a pin off the producer\'s line is refused, and a commit engine does not hold is refused as unknown', () => {
  const { fixture, engine, game } = family('off-line');
  git(engine, 'switch', '-q', '-c', 'topic');
  const topic = commit(engine, 'on a branch, never landed');
  git(engine, 'switch', '-q', 'main');

  pin(fixture, topic);
  const off = run(game, 'check');
  assert.equal(off.code, 1, off.out);
  assert.match(off.out, new RegExp(`the pin ${topic.slice(0, 12)} is not on engine's line main`));

  pin(fixture, 'c'.repeat(40));
  const unknown = run(game, 'check');
  assert.equal(unknown.code, 1, unknown.out);
  assert.match(unknown.out, /engine's checkout holds no commit cccccccccccc/);
  fixture.cleanup();
});

test('a pin is read through git\'s packs as well as its loose objects', () => {
  const { fixture, engine, game, first } = family('packed');
  git(engine, 'gc', '-q');
  pin(fixture, first);

  const { code, out } = run(game, 'check');

  assert.equal(code, 0, out);
  assert.match(out, new RegExp(`ok +game → engine at ${first.slice(0, 12)}`));
  fixture.cleanup();
});

test('the line is origin\'s HEAD where the producer\'s checkout is a clone', () => {
  const { fixture, engine, game, head } = family('clone');
  git(engine, 'branch', '-q', '-m', 'main', 'trunk');
  const clone = join(fixture.root, 'engine-clone');
  git(fixture.root, 'clone', '-q', engine, clone);
  git(clone, 'switch', '-q', '-c', 'local-work');
  fixture.write('daoris.family.json', JSON.stringify({ layers: [['engine-clone'], ['game']] }));
  fixture.write('engine-clone/daoris.json', manifest([]));
  fixture.write('game/daoris.json', manifest(['engine-clone']));
  fixture.write('game/daoris.family.lock', JSON.stringify({ pins: [{ producer: 'engine-clone', commit: head }] }));

  const { code, out } = run(game, 'check');

  assert.equal(code, 0, out);
  assert.match(out, /on engine-clone's line trunk/);
  fixture.cleanup();
});

test('a uses with no pin is warned and the check stays green', () => {
  const { fixture, game } = family('unpinned');

  const { code, out } = run(game, 'check');

  assert.equal(code, 0, out);
  assert.match(out, /warned +game uses engine and pins no commit of it/);
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
  assert.match(out, /ok +game → engine/);
  fixture.cleanup();
});

test('family with no verb, or another verb, is a usage error', () => {
  const fixture = makeFixture('family-check/usage');
  assert.equal(run(fixture.root).code, 2);
  assert.equal(run(fixture.root, 'move').code, 2);
  fixture.cleanup();
});
