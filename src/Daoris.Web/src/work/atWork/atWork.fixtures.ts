import type { Consideration } from '../../data/signals';
import type { ChannelItem } from '../channel';
import type { AtWorkA, AtWorkAsker, AtWorkChild, AtWorkHeld, AtWorkHint, AtWorkPart, AtWorkSubagents } from './types';

// Who is at work (COWORKVIEW1a): the answers the view's stories and tests draw, each a state the cowork view design §5.3 names.

const AT = '2026-10-31T14:02:00Z';
const LATER = '2026-10-31T14:05:00Z';

export const child = (key: string, state: AtWorkChild['state'], extra: Partial<AtWorkChild> = {}): AtWorkChild => ({
  key, state, startSaid: true, since: AT, depth: 1, parentKey: 'root', type: 'general-purpose', ...extra,
});

const subagents = (children: AtWorkChild[], extra: Partial<AtWorkSubagents> = {}): AtWorkSubagents => ({
  read: 'read', harness: 'claude-code', working: children.filter((each) => each.state === 'working').length, children, ...extra,
});

export const answerOf = (sub: AtWorkSubagents, extra: Partial<AtWorkA> = {}): AtWorkHeld => ({
  answer: {
    of: { session: 's1' }, attended: 's1',
    parts: [{ key: 's1', agent: 'claude-code', state: 'working', children: { count: sub.children.length, working: sub.working } }],
    subagents: sub,
    ...extra,
  },
  at: { epoch: 'e1', n: 1 },
});

/** A chat with three subagents: one working, one completed, one failed. */
export const THREE = answerOf(subagents([
  child('a1', 'working', { label: { text: 'Read the parser', claim: true } }),
  child('a2', 'completed', { ended: AT }),
  child('a3', 'failed', { ended: LATER }),
]));

/** On the protocol door, its turn waiting for the child working. */
export const TURN_WAITS = answerOf({ ...subagents([child('a1', 'working')]), turnWaits: true });

/** A nested child whose parent no harness said. */
export const NO_PARENT = answerOf(subagents([child('a1', 'working'), child('a9', 'working', { depth: undefined, parentKey: undefined })]));

/** A Codex lead: one child first seen, its end not said; and one with no lineage yet. */
export const CODEX = answerOf({ ...subagents([child('t-1', 'unsaid', { startSaid: false })], { harness: 'codex' }), seenByCalls: true });
export const CODEX_NONE = answerOf({ read: 'none', working: 0, children: [], seenByCalls: true });

/** A lost child, and one whose agent's exit this window saw before its end said how. */
export const LOST = answerOf(subagents([
  child('a1', 'lost', { ended: LATER }),
  child('a2', 'agentEnded', { lastSeen: LATER }),
]));

/** Not known whether it still runs: another Daoris process's run, beside one this window watches and one lost. */
export const UNCERTAIN = answerOf(subagents([
  child('a1', 'working'),
  child('a2', 'uncertain', { lastSeen: LATER }),
  child('a3', 'lost', { ended: LATER }),
]));

/** Agent ids alone: unmatched and not tied to a process. */
export const IDS_ONLY = answerOf({ read: 'read', working: 0, children: [], observed: 2, unplaced: 2 });

/** A driven record's history: one child whose end was not recorded beside one its harness ended. */
export const HISTORY = answerOf(subagents([
  child('a1', 'unrecorded', { lastSeen: LATER }),
  child('a2', 'completed', { ended: AT }),
  child('a3', 'finished', { ended: AT }),
  child('a4', 'stopped', { ended: AT }),
]));

/** An ended first process beside its working continuation. */
export const CONTINUED = answerOf(subagents([child('a1', 'ended', { ended: AT }), child('a2', 'working')]));

/** The bound: forty-one working, forty drawn, one more not drawn; and three ended folded. */
export const BOUND = answerOf({
  ...subagents(Array.from({ length: 40 }, (_, i) => child(`w${i}`, 'working'))),
  working: 41, more: { working: 1 }, folded: 3,
});

