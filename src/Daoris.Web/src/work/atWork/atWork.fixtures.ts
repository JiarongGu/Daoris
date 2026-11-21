import type { Consideration } from '../../data/signals';
import type { ChannelItem } from '../channel';
import type { AtWorkA, AtWorkAsker, AtWorkExchange, AtWorkHeld, AtWorkHint, AtWorkPart } from './types';

// Who is at work: the answers the view's stories and tests draw, each a state of the view.

const AT = '2026-10-31T14:02:00Z';
const LATER = '2026-10-31T14:05:00Z';

/** A lone attended part `s1` of `agent`, its subagents counted as `children`; `extra` over the rest of the answer. */
export const answerOf = (children: AtWorkPart['children'], extra: Partial<AtWorkA> = {}, agent = 'claude-code'): AtWorkHeld => ({
  answer: {
    of: { session: 's1' }, attended: 's1',
    parts: [{ key: 's1', agent, state: 'working', children }],
    ...extra,
  },
  at: { epoch: 'e1', n: 1 },
});

/** A chat with three subagents, one of them working. */
export const THREE = answerOf({ count: 3, working: 1 });

/** A Codex lead with no lineage yet: its subagents appear only when they call Daoris, so none counted is never *nobody*. */
export const CODEX_NONE = answerOf({ count: 0, working: 0 }, {}, 'codex');

/** Agent ids alone: unmatched and not tied to a process. */
export const IDS_ONLY = answerOf({ count: 0, working: 0, observed: 2, unplaced: 2 });

/** An entry of three parts, the attended first, one a teammate's. */
export const PARTS = answerOf({ count: 1, working: 1 }, {
  parts: [
    { key: 's1', agent: 'claude-code', state: 'working', children: { count: 1, working: 1 } },
    { key: 's0', agent: 'codex', state: 'completed', children: { count: 2, working: 0 } },
    { key: 'origin/s7', state: 'working', children: { elsewhere: true } },
  ],
  moreParts: { completed: 2 },
});

const quiet: AtWorkPart['children'] = { count: 0, working: 0 };

/** A part of the routing half's entries: numbered, its children counted. */
export const routedPart = (key: string, number: number, extra: Partial<AtWorkPart> = {}): AtWorkPart => ({
  key, number, agent: 'claude-code', state: 'working', children: { count: 0, working: 0 }, ...extra,
});

const routed = (parts: AtWorkPart[], extra: Partial<AtWorkA> = {}, attended = parts[0]!.key): AtWorkHeld =>
  answerOf(quiet, { attended, parts, ...extra });

/** A lead waiting on its own lane part, drawn as the lead and never folded; the lane part alongside, in a tree of its own. */
export const LEAD_WAITING_ON_LANE = routed([
  routedPart('s1', 1, { state: 'completed', waits: { quest: 'q7', of: 'driver', lanes: ['core'] } }),
  routedPart('s2', 2, {
    joined: { began: 'opened', quest: 'q7', lanes: ['core'], byPart: 1, of: 'driver' }, alongside: true,
  }),
], {
  exchanges: [{ id: 'quest:q7', part: 's1', expects: 'work', to: 'driver', delivery: 'taken', outcome: 'taken-in', holds: true, lanes: ['core'] }],
});

/** A helper the line asked, by its exchange as the driver's read answers it. */
export const helperAsked = (over: Partial<AtWorkExchange> = {}): AtWorkExchange => ({
  id: 'helper:h7', part: 's1', expects: 'answer', to: 'codex-acp', delivery: 'taken', outcome: 'asked', holds: true, at: AT,
  session: 'h-session', agent: 'codex-acp', ...over,
});

/** A lead waiting on its helper, which the strip words *awaiting helper*. */
export const LEAD_WAITING_ON_HELPER = routed([
  routedPart('s1', 1, { state: 'completed', waits: { helper: true } }),
  routedPart('s2', 2, { state: 'completed', joined: { began: 'retried', after: 1 } }),
], { exchanges: [helperAsked()] });

