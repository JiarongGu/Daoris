import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// *What needs you* on a machine (UX6c, design §6), over a mocked shell whose last tick the cache holds: the rows only a
// driver can say (a parked quest, a folder held for trust, a widening of the rules, work to review) and the acts that
// settle them where they stand. `OverviewView.test.tsx` holds the band in a browser, where none of these exists.

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

import '../i18n';
import { keys } from '../queries';
import { WorkspaceScopeProvider } from '../scope';
import type { RuleProposal } from '../settings/AgentRules';
import type { Consideration, TrustHold } from '../signals';
import { AttentionBand, type AttentionDoors } from './AttentionBand';
import type { SessionGrouping } from './groups';
import { sessionTitle } from './identity';
import type { Session } from '../api';

const base = { adapter: 'claude-code', created: '2026-10-01T09:00:00Z', workspace: 'default' };

/** Seven sessions ended with work in their trees, one an hour apart, and the quest the tick parked. */
const ENDED = Array.from({ length: 7 }, (_, at) => ({
  ...base, id: `r3v13w0${at}`, quest: null, repository: 'engine', kind: 'chat', state: 'completed',
  updated: `2026-10-01T1${at}:00:00Z`,
}));
const QUESTS = [{
  id: 'q1', from: 'game', to: 'engine', title: 'Expose a streaming budget', body: 'a per-frame cap.', status: 'Taken',
  filed: '2026-09-30T09:00:00Z', updated: '2026-10-01T09:00:00Z', workspace: 'default',
}];

const PARKED: Consideration = {
  quest: 'q1', repository: 'engine', verdict: 'Exhausted', reason: 'engine has failed q1 3 times.', strikes: 3,
  since: '2026-10-01T09:21:00+00:00',
};
const HOLD: TrustHold = { folder: 'C:/somewhere/engine', trustFile: 'C:/somewhere/data/.claude.json', quest: 'q1' };
const WIDENING: RuleProposal = {
  id: 'p0000002', state: 'waiting', action: 'add', scope: 'machine', list: 'allow', rule: 'WebFetch',
  why: 'The docs it needs are on the web.', proposed: '2026-10-01T11:00:00Z',
};

const review = (session: string): SessionGrouping => ({
  session, group: 'review', shown: 'completed', archived: false, teammate: false, work: { commits: 2, uncommitted: 0 },
});

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) {
    return Response.json([{ repository: 'engine', workspace: 'default', registered: true, adopted: true, owns: [], accepts: [], packs: [] }]);
  }
  // The live list holds none of the ended sessions; the list with the ended ones holds them all.
  if (url.startsWith('/api/sessions')) return Response.json(url.includes('includeClosed=true') ? ENDED : []);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  return Response.json([]);
}

function start({ considered = [], untrusted = [], proposals = [], groups = [] }: {
  considered?: Consideration[]; untrusted?: TrustHold[]; proposals?: RuleProposal[]; groups?: SessionGrouping[];
}, props: { doors?: AttentionDoors; notify?: (text: string, kind?: 'ok' | 'error') => void; onSessions?: () => void } = {}) {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (_module: string, type: string, request?: { payload?: Record<string, unknown> }) => {
    if (type === 'RULES') return { proposals };
    if (type === 'SESSION_GROUPS') return { sessions: groups };
    if (type === 'RETRY_QUEST') return { drivable: [], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {}, retried: { quest: 'q1', did: 'marked' } };
    if (type === 'TRUST_FOLDER') return { folder: HOLD.folder, key: 'k', changed: true, verified: true, message: 'Trusted `C:/somewhere/engine` for the agent.' };
    if (type === 'RULE_PROPOSAL') return { path: 'permissions.json', defaults: [], scopes: [], proposals: [{ ...WIDENING, state: request?.payload?.accept ? 'accepted' : 'declined' }] };
    return {};
  });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  // What the last tick told the page, as `ShellSignals` writes it.
  client.setQueryData(keys.considered, considered);
  client.setQueryData(keys.untrusted, untrusted);
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <AttentionBand {...props} />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

/** What the band asked the bridge, by its call's type. */
const asked = () => invoke.mock.calls.map(([, type]) => type as string);

