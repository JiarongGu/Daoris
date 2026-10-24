import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// ASKSCOPE1d (D158's ASKSCOPE1 note, points 1 and 6): Ask Daoris follows the window's workspace. Its start carries the workspace
// the window shows, captured at the press; a window on every workspace asks which before a start; its history is the window's
// workspace unless every workspace's is chosen, each row then labelled; and another workspace's conversation is named, and gone
// on in only once the window has switched to it. The bridge is mocked as present, as Ask Daoris's own suite mocks it.

const { invoke, notifyReady, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  heard: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => {
  const { publishing } = await import('../test/publishing');
  const published = publishing(invoke, (type, payload) => heard.get(type)?.(payload));
  return {
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke: published, notifyReady }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      if (module === 'DAORIS') heard.set(type, handler);
    },
  };
});

import i18n from '../locales/i18n';
import { useStateHub } from '../bridge/state/stateHub';
import { WorkspaceScopeProvider, useScope } from '../data/scope';
import { AskDaoris } from './AskDaoris';

type Payload = Record<string, unknown>;
type Start = { sessionId: string | null; message: string };

const help = (id: string, workspace: string, state = 'working') => ({
  id, quest: null, repository: 'daoris:help', adapter: 'claude-code-acp', state, kind: 'chat',
  created: '2026-10-19T09:00:00Z', updated: '2026-10-19T09:01:00Z', workspace,
});

const row = (session: string, workspace: string, title: string, live = false) => ({
  session, title, name: null, opening: title, about: null, created: '2026-10-19T09:00:00Z', last: '2026-10-19T09:01:00Z',
  pinned: null, live, mode: 'restricted', workspace, resumable: true, from: null, handed: null, found: null, foundLine: null,
});

const ACTIVE = ['queued', 'starting', 'working', 'awaiting-person'];
let SESSIONS: ReturnType<typeof help>[] = [];
let ROWS: ReturnType<typeof row>[] = [];
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
      case 'STATE': return { drivable: ['engine'], holds: [], trees: [], helperAdapter: 'claude-code-acp' };
      case 'HARNESSES': return { harnesses: [] };
      case 'LINES': return { lines: [], landings: [] };
      case 'START_HELP': return start(payload);
      case 'SESSION_INPUT': return { sent: true };
      case 'SESSION_HISTORY': return { session: payload.id, events: [], earlier: false, latest: 0 };
      case 'SESSION_QUEUE': return { session: payload.id, queued: [], taking: false };
      case 'HELP_CONVERSATIONS': return listing(payload);
      case 'HELP_PROPOSALS': return { session: payload.session, proposals: [] };
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

