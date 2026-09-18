import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QuestsView } from './QuestsView';

// The view over a stubbed service — the shapes the real endpoints return, without a host. The
// Playwright loop owns the real end-to-end; this owns the view's own logic at millisecond speed.

const QUESTS = [{
  id: 'abc123', from: 'game', to: 'engine',
  title: 'Expose a streaming budget', body: 'World streaming needs a per-frame cap.',
  status: 'Open', filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
}];

const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 1 },
  { repository: 'game', adopted: true, registered: true, summary: 'the game', owns: [], accepts: [], packs: [], entries: 1 },
];

function respond(url: string): Response {
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

function view() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <QuestsView notify={() => {}} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('QuestsView', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => vi.unstubAllGlobals());

  it('groups what the service returns by where it is in its life', async () => {
    view();
    expect(await screen.findByText('Open — waiting to be taken (1)')).toBeInTheDocument();
    expect(screen.getByText('Expose a streaming budget')).toBeInTheDocument();
  });

  it('a card is a door: clicking it opens the detail drawer, where the acting happens', async () => {
    view();
    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    // Scoped to the dialog: the ask's text also lives in the card's excerpt behind the drawer —
    // the list surviving the detail is the drawer pattern's whole point.
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('World streaming needs a per-frame cap.')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'take' })).toBeInTheDocument();
  });

  it('publish stays disabled until the ask is complete — the form does not offer the mistake', async () => {
    view();
    await userEvent.click(await screen.findByRole('button', { name: 'new quest' }));
    expect(await screen.findByRole('button', { name: 'publish quest' })).toBeDisabled();
  });
});
