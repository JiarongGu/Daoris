import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
// @ts-expect-error — untyped workspace tooling; see desktop-publish.test.ts
import { freshContracts } from '../../../tools/desktop-publish.mjs';
// @ts-expect-error — untyped workspace tooling; see above
import { OUTPUTS, staleness } from '../../../tools/contracts.mjs';
import { makeFixture } from '../test/_fixture.ts';

const HASH = 'a'.repeat(64);

/** A checkout whose generated routes predate a route the C# declares: stamped with an earlier hash, the route absent. */
function checkout(root: string): void {
  for (const output of Object.values(OUTPUTS) as string[]) mkdirSync(dirname(join(root, output)), { recursive: true });
  writeFileSync(join(root, OUTPUTS.service), `// contracts: ${HASH}\nexport type Search = { q: string };\n`);
  writeFileSync(join(root, OUTPUTS.routes), `// contracts: ${'0'.repeat(64)}\nexport type DriverRoute = 'SAVE_SETTING';\n`);
}

/** The files as the generator's check reads them, against the sources' hash `HASH`. */
const look = (root: string) => ({
  hash: HASH,
  why: (Object.values(OUTPUTS) as string[])
    .map((output) => staleness(readFileSync(join(root, output), 'utf8'), HASH, output))
    .find((reason) => reason !== null) ?? null,
});

/** A generator that is the test's: it writes what the sources declare now, the new route among them. */
const generator = (builds: string[]) => (root: string, hash: string) => {
  builds.push(hash);
  writeFileSync(join(root, OUTPUTS.service), `// contracts: ${hash}\nexport type Search = { q: string };\n`);
  writeFileSync(join(root, OUTPUTS.routes), `// contracts: ${hash}\nexport type DriverRoute = 'SAVE_SETTING' | 'SET_RESERVE';\n`);
  return true;
};

test('a publish from a checkout whose generated routes lack a declared route regenerates them before the bundle, and says so', async (t) => {
  const fx = makeFixture('publish-contracts-stale');
  t.after(() => fx.cleanup());
  checkout(fx.root);
  const said: string[] = [];
  const builds: string[] = [];

  const code = freshContracts(fx.root, { look, build: generator(builds), say: (line: string) => said.push(line) });

  assert.equal(code, 0);
  assert.deepEqual(builds, [HASH], 'the generator ran once, for the sources as they stand');
  assert.match(readFileSync(join(fx.root, OUTPUTS.routes), 'utf8'), /'SET_RESERVE'/, 'the routes the bundle builds against carry the route');
  assert.equal(look(fx.root).why, null, 'what it wrote is fresh');
  assert.match(said.join('\n'), /routes\.ts is stale.*regenerating/);
  assert.match(said.at(-1) ?? '', /regenerated the page's contracts/);
});

test('a publish whose generated contracts are fresh builds none, and says they are', async (t) => {
  const fx = makeFixture('publish-contracts-fresh');
  t.after(() => fx.cleanup());
  checkout(fx.root);
  generator([])(fx.root, HASH);
  const said: string[] = [];
  const builds: string[] = [];

  assert.equal(freshContracts(fx.root, { look, build: generator(builds), say: (line: string) => said.push(line) }), 0);

  assert.deepEqual(builds, []);
  assert.deepEqual(said, ['desktop-publish: the page\'s generated contracts are fresh.']);
});

test('a generator that fails stops the publish before the bundle, with the generator\'s exit code', async (t) => {
  const fx = makeFixture('publish-contracts-failed');
  t.after(() => fx.cleanup());
  checkout(fx.root);
  const said: string[] = [];

  assert.equal(freshContracts(fx.root, { look, build: () => false, say: (line: string) => said.push(line) }), 2);

  assert.match(said.at(-1) ?? '', /could not regenerate the page's contracts/);
});
