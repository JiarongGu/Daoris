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

import '../locales/i18n';
import { keys } from '../data/queries';
import { WorkspaceScopeProvider } from '../data/scope';
import type { RuleProposal } from '../settings/repository/AgentRules';
import { type AccountScope, type AccountsAnswer, USE_DEFAULTS } from '../settings/accounts/accounts';
import type { HarnessRoster } from '../bridge/shell';
import type { AccountWaitTick, Consideration, TrustHold } from '../data/signals';
import { AttentionBand, type AttentionDoors } from './AttentionBand';
import type { SessionGrouping } from './groups';
import { sessionTitle } from './identity';
import type { OpinionWait } from './opinion';
import type { HelperWait } from './HelperAsks';
import type { PermissionWait } from './PermissionAsks';
import type { Session } from '../data/api';

const base = { adapter: 'claude-code', created: '2026-10-01T09:00:00Z', workspace: 'default' };

/** Seven sessions ended with work in their trees, one an hour apart, and the quest the tick parked. */
const ENDED = Array.from({ length: 7 }, (_, at) => ({
  ...base, id: `r3v13w0${at}`, quest: null, repository: 'engine', kind: 'chat', state: 'completed',
  updated: `2026-10-01T1${at}:00:00Z`,
}));
/** An intake, the one kind of record that names an ask: it takes no words (INT4b). */
const INTAKE = { ...base, id: 'i4t4k300', quest: null, repository: 'engine', kind: 'chat', state: 'completed', ask: 'a1', updated: '2026-10-01T09:30:00Z' };
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

// UX6d: the accounts as the roster last read them (ROSTER1) and as their files say (TOOL4g). `work` may run on two
// accounts: one cooling, one read signed out; a third, outside its list, read signed in.
const READ = '2026-10-01T10:42:00Z';
const ROSTER: HarnessRoster = {
  settingsPath: 'harnesses.json', adapter: 'claude-code',
  harnesses: [{
    harness: 'claude-code', present: true, product: 'Claude Code', maker: 'Anthropic', signsIn: true, wire: 'pipe',
    profiles: [
      { name: 'account-1', home: 'H/account-1', login: 'out', read: READ },
      { name: 'account-2', home: 'H/account-2', login: 'in', read: READ },
      { name: 'account-4', home: 'H/account-4', login: 'in', read: READ },
    ],
  }],
};
const scope = (over: Partial<AccountScope>): AccountScope => ({
  workspace: null, default: null, list: [], begins: null, use: USE_DEFAULTS, unknown: [], problem: null, near: [], ...over,
});
const ACCOUNTS = (list: string[]): AccountsAnswer => ({
  agents: [{
    agent: 'claude-code', speaks: true, own: {},
    accounts: [{ name: 'account-2', cooling: { until: '2026-10-02T04:42:00Z', stated: true, seen: READ, assumedZone: false, notBelieved: false } }],
    scopes: [scope({}), scope({ workspace: 'default', list, begins: list[0] ?? null })],
  }],
});
/** The intake for an ask, held behind `account-2`'s cool-off, having passed `account-1` signed out. */
const WAIT: AccountWaitTick = {
  agent: 'claude-code', account: 'account-2', workspace: 'default', until: '2026-10-02T04:42:00Z', stated: true,
  quests: [], asks: ['a1'], signedOut: ['account-1'],
};

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) {
    return Response.json([{ repository: 'engine', workspace: 'default', registered: true, adopted: true, owns: [], accepts: [], packs: [] }]);
  }
  // The live list holds none of the ended sessions; the list with the ended ones holds them all.
  if (url.startsWith('/api/sessions')) return Response.json(url.includes('includeClosed=true') ? [...ENDED, INTAKE] : []);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  return Response.json([]);
}