/** A chat that took a quest and waits on the question the quest asked. */
export const CHAT_TOOK_A_QUEST = routed([
  routedPart('c1', 1, { state: 'completed', joined: { began: 'opened', quest: 'q1', took: true, of: 'driver' }, waits: { quest: 'q9', of: 'docs' } }),
], { exchanges: [{ id: 'quest:q9', part: 'c1', expects: 'work', to: 'docs', delivery: 'kept', outcome: 'asked', holds: true }] });

/**
 * A line's co-workers in each state a member can stand in: a helper asked and waiting for the person's yes, one reading, one
 * answered and taken, a partner refused, and a successor asked by the part before a hand-over; one source not read.
 */
export const COWORKERS = routed([routedPart('s2', 2, { state: 'working' }), routedPart('s1', 1, { state: 'completed' })], {
  exchanges: [
    helperAsked({ id: 'helper:h5', outcome: 'asked', delivery: 'kept', session: undefined, agent: undefined, to: 'helper' }),
    helperAsked({ id: 'helper:h6', outcome: 'approved', delivery: 'kept', session: undefined }),
    helperAsked({ id: 'helper:h7', part: 's2' }),
    helperAsked({ id: 'helper:h8', part: 's2', outcome: 'taken-in', holds: false, answered: LATER }),
    { id: 'piece:p1', part: 's2', expects: 'piece', to: 'claude-code', delivery: 'not-reached', outcome: 'refused', holds: false },
    { id: 'hand-over:o1', part: 's1', expects: 'successor', to: 'successor', delivery: 'uncertain', outcome: 'approved', holds: true },
  ],
  unread: ['piece'],
}, 's2');

/**
 * A finished line's reviewers, read from their records that serve it: one reading, which the line waits on,
 * one whose reading ended and one that failed.
 */
export const REVIEWERS = routed([routedPart('s1', 1, { state: 'completed' })], {
  exchanges: [
    { id: 'opinion:o1', part: 's1', expects: 'opinion', to: 'codex-acp', delivery: 'kept', outcome: 'asked', holds: true, session: 'r1', agent: 'codex-acp' },
    { id: 'opinion:o0', part: 's1', expects: 'opinion', to: 'claude-code', delivery: 'taken', outcome: 'ended', holds: false, session: 'r0', agent: 'claude-code' },
    { id: 'opinion:o2', part: 's1', expects: 'opinion', to: 'reviewer', delivery: 'not-reached', outcome: 'declined', holds: false, session: 'r2' },
  ],
}, 's1');

/** A session that published three quests and waits on none: two lane parts alongside it. */
export const PUBLISHED_AND_WAITS_ON_NONE = routed([
  routedPart('s1', 1, { state: 'completed' }),
  routedPart('s2', 2, { joined: { began: 'opened', quest: 'q2', lanes: ['core'], byPart: 1, of: 'driver' }, alongside: true }),
  routedPart('s3', 3, { joined: { began: 'opened', quest: 'q3', lanes: ['docs'], byPart: 1, of: 'driver' }, alongside: true }),
], { published: 3 });

/** The resumed record first, the record that waited among the earlier parts, and four earlier parts folded. */
export const RESUMED_AND_EARLIER = routed([
  routedPart('s5', 5, { joined: { began: 'resumed', after: 4, quest: 'q1', of: 'driver' } }),
  routedPart('s2', 2, { state: 'failed', joined: { began: 'retried', after: 1 } }),
  routedPart('s3', 3, { state: 'completed', joined: { began: 'carried-on', after: 2 } }),
  routedPart('s4', 4, { state: 'completed', joined: { began: 'handed-over', after: 3 } }),
], { earlier: 4 });

/** Parts that joined by each of the other doors. */
export const JOINED_EACH_WAY = routed([
  routedPart('s1', 1, { joined: { began: 'asked', quest: 'q4', ask: 'a7' } }),
  routedPart('s2', 2, { joined: { began: 'opened', quest: 'q5', ask: 'a7' } }),
  routedPart('s3', 3, { joined: { began: 'carried-on', quest: 'q6', parent: 'q5', after: 2 } }),
  routedPart('s4', 4, { joined: { began: 'opened', quest: 'q8', byPart: 1, of: 'driver' } }),
  routedPart('s5', 5, { joined: { began: 'asked', askedBy: 'core' } }),
]);

