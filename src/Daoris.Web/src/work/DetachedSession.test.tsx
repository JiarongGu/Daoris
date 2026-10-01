import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// One session in a window of its own (SURF8). An ORGANISM, so the bridge is mocked, and as PRESENT:
// a secondary window exists only where a shell does.

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

import { DetachedSession } from './DetachedSession';

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const SESSIONS = [{
  id: 's1a2b3c4', quest: null, kind: 'chat', repository: 'engine', state: 'working',
  adapter: 'claude-code-acp', created: at(30), updated: at(1),
}, {
  // Another machine's: the record travelled here and the console did not (D47 §4).
  id: 'laptop/e5f6a7b8', quest: null, kind: 'driven', repository: 'engine', state: 'working',
  adapter: 'claude-code', created: at(20), updated: at(2),
}];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json([]);
  throw new Error(`unstubbed request: ${url}`);
}

function show(id = 's1a2b3c4') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider><DetachedSession id={id} notify={() => {}} /></Tooltip.Provider>
    </QueryClientProvider>,
  );
}

/** The console's region, as the main window's panel is named; and once the session's record has come. */
const panel = () => screen.getByRole('region', { name: 'the panel' });
const panelShown = () => screen.findByRole('region', { name: 'the panel' });

describe('a session in a window of its own', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string, request?: { payload?: { id?: string } }) => {
      if (type === 'SESSION_STREAMS') {
        return {
          session: 's1a2b3c4',
          streams: [{ key: 's1a2b3c4/task/bs00', kind: 'task', name: 'dev server', live: true, state: null, canStop: true }],
        };
      }
      if (type === 'SESSION_OPENINGS') return { openings: { s1a2b3c4: 'start the dev server' } };
      if (type === 'SESSION_QUEUE') return { session: 's1a2b3c4', queued: [], taking: false, listening: false };
      if (type === 'TAIL_SESSION') {
        const id = request?.payload?.id;
        return {
          session: id,
          lines: [{ sequence: 1, text: id === 's1a2b3c4/task/bs00' ? 'listening on 4200' : '→ background: dev server' }],
          sequence: 1,
          live: true,
          dropped: 0,
        };
      }
      return {};
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  /**
   * CONSOLE3b: what the session runs beside itself has a tab here too, as in the main window, and a
   * picked tab shows that stream. Read-only, as this window is (D56's one owner across windows): a
   * task its harness can stop has no stop here, where no move is made.
   */
  it('grows the session’s stream tabs, shows a picked one, and offers no stop', async () => {
    show();

    expect(await screen.findByText(/→ background: dev server/)).toBeInTheDocument();
    await userEvent.click(await screen.findByRole('tab', { name: /dev server — background · running/ }));

    expect(await screen.findByText(/listening on 4200/)).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TAIL_SESSION', { payload: { id: 's1a2b3c4/task/bs00' } });
    expect(screen.queryByRole('button', { name: /^Stop / })).toBeNull();
  });

  /**
   * The same head as the main window's and the monitor's: a chat named by what was asked of it, and
   * idle between turns. Found looking at CONSOLE3b: this window said `conversation · working` beside a
   * main window saying `start the dev server · idle`, for the one session.
   */
  it('heads a chat by its opening, and idle between turns, as the other windows do', async () => {
    show();

    expect(await screen.findByRole('heading', { name: 'start the dev server' })).toBeInTheDocument();
    expect(await screen.findByText('idle')).toBeInTheDocument();
  });

  /**
   * FRAME1h, audit DE6: the console sat in a fixed 14 rem well, which could be neither grown for a long
   * console nor hidden for a long conversation. It is the main window's panel now, and what a person makes
   * of it is kept for every detached window, apart from the main window's own panel.
   */
  it('holds its console in a panel that grows and hides, kept for detached windows apart from the main window\'s', async () => {
    show();

    expect(await within(await panelShown()).findByText(/→ background: dev server/)).toBeInTheDocument();
    const edge = within(panel()).getByRole('separator', { name: 'panel height' });
    edge.focus();
    await userEvent.keyboard('{ArrowUp}');
    expect(edge).toHaveAttribute('aria-valuenow', '248');
    expect(window.localStorage.getItem('daoris.detached.panelHeight')).toBe('248');

    await userEvent.click(within(panel()).getByRole('button', { name: 'Hide the panel' }));
    expect(within(panel()).queryByText(/→ background: dev server/)).toBeNull();
    expect(window.localStorage.getItem('daoris.detached.panelClosed')).toBe('1');
    expect(window.localStorage.getItem('daoris.panelHeight')).toBeNull();
    expect(window.localStorage.getItem('daoris.panelClosed')).toBeNull();

    await userEvent.click(within(panel()).getByRole('button', { name: 'Show the panel' }));
    expect(await within(panel()).findByText(/→ background: dev server/)).toBeInTheDocument();
  });

  it('opens on what the last detached window kept', async () => {
    window.localStorage.setItem('daoris.detached.panelClosed', '1');
    window.localStorage.setItem('daoris.detached.panelHeight', '320');
    show();

    expect(await within(await panelShown()).findByRole('button', { name: 'Show the panel' })).toBeInTheDocument();
    await userEvent.click(within(panel()).getByRole('button', { name: 'Show the panel' }));
    expect(within(panel()).getByRole('separator', { name: 'panel height' })).toHaveAttribute('aria-valuenow', '320');
  });

  /**
   * Each window answers the key of the region it has (D118 §4): a detached window's is its console, on
   * Ctrl+J. A read-only window has nowhere to move a view to, so its panel offers no views menu either.
   */
  it('toggles its console on Ctrl+J, and offers no views menu to move it with', async () => {
    show();

    expect(await within(await panelShown()).findByText(/→ background: dev server/)).toBeInTheDocument();
    expect(within(panel()).queryByRole('button', { name: 'views in the panel' })).toBeNull();
    await userEvent.keyboard('{Control>}j{/Control}');
    expect(within(panel()).queryByText(/→ background: dev server/)).toBeNull();
    await userEvent.keyboard('{Control>}j{/Control}');
    expect(await within(panel()).findByText(/→ background: dev server/)).toBeInTheDocument();
  });

  it('opens a hidden console when a stream is picked, as the main window\'s does', async () => {
    window.localStorage.setItem('daoris.detached.panelClosed', '1');
    show();

    await userEvent.click(await screen.findByRole('tab', { name: /dev server — background · running/ }));
    expect(await within(panel()).findByText(/listening on 4200/)).toBeInTheDocument();
  });

  /** The disclosure boundary (D47 §4): a session another machine runs has no console here, so no panel. */
  it('draws no panel for a session another machine runs, and says where it runs', async () => {
    show('laptop/e5f6a7b8');

    expect(await screen.findByText(/Runs on laptop/)).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'the panel' })).toBeNull();
  });
});
