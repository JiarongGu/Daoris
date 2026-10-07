import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import type { Ask, GoAhead, Quest, Registration, Session } from '../api';
import type { RuleProposal } from '../settings/AgentRules';
import type { Consideration } from '../signals';
import { byTool } from '../tools';
import type { AccountRowFacts, NamedAccount } from './accountAttention';
import type { Attention } from './AttentionRow';
import type { SessionGrouping } from './groups';
import {
  ASKS_ONCE, ATTENTION_GROUP, attentionActs, attentionGroups, attentionOffers, needsAPerson, waitingInSessions,
} from './attention';

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  kind: 'driven',
  created: '2026-09-21T09:00:00Z',
  updated: '2026-09-21T10:00:00Z',
  ...over,
});

const quest = (over: Partial<Quest> = {}): Quest => ({
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs a per-frame cap.',
  status: 'Open',
  filed: '2026-09-14T09:00:00Z',
  updated: '2026-09-14T09:00:00Z',
  ...over,
});

const registration = (repository: string): Registration => ({
  repository, adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 1,
});

/** An ask the declarations tier proposed and nobody has settled (INT4a). */
const ask = (over: Partial<Ask> = {}): Ask => ({
  id: '7c1e9a04b2d5',
  workspace: 'aurora',
  sentence: 'The chunk streamer stalls on a cold cache.\n\nThe trace is attached.',
  state: 'Proposed',
  tier: 'declarations',
  asked: '2026-09-21T08:30:00Z',
  updated: '2026-09-21T08:30:00Z',
  links: [],
  attachments: [],
  proposal: [
    { repository: 'engine', score: 5, matched: ['chunk', 'stream'] },
    { repository: 'game', score: 2, matched: ['cache'] },
  ],
  quests: [],
  ...over,
});

/** The session an intake opened for the ask above (D65 §1b): a chat, in the ask's name. */
const intake = (over: Partial<Session> = {}): Session => session({
  id: 'i9n8t7k6', repository: 'ask #7c1e9a04b2d5', kind: 'chat', adapter: 'stub', ask: '7c1e9a04b2d5',
  ...over,
});

describe('what needs a person', () => {
  it('is empty when nothing is parked and every open quest has a receiver', () => {
    expect(needsAPerson([session()], [quest()], [registration('engine')], [])).toEqual([]);
  });

  it('names a parked session by its derived identity, with the analysis it is waiting on', () => {
    const waiting = needsAPerson(
      [session({ state: 'awaiting-person', quest: '7a82cc', note: 'two ways forward.' })],
      [quest({ status: 'Taken' })],
      [registration('engine')],
      [],
    );

    expect(waiting).toEqual([{
      id: 's1a2b3c4',
      kind: 'parked',
      title: 'Expose a streaming budget on the chunk API',
      where: 'engine',
      since: '2026-09-21T10:00:00Z',
      note: { note: 'two ways forward.' },
    }]);
  });

  /** LANG1b: the record's note travels whole, its parts beside its English, for the row to word in the reader's language. */
  it('hands the row a parked session’s note with its parts, never an English sentence made of them', () => {
    const noteParts = [{ code: 'ended.parked-asked', values: {}, text: 'It stopped with its quest still taken, to ask you:' }];
    const [item] = needsAPerson(
      [session({ state: 'awaiting-person', quest: '7a82cc', note: 'It stopped…', noteParts })],
      [quest({ status: 'Taken' })], [registration('engine')], [],
    );
    expect(item.note).toEqual({ note: 'It stopped…', parts: noteParts });
    expect(item.detail).toBeUndefined();
  });

  /**
   * The publish door refuses a quest addressed to a non-adopter, so one only comes to exist
   * afterwards — the receiver retired, or its registration never reached this deployment. Either
   * way no agent will ever pull it, and nothing else in the platform says so.
   */
  it('names an open quest addressed to a repository this deployment does not hold', () => {
    const waiting = needsAPerson([], [quest({ to: 'retired' })], [registration('engine')], []);

    expect(waiting).toHaveLength(1);
    expect(waiting[0]).toMatchObject({ kind: 'unanswerable', where: 'retired', id: '7a82cc' });
    expect(waiting[0].detail).toContain('retired');
  });

  it('matches a receiver whatever its case — a registry row is not a string comparison', () => {
    expect(needsAPerson([], [quest({ to: 'Engine' })], [registration('engine')], [])).toEqual([]);
  });

  it('leaves a quest somebody already took out of it — it is not waiting on anyone here', () => {
    expect(needsAPerson([], [quest({ to: 'retired', status: 'Taken' })], [], [])).toEqual([]);
  });

  /** A parked session is holding a working tree while it waits; a sitting quest is holding nothing. */
  it('puts parked sessions ahead of quests, and the oldest first within each', () => {
    const waiting = needsAPerson(
      [
        session({ id: 'newer', state: 'awaiting-person', updated: '2026-09-21T11:00:00Z' }),
        session({ id: 'older', state: 'awaiting-person', updated: '2026-09-21T08:00:00Z' }),
      ],
      [
        quest({ id: 'q-new', to: 'gone', filed: '2026-09-20T00:00:00Z' }),
        quest({ id: 'q-old', to: 'gone', filed: '2026-09-01T00:00:00Z' }),
      ],
      [registration('engine')],
      [],
    );

    expect(waiting.map((item) => item.id)).toEqual(['older', 'newer', 'q-old', 'q-new']);
  });

  it('explains an unanswerable quest in the active language', async () => {
    await i18n.changeLanguage('zh');
    const waiting = needsAPerson([], [quest({ to: 'retired' })], [], []);
    expect(waiting[0].detail).toContain('本部署');
    await i18n.changeLanguage('en');
  });
});