/** More parts than the limit holds: overflow counted apart from the three-ended-parts fold. */
export const OVERFLOW_AND_EARLIER = routed(
  [routedPart('s1', 1), routedPart('s2', 2, { waits: { helper: true }, state: 'completed' })],
  { earlier: 2, moreParts: { working: 10 } },
);

// The quests a part asked, and what asked it.

/** The tick's reasons for two untaken quests, as `considered` holds them. */
export const CONSIDERED: Consideration[] = [
  { quest: 'q4', repository: 'driver', verdict: 'AtCapacity', reason: 'The concurrency cap (1) is spent.', said: { code: 'cap', cap: 1 } },
  { quest: 'q8', repository: 'docs', verdict: 'NotDrivable', reason: '`docs` is not driven here.' },
];

/** Two lane parts alongside the chat that published them, and a third lane quest untaken while the cap is spent. */
export const LANES_ALONGSIDE_AND_UNTAKEN = routed([
  routedPart('c1', 1, { state: 'completed' }),
  routedPart('s2', 2, { joined: { began: 'opened', quest: 'q2', lanes: ['core'], byPart: 1, of: 'driver' }, alongside: true }),
  routedPart('s3', 3, { joined: { began: 'opened', quest: 'q3', lanes: ['docs'], byPart: 1, of: 'driver' }, alongside: true }),
], {
  published: 3,
  quests: [{ id: 'q4', title: 'Tidy the media lane', status: 'Open', lanes: ['media'], taker: { untaken: true } }],
});

/** Each taker: a session here, a chat through its connector, another machine, no session recorded here, and nobody yet. */
export const QUESTS_EACH_TAKER = routed([routedPart('c1', 1, { state: 'completed' })], {
  published: 6,
  quests: [
    { id: 'q1', title: 'Index the changes', status: 'Taken', lanes: ['core'], taker: { session: 's9', agent: 'claude-code', state: 'working' } },
    { id: 'q2', title: 'Write the notes', status: 'Taken', taker: { conversation: 'c7' } },
    { id: 'q3', title: 'Check the build', status: 'Taken', of: 'engine', taker: { elsewhere: true } },
    { id: 'q5', title: 'Update the guide', status: 'Taken', of: 'docs', taker: { unrecorded: true } },
    { id: 'q4', title: 'Tidy the media lane', status: 'Open', lanes: ['media'], taker: { untaken: true } },
    { id: 'q6', title: 'Drop the old route', status: 'Declined' },
  ],
});

/** A chain: a quest done, its next step taken beneath it, and a quest to another repository whose taker waits on a question. */
export const CHAIN_AND_QUESTION = routed([routedPart('c1', 1, { state: 'completed' })], {
  published: 2,
  quests: [
    { id: 'q1', title: 'Cut the release', status: 'Done', of: 'engine', taker: { session: 's4', agent: 'codex', state: 'completed' } },
    { id: 'q2', title: 'Publish the notes', status: 'Taken', of: 'engine', step: 'q1', taker: { session: 's5', agent: 'codex', state: 'awaiting-person' }, waits: { quest: 'q9', of: 'docs', lanes: ['api'] } },
  ],
});

/** The attended part's own quest: asked by a session, the step before, an ask, or the person. */
export const ASKED_BY = (by: AtWorkAsker['by']): AtWorkHeld => routed(
  [routedPart('s2', 2, { joined: { began: 'opened', quest: 'q1', of: 'driver' } }), routedPart('s1', 1, { state: 'completed' })],
  { askedBy: { quest: 'q1', by } },
  's2',
);

// An ask's view, a branch of its own.

/** An ask with two lines of work (one with a subagent working, one done) and a quest untaken. */
export const ASK_TWO_ENTRIES: AtWorkHeld = {
  askAnswer: {
    of: { ask: 'a1' },
    entries: [
      { entry: 'ask:a1/driver', repository: 'driver', opens: 's2', shown: 'working', parts: 2, working: 1, subagents: 3, subagentsWorking: 1 },
      { entry: 'ask:a1/docs', repository: 'docs', opens: 's3', shown: 'completed', parts: 1, working: 0, subagents: 0, subagentsWorking: 0 },
    ],
    quests: [{ id: 'q4', title: 'Tidy the media lane', status: 'Open', lanes: ['media'], taker: { untaken: true } }],
  },
  at: { epoch: 'e1', n: 1 },
};

