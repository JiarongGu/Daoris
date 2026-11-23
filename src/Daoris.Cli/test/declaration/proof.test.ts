import { test } from 'node:test';
import assert from 'node:assert/strict';
import { splitDeclaration, type Declaration } from '../../src/declaration/split.ts';
import { proveSame, type Proof } from '../../src/declaration/proof.ts';
import { globToRegExp as toolsGlob } from '../../../../tools/merge/lanes-file.ts';
import { globToRegExp } from '../../src/declaration/reading.ts';

/**
 * The proof the conversion owes: the folder and the home resolve to the old file member for member: every gate's
 * commands, its expanded reach, the set holding each tracked test file, the gates' order. A difference is named and stops the
 * conversion; sets that overlap today are named for the repository to resolve.
 */

const FILES = [
  'tools/a.mjs', 'tools/a.test.mjs', 'tools/b.test.mjs', 'tools/merge/x.test.ts', 'tools/merge/x.e2e.ts',
  'src/Cli/tool-test/one.test.ts', 'src/Cli/package.json', 'docs/README.md',
];

function declaration(): Declaration {
  return {
    gates: [
      { name: 'universal', run: 'node tools/universal.mjs' },
      { name: 'cli', run: 'npm run verify:checks', clock: true, paths: ['tools/', 'src/Cli/package.json', 'docs/**'] },
      { name: 'tool-tests', timeoutMinutes: 8, run: 'npm run tooltests', paths: ['src/Cli/tool-test/'] },
      { name: 'cli-tests', run: 'npm run verify:tests', paths: ['tools/*.test.mjs', 'tools/merge/'] },
    ],
    tests: {
      sets: [
        { name: 'tool-tests', gate: 'tool-tests', runner: 'node', files: ['src/Cli/tool-test/**/*.test.ts'] },
        { name: 'merge-unit', gate: 'cli-tests', runner: 'vitest', timeoutMs: 10000, files: ['tools/merge/**/*.test.ts'] },
        { name: 'merge-e2e', gate: 'cli-tests', runner: 'vitest', files: ['tools/merge/**/*.e2e.ts'] },
        { name: 'tools', gate: 'cli-tests', runner: 'node', files: ['tools/*.test.mjs'] },
      ],
    },
  };
}

const prove = (old: Declaration, tamper?: (folder: Record<string, unknown>, home: Record<string, unknown>) => void): Proof => {
  const split = splitDeclaration(old);
  tamper?.(split.folder, split.home as Record<string, unknown>);
  return proveSame(old, split, FILES);
};
const kinds = (proof: Proof): string[] => proof.differences.map((each) => `${each.kind}:${each.subject}`);

test('a converted declaration resolves to the old one: no difference and nothing to resolve', () => {
  const proof = prove(declaration());

  assert.deepEqual(proof.differences, []);
  assert.deepEqual(proof.overlaps, []);
  assert.equal(proof.gates, 4);
  assert.equal(proof.testFiles, 5);
});

test('a test file moved between sets in the folder is refused, naming the file and both sets', () => {
  const proof = prove(declaration(), (folder) => {
    const cliTests = folder['gates/cli-tests.json'] as { tests: { name: string; files: string[] }[] };
    cliTests.tests.find((set) => set.name === 'merge-unit')!.files = ['tools/merge/**/*.test.ts', 'tools/a.test.mjs'];
    cliTests.tests.find((set) => set.name === 'tools')!.files = ['tools/b.test.mjs'];
  });

  assert.ok(kinds(proof).includes('set:tools/a.test.mjs'), kinds(proof).join('\n'));
  const moved = proof.differences.find((each) => each.kind === 'set');
  assert.equal(moved?.was, 'tools');
  assert.equal(moved?.now, 'merge-unit');
});

test('a reordered gate is refused, naming the order each reads', () => {
  const proof = prove(declaration(), (folder) => {
    delete (folder['gates/cli.json'] as { after?: string[] }).after;
  });

  const order = proof.differences.find((each) => each.kind === 'order');
  assert.ok(order, kinds(proof).join('\n'));
  assert.equal(order.was, 'universal, cli, tool-tests, cli-tests');
  assert.equal(order.now, 'cli, universal, tool-tests, cli-tests');
});

