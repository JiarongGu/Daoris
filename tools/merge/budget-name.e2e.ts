import { test } from 'vitest';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { budgetSourceOf } from './budget-overlay.ts';
import { fixtureFolder } from './fixture-home.ts';

test('the repository is named by the folder of its main checkout, from a linked tree too, and a folder inside another repository by itself', () => {
  const { dir: base, remove } = fixtureFolder('budget-name');
  try {
    const main = join(base, 'Named');
    mkdirSync(main);
    const git = (cwd: string, ...args: string[]) => spawnSync('git', args, { cwd, encoding: 'utf8', windowsHide: true });
    git(main, 'init', '-q');
    git(main, '-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '--no-verify', '--allow-empty', '-q', '-m', 'x');
    const linked = join(base, 'tree-one');
    git(main, 'worktree', 'add', '-q', '--detach', linked);
    mkdirSync(join(main, 'inside'));
    assert.equal(budgetSourceOf(main, {}).repository, 'Named');
    assert.equal(budgetSourceOf(linked, {}).repository, 'Named');
    assert.equal(budgetSourceOf(join(main, 'inside'), {}).repository, 'inside');
    assert.equal(budgetSourceOf(main, { DAORIS_HOME: 'h' }).home, 'h');
    assert.equal(budgetSourceOf(main, {}).home, null);
  } finally {
    remove();
  }
}, 60_000);
