import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// Ask Daoris follows the window's workspace, and a conversation's focus is its own. Its start carries the workspace the window
// shows, captured at the press, and the focus chosen before the first words; a window on every workspace starts on this machine
// with no ask; its history is the window's workspace unless every workspace's is chosen, each row then labelled; and another
// workspace's conversation goes on in place, its head saying where it started. The bridge is mocked as present, as Ask Daoris's
// own suite mocks it.

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

import i18n from '../locales/i18n';
import { useStateHub } from '../bridge/state/stateHub';
import { WorkspaceScopeProvider, useScope } from '../data/scope';
import { AskDaoris } from './AskDaoris';
import type { AskOpening } from './ask/askTypes';

type Payload = Record<string, unknown>;
type Start = { sessionId: string | null; message: string };

const help = (id: string, workspace: string, state = 'working') => ({
  id, quest: null, repository: 'daoris:help', door: 'claude-code-acp', state, kind: 'chat',
  created: '2026-10-19T09:00:00Z', updated: '2026-10-19T09:01:00Z', workspace,
});

const row = (session: string, workspace: string, title: string, live = false) => ({
  session, title, name: null, opening: title, about: null, created: '2026-10-19T09:00:00Z', last: '2026-10-19T09:01:00Z',
  pinned: null, live, mode: 'restricted', workspace, resumable: true, from: null, handed: null, found: null, foundLine: null,
});

const ACTIVE = ['queued', 'starting', 'working', 'awaiting-person'];
let SESSIONS: ReturnType<typeof help>[] = [];
let ROWS: (ReturnType<typeof row> & { focus?: Payload; focusNext?: Payload | null })[] = [];
// Two workspaces, so the window's switcher offers a choice between them, and *every workspace* is a choice of its own.
const REGISTRY = [{ repository: 'engine', workspace: 'games' }, { repository: 'ledger', workspace: 'work' }];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) {
    return Response.json(url.includes('includeClosed=true') ? SESSIONS : SESSIONS.filter((each) => ACTIVE.includes(each.state)));
  }
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

/** The history as the driver answers it: the workspace named, else every one's; a search over that scope alone. */
function listing(payload: Payload) {
  const named = typeof payload.workspace === 'string' ? payload.workspace.toLowerCase() : null;
  const words = typeof payload.q === 'string' ? payload.q.toLowerCase() : null;
  const rows = ROWS.filter((each) => (named === null || each.workspace === named) && (words === null || each.title.toLowerCase().includes(words)));
  return { conversations: rows, cut: false, total: rows.length, next: null };
}

function bridge(start: (payload: Payload) => Start | Promise<Start> = () => ({ sessionId: null, message: 'not yet' })) {
  invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: Payload }) => {
    const payload = options?.payload ?? {};
    switch (type) {
      case 'STATE': return { drivable: ['engine'], holds: [], trees: [], helperAgent: 'claude-code-acp' };
      case 'HARNESSES': return { harnesses: [] };
      case 'LINES': return { lines: [], landings: [] };
      case 'START_HELP': return start(payload);
      case 'SESSION_INPUT': return { sent: true };
      case 'SESSION_HISTORY': return { session: payload.id, events: [], earlier: false, latest: 0 };
      case 'SESSION_QUEUE': return { session: payload.id, queued: [], taking: false };
      case 'HELP_CONVERSATIONS': return listing(payload);
      default: return {};
    }
  });
}

/** What each press of a route sent, in order. */
const sent = (route: string): Payload[] =>
  invoke.mock.calls.filter(([, type]) => type === route).map(([, , options]) => (options as { payload?: Payload } | undefined)?.payload ?? {});

function Hub() {
  useStateHub();
  return null;
}

/** The window: the workspace it shows, and a press that moves it to *work*, as its switcher would. */
function Window() {
  const { workspace, setWorkspace } = useScope();
  return (
    <p>
      <span>window: {workspace ?? 'every'}</span>
      <button type="button" onClick={() => setWorkspace('work')}>to work</button>
    </p>
  );
}

function show(workspace: string | null, opening?: AskOpening, changeFocus?: number) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Hub />
      <WorkspaceScopeProvider initial={workspace}>
        <Window />
        <Tooltip.Provider>
          <AskDaoris onGo={vi.fn()} onClose={vi.fn()} opening={opening ?? null} changeFocus={changeFocus} />
        </Tooltip.Provider>
      </WorkspaceScopeProvider>
    </QueryClientProvider>,
  );
}

async function say(words: string) {
  await userEvent.type(await screen.findByLabelText('Message'), words);
  await userEvent.click(screen.getByRole('button', { name: 'Send' }));
}

async function openHistory() {
  await userEvent.click(await screen.findByRole('button', { name: 'Back to history' }));
  return screen.findByRole('region', { name: 'Conversation history' });
}

