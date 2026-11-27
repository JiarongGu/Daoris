import { mkdirSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { HEAVY_QUIET_MS, bump, changePending, enlist, judgeLanding, look, rangesOf, readPending, type Landing, type Pending } from './heavy-batch.ts';
import { verifyBudgetOf } from './verification.ts';

// test-map: strings name paths as data — the gates file declares the wide paths the judgement reads

const rule = verifyBudgetOf(JSON.stringify({
  verifyBudget: { budgetMinutes: 10, fullRunReasons: { widePaths: ['daoris.gates.json', 'src/Daoris.Contracts/**'], reachShare: 0.5, runs: ['after-landing', 'release'] } },
}));
const noReach = (): never => { throw new Error('the reach is measured only where no wide path decides'); };

const at = (minute: number): string => new Date(Date.UTC(2026, 10, 27, 10, minute)).toISOString();
const landing = (n: number, minute: number): Landing => ({ id: `heavy-${n}`, commit: `c${n}`, base: `c${n - 1}`, branches: [`branch-${n}`], at: at(minute) });

describe('a landing is judged by the change it landed, never by the run that follows it', () => {
  it('queues nothing heavy for a change that bears no reason: no wide path and a reach under the declared share', () => {
    const judged = judgeLanding(rule, ['src/Daoris.Web/src/a.tsx', 'docs/x.md'], () => ({ reached: 12, of: 100, what: 'classes' }));
    expect(judged.run).toBe(false);
    expect(judged.said).toContain('12 of 100 classes');
    expect(judged.said).toContain('no reason');
  });

  it('runs for a declared wide path without measuring the reach, and says which', () => {
    const judged = judgeLanding(rule, ['src/Daoris.Contracts/A.cs', 'docs/x.md'], noReach);
    expect(judged).toMatchObject({ run: true, reason: { kind: 'wide-path' } });
    expect(judged.said).toContain('src/Daoris.Contracts/A.cs is a declared wide path');
  });

  it('runs for a reach at or over the declared share', () => {
    const judged = judgeLanding(rule, ['src/Daoris.Desktop/X.cs'], () => ({ reached: 50, of: 100, what: 'classes' }));
    expect(judged).toMatchObject({ run: true, reason: { kind: 'reach-share' } });
  });

  it('does not count the landing itself as a reason, though the rule lists the run after a landing by its kind', () => {
    expect(rule?.runs).toContain('after-landing');
    expect(judgeLanding(rule, ['docs/x.md'], () => ({ reached: 0, of: 100, what: 'classes' })).run).toBe(false);
  });

  it('runs as it always did where the repository sets no rule', () => {
    expect(judgeLanding(null, ['docs/x.md'], noReach)).toMatchObject({ run: true, reason: null });
  });
});

describe('landings are batched: one tier starts once none has come for the pause', () => {
  it('lets a lone reasoned landing start once the pause has passed since it, and not before', () => {
    const { pending } = enlist(null, landing(1, 0));
    expect(look(pending, 'heavy-1', Date.parse(at(19)), HEAVY_QUIET_MS)).toMatchObject({ role: 'wait' });
    const started = look(pending, 'heavy-1', Date.parse(at(20)), HEAVY_QUIET_MS);
    expect(started.role).toBe('start');
    expect(started.pending).toBeNull();
    expect(started.batch).toMatchObject({ id: 'heavy-1', base: 'c0', commit: 'c1', branches: ['branch-1'] });
  });

  it('carries a newer reasoned landing over the waiting one: the older ends superseded, the newer runs both ranges from the oldest base', () => {
    const first = enlist(null, landing(1, 0)).pending;
    const second = enlist(first, landing(2, 12));
    expect(second.superseded).toEqual({ id: 'heavy-1', commit: 'c1', branches: ['branch-1'] });
    expect(look(second.pending, 'heavy-1', Date.parse(at(40)), HEAVY_QUIET_MS)).toMatchObject({ role: 'superseded', by: { id: 'heavy-2', commit: 'c2' } });
    // The pause counts from the newest landing, not the first.
    expect(look(second.pending, 'heavy-2', Date.parse(at(31)), HEAVY_QUIET_MS).role).toBe('wait');
    const started = look(second.pending, 'heavy-2', Date.parse(at(32)), HEAVY_QUIET_MS);
    expect(started.role).toBe('start');
    expect(started.batch).toMatchObject({
      id: 'heavy-2', commit: 'c2', base: 'c0', branches: ['branch-1', 'branch-2'],
      ranges: [{ base: 'c0', commit: 'c1' }, { base: 'c1', commit: 'c2' }],
      carried: [{ id: 'heavy-1', commit: 'c1', branches: ['branch-1'] }],
    });
  });

  it('moves the pause for a landing that bears no reason, and adds no range of its own', () => {
    const first = enlist(null, landing(1, 0)).pending;
    const bumped = bump(first, at(15));
    expect(bumped?.ranges).toEqual(first.ranges);
    expect(look(bumped, 'heavy-1', Date.parse(at(34)), HEAVY_QUIET_MS).role).toBe('wait');
    expect(look(bumped, 'heavy-1', Date.parse(at(35)), HEAVY_QUIET_MS).role).toBe('start');
    expect(bump(null, at(15))).toBeNull();
  });

  it('never moves the pause back for a landing seen late', () => {
    const first = enlist(null, landing(1, 10)).pending;
    expect(bump(first, at(5))?.last).toBe(at(10));
  });

  it('says gone where the waiting tier finds nothing pending, so it settles an error rather than run unrecorded', () => {
    expect(look(null, 'heavy-1', Date.parse(at(30)), HEAVY_QUIET_MS).role).toBe('gone');
  });

  it('names the ranges a batch runs, so only what the reasoned landings changed is reached', () => {
    const batch = enlist(enlist(null, landing(1, 0)).pending, landing(2, 3)).pending;
    expect(rangesOf(batch)).toEqual([{ base: 'c0', commit: 'c1' }, { base: 'c1', commit: 'c2' }]);
  });
});

describe('the pending record is one file changed under its lock', () => {
  const dir = join('_fixtures', 'heavy-batch-test');
  afterEach(() => rmSync(dir, { recursive: true, force: true }));

  it('reads none where there is no file, and keeps what a change wrote', () => {
    rmSync(dir, { recursive: true, force: true });
    mkdirSync(dir, { recursive: true });
    const file = join(dir, 'local', 'heavy-pending.json');
    expect(readPending(file)).toBeNull();
    const kept: Pending = enlist(null, landing(1, 0)).pending;
    changePending(file, () => ({ pending: kept }));
    expect(readPending(file)).toEqual(kept);
    changePending(file, () => ({ pending: null }));
    expect(readPending(file)).toBeNull();
  });
});
