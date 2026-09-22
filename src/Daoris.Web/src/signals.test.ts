import { describe, expect, it } from 'vitest';
import { type Consideration, capped, newsFrom, sittingBecause } from './signals';

/**
 * The driver's tick lines, as notices. Written from four identical toasts stacked on the deployed
 * application — one repository, one reason, one per tick, forever.
 */
describe('newsFrom', () => {
  const HELD = 'held  #7786da → testbed-core: `…` has never been trusted by this harness';

  /** 🔴 The defect, in one assertion. */
  it('says nothing about a hold that has not changed', () => {
    expect(newsFrom([HELD], [HELD])).toEqual([]);
  });

  it('tells the person the first time', () => {
    expect(newsFrom([], [HELD])).toEqual([HELD]);
  });

  /**
   * News again, and deliberately. The state it reports went away and came back — a quest that was
   * held, ran, and is held again is a second thing that happened, not a repeat of the first.
   */
  it('tells them again when it stopped and came back', () => {
    expect(newsFrom([HELD], ['engine  spawned s1a2b3c4'])).toEqual(['engine  spawned s1a2b3c4']);
    expect(newsFrom(['engine  spawned s1a2b3c4'], [HELD])).toEqual([HELD]);
  });

  it('lets genuinely different lines all through', () => {
    const tick = ['engine  spawned s1', 'game  spawned s2', 'sync  fed 2'];
    expect(newsFrom([], tick)).toEqual(tick);
  });

  /** A tick that repeats itself within one report is still one piece of news. */
  it('does not say the same thing twice in one tick', () => {
    expect(newsFrom([], [HELD, HELD])).toEqual([HELD]);
  });

  it('carries only what changed when a tick says several things', () => {
    const before = [HELD, 'sync  fed 2'];
    const now = [HELD, 'engine  spawned s3'];
    expect(newsFrom(before, now)).toEqual(['engine  spawned s3']);
  });

  it('is empty for an empty tick, and never mutates what it was given', () => {
    const before = [HELD];
    expect(newsFrom(before, [])).toEqual([]);
    expect(before).toEqual([HELD]);
  });
});

/**
 * Why a quest is sitting, in the driver's own words. The driver has said this every tick since D46
 * ("sitting must always say why"); the page dropped it, and the Overview asked "is anything
 * sitting" without ever answering "why" (deployed application, 2026-09-23).
 */
describe('sittingBecause', () => {
  const considered: Consideration[] = [
    { quest: '7786da', repository: 'testbed-core', verdict: 'Held', reason: 'the person paused testbed-core' },
    { quest: 'abc123', repository: 'engine', verdict: 'Start', reason: 'starting' },
    { quest: 'def456', repository: 'game', verdict: 'NotDrivable', reason: 'game is not drivable on this machine' },
  ];

  it('answers with the consideration for a quest the driver is not starting', () => {
    expect(sittingBecause(considered, '7786da')?.reason).toBe('the person paused testbed-core');
    expect(sittingBecause(considered, 'def456')?.verdict).toBe('NotDrivable');
  });

  /** A quest the driver is starting is not sitting — saying "starting" under it would be noise. */
  it('answers nothing for a quest the driver is starting', () => {
    expect(sittingBecause(considered, 'abc123')).toBeNull();
  });

  it('answers nothing for a quest the driver has not spoken about', () => {
    expect(sittingBecause(considered, 'nobody')).toBeNull();
    expect(sittingBecause([], '7786da')).toBeNull();
  });
});

describe('capped', () => {
  it('keeps the newest, because the newest is what a corner can promise to show', () => {
    expect(capped([1, 2, 3, 4, 5], 3)).toEqual([3, 4, 5]);
  });

  it('leaves a short list whole', () => {
    expect(capped([1, 2], 3)).toEqual([1, 2]);
    expect(capped([], 3)).toEqual([]);
  });

  it('survives a cap of nothing', () => {
    expect(capped([1, 2, 3], 0)).toEqual([]);
    expect(capped([1, 2, 3], -1)).toEqual([]);
  });
});
