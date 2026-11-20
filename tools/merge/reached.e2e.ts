import { test } from 'vitest';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { join } from 'node:path';
import { repoRoot } from './repo-gates.ts';

// test-map: strings name paths as data — the changed path is what the plan is asked about
// reaches: tools/reached-checks.mjs — the tool the test runs
// reaches: tools/merge — the typed modules that tool imports (a declared reach is not followed through its imports)

test('the tool reads the census of this repository: a changed module reaches the tool-tests, --plan runs nothing, and a clean checkout reaches nothing', () => {
  const run = (...args: string[]) => spawnSync(process.execPath, [join(repoRoot, 'tools', 'reached-checks.mjs'), ...args], {
    cwd: repoRoot, encoding: 'utf8', windowsHide: true,
    env: { ...process.env, DAORIS_REACHED_STORE: join(repoRoot, '_fixtures', 'reached-test-none', 'passes.json') },
  });
  const planned = run('--plan', '--paths', 'tools/merge/reached.ts');
  assert.equal(planned.status, 0, planned.stderr);
  assert.match(planned.stdout, /reached: verification: scoped to its reach: 1 path reaches \d+ of \d+ classes; the light gates/);
  assert.match(planned.stdout, /reached: tool-e2e: (\d+ of )?\d+ reached units? run/);
  const wide = run('--plan', '--paths', 'daoris.gates.json');
  assert.equal(wide.status, 0, wide.stderr);
  assert.match(wide.stdout, /reached: verification: scoped to its reach: 1 path reaches \d+ of \d+ classes; it bears a full run's reason \(daoris\.gates\.json is a declared wide path\)/);
  assert.match(planned.stdout, /tool-e2e: node .*vitest\.mjs run .*tools\/merge\/reached\.e2e\.ts/);
  const none = run('--plan', '--paths', '');
  assert.equal(none.status, 0, none.stderr);
  assert.match(none.stdout, /no reached unit/);
});

test('a tool added under tools/ reaches the censuses that read every tool by text: the home census and the provider census', () => {
  const planned = spawnSync(process.execPath, [join(repoRoot, 'tools', 'reached-checks.mjs'), '--plan', '--paths', 'tools/dev-metrics/actors.mjs'], {
    cwd: repoRoot, encoding: 'utf8', windowsHide: true,
    env: { ...process.env, DAORIS_REACHED_STORE: join(repoRoot, '_fixtures', 'reached-test-none', 'passes.json') },
  });
  assert.equal(planned.status, 0, planned.stderr);
  assert.match(planned.stdout, /cli: node .*test\/platform\/home-census\.test\.ts/);
  assert.match(planned.stdout, /tool-tests: node .*tool-test\/provider-census\.test\.ts/);
});