function show(workspace: string | null) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Hub />
      <WorkspaceScopeProvider initial={workspace}>
        <Window />
        <Tooltip.Provider>
          <AskDaoris onGo={vi.fn()} onClose={vi.fn()} />
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
    // The window on *work* shows where the words went, and goes on in it only once it switches back.
    expect(await screen.findByRole('button', { name: 'Switch to games and continue' })).toBeInTheDocument();
  });

  /**
   * MODELROLE1d2 (D205 §3.5(b)): the conversation its first words opened had its model refused at the open, and the driver opened another
   * in its place, in the same workspace's room. The panel follows it, so the words said next go to the replacement, never to the refused one.
   */
  it('follows a conversation its agent refused at the open to the one opened in its place', async () => {
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
    SESSIONS = [help('r3fused0', 'games')];
    await act(async () => { open({ sessionId: 'r3fused0', message: 'opened' }); });
    await waitFor(() => expect(sent('SESSION_INPUT')).toContainEqual({ id: 'r3fused0', text: words }));

    SESSIONS = [help('r3fused0', 'games', 'failed'), { ...help('n3wgame1', 'games'), created: '2026-10-19T09:02:00Z' }];
    act(() => heard.get('SESSION_REPLACED')?.({ from: 'r3fused0', to: 'n3wgame1' }));
    act(() => heard.get('STATE_CHANGED')?.({ scopes: ['sessions'] }));

    await userEvent.click(await screen.findByRole('button', { name: 'Switch to games and continue' }));
    await say('and the tests?');
    await waitFor(() => expect(sent('SESSION_INPUT')).toContainEqual({ id: 'n3wgame1', text: 'and the tests?' }));
    expect(sent('SESSION_INPUT')).not.toContainEqual({ id: 'r3fused0', text: 'and the tests?' });
  });

  it('asks which workspace before a start on every workspace, and starts none by itself', async () => {
    const words = 'which agent runs intake?';
    bridge(() => ({ sessionId: 'n3wwork0', message: 'opened' }));
    show(null);

    await say(words);

    expect(await screen.findByText(/Which workspace is this conversation in\?/)).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /^Start in / }).map((each) => each.textContent)).toEqual(['Start in games', 'Start in work']);
    expect(screen.getByLabelText('Message')).toHaveValue(words);
    expect(sent('START_HELP')).toEqual([]);

    SESSIONS = [help('n3wwork0', 'work')];
    await userEvent.click(screen.getByRole('button', { name: 'Start in work' }));

    await waitFor(() => expect(sent('SESSION_INPUT')).toContainEqual({ id: 'n3wwork0', text: words }));
    expect(sent('START_HELP')).toEqual([{ workspace: 'work' }]);
    // Asked for the start alone: the window stays on every workspace.
    expect(screen.getByText('window: every')).toBeInTheDocument();
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

    await userEvent.click(within(list).getByRole('button', { name: 'All workspaces' }));
    expect(await within(list).findByText('and the remote?')).toBeInTheDocument();
    expect(within(list).getByText(/in work/)).toBeInTheDocument();
    expect(within(list).getByText(/in default/)).toBeInTheDocument();
    expect(sent('HELP_CONVERSATIONS')).toContainEqual({});

    await userEvent.click(within(list).getByRole('button', { name: 'games' }));
    await userEvent.type(within(list).getByRole('searchbox', { name: 'Search Ask Daoris’s conversations' }), 'remote');
    expect(await within(list).findByText('No conversation holds “remote”.')).toBeInTheDocument();
    expect(within(list).getByText(/Every conversation in games was searched/)).toBeInTheDocument();
    expect(sent('HELP_CONVERSATIONS')).toContainEqual({ workspace: 'games', q: 'remote' });
  });

  it('names another workspace’s conversation, and goes on in it only after the window switches', async () => {
    const words = 'and its remote?';
    SESSIONS = [help('h2w0rk00', 'work')];
    ROWS = [row('h2w0rk00', 'work', 'and the remote?', true)];
    bridge();
    show('games');

    const list = await openHistory();
    await userEvent.click(within(list).getByRole('button', { name: 'All workspaces' }));
    await userEvent.click(await within(list).findByRole('button', { name: /^and the remote\?/ }));

    expect(await screen.findByText(/This conversation is in work, and this window is on games/)).toBeInTheDocument();
    expect(screen.queryByLabelText('Message')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Switch to work and continue' }));
    expect(screen.getByText('window: work')).toBeInTheDocument();
    await say(words);

    await waitFor(() => expect(sent('SESSION_INPUT')).toContainEqual({ id: 'h2w0rk00', text: words }));
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    ROWS = [row('h2', 'work', 'and the remote?')];
    bridge();
    show(null);

    await userEvent.type(await screen.findByLabelText('消息'), '谁负责 intake？');
    await userEvent.click(screen.getByRole('button', { name: '发送' }));

    expect(await screen.findByText(/这段对话属于哪个工作区？/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '在 work 中开始' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: '返回对话历史' }));
    const list = await screen.findByRole('region', { name: '对话历史' });
    expect(await within(list).findByText(/在 work 中/)).toBeInTheDocument();
  });
});