describe('What needs you on a machine', () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  /**
   * 🔴 No row starts a process to find out (design §6.3): the band reads what the frame already holds, the session list's
   * groups and the rules file, and asks nothing else of the machine until the person presses.
   */
  it('asks the machine nothing but the answers it already holds', async () => {
    start({ considered: [PARKED], untrusted: [HOLD], proposals: [WIDENING], groups: [review('r3v13w00')] });
    await screen.findByRole('group', { name: 'Ready for you' });

    expect([...new Set(asked())].sort()).toEqual(['RULES', 'SESSION_GROUPS']);
  });

  /** Review can be many (§6.2): five rows, then the rest counted, a door into Sessions, where they are. */
  it('shows five sessions to review, longest waiting first, then sends the rest to Sessions', async () => {
    const onSessions = vi.fn();
    const reviewDoor = vi.fn();
    start({ groups: ENDED.map((session) => review(session.id)) }, { onSessions, doors: { review: reviewDoor } });
    const ready = await screen.findByRole('group', { name: 'Ready for you' });

    // Named from their records, the list with the ended ones, the longest waiting first.
    const rows = await within(ready).findAllByRole('listitem', { name: sessionTitle(ENDED[0] as Session) });
    expect(within(ready).getAllByRole('listitem')).toHaveLength(5);
    expect(within(rows[0]!).getByText('2 commits to review')).toBeInTheDocument();
    await userEvent.click(within(rows[0]!).getByRole('button', { name: 'Review' }));
    expect(reviewDoor).toHaveBeenCalledWith(expect.objectContaining({ id: 'r3v13w00', kind: 'review' }));
    await userEvent.click(within(ready).getByRole('button', { name: '2 more in Sessions' }));
    expect(onSessions).toHaveBeenCalled();
  });

  it('tries a parked quest again from its row, saying what the driver did', async () => {
    const notify = vi.fn();
    start({ considered: [PARKED] }, { notify });
    const row = await screen.findByRole('listitem', { name: 'Expose a streaming budget' });
    await userEvent.click(within(row).getByRole('button', { name: 'Try again' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      "#q1 will be tried again at the driver's next look. 3 more failed sessions park it again."));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RETRY_QUEST', { payload: { quest: 'q1' } });
  });

  it('trusts a folder from its row once the agent’s question is answered there', async () => {
    const notify = vi.fn();
    start({ untrusted: [HOLD] }, { notify });
    const row = await screen.findByRole('listitem', { name: 'C:/somewhere/engine' });
    await userEvent.click(within(row).getByRole('button', { name: 'Trust this folder…' }));
    expect(asked()).not.toContain('TRUST_FOLDER');
    await userEvent.click(within(row).getByRole('button', { name: 'Trust this folder' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith('Trusted `C:/somewhere/engine` for the agent.', 'ok'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TRUST_FOLDER', { payload: { folder: HOLD.folder, trustFile: HOLD.trustFile } });
  });

  it('declines a widening in a press, and accepts one only after saying what it widens', async () => {
    const notify = vi.fn();
    start({ proposals: [WIDENING] }, { notify });
    const row = await screen.findByRole('listitem', { name: 'allow WebFetch for every session on this machine' });

    await userEvent.click(within(row).getByRole('button', { name: 'Accept…' }));
    expect(asked()).not.toContain('RULE_PROPOSAL');
    await userEvent.click(within(row).getByRole('button', { name: 'Accept' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000002', accept: true } }));
    expect(notify).toHaveBeenCalledWith('Accepted: allow WebFetch for every session on this machine. Sessions started from now on are handed it.');
  });

  it('declines a widening in one press', async () => {
    const notify = vi.fn();
    start({ proposals: [WIDENING] }, { notify });
    const row = await screen.findByRole('listitem', { name: 'allow WebFetch for every session on this machine' });
    await userEvent.click(within(row).getByRole('button', { name: 'Decline' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000002', accept: false } }));
    expect(notify).toHaveBeenCalledWith('Declined: allow WebFetch for every session on this machine. The rules are as they were.');
  });
});
