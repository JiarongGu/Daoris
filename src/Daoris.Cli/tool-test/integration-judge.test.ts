import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * INTEGRATE1f part f1 (D207, the integration design §4.5, §5.3, §11 row f): how a red candidate's failures are judged, held to ONE
 * table with the driver's twin. `tools/merge-queue.mjs` (`placeOnPrefixes`, `judgeFailure`, `candidateOutcome`, `confirmStep`,
 * `confirmReadings`, `memoryStep`, `recheckDue`, `noteRecheck`) and `tools/ci-background.mjs` (`knownRedStanding`) read
 * `fixtures/failure-judging.json` here, row for row, and the driver's `FailureJudge` reads it there (`FailureJudgeTests`), so the
 * integration step Daoris runs gives the verdict the merge queue gives on the same runs. The table keeps the rule that a sighting is
 * never a pass: `unconfirmed` is never `green`, never a flake and never stored as a pass, and `inconclusive` reads as split under every
 * policy. Run by the `tool-tests` gate.
 */

// The census of test units reads this file's strings as data, not as reads; what it reads is declared.
// test-map: strings name paths as data
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/failure-judging.json — INTEGRATE1f's driver twin reads the table row for row

interface Signature { test?: string; gate?: string }
interface Failure { unit?: string; test?: string; gate: string; signature: Signature | null; names: 'test' | 'gate' }
type Outcome = { result: 'pass' } | { result: 'fail' | 'cannot'; signature: Signature };
interface Confirm { result?: string; why?: string; evidence?: unknown; failures?: Failure[]; composition?: { units: string[] | null; digest: string | null } }
interface Judged { outcome: string; member?: number; with?: number[]; interacting?: number[]; why?: string; said?: string; runs: Record<string, unknown>[]; confirm?: Confirm }
interface Member { branch: string; tip: string; handedBack?: { how: string; failure: string; with?: string[]; interacting?: string[]; why?: string } }
interface Outcomes {
  action: string; on?: string; why?: string; said?: string; failure?: Failure; passedOnConfirm?: string[];
  inherited?: Failure[]; flakes?: Failure[]; sightings?: { failure: Failure }[]; inconclusive?: Failure[]; handedBack?: Member[]; retain?: Member[];
}
interface Evidence { conclusive: boolean; doubts: { at: string; why: string }[]; waitedMs?: number; ids?: string[] }
type Whole = { held: true; end: () => Evidence } | { held: false; dropped: boolean; why?: string; evidence: Evidence };

const q = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-queue.test.ts documents
  '../../../tools/merge-queue.mjs') as {
  placeOnPrefixes: (seq: Outcome[], signature: Signature) => { outcome: string; member?: number; interacting?: number[]; monotonic?: boolean; why?: string };
  judgeFailure: (failure: Failure, context: Record<string, unknown>) => Promise<Judged>;
  candidateOutcome: (judgements: { failure: Failure; judged: Judged }[], members: Member[], policy?: { unconfirmed?: string }) => Outcomes;
  confirmStep: (input: { take: (request: { what: string; halted: () => boolean }) => Promise<Whole>; run: () => Promise<unknown>; halted: () => boolean }) => Promise<{
    result: string; why: string; composition: unknown; failures?: Failure[]; error?: Error }>;
  confirmReadings: (confirm: Confirm | null, failures: Failure[]) => { failure: Failure; confirm: { result: string; why: string } }[];
  memoryStep: (attempts: { died: boolean; peakGb?: number }[], options?: { afterHold?: boolean; needGb?: number | null }) => { next: string; note?: string };
  recheckDue: (stop: { at: string; rechecks?: { at: string }[] } | null, now: number) => boolean;
  noteRecheck: (stop: { at: string }, recheck: { at: string; passed: boolean; by: string }) => unknown;
};
const bg = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../tools/ci-background.mjs') as { knownRedStanding: (finding: { since: string }, now: number) => { expired: boolean; warn: boolean } };
const failures = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../tools/test-failures.mjs') as { signatureKey: (signature: Signature) => string };

const TABLE = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/failure-judging.json'), 'utf8')) as {
  signatures: Record<string, Signature>;
  placements: { name: string; seq: string[]; signature: string; expect: Record<string, unknown> }[];
  judgements: {
    name: string; failure: string; members: number; prefixes: string[]; second?: Record<string, string>; alone?: string; known?: { expired: boolean; lastSeen: string };
    baseCommit?: string; confirm?: Confirm; expect: Record<string, unknown>;
  }[];
  outcomes: { name: string; judgements: { failure: string; judged: Omit<Judged, 'runs'> }[]; members: number; policies: (string | null)[]; expect: Record<string, unknown> }[];
  readings: { name: string; confirm: { result: string; why?: string; failures?: string[] } | null; failures: string[]; expect: unknown[] }[];
  steps: {
    name: string; take: { held: boolean; conclusive?: boolean; doubt?: string; dropped?: boolean; why?: string }; halted: 'never' | 'before' | 'afterRun';
    run: { result?: string; why?: string; failures?: (string | null)[]; composition?: unknown; throws?: string }; expect: Record<string, unknown>;
  }[];
  memory: { name: string; attempts: { died: boolean; peakGb?: number }[]; afterHold: boolean; needGb: number | null; expect: { next: string; note: string | null } }[];
  rechecks: {
    due: { name: string; stop: { at: string; rechecks?: { at: string }[] } | null; now: string; due: boolean }[];
    notes: { name: string; stop: { at: string }; recheck: { at: string; passed: boolean; by: string }; expect: unknown }[];
  };
  standing: { name: string; since: string; now: string; expired: boolean; warn: boolean }[];
};

