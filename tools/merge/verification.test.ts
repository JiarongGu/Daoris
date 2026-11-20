import { test } from 'vitest';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { REACH } from './reach-table.ts';
import { repoLanes, repoPlan, repoRoot } from './repo-gates.ts';
import { BASELINE, BLOCKING, MERGE_SUITES, gateReach, selectGates } from './reach.ts';
import { isLongGate, isProcessGate } from './plan.ts';
import { buildRecord } from './merge-record.ts';
import {
  declaredWidePaths, fileOverrun, fullReason, fullRefusal, overrunLines, planVerification, readVerifyBudget, readWidePaths, scopeLine, settleVerification,
  verifyBudgetOf, verificationRecord, RUN_REASONS, type FullReason,
} from './verification.ts';

// test-map: strings name paths as data — the cases name changed paths, which the rule matches and nothing reads

const RULE = verifyBudgetOf(JSON.stringify({
  verifyBudget: {
    budgetMinutes: 10, overruns: 'local/verify-overruns.jsonl',
    fullRunReasons: { widePaths: ['daoris.gates.json', 'package-lock.json', 'src/Contracts/**'], reachShare: 0.5, runs: ['after-landing', 'release'] },
  },
}))!;

const reachOf = (paths: string[]) => gateReach(selectGates(repoPlan, paths, { lanes: repoLanes }).gates);
const SKIPPABLE = 'gates the lane table can skip';
const WIDE: FullReason = { kind: 'wide-path', said: 'x is a declared wide path' };

test("this repository declares its rule, a gates file with none has no rule, and the lane table's every-gate rule is the declared wide paths", () => {
  const rule = readVerifyBudget(repoRoot);
  assert.ok(rule, 'daoris.gates.json declares a verifyBudget');
  assert.equal(rule.budgetMs, 10 * 60_000);
  assert.equal(REACH[0]!.gates, '*');
  assert.deepEqual([...REACH[0]!.paths!], [...rule.widePaths], 'one source: the paths that change how every gate runs are the declared wide paths');
  assert.deepEqual([...REACH[0]!.paths!], [...readWidePaths(repoRoot)]);
  for (const path of ['daoris.gates.json', 'package-lock.json', 'src/Daoris.Web/package-lock.json', 'src/Daoris.Contracts/x.json', 'tools/test-map.mjs']) {
    assert.ok(fullReason(rule, [path]), `${path} is a declared wide path`);
  }
  assert.equal(verifyBudgetOf(JSON.stringify({ gates: [] })), null, 'nothing applies to a workspace that did not set it');
  assert.equal(verifyBudgetOf('not json'), null);
});

test("the lane table's every-gate rule reads the declared wide paths whatever the budget, and a rule declared without them fails at load", () => {
  const block = (verifyBudget: object) => JSON.stringify({ verifyBudget, gates: [] });
  for (const budgetMinutes of [0, 'ten', undefined]) {
    const text = block({ budgetMinutes, fullRunReasons: { widePaths: ['a.json', 'b/**'] } });
    assert.deepEqual([...declaredWidePaths(text)], ['a.json', 'b/**'], `a budget of ${String(budgetMinutes)} leaves the wide paths`);
    assert.equal(verifyBudgetOf(text), null, 'while the rule itself still needs its budget');
  }
  assert.deepEqual([...declaredWidePaths(JSON.stringify({ gates: [] }))], [], 'a gates file with no rule declares none');
  for (const reasons of [undefined, {}, { widePaths: [] }, { widePaths: 'a.json' }, { widePaths: [3] }]) {
    assert.throws(() => declaredWidePaths(block({ budgetMinutes: 10, fullRunReasons: reasons })), /declares a verifyBudget with no fullRunReasons\.widePaths/, JSON.stringify(reasons));
  }
});

test('the rule reads only the runs it knows and keeps its findings inside the checkout', () => {
  const of = (verifyBudget: object) => verifyBudgetOf(JSON.stringify({ verifyBudget }));
  assert.deepEqual(of({ budgetMinutes: 10, fullRunReasons: { widePaths: ['a'], runs: ['toString', 'constructor', '__proto__', 'release'] } })!.runs, ['release']);
  for (const outside of ['../elsewhere.jsonl', 'local/../../x.jsonl', 'local\\..\\..\\x.jsonl', '/tmp/x.jsonl', 'C:\\x.jsonl', 'C:x.jsonl', '\\\\host\\share\\x.jsonl']) {
    assert.throws(() => of({ budgetMinutes: 10, overruns: outside }), /verifyBudget.overruns names .* outside the checkout/, outside);
  }
  assert.equal(of({ budgetMinutes: 10, overruns: 'local/findings/over.jsonl' })!.overruns, 'local/findings/over.jsonl');
  assert.equal(of({ budgetMinutes: 10 })!.overruns, 'local/verify-overruns.jsonl');
});

