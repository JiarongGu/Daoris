import { describe, expect, it } from 'vitest';
import type { GoAhead } from '../../data/api';
import {
  ANCHOR_BOUND_MS, anchorOf, anchorsOf, goAheadKey, helperKey, marksOf, opinionKey, proposalKey, turnEnd, waitEvents, workKey,
} from './anchors';
import { lastWords, type SessionEvent, toTurns } from '../conversation/conversation';
import type { Wait } from './waits';

// WAITFOOT1c (D168 point 4): each wait on the person is anchored to the event in its conversation that raised it, by a join the
// page can make from what it holds, never by a call's title (D52). A wait with no raising event, or none the join finds, has no
// anchor rather than a guessed one.

const SESSION = 's1a2b3c4';
const at = (minute: number) => `2026-10-15T10:${String(minute).padStart(2, '0')}:00Z`;
const target: SessionEvent = { seq: 1, at: at(0), kind: 'user', origin: 'target', text: 'Your target is the quest…' };
const said = (seq: number, text: string, minute = seq): SessionEvent => ({ seq, at: at(minute), kind: 'message', id: `m${seq}`, text });
const call = (seq: number, minute: number, over: Partial<SessionEvent>): SessionEvent => ({
  seq, at: at(minute), kind: 'tool', id: `t${seq}`, status: 'completed', ...over,
});
const ended = (seq: number, minute = seq): SessionEvent => ({ seq, at: at(minute), kind: 'turn', stopReason: 'end_turn' });
const turnsOf = (events: SessionEvent[]) => toTurns(events).turns;

const WHY = 'The tile reads its target from the dashboard configuration; I would set it to 0.95.';
const goAheadCall = (seq: number, minute: number, input: Record<string, unknown>, title = 'go_ahead_ask') =>
  call(seq, minute, { title, input: JSON.stringify(input) });
const goAhead = (over: Partial<GoAhead> = {}): GoAhead => ({
  number: 3, kind: 'write', on: 'production', act: 'dashboard configuration', state: 'asked',
  asked: [{ session: SESSION, quest: '7a82cc', at: at(20), why: WHY }],
  ...over,
});
const ASKED = { kind: 'write', on: 'production', act: 'dashboard configuration', why: WHY };

describe('a park\'s question', () => {
  it('is raised by the last thing the agent said: its block', () => {
    const turns = turnsOf([target, said(2, 'Reading.'), call(3, 3, { title: 'Read' }), said(4, 'Which of the two should I keep?'), ended(5)]);
    expect(lastWords(turns)).toBe('e4');
    expect(anchorsOf(turns, { session: SESSION, waits: [{ kind: 'question', below: true }] }).held).toEqual({ question: 'e4' });
    // An intake's park is a question too, raised the same way.
    expect(anchorsOf(turns, { session: SESSION, waits: [{ kind: 'intake', ask: '0fda18' }] }).held).toEqual({ question: 'e4' });
  });

  it('has none where the agent said nothing the page holds', () => {
    const turns = turnsOf([target, call(2, 2, { title: 'Read' }), ended(3)]);
    expect(anchorsOf(turns, { session: SESSION, waits: [{ kind: 'question', below: false }] })).toEqual({ held: {}, earlier: [] });
    // Unless the pages before may hold it: then the way loads them, as a jump does.
    expect(anchorsOf(turns, { session: SESSION, waits: [{ kind: 'question', below: false }] }, { earlier: true }))
      .toEqual({ held: {}, earlier: ['question'] });
  });
});

describe('work to land', () => {
  it('is raised by the end of the turn that did it, past the driver\'s notes after it and words waiting at the foot', () => {
    const turns = turnsOf([
      target, said(2, 'Done: two commits.'), ended(3),
      { seq: 4, at: at(4), kind: 'note', text: '— parked: waiting on the person' },
      { seq: 5, at: at(5), kind: 'user', id: 'w1', reaches: 'resume', text: 'and the tests?' },
    ]);
    expect(workKey(turns)).toBe(turnEnd('e1'));
    expect(anchorsOf(turns, { session: SESSION, waits: [{ kind: 'accept' }] }).held).toEqual({ work: 'end-e1' });
    // The rebase words are the same work's.
    expect(anchorsOf(turns, { session: SESSION, waits: [{ kind: 'rebase' }] }).held).toEqual({ work: 'end-e1' });
  });

  it('is the last turn that worked, where a conversation has several', () => {
    const turns = turnsOf([
      { seq: 1, at: at(1), kind: 'user', text: 'Fix the loader.' }, said(2, 'Fixed.'), ended(3),
      { seq: 4, at: at(4), kind: 'user', text: 'And commit it.' }, said(5, 'Committed.'), ended(6),
    ]);
    expect(workKey(turns)).toBe('end-e4');
  });
});