const SIGS = TABLE.signatures;
const nameOf = (signature: Signature | null) => Object.entries(SIGS).find(([, each]) => failures.signatureKey(each) === failures.signatureKey(signature as Signature))?.[0] ?? null;
const failureOf = (key: string): Failure => {
  const signature = SIGS[key]!;
  return signature.gate !== undefined
    ? { gate: signature.gate, signature, names: 'gate' }
    : { unit: 'SessionTests', test: signature.test, gate: 'driver', signature, names: 'test' };
};
const label = (failure: Failure) => failure.test ?? failure.gate;
const members = (n: number): Member[] => Array.from({ length: n }, (_, i) => ({ branch: `b${i + 1}`, tip: `t${i + 1}`.padEnd(40, '0') }));

/** A prefix answer as the table spells it: `pass`, `fail:<signature>` or `cannot:<signature>`. */
const answer = (text: string): Outcome => {
  if (text === 'pass') return { result: 'pass' };
  const [result, key] = text.split(':') as ['fail' | 'cannot', string];
  return { result, signature: SIGS[key]! };
};

/** A run as the table spells it: `<prefix>:<answer>` or `alone<member>:<answer>`. */
const runText = (run: Record<string, unknown>) =>
  `${run.alone !== undefined ? `alone${run.alone}` : run.prefix}:${run.result}${run.signature ? `:${nameOf(run.signature as Signature)}` : ''}`;

