import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// Sessions' badge on the activity bar (SESSUX1c, D126 §2.5), the way the application holds it: the whole window over a
// mocked shell that pushes who needs the person (PUSH1d4: judged in .NET, `AttentionStateSliceTests` holds what is counted),
// since the badge is only drawn where Sessions is, and Sessions is the shell's alone (D47 §4). `App.test.tsx` holds the window
// in a browser, where there is no Sessions to badge. And Overview's *What needs you* with the quest the driver parked, and its
// badge (SESSUX1i, §4.6), which only a driver says.

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
import { AT_START } from '../help/setupGuide';
import { WorkspaceScopeProvider } from '../data/scope';
import type { Consideration } from '../data/signals';
import type { RuleProposal } from '../settings/repository/AgentRules';
import type { PushedAttention } from '../work/attention/attention';

const base = { adapter: 'claude-code', created: '2026-10-01T09:00:00Z', updated: '2026-10-01T09:30:00Z', workspace: 'default' };

const SESSIONS = [
  // Parked to ask the person: waiting on you.
  { ...base, id: 'c4a7c4a7', quest: null, repository: 'engine', kind: 'chat', state: 'awaiting-person' },
  // A teammate's, parked to ask them: listed under Working, and not this person's to count.
  { ...base, id: 'laptop/b05y0000', quest: null, repository: 'engine', kind: 'chat', state: 'awaiting-person' },
  { ...base, id: 'w0rk1ng0', quest: 'q2', repository: 'game', kind: 'driven', state: 'working' },
];

/** An ask the declarations proposed and nobody has published (INT4a), as the service lists it. */
const ASKS = [{
  id: '7c1e9a04b2d5', workspace: 'default', sentence: 'Cap the hydration per frame.\n\nThe trace is attached.', state: 'Proposed',
  tier: 'declarations', asked: '2026-09-21T08:00:00Z', updated: '2026-09-21T08:00:00Z',
  links: [], attachments: [], quests: [], proposal: [{ repository: 'engine', score: 3, matched: ['frame'] }],
}];

/** The quest the driver's last look parked on its failed sessions. */
const PARKED: Consideration = {
  quest: 'q1', repository: 'engine', verdict: 'Exhausted', reason: 'engine has failed q1 3 times; it sits until you try again.',
  strikes: 3, since: '2026-10-01T09:21:00+00:00',
};

/**
 * What the shell pushes of the sessions above (PUSH1d4), as .NET judges them: each parked session a row, the teammate's among
 * them; what the Sessions badge counts, this machine's park alone.
 */
const PARKS: PushedAttention[] = SESSIONS.filter((one) => one.state === 'awaiting-person').map((one) => ({
  kind: 'parked', id: one.id, since: one.updated, workspace: 'default', where: one.repository, note: {},
}));
const PARKED_QUEST: PushedAttention = { kind: 'parked-quest', id: 'q1', since: PARKED.since!, where: 'engine', sitting: PARKED };
const COUNTED = { 'session:c4a7c4a7': { kind: 'parked', workspace: 'default' } };

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
  if (url.startsWith('/api/asks')) return Response.json(ASKS);
  if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
  return Response.json([]);
}

/**
 * The window over a shell that pushes who needs the person (PUSH1d4): its snapshot answers `STATE_SLICE` for the attention slice,
 * every other slice keeping its pull. `rows` are the band's, `counted` what the Sessions badge counts, each as .NET judged them.
 */
