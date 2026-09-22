import { describe, expect, it } from 'vitest';
import type { Session } from '../api';
import { ENDED_SHOWN, partition } from './rail';

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const session = (over: Partial<Session> & { id: string; state: Session['state'] }): Session => ({
  repository: 'engine', adapter: 'claude-code', created: at(60), updated: at(1), ...over,
});

/**
 * The rail's two lists, after the deployed application showed four empty-state sentences on a
 * machine with four real session records (D62; working-surface design §7).
 */
describe('partition', () => {
  /** 🔴 The defect in one assertion: an ended session is somewhere, not nowhere. */
  it('lists an ended session beneath the live ones rather than dropping it', () => {
    const { active, ended } = partition([
      session({ id: 'live', state: 'working' }),
      session({ id: 'gone', state: 'failed' }),
    ], null);

    expect(active.map((s) => s.id)).toEqual(['live']);
    expect(ended.map((s) => s.id)).toEqual(['gone']);
  });

  it('keeps every live state live', () => {
    const { active, ended } = partition([
      session({ id: 'q', state: 'queued' }),
      session({ id: 's', state: 'starting' }),
      session({ id: 'w', state: 'working' }),
      session({ id: 'a', state: 'awaiting-person' }),
    ], null);

    expect(active).toHaveLength(4);
    expect(ended).toHaveLength(0);
  });

  /**
   * The rule that was already here, kept exactly: a session that ends while you are reading it
   * must not jump down the rail out from under you.
   */
  it('keeps the attended session in the live list whatever state it reached', () => {
    const { active, ended } = partition([session({ id: 'read', state: 'completed' })], 'read');

    expect(active.map((s) => s.id)).toEqual(['read']);
    expect(ended).toHaveLength(0);
  });

  it('orders ended sessions newest first — the reading order for a record', () => {
    const { ended } = partition([
      session({ id: 'older', state: 'failed', updated: at(300) }),
      session({ id: 'newest', state: 'stopped', updated: at(2) }),
      session({ id: 'middle', state: 'completed', updated: at(40) }),
    ], null);

    expect(ended.map((s) => s.id)).toEqual(['newest', 'middle', 'older']);
  });

  /** Counted, never silently cut — the same promise every capped list here makes. */
  it('caps the ended list and counts what it did not show', () => {
    const many = Array.from({ length: ENDED_SHOWN + 5 }, (_, i) =>
      session({ id: `e${i}`, state: 'failed', updated: at(i) }));
    const { ended, hiddenEnded } = partition(many, null);

    expect(ended).toHaveLength(ENDED_SHOWN);
    expect(hiddenEnded).toBe(5);
    // The newest survive the cap, not the first given.
    expect(ended[0]!.id).toBe('e0');
  });

  it('survives nothing at all, and a limit of nothing', () => {
    expect(partition([], null)).toEqual({ active: [], ended: [], hiddenEnded: 0 });
    const { ended, hiddenEnded } = partition([session({ id: 'x', state: 'failed' })], null, 0);
    expect(ended).toEqual([]);
    expect(hiddenEnded).toBe(1);
  });
});
