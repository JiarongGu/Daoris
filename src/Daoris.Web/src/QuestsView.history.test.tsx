import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Quests in SHELL mode, clearing finished history (HIST1e, D153; the history-clearing design §5, §6.1): a closed quest's page
// and a done ask's page ask this machine's driver for their plans, offer each clear the plan says may go, and the second
// press goes over DAORIS.DRIVER with exactly the units the first listed. Over a mocked bridge, as the shell-attached half is
// held (`docs/2026-09-19-frontend-architecture.md` §4).

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import { moreAct, questMain, questPage, QuestsView } from './test/questsView';
import { DRIVER_STATE, QUESTS, respond, show, WIRING } from './test/shellHarness';
import { ASK_PLAN, FAILED_PLAN, QUEST_CLEARED, QUEST_PLAN } from './work/historyFixtures';

const DONE = { ...QUESTS[0]!, status: 'Done', updated: '2026-09-03T00:00:00Z' };

const ASK = {
  id: 'a1b2c3', workspace: 'aurora', sentence: 'Make chunk streaming smooth', state: 'Done', tier: 'named',
  asked: '2026-10-02T00:00:00Z', updated: '2026-10-02T00:00:00Z', links: [], attachments: [], quests: ['abc123'], proposal: [],
};

/** The plans the driver answers, by what the route was asked: the quest's work, its failed sessions, or the ask's work. */
const plans = (payload: Record<string, unknown>) => {
  if (payload.ask === 'a1b2c3') return ASK_PLAN;
  const of = payload.failed ? FAILED_PLAN : QUEST_PLAN;
  return { ...of, id: 'abc123', units: of.units.map((unit) => ({ ...unit, id: 'abc123', quests: unit.kind === 'quest' ? ['abc123'] : [] })) };
};

describe('clearing finished history in the shell', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/asks')) return Response.json([ASK]);
      if (url.startsWith('/api/quests')) return Response.json([DONE]);
      return respond(url);
    }));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  const answering = (answers: Record<string, unknown>) => invoke.mockImplementation(
    async (module: string, type: string, request?: { payload?: Record<string, unknown> }) => {
      if (module === 'DAORIS.REMOTES') return WIRING;
      if (type === 'HISTORY_PLAN') return plans(request?.payload ?? {});
      if (type in answers) {
        const answer = answers[type];
        if (answer instanceof Error) throw answer;
        return answer;
      }
      return DRIVER_STATE;
    },
  );

  it('asks a closed quest’s two plans, lists its clear, sends exactly the listed unit, then shows the list with a toast', async () => {
    answering({ HISTORY_CLEAR: { ...QUEST_CLEARED, id: 'abc123', cleared: [{ kind: 'quest', id: 'abc123' }] } });
    const notify = vi.fn();
    show(<QuestsView notify={notify} door="abc123" />);

    const page = await questPage('Expose a streaming budget');
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HISTORY_PLAN', expect.objectContaining({ payload: { quest: 'abc123' } })));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'HISTORY_PLAN', expect.objectContaining({ payload: { quest: 'abc123', failed: true } })));
    await moreAct(page, 'Clear from this machine…');

    const ask = within(questMain()).getByRole('group', { name: 'clear from this machine' });
    expect(ask).toHaveTextContent('Clears #abc123, its 3 sessions and what this machine kept of them');
    await userEvent.click(within(ask).getByRole('button', { name: 'Clear quest' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HISTORY_CLEAR', expect.objectContaining({
      payload: { quest: 'abc123', units: [{ kind: 'quest', id: 'abc123' }] },
    })));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Cleared `#abc123` from this machine.', 'ok'));
    // The page closes and the list it came from is shown (§6.1).
    await waitFor(() => expect(screen.queryByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeNull());
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBeNull();
  });

  it('clears a closed quest’s failed sessions and stays on its page', async () => {
    answering({
      HISTORY_CLEAR: { ...QUEST_CLEARED, scope: 'failed', id: 'abc123', cleared: [{ kind: 'failed', id: 'abc123' }], quests: 0, sessions: 2 },
    });
    const notify = vi.fn();
    show(<QuestsView notify={notify} door="abc123" />);

    const page = await questPage('Expose a streaming budget');
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'HISTORY_PLAN', expect.objectContaining({ payload: { quest: 'abc123', failed: true } })));
    await moreAct(page, 'Clear failed sessions…');
    await userEvent.click(within(questMain()).getByRole('button', { name: 'Clear 2' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HISTORY_CLEAR', expect.objectContaining({
      payload: { quest: 'abc123', failed: true, units: [{ kind: 'failed', id: 'abc123' }] },
    })));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Cleared 2 failed sessions of `#abc123` from this machine.', 'ok'));
    expect(screen.getByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeInTheDocument();
    expect(within(questMain()).queryByRole('group', { name: 'clear from this machine' })).toBeNull();
  });

  /**
   * A unit that changed since the list is said in its code's sentence (D153 §5), and since UXFIX2 inside the ask where it was
   * pressed, which stays open, rather than in a toast.
   */
  it('says a unit that changed since the list in its code’s sentence inside the ask, and leaves the page where it was', async () => {
    answering({
      HISTORY_CLEAR: Object.assign(new Error('raw'), { code: 'HISTORY_LIVE', parameters: { session: 's1a2b3c4' } }),
    });
    const notify = vi.fn();
    show(<QuestsView notify={notify} door="abc123" />);

    const page = await questPage('Expose a streaming budget');
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HISTORY_PLAN', expect.objectContaining({ payload: { quest: 'abc123' } })));
    await moreAct(page, 'Clear from this machine…');
    await userEvent.click(within(questMain()).getByRole('button', { name: 'Clear quest' }));

    const ask = within(questMain()).getByRole('group', { name: 'clear from this machine' });
    expect(await within(ask).findByRole('alert')).toHaveTextContent('s1a2b3c4 is still running, so it was not cleared. Stop it first.');
    expect(notify).not.toHaveBeenCalledWith(expect.stringContaining('so it was not cleared'), 'error');
    expect(screen.getByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeInTheDocument();
  });

  it('clears a done ask’s work whole from its page, sending exactly the ask', async () => {
    answering({
      HISTORY_CLEAR: { ...QUEST_CLEARED, scope: 'ask', id: 'a1b2c3', cleared: [{ kind: 'ask', id: 'a1b2c3' }], quests: 2, asks: 1, sessions: 5 },
    });
    const notify = vi.fn();
    show(<QuestsView notify={notify} door="ask:a1b2c3" />);

    const page = await questPage('Make chunk streaming smooth');
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HISTORY_PLAN', expect.objectContaining({ payload: { ask: 'a1b2c3' } })));
    await moreAct(page, 'Clear from this machine…');
    await userEvent.click(within(questMain()).getByRole('button', { name: 'Clear ask' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HISTORY_CLEAR', expect.objectContaining({
      payload: { ask: 'a1b2c3', units: [{ kind: 'ask', id: 'a1b2c3' }] },
    })));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Cleared ask `#a1b2c3` from this machine.', 'ok'));
    await waitFor(() => expect(screen.queryByRole('heading', { level: 1, name: 'Make chunk streaming smooth' })).toBeNull());
  });

  it('asks no plan of a quest still in progress', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/asks')) return Response.json([]);
      return respond(url);
    }));
    answering({});
    show(<QuestsView notify={() => {}} door="abc123" />);

    await questPage('Expose a streaming budget');
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_PLAN', expect.anything()));
    expect(invoke.mock.calls.filter(([, type]) => type === 'HISTORY_PLAN')).toEqual([]);
  });
});
