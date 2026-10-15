import { describe, expect, it } from 'vitest';
import type { GoAhead, Session } from '../data/api';
import { GATES } from './opinionFixtures';
import { landingStands, waitsOf } from './waits';

// WAITFOOT1b (D168 point 2): which waits on the person stand, one rule the foot's strip and the head's work read, so the two
// cannot disagree. A wait is the person's to press; what waits on an agent, a workflow or nothing is not one.

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: '7a82cc', repository: 'engine', adapter: 'claude-code', state: 'completed', kind: 'driven',
  created: '2026-09-21T09:46:00Z', updated: '2026-09-21T11:56:00Z', ...over,
});

const parked = session({ state: 'awaiting-person', note: 'Which of the two should I keep?' });
const press = () => {};
const LANDS = { branch: 'daoris/s-1a2b3c4', tree: 's-1a2b3c4', commits: 2, uncommitted: 0 };
const PLAN = { session: 's1a2b3c4', form: 'merge', target: 'main' };

const goAhead = (number: number, state: GoAhead['state']): GoAhead => ({
  number, kind: 'write', on: 'production', act: 'dashboard configuration', state,
  asked: [{ session: 's1a2b3c4', quest: '7a82cc', at: '2026-09-21T11:00:00Z', why: 'The tile reads its target from it.' }],
});

describe('the waits on the person', () => {
  it('is none where nothing is the person\'s to press', () => {
    expect(waitsOf({ session: session({ state: 'working' }) })).toEqual([]);
    // Parked, but nothing here can act (a teammate's record, a browser): the strip is absent.
    expect(waitsOf({ session: parked })).toEqual([]);
  });

  it('names a park\'s question, and whether the box below carries its answer', () => {
    expect(waitsOf({ session: parked, onResolve: press, answerBelow: true })).toEqual([{ kind: 'question', below: true }]);
    expect(waitsOf({ session: parked, onResolve: press })).toEqual([{ kind: 'question', below: false }]);
    // Answered (ANSWER1c), it goes on at the driver's next look: nothing asks the person again.
    expect(waitsOf({ session: { ...parked, answer: 'the first' }, onResolve: press, answerBelow: true })).toEqual([]);
  });

  it('names an intake\'s park by its ask, whose door answers it', () => {
    const intake = session({ state: 'awaiting-person', kind: 'chat', quest: null, ask: '0fda18', repository: 'ask #0fda18' });
    expect(waitsOf({ session: intake, onAnswerAsk: press, onResolve: press })).toEqual([{ kind: 'intake', ask: '0fda18' }]);
    expect(waitsOf({ session: intake, onResolve: press })).toEqual([]);
  });

  it('names each go-ahead still open while it is parked, and none answered', () => {
    const goAheads = [goAhead(1, 'approved'), goAhead(2, 'asked'), goAhead(3, 'asked')];
    expect(waitsOf({ session: parked, onResolve: press, answerBelow: true, goAheads, onGoAhead: press })).toEqual([
      { kind: 'question', below: true }, { kind: 'goAhead', number: 2 }, { kind: 'goAhead', number: 3 },
    ]);
    // With no door to answer them, they are shown at the head and are no wait here.
    expect(waitsOf({ session: parked, goAheads })).toEqual([]);
  });

  it('names the work to land where *Accept…* is offered, and not where nothing is beyond the line', () => {
    expect(waitsOf({ session: session(), lands: LANDS, landing: PLAN, onLand: press })).toEqual([{ kind: 'accept' }]);
    expect(waitsOf({ session: session(), lands: LANDS, landing: PLAN })).toEqual([]);
    expect(waitsOf({ session: session(), lands: LANDS, landing: PLAN, onLand: press, behind: { line: 'main', commits: 0, ahead: 0 } }))
      .toEqual([]);
  });

  it('names the rebase words in Accept\'s place where replaying conflicts', () => {
    const landing = { ...PLAN, conflict: { says: 'CONFLICT (content)', words: 'Rebase your work onto `main`.' } };
    expect(waitsOf({ session: session(), lands: LANDS, landing, onLand: press, onSayRebase: async () => true }))
      .toEqual([{ kind: 'rebase' }]);
  });

  it('names a second opinion in the person\'s hue, and not one an agent is reading', () => {
    const acts = { ask: press };
    expect(waitsOf({ session: session(), lands: LANDS, landing: { ...PLAN, opinion: GATES.disputed }, onLand: press, opinion: { acts } }))
      .toEqual([{ kind: 'accept' }, { kind: 'opinion' }]);
    expect(waitsOf({ session: session(), lands: LANDS, landing: { ...PLAN, opinion: GATES.reading }, onLand: press, opinion: { acts } }))
      .toEqual([]);
  });

  it('names a review\'s gate waiting on the person\'s look, and not one being set up', () => {
    const review = { acts: { reviewed: press } };
    expect(waitsOf({ session: session(), lands: LANDS, landing: { ...PLAN, review: { state: 'shown', environment: 'local' } }, onLand: press, review }))
      .toEqual([{ kind: 'review' }]);
    expect(waitsOf({ session: session(), lands: LANDS, landing: { ...PLAN, review: { state: 'being-set-up', environment: 'local' } }, onLand: press, review }))
      .toEqual([]);
  });

  it('names a held discard where an unforced discard would go', () => {
    const discards = {
      branch: 'daoris/s-1a2b3c4', tree: 's-1a2b3c4', says: 'Its work is on `main` by content.', keeps: 'refs/daoris/discarded/x',
      keptAt: 'Its commits stay at a ref.',
    };
    expect(waitsOf({ session: session(), discards, onDiscardTree: press })).toEqual([{ kind: 'discard' }]);
    expect(waitsOf({ session: session(), discards: { ...discards, keptAt: null }, onDiscardTree: press })).toEqual([]);
  });
});

describe('how the work stands for its landing', () => {
  it('offers *Accept…* unless a gate holds it, nothing is beyond the line, or the replay conflicts', () => {
    expect(landingStands({ lands: LANDS, landing: PLAN })).toMatchObject({ acceptable: true, conflict: null });
    expect(landingStands({ lands: LANDS, landing: { ...PLAN, review: { state: 'shown' } } })).toMatchObject({ acceptable: false });
    expect(landingStands({ lands: { ...LANDS, uncommitted: 2 }, landing: PLAN, behind: { line: 'main', commits: 0, ahead: 0 } }))
      .toMatchObject({ acceptable: true });
    const conflict = { says: 'CONFLICT', words: 'Rebase.' };
    expect(landingStands({ lands: LANDS, landing: { ...PLAN, conflict } })).toMatchObject({ acceptable: true, conflict });
    // A dispute *Accept…* answers keeps it, with the token it sends back (XAGENT1g).
    expect(landingStands({ lands: LANDS, landing: { ...PLAN, opinion: { ...GATES.disputed, answers: 't0k' } } }))
      .toMatchObject({ acceptable: true, answers: 't0k' });
  });
});
