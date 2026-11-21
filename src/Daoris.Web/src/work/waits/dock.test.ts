import { describe, expect, it } from 'vitest';
import type { Session } from '../../data/api';
import { driverSource } from '../../test/driverSource';
import { DOCK_CENSUS, DOCKED_REASONS, dockOf, stepReason } from './dock';

// The dock's census and order: every kind it holds has the reason the driver's reader gives it, every step wait the reader reads
// lands on a reason the state line knows is docked, and the dock is oldest first.

const constants = (source: string) => [...source.matchAll(/public const string \w+ = "([^"]+)"/g)].map((match) => match[1]!);

describe('the dock census', () => {
  it('holds a reader reason for each dock kind', () => {
    expect(Object.keys(DOCK_CENSUS).sort()).toEqual([
      'accept', 'answerAlone', 'discard', 'gitAsk', 'goAhead', 'handOver', 'helper', 'intake', 'opinion', 'permission', 'question', 'rebase',
      'review', 'rule', 'step',
    ]);
    // A kind the reader reads is placed by a reason the driver's own source names.
    const named = new Set([...constants(driverSource('SessionReason.cs')), ...constants(driverSource('PersonWaits.cs')), 'review']);
    for (const [kind, { reason, read }] of Object.entries(DOCK_CENSUS)) {
      if (read) expect(named.has(reason), `${kind} → ${reason}`).toBe(true);
    }
  });

  it('places every step wait the reader reads on a reason the state line counts as docked', () => {
    const codes = constants(driverSource('PersonWaits.cs'));
    expect(codes).toContain('accept');
    for (const code of codes) expect(DOCKED_REASONS.has(stepReason([code])), code).toBe(true);
    expect(stepReason(['accept', 'left'])).toBe('review');
    expect(stepReason(['left', 'held'])).toBe('held');
  });
});

describe('dockOf', () => {
  const session: Session = {
    id: 's1', quest: 'q1', repository: 'engine', door: 'claude-code', state: 'completed', kind: 'driven',
    created: '2026-11-18T08:00:00Z', updated: '2026-11-18T09:30:00Z',
  };

  it('holds the run\'s steps beside the page\'s waits, oldest first, each once', () => {
    const lines = dockOf(
      {
        session,
        permissions: {
          onAnswer: () => {},
          waits: [{ session: 's1', request: 'p1', since: '2026-11-18T09:50:00Z', title: 'Bash', options: [] } as never],
        },
      },
      [
        { run: 'r1', node: 'accept', why: 'accept', role: 'reviewer', since: '2026-11-18T09:10:00Z' },
        { run: 'r1', node: 'accept', why: 'accept', role: 'reviewer', since: '2026-11-18T09:10:00Z' },
        { run: 'r1', node: 'deploy', why: 'left', since: null },
      ],
    );
    expect(lines.map((line) => line.key)).toEqual(['step:r1/accept', 'step:r1/deploy', 'permission:p1']);
    expect(lines[1]!.since).toBe(session.updated);
  });

  it('tags a wait a subagent raised with its subagent, and the session\'s own agent\'s with none', () => {
    const held = (request: string, child?: string | null) =>
      ({ session: 's1', request, call: null, title: 'Bash', kind: null, choices: [], since: '2026-11-18T09:50:00Z', until: null, child });
    const lines = dockOf({ session, permissions: { onAnswer: () => {}, waits: [held('p1', 'child-7'), held('p2', null), held('p3')] } });
    expect(lines.map((line) => [line.key, line.child])).toEqual([
      ['permission:p1', 'child-7'], ['permission:p2', undefined], ['permission:p3', undefined],
    ]);
  });
});
