import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { makeFixture } from '../test/_fixture.ts';
// @ts-expect-error — untyped workspace tooling; see desktop-publish.test.ts
import { ungatedRefusal } from '../../../tools/desktop-publish.mjs';

// reaches: tools/desktop-publish.mjs — the publisher's stage judgement

type Ask = (root: string, options?: { light?: boolean }) => { code: number; out: string };

test('the light stage asks the light judgement and names the plain CI as what it lacks: the install may be staged before the rehearsals the CI runs after it', () => {
  const fx = makeFixture('publish-light');
  const to = join(fx.root, 'install');
  const seen: (boolean | undefined)[] = [];
  const ask = (code: number, out: string): Ask => (_root, options) => { seen.push(options?.light); return { code, out }; };
  assert.equal(ungatedRefusal(to, fx.root, { light: true, ask: ask(0, 'merge-branch: the light stage has passed this checkout\n') }).refusal, null);
  const refused = ungatedRefusal(to, fx.root, { light: true, ask: ask(1, 'merge-branch: the light stage has NOT passed this checkout\n  driver-process   none   no verdict on this tree\n') });
  assert.match(refused.refusal!, /the light stage's gates have not passed this checkout/);
  ungatedRefusal(to, fx.root, { ask: ask(0, 'merge-branch: the full set has passed this checkout\n') });
  assert.deepEqual(seen, [true, true, false]);
  fx.cleanup();
});
