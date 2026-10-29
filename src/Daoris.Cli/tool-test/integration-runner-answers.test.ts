import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * INTEGRATE1f part f1 (the integration design §5.9; D192's runner contract, extended; D215's notes on INTEGRATE1j): the answers the
 * driver reads, held to what this tool writes. The driver's `RunnerAnswers` reads `fixtures/runner-answers.json` there
 * (`RunnerAnswerTests`); here each piece of an answer is built with the tool's own writers (`verifyGates`, `verifyRunGates`,
 * `verifyPlanAnswer`, `verifyCommitsAnswer`, `verifyUnfinished`) and held to the table's answer, so a field the tool names `composition`,
 * `failures`, `kept`, `deferred` or `probe.result` is the field the driver reads, and one renamed on either side fails the other.
 * Run by the `tool-tests` gate.
 */

// The census of test units reads this file's strings as data, not as reads; what it reads is declared.
// test-map: strings name paths as data
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/runner-answers.json — INTEGRATE1f's driver twin reads the answers row for row

type Json = Record<string, unknown>;
interface Result { gate: { name: string }; verdict: string; log?: string }
interface PlanGate { name: string; declared: string }

const tool = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-queue.test.ts documents
  '../../../tools/merge-branch.mjs') as {
  verifyGates: (plan: PlanGate[], results: Result[], options?: { keep?: (log: string) => string | undefined; seconds?: (name: string) => number | undefined }) => Json[];
  verifyRunGates: (plan: PlanGate[], results: Result[], options: {
    keep?: (path: string) => string | undefined; seconds?: (name: string) => number | undefined;
    compositions: Map<string, { units: string[] | null; digest: string; file: string }>; failures: Map<string, unknown[]>;
  }) => Json[];
  verifyPlanAnswer: (input: { plan: PlanGate[]; named: string[] }) => Json;
  verifyCommitsAnswer: (input: { commit: string; base: string; declaration: string; commits: unknown[] }) => Json;
  verifyUnfinished: (mode: string | null, input: { commit: string; base: string; declaration: string; why: string; plan: PlanGate[]; results: Result[]; kept: unknown[]; deferred: unknown[] }) => Json;
};

const TABLE = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/runner-answers.json'), 'utf8')) as {
  ids: { tip: string; base: string; declaration: string };
  answers: Record<string, Json & { gates?: Json[]; run?: Json[]; kept?: Json[]; deferred?: Json[] }>;
  written: {
    plan: PlanGate[]; schema1Results: Result[]; schema1Seconds: Record<string, number>; runPlan: PlanGate[]; runResults: Result[]; runSeconds: Record<string, number>;
    runCompositions: Record<string, { units: string[] | null; digest: string; file: string }>; unfinished: { why: string; answer: Json };
  };
  reads: unknown[]; standings: unknown[]; promotions: unknown[];
};

describe('INTEGRATE1f: the runner answers the driver reads are the ones this tool writes', () => {
  const { ids, answers, written } = TABLE;

  test("D192's schema-1 gates: the declared name, the verdict as D192 names it, the wall and the log", () => {
    const gates = tool.verifyGates(written.plan, written.schema1Results, { seconds: (name) => written.schema1Seconds[name] });
    assert.deepEqual(gates, answers.schema1!.gates);
  });

  test("a run's gates are schema 1's with each gate's composition (the units, where its settings stay, the digest) and its failures", () => {
    const gates = tool.verifyRunGates(written.runPlan, written.runResults, {
      seconds: (name) => written.runSeconds[name],
      compositions: new Map(Object.entries(written.runCompositions)),
      failures: new Map(),
    });
    assert.deepEqual(gates, answers.run!.gates);
  });

  test('a plan with gates named runs each whole and keeps and defers none', () => {
    const planned = tool.verifyPlanAnswer({ plan: written.runPlan, named: ['driver', 'web'] });
    assert.deepEqual(planned, { run: answers.planNamed!.run, kept: answers.planNamed!.kept, deferred: answers.planNamed!.deferred });
  });

  test('the commit check answers in schema 2 with its problems', () => {
    const answer = tool.verifyCommitsAnswer({ commit: ids.tip, base: ids.base, declaration: ids.declaration, commits: [] });
    assert.deepEqual(answer, answers.commits);
  });

  test("a run that did not finish answers in its mode's shape, with the words it did not finish by, every gate not-run", () => {
    const answer = tool.verifyUnfinished('run', { commit: ids.tip, base: ids.base, declaration: ids.declaration, why: written.unfinished.why, plan: written.runPlan, results: [], kept: [], deferred: [] });
    assert.deepEqual(answer, written.unfinished.answer);
  });

  test("the envelope's names are the ones the table's answers carry: commit, base and declaration beside the mode, and a probe's result", () => {
    for (const name of ['run', 'plan', 'planNamed', 'probe', 'commits']) {
      const answer = answers[name]!;
      assert.equal(answer.schema, 2, name);
      assert.deepEqual([answer.commit, answer.base, answer.declaration], [ids.tip, ids.base, ids.declaration], name);
    }
    assert.equal(answers.probe!.result, 'fail');
    assert.ok(TABLE.reads.length >= 40 && TABLE.standings.length >= 10 && TABLE.promotions.length >= 12, 'the table holds its rows');
  });
});
