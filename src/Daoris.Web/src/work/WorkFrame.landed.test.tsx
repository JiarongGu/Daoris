import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// A session's page header and its acts, reviewing what a session landed, and acting on it, in the Work frame, one part of
// its suite (WEBSPEED1 cut the one file, which ran 181 s alone, into parts that run beside each other). The bridge is mocked
// as present, because the frame does not exist without one (D55); what the parts share is in `test/workFrameBridge.ts`
// and `test/workFrameKit.tsx`.
vi.mock('@shenora/react', async () => (await import('../test/workFrameBridge')).shenoraMock());

import { watchScreen } from '../test/onScreen';
import { eventHandlers, invoke } from '../test/workFrameBridge';
import {
  CHAT, DRIVEN, DRIVER_STATE, PARKED, QUESTS, REGISTRY, ROSTER, STRUCTURED_ROSTER, show, userEvent,
} from '../test/workFrameKit';
import { DiffPane } from './review/DiffPane';
import { WorkFrame } from './WorkFrame';

let SESSIONS: unknown[] = [DRIVEN];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

// 🔴 Every test starts from a viewer with nothing remembered. The frame keeps its layout per viewer,
// and a test that hid the console left it hidden for whichever console test ran next: four failed
// in a shuffled order (UX5, 2026-09-26), and passed in file order only by luck of the order.
afterEach(() => window.localStorage.clear());

/**
 * The session's page header, and every act where its session is (SESSUX1d, D126 §3): a running driven session is
 * stopped where it is read, asking once; a parked quest is tried again from its session; a row's acts attend their
 * session and do their part there.
 */
