import { test } from 'vitest';
import assert from 'node:assert/strict';
import { constants } from 'node:os';
import { BACKGROUND, lowerPriority, startBackground } from './background.ts';

test('a background run marks its environment and lowers its own process below normal, saying what went wrong where it cannot', () => {
  const asked: Array<[number, number]> = [];
  const env: Record<string, string | undefined> = { KEEP: 'x' };
  assert.equal(startBackground({ setPriority: (pid, priority) => { asked.push([pid, priority]); }, env }), null);
  assert.deepEqual(asked, [[0, constants.priority.PRIORITY_BELOW_NORMAL]]);
  assert.deepEqual(env, { KEEP: 'x', [BACKGROUND]: '1' });
  const bare: Record<string, string | undefined> = {};
  assert.equal(startBackground({ setPriority: () => { throw new Error('access denied'); }, env: bare }), 'access denied');
  assert.equal(bare[BACKGROUND], '1', 'its places are background ones though its priority stands');
  assert.equal(lowerPriority(() => {}), null);
});
