import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// test-map: reads the process tool's stray enders and the merge tool's use of them
// reaches: tool-tests — the unit and its test are the tool-tests gate's

const procs = await import(new URL('../../../../tools/processes.mjs', import.meta.url).href) as Record<string, unknown>;
const mergeTool = readFileSync(new URL('../../../../tools/merge-branch.mjs', import.meta.url), 'utf8');

test('strays are ended one way: by the tree under a folder, which spares the runner and matches the folder as a whole word', () => {
  assert.equal(typeof procs.endStrandedUnder, 'function');
  assert.equal('endStranded' in procs, false, 'a second ender matched the folder as a prefix and did not spare the runner');
  assert.match(mergeTool, /endStrays = endStrandedUnder\b/, "a dead run's gate tree is cleared by the one ender");
});
