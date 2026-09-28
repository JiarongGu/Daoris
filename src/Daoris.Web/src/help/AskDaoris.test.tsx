import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// HELP1a (D89): Ask Daoris's conversation, with the bridge mocked as present — the panel exists only in
// the shell, since most of what it reads is this machine's (D47 §4).

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

import '../i18n';
import { AskDaoris } from './AskDaoris';

const HELP = {
  id: 'h1e1p000', quest: null, repository: 'daoris:help', adapter: 'claude-code-acp', state: 'working',
  kind: 'chat', created: '2026-09-29T00:00:00Z', updated: '2026-09-29T00:01:00Z', workspace: 'default',
};

let SESSIONS: unknown[] = [];
let HELPER: string | null = 'claude-code-acp';
const asked: string[] = [];

function respond(url: string): Response {
  asked.push(url);
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/registry')) return Response.json([]);
  throw new Error(`unstubbed request: ${url}`);
}

function bridge(start: { sessionId: string | null; message: string } = { sessionId: HELP.id, message: 'opened' }) {
  invoke.mockImplementation(async (_module: string, type: string) => {
    switch (type) {
      case 'STATE': return { drivable: ['engine'], holds: [], trees: [], helperAdapter: HELPER ?? '' };
      case 'HARNESSES': return { harnesses: [] };
      case 'LINES': return { lines: [], landings: [] };
      case 'START_HELP': return start;
      case 'SESSION_INPUT': return { sent: true };
      case 'SESSION_HISTORY': return { session: HELP.id, events: [], earlier: false, latest: 0 };
      case 'SESSION_QUEUE': return { session: HELP.id, queued: [], taking: false };
      case 'END_CHAT': return { ended: true };
      default: return {};
    }
  });
}

function show() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <AskDaoris onGo={vi.fn()} onClose={vi.fn()} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('Ask Daoris, with an agent named', () => {
  beforeEach(() => {
    SESSIONS = [];
    HELPER = 'claude-code-acp';
    asked.length = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('starts a conversation with the first message, and sends that message to it', async () => {
    bridge();
    show();

    const box = await screen.findByLabelText('message');
    await userEvent.type(box, 'how do I drive a repository?');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'how do I drive a repository?' },
    }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'START_HELP', {});
    // Its conversations are read across every workspace: it belongs to none.
    expect(asked.some((url) => url.includes('repository=daoris%3Ahelp') && !url.includes('workspace='))).toBe(true);
  });

  it('keeps the words in the box, with the driver\'s sentence, when no conversation can start', async () => {
    bridge({ sessionId: null, message: 'Claude Code has no account signed in.' });
    show();

    const box = await screen.findByLabelText('message');
    await userEvent.type(box, 'hello');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));

    expect(await screen.findByText('Claude Code has no account signed in.')).toBeInTheDocument();
    expect((box as HTMLTextAreaElement).value).toBe('hello');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());
  });

  it('carries on the conversation running, and starts again on new conversation', async () => {
    SESSIONS = [HELP];
    bridge();
    show();

    const box = await screen.findByLabelText('message');
    await userEvent.type(box, 'and the line?');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'and the line?' },
    }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'START_HELP', expect.anything());

    await userEvent.click(screen.getByRole('button', { name: 'new conversation' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'END_CHAT', { payload: { id: HELP.id } });
    // Cleared: the starters are back, and the next message opens the next conversation.
    expect(await screen.findByText(/Ask below about Daoris on this machine/)).toBeInTheDocument();
  });

  it('says a conversation that ended has, and that a message starts the next', async () => {
    SESSIONS = [{ ...HELP, state: 'completed' }];
    bridge();
    show();

    const panel = await screen.findByRole('complementary', { name: 'Ask Daoris' });
    expect(await within(panel).findByText('This conversation has ended. A message starts a new one.')).toBeInTheDocument();
  });
});

describe('Ask Daoris, with no agent named', () => {
  beforeEach(() => {
    SESSIONS = [];
    HELPER = null;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('offers its starters and no message box', async () => {
    bridge();
    show();

    expect(await screen.findByText(/Name an agent under Daoris's own AI/)).toBeInTheDocument();
    expect(screen.queryByLabelText('message')).toBeNull();
  });
});
