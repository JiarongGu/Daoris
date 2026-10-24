import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { makeFixture } from './_fixture.ts';

/**
 * GITHUBDOCS1 (D211): the README's quickstart is the first thing a person runs, so it runs here as written. Its commands are
 * read out of the README's own `## Quickstart` block, `npx daoris@<version>` replaced by this checkout's entry point (the
 * README's last quickstart line says the same), in a fresh git repository under `_fixtures/`.
 */

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const entry = join(repoRoot, 'src', 'Daoris.Cli', 'bin', 'daoris.mjs');

/** The `npx daoris@X <verb>` lines of the README's Quickstart, in order, as the verbs and arguments they run. */
function quickstart(): string[][] {
  const readme = readFileSync(join(repoRoot, 'README.md'), 'utf8');
  const section = readme.split(/^## /m).find((part) => part.startsWith('Quickstart\n'));
  assert.ok(section, 'the README has no ## Quickstart');
  const block = /```sh\n([\s\S]*?)```/.exec(section)?.[1];
  assert.ok(block, 'the Quickstart has no sh block');
  return block.split('\n').filter((line) => line.trim()).map((line) => {
    const command = line.replace(/\s+#.*$/, '').trim();
    const match = /^npx daoris@\d+\.\d+\.\d+ (.+)$/.exec(command);
    assert.ok(match, `a quickstart line is not \`npx daoris@<version> …\`: ${line}`);
    return match[1]!.split(/\s+/);
  });
}

test('the README\'s quickstart runs as written in a fresh repository, and leaves what it says it writes', () => {
  const steps = quickstart();
  assert.deepEqual(steps.map((args) => args[0]), ['init', 'sync', 'check'], 'the quickstart changed: read this test again');
  const fx = makeFixture('readme-quickstart');
  try {
    // A machine's own wiring is not the quickstart's: the doctrine commands need none of it.
    const env = Object.fromEntries(Object.entries(process.env).filter(([name]) => !name.startsWith('DAORIS_')));
    const git = spawnSync('git', ['init', '-q', '.'], { cwd: fx.root, encoding: 'utf8', env });
    assert.equal(git.status, 0, git.stderr);
    for (const args of steps) {
      const run = spawnSync(process.execPath, [entry, ...args], { cwd: fx.root, encoding: 'utf8', env });
      assert.equal(run.status, 0, `daoris ${args.join(' ')} exited ${run.status}\n${run.stdout}${run.stderr}`);
    }
    for (const rel of ['daoris.json', 'daoris.lock', 'AGENTS.md', 'CLAUDE.md', '.gitattributes', '.claude/INDEX.md']) {
      assert.ok(fx.exists(rel), `the quickstart left no ${rel}`);
    }
    assert.match(fx.read('AGENTS.md'), /<!-- daoris:rules/);
    assert.deepEqual(JSON.parse(fx.read('daoris.json')).packs, [], 'init writes the core doctrine only, as the README says');
  } finally {
    fx.cleanup();
  }
});