test('a one-file change runs no wide gate and its plan states its scope: the light gates and reached classes now, the heavy tier after', () => {
  const rule = readVerifyBudget(repoRoot)!;
  const paths = ['tools/merge/format.ts'];
  const selection = selectGates(repoPlan, paths, { lanes: repoLanes }).gates;
  const ran = selection.filter((entry) => entry.run).map((entry) => entry.gate);
  assert.ok(ran.length > 0);
  assert.deepEqual(ran.filter((gate) => isLongGate(gate) || isProcessGate(gate)).map((gate) => gate.name), [], 'no Process half, rehearsal or long gate before the landing');
  const reach = gateReach(selection);
  assert.equal(fullReason(rule, paths, reach), null, 'no reason for a full run');
  const line = scopeLine(rule, paths, reach, null, 'reach');
  assert.match(line, /^verification: scoped to its reach: 1 path reaches \d+ of \d+ gates the lane table can skip; the light gates on its paths and the reached classes now, the heavy tier after the landing; budget 10 min \(D349\)$/);
});

test('the reach share counts only the gates the lane table can skip: a Service and a Desktop file read as scoped, a declared wide path as full', () => {
  const rule = readVerifyBudget(repoRoot)!;
  // The gates every merge runs, and those no rule names (which every path reaches), are not a measure of a change's reach.
  const skippable = repoPlan.filter((gate) => ![...BLOCKING, ...BASELINE, ...MERGE_SUITES].includes(gate.name)
    && REACH.some((each) => each.gates !== '*' && each.gates.includes(gate.name)));
  const all = reachOf(['daoris.gates.json']);
  assert.deepEqual(all, { reached: skippable.length, of: skippable.length, what: SKIPPABLE });
  assert.ok(all.of < repoPlan.length);
  assert.equal(fullReason(rule, ['daoris.gates.json'], all)!.kind, 'wide-path', 'the declared path is the reason said, before the reach it brings');
  for (const path of [
    'src/Daoris.Service/Daoris.Service.Http/Program.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/AccountChoice.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/AcceptAnswers.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/SomeTests.cs',
  ]) {
    const reach = reachOf([path]);
    assert.equal(fullReason(rule, [path], reach), null, `${path} reaches ${reach.reached} of ${reach.of} ${reach.what}, and reads as scoped`);
  }
  // The ports, which most of the desktop's and the service's suites build on, still reach over half.
  const ports = reachOf(['src/Daoris.Ports/Agents/IAgentProvider.cs']);
  assert.equal(fullReason(rule, ['src/Daoris.Ports/Agents/IAgentProvider.cs'], ports)?.kind, 'reach-share', JSON.stringify(ports));
  // A reach at the declared share is a reason, and a reach of none of a plan the table cannot skip is none.
  assert.equal(fullReason(RULE, ['a.ts'], { reached: 5, of: 10, what: SKIPPABLE })!.said, `its paths reach 5 of 10 ${SKIPPABLE} (50%), at or over the declared 50%`);
  assert.equal(fullReason(RULE, ['a.ts'], { reached: 0, of: 0, what: SKIPPABLE }), null);
});

test('a daoris.gates.json change states its full-run reason instead of running everything silently', () => {
  const reason = fullReason(RULE, ['daoris.gates.json', 'docs/a.md']);
  assert.deepEqual(reason, { kind: 'wide-path', said: 'daoris.gates.json is a declared wide path' });
  assert.equal(scopeLine(RULE, ['daoris.gates.json', 'docs/a.md'], undefined, reason, 'full'), 'verification: full: daoris.gates.json is a declared wide path; budget 10 min (D349)');
  assert.equal(fullReason(RULE, ['src/Contracts/a.json', 'package-lock.json'])!.said, 'src/Contracts/a.json and 1 more are declared wide paths');
  // A reach over the declared share is a reason too, measured on what the run would run.
  assert.deepEqual(fullReason(RULE, ['a.ts'], { reached: 9, of: 10, what: 'gates' }), { kind: 'reach-share', said: 'its paths reach 9 of 10 gates (90%), at or over the declared 50%' });
  assert.equal(fullReason(RULE, ['a.ts'], { reached: 4, of: 10, what: 'gates' }), null);
});

