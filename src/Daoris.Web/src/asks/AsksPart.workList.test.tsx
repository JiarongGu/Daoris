import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient } from '@tanstack/react-query';

// The ask's page lists its work in the shell (PAUSE1h, D132 §7.1): the asks' organism hands the page the driver's plan of the
// chosen ask and the tick's last look at each quest, so each quest says why it sits, and a session's door attends it. Over a
// mocked bridge, as the shell-attached half is held (`docs/2026-09-19-frontend-architecture.md` §4).

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import { keys } from '../queries';
import { chooseRow, QuestsView } from '../test/questsView';
import { DRIVER_STATE, respond, show, WIRING } from '../test/shellHarness';
import { LISTED_ASK, LISTED_CONSIDERED } from '../work/pausingFixtures';

const ASK = {
  id: 'a1b2c3', workspace: 'aurora', sentence: 'Make chunk streaming smooth', state: 'Published', tier: 'named',
  asked: '2026-10-02T00:00:00Z', updated: '2026-10-02T00:00:00Z', links: [], attachments: [],
  quests: ['9a8b7c', '5e4f3d', '2d3e4f', '3c2b1a'], proposal: [],
};

describe("the ask's work in the shell", () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/asks') ? Response.json([ASK]) : respond(url);
    }));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module === 'DAORIS.REMOTES') return WIRING;
      return type === 'WORK_PLAN' ? LISTED_ASK : DRIVER_STATE;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('lists the chosen ask’s work with why each quest sits, from the tick, and attends a session from its door', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    // What the last tick said of each quest, as the shell's signals write it.
    client.setQueryData(keys.considered, LISTED_CONSIDERED);
    const onAttend = vi.fn();
    show(<QuestsView notify={vi.fn()} onAttend={onAttend} />, client);

    const page = await chooseRow('Make chunk streaming smooth');
    const work = await within(page).findByRole('region', { name: 'Became' });
    await waitFor(() => expect(work).toHaveTextContent('game is held by the person.'));
    expect(work).toHaveTextContent('Paused on its own: its own page resumes it, and resuming this ask does not.');

    await userEvent.click(within(work).getByRole('button', { name: 'Open session w0rk1ng0 in Sessions' }));
    expect(onAttend).toHaveBeenCalledWith('w0rk1ng0');
  });
});
