import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { readdirSync, statSync } from 'node:fs';
import { makeFixture } from './_fixture.ts';
import { runCli } from '../src/cli.ts';

/**
 * Daoris must never write into a repository that is not the one it was run in.
 *
 * This is not a hypothetical. A quest command once wrote into a sibling's backlog, and removing it
 * again damaged that repository's working tree — including an uncommitted edit its own session was
 * about to commit. The rule (`repository-owns-its-work`) already said not to; the tooling did it
 * anyway, and no test would have caught it.
 *
 * So the guarantee is asserted the only way worth trusting: run the commands against one repository
 * with a sibling sitting beside it, and require the sibling to be byte-for-byte unchanged. A future
 * command that reaches sideways fails here, whatever its intentions.
 */
function snapshot(root: string): Map<string, string> {
  const files = new Map<string, string>();
  const walk = (dir: string, prefix = ''): void => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const rel = prefix ? `${prefix}/${entry.name}` : entry.name;
      const abs = join(dir, entry.name);
      if (entry.isDirectory()) walk(abs, rel);
      // Size and mtime rather than content: this is about the file being TOUCHED at all, and a
      // rewrite with identical bytes is still a rewrite that could have lost someone's work.
      else files.set(rel, `${statSync(abs).size}`);
    }
  };
  walk(root);
  return files;
}

test('no command writes into a repository other than the one it runs in', () => {
  const canonFx = makeFixture('containment-canon');
  canonFx.write('canon.json', '{"version":"0.1.0"}');
  canonFx.write(
    'core/rules/sensitive-info.md',
    '---\nname: sensitive-info\napplies_when: w\nenforces: e\n---\n\nBody.\n',
  );

  const mine = makeFixture('containment-mine');
  mine.write('daoris.json', JSON.stringify({ source: 's', packs: [] }));

  // The sibling: adopted, with work of its own that nothing here has any business touching.
  const sibling = makeFixture('containment-sibling');
  sibling.write('daoris.json', JSON.stringify({ source: 's', packs: [] }));
  sibling.write('TASKS.md', '# TASKS\n\n- [ ] work in progress, uncommitted\n');
  sibling.write('.claude/rules/theirs.md', '---\nname: theirs\napplies_when: w\nenforces: e\n---\n\nTheirs.\n');

  const before = snapshot(sibling.root);

  const previous = process.env.DAORIS_CANON;
  process.env.DAORIS_CANON = canonFx.root;
  try {
    // Every command that writes anything, plus the ones that only read — a read-only command that
    // started writing would be the surprising case, so it is worth including.
    for (const argv of [['init'], ['sync'], ['index'], ['check'], ['status'], ['doctor'], ['analyze']]) {
      runCli(argv, mine.root, () => {});
    }
  } finally {
    if (previous === undefined) delete process.env.DAORIS_CANON;
    else process.env.DAORIS_CANON = previous;
  }

  assert.deepEqual(
    [...snapshot(sibling.root).entries()].sort(),
    [...before.entries()].sort(),
    'a command touched a repository it was not run in',
  );

  canonFx.cleanup();
  mine.cleanup();
  sibling.cleanup();
});
