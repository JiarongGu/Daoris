import { describe, expect, it } from 'vitest';
import {
  newestSetUp, reviewPresses, reviewState, setUpRef, skipOn, stepState, verdictOn, waitsForLook,
} from './review';
import {
  SET_UP, SET_UP_AGAIN, STEP_DEPLOYED, STEP_NOT_YET, STEP_REVIEWED, STEP_SETTING_UP, STEP_SHOWN, STEP_SHOWN_AGAIN, STEP_SKIPPED,
  WORK,
} from './reviewFixtures';

/**
 * REVIEWENV1g (the review environment design §3.2–§3.3, §3.6): the gate as the page reads it. The driver's state where it
 * answers one, and otherwise a set-up step's own record; the set-up a verdict answers named whole; which presses each state
 * offers, by one rule.
 */
describe("a review's gate, as the page reads it", () => {
  it("reads the driver's states, and one it does not know as unread, which holds", () => {
    expect(reviewState('shown')).toBe('shown');
    expect(reviewState('not-held')).toBe('not-held');
    expect(reviewState('a-newer-state')).toBe('unread');
    expect(reviewState(undefined)).toBe('unread');
  });

  it("reads a set-up step's state from its own record", () => {
    expect(stepState(STEP_SETTING_UP)).toBe('being-set-up');
    expect(stepState({ ...STEP_SHOWN, setUps: [] })).toBe('being-set-up');
    expect(stepState(STEP_SHOWN)).toBe('shown');
    expect(stepState(STEP_NOT_YET)).toBe('not-yet');
    expect(stepState(STEP_SHOWN_AGAIN)).toBe('shown');
    expect(stepState(STEP_REVIEWED)).toBe('reviewed');
    expect(stepState(STEP_SKIPPED)).toBe('skipped');
    expect(stepState({ ...STEP_SHOWN, status: 'Declined' })).toBe('none');
  });

  it('names the newest set-up whole, and none where the record carries half of one', () => {
    expect(newestSetUp(STEP_SHOWN_AGAIN)).toBe(SET_UP_AGAIN);
    expect(setUpRef(SET_UP)).toEqual({ machine: 'desk', sequence: 7 });
    expect(setUpRef({ ...SET_UP, sequence: null })).toBeNull();
    expect(setUpRef({ ...SET_UP, machine: null })).toBeNull();
    expect(setUpRef(null)).toBeNull();
  });

  it('finds a verdict by the set-up it names, else by its commit, as the driver does', () => {
    expect(verdictOn(STEP_NOT_YET, SET_UP, 'not-yet')?.words).toBe('The label still reads the old name.');
    expect(verdictOn(STEP_NOT_YET, SET_UP_AGAIN, 'not-yet')).toBeNull();
    const unnamed = { ...SET_UP, machine: null, sequence: null };
    expect(verdictOn({ verdicts: [{ said: 'reviewed', commit: SET_UP.commit.toUpperCase() }] }, unnamed, 'reviewed')).not.toBeNull();
  });

  it("lists a set-up step for What needs you only while its newest set-up waits for the person's look", () => {
    expect(waitsForLook(STEP_SHOWN)).toBe(true);
    expect(waitsForLook(STEP_SHOWN_AGAIN)).toBe(true);
    expect(waitsForLook(STEP_NOT_YET)).toBe(false);
    expect(waitsForLook(STEP_REVIEWED)).toBe(false);
    expect(waitsForLook(STEP_SETTING_UP)).toBe(false);
    expect(waitsForLook(WORK)).toBe(false);
  });

  it('offers each state its presses, and Show it again only for a set-up Daoris serves', () => {
    expect(reviewPresses('not-shown')).toEqual(['setUp', 'skip']);
    expect(reviewPresses('shown', { local: true })).toEqual(['reviewed', 'notYet', 'showAgain', 'skip']);
    expect(reviewPresses('shown', { local: Boolean(newestSetUp(STEP_DEPLOYED)?.local) })).toEqual(['reviewed', 'notYet', 'skip']);
    expect(reviewPresses('being-set-up')).toEqual(['skip']);
    expect(reviewPresses('not-yet')).toEqual(['skip']);
    expect(reviewPresses('not-held')).toEqual(['skip']);
    expect(reviewPresses('reviewed')).toEqual([]);
    expect(reviewPresses('unread')).toEqual([]);
  });

  it('gives a skip on the set-up step, and on the work once the step was reviewed', () => {
    expect(skipOn('shown', 'q2', 'q1')).toBe('q2');
    expect(skipOn('not-held', 'q2', 'q1')).toBe('q1');
    expect(skipOn('not-shown', null, 'q1')).toBe('q1');
  });
});
