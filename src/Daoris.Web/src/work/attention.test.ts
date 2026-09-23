import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import type { Ask, Quest, Registration, Session } from '../api';
import { needsAPerson } from './attention';

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
      detail: 'two ways forward.',
    }]);
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
      detail: note,
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
