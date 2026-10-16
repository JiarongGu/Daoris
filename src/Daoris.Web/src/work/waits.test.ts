import { describe, expect, it } from 'vitest';
import type { GoAhead, Session } from '../data/api';
import type { RuleProposal } from '../settings/AgentRules';
import type { SessionDiff } from './diff';
import type { HelperAskRow } from './HelperAsks';
import { GATES } from './opinionFixtures';
import { conversationOffer, landingStands, rulesAsked, waitsOf } from './waits';

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

  // PROPOSEHERE1b (D168 point 2, D74): a rule the session proposed waits on the person where they talk to it.
  const proposal = (id: string, state: RuleProposal['state'], over: Partial<RuleProposal> = {}): RuleProposal => ({
    id, state, action: 'add', scope: 'repository', name: 'engine', list: 'allow', rule: 'Bash(git branch -D:*)',
    why: 'Auto mode refused the branch delete you asked for.', session: 's1a2b3c4', proposed: '2026-10-15T10:20:00Z', ...over,
  });

  it('names each rule this session proposed that the person still answers, and none of another\'s or one settled', () => {
    const proposals = [
      proposal('r1', 'waiting'), proposal('r2', 'proposed'), proposal('r3', 'applied'), proposal('r4', 'declined'),
      proposal('r5', 'waiting', { session: 'o7h3r000' }), proposal('r6', 'waiting', { session: undefined }),
    ];
    expect(rulesAsked(proposals, 's1a2b3c4').map((one) => one.id)).toEqual(['r1', 'r2']);
    expect(waitsOf({ session: session(), rules: { proposals, onAnswer: press } })).toEqual([
      { kind: 'rule', id: 'r1' }, { kind: 'rule', id: 'r2' },
    ]);
    // With no door to answer them (a browser has no rules), none; an older shell's answer that is no list holds none.
    expect(waitsOf({ session: session(), rules: { proposals } })).toEqual([]);
    expect(rulesAsked({ proposals }, 's1a2b3c4')).toEqual([]);
    expect(rulesAsked(undefined, 's1a2b3c4')).toEqual([]);
  });

  it('stands after the go-aheads and before the work, parked or not', () => {
    const rules = { proposals: [proposal('r1', 'waiting')], onAnswer: press };
    expect(waitsOf({ session: parked, onResolve: press, answerBelow: true, goAheads: [goAhead(2, 'asked')], onGoAhead: press, rules }))
      .toEqual([{ kind: 'question', below: true }, { kind: 'goAhead', number: 2 }, { kind: 'rule', id: 'r1' }]);
    expect(waitsOf({ session: session(), lands: LANDS, landing: PLAN, onLand: press, rules }))
      .toEqual([{ kind: 'rule', id: 'r1' }, { kind: 'accept' }]);
  });

  // MIXAGENT2c (D168 points 2 and 3, D163 point 3): an agent's ask for a helper the driver's look holds waiting on the person's
  // word is one of the session's waits, as a rule it proposed is.
  const ask = (id: string, over: Partial<HelperAskRow> = {}): HelperAskRow => ({
    id, question: 'Which test covers the loader?', agent: null, askedBy: 'agent', at: '2026-09-21T11:00:00Z', state: 'waiting',
    code: null, helper: null, startedOn: null, ...over,
  });

  it('names each agent’s ask for a helper that waits on the person’s word, and none that waits on anything else', () => {
    const asks = [
      ask('a1'), ask('a2', { state: null }), ask('a3', { askedBy: 'person' }), ask('a4', { code: 'cap' }),
      ask('a5', { state: 'approved' }), ask('a6', { state: 'started', helper: 'h1' }), ask('a7', { state: 'refused', code: 'declined' }),
      ask('a8'),
    ];
    expect(waitsOf({ session: session({ state: 'working' }), helpers: { asks, onApprove: press } }))
      .toEqual([{ kind: 'helper', id: 'a1' }, { kind: 'helper', id: 'a8' }]);
    // With no press to answer them (a browser, a teammate's record), none.
    expect(waitsOf({ session: session({ state: 'working' }), helpers: { asks } })).toEqual([]);
  });

  it('stands after the rules and before the work', () => {
    const rules = { proposals: [proposal('r1', 'waiting')], onAnswer: press };
    expect(waitsOf({ session: session(), lands: LANDS, landing: PLAN, onLand: press, rules, helpers: { asks: [ask('a1')], onApprove: press } }))
      .toEqual([{ kind: 'rule', id: 'r1' }, { kind: 'helper', id: 'a1' }, { kind: 'accept' }]);
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

// ACCEPTFOOT1 (D164's note): an open conversation's record stays `working` between turns, so the driver's look offers its tree
// nothing; once its turn has ended, the review's own answer says what is beyond the line, and that is its offer.
describe('what an open conversation offers once its turn has ended', () => {
  const ENDED = { taking: false, queued: [] };
  const REVIEW = {
    session: 'c0ffee11', base: 'abc1234', truncated: null, files: [{ path: 'a.ts', status: 'modified', added: 1, removed: 0, patch: '' }],
    behind: { line: 'main', commits: 0, ahead: 2 }, treeBranch: 'daoris/s-c0ffee11',
  };

  it('offers the commits beyond the line on the branch its tree is on', () => {
    expect(conversationOffer({ tree: 's-c0ffee11', turn: ENDED, review: REVIEW }))
      .toEqual({ branch: 'daoris/s-c0ffee11', tree: 's-c0ffee11', commits: 2, uncommitted: 0 });
    // A branch its agent moved it to (TREEFOLLOW1), and the uncommitted files the ask counts.
    const uncommitted = { files: [REVIEW.files[0]!], truncated: false };
    expect(conversationOffer({ tree: 's-c0ffee11', turn: ENDED, review: { ...REVIEW, treeBranch: 'feature/x', uncommitted } }))
      .toEqual({ branch: 'feature/x', tree: 's-c0ffee11', commits: 2, uncommitted: 1 });
    // A host before REVIEWBASE4 names no branch: the layout's, as the driver's offer says it.
    expect(conversationOffer({ tree: 's-c0ffee11', turn: ENDED, review: { ...REVIEW, treeBranch: undefined } })?.branch)
      .toBe('daoris/s-c0ffee11');
  });

  it('offers nothing while its turn runs, is queued, or is not known yet', () => {
    expect(conversationOffer({ tree: 's-c0ffee11', turn: { taking: true, queued: [] }, review: REVIEW })).toBeNull();
    expect(conversationOffer({ tree: 's-c0ffee11', turn: { taking: false, queued: [{ text: 'and then' }] }, review: REVIEW })).toBeNull();
    expect(conversationOffer({ tree: 's-c0ffee11', turn: undefined, review: REVIEW })).toBeNull();
  });

  it('offers nothing where nothing is beyond the line, or the line was not read (REVIEWBASE4)', () => {
    expect(conversationOffer({ tree: 's-c0ffee11', turn: ENDED, review: { ...REVIEW, behind: { line: 'main', commits: 0, ahead: 0 } } }))
      .toBeNull();
    expect(conversationOffer({ tree: 's-c0ffee11', turn: ENDED, review: { ...REVIEW, behind: null } })).toBeNull();
    expect(conversationOffer({ tree: 's-c0ffee11', turn: ENDED, review: undefined })).toBeNull();
  });

  it('offers nothing in the person\'s checkout, or once the review reads as landed', () => {
    expect(conversationOffer({ tree: null, turn: ENDED, review: REVIEW })).toBeNull();
    const landed = { branch: 'feature/x', state: 'standing', asLanded: true } as NonNullable<SessionDiff['landed']>;
    expect(conversationOffer({ tree: 's-c0ffee11', turn: ENDED, review: { ...REVIEW, landed } })).toBeNull();
    expect(conversationOffer({ tree: 's-c0ffee11', turn: ENDED, review: { ...REVIEW, source: 'branch' } })).toBeNull();
  });
});