/** An entry of three parts, the attended first, one a teammate's. */
export const PARTS = answerOf(subagents([child('a1', 'working')]), {
  parts: [
    { key: 's1', agent: 'claude-code', state: 'working', children: { count: 1, working: 1 } },
    { key: 's0', agent: 'codex', state: 'completed', children: { count: 2, working: 0 } },
    { key: 'origin/s7', state: 'working', children: { elsewhere: true } },
  ],
  moreParts: { completed: 2 },
});

const quiet: AtWorkSubagents = { read: 'none', working: 0, children: [] };

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
]);

/** A lead waiting on its helper, which the strip words *awaiting helper*. */
export const LEAD_WAITING_ON_HELPER = routed([
  routedPart('s1', 1, { state: 'completed', waits: { helper: true } }),
  routedPart('s2', 2, { state: 'completed', joined: { began: 'retried', after: 1 } }),
]);

/** A chat that took a quest and waits on the question the quest asked. */
export const CHAT_TOOK_A_QUEST = routed([
  routedPart('c1', 1, { state: 'completed', joined: { began: 'opened', quest: 'q1', took: true, of: 'driver' }, waits: { quest: 'q9', of: 'docs' } }),
]);

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

/** Parts that joined by each of §3.4's other doors. */
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

// COWORKVIEW1b-ii: the quests a part asked, and what asked it.

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

// COWORKVIEW1d: an ask's view, a branch of its own.

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

// COWORKVIEW1c: the channel's last words.

const posted = (id: string, at: string, over: Partial<ChannelItem>): ChannelItem => ({ kind: 'message', id, at, ...over });

/** The newest three messages of the channel the page holds: the person's, a lane part's, and the lead's own. */
export const SAID: ChannelItem[] = [
  posted('m1', '2026-10-31T14:01:00Z', { person: true, text: 'Start with the index, then the notes.' }),
  posted('m2', '2026-10-31T14:03:00Z', { session: 's2', repository: 'driver', agent: 'claude-code', text: 'The core lane is done; the index needs a second look before the notes go in.' }),
  posted('m3', '2026-10-31T14:04:00Z', { session: 'c1', repository: 'game', agent: 'claude-code', text: 'Waiting on the docs lane.' }),
];

/** The ask's channel: a message a line of work's part posted, and the person's. */
export const ASK_SAID: ChannelItem[] = [
  posted('m1', '2026-10-31T14:01:00Z', { person: true, text: 'Start with the index, then the notes.' }),
  posted('m2', '2026-10-31T14:03:00Z', { entry: 'ask:a1/driver', session: 's2', repository: 'driver', agent: 'claude-code', text: 'The core lane is done; the index needs a second look.' }),
];

// COWORKVIEW1e-iii: the hint, where two subagents ran and the way to hand rows on is not open.

/** Two subagents working in one generation, and the standing the driver's judge answered. */
export const HINTED = (hint: AtWorkHint): AtWorkHeld => answerOf(
  subagents([child('a1', 'working', { label: { text: 'Read the parser', claim: true } }), child('a2', 'working')]),
  { hint },
);

export const EMPTY =answerOf({ read: 'none', working: 0, children: [] });

export const ELSEWHERE = answerOf({ read: 'elsewhere', working: 0, children: [] });

export const UNREADABLE = answerOf({ read: 'unreadable', unreadable: 'IOException', working: 0, children: [] });

export const STALE: AtWorkHeld = { ...(THREE as { answer: AtWorkA }), stale: { why: 'the service timed out' } };

export const FAILED: AtWorkHeld = { failed: { why: 'the sessions could not be read' } };

export const READING: AtWorkHeld = { reading: true };

/** The clock the fixtures' times are read against: the same day. */
export const NOW = new Date('2026-10-31T15:00:00Z');
