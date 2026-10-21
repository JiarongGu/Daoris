import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient } from '@tanstack/react-query';

// SESSONE2h (D167): the Asks list gains the asks the mirrored messages name and this machine does not hold, and choosing one opens a
// page with no ask record: its channel, read-only. Over a mocked bridge, as the shell-attached half is held.

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import { QuestsView, questList, questMain } from '../test/questsView';
import { DRIVER_STATE, respond, show, WIRING } from '../test/shellHarness';

const HELD = { id: '7e6f51', workspace: 'default', origins: ['key-a'], latest: '2026-10-15T09:05:00Z', messages: 1, unread: 1 };
const CHANNEL = {
  ask: '7e6f51', entry: 'person', unread: 1, mark: null,
  items: [{
    kind: 'message', at: '2026-10-15T09:05:00Z', id: 'm1', person: false, session: 'key-a/s1', repository: 'db', agent: 'claude-code',
    text: 'Which column?', origin: 'key-a',
  }],
};

describe('a teammate’s ask in the shell', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/asks') ? Response.json([]) : respond(url);
    }));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module === 'DAORIS.REMOTES') return WIRING;
      if (type === 'ASKS_ELSEWHERE') return { asks: [HELD] };
      if (type === 'CHANNEL_READ') return CHANNEL;
      return DRIVER_STATE;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('lists it, opens it from its row, and shows its channel read-only with none of an ask’s sections', async () => {
    show(<QuestsView notify={vi.fn()} />, new QueryClient({ defaultOptions: { queries: { retry: false } } }));

    await userEvent.click(await within(questList()).findByText('#7e6f51'));

    const page = questMain();
    await screen.findByRole('heading', { level: 1, name: /teammate’s ask #7e6f51/ });
    await waitFor(() => expect(within(page).getByText('Which column?')).toBeInTheDocument());
    expect(within(page).queryByRole('textbox')).toBeNull();
    expect(within(page).queryByRole('region', { name: 'Proposal' })).toBeNull();
    expect(within(page).getByText(/held on their machine/)).toBeInTheDocument();
    // NOTIFY2g: the service keeps the person's mark on a teammate's ask its mirror holds, so reading its page moves it through the
    // newest, and its item leaves Messages (it moved no mark before, the host holding no record to mark it on).
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CHANNEL_MARK', {
      payload: { ask: '7e6f51', through: '2026-10-15T09:05:00Z' },
    }));
  });
});