describe('Ask Daoris follows the window’s workspace (ASKSCOPE1d)', () => {
  beforeEach(() => {
    SESSIONS = [];
    ROWS = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(async () => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
    window.sessionStorage.clear();
    await i18n.changeLanguage('en');
  });

  it('starts in the window’s workspace, opened ahead and with the first words', async () => {
    bridge();
    show('games');

    await waitFor(() => expect(sent('START_HELP')).toHaveLength(1));
    await say('what is in games?');

    await waitFor(() => expect(sent('START_HELP')).toHaveLength(2));
    expect(sent('START_HELP')).toEqual([{ workspace: 'games' }, { workspace: 'games' }]);
  });

  it('keeps the workspace captured at the press when the window switches while it starts', async () => {
    const words = 'and the engine’s line?';
    let open: (answer: Start) => void = () => {};
    bridge(() => (sent('START_HELP').length === 1
      ? { sessionId: null, message: 'not yet' }
      : new Promise<Start>((resolve) => { open = resolve; })));
    show('games');

    await waitFor(() => expect(sent('START_HELP')).toHaveLength(1));
    await say(words);
    await waitFor(() => expect(sent('START_HELP')).toHaveLength(2));
    await userEvent.click(screen.getByRole('button', { name: 'to work' }));
    expect(screen.getByText('window: work')).toBeInTheDocument();
    SESSIONS = [help('n3wgame0', 'games')];
    await act(async () => { open({ sessionId: 'n3wgame0', message: 'opened' }); });

    expect(sent('START_HELP')[1]).toEqual({ workspace: 'games' });
    await waitFor(() => expect(sent('SESSION_INPUT')).toContainEqual({ id: 'n3wgame0', text: words }));
    // The window on *work* goes on in it where it is, its head saying where it started.
    expect(await screen.findByText('started in games')).toBeInTheDocument();
    expect(screen.getByLabelText('Message')).toBeInTheDocument();
  });

  it('starts a window on every workspace on this machine, with no ask', async () => {
    const words = 'which agent runs intake?';
    bridge(() => ({ sessionId: 'n3wmach0', message: 'opened' }));
    show(null);

    expect(await screen.findByRole('button', { name: 'Ask Daoris’s focus: this machine' })).toBeInTheDocument();
    SESSIONS = [help('n3wmach0', 'default')];
    await say(words);

    await waitFor(() => expect(sent('SESSION_INPUT')).toContainEqual({ id: 'n3wmach0', text: words }));
    expect(sent('START_HELP').every((payload) => payload.workspace === undefined)).toBe(true);
    expect(screen.queryByText(/Which workspace/)).toBeNull();
    expect(screen.getByText('window: every')).toBeInTheDocument();
  });

  it('starts with the focus chosen before the first words', async () => {
    bridge();
    show('games');
    await waitFor(() => expect(sent('START_HELP')).toHaveLength(1));

    await userEvent.click(await screen.findByRole('button', { name: 'Ask Daoris’s focus: workspace games, this window’s' }));
    await userEvent.keyboard('engine{Enter}');
    expect(screen.getByRole('button', { name: 'Ask Daoris’s focus: repository engine in games' })).toBeInTheDocument();
    await say('what does the engine wait on?');

    await waitFor(() => expect(sent('START_HELP')).toHaveLength(2));
    expect(sent('START_HELP')[1]).toEqual({ workspace: 'games', focus: { kind: 'repository', workspace: 'games', repository: 'engine' } });
  });

  it('starts a task handed from the new conversation’s composer focused on its repository, its words as typed', async () => {
    const engine = { kind: 'repository' as const, workspace: 'games', repository: 'engine' };
    bridge();
    show('games', { text: 'Fix the build', id: 1, draft: true, focus: engine });

    expect(await screen.findByRole('button', { name: 'Ask Daoris’s focus: repository engine in games' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue('Fix the build'));
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(sent('START_HELP').at(-1)).toEqual({ workspace: 'games', focus: engine }));
  });

  it('sends a focus chosen for the conversation opened ahead at once, and opens it ahead again when the driver ended it', async () => {
    bridge(() => {
      SESSIONS = [help('r3adied0', 'games', 'awaiting-person')];
      return { sessionId: 'r3adied0', message: 'opened' };
    });
    const answer = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, options?: { payload?: Payload }) => {
      if (type !== 'HELP_FOCUS') return answer(module, type, options);
      SESSIONS = [help('r3adied0', 'games', 'ended')];
      return { session: 'r3adied0', focus: options!.payload!.focus, focusNext: null, reopens: true };
    });
    show('games');
    await waitFor(() => expect(sent('START_HELP')).toHaveLength(1));

    await userEvent.click(await screen.findByRole('button', { name: /^Ask Daoris’s focus/ }));
    await userEvent.keyboard('engine{Enter}');

    const engine = { kind: 'repository', workspace: 'games', repository: 'engine' };
    await waitFor(() => expect(sent('HELP_FOCUS')).toEqual([{ session: 'r3adied0', focus: engine }]));
    await waitFor(() => expect(sent('START_HELP')).toHaveLength(2));
    expect(sent('START_HELP')[1]).toEqual({ workspace: 'games', focus: engine });
  });

  it('lists the window’s workspace, every one’s labelled when chosen, and searches within the scope', async () => {
    ROWS = [row('h1', 'games', 'what is a workspace?'), row('h2', 'work', 'and the remote?'), row('h3', 'default', 'and before?')];
    bridge();
    show('games');

    const list = await openHistory();
    expect(await within(list).findByText('what is a workspace?')).toBeInTheDocument();
    expect(within(list).queryByText('and the remote?')).toBeNull();
    expect(within(list).queryByText(/in games/)).toBeNull();
    expect(sent('HELP_CONVERSATIONS')).toContainEqual({ workspace: 'games' });

    await userEvent.click(within(list).getByRole('button', { name: 'Showing: This workspace · games' }));
    await userEvent.click(screen.getByRole('option', { name: 'All workspaces' }));
    expect(await within(list).findByText('and the remote?')).toBeInTheDocument();
    expect(within(list).getByRole('heading', { name: 'work' })).toBeInTheDocument();
    expect(within(list).getByRole('heading', { name: 'default' })).toBeInTheDocument();
    expect(within(list).getByRole('heading', { name: 'games · this window' })).toBeInTheDocument();
    expect(sent('HELP_CONVERSATIONS')).toContainEqual({});

    await userEvent.click(within(list).getByRole('button', { name: 'Showing: All workspaces' }));
    await userEvent.click(screen.getByRole('option', { name: 'This workspace · games this window' }));
    await userEvent.type(within(list).getByRole('searchbox', { name: 'Search Ask Daoris’s conversations' }), 'remote');
    expect(await within(list).findByText('No conversation holds “remote”.')).toBeInTheDocument();
    expect(within(list).getByText(/Every conversation in games was searched/)).toBeInTheDocument();
    expect(sent('HELP_CONVERSATIONS')).toContainEqual({ workspace: 'games', q: 'remote' });
  });

  it('goes on in another workspace’s conversation in place, its head saying where it started and its focus waiting', async () => {
    const words = 'and its remote?';
    SESSIONS = [help('h2w0rk00', 'work')];
    ROWS = [{ ...row('h2w0rk00', 'work', 'and the remote?', true), focus: { kind: 'workspace', workspace: 'work' }, focusNext: { kind: 'repository', workspace: 'work', repository: 'ledger' } }];
    bridge();
    show('games');

    const list = await openHistory();
    await userEvent.click(within(list).getByRole('button', { name: 'Showing: This workspace · games' }));
    await userEvent.click(screen.getByRole('option', { name: 'All workspaces' }));
    await userEvent.click(await within(list).findByRole('button', { name: /^and the remote\?/ }));

    const head = await screen.findByRole('navigation', { name: 'Focus' });
    expect(head.textContent?.replace(/\s+/g, ' ').trim()).toBe('This machine › work ◇ this window: games · started in work · ledger next message');
    expect(screen.getByRole('button', { name: 'Ask Daoris’s focus: workspace work, ledger from your next message' })).toBeInTheDocument();
    await say(words);

    await waitFor(() => expect(sent('SESSION_INPUT')).toContainEqual({ id: 'h2w0rk00', text: words }));
    expect(screen.getByText('window: games')).toBeInTheDocument();
  });

  it('opens the focus picker where the palette asked to change the focus, and not before', async () => {
    ROWS = [];
    bridge();
    const view = show('games');
    expect(await screen.findByRole('button', { name: /^Ask Daoris’s focus/ })).toBeInTheDocument();
    expect(screen.queryByRole('listbox', { name: 'Focus' })).toBeNull();
    view.unmount();

    bridge();
    show('games', undefined, 1);
    expect(await screen.findByRole('listbox', { name: 'Focus' })).toBeInTheDocument();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    ROWS = [row('h2', 'work', 'and the remote?')];
    bridge();
    show(null);

    expect(await screen.findByRole('button', { name: '问道衍的聚焦：本机' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: '返回对话历史' }));
    const list = await screen.findByRole('region', { name: '对话历史' });
    expect(await within(list).findByRole('heading', { name: 'work' })).toBeInTheDocument();
    expect(within(list).getByRole('button', { name: '显示：全部工作区' })).toBeInTheDocument();
  });
});