describe('a session’s page header and its acts', () => {
  beforeEach(() => {
    SESSIONS = [DRIVEN];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    window.localStorage.removeItem('daoris.drafts');
  });

  /** The frame with the application's selection held in state, as `App` holds it, so a row's act can attend another. */
  function Attending({ initial, terminal = false }: { initial: string; terminal?: boolean }) {
    const [selected, setSelected] = useState<string | null>(initial);
    return <WorkFrame selected={selected} onSelect={setSelected} notify={() => {}} terminal={terminal} />;
  }
  const attending = (initial: string, terminal = false) => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return render(<QueryClientProvider client={client}><Tooltip.Provider><Attending initial={initial} terminal={terminal} /></Tooltip.Provider></QueryClientProvider>);
  };

  /**
   * M1, the audit's first finding: a running driven session had no stop in Sessions, and its one stop was on its quest's
   * page. Its page header's *Stop…* asks once, saying what follows for a session that has not taken its quest yet, and
   * stops nothing until its second press.
   */
  it('stops a running driven session from its page header, asking once and saying what follows', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'STOP_SESSION' ? { stopped: true } : DRIVER_STATE));
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    await userEvent.click(acts.getByRole('button', { name: 'Stop…' }));
    const ask = screen.getByRole('group', { name: 'stop this session' });
    expect(ask).toHaveTextContent(
      'Stops the session now. The driver will not start #abc123 again on this machine until you choose Try again; another machine may still take it.');
    await userEvent.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(screen.queryByRole('group', { name: 'stop this session' })).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', expect.anything());

    await userEvent.click(acts.getByRole('button', { name: 'Stop…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Stop session' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', { payload: { id: 's1a2b3c4' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('session s1a2b3c4 is being stopped — the record will say the person ended it.'));
    // UXFIX2: it closes once the stop lands, never on the press.
    await waitFor(() => expect(screen.queryByRole('group', { name: 'stop this session' })).toBeNull());
  });

  /**
   * UXFIX2 (the second-opinion review): the stop's ask takes the focus to what follows, gives it back to *Stop…* when put
   * down, and a refused stop is said inside it, word for word, where it was pressed, rather than in a toast; it stays open.
   */
  it('gives the focus back to Stop… when put down, and says a refused stop inside its ask', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type !== 'STOP_SESSION') return DRIVER_STATE;
      throw Object.assign(new Error('fallback'), { code: 'DRIVER_REFUSED', parameters: { message: 'Session `s1a2b3c4` already ended.' } });
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    await user.click(acts.getByRole('button', { name: 'Stop…' }));
    const ask = screen.getByRole('group', { name: 'stop this session' });
    await waitFor(() => expect(within(ask).getByText(/^Stops the session now/)).toHaveFocus());
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('group', { name: 'stop this session' })).toBeNull();
    expect(acts.getByRole('button', { name: 'Stop…' })).toHaveFocus();

    await user.click(acts.getByRole('button', { name: 'Stop…' }));
    await user.click(screen.getByRole('button', { name: 'Stop session' }));
    const refused = await within(screen.getByRole('group', { name: 'stop this session' })).findByRole('alert');
    expect(refused).toHaveTextContent('Session s1a2b3c4 already ended.');
    expect(notify).not.toHaveBeenCalledWith(expect.stringContaining('already ended'), 'error');
    expect(screen.getByRole('button', { name: 'Stop session' })).toBeEnabled();
  });

  /**
   * MIXAGENT1b-i (D163): *Go on with another agent…* folds into the head's ⋯ and asks under the head: the agent chip lists the
   * agents but the conversation's own; *Hand over* asks `SESSION_HAND_OVER`, and the page attends the conversation it opened.
   */
  const TWO_AGENTS = {
    ...ROSTER,
    harnesses: [
      { harness: 'stub', present: true, version: 'stub 1.0.0', problem: null, machineDefault: null, profiles: [], structured: true },
      { harness: 'codex', present: true, version: 'codex 1.0.0', problem: null, machineDefault: null, profiles: [{ name: 'team', home: 'x', login: 'in' }] },
    ],
  };

  const openHandOver = async (name: string) => {
    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    expect(acts.queryByRole('button', { name: 'Stop and hand over…' })).toBeNull();
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name }));
    return { user, ask: await screen.findByRole('group', { name: 'go on with another agent' }) };
  };

  it('hands an idle conversation over from the head: the ask lists the other agents, and the page attends the new one', async () => {
    SESSIONS = [{ ...CHAT, state: 'completed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return TWO_AGENTS;
      if (type === 'SESSION_HAND_OVER') return { sessionId: 'n3wn3w00', kept: true, why: null, message: 'goes on.' };
      return DRIVER_STATE;
    });
    const { onSelect } = show('c0ffee11');

    const { user, ask } = await openHandOver('Go on with another agent…');
    expect(within(ask).getByRole('button', { name: /Agent and account: codex · / })).toBeInTheDocument();
    await user.click(within(ask).getByRole('button', { name: 'Hand over' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', { payload: { id: 'c0ffee11', agent: 'codex', why: 'press' } });
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('n3wn3w00'));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', expect.anything());
  });

  it('stops and hands over a conversation that works: the hand-over is asked first, then its turn is stopped', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return TWO_AGENTS;
      if (type === 'SESSION_HAND_OVER') return { sessionId: null, kept: true, why: null, message: 'kept.' };
      if (type === 'CANCEL_TURN') return { cancelled: true, withdrawn: [] };
      return DRIVER_STATE;
    });
    show('c0ffee11');

    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    expect(acts.getByRole('button', { name: 'Stop…' })).toBeInTheDocument();
    const user = userEvent.setup();
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Stop and hand over…' }));
    const ask = await screen.findByRole('group', { name: 'go on with another agent' });
    await user.click(within(ask).getByRole('button', { name: 'Hand over' }));

    await waitFor(() => expect(within(screen.getByRole('group', { name: 'go on with another agent' }))
      .getByText('It goes on with codex when its turn ends, in a conversation of its own.')).toBeInTheDocument());
    expect(invoke.mock.calls.map((call) => call[1]).filter((type) => type === 'SESSION_HAND_OVER' || type === 'CANCEL_TURN'))
      .toEqual(['SESSION_HAND_OVER', 'CANCEL_TURN']);
  });

  it('says a refused hand-over in the ask and keeps the choice', async () => {
    SESSIONS = [{ ...CHAT, state: 'completed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return TWO_AGENTS;
      if (type === 'SESSION_HAND_OVER') return { sessionId: null, kept: false, why: 'tree-gone', message: 'x' };
      return DRIVER_STATE;
    });
    show('c0ffee11');

    const { user, ask } = await openHandOver('Go on with another agent…');
    await user.click(within(ask).getByRole('button', { name: 'Hand over' }));

    expect(await within(ask).findByRole('alert')).toHaveTextContent('The folder it worked in is gone, so there is nothing to go on in.');
    expect(within(ask).getByRole('button', { name: 'Hand over' })).toBeEnabled();
    expect(within(ask).getByRole('button', { name: /Agent and account: codex · / })).toBeInTheDocument();
  });

  /**
   * AUTONOMY1b3c (D184 point 3): a tool call the running session holds for the person's answer under asking stands in its head,
   * read off `PERMISSION_WAITS`, and *Allow once* sends `SESSION_PERMISSION_ANSWER` with the option the agent offered; a refusal of
   * the answer is said where it was pressed, by its code in the catalogue's words.
   */
  it('answers a tool call a running session holds for the person with the option they pressed', async () => {
    const held = {
      session: 's1a2b3c4', request: '900', call: 'tc-1', title: 'npm test', kind: 'execute', since: '2026-10-20T09:00:00Z',
      choices: [{ id: 'allow', name: 'Allow', kind: 'allow_once' }, { id: 'reject', name: 'Reject', kind: 'reject_once' }],
    };
    let answer: unknown = { answered: false, why: 'unheld', message: 'Session `s1a2b3c4` holds no permission request `900`.' };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'PERMISSION_WAITS') return { waits: [held] };
      if (type === 'SESSION_PERMISSION_ANSWER') return answer;
      return DRIVER_STATE;
    });
    show('s1a2b3c4');

    const section = await screen.findByRole('region', { name: 'Tool calls held for your answer' });
    expect(section).toHaveTextContent('npm test');
    const user = userEvent.setup();
    await user.click(within(section).getByRole('button', { name: 'Allow once' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_PERMISSION_ANSWER', { payload: { session: 's1a2b3c4', request: '900', option: 'allow' } });
    expect(await within(section).findByRole('alert')).toHaveTextContent('It no longer waits: it was answered or released first. Nothing was answered.');

    answer = { answered: true, why: null, message: 'Refused.' };
    await user.click(within(section).getByRole('button', { name: 'Refuse' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_PERMISSION_ANSWER', { payload: { session: 's1a2b3c4', request: '900', option: 'reject' } });
  });

  /**
   * MIXAGENT2b (D163 points 2 and 3; the mixed-agents design §3, §5): the session's asks of helpers stand in its head under
   * *Helpers*, an agent's that waits on the person with *Let it ask*, which sends `SESSION_HELPER_APPROVE`; *Ask another agent…*
   * folds into the head's ⋯ and asks under the head, its *Ask* sending `SESSION_HELPER_ASK` with the question, the agent left to
   * Daoris, and the ask closes once it is on file.
   */
  it('lists a session’s asks of helpers with the person’s yes, and asks another agent from the head', async () => {
    SESSIONS = [{ ...CHAT, state: 'completed' }];
    const helpers = [{
      id: 'a1', question: 'Is the cap right?', agent: null, askedBy: 'agent', at: '2026-10-16T09:00:00Z', state: 'waiting', code: null,
      helper: null, startedOn: null,
    }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return TWO_AGENTS;
      if (type === 'SESSION_HELPERS') return { helpers };
      if (type === 'SESSION_HELPER_APPROVE') return { approved: true, why: null, message: 'yes' };
      if (type === 'SESSION_HELPER_ASK') return { asked: 'a2', why: null, message: 'asked' };
      return DRIVER_STATE;
    });
    show('c0ffee11');

    const asks = await screen.findByRole('region', { name: 'Helpers' });
    expect(within(asks).getByText('The agent asked · waits for your yes')).toBeInTheDocument();
    const user = userEvent.setup();
    await user.click(within(asks).getByRole('button', { name: 'Let it ask' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HELPER_APPROVE', { payload: { session: 'c0ffee11', ask: 'a1' } });

    const acts = within(screen.getByRole('group', { name: 'Session actions' }));
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Ask another agent…' }));
    const ask = await screen.findByRole('group', { name: 'ask another agent' });
    await user.type(within(ask).getByRole('textbox', { name: 'Your question' }), 'Which test covers the loader?');
    await user.click(within(ask).getByRole('button', { name: 'Ask' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HELPER_ASK', { payload: { id: 'c0ffee11', question: 'Which test covers the loader?' } });
    await waitFor(() => expect(screen.queryByRole('group', { name: 'ask another agent' })).toBeNull());
  });

  /**
   * MIXAGENT1b-ii (D163 point 3): when the conversation's own account is near its limit by its scope's near, a line under the head
   * offers *Go on with codex (team)* and *Choose…*; the press asks `SESSION_HAND_OVER` with the reason `near` as Daoris's, *Choose…*
   * opens the ask with that choice made, and *Not now* puts it down for the run. Nothing is taken without the press.
   */
  const RUNNING_LOW = {
    ...ROSTER,
    harnesses: [
      { ...ROSTER.harnesses[0], profiles: [{ name: 'personal', home: 'x', login: 'in' }] },
      { harness: 'codex', present: true, version: 'codex 1.0.0', problem: null, machineDefault: null, profiles: [{ name: 'team', home: 'x', login: 'in' }] },
    ],
  };
  const scopeOf = (over: Record<string, unknown>) => ({
    workspace: null, default: null, list: [], begins: null, unknown: [], problem: null, near: [],
    use: { use: 'goal', keep: null, early: true, near: 90 }, ...over,
  });
  const LOW_ACCOUNTS = (cooling: unknown = null) => ({
    agents: [
      {
        agent: 'claude-code', speaks: true, own: {}, accounts: [{ name: 'personal', ...(cooling ? { cooling } : {}) }],
        scopes: [scopeOf({ near: [{ account: 'personal', window: 'session', by: 'number' }] })],
      },
      { agent: 'codex', speaks: true, own: {}, accounts: [{ name: 'team' }], scopes: [scopeOf({ next: { account: 'team', reason: 'onlyReady', others: [] } })] },
    ],
  });
  const LOW_CHAT = { ...CHAT, adapter: 'claude-code', profile: 'personal', state: 'completed' };
  const lowRespond = (handOver: unknown = { sessionId: 'n3wn3w00', kept: true, why: null, message: 'goes on.' }, accounts = LOW_ACCOUNTS()) =>
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return RUNNING_LOW;
      if (type === 'ACCOUNTS') return accounts;
      if (type === 'SESSION_HAND_OVER') return handOver;
      return DRIVER_STATE;
    });

  it('offers a conversation whose account is near its limit to go on with another agent, taken only by its press', async () => {
    SESSIONS = [LOW_CHAT];
    lowRespond();
    const { onSelect } = show('c0ffee11');

    const line = await screen.findByRole('group', { name: 'hand-over offer' });
    expect(line).toHaveTextContent('This conversation’s account is near its limit.');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', expect.anything());
    await userEvent.setup().click(within(line).getByRole('button', { name: 'Go on with codex (team)' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', {
      payload: { id: 'c0ffee11', agent: 'codex', account: 'team', why: 'near', askedBy: 'daoris' },
    });
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('n3wn3w00'));
  });

  it('says a cut account’s offer with when it is offered again, and sends the reason limit', async () => {
    SESSIONS = [LOW_CHAT];
    const until = new Date(Date.now() + 3 * 3600_000).toISOString();
    lowRespond(undefined, LOW_ACCOUNTS({ until, stated: true, window: 'session', seen: until, assumedZone: false, notBelieved: false }));
    show('c0ffee11');

    const line = await screen.findByRole('group', { name: 'hand-over offer' });
    expect(line).toHaveTextContent(/This conversation’s account is cut until /);
    await userEvent.setup().click(within(line).getByRole('button', { name: 'Go on with codex (team)' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', expect.objectContaining({
      payload: expect.objectContaining({ why: 'limit', askedBy: 'daoris' }),
    }));
  });

  it('opens the hand-over’s ask with that choice made from *Choose…*, and puts the offer down for the run with *Not now*', async () => {
    SESSIONS = [LOW_CHAT];
    lowRespond();
    show('c0ffee11');

    const user = userEvent.setup();
    const line = await screen.findByRole('group', { name: 'hand-over offer' });
    await user.click(within(line).getByRole('button', { name: 'Choose…' }));
    const ask = await screen.findByRole('group', { name: 'go on with another agent' });
    expect(within(ask).getByRole('button', { name: /Agent and account: codex · / })).toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'hand-over offer' })).toBeNull();
    await user.click(within(ask).getByRole('button', { name: 'Hand over' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', {
      payload: { id: 'c0ffee11', agent: 'codex', account: 'team', why: 'near', askedBy: 'daoris' },
    });
  });

  it('puts the offer down for the run with Not now, and offers nothing while the account says nothing is wrong', async () => {
    SESSIONS = [LOW_CHAT];
    lowRespond();
    const view = show('c0ffee11');

    const user = userEvent.setup();
    await user.click(within(await screen.findByRole('group', { name: 'hand-over offer' })).getByRole('button', { name: 'Not now' }));
    expect(screen.queryByRole('group', { name: 'hand-over offer' })).toBeNull();
    view.unmount();

    lowRespond(undefined, { agents: [{ ...LOW_ACCOUNTS().agents[0], scopes: [scopeOf({})] }, LOW_ACCOUNTS().agents[1]] });
    show('c0ffee11');
    await screen.findByRole('group', { name: 'Session actions' });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ACCOUNTS', expect.anything()));
    expect(screen.queryByRole('group', { name: 'hand-over offer' })).toBeNull();
  });

  /**
   * SESSUX1f (D126 §5.4): an ended conversation the reader says is deletable offers *Delete…* in its header's ⋯; it asks
   * once under the header, deletes nothing until its second press, and a driven session's ⋯ offers none.
   */
  it('deletes an ended conversation from its header’s ⋯, asking once, and offers a driven session none', async () => {
    const ended = { ...CHAT, state: 'completed' };
    SESSIONS = [ended, { ...DRIVEN, state: 'completed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [
          { session: 'c0ffee11', group: 'ended', shown: 'completed', archived: false, teammate: false, deletable: true },
          { session: 's1a2b3c4', group: 'ended', shown: 'completed', archived: false, teammate: false, deletable: false },
        ] };
      }
      if (type === 'SESSION_DELETE') return { deleted: 'c0ffee11', removed: ['record'] };
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('c0ffee11', notify);

    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GROUPS', {}));
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Delete…' }));

    const ask = screen.getByRole('group', { name: 'delete this session' });
    expect(ask).toHaveTextContent('Nothing brings it back.');
    // UXFIX2: opened from the ⋯, its sentence takes the focus, and *Never mind* gives it back to the ⋯.
    await waitFor(() => expect(within(ask).getByText(/^Deletes this conversation’s record/)).toHaveFocus());
    await user.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(screen.queryByRole('group', { name: 'delete this session' })).toBeNull();
    expect(acts.getByRole('button', { name: 'More actions' })).toHaveFocus();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DELETE', expect.anything());

    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Delete…' }));
    await user.click(screen.getByRole('button', { name: 'Delete session' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DELETE', { payload: { id: 'c0ffee11' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^Deleted c0ffee11/)));
  });

  /**
   * PAUSE1e (D132 §2.6, §7.1): a running driven session's ⋯ offers *Pause quest…*; it asks once under the header, saying
   * from the driver's plan what it stops, and pauses on its second press. Where a pause holds its quest, *Resume quest* is
   * the header's loud act, in *Try again*'s place.
   */
  it('pauses a running session’s quest from its header’s ⋯, asking once and saying what it stops', async () => {
    const plan = {
      scope: 'quest', id: 'abc123', pausable: true, paused: null, trees: [], landings: [],
      quests: [{ quest: 'abc123', title: 'Expose a streaming budget', to: 'engine', status: 'Open', joined: 'named', pause: 'paused', key: 'quest:abc123', abandon: 'decline', whileOpen: true }],
      sessions: [{ session: 's1a2b3c4', repository: 'engine', state: 'working', quest: 'abc123', pause: 'stopped', key: 'session:s1a2b3c4', abandon: 'stop', archive: true }],
      abandon: { abandonable: true, pieces: ['quest:abc123', 'session:s1a2b3c4'], closes: null, abandoned: null },
    };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'WORK_PLAN') return plan;
      if (type === 'WORK_PAUSE') return { scope: 'quest', id: 'abc123', did: 'paused', already: false, stopped: [{ session: 's1a2b3c4', quest: 'abc123' }], kept: [] };
      if (type === 'SESSION_GROUPS') return { sessions: [{ session: 's1a2b3c4', group: 'working', shown: 'working', archived: false, teammate: false }] };
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GROUPS', {}));
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Pause quest…' }));

    const ask = await screen.findByRole('group', { name: 'pause this work' });
    await waitFor(() => expect(ask).toHaveTextContent('Stops 1 running session now and starts nothing of quest #abc123 on this machine'));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_PAUSE', expect.anything());
    await user.click(within(ask).getByRole('button', { name: 'Pause quest' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_PAUSE', expect.objectContaining({ payload: { quest: 'abc123' } })));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^Paused quest #abc123 and stopped 1 session/), 'ok'));
    await waitFor(() => expect(screen.queryByRole('group', { name: 'pause this work' })).toBeNull());
  });

  it('leads a stopped session’s header with Resume where a pause holds its quest, and resumes over the driver', async () => {
    SESSIONS = [{ ...DRIVEN, state: 'stopped' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 's1a2b3c4', group: 'ended', shown: 'stopped', archived: false, teammate: false, pausedBy: { scope: 'quest', id: 'abc123' } }] };
      }
      if (type === 'WORK_RESUME') return { scope: 'quest', id: 'abc123', did: 'resumed', released: [{ quest: 'abc123', session: 's1a2b3c4' }], holds: [] };
      return DRIVER_STATE;
    });
    show('s1a2b3c4');

    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    const resume = await acts.findByRole('button', { name: 'Resume quest' });
    expect(resume.className).toContain('bg-accent');
    expect(acts.queryByRole('button', { name: 'Try again' })).toBeNull();
    await userEvent.click(resume);
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_RESUME', expect.objectContaining({ payload: { quest: 'abc123' } })));
  });

  it('offers a driven session that served a quest no Delete… in its header', async () => {
    SESSIONS = [{ ...DRIVEN, state: 'completed' }];
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_GROUPS'
      ? { sessions: [{ session: 's1a2b3c4', group: 'ended', shown: 'completed', archived: false, teammate: false, deletable: false }] }
      : DRIVER_STATE));
    show('s1a2b3c4');

    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GROUPS', {}));
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await screen.findByRole('menuitem', { name: 'Archive' });
    expect(screen.queryByRole('menuitem', { name: 'Delete…' })).toBeNull();
  });

  /**
   * UX7c (D152 §7, amending D126 §3.2): the header says its word, its title and its facts; its id is its ⋯'s and its
   * record's *Details*, and the record under it says neither its word nor its title again.
   */
  it('names the session in its header with its word and its facts, its id in Details alone', async () => {
    show('s1a2b3c4');

    const header = (await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' })).closest('header')!;
    expect(within(header).getByText('working')).toBeInTheDocument();
    expect(within(header).getByText(/^engine · /)).toBeInTheDocument();
    expect(within(header).queryByText('s1a2b3c4')).toBeNull();
    const head = screen.getByRole('button', { name: /^Details/ }).closest('header')!;
    expect(within(head).queryByText('working')).toBeNull();
    expect(within(head).queryByRole('heading', { level: 2 })).toBeNull();
    fireEvent.click(within(head).getByRole('button', { name: /^Details/ }));
    expect(within(head).getByText('s1a2b3c4')).toBeInTheDocument();
  });

  /**
   * PROCESS1e (D164's amendment point 3): the attended session's *Details* says the process its own tree runs under, as the driver
   * answers it for that session, with where each rule was set.
   */
  it('says in Details how the session commits and lands, as the driver answers for it', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_PROCESS'
      ? {
          session: 's1a2b3c4',
          process: {
            commits: { repository: 'engine', workspace: 'default', rule: { mode: 'at-end' }, source: 'repository' },
            landing: { repository: 'engine', workspace: 'default', source: 'default', form: 'merge' },
          },
        }
      : DRIVER_STATE));
    show('s1a2b3c4');

    const head = (await screen.findByRole('button', { name: /^Details/ })).closest('header')!;
    fireEvent.click(within(head).getByRole('button', { name: /^Details/ }));
    expect(await within(head).findByText('Commits')).toBeInTheDocument();
    expect(within(head).getByText('Commits').nextElementSibling)
      .toHaveTextContent('One commit is made when the work is done. Set for this repository.');
    expect(within(head).getByText('Landing').nextElementSibling).toHaveTextContent('Merged into its line — the default.');
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_PROCESS', expect.objectContaining({ payload: { id: 's1a2b3c4' } }));
  });

  /**
   * FABRIC-MILESTONE1b (the fabric events design §D0): the attended session's foot says where its work stands, as the driver's
   * `WORK_STATE` answers for that session: the end, the result, the clean-up and the recovery.
   */
  it('says at the foot where the work stands: the end, the result, the clean-up and the recovery, as the driver answers', async () => {
    const entry = { unit: 'u1', kind: 'tree', resource: 's1a2b3c4', role: 'temporary', disposition: 'blocked', detail: 'a session still holds this tree' };
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'WORK_STATE'
      ? {
          units: [{
            unit: 'u1', repository: 'engine', branch: 'daoris/s1a2b3c4', session: 's1a2b3c4',
            end: { state: 'ended', by: 'merged', on: 'main', revision: 'a1b2c3d4e5f6', at: '2026-10-17T09:00:00Z', said: 'every commit of it is on `main`' },
            result: [{ ...entry, kind: 'line', resource: 'main', role: 'result', disposition: 'kept', detail: 'the line holds it' }],
            cleanup: { state: 'pending', waits: [entry], gone: [] },
            recovery: [{ ...entry, kind: 'moved-aside', resource: 's1a2b3c4-20261017', role: 'recovery', disposition: 'retained', until: '2026-10-24T09:00:00Z' }],
            entries: [],
          }],
          unread: [],
        }
      : DRIVER_STATE));
    show('s1a2b3c4');

    const foot = await screen.findByRole('region', { name: 'Where this work stands' });
    expect(foot).toHaveTextContent('Reached: every commit of it is on main');
    expect(foot).toHaveTextContent('Kept: main');
    expect(foot).toHaveTextContent('Blocked: tree s1a2b3c4, a session still holds this tree');
    expect(foot).toHaveTextContent('folder set aside s1a2b3c4-20261017, kept until');
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_STATE', expect.objectContaining({ payload: { unit: 's1a2b3c4' } }));
  });

  it('says nothing at the foot where the driver names no unit for the session', async () => {
    show('s1a2b3c4');

    await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' });
    expect(screen.queryByRole('region', { name: 'Where this work stands' })).toBeNull();
  });

  /**
   * MIXAGENT3a-i (the mixed-agents design §4): a session that continues another says which agent's in its facts, by the roster's
   * name for it, and why in the hand-over's words; its *Details* names that session and attends it on a press.
   */
  it('says whom a session continues in its header, and attends that session from Details', async () => {
    const earlier = { ...CHAT, id: 'e4r1i3r0', adapter: 'codex', state: 'completed' };
    SESSIONS = [earlier, { ...CHAT, from: 'e4r1i3r0', fromWhy: 'near', fromBy: 'daoris' }];
    // The roster's codex, named as its product.
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: ROSTER.harnesses.map((row) => (row.harness === 'codex' ? { ...row, product: 'Codex' } : row)) }
      : DRIVER_STATE));
    const { onSelect } = show('c0ffee11');

    const header = (await screen.findByRole('heading', { level: 1, name: 'Chat' })).closest('header')!;
    expect(await within(header).findByText(/^engine · continues from Codex · its account was near its limit · /)).toBeInTheDocument();
    const head = screen.getByRole('button', { name: /^Details/ }).closest('header')!;
    fireEvent.click(within(head).getByRole('button', { name: /^Details/ }));
    fireEvent.click(within(within(head).getByText('Continues').nextElementSibling as HTMLElement).getByRole('button', { name: 'Open session' }));
    expect(onSelect).toHaveBeenCalledWith('e4r1i3r0');
  });

  /** And, as a reviewer's, a helper's record takes one turn and no words (D163's MIXAGENT2a-i note): no box is offered. */
  it('titles a helper as one, says whom it helps by that session’s title, and offers it no box', async () => {
    SESSIONS = [DRIVEN, { ...CHAT, id: 'h3lp3r00', serves: 's1a2b3c4' }];
    show('h3lp3r00');

    const header = (await screen.findByRole('heading', { level: 1, name: 'Helper' })).closest('header')!;
    expect(await within(header).findByText(/^engine · helping Expose a streaming budget · /)).toBeInTheDocument();
    expect(screen.queryByLabelText('Message')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
  });

  /** M2: a quest parked on its failed sessions is tried again where its session is, the loud act in its header. */
  it('tries a parked quest again from its session’s page header', async () => {
    SESSIONS = [{ ...DRIVEN, id: 'f41led00', state: 'failed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 'f41led00', group: 'you', shown: 'parked', archived: false, teammate: false, strikes: 3 }] };
      }
      if (type === 'RETRY_QUEST') return { ...DRIVER_STATE, strikes: 3, retried: { quest: 'abc123', did: 'marked', session: null } };
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('f41led00', notify);

    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    const retry = await acts.findByRole('button', { name: 'Try again' });
    expect(acts.queryByRole('button', { name: 'Stop…' })).toBeNull();
    await userEvent.click(retry);

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RETRY_QUEST', { payload: { quest: 'abc123' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^#abc123 will be tried again/)));
  });

  /** A row's *Stop…* attends its session, then opens the same ask under its header (§3.1). */
  it('asks to stop a session from its row: attended, the ask under its header', async () => {
    SESSIONS = [DRIVEN, { ...CHAT }];
    attending('c0ffee11');
    await screen.findByLabelText('Message');

    const user = userEvent.setup();
    const list = await screen.findByRole('complementary', { name: 'Sessions' });
    within(list).getByRole('button', { name: 'more for Expose a streaming budget' }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Stop…' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'stop this session' })).toBeInTheDocument();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', expect.anything());
  });

  /** A row's *Answer…* attends a session waiting on you and puts the focus in the box at its foot (§3.1). */
  it('answers a session from its row: attended, the focus in the box at its foot', async () => {
    SESSIONS = [{ ...CHAT }, PARKED];
    attending('c0ffee11');
    await screen.findByLabelText('Message');

    const user = userEvent.setup();
    const list = await screen.findByRole('complementary', { name: 'Sessions' });
    const waiting = within(list).getByRole('heading', { level: 3, name: 'Waiting on you (1)' }).closest('section')!;
    within(waiting).getByRole('button', { name: /^more for / }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Answer…' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Carry on with this answer' })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveFocus());
  });

  /** *Open a terminal here* opens the panel's terminal in the session's folder, the panel shown (§3.5). */
  it('opens a terminal in the session’s folder from its header, and shows it', async () => {
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module === 'DAORIS.TERMINAL' && type === 'OPEN') return { id: 't1', shell: 'pwsh', cwd: 'C:/somewhere/engine' };
      if (module === 'DAORIS.TERMINAL' && type === 'SHELLS') return { shells: [{ shell: 'pwsh' }], default: 'pwsh' };
      return DRIVER_STATE;
    });
    // A viewer who hid the panel: the press shows it again, on the terminal.
    window.localStorage.setItem('daoris.panelClosed', '1');
    attending('s1a2b3c4', true);

    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Open a terminal here' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.TERMINAL', 'OPEN', { payload: { cwd: 'C:/somewhere/engine' } }));
    expect(await screen.findByRole('tab', { name: 'Terminal', selected: true })).toBeInTheDocument();
    window.localStorage.removeItem('daoris.panelClosed');
  });

  /** *Open folder* names the session alone: the module names its folder (§3.5). */
  it('opens the session’s folder from its header by its id', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_OPEN_FOLDER' ? { opened: true } : DRIVER_STATE));
    show('s1a2b3c4');

    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Open folder' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_OPEN_FOLDER', { payload: { id: 's1a2b3c4' } }));
  });
});

