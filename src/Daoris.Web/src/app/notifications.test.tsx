import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// NOTIFY2b (D196 points 3 to 5, the notification design §2.1, §3, §4), the way the application holds it: the whole window over a
// mocked shell that tells a terminal's press over the confirmation channel and its own event, and a service whose door holds a
// press of its own. No band under the title bar for a press of either source; the bell counting each press once, in the warning hue
// with the presses' own count while one waits unshown; the press's toast in the focused window, said once, its *Open* showing the
// view at the card, staying until the view opens; and Overview with no badge.

const { invoke, notifyReady, handlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  handlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      handlers.set(`${module}.${type}`, handler);
    },
    useWindowMaximized: () => false,
  };
});

import i18n from '../locales/i18n';
import { App } from './App';
import { AT_START } from '../help/setupGuide';
import { WorkspaceScopeProvider } from '../data/scope';
import type { PushedAttention } from '../work/attention/attention';

const base = { adapter: 'claude-code', created: '2026-10-21T09:00:00Z', updated: '2026-10-21T09:30:00Z', workspace: 'default' };
const SESSIONS = [{ ...base, id: '3f2a91c4', quest: null, repository: 'game', kind: 'chat', state: 'working' }];

const LATER = new Date(Date.now() + 9 * 60_000).toISOString();

/** A terminal's `sessions say`, waiting for the window, as `PERSON_CONFIRMATIONS` answers it. */
const SAY = {
  id: 'c1', module: 'DAORIS.DRIVER', route: 'SESSION_INPUT', asker: { process: 'bash', pid: 4120 }, asked: base.updated, expires: LATER,
  fields: [{ name: '/id', type: 'string', value: '3f2a91c4' }, { name: '/text', type: 'string', value: 'Run the tests.' }],
  named: [{ role: 'session', name: '/id', value: '3f2a91c4' }, { role: 'words', name: '/text', value: 'Run the tests.' }],
};

/** What waits on the person beside it: a park, and the press's own row, which the press's card stands for. */
const ROWS: PushedAttention[] = [
  { kind: 'parked', id: '9b1e0d2a', since: base.updated, workspace: 'default', where: 'game', note: {} },
  { kind: 'go-ahead', id: 'g1', since: base.updated, where: 'default', ask: 'a1', number: 1, goAhead: { kind: 'push', on: 'game', act: 'push' } },
  { kind: 'confirmation', id: 'c1', since: base.updated, where: '', confirmation: { module: 'DAORIS.DRIVER', route: 'SESSION_INPUT', expires: LATER } },
];

/** A press at the service's own door, as `GET /api/confirmations` answers it. */
const SERVICE = {
  id: 'h1', state: 'waiting', method: 'POST', path: '/api/quests/q1/review', route: '/api/quests/{id}/review', act: "a review's verdict",
  values: [], fields: [{ name: '/verdict', type: 'string', value: 'reviewed' }], asked: base.updated, expires: LATER,
};

let waiting: unknown[] = [];
let service: unknown[] = [];
// The `notices` slice's messages partition (NOTIFY2g), keyed by ask; none where the shell pushes no notices.
let messages: Record<string, unknown> | null = null;
const NEWEST = { id: 'm2', at: base.updated, who: 'agent', repository: 'db', line: 'Stop before you land.', level: 'urgent', toEveryone: true };
// Whether the shell tells what waits at the service (NOTIFY2j); an older one answers none of it, and the page polls.
let tells = true;
let keyed = false;

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) {
    return Response.json([{ repository: 'game', workspace: 'default', registered: true, adopted: true, owns: [], accepts: [], packs: [] }]);
  }
  if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
  if (url.startsWith('/api/sessions')) return Response.json(url.includes('daoris%3Ahelp') ? [] : SESSIONS);
  if (url.startsWith('/api/confirmations')) return Response.json(service);
  if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
  return Response.json([]);
}

