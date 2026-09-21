import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// The monitor window (SURF8). An ORGANISM, so this is the layer a mocked bridge is for (components
// plan §2) — and the bridge is mocked as PRESENT, because a secondary window exists only where a
// shell does.

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

import { MonitorWindow } from './MonitorWindow';

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const DRIVER_STATE = { drivable: ['engine'], holds: [], trees: [], running: ['s1a2b3c4'] };

const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 4, root: 'C:/somewhere/engine' },
];

const QUESTS = [{
  id: '7a82cc', from: 'platform', to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs a per-frame cap.',
  status: 'Taken', filed: at(4000), updated: at(200),
}];

const base = { adapter: 'claude-code', kind: 'driven', created: at(120), updated: at(4) };

let SESSIONS: unknown[] = [];

const LIVE = [
  { ...base, id: 's1a2b3c4', quest: '7a82cc', repository: 'engine', state: 'working' },
  { ...base, id: 'b2c3d4e5', quest: null, kind: 'chat', repository: 'engine', state: 'awaiting-person', created: at(30) },
  // Finished: the monitor is the present tense, and reviewing is a surface of its own (SURF6).
  { ...base, id: 'd4e5f6a7', quest: null, repository: 'engine', state: 'completed', created: at(300) },
  // Another machine's: the record travelled here and the console did not (D47 §4).
  { ...base, id: 'laptop/e5f6a7b8', quest: null, repository: 'engine', state: 'working', created: at(20) },
];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

function show() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}><MonitorWindow notify={() => {}} /></QueryClientProvider>,
  );
}

describe('the monitor window', () => {
  beforeEach(() => {
    SESSIONS = LIVE;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    // Answered per session, because the whole question this window raises is whether TWO consoles
    // can be read at once — a stub that gave every tile the same lines would prove nothing.
    invoke.mockImplementation(async (_module: string, type: string, request?: unknown) => {
      if (type !== 'TAIL_SESSION') return DRIVER_STATE;
      const id = (request as { payload: { id: string } }).payload.id;
      return {
        session: id,
        lines: [{ sequence: 1, text: `${id} is talking` }],
        sequence: 1,
        live: true,
        dropped: 0,
      };
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('shows what is running, and leaves what has finished to the views that review it', async () => {
    show();

    expect(await screen.findByText('Expose a streaming budget on the chunk API')).toBeInTheDocument();
    // Two live sessions plus one on another machine; the completed one is not a tile.
    expect(screen.getAllByText('engine').length).toBeGreaterThan(0);
    expect(screen.queryByText(/4h 60m/)).not.toBeInTheDocument();
  });

  /** What needs a person comes first — the rail's rule, which matters more across a desk. */
  it('puts what is waiting on a person at the top', async () => {
    show();

    await screen.findByText('Expose a streaming budget on the chunk API');
    const headings = screen.getAllByTitle(/conversation|Expose a streaming budget/);
    expect(headings[0]!.textContent).toBe('conversation');
  });

  /**
   * 🔴 The claim SURF8 rests on: **a second reader on the console pump** (D55 §b — SES1's bounded
   * buffer was written for one). Here it is two readers in one window; the driver's own tests hold
   * the other half, that the buffer keeps no cursor and so cannot be consumed.
   */
  it('streams every session this machine is running, each its own', async () => {
    show();

    expect(await screen.findByText(/s1a2b3c4 is talking/)).toBeInTheDocument();
    expect(screen.getByText(/b2c3d4e5 is talking/)).toBeInTheDocument();
  });

  /**
   * 🔴 The disclosure boundary (D47 §4). A session mirrored from another machine has no console
   * here and never will, so the tile says where it runs — an empty well would read as silence.
   */
  it('never asks for a console this machine does not hold', async () => {
    show();

    expect(await screen.findByText(/Runs on laptop/)).toBeInTheDocument();
    const tailed = invoke.mock.calls
      .filter(([, type]) => type === 'TAIL_SESSION')
      .map(([, , payload]) => (payload as { payload: { id: string } }).payload.id);
    expect(tailed).not.toContain('laptop/e5f6a7b8');
  });

  it('asks the shell for a window of its own, by the name the shell opens', async () => {
    show();

    await screen.findByText('Expose a streaming budget on the chunk API');
    const [detach] = screen.getAllByRole('button', { name: /own window/i });
    await userEvent.click(detach!);

    expect(invoke).toHaveBeenCalledWith(
      'DAORIS.WINDOWS', 'OPEN', { payload: { name: 'session:b2c3d4e5' } });
  });

  it('offers no window for a session another machine is running', async () => {
    show();

    await screen.findByText(/Runs on laptop/);
    // Two of the four records are this machine's and live; only those get the control.
    expect(screen.getAllByRole('button', { name: /own window/i })).toHaveLength(2);
  });

  it('says when nothing is running rather than showing an empty grid', async () => {
    SESSIONS = [];
    show();

    expect(await screen.findByText('Nothing is running')).toBeInTheDocument();
  });
});
