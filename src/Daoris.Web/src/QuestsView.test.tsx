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

// A driven session's record, attached to the quest above (D46): active, so the card wears its state.
// TWO records for the one quest, deliberately — a retry is its own record, and the view must show
// where things stand now (the later one), not the failed first attempt.
const SESSIONS = [{
  id: 's0f1r2s3', quest: 'abc123', repository: 'engine', adapter: 'stub', state: 'failed',
  note: 'the first attempt died', created: '2026-09-01T22:00:00Z', updated: '2026-09-01T23:00:00Z',
}, {
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'stub', state: 'working',
  note: 'the process is alive', evidence: 'commits landed:\nfff000 stub: answer quest abc123',
  created: '2026-09-02T00:00:00Z', updated: '2026-09-02T01:00:00Z',
}];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
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

  it('a quest a driver is working wears its session state on the card', async () => {
    view();
    expect(await screen.findByText('working')).toBeInTheDocument();
  });

  it("the drawer carries the session's record — state, adapter, and the evidence, verbatim", async () => {
    view();
    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('working')).toBeInTheDocument();
    expect(within(dialog).getByText(/s1a2b3c4 · stub/)).toBeInTheDocument();
    expect(within(dialog).getByText(/stub: answer quest abc123/)).toBeInTheDocument();
  });

  it('a retried quest shows its freshest attempt, never the failed first one', async () => {
    view();
    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/s1a2b3c4 · stub/)).toBeInTheDocument();
    expect(within(dialog).queryByText('failed')).not.toBeInTheDocument();
    expect(within(dialog).queryByText(/s0f1r2s3/)).not.toBeInTheDocument();
  });

  it('a browser offers no stop — the control reaches a process, and only the desktop has one', async () => {
    view();
    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    const dialog = await screen.findByRole('dialog');
    // The record renders (above); the control must not — a browser could only wish (D46 §6).
    expect(within(dialog).getByText('working')).toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: 'stop session' })).not.toBeInTheDocument();
  });
});