function start(rows: PushedAttention[], counted: Record<string, unknown> = COUNTED) {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (module: string, type: string, request?: { payload?: Record<string, unknown> }) => {
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module === 'DAORIS.DRIVER' && type === 'STATE_SLICE' && request?.payload?.slice === 'attention') {
      return {
        slice: 'attention', epoch: 'e1', status: 'ready',
        partitions: [
          { partition: 'rows', seq: 1, status: 'ready', data: Object.fromEntries(rows.map((row) => [`${row.kind}:${row.id}`, row])) },
          { partition: 'sessions', seq: 1, status: 'ready', data: counted },
        ],
      };
    }
    if (module === 'DAORIS.DRIVER' && type === 'STATE') {
      return { drivable: ['engine'], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {} };
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

const sessionsButton = () => within(screen.getByRole('navigation', { name: 'Views' })).getByRole('button', { name: 'Sessions' });

describe("Sessions' badge", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  /**
   * It counts the list's first group as .NET counted it: this machine's session parked to ask, and the quest parked on its failed
   * sessions, whose last session the list shows parked. The teammate's park is not the person's to press, though the band lists it.
   */
  it('counts what waits on the person: a session parked to ask, and a quest parked on its failed sessions', async () => {
    start([...PARKS, PARKED_QUEST], { ...COUNTED, 'quest:q1': { kind: 'parked-quest' } });

    await waitFor(() => expect(within(sessionsButton()).getByText('2')).toBeInTheDocument());
    expect(within(sessionsButton()).getByText('2').className).toContain('text-ink-open');
  });

  it('counts the sessions alone where nothing is parked', async () => {
    start(PARKS);

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
    start([...PARKS, PARKED_QUEST]);

    // The band is drawn again as the window's answers arrive, so its row is found afresh rather than inside a held node.
    await screen.findByRole('listitem', { name: 'Expose a streaming budget' });
    // Two sessions parked to ask, and the parked quest: the band's count, on the icon of the view that holds it (UX5 U20), in
    // the waiting hue.
    await waitFor(() => expect(within(overviewButton()).getByText('3')).toBeInTheDocument());
    expect(within(overviewButton()).getByText('3').className).toContain('text-ink-open');
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
    const proposal: RuleProposal = {
      id: 'p0000002', state: 'waiting', action: 'add', scope: 'machine', list: 'allow', rule: 'WebFetch',
      why: 'The docs it needs are on the web.', proposed: '2026-10-01T11:00:00Z',
    };
    start([...PARKS, { kind: 'rule', id: proposal.id, since: proposal.proposed, detail: proposal.why, proposal }]);

    await screen.findByText('an agent asks to do more');
    await waitFor(() => expect(within(overviewButton()).getByText('3')).toBeInTheDocument());
  });

  /**
   * INT4d: an ask waits on a person, and its row is a door into the ask's record in Quests, where the answer is, chosen there as
   * an ask; its row is named by the ask's first line, from the asks the page holds.
   */
  it('opens an ask’s record from its row, chosen as an ask', async () => {
    start([{ kind: 'proposal', id: ASKS[0]!.id, since: ASKS[0]!.asked, workspace: 'default', where: 'default', publishTo: ['engine'], choices: ['engine'] }]);

    const row = await screen.findByRole('listitem', { name: 'Cap the hydration per frame.' });
    expect(within(row).getByText('proposed, not yet published')).toBeInTheDocument();
    await userEvent.click(within(row).getByRole('button', { name: 'Open' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Cap the hydration per frame.' })).toBeInTheDocument();
    expect(within(screen.getByRole('main')).getByRole('button', { name: 'Publish to engine' })).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBe('ask:7c1e9a04b2d5');
  });

  it('holds no parked quest where none is pushed', async () => {
    start(PARKS);

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
    start([...PARKS, PARKED_QUEST]);

    await waitFor(() => expect(within(overviewButton()).getByText('3')).toBeInTheDocument());
    await waitFor(() => expect(badges().at(-1)).toEqual(
      ['DAORIS.WINDOWS', 'SET_BADGE', { payload: { count: 3, description: '3 waiting on you' } }]));
  });

  /**
   * WAITCOUNT1: the band listed an agent's ask for a helper and a second opinion that waits on the person, and the badge and
   * the taskbar did not count them: two answers to "how many need me". One count, every row the band lists, one judgement.
   */
  it('counts an agent’s ask for a helper and a second opinion that wait on the person, as the band lists them', async () => {
    start([
      ...PARKS,
      { kind: 'review', id: 'r3v13w00', since: null, where: '', placed: { session: 'r3v13w00', group: 'review', shown: 'completed', archived: false, teammate: false } },
      { kind: 'helper', id: 'a1b2c3d4', since: '2026-10-01T11:00:00Z', where: 'engine', session: 'c4a7c4a7', title: 'Which test covers the loader?' },
      {
        kind: 'opinion', id: 'r3v13w00', since: '2026-10-01T10:00:00Z', where: 'engine', opinion: {
          session: 'r3v13w00', quest: 'q1', repository: 'engine', since: '2026-10-01T10:00:00Z', auto: false,
          opinion: { state: 'unavailable', holds: true, required: true, code: 'no-reviewer' },
        },
      },
    ]);

    // The two parks, the work ready to review, the helper's ask and the opinion.
    await waitFor(() => expect(within(overviewButton()).getByText('5')).toBeInTheDocument());
    await waitFor(() => expect(badges().at(-1)).toEqual(
      ['DAORIS.WINDOWS', 'SET_BADGE', { payload: { count: 5, description: '5 waiting on you' } }]));
  });

  /**
   * AUTONOMY1b3c (D184 point 3): a tool call a session holds for the person's answer stands its turn still until they answer, or
   * the bound refuses it, so the badge counts it as the band lists it: the window behind others still says a turn waits on them.
   */
  it('counts a tool call a session holds for the person, as the band lists it', async () => {
    const permission = {
      session: 'c4a7c4a7', request: '900', call: null, title: 'npm test', kind: 'execute', since: '2026-10-01T11:00:00Z',
      choices: [{ id: 'allow', name: 'Allow', kind: 'allow_once' }],
    };
    start([...PARKS, { kind: 'permission', id: 'c4a7c4a7#900', since: permission.since, where: 'engine', session: 'c4a7c4a7', permission }]);

    // The two parks, and the held call.
    await waitFor(() => expect(within(overviewButton()).getByText('3')).toBeInTheDocument());
    await waitFor(() => expect(badges().at(-1)).toEqual(
      ['DAORIS.WINDOWS', 'SET_BADGE', { payload: { count: 3, description: '3 waiting on you' } }]));
  });

  it('is never told from a secondary window’s page', async () => {
    window.history.replaceState(null, '', '/?window=monitor');
    start([...PARKS, PARKED_QUEST]);

    await waitFor(() => expect(within(overviewButton()).getByText('3')).toBeInTheDocument());
    expect(badges()).toEqual([]);
  });
});