test('--full is refused with no reason the change bears, naming the reasons it accepts, and taken for a lockfile change', () => {
  const refused = fullRefusal(RULE, ['tools/merge/format.ts'], { reached: 6, of: 25, what: 'gates' });
  assert.ok(refused);
  assert.match(refused, /^--full needs a reason the change bears \(D349\), and its 1 path bears none: it reaches 6 of 25 gates\./);
  assert.match(refused, /a declared wide path \(daoris\.gates\.json, package-lock\.json, src\/Contracts\/\*\*\)/);
  assert.match(refused, /a reach of 50% or more of the gates/);
  assert.match(refused, /the run after a landing \(--ci, --full --beside\)/);
  assert.match(refused, /a release \(--full on the checkout, --ci --stage\)/);
  assert.match(refused, /Merge without --full: its verification runs scoped to its reach/);
  assert.equal(fullRefusal(RULE, ['package-lock.json'], { reached: 6, of: 25, what: 'gates' }), null, 'a lockfile is a declared wide path');
  assert.equal(fullRefusal(RULE, ['a.ts'], { reached: 20, of: 25, what: 'gates' }), null, 'a reach over the share');
  // The runs that are a reason by their kind, named only where the rule declares them.
  assert.deepEqual(Object.keys(RUN_REASONS), ['after-landing', 'release']);
  const releaseOnly = verifyBudgetOf(JSON.stringify({ verifyBudget: { budgetMinutes: 10, fullRunReasons: { widePaths: ['a.json'], runs: ['release'] } } }))!;
  assert.deepEqual(releaseOnly.runs, ['release']);
  const said = fullRefusal(releaseOnly, ['b.ts'], { reached: 1, of: 10, what: 'gates' })!;
  assert.doesNotMatch(said, /the run after a landing/);
  assert.match(said, /of the gates or a release \(--full on the checkout, --ci --stage\)\. Merge without --full/);
});

