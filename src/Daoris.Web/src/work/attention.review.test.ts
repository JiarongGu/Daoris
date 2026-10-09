import { describe, expect, it } from 'vitest';
import '../i18n';
import { attentionActs, needsAPerson } from './attention';
import { STEP_DEPLOYED, STEP_NOT_YET, STEP_REVIEWED, STEP_SETTING_UP, STEP_SHOWN, STEP_SHOWN_AGAIN, WORK } from './reviewFixtures';

/**
 * REVIEWENV1g (D154 point 8; the review environment design §3.3): *What needs you* lists a set-up step whose newest set-up
 * waits for the person's look, the set-up named as the row drew it, and never as a departure, since no yes lifts a review.
 */
describe('a set-up in what needs you', () => {
  it('lists a set-up shown and waiting, with what it showed and the set-up its verdict names', () => {
    const [row, ...rest] = needsAPerson([], [WORK, STEP_SHOWN], [], []);

    expect(rest).toEqual([]);
    expect(row).toMatchObject({
      id: 'q2', kind: 'set-up', where: 'reports', since: STEP_SHOWN.setUps![0]!.at,
      setUp: { machine: 'desk', sequence: 7 }, local: true, session: 's9',
    });
    expect(row!.detail).toBe('Shown in `local`: The report with the compare setting turned on, showing last month beside this one.');
    expect(attentionActs(row!)).toEqual(['reviewed', 'not-yet', 'show-again']);
  });

  it('names the newest set-up once a step showed it again', () => {
    const [row] = needsAPerson([], [STEP_SHOWN_AGAIN], [], []);

    expect(row!.setUp).toEqual({ machine: 'desk', sequence: 9 });
  });

  it('lists nothing while it is set up, once said not yet, or once reviewed', () => {
    for (const quest of [STEP_SETTING_UP, STEP_NOT_YET, STEP_REVIEWED]) expect(needsAPerson([], [quest], [], [])).toEqual([]);
  });

  it('offers no Show it again for a deployed set-up, which Daoris does not serve', () => {
    const [row] = needsAPerson([], [STEP_DEPLOYED], [], []);

    expect(attentionActs(row!)).toEqual(['reviewed', 'not-yet']);
  });
});
