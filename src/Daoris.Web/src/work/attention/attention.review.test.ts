import { describe, expect, it } from 'vitest';
import '../../locales/i18n';
import { attentionActs, attentionWorded, type PushedAttention } from './attention';
import { SET_UP, STEP_SHOWN } from '../gates/reviewFixtures';

/**
 * REVIEWENV1g (D154 point 8; the review environment design §3.3): *What needs you* lists a set-up step whose newest set-up
 * waits for the person's look, the set-up named as the row drew it. Which steps wait is .NET's (`AttentionStateSliceTests`' set-up
 * cases); the page words the row and offers the review's presses.
 */
describe('a set-up in what needs you', () => {
  const shown = (over: Partial<PushedAttention> = {}): PushedAttention => ({
    kind: 'set-up', id: 'q2', since: SET_UP.at ?? null, workspace: 'aurora', where: 'reports', setUp: { machine: 'desk', sequence: 7 },
    local: true, session: 's9', environment: 'local', shows: SET_UP.shows ?? null, ...over,
  });

  it('says what it showed, names the set-up its verdict names, and offers the review’s presses', () => {
    const row = attentionWorded(shown(), { quests: [STEP_SHOWN] });

    expect(row).toMatchObject({
      id: 'q2', kind: 'set-up', where: 'reports', since: SET_UP.at, title: STEP_SHOWN.title,
      setUp: { machine: 'desk', sequence: 7 }, local: true, session: 's9',
    });
    expect(row.detail).toBe('Shown in `local`: The report with the compare setting turned on, showing last month beside this one.');
    expect(attentionActs(row)).toEqual(['reviewed', 'not-yet', 'show-again']);
  });

  it('says it showed nothing it named where its set-up said nothing', () => {
    expect(attentionWorded(shown({ shows: null })).detail).toBe('Shown in `local`; it said nothing of what it showed.');
  });

  it('offers no Show it again for a deployed set-up, which Daoris does not serve', () => {
    expect(attentionActs(attentionWorded(shown({ local: false, environment: 'dev' })))).toEqual(['reviewed', 'not-yet']);
  });
});
