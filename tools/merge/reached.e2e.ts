import { test } from 'vitest';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { join } from 'node:path';
import { repoRoot } from './repo-gates.ts';

// test-map: strings name paths as data — the changed path is what the plan is asked about
// reaches: tools/reached-checks.mjs — the tool the test runs, whose closure is the typed modules under tools/merge it imports

test('the tool reads the census of this repository: a changed module reaches the tool-tests, --plan runs nothing, and a clean checkout reaches nothing', () => {
  const run = (...args: string[]) => spawnSync(process.execPath, [join(repoRoot, 'tools', 'reached-checks.mjs'), ...args], {
    cwd: repoRoot, encoding: 'utf8', windowsHide: true,
    // A store that holds no pass, which a plan only reads: the plan is the census's alone.
    env: { ...process.env, DAORIS_REACHED_STORE: join(repoRoot, '_fixtures', 'reached-test-none', 'passes.json') },
  });
  const planned = run('--plan', '--paths', 'tools/merge/reached.ts');
  assert.equal(planned.status, 0, planned.stderr);
  assert.match(planned.stdout, /reached: tool-tests: (\d+ of )?\d+ reached units? run/);
  assert.match(planned.stdout, /tool-tests: node --test .*tool-test\/merge-branch\//);
  // No path changed, none reached.
  const none = run('--plan', '--paths', '');
  assert.equal(none.status, 0, none.stderr);
  assert.match(none.stdout, /no reached unit/);
});
