import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
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
    <QueryClientProvider client={client}>
      <Tooltip.Provider><MonitorWindow notify={() => {}} /></Tooltip.Provider>
    </QueryClientProvider>,
  );
}

/** The window this wide. Nothing lays out in jsdom, so the frame is read as the window less 48 px. */
const widen = (width: number) => act(() => {
  Object.defineProperty(window, 'innerWidth', { configurable: true, value: width });
  window.dispatchEvent(new Event('resize'));
});

/** The rail's pane, by the list's name, and how the room left it. */
const railPane = () => screen.getByRole('complementary', { name: 'Sessions' });

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
    window.localStorage.clear();
    widen(1024);
  });

  it('shows what is running, and leaves what has finished to the views that review it', async () => {
    show();

    expect(await screen.findByText('Expose a streaming budget on the chunk API')).toBeInTheDocument();
    // Two live sessions plus one on another machine; the completed one is not a tile.
    expect(screen.getAllByText('engine').length).toBeGreaterThan(0);
    expect(screen.queryByText(/4h 60m/)).not.toBeInTheDocument();
  });

  /**
   * UX5 U70, seen on the window: the monitor's rail listed what had ended, beneath the running, and
   * a press on one scrolled to a tile that is not there. The monitor is the present tense, rail and
   * tiles alike. U69: and its rail is the main rail's width, where the search's words were cut.
   */
  it('lists only what is running in its rail too, at the main rail’s width', async () => {
    show();
    const rail = await screen.findByRole('navigation', { name: 'Sessions' });

    await waitFor(() => expect(within(rail).getAllByRole('listitem').length).toBeGreaterThan(0));
    expect(within(rail).queryByRole('region', { name: 'Ended' })).toBeNull();
    // The main rail's own bounds (FRAME1h): 280 px to start, as Sessions' list.
    expect(rail.closest('aside')!.style.width).toBe('280px');
  });

  /**
   * FRAME1h, audit MO2: below 1024 px the rail was hidden with no way back, and the running sessions it
   * scrolls to were out of reach. It is a list pane now: a strip where the tiles leave it no room, which
   * keeps each running session's mark and an open that lays the rail over the tiles.
   */
  it('keeps its rail as a strip where there is no room, never hidden, and lays it over the tiles from there', async () => {
    widen(600);
    show();

    await screen.findByText('Expose a streaming budget on the chunk API');
    expect(railPane()).toHaveAttribute('data-list-mode', 'strip');
    // Each running session this machine or another holds, by its mark; what ended is not one.
    await waitFor(() => expect(within(railPane()).getAllByRole('button', { name: / · engine · / })).toHaveLength(3));

    await userEvent.click(within(railPane()).getByRole('button', { name: 'Show the session list' }));
    const over = screen.getByRole('region', { name: 'Sessions' });
    // The row, before its own menu.
    const [row] = await within(over).findAllByRole('button', { name: /Expose a streaming budget/ });
    await userEvent.click(row!);
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull());
    expect(railPane()).toHaveAttribute('data-list-mode', 'strip');
  });

  /** The monitor remembers its own rail (D118 §4, §3f): the main window's is another list. */
  it('remembers its rail\'s closing and width for the monitor, apart from the main window\'s', async () => {
    widen(1400);
    show();

    await userEvent.click(await screen.findByRole('button', { name: 'Hide the session list' }));
    expect(railPane()).toHaveAttribute('data-list-mode', 'strip');
    expect(window.localStorage.getItem('daoris.list.monitor.closed')).toBe('1');
    expect(window.localStorage.getItem('daoris.railClosed')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Show the session list' }));
    const edge = screen.getByRole('separator', { name: 'session list width' });
    edge.focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(window.localStorage.getItem('daoris.list.monitor.width')).toBe('304');
    expect(window.localStorage.getItem('daoris.railWidth')).toBeNull();
  });

  it('opens on what the monitor kept', async () => {
    window.localStorage.setItem('daoris.list.monitor.closed', '1');
    widen(1400);
    show();

    await screen.findByText('Expose a streaming budget on the chunk API');
    expect(railPane()).toHaveAttribute('data-list-mode', 'strip');
  });

  /** Each window answers the key of the region it has (D118 §4): the monitor's is its list, on Ctrl+B. */
  it('toggles its rail on Ctrl+B, and has no panel for Ctrl+J to toggle', async () => {
    widen(1400);
    show();

    await screen.findByText('Expose a streaming budget on the chunk API');
    expect(railPane()).toHaveAttribute('data-list-mode', 'open');
    await userEvent.keyboard('{Control>}b{/Control}');
    expect(railPane()).toHaveAttribute('data-list-mode', 'strip');
    await userEvent.keyboard('{Control>}j{/Control}');
    expect(railPane()).toHaveAttribute('data-list-mode', 'strip');
    await userEvent.keyboard('{Control>}b{/Control}');
    expect(railPane()).toHaveAttribute('data-list-mode', 'open');
  });

  /** Audit MO11: its title wore `text-h3`, which no token defines, and was drawn at the body's size. */
  it('titles itself on the one heading step a view has', async () => {
    show();

    expect(await screen.findByRole('heading', { level: 1, name: 'Monitor window' })).toHaveClass('text-view');
  });

  /** What needs a person comes first — the rail's rule, which matters more across a desk. */
  it('puts what is waiting on a person at the top', async () => {
    show();

    await screen.findByText('Expose a streaming budget on the chunk API');
    const headings = screen.getAllByTitle(/Chat|Expose a streaming budget/);
    expect(headings[0]!.textContent).toBe('Chat');
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

  /**
   * UX5 U2: every tile was the console, and D76 made the console the raw view of a conversation. A
   * tile over a kept conversation said *Nothing said yet*. Where the door keeps one, the tile shows
   * what was said; a text door, or a roster that has not answered, keeps the console.
   */
  it('shows the conversation where the door keeps one, and the console where it does not', async () => {
    invoke.mockImplementation(async (_module: string, type: string, request?: unknown) => {
      const id = (request as { payload?: { id?: string } } | undefined)?.payload?.id;
      if (type === 'HARNESSES') {
        return { adapter: 'claude-code', harnesses: [
          { harness: 'claude-code', present: true, structured: true, profiles: [] },
          { harness: 'pipe-tool', present: true, structured: false, profiles: [] },
        ] };
      }
      if (type === 'SESSION_HISTORY') {
        return { events: [{ seq: 1, at: at(1), kind: 'message', text: `${id} said this` }], earlier: false };
      }
      if (type === 'TAIL_SESSION') {
        return { session: id, lines: [{ sequence: 1, text: `${id} is talking` }], sequence: 1, live: true, dropped: 0 };
      }
      return DRIVER_STATE;
    });
    SESSIONS = [
      LIVE[0],
      { ...(LIVE[1] as object), adapter: 'pipe-tool' },
    ];
    show();

    expect(await screen.findByText('s1a2b3c4 said this')).toBeInTheDocument();
    expect(await screen.findByText(/b2c3d4e5 is talking/)).toBeInTheDocument();
    expect(screen.queryByText(/s1a2b3c4 is talking/)).toBeNull();
    expect(screen.queryByText('Nothing said yet.')).toBeNull();
  });

  it('says when nothing is running rather than showing an empty grid', async () => {
    SESSIONS = [];
    show();

    expect(await screen.findByText('Nothing is running')).toBeInTheDocument();
  });
});
