import { test } from 'node:test';
import assert from 'node:assert/strict';
import { splitDeclaration, type Declaration } from '../../src/declaration/split.ts';
import { readFolder } from '../../src/declaration/folder.ts';
import { GATE_MEMBERS, SET_MEMBERS } from '../../src/declaration/members.ts';

/**
 * The converter's data and its writing: one test sorts every member, so each member of a declaration goes to the
 * repository's folder, to the home, or to the product's documentation, and a member nobody sorted stops the conversion by name.
 */

/** A declaration with one member of every kind the member table names. */
function sample(): Declaration {
  return {
    devkit: '0.0.1',
    tiers: { needs: { git: ['src/git/'] } },
    treeBaseline: 'node tools/quick.mjs --record-base',
    localInputs: ['local/a.props'],
    generated: [{ paths: 'tools/size.json', run: 'node tools/size.mjs --shrink' }],
    reached: { run: 'node tools/reached.mjs', clock: true, outcome: 'reached: ' },
    admission: { _why: 'the line', always: ['universal'], suites: [], byPaths: [] },
    verifyBudget: { _why: 'the rule', budgetMinutes: 10, overruns: 'local/overruns.jsonl', fullRunReasons: { widePaths: ['daoris.gates.json'] } },
    gates: [
      { name: 'universal', run: 'node tools/universal.mjs' },
      { name: 'cli', run: 'npm run verify:checks', clock: true, handback: true, regenerate: 'node tools/contracts.mjs' },
      { name: 'tool-tests', timeoutMinutes: 8, timeoutWhy: 'ran 1.8 min x 4', run: 'npm run tooltests', clock: true, paths: ['tools/', 'src/Cli/'] },
      { name: 'reached', run: 'node tools/reached.mjs --base {base}', memoryGb: 6, reuse: 'inputs+diff', alone: true, paths: ['src/'] },
    ],
    tests: {
      _why: 'documents the format',
      sets: [
        { name: 'tool-tests', gate: 'tool-tests', runner: 'node', cwd: 'src/Cli', files: ['src/Cli/tool-test/**/*.test.ts'], whole: ['src/Cli/package.json'], args: ['--test-force-exit'] },
        { name: 'merge-e2e', gate: 'tool-tests', runner: 'vitest', config: 'tools/vitest.config.ts', project: 'e2e', timeoutMs: 120000, files: ['tools/merge/**/*.e2e.ts'], _why: 'slow' },
      ],
    },
    unasked: { _why: 'what an agent may do', may: ['merge a branch'], ownerOnly: ['money'] },
    integration: { merge: [{ paths: 'docs/README.md', by: 'rows' }] },
    sensitive: { patternsFile: 'local/patterns.txt', _reviewedObjects: ['04801cb — judged benign'], reviewedObjects: ['04801cb'] },
    version: { source: 'src/Cli/package.json' },
    doctrine: { command: 'node', args: ['src/Cli/bin/daoris.mjs'] },
    docs: { changelog: 'CHANGELOG.md', graceDays: 7, tracked: [{ document: 'docs/a.md', describes: ['src/a'] }] },
  };
}

test('each member goes where the sorting test puts it: the folder, the home or the product', () => {
  const split = splitDeclaration(sample());

  assert.deepEqual(Object.keys(split.folder).sort(), [
    'admission.json', 'doctrine.json', 'docs.json', 'generated.json', 'gates/cli.json', 'gates/reached.json', 'gates/tool-tests.json',
    'gates/universal.json', 'merge.json', 'reached.json', 'sensitive.json', 'tiers.json', 'verify-budget.json', 'version.json',
  ].sort());
  assert.deepEqual(split.home, {
    devkit: '0.0.1',
    treeBaseline: 'node tools/quick.mjs --record-base',
    localInputs: ['local/a.props'],
    unasked: sample().unasked,
    sensitive: { patternsFile: 'local/patterns.txt' },
    verifyBudget: { overruns: 'local/overruns.jsonl' },
    gates: {
      'tool-tests': { timeoutMinutes: 8, timeoutWhy: 'ran 1.8 min x 4' },
      reached: { memoryGb: 6, alone: true },
    },
    sets: { 'merge-e2e': { timeoutMs: 120000 } },
  });
  assert.deepEqual(split.product, ['tests._why']);
});

