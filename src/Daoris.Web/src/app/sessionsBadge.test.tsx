import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// Sessions' badge on the activity bar (SESSUX1c, D126 §2.5), the way the application holds it: the whole window over a
// mocked shell whose last tick the cache holds, since the badge is only drawn where Sessions is, and Sessions is the
// shell's alone (D47 §4). `App.test.tsx` holds the window in a browser, where there is no Sessions to badge. And
// Overview's *What needs you* with the quest that tick parked, and its badge (SESSUX1i, §4.6), which only a driver says.

const { invoke, notifyReady } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
}));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
    useShenoraEvent: () => {},
    useWindowMaximized: () => false,
  };
});

import '../locales/i18n';
import { App } from './App';
import { keys } from '../data/queries';
import { AT_START } from '../help/setupGuide';
import { WorkspaceScopeProvider } from '../data/scope';
import type { RuleProposal } from '../settings/repository/AgentRules';
import type { Consideration } from '../data/signals';

const base = { adapter: 'claude-code', created: '2026-10-01T09:00:00Z', updated: '2026-10-01T09:30:00Z', workspace: 'default' };

const SESSIONS = [
  // Parked to ask the person: waiting on you.
  { ...base, id: 'c4a7c4a7', quest: null, repository: 'engine', kind: 'chat', state: 'awaiting-person' },
  // A teammate's, parked to ask them: listed under Working, and not this person's to count.
  { ...base, id: 'laptop/b05y0000', quest: null, repository: 'engine', kind: 'chat', state: 'awaiting-person' },
  { ...base, id: 'w0rk1ng0', quest: 'q2', repository: 'game', kind: 'driven', state: 'working' },
];

/** The last tick's verdicts: one quest parked on its failed sessions, one only waiting its turn. */
const CONSIDERED: Consideration[] = [
  {
    quest: 'q1', repository: 'engine', verdict: 'Exhausted', reason: 'engine has failed q1 3 times; it sits until you try again.',
    strikes: 3, since: '2026-10-01T09:21:00+00:00',
  },
  { quest: 'q3', repository: 'game', verdict: 'Busy', reason: 'game is busy.' },
];

/** The quest that tick parked, as the service lists it. */
const QUESTS = [{
  id: 'q1', from: 'game', to: 'engine', title: 'Expose a streaming budget', body: 'a per-frame cap.', status: 'Taken',
  filed: '2026-09-30T09:00:00Z', updated: '2026-10-01T09:00:00Z', workspace: 'default',
}];

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) {
    return Response.json([{ repository: 'engine', workspace: 'default', registered: true, adopted: true, owns: [], accepts: [], packs: [] }]);
  }
  if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
  if (url.startsWith('/api/sessions')) return Response.json(url.includes('daoris%3Ahelp') ? [] : SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
  return Response.json([]);
}

/** What the driver's own lists say waits on the person besides the tick's: an agent's ask for a helper, and a second opinion. */
type DriverWaits = { helpers?: unknown[]; opinions?: unknown[]; permissions?: unknown[] };

function start(considered: Consideration[], proposals: RuleProposal[] = [], driverWaits: DriverWaits = {}) {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module === 'DAORIS.DRIVER' && type === 'RULES') return { proposals };
    if (module === 'DAORIS.DRIVER' && type === 'HELPER_WAITS') return { waits: driverWaits.helpers ?? [] };
    if (module === 'DAORIS.DRIVER' && type === 'OPINION_WAITS') return { waits: driverWaits.opinions ?? [] };
    if (module === 'DAORIS.DRIVER' && type === 'PERMISSION_WAITS') return { waits: driverWaits.permissions ?? [] };
    if (module === 'DAORIS.DRIVER' && type === 'SESSION_GROUPS' && driverWaits.opinions) {
      // One session to review, so the second opinions are asked at all (the band asks them only while there is one to name).
      return { sessions: [{ session: 'r3v13w00', group: 'review', shown: 'completed', archived: false, teammate: false }] };
    }
    if (module === 'DAORIS.DRIVER' && type === 'STATE') {
      return { drivable: ['engine'], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {} };
    }
    return {};
  });
  window.localStorage.setItem(AT_START, 'off');
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  // What the last tick told the page, as `ShellSignals` writes it.
  client.setQueryData(keys.considered, considered);
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

const sessionsButton = () => within(screen.getByRole('navigation', { name: 'Views' })).getByRole('button', { name: 'Sessions' });

describe("Sessions' badge", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  /**
   * It counts the list's first group: this machine's session parked to ask, and the quest parked on its failed sessions,
   * whose last session the list shows parked. The teammate's park and the quest only waiting its turn are not the
   * person's to press.
   */
  it('counts what waits on the person: a session parked to ask, and a quest parked on its failed sessions', async () => {
    start(CONSIDERED);

    await waitFor(() => expect(within(sessionsButton()).getByText('2')).toBeInTheDocument());
    expect(within(sessionsButton()).getByText('2').className).toContain('text-ink-open');
  });

  it('counts the sessions alone before a tick has said what is parked', async () => {
    start([]);

    await waitFor(() => expect(within(sessionsButton()).getByText('1')).toBeInTheDocument());
  });
});

const overviewButton = () => within(screen.getByRole('navigation', { name: 'Views' })).getByRole('button', { name: 'Overview' });

/**
 * SESSUX1i (D126 §4.6): on 1 October the owner's work stood parked on its failed sessions, and *What needs you* said
 * nothing. It holds the quest the driver's last look parked, after the parked sessions; Overview's badge counts the band,
 * as it always has; and the row's door opens the quest's page, where *Try again* is.
 */
