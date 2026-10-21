import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// *What needs you* on a machine (UX6c, design §6), over a mocked shell: the rows as the shell pushes them (PUSH1d4, judged in
// .NET), and the acts that settle them where they stand. `OverviewView.test.tsx` holds the band in a browser, which says it is the
// app's.

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

import i18n from '../../locales/i18n';
import { keys } from '../../data/queries';
import { WorkspaceScopeProvider } from '../../data/scope';
import type { RuleProposal } from '../../settings/repository/AgentRules';
import { composeAttention, type HarnessRoster } from '../../bridge/shell';
import type { Consideration, TrustHold } from '../../data/signals';
import { AttentionBand, type AttentionDoors } from './AttentionBand';
import type { PushedAttention } from './attention';
import type { PushedAccountFacts } from './accountWords';
import type { SessionGrouping } from '../groups';
import { sessionTitle } from '../identity';
import type { Session } from '../../data/api';

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

// The rows as the shell pushes them (PUSH1d4): each as `NeedsAPerson` judges it from the facts above.
const row = (kind: PushedAttention['kind'], id: string, since: string | null, over: Partial<PushedAttention> = {}): PushedAttention =>
  ({ kind, id, since, ...over });
const parkedQuest = row('parked-quest', 'q1', PARKED.since!, { where: 'engine', sitting: PARKED });
const held = row('trust', `${HOLD.trustFile}\n${HOLD.folder}`, QUESTS[0]!.filed, {
  where: 'engine', trust: { folder: HOLD.folder, trustFile: HOLD.trustFile }, quest: 'q1',
});
const widening = (proposal: RuleProposal = WIDENING) => row('rule', proposal.id, proposal.proposed, { detail: proposal.why, proposal });
const toReview = (session: string) => row('review', session, ENDED.find((one) => one.id === session)?.updated ?? null, { where: 'engine', placed: review(session) });

// UX6d: the accounts as the roster last read them (ROSTER1). `default` may run on two accounts: one cooling, one read signed out;
// a third, outside its list, read signed in.
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
const facts = (over: Partial<PushedAccountFacts> = {}): PushedAccountFacts => ({
  agent: 'claude-code', product: 'Claude Code', harness: 'claude-code', signsIn: true, outside: null, readFirst: null,
  named: [
    { id: 'account-2', name: null, state: 'cooling', read: READ, until: '2026-10-02T04:42:00Z' },
    { id: 'account-1', name: null, state: 'out', read: READ, until: null },
  ],
  ...over,
});
/** The intake for an ask, held behind `account-2`'s cool-off, having passed `account-1` signed out. */
const waitRow = (over: Partial<PushedAttention> = {}) => row('account-wait', 'wait:claude-code/account-2', READ, {
  where: 'default', wholeWorkspace: true, holding: { quests: [], asks: ['a1'] }, account: facts(), ...over,
});

function respond(url: string): Response {
  // The list with the ended ones holds them all, which names the rows.
  if (url.startsWith('/api/sessions')) return Response.json(url.includes('includeClosed=true') ? [...ENDED, INTAKE] : []);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  return Response.json([]);
}

