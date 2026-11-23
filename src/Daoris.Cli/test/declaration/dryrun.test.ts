import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { makeFixture } from '../_fixture.ts';
import { runCli } from '../../src/cli.ts';
import { splitDeclaration } from '../../src/declaration/split.ts';
import { proveSame } from '../../src/declaration/proof.ts';
import { repositoryFiles } from '../../src/declaration/tree.ts';

/**
 * `daoris declaration split --dry-run`: the folder and the home's values written to scratch, the proof that they resolve to the
 * old file said line by line, and nothing else touched. Run on this repository and the examples, each is equal or each
 * difference is named.
 */

const REPOSITORY = dirname(dirname(dirname(dirname(dirname(fileURLToPath(import.meta.url))))));

const GATES = {
  gates: [
    { name: 'universal', run: 'node tools/universal.mjs' },
    { name: 'tool-tests', timeoutMinutes: 8, timeoutWhy: 'ran 1.8 min', run: 'npm run tooltests', paths: ['tools/'] },
  ],
  tests: { _why: 'the format', sets: [{ name: 'tools', gate: 'tool-tests', runner: 'node', timeoutMs: 5000, files: ['tools/*.test.mjs'] }] },
  unasked: { may: ['merge'] },
};

function run(root: string, argv: string[]): { code: number; out: string } {
  const lines: string[] = [];
  const code = runCli(['declaration', ...argv], root, (line) => lines.push(line), () => 2, null) as number;
  return { code, out: lines.join('\n') };
}

const filesUnder = (root: string, folder = ''): string[] => readdirSync(join(root, folder), { withFileTypes: true })
  .flatMap((entry) => (entry.isDirectory() ? filesUnder(root, `${folder}${entry.name}/`) : [`${folder}${entry.name}`])).sort();

test('a dry run writes the folder and the home\'s values to scratch, proves them equal and changes nothing else', () => {
  const fixture = makeFixture('declaration-dry-run');
  fixture.write('daoris.gates.json', JSON.stringify(GATES, null, 2));
  fixture.write('tools/a.test.mjs', '');
  fixture.write('tools/a.mjs', '');

  const { code, out } = run(fixture.root, ['split', '--dry-run']);

  assert.equal(code, 0, out);
  assert.match(out, /resolves to the old declaration member for member/);
  assert.match(out, /2 gates, 1 test file/);
  const scratch = '_fixtures/declaration-split';
  assert.deepEqual(filesUnder(join(fixture.root, scratch)), ['daoris/gates/tool-tests.json', 'daoris/gates/universal.json', 'home.json']);
  const home = JSON.parse(fixture.read(`${scratch}/home.json`));
  assert.deepEqual(home.gates, { 'tool-tests': { timeoutMinutes: 8, timeoutWhy: 'ran 1.8 min' } });
  assert.deepEqual(home.sets, { tools: { timeoutMs: 5000 } });
  assert.ok(fixture.exists('daoris.gates.json'), 'the old file is still there: a dry run deletes nothing');
  assert.ok(!fixture.exists('daoris/gates/universal.json'), 'the folder is written to scratch, never to the repository');
  fixture.cleanup();
});

test('a run without --dry-run is refused until the home has its door, and writes nothing', () => {
  const fixture = makeFixture('declaration-no-dry-run');
  fixture.write('daoris.gates.json', JSON.stringify(GATES));

  const { code, out } = run(fixture.root, ['split']);

  assert.equal(code, 2);
  assert.match(out, /--dry-run/);
  assert.deepEqual(readdirSync(fixture.root), ['daoris.gates.json']);
  fixture.cleanup();
});

test('an `--out` folder takes the scratch: its earlier folder is replaced, a file beside it that is not its own is left, and outside the repository is refused', () => {
  const fixture = makeFixture('declaration-out');
  fixture.write('daoris.gates.json', JSON.stringify(GATES));
  assert.equal(run(fixture.root, ['split', '--dry-run', '--out', 'scratch']).code, 0);
  fixture.write('scratch/daoris/gates/old.json', '{}');
  fixture.write('scratch/theirs.txt', 'kept');

  assert.equal(run(fixture.root, ['split', '--dry-run', '--out', 'scratch']).code, 0);

  assert.ok(fixture.exists('scratch/home.json'));
  assert.ok(!fixture.exists('scratch/daoris/gates/old.json'));
  assert.equal(fixture.read('scratch/theirs.txt'), 'kept');
  for (const refused of ['.', '..', 'elsewhere/../..']) {
    const result = run(fixture.root, ['split', '--dry-run', '--out', refused]);
    assert.equal(result.code, 2, refused);
    assert.match(result.out, /outside it|the repository itself/);
  }
  assert.ok(!fixture.exists('daoris'), 'a refused folder is never cleared');
  fixture.cleanup();
});

test('a difference or an overlap is named and the run exits 1; no gates file is nothing to convert', () => {
  const fixture = makeFixture('declaration-named');
  fixture.write('daoris.json', '{}');

  const none = run(fixture.root, ['split', '--dry-run']);
  assert.equal(none.code, 0);
  assert.match(none.out, /no daoris\.gates\.json: nothing to convert/);

  const overlapping = { ...GATES, tests: { sets: [...GATES.tests.sets, { name: 'again', gate: 'tool-tests', runner: 'node', files: ['tools/a.test.mjs'] }] } };
  fixture.write('daoris.gates.json', JSON.stringify(overlapping));
  fixture.write('tools/a.test.mjs', '');
  const result = run(fixture.root, ['split', '--dry-run']);
  assert.equal(result.code, 1);
  assert.match(result.out, /tools\/a\.test\.mjs is held by the sets tools and again/);
  assert.match(result.out, /resolve/);

  fixture.write('daoris.gates.json', JSON.stringify({ ...GATES, heavySlots: 1 }));
  const unsorted = run(fixture.root, ['split', '--dry-run']);
  assert.equal(unsorted.code, 1);
  assert.match(unsorted.out, /heavySlots/);
  fixture.cleanup();
});

test('help and usage name the verb, the way each row does', () => {
  const { code, out } = run(process.cwd(), ['--help']);
  assert.equal(code, 0);
  assert.match(out, /declaration split --dry-run/);
});

test('this repository\'s declaration converts with no difference but the sets it names for a person to resolve', () => {
  const old = JSON.parse(readFileSync(join(REPOSITORY, 'daoris.gates.json'), 'utf8'));
  const split = splitDeclaration(old);

  const proof = proveSame(old, split, repositoryFiles(REPOSITORY));

  assert.deepEqual(proof.differences, []);
  for (const overlap of proof.overlaps) assert.ok(overlap.sets.length >= 2 && overlap.today, `${overlap.file} is named with its sets`);
});

test('the examples hold no gates file, so there is nothing for them to convert', () => {
  for (const example of ['engine', 'game']) {
    const root = join(REPOSITORY, 'examples', example);
    assert.ok(existsSync(root), example);
    const { code, out } = run(root, ['split', '--dry-run']);
    assert.equal(code, 0, out);
    assert.match(out, /no daoris\.gates\.json: nothing to convert/);
    assert.ok(!existsSync(join(root, '_fixtures')), 'nothing is written where nothing converts');
  }
});
