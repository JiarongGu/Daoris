import { describe, expect, it } from 'vitest';
import { stewardReverse, stewardSetAt, type StewardActNow } from './standing';

// The steward's acts as the page reads them (STEWARDROLE1b, D209 §5.1, §5.4): which repository's driving the steward set, from this
// run's memory, and the owner's reverse press each standing act has.

const act = (id: string, kind: string, at: string, more: Partial<StewardActNow> = {}): StewardActNow => ({
  id, act: kind, repository: null, quest: null, session: null, at, standing: true, waiting: false, ...more,
});

describe("the steward's acts", () => {
  it("names the newest standing driving act on a repository, matched without case, and none for a session's act", () => {
    const acts = [
      act('a4', 'stop', '2026-10-25T10:04:00Z', { repository: 'engine', quest: 'q1', session: 's1' }),
      act('a3', 'hold', '2026-10-25T10:03:00Z', { repository: 'Engine', standing: false }),
      act('a2', 'trees', '2026-10-25T10:02:00Z', { repository: 'engine' }),
      act('a1', 'drive', '2026-10-25T10:01:00Z', { repository: 'engine' }),
    ];
    expect(stewardSetAt(acts, 'ENGINE')).toBe('2026-10-25T10:02:00Z');
    expect(stewardSetAt(acts, 'atlas')).toBeNull();
    expect(stewardSetAt([acts[0]!, acts[1]!], 'engine')).toBeNull();
  });

  it("answers the owner's ordinary press for each standing act, and none for a finish, a Try again or one that no longer stands", () => {
    expect(stewardReverse(act('a', 'drive', '', { repository: 'engine' }))).toBe('stopDriving');
    expect(stewardReverse(act('a', 'undrive', '', { repository: 'engine' }))).toBe('drive');
    expect(stewardReverse(act('a', 'trees', '', { repository: 'engine' }))).toBe('treesOff');
    expect(stewardReverse(act('a', 'hold', '', { repository: 'engine' }))).toBe('resume');
    expect(stewardReverse(act('a', 'resume', '', { repository: 'engine' }))).toBe('hold');
    expect(stewardReverse(act('a', 'stop', '', { quest: 'q1', session: 's1' }))).toBe('retry');
    expect(stewardReverse(act('a', 'finish', '', { quest: 'q1', session: 's1' }))).toBeNull();
    expect(stewardReverse(act('a', 'retry', '', { quest: 'q1' }))).toBeNull();
    expect(stewardReverse(act('a', 'drive', '', { repository: 'engine', standing: false }))).toBeNull();
    // An act naming nothing its press could carry offers none.
    expect(stewardReverse(act('a', 'drive', ''))).toBeNull();
    expect(stewardReverse(act('a', 'stop', '', { session: 's1' }))).toBeNull();
  });
});