test('a gate file holds the gate\'s commands, reach and the test sets it runs, and nothing tuned to a machine', () => {
  const { folder } = splitDeclaration(sample());

  assert.deepEqual(folder['gates/tool-tests.json'], {
    name: 'tool-tests',
    run: 'npm run tooltests',
    clock: true,
    paths: ['tools/', 'src/Cli/'],
    after: ['universal'],
    tests: [
      { name: 'tool-tests', runner: 'node', cwd: 'src/Cli', files: ['src/Cli/tool-test/**/*.test.ts'], whole: ['src/Cli/package.json'], args: ['--test-force-exit'] },
      { name: 'merge-e2e', runner: 'vitest', config: 'tools/vitest.config.ts', project: 'e2e', files: ['tools/merge/**/*.e2e.ts'], _why: 'slow' },
    ],
  });
  assert.deepEqual(folder['gates/reached.json'], { name: 'reached', run: 'node tools/reached.mjs --base {base}', reuse: 'inputs+diff', paths: ['src/'], after: ['tool-tests'] });
  assert.deepEqual(folder['gates/cli.json'], { name: 'cli', run: 'npm run verify:checks', clock: true, handback: true, regenerate: 'node tools/contracts.mjs', after: ['universal'] });
});

test('a section keeps its own members in its file and sends only the machine\'s to the home', () => {
  const { folder } = splitDeclaration(sample());

  assert.deepEqual(folder['sensitive.json'], { _reviewedObjects: ['04801cb — judged benign'], reviewedObjects: ['04801cb'] });
  assert.deepEqual(folder['verify-budget.json'], { _why: 'the rule', budgetMinutes: 10, fullRunReasons: { widePaths: ['daoris.gates.json'] } });
  assert.deepEqual(folder['merge.json'], { merge: [{ paths: 'docs/README.md', by: 'rows' }] });
  assert.deepEqual(folder['generated.json'], { generated: [{ paths: 'tools/size.json', run: 'node tools/size.mjs --shrink' }] });
  assert.deepEqual(folder['reached.json'], { run: 'node tools/reached.mjs', clock: true, outcome: 'reached: ' });
});

test('a gate\'s place is its own name and, only where the default order would move it, the gate it follows', () => {
  const declaration = sample();
  declaration.gates = [{ name: 'b', run: 'b' }, { name: 'a', run: 'a' }, { name: 'c', run: 'c' }, { name: 'aa', run: 'aa' }];
  delete declaration.tests;

  const { folder } = splitDeclaration(declaration);

  assert.equal((folder['gates/b.json'] as { after?: string[] }).after, undefined);
  assert.deepEqual((folder['gates/a.json'] as { after?: string[] }).after, ['b']);
  assert.equal((folder['gates/c.json'] as { after?: string[] }).after, undefined);
  assert.deepEqual((folder['gates/aa.json'] as { after?: string[] }).after, ['c']);
  const back = readFolder(folder, splitDeclaration(declaration).home);
  assert.deepEqual(back.refusals, []);
  assert.deepEqual((back.declaration.gates as { name: string }[]).map((gate) => gate.name), ['b', 'a', 'c', 'aa']);
});

test('a member nobody sorted stops the conversion, each named, rather than going to one side by luck', () => {
  const declaration = sample();
  declaration.heavySlots = 1;
  (declaration.gates as Record<string, unknown>[])[0]!.priority = 3;
  (declaration.tests as { sets: Record<string, unknown>[] }).sets[0]!.shuffle = true;
  (declaration.tests as Record<string, unknown>).retries = 2;
  (declaration.sensitive as Record<string, unknown>).scanner = 'x';

  assert.throws(() => splitDeclaration(declaration), (error: Error) => {
    for (const named of ['heavySlots', 'gates[universal].priority', 'tests.sets[tool-tests].shuffle', 'tests.retries', 'sensitive.scanner']) {
      assert.match(error.message, new RegExp(named.replace(/[[\].]/g, '\\$&')), named);
    }
    return true;
  });
});

test('a gate named twice, a set of no gate and a declaration that is no object are refused by name', () => {
  const twice = sample();
  (twice.gates as { name: string }[]).push({ name: 'cli' });
  assert.throws(() => splitDeclaration(twice), /gate cli is declared twice/);

  const orphan = sample();
  (orphan.tests as { sets: Record<string, unknown>[] }).sets.push({ name: 'lost', gate: 'nobody', runner: 'node', files: ['x'] });
  assert.throws(() => splitDeclaration(orphan), /test set lost names gate nobody, which is no gate/);

  assert.throws(() => splitDeclaration([] as unknown as Declaration), /no object/);
});

test('every member the table names has one side, and the table covers what the gates file holds today', () => {
  for (const [name, side] of Object.entries({ ...GATE_MEMBERS, ...SET_MEMBERS })) {
    assert.ok(['repository', 'home'].includes(side), `${name} sits on a side`);
  }
  assert.equal(GATE_MEMBERS.timeoutMinutes, 'home');
  assert.equal(GATE_MEMBERS.clock, 'repository');
  assert.equal(SET_MEMBERS.timeoutMs, 'home');
});
