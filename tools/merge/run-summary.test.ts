import { test } from 'vitest';
import assert from 'node:assert/strict';
import { buildSummary, rulesOf, summaryLines, type SummaryInput } from './run-summary.ts';
import type { GateReceipt } from './receipts.ts';

const pass = (unit: string, over: Partial<GateReceipt> = {}): GateReceipt => ({
  unit, command: `run ${unit}`, verdict: 'pass', ms: 60_000, waitMs: 0, replayed: false, at: '2026-11-27T10:00:00Z', ...over,
});

const input = (over: Partial<SummaryInput> = {}): SummaryInput => ({
  branch: 'b', tip: 'a'.repeat(40), base: 'c'.repeat(40),
  changes: [
    { path: 'tools/merge/receipts.ts', added: 90, removed: 0 },
    { path: 'tools/merge/preflight.ts', added: 10, removed: 2 },
    { path: 'src/Daoris.Web/src/a.ts', added: 3, removed: 1 },
    { path: 'TASKS.md', added: 2, removed: 2 },
  ],
  modules: [{ id: 'tools', path: 'tools' }, { id: 'Daoris.Web', path: 'src/Daoris.Web' }, { id: 'Daoris.Web.src', path: 'src/Daoris.Web/src' }],
  areas: [], receipts: [pass('censuses'), pass('reached', { replayed: true })],
  required: [{ unit: 'censuses', command: 'dotnet test --filter Category=Census' }, { unit: 'reached', command: 'node tools/reached-checks.mjs' }],
  deferred: [], ...over,
});

test('the changes are grouped by the module whose path is the longest prefix, with their lines, and a path no module places is named', () => {
  const said = buildSummary(input()).changed;
  assert.equal(said.files, 4);
  assert.equal(said.added, 105);
  assert.equal(said.removed, 5);
  assert.deepEqual(said.modules.map((each) => [each.id, each.files, each.added, each.removed]), [['tools', 2, 100, 2], ['Daoris.Web.src', 1, 3, 1]]);
  assert.deepEqual(said.unplaced, ['TASKS.md']);
});

test('an area document\'s rules are compared by number: a rule added, removed or rewritten is named with its first words', () => {
  const before = '# Area\n\n1. One stands.\n2. Two stands, as it was.\n3. Three goes.\n';
  const after = '# Area\n\n1. One stands.\n2. Two now says more.\n4. Four is new.\n';
  const found = rulesOf('docs/areas/x.md', before, after);
  assert.deepEqual(found.map((each) => [each.rule, each.change]), [['2', 'rewritten'], ['3', 'removed'], ['4', 'added']]);
  assert.equal(found[0]?.heading, 'Two now says more.');
});

test('a rule that continues on indented lines is one rule, and a lettered rule is its own', () => {
  const before = '3e. A role.\n    its second line.\n3f. Another.\n';
  const after = '3e. A role.\n    its second line, changed.\n3f. Another.\n';
  assert.deepEqual(rulesOf('docs/areas/x.md', before, after).map((each) => [each.rule, each.change]), [['3e', 'rewritten']]);
});

test('a document with no rule changed, or no before, names nothing but its added rules', () => {
  assert.deepEqual(rulesOf('docs/areas/x.md', '1. A.\n', '1. A.\n'), []);
  assert.deepEqual(rulesOf('docs/areas/x.md', null, '1. A.\n').map((each) => each.change), ['added']);
});

test('what is verified lists each unit with its receipt, saying which was replayed, and counts passes, fails and the missing', () => {
  const said = buildSummary(input({ receipts: [pass('censuses'), pass('reached', { verdict: 'fail', note: 'RED x' })], required: [...input().required, { unit: 'cli', command: 'npm run verify:checks' }] })).verified;
  assert.equal(said.passed, 1);
  assert.equal(said.failed, 1);
  assert.deepEqual(said.missing.map((each) => each.unit), ['cli']);
  assert.equal(said.units.find((each) => each.unit === 'reached')?.note, 'RED x');
});

test('what is left for judgement is what code cannot decide: a decision, a rewritten rule, a deferred tier, a failed or missing unit', () => {
  const said = buildSummary(input({
    changes: [{ path: 'docs/decisions/D367.md', added: 20, removed: 0 }, { path: 'docs/areas/x.md', added: 1, removed: 1 }],
    areas: [{ path: 'docs/areas/x.md', before: '1. Old.\n', after: '1. New.\n' }],
    receipts: [pass('censuses', { verdict: 'fail' })],
    deferred: [{ name: 'driver-process', why: 'a Process half' }],
  })).judgement;
  assert.deepEqual(said.map((each) => each.kind).sort(), ['decision', 'deferred', 'failed', 'rule', 'unverified'].sort());
  assert.match(said.find((each) => each.kind === 'decision')!.what, /D367/);
  assert.match(said.find((each) => each.kind === 'deferred')!.what, /driver-process/);
});

test('a clean change has nothing left for judgement', () => {
  assert.deepEqual(buildSummary(input()).judgement, []);
});

test('the text names the three parts in order and says each replayed unit; the record is the same data, not a second reading', () => {
  const summary = buildSummary(input());
  const lines = summaryLines(summary);
  const at = (word: string) => lines.findIndex((line) => line.startsWith(word));
  assert.ok(at('Changed') >= 0 && at('Verified') > at('Changed') && at('Left for judgement') > at('Verified'));
  assert.ok(lines.some((line) => /reached.*replayed/.test(line)));
  assert.ok(lines.some((line) => /tools: 2 files/.test(line)));
  assert.equal(JSON.parse(JSON.stringify(summary)).schema, 1);
});

test('rules are told apart by their section, so a second list that starts at 1 again never stands for the first', () => {
  const doc = (rule5: string, contradiction5: string) => `# Area\n\n## Its rules\n\n4. Four.\n5. ${rule5}\n\n## Contradictions\n\n4. Other four.\n5. ${contradiction5}\n`;
  const found = rulesOf('docs/areas/x.md', doc('Five.', 'Other five.'), doc('Five, rewritten.', 'Other five.'));
  assert.deepEqual(found.map((each) => [each.section, each.rule, each.change]), [['Its rules', '5', 'rewritten']]);
  const removed = rulesOf('docs/areas/x.md', doc('Five.', 'Other five.'), '# Area\n\n## Its rules\n\n4. Four.\n\n## Contradictions\n\n4. Other four.\n5. Other five.\n');
  assert.deepEqual(removed.map((each) => [each.section, each.rule, each.change]), [['Its rules', '5', 'removed']]);
});

test('the summary counts the failed and the missing as the done refusal does, from the required units alone', () => {
  const said = buildSummary(input({ receipts: [pass('censuses'), pass('stray', { verdict: 'fail' }), pass('reached', { verdict: 'ended' })] }));
  assert.deepEqual([said.verified.failed, said.verified.missing.map((each) => each.unit)], [0, ['reached']]);
});