/**
 * Review (SURF6, design §5): what the session actually did. The dock's second occupant — which is
 * why the dock exists at all now, and why the timeline moved into it.
 */
describe('reviewing what a session landed', () => {
  const DIFF = {
    session: 's1a2b3c4',
    base: 'abc1234567890',
    truncated: null as string | null,
    files: [
      {
        path: 'src/chunk.ts', status: 'modified', added: 4, removed: 1,
        patch: '@@ -1 +1,2 @@\n-old\n+new',
      },
      { path: 'assets/logo.png', status: 'added', added: null, removed: null, patch: null },
    ],
  };

  beforeEach(() => {
    SESSIONS = [DRIVEN];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? DIFF : DRIVER_STATE));
    // A viewer who has opened the dock, where review lives: it opens on demand (UX5 U7).
    window.localStorage.setItem('daoris.dockClosed', '0');
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    // A draft outlives its test the way it outlives a reload (CONV4b).
    window.localStorage.removeItem('daoris.drafts');
    window.localStorage.removeItem('daoris.dockClosed');
  });

  /** The dock opens on the timeline: a running session is watched far more often than reviewed. */
  it('docks the timeline beside the session, and asks for no diff until someone looks', async () => {
    show('s1a2b3c4');
    await screen.findByRole('tab', { name: 'Review' });

    expect(screen.getByRole('tab', { name: 'Timeline' }).getAttribute('aria-selected')).toBe('true');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DIFF', expect.anything());
  });

  /**
   * REVIEW2: the changes laid out in one column or the old side beside the new — one choice for the
   * whole review, remembered per viewer like the frame's widths.
   */
  it('lays the changes out unified or side by side, and remembers which', async () => {
    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    await userEvent.click(await screen.findByText('src/chunk.ts'));

    const layout = screen.getByRole('radiogroup', { name: 'Layout' });
    expect(within(layout).getByRole('radio', { name: 'Unified' })).toHaveAttribute('aria-checked', 'true');
    expect(within(screen.getAllByRole('row')[0]!).getAllByRole('cell').map((cell) => cell.textContent))
      .toEqual(['1', '', '−', 'old']);

    await userEvent.click(within(layout).getByRole('radio', { name: 'Side by side' }));
    expect(within(screen.getAllByRole('row')[0]!).getAllByRole('cell').map((cell) => cell.textContent))
      .toEqual(['1', 'old', '1', 'new']);
    expect(window.localStorage.getItem('daoris.reviewLayout')).toBe('split');
    window.localStorage.removeItem('daoris.reviewLayout');
  });

  it('reads the landed work off the checkout when the review tab is opened', async () => {
    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText('src/chunk.ts')).toBeTruthy();
    // Within the review's own bound (REVIEW4): the bridge's 30 seconds gave up on a diff git took most of a minute over.
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DIFF', {
      payload: { id: 's1a2b3c4' }, timeoutMs: 180_000,
    });
    // The range it is measured from, stated — a review that does not say so is an opinion.
    expect(screen.getByText(/abc12345/)).toBeTruthy();
    // And a binary file is listed as uncounted rather than as an empty change.
    expect(screen.getByText('binary')).toBeTruthy();
  });

  /**
   * REVIEW4: while git reads, the review's frame is the attended record's — its own branch, the commit it began at, the
   * commits it reported — above the skeleton and the words saying what is read. The installed window sat blank here.
   */
  it('stands the review’s frame from the attended record at once while git reads', async () => {
    SESSIONS = [{
      ...DRIVEN, state: 'completed', tree: 'C:/somewhere/.daoris/trees/default/engine/s-2394e5d9',
      baseCommit: 'abc1234567890', evidence: 'commits landed:\nabc1234 Cap the streaming budget',
    }];
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? new Promise(() => {}) : DRIVER_STATE));

    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText('Reading the changes in engine…')).toBeTruthy();
    expect(screen.getByText('daoris/s-2394e5d9')).toBeTruthy();
    expect(screen.getByText('since abc12345')).toBeTruthy();
    expect(screen.getByText('1 commit')).toBeTruthy();
  });

  /**
   * The bound is the host's sentence and reaches the person word for word — the console's rule,
   * applied to a surface that has no upper size either.
   */
  it('shows the bound verbatim when the host truncated the patches', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_DIFF'
      ? { ...DIFF, truncated: '9 more files changed; their patches are not shown here. `git diff` in the tree has all of it.' }
      : DRIVER_STATE));

    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText(/9 more files changed/)).toBeTruthy();
  });

  /**
   * Unreviewable is INFORMATION, not a fault (D48 §6's class): a record that travelled from another
   * machine names no tree here and never will. The host's sentence says which of the three it is.
   */
  it('renders the host’s own sentence when there is nothing here to diff', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type !== 'SESSION_DIFF') return DRIVER_STATE;
      throw Object.assign(new Error('fallback'), {
        code: 'SESSION_NOT_REVIEWABLE', parameters: { session: 's1a2b3c4' },
      });
    });

    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText(/work is not on this machine/)).toBeTruthy();
  });

  /** A session that committed nothing changed nothing — an answer, not an empty list. */
  it('says a session landed nothing rather than showing an empty diff', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? { ...DIFF, files: [] } : DRIVER_STATE));

    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText('Nothing landed')).toBeTruthy();
  });

  /** REVIEWDIRTY1-b: a tree holding only uncommitted work shows it, and does not say nothing landed. */
  it('shows a session\'s uncommitted work where it committed nothing', async () => {
    const uncommitted = { files: [{ path: 'src/draft.ts', status: 'modified', added: 1, removed: 0, patch: '@@ -1 +1 @@\n-a\n+b' }], truncated: false };
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? { ...DIFF, files: [], uncommitted } : DRIVER_STATE));

    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText('src/draft.ts')).toBeTruthy();
    expect(screen.queryByText('Nothing landed')).toBeNull();
  });
});

