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
import type { HelpWhere } from './where';

const HELP = {
  id: 'h1e1p000', quest: null, repository: 'daoris:help', adapter: 'claude-code-acp', state: 'working',
  kind: 'chat', created: '2026-09-29T00:00:00Z', updated: '2026-09-29T00:01:00Z', workspace: 'default',
};

let SESSIONS: unknown[] = [];
let HELPER: string | null = 'claude-code-acp';
let PROPOSALS: unknown[] = [];
const asked: string[] = [];

function respond(url: string): Response {
  asked.push(url);
  // As the ledger answers: an ended session only to a caller that asks for closed ones.
  if (url.startsWith('/api/sessions')) {
    return Response.json(url.includes('includeClosed=true') ? SESSIONS
      : SESSIONS.filter((row) => ['queued', 'starting', 'working', 'awaiting-person'].includes((row as { state: string }).state)));
  }
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
      case 'HELP_PROPOSALS': return { session: HELP.id, proposals: PROPOSALS };
      case 'HELP_APPLY': return { message: 'Applied: `#p1a2b3c4` — Drive `engine`.', applied: true };
      case 'HELP_DISMISS': return { message: 'Not now: the person did not apply `#p1a2b3c4`.' };
      default: return {};
    }
  });
}

function show(where?: Omit<HelpWhere, 'session'>, attending: string | null = null) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <AskDaoris where={where} attending={attending} onGo={vi.fn()} onClose={vi.fn()} />
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

  /**
   * DOCK1d: a question asked from the palette arrives already asked — sent once, as a typed one is,
   * and not again when the frame draws it again; a new question is a new id.
   */
  it('sends a question it is handed once, as if typed, and a new one when handed another', async () => {
    bridge();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const view = (opening: { text: string; id: number }) => (
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <AskDaoris opening={opening} onGo={vi.fn()} onClose={vi.fn()} />
        </Tooltip.Provider>
      </QueryClientProvider>
    );
    const sent = () => invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT').map(([, , request]) => request.payload.text);

    const { rerender } = render(view({ text: 'why is engine held?', id: 1 }));
    await waitFor(() => expect(sent()).toEqual(['why is engine held?']));
    rerender(view({ text: 'why is engine held?', id: 1 }));
    rerender(view({ text: 'and how do I drive it?', id: 2 }));
    await waitFor(() => expect(sent()).toEqual(['why is engine held?', 'and how do I drive it?']));
  });

  /** HELP1b: where the person is goes ahead of their words, and an unchanged screen is not said twice. */
  it('hands the conversation where the person is, once for as long as the screen is the same', async () => {
    // The attended session has ENDED — what the person reads is named all the same (found looking at HELP1b).
    SESSIONS = [HELP, { ...HELP, id: 'p4rk3d00', repository: 'engine', state: 'completed', note: 'Which one?' }];
    bridge();
    show({ view: 'sessions', workspace: 'aurora' }, 'p4rk3d00');

    const box = await screen.findByLabelText('message');
    await userEvent.type(box, 'what is it asking?');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: {
        id: HELP.id, text: 'what is it asking?',
        preface: 'Where the person is now: the Sessions view, workspace `aurora`, attending session `p4rk3d00` in `engine`, which is completed. It says: "Which one?".',
      },
    }));

    await userEvent.type(box, 'and then?');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'and then?' },
    }));
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

  /** HELP1c: what it proposes is a card under the conversation, applied or not by the person's press. */
  it('shows what the conversation proposes, and applies or dismisses it on the person\'s press', async () => {
    SESSIONS = [HELP];
    PROPOSALS = [{
      id: 'p1a2b3c4', kind: 'setting', describe: 'Drive `engine`: a quest addressed to it starts a session on this machine.',
      terminal: 'daoris driver drive engine', why: 'the person asked for engine to be driven',
    }];
    bridge();
    show();

    const cards = await screen.findByRole('list', { name: 'what Ask Daoris proposes' });
    expect(within(cards).getByText('daoris driver drive engine', { selector: 'code' })).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_PROPOSALS', { payload: { session: HELP.id } });

    await userEvent.click(within(cards).getByRole('button', { name: 'apply' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_APPLY', { payload: { id: 'p1a2b3c4' } });
    await userEvent.click(within(cards).getByRole('button', { name: 'not now' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_DISMISS', { payload: { id: 'p1a2b3c4' } });
    PROPOSALS = [];
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
