import { test } from 'vitest';
import assert from 'node:assert/strict';
// @ts-expect-error — untyped workspace tooling, read through the seam its own tests use
import * as untyped from '../merge-branch.mjs';
import { failsFast } from './reach.ts';

type Gate = { name: string; run: string; kind: string };
type Result = { gate: Gate; code: number; verdict: string; note: string; ms: number; log: string };
const tool = untyped as { runGates: (root: string, gates: Gate[], dir: string, options: Record<string, unknown>) => Promise<Result[]> };

const planned = (name: string, run = `node ${name}.mjs`): Gate => ({ name, run, kind: 'check' });

/** The gates run with `failing` failing, in turn or in lanes; answers the names that ran, in the order they ended. */
async function ranWith(names: string[], failing: string[], keepGoing: unknown, oneAtATime = true): Promise<string[]> {
  const ran: string[] = [];
  await tool.runGates('root', names.map((name) => planned(name)), 'logs', {
    keepGoing, oneAtATime, scripts: {}, print: () => {},
    run: async (_root: string, gate: Gate) => {
      ran.push(gate.name);
      const fails = failing.includes(gate.name);
      return { gate, code: fails ? 1 : 0, verdict: fails ? 'FAIL' : 'PASS', note: '', ms: 1, log: `logs/${gate.name}.log` };
    },
  });
  return ran;
}

test('a merge keeps going past a failed gate, so every red of the run is found in one round', async () => {
  assert.deepEqual(await ranWith(['cli', 'cli-tests', 'tool-tests'], ['cli'], 'blocking'), ['cli', 'cli-tests', 'tool-tests']);
  assert.deepEqual((await ranWith(['cli', 'cli-tests', 'tool-tests', 'comments'], ['cli'], 'blocking', false)).sort(), ['cli', 'cli-tests', 'comments', 'tool-tests']);
});

test('a blocking light gate still fails fast, unless the run keeps going past everything', async () => {
  assert.deepEqual(await ranWith(['universal', 'code-map', 'cli'], ['universal'], 'blocking'), ['universal']);
  assert.deepEqual(await ranWith(['universal', 'code-map', 'cli'], ['universal'], true), ['universal', 'code-map', 'cli']);
  assert.deepEqual(await ranWith(['cli', 'cli-tests'], ['cli'], false), ['cli'], 'a verb that stops at the first failure still does');
});

test('which failure stops a run: every one, a blocking light gate only, or none', () => {
  assert.deepEqual(['universal', 'code-map', 'test-map', 'cli'].map((gate) => failsFast('blocking', gate)), [true, true, true, false]);
  assert.equal(failsFast(false, 'cli'), true);
  assert.equal(failsFast(true, 'universal'), false);
});
