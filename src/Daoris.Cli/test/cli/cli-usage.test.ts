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
 * `fixtures/cli-usage/` was captured from the program's own strings before the commands became a
 * table, so the split is proven by bytes rather than by a reviewer comparing two long texts. A change
 * that means to alter the usage or a refusal updates the fixture in the same change, and the diff is
 * then the one place a reviewer sees what a person will read.
 */
const golden = join(dirname(dirname(fileURLToPath(import.meta.url))), 'fixtures', 'cli-usage');
const HELP = readText(join(golden, 'help.txt')).replace(/\n$/, '');

interface Refusal { argv: string[]; code: number; out: string[] }
const REFUSALS = JSON.parse(readText(join(golden, 'refusals.json'))) as Refusal[];

/** What the driver's program was handed by the dispatcher, and what had been written when it was. */
interface Handed { family: string; words: readonly string[]; written: number }

/**
 * Runs one argv in an empty repository on an empty home, with the repository's path spelled `<cwd>`. The driver's program is a stand-in
 * (PUSH1f2a, the alone-path design §3): the verbs a family hands it are its own words, read by the host's table, and the stand-in records
 * what it was handed and answers `exit`.
 */
async function run(argv: string[], handed: Handed[] = [], exit: 0 | 1 | 2 = 0): Promise<{ code: number; out: string[] }> {
  const home = makeFixture('cli-usage-home');
  const repo = makeFixture('cli-usage-repo');
  const previous = process.env.DAORIS_HOME;
  // An empty home, never the developer's own: several refusals are reached only after the home is read.
  process.env.DAORIS_HOME = home.root;
  try {
    const out: string[] = [];
    const code = await runCli(argv, repo.root, (line) => out.push(line), (family, words) => {
      handed.push({ family, words, written: out.length });
      return exit;
    });
    return { code, out: out.map((line) => line.split(repo.root).join('<cwd>')) };
  } finally {
    if (previous === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = previous;
    home.cleanup();
    repo.cleanup();
  }
}

test('--help prints exactly the usage captured before the split, wherever the flag appears', async () => {
  assert.ok(HELP.startsWith('daoris <command> [options]\n'), 'the golden usage is not the usage');
  for (const argv of [['--help'], ['driver', '--help'], ['--help', 'driver']]) {
    assert.deepEqual(await run(argv), { code: 0, out: [HELP] }, `daoris ${argv.join(' ')}`);
  }
});

test('no arguments prints the same usage and exits 2', async () => {
  assert.deepEqual(await run([]), { code: 2, out: [HELP] });
});

test('every refusal says what it said before the split, with the same exit code', async () => {
  assert.ok(REFUSALS.length >= 20, `only ${REFUSALS.length} refusals were captured, so this proves little`);
  for (const { argv, code, out } of REFUSALS) {
    const handed: Handed[] = [];
    assert.deepEqual(await run(argv, handed), { code, out }, `daoris ${argv.join(' ')}`);
    // A row the driver's program says is the host's table's now (`driver-choices-text.json`), never this one's.
    assert.deepEqual(handed, [], `daoris ${argv.join(' ')} was handed to the driver's program`);
  }
});

/**
 * The families' verbs the CLI hands Daoris's driver program (PUSH1f2a, D194 point 1): the family and its words whole, nothing written
 * before the start, and the program's exit the run's; `--help` stays the dispatcher's own, wherever it stands, and a verb the
 * allow-list does not name stays the CLI's.
 */
test('a handed verb goes to the driver\'s program whole, with nothing written first, and its exit is the run\'s', async () => {
  for (const [argv, words] of [
    [['driver'], []],
    [['driver', 'drive', 'engine'], ['drive', 'engine']],
    [['driver', 'standing', 'engine', 'a "quoted" & 100% 中文 word', '--clear'], ['standing', 'engine', 'a "quoted" & 100% 中文 word', '--clear']],
    [['driver', 'landing', 'engine', 'merge'], ['landing', 'engine', 'merge']],
    [['driver', 'retry', '12', '--at', '2'], ['retry', '12', '--at', '2']],
  ] as const) {
    for (const exit of [0, 1, 2] as const) {
      const handed: Handed[] = [];
      assert.deepEqual(await run([...argv], handed, exit), { code: exit, out: [] }, `daoris ${argv.join(' ')}`);
      assert.deepEqual(handed, [{ family: 'driver', words: [...words], written: 0 }], `daoris ${argv.join(' ')}`);
    }
  }

  for (const argv of [['driver', '--help'], ['driver', 'drive', '--help'], ['driver', 'workflow', 'list'], ['driver', 'brainstorms'],
    ['driver', 'work-cycle', 'engine', 'quest', '--clear'], ['driver', 'landing', 'engine', 'merge', '--no-cycle'], ['driver', 'frobnicate']]) {
    const handed: Handed[] = [];
    await run(argv, handed);
    assert.deepEqual(handed, [], `daoris ${argv.join(' ')} is the CLI's own`);
  }
});