function start(rows: PushedAttention[], props: {
  doors?: AttentionDoors; notify?: (text: string, kind?: 'ok' | 'error') => void; onSessions?: () => void;
} = {}, roster: HarnessRoster | object = ROSTER) {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (_module: string, type: string, request?: { payload?: Record<string, unknown> }) => {
    if (type === 'SESSION_PERMISSION_ANSWER') {
      return request?.payload?.option === 'allow'
        ? { answered: true, why: null, message: 'Answered `Allow`.' }
        : { answered: false, why: 'unheld', message: 'Nothing was answered.' };
    }
    if (type === 'SESSION_ANSWER_ALONE') {
      return request?.payload?.goOn === true
        ? { answered: true, why: null, message: 'It goes on.' }
        : { answered: false, why: 'unheld', message: 'Nothing was answered.' };
    }
    if (type === 'SESSION_HELPER_APPROVE') return { approved: true, why: null, message: 'yes' };
    if (type === 'SESSION_HELPER_DECLINE') return { declined: false, why: 'approved', message: 'x' };
    if (type === 'ASK_OPINION') return { session: request?.payload?.id, done: true, message: 'A second opinion is asked.' };
    if (type === 'OPINION_ANYWAY') return { session: request?.payload?.id, done: true, message: 'You went on without a settled second opinion.' };
    if (type === 'HARNESSES') return roster;
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
  // What the shell pushed, composed as the slice composes it (PUSH1d4).
  client.setQueryData(keys.attention, composeAttention(new Map<string, unknown>([
    ['rows', Object.fromEntries(rows.map((each) => [`${each.kind}:${each.id}`, each]))], ['sessions', {}],
  ]), true));
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
   * 🔴 No row starts a process to find out (design §6.3), and the band judges nothing (PUSH1d4): its rows are pushed, so it asks the
   * machine none of what they were judged from (the rules, the groups, the driver's waits, the work, the accounts). It names an
   * account from the roster as last read, never refreshed: a refresh asks the agents (UX6d, D150 point 5).
   */
  it('asks the machine nothing but the roster’s last readings, which name the accounts', async () => {
    start([parkedQuest, held, widening(), toReview('r3v13w00'), waitRow()]);
    await screen.findByRole('group', { name: 'Ready for you' });
    await screen.findByRole('listitem', { name: 'Intake for ask #a1' });

    expect([...new Set(asked())].sort()).toEqual(['HARNESSES']);
    const roster = invoke.mock.calls.filter(([, type]) => type === 'HARNESSES');
    expect(roster.every(([, , request]) => !request?.payload?.refresh)).toBe(true);
  });

  /** PUSHUX1: before the owner has judged, the band draws its skeleton, never *Nothing needs you*. */
  it('says nothing is waiting only once who needs the person is judged', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockResolvedValue({});
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <WorkspaceScopeProvider initial={null}><AttentionBand /></WorkspaceScopeProvider>
      </QueryClientProvider>,
    );

    const band = await screen.findByRole('region', { name: 'What needs you' });
    expect(within(band).queryByText('Nothing needs you')).toBeNull();
    client.setQueryData(keys.attention, composeAttention(new Map<string, unknown>([['rows', {}], ['sessions', {}]]), true));
    expect(await within(band).findByText('Nothing needs you')).toBeInTheDocument();
  });

  /**
   * MIXAGENT2c (D168, D163 point 3): an agent's ask for a helper that waits on the person's word is a row waiting for it, with
   * *Let it ask* and *Not now*, the session's page's own presses by the session and the ask; a refusal is said in its words.
   */
  it('lists an agent’s ask for a helper, lets it ask in one press and says a refused no in its words', async () => {
    const notify = vi.fn();
    start([row('helper', 'a1', '2026-10-01T09:00:00Z', { where: '', session: 'c0nv0001', title: 'Which test covers the loader?' })],
      { notify, doors: { helper: vi.fn() } });

    const item = await screen.findByRole('listitem', { name: 'Which test covers the loader?' });
    expect(within(item).getByText('an agent asks for a helper')).toBeInTheDocument();
    await userEvent.click(within(item).getByRole('button', { name: 'Let it ask' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HELPER_APPROVE', { payload: { session: 'c0nv0001', ask: 'a1' } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('It has your yes: the driver starts a helper at its next look.'));

    await userEvent.click(within(item).getByRole('button', { name: 'Not now' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HELPER_DECLINE', { payload: { session: 'c0nv0001', ask: 'a1' } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('It has your yes already: the driver starts it at its next look.', 'error'));
  });

  /**
   * AUTONOMY1b3c (D184 point 3): a tool call a session holds for the person's answer holds work, with a press per choice its agent
   * offered, the session's page's own, sending that choice's id; its answer said in the page's words, a refusal by its code.
   */
  it('lists a tool call held for the person, answers it with the choice pressed and says a refused answer in its words', async () => {
    const notify = vi.fn();
    const permission = {
      session: 'c0nv0001', request: '900', call: null, title: 'npm test', kind: 'execute', since: '2026-10-01T09:00:00Z', until: '2026-10-01T09:15:00Z',
      choices: [{ id: 'allow', name: 'Allow', kind: 'allow_once' }, { id: 'reject', name: 'Reject', kind: 'reject_once' }],
    };
    start([row('permission', 'c0nv0001#900', permission.since, { where: '', session: 'c0nv0001', permission })], { notify, doors: { permission: vi.fn() } });

    const holding = await screen.findByRole('group', { name: 'Holding work' });
    const item = within(holding).getByRole('listitem', { name: 'npm test' });
    expect(within(item).getByText('an agent asks permission')).toBeInTheDocument();
    await userEvent.click(within(item).getByRole('button', { name: 'Allow once' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'SESSION_PERMISSION_ANSWER', { payload: { session: 'c0nv0001', request: '900', option: 'allow' } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Allowed once: the call goes on.'));

    await userEvent.click(within(item).getByRole('button', { name: 'Refuse' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'SESSION_PERMISSION_ANSWER', { payload: { session: 'c0nv0001', request: '900', option: 'reject' } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('It no longer waits: it was answered or released first. Nothing was answered.', 'error'));
  });

  /**
   * FABRIC-PERFORM1j: a node record that does not read is listed with its folder's press, which asks the shell to open it, naming no
   * folder; a refusal (no record kept now) is said in the driver's words.
   */
  it('lists a node record that does not read and opens its folder, saying a refusal in its words', async () => {
    const notify = vi.fn();
    const unreadable = { file: 'k.json', unit: 'quest:q7', at: '2026-10-17T09:00:00Z', said: 'x' };
    start([row('work', 'record#k.json', unreadable.at, { where: 'quest:q7', unreadable })], { notify });

    const item = await screen.findByRole('listitem', { name: 'The record of quest:q7 does not read' });
    await userEvent.click(within(item).getByRole('button', { name: 'Open its folder' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_RECORDS_FOLDER', { payload: {} }));
    expect(notify).not.toHaveBeenCalled();

    invoke.mockRejectedValueOnce(new Error('no unit keeps a node record here now, so there is no folder to open.'));
    await userEvent.click(within(item).getByRole('button', { name: 'Open its folder' }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('no unit keeps a node record here now'), 'error'));
  });

  /**
   * COWORK1e (D188 point 4; the message-urgency design §8.3): a third answer in a row with nobody between holds its session's work for
   * the person, with *Go on with this answer* and *Not now*, the session's page's own, each sending the session and the choice; its
   * answer said in the page's words, a refusal by its code.
   */
  it('lists a third answer alone held for the person, lets it go on or keeps it, and says a refused answer in its words', async () => {
    const notify = vi.fn();
    const answer = { session: 'c0nv0001', agent: 'codex-acp', words: ['w3'], since: '2026-10-01T09:00:00Z' };
    start([row('answer', 'c0nv0001', answer.since, { where: 'engine', session: 'c0nv0001', answer })], { notify, doors: { answer: vi.fn() } });

    const holding = await screen.findByRole('group', { name: 'Holding work' });
    const item = within(holding).getAllByRole('listitem')[0]!;
    expect(within(item).getByText('an answer waits')).toBeInTheDocument();
    expect(item).toHaveTextContent('codex-acp’s answer would take');
    await userEvent.click(within(item).getByRole('button', { name: 'Go on with this answer' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_ANSWER_ALONE', { payload: { session: 'c0nv0001', goOn: true } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('It goes on with this answer, as your word.'));

    await userEvent.click(within(item).getByRole('button', { name: 'Not now' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_ANSWER_ALONE', { payload: { session: 'c0nv0001', goOn: false } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('It no longer waits: it was answered, or its words went on. Nothing was answered.', 'error'));
  });

  /**
   * XAGENT1g (the second-agent design §9): a second opinion disputed where the work lands by itself waits for the person's word,
   * saying what its gate says; *Ask again* is one press, *Go on anyway…* asks once with their words, and its door opens the
   * session, where every press is.
   */
  it('lists a second opinion that waits on the person, asks again in one press and goes on with their words', async () => {
    const notify = vi.fn();
    const door = vi.fn();
    const opinion = {
      session: 's42opin1', quest: 'q1', repository: 'engine', since: '2026-10-01T09:00:00Z', auto: true,
      opinion: { state: 'disputed', holds: true, product: 'Codex', maker: 'OpenAI', disputes: 1 },
    };
    start([row('opinion', 's42opin1', opinion.since, { where: 'engine', opinion })], { notify, doors: { opinion: door } });

    const item = await screen.findByRole('listitem', { name: 'Expose a streaming budget' });
    expect(item).toHaveTextContent('second opinion');
    expect(item).toHaveTextContent('1 finding by Codex (OpenAI) is disputed');

    await userEvent.click(within(item).getByRole('button', { name: 'Ask again' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ASK_OPINION', { payload: { id: 's42opin1' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('A second opinion is asked.'));

    await userEvent.click(within(item).getByRole('button', { name: 'Go on anyway…' }));
    expect(within(item).getByRole('group', { name: 'Go on anyway…' })).toHaveTextContent('1 disputed finding stays so');
    await userEvent.type(within(item).getByRole('textbox', { name: 'why, if you want to say' }), 'Read it myself');
    await userEvent.click(within(item).getByRole('button', { name: 'Go on anyway' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'OPINION_ANYWAY', { payload: { id: 's42opin1', words: 'Read it myself' } });

    await userEvent.click(within(item).getByRole('button', { name: 'Open' }));
    expect(door).toHaveBeenCalledWith(expect.objectContaining({ id: 's42opin1', kind: 'opinion' }));
  });

  /**
   * UX6d (design §6.2, §9.4): the intake waiting behind a cooling account says so, and signs in the account it passed signed
   * out in one press, whose steps follow under the row (the platform language §4: a step happens where it was started).
   */
  it('signs in an account a waiting start needs from its row, its steps under the row', async () => {
    start([waitRow()]);
    const item = await screen.findByRole('listitem', { name: 'Intake for ask #a1' });
    expect(within(item).getByText('waits for an account')).toBeInTheDocument();
    expect(await within(item).findByText(/account-1 read signed out at/)).toBeInTheDocument();

    await userEvent.click(within(item).getByRole('button', { name: 'Sign in to account-1' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'login', profile: 'account-1' },
    });
    expect(await within(item).findByRole('region', { name: 'Signing in to account-1' })).toBeInTheDocument();
  });

  /**
   * OWNSPACE1b-B (D175 point 7): the start held for no Daoris account is a row whose press is *Sign in for Daoris*, running
   * `login-new` in place with the sign-in's steps under the row, and no step 3 there (a new account serves by default).
   */
  it('signs in for Daoris from the row of a start held for no account, its steps under the row', async () => {
    start([waitRow({ id: 'wait:claude-code:no-account', account: facts({ named: [], noAccount: true }) })],
      {}, { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0]!, profiles: [] }] });
    const item = await screen.findByRole('listitem', { name: 'Intake for ask #a1' });
    expect(await within(item).findByText(/No Daoris account is signed in for Claude Code/)).toBeInTheDocument();

    await userEvent.click(within(item).getByRole('button', { name: 'Sign in for Daoris' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', { payload: { harness: 'claude-code', action: 'login-new' } });
    expect(await within(item).findByRole('region', { name: 'Signing in to another Claude Code account' })).toBeInTheDocument();
  });

  /** CODEXSIGNIN2: a sign-in refused at its start is said in the panel under the row, whole, with the way to try again or close. */
  it('keeps a refused sign-in’s panel under its row and says the sentence in it', async () => {
    start([waitRow()]);
    const was = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: Record<string, unknown> }) => {
      if (type === 'HARNESS_ACTION' && request?.payload?.action === 'login') {
        throw Object.assign(new Error('refused'), { code: 'DRIVER_REFUSED', parameters: { message: 'Another sign-in is running.' } });
      }
      return was(module, type, request);
    });
    const item = await screen.findByRole('listitem', { name: 'Intake for ask #a1' });
    expect(await within(item).findByText(/account-1 read signed out at/)).toBeInTheDocument();

    await userEvent.click(within(item).getByRole('button', { name: 'Sign in to account-1' }));

    const panel = await within(item).findByRole('region', { name: 'Signing in to account-1' });
    expect((await within(panel).findByRole('alert')).textContent).toBe('Another sign-in is running.');
    await userEvent.click(within(panel).getByRole('button', { name: 'Close' }));
    await waitFor(() => expect(within(item).queryByRole('region', { name: 'Signing in to account-1' })).toBeNull());
  });

  /** D130 §3.3: a ready account outside the workspace's list joins it only after the row says what that lets Daoris spend. */
  it('lets a ready account run the workspace after asking once, saying where it runs now', async () => {
    const notify = vi.fn();
    start([waitRow({ account: facts({ named: [facts().named[0]!], outside: { id: 'account-4', list: 'default', workspace: 'default' } }) })], { notify });
    const item = await screen.findByRole('listitem', { name: 'Intake for ask #a1' });

    await userEvent.click(await within(item).findByRole('button', { name: 'Let account-4 run default…' }));
    expect(asked()).not.toContain('HARNESS_ACTION');
    await userEvent.click(within(item).getByRole('button', { name: "Add to default's list" }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-join', profile: 'account-4', join: ['default'] },
    }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('account-4 runs work in default now.'));
  });

  /** A signed-out account a list holds is a row of its own where no waiting start names it, its door the agent's page. */
  it('lists a signed-out account a list holds, and opens its agent', async () => {
    const door = vi.fn();
    start([row('signed-out', 'signed-out:claude-code/account-1', null, {
      read: READ, where: 'Claude Code', runs: ['default'], account: facts({ named: [facts().named[1]!] }),
    })], { doors: { 'signed-out': door } });
    const item = await screen.findByRole('listitem', { name: 'account-1' });

    expect(within(item).getByText('signed out')).toBeInTheDocument();
    await userEvent.click(within(item).getByRole('button', { name: 'Claude Code' }));
    expect(door).toHaveBeenCalledWith(expect.objectContaining({ kind: 'signed-out', account: expect.objectContaining({ agent: 'claude-code' }) }));
  });

  /**
   * OVERVIEWNOISE1b, the owner's report: a tree its session recorded that no branch record follows is one row, named by that session
   * as Sessions names it, its block said once in the reader's language and never by the tree's path; its door is where the clean-up's
   * press is.
   */
  it('lists a tree only the clean-up settles under its session’s name, said once in 中文, with its door', async () => {
    await i18n.changeLanguage('zh');
    try {
      const door = vi.fn();
      start([row('work', 'r3v13w00', null, {
        where: 'engine', session: 'r3v13w00', title: 's-e0a8e0fd',
        blocked: {
          unknown: false,
          entries: [{
            kind: 'tree', resource: 'X:/daoris/trees/default/engine/s-e0a8e0fd', code: 'unfollowed', facts: { branch: 'daoris/s-e0a8e0fd' },
            detail: 'its session recorded it, but no branch record follows `daoris/s-e0a8e0fd`, so the look’s tidy never takes it',
          }],
        },
      })], { doors: { work: door } });
      const item = await screen.findByRole('listitem', { name: sessionTitle(ENDED[0] as Session) });

      expect(item).toHaveTextContent('受阻：工作树 s-e0a8e0fd，它的会话记录了它');
      expect(item.textContent?.match(/daoris\/s-e0a8e0fd/g)).toHaveLength(1);
      expect(item).not.toHaveTextContent(/X:\/daoris|its session recorded|tidy/);
      await userEvent.click(within(item).getByRole('button', { name: i18n.t('work.attention.door.open') }));
      expect(door).toHaveBeenCalledWith(expect.objectContaining({ kind: 'work', session: 'r3v13w00' }));
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /** Review can be many (§6.2): five rows, then the rest counted, a door into Sessions, where they are. */
  it('shows five sessions to review, longest waiting first, then sends the rest to Sessions', async () => {
    const onSessions = vi.fn();
    const reviewDoor = vi.fn();
    start(ENDED.map((session) => toReview(session.id)), { onSessions, doors: { review: reviewDoor } });
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
    start([parkedQuest], { notify });
    const item = await screen.findByRole('listitem', { name: 'Expose a streaming budget' });
    await userEvent.click(within(item).getByRole('button', { name: 'Try again' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      "#q1 will be tried again at the driver's next look. 3 more failed sessions park it again."));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RETRY_QUEST', { payload: { quest: 'q1' } });
  });

  it('trusts a folder from its row once the agent’s question is answered there', async () => {
    const notify = vi.fn();
    start([held], { notify });
    const item = await screen.findByRole('listitem', { name: 'C:/somewhere/engine' });
    await userEvent.click(within(item).getByRole('button', { name: 'Trust this folder…' }));
    expect(asked()).not.toContain('TRUST_FOLDER');
    await userEvent.click(within(item).getByRole('button', { name: 'Trust this folder' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith('Trusted `C:/somewhere/engine` for the agent.', 'ok'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TRUST_FOLDER', { payload: { folder: HOLD.folder, trustFile: HOLD.trustFile } });
  });

  it('says a refused grant inside the trust question, and toasts nothing (UXFIX2b3b)', async () => {
    const notify = vi.fn();
    start([held], { notify });
    const was = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: Record<string, unknown> }) =>
      type === 'TRUST_FOLDER' ? Promise.reject(new Error('The agent file is locked.')) : was(module, type, request));
    const item = await screen.findByRole('listitem', { name: 'C:/somewhere/engine' });
    await userEvent.click(within(item).getByRole('button', { name: 'Trust this folder…' }));
    await userEvent.click(within(item).getByRole('button', { name: 'Trust this folder' }));

    expect(await within(item).findByRole('alert')).toHaveTextContent('The agent file is locked.');
    expect(within(item).getByRole('button', { name: 'Trust this folder' })).toBeEnabled();
    expect(notify).not.toHaveBeenCalled();
  });

  it('declines a widening in a press, and accepts one only after saying what it widens', async () => {
    const notify = vi.fn();
    start([widening()], { notify });
    const item = await screen.findByRole('listitem', { name: 'allow WebFetch for every session on this machine' });

    await userEvent.click(within(item).getByRole('button', { name: 'Allow…' }));
    expect(asked()).not.toContain('RULE_PROPOSAL');
    await userEvent.click(within(item).getByRole('button', { name: 'Allow' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000002', accept: true } }));
    // Said as it was pressed, *Allow* (PROPOSEHERE1b): the conversation's card answers through the same owner.
    expect(notify).toHaveBeenCalledWith('Allowed: allow WebFetch for every session on this machine. Sessions started from now on are handed it.');
  });

  /** PROPOSEHERE1c: an *Allow* here tells the session that proposed the rule, as the conversation's own does (PROPOSEHERE1b). */
  const allow = async () => {
    const item = await screen.findByRole('listitem', { name: 'allow WebFetch for every session on this machine' });
    await userEvent.click(within(item).getByRole('button', { name: 'Allow…' }));
    await userEvent.click(within(item).getByRole('button', { name: 'Allow' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000002', accept: true } }));
  };
  const said = () => invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT');

  it('tells the session that proposed the rule once it is allowed', async () => {
    const notify = vi.fn();
    start([widening({ ...WIDENING, session: 'r3v13w01' })], { notify });
    await allow();

    await waitFor(() => expect(said()).toEqual([['DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 'r3v13w01', text: 'The rule you proposed is allowed now: allow WebFetch for every session on this machine. Go on with what you needed it for.' },
    }]]));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Session r3v13w01 is told the rule is allowed, and goes on.', 'ok'));
  });

  it('tells no session the list does not hold', async () => {
    start([widening({ ...WIDENING, session: 'gone0000' })]);
    await allow();
    expect(said()).toEqual([]);
  });

  it('says an intake is not told, since it takes no words', async () => {
    const notify = vi.fn();
    start([widening({ ...WIDENING, session: 'i4t4k300' })], { notify });
    await allow();

    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('Session i4t4k300 takes no words now, so it is not told')));
    expect(said()).toEqual([]);
  });

  it('declines a widening in one press', async () => {
    const notify = vi.fn();
    start([widening()], { notify });
    const item = await screen.findByRole('listitem', { name: 'allow WebFetch for every session on this machine' });
    await userEvent.click(within(item).getByRole('button', { name: 'Decline' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000002', accept: false } }));
    expect(notify).toHaveBeenCalledWith('Declined: allow WebFetch for every session on this machine. The rules are as they were.');
  });
});

/**
 * UX6c (design §6): the band settles from its rows what one press can, each on its record's own route at the service, its
 * sentence said back. Two asks sat proposed on the install until someone pressed through to publish them; now the row publishes.
 */
describe('What needs you settles what one press can', () => {
  /** An ask the declarations proposed and nobody has published (INT4a), and a done held for the person's yes (DRIFT1d2). */
  const PROPOSED = {
    id: '7c1e9a04b2d5', workspace: 'default', sentence: 'Cap the hydration per frame.', state: 'Proposed',
    tier: 'declarations', asked: '2026-09-21T08:00:00Z', updated: '2026-09-21T08:00:00Z',
    links: [], attachments: [], quests: [], proposal: [{ repository: 'engine', score: 3, matched: ['frame'] }],
  };
  const HELD = {
    id: '5e7a11', from: 'game', to: 'engine', title: 'Read the media field names from config', body: 'hard-coded.', workspace: 'default',
    status: 'Done', filed: '2026-09-20T00:00:00Z', updated: '2026-09-21T07:00:00Z', held: true,
  };
  const proposed = row('proposal', PROPOSED.id, PROPOSED.asked, { workspace: 'default', where: 'default', publishTo: ['engine'], choices: ['engine'] });
  const departure = row('departure', HELD.id, HELD.updated, {
    workspace: 'default', where: 'engine', departed: { count: 1, number: 1, reason: 'the names stay in code for now' },
  });
  const goAhead = row('go-ahead', `${PROPOSED.id}#1`, '2026-09-21T09:00:00Z', {
    workspace: 'default', ask: PROPOSED.id, number: 1, goAhead: { kind: 'push', on: 'prod', act: 'the menu entries' },
    detail: 'The menu ships with the release.', session: 's1a2b3c4',
  });
  const posted: { url: string; body: unknown }[] = [];

  function settle(rows: PushedAttention[], { refused = false, sessions = [] as unknown[] } = {}) {
    const notify = vi.fn();
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (init?.method === 'POST') {
        posted.push({ url, body: init.body ? JSON.parse(String(init.body)) : null });
        if (refused) return Response.json({ error: '`engine` is not an adopted repository in `default` — only an adopter can be asked.' }, { status: 400 });
        if (url.endsWith('/publish')) return Response.json({ ask: { ...PROPOSED, state: 'Published' }, message: 'Published as quest `#9a8b7c` to `engine`.' });
        if (url.endsWith('/accept')) return Response.json({ quest: { ...HELD, held: false }, message: 'Accepted the departure on `#5e7a11`.' });
        if (url.includes('/go-aheads/')) return Response.json({ ask: PROPOSED, message: 'Go-ahead 1 on `#7c1e9a04b2d5` approved.' });
      }
      if (url.startsWith('/api/asks')) return Response.json([PROPOSED]);
      if (url.startsWith('/api/quests')) return Response.json([HELD]);
      if (url.startsWith('/api/sessions')) return Response.json(sessions);
      return Response.json([]);
    }));
    invoke.mockResolvedValue({});
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(keys.attention, composeAttention(new Map<string, unknown>([
      ['rows', Object.fromEntries(rows.map((each) => [`${each.kind}:${each.id}`, each]))], ['sessions', {}],
    ]), true));
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkspaceScopeProvider initial={null}><AttentionBand notify={notify} /></WorkspaceScopeProvider>
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    return notify;
  }

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    posted.length = 0;
  });

  /** Never a card saying all clear (§6.1): one line, and what is running, which is the next thing a person asks. */
  it('is one line when nothing needs you, saying what is working', async () => {
    const working = (id: string, state: string) => ({
      id, quest: 'q1', repository: 'engine', adapter: 'stub', kind: 'driven', state, created: '2026-09-21T09:00:00Z', updated: '2026-09-21T09:00:00Z',
    });
    settle([], { sessions: [working('w0rk1ng0', 'working'), working('w0rk1ng1', 'starting')] });
    const band = await screen.findByRole('region', { name: 'What needs you' });
    await waitFor(() => expect(within(band).getByText('2 sessions working')).toBeInTheDocument());
    expect(within(band).getByText('Nothing needs you')).toBeInTheDocument();
    expect(within(band).queryByRole('heading')).toBeNull();
    expect(within(band).queryByRole('list')).toBeNull();
  });

  /** A group with no rows is not shown (§6.2). */
  it('shows the groups that hold rows, each named', async () => {
    settle([proposed, departure]);
    const band = await screen.findByRole('region', { name: 'What needs you' });
    const word = await within(band).findByRole('group', { name: 'Waiting for your word' });
    expect(await within(word).findByRole('listitem', { name: 'Cap the hydration per frame.' })).toBeInTheDocument();
    expect(await within(word).findByRole('listitem', { name: 'Read the media field names from config' })).toBeInTheDocument();
    expect(within(band).queryByRole('group', { name: 'Holding work' })).toBeNull();
    expect(within(band).queryByRole('group', { name: 'Ready for you' })).toBeNull();
  });

  /** §9.4: publishing an ask its declarations proposed is one press, on the ask's own route, its sentence said back. */
  it('publishes a proposed ask from its row, saying the service’s sentence', async () => {
    const notify = settle([proposed]);
    const item = await screen.findByRole('listitem', { name: 'Cap the hydration per frame.' });
    await userEvent.click(within(item).getByRole('button', { name: 'Publish to engine' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith('Published as quest `#9a8b7c` to `engine`.'));
    expect(posted).toEqual([{ url: '/api/asks/7c1e9a04b2d5/publish', body: { to: 'engine' } }]);
  });

  it('accepts a departure from its row', async () => {
    const notify = settle([departure]);
    const item = await screen.findByRole('listitem', { name: 'Read the media field names from config' });
    expect(within(item).getByText('Requirement 1 departed: the names stay in code for now')).toBeInTheDocument();
    await userEvent.click(within(item).getByRole('button', { name: 'Accept the departure' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith('Accepted the departure on `#5e7a11`.'));
    expect(posted.map((one) => one.url)).toEqual(['/api/quests/5e7a11/accept']);
  });

  /** A go-ahead's yes reaches every session on its ask, so it asks once under the row before it is sent. */
  it('approves a go-ahead from its row once it has said what that hands every session', async () => {
    const notify = settle([goAhead]);
    const holding = await screen.findByRole('group', { name: 'Holding work' });
    const item = within(holding).getByRole('listitem', { name: 'push on prod: “the menu entries”' });
    await userEvent.click(within(item).getByRole('button', { name: 'Approve…' }));
    expect(posted).toEqual([]);
    await userEvent.click(within(item).getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith('Go-ahead 1 on `#7c1e9a04b2d5` approved.'));
    expect(posted).toEqual([{ url: '/api/asks/7c1e9a04b2d5/go-aheads/1', body: { answer: 'approved' } }]);
  });

  /**
   * GOAHEADSTALE1: the go-aheads nothing on their asks could still use are refused in one press, with one reason. Each goes through its
   * own door, the service's answer, as its row's Refuse would, saying no park goes on (`goesOn: false`), and nothing is nudged: refusing
   * runs nothing. A go-ahead something still waits for is left as it was.
   */
  describe('the stale go-aheads', () => {
    const stale = (ask: string, number: number) => row('go-ahead', `${ask}#${number}`, '2026-09-20T09:00:00Z', {
      workspace: 'default', ask, number, goAhead: { kind: 'write', on: 'production', act: `configuration ${number}` },
      detail: 'The tile reads its target there.', session: 'g0n30000', stale: { since: '2026-09-25T09:00:00Z' },
    });

    it.each([
      ['en', 'Waiting for your word', '2 go-aheads are stale: nothing on their asks waits for them.', 'Refuse all 2…',
        'why you refuse them', 'Refuse 2 go-aheads', 'Refused 2 stale go-aheads.'],
      ['zh', '等你发话', '有 2 个放行已过时：它们的需求上已没有任何东西在等。', '全部拒绝 2 个…',
        '你拒绝的理由', '拒绝 2 个放行', '已拒绝 2 个过时的放行。'],
    ])('refuses every stale one at once with one reason, and runs nothing (%s)', async (language, group, lead, press, field, move, said) => {
      await i18n.changeLanguage(language);
      try {
        const notify = settle([stale('a1', 1), goAhead, stale('b2', 3)]);
        const word = await screen.findByRole('group', { name: group });
        expect(within(word).getByText(lead)).toBeInTheDocument();
        await userEvent.click(within(word).getByRole('button', { name: press }));
        const confirm = within(word).getByRole('button', { name: move });
        expect(confirm).toBeDisabled();
        await userEvent.type(within(word).getByRole('textbox', { name: field }), 'Done another way.');
        await userEvent.click(confirm);

        await waitFor(() => expect(notify).toHaveBeenCalledWith(said));
        expect(posted).toEqual([
          { url: '/api/asks/a1/go-aheads/1', body: { answer: 'refused', words: 'Done another way.', goesOn: false } },
          { url: '/api/asks/b2/go-aheads/3', body: { answer: 'refused', words: 'Done another way.', goesOn: false } },
        ]);
        expect(asked()).not.toContain('NUDGE');
      } finally {
        await i18n.changeLanguage('en');
      }
    });

    /** One stale go-ahead is its own row's Refuse; the row offers no yes, and its refusal runs nothing either. */
    it('offers a lone stale one its refusal alone, which runs nothing', async () => {
      const notify = settle([stale('a1', 1)]);
      const word = await screen.findByRole('group', { name: 'Waiting for your word' });
      expect(within(word).queryByRole('button', { name: /Refuse all/ })).toBeNull();
      const item = within(word).getByRole('listitem', { name: 'write on production: “configuration 1”' });
      expect(within(item).getByText('stale go-ahead')).toBeInTheDocument();
      expect(within(item).queryByRole('button', { name: 'Approve…' })).toBeNull();
      await userEvent.click(within(item).getByRole('button', { name: 'Refuse…' }));
      await userEvent.click(within(item).getByRole('button', { name: 'Refuse' }));

      await waitFor(() => expect(notify).toHaveBeenCalledWith('Go-ahead 1 on `#7c1e9a04b2d5` approved.'));
      expect(posted).toEqual([{ url: '/api/asks/a1/go-aheads/1', body: { answer: 'refused', goesOn: false } }]);
      expect(asked()).not.toContain('NUDGE');
    });

    /** A refusal the service gives is said inside the ask, and the ones it took stay taken: each is its own answer. */
    it('says a go-ahead the service would not refuse inside the ask, the rest refused', async () => {
      const notify = settle([stale('a1', 1), stale('gone', 2)]);
      const word = await screen.findByRole('group', { name: 'Waiting for your word' });
      vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input);
        if (init?.method === 'POST') {
          posted.push({ url, body: init.body ? JSON.parse(String(init.body)) : null });
          return url.includes('/gone/')
            ? Response.json({ error: 'No ask #gone is held here.' }, { status: 404 })
            : Response.json({ ask: PROPOSED, message: 'Go-ahead 1 refused.' });
        }
        return Response.json([]);
      }));
      await userEvent.click(within(word).getByRole('button', { name: 'Refuse all 2…' }));
      await userEvent.type(within(word).getByRole('textbox', { name: 'why you refuse them' }), 'Done another way.');
      await userEvent.click(within(word).getByRole('button', { name: 'Refuse 2 go-aheads' }));

      expect(await within(word).findByRole('alert')).toHaveTextContent('No ask #gone is held here.');
      expect(notify).toHaveBeenCalledWith('Refused 1 stale go-ahead.');
      expect(posted.map((one) => one.url)).toEqual(['/api/asks/a1/go-aheads/1', '/api/asks/gone/go-aheads/2']);
    });
  });

  /** A refusal is the service's sentence, verbatim, and the row stays as it was. */
  it('says a refused publish in the service’s words', async () => {
    const notify = settle([proposed], { refused: true });
    const item = await screen.findByRole('listitem', { name: 'Cap the hydration per frame.' });
    await userEvent.click(within(item).getByRole('button', { name: 'Publish to engine' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      '`engine` is not an adopted repository in `default` — only an adopter can be asked.', 'error'));
    expect(within(item).getByRole('button', { name: 'Publish to engine' })).toBeEnabled();
  });
});