describe('a second opinion handed to the session', () => {
  const findings: SessionEvent = { seq: 9, at: at(9), kind: 'user', origin: 'target', opinion: 'op-7', text: 'Findings: …' };
  const turns = turnsOf([target, said(2, 'Done.'), ended(3), findings, said(10, 'Disputed finding 2.'), ended(11)]);

  it('is raised by the turn its findings were handed in, by the opinion they are from', () => {
    expect(opinionKey(turns, 'op-7')).toBe('e9');
    expect(anchorsOf(turns, { session: SESSION, waits: [{ kind: 'opinion' }], opinion: 'op-7' }).held).toEqual({ opinion: 'e9' });
  });

  it('has none for another opinion, or a gate that names none', () => {
    expect(opinionKey(turns, 'op-8')).toBeUndefined();
    expect(opinionKey(turns, null)).toBeUndefined();
    // A composed target is no findings, whatever it says.
    expect(opinionKey(turnsOf([target]), 'op-7')).toBeUndefined();
  });
});

describe('a go-ahead', () => {
  it('is raised by the call whose input names its act and why, near when the service took it', () => {
    const turns = turnsOf([target, said(2, 'I need a go-ahead.'), goAheadCall(3, 20, ASKED), said(4, 'Waiting on go-ahead #3.'), ended(5)]);
    expect(goAheadKey(turns, goAhead(), SESSION)).toBe('e3');
    expect(anchorsOf(turns, { session: SESSION, waits: [{ kind: 'goAhead', number: 3 }], goAheads: [goAhead()] }).held)
      .toEqual({ 'goAhead:3': 'e3' });
  });

  it('is joined on the input, never on the title (D52)', () => {
    // Another door names the call otherwise: the input still joins it.
    const named = turnsOf([target, goAheadCall(2, 20, ASKED, 'daoris-knowledge: ask for a go-ahead'), ended(3)]);
    expect(goAheadKey(named, goAhead(), SESSION)).toBe('e2');
    // A call titled as the tool whose input names another act, or another why, is not this go-ahead's.
    const other = turnsOf([
      target,
      goAheadCall(2, 20, { ...ASKED, act: 'release notes' }),
      goAheadCall(3, 20, { ...ASKED, why: 'Something else.' }),
      call(4, 20, { title: 'go_ahead_ask' }),
      ended(5),
    ]);
    expect(goAheadKey(other, goAhead(), SESSION)).toBeUndefined();
  });

  it('takes the call nearest the ask within the bound, and none beyond it', () => {
    const twice = turnsOf([target, goAheadCall(2, 5, ASKED), goAheadCall(3, 19, ASKED), ended(4)]);
    expect(goAheadKey(twice, goAhead(), SESSION)).toBe('e3');
    const minutes = ANCHOR_BOUND_MS / 60_000;
    const far = turnsOf([target, goAheadCall(2, 20 - minutes - 1, ASKED), ended(3)]);
    expect(goAheadKey(far, goAhead(), SESSION)).toBeUndefined();
  });

  it('joins an ask that joined the first in more words, by this session only', () => {
    const later = { session: SESSION, quest: '7a82cc', at: at(40), why: 'Again: the target must be 0.95.' };
    const turns = turnsOf([target, goAheadCall(2, 40, { ...ASKED, act: 'the dashboard configuration tile', why: later.why }), ended(3)]);
    // The latest ask of this session's is the one the park waits on.
    expect(goAheadKey(turns, goAhead({ asked: [goAhead().asked[0]!, later] }), SESSION)).toBe('e2');
    // Another session's ask on the same quest's ask is not this conversation's.
    expect(goAheadKey(turns, goAhead({ asked: [{ ...later, session: 'other' }] }), SESSION)).toBeUndefined();
  });

  it('may be on an earlier page only where it was asked before the page begins', () => {
    const page = turnsOf([said(50, 'Waiting on go-ahead #3.', 30), ended(51, 31)]);
    const asks = { session: SESSION, waits: [{ kind: 'goAhead', number: 3 } as Wait], goAheads: [goAhead()] };
    expect(anchorsOf(page, asks, { earlier: true, from: at(30) }).earlier).toEqual(['goAhead:3']);
    // Asked after the page began, its call would be on it: not found there, it has none.
    expect(anchorsOf(page, asks, { earlier: true, from: at(5) }).earlier).toEqual([]);
  });
});

