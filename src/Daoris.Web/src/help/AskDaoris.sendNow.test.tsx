import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// Ask Daoris's composer offers *Send now* while a reply runs, as a conversation's composer does: the words go into the running turn
// where its agent steers. The bridge is mocked as present, as Ask Daoris's own suite mocks it.

const { invoke, notifyReady, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  heard: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => {
  const { publishing } = await import('../test/publishing');
  const through = publishing(invoke, (type, payload) => heard.get(type)?.(payload));
  return {
    ...(await import('../test/sliceShell')).mockedShell(invoke, { notifyReady, through }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      if (module === 'DAORIS') heard.set(type, handler);
    },
  };
});

import { useStateHub } from '../bridge/state/stateHub';
import { AskDaoris } from './AskDaoris';

const HELP = {
  id: 'h1e1p000', quest: null, repository: 'daoris:help', door: 'claude-code-acp', state: 'working',
  kind: 'chat', created: '2026-09-29T00:00:00Z', updated: '2026-09-29T00:01:00Z', workspace: 'default',
};

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json([HELP]);
  if (url.startsWith('/api/registry') || url.startsWith('/api/quests') || url.startsWith('/api/asks')) return Response.json([]);
  throw new Error(`unstubbed request: ${url}`);
}

/** A reply running on a door whose agent steers: a word now goes into the running turn. */
function bridge(steers: boolean) {
  invoke.mockImplementation(async (_module: string, type: string) => {
    switch (type) {
      case 'STATE': return { drivable: ['engine'], holds: [], trees: [], helperAgent: 'claude-code-acp' };
      case 'HARNESSES': return { harnesses: [] };
      case 'LINES': return { lines: [], landings: [] };
      case 'SESSION_INPUT': return { sent: true };
      case 'SESSION_HISTORY': return { session: HELP.id, events: [], earlier: false, latest: 0 };
      case 'SESSION_QUEUE': return { session: HELP.id, queued: [], taking: true, reaches: 'next-step', why: null, steers };
      case 'HELP_PUBLISHED': return { session: HELP.id, published: [] };
      default: return {};
    }
  });
}

function Hub() {
  useStateHub();
  return null;
}

function show() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Hub />
      <Tooltip.Provider>
        <AskDaoris attending={null} onGo={vi.fn()} onClose={vi.fn()} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('Ask Daoris, while a reply runs', () => {
  beforeEach(() => vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input)))));
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('offers Send now, sending the words into the running turn', async () => {
    bridge(true);
    show();

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'use the index instead');
    await userEvent.click(await screen.findByRole('button', { name: 'Send now' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'use the index instead', level: 'now' },
    }));
  });
});