describe('INTEGRATE1f: the judge, row for row with the driver', () => {
  for (const row of TABLE.placements) {
    test(`a placement on the prefixes: ${row.name}`, () => {
      const placed = q.placeOnPrefixes(row.seq.map(answer), SIGS[row.signature]!);
      assert.deepEqual({
        outcome: placed.outcome, member: placed.member ?? null, interacting: placed.interacting ?? [], monotonic: placed.monotonic ?? null, why: placed.why ?? null,
      }, row.expect);
    });
  }

  for (const row of TABLE.judgements) {
    test(`a failure judged: ${row.name}`, async () => {
      const asked: number[] = [];
      const seen = new Map<number, number>();
      let confirmAsked = 0;
      const judged = await q.judgeFailure(failureOf(row.failure), {
        members: row.members,
        run: async (_failure: Failure, k: number) => {
          asked.push(k);
          const times = (seen.get(k) ?? 0) + 1;
          seen.set(k, times);
          const text = times > 1 && row.second?.[String(k)] ? row.second[String(k)]! : row.prefixes[k];
          assert.ok(text !== undefined, `${row.name}: the prefix ${k} was run, and the table gives it no answer`);
          return answer(text);
        },
        alone: row.alone ? async () => answer(row.alone!) : null,
        known: row.known ?? null,
        baseCommit: row.baseCommit ?? null,
        confirm: row.confirm ? () => { confirmAsked += 1; return row.confirm; } : null,
      });
      assert.deepEqual({
        outcome: judged.outcome, member: judged.member ?? null, with: judged.with ?? [], interacting: judged.interacting ?? [], why: judged.why ?? null, said: judged.said ?? null,
        runs: judged.runs.map(runText), confirmAsked,
      }, row.expect);
    });
  }

  for (const row of TABLE.outcomes) {
    for (const policy of row.policies) {
      test(`a candidate's outcome (${policy ?? 'no policy'}): ${row.name}`, () => {
        const judgements = row.judgements.map((each) => ({ failure: failureOf(each.failure), judged: { runs: [], confirm: { result: each.judged.outcome, evidence: null }, ...each.judged } as Judged }));
        const outcome = q.candidateOutcome(judgements, members(row.members), policy === null ? undefined : { unconfirmed: policy });
        const branches = (list?: Member[]) => (list ?? []).map((each) => each.branch);
        const labels = (list?: Failure[]) => (list ?? []).map(label);
        const projected: Record<string, unknown> = outcome.action === 'stop'
          ? { action: 'stop', failure: label(outcome.failure!), why: outcome.why, said: outcome.said }
          : {
            action: outcome.action, retain: branches(outcome.retain),
            handedBack: (outcome.handedBack ?? []).map((each) => ({
              branch: each.branch, how: each.handedBack!.how, failure: each.handedBack!.failure, with: each.handedBack!.with ?? [], interacting: each.handedBack!.interacting ?? [],
              why: each.handedBack!.how === 'unresolved' ? each.handedBack!.why : null,
            })),
            inherited: labels(outcome.inherited), flakes: labels(outcome.flakes), sightings: (outcome.sightings ?? []).map((each) => label(each.failure)), inconclusive: labels(outcome.inconclusive),
            ...(outcome.action === 'hold' ? { on: outcome.on, why: outcome.why } : {}),
            ...(outcome.action === 'land' ? { passedOnConfirm: outcome.passedOnConfirm } : {}),
          };
        assert.deepEqual(projected, row.expect);
        assert.notEqual(outcome.action === 'green' && row.judgements.some((each) => each.judged.outcome === 'unconfirmed' || each.judged.outcome === 'inconclusive'), true, 'a sighting is never green');
      });
    }
  }

  for (const row of TABLE.readings) {
    test(`a gate's failures read by its confirm run: ${row.name}`, () => {
      const confirm = row.confirm === null ? null : { ...row.confirm, failures: row.confirm.failures?.map(failureOf) };
      const read = q.confirmReadings(confirm, row.failures.map(failureOf));
      assert.deepEqual(read.map((each) => ({ failure: label(each.failure), result: each.confirm.result, why: each.confirm.why })), row.expect);
    });
  }

  for (const row of TABLE.steps) {
    test(`a confirm run: ${row.name}`, async () => {
      let taken = false;
      let ran = false;
      let ended = false;
      const take = async (): Promise<Whole> => {
        taken = true;
        if (!row.take.held) return { held: false, dropped: Boolean(row.take.dropped), ...(row.take.why ? { why: row.take.why } : {}), evidence: { conclusive: false, doubts: [] } };
        return {
          held: true,
          end: () => {
            ended = true;
            return { conclusive: Boolean(row.take.conclusive), doubts: row.take.doubt ? [{ at: 'beat', why: row.take.doubt }] : [], waitedMs: 1, ids: ['p-1'] };
          },
        };
      };
      const run = async () => {
        ran = true;
        if (row.run.throws) throw new Error(row.run.throws);
        const { failures: given, ...rest } = row.run;
        return { ...rest, ...(given ? { failures: given.map((key) => (key === null ? { gate: 'driver', test: 'no signature', names: 'test' } : failureOf(key))) } : {}) };
      };
      const halted = () => row.halted === 'before' || (row.halted === 'afterRun' && ran);
      if (row.expect.thrown) {
        await assert.rejects(q.confirmStep({ take, run, halted }), new Error(row.expect.thrown as string));
        assert.deepEqual({ taken, ran, ended }, { taken: row.expect.taken, ran: row.expect.ran, ended: row.expect.ended });
        return;
      }
      const confirm = await q.confirmStep({ take, run, halted });
      assert.deepEqual({
        result: confirm.result, why: confirm.why, taken, ran, ended, composition: confirm.composition ?? null, failures: (confirm.failures ?? []).map(label),
        thrown: null, error: confirm.error?.message ?? null,
      }, row.expect);
    });
  }

  for (const row of TABLE.memory) {
    test(`a death of memory: ${row.name}`, () => {
      const step = q.memoryStep(row.attempts, { afterHold: row.afterHold, needGb: row.needGb });
      assert.deepEqual({ next: step.next, note: step.note ?? null }, row.expect);
    });
  }

  for (const row of TABLE.rechecks.due) {
    test(`a stopped queue's recheck: ${row.name}`, () => {
      assert.equal(q.recheckDue(row.stop, Date.parse(row.now)), row.due);
    });
  }

  for (const row of TABLE.rechecks.notes) {
    test(`a stop rechecked: ${row.name}`, () => {
      assert.deepEqual(q.noteRecheck(row.stop, row.recheck), row.expect);
    });
  }

  for (const row of TABLE.standing) {
    test(`a known red's standing: ${row.name}`, () => {
      const standing = bg.knownRedStanding({ since: row.since }, Date.parse(row.now));
      assert.deepEqual({ expired: standing.expired, warn: standing.warn }, { expired: row.expired, warn: row.warn });
    });
  }

  test('the table holds its rows, the known-red expiry among them', () => {
    assert.ok(TABLE.judgements.length >= 25 && TABLE.outcomes.length >= 20 && TABLE.steps.length >= 14);
    assert.ok(TABLE.judgements.some((row) => row.known?.expired && row.expect.outcome === 'stop'), 'the 72-hour branch that stops the line');
    assert.ok(TABLE.judgements.some((row) => row.confirm?.result === 'unconfirmed'), 'a sighting');
  });
});
