import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readdirSync, readFileSync, symlinkSync, writeFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { makeFixture } from '../_fixture.ts';
import { runCli } from '../../src/cli.ts';
import { splitDeclaration } from '../../src/declaration/split.ts';
import { proveSame } from '../../src/declaration/proof.ts';
import { repositoryFiles } from '../../src/declaration/tree.ts';
import { readScratch } from '../../src/declaration/dryrun.ts';
import { formatJson } from '../../src/declaration/json.ts';

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
  tests: { _why: 'the format', sets: [{ name: 'tools', gate: 'tool-tests', runner: 'node', timeoutMs: 120000, files: ['tools/*.test.mjs'] }] },
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
  assert.deepEqual(filesUnder(join(fixture.root, scratch)), ['.declaration-split', 'daoris/gates/tool-tests.json', 'daoris/gates/universal.json', 'home.json']);
  const home = JSON.parse(fixture.read(`${scratch}/home.json`));
  assert.deepEqual(home.gates, { 'tool-tests': { timeoutMinutes: 8, timeoutWhy: 'ran 1.8 min' } });
  assert.deepEqual(home.sets, { tools: { timeoutMs: 120000 } });
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

test('the example engine holds no gates file, and the example game\'s, copied out of the tracked example, converts member for member', () => {
  const engine = join(REPOSITORY, 'examples', 'engine');
  const none = run(engine, ['split', '--dry-run']);
  assert.equal(none.code, 0, none.out);
  assert.match(none.out, /no daoris\.gates\.json: nothing to convert/);
  assert.ok(!existsSync(join(engine, '_fixtures')), 'nothing is written where nothing converts');

  const fixture = makeFixture('declaration-example-game');
  fixture.write('daoris.gates.json', readFileSync(join(REPOSITORY, 'examples', 'game', 'daoris.gates.json'), 'utf8'));
  const game = run(fixture.root, ['split', '--dry-run']);
  assert.equal(game.code, 0, game.out);
  assert.match(game.out, /resolves to the old declaration member for member/);
  fixture.cleanup();
});

test('a folder the command did not make is never cleared: an existing daoris folder or home.json under --out is refused and kept', () => {
  const fixture = makeFixture('declaration-unowned');
  fixture.write('daoris.gates.json', JSON.stringify(GATES));
  fixture.write('samples/daoris/keep.txt', 'mine');

  const folder = run(fixture.root, ['split', '--dry-run', '--out', 'samples']);
  assert.equal(folder.code, 2);
  assert.match(folder.out, /did not make/);
  assert.equal(fixture.read('samples/daoris/keep.txt'), 'mine');
  assert.ok(!fixture.exists('samples/home.json'));

  fixture.write('other/home.json', 'mine');
  assert.equal(run(fixture.root, ['split', '--dry-run', '--out', 'other']).code, 2);
  assert.equal(fixture.read('other/home.json'), 'mine');
  fixture.cleanup();
});

test('a link under --out or above it that leads outside the repository is refused, and nothing outside is touched', () => {
  const fixture = makeFixture('declaration-link');
  const outside = makeFixture('declaration-link-outside');
  fixture.write('daoris.gates.json', JSON.stringify(GATES));
  outside.write('daoris/theirs.txt', 'theirs');
  symlinkSync(outside.root, join(fixture.root, 'scratch-link'), 'junction');
  mkdirSync(join(fixture.root, 'scratch'));
  symlinkSync(join(outside.root, 'daoris'), join(fixture.root, 'scratch', 'daoris'), 'junction');

  for (const refused of ['scratch-link', 'scratch-link/deeper', 'scratch']) {
    const result = run(fixture.root, ['split', '--dry-run', '--out', refused]);
    assert.equal(result.code, 2, refused);
    assert.match(result.out, /outside it|link|did not make/, refused);
  }
  assert.equal(outside.read('daoris/theirs.txt'), 'theirs');
  assert.ok(!outside.exists('home.json'));
  fixture.cleanup();
  outside.cleanup();
});

test('gate names that differ only by case are refused before anything is written: one filesystem folds them into one file', () => {
  const fixture = makeFixture('declaration-case');
  fixture.write('daoris.gates.json', JSON.stringify({ gates: [{ name: 'check', run: 'one' }, { name: 'Check', run: 'two' }] }));

  const result = run(fixture.root, ['split', '--dry-run']);

  assert.equal(result.code, 1);
  assert.match(result.out, /check and Check would be one file/);
  assert.ok(!fixture.exists('_fixtures'));
  fixture.cleanup();
});

test('the proof reads the files the dry run wrote: the folder and home read back from scratch equal what was split', () => {
  const fixture = makeFixture('declaration-readback');
  fixture.write('daoris.gates.json', JSON.stringify(GATES));
  assert.equal(run(fixture.root, ['split', '--dry-run']).code, 0);
  const split = splitDeclaration(GATES);

  const back = readScratch(join(fixture.root, '_fixtures', 'declaration-split'));

  assert.deepEqual(back.folder, split.folder);
  assert.deepEqual(back.home, split.home);
  writeFileSync(join(fixture.root, '_fixtures', 'declaration-split', 'daoris', 'gates', 'universal.json'), formatJson({ name: 'universal', run: 'changed' }));
  assert.equal((readScratch(join(fixture.root, '_fixtures', 'declaration-split')).folder['gates/universal.json'] as { run: string }).run, 'changed');
  fixture.cleanup();
});

test('a tracked file under a folder named bin is a tracked file: held by two sets it is named, and the walk lists it', () => {
  const fixture = makeFixture('declaration-tracked-bin');
  const overlapping = { gates: [{ name: 'g', run: 'x' }], tests: { sets: [
    { name: 'one', gate: 'g', runner: 'node', files: ['pkg/bin/*.test.ts'] },
    { name: 'two', gate: 'g', runner: 'node', files: ['pkg/**/x.test.ts'] },
  ] } };
  fixture.write('daoris.gates.json', JSON.stringify(overlapping));
  fixture.write('pkg/bin/x.test.ts', '');
  fixture.write('pkg/obj/ignored.txt', '');
  for (const git of [['init', '-q'], ['add', 'daoris.gates.json', 'pkg/bin/x.test.ts']]) {
    assert.equal(spawnSync('git', git, { cwd: fixture.root, windowsHide: true }).status, 0, git.join(' '));
  }

  assert.deepEqual(repositoryFiles(fixture.root), ['daoris.gates.json', 'pkg/bin/x.test.ts']);
  const result = run(fixture.root, ['split', '--dry-run']);
  assert.equal(result.code, 1, result.out);
  assert.match(result.out, /pkg\/bin\/x\.test\.ts is held by the sets one and two/);
  fixture.cleanup();
});

test('the tracked files of this repository include what a folder named bin holds', () => {
  assert.ok(repositoryFiles(REPOSITORY).includes('src/Daoris.Cli/bin/daoris.mjs'));
});
