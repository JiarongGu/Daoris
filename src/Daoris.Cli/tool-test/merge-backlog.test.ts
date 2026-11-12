import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../test/_fixture.ts';
import { repoRoot } from './support/merge-branch.ts';

// reaches: .gitattributes — the backlog's driver is named there

const driver = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-branch.test.ts documents
  '../../../tools/merge-backlog.mjs') as {
  DRIVER: string;
  mergeBacklog: (base: string, ours: string, theirs: string, markerSize?: number) => { text: string; conflicts: number };
};

const BASE = ['# Tasks', '', '- [ ] **A1** one.', '- [ ] **A2** two.', '- [ ] **A3** three.', ''].join('\n');

test('the backlog merges by its rows: two builders adding and archiving at one place merge clean, a row both edited is a conflict marked in git\'s form', () => {
  // One side archives a row, the other adds a row beside it: the text merge reads one conflict, the rows' rule none.
  const archived = BASE.replace('- [ ] **A2** two.\n', '');
  const added = BASE.replace('- [ ] **A3** three.\n', '- [ ] **A3** three.\n- [ ] **A4** four.\n');
  const clean = driver.mergeBacklog(BASE, archived, added);
  assert.equal(clean.conflicts, 0);
  assert.equal(clean.text, ['# Tasks', '', '- [ ] **A1** one.', '- [ ] **A3** three.', '- [ ] **A4** four.', ''].join('\n'));
  // Both add a row at the end: both kept.
  const both = driver.mergeBacklog(BASE, BASE.replace('three.\n', 'three.\n- [ ] **B1** mine.\n'), BASE.replace('three.\n', 'three.\n- [ ] **C1** yours.\n'));
  assert.equal(both.conflicts, 0);
  assert.match(both.text, /\*\*B1\*\* mine\.\n- \[ \] \*\*C1\*\* yours\./);
  // One row edited on both sides is the person's: git's markers, both texts, the count.
  const edited = driver.mergeBacklog(BASE, BASE.replace('two.', 'two, mine.'), BASE.replace('two.', 'two, yours.'));
  assert.equal(edited.conflicts, 1);
  assert.match(edited.text, /<<<<<<< ours\n- \[ \] \*\*A2\*\* two, mine\.\n=======\n- \[ \] \*\*A2\*\* two, yours\.\n>>>>>>> theirs/);
});

test('git runs the driver for TASKS.md alone when the clone is configured: a merge of two branches\' rows is clean, and the same merge without it conflicts', () => {
  const fx = makeFixture('merge-backlog-git');
  const git = (...args: string[]) => {
    const ran = spawnSync('git', args, { cwd: fx.root, encoding: 'utf8', windowsHide: true });
    return { status: ran.status, out: `${ran.stdout}${ran.stderr}` };
  };
  const at = (name: string, text: string) => writeFileSync(join(fx.root, name), text);
  const commit = (message: string) => { git('add', '-A'); assert.equal(git('-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '-q', '-m', message).status, 0); };
  git('init', '-q', '-b', 'main');
  at('.gitattributes', 'TASKS.md merge=backlog\n');
  at('TASKS.md', BASE);
  commit('base');
  git('checkout', '-q', '-b', 'one');
  at('TASKS.md', BASE.replace('- [ ] **A3** three.\n', ''));
  commit('archive A3');
  git('checkout', '-q', 'main');
  at('TASKS.md', BASE.replace('- [ ] **A3** three.\n', '- [ ] **A3** three.\n- [ ] **A4** four.\n'));
  commit('add A4');
  // Without the driver configured git reads the two edits, at neighbouring lines, as a conflict.
  assert.equal(git('merge', '--no-commit', '--no-ff', 'one').status, 1);
  git('merge', '--abort');
  git('config', 'merge.backlog.driver', `node ${join(repoRoot, 'tools', 'merge-backlog.mjs').split('\\').join('/')} %O %A %B %L %P`);
  const merged = git('merge', '--no-edit', '--no-ff', 'one');
  assert.equal(merged.status, 0, merged.out);
  assert.equal(readFileSync(join(fx.root, 'TASKS.md'), 'utf8').replace(/\r\n/g, '\n'), ['# Tasks', '', '- [ ] **A1** one.', '- [ ] **A2** two.', '- [ ] **A4** four.', ''].join('\n'));
  fx.cleanup();
});

test('the driver is configured by the root install with the rows\' and the notes\' drivers, and worktree-setup does the same', () => {
  const scripts = (JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8')) as { scripts: Record<string, string> }).scripts;
  assert.match(scripts.prepare!, /node tools\/merge-backlog\.mjs --install/);
  assert.match(readFileSync(join(repoRoot, '.gitattributes'), 'utf8'), /^TASKS\.md merge=backlog$/m);
  assert.equal(driver.DRIVER, 'node tools/merge-backlog.mjs %O %A %B %L %P');
});