/** An ask the slice holds with nothing at work: valid and empty, never reading. */
export const ASK_EMPTY: AtWorkHeld = { askAnswer: { of: { ask: 'a1' }, entries: [] }, at: { epoch: 'e1', n: 1 } };

/** Its lines of work, a teammate's part among one of them. */
export const ASK_TEAMMATE: AtWorkHeld = {
  askAnswer: {
    of: { ask: 'a1' },
    entries: [{ entry: 'ask:a1/driver', repository: 'driver', opens: 's2', shown: 'working', parts: 2, working: 1, subagents: 1, subagentsWorking: 1, elsewhere: true }],
  },
  at: { epoch: 'e1', n: 1 },
};

// The channel's last words.

const posted = (id: string, at: string, over: Partial<ChannelItem>): ChannelItem => ({ kind: 'message', id, at, ...over });

/** The newest three messages of the channel the page holds: the person's, a lane part's, and the lead's own. */
export const SAID: ChannelItem[] = [
  posted('m1', '2026-10-31T14:01:00Z', { person: true, text: 'Start with the index, then the notes.' }),
  posted('m2', '2026-10-31T14:03:00Z', { session: 's2', repository: 'driver', agent: 'claude-code', text: 'The core lane is done; the index needs a second look before the notes go in.' }),
  posted('m3', '2026-10-31T14:04:00Z', { session: 'c1', repository: 'game', agent: 'claude-code', text: 'Waiting on the docs lane.' }),
];

/**
 * A line's own channel, a chat's on no ask: the lead and its lane part alongside post to each other, each to the other alone,
 * and the lead to everyone on it.
 */
export const LINE_SAID: ChannelItem[] = [
  posted('aaaaaaaaa1', '2026-10-31T14:01:00Z', { session: 'c1', repository: 'game', agent: 'claude-code', channel: 'session:c1', part: 's2', receipt: 'read', text: 'The contract changed: the bridge takes a region.' }),
  posted('aaaaaaaaa2', '2026-10-31T14:02:00Z', { session: 's2', repository: 'driver', agent: 'claude-code', channel: 'session:c1', part: 'c1', receipt: 'taken', text: 'Taken; the index reads the region now.' }),
  posted('aaaaaaaaa3', '2026-10-31T14:05:00Z', { session: 'c1', repository: 'game', agent: 'claude-code', channel: 'session:c1', text: 'Both lanes are on the new contract.' }),
];

/** The ask's channel: a message a line of work's part posted, and the person's. */
export const ASK_SAID: ChannelItem[] = [
  posted('m1', '2026-10-31T14:01:00Z', { person: true, text: 'Start with the index, then the notes.' }),
  posted('m2', '2026-10-31T14:03:00Z', { entry: 'ask:a1/driver', session: 's2', repository: 'driver', agent: 'claude-code', text: 'The core lane is done; the index needs a second look.' }),
];

// The hint, where two subagents ran and the way to hand rows on is not open.

/** Two subagents working in one generation, and the standing the driver's judge answered. */
export const HINTED = (hint: AtWorkHint): AtWorkHeld => answerOf({ count: 2, working: 2 }, { hint });

export const EMPTY = answerOf({ count: 0, working: 0 });

/** An attended teammate's part: its subagents not visible on this machine. */
export const ELSEWHERE = answerOf({ elsewhere: true });

/** A part whose lineage could not be read. */
export const UNREADABLE = answerOf({ count: 0, working: 0, unreadable: true });

export const STALE: AtWorkHeld = { ...(THREE as { answer: AtWorkA }), stale: { why: 'the service timed out' } };

export const FAILED: AtWorkHeld = { failed: { why: 'the sessions could not be read' } };

export const READING: AtWorkHeld = { reading: true };
