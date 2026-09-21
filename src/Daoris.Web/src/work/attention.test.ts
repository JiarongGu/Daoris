import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import type { Quest, Registration, Session } from '../api';
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

describe('what needs a person', () => {
  it('is empty when nothing is parked and every open quest has a receiver', () => {
    expect(needsAPerson([session()], [quest()], [registration('engine')])).toEqual([]);
  });

  it('names a parked session by its derived identity, with the analysis it is waiting on', () => {
    const waiting = needsAPerson(
      [session({ state: 'awaiting-person', quest: '7a82cc', note: 'two ways forward.' })],
      [quest({ status: 'Taken' })],
      [registration('engine')],
    );

    expect(waiting).toEqual([{
      id: 's1a2b3c4',
      kind: 'parked',
      title: 'Expose a streaming budget on the chunk API',
      repository: 'engine',
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
    const waiting = needsAPerson([], [quest({ to: 'retired' })], [registration('engine')]);

    expect(waiting).toHaveLength(1);
    expect(waiting[0]).toMatchObject({ kind: 'unanswerable', repository: 'retired', id: '7a82cc' });
    expect(waiting[0].detail).toContain('retired');
  });

  it('matches a receiver whatever its case — a registry row is not a string comparison', () => {
    expect(needsAPerson([], [quest({ to: 'Engine' })], [registration('engine')])).toEqual([]);
  });

  it('leaves a quest somebody already took out of it — it is not waiting on anyone here', () => {
    expect(needsAPerson([], [quest({ to: 'retired', status: 'Taken' })], [])).toEqual([]);
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
    );

    expect(waiting.map((item) => item.id)).toEqual(['older', 'newer', 'q-old', 'q-new']);
  });

  it('explains an unanswerable quest in the active language', async () => {
    await i18n.changeLanguage('zh');
    const waiting = needsAPerson([], [quest({ to: 'retired' })], []);
    expect(waiting[0].detail).toContain('本部署');
    await i18n.changeLanguage('en');
  });
});