function start({
  considered = [], untrusted = [], proposals = [], groups = [], waits = [], roster = {}, accounts = {}, opinions = [], helpers = [], permissions = [],
}: {
  considered?: Consideration[]; untrusted?: TrustHold[]; proposals?: RuleProposal[]; groups?: SessionGrouping[];
  waits?: AccountWaitTick[]; roster?: HarnessRoster | object; accounts?: AccountsAnswer | object; opinions?: OpinionWait[];
  helpers?: HelperWait[]; permissions?: PermissionWait[];
}, props: { doors?: AttentionDoors; notify?: (text: string, kind?: 'ok' | 'error') => void; onSessions?: () => void } = {}) {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (_module: string, type: string, request?: { payload?: Record<string, unknown> }) => {
    if (type === 'RULES') return { proposals };
    if (type === 'OPINION_WAITS') return { waits: opinions };
    if (type === 'HELPER_WAITS') return { waits: helpers };
    if (type === 'PERMISSION_WAITS') return { waits: permissions };
    if (type === 'SESSION_PERMISSION_ANSWER') {
      return request?.payload?.option === 'allow'
        ? { answered: true, why: null, message: 'Answered `Allow`.' }
        : { answered: false, why: 'unheld', message: 'Nothing was answered.' };
    }
    if (type === 'SESSION_HELPER_APPROVE') return { approved: true, why: null, message: 'yes' };
    if (type === 'SESSION_HELPER_DECLINE') return { declined: false, why: 'approved', message: 'x' };
    if (type === 'ASK_OPINION') return { session: request?.payload?.id, done: true, message: 'A second opinion is asked.' };
    if (type === 'OPINION_ANYWAY') return { session: request?.payload?.id, done: true, message: 'You went on without a settled second opinion.' };
    if (type === 'SESSION_GROUPS') return { sessions: groups };
    if (type === 'HARNESSES') return roster;
    if (type === 'ACCOUNTS') return accounts;
    if (type === 'HARNESS_ACTION' && request?.payload?.action === 'login') return { harness: 'claude-code', action: 'login', started: true };
    if (type === 'HARNESS_ACTION' && request?.payload?.action === 'login-new') return { harness: 'claude-code', action: 'login-new', started: true };
    if (type === 'HARNESS_ACTION' && request?.payload?.action === 'profile-join') {
      // Its default there, and still no place marked first: *first* is a next start the join's answer never names (ACCTUX4b).
      return { harness: 'claude-code', action: 'profile-join', profile: 'account-4', places: [{ workspace: 'default', list: true, default: true }] };
    }
    if (type === 'RETRY_QUEST') return { drivable: [], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {}, retried: { quest: 'q1', did: 'marked' } };
    if (type === 'TRUST_FOLDER') return { folder: HOLD.folder, key: 'k', changed: true, verified: true, message: 'Trusted `C:/somewhere/engine` for the agent.' };
    if (type === 'SESSION_INPUT') return { sent: true };
    if (type === 'RULE_PROPOSAL') return { path: 'permissions.json', defaults: [], scopes: [], proposals: [{ ...WIDENING, state: request?.payload?.accept ? 'accepted' : 'declined' }] };
    return {};
  });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  // What the last tick told the page, as `ShellSignals` writes it.
  client.setQueryData(keys.considered, considered);
  client.setQueryData(keys.untrusted, untrusted);
  client.setQueryData(keys.waits, waits);
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
   * groups, the rules file, the roster's last readings and the accounts' files, and asks nothing else of the machine until
   * the person presses. The roster is asked as it was last read, never refreshed: a refresh asks the agents (UX6d, D150
   * point 5).
   */
  it('asks the machine nothing but the answers it already holds', async () => {
    start({
      considered: [PARKED], untrusted: [HOLD], proposals: [WIDENING], groups: [review('r3v13w00')], waits: [WAIT],
      roster: ROSTER, accounts: ACCOUNTS(['account-2', 'account-1']),
    });
    await screen.findByRole('group', { name: 'Ready for you' });
    await screen.findByRole('listitem', { name: 'Intake for ask #a1' });

    // The second opinion's waits (XAGENT1g): the driver's own reading of the work it owes an opinion, asked only while the list
    // places work To review, the only work one is owed on. The agents' asks for a helper waiting on the person (MIXAGENT2c): the
    // driver's own files. And each unit's milestone (FABRIC-MILESTONE1b), the driver's read of its records, for the unit whose
    // clean-up waits on the person. The tool calls the sessions hold for the person (AUTONOMY1b3c): the driver's memory. None asks
    // an agent.
    expect([...new Set(asked())].sort()).toEqual([
      'ACCOUNTS', 'HARNESSES', 'HELPER_WAITS', 'OPINION_WAITS', 'PERMISSION_WAITS', 'RULES', 'SESSION_GROUPS', 'WORK_STATE',
    ]);
    const roster = invoke.mock.calls.filter(([, type]) => type === 'HARNESSES');
    expect(roster.every(([, , request]) => !request?.payload?.refresh)).toBe(true);
  });

  /**
   * MIXAGENT2c (D168, D163 point 3): an agent's ask for a helper that waits on the person's word is a row waiting for it, with
   * *Let it ask* and *Not now*, the session's page's own presses by the session and the ask; a refusal is said in its words.
   */
  it('lists an agent’s ask for a helper, lets it ask in one press and says a refused no in its words', async () => {
    const notify = vi.fn();
    start({ helpers: [{ id: 'a1', session: 'c0nv0001', question: 'Which test covers the loader?', agent: null, at: '2026-10-01T09:00:00Z' }] },
      { notify, doors: { helper: vi.fn() } });

    const row = await screen.findByRole('listitem', { name: 'Which test covers the loader?' });
    expect(within(row).getByText('an agent asks for a helper')).toBeInTheDocument();
    await userEvent.click(within(row).getByRole('button', { name: 'Let it ask' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HELPER_APPROVE', { payload: { session: 'c0nv0001', ask: 'a1' } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('It has your yes: the driver starts a helper at its next look.'));

    await userEvent.click(within(row).getByRole('button', { name: 'Not now' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HELPER_DECLINE', { payload: { session: 'c0nv0001', ask: 'a1' } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('It has your yes already: the driver starts it at its next look.', 'error'));
  });

  /**
   * AUTONOMY1b3c (D184 point 3): a tool call a session holds for the person's answer holds work, with a press per choice its agent
   * offered, the session's page's own, sending that choice's id; its answer said in the page's words, a refusal by its code.
   */
  it('lists a tool call held for the person, answers it with the choice pressed and says a refused answer in its words', async () => {
    const notify = vi.fn();
    start({
      permissions: [{
        session: 'c0nv0001', request: '900', call: null, title: 'npm test', kind: 'execute', since: '2026-10-01T09:00:00Z',
        choices: [{ id: 'allow', name: 'Allow', kind: 'allow_once' }, { id: 'reject', name: 'Reject', kind: 'reject_once' }],
      }],
    }, { notify, doors: { permission: vi.fn() } });

    const holding = await screen.findByRole('group', { name: 'Holding work' });
    const row = within(holding).getByRole('listitem', { name: 'npm test' });
    expect(within(row).getByText('an agent asks permission')).toBeInTheDocument();
    await userEvent.click(within(row).getByRole('button', { name: 'Allow once' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'SESSION_PERMISSION_ANSWER', { payload: { session: 'c0nv0001', request: '900', option: 'allow' } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Allowed once: the call goes on.'));

    await userEvent.click(within(row).getByRole('button', { name: 'Refuse' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'SESSION_PERMISSION_ANSWER', { payload: { session: 'c0nv0001', request: '900', option: 'reject' } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('It no longer waits: it was answered or released first. Nothing was answered.', 'error'));
  });

  /**
   * XAGENT1g (the second-agent design §9): a second opinion disputed where the work lands by itself waits for the person's word,
   * saying what its gate says; *Ask again* is one press, *Go on anyway…* asks once with their words, and its door opens the
   * session, where every press is.
   */
  it('lists a second opinion that waits on the person, asks again in one press and goes on with their words', async () => {
    const notify = vi.fn();
    const door = vi.fn();
    start({
      groups: [review('s42opin1')],
      opinions: [{
        session: 's42opin1', quest: 'q1', repository: 'engine', since: '2026-10-01T09:00:00Z', auto: true,
        opinion: { state: 'disputed', holds: true, product: 'Codex', maker: 'OpenAI', disputes: 1 },
      }],
    }, { notify, doors: { opinion: door } });

    const row = await screen.findByRole('listitem', { name: 'Expose a streaming budget' });
    expect(row).toHaveTextContent('second opinion');
    expect(row).toHaveTextContent('1 finding by Codex (OpenAI) is disputed');

    await userEvent.click(within(row).getByRole('button', { name: 'Ask again' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ASK_OPINION', { payload: { id: 's42opin1' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('A second opinion is asked.'));

    await userEvent.click(within(row).getByRole('button', { name: 'Go on anyway…' }));
    expect(within(row).getByRole('group', { name: 'Go on anyway…' })).toHaveTextContent('1 disputed finding stays so');
    await userEvent.type(within(row).getByRole('textbox', { name: 'why, if you want to say' }), 'Read it myself');
    await userEvent.click(within(row).getByRole('button', { name: 'Go on anyway' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'OPINION_ANYWAY', { payload: { id: 's42opin1', words: 'Read it myself' } });

    await userEvent.click(within(row).getByRole('button', { name: 'Open' }));
    expect(door).toHaveBeenCalledWith(expect.objectContaining({ id: 's42opin1', kind: 'opinion' }));
  });

  /**
   * UX6d (design §6.2, §9.4): the intake waiting behind a cooling account says so, and signs in the account it passed signed
   * out in one press, whose steps follow under the row (the platform language §4: a step happens where it was started).
   */
  it('signs in an account a waiting start needs from its row, its steps under the row', async () => {
    start({ waits: [WAIT], roster: ROSTER, accounts: ACCOUNTS(['account-2', 'account-1']) });
    const row = await screen.findByRole('listitem', { name: 'Intake for ask #a1' });
    expect(within(row).getByText('waits for an account')).toBeInTheDocument();
    // As the roster last read it, once its answer is in.
    expect(await within(row).findByText(/account-1 read signed out at/)).toBeInTheDocument();

    await userEvent.click(within(row).getByRole('button', { name: 'Sign in to account-1' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'login', profile: 'account-1' },
    });
    expect(await within(row).findByRole('region', { name: 'Signing in to account-1' })).toBeInTheDocument();
  });

  /**
   * OWNSPACE1b-B (D175 point 7): the start held for no Daoris account is a row whose press is *Sign in for Daoris*, running
   * `login-new` in place with the sign-in's steps under the row, and no step 3 there (a new account serves by default).
   */
  it('signs in for Daoris from the row of a start held for no account, its steps under the row', async () => {
    const none: AccountWaitTick = { ...WAIT, account: null, until: null, stated: false, signedOut: [], noAccount: true };
    start({ waits: [none], roster: { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0]!, profiles: [] }] }, accounts: ACCOUNTS([]) });
    const row = await screen.findByRole('listitem', { name: 'Intake for ask #a1' });
    expect(await within(row).findByText(/No Daoris account is signed in for Claude Code/)).toBeInTheDocument();

    await userEvent.click(within(row).getByRole('button', { name: 'Sign in for Daoris' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', { payload: { harness: 'claude-code', action: 'login-new' } });
    expect(await within(row).findByRole('region', { name: 'Signing in to another Claude Code account' })).toBeInTheDocument();
  });

  /** CODEXSIGNIN2: a sign-in refused at its start is said in the panel under the row, whole, with the way to try again or close. */
  it('keeps a refused sign-in’s panel under its row and says the sentence in it', async () => {
    start({ waits: [WAIT], roster: ROSTER, accounts: ACCOUNTS(['account-2', 'account-1']) });
    const base = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: Record<string, unknown> }) => {
      if (type === 'HARNESS_ACTION' && request?.payload?.action === 'login') {
        throw Object.assign(new Error('refused'), { code: 'DRIVER_REFUSED', parameters: { message: 'Another sign-in is running.' } });
      }
      return base(module, type, request);
    });
    const row = await screen.findByRole('listitem', { name: 'Intake for ask #a1' });
    expect(await within(row).findByText(/account-1 read signed out at/)).toBeInTheDocument();

    await userEvent.click(within(row).getByRole('button', { name: 'Sign in to account-1' }));

    const panel = await within(row).findByRole('region', { name: 'Signing in to account-1' });
    expect((await within(panel).findByRole('alert')).textContent).toBe('Another sign-in is running.');
    await userEvent.click(within(panel).getByRole('button', { name: 'Close' }));
    await waitFor(() => expect(within(row).queryByRole('region', { name: 'Signing in to account-1' })).toBeNull());
  });

  /** D130 §3.3: a ready account outside the workspace's list joins it only after the row says what that lets Daoris spend. */
  it('lets a ready account run the workspace after asking once, saying where it runs now', async () => {
    const notify = vi.fn();
    start({ waits: [{ ...WAIT, signedOut: [] }], roster: ROSTER, accounts: ACCOUNTS(['account-2']) }, { notify });
    const row = await screen.findByRole('listitem', { name: 'Intake for ask #a1' });

    await userEvent.click(await within(row).findByRole('button', { name: 'Let account-4 run default…' }));
    expect(asked()).not.toContain('HARNESS_ACTION');
    await userEvent.click(within(row).getByRole('button', { name: "Add to default's list" }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-join', profile: 'account-4', join: ['default'] },
    }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('account-4 runs work in default now.'));
  });

  /** A signed-out account a list holds is a row of its own where no waiting start names it, its door the agent's page. */
  it('lists a signed-out account a list holds, and opens its agent', async () => {
    const door = vi.fn();
    start({ roster: ROSTER, accounts: ACCOUNTS(['account-1']) }, { doors: { 'signed-out': door } });
    const row = await screen.findByRole('listitem', { name: 'account-1' });

    expect(within(row).getByText('signed out')).toBeInTheDocument();
    await userEvent.click(within(row).getByRole('button', { name: 'Claude Code' }));
    expect(door).toHaveBeenCalledWith(expect.objectContaining({ kind: 'signed-out', account: expect.objectContaining({ agent: 'claude-code' }) }));
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

  it('says a refused grant inside the trust question, and toasts nothing (UXFIX2b3b)', async () => {
    const notify = vi.fn();
    start({ untrusted: [HOLD] }, { notify });
    const was = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: Record<string, unknown> }) =>
      type === 'TRUST_FOLDER' ? Promise.reject(new Error('The agent file is locked.')) : was(module, type, request));
    const row = await screen.findByRole('listitem', { name: 'C:/somewhere/engine' });
    await userEvent.click(within(row).getByRole('button', { name: 'Trust this folder…' }));
    await userEvent.click(within(row).getByRole('button', { name: 'Trust this folder' }));

    expect(await within(row).findByRole('alert')).toHaveTextContent('The agent file is locked.');
    expect(within(row).getByRole('button', { name: 'Trust this folder' })).toBeEnabled();
    expect(notify).not.toHaveBeenCalled();
  });

  it('declines a widening in a press, and accepts one only after saying what it widens', async () => {
    const notify = vi.fn();
    start({ proposals: [WIDENING] }, { notify });
    const row = await screen.findByRole('listitem', { name: 'allow WebFetch for every session on this machine' });

    await userEvent.click(within(row).getByRole('button', { name: 'Allow…' }));
    expect(asked()).not.toContain('RULE_PROPOSAL');
    await userEvent.click(within(row).getByRole('button', { name: 'Allow' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000002', accept: true } }));
    // Said as it was pressed, *Allow* (PROPOSEHERE1b): the conversation's card answers through the same owner.
    expect(notify).toHaveBeenCalledWith('Allowed: allow WebFetch for every session on this machine. Sessions started from now on are handed it.');
  });

  /** PROPOSEHERE1c: an *Allow* here tells the session that proposed the rule, as the conversation's own does (PROPOSEHERE1b). */
  const allow = async () => {
    const row = await screen.findByRole('listitem', { name: 'allow WebFetch for every session on this machine' });
    await userEvent.click(within(row).getByRole('button', { name: 'Allow…' }));
    await userEvent.click(within(row).getByRole('button', { name: 'Allow' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000002', accept: true } }));
  };
  const said = () => invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT');

  it('tells the session that proposed the rule once it is allowed', async () => {
    const notify = vi.fn();
    start({ proposals: [{ ...WIDENING, session: 'r3v13w01' }] }, { notify });
    await allow();

    await waitFor(() => expect(said()).toEqual([['DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 'r3v13w01', text: 'The rule you proposed is allowed now: allow WebFetch for every session on this machine. Go on with what you needed it for.' },
    }]]));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Session r3v13w01 is told the rule is allowed, and goes on.', 'ok'));
  });

  it('tells no session the list does not hold', async () => {
    start({ proposals: [{ ...WIDENING, session: 'gone0000' }] });
    await allow();
    expect(said()).toEqual([]);
  });

  it('says an intake is not told, since it takes no words', async () => {
    const notify = vi.fn();
    start({ proposals: [{ ...WIDENING, session: 'i4t4k300' }] }, { notify });
    await allow();

    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('Session i4t4k300 takes no words now, so it is not told')));
    expect(said()).toEqual([]);
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