describe("Overview's What needs you, on a machine", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('holds the quest the driver parked, counts it, and opens its page', async () => {
    start(CONSIDERED);

    // The band is drawn again as the window's answers arrive, so its row is found afresh rather than inside a held node.
    await screen.findByRole('listitem', { name: 'Expose a streaming budget' });
    // Two sessions parked to ask, and the parked quest: the band's count, on the icon of the view that holds it.
    await waitFor(() => expect(within(overviewButton()).getByText('3')).toBeInTheDocument());
    const row = screen.getByRole('listitem', { name: 'Expose a streaming budget' });
    expect(screen.getByRole('region', { name: 'What needs you' })).toContainElement(row);
    expect(within(row).getByText('parked after failed sessions')).toBeInTheDocument();

    // UX6c: the row's door is its own press, beside *Try again*, which the row offers in place.
    expect(within(row).getByRole('button', { name: 'Try again' })).toBeInTheDocument();
    await userEvent.click(within(row).getByRole('button', { name: 'Open' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBe('q1');
  });

  /** The badge counts every row the band lists: an agent's proposal to widen the rules was listed and not counted. */
  it('counts an agent’s proposal waiting on the person, as the band lists it', async () => {
    start([], [{
      id: 'p0000002', state: 'waiting', action: 'add', scope: 'machine', list: 'allow', rule: 'WebFetch',
      why: 'The docs it needs are on the web.', proposed: '2026-10-01T11:00:00Z',
    }]);

    await screen.findByText('an agent asks to do more');
    await waitFor(() => expect(within(overviewButton()).getByText('3')).toBeInTheDocument());
  });

  it('holds no parked quest before a tick has said one', async () => {
    start([]);

    await waitFor(() => expect(within(overviewButton()).getByText('2')).toBeInTheDocument());
    expect(within(screen.getByRole('region', { name: 'What needs you' })).queryByText('parked after failed sessions')).toBeNull();
  });
});

/** What the page told the window's frame to wear on its taskbar button. */
const badges = () => invoke.mock.calls.filter(([module, type]) => module === 'DAORIS.WINDOWS' && type === 'SET_BADGE');

/**
 * NOTIFY1b: the owner, on 15 October, asked for *a notification number at the tab icon*. The main window's page tells its
 * frame Overview's count, the one its badge shows, for the taskbar button's overlay and the window's title, with the
 * words a screen reader says of it. A secondary window counts the same and says nothing: the button is the main window's.
 */
describe("The taskbar's count", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
    window.history.replaceState(null, '', '/');
  });

  it('tells the main window’s frame what needs the person, as Overview’s badge counts it', async () => {
    start(CONSIDERED);

    await waitFor(() => expect(within(overviewButton()).getByText('3')).toBeInTheDocument());
    await waitFor(() => expect(badges().at(-1)).toEqual(
      ['DAORIS.WINDOWS', 'SET_BADGE', { payload: { count: 3, description: '3 waiting on you' } }]));
  });

  /**
   * WAITCOUNT1: the band listed an agent's ask for a helper and a second opinion that waits on the person, and the badge and
   * the taskbar did not count them: two answers to "how many need me". One count, with every row the band lists.
   */
  it('counts an agent’s ask for a helper and a second opinion that wait on the person, as the band lists them', async () => {
    start([], [], {
      helpers: [{ id: 'a1b2c3d4', session: 'c4a7c4a7', question: 'Which test covers the loader?', agent: null, at: '2026-10-01T11:00:00Z' }],
      opinions: [{
        session: 'r3v13w00', quest: 'q1', repository: 'engine', since: '2026-10-01T10:00:00Z', auto: false,
        opinion: { state: 'unavailable', holds: true, required: true, code: 'no-reviewer' },
      }],
    });

    // The two the band lists with no tick (see 'holds no parked quest'), the work ready to review, the helper's ask and the opinion.
    await waitFor(() => expect(within(overviewButton()).getByText('5')).toBeInTheDocument());
    await waitFor(() => expect(badges().at(-1)).toEqual(
      ['DAORIS.WINDOWS', 'SET_BADGE', { payload: { count: 5, description: '5 waiting on you' } }]));
  });

  /**
   * AUTONOMY1b3c (D184 point 3): a tool call a session holds for the person's answer stands its turn still until they answer, or
   * the bound refuses it, so the badge counts it as the band lists it: the window behind others still says a turn waits on them.
   */
  it('counts a tool call a session holds for the person, as the band lists it', async () => {
    start([], [], {
      permissions: [{
        session: 'c4a7c4a7', request: '900', call: null, title: 'npm test', kind: 'execute', since: '2026-10-01T11:00:00Z',
        choices: [{ id: 'allow', name: 'Allow', kind: 'allow_once' }],
      }],
    });

    // The two the band lists with no tick (see 'holds no parked quest'), and the held call.
    await waitFor(() => expect(within(overviewButton()).getByText('3')).toBeInTheDocument());
    await waitFor(() => expect(badges().at(-1)).toEqual(
      ['DAORIS.WINDOWS', 'SET_BADGE', { payload: { count: 3, description: '3 waiting on you' } }]));
  });

  it('is never told from a secondary window’s page', async () => {
    window.history.replaceState(null, '', '/?window=monitor');
    start(CONSIDERED);

    await waitFor(() => expect(within(overviewButton()).getByText('3')).toBeInTheDocument());
    expect(badges()).toEqual([]);
  });
});
