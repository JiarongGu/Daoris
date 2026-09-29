import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
}];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json([]);
  throw new Error(`unstubbed request: ${url}`);
}

function show() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}><DetachedSession id="s1a2b3c4" notify={() => {}} /></QueryClientProvider>,
  );
}

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
});
