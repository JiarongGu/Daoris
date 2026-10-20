import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../locales/i18n';
import { chooseRow, QuestsView } from '../test/questsView';

// FLOWCHART1a2 (D186; the flowchart design §2.1, §7 trap 18): the quest's page reads its moves from the service's own door, in
// a browser as in the window, and only once its flowchart is opened, as the trace reads only when opened (D143).

const QUESTS = [{
  id: 'f10c4a', from: 'game', to: 'engine', title: 'Draw the way it went', body: 'Its moves, on its page.',
  status: 'Taken', filed: '2026-10-20T09:00:00Z', updated: '2026-10-20T09:05:00Z',
}];

const MOVES = {
  quest: 'f10c4a',
  moves: [
    { kind: 'published', at: '2026-10-20T09:00:00Z', machine: 'm2', sequence: 1, number: 1, applied: true },
    { kind: 'taken', at: '2026-10-20T09:05:00Z', machine: 'm3', sequence: 1, number: 2, applied: true, from: 'Open' },
    { kind: 'waited', at: '2026-10-20T09:06:00Z', machine: 'm3', sequence: 2, number: 3, applied: true, from: 'Taken', waitsOn: 'q2q2q2' },
  ],
};

let asked: string[] = [];

function view() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <QuestsView notify={vi.fn()} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe("a quest's moves on its page", () => {
  beforeEach(() => {
    asked = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      asked.push(url);
      if (url === '/api/quests/f10c4a/moves') return Response.json(MOVES);
      if (url.startsWith('/api/quests')) return Response.json(QUESTS);
      if (url.startsWith('/api/sessions') || url.startsWith('/api/registry') || url.startsWith('/api/asks')) return Response.json([]);
      throw new Error(`unstubbed request: ${url}`);
    }));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    window.localStorage.clear();
  });

  it('reads them only once the flowchart is opened, and draws them in the status they happened in', async () => {
    view();
    const page = await chooseRow('Draw the way it went');
    expect(asked.some((url) => url.endsWith('/moves'))).toBe(false);

    await userEvent.click(within(page).getByRole('button', { name: /^Flowchart/ }));

    const flow = await within(page).findByRole('list', { name: 'How #f10c4a moves' });
    await waitFor(() => expect(within(flow).getByRole('listitem', { current: 'step' })).toHaveTextContent('waited on #q2q2q2'));
    expect(asked.filter((url) => url.endsWith('/moves'))).toEqual(['/api/quests/f10c4a/moves']);
  });

  it('draws the statuses and says the path is not kept, where the host is from before the door (trap 18)', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.endsWith('/moves')) return new Response('', { status: 404, statusText: 'Not Found' });
      if (url.startsWith('/api/quests')) return Response.json(QUESTS);
      return Response.json([]);
    }));
    view();
    const page = await chooseRow('Draw the way it went');

    await userEvent.click(within(page).getByRole('button', { name: /^Flowchart/ }));

    expect(await within(page).findByText('This service keeps no record of its moves, so where it stands now is drawn and the way it went is not.'))
      .toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'How #f10c4a moves' })).toBeInTheDocument();
  });
});