test('a run says and records what ran beside the reason its change bears, and an overrun\'s cause is read from what ran', () => {
  const reach = { reached: 1, of: 20, what: SKIPPABLE };
  // A batch's --full over a branch that bears no reason of its own: it ran full, for the batch.
  assert.equal(scopeLine(RULE, ['a.ts'], reach, null, 'full'),
    `verification: full: its batch's --full, for a reason another of its branches bears; its own 1 path reaches 1 of 20 ${SKIPPABLE}; budget 10 min (D349)`);
  assert.deepEqual(verificationRecord(RULE, { wallMs: 60_000, reason: null, ran: 'full' }), { budgetMs: 600_000, wallMs: 60_000, over: false, scope: 'reach', ran: 'full' });
  // A plain merge touching a wide path: the change bears the reason, the merge ran its reach.
  assert.match(scopeLine(RULE, ['x'], reach, WIDE, 'reach'),
    /^verification: scoped to its reach: 1 path reaches 1 of 20 gates the lane table can skip; it bears a full run's reason \(x is a declared wide path\); the light gates/);
  assert.deepEqual(verificationRecord(RULE, { wallMs: 60_000, reason: WIDE, ran: 'reach' }),
    { budgetMs: 600_000, wallMs: 60_000, over: false, scope: 'full', ran: 'reach', reason: 'x is a declared wide path', kind: 'wide-path' });
  const cause = (reason: FullReason | null, ran: 'full' | 'reach') => overrunLines(RULE, { wallMs: 11 * 60_000, gates: [], reason, ran, file: 'x' })[0]!;
  assert.match(cause(WIDE, 'full'), /its scope: ran full: x is a declared wide path$/);
  assert.match(cause(null, 'full'), /its scope: ran full for its batch's --full, a reason another of its branches bears$/);
  assert.match(cause(WIDE, 'reach'), /its scope: ran scoped to its reach, and the change is code-wide \(x is a declared wide path\): its reach is most of the gates$/);
  assert.match(cause(null, 'reach'), /its scope: ran scoped to its reach, so the scope was too big for the change or the change is code-wide and should have said so$/);
  // A run whose kind is its reason runs full; a plan or a merge says what it will run.
  assert.equal(planVerification(RULE, [], undefined, { kind: RUN_REASONS.release })!.ran, 'full');
  assert.equal(planVerification(RULE, ['x'], undefined)!.ran, 'reach');
  assert.equal(planVerification(RULE, ['x'], undefined, { ran: 'full' })!.ran, 'full');
  assert.equal(planVerification(null, ['x'], undefined), null);
});

test('a planted overrun is said with where its time went, per gate and the reached run\'s steps, and filed as a finding', () => {
  const gates = [
    { name: 'universal', ms: 112_000 },
    { name: 'reached', ms: 756_000, steps: [{ step: 'census', ms: 9_000 }, { step: 'plan', ms: 113_000 }, { step: 'run cli', ms: 498_000 }, { step: 'run web-unit', ms: 188_000 }] },
    { name: 'web-types', ms: 190_000 },
    { name: 'cli', ms: 6_000 },
    { name: 'code-map', ms: 0 },
  ];
  const lines = overrunLines(RULE, { wallMs: 1_909_000, gates, reason: null, ran: 'reach', file: 'local/verify-overruns.jsonl' });
  assert.equal(lines.length, 3);
  assert.equal(lines[0], '  verification: 31.8 min, over this workspace\'s 10 min budget (D349); its scope: ran scoped to its reach, so the scope was too big for the change or the change is code-wide and should have said so');
  assert.equal(lines[1], '    where its time went: reached 12.6 min (run cli 8 m 18 s, run web-unit 3 m 8 s, plan 1 m 53 s, census 9 s), web-types 3.2 min, universal 1.9 min, cli 0.1 min');
  assert.equal(lines[2], '    filed in local/verify-overruns.jsonl for the steward');
  assert.deepEqual(overrunLines(RULE, { wallMs: 9 * 60_000, gates, reason: null, ran: 'reach', file: 'x' }), [], 'within the budget nothing is said');
  assert.deepEqual(overrunLines(RULE, { wallMs: 10 * 60_000, gates, reason: null, ran: 'reach', file: 'x' }), [], 'at the budget nothing is said');
  const full = overrunLines(RULE, { wallMs: 11 * 60_000, gates, reason: { kind: 'wide-path', said: 'daoris.gates.json is a declared wide path' }, ran: 'full', file: 'x' });
  assert.match(full[0]!, /its scope: ran full: daoris\.gates\.json is a declared wide path$/);

  // The record carries the budget, the wall time, the scope, what ran and the reason; the finding goes to the file the steward reads.
  const verification = verificationRecord(RULE, { wallMs: 1_909_000, reason: null, ran: 'reach' });
  assert.deepEqual(verification, { budgetMs: 600_000, wallMs: 1_909_000, over: true, scope: 'reach', ran: 'reach' });
  const record = buildRecord({ branch: 'b', at: '2026-11-20T00:00:00Z', ms: 1, wallMs: 1_909_000, gates: [], mainReds: [], previous: [], verification });
  assert.deepEqual(record.verification, verification);
  const dir = join(repoRoot, '_fixtures', `verify-overruns-${process.pid}`);
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  try {
    const file = join(dir, 'nested', 'verify-overruns.jsonl');
    fileOverrun(file, { at: '2026-11-20T00:00:00Z', branch: 'b', ...verification, gates: gates.map(({ name, ms, steps }) => ({ name, ms, ...(steps ? { steps } : {}) })) });
    fileOverrun(file, { at: '2026-11-20T01:00:00Z', branch: 'c', ...verification, gates: [] });
    assert.ok(existsSync(file));
    const filed = readFileSync(file, 'utf8').trim().split('\n').map((line) => JSON.parse(line));
    assert.deepEqual(filed.map((each) => each.branch), ['b', 'c']);
    assert.equal(filed[0].gates[1].steps[2].step, 'run cli');
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('an overrun that cannot be filed is said so, and the run keeps its record rather than ending after its gates ran', () => {
  const dir = join(repoRoot, '_fixtures', `verify-unfiled-${process.pid}`);
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  try {
    // A file where the findings' folder would be: the folder cannot be made, nor the line written.
    writeFileSync(join(dir, 'blocker'), 'a file, not a folder\n');
    const planned = planVerification(RULE, ['a.ts'], undefined)!;
    const settled = settleVerification(planned, { branch: 'b', at: 'now', wallMs: 11 * 60_000, gates: [], file: join(dir, 'blocker', 'over.jsonl'), shown: 'local/verify-overruns.jsonl' });
    assert.equal(settled.record.over, true);
    assert.match(settled.lines.at(-1)!, /^ {4}NOT filed in local\/verify-overruns\.jsonl for the steward \(.+\): this summary and the merge record keep it$/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});
