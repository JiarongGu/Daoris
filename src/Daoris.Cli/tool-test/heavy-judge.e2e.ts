import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { git } from './support/merge-branch-kit.ts';

// test-map: strings name paths as data
// reaches: tool-e2e — the judgement and the ranges read a real git repository

const tool = (name: string) => import(new URL('../../../tools/' + name, import.meta.url).href);

test('the landing is judged on the paths its range changed, and a batch is reached over the ranges alone', async () => {
  const { judgeIn } = await tool('heavy-run.mjs');
  const { pathsOfRanges } = await tool('reached-checks.mjs');
  const dir = realpathSync(mkdtempSync(join(tmpdir(), 'heavy-judge-')));
  const commitFile = (path: string, text: string) => {
    mkdirSync(join(dir, dirname(path)), { recursive: true });
    writeFileSync(join(dir, path), text);
    git(dir, 'add', '-A');
    git(dir, 'commit', '-q', '-m', path);
    return git(dir, 'rev-parse', 'HEAD');
  };
  try {
    git(dir, 'init', '-q');
    const rule = { verifyBudget: { budgetMinutes: 10, fullRunReasons: { widePaths: ['daoris.gates.json'], reachShare: 0.5, runs: ['after-landing'] } } };
    const c0 = commitFile('daoris.gates.json', JSON.stringify(rule));
    const c1 = commitFile('docs/a.md', 'a');
    const c2 = commitFile('daoris.gates.json', JSON.stringify({ ...rule, gates: [] }));
    const c3 = commitFile('docs/b.md', 'b');
    const wide = await judgeIn(dir)({ base: c1, commit: c2 });
    assert.equal(wide.run, true);
    assert.match(wide.said, /daoris\.gates\.json is a declared wide path/);
    assert.deepEqual(pathsOfRanges(dir, [`${c0}..${c1}`, `${c2}..${c3}`]).sort(), ['docs/a.md', 'docs/b.md'], 'the change between the ranges is not reached');
    rmSync(join(dir, 'daoris.gates.json'));
    assert.equal((await judgeIn(dir)({ base: c2, commit: c3 })).run, true, 'with no rule the tier follows every landing');
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});