function start(rows: PushedAttention[] = ROWS) {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (module: string, type: string, request?: { payload?: Record<string, unknown> }) => {
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module === 'DAORIS.DRIVER' && type === 'PERSON_KEY') return { key: keyed || service.length > 0 ? 'k'.repeat(32) : null };
    if (module === 'DAORIS.DRIVER' && type === 'SERVICE_CONFIRMATIONS' && tells) return { seq: 1, waiting: service };
    if (module === 'DAORIS.DRIVER' && type === 'PERSON_CONFIRMATIONS') return { seq: 1, waiting };
    if (module === 'DAORIS.DRIVER' && type === 'PERSON_CONFIRM_SHOWN') return { shown: true };
    if (module === 'DAORIS.DRIVER' && type === 'STATE_SLICE' && request?.payload?.slice === 'attention') {
      return {
        slice: 'attention', epoch: 'e1', status: 'ready',
        partitions: [
          { partition: 'rows', seq: 1, status: 'ready', data: Object.fromEntries(rows.map((row) => [`${row.kind}:${row.id}`, row])) },
          { partition: 'sessions', seq: 1, status: 'ready', data: {} },
          { partition: 'working', seq: 1, status: 'ready', data: {} },
        ],
      };
    }
    if (module === 'DAORIS.DRIVER' && type === 'STATE_SLICE' && request?.payload?.slice === 'notices' && messages) {
      return {
        slice: 'notices', epoch: 'e1', status: 'ready',
        partitions: [
          { partition: 'news', seq: 1, status: 'ready', data: {} },
          { partition: 'messages', seq: 1, status: 'ready', data: messages },
        ],
      };
    }
    if (module === 'DAORIS.DRIVER' && type === 'STATE') {
      return { drivable: ['game'], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {} };
    }
    return {};
  });
  window.localStorage.setItem(AT_START, 'off');
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <App />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

const bell = () => screen.getByRole('button', { name: /^Notifications/ });
const cards = () => screen.queryAllByRole('group', { name: /^A terminal asks/ });
const badges = () => invoke.mock.calls.filter(([module, type]) => module === 'DAORIS.WINDOWS' && type === 'SET_BADGE');
const confirming = (id: string) => act(() => {
  handlers.get('DAORIS.SESSION_ATTENTION')!({ kind: 'Confirming', id, session: null, headline: "A terminal's press waits for you" });
});