describe('a rule proposal', () => {
  const proposal = { action: 'add' as const, scope: 'repository' as const, rule: 'Bash(git branch -D:*)', proposed: at(20) };
  const input = { action: 'add', scope: 'repository', list: 'allow', rule: 'Bash(git branch -D:*)', why: 'To tidy the branch.' };

  it('is raised by the call whose input names its rule and scope', () => {
    const turns = turnsOf([target, call(2, 20, { title: 'permission_propose', input: JSON.stringify(input) }), ended(3)]);
    expect(proposalKey(turns, proposal)).toBe('e2');
  });

  it('has none where no call names them', () => {
    const turns = turnsOf([
      target,
      call(2, 20, { title: 'permission_propose', input: JSON.stringify({ ...input, scope: 'machine' }) }),
      call(3, 20, { title: 'permission_propose', input: JSON.stringify({ ...input, rule: 'Bash(git push:*)' }) }),
      call(4, 20, { title: 'permission_propose' }),
      ended(5),
    ]);
    expect(proposalKey(turns, proposal)).toBeUndefined();
  });

  // PROPOSEHERE1b: the proposal is a wait, anchored by its call.
  const asks = { session: SESSION, waits: [{ kind: 'rule', id: 'r1' } as Wait], proposals: [{ id: 'r1', ...proposal }] };

  it('anchors the wait the proposal is by its call', () => {
    expect(anchorOf({ kind: 'rule', id: 'r1' })).toBe('rule:r1');
    const turns = turnsOf([target, call(2, 20, { title: 'permission_propose', input: JSON.stringify(input) }), ended(3)]);
    expect(anchorsOf(turns, asks).held).toEqual({ 'rule:r1': 'e2' });
    // A wait whose proposal the frame does not hand has none.
    expect(anchorsOf(turns, { ...asks, proposals: [] }, { earlier: true })).toEqual({ held: {}, earlier: [] });
  });

  it('may be on an earlier page only where it was proposed before the page begins', () => {
    const page = turnsOf([said(50, 'I proposed the rule.', 30), ended(51, 31)]);
    expect(anchorsOf(page, asks, { earlier: true, from: at(30) }).earlier).toEqual(['rule:r1']);
    expect(anchorsOf(page, asks, { earlier: true, from: at(5) }).earlier).toEqual([]);
  });
});

