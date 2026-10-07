import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient } from '@tanstack/react-query';

// Quests in SHELL mode, pausing and abandoning (PAUSE1e, D132 §7.1): the ask's page and the quest's page ask this machine's
// driver for their work's plan, and each press goes over DAORIS.DRIVER and says how it went. Over a mocked bridge, as the
// shell-attached half is held (`docs/2026-09-19-frontend-architecture.md` §4).

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import { keys } from './queries';
import { chooseRow, QuestsView } from './test/questsView';
import { DRIVER_STATE, respond, show, WIRING } from './test/shellHarness';
import { ABANDON_ANSWER, PAUSABLE_ASK, PAUSABLE_QUEST } from './work/pausingFixtures';

const ASK = {
  id: 'a1b2c3', workspace: 'aurora', sentence: 'Make chunk streaming smooth', state: 'Published', tier: 'named',
  asked: '2026-10-02T00:00:00Z', updated: '2026-10-02T00:00:00Z', links: [], attachments: [], quests: ['abc123'], proposal: [],
};

/** The quest's own plan: the shell harness's quest, taken here and running. */
const QUEST_PLAN = { ...PAUSABLE_QUEST, id: 'abc123', quests: [{ ...PAUSABLE_QUEST.quests[0], quest: 'abc123', key: 'quest:abc123' }] };

describe('pausing and abandoning in the shell', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/asks') ? Response.json([ASK]) : respond(url);
    }));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  const answering = (answers: Record<string, unknown>) => invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.REMOTES') return WIRING;
    if (type in answers) return answers[type];
    return DRIVER_STATE;
  });

  it('asks the ask’s plan, asks once before its pause, pauses over the driver and says what it stopped', async () => {
    answering({
      WORK_PLAN: PAUSABLE_ASK,
      WORK_PAUSE: { scope: 'ask', id: 'a1b2c3', did: 'paused', already: false, stopped: [{ session: 's1a2b3c4', quest: '9a8b7c' }], kept: [] },
    });
    const notify = vi.fn();
    show(<QuestsView notify={notify} />);

    const page = await chooseRow('Make chunk streaming smooth');
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_PLAN', { payload: { ask: 'a1b2c3' } }));
    await userEvent.click(await within(page).findByRole('button', { name: 'Pause…' }));

    // The ask's workspace is wired here (`aurora`), so another machine may still take its open quest.
    const ask = within(page).getByRole('group', { name: 'pause this work' });
    expect(ask).toHaveTextContent('Another machine may still take #5e4f3d');
    await userEvent.click(within(ask).getByRole('button', { name: 'Pause ask' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_PAUSE', expect.objectContaining({ payload: { ask: 'a1b2c3' } })));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Paused ask #a1b2c3 and stopped 1 session: nothing of it starts on this machine until you resume it.', 'ok'));
  });

  it('abandons the ask with the reason and the listed pieces, then says what went at once', async () => {
    answering({ WORK_PLAN: PAUSABLE_ASK, WORK_ABANDON: ABANDON_ANSWER });
    const notify = vi.fn();
    show(<QuestsView notify={notify} />);
    const user = userEvent.setup();

    const page = await chooseRow('Make chunk streaming smooth');
    await user.click(await within(page).findByRole('button', { name: 'Abandon…' }));
    await user.type(within(page).getByRole('textbox', { name: 'why — kept with each decline' }), 'Taking another approach.');
    await user.click(within(page).getByRole('button', { name: 'Abandon ask' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_ABANDON', expect.objectContaining({
      payload: { ask: 'a1b2c3', reason: 'Taking another approach.', pieces: PAUSABLE_ASK.abandon.pieces },
    })));
    // A partial abandon, said in the error's tone (PAUSE1h).
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Abandoned ask #a1b2c3: 5 of 6 pieces; 1 changed since the list and stayed.', 'error'));
    expect(await within(page).findByRole('region', { name: 'What went' })).toHaveTextContent('#5e4f3d was taken on another machine');
  });

  it('resumes a quest paused on its own from under Sitting', async () => {
    answering({
      WORK_PLAN: { ...QUEST_PLAN, paused: { at: '2026-10-03T09:20:00Z', stopped: [] } },
      WORK_RESUME: { scope: 'quest', id: 'abc123', did: 'resumed', released: [], holds: [] },
    });
    const notify = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(keys.considered, [{
      quest: 'abc123', repository: 'engine', verdict: 'Paused', pausedBy: { scope: 'quest', id: 'abc123' },
      reason: 'you paused `#abc123`; Resume starts it — `daoris-driver quest resume abc123`.',
    }]);
    show(<QuestsView notify={notify} />, client);

    const page = await chooseRow('Expose a streaming budget');
    await userEvent.click(await within(page).findByRole('button', { name: 'Resume' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_RESUME', expect.objectContaining({ payload: { quest: 'abc123' } })));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      "Resumed quest #abc123: the driver's next look plans its work as it would have.", 'ok'));
  });

  it('opens the ask whose pause holds a quest, from under its Sitting', async () => {
    answering({ WORK_PLAN: QUEST_PLAN });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(keys.considered, [{
      quest: 'abc123', repository: 'engine', verdict: 'Paused', pausedBy: { scope: 'ask', id: 'a1b2c3' },
      reason: 'paused with ask `#a1b2c3`; Resume starts it — `daoris-driver ask --resume a1b2c3`.',
    }]);
    show(<QuestsView notify={() => {}} />, client);

    const page = await chooseRow('Expose a streaming budget');
    await userEvent.click(within(page).getByRole('button', { name: 'Open ask #a1b2c3' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Make chunk streaming smooth' })).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBe('ask:a1b2c3');
  });
});
