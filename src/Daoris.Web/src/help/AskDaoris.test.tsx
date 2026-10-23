import { StrictMode, useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// HELP1a (D89): Ask Daoris's conversation, with the bridge mocked as present — the panel exists only in
// the shell, since most of what it reads is this machine's (D47 §4).

const { invoke, notifyReady, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  // The state hub's ear (STATEHUB1f, D169): what the mocked shell publishes after a press reaches it, as the kit's pump would.
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
import { keys } from '../data/queries';
import { useStateHub } from '../bridge/state/stateHub';
import { pushedOf, useStateSlices } from '../bridge/state/slices';
import { asksSlice, questsSlice } from '../bridge/sessions/quests';
import { sessionsSlice } from '../bridge/sessions/sessions';

/** ASKQUESTS1: the slices a go card's standings are read from, pushed as `ShellSignals` mounts them. */
const PUSHED_HERE = [questsSlice, asksSlice, sessionsSlice];
import { code } from '../test/code';
import { AskDaoris } from './AskDaoris';
import type { HelpWhere } from './where';

const HELP = {
  id: 'h1e1p000', quest: null, repository: 'daoris:help', adapter: 'claude-code-acp', state: 'working',
  kind: 'chat', created: '2026-09-29T00:00:00Z', updated: '2026-09-29T00:01:00Z', workspace: 'default',
};

let SESSIONS: unknown[] = [];
let HELPER: string | null = 'claude-code-acp';
let PROPOSALS: unknown[] = [];
/** ASKQUESTS1: what the conversation published, by id, and the quests and asks the service lists. */
let PUBLISHED_HERE: unknown[] = [];
let QUESTS: unknown[] = [];
let ASKS: unknown[] = [];
const APPLIED_SETTING = { message: 'Applied: `#p1a2b3c4` — Drive `engine`.', applied: true };
let APPLIED: unknown = APPLIED_SETTING;
const asked: string[] = [];

function respond(url: string): Response {
  asked.push(url);
  // As the ledger answers: an ended session only to a caller that asks for closed ones.
  if (url.startsWith('/api/sessions')) {
    return Response.json(url.includes('includeClosed=true') ? SESSIONS
      : SESSIONS.filter((row) => ['queued', 'starting', 'working', 'awaiting-person'].includes((row as { state: string }).state)));
  }
  if (url.startsWith('/api/registry')) return Response.json([]);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/asks')) return Response.json(ASKS);
  throw new Error(`unstubbed request: ${url}`);
}

function bridge(start: { sessionId: string | null; message: string } = { sessionId: HELP.id, message: 'opened' }) {
  invoke.mockImplementation(async (_module: string, type: string) => {
    switch (type) {
      case 'STATE': return { drivable: ['engine'], holds: [], trees: [], helperAdapter: HELPER ?? '' };
      case 'HARNESSES': return { harnesses: [] };
      case 'LINES': return { lines: [], landings: [] };
      case 'START_HELP': return start;
      case 'SESSION_INPUT': return { sent: true };
      case 'SESSION_HISTORY': return { session: HELP.id, events: [], earlier: false, latest: 0 };
      case 'SESSION_QUEUE': return { session: HELP.id, queued: [], taking: false };
      case 'END_CHAT': return { ended: true };
      case 'HELP_PROPOSALS': return { session: HELP.id, proposals: PROPOSALS };
      case 'HELP_APPLY': return APPLIED;
      case 'HELP_DISMISS': return { message: 'Not now: the person did not apply `#p1a2b3c4`.' };
      case 'HELP_PUBLISHED': return { session: HELP.id, published: PUBLISHED_HERE };
      default: return {};
    }
  });
}

/** How many times the page asked the driver to open Ask Daoris's conversation. */
const starts = () => invoke.mock.calls.filter(([, type]) => type === 'START_HELP').length;

/** The window's state hub, as `ShellSignals` mounts it: the sessions a finished conversation moved are asked again here. */
function Hub() {
  useStateHub();
  return null;
}

function show(where?: Omit<HelpWhere, 'session'>, attending: string | null = null, onGo = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Hub />
      <Tooltip.Provider>
        <AskDaoris where={where} attending={attending} onGo={onGo} onClose={vi.fn()} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('Ask Daoris, with an agent named', () => {
  beforeEach(() => {
    SESSIONS = [];
    HELPER = 'claude-code-acp';
    asked.length = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('starts a conversation with the first message, and sends that message to it', async () => {
    bridge();
    show();

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'how do I drive a repository?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'how do I drive a repository?' },
    }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'START_HELP', {});
    // Its conversations are read across every workspace, and the panel keeps the window's own (ASKSCOPE1d).
    expect(asked.some((url) => url.includes('repository=daoris%3Ahelp') && !url.includes('workspace='))).toBe(true);
  });

  /** QUEUEEDIT1b: words waiting behind Ask Daoris's turn can be taken back out by their id. */
  it('removes a waiting message from the conversation by its id', async () => {
    SESSIONS = [HELP];
    bridge();
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      if (type === 'SESSION_QUEUE') return { session: HELP.id, queued: [{ id: 'q1', text: 'and the index', files: [] }], taking: true };
      if (type === 'SESSION_QUEUE_EDIT') return { done: true };
      return answered(module, type, ...rest);
    });
    show();

    await userEvent.click(await screen.findByRole('button', { name: 'Remove the waiting message: and the index' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE_EDIT', { payload: { session: HELP.id, id: 'q1' } });
  });

  /**
   * HELP4: with none running, a message first opens the conversation, which takes seconds (the room is
   * written, the agent spawned). The person's words showed nowhere meanwhile, and read as lost. They are
   * shown at once, as opening Ask Daoris, and stay shown until the driver's own queue answers for the
   * conversation — never gone while the session list catches up, and never shown twice. The send opens
   * it here: the one opened as the panel showed (HELP5) was refused, silently.
   */
  it('shows the first words at once while the conversation opens, and keeps them until the driver has them', async () => {
    const words = 'tidy the branches so one PR is left';
    let open: (answer: { sessionId: string | null; message: string }) => void = () => {};
    bridge();
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      if (type === 'START_HELP' && starts() === 1) return { sessionId: null, message: 'not yet' };
      if (type === 'START_HELP') return new Promise((resolve) => { open = resolve; });
      // The driver holds them for a door still opening: taking, and saying why.
      if (type === 'SESSION_QUEUE') return { session: HELP.id, queued: [{ text: words, files: [] }], taking: true, opening: true };
      return answered(module, type, ...rest);
    });
    // The session list answers late, so the gap between the words going and the page watching the new
    // conversation is one this test can stand in.
    let listed: () => void = () => {};
    const late = new Promise<void>((resolve) => { listed = resolve; });
    let holding = false;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      if (holding && String(input).startsWith('/api/sessions')) await late;
      return respond(String(input));
    }));
    show();

    const box = await screen.findByLabelText('Message');
    await waitFor(() => expect(starts()).toBe(1));
    await userEvent.type(box, words);
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText(words)).toBeInTheDocument();
    expect(screen.getByText('opening Ask Daoris…')).toBeInTheDocument();
    expect(screen.queryByText('not yet')).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());

    holding = true;
    SESSIONS = [HELP];
    await act(async () => { open({ sessionId: HELP.id, message: 'opened' }); });

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: words },
    }));
    expect(screen.getByText(words)).toBeInTheDocument();
    expect(screen.getByText('opening Ask Daoris…')).toBeInTheDocument();

    await act(async () => { listed(); });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE', { payload: { id: HELP.id } }));
    // Once in the conversation, besides its head, which calls it by them (ASKHIST1c).
    await waitFor(() => expect(screen.getAllByText(words, { ignore: 'script, style, h3' })).toHaveLength(1));
    expect(screen.getByRole('heading', { name: words })).toBeInTheDocument();
    expect(screen.getByText('opening Ask Daoris…')).toBeInTheDocument();
  });

  /**
   * HELPSTART1: the driver answers the press once the conversation is placed, `starting`, its record still `queued` while the
   * room is read. The words go to it at once and show once, as it opening, from the driver's own queue.
   */
  it('hands the first words to a conversation the driver answers as starting, shown once as it opens', async () => {
    const words = 'what is a workspace?';
    const placed = { ...HELP, state: 'queued' };
    bridge({ sessionId: HELP.id, message: 'Ask Daoris `h1e1p000` is starting.' });
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      if (type === 'START_HELP') {
        SESSIONS = [placed];
        return { sessionId: HELP.id, message: 'Ask Daoris `h1e1p000` is starting.', running: false, starting: true };
      }
      if (type === 'SESSION_QUEUE') return { session: HELP.id, queued: [{ text: words, files: [] }], taking: false, opening: true };
      return answered(module, type, ...rest);
    });
    show();

    await waitFor(() => expect(starts()).toBe(1));
    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, words);
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: words },
    }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE', { payload: { id: HELP.id } }));
    await waitFor(() => expect(screen.getAllByText(words, { ignore: 'script, style, h3' })).toHaveLength(1));
    expect(screen.getByText('opening Ask Daoris…')).toBeInTheDocument();
    expect(starts()).toBe(1);
  });

  /**
   * SENDLOST1: on a loaded machine the page stopped waiting on Ask Daoris's start (the bridge's TIMEOUT) and the person's
   * first words vanished, though the driver went on to open the conversation. They stay shown, saying the machine has not
   * answered yet, nothing put back in the box to send twice, and go once to the conversation the list next shows.
   */
  it('keeps the first words when the machine stops answering, and gives them once to the conversation the list then shows', async () => {
    const words = 'why did my message vanish?';
    bridge();
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      // The one opened ahead could not; the send's own is still at work when the page stops waiting for it.
      if (type === 'START_HELP' && starts() === 1) return { sessionId: null, message: 'not yet' };
      if (type === 'START_HELP') throw Object.assign(new Error('timed out'), { code: 'TIMEOUT' });
      return answered(module, type, ...rest);
    });
    let listed: () => void = () => {};
    const late = new Promise<void>((resolve) => { listed = resolve; });
    let holding = false;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      if (holding && String(input).startsWith('/api/sessions')) await late;
      return respond(String(input));
    }));
    show();

    const box = await screen.findByLabelText('Message');
    await waitFor(() => expect(starts()).toBe(1));
    await userEvent.type(box, words);
    holding = true;
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(starts()).toBe(2));
    expect(await screen.findByText('The machine has not answered yet; it may still take them.')).toBeInTheDocument();
    expect(screen.getByText(words)).toBeInTheDocument();
    expect((screen.getByLabelText('Message') as HTMLTextAreaElement).value).toBe('');
    expect(screen.queryByText(/did not answer in time/)).toBeNull();

    // The list's next read holds the conversation the driver opened: the words go to it, once.
    SESSIONS = [HELP];
    await act(async () => { listed(); });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: words },
    }));
    expect(invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT')).toHaveLength(1);
    expect(starts()).toBe(2);
  });

  /**
   * SENDLOST1: words to the conversation running show the moment they are sent, as sending. A send the page stopped waiting
   * on keeps them, saying the machine has not answered yet — never back in the box to be sent twice — until the
   * conversation's own record holds them.
   */
  it('shows words as sending, keeps them when their send times out, and lets them go once the record holds them', async () => {
    const words = 'and the line?';
    SESSIONS = [HELP];
    bridge();
    const answered = invoke.getMockImplementation()!;
    let stop: () => void = () => {};
    let recorded = false;
    let read: () => void = () => {};
    const reading = new Promise<void>((resolve) => { read = resolve; });
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      if (type === 'SESSION_INPUT') {
        return new Promise((_, reject) => {
          stop = () => { recorded = true; reject(Object.assign(new Error('timed out'), { code: 'TIMEOUT' })); };
        });
      }
      if (type === 'SESSION_HISTORY' && recorded) {
        await reading;
        return {
          session: HELP.id, earlier: false, latest: 1,
          events: [{ seq: 1, at: new Date().toISOString(), kind: 'user', origin: 'person', text: words }],
        };
      }
      return answered(module, type, ...rest);
    });
    show();

    const box = await screen.findByLabelText('Message');
    await screen.findByRole('button', { name: 'New conversation' });
    await userEvent.type(box, words);
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText('sending…')).toBeInTheDocument();
    expect(screen.getByText(words)).toBeInTheDocument();

    await act(async () => { stop(); });
    expect(await screen.findByText('The machine has not answered yet; it may still take them.')).toBeInTheDocument();
    expect(screen.getByText(words)).toBeInTheDocument();
    expect((screen.getByLabelText('Message') as HTMLTextAreaElement).value).toBe('');
    expect(screen.queryByText(/did not answer in time/)).toBeNull();

    await act(async () => { read(); });
    await waitFor(() => expect(screen.queryByText('The machine has not answered yet; it may still take them.')).toBeNull());
    expect(invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT')).toHaveLength(1);
  });

  /**
   * SENDLOST1: words whose send timed out to a conversation that then ended without them never arrived: they go back in the
   * box, said so, for the person to send again.
   */
  it('gives words back once their conversation ended without them', async () => {
    const words = 'are you there?';
    SESSIONS = [HELP];
    // Once it ended, the next opens ahead (HELP5), under an id of its own.
    bridge({ sessionId: 'n3xt0000', message: 'opened' });
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      if (type === 'SESSION_INPUT') {
        SESSIONS = [{ ...HELP, state: 'failed' }];
        throw Object.assign(new Error('timed out'), { code: 'TIMEOUT' });
      }
      return answered(module, type, ...rest);
    });
    show();

    const box = await screen.findByLabelText('Message');
    await screen.findByRole('button', { name: 'New conversation' });
    await userEvent.type(box, words);
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText(/Ask Daoris ended before this reached it/)).toBeInTheDocument();
    await waitFor(() => expect((screen.getByLabelText('Message') as HTMLTextAreaElement).value).toBe(words));
    expect(invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT')).toHaveLength(1);
  });

  /**
   * HELP5: the agent's spawn and its protocol session took seconds after the person pressed send. The
   * conversation opens as the panel shows instead: once, however often the panel draws, and once under
   * React's development double-mount. Nobody has spoken in it yet, so the panel goes on showing what it
   * showed, and the first words go to it rather than opening another.
   */
  it('opens the conversation as the panel shows, once, and hands it the first words', async () => {
    bridge();
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      // The ledger holds the record from the moment it opens.
      if (type === 'START_HELP') SESSIONS = [HELP];
      return answered(module, type, ...rest);
    });
    const listings = () => asked.filter((url) => url.includes('repository=daoris%3Ahelp')).length;
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    // Known before the first render, as on a page that has read them already: the panel is idle as it
    // mounts, which is when the double-mount runs the effect twice.
    client.setQueryData(keys.driver, { drivable: ['engine'], holds: [], trees: [], helperAdapter: HELPER });
    client.setQueryData(keys.sessions(HELP.repository, true, null), []);
    render(
      <StrictMode>
        <QueryClientProvider client={client}>
          <Tooltip.Provider>
            <AskDaoris onGo={vi.fn()} onClose={vi.fn()} />
          </Tooltip.Provider>
        </QueryClientProvider>
      </StrictMode>,
    );

    await waitFor(() => expect(starts()).toBe(1));
    // The list, asked again once it opened, holds it; the panel still shows its starters.
    await waitFor(() => expect(listings()).toBeGreaterThanOrEqual(2));
    await act(async () => {});
    expect(screen.getByText(/Ask below about Daoris on this machine/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New conversation' })).toBeNull();
    // Asking every query again does not forget which one it is.
    await act(() => client.invalidateQueries());
    expect(screen.getByText(/Ask below about Daoris on this machine/)).toBeInTheDocument();

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'how do I drive a repository?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'how do I drive a repository?' },
    }));
    expect(starts()).toBe(1);
    // Spoken in, it is the conversation the panel shows.
    expect(await screen.findByRole('button', { name: 'New conversation' })).toBeInTheDocument();
  });

  /**
   * HELP5 with HELP4: words sent while the conversation opened ahead is still opening show at once, as
   * opening Ask Daoris, and go to it once it answers — never to a second one.
   */
  it('holds words sent while the conversation opened ahead is still opening, and gives them to it', async () => {
    const words = 'why is engine held?';
    let open: (answer: { sessionId: string | null; message: string }) => void = () => {};
    bridge();
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      if (type === 'START_HELP') return new Promise((resolve) => { open = resolve; });
      return answered(module, type, ...rest);
    });
    show();

    // Opening before a word is typed.
    await waitFor(() => expect(starts()).toBe(1));
    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, words);
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText(words)).toBeInTheDocument();
    expect(screen.getByText('opening Ask Daoris…')).toBeInTheDocument();
    expect(starts()).toBe(1);
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());

    SESSIONS = [HELP];
    await act(async () => { open({ sessionId: HELP.id, message: 'opened' }); });

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: words },
    }));
    expect(starts()).toBe(1);
  });

  /**
   * HELP5: the side bar and Quick Ask hold one conversation. The one opened ahead is spoken in from either
   * host, and an opening that answers late, from the other, never hides the conversation spoken in.
   */
  it('keeps the conversation spoken in shown in both hosts, however late the other opening answers', async () => {
    let late: (answer: { sessionId: string | null; message: string }) => void = () => {};
    bridge();
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      if (type === 'START_HELP' && starts() === 1) return new Promise((resolve) => { late = resolve; });
      if (type === 'START_HELP') SESSIONS = [HELP];
      return answered(module, type, ...rest);
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(keys.driver, { drivable: ['engine'], holds: [], trees: [], helperAdapter: HELPER });
    client.setQueryData(keys.sessions(HELP.repository, true, null), []);
    // The window's hub hears what each opening published (STATEHUB1h): the sessions it opened one among.
    const hosts = (count: number) => (
      <QueryClientProvider client={client}>
        <Hub />
        <Tooltip.Provider>
          {['side', 'quick'].slice(0, count).map((host) => <AskDaoris key={host} onGo={vi.fn()} onClose={vi.fn()} />)}
        </Tooltip.Provider>
      </QueryClientProvider>
    );

    const { rerender } = render(hosts(1));
    // The side bar's opening, still on its way; then Quick Ask's, answered at once.
    await waitFor(() => expect(starts()).toBe(1));
    rerender(hosts(2));
    await waitFor(() => expect(starts()).toBe(2));

    await userEvent.type(screen.getAllByLabelText('Message')[1], 'why is engine held?');
    await userEvent.click(screen.getAllByRole('button', { name: 'Send' })[1]);
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'why is engine held?' },
    }));
    await waitFor(() => expect(screen.getAllByRole('button', { name: 'New conversation' })).toHaveLength(2));

    // Settled all the way, its answer invalidating the list included, before the panels are read again.
    const listings = asked.length;
    await act(async () => { late({ sessionId: HELP.id, message: 'opened' }); });
    await waitFor(() => expect(asked.length).toBeGreaterThan(listings));
    await act(() => new Promise((settled) => { setTimeout(settled, 50); }));
    expect(screen.getAllByRole('button', { name: 'New conversation' })).toHaveLength(2);
  });

  /**
   * HELP5: a conversation that runs is carried on, so none is opened beside it. *New conversation*
   * clears the panel, and once the one it finished has gone, the next opens ahead too.
   */
  it('opens none beside the one running, and the next once new conversation has finished it', async () => {
    SESSIONS = [HELP];
    bridge({ sessionId: 'n3xt0000', message: 'opened' });
    show();

    await screen.findByRole('button', { name: 'New conversation' });
    await act(async () => {});
    expect(starts()).toBe(0);

    // Finishing it ends it, and the list says so when it is asked again.
    SESSIONS = [{ ...HELP, state: 'completed' }];
    await userEvent.click(screen.getByRole('button', { name: 'New conversation' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'END_CHAT', { payload: { id: HELP.id } });
    await waitFor(() => expect(starts()).toBe(1));
    expect(await screen.findByText(/Ask below about Daoris on this machine/)).toBeInTheDocument();
  });

  it('keeps the conversation and its draft while New conversation waits, and after its refusal', async () => {
    SESSIONS = [HELP];
    bridge();
    const answered = invoke.getMockImplementation()!;
    let refuse!: (error: unknown) => void;
    invoke.mockImplementation((module: string, type: string, ...rest: unknown[]) => type === 'END_CHAT'
      ? new Promise((_, reject) => { refuse = reject; }) : answered(module, type, ...rest));
    show();
    await userEvent.type(await screen.findByLabelText('Message'), 'keep my draft');
    await userEvent.click(screen.getByRole('button', { name: 'New conversation' }));
    expect(screen.getByLabelText('Message')).toHaveValue('keep my draft');
    expect(screen.getByRole('button', { name: 'New conversation' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Send' })).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Finishing this conversation');
    expect(screen.queryByText(/Ask below about Daoris on this machine/)).toBeNull();
    await act(async () => refuse(new Error('The conversation could not be finished.')));
    expect(await screen.findByRole('alert')).toHaveTextContent('The conversation could not be finished.');
    expect(screen.getByLabelText('Message')).toHaveValue('keep my draft');
    expect(starts()).toBe(0);
  });

  it('keeps the draft when the host answers that the conversation was not ended', async () => {
    SESSIONS = [HELP];
    bridge();
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation((module: string, type: string, ...rest: unknown[]) => type === 'END_CHAT'
      ? Promise.resolve({ ended: false }) : answered(module, type, ...rest));
    show();
    await userEvent.type(await screen.findByLabelText('Message'), 'keep these words');
    await userEvent.click(screen.getByRole('button', { name: 'New conversation' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Your draft is still here');
    expect(screen.getByLabelText('Message')).toHaveValue('keep these words');
    expect(starts()).toBe(0);
  });

  /**
   * DOCK1d: a question asked from the palette arrives already asked — sent once, as a typed one is,
   * and not again when the frame draws it again; a new question is a new id.
   */
  it('sends a question it is handed once, as if typed, and a new one when handed another', async () => {
    bridge();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const view = (opening: { text: string; id: number }) => (
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <AskDaoris opening={opening} onGo={vi.fn()} onClose={vi.fn()} />
        </Tooltip.Provider>
      </QueryClientProvider>
    );
    const sent = () => invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT').map(([, , request]) => request.payload.text);

    const { rerender } = render(view({ text: 'why is engine held?', id: 1 }));
    await waitFor(() => expect(sent()).toEqual(['why is engine held?']));
    rerender(view({ text: 'why is engine held?', id: 1 }));
    rerender(view({ text: 'and how do I drive it?', id: 2 }));
    await waitFor(() => expect(sent()).toEqual(['why is engine held?', 'and how do I drive it?']));
  });

  /**
   * CTX1 (D138, design §3): *Ask Daoris about it* hands its words as a draft, quoted in the box for the person's question,
   * and sends nothing until they do; it is let go once in the box, as a sent one is.
   */
  it('puts a draft it is handed in the box, sends nothing, and lets it go', async () => {
    bridge();
    const onOpened = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <AskDaoris opening={{ text: '> a per-frame budget\n\n', id: 1, draft: true }} onOpened={onOpened} onGo={vi.fn()} onClose={vi.fn()} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue('> a per-frame budget\n\n'));
    expect(onOpened).toHaveBeenCalledOnce();
    expect(invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT')).toEqual([]);
  });

  /**
   * NEW1g (D162): a draft that will start a new conversation shows one. With the newest ended, the panel clears to a blank one
   * first (as *New conversation* does, without ending anything), and the draft goes in the box, not under the ended transcript.
   */
  describe('a draft handed to it', () => {
    const ENDED_NOTICE = 'This conversation has ended. A message starts a new one.';
    const sent = () => invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT');
    const offer = (readied: string | null = null, text = '> a per-frame budget\n\n') => {
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      if (readied) client.setQueryData(keys.helpReadied, { readied, spoken: [] });
      return render(
        <QueryClientProvider client={client}>
          <Tooltip.Provider>
            <AskDaoris opening={{ text, id: 1, draft: true }} onOpened={vi.fn()} onGo={vi.fn()} onClose={vi.fn()} />
          </Tooltip.Provider>
        </QueryClientProvider>,
      );
    };

    it('opens a new conversation when the newest has ended: no ended transcript, the draft in the box, nothing sent', async () => {
      SESSIONS = [{ ...HELP, state: 'completed' }];
      bridge({ sessionId: 'n3xt0000', message: 'opened' });
      offer();

      await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue('> a per-frame budget\n\n'));
      await act(async () => {});
      expect(screen.queryByText(ENDED_NOTICE)).not.toBeInTheDocument();
      expect(sent()).toEqual([]);
      expect(invoke.mock.calls.filter(([, type]) => type === 'END_CHAT')).toEqual([]);
    });

    it('keeps the live conversation shown, the draft in its box, and ends nothing', async () => {
      SESSIONS = [HELP];
      bridge();
      offer();

      await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue('> a per-frame budget\n\n'));
      await act(async () => {});
      expect(sent()).toEqual([]);
      expect(invoke.mock.calls.filter(([, type]) => type === 'END_CHAT')).toEqual([]);
    });

    it('is not held back by a conversation opened ahead, which is not the newest', async () => {
      SESSIONS = [{ ...HELP, state: 'completed' }, { ...HELP, id: 'n3xt0000', created: '2026-09-30T00:00:00Z' }];
      bridge({ sessionId: 'n3xt0000', message: 'opened' });
      offer('n3xt0000');

      await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue('> a per-frame budget\n\n'));
      await act(async () => {});
      expect(screen.queryByText(ENDED_NOTICE)).not.toBeInTheDocument();
      expect(sent()).toEqual([]);
    });

    // The composer's *Make it a task with Daoris…* hands a draft that ends on its words (NEW1d-b), unlike CTX1's quote, which
    // ends on a blank line for the person's question: put above words already in the box, a blank line keeps them apart.
    it('keeps a blank line between a draft that ends on its words and the words already in the box', async () => {
      SESSIONS = [HELP];
      bridge();
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      const tree = (opening: { text: string; id: number; draft: boolean }) => (
        <QueryClientProvider client={client}>
          <Tooltip.Provider>
            <AskDaoris opening={opening} onOpened={vi.fn()} onGo={vi.fn()} onClose={vi.fn()} />
          </Tooltip.Provider>
        </QueryClientProvider>
      );
      const view = render(tree({ text: 'what I was asking', id: 1, draft: true }));
      await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue('what I was asking'));

      view.rerender(tree({ text: 'In engine:\n\nCheck the idle line', id: 2, draft: true }));

      await waitFor(() =>
        expect(screen.getByLabelText('Message')).toHaveValue('In engine:\n\nCheck the idle line\n\nwhat I was asking'));
      expect(sent()).toEqual([]);
    });
  });

  /**
   * SETUP1b: the side bar's AskDaoris is drawn again whenever its tab is, and a Quick Ask box each time it
   * opens, so a question still held by the application would be asked again by every new drawing. It
   * says when it has sent one, and the holder, told, lets it go (frontend-architecture §4b): held the way
   * the application holds it, a drawing after that sends nothing.
   */
  it('says when it has sent a question it was handed, so a later drawing does not send it again', async () => {
    bridge();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    function Holder({ shown }: { shown: boolean }) {
      const [opening, setOpening] = useState<{ text: string; id: number } | null>({ text: 'walk me through it', id: 1 });
      return shown
        ? <AskDaoris opening={opening} onOpened={() => setOpening(null)} onGo={vi.fn()} onClose={vi.fn()} />
        : null;
    }
    const view = (shown: boolean) => (
      <QueryClientProvider client={client}>
        <Tooltip.Provider><Holder shown={shown} /></Tooltip.Provider>
      </QueryClientProvider>
    );
    const sent = () => invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT').map(([, , request]) => request.payload.text);

    const { rerender } = render(view(true));
    await waitFor(() => expect(sent()).toEqual(['walk me through it']));
    rerender(view(false));
    rerender(view(true));
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 50)); });
    expect(sent()).toEqual(['walk me through it']);
  });

  /** HELP1b: where the person is goes ahead of their words, and an unchanged screen is not said twice. */
  it('hands the conversation where the person is, once for as long as the screen is the same', async () => {
    // The attended session has ENDED — what the person reads is named all the same (found looking at HELP1b).
    SESSIONS = [HELP, { ...HELP, id: 'p4rk3d00', repository: 'engine', state: 'completed', note: 'Which one?' }];
    bridge();
    show({ view: 'sessions', workspace: 'aurora' }, 'p4rk3d00');

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'what is it asking?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: {
        id: HELP.id, text: 'what is it asking?',
        preface: 'Where the person is now: the Sessions view, workspace `aurora`, attending session `p4rk3d00` in `engine`, which is completed. It says: "Which one?".',
      },
    }));

    await userEvent.type(box, 'and then?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'and then?' },
    }));
  });

  /** FRAME1i-a: the chosen item is part of where the person is, so a change of it is said again and a same one is not. */
  it('hands the conversation the item again when it changed', async () => {
    SESSIONS = [HELP];
    bridge();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const page = (id: string) => (
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <AskDaoris where={{ view: 'quests', workspace: null, item: { kind: 'quest', id } }} attending={null} onGo={vi.fn()} onClose={vi.fn()} />
        </Tooltip.Provider>
      </QueryClientProvider>
    );
    const { rerender } = render(page('abc123'));

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'what is this?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'what is this?', preface: 'Where the person is now: the Quests view, every workspace, looking at quest `abc123`.' },
    }));

    rerender(page('def456'));
    await userEvent.type(box, 'and this?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'and this?', preface: 'Where the person is now: the Quests view, every workspace, looking at quest `def456`.' },
    }));
  });

  /**
   * With HELP5: the conversation opened as the panel showed was refused, which says nothing and is not
   * asked again while the person types. The send asks for itself, and says why it cannot.
   */
  it('keeps the words in the box, with the driver\'s sentence, when no conversation can start', async () => {
    bridge({ sessionId: null, message: 'Claude Code has no account signed in.' });
    show();

    const box = await screen.findByLabelText('Message');
    await waitFor(() => expect(starts()).toBe(1));
    await userEvent.type(box, 'hello');
    expect(screen.queryByText('Claude Code has no account signed in.')).toBeNull();
    expect(starts()).toBe(1);
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText('Claude Code has no account signed in.')).toBeInTheDocument();
    expect((box as HTMLTextAreaElement).value).toBe('hello');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());
    expect(starts()).toBe(2);
  });

  it('carries on the conversation running, and starts again on new conversation', async () => {
    SESSIONS = [HELP];
    bridge();
    show();

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'and the line?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'and the line?' },
    }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'START_HELP', expect.anything());

    await userEvent.click(screen.getByRole('button', { name: 'New conversation' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'END_CHAT', { payload: { id: HELP.id } });
    // Cleared: the starters are back, and the next message opens the next conversation.
    expect(await screen.findByText(/Ask below about Daoris on this machine/)).toBeInTheDocument();
  });

  /** HELP1c: what it proposes is a card under the conversation, applied or not by the person's press. */
  it('shows what the conversation proposes, and applies or dismisses it on the person\'s press', async () => {
    SESSIONS = [HELP];
    PROPOSALS = [{
      id: 'p1a2b3c4', kind: 'setting', describe: 'Drive `engine`: a quest addressed to it starts a session on this machine.',
      terminal: 'daoris driver drive engine', why: 'the person asked for engine to be driven',
    }];
    bridge();
    show();

    const cards = await screen.findByRole('list', { name: 'what Ask Daoris proposes' });
    expect(within(cards).getByText(code('daoris driver drive engine'))).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_PROPOSALS', { payload: { session: HELP.id } });

    await userEvent.click(within(cards).getByRole('button', { name: 'Apply' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_APPLY', { payload: { id: 'p1a2b3c4' } });
    await userEvent.click(within(cards).getByRole('button', { name: 'Not now' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_DISMISS', { payload: { id: 'p1a2b3c4' } });
    PROPOSALS = [];
  });

  /**
   * ASKQUESTS1 (the Ask Daoris design §9 item 12): what the conversation published is in its own strip, with the state each one's
   * list holds now: here a quest a session has taken, named by that session's state. A row opens its record on Quests, the door a
   * go uses, and nothing else is pressed.
   */
  it('lists what the conversation published with its live state, and opens one on Quests', async () => {
    const at = '2026-10-22T09:00:00Z';
    SESSIONS = [HELP, {
      id: 's9w8v7u6', quest: 'q1a2b3c4', repository: 'engine', adapter: 'claude-code', state: 'working', kind: 'task',
      created: at, updated: at, workspace: 'default',
    }];
    QUESTS = [{
      id: 'q1a2b3c4', from: 'daoris', to: 'engine', status: 'Taken', title: 'Cap the chunk budget', body: 'Cap it.', filed: at,
      updated: at, workspace: 'default',
    }];
    ASKS = [{
      id: 'a1b2c3d4', workspace: 'default', sentence: 'cap the chunk budget', state: 'Published', tier: 'named', asked: at,
      updated: at, links: [], attachments: [], proposal: [], quests: ['q1a2b3c4'],
    }];
    PUBLISHED_HERE = [{ at, proposal: 'p1a2b3c4', kind: 'ask', workspace: 'default', ask: 'a1b2c3d4', quest: 'q1a2b3c4' }];
    bridge();
    const onGo = vi.fn();
    show(undefined, null, onGo);

    const strip = await screen.findByRole('button', { name: /Published from this conversation/ });
    await waitFor(() => expect(strip).toHaveTextContent('Published from this conversation · 1 quest · 1 taken'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_PUBLISHED', { payload: { session: HELP.id } });
    await userEvent.click(strip);
    const taken = screen.getByRole('button', { name: 'Open quest Cap the chunk budget' });
    expect(taken).toHaveTextContent('session working');
    await userEvent.click(taken);

    expect(onGo).toHaveBeenCalledWith({ view: 'quests', item: 'q1a2b3c4' });
    expect(invoke.mock.calls.map(([, type]) => type)).not.toContain('HELP_APPLY');
    PUBLISHED_HERE = [];
    QUESTS = [];
    ASKS = [];
  });

  /**
   * ASKQUESTS1: a go card's sentence says where its quest stands (since ASKDISMISS1), so the plan its Apply judges moves with a take.
   * Where the shell pushes the quests, the asks and the sessions, the card is drawn again at the push that moves a standing, never on
   * a clock and never by a pull, so its Apply finds the plan it shows rather than answering that it changed.
   */
  it('draws a go card again when a push moves where a quest stands', async () => {
    const at = '2026-10-22T09:00:00Z';
    const quest = {
      id: 'q1a2b3c4', from: 'daoris', to: 'engine', status: 'Open', title: 'Cap the chunk budget', body: 'Cap it.', filed: at,
      updated: at, workspace: 'default',
    };
    const part = (data: Record<string, unknown>) => [{ partition: 'default', seq: 1, status: 'ready', data }];
    const snapshots: Record<string, unknown> = {
      quests: { slice: 'quests', epoch: 'q1', status: 'ready', partitions: part({ [quest.id]: quest }) },
      asks: { slice: 'asks', epoch: 'k1', status: 'ready', partitions: part({}) },
      sessions: { slice: 'sessions', epoch: 's1', status: 'ready', partitions: part({ [HELP.id]: HELP }) },
    };
    SESSIONS = [HELP];
    PROPOSALS = [{
      id: 'g6o7t8o9', kind: 'go', describe: 'Open Quests → quest `#q1a2b3c4` “Cap the chunk budget”.', terminal: '', why: 'where it went',
    }];
    bridge();
    const base = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, options?: { payload?: { slice?: string } }) =>
      (type === 'STATE_SLICE' ? snapshots[options?.payload?.slice ?? ''] : base(module, type, options)));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    function Pushes() {
      useStateHub();
      useStateSlices(PUSHED_HERE);
      return null;
    }
    render(
      <QueryClientProvider client={client}>
        <Pushes />
        <Tooltip.Provider>
          <AskDaoris onGo={vi.fn()} onClose={vi.fn()} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await screen.findByRole('list', { name: 'what Ask Daoris proposes' });
    const drawn = () => invoke.mock.calls.filter(([, type]) => type === 'HELP_PROPOSALS').length;
    await waitFor(() => expect(pushedOf(client).live('quests')).toBe(true));
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 50)); });
    const before = drawn();

    act(() => {
      heard.get('STATE_PUSH')?.({
        slice: 'quests', epoch: 'q1', partition: 'default', seq: 2, status: 'ready', baseSeq: 1, removed: [],
        data: { [quest.id]: { ...quest, status: 'Taken' } },
      });
    });

    await waitFor(() => expect(drawn()).toBeGreaterThan(before));
    expect(asked.some((url) => url.startsWith('/api/quests') || url.startsWith('/api/asks'))).toBe(false);
    PROPOSALS = [];
  });

  /** ASKQUESTS1: a conversation that published nothing shows no strip, and asks no list of quests or asks for one. */
  it('shows no strip where the conversation published nothing', async () => {
    SESSIONS = [HELP];
    bridge();
    show();

    await waitFor(() => expect(invoke.mock.calls.map(([, type]) => type)).toContain('HELP_PUBLISHED'));
    expect(screen.queryByRole('button', { name: /Published from this conversation/ })).not.toBeInTheDocument();
    expect(asked.some((url) => url.startsWith('/api/quests') || url.startsWith('/api/asks'))).toBe(false);
  });

  /**
   * HELP6: a go is applied by navigating, through the starters' own door, and nothing else changes. The
   * driver settles it and names the place; the page opens it — here the setup guide at its second step.
   */
  it('takes the person to the place a go names once they press go there', async () => {
    SESSIONS = [HELP];
    PROPOSALS = [{
      id: 'g1o2t3o4', kind: 'go', describe: 'Open Settings → Get started at step 2, Ask Daoris\'s agent.', terminal: '',
      why: 'the person asked where to name its agent',
    }];
    APPLIED = {
      message: 'Applied: `#g1o2t3o4` — Open Settings → Get started at step 2, Ask Daoris\'s agent. Nothing else changed.',
      applied: true, go: { view: 'settings', domain: 'start', part: 'helper' },
    };
    bridge();
    const onGo = vi.fn();
    show(undefined, null, onGo);

    const cards = await screen.findByRole('list', { name: 'what Ask Daoris proposes' });
    await userEvent.click(within(cards).getByRole('button', { name: 'Go there' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_APPLY', { payload: { id: 'g1o2t3o4' } });
    await waitFor(() => expect(onGo).toHaveBeenCalledWith({ view: 'settings', section: 'start', anchor: 'step-helper' }));
    PROPOSALS = [];
    APPLIED = APPLIED_SETTING;
  });

  /**
   * ENTRY1f1 (D161's ENTRY1f note): a go may name the one ask that waits, judged by the driver against the machine's
   * records; the page opens Quests with it chosen, named as Quests' list names an ask.
   */
  it('takes the person to the one ask a go names', async () => {
    SESSIONS = [HELP];
    PROPOSALS = [{
      id: 'g2o3t4o5', kind: 'go', describe: 'Open Quests → ask `#a2none00` “a test ask”.', terminal: '',
      why: 'the person asked where their ask went',
    }];
    APPLIED = {
      message: 'Applied: `#g2o3t4o5` — Open Quests → ask `#a2none00` “a test ask”. Nothing else changed.',
      applied: true, go: { view: 'quests', domain: null, part: null, item: 'ask:a2none00' },
    };
    bridge();
    const onGo = vi.fn();
    show(undefined, null, onGo);

    const cards = await screen.findByRole('list', { name: 'what Ask Daoris proposes' });
    await userEvent.click(within(cards).getByRole('button', { name: 'Go there' }));

    await waitFor(() => expect(onGo).toHaveBeenCalledWith({ view: 'quests', item: 'ask:a2none00' }));
    PROPOSALS = [];
    APPLIED = APPLIED_SETTING;
  });

  /**
   * ASKDISMISS1: the block a published quest leaves in the conversation is a go card, and its dismiss only takes it away. The
   * driver settles the card and says nothing into the conversation; the page presses nothing about the quest: no Apply, no
   * words, no quest's or ask's route. A taken quest's card says it is taken, in the driver's sentence.
   */
  it('takes a go card about a quest away on its dismiss, and sends nothing about the quest', async () => {
    SESSIONS = [HELP];
    PROPOSALS = [{
      id: 'g6t7k8n9', kind: 'go', describe: 'Open Quests → quest `#q2taken0` “Stream the tiles”, taken.', terminal: '',
      why: 'the person asked where their quest went',
    }];
    bridge();
    const answers = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, request?: unknown) => {
      // As the driver settles it: a dismissed card is pending no longer.
      if (type === 'HELP_DISMISS') PROPOSALS = [];
      return answers(module, type, request);
    });
    show();

    const cards = await screen.findByRole('list', { name: 'what Ask Daoris proposes' });
    expect(within(cards).getByText(/, taken\.$/)).toBeInTheDocument();
    invoke.mockClear();
    asked.length = 0;
    await userEvent.click(within(cards).getByRole('button', { name: 'Dismiss' }));

    await waitFor(() => expect(screen.queryByRole('list', { name: 'what Ask Daoris proposes' })).not.toBeInTheDocument());
    const sent = invoke.mock.calls.map(([, type]) => type as string);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_DISMISS', { payload: { id: 'g6t7k8n9' } });
    expect(sent.filter((type) => type === 'HELP_DISMISS')).toHaveLength(1);
    expect(sent.filter((type) => type !== 'HELP_DISMISS' && /APPLY|INPUT|QUEST|ASK|WORK|DELETE|DECLINE|PAUSE|CLEAR/.test(type))).toEqual([]);
    expect(asked.filter((url) => /\/api\/(quests|asks)/.test(url))).toEqual([]);
    PROPOSALS = [];
  });

  /**
   * ENTRY1f2 (D161's ENTRY1f note): a go may name the one session that waits, judged by the driver against the machine's
   * own records; the page opens Sessions with it chosen, by its id.
   */
  it('takes the person to the one session a go names', async () => {
    SESSIONS = [HELP];
    PROPOSALS = [{
      id: 'g3o4t5o6', kind: 'go', describe: 'Open Sessions → session `s1a2b3c4` in `console-ui`, waiting on you.', terminal: '',
      why: 'the person asked which session waits on them',
    }];
    APPLIED = {
      message: 'Applied: `#g3o4t5o6` — Open Sessions → session `s1a2b3c4` in `console-ui`, waiting on you. Nothing else changed.',
      applied: true, go: { view: 'sessions', domain: null, part: null, item: 's1a2b3c4' },
    };
    bridge();
    const onGo = vi.fn();
    show(undefined, null, onGo);

    const cards = await screen.findByRole('list', { name: 'what Ask Daoris proposes' });
    await userEvent.click(within(cards).getByRole('button', { name: 'Go there' }));

    await waitFor(() => expect(onGo).toHaveBeenCalledWith({ view: 'sessions', item: 's1a2b3c4' }));
    PROPOSALS = [];
    APPLIED = APPLIED_SETTING;
  });

  /** PLUGUI1h (D119's note): a go may name an offer, as `offer:<id>`; the page opens Plugins with it chosen. */
  it('takes the person to the one offer a go names', async () => {
    SESSIONS = [HELP];
    PROPOSALS = [{
      id: 'g4o5t6o7', kind: 'go', describe: 'Open Plugins → Daoris\'s own plugin `github-pull-request`, not installed.', terminal: '',
      why: 'the person asked about pull requests',
    }];
    APPLIED = {
      message: 'Applied: `#g4o5t6o7` — Open Plugins. Nothing else changed.',
      applied: true, go: { view: 'plugins', domain: null, part: null, item: 'offer:github-pull-request' },
    };
    bridge();
    const onGo = vi.fn();
    show(undefined, null, onGo);

    const cards = await screen.findByRole('list', { name: 'what Ask Daoris proposes' });
    await userEvent.click(within(cards).getByRole('button', { name: 'Go there' }));

    await waitFor(() => expect(onGo).toHaveBeenCalledWith({ view: 'plugins', item: 'offer:github-pull-request' }));
    PROPOSALS = [];
    APPLIED = APPLIED_SETTING;
  });

  /**
   * ENTRY1d2b (D161's ENTRY1d note): a go to Add repository or Import a folder may carry the workspace the driver judged; the
   * page opens the drawer with it filled, and the folder stays the person's pick, so no path is in the go.
   */
  it('takes the person to Add repository with the workspace a go names filled', async () => {
    SESSIONS = [HELP];
    PROPOSALS = [{
      id: 'g5a6d7d8', kind: 'go', describe: 'Open Repositories → Add repository, its workspace `work` filled.', terminal: '',
      why: 'the person asked to add a repository to work',
    }];
    APPLIED = {
      message: 'Applied: `#g5a6d7d8` — Open Repositories → Add repository, its workspace `work` filled. Nothing else changed.',
      applied: true, go: { view: 'projects', domain: null, part: 'add', item: null, workspace: 'work' },
    };
    bridge();
    const onGo = vi.fn();
    show(undefined, null, onGo);

    const cards = await screen.findByRole('list', { name: 'what Ask Daoris proposes' });
    await userEvent.click(within(cards).getByRole('button', { name: 'Go there' }));

    await waitFor(() => expect(onGo).toHaveBeenCalledWith({ view: 'projects', drawer: 'add', drawerWorkspace: 'work' }));
    PROPOSALS = [];
    APPLIED = APPLIED_SETTING;
  });

  /** NEW1d-b: a go to New conversation names the repository and the words, and the door carries both to the composer. */
  it('takes the person to the composer with the repository and words a go names', async () => {
    SESSIONS = [HELP];
    PROPOSALS = [{
      id: 'n5e6w7c8', kind: 'go', describe: 'Open Sessions → New conversation in `engine`: fix the build.', terminal: '',
      why: 'the person asked to start a conversation',
    }];
    APPLIED = {
      message: 'Applied: `#n5e6w7c8` — Open New conversation. Nothing else changed.',
      applied: true, go: { view: 'sessions', domain: null, part: 'new', item: null, workspace: null, repository: 'engine', sentence: 'fix the build' },
    };
    bridge();
    const onGo = vi.fn();
    show(undefined, null, onGo);

    const cards = await screen.findByRole('list', { name: 'what Ask Daoris proposes' });
    await userEvent.click(within(cards).getByRole('button', { name: 'Go there' }));

    await waitFor(() => expect(onGo).toHaveBeenCalledWith({ view: 'sessions', compose: { repository: 'engine', sentence: 'fix the build' } }));
    PROPOSALS = [];
    APPLIED = APPLIED_SETTING;
  });

  /** HELP6: a delete is applied through the driver's Apply, like every card, and opens nothing. */
  it('deletes a record made by mistake on the person\'s press, and goes nowhere', async () => {
    SESSIONS = [HELP];
    PROPOSALS = [{
      id: 'd1e2l3e4', kind: 'delete', describe: 'Delete ask `#a2none00` “a test ask”: it became no quest, so it goes alone.',
      terminal: 'daoris-driver ask --delete a2none00', why: 'it was a test',
    }];
    APPLIED = { message: 'Applied: `#d1e2l3e4` — Deleted ask #a2none00.', applied: true, go: null, harnessAction: null };
    bridge();
    const onGo = vi.fn();
    show(undefined, null, onGo);

    const cards = await screen.findByRole('list', { name: 'what Ask Daoris proposes' });
    expect(within(cards).getByText(/cannot be undone/)).toBeInTheDocument();
    await userEvent.click(within(cards).getByRole('button', { name: 'Delete' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_APPLY', { payload: { id: 'd1e2l3e4' } }));
    expect(onGo).not.toHaveBeenCalled();
    PROPOSALS = [];
    APPLIED = APPLIED_SETTING;
  });

  /**
   * HELP6: an update the person applied is followed by the Agents screen where the application holds its
   * running action; drawn alone, with nothing holding one, the card still applies.
   */
  it('applies an agent\'s update with nothing above to follow it', async () => {
    SESSIONS = [HELP];
    PROPOSALS = [{
      id: 'u1p2d3a4', kind: 'agent', describe: 'Update `claude-code` with its own updater.',
      terminal: 'daoris agent update claude-code', why: 'the person asked for the newest',
    }];
    APPLIED = {
      message: 'Started: `#u1p2d3a4` — Update `claude-code` with its own updater.', applied: true,
      harnessAction: { harness: 'claude-code', action: 'update' },
    };
    bridge();
    show();

    const cards = await screen.findByRole('list', { name: 'what Ask Daoris proposes' });
    await userEvent.click(within(cards).getByRole('button', { name: 'Apply' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_APPLY', { payload: { id: 'u1p2d3a4' } }));
    PROPOSALS = [];
    APPLIED = APPLIED_SETTING;
  });

  /** With HELP5: the next conversation opens ahead, and the one that ended stays in front until it is spoken in. */
  it('says a conversation that ended has, and that a message starts the next', async () => {
    SESSIONS = [{ ...HELP, state: 'completed' }];
    bridge({ sessionId: 'n3xt0000', message: 'opened' });
    show();

    const panel = await screen.findByRole('complementary', { name: 'Ask Daoris' });
    await waitFor(() => expect(starts()).toBe(1));
    await act(async () => {});
    expect(await within(panel).findByText('This conversation has ended. A message starts a new one.')).toBeInTheDocument();
  });
});

/**
 * ASKHIST1: Ask Daoris's conversations, kept on this machine only. The history lists them; one the person chooses is shown in
 * the newest's place and goes on in itself with their next words where its own conversation was kept, so its knowledge
 * carries; one that cannot is offered a new conversation from its words.
 */
describe('Ask Daoris’s history', () => {
  const EARLIER = { ...HELP, id: 'e4rl1er0', state: 'completed', created: '2026-09-28T00:00:00Z', updated: '2026-09-28T00:10:00Z' };
  const row = (over: Record<string, unknown>) => ({
    session: EARLIER.id, title: 'what is a workspace?', name: null, opening: 'what is a workspace?', about: 'A circle.',
    created: EARLIER.created, last: EARLIER.updated, pinned: null, live: false, resumable: true, from: null, handed: null, found: null,
    ...over,
  });

  /** The bridge as {@link bridge} answers it, with the history's routes beside it. */
  function withHistory(rows: unknown[], started?: { sessionId: string | null; message: string; from: string }) {
    bridge({ sessionId: 'n3xt0000', message: 'opened' });
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      if (type === 'HELP_CONVERSATIONS') return { conversations: rows, cut: false };
      if (type === 'HELP_START_FROM') return started;
      // Words to an ended record are kept on it, to go on in it.
      if (type === 'SESSION_INPUT') return { sent: true, reaches: 'resume', why: null };
      return answered(module, type, ...rest);
    });
  }

  beforeEach(() => {
    SESSIONS = [EARLIER];
    HELPER = 'claude-code-acp';
    asked.length = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('keeps an account limit continuation and its earlier exchange in one conversation', async () => {
    SESSIONS = [EARLIER, HELP];
    withHistory([row({ session: HELP.id, live: true, from: EARLIER.id, handed: 'brief', parts: [EARLIER.id, HELP.id] })]);
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, body?: { payload?: { id?: string } }) => {
      if (type === 'SESSION_HISTORY') {
        const id = body?.payload?.id;
        return { session: id, events: [
          { seq: 1, kind: 'user', origin: 'person', text: id === EARLIER.id ? 'Original question' : 'Held words', at: HELP.created },
          { seq: 2, kind: 'message', text: id === EARLIER.id ? 'Earlier answer' : 'Continued answer', at: HELP.created },
          { seq: 3, kind: 'turn', stopReason: 'end_turn', at: HELP.created },
        ], earlier: false, latest: 3 };
      }
      return answered(module, type, body);
    });
    show();
    expect(await screen.findByText('Earlier answer')).toBeInTheDocument();
    expect(await screen.findByText('Continued answer')).toBeInTheDocument();
    expect(screen.getAllByLabelText('Message')).toHaveLength(1);
    await userEvent.type(screen.getByLabelText('Message'), 'Next question');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'Next question' },
    }));
  });

  it('goes on in an earlier conversation the person chose from the history, with their words', async () => {
    withHistory([row({})]);
    show();

    await userEvent.click(await screen.findByRole('button', { name: 'Back to history' }));
    const list = await screen.findByRole('region', { name: 'Conversation history' });
    await userEvent.click(within(list).getAllByRole('button', { name: /^what is a workspace\?/ })[0]);

    expect(await screen.findByText('This conversation has ended. Write to go on in it: it remembers what was said.')).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('Message'), 'and a remote?');
    const listed = () => invoke.mock.calls.filter(([, type]) => type === 'HELP_CONVERSATIONS').length;
    const before = listed();
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: EARLIER.id, text: 'and a remote?' },
    }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', { payload: expect.objectContaining({ id: 'n3xt0000' }) });
    // The word said declares the history (HELPHISTORYHUB1), so the state hub lists it again, not the conversation's own ask.
    await waitFor(() => expect(listed()).toBeGreaterThan(before));
  });

  /**
   * SESSCTX1c part B (the steward's choice: Ask Daoris follows the same rule in its own room): words to an ended conversation whose
   * resume was refused wait for the person, and *Go on with Daoris's brief* goes on in a new conversation of the room handed Daoris's
   * brief and the words, which the panel then shows.
   */
  it('goes on with Daoris’s brief in a new conversation where its words wait for the person', async () => {
    withHistory([row({})]);
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      if (type === 'SESSION_HISTORY') {
        return {
          session: EARLIER.id, earlier: false, latest: 2,
          events: [
            { seq: 1, at: '2026-09-28T00:20:00Z', kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'and a remote?' },
            { seq: 2, at: '2026-09-28T00:20:05Z', kind: 'note', text: '— It cannot go on in this session, because its conversation could not be resumed.', words: ['w1'], why: 'refused' },
          ],
        };
      }
      if (type === 'SESSION_GO_ON_BRIEF') return { sessionId: 'br1ef000', sent: true, why: null, message: 'went on' };
      return answered(module, type, ...rest);
    });
    show();

    await userEvent.click(await screen.findByRole('button', { name: "Go on with Daoris's brief" }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GO_ON_BRIEF', { payload: { id: EARLIER.id } }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_START_FROM', expect.anything());
  });

  /** The newest that ended still starts a new conversation by default, as before; *Go on in it* chooses it. */
  it('offers to go on in the newest that ended, and starts a new one unless the person chooses it', async () => {
    withHistory([row({})]);
    show();

    const press = await screen.findByRole('button', { name: 'Go on in it' });
    expect(screen.getByText('This conversation has ended. A message starts a new one.')).toBeInTheDocument();
    await userEvent.click(press);

    expect(await screen.findByText(/Write to go on in it/)).toBeInTheDocument();
    expect(screen.getByLabelText('Message')).toHaveAttribute('placeholder', expect.stringMatching(/^write to go on in this conversation/));
  });

  /**
   * LIMITRESUME1d2 (D206 §3.7): a conversation its account's limit holds says so above the box, read from its events; its *Go on now
   * with…* is Ask Daoris's own door (`ResumeWords.HelpDoor`), a new conversation from this one, never the hand-over, which refuses Ask
   * Daoris. No accounts read, the driver's own line says when the first is ready again.
   */
  it('says a limit holds it, and goes on now in a new conversation from it', async () => {
    withHistory([row({})], { sessionId: 'fr0m0000', message: 'opened', from: EARLIER.id });
    const until = new Date(Date.now() + 3 * 3600_000).toISOString();
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      if (type === 'SESSION_HISTORY') {
        return {
          session: EARLIER.id, earlier: false, latest: 3,
          events: [
            { seq: 1, at: EARLIER.updated, kind: 'user', origin: 'person', text: 'and a remote?' },
            { seq: 2, at: EARLIER.updated, kind: 'turn', stopReason: 'error' },
            {
              seq: 3, at: EARLIER.updated, kind: 'note', text: 'No account has room now.',
              parts: [{ code: 'chat.limit-waits', values: { owner: 'claude-code', until, why: 'stated' } }],
            },
          ],
        };
      }
      return answered(module, type, ...rest);
    });
    show();

    const line = await screen.findByRole('group', { name: 'Waiting for a limit' });
    expect(line).toHaveTextContent(/^No account of `claude-code` has room now; the first is ready again at /);
    expect(within(line).getByRole('button', { name: 'Stop waiting' })).toBeInTheDocument();
    await userEvent.click(within(line).getByRole('button', { name: 'Go on now with…' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_START_FROM', { payload: { id: EARLIER.id } }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', expect.anything());
  });

  /** One whose own conversation was not kept cannot go on in itself: a new one starts from its words, handed as a file. */
  it('starts a new conversation from the words of one that cannot go on in itself', async () => {
    withHistory([row({ resumable: false })], { sessionId: 'fr0m0000', message: 'opened', from: EARLIER.id });
    show();

    const press = await screen.findByRole('button', { name: 'New conversation from it' });
    SESSIONS = [EARLIER, { ...HELP, id: 'fr0m0000', created: '2026-09-29T01:00:00Z' }];
    await userEvent.click(press);

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_START_FROM', { payload: { id: EARLIER.id } }));
    expect(await screen.findByText('This conversation starts from “what is a workspace?”: its words go to Ask Daoris as a file with your first message.'))
      .toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('Message'), 'and a remote?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 'fr0m0000', text: 'and a remote?' },
    }));
  });

  /** ASKHIST1b: a title too long for the dock, a pasted URL, breaks at the panel's edge where the conversation says it. */
  it('says which conversation one started from at the dock’s edge, however long its title', async () => {
    const title = 'to complete this https://example.atlassian.net/browse/TK-2205?focusedCommentId=1234567&page=com.example.plugin.tabpanels';
    withHistory([row({ resumable: false, title, opening: title })], { sessionId: 'fr0m0000', message: 'opened', from: EARLIER.id });
    show();

    const press = await screen.findByRole('button', { name: 'New conversation from it' });
    SESSIONS = [EARLIER, { ...HELP, id: 'fr0m0000', created: '2026-09-29T01:00:00Z' }];
    await userEvent.click(press);

    expect(await screen.findByText(`This conversation starts from “${title}”: its words go to Ask Daoris as a file with your first message.`))
      .toHaveClass('wrap-anywhere');
  });

  /** A rename, a pin and a delete go through the driver's routes, the delete through Sessions' own, asked once. */
  it('renames, pins and deletes from the history through the driver', async () => {
    withHistory([row({ session: EARLIER.id })]);
    show();

    await userEvent.click(await screen.findByRole('button', { name: 'Back to history' }));
    const user = userEvent.setup();
    const menu = async () => {
      (await screen.findByRole('button', { name: 'More for what is a workspace?' })).focus();
      await user.keyboard('{Enter}');
    };

    await menu();
    await user.click(await screen.findByRole('menuitem', { name: 'Pin' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_PIN', { payload: { id: EARLIER.id, pinned: true } }));

    await menu();
    await user.click(await screen.findByRole('menuitem', { name: 'Rename…' }));
    // It starts from the title shown (ASKHIST1c).
    await user.clear(screen.getByRole('textbox', { name: 'Name' }));
    await user.type(screen.getByRole('textbox', { name: 'Name' }), 'Workspaces');
    await user.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_RENAME', { payload: { id: EARLIER.id, name: 'Workspaces' } }));

    await menu();
    await user.click(await screen.findByRole('menuitem', { name: 'Delete…' }));
    const listed = () => invoke.mock.calls.filter(([, type]) => type === 'HELP_CONVERSATIONS').length;
    const before = listed();
    await user.click(await screen.findByRole('button', { name: 'Delete conversation' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DELETE', { payload: { id: EARLIER.id } }));
    // The delete declares the history (HELPHISTORYHUB1): the state hub lists it again, the conversation asks nothing by hand.
    await waitFor(() => expect(listed()).toBeGreaterThan(before));
  });
});

/**
 * ASKHIST1c: the history and the open conversation, made right after the owner looked at the window. Each conversation keeps
 * its own draft, words and files, and a new one its own; one the person chose that cannot go on takes no words until they
 * choose a new conversation from it, or a blank one, and one being checked takes none either; the keys go from the list to a
 * conversation and back to the row it was opened from; and the list says what it is doing.
 */
describe.each(['en', 'zh'])('Ask Daoris’s history and open conversation in %s', (language) => {
  const ANSWERED = { ...HELP, state: 'completed' };
  const A = { ...ANSWERED, id: 'a1a1a1a1', created: '2026-09-28T00:00:00Z', updated: '2026-09-28T00:10:00Z' };
  const B = { ...ANSWERED, id: 'b2b2b2b2', created: '2026-09-27T00:00:00Z', updated: '2026-09-27T00:10:00Z' };
  const row = (session: string, title: string, over: Record<string, unknown> = {}) => ({
    session, title, name: null, opening: title, about: 'A circle.', created: '2026-09-27T00:00:00Z', last: new Date().toISOString(),
    pinned: null, live: false, resumable: true, from: null, handed: null, found: null, ...over,
  });
  const ROWS = [row(A.id, 'about A?'), row(B.id, 'about B?')];

  /** The bridge as {@link bridge} answers it, with the history's routes answering by the words searched and the page's offset. */
  function historyBridge(
    listed: (words: string | undefined, offset: number | undefined) => unknown,
    started?: { sessionId: string; message: string; from: string },
  ) {
    bridge({ sessionId: 'n3xt0000', message: 'opened' });
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, body?: { payload?: { q?: string; offset?: number } }, ...rest: unknown[]) => {
      if (type === 'HELP_CONVERSATIONS') return listed(body?.payload?.q, body?.payload?.offset);
      if (type === 'HELP_START_FROM') return started;
      if (type === 'SESSION_INPUT') return { sent: true, reaches: 'resume', why: null };
      return answered(module, type, body, ...rest);
    });
  }

  const box = () => screen.queryByLabelText(i18n.t('work.composer.label'));
  const back = () => screen.findByRole('button', { name: i18n.t('help.history.back') });
  /** A row's door in the history: the button that leads with its title. */
  const door = async (title: string) => (await screen.findAllByRole('button', { name: (name) => name.startsWith(title) }))[0]!;
  /** A conversation opened from the history, as a person opens it. */
  async function open(title: string) {
    await userEvent.click(await back());
    await userEvent.click(await door(title));
    await screen.findByRole('heading', { name: title });
  }

  beforeEach(async () => {
    await i18n.changeLanguage(language);
    SESSIONS = [A, B];
    HELPER = 'claude-code-acp';
    asked.length = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(async () => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
    await i18n.changeLanguage('en');
  });

  it('keeps each conversation’s words and files, and a new one’s apart, across the history', async () => {
    historyBridge(() => ({ conversations: ROWS, cut: false }));
    show();

    await open('about A?');
    await userEvent.type(box()!, 'more on A');
    await userEvent.upload(screen.getByLabelText(i18n.t('carry.choose')), new File(['exit 3'], 'run.log'));

    await open('about B?');
    expect(box()).toHaveValue('');
    expect(screen.queryByText('run.log')).toBeNull();
    await userEvent.type(box()!, 'more on B');

    await open('about A?');
    expect(box()).toHaveValue('more on A');
    expect(screen.getByText('run.log')).toBeInTheDocument();

    // A blank new conversation's box is its own, and takes the focus.
    const head = screen.getByRole('heading', { name: 'about A?' }).closest('header')!;
    await userEvent.click(within(head).getByRole('button', { name: i18n.t('help.new') }));
    await waitFor(() => expect(box()).toHaveFocus());
    expect(box()).toHaveValue('');
    expect(screen.getByRole('heading', { name: i18n.t('help.new') })).toBeInTheDocument();

    await open('about B?');
    expect(box()).toHaveValue('more on B');

    // A new conversation from an older one is blank too, never the newest that ended in its place.
    const older = screen.getByRole('heading', { name: 'about B?' }).closest('header')!;
    await userEvent.click(within(older).getByRole('button', { name: i18n.t('help.new') }));
    expect(await screen.findByRole('heading', { name: i18n.t('help.new') })).toBeInTheDocument();
    expect(box()).toHaveValue('');
  });

  it('takes no words under one it chose that cannot go on, offering a new conversation from it first and a blank one beside', async () => {
    historyBridge(() => ({ conversations: [row(A.id, 'about A?', { resumable: false }), row(B.id, 'about B?')], cut: false }),
      { sessionId: 'fr0m0000', message: 'opened', from: A.id });
    show();

    await open('about A?');
    expect(box()).toBeNull();
    expect(screen.getByText(i18n.t('help.endedAnew'))).toBeInTheDocument();
    const offer = screen.getByRole('button', { name: i18n.t('help.history.startFrom') });
    expect(offer).toHaveClass('bg-accent');
    expect(within(offer.parentElement!).getByRole('button', { name: i18n.t('help.new') })).toBeInTheDocument();
    // No row says it starts anew: opening one only reads it.
    expect(screen.queryByText(/starts anew|将开始新对话/)).toBeNull();

    SESSIONS = [A, B, { ...HELP, id: 'fr0m0000', created: '2026-09-29T01:00:00Z' }];
    await userEvent.click(offer);
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_START_FROM', { payload: { id: A.id } }));
    await waitFor(() => expect(box()).toHaveFocus());
  });

  // ASKHIST1d2: a search reaches every conversation, past the pages the whole list has read, and its row says whether one
  // can go on as the list's does (D158's ASKHIST1d1 note).
  it('knows from the search that found it whether a conversation can go on, while the whole list has not answered', async () => {
    historyBridge((words) => (words
      ? { conversations: [row(A.id, 'about A?', { found: 'about A?', resumable: false })], total: 1, next: null }
      : new Promise(() => {})));
    show();

    await userEvent.click(await back());
    await userEvent.type(screen.getByRole('searchbox', { name: i18n.t('help.history.search.label') }), 'about');
    await userEvent.click(await door('about A?'));

    expect(await screen.findByText(i18n.t('help.endedAnew'))).toBeInTheDocument();
    expect(screen.queryByText(i18n.t('help.checking'))).toBeNull();
    expect(box()).toBeNull();
  });

  it('keeps knowing that one a search found past the list’s pages goes on, once the search is cleared', async () => {
    historyBridge((words) => (words
      ? { conversations: [row(A.id, 'about A?', { found: 'about A?' })], total: 1, next: null }
      : { conversations: [row(B.id, 'about B?')], total: 2, next: 1 }));
    show();

    await userEvent.click(await back());
    const search = screen.getByRole('searchbox', { name: i18n.t('help.history.search.label') });
    await userEvent.type(search, 'about');
    await userEvent.click(await door('about A?'));
    await screen.findByRole('heading', { name: 'about A?' });
    expect(box()).not.toBeNull();

    // Back in the list, the search cleared: the whole list's one page does not hold A, and A still goes on.
    await userEvent.click(await back());
    const cleared = screen.getByRole('searchbox', { name: i18n.t('help.history.search.label') });
    await userEvent.clear(cleared);
    expect(await door('about B?')).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await screen.findByRole('heading', { name: 'about A?' });
    expect(box()).not.toBeNull();
    expect(screen.queryByText(i18n.t('help.endedAnew'))).toBeNull();
  });

  it('says it is checking whether a conversation can go on while the whole list has not answered, and takes no words', async () => {
    // The whole list has not answered; a search has, and is then cleared, so no list read names the one shown.
    historyBridge((words) => (words
      ? { conversations: [row(A.id, 'about A?', { found: 'about A?' })], total: 1, next: null }
      : new Promise(() => {})));
    show();

    await userEvent.click(await back());
    await userEvent.type(screen.getByRole('searchbox', { name: i18n.t('help.history.search.label') }), 'about');
    await userEvent.click(await door('about A?'));
    await userEvent.click(await back());
    await userEvent.clear(screen.getByRole('searchbox', { name: i18n.t('help.history.search.label') }));
    await userEvent.keyboard('{Escape}');

    // Called by the row it was opened from meanwhile.
    await screen.findByRole('heading', { name: 'about A?' });
    expect(await screen.findByRole('status')).toHaveTextContent(i18n.t('help.checking'));
    expect(box()).toBeNull();
  });

  it('pages through every conversation with the driver’s next, and asks again from the start after a change', async () => {
    historyBridge((_words, offset) => (offset
      ? { conversations: [row(B.id, 'about B?')], cut: false, total: 2, next: null }
      : { conversations: [row(A.id, 'about A?')], cut: true, total: 2, next: 1 }));
    show();

    await userEvent.click(await back());
    expect(await door('about A?')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: (name) => name.startsWith('about B?') })).toBeNull();
    expect(screen.getByText(i18n.t('help.history.listed', { shown: 1, total: 2 }))).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: i18n.t('help.history.more') }));
    // The next page, and the reader taken to its first row; the last page offers no more.
    await waitFor(async () => expect(await door('about B?')).toHaveFocus());
    expect(screen.queryByRole('button', { name: i18n.t('help.history.more') })).toBeNull();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_CONVERSATIONS', { payload: { offset: 1 } });

    // A pin moves a conversation in the order: the list is asked again from its start, each page from the one before.
    const pages = () => invoke.mock.calls.filter((call) => call[1] === 'HELP_CONVERSATIONS').map((call) => (call[2] as { payload: unknown }).payload);
    const before = pages().length;
    (screen.getByRole('button', { name: i18n.t('help.history.menu', { title: 'about A?' }) })).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('menuitem', { name: i18n.t('help.history.pin') }));
    await waitFor(() => expect(pages().slice(before)).toEqual([{}, { offset: 1 }]));
    expect(await door('about B?')).toBeInTheDocument();
    // Each conversation once, keyed by its session.
    const list = screen.getByRole('region', { name: i18n.t('help.history.title') });
    expect(list.querySelectorAll(`[data-session="${A.id}"]`)).toHaveLength(1);
    expect(list.querySelectorAll(`[data-session="${B.id}"]`)).toHaveLength(1);
  });

  it('goes from the list into a conversation and back to the row it was opened from, by the keys alone', async () => {
    historyBridge(() => ({ conversations: ROWS, cut: false }));
    show();

    (await back()).focus();
    await userEvent.keyboard('{Enter}');
    // The list opens on the conversation shown, the newest.
    await waitFor(() => expect(screen.getAllByRole('button', { name: (name) => name.startsWith('about A?') })[0]).toHaveFocus());

    await userEvent.keyboard('{End}');
    expect(await door('about B?')).toHaveFocus();
    await userEvent.keyboard('{Enter}');
    await waitFor(() => expect(screen.getByRole('heading', { name: 'about B?' })).toHaveFocus());

    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.getAllByRole('button', { name: (name) => name.startsWith('about B?') })[0]).toHaveFocus());

    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.getByRole('heading', { name: 'about B?' })).toHaveFocus());
  });

  it('says a list it could not read and tries again, and keeps the rows while a search answers', async () => {
    let failing = true;
    let searched: (answer: unknown) => void = () => {};
    historyBridge((words) => {
      if (words) return new Promise((resolve) => { searched = resolve; });
      if (failing) throw new Error('the driver is not running.');
      return { conversations: ROWS, cut: false };
    });
    show();

    await userEvent.click(await back());
    expect(await screen.findByText(i18n.t('help.history.failed'))).toBeInTheDocument();
    expect(screen.queryByText(i18n.t('help.history.emptyHeadline'))).toBeNull();
    failing = false;
    await userEvent.click(screen.getByRole('button', { name: i18n.t('help.history.retry') }));
    expect(await door('about B?')).toBeInTheDocument();

    // One letter is too few for the driver (one Han character is not): said, and the whole list kept.
    const search = screen.getByRole('searchbox', { name: i18n.t('help.history.search.label') });
    await userEvent.type(search, 'a');
    expect(screen.getByText(i18n.t('help.history.short'))).toBeInTheDocument();
    expect(await door('about B?')).toBeInTheDocument();

    // A search on its way keeps the last rows, dimmed, never a blank list.
    await userEvent.type(search, 'b');
    expect(await door('about B?')).toBeInTheDocument();
    expect((await door('about B?')).closest('.overflow-y-auto')).toHaveClass('opacity-60');
    await act(async () => { searched({ conversations: [row(B.id, 'about B?', { found: 'about B?' })], cut: false }); });
    await waitFor(() => expect(screen.queryByRole('button', { name: (name) => name.startsWith('about A?') })).toBeNull());
  });
});