// WAITFOOT1e (D168 point 4): an agent's ask for a helper that waits on the person is raised by its `helper_ask` call, joined on the
// question the call's input carries (and the agent, where the ask named one), nearest when the ask was made, within the bound.
describe('an agent\'s ask for a helper', () => {
  const ask = { id: 'a1', question: 'Which test covers the loader?', agent: null, at: at(20) };
  const input = { question: 'Which test covers the loader?' };
  const asks = { session: SESSION, waits: [{ kind: 'helper', id: 'a1' } as Wait], helpers: [ask] };

  it('is raised by the call whose input asks its question', () => {
    const turns = turnsOf([target, call(2, 20, { title: 'helper_ask', input: JSON.stringify(input) }), ended(3)]);
    expect(helperKey(turns, ask)).toBe('e2');
  });

  it('is joined on the input, never the title, and by the agent the ask named', () => {
    const named = { ...ask, agent: 'codex-acp' };
    const turns = turnsOf([
      target,
      call(2, 20, { title: 'something else', input: JSON.stringify({ question: ` ${ask.question}\n`, agent: 'CODEX-ACP' }) }),
      call(3, 20, { title: 'helper_ask', input: JSON.stringify({ question: ask.question, agent: 'dsh' }) }),
      ended(4),
    ]);
    expect(helperKey(turns, named)).toBe('e2');
    expect(helperKey(turns, { ...named, agent: 'gemini' })).toBeUndefined();
  });

  it('has none where no call asks the question, and takes the nearest within the bound', () => {
    const turns = turnsOf([
      target,
      call(2, 20, { title: 'helper_ask', input: JSON.stringify({ question: 'Another question?' }) }),
      call(3, 2, { title: 'helper_ask', input: JSON.stringify(input) }),
      call(4, 19, { title: 'helper_ask', input: JSON.stringify(input) }),
      ended(5),
    ]);
    expect(helperKey(turns, ask)).toBe('e4');
    expect(helperKey(turns, { ...ask, question: 'Not asked at all?' })).toBeUndefined();
    const farOnly = turnsOf([target, call(2, 20 + ANCHOR_BOUND_MS / 60_000 + 1, { title: 'helper_ask', input: JSON.stringify(input) }), ended(3)]);
    expect(helperKey(farOnly, ask)).toBeUndefined();
  });

  it('anchors the wait the ask is by its call, and is on an earlier page only where it was asked before the page begins', () => {
    expect(anchorOf({ kind: 'helper', id: 'a1' })).toBe('helper:a1');
    const turns = turnsOf([target, call(2, 20, { title: 'helper_ask', input: JSON.stringify(input) }), ended(3)]);
    expect(anchorsOf(turns, asks).held).toEqual({ 'helper:a1': 'e2' });
    // A wait whose ask the frame does not hand has none.
    expect(anchorsOf(turns, { ...asks, helpers: [] }, { earlier: true })).toEqual({ held: {}, earlier: [] });

    const page = turnsOf([said(50, 'I asked.', 30), ended(51, 31)]);
    expect(anchorsOf(page, asks, { earlier: true, from: at(30) }).earlier).toEqual(['helper:a1']);
    expect(anchorsOf(page, asks, { earlier: true, from: at(5) }).earlier).toEqual([]);
  });

  it('marks its call with the wait and hands the wait its press', () => {
    const wait = { kind: 'helper', id: 'a1' } as Wait;
    expect(marksOf([wait], { 'helper:a1': 'e2' })).toEqual({ e2: [wait] });
    const shown: string[] = [];
    const events = waitEvents(['helper:a1'], (anchor) => shown.push(anchor));
    events!.helpers!.a1!();
    expect(shown).toEqual(['helper:a1']);
  });
});

describe('the waits with no raising event', () => {
  it('anchor neither a review\'s gate nor a discard the line holds', () => {
    expect(anchorOf({ kind: 'review' })).toBeNull();
    expect(anchorOf({ kind: 'discard' })).toBeNull();
    const turns = turnsOf([target, said(2, 'Done.'), ended(3)]);
    expect(anchorsOf(turns, { session: SESSION, waits: [{ kind: 'review' }, { kind: 'discard' }] }, { earlier: true }))
      .toEqual({ held: {}, earlier: [] });
  });
});

describe('the marks and the ways', () => {
  it('marks each raising block with the waits it raised', () => {
    const waits: Wait[] = [{ kind: 'question', below: true }, { kind: 'goAhead', number: 3 }, { kind: 'accept' }, { kind: 'review' }];
    expect(marksOf(waits, { question: 'e4', 'goAhead:3': 'e3', work: 'end-e1' })).toEqual({
      e4: [{ kind: 'question', below: true }],
      e3: [{ kind: 'goAhead', number: 3 }],
      'end-e1': [{ kind: 'accept' }],
    });
  });

  it('hands each anchored wait its press, and none to the others', () => {
    const shown: string[] = [];
    const events = waitEvents(['question', 'goAhead:3', 'work'], (wait) => shown.push(wait))!;
    events.question!();
    events.goAheads![3]!();
    events.work!();
    expect(shown).toEqual(['question', 'goAhead:3', 'work']);
    expect(events.opinion).toBeUndefined();
    expect(events.goAheads![4]).toBeUndefined();
    expect(waitEvents([], () => {})).toBeUndefined();
    // A rule proposal's, by its id (PROPOSEHERE1b).
    const rules = waitEvents(['rule:r1'], (wait) => shown.push(wait))!;
    rules.rules!.r1!();
    expect(shown.at(-1)).toBe('rule:r1');
    expect(rules.goAheads).toBeUndefined();
  });

  it('marks a rule proposal\'s call with its wait', () => {
    expect(marksOf([{ kind: 'rule', id: 'r1' }], { 'rule:r1': 'e2' })).toEqual({ e2: [{ kind: 'rule', id: 'r1' }] });
  });
});
