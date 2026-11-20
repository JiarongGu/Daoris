import { test } from 'vitest';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { REACH } from './reach-table.ts';
import { repoLanes, repoPlan, repoRoot } from './repo-gates.ts';
import { selectGates } from './reach.ts';
import { isLongGate, isProcessGate } from './plan.ts';
import { buildRecord } from './merge-record.ts';
import {
  fileOverrun, fullReason, fullRefusal, gateReach, overrunLines, readVerification, scopeLine, verificationOf, verificationRecord, RUN_REASONS,
} from './verification.ts';

// test-map: strings name paths as data — the cases name changed paths, which the rule matches and nothing reads

const RULE = verificationOf(JSON.stringify({
  verification: {
    budgetMinutes: 10, overruns: 'local/verify-overruns.jsonl',
    fullRunReasons: { widePaths: ['daoris.gates.json', 'package-lock.json', 'src/Contracts/**'], reachShare: 0.5, runs: ['after-landing', 'release'] },
  },
}))!;

const reachOf = (paths: string[]) => gateReach(selectGates(repoPlan, paths, { lanes: repoLanes }).gates);

test("this repository declares its rule, a gates file with none has no rule, and the lane table's every-gate rule is the declared wide paths", () => {
  const rule = readVerification(repoRoot);
  assert.ok(rule, 'daoris.gates.json declares a verification');
  assert.equal(rule.budgetMs, 10 * 60_000);
  assert.equal(REACH[0]!.gates, '*');
  assert.deepEqual([...REACH[0]!.paths!], [...rule.widePaths], 'one source: the paths that change how every gate runs are the declared wide paths');
  for (const path of ['daoris.gates.json', 'package-lock.json', 'src/Daoris.Web/package-lock.json', 'src/Daoris.Contracts/x.json', 'tools/test-map.mjs']) {
    assert.ok(fullReason(rule, [path]), `${path} is a declared wide path`);
  }
  assert.equal(verificationOf(JSON.stringify({ gates: [] })), null, 'nothing applies to a workspace that did not set it');
  assert.equal(verificationOf('not json'), null);
});

test('a one-file change runs no wide gate and its plan states its scope: the light gates and reached classes now, the heavy tier after', () => {
  const rule = readVerification(repoRoot)!;
  const paths = ['tools/merge/format.ts'];
  const selection = selectGates(repoPlan, paths, { lanes: repoLanes }).gates;
  const ran = selection.filter((entry) => entry.run).map((entry) => entry.gate);
  assert.ok(ran.length > 0);
  assert.deepEqual(ran.filter((gate) => isLongGate(gate) || isProcessGate(gate)).map((gate) => gate.name), [], 'no Process half, rehearsal or long gate before the landing');
  const reach = gateReach(selection);
  assert.equal(fullReason(rule, paths, reach), null, 'no reason for a full run');
  const line = scopeLine(rule, paths, reach, null);
  assert.match(line, /^verification: scoped to its reach: 1 path reaches \d+ of \d+ gates; the light gates on its paths and the reached classes now, the heavy tier after the landing; budget 10 min \(D349\)$/);
});

test('a daoris.gates.json change states its full-run reason instead of running everything silently', () => {
  const reason = fullReason(RULE, ['daoris.gates.json', 'docs/a.md']);
  assert.deepEqual(reason, { kind: 'wide-path', said: 'daoris.gates.json is a declared wide path' });
  assert.equal(scopeLine(RULE, ['daoris.gates.json', 'docs/a.md'], undefined, reason), 'verification: full: daoris.gates.json is a declared wide path; budget 10 min (D349)');
  assert.equal(fullReason(RULE, ['src/Contracts/a.json', 'package-lock.json'])!.said, 'src/Contracts/a.json and 1 more are declared wide paths');
  // A reach over the declared share is a reason too, measured on what the run would run.
  assert.deepEqual(fullReason(RULE, ['a.ts'], { reached: 9, of: 10, what: 'gates' }), { kind: 'reach-share', said: 'its paths reach 9 of 10 gates (90%), over the declared 50%' });
  assert.equal(fullReason(RULE, ['a.ts'], { reached: 4, of: 10, what: 'gates' }), null);
  // The real table: this repository's own gates file is a wide path, and reaches every gate.
  const rule = readVerification(repoRoot)!;
  const reach = reachOf(['daoris.gates.json']);
  assert.equal(reach.reached, reach.of);
  assert.equal(fullReason(rule, ['daoris.gates.json'], reach)!.kind, 'wide-path', 'the declared path is the reason said, before the reach it brings');
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
  // The runs that are a reason by their kind.
  assert.deepEqual(Object.keys(RUN_REASONS), ['after-landing', 'release']);
});

test('a planted overrun is said with where its time went, per gate and the reached run\'s steps, and filed as a finding', () => {
  const gates = [
    { name: 'universal', ms: 112_000 },
    { name: 'reached', ms: 756_000, steps: [{ step: 'census', ms: 9_000 }, { step: 'plan', ms: 113_000 }, { step: 'run cli', ms: 498_000 }, { step: 'run web-unit', ms: 188_000 }] },
    { name: 'web-types', ms: 190_000 },
    { name: 'cli', ms: 6_000 },
    { name: 'code-map', ms: 0 },
  ];
  const lines = overrunLines(RULE, { wallMs: 1_909_000, gates, reason: null, file: 'local/verify-overruns.jsonl' });
  assert.equal(lines.length, 3);
  assert.equal(lines[0], '  verification: 31.8 min, over this workspace\'s 10 min budget (D349); its scope: scoped to its reach, so the scope was too big for the change or the change is code-wide and should have said so');
  assert.equal(lines[1], '    where its time went: reached 12.6 min (run cli 8 m 18 s, run web-unit 3 m 8 s, plan 1 m 53 s, census 9 s), web-types 3.2 min, universal 1.9 min, cli 0.1 min');
  assert.equal(lines[2], '    filed in local/verify-overruns.jsonl for the steward');
  assert.deepEqual(overrunLines(RULE, { wallMs: 9 * 60_000, gates, reason: null, file: 'x' }), [], 'within the budget nothing is said');
  const full = overrunLines(RULE, { wallMs: 11 * 60_000, gates, reason: { kind: 'wide-path', said: 'daoris.gates.json is a declared wide path' }, file: 'x' });
  assert.match(full[0]!, /its scope: full: daoris\.gates\.json is a declared wide path$/);

  // The record carries the budget, the wall time, the scope and the reason; the finding goes to the file the steward reads.
  const verification = verificationRecord(RULE, { wallMs: 1_909_000, reason: null });
  assert.deepEqual(verification, { budgetMs: 600_000, wallMs: 1_909_000, over: true, scope: 'reach' });
  assert.deepEqual(verificationRecord(RULE, { wallMs: 60_000, reason: { kind: 'wide-path', said: 'x is a declared wide path' } }),
    { budgetMs: 600_000, wallMs: 60_000, over: false, scope: 'full', reason: 'x is a declared wide path', kind: 'wide-path' });
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