describe('Notifications, in the window', () => {
  let focused: ReturnType<typeof vi.spyOn>;
  beforeEach(() => {
    waiting = [SAY];
    service = [];
    tells = true;
    keyed = false;
    messages = null;
    focused = vi.spyOn(document, 'hasFocus').mockReturnValue(false);
  });
  afterEach(async () => {
    cleanup();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    invoke.mockReset();
    handlers.clear();
    window.localStorage.clear();
    window.sessionStorage.clear();
    await i18n.changeLanguage('en');
  });

  it('draws no band across the page for a press of either source: each is its card in Notifications', async () => {
    service = [SERVICE];
    start();

    await waitFor(() => expect(within(bell()).getByText('2')).toBeInTheDocument());
    expect(cards()).toHaveLength(0);

    await userEvent.click(bell());
    await waitFor(() => expect(cards()).toHaveLength(2));
    const presses = screen.getByRole('region', { name: 'Waiting for your confirmation' });
    expect(presses).toContainElement(cards()[0]!);
    expect(within(presses).getByText('A terminal asks to say to Chat (game · 3f2a91c4), as your words, at its next step:')).toBeInTheDocument();
    // The press's own row of What needs you is its card: listed once.
    expect(screen.queryByRole('listitem', { name: /A terminal asks/ })).toBeNull();
  });

  it("wears the warning hue with the presses' count while one waits unshown, and the whole count in open's hue once shown", async () => {
    start();

    // One press unshown, and two other rows: the presses' count, in the warning hue.
    await waitFor(() => expect(within(bell()).getByText('1')).toHaveAttribute('data-tone', 'warn'));
    expect(bell()).toHaveAccessibleName('Notifications: 1 waiting for your confirmation');

    act(() => { handlers.get('DAORIS.CONFIRMATIONS')!({ seq: 2, waiting: [{ ...SAY, shownAt: base.updated }] }); });
    // Each press once: the press, the park and the go-ahead, its row not counted twice.
    await waitFor(() => expect(within(bell()).getByText('3')).toHaveAttribute('data-tone', 'open'));
    expect(bell()).toHaveAccessibleName('Notifications: 3 need you');
    await waitFor(() => expect(badges().at(-1)?.[2]).toEqual({ payload: { count: 3, description: '3 waiting on you', order: expect.any(Number) } }));

    act(() => { handlers.get('DAORIS.CONFIRMATIONS')!({ seq: 3, waiting: [] }); });
    await waitFor(() => expect(within(bell()).getByText('2')).toHaveAttribute('data-tone', 'open'));
  });

  it("says a terminal's press by a toast in the focused window, once, whose Open shows its card, and which stays until then", async () => {
    focused.mockReturnValue(true);
    start();
    await waitFor(() => expect(within(bell()).getByText('1')).toBeInTheDocument());

    confirming('c1');
    confirming('c1');
    const toast = await screen.findByText('A terminal asks to say words to a session as yours: Chat');
    expect(screen.getAllByText('A terminal asks to say words to a session as yours: Chat')).toHaveLength(1);

    await userEvent.click(within(toast.closest('li')!).getByRole('button', { name: 'Open' }));
    await screen.findByRole('tab', { name: 'Notifications', selected: true });
    expect(cards()).toHaveLength(1);
    await waitFor(() => expect(screen.queryByText('A terminal asks to say words to a session as yours: Chat')).toBeNull());
  });

  it('says no toast in a window not focused: the bell counts it', async () => {
    start();
    await waitFor(() => expect(within(bell()).getByText('1')).toBeInTheDocument());

    confirming('c1');
    expect(screen.queryByText(/A terminal asks to say words/)).toBeNull();
  });

  // NOTIFY2j: told by the shell to every window, read once as the window mounts and never polled; one waiting at that read arrived
  // before the window did, and one told after it is said.
  it("says a service's press once, as the shell tells it, counts it, and never polls the service", async () => {
    focused.mockReturnValue(true);
    waiting = [];
    keyed = true;
    start([]);
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SERVICE_CONFIRMATIONS', {}));

    act(() => { handlers.get('DAORIS.SERVICE_CONFIRMATIONS')!({ seq: 2, waiting: [SERVICE] }); });
    expect(await screen.findByText("A terminal asks for a review's verdict.")).toBeInTheDocument();
    await waitFor(() => expect(within(bell()).getByText('1')).toHaveAttribute('data-tone', 'warn'));
    const fetched = (vi.mocked(fetch).mock.calls as unknown as [RequestInfo | URL][]).map(([input]) => String(input));
    expect(fetched.filter((url) => url.startsWith('/api/confirmations'))).toEqual([]);

    act(() => { handlers.get('DAORIS.SERVICE_CONFIRMATIONS')!({ seq: 3, waiting: [] }); });
    await waitFor(() => expect(screen.queryByText("A terminal asks for a review's verdict.")).toBeNull());
  });

  it("polls the service's list where the shell is older than the telling, and counts what waits there", async () => {
    tells = false;
    waiting = [];
    service = [SERVICE];
    start([]);

    await waitFor(() => expect(within(bell()).getByText('1')).toHaveAttribute('data-tone', 'warn'));
    const fetched = (vi.mocked(fetch).mock.calls as unknown as [RequestInfo | URL][]).map(([input]) => String(input));
    expect(fetched).toContain('/api/confirmations');
  });

  it('leads Overview with its summary, each line a door into Notifications at its group, and wears no badge on Overview', async () => {
    start();

    const line = await screen.findByRole('button', { name: /^Waiting for your confirmation1/ });
    const overview = within(screen.getByRole('navigation', { name: 'Views' })).getByRole('button', { name: 'Overview' });
    expect(within(overview).queryByText(/^\d+$/)).toBeNull();

    await userEvent.click(line);
    await screen.findByRole('tab', { name: 'Notifications', selected: true });
    await waitFor(() => expect(cards()).toHaveLength(1));
  });

  // CONFIRMWAIT1c (the confirm-wait design §8.1): a press resting on a plan opens that plan's own page from its card, its own scope.
  it("opens a set-up's repository from its card's door, where its plan is drawn", async () => {
    waiting = [{
      id: 'c4', module: 'DAORIS.DRIVER', route: 'SETUP_PRESS', asker: { process: 'bash', pid: 4120 }, asked: base.updated, expires: null,
      fields: [{ name: '/repository', type: 'string', value: 'game' }], named: [{ role: 'repository', name: '/repository', value: 'game' }],
    }];
    start();
    await waitFor(() => expect(within(bell()).getByText('1')).toBeInTheDocument());
    await userEvent.click(bell());
    await screen.findByRole('tab', { name: 'Notifications', selected: true });

    await userEvent.click(await screen.findByRole('button', { name: 'Look at it now' }));

    await screen.findByRole('tab', { name: 'Details', selected: true });
    expect(within(screen.getByRole('navigation', { name: 'Views' })).getByRole('button', { name: 'Repositories', current: 'page' }))
      .toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'game' })).toBeInTheDocument();
  });

  it('gives its region back from the bell: closed where it was closed, else the view it held', async () => {
    start();
    await waitFor(() => expect(within(bell()).getByText('1')).toBeInTheDocument());

    // The side bar closed: the bell opens it on Notifications, and its next press closes it again.
    await userEvent.click(bell());
    await screen.findByRole('tab', { name: 'Notifications', selected: true });
    await userEvent.click(bell());
    await waitFor(() => expect(screen.queryByRole('tab', { name: 'Notifications' })).toBeNull());
    cleanup();

    // The side bar open on Ask Daoris: the bell shows Notifications there, and its next press gives Ask Daoris back.
    window.localStorage.setItem('daoris.dockClosed', '0');
    start();
    await screen.findByRole('tab', { name: 'Ask Daoris', selected: true });
    await userEvent.click(bell());
    await screen.findByRole('tab', { name: 'Notifications', selected: true });
    await userEvent.click(bell());
    await screen.findByRole('tab', { name: 'Ask Daoris', selected: true });
  });

  /**
   * NOTIFY2g (§2.1, §5): an ask with anything unread wears the bell's dot and is listed in Messages; an urgent message to everyone is
   * said once by a toast in the focused window, whose Open goes to the ask's page.
   */
  it("wears the dot for an unread message, lists it in Messages, and says an urgent one once with a door to its ask", async () => {
    focused.mockReturnValue(true);
    waiting = [];
    messages = { a1: { ask: 'a1', workspace: 'default', teammate: false, unread: 2, newest: NEWEST } };
    start([]);

    await waitFor(() => expect(bell()).toHaveAccessibleName('Notifications: something unread'));
    const said = { ...NEWEST, ask: 'a1', workspace: 'default', teammate: false };
    act(() => { handlers.get('DAORIS.MESSAGE')!(said); });
    act(() => { handlers.get('DAORIS.MESSAGE')!(said); });
    const toast = await screen.findByText('db, urgent, to everyone: Stop before you land.');
    expect(screen.getAllByText('db, urgent, to everyone: Stop before you land.')).toHaveLength(1);

    await userEvent.click(bell());
    const group = await screen.findByRole('region', { name: 'Messages' });
    expect(group).toHaveTextContent('2 unread');
    expect(group).toHaveTextContent('Stop before you land.');

    await userEvent.click(within(toast.closest('li')!).getByRole('button', { name: 'Open' }));
    await waitFor(() => expect(within(screen.getByRole('navigation', { name: 'Views' })).getByRole('button', { name: 'Quests' }))
      .toHaveAttribute('aria-current', 'page'));
  });

  it('is said in Chinese', async () => {
    await i18n.changeLanguage('zh');
    start();

    await waitFor(() => expect(screen.getByRole('button', { name: '通知：1 个等你确认' })).toBeInTheDocument());
  });
});