/**
 * An ask waits on a person (INT4d): a proposal only a person publishes (INT4a), or an intake that could
 * not settle it and asked (D65 §1b). Its place is its circle — an ask has no repository until it
 * becomes quests.
 */
describe('an ask that waits on a person', () => {
  it('is a proposal while nothing serves it: its first line, its circle, since it was asked, and what was proposed', () => {
    const waiting = needsAPerson([], [], [registration('engine')], [ask()]);

    expect(waiting).toEqual([{
      id: '7c1e9a04b2d5',
      kind: 'proposal',
      title: 'The chunk streamer stalls on a cold cache.',
      where: 'aurora',
      since: '2026-09-21T08:30:00Z',
      detail: 'The declarations propose engine, game. Nothing is published until you choose.',
      publishTo: ['engine', 'game'],
      choices: [],
    }]);
  });

  it('carries the refused receiver\'s sentence verbatim rather than the proposal', () => {
    const note = '`newcomer` is not an adopted repository in `aurora` — only an adopter can be asked.';
    const [item] = needsAPerson([], [], [], [ask({ note })]);
    expect(item.detail).toBe(note);
  });

  it('says so when the declarations proposed nobody', () => {
    const [item] = needsAPerson([], [], [], [ask({ proposal: [] })]);
    expect(item.detail).toMatch(/No repository's declarations share its words/);
  });

  it('is the harness\'s, not the person\'s, while its intake is queued, starting or working', () => {
    for (const state of ['queued', 'starting', 'working'] as const) {
      expect(needsAPerson([intake({ state })], [], [], [ask({ intake: 'i9n8t7k6' })])).toEqual([]);
    }
  });

  /**
   * The parked intake is a session awaiting a person AND the ask is waiting — one thing, and it is
   * counted once. The answer is on the ask (publish or close), so the ask's row stands for both.
   */
  it('whose intake parked asking is ONE row — the ask\'s, since it parked, with the intake\'s own note', () => {
    const note = 'published nothing: the declarations did not settle ask `#7c1e9a04b2d5`, so it asks you rather than guess.';
    const waiting = needsAPerson(
      [intake({ state: 'awaiting-person', updated: '2026-09-21T09:45:00Z', note })],
      [], [], [ask({ intake: 'i9n8t7k6' })],
    );

    expect(waiting).toEqual([{
      id: '7c1e9a04b2d5',
      kind: 'intake',
      title: 'The chunk streamer stalls on a cold cache.',
      where: 'aurora',
      since: '2026-09-21T09:45:00Z',
      note: { note },
    }]);
  });

  it('keeps a parked intake as a parked session when its ask is not in hand — nothing waiting is dropped', () => {
    const waiting = needsAPerson([intake({ state: 'awaiting-person' })], [], [], []);

    expect(waiting).toHaveLength(1);
    expect(waiting[0]).toMatchObject({ kind: 'parked', id: 'i9n8t7k6' });
  });

  it('is a proposal again once its intake ended without publishing — the proposal is still the person\'s', () => {
    // An ended session is not in the live list: the ask names an intake the list no longer holds.
    const [item] = needsAPerson([], [], [], [ask({ intake: 'i9n8t7k6' })]);
    expect(item).toMatchObject({ kind: 'proposal', id: '7c1e9a04b2d5' });
  });

  it('leaves an ask that became quests, or was closed, out of it', () => {
    expect(needsAPerson([], [], [], [
      ask({ state: 'Published', quests: ['9a8b7c6d5e4f'] }),
      ask({ id: 'closed', state: 'Closed', note: 'Answered elsewhere.' }),
    ])).toEqual([]);
  });

  /**
   * UX6c (design §6.2): an ask waits for the person's word, after what holds work, and within that group what has waited
   * longest comes first, whatever its kind: a quest nobody can take, sitting since September, before this morning's asks.
   */
  it('waits for the person’s word after what holds work, the longest waiting first within the group', () => {
    const waiting = needsAPerson(
      [
        session({ id: 'parked', state: 'awaiting-person', updated: '2026-09-21T11:00:00Z' }),
        intake({ state: 'awaiting-person', updated: '2026-09-21T07:00:00Z' }),
      ],
      [quest({ id: 'sitting', to: 'gone', filed: '2026-09-01T00:00:00Z' })],
      [registration('engine')],
      [
        ask({ id: 'later', asked: '2026-09-21T10:00:00Z' }),
        ask({ intake: 'i9n8t7k6' }),
      ],
    );

    expect(waiting.map((item) => `${item.kind}:${item.id}`)).toEqual([
      'parked:parked', 'unanswerable:sitting', 'intake:7c1e9a04b2d5', 'proposal:later',
    ]);
  });

  /**
   * UX6c (design §6.2): a proposal's row publishes where its declarations named receivers, in one press, and chooses among
   * whoever its workspace can ask otherwise. Both are read from the facts the band already holds: the ask and the registry.
   */
  it('names whom its publish goes to and whom it could choose, from the ask and the registry', () => {
    const inAurora = (name: string, over: Partial<Registration> = {}): Registration => ({ ...registration(name), workspace: 'aurora', ...over });
    const [item] = needsAPerson([], [], [
      inAurora('game'), inAurora('engine'), inAurora('tools', { adopted: false }), registration('elsewhere'),
    ], [ask()]);

    expect(item.publishTo).toEqual(['engine', 'game']);
    expect(item.choices).toEqual(['engine', 'game']);
    const [nobody] = needsAPerson([], [], [inAurora('engine')], [ask({ proposal: [] })]);
    expect(nobody.publishTo).toEqual([]);
    expect(nobody.choices).toEqual(['engine']);
  });

  it('explains a proposal in the active language', async () => {
    await i18n.changeLanguage('zh');
    const [item] = needsAPerson([], [], [], [ask()]);
    expect(item.detail).toContain('engine, game');
    expect(item.detail).not.toContain('declarations');
    await i18n.changeLanguage('en');
  });
});

/**
 * A folder the agent has not been trusted in (D73). The driver holds rather than spend a session that
 * could not take its quest, and only the person can give the grant, so it waits on them. It is one
 * row per folder, in the file the hold read.
 */
describe('a folder waiting on the person\'s trust', () => {
  const hold = { folder: 'C:/somewhere/engine', trustFile: 'C:/somewhere/data/harnesses/claude-code/work/.claude.json' };

  it('is one row per folder, naming the folder and what it holds, since the oldest thing it holds', () => {
    const waiting = needsAPerson(
      [], [quest({ id: 'q1', filed: '2026-09-14T09:00:00Z' }), quest({ id: 'q2', filed: '2026-09-10T09:00:00Z' })],
      [registration('engine')], [],
      [{ ...hold, quest: 'q1' }, { ...hold, quest: 'q2' }],
    );

    expect(waiting).toHaveLength(1);
    const [item] = waiting;
    expect(item.kind).toBe('trust');
    expect(item.title).toBe('C:/somewhere/engine');
    expect(item.where).toBe('engine');
    expect(item.since).toBe('2026-09-10T09:00:00Z');
    expect(item.trust).toEqual(hold);
    expect(item.detail).toContain('permissions.allow');
  });

  /**
   * REV3 web-work F12: one folder held in two accounts' files is two grants, and two rows — and each
   * row needs an id of its own, because the band keys its rows by kind and id. Both were `folder`.
   */
  it('gives one folder held in two files two rows with ids of their own', () => {
    const other = { ...hold, trustFile: 'C:/somewhere/data/harnesses/claude-code/personal/.claude.json' };

    const rows = needsAPerson([], [quest()], [registration('engine')], [], [hold, other])
      .filter((row) => row.kind === 'trust');

    expect(rows).toHaveLength(2);
    expect(new Set(rows.map((row) => row.id)).size).toBe(2);
  });

  it('names an intake\'s room by its ask, since the ask was asked', () => {
    const room = { folder: 'C:/somewhere/data/intake/aurora', trustFile: hold.trustFile, ask: '7c1e9a04b2d5' };

    const [item] = needsAPerson([], [], [], [ask()], [room]).filter((row) => row.kind === 'trust');

    expect(item.where).toBe('ask #7c1e9a04b2d5');
    expect(item.since).toBe('2026-09-21T08:30:00Z');
  });

  /**
   * Nothing it holds can start until it is granted, so it holds work, ahead of every ask; beside a parked session, the one
   * that has waited longer comes first (UX6c, design §6.2).
   */
  it('holds work, ahead of the asks, beside the parked sessions by how long each has waited', () => {
    const waiting = needsAPerson(
      [session({ id: 'parked', state: 'awaiting-person', updated: '2026-09-21T09:00:00Z' })],
      [quest({ id: 'q1' })], [registration('engine')], [ask()],
      [{ ...hold, quest: 'q1' }],
    );

    expect(waiting.map((item) => item.kind)).toEqual(['trust', 'parked', 'proposal']);
  });

  /** Its door is what it holds: the quest's page, or the ask's for an intake's room. */
  it('names the quest or the ask it holds, which its door opens', () => {
    const [onQuest] = needsAPerson([], [quest({ id: 'q1' })], [registration('engine')], [], [{ ...hold, quest: 'q1' }]);
    expect(onQuest).toMatchObject({ quest: 'q1' });
    const [onAsk] = needsAPerson([], [], [], [ask()], [{ ...hold, ask: '7c1e9a04b2d5' }]).filter((row) => row.kind === 'trust');
    expect(onAsk).toMatchObject({ ask: '7c1e9a04b2d5' });
  });

  it('adds nothing when nothing is held for trust, and a browser has no holds at all', () => {
    expect(needsAPerson([], [quest()], [registration('engine')], [], [])).toEqual([]);
    expect(needsAPerson([], [quest()], [registration('engine')], [])).toEqual([]);
  });
});

/**
 * An agent's proposal to widen what agents may do (PERM2, D74). 🔴 A widening never applies without
 * the person, so one the driver is holding waits on them. A narrowing applied itself, and a settled
 * proposal is history: neither is here.
 */
describe('a proposal to widen the rules', () => {
  const proposal = (over: Partial<RuleProposal> = {}): RuleProposal => ({
    id: 'p0000002', state: 'waiting', action: 'add', scope: 'machine', list: 'allow', rule: 'WebFetch',
    why: 'The docs it needs are on the web.', session: 'i9n8t7k6', ask: 'a1b2c3', proposed: '2026-09-24T11:00:00Z',
    ...over,
  });

  it('is a row saying what it would change, who proposed it and why, since it was proposed', () => {
    const [item] = needsAPerson([], [], [], [], [], [proposal()]);

    expect(item.kind).toBe('rule');
    expect(item.id).toBe('p0000002');
    expect(item.title).toBe('allow WebFetch for every session on this machine');
    expect(item.where).toBe('session i9n8t7k6 (ask #a1b2c3)');
    expect(item.detail).toBe('The docs it needs are on the web.');
    expect(item.since).toBe('2026-09-24T11:00:00Z');
  });

  it('leaves out what applied itself, what the driver has not judged yet, and what was settled', () => {
    const settled = ['applied', 'proposed', 'accepted', 'declined', 'refused', 'unchanged'] as const;
    expect(needsAPerson([], [], [], [], [], settled.map((state) => proposal({ id: state, state })))).toEqual([]);
  });

  /** It waits for the person's word beside the asks and the quests nobody can take, the longest waiting first (UX6c). */
  it('waits for the person’s word, with the asks and the quests nobody can take, the longest waiting first', () => {
    const waiting = needsAPerson(
      [], [quest({ to: 'nobody' })], [registration('engine')], [ask()], [], [proposal()]);

    expect(waiting.map((item) => item.kind)).toEqual(['unanswerable', 'proposal', 'rule']);
  });
});

/**
 * SESSUX1i (D126 §4.6): a quest parked on its failed sessions here. On 1 October the owner's work stood exactly there,
 * and *What needs you* did not say so: the one press that moved it was on the quest's page. The row is read from the
 * planner's verdicts the tick hands the page, so a browser, which has no driver, has none.
 */
describe('a quest parked on its failed sessions', () => {
  const parked = (over: Partial<Consideration> = {}): Consideration => ({
    quest: '7a82cc', repository: 'engine', verdict: 'Exhausted', strikes: 3, since: '2026-10-01T09:21:00+00:00',
    reason: '3 session(s) have failed on `#7a82cc` without landing anything — parked, because trying again spends an account rather than making progress. `daoris driver retry 7a82cc` starts it again once you know why.',
    ...over,
  });

  it('is a row: the quest, where it is addressed, since its last session ended, and why it sits in the driver’s words', () => {
    const waiting = needsAPerson([], [quest({ status: 'Taken' })], [registration('engine')], [], [], [], [parked()]);

    expect(waiting).toEqual([{
      id: '7a82cc',
      kind: 'parked-quest',
      title: 'Expose a streaming budget on the chunk API',
      where: 'engine',
      since: '2026-10-01T09:21:00+00:00',
      detail: parked().reason,
    }]);
  });

  /**
   * It holds work, as a parked session and a folder held for trust do, and within that group what has waited longest comes
   * first (UX6c, design §6.2): the folder held since its quest was filed, then the quests by when each parked, then the
   * session that parked this noon.
   */
  it('holds work beside the parked sessions and the folders held for trust, the longest waiting first', () => {
    const waiting = needsAPerson(
      [session({ id: 'asking', state: 'awaiting-person', updated: '2026-10-01T12:00:00Z' })],
      [quest({ id: 'q-new', status: 'Taken' }), quest({ id: 'q-old' })],
      [registration('engine')], [],
      [{ folder: 'C:/somewhere/engine', trustFile: 'C:/somewhere/.claude.json', quest: 'q-new' }], [],
      [parked({ quest: 'q-new', since: '2026-10-01T10:00:00+00:00' }), parked({ quest: 'q-old', since: '2026-09-30T10:00:00+00:00' })],
    );

    expect(waiting.map((item) => `${item.kind}:${item.id}`)).toEqual([
      `trust:C:/somewhere/.claude.json\nC:/somewhere/engine`, 'parked-quest:q-old', 'parked-quest:q-new', 'parked:asking',
    ]);
  });

  /**
   * Only the planner's park is the person's to press. A stop holds its quest too, and the person caused it; every other
   * verdict is a wait or a hold the row does not answer.
   */
  it('is only the planner’s park, never a stop’s hold or a wait', () => {
    const waiting = needsAPerson([], [quest()], [registration('engine')], [], [], [], [
      parked({ verdict: 'Stopped', heldBy: 's1a2b3c4' }), parked({ verdict: 'RepositoryBusy' }), parked({ verdict: 'Start' }),
    ]);

    expect(waiting).toEqual([]);
  });

  it('is absent in a browser, where no driver says what it parked', () => {
    expect(needsAPerson([], [quest()], [registration('engine')], [])).toEqual([]);
  });

  /**
   * A shell older than the fact names no time, and a quest a scope does not list has no title in hand: the row still
   * says what waits, waiting since the quest's last move and named by its id.
   */
  it('still says what waits where the tick names no time and the quest is not in hand', () => {
    const [named] = needsAPerson([], [quest({ updated: '2026-09-30T08:00:00Z' })], [registration('engine')], [], [], [],
      [parked({ since: undefined, strikes: undefined })]);
    expect(named.since).toBe('2026-09-30T08:00:00Z');

    const [unnamed] = needsAPerson([], [], [], [], [], [], [parked()]);
    expect(unnamed.title).toBe('#7a82cc');
  });

  /** Why it sits, in the reader's language, by the verdict and the number the tick carries (never its English). */
  it('says why it sits in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      const [item] = needsAPerson([], [quest()], [registration('engine')], [], [], [], [parked()]);
      expect(item.detail).toMatch(/3 个会话/);
      expect(item.detail).toContain('`daoris driver retry 7a82cc`');
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});

/**
 * 🔴 A badge counts what its place holds (UX5 U20, the owner's choice). The Sessions icon carried
 * the whole of *What needs you*, six on the scratch window, and Sessions held one of them: the asks
 * are on Quests and in Overview's band. Overview carries the whole now, beside the band that lists
 * it, and Sessions counts only its own sessions waiting on the person.
 */
describe('what the Sessions badge counts', () => {
  it('counts the sessions waiting on the person, a parked intake among them, and nothing else the band holds', () => {
    const sessions = [
      session({ id: 'c4a7c4a7', kind: 'chat', state: 'awaiting-person' }),
      session({ id: '1n7a4e00', ask: '7c1e9a04b2d5', state: 'awaiting-person' }),
      session({ id: 'b05y0000', state: 'working' }),
      session({ id: 'd0ne0000', state: 'completed' }),
    ];

    expect(waitingInSessions(sessions)).toBe(2);
    // The band counts more than Sessions holds: the ask a parked intake stands for, and a quest
    // nobody can take, are both the person's and neither is a session.
    const band = needsAPerson(
      sessions, [quest({ to: 'nobody' })], [registration('engine')], [ask({ intake: '1n7a4e00' })]);
    expect(band.length).toBeGreaterThan(waitingInSessions(sessions));
  });

  /**
   * D126 §2.5: the badge counts the list's first group, *Waiting on you*: a session parked to ask, and a quest parked on
   * its failed sessions, whose last session here the list shows *parked*. Read from the planner's verdicts the tick
   * hands the page, as Overview's band reads them, so both counts read the same two facts and no view off Sessions walks
   * git in every tree to count it.
   */
  it('counts a quest parked on its failed sessions, and nothing else the driver is holding', () => {
    const sessions = [
      session({ id: 'c4a7c4a7', kind: 'chat', state: 'awaiting-person' }),
      session({ id: 'f41led00', quest: 'q1', state: 'failed' }),
    ];
    const considered = [
      { quest: 'q1', repository: 'engine', verdict: 'Exhausted', reason: '3 failed sessions' },
      { quest: 'q2', repository: 'engine', verdict: 'Busy', reason: 'engine is busy' },
      { quest: 'q3', repository: 'game', verdict: 'Start', reason: '' },
    ];

    expect(waitingInSessions(sessions, considered)).toBe(2);
    expect(waitingInSessions(sessions, [])).toBe(1);
  });

  /** A teammate's session parked to ask waits on them, and is listed under Working (SESSUX1a): not the person's to count. */
  it("leaves a teammate's parked session to its own machine", () => {
    expect(waitingInSessions([session({ id: 'laptop/c4a7c4a7', kind: 'chat', state: 'awaiting-person' })])).toBe(0);
  });

  /**
   * ANSWER1c (D131): a park the person answered goes on at the driver's next look, and the list shows it under Working,
   * so neither the badge nor the band counts it while its record is still parked.
   */
  it('counts no park the person has answered, and the band lists none', () => {
    const sessions = [
      session({ id: 'p4rk3d00', quest: '7a82cc', state: 'awaiting-person' }),
      session({ id: 'an5wered', quest: '7a82cd', state: 'awaiting-person', answer: 'Use the second.' }),
    ];

    expect(waitingInSessions(sessions)).toBe(1);
    expect(needsAPerson(sessions, [], [], []).map((item) => item.id)).toEqual(['p4rk3d00']);
  });
});

/**
 * UX6c (design §6.2): a go-ahead a session asked on an ask (KNOWUSE1a) holds that session's act until the person answers,
 * so it holds work. Read from the ask's own `goAheads`, which the asks list already carries: nothing is asked to find it.
 */
describe('a go-ahead asked on an ask', () => {
  const goAhead = (over: Partial<GoAhead> = {}): GoAhead => ({
    number: 1, kind: 'push', on: 'prod', act: 'the report’s menu entries', state: 'asked',
    asked: [{ session: 's1a2b3c4', quest: '7a82cc', at: '2026-09-21T09:10:00Z', why: 'The menu ships with the release.' }],
    ...over,
  });

  it('is a row per waiting go-ahead: the act, its ask, since it was first asked, and why in the session’s words', () => {
    const waiting = needsAPerson([], [], [], [ask({ state: 'Published', goAheads: [goAhead()] })]);

    expect(waiting).toEqual([{
      id: '7c1e9a04b2d5#1',
      kind: 'go-ahead',
      ask: '7c1e9a04b2d5',
      number: 1,
      title: 'push on prod: “the report’s menu entries”',
      where: 'ask #7c1e9a04b2d5',
      since: '2026-09-21T09:10:00Z',
      detail: 'The menu ships with the release.',
    }]);
  });

  it('leaves one answered out, and one on a closed ask', () => {
    expect(needsAPerson([], [], [], [
      ask({ state: 'Published', goAheads: [goAhead({ state: 'approved', answer: { approved: true, at: '2026-09-21T10:00:00Z' } })] }),
      ask({ id: 'closed', state: 'Closed', goAheads: [goAhead()] }),
    ])).toEqual([]);
  });

  /** A kind this page has no word for is shown as the service wrote it; one it has is said in the reader's language. */
  it('names the act’s kind in the reader’s language where it can, and as written where it cannot', async () => {
    const [unknown] = needsAPerson([], [], [], [ask({ state: 'Published', goAheads: [goAhead({ kind: 'migrate' })] })]);
    expect(unknown.title).toBe('migrate on prod: “the report’s menu entries”');
    await i18n.changeLanguage('zh');
    try {
      const [item] = needsAPerson([], [], [], [ask({ state: 'Published', goAheads: [goAhead()] })]);
      expect(item.title).toBe('在 prod 上推送：“the report’s menu entries”');
      expect(item.where).toBe('需求 #7c1e9a04b2d5');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('holds work: after a parked session that waited longer, before every ask', () => {
    const waiting = needsAPerson(
      [session({ id: 'parked', state: 'awaiting-person', updated: '2026-09-21T09:00:00Z' })], [], [],
      [ask({ id: 'proposed', asked: '2026-09-20T09:00:00Z' }), ask({ state: 'Published', goAheads: [goAhead()] })],
    );
    expect(waiting.map((item) => item.kind)).toEqual(['parked', 'go-ahead', 'proposal']);
  });
});

/**
 * UX6c (design §6.2): a done that departed from what the person required is held for their yes (DRIFT1d2), so it waits for
 * their word. The quests list carries it (`held`), and the requirement and the departure's reason are the done's words.
 */
describe('a departure that waits for your yes', () => {
  const held = (over: Partial<Quest> = {}): Quest => quest({
    status: 'Done', held: true, updated: '2026-09-22T09:00:00Z',
    requirements: [{ quote: 'cap it per frame', check: 'a test' }, { quote: 'on the chunk API', check: 'the API' }],
    answers: [
      { requirement: 1, met: 'tested' },
      { requirement: 2, departed: 'the scheduler holds the budget instead', quote: 'on the chunk API' },
    ],
    ...over,
  });

  it('is a row: the quest, its receiver, since its done, and the departed requirement with its reason', () => {
    expect(needsAPerson([], [held()], [registration('engine')], [])).toEqual([{
      id: '7a82cc',
      kind: 'departure',
      title: 'Expose a streaming budget on the chunk API',
      where: 'engine',
      since: '2026-09-22T09:00:00Z',
      detail: 'Requirement 2 departed: the scheduler holds the budget instead',
    }]);
  });

  it('counts the departures where its done departed from more than one', () => {
    const [item] = needsAPerson([], [held({
      answers: [
        { requirement: 1, departed: 'no per-frame cap', quote: 'cap it per frame' },
        { requirement: 2, departed: 'the scheduler holds it', quote: 'on the chunk API' },
      ],
    })], [registration('engine')], []);
    expect(item.detail).toBe('2 requirements departed; requirement 1: no per-frame cap');
  });

  it('is gone once accepted, and absent for a quest no departure holds', () => {
    expect(needsAPerson([], [held({ accepted: '2026-09-22T10:00:00Z' }), quest({ status: 'Done' })], [registration('engine')], []))
      .toEqual([]);
  });
});

/**
 * UX6c (design §6.2): finished work the person has not looked at is ready for them. Sessions' list already places it in
 * *To review* (D126), from the driver's one reader the frame holds on every view, so the band reads that answer and never a
 * reader of its own. The foot sentence that said such work was not listed goes with it.
 */
describe('work to review', () => {
  const placed = (over: Partial<SessionGrouping> = {}): SessionGrouping => ({
    session: 'r3v13w00', group: 'review', shown: 'completed', archived: false, teammate: false,
    work: { commits: 3, uncommitted: 0 }, ...over,
  });

  it('is a row per session the list places to review: its identity, where, since it ended, and what its tree holds', () => {
    const waiting = needsAPerson(
      [session({ id: 'r3v13w00', quest: '7a82cc', state: 'completed', updated: '2026-09-21T11:00:00Z' })],
      [quest({ status: 'Done' })], [registration('engine')], [], [], [], [], [placed()],
    );

    expect(waiting).toEqual([{
      id: 'r3v13w00',
      kind: 'review',
      title: 'Expose a streaming budget on the chunk API',
      where: 'engine',
      since: '2026-09-21T11:00:00Z',
      detail: '3 commits to review',
    }]);
  });

  it('still says what waits where the record is not in hand, named by its id', () => {
    const [item] = needsAPerson([], [], [], [], [], [], [], [placed()]);
    expect(item).toMatchObject({ kind: 'review', title: '#r3v13w00', where: '' });
  });

  it('is only the list’s To review, and comes last: it holds nothing and waits on nobody', () => {
    const waiting = needsAPerson(
      [session({ id: 'parked', state: 'awaiting-person', updated: '2026-09-21T12:00:00Z' })], [], [], [], [], [], [],
      [placed(), placed({ session: 'w0rk1ng0', group: 'working' }), placed({ session: 'end3d000', group: 'ended' })],
    );
    expect(waiting.map((item) => `${item.kind}:${item.id}`)).toEqual(['parked:parked', 'review:r3v13w00']);
  });
});

/** UX6c (design §6.2): the three groups, in the order a person acts on them, and none shown empty. */
describe('the groups', () => {
  it('puts each kind in its group', () => {
    expect(ATTENTION_GROUP).toEqual({
      parked: 'holding', 'parked-quest': 'holding', 'go-ahead': 'holding', trust: 'holding',
      'account-wait': 'holding', 'signed-out': 'holding',
      proposal: 'word', intake: 'word', departure: 'word', rule: 'word', unanswerable: 'word',
      review: 'ready',
    });
  });

  it('splits the rows into the groups that hold any, in order, keeping each group’s order', () => {
    const rows = needsAPerson(
      [session({ id: 'parked', state: 'awaiting-person' })], [quest({ id: 'q1', to: 'gone' })], [registration('engine')], [],
      [], [], [], [{ session: 'r3v13w00', group: 'review', shown: 'completed', archived: false, teammate: false }],
    );
    expect(attentionGroups(rows).map(({ group, items }) => [group, items.map((item) => item.id)])).toEqual([
      ['holding', ['parked']], ['word', ['q1']], ['ready', ['r3v13w00']],
    ]);
    expect(attentionGroups([])).toEqual([]);
  });
});

/**
 * UX6c (design §6.3): an act is on a row only where one press is safe and the row says what it does; one that widens what
 * Daoris may do asks once; one that needs reading first is the row's door, never an act. One rule per kind, so every row
 * of a kind is offered the same acts.
 */
describe('what a row offers', () => {
  const row = (kind: Attention['kind'], over: Partial<Attention> = {}): Attention =>
    ({ id: 'x', kind, title: 't', where: 'w', since: '2026-09-21T09:00:00Z', ...over });

  it('publishes where its declarations named receivers, and chooses otherwise', () => {
    expect(attentionActs(row('proposal', { publishTo: ['engine'] }))).toEqual(['publish', 'choose']);
    expect(attentionActs(row('proposal', { publishTo: [] }))).toEqual(['choose']);
  });

  it('tries a parked quest again, accepts a departure, and answers a go-ahead, a folder and a widening', () => {
    expect(attentionActs(row('parked-quest'))).toEqual(['retry']);
    expect(attentionActs(row('departure'))).toEqual(['accept-departure']);
    expect(attentionActs(row('go-ahead'))).toEqual(['approve', 'refuse']);
    expect(attentionActs(row('trust', { trust: { folder: 'f', trustFile: 'g' } }))).toEqual(['trust']);
    expect(attentionActs(row('rule'))).toEqual(['accept-rule', 'decline-rule']);
  });

  /** Answering a park or an intake's question, and accepting a review, need reading: each is its door alone. */
  it('offers nothing where reading comes first, or where nothing here can act', () => {
    for (const kind of ['parked', 'intake', 'review', 'unanswerable'] as const) expect(attentionActs(row(kind))).toEqual([]);
    expect(attentionActs(row('trust'))).toEqual([]);
  });

  it('asks once before what widens what Daoris may do, and before a choice', () => {
    expect([...ASKS_ONCE].sort()).toEqual(['accept-rule', 'approve', 'choose', 'let-run', 'refuse', 'trust']);
  });
});

/**
 * UX6d (design §6.2–§6.3): an account's row offers what settles it in one press, *Sign in* to an account read signed out and
 * *Read* for one no read answered, and *Let … run …* (D130 §3.3), which asks once since it widens what Daoris may spend. Two
 * at most beside the door, so a row holds three controls (§9.3).
 */
describe('what an account’s row offers', () => {
  const named = (id: string | null, state: NamedAccount['state'], read: string | null = '2026-10-05T10:42:00Z'): NamedAccount =>
    ({ id, label: id ?? 'Your own sign-in', state, read, until: null });
  const facts = (over: Partial<AccountRowFacts> = {}): AccountRowFacts => ({
    agent: 'claude-code', product: 'Claude Code', harness: 'claude-code', signsIn: true, named: [], outside: null, ...over,
  });
  const row = (kind: 'account-wait' | 'signed-out', account: AccountRowFacts): Attention =>
    ({ id: 'x', kind, title: 't', where: 'w', since: '2026-10-05T10:00:00Z', account });

  it('holds work, whichever it is', () => {
    expect(ATTENTION_GROUP['account-wait']).toBe('holding');
    expect(ATTENTION_GROUP['signed-out']).toBe('holding');
  });

  it('signs in each account a waiting start passed signed out, in the order it names them', () => {
    const item = row('account-wait', facts({ named: [named('account-2', 'cooling'), named('account-1', 'out'), named('account-3', 'out')] }));
    expect(attentionOffers(item)).toEqual([{ act: 'sign-in', account: 'account-1' }, { act: 'sign-in', account: 'account-3' }]);
    expect(attentionActs(item)).toEqual(['sign-in', 'sign-in']);
  });

  it('lets a ready account in, and reads one no read answered, after the sign-ins, two at most', () => {
    const outside = { id: 'account-4', label: 'account-4', list: 'work', workspace: 'work' };
    expect(attentionOffers(row('account-wait', facts({ named: [named('account-2', 'cooling'), named('account-5', 'unknown', null)], outside }))))
      .toEqual([{ act: 'let-run', account: 'account-4' }, { act: 'read', account: 'account-5' }]);
    expect(attentionOffers(row('account-wait', facts({
      named: [named('account-1', 'out'), named('account-3', 'out'), named('account-6', 'out')], outside,
    })))).toEqual([{ act: 'sign-in', account: 'account-1' }, { act: 'sign-in', account: 'account-3' }]);
  });

  /**
   * UXFIX3: an account outside the list that no read answered may be signed out, so the row reads it first, in *Let … run
   * …*'s place, and lets it in only once it reads ready.
   */
  it('reads an unread account outside the list first, where it would offer to let it run', () => {
    const readFirst = { id: 'account-4', label: 'account-4' };
    expect(attentionOffers(row('account-wait', facts({ named: [named('account-2', 'cooling'), named('account-5', 'unknown', null)], readFirst }))))
      .toEqual([{ act: 'read', account: 'account-4' }, { act: 'read', account: 'account-5' }]);
    expect(attentionOffers(row('account-wait', facts({ named: [named('account-1', 'out')], readFirst }))))
      .toEqual([{ act: 'sign-in', account: 'account-1' }, { act: 'read', account: 'account-4' }]);
    expect(attentionActs(row('account-wait', facts({ named: [named('account-2', 'cooling')], readFirst })))).not.toContain('let-run');
  });

  /** A read that failed is a reading: *Read* is for an account never read (§6.3), as the agent's page offers it. */
  it('reads only an account never read, and signs in nothing where Daoris runs no sign-in', () => {
    expect(attentionOffers(row('account-wait', facts({ named: [named('account-5', 'unknown')] })))).toEqual([]);
    expect(attentionOffers(row('account-wait', facts({ signsIn: false, named: [named('account-1', 'out')] })))).toEqual([]);
  });

  it('signs a signed-out account in, and never the tool’s own sign-in, which is the person’s', () => {
    expect(attentionOffers(row('signed-out', facts({ named: [named('account-1', 'out')] }))))
      .toEqual([{ act: 'sign-in', account: 'account-1' }]);
    expect(attentionOffers(row('signed-out', facts({ named: [named(null, 'out')] })))).toEqual([]);
    expect(attentionOffers(row('signed-out', facts({ signsIn: false, named: [named('account-1', 'out')] })))).toEqual([]);
  });
});

/**
 * UX6d: the account rows are What needs you's holding work, the longest waiting first among the rest (§6.2). UXFIX3: a
 * signed-out account's row knows when it was read, not since when it holds work, so it has no wait to be sorted by: it comes
 * after the rows that say how long, in the roster's order, wherever a reading moves its time.
 */
describe('what needs a person, with the accounts as last known', () => {
  const tools = (reads: Record<string, string>) => byTool([{
    harness: 'claude-code', present: true, product: 'Claude Code', signsIn: true, machineDefault: 'account-1',
    workspaceDefaults: [{ workspace: 'aurora', profile: 'account-2' }],
    profiles: Object.entries(reads).map(([name, read]) => ({ name, home: `H/${name}`, login: 'out' as const, read })),
  }]);
  const derive = (reads: Record<string, string>) => needsAPerson(
    [session({ state: 'awaiting-person' })], [], [registration('engine')], [], [], [], [], [],
    { tools: tools(reads), use: null, waits: [] },
  );

  it('lists a signed-out account a list holds after a parked session, which says how long it waited', () => {
    const rows = derive({ 'account-1': '2026-09-21T09:30:00Z' });

    expect(rows.map(({ kind, id }) => [kind, id])).toEqual([
      ['parked', 's1a2b3c4'], ['signed-out', 'signed-out:claude-code/account-1'],
    ]);
    expect(attentionGroups(rows).map(({ group }) => group)).toEqual(['holding']);
  });

  it('keeps a re-read account where it was: reading it again neither moves it nor makes it a wait', () => {
    const before = derive({ 'account-1': '2026-09-21T09:30:00Z', 'account-2': '2026-09-21T08:00:00Z' });
    const after = derive({ 'account-1': '2026-09-21T11:58:00Z', 'account-2': '2026-09-21T08:00:00Z' });

    const order = (rows: Attention[]) => rows.map(({ id }) => id);
    expect(order(after)).toEqual(order(before));
    expect(order(after)).toEqual(['s1a2b3c4', 'signed-out:claude-code/account-1', 'signed-out:claude-code/account-2']);
    expect(after.filter(({ kind }) => kind === 'signed-out').map(({ since }) => since)).toEqual([null, null]);
  });
});
