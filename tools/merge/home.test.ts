import { afterAll, beforeAll, test } from 'vitest';
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { Refusal } from './args.ts';
import { ignoredOf, locate, preconditions, requireIgnored } from './git.ts';
import { startListing, toolCheckout, verdictsHome, verdictsNeed } from './home.ts';

const scratch = join(dirname(dirname(dirname(fileURLToPath(import.meta.url)))), '_fixtures', 'merge-home');
const repo = join(scratch, 'repo');
const linked = join(scratch, 'linked');
const run = (cwd: string, ...args: string[]) => {
  const ran = spawnSync('git', args, { cwd, encoding: 'utf8', windowsHide: true });
  assert.equal(ran.status, 0, `git ${args.join(' ')}: ${ran.stderr}`);
  return ran.stdout.trim();
};

beforeAll(() => {
  rmSync(scratch, { recursive: true, force: true });
  mkdirSync(join(repo, 'tools'), { recursive: true });
  run(repo, 'init', '--quiet', '--template=', '-b', 'main');
  writeFileSync(join(repo, '.gitignore'), 'local/\n');
  run(repo, 'add', '-A');
  run(repo, '-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.test', 'commit', '--quiet', '-m', 'first');
  run(repo, 'worktree', 'add', '--quiet', '-b', 'side', linked);
});
afterAll(() => rmSync(scratch, { recursive: true, force: true }));

test('the paths git ignores are asked in one start, each named as given, and a failed start is a refusal', () => {
  assert.deepEqual([...ignoredOf(repo, ['local/gate-verdicts.json', 'src/a.ts', 'local/scope-tree'])].sort(), ['local/gate-verdicts.json', 'local/scope-tree']);
  assert.deepEqual([...ignoredOf(repo, ['src/a.ts'])], [], 'none ignored is an answer, not an error');
  assert.throws(() => ignoredOf(join(scratch, 'nowhere'), ['local/x']), Refusal);
});

test('the first need git does not ignore is refused with its own words, in the order given', () => {
  requireIgnored(repo, [['local/a.json', 'not a']]);
  assert.throws(() => requireIgnored(repo, [['local/a.json', 'not a'], ['src/b.ts', 'not b'], ['src/c.ts', 'not c']]), /^Refusal: not b$|not b/);
  assert.throws(() => preconditions(repo, [['src/b.ts', 'the verdicts are not ignored']]), /the verdicts are not ignored/);
});

test('one rev-parse locates the checkout with its common git folder; the home of a tree the listing names asks git nothing', () => {
  const root = locate(join(repo, 'tools'));
  assert.equal(root.toLowerCase(), repo.toLowerCase());
  assert.equal(verdictsHome(root), root);
  assert.match(startListing(root) ?? '', /^worktree /);
  // The linked tree is in the listing just read, so its home is answered from it.
  rmSync(join(linked, '.git'));
  assert.equal(verdictsHome(linked).toLowerCase(), repo.toLowerCase(), 'answered from the listing: git could not have been asked there');
  assert.match(verdictsNeed(linked)[1], /in the main checkout, where every worktree records/);
  assert.match(verdictsNeed(root)[1], /is not ignored here/);
});

test("a tool in the checkout's own tools/ is the checkout's without a git start; another checkout's tool is asked", () => {
  const root = locate(repo);
  assert.deepEqual(toolCheckout(root, join(repo, 'tools')), { tool: root, sameRepository: true });
  const other = toolCheckout(root, dirname(fileURLToPath(import.meta.url)));
  assert.equal(other.sameRepository, false);
  assert.notEqual(other.tool, null);
});
