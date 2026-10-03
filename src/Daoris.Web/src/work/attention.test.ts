import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import type { Ask, Quest, Registration, Session } from '../api';
import type { RuleProposal } from '../settings/AgentRules';
import type { Consideration } from '../signals';
import { needsAPerson, waitingInSessions } from './attention';

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

  /** Asks hold nothing while they wait, and nothing downstream moves until the person settles one. */
  it('sits after the parked sessions and before the quests nobody can take, oldest first', () => {
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
      'parked:parked', 'intake:7c1e9a04b2d5', 'proposal:later', 'unanswerable:sitting',
    ]);
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

  it('sits after the parked sessions and ahead of the asks: nothing it holds can start until it is granted', () => {
    const waiting = needsAPerson(
      [session({ id: 'parked', state: 'awaiting-person', updated: '2026-09-21T09:00:00Z' })],
      [quest({ id: 'q1' })], [registration('engine')], [ask()],
      [{ ...hold, quest: 'q1' }],
    );

    expect(waiting.map((item) => item.kind)).toEqual(['parked', 'trust', 'proposal']);
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

  it('sits after the asks and before the quests nobody can take', () => {
    const waiting = needsAPerson(
      [], [quest({ to: 'nobody' })], [registration('engine')], [ask()], [], [proposal()]);

    expect(waiting.map((item) => item.kind)).toEqual(['proposal', 'rule', 'unanswerable']);
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

  /** A session parked to ask holds a tree while it waits; a parked quest holds nothing, and comes after it. */
  it('sits after the parked sessions and ahead of the folders held for trust, oldest first', () => {
    const waiting = needsAPerson(
      [session({ id: 'asking', state: 'awaiting-person', updated: '2026-10-01T12:00:00Z' })],
      [quest({ id: 'q-new', status: 'Taken' }), quest({ id: 'q-old' })],
      [registration('engine')], [],
      [{ folder: 'C:/somewhere/engine', trustFile: 'C:/somewhere/.claude.json', quest: 'q-new' }], [],
      [parked({ quest: 'q-new', since: '2026-10-01T10:00:00+00:00' }), parked({ quest: 'q-old', since: '2026-09-30T10:00:00+00:00' })],
    );

    expect(waiting.map((item) => `${item.kind}:${item.id}`)).toEqual([
      'parked:asking', 'parked-quest:q-old', 'parked-quest:q-new', `trust:C:/somewhere/.claude.json\nC:/somewhere/engine`,
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
