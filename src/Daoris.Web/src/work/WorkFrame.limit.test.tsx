import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';

// A conversation its account's limit holds, in the Work frame: one part of its suite.
// The bridge is mocked as present, because the frame does not exist without one.
vi.mock('@shenora/react', async () => (await import('../test/workFrameBridge')).shenoraMock());

import i18n from '../locales/i18n';
import { moment } from '../locales/format';
import { eventHandlers, invoke } from '../test/workFrameBridge';
import { CHAT, DRIVEN, DRIVER_STATE, QUESTS, REGISTRY, ROSTER, show, userEvent } from '../test/workFrameKit';

let SESSIONS: unknown[] = [];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

afterEach(() => window.localStorage.clear());

/**
 * The conversation's line: read from its events' `chat.limit-waits`, never from the accounts alone, worded by the
 * walk of its agent's accounts; *Stop waiting* once it has ended, its refusal said in the runner's own sentence.
 */
describe('a conversation its limit holds', () => {
  const until = new Date(Date.now() + 3 * 3600_000).toISOString();
  const cooling = { until, stated: true, window: 'session', seen: until, assumedZone: false, notBelieved: false };
  const ACCOUNTS = {
    agents: [{
      agent: 'claude-code', speaks: true, own: {}, accounts: [{ name: 'personal', cooling }],
      scopes: [{
        workspace: null, default: null, list: ['personal'], begins: null, unknown: [], problem: null, near: [],
        use: { use: 'goal', keep: null, early: true, near: 90 },
        next: { reason: 'waits', others: [{ account: 'personal', hold: 'cooling', until }] },
      }],
    }],
  };
  const HARNESSES = {
    ...ROSTER,
    harnesses: [{ ...ROSTER.harnesses[0], profiles: [{ name: 'personal', home: 'x', login: 'in' }] }],
  };
  const ENDED = { ...CHAT, door: 'claude-code', profile: 'personal', state: 'completed' };
  const at = '2026-10-24T09:00:00Z';
  const WAITING = [
    { seq: 1, at, kind: 'user', origin: 'person', text: 'Go on with the loader.' },
    { seq: 2, at, kind: 'turn', stopReason: 'error' },
    {
      seq: 3, at, kind: 'note', text: 'No account of `claude-code` has room now.',
      parts: [{ code: 'chat.limit-waits', values: { owner: 'claude-code', until, why: 'stated' } }],
    },
  ];
  const answering = (events: unknown[], stopped: unknown = { stopped: true, message: '' }) =>
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return HARNESSES;
      if (type === 'ACCOUNTS') return ACCOUNTS;
      if (type === 'SESSION_HISTORY') return { events, earlier: false };
      if (type === 'SESSION_STOP_WAITING') return stopped;
      return DRIVER_STATE;
    });

  beforeEach(() => {
    SESSIONS = [ENDED];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    await i18n.changeLanguage('en');
  });

  it('says when it goes on by itself, and stops waiting by its press', async () => {
    answering(WAITING);
    show('c0ffee11');

    const [line] = await screen.findAllByRole('group', { name: 'Waiting for a limit' });
    expect(line!).toHaveTextContent(
      `Waiting for a limit to reset: it resumes by itself at ${moment(until)}, on personal. It goes on in its own conversation.`,
    );
    // The head's line, and the foot's while the head is off screen; its own line stands where Daoris's offer to hand over would.
    expect(screen.queryByRole('group', { name: 'hand-over offer' })).toBeNull();
    expect(within(line!).queryByRole('button', { name: 'Stop…' })).toBeNull();
    await userEvent.setup().click(within(line!).getByRole('button', { name: 'Stop waiting' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_STOP_WAITING', { payload: { id: 'c0ffee11' } });
  });

  it('says a refused *Stop waiting* in the runner’s own sentence', async () => {
    answering(WAITING, { stopped: false, message: 'conversation `c0ffee11` does not wait for its account\'s limit.' });
    show('c0ffee11');

    const [line] = await screen.findAllByRole('group', { name: 'Waiting for a limit' });
    await userEvent.setup().click(within(line!).getByRole('button', { name: 'Stop waiting' }));
    expect(await within(line!).findByText(/does not wait for its account's limit/)).toBeInTheDocument();
  });

  it('draws no line where its events hold no wait, its account cooling or not', async () => {
    answering(WAITING.slice(0, 2));
    show('c0ffee11');

    await screen.findByRole('group', { name: 'Session actions' });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HISTORY', expect.anything()));
    expect(screen.queryByRole('group', { name: 'Waiting for a limit' })).toBeNull();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    answering(WAITING);
    show('c0ffee11');

    const [line] = await screen.findAllByRole('group', { name: '等待额度' });
    expect(line!).toHaveTextContent(`正在等待额度重置：将于 ${moment(until)} 在 personal 上自动继续。它将在原对话中继续。`);
    expect(within(line!).getByRole('button', { name: '停止等待' })).toBeInTheDocument();
  });
});

/**
 * A closed quest's finished session whose words are held for a cooling account reads the wait on its rail row and, by the
 * reader, its hold's reason in the panel's head, never *Finished*; with nothing held the head reads *Finished*.
 */
describe('a closed quest’s session held for an account’s reset', () => {
  const until = '2026-10-28T12:30:00Z';
  const HELD = {
    session: 's1a2b3c4', group: 'pending', shown: 'limit-waits', archived: false, teammate: false,
    holds: { why: 'cooling', reason: 'every account is cooling.', until, account: 'account-2', accountName: 'Work' },
  };
  const FREE = { session: 's1a2b3c4', group: 'done', shown: 'completed', archived: false, teammate: false };
  const head = async () => (await screen.findByRole('region', { name: i18n.t('work.session.label') })).querySelector('[data-region="head"]') as HTMLElement;
  const answering = (row: unknown) =>
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_GROUPS' ? { sessions: [row] } : DRIVER_STATE));

  beforeEach(() => {
    SESSIONS = [{ ...DRIVEN, state: 'completed' }];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    await i18n.changeLanguage('en');
  });

  it('wears the wait on its row and its hold in the head, never finished, in English and 中文', async () => {
    answering(HELD);
    show('s1a2b3c4');

    const header = await head();
    await waitFor(() => expect(header).toHaveTextContent('every account is cooling.'));
    expect(within(header).getByText('Held')).toBeInTheDocument();
    expect(header).not.toHaveTextContent('Finished');
    // The rail's row beside it says the wait's word, and still the time and the account on its line.
    expect(screen.getAllByText('waiting for a limit to reset')).toHaveLength(1);
    expect(screen.queryByText('completed')).toBeNull();
    expect(screen.getAllByText((said) => said.includes(`it resumes by itself at ${moment(until)}, on Work.`)).length).toBeGreaterThan(0);

    await i18n.changeLanguage('zh');
    await waitFor(() => expect(screen.getAllByText('正在等待额度重置')).toHaveLength(1));
    expect(within(await head()).getByText('被挂起')).toBeInTheDocument();
    expect(await head()).not.toHaveTextContent('已完成');
  });

  it('reads finished in the head where nothing is held', async () => {
    answering(FREE);
    show('s1a2b3c4');

    const header = await head();
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STATE_SLICE', { payload: { slice: 'groups' } }));
    await waitFor(() => expect(header).toHaveTextContent('Finished'));
    expect(screen.queryByText('waiting for a limit to reset')).toBeNull();
  });
});