describe('Ask Daoris, with no agent named', () => {
  beforeEach(() => {
    SESSIONS = [];
    HELPER = null;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('offers its starters and no message box, and opens no conversation', async () => {
    bridge();
    show();

    expect(await screen.findByText(/Name an agent under AI features/)).toBeInTheDocument();
    expect(screen.queryByLabelText('Message')).toBeNull();
    await act(async () => {});
    expect(starts()).toBe(0);
  });
});

/**
 * HELPMODE1b (D183 points 2-4; the modes design §1, §5, §6): Ask Daoris's access on its panel. The chip beside the box shows the mode
 * the conversation in view runs under, its record's, and a switch that waits for its next turn (ACCESSSWAP1); restricted is set at
 * once and full access asked once; *What it has* reads what its start handed, with the door that grants each thing it lacks.
 */
describe('Ask Daoris’s access and what it has', () => {
  let MODE = 'restricted';
  // The conversations the driver says run under another access than the one chosen (ACCESSSWAP1).
  let SWAPPING: string[] = [];
  let ROWS: unknown[] = [];
  let WIRED: unknown = { session: HELP.id, recorded: false, receipts: [] };
  let PLUGINS: unknown = { folder: 'plugins', plugins: [] };
  const row = (over: Record<string, unknown>) => ({
    session: HELP.id, title: 'what is a workspace?', name: null, opening: 'what is a workspace?', about: null,
    created: HELP.created, last: HELP.updated, pinned: null, live: true, mode: 'restricted', resumable: true, from: null,
    handed: null, found: null, ...over,
  });
  const RECEIPT = {
    at: '2026-10-19T10:00:00+00:00', start: 'new', mode: 'restricted', harness: 'claude-code-acp', version: '0.9.1', account: null,
    door: 'acp', posture: 'default', permissionMode: null, folder: 'C:/daoris/help/room', tools: [], skills: [],
    servers: [{ name: 'browser', plugin: 'browser', state: 'withheld', tools: null, why: 'not-approved' }],
    rules: { allow: ['Read'], ask: [], deny: [] }, guarded: true, checkouts: [],
    lacks: [
      { kind: 'server', name: 'browser', door: 'plugins', why: 'not-approved', plugin: 'browser' },
      { kind: 'server', name: 'deploy', door: 'mode', why: 'not-for-help', plugin: 'deploy' },
      { kind: 'shell', name: 'shell', door: 'mode', why: 'restricted', plugin: null },
    ],
  };
  const STATE = () => ({
    drivable: ['engine'], holds: [], trees: [], helperAdapter: HELPER ?? '', helpMode: MODE, helpSwapping: SWAPPING,
  });

  /** The bridge as {@link bridge} answers it, with the mode in the driver's state, its press, the history, the receipts and the plugins. */
  function access(start?: { sessionId: string | null; message: string }) {
    bridge(start);
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: { mode?: string } }) => {
      if (type === 'STATE') return STATE();
      if (type === 'SET_HELP_MODE') {
        MODE = request?.payload?.mode ?? MODE;
        return STATE();
      }
      if (type === 'HELP_CONVERSATIONS') return { conversations: ROWS, total: ROWS.length, next: null };
      if (type === 'HELP_WIRED') return WIRED;
      if (type === 'PLUGINS') return PLUGINS;
      return answered(module, type, request);
    });
  }

  const chip = (says: string) => screen.findByRole('button', { name: `Ask Daoris’s access: ${says}` });
  /** The chip's menu, opened by the keyboard as Radix opens it in a page with no pointer, and a mode picked. */
  async function pick(option: string) {
    screen.getByRole('button', { name: /^Ask Daoris’s access:/ }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('menuitemradio', { name: option }));
  }
  const pressed = (mode: string) => invoke.mock.calls.some(([, type, request]) =>
    type === 'SET_HELP_MODE' && (request as { payload?: { mode?: string } }).payload?.mode === mode);

  beforeEach(() => {
    MODE = 'restricted';
    SWAPPING = [];
    ROWS = [];
    WIRED = { session: HELP.id, recorded: false, receipts: [] };
    PLUGINS = { folder: 'plugins', plugins: [] };
    SESSIONS = [];
    HELPER = 'claude-code-acp';
    asked.length = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('shows the mode its conversation runs under, from its record, and a switch that waits for its next turn', async () => {
    SESSIONS = [HELP];
    ROWS = [row({ mode: 'restricted' })];
    MODE = 'full';
    access();
    show();

    expect(await chip('Restricted · full access next turn')).toBeInTheDocument();
  });

  /**
   * ACCESSSWAP1 (D183 point 2): the driver ends a conversation whose access changed at its turn's end, and its next words resume the
   * same conversation under the new access (D131). The panel keeps it in view as it ends, so those words go on in it, never in a new
   * conversation opened beside it.
   */
  it('keeps a conversation ended for an access change in view, and its next words go on in it', async () => {
    SESSIONS = [HELP];
    ROWS = [row({ mode: 'restricted' })];
    MODE = 'full';
    SWAPPING = [HELP.id];
    access({ sessionId: 'n3xt0000', message: 'opened' });
    show();
    await chip('Restricted · full access next turn');

    // Its turn ended, and the driver ended its agent: pending no more.
    SESSIONS = [{ ...HELP, state: 'completed' }];
    ROWS = [row({ live: false, mode: 'restricted' })];
    SWAPPING = [];
    act(() => heard.get('STATE_CHANGED')?.({ scopes: ['sessions', 'driver', 'help'] }));

    expect(await screen.findByText('This conversation has ended. Write to go on in it: it remembers what was said.')).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('Message'), 'and now?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'and now?' },
    }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', { payload: expect.objectContaining({ id: 'n3xt0000' }) });
  });

  it('starts a new conversation after one that ended for no access change, as before', async () => {
    SESSIONS = [HELP];
    ROWS = [row({ mode: 'restricted' })];
    access({ sessionId: 'n3xt0000', message: 'opened' });
    show();
    await chip('Restricted');

    SESSIONS = [{ ...HELP, state: 'completed' }];
    ROWS = [row({ live: false, mode: 'restricted' })];
    act(() => heard.get('STATE_CHANGED')?.({ scopes: ['sessions', 'driver', 'help'] }));

    expect(await screen.findByText('This conversation has ended. A message starts a new one.')).toBeInTheDocument();
  });

  it('shows the mode chosen before any start, and says what full access does above the box', async () => {
    MODE = 'full';
    access();
    show();

    expect(await chip('Full access')).toBeInTheDocument();
    expect(screen.getByText(/runs on claude-code-acp with full access: it runs commands and edits files/)).toBeInTheDocument();
  });

  it('offers no choice on a shell older than the mode', async () => {
    SESSIONS = [HELP];
    bridge();
    show();

    expect(await screen.findByLabelText('Message')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Ask Daoris’s access:/ })).toBeNull();
  });

  it('sets restricted at once from the chip', async () => {
    SESSIONS = [HELP];
    ROWS = [row({ mode: 'full' })];
    MODE = 'full';
    access();
    show();

    await chip('Full access');
    await pick('Restricted — reads and proposes');

    await waitFor(() => expect(pressed('restricted')).toBe(true));
    expect(await chip('Full access · restricted next turn')).toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Enable full access?' })).toBeNull();
  });

  it('asks once before full access, naming the servers it would add, and sets it on Enable', async () => {
    SESSIONS = [HELP];
    ROWS = [row({ mode: 'restricted' })];
    WIRED = { session: HELP.id, recorded: true, receipts: [RECEIPT] };
    PLUGINS = {
      folder: 'plugins',
      plugins: [{
        id: 'tracker', name: 'Tracker', version: '1.0.0', description: '', enabled: true, problem: null, harnesses: [], points: [],
        running: true, folder: 'f', data: 'd', helpServers: [{ name: 'tracker', allow: ['list'], state: 'waits' }],
      }],
    };
    access();
    show();

    await chip('Restricted');
    await pick('Full access — acts itself');

    const asks = await screen.findByRole('group', { name: 'Enable full access?' });
    expect(await within(asks).findByText('tracker')).toBeInTheDocument();
    expect(within(asks).getByText('browser')).toBeInTheDocument();
    expect(within(asks).getByText('deploy')).toBeInTheDocument();
    expect(pressed('full')).toBe(false);

    await userEvent.click(within(asks).getByRole('button', { name: 'Enable full access' }));
    await waitFor(() => expect(pressed('full')).toBe(true));
    expect(await chip('Restricted · full access next turn')).toBeInTheDocument();
  });

  // ACCESSASK1 (the UI/UX design R3, §3.4): full access's refusal is said inside its ask, open to be pressed again, and a switch
  // back refused under the chip in the composer bar; neither in the composer's own line.
  it('says a refusal where it was pressed: inside full access’s ask, and under the chip for a switch back', async () => {
    SESSIONS = [HELP];
    ROWS = [row({ mode: 'restricted' })];
    access();
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      if (type === 'SET_HELP_MODE') throw new Error('The driver could not write driver.json.');
      return answered(module, type, ...rest);
    });
    show();

    await chip('Restricted');
    await pick('Full access — acts itself');
    const asks = await screen.findByRole('group', { name: 'Enable full access?' });
    await userEvent.click(within(asks).getByRole('button', { name: 'Enable full access' }));
    expect(await within(asks).findByRole('alert')).toHaveTextContent('The driver could not write driver.json.');
    expect(within(asks).getByRole('button', { name: 'Enable full access' })).toBeEnabled();
    expect(screen.getAllByRole('alert')).toHaveLength(1);

    cleanup();
    MODE = 'full';
    ROWS = [row({ mode: 'full' })];
    show();
    await chip('Full access');
    await pick('Restricted — reads and proposes');
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('The driver could not write driver.json.');
    // Under the chip, in the bar that holds it.
    expect(alert.parentElement).toContainElement(screen.getByRole('button', { name: /^Ask Daoris’s access:/ }));
  });

  it('ends the conversation opened ahead and not yet spoken in, so the next opens under the mode chosen', async () => {
    access();
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      // The ledger holds the record from the moment it opens, and ends it at the driver's word.
      if (type === 'START_HELP') SESSIONS = [HELP];
      if (type === 'END_CHAT') SESSIONS = [{ ...HELP, state: 'completed' }];
      return answered(module, type, ...rest);
    });
    show();

    await waitFor(() => expect(starts()).toBe(1));
    await chip('Restricted');
    await pick('Full access — acts itself');
    await userEvent.click(await screen.findByRole('button', { name: 'Enable full access' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'END_CHAT', { payload: { id: HELP.id } }));
    await waitFor(() => expect(starts()).toBe(2));
  });

  it('opens What it has for the conversation in view, each lack with its door', async () => {
    SESSIONS = [HELP];
    ROWS = [row({ mode: 'restricted' })];
    WIRED = { session: HELP.id, recorded: true, receipts: [RECEIPT] };
    const onGo = vi.fn();
    access();
    show(undefined, null, onGo);

    await userEvent.click(await screen.findByRole('button', { name: 'What it has' }));
    const has = await screen.findByRole('region', { name: 'What it has' });
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_WIRED', { payload: { session: HELP.id } });
    expect(await within(has).findByText('C:/daoris/help/room')).toBeInTheDocument();

    await userEvent.click(within(has).getByRole('button', { name: 'Open Plugins' }));
    expect(onGo).toHaveBeenCalledWith({ view: 'plugins', item: 'browser' });
    // The mode's door opens the chip's menu.
    await userEvent.click(within(has).getAllByRole('button', { name: 'Choose full access' })[0]!);
    expect(await screen.findByRole('menuitemradio', { name: 'Full access — acts itself' })).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');

    await userEvent.click(within(has).getByRole('button', { name: 'Back to the conversation' }));
    expect(screen.queryByRole('region', { name: 'What it has' })).toBeNull();
  });

  it('shows a receipt recorded while What it has is open, with no reopening (HELPWIREDLIVE1)', async () => {
    SESSIONS = [HELP];
    ROWS = [row({ mode: 'restricted' })];
    WIRED = { session: HELP.id, recorded: true, receipts: [RECEIPT] };
    access();
    show();

    await userEvent.click(await screen.findByRole('button', { name: 'What it has' }));
    const has = await screen.findByRole('region', { name: 'What it has' });
    expect(await within(has).findByText('C:/daoris/help/room')).toBeInTheDocument();

    // A resume records a second receipt; the history watch tells the page the help scope moved.
    WIRED = {
      session: HELP.id, recorded: true,
      receipts: [{ ...RECEIPT, at: '2026-10-19T11:00:00+00:00', start: 'resume', folder: 'C:/daoris/help/room-two' }, RECEIPT],
    };
    act(() => heard.get('STATE_CHANGED')?.({ scopes: ['help'] }));

    expect(await within(has).findByText('C:/daoris/help/room-two')).toBeInTheDocument();
  });

  it('says a conversation from before as not recorded', async () => {
    SESSIONS = [HELP];
    ROWS = [row({})];
    access();
    show();

    await userEvent.click(await screen.findByRole('button', { name: 'What it has' }));
    expect(await screen.findByText(/^Not recorded: this conversation started before/)).toBeInTheDocument();
  });
});
