import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readText } from '../src/platform/fsx.ts';
import { checkSteps, quickSteps } from '../../../tools/quick/steps.mjs';

const repoRoot = dirname(dirname(dirname(dirname(fileURLToPath(import.meta.url)))));

/**
 * A session hands back work the merge's `cli` gate runs again, so what a builder runs before it hands back holds every check that gate
 * runs: a comment, partial or sensitive finding is then read in the builder's tree, never first at the merge.
 */
test("the builder's quick checks hold every check the merge's cli gate runs", () => {
  const gates = JSON.parse(readText(join(repoRoot, 'daoris.gates.json'))) as { gates: { name: string; run: string }[] };
  const scripts = (JSON.parse(readText(join(repoRoot, 'package.json'))) as { scripts: Record<string, string> }).scripts;
  const cli = gates.gates.find((gate) => gate.name === 'cli');
  assert.ok(cli, 'the cli gate is no longer declared — that is not a pass');
  assert.equal(cli.run, 'npm run verify:checks', 'the quick steps read the script the cli gate runs');
  assert.equal(scripts.quick, 'node tools/quick.mjs', 'quick is the one runner that runs every step, never a chain that stops at a red');

  const wanted = checkSteps(repoRoot);
  assert.ok(wanted.length > 10, `the cli gate's checks emptied: ${cli.run}`);
  const quick = quickSteps(repoRoot);
  const missing = wanted.filter((step) => !quick.includes(step));
  assert.deepEqual(missing, [], `npm run quick leaves out what the merge's cli gate runs: ${missing.join(' | ')}`);
});
