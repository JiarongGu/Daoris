import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readText } from '../../src/platform/fsx.ts';
import { runCli } from '../../src/cli.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * What a person reads from the dispatcher, byte for byte (MOD7).
 *
 * `fixtures/cli-usage/` holds the usage and the refusals the CLI says itself, so a change to them is proven by bytes rather than by
 * a reviewer comparing two long texts. A change that means to alter the usage or a refusal updates the fixture in the same change,
 * and the diff is then the one place a reviewer sees what a person will read. Every word outside the doctrine commands is the
 * desktop's program's (ONEWAY1ai-i), so its usage and refusals are that program's tests'.
 */
const golden = join(dirname(dirname(fileURLToPath(import.meta.url))), 'fixtures', 'cli-usage');
const HELP = readText(join(golden, 'help.txt')).replace(/\n$/, '');

interface Refusal { argv: string[]; code: number; out: string[] }
const REFUSALS = JSON.parse(readText(join(golden, 'refusals.json'))) as Refusal[];

/** What the desktop's program was handed by the dispatcher, and what had been written when it was. */
interface Handed { words: readonly string[]; written: number }

/**
 * Runs one argv in an empty repository on an empty home, with the repository's path spelled `<cwd>`. The desktop's program is a
 * stand-in, which records what it was handed and answers `exit`.
 */
async function run(argv: string[], handed: Handed[] = [], exit: 0 | 1 | 2 = 0): Promise<{ code: number; out: string[] }> {
  const home = makeFixture('cli-usage-home');
  const repo = makeFixture('cli-usage-repo');
  const previous = process.env.DAORIS_HOME;
  // An empty home, never the developer's own: several refusals are reached only after the home is read.
  process.env.DAORIS_HOME = home.root;
  try {
    const out: string[] = [];
    const code = await runCli(argv, repo.root, (line) => out.push(line), (words) => {
      handed.push({ words, written: out.length });
      return exit;
    }, null);
    return { code, out: out.map((line) => line.split(repo.root).join('<cwd>')) };
  } finally {
    if (previous === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = previous;
    home.cleanup();
    repo.cleanup();
  }
}

test('--help prints exactly the usage, for the dispatcher and a doctrine command alike', async () => {
  assert.ok(HELP.startsWith('daoris <command> [options]\n'), 'the golden usage is not the usage');
  for (const argv of [['--help'], ['check', '--help'], ['--help', 'session']]) {
    assert.deepEqual(await run(argv), { code: 0, out: [HELP] }, `daoris ${argv.join(' ')}`);
  }
});

test('no arguments prints the same usage and exits 2', async () => {
  assert.deepEqual(await run([]), { code: 2, out: [HELP] });
});

test('every refusal the CLI says itself says it, with its exit code, and hands nothing on', async () => {
  assert.ok(REFUSALS.length >= 5, `only ${REFUSALS.length} refusals were captured, so this proves little`);
  for (const { argv, code, out } of REFUSALS) {
    const handed: Handed[] = [];
    assert.deepEqual(await run(argv, handed), { code, out }, `daoris ${argv.join(' ')}`);
    assert.deepEqual(handed, [], `daoris ${argv.join(' ')} was handed to the desktop's program`);
  }
});

/**
 * ONEWAY1ai-i (the oneway design §11): every word outside the doctrine commands goes to the desktop's program whole, its `--help` and
 * a word it has no command for among them, nothing written before the start, and the program's exit the run's.
 */
test('a word outside the doctrine goes to the desktop\'s program whole, with nothing written first, and its exit is the run\'s', async () => {
  for (const argv of [
    ['session', 'say', 's-1', 'a "quoted" & 100% 中文 word', '--level', 'urgent'],
    ['place', 'standing', 'engine', 'a "quoted" & 100% 中文 word', '--clear'],
    ['tool', 'use', 'git', 'file', 'C:/Program Files/Git/bin/git.exe'],
    ['agent', 'account', 'rename', 'claude-code', 'account-1', 'you@work.example'],
    ['plugin', 'add', 'C:/Work Trees/acme & co/gate'],
    ['remote', 'add', 'Work & play', '--url', 'https://team.example'],
    ['browser', 'favorite', 'add', 'https://example.com', '--title', 'R&D'],
    ['repository', 'import', 'C:/Work Trees', '--workspace', 'Work & play'],
    ['brainstorm', 'why is it slow?', '--agents', 'codex,claude-code'],
    // ONEWAY1ae: `daoris convert` is the home's one conversion door, a group of the tree, a leave among its words.
    ['convert', '--apply', 'a'.repeat(64), '--leave', 'quests'],
    ['tool', '--help'],
    ['driver', 'drive', 'engine'],
    ['connect', '--dry-run'],
    ['--once'],
    ['frobnicate'],
  ]) {
    for (const exit of [0, 1, 2] as const) {
      const handed: Handed[] = [];
      assert.deepEqual(await run([...argv], handed, exit), { code: exit, out: [] }, `daoris ${argv.join(' ')}`);
      assert.deepEqual(handed, [{ words: argv, written: 0 }], `daoris ${argv.join(' ')}`);
    }
  }
});
