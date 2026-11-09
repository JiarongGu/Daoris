import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../../test/_fixture.ts';

const { holdBacklog } = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
  '../../../../tools/merge-branch/backlog-held.mjs') as { holdBacklog: (root: string, backlog: string | null) => { said: string[]; refused: string[] } };

const ENV = { ...process.env, GIT_TERMINAL_PROMPT: '0', GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.test', GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.test' };
const SECTIONS = '# Tasks\n\n- [ ] **A1** one.\n- [ ] **A2** two.\n\n## Later\n';

/** A scratch repository whose main makes `main` of the backlog while branch `work` archives A2, merged in place with no commit. */
function merged(name: string, base: string, main: string, work: string): string {
  const root = makeFixture(name).root;
  const git = (...args: string[]) => {
    const ran = spawnSync('git', args, { cwd: root, encoding: 'utf8', env: ENV, windowsHide: true });
    assert.equal(ran.status, 0, `git ${args.join(' ')}: ${ran.stderr}`);
  };
  const commit = (text: string, subject: string) => {
    writeFileSync(join(root, 'TASKS.md'), text);
    git('add', '-A');
    git('commit', '--quiet', '-m', subject);
  };
  git('init', '--quiet', '--initial-branch=main');
  commit(base, 'the backlog');
  git('checkout', '--quiet', '-b', 'work');
  writeFileSync(join(root, 'work.txt'), 'the work\n');
  commit(work, 'the work');
  git('checkout', '--quiet', 'main');
  commit(main, 'the line');
  git('merge', '--quiet', '--no-ff', '--no-commit', 'work');
  return root;
}

test('a clean merge keeping a row the line moved while the work archived it takes the row out, staged and said', () => {
  const root = merged('backlog-held-moved', SECTIONS, SECTIONS.replace('- [ ] **A2** two.\n', '') + '- [ ] **A2** two.\n', SECTIONS.replace('- [ ] **A2** two.\n', ''));
  assert.deepEqual(holdBacklog(root, 'TASKS.md'), {
    said: ['TASKS.md: row A2 left by its rows, where git had kept it: one side removed it while the other only moved it'], refused: [],
  });
  assert.equal(readFileSync(join(root, 'TASKS.md'), 'utf8'), SECTIONS.replace('- [ ] **A2** two.\n', ''));
  assert.equal(spawnSync('git', ['diff', '--quiet', '--', 'TASKS.md'], { cwd: root }).status, 0, 'the change is staged');
});

test("a clean merge losing the line's words in a row the work archived is refused, both texts said", () => {
  const base = SECTIONS.replace('two.', 'Do not enable A.');
  const root = merged('backlog-held-words', base, base.replace('- [ ] **A2** Do not enable A.\n', '') + '- [ ] **A2** Do enable A.\n', base.replace('- [ ] **A2** Do not enable A.\n', ''));
  assert.deepEqual(holdBacklog(root, 'TASKS.md'), {
    said: [], refused: ['TASKS.md: row A2 changed on both sides; left for you: main has "- [ ] **A2** Do enable A.", and the branch removed it'],
  });
});

test('a clean merge keeping one id twice is refused, the id said; one that holds is left alone', () => {
  const root = merged('backlog-held-twice', SECTIONS, SECTIONS.replace('# Tasks\n\n', '# Tasks\n\n- [ ] **B1** new.\n'), SECTIONS + '- [ ] **B1** new.\n');
  assert.deepEqual(holdBacklog(root, 'TASKS.md'), { said: [], refused: ['TASKS.md: the merge covers the row B1 twice; left for you'] });
  const held = merged('backlog-held-holds', SECTIONS, SECTIONS + '- [ ] **C1** filed.\n', SECTIONS.replace('- [ ] **A2** two.\n', ''));
  assert.deepEqual(holdBacklog(held, 'TASKS.md'), { said: [], refused: [] });
  assert.deepEqual(holdBacklog(held, null), { said: [], refused: [] });
});
