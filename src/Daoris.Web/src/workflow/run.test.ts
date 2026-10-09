import { describe, expect, it } from 'vitest';
import { knownState, runSessionOf, runTone, standsAt, waitsOnYou } from './run';
import { FAILED, IN_REVIEW, LANDED, OPINION_DECLARED, PULL_REQUEST_OPEN, WORKING } from './runFixtures';

// WORKFLOW1c (the workflow design §5.2, §7): what the page reads of a run the driver answered: the step it stands at, the hue a
// state wears, and the session a door into it attends. The driver derives every state; nothing here does.

describe('a run as the page reads it', () => {
  it('stands at the step the driver named, or nowhere once it finished', () => {
    expect(standsAt(WORKING)?.step.id).toBe('work');
    expect(standsAt(IN_REVIEW)?.step.id).toBe('look');
    expect(standsAt(PULL_REQUEST_OPEN)?.step.id).toBe('pull-request');
    // A declared opinion holds nothing, so the run stands past it.
    expect(standsAt(OPINION_DECLARED)?.step.id).toBe('landing');
    expect(standsAt(LANDED)).toBeNull();
  });

  it('waits on the person only where the step it stands at does', () => {
    expect(waitsOnYou(IN_REVIEW)).toBe(true);
    expect(waitsOnYou(PULL_REQUEST_OPEN)).toBe(true);
    expect(waitsOnYou(WORKING)).toBe(false);
    expect(waitsOnYou(FAILED)).toBe(false);
    expect(waitsOnYou(LANDED)).toBe(false);
  });

  it("wears open's hue for the person's wait, green for done, red only for a failure, and no hue for any other", () => {
    expect(runTone('waiting-on-you')).toBe('open');
    expect(runTone('done')).toBe('done');
    expect(runTone('failed')).toBe('declined');
    for (const state of ['working', 'waiting-on-agent', 'not-known', 'cannot-start', 'skipped', 'stopped', 'declared', 'not-reached', 'later']) {
      expect(runTone(state)).toBe('neutral');
    }
  });

  it('knows the states the driver spells, and keeps a newer one as recorded', () => {
    expect(knownState('waiting-on-agent')).toBe(true);
    expect(knownState('waiting-on-service')).toBe(false);
  });

  it("attends the session a row names, else its quest's newest here, else none", () => {
    const sessions = [
      { id: 's1', quest: 'q1', created: '2026-10-09T09:00:00Z' },
      { id: 's2', quest: 'q1', created: '2026-10-09T10:00:00Z' },
      { id: 's3', quest: 'q2', created: '2026-10-09T08:00:00Z' },
    ];
    expect(runSessionOf({ session: 's3', quest: 'q1' }, sessions)).toBe('s3');
    expect(runSessionOf({ quest: 'q1' }, sessions)).toBe('s2');
    expect(runSessionOf({ quest: 'q9' }, sessions)).toBeNull();
    expect(runSessionOf({}, sessions)).toBeNull();
  });
});
