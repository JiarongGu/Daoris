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

import './i18n';
import { App } from './App';
import { keys } from './queries';
import { AT_START } from './setupGuide';
import { WorkspaceScopeProvider } from './scope';
import type { RuleProposal } from './settings/AgentRules';
import type { Consideration } from './signals';

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

function start(considered: Consideration[], proposals: RuleProposal[] = []) {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module === 'DAORIS.DRIVER' && type === 'RULES') return { proposals };
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
    expect(within(sessionsButton()).getByText('2').className).toContain('text-st-open');
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
    const row = await screen.findByRole('button', { name: /Expose a streaming budget.*parked after failed sessions/ });
    expect(screen.getByRole('region', { name: 'What needs you' })).toContainElement(row);
    // Two sessions parked to ask, and the parked quest: the band's count, on the icon of the view that holds it.
    await waitFor(() => expect(within(overviewButton()).getByText('3')).toBeInTheDocument());

    await userEvent.click(row);
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
