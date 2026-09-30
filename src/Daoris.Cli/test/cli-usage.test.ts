import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readText } from '../src/fsx.ts';
import { runCli } from '../src/cli.ts';
import { makeFixture } from './_fixture.ts';

/**
 * What a person reads from the dispatcher, byte for byte (MOD7).
 *
 * `fixtures/cli-usage/` was captured from the program's own strings before the commands became a
 * table, so the split is proven by bytes rather than by a reviewer comparing two long texts. A change
 * that means to alter the usage or a refusal updates the fixture in the same change, and the diff is
 * then the one place a reviewer sees what a person will read.
 */
const golden = join(dirname(fileURLToPath(import.meta.url)), 'fixtures', 'cli-usage');
const HELP = readText(join(golden, 'help.txt')).replace(/\n$/, '');

interface Refusal { argv: string[]; code: number; out: string[] }
const REFUSALS = JSON.parse(readText(join(golden, 'refusals.json'))) as Refusal[];

/** Runs one argv in an empty repository on an empty home, with the repository's path spelled `<cwd>`. */
async function run(argv: string[]): Promise<{ code: number; out: string[] }> {
  const home = makeFixture('cli-usage-home');
  const repo = makeFixture('cli-usage-repo');
  const previous = process.env.DAORIS_HOME;
  // An empty home, never the developer's own: several refusals are reached only after the home is read.
  process.env.DAORIS_HOME = home.root;
  try {
    const out: string[] = [];
    const code = await runCli(argv, repo.root, (line) => out.push(line));
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
    assert.deepEqual(await run(argv), { code, out }, `daoris ${argv.join(' ')}`);
  }
});