test('gates that wait on each other in a ring are refused, the ring named', () => {
  const proof = prove(declaration(), (folder) => {
    (folder['gates/universal.json'] as { after?: string[] }).after = ['cli-tests'];
  });

  const ring = proof.differences.find((each) => each.kind === 'folder');
  assert.ok(ring, kinds(proof).join('\n'));
  assert.match(ring.now, /follow each other in a ring/);
});

test('a gate dropped from the folder, or one added to it, is named', () => {
  const dropped = prove(declaration(), (folder) => { delete folder['gates/cli.json']; });
  assert.ok(kinds(dropped).includes('gate:gates[cli]'), kinds(dropped).join('\n'));

  const added = prove(declaration(), (folder) => { folder['gates/extra.json'] = { name: 'extra', run: 'x' }; });
  assert.ok(kinds(added).includes('gate:gates[extra]'), kinds(added).join('\n'));
});

test('a changed command, a changed reach and a machine value lost from the home are each named', () => {
  const command = prove(declaration(), (folder) => { (folder['gates/cli.json'] as { run: string }).run = 'npm run other'; });
  assert.ok(kinds(command).includes('member:gates[cli].run'), kinds(command).join('\n'));

  const reach = prove(declaration(), (folder) => { (folder['gates/cli.json'] as { paths: string[] }).paths = ['tools/']; });
  const lost = reach.differences.find((each) => each.kind === 'reach');
  assert.equal(lost?.subject, 'cli');
  assert.match(lost?.was ?? '', /docs\/README\.md/);

  const home = prove(declaration(), (_folder, values) => { delete (values.gates as Record<string, unknown>)['tool-tests']; });
  assert.ok(kinds(home).includes('member:gates[tool-tests].timeoutMinutes'), kinds(home).join('\n'));
});

test('a reach written as a folder, a glob or a file expands to the files it holds', () => {
  const proof = prove(declaration());
  assert.deepEqual(proof.differences, []);
  const reaches = proof.reaches;
  assert.deepEqual(reaches.cli, ['docs/README.md', 'src/Cli/package.json', 'tools/a.mjs', 'tools/a.test.mjs', 'tools/b.test.mjs', 'tools/merge/x.e2e.ts', 'tools/merge/x.test.ts']);
  assert.equal(reaches.universal, null, 'a gate with no paths reaches every change');
});

test('sets that hold one file today are named for the repository to resolve, and the folder refuses them as the old order let pass', () => {
  const old = declaration();
  (old.tests as { sets: Record<string, unknown>[] }).sets.push({ name: 'a-only', gate: 'cli-tests', runner: 'node', files: ['tools/a.test.mjs'] });

  const proof = prove(old);

  assert.deepEqual(proof.overlaps, [{ file: 'tools/a.test.mjs', sets: ['tools', 'a-only'], today: 'tools' }]);
  assert.deepEqual(proof.differences, []);
});

test('a set that names the other by `of` may hold the same files: that is a pick, not an overlap', () => {
  const old = declaration();
  (old.tests as { sets: Record<string, unknown>[] }).sets.push({ name: 'pick', gate: 'cli-tests', runner: 'node', of: 'tools', files: ['tools/a.test.mjs'] });

  assert.deepEqual(prove(old).overlaps, []);
});

test('a gate\'s order is read from the files alone: two orders of the same files resolve the same', () => {
  const old = declaration();
  const split = splitDeclaration(old);
  const names = Object.keys(split.folder).reverse();
  const shuffled = Object.fromEntries(names.map((name) => [name, split.folder[name]]));

  assert.deepEqual(proveSame(old, { ...split, folder: shuffled }, FILES).differences, []);
});

test('the glob grammar is the lane table\'s: the two read every pattern alike', () => {
  const patterns = ['tools/*.test.mjs', 'tools/merge/**/*.test.ts', 'src/**', '**/x.ts', 'a/{b,c}/*.ts', 'a/?.ts', 'docs/**/*.md', 'src/Daoris.Cli/test/**/*.ts'];
  const paths = ['tools/a.test.mjs', 'tools/merge/x.test.ts', 'tools/merge/deep/y.test.ts', 'src/a/b.ts', 'x.ts', 'a/b/z.ts', 'a/c/d/z.ts', 'a/q.ts', 'docs/a/b.md', 'docs/x.md', 'src/Daoris.Cli/test/a.ts'];
  for (const pattern of patterns) {
    for (const path of paths) assert.equal(globToRegExp(pattern).test(path), toolsGlob(pattern).test(path), `${pattern} vs ${path}`);
  }
});
