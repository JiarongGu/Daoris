import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { makeFixture } from '../../test/_fixture.ts';
import { tool } from '../support/merge-branch.ts';

// reaches: tool-tests — the unit and its test are the tool-tests gate's

test('CODESTRREV2c: the lane report and the tree drift list a non-ASCII path whole', () => {
  const fx = makeFixture('merge-branch-changed-paths');
  try {
    const run = (...args: string[]) => {
      const result = spawnSync('git', ['-c', 'core.quotepath=true', ...args], { cwd: fx.root, encoding: 'utf8', windowsHide: true });
      assert.equal(result.status, 0, result.stderr);
      return result.stdout.trim();
    };
    run('init', '--quiet', '--template=', '-b', 'main');
    run('config', 'user.email', 't@example.invalid');
    run('config', 'user.name', 'T');
    writeFileSync(join(fx.root, 'a.txt'), 'a\n');
    writeFileSync(join(fx.root, 'daoris.lanes.json'), `${JSON.stringify({ lanes: [{ id: 'docs', title: 'Docs', summary: 'x', steward: false, paths: ['docs/**'] }] })}\n`);
    run('add', '-A');
    run('commit', '--quiet', '-m', 'first');
    const base = run('rev-parse', 'HEAD');
    run('checkout', '--quiet', '-b', 'feature');
    mkdirSync(join(fx.root, 'docs'), { recursive: true });
    writeFileSync(join(fx.root, 'docs', 'café notes.md'), 'x\n');
    run('add', '-A');
    run('commit', '--quiet', '-m', 'work');
    run('checkout', '--quiet', 'main');
    const tip = run('rev-parse', 'feature');

    const report = tool.laneReport(fx.root, 'feature').join('\n');
    assert.match(report, /docs\s+Docs\s+1 file/, report);
    assert.doesNotMatch(report, /outside every lane/, report);
    assert.deepEqual(tool.treeDrift(fx.root, base, tip), ['docs/café notes.md']);
  } finally {
    fx.cleanup();
  }
});