/**
 * The two acts on a reviewed session (SURF6b, D51 rules 6–7). The guards themselves live in the tree
 * layer and are tested against real git; what is asserted here is that the surface carries the
 * person's intent faithfully — and, above all, that it never sends `force` on a first press.
 */
describe('acting on what a session landed', () => {
  const DIFF = {
    session: 's1a2b3c4',
    base: 'abc1234567890',
    truncated: null as string | null,
    files: [{ path: 'src/chunk.ts', status: 'modified', added: 4, removed: 1, patch: '@@ -1 +1 @@\n-a\n+b' }],
  };

  // The acts act on a session TREE (D51), so the session under test holds one. A record that names
  // no tree here is the other case, asserted at the end.
  const IN_A_TREE = { ...DRIVEN, tree: 'C:/somewhere/.daoris/trees/default/engine-abc' };

  beforeEach(() => {
    SESSIONS = [IN_A_TREE];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    // A viewer who has opened the dock, where review lives: it opens on demand (UX5 U7).
    window.localStorage.setItem('daoris.dockClosed', '0');
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    // A draft outlives its test the way it outlives a reload (CONV4b).
    window.localStorage.removeItem('daoris.drafts');
    window.localStorage.removeItem('daoris.dockClosed');
  });

  const review = async () => {
    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    await screen.findByText('src/chunk.ts');
  };

  /** WSR1 (D87): the pane says where a press sends the work before it is pressed, in the driver's plan. */
  it('says before the press that accepting puts the work on the branch the rule names', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'LANDING') return { session: 's1a2b3c4', form: 'branch', target: 'feature/0fda18-fix', source: 'workspace' };
      return DRIVER_STATE;
    });

    await review();

    expect(await screen.findByText(/for you to push and open a pull request from/)).toBeTruthy();
    expect(screen.getByText('feature/0fda18-fix', { selector: 'code' })).toBeTruthy();
  });

  /** WSR4 (D100): who pushes it is said before the press — and what would refuse the press, where something would. */
  it('says before the press which plugin pushes the branch, and what stands in its way', async () => {
    let plan: Record<string, unknown> = {
      session: 's1a2b3c4', form: 'branch', target: 'feature/0fda18-fix', source: 'workspace', plugin: 'github-pull-request',
    };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'LANDING') return plan;
      return DRIVER_STATE;
    });

    await review();

    expect(await screen.findByText(/pushes it and opens the pull request\. Nothing is merged/)).toBeTruthy();
    expect(screen.getByText('github-pull-request', { selector: 'code' })).toBeTruthy();
    cleanup();

    plan = { ...plan, problem: 'plugin `github-pull-request` is switched off on this machine — `daoris plugin enable github-pull-request` switches it on.' };
    await review();
    expect(await screen.findByText(/switched off on this machine/)).toBeTruthy();
  });

  /** WSR4 (D100): the plugin's pull request, once opened, is one press away — a link that opens as the person's links do. */
  it('offers the pull request a plugin opened as a link beside the landing sentence', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'LAND_SESSION_TREE') {
        return {
          session: 's1a2b3c4', done: true, branch: 'feature/0fda18-fix',
          message: 'put the work on `feature/0fda18-fix` — 1 commit(s) from `main`. Plugin `github-pull-request`: pushed it.',
          plugin: { id: 'github-pull-request', pushed: true, pullRequest: 'https://example.test/example-org/engine/pull/7', message: 'pushed it.', failed: false },
        };
      }
      return DRIVER_STATE;
    });

    await review();
    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));

    expect(await screen.findByText(/Plugin `github-pull-request`: pushed it/)).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Open the pull request' })).toHaveAttribute('href', 'https://example.test/example-org/engine/pull/7');
  });

  /**
   * WSR5b: a session whose work already landed on a branch can hand that branch to a landing plugin
   * afterwards — the rule's, named before the press — and the pane renders what the driver says back.
   */
  it('hands the branch its landing made to the plugin, and renders what it says back', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'HANDOFF_PLAN') {
        return { session: 's1a2b3c4', branch: 'feature/0fda18-fix', repository: 'engine', plugin: 'github-pull-request', problem: null, commits: 1 };
      }
      if (type === 'HANDOFF') {
        return {
          session: 's1a2b3c4', done: true, branch: 'feature/0fda18-fix',
          message: 'handed `feature/0fda18-fix` to plugin `github-pull-request`. Plugin `github-pull-request`: pushed it.',
          plugin: { id: 'github-pull-request', pushed: true, pullRequest: 'https://example.test/example-org/engine/pull/8', message: 'pushed it.', failed: false },
        };
      }
      return DRIVER_STATE;
    });

    await review();

    expect(await screen.findByText(/can push it and open the pull request/)).toBeTruthy();
    expect(screen.getByText('feature/0fda18-fix', { selector: 'code' })).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Hand to github-pull-request' }));

    // LEFT3 a: a hand-off waits as long as its plugin may (`pluginBound`, six minutes), not the bridge's 30 seconds.
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HANDOFF', { payload: { id: 's1a2b3c4' }, timeoutMs: 6 * 60_000 });
    expect(await screen.findByText(/Plugin `github-pull-request`: pushed it/)).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Open the pull request' })).toHaveAttribute('href', 'https://example.test/example-org/engine/pull/8');
  });

  /** WSR5b: what stands in the way is said, and the press that could only be refused is not offered (UX5 U66). */
  it('says what stands in the way of a hand-off, and offers no press that could only be refused', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'HANDOFF_PLAN') {
        return {
          session: 's1a2b3c4', branch: 'feature/0fda18-fix', repository: 'engine', plugin: 'github-pull-request', commits: 1,
          problem: '`feature/0fda18-fix` is already on its remote at this commit, and its pull request was answered: https://example.test/pr/8 Nothing to hand on.',
        };
      }
      return DRIVER_STATE;
    });

    await review();

    expect(await screen.findByText(/already on its remote at this commit/)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Hand to github-pull-request' })).toBeDisabled();
  });

  /** WSR5b: a session whose landing made no branch, or whose rule names no plugin, has nothing to hand on. */
  it('offers no hand-off where the landing made no branch or no plugin is named', async () => {
    let plan: Record<string, unknown> = { session: 's1a2b3c4', branch: null };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'HANDOFF_PLAN') return plan;
      return DRIVER_STATE;
    });

    await review();
    await screen.findByRole('button', { name: 'Accept' });
    expect(screen.queryByRole('button', { name: /Hand to/ })).toBeNull();
    cleanup();

    plan = { session: 's1a2b3c4', branch: 'feature/0fda18-fix', repository: 'engine', plugin: null, problem: 'no plugin is named', commits: 1 };
    await review();
    await screen.findByRole('button', { name: 'Accept' });
    expect(screen.queryByRole('button', { name: /Hand to/ })).toBeNull();
  });

  /** WSR5b: a tidied landing leaves no tree, and its branch can still be handed on. */
  it('offers the hand-off for a session whose tree the landing tidied away', async () => {
    SESSIONS = [DRIVEN];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'HANDOFF_PLAN') {
        return { session: 's1a2b3c4', branch: 'feature/0fda18-fix', repository: 'engine', plugin: 'github-pull-request', problem: null, commits: 1 };
      }
      return DRIVER_STATE;
    });

    await review();

    expect(await screen.findByRole('button', { name: 'Hand to github-pull-request' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
  });

  /** SESS1 S10: the head finds its branch in the clean-up's list by the tree's folder, on a Windows path too. */
  it('names the branch its tree left and whether its work landed, in the head', async () => {
    SESSIONS = [{ ...IN_A_TREE, tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-abc12345' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'SWEEP_PLAN') {
        return { branches: [{ repository: 'engine', workspace: 'default', branch: 'daoris/s-abc12345', hasTree: true,
          kind: 'landed', commits: 2, where: 'feature/x', removable: true }] };
      }
      return DRIVER_STATE;
    });

    show('s1a2b3c4');

    expect(await screen.findByText('landed on feature/x')).toBeTruthy();
    expect(screen.getByText('daoris/s-abc12345')).toBeTruthy();
  });

  /**
   * LAND3b (D102's LAND3 note): a failed session whose tree is gone left a branch only the terminal's
   * `trees remove … --force` reached. Its head offers the discard, asks once, and presses the driver's forced removal of
   * that branch by its name and repository; the driver's sentence for a branch it kept is said whole, as a refusal is.
   */
  it("discards the branch a failed session left once its tree is gone, after asking once", async () => {
    SESSIONS = [{ ...IN_A_TREE, state: 'failed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-abc12345' }];
    let answer = { repository: 'engine', branch: 'daoris/s-abc12345', done: true, message: 'removed the session branch `daoris/s-abc12345` from `engine`.' };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'SWEEP_PLAN') {
        return { branches: [{ repository: 'engine', workspace: 'default', branch: 'daoris/s-abc12345', hasTree: false,
          kind: 'unlanded', commits: 2, removable: false, discardable: true }] };
      }
      if (type === 'DISCARD_SESSION_BRANCH') return answer;
      return DRIVER_STATE;
    });
    const notify = vi.fn();

    show('s1a2b3c4', notify);
    await userEvent.click(await screen.findByRole('button', { name: 'Discard branch…' }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'DISCARD_SESSION_BRANCH', expect.anything());
    const ask = screen.getByRole('group', { name: 'discard daoris/s-abc12345' });
    expect(ask).toHaveTextContent('2 commits no branch of yours holds. Nothing brings them back.');
    const asked = invoke.mock.calls.filter(([, type]) => type === 'SWEEP_PLAN').length;

    await userEvent.click(within(ask).getByRole('button', { name: 'Discard branch' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'DISCARD_SESSION_BRANCH', {
      payload: { repository: 'engine', branch: 'daoris/s-abc12345', force: true },
    });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('Discarded `daoris/s-abc12345` in engine.'));
    // The clean-up's list is read again, so the head says what is left.
    await vi.waitFor(() => expect(invoke.mock.calls.filter(([, type]) => type === 'SWEEP_PLAN').length).toBeGreaterThan(asked));

    // UXFIX2: it closed once the discard landed; a branch the driver kept is said inside the ask, whole, which stays open.
    await vi.waitFor(() => expect(screen.queryByRole('group', { name: 'discard daoris/s-abc12345' })).toBeNull());
    answer = { repository: 'engine', branch: 'daoris/s-abc12345', done: false, message: 'there is no branch `daoris/s-abc12345` in `engine` — it is gone already.' };
    await userEvent.click(await screen.findByRole('button', { name: 'Discard branch…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Discard branch' }));
    const kept = await within(screen.getByRole('group', { name: 'discard daoris/s-abc12345' })).findByRole('alert');
    expect(kept).toHaveTextContent('there is no branch daoris/s-abc12345 in engine — it is gone already.');
    expect(notify).not.toHaveBeenCalledWith(expect.stringContaining('it is gone already'), 'error');
  });

  /**
   * LAND4 (D102's LAND4 note, the owner's case): a failed session's commits stayed in its tree and nothing offered to land
   * them. Where the driver's reader says what its tree offers to land, its page says the commits, the branch and the tree
   * beside what it left, and *Accept…* asks once, saying where the rule puts it, then presses the review's own landing; the
   * driver's sentence is said once it landed, and a refusal inside the ask.
   */
  it('offers a failed session’s commits to land on its page, and lands them through the review’s own door', async () => {
    SESSIONS = [{ ...IN_A_TREE, state: 'failed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
    let landed = { session: 's1a2b3c4', done: true, message: 'put its work on `feature/delta` — 1 commit(s).' };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 's1a2b3c4', group: 'ended', shown: 'failed', archived: false, teammate: false,
          lands: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', commits: 1, uncommitted: 0 } }] };
      }
      if (type === 'LANDING') return { session: 's1a2b3c4', form: 'branch', target: 'feature/delta', source: 'workspace' };
      if (type === 'LAND_SESSION_TREE') return landed;
      if (type === 'SESSION_DIFF') return DIFF;
      return DRIVER_STATE;
    });
    const notify = vi.fn();

    show('s1a2b3c4', notify);
    // The head's, in the main area: the strip at the foot draws the same line (WAITFOOT1b, D168).
    const head = within(screen.getByRole('main'));
    await userEvent.click(await head.findByRole('button', { name: 'Accept…' }));
    expect(head.getByText('in its tree s-4e6837ed')).toBeTruthy();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_SESSION_TREE', expect.anything());
    const ask = screen.getByRole('group', { name: 'accept daoris/s-4e6837ed' });
    await within(ask).findByText(/on a new branch/);
    expect(ask).toHaveTextContent('Accepting puts this work on a new branch, feature/delta, for you to push and open a pull request from.');

    await userEvent.click(within(ask).getByRole('button', { name: 'Accept' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_SESSION_TREE', { payload: { id: 's1a2b3c4' }, timeoutMs: 6 * 60_000 });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('put its work on `feature/delta` — 1 commit(s).'));
    await vi.waitFor(() => expect(screen.queryByRole('group', { name: 'accept daoris/s-4e6837ed' })).toBeNull());

    // A refusal is the driver's sentence, said inside the ask, which stays open.
    landed = { session: 's1a2b3c4', done: false, message: 'the session\'s tree has uncommitted work — 2 path(s) — which a branch would leave behind.' };
    await userEvent.click(await head.findByRole('button', { name: 'Accept…' }));
    await userEvent.click(within(screen.getByRole('group', { name: 'accept daoris/s-4e6837ed' })).getByRole('button', { name: 'Accept' }));
    expect(await within(screen.getByRole('group', { name: 'accept daoris/s-4e6837ed' })).findByRole('alert'))
      .toHaveTextContent('which a branch would leave behind.');
  });

  /**
   * ACCEPTALL1 (D167's 2026-10-15 amendment, the owner's *"I cannot accept for 2 sessions/quests at the same time"*): where more
   * than one part of the attended line of work offers work to land, its page offers one Accept under the Parts strip; the plan is
   * read only once its ask opens, and the press lands the entry.
   */
  it('offers one Accept for every part of a line of work that has work to land', async () => {
    const tree = (name: string) => `C:\\somewhere\\.daoris\\trees\\default\\engine\\${name}`;
    SESSIONS = [
      { ...IN_A_TREE, state: 'completed', tree: tree('s-1a2b3c4') },
      { ...IN_A_TREE, id: 's5e6f7a8', quest: 'abc124', state: 'completed', tree: tree('s-5e6f7a8'), created: '2026-09-02T02:00:00Z', updated: '2026-09-02T03:00:00Z' },
    ];
    const lands = (name: string) => ({ branch: `daoris/${name}`, tree: name, commits: 1, uncommitted: 0 });
    const part = (session: string, quest: string) => ({ session, quest, shown: 'completed', group: 'review', began: 'opened', after: null, askedBy: null });
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return {
          sessions: [
            { session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false, lands: lands('s-1a2b3c4') },
            { session: 's5e6f7a8', group: 'review', shown: 'completed', archived: false, teammate: false, lands: lands('s-5e6f7a8') },
          ],
          entries: [{ entry: 'ask:a3/engine', origin: 'ask:a3', repository: 'engine', group: 'review', leading: 's5e6f7a8',
            parts: [part('s1a2b3c4', 'abc123'), part('s5e6f7a8', 'abc124')] }],
        };
      }
      if (type === 'LANDING') return { session: 's1a2b3c4', form: 'branch', target: 'feature/a3', source: 'workspace' };
      if (type === 'ENTRY_LANDING') {
        return { entry: 'ask:a3/engine', repository: 'engine', form: 'branch', target: 'feature/a3',
          parts: [{ session: 's1a2b3c4', target: 'feature/a3' }, { session: 's5e6f7a8', target: 'feature/a3' }] };
      }
      if (type === 'LAND_ENTRY') return { entry: 'ask:a3/engine', repository: 'engine', done: true, message: 'Landed 2 part(s) on `feature/a3`.', parts: [] };
      if (type === 'SESSION_DIFF') return DIFF;
      return DRIVER_STATE;
    });
    const notify = vi.fn();

    show('s1a2b3c4', notify);
    const main = within(screen.getByRole('main'));
    const press = await main.findByRole('button', { name: 'Accept all 2 parts…' });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'ENTRY_LANDING', expect.anything());

    await userEvent.click(press);
    const ask = screen.getByRole('group', { name: 'accept every part of this line of work' });
    expect(await within(ask).findByText('Lands 2 parts on feature/a3, one after another, in the order they ran.')).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ENTRY_LANDING', { payload: { entry: 'ask:a3/engine' } });

    await userEvent.click(within(ask).getByRole('button', { name: 'Accept all' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_ENTRY', { payload: { entry: 'ask:a3/engine' }, timeoutMs: expect.any(Number) });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('Landed 2 part(s) on `feature/a3`.'));
  });

  /**
   * ACCEPTALL1h (D169's ACCEPTALL1e note): while a line of work's one Accept lands it, its part's own *Accept…* in the head and its
   * review's *Accept* wait, read by the entry the press names, and come back once it answered.
   */
  it('holds a part’s own Accept in the head and in its review while its line of work lands', async () => {
    const tree = (name: string) => `C:\\somewhere\\.daoris\\trees\\default\\engine\\${name}`;
    SESSIONS = [
      { ...IN_A_TREE, state: 'completed', tree: tree('s-1a2b3c4') },
      { ...IN_A_TREE, id: 's5e6f7a8', quest: 'abc124', state: 'completed', tree: tree('s-5e6f7a8'), created: '2026-09-02T02:00:00Z', updated: '2026-09-02T03:00:00Z' },
    ];
    const lands = (name: string) => ({ branch: `daoris/${name}`, tree: name, commits: 1, uncommitted: 0 });
    const part = (session: string, quest: string) => ({ session, quest, shown: 'completed', group: 'review', began: 'opened', after: null, askedBy: null });
    let landed: (answer: unknown) => void = () => {};
    invoke.mockImplementation((_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return Promise.resolve({
          sessions: [
            { session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false, lands: lands('s-1a2b3c4') },
            { session: 's5e6f7a8', group: 'review', shown: 'completed', archived: false, teammate: false, lands: lands('s-5e6f7a8') },
          ],
          entries: [{ entry: 'ask:a3/engine', origin: 'ask:a3', repository: 'engine', group: 'review', leading: 's5e6f7a8',
            parts: [part('s1a2b3c4', 'abc123'), part('s5e6f7a8', 'abc124')] }],
        });
      }
      if (type === 'LANDING') return Promise.resolve({ session: 's1a2b3c4', form: 'branch', target: 'feature/a3', source: 'workspace' });
      if (type === 'ENTRY_LANDING') {
        return Promise.resolve({ entry: 'ask:a3/engine', repository: 'engine', form: 'branch', target: 'feature/a3',
          parts: [{ session: 's1a2b3c4', target: 'feature/a3' }, { session: 's5e6f7a8', target: 'feature/a3' }] });
      }
      if (type === 'LAND_ENTRY') return new Promise((resolve) => { landed = resolve; });
      if (type === 'SESSION_DIFF') return Promise.resolve(DIFF);
      return Promise.resolve(DRIVER_STATE);
    });

    show('s1a2b3c4', vi.fn());
    const main = within(screen.getByRole('main'));
    expect(await main.findByRole('button', { name: 'Accept…' })).toBeEnabled();
    await userEvent.click(await main.findByRole('button', { name: 'Accept all 2 parts…' }));
    const ask = screen.getByRole('group', { name: 'accept every part of this line of work' });
    await within(ask).findByText('Lands 2 parts on feature/a3, one after another, in the order they ran.');
    await userEvent.click(within(ask).getByRole('button', { name: 'Accept all' }));

    await vi.waitFor(() => expect(main.getByRole('button', { name: 'Accept…' })).toBeDisabled());
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    const review = within(await screen.findByRole('tabpanel'));
    await review.findByText('src/chunk.ts');
    expect(await review.findByRole('button', { name: 'Landing…' })).toBeDisabled();

    await act(async () => landed({ entry: 'ask:a3/engine', repository: 'engine', done: true, message: 'Landed 2 part(s) on `feature/a3`.', parts: [] }));
    await vi.waitFor(() => expect(review.queryByRole('button', { name: 'Landing…' })).toBeNull());
  });

  /**
   * STATEHUB1c (D169 point 7, the owner's 2026-10-15 采纳): a landing pressed in the head was the head's alone, so the review
   * offered *Accept* while it ran. Under way, every view of the session says so: the review's *Accept* reads *Landing…* and
   * waits, as it would for its own press, until the landing answers.
   */
  it('holds the review’s Accept as Landing… while the head’s landing is under way', async () => {
    SESSIONS = [{ ...IN_A_TREE, state: 'completed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
    let landed: (answer: unknown) => void = () => {};
    invoke.mockImplementation((_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return Promise.resolve({ sessions: [{ session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false,
          lands: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', commits: 1, uncommitted: 0 } }] });
      }
      if (type === 'LANDING') return Promise.resolve({ session: 's1a2b3c4', form: 'branch', target: 'feature/delta', source: 'workspace' });
      if (type === 'LAND_SESSION_TREE') return new Promise((resolve) => { landed = resolve; });
      if (type === 'SESSION_DIFF') return Promise.resolve(DIFF);
      return Promise.resolve(DRIVER_STATE);
    });

    show('s1a2b3c4', vi.fn());
    await userEvent.click(await within(screen.getByRole('main')).findByRole('button', { name: 'Accept…' }));
    const ask = screen.getByRole('group', { name: 'accept daoris/s-4e6837ed' });
    await vi.waitFor(() => expect(within(ask).getByRole('button', { name: 'Accept' })).toBeEnabled());
    await userEvent.click(within(ask).getByRole('button', { name: 'Accept' }));

    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    const review = within(await screen.findByRole('tabpanel'));
    await review.findByText('src/chunk.ts');
    expect(await review.findByRole('button', { name: 'Landing…' })).toBeDisabled();
    expect(review.queryByRole('button', { name: 'Accept' })).toBeNull();

    await act(async () => landed({ session: 's1a2b3c4', done: true, message: 'put its work on `feature/delta` — 1 commit(s).' }));
    await vi.waitFor(() => expect(review.queryByRole('button', { name: 'Landing…' })).toBeNull());
  });

  /**
   * LANDNAME1b (D145's LANDNAME1a note): a branch name entered in the head's ask asks the plan again with it, so the driver's
   * refusal shows before the press, and the press sends it; a press with no name stays `{ id }` (the test above).
   */
  it('asks the plan again with an entered branch name, and the press sends it', async () => {
    SESSIONS = [{ ...IN_A_TREE, state: 'completed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
    invoke.mockImplementation(async (_module: string, type: string, args?: { payload?: { branch?: string } }) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false,
          lands: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', commits: 1, uncommitted: 0 } }] };
      }
      if (type === 'LANDING') {
        return args?.payload?.branch === 'main'
          ? { session: 's1a2b3c4', form: 'branch', target: 'main', problem: 'a branch named `main` stands, which Daoris did not make.' }
          : { session: 's1a2b3c4', form: 'branch', target: 'feature/delta', source: 'workspace' };
      }
      if (type === 'LAND_SESSION_TREE') return { session: 's1a2b3c4', done: true, message: 'put its work on `mine/x` — 1 commit(s).' };
      if (type === 'SESSION_DIFF') return DIFF;
      return DRIVER_STATE;
    });

    show('s1a2b3c4', vi.fn());
    await userEvent.click(await within(screen.getByRole('main')).findByRole('button', { name: 'Accept…' }));
    const ask = screen.getByRole('group', { name: 'accept daoris/s-4e6837ed' });
    const field = await within(ask).findByRole('textbox', { name: 'Branch name' });
    expect(field).toHaveAttribute('placeholder', 'feature/delta');
    await userEvent.type(field, 'main');
    // Asked once after the pause, not per letter (the driver's plan reads git).
    expect(await within(ask).findByText(/which Daoris did not make/)).toBeTruthy();
    expect(invoke.mock.calls.filter(([, type, args]) => type === 'LANDING' && args?.payload?.branch)).toEqual([
      ['DAORIS.DRIVER', 'LANDING', { payload: { id: 's1a2b3c4', branch: 'main' } }],
    ]);

    await userEvent.clear(field);
    await userEvent.type(field, 'mine/x');
    // Held until the plan of that exact name has answered.
    await vi.waitFor(() => expect(within(ask).getByRole('button', { name: 'Accept' })).toBeEnabled());
    await userEvent.click(within(ask).getByRole('button', { name: 'Accept' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_SESSION_TREE', { payload: { id: 's1a2b3c4', branch: 'mine/x' }, timeoutMs: 6 * 60_000 });
  });

  /**
   * WAITFOOT1b (D168 point 3): *Accept…* stands in the head and in the strip at the foot, one press, and its ask is open in one
   * of them at a time: opening it in one puts the other down, a branch name typed there with it.
   */
  it('opens the work\'s ask in one place at a time, the head\'s or the foot\'s', async () => {
    SESSIONS = [{ ...IN_A_TREE, state: 'completed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false,
          lands: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', commits: 1, uncommitted: 0 } }] };
      }
      if (type === 'LANDING') return { session: 's1a2b3c4', form: 'branch', target: 'feature/delta', source: 'workspace' };
      if (type === 'SESSION_DIFF') return DIFF;
      return DRIVER_STATE;
    });
    const asks = () => screen.queryAllByRole('group', { name: 'accept daoris/s-4e6837ed' });

    show('s1a2b3c4', vi.fn());
    const strip = await screen.findByRole('region', { name: 'Waiting on you' });
    const presses = await screen.findAllByRole('button', { name: 'Accept…' });
    expect(presses).toHaveLength(2);
    const head = presses.find((press) => !strip.contains(press))!;

    await userEvent.click(within(strip).getByRole('button', { name: 'Accept…' }));
    expect(asks()).toHaveLength(1);
    expect(strip.contains(asks()[0]!)).toBe(true);
    await userEvent.type(within(asks()[0]!).getByRole('textbox', { name: 'Branch name' }), 'mine/x');

    await userEvent.click(head);
    expect(asks()).toHaveLength(1);
    expect(strip.contains(asks()[0]!)).toBe(false);
    expect(within(strip).getByRole('button', { name: 'Accept…' })).toBeInTheDocument();
    // The name typed at the foot went with its ask.
    expect(within(asks()[0]!).getByRole('textbox', { name: 'Branch name' })).toHaveValue('');
  });

  /**
   * ACCEPTFOOT1 (D164's note, D168 point 2): the owner, 2026-10-16, found no *Accept* in an open conversation whose turn ended
   * with a commit beyond the line: its record stays `working`, so the driver's look offered nothing, and only the review's side
   * bar had the press. Once its turn ends, the head and the foot offer it from the review's own answer; never while a turn runs.
   */
  describe('an open conversation whose turn ended with work', () => {
    const TALK = { ...CHAT, tree: 'C:/somewhere/.daoris/trees/default/engine/s-c0ffee11' };
    const BEYOND = {
      ...DIFF, session: 'c0ffee11', behind: { line: 'main', commits: 0, ahead: 1 }, treeBranch: 'daoris/s-c0ffee11',
      offer: { branch: 'daoris/s-c0ffee11', tree: 's-c0ffee11', commits: 1, uncommitted: 0 },
    };
    const NOTHING = { ...BEYOND, files: [], behind: { line: 'main', commits: 0, ahead: 0 }, offer: null };
    const answering = (review: object | (() => unknown), taking = false) => async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking };
      if (type === 'SESSION_GROUPS') return { sessions: [{ session: 'c0ffee11', group: 'working', shown: 'working', archived: false, teammate: false }] };
      if (type === 'LANDING') return { session: 'c0ffee11', form: 'branch', target: 'feature/delta', source: 'workspace' };
      if (type === 'SESSION_DIFF') return typeof review === 'function' ? review() : review;
      return DRIVER_STATE;
    };
    const turn = (taking: boolean) => act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 'c0ffee11', queued: [], taking }));
    const diffs = () => invoke.mock.calls.filter((call) => call[1] === 'SESSION_DIFF').length;

    // The second maker's review: a review read before the turn's last commit was reused at its end, within the 15 s a live review
    // is fresh for, so a turn that committed offered nothing; and a stale answer that offered stayed pressable while it was read again.
    it('reads the review again for the turn that ended, never offering from an answer read before it', async () => {
      SESSIONS = [TALK];
      let review: () => unknown = () => NOTHING;
      invoke.mockImplementation(answering(() => review()));

      show('c0ffee11');
      await waitFor(() => expect(diffs()).toBe(1));
      expect(await screen.findByRole('button', { name: 'Send' })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Accept…' })).not.toBeInTheDocument();

      turn(true);
      review = () => BEYOND;
      turn(false);
      const strip = await screen.findByRole('region', { name: 'Waiting on you' });
      expect(await within(strip).findByRole('button', { name: 'Accept…' })).toBeInTheDocument();

      // The next turn's answer is not in yet: the one before it, which offered, offers nothing meanwhile.
      turn(true);
      review = () => new Promise(() => {});
      turn(false);
      await waitFor(() => expect(diffs()).toBe(3));
      expect(screen.queryByRole('button', { name: 'Accept…' })).not.toBeInTheDocument();
    });

    it('offers none where the driver would land nothing, its work held by content', async () => {
      SESSIONS = [TALK];
      invoke.mockImplementation(answering({ ...BEYOND, offer: null }));

      show('c0ffee11');
      await waitFor(() => expect(diffs()).toBe(1));
      expect(await screen.findByRole('button', { name: 'Send' })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Accept…' })).not.toBeInTheDocument();
    });

    it('offers *Accept…* at its foot and its head once the turn has ended', async () => {
      SESSIONS = [TALK];
      invoke.mockImplementation(answering(BEYOND));

      show('c0ffee11');
      const strip = await screen.findByRole('region', { name: 'Waiting on you' });
      expect(await within(strip).findByRole('button', { name: 'Accept…' })).toBeInTheDocument();
      // The head's and the foot's, one press (D56).
      expect(screen.getAllByRole('button', { name: 'Accept…' })).toHaveLength(2);
    });

    it('offers none while a turn runs, and offers it the moment the turn ends', async () => {
      SESSIONS = [TALK];
      invoke.mockImplementation(answering(BEYOND, true));

      show('c0ffee11');
      expect(await screen.findByRole('button', { name: 'Stop turn' })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Accept…' })).not.toBeInTheDocument();
      expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DIFF', expect.anything());

      act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 'c0ffee11', queued: [], taking: false }));
      const strip = await screen.findByRole('region', { name: 'Waiting on you' });
      expect(await within(strip).findByRole('button', { name: 'Accept…' })).toBeInTheDocument();
    });

    it('offers none where its tree holds nothing beyond the line (REVIEWBASE4)', async () => {
      SESSIONS = [TALK];
      invoke.mockImplementation(answering(NOTHING));

      show('c0ffee11');
      await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DIFF', expect.anything()));
      expect(await screen.findByRole('button', { name: 'Send' })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Accept…' })).not.toBeInTheDocument();
      expect(screen.queryByRole('region', { name: 'Waiting on you' })).not.toBeInTheDocument();
    });
  });

  // WAITFOOT2a: the strip stands aside while the head's waits are on screen, never from under an ask open in it.
  it('keeps the strip up while the work\'s ask is open in it, though the head came on screen', async () => {
    const watched = watchScreen();
    try {
      SESSIONS = [{ ...IN_A_TREE, state: 'completed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
      invoke.mockImplementation(async (_module: string, type: string) => {
        if (type === 'SESSION_GROUPS') {
          return { sessions: [{ session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false,
            lands: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', commits: 1, uncommitted: 0 } }] };
        }
        if (type === 'LANDING') return { session: 's1a2b3c4', form: 'branch', target: 'feature/delta', source: 'workspace' };
        if (type === 'SESSION_DIFF') return DIFF;
        return DRIVER_STATE;
      });
      show('s1a2b3c4', vi.fn());
      await screen.findAllByRole('button', { name: 'Accept…' });
      await waitFor(() => expect(watched.observers()).not.toHaveLength(0));
      watched.show(false);
      const strip = screen.getByRole('region', { name: 'Waiting on you' });
      await userEvent.click(within(strip).getByRole('button', { name: 'Accept…' }));
      await userEvent.click(screen.getByRole('main'));

      watched.show(true);
      expect(within(strip).getByRole('group', { name: 'accept daoris/s-4e6837ed' })).toBeInTheDocument();
      // Put down, and the person gone from it, the strip stands aside: the head's *Accept…* is the one on screen.
      await userEvent.click(within(strip).getByRole('button', { name: 'Never mind' }));
      await userEvent.click(screen.getByRole('main'));
      expect(screen.queryByRole('region', { name: 'Waiting on you' })).toBeNull();
      expect(screen.getAllByRole('button', { name: 'Accept…' })).toHaveLength(1);
    } finally {
      watched.restore();
    }
  });

  /**
   * XAGENT1g (the second-agent design §7–§9): where a level asks a second opinion, the session's page draws it before *Accept…*
   * from the landing's plan, its findings read from the gate, and *Accept…*, which answers a dispute, sends the gate's token back.
   * *Ask again* is the driver's own press.
   */
  it('draws the second opinion on a session’s page, and Accept… answers its dispute with the gate’s token', async () => {
    SESSIONS = [{ ...IN_A_TREE, state: 'completed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
    const token = 'o1:4f9c2a7e:disputed:1:0';
    const disputed = {
      state: 'disputed', holds: true, required: false, opinion: 'o1', reviewer: 'codex-acp', product: 'Codex', maker: 'OpenAI',
      label: 'another-maker', reviewing: 'r7c1', findings: 1, disputes: 1, since: 0, answers: token,
    };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false,
          lands: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', commits: 1, uncommitted: 0 } }] };
      }
      if (type === 'LANDING') return { session: 's1a2b3c4', form: 'merge', target: 'main', source: 'workspace', opinion: disputed };
      if (type === 'OPINION_GATE') {
        return { session: 's1a2b3c4', done: true, message: '', opinion: disputed, detail: { tier: 'agent', first: {
          id: 'o1', reviewer: 'codex-acp', product: 'Codex', maker: 'OpenAI', label: 'another-maker', read: 'src/',
          findings: [{ number: 1, weight: 'must', where: 'src/a.ts:3', claim: 'Drops the last row.',
            beside: { answer: { said: 'rejected', evidence: 'The bound is exclusive.' }, disputed: true } }] } } };
      }
      if (type === 'ASK_OPINION') return { session: 's1a2b3c4', done: true, message: 'A second opinion on session s1a2b3c4\'s work is asked.' };
      if (type === 'LAND_SESSION_TREE') return { session: 's1a2b3c4', done: true, message: 'merged into `main`.' };
      if (type === 'SESSION_DIFF') return DIFF;
      return DRIVER_STATE;
    });
    const notify = vi.fn();

    show('s1a2b3c4', notify);
    // The head's, in the main area: the strip at the foot draws the same gate (WAITFOOT1b, D168).
    const head = within(screen.getByRole('main'));
    const gate = await head.findByRole('region', { name: 'Second opinion' });
    expect(gate).toHaveTextContent('1 finding by Codex (OpenAI) is disputed');
    await within(gate).findByText('Drops the last row.');
    expect(gate).toHaveTextContent('Rejected by the working session: The bound is exclusive.');
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'OPINION_GATE', { payload: { id: 's1a2b3c4' } });

    await userEvent.click(within(gate).getByRole('button', { name: 'Ask again' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ASK_OPINION', { payload: { id: 's1a2b3c4' } });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('A second opinion on session s1a2b3c4\'s work is asked.'));

    await userEvent.click(head.getByRole('button', { name: 'Accept…' }));
    const ask = screen.getByRole('group', { name: 'accept daoris/s-4e6837ed' });
    expect(ask).toHaveTextContent('Accepting answers what the second opinion left unsettled: 1 disputed finding.');
    await userEvent.click(within(ask).getByRole('button', { name: 'Accept' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_SESSION_TREE', { payload: { id: 's1a2b3c4', answers: token }, timeoutMs: 6 * 60_000 });
  });

  /**
   * SQUASHTIDY1b (D102's SQUASHTIDY1b note, the owner's case): a session whose work a squash-merged pull request carried, its
   * tree still standing, was offered *Accept…* again. Where the driver's reader says the line holds its commits by content,
   * its page offers no *Accept…*: the driver's sentence says where the work is, and *Discard branch…* asks once with the
   * driver's sentence naming the ref the commits stay at, then presses the review's Discard, unforced.
   */
  it('offers a squash-merged session’s discard on its page, never its landing, and discards through the review’s door unforced', async () => {
    const kept = 'refs/daoris/discarded/daoris/s-4e6837ed';
    SESSIONS = [{ ...IN_A_TREE, state: 'completed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false, lands: null,
          discards: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', says: 'Its work is on `main` by content (a squash merge).',
            keeps: kept, keptAt: `Its commits stay at \`${kept}\` until you delete that ref; \`git branch daoris/s-4e6837ed ${kept}\` brings the branch back.` } }] };
      }
      if (type === 'DISCARD_SESSION_TREE') {
        return { session: 's1a2b3c4', done: true,
          message: `removed the session tree at s-4e6837ed (branch \`daoris/s-4e6837ed\`): its work is on \`main\` by content (a squash merge). Its commits stay at \`${kept}\` until you delete that ref.` };
      }
      if (type === 'SESSION_DIFF') return DIFF;
      return DRIVER_STATE;
    });
    const notify = vi.fn();

    show('s1a2b3c4', notify);
    // The head's, in the main area: the strip at the foot draws the same line (WAITFOOT1b, D168).
    const head = within(screen.getByRole('main'));
    await userEvent.click(await head.findByRole('button', { name: 'Discard branch…' }));
    expect(screen.queryByRole('button', { name: 'Accept…' })).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'LANDING', expect.anything());
    expect(head.getByText('in its tree s-4e6837ed')).toBeTruthy();
    const ask = screen.getByRole('group', { name: 'discard daoris/s-4e6837ed' });
    expect(ask).toHaveTextContent(`Its commits stay at ${kept} until you delete that ref`);
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'DISCARD_SESSION_TREE', expect.anything());

    await userEvent.click(within(ask).getByRole('button', { name: 'Discard branch' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'DISCARD_SESSION_TREE', { payload: { id: 's1a2b3c4' } });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('its work is on `main` by content (a squash merge)')));
    await vi.waitFor(() => expect(screen.queryByRole('group', { name: 'discard daoris/s-4e6837ed' })).toBeNull());
  });

  /**
   * SQUASHTIDY1f (D102's SQUASHTIDY1f note): the review's own *Accept* was still offered for that session, and the landing
   * door would land a second copy. Its review offers no *Accept*: the driver's sentence stands in its place, and the head's
   * *Discard branch…* asks once and presses the review's Discard unforced, through the head's own press.
   */
  it('offers a squash-merged session’s discard in its review where Accept stood, through the head’s own press', async () => {
    const kept = 'refs/daoris/discarded/daoris/s-4e6837ed';
    SESSIONS = [{ ...IN_A_TREE, state: 'completed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false, lands: null,
          discards: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', says: 'Its work is on `main` by content (a squash merge).',
            keeps: kept, keptAt: `Its commits stay at \`${kept}\` until you delete that ref; \`git branch daoris/s-4e6837ed ${kept}\` brings the branch back.` } }] };
      }
      if (type === 'DISCARD_SESSION_TREE') {
        return { session: 's1a2b3c4', done: true,
          message: `removed the session tree at s-4e6837ed (branch \`daoris/s-4e6837ed\`): its work is on \`main\` by content (a squash merge). Its commits stay at \`${kept}\` until you delete that ref.` };
      }
      if (type === 'SESSION_DIFF') return DIFF;
      return DRIVER_STATE;
    });
    const notify = vi.fn();

    show('s1a2b3c4', notify);
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    const review = await screen.findByRole('tabpanel');
    await within(review).findByText('src/chunk.ts');
    await within(review).findByRole('button', { name: 'Discard branch…' });
    expect(review).toHaveTextContent('Its work is on main by content (a squash merge).');
    expect(within(review).queryByRole('button', { name: 'Accept' })).toBeNull();
    expect(within(review).queryByRole('button', { name: 'Discard tree' })).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'LANDING', expect.anything());

    await userEvent.click(within(review).getByRole('button', { name: 'Discard branch…' }));
    const ask = within(review).getByRole('group', { name: 'discard daoris/s-4e6837ed' });
    expect(ask).toHaveTextContent(`Its commits stay at ${kept} until you delete that ref`);
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'DISCARD_SESSION_TREE', expect.anything());
    await userEvent.click(within(ask).getByRole('button', { name: 'Discard branch' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'DISCARD_SESSION_TREE', { payload: { id: 's1a2b3c4' } });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_SESSION_TREE', expect.anything());
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('its work is on `main` by content (a squash merge)')));
  });

  it('accepts by asking the driver to land the work, and renders whatever it says back', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'LAND_SESSION_TREE') {
        return { session: 's1a2b3c4', done: true, message: 'merged `daoris/x` into `main` — 2 commit(s).' };
      }
      return DRIVER_STATE;
    });

    await review();
    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));

    // LEFT3 a: a landing may hand its branch to a plugin, so it waits as long as the plugin may (`pluginBound`).
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_SESSION_TREE', {
      payload: { id: 's1a2b3c4' },
      timeoutMs: 6 * 60_000,
    });
    expect(await screen.findByText(/merged `daoris\/x` into `main`/)).toBeTruthy();
  });

  /**
   * A refusal is an ANSWER and its sentence is the contract — "the checkout is not clean" is the
   * `reaching-in` guard speaking, and rewording it here would be a second copy of the reason.
   */
  it('renders a refused merge verbatim and changes nothing', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'LAND_SESSION_TREE') {
        return {
          session: 's1a2b3c4', done: false,
          message: "the repository's own checkout is not clean (2 paths), and merging into somebody's work in flight is exactly what Daoris does not do.",
        };
      }
      return DRIVER_STATE;
    });

    await review();
    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));

    expect(await screen.findByText(/not clean \(2 paths\)/)).toBeTruthy();
  });

  /**
   * 🔴 The one that matters most: a first press NEVER forces. The unforced call is what produces the
   * sentence naming what would be lost, and only then is a destructive confirm offered.
   */
  it('never forces a discard on the first press, and arms the confirm with the host’s own warning', async () => {
    let forced: unknown = 'never called';
    invoke.mockImplementation(async (_module: string, type: string, body?: { payload?: unknown }) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'DISCARD_SESSION_TREE') {
        forced = body?.payload;
        const payload = body?.payload as { force?: boolean } | undefined;
        return payload?.force
          ? { session: 's1a2b3c4', done: true, message: 'removed the session tree.' }
          : {
            session: 's1a2b3c4', done: false,
            message: 'the tree holds commits `main` has not taken:\nabc1234 cap hydration\nMerge them from the root, or say it again with --force to discard them.',
          };
      }
      return DRIVER_STATE;
    });

    await review();
    await userEvent.click(screen.getByRole('button', { name: 'Discard tree' }));

    // No `force` anywhere in the first call.
    expect(forced).toEqual({ id: 's1a2b3c4' });
    // The host's warning is what the person now reads, naming what would go.
    expect(await screen.findByText(/has not taken/)).toBeTruthy();
    // And only now is the destructive press available.
    const again = screen.getByRole('button', { name: 'Discard anyway' });

    await userEvent.click(again);
    expect(forced).toEqual({ id: 's1a2b3c4', force: true });
    expect(await screen.findByText(/removed the session tree/)).toBeTruthy();
  });

  /**
   * 🔴 REV3: an answer belongs to the session it was asked about. A refusal that landed after the
   * person moved to another session armed *discard it anyway* THERE — and the forced press discards
   * whichever session is attended now, a tree nobody had looked at.
   */
  it('drops a discard answer that lands after the person moved to another session', async () => {
    let answer: (value: unknown) => void = () => {};
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'DISCARD_SESSION_TREE') return new Promise((resolve) => { answer = resolve; });
      return DRIVER_STATE;
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const pane = (session: string) => (
      <QueryClientProvider client={client}><DiffPane session={session} hasTree /></QueryClientProvider>
    );

    const { rerender } = render(pane('s1a2b3c4'));
    await userEvent.click(await screen.findByRole('button', { name: 'Discard tree' }));
    rerender(pane('s9f8e7d6'));
    await screen.findByRole('button', { name: 'Discard tree' });
    await act(async () => answer({ session: 's1a2b3c4', done: false, message: 'the tree holds commits `main` has not taken.' }));

    expect(screen.queryByRole('button', { name: 'Discard anyway' })).toBeNull();
    expect(screen.queryByText(/has not taken/)).toBeNull();
  });

  it('lets the person back out of a discard they have been warned about', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'DISCARD_SESSION_TREE') {
        return { session: 's1a2b3c4', done: false, message: 'the tree holds commits `main` has not taken.' };
      }
      return DRIVER_STATE;
    });

    await review();
    await userEvent.click(screen.getByRole('button', { name: 'Discard tree' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Never mind' }));

    expect(screen.queryByRole('button', { name: 'Discard anyway' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Discard tree' })).toBeTruthy();
  });

  /** Sending it back is a door into the platform's own composer, never a second publish path. */
  it('hands the repository to whoever opens the quest composer', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? DIFF : DRIVER_STATE));
    const onSendBack = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}} onSendBack={onSendBack} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Send back…' }));

    expect(onSendBack).toHaveBeenCalledWith('engine');
  });

  it('offers no send-back door where none was given', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? DIFF : DRIVER_STATE));

    await review();
    expect(screen.queryByRole('button', { name: 'Send back…' })).toBeNull();
  });

  /**
   * A record that travelled here from the machine that did the work names no tree on THIS one, so
   * there is nothing to merge and nothing to discard. Offering either would be offering something
   * that can only ever refuse — and one of them is destructive.
   */
  it('offers no acts at all on a session that holds no tree here', async () => {
    SESSIONS = [DRIVEN];
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? DIFF : DRIVER_STATE));

    await review();

    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Discard tree' })).toBeNull();
    // The diff itself is still readable — seeing the work never depended on holding the tree.
    expect(screen.getByText('src/chunk.ts')).toBeTruthy();
  });

  /**
   * UX5 U66, seen on the window: a chat in the repository's OWN checkout names that checkout as its
   * tree, and Review offered to merge it and to discard it. The tree layer refuses both — a
   * checkout is never a side effect's to delete — so they were moves that could only refuse. The
   * work can still be sent back as a quest: that is about the work, not the tree.
   */
  it('offers no tree acts on a session in the repository\u2019s own checkout, and still sends it back', async () => {
    SESSIONS = [{ ...DRIVEN, tree: 'C:/somewhere/engine' }];
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? DIFF : DRIVER_STATE));
    const onSendBack = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <Tooltip.Provider>
          <WorkFrame selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}} onSendBack={onSendBack} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    await screen.findByText('src/chunk.ts');

    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Discard tree' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Send back…' }));
    expect(onSendBack).toHaveBeenCalledWith('engine');
  });
});
