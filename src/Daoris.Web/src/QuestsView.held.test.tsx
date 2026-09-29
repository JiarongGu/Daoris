import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// USE1: why the driver leaves a quest waiting, on its card, with the bridge present — the hold is this
// machine's driver's, so the card's resume exists only where one is attached.

const { invoke, notifyReady } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => {},
}));

import './i18n';
import { keys } from './queries';
import { QuestsView } from './QuestsView';

const QUESTS = [
  {
    id: 'held01', from: 'ask #c24c28', to: 'engine', title: 'Make the two speeds one figure', body: 'The gauge and the tile disagree.',
    status: 'Open', filed: '2026-09-29T21:47:00Z', updated: '2026-09-29T21:47:00Z',
  },
  {
    id: 'free02', from: 'game', to: 'game', title: 'A quest nothing holds', body: 'Its driver is not the story here.',
    status: 'Open', filed: '2026-09-29T21:48:00Z', updated: '2026-09-29T21:48:00Z',
  },
];

function respond(url: string): Response {
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/sessions')) return Response.json([]);
  if (url.startsWith('/api/registry')) return Response.json([]);
  if (url.startsWith('/api/asks')) return Response.json([]);
  throw new Error(`unstubbed request: ${url}`);
}

function show() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  // What the last tick said (ShellSignals writes it): one quest held, one sitting for another reason.
  client.setQueryData(keys.considered, [
    { quest: 'held01', repository: 'engine', verdict: 'Held', reason: '`engine` is held by the person.' },
    { quest: 'free02', repository: 'game', verdict: 'NotDrivable', reason: '`game` is not driven on this machine.' },
  ]);
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <QuestsView notify={() => {}} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('a waiting quest says why on its card', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) => {
      switch (type) {
        case 'STATE': return { drivable: ['engine'], holds: ['engine'], trees: [], running: [] };
        case 'SET_HOLD': return { drivable: ['engine'], holds: [], trees: [], running: [] };
        default: return {};
      }
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  /**
   * 🔴 Seen on the install: two quests to a held repository sat under *Open — waiting to be taken*, and
   * the person read them as requests that would not start. The drawer said why; the card did not.
   */
  it('says the repository is held, and resumes it from the card without opening the quest', async () => {
    show();

    expect(await screen.findByText(/is held by the person/)).toBeInTheDocument();
    // Another reason is said too, with no resume: only a hold is the person's to lift here.
    expect(screen.getByText(/is not driven on this machine/)).toBeInTheDocument();
    const resume = screen.getByRole('button', { name: 'resume engine' });
    expect(screen.getAllByRole('button', { name: /^resume / })).toHaveLength(1);

    await userEvent.click(resume);

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HOLD', {
      payload: { repository: 'engine', held: false },
    }));
    // The press was the button's: the quest's drawer did not open under it.
    expect(screen.queryByRole('dialog')).toBeNull();
  });
});
