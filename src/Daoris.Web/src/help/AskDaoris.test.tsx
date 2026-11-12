import { StrictMode, useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// Ask Daoris's conversation, with the bridge mocked as present — the panel exists only in
// the shell, since most of what it reads is this machine's.

const { invoke, notifyReady, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  heard: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => {
  const { publishing } = await import('../test/publishing');
  const through = publishing(invoke, (type, payload) => heard.get(type)?.(payload));
  return {
    ...(await import('../test/sliceShell')).mockedShell(invoke, { notifyReady, through }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      if (module === 'DAORIS') heard.set(type, handler);
    },
  };
});

import i18n from '../locales/i18n';
import { keys } from '../data/queries';
import { useStateHub } from '../bridge/state/stateHub';
import { moved } from '../test/sliceShell';
import { AskDaoris } from './AskDaoris';
import type { HelpWhere } from './where';
import { registerOpenDraft } from './openDraft';
import { fingerprintOf } from '../workflow/draftFingerprint';

const HELP = {
  id: 'h1e1p000', quest: null, repository: 'daoris:help', door: 'claude-code-acp', state: 'working',
  kind: 'chat', created: '2026-09-29T00:00:00Z', updated: '2026-09-29T00:01:00Z', workspace: 'default',
};

let SESSIONS: unknown[] = [];
let HELPER: string | null = 'claude-code-acp';
/** What the conversation published, by id, and the quests and asks the service lists. */
let PUBLISHED_HERE: unknown[] = [];
let QUESTS: unknown[] = [];
let ASKS: unknown[] = [];
function respond(url: string): Response {
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
      case 'STATE': return { drivable: ['engine'], holds: [], trees: [], helperAgent: HELPER ?? '' };
      case 'HARNESSES': return { harnesses: [] };
      case 'LINES': return { lines: [], landings: [] };
      case 'START_HELP': return start;
      case 'SESSION_INPUT': return { sent: true };
      case 'SESSION_HISTORY': return { session: HELP.id, events: [], earlier: false, latest: 0 };
      case 'SESSION_QUEUE': return { session: HELP.id, queued: [], taking: false };
      case 'END_CHAT': return { ended: true };
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
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'START_HELP', { payload: {} });
    // Its conversations are read from the sessions the shell pushes, every workspace's, and the panel keeps the window's own.
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STATE_SLICE', { payload: { slice: 'sessions' } });
  });

  /** Words waiting behind Ask Daoris's turn can be taken back out by their id. */
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
   * With none running, a message first opens the conversation, which takes seconds (the room is
   * written, the agent spawned). The person's words showed nowhere meanwhile, and read as lost. They are
   * shown at once, as opening Ask Daoris, and stay shown until the driver's own queue answers for the
   * conversation — never gone while the session list catches up, and never shown twice. The send opens
   * it here: the one opened as the panel shows is refused, silently.
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
    // Once in the conversation, besides its head, which calls it by them.
    await waitFor(() => expect(screen.getAllByText(words, { ignore: 'script, style, h3' })).toHaveLength(1));
    expect(screen.getByRole('heading', { name: words })).toBeInTheDocument();
    expect(screen.getByText('opening Ask Daoris…')).toBeInTheDocument();
  });

  /**
   * The driver answers the press once the conversation is placed, `starting`, its record still `queued` while the
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
   * On a loaded machine the page stopped waiting on Ask Daoris's start (the bridge's TIMEOUT) and the person's
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
   * Words to the conversation running show the moment they are sent, as sending. A send the page stopped waiting
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
   * Words whose send timed out to a conversation that then ended without them never arrived: they go back in the
   * box, said so, for the person to send again.
   */
  it('gives words back once their conversation ended without them', async () => {
    const words = 'are you there?';
    SESSIONS = [HELP];
    // Once it ended, the next opens ahead, under an id of its own.
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
   * The agent's spawn and its protocol session took seconds after the person pressed send. The
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
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    // Known before the first render, as on a page that has read them already: the panel is idle as it
    // mounts, which is when the double-mount runs the effect twice.
    client.setQueryData(keys.driver, { drivable: ['engine'], holds: [], trees: [], helperAgent: HELPER });
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
    // The list, pushed once it opened, holds it; the panel still shows its starters.
    await waitFor(() => expect((client.getQueryData(keys.sessions(null, true, null)) as { id: string }[] | undefined)?.map((row) => row.id)).toContain(HELP.id));
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
   * Words sent while the conversation opened ahead is still opening show at once, as
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
   * The side bar and Quick Ask hold one conversation. The one opened ahead is spoken in from either
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
    client.setQueryData(keys.driver, { drivable: ['engine'], holds: [], trees: [], helperAgent: HELPER });
    client.setQueryData(keys.sessions(HELP.repository, true, null), []);
    // The window's hub hears what each opening published: the sessions it opened one among.
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

    // Settled all the way, the list its answer moved pushed included, before the panels are read again.
    await act(async () => { late({ sessionId: HELP.id, message: 'opened' }); });
    await act(() => moved());
    await act(() => new Promise((settled) => { setTimeout(settled, 50); }));
    expect(screen.getAllByRole('button', { name: 'New conversation' })).toHaveLength(2);
  });

  /**
   * A conversation that runs is carried on, so none is opened beside it. *New conversation*
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
   * A question asked from the palette arrives already asked — sent once, as a typed one is,
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
   * *Ask Daoris about it* hands its words as a draft, quoted in the box for the person's question,
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
   * A draft that will start a new conversation shows one. With the newest ended, the panel clears to a blank one
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

    // The composer's *Make it a task with Daoris…* hands a draft that ends on its words, unlike a quote, which
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
   * The side bar's AskDaoris is drawn again whenever its tab is, and a Quick Ask box each time it
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

  /** Where the person is goes ahead of their words, and an unchanged screen is not said twice. */
  it('hands the conversation where the person is, once for as long as the screen is the same', async () => {
    // The attended session has ENDED — what the person reads is named all the same.
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

  /**
   * The workflow draft open in the editor goes beside the words with its fingerprint, frozen with them, where that moved since
   * the conversation is told it, and never again unchanged; where the person is says a draft is open.
   */
  it('hands the conversation the open draft beside the words, and not again while it is unchanged', async () => {
    SESSIONS = [HELP];
    bridge();
    const graph = { schemaVersion: 2, entries: ['work'], end: 'work', nodes: [{ id: 'work', kind: 'claimed', version: 1, reach: 'observed' }], wires: [] };
    const draft = { place: 'engine', graph, workflow: 'release-flow', base: 2 };
    const unregister = [registerOpenDraft(draft)];
    try {
      show({ view: 'projects', workspace: null });
      const box = await screen.findByLabelText('Message');
      const sentDraft = () => invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT').map(([, , request]) => request.payload.draft);

      await userEvent.type(box, 'add a release');
      await userEvent.click(screen.getByRole('button', { name: 'Send' }));
      await waitFor(() => expect(sentDraft()).toEqual([{ ...draft, fingerprint: fingerprintOf(graph) }]));
      const first = invoke.mock.calls.find(([, type]) => type === 'SESSION_INPUT')![2].payload;
      expect(first.preface).toContain('editing a workflow draft of `release-flow` from v2 for `engine`, not saved: 1 steps;');

      await userEvent.type(box, 'and then?');
      await userEvent.click(screen.getByRole('button', { name: 'Send' }));
      await waitFor(() => expect(sentDraft()).toEqual([expect.objectContaining({ place: 'engine' }), undefined]));

      unregister.push(registerOpenDraft({ ...draft, graph: { ...graph, end: 'line' } }));
      await userEvent.type(box, 'now?');
      await userEvent.click(screen.getByRole('button', { name: 'Send' }));
      await waitFor(() => expect(sentDraft()).toHaveLength(3));
      expect(sentDraft()[2].fingerprint).toBe(fingerprintOf({ ...graph, end: 'line' }));
    } finally {
      unregister.reverse().forEach((each) => each());
    }
  });

  /** The chosen item is part of where the person is, so a change of it is said again and a same one is not. */
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
   * The conversation opened as the panel shows is refused, which says nothing and is not
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

  /**
   * *New conversation* finishes the one running, which winds up before it ends; the next start is a conversation of
   * its own beside it, so the new conversation's first words go to it at once, never to the one winding up.
   */
  it('takes a new conversation’s first words into it, never the one New conversation finished while it winds up', async () => {
    const words = 'a question of its own';
    const next = { ...HELP, id: 'n3wc0000', state: 'queued', created: '2026-09-29T00:05:00Z' };
    SESSIONS = [HELP];
    bridge();
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      if (type === 'START_HELP') {
        SESSIONS = [HELP, next];
        return { sessionId: next.id, message: 'Ask Daoris `n3wc0000` is starting.', starting: true };
      }
      if (type === 'SESSION_QUEUE') return { session: next.id, queued: [{ text: words, files: [] }], taking: false, opening: true };
      return answered(module, type, ...rest);
    });
    show();

    await userEvent.click(await screen.findByRole('button', { name: 'New conversation' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'END_CHAT', { payload: { id: HELP.id } }));
    await userEvent.type(await screen.findByLabelText('Message'), words);
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    await waitFor(() => expect(starts()).toBe(1));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', { payload: { id: next.id, text: words } }));
    expect(invoke.mock.calls.filter(([, type, body]) => type === 'SESSION_INPUT'
      && (body as { payload: { id: string } }).payload.id === HELP.id)).toEqual([]);
    expect(screen.getByLabelText('Message')).toHaveValue('');
  });

  /**
   * First words handed to a conversation whose queue has not answered yet are its own. *New conversation* sets that
   * conversation aside, and its words with it: the new one shows no words opening, and opens ahead once the other has ended.
   */
  it('sets a conversation’s first words aside with it on New conversation, never left opening', async () => {
    const words = 'how do I land a branch?';
    bridge();
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      if (type === 'START_HELP' && starts() === 1) {
        SESSIONS = [HELP];
        return { sessionId: HELP.id, message: 'Ask Daoris `h1e1p000` is starting.', running: false, starting: true };
      }
      if (type === 'START_HELP') return { sessionId: 'n3wc0000', message: 'Ask Daoris `n3wc0000` is starting.', running: false, starting: true };
      // Its queue never answers, so the words it took are still shown opening.
      if (type === 'SESSION_QUEUE') return new Promise(() => {});
      if (type === 'END_CHAT') SESSIONS = [{ ...HELP, state: 'completed' }];
      return answered(module, type, ...rest);
    });
    show();

    await waitFor(() => expect(starts()).toBe(1));
    await userEvent.type(await screen.findByLabelText('Message'), words);
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', { payload: { id: HELP.id, text: words } }));
    expect(await screen.findByText('opening Ask Daoris…')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'New conversation' }));

    expect(await screen.findByText(/Ask below about Daoris on this machine/)).toBeInTheDocument();
    expect(screen.queryByText('opening Ask Daoris…')).toBeNull();
    await waitFor(() => expect(starts()).toBe(2));
  });

  /** A go changes nothing, so its link sends the driver nothing. */
  it('takes the person to the place a go link in its words names, and sends nothing', async () => {
    SESSIONS = [HELP];
    bridge();
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => (type === 'SESSION_HISTORY'
      ? {
        session: HELP.id, earlier: false, latest: 1,
        events: [{ seq: 1, at: new Date().toISOString(), kind: 'message', text: 'Its agent is named at [step 2](daoris:go?view=settings&domain=start&part=helper).' }],
      }
      : answered(module, type, ...rest)));
    const onGo = vi.fn();
    show(undefined, null, onGo);

    await userEvent.click(await screen.findByRole('button', { name: 'step 2' }));

    expect(onGo).toHaveBeenCalledWith({ view: 'settings', section: 'start', anchor: 'step-helper' });
    expect(screen.queryByRole('link', { name: 'step 2' })).toBeNull();
    expect(invoke.mock.calls.map(([, type]) => type).filter((type) => /APPLY|DISMISS|PROPOSALS/.test(String(type)))).toEqual([]);
  });

  /**
   * What the conversation published is in its own strip, with the state each one's
   * list holds now: here a quest a session has taken, named by that session's state. A row opens its record on Quests, the door a
   * go uses, and nothing else is pressed.
   */
  it('lists what the conversation published with its live state, and opens one on Quests', async () => {
    const at = '2026-10-22T09:00:00Z';
    SESSIONS = [HELP, {
      id: 's9w8v7u6', quest: 'q1a2b3c4', repository: 'engine', door: 'claude-code', state: 'working', kind: 'task',
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

  /** A conversation that published nothing shows no strip, and asks no list of quests or asks for one. */
  it('shows no strip where the conversation published nothing', async () => {
    SESSIONS = [HELP];
    bridge();
    show();

    await waitFor(() => expect(invoke.mock.calls.map(([, type]) => type)).toContain('HELP_PUBLISHED'));
    expect(screen.queryByRole('button', { name: /Published from this conversation/ })).not.toBeInTheDocument();
    // The page fetches neither: the quests and the asks are the shell's to push.
    expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).startsWith('/api/quests') || String(url).startsWith('/api/asks')))
      .toBe(false);
  });


  /** A stop ends the reply, never the conversation: its box stays, nothing opens ahead, and the next word goes to it. */
  it('keeps a stopped conversation open, says the next word continues it, and sends that word to it', async () => {
    SESSIONS = [{ ...HELP, state: 'stopped' }];
    bridge({ sessionId: 'n3xt0000', message: 'opened' });
    show();

    const panel = await screen.findByRole('complementary', { name: 'Ask Daoris' });
    expect(await within(panel).findByText('Stopped. Your next word continues this conversation.')).toBeInTheDocument();
    await userEvent.type(within(panel).getByLabelText('Message'), 'and the remote?');
    await userEvent.click(within(panel).getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'and the remote?' },
    }));
    expect(starts()).toBe(0);
  });

  it('says one that ended for any other reason continues too', async () => {
    SESSIONS = [{ ...HELP, state: 'failed' }];
    bridge({ sessionId: 'n3xt0000', message: 'opened' });
    show();

    const panel = await screen.findByRole('complementary', { name: 'Ask Daoris' });
    expect(await within(panel).findByText('Your next word continues this conversation.')).toBeInTheDocument();
    expect(within(panel).getByLabelText('Message')).toBeEnabled();
    expect(within(panel).queryByRole('button', { name: 'Finish' })).toBeNull();
  });

  /** Its one Stop, while a reply runs on any door, stops the reply's whole process tree through the session's stop. */
  it('stops the reply in flight with its one Stop, offering no ending', async () => {
    SESSIONS = [HELP];
    bridge();
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...rest: unknown[]) => {
      if (type === 'SESSION_QUEUE') return { session: HELP.id, queued: [], taking: true };
      if (type === 'STOP_SESSION') return { stopped: true, orphan: false, elsewhere: false };
      return answered(module, type, ...rest);
    });
    show();

    const panel = await screen.findByRole('complementary', { name: 'Ask Daoris' });
    await userEvent.click(await within(panel).findByRole('button', { name: 'Stop' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', { payload: { id: HELP.id } }));
    expect(within(panel).queryByRole('button', { name: 'Finish' })).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', expect.anything());
  });
});

/**
 * Ask Daoris's conversations, kept on this machine only. The history lists them; one the person chooses is shown in
 * the newest's place and goes on in itself with their next words where its own conversation is kept, so its knowledge
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

  /**
   * An earlier part owns neither the region's scroll nor the frame's facts. Ended,
   * it would open the shared region at its own head, so the line would open mid-way; only the part shown follows the tail.
   */
  it('lets the part shown alone own the region’s scroll, no earlier part opening it at its own head', async () => {
    SESSIONS = [EARLIER, HELP];
    withHistory([row({ session: HELP.id, live: true, from: EARLIER.id, handed: 'brief', parts: [EARLIER.id, HELP.id] })]);
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, body?: { payload?: { id?: string } }) => {
      if (type === 'SESSION_HISTORY') {
        const id = body?.payload?.id;
        return { session: id, events: [
          { seq: 1, kind: 'user', origin: 'person', text: id === EARLIER.id ? 'Original question' : 'Held words', at: HELP.created },
          { seq: 2, kind: 'message', text: id === EARLIER.id ? 'Earlier answer' : 'Continued answer', at: HELP.created },
        ], earlier: false, latest: 2 };
      }
      return answered(module, type, body);
    });
    vi.spyOn(HTMLElement.prototype, 'scrollHeight', 'get').mockReturnValue(5000);
    const set = vi.spyOn(Element.prototype, 'scrollTop', 'set');
    show();

    expect(await screen.findByText('Earlier answer')).toBeInTheDocument();
    expect(await screen.findByText('Continued answer')).toBeInTheDocument();
    expect(set.mock.calls.length).toBeGreaterThan(0);
    expect(set.mock.calls.map(([top]) => top)).not.toContain(0);
    set.mockRestore();
    vi.restoreAllMocks();
  });

  it('goes on in an earlier conversation the person chose from the history, with their words', async () => {
    withHistory([row({})]);
    show();

    await userEvent.click(await screen.findByRole('button', { name: 'Back to history' }));
    const list = await screen.findByRole('region', { name: 'Conversation history' });
    await userEvent.click(within(list).getAllByRole('button', { name: /^what is a workspace\?/ })[0]);

    expect(await screen.findByText('Your next word continues this conversation.')).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('Message'), 'and a remote?');
    const listed = () => invoke.mock.calls.filter(([, type]) => type === 'HELP_CONVERSATIONS').length;
    const before = listed();
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: EARLIER.id, text: 'and a remote?' },
    }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', { payload: expect.objectContaining({ id: 'n3xt0000' }) });
    // The word said declares the history, so the state hub lists it again, not the conversation's own ask.
    await waitFor(() => expect(listed()).toBeGreaterThan(before));
  });

  /**
   * Ask Daoris follows the same rule in its own room: words to an ended conversation whose
   * resume is refused wait for the person, and *Go on with Daoris's brief* goes on in a new conversation of the room handed Daoris's
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
      if (type === 'SESSION_HAND_OVER') return { sessionId: 'br1ef000', kept: true, message: 'went on' };
      return answered(module, type, ...rest);
    });
    show();

    await userEvent.click(await screen.findByRole('button', { name: "Go on with Daoris's brief" }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', { payload: { id: EARLIER.id, why: 'press' } }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_START_FROM', expect.anything());
  });

  /** The newest that ended, shown by default, takes the next word itself: no press first, and nothing opened ahead. */
  it('continues the newest that ended with the next word, opening no other', async () => {
    withHistory([row({})]);
    show();

    expect(await screen.findByText('Your next word continues this conversation.')).toBeInTheDocument();
    expect(screen.getByLabelText('Message')).toHaveAttribute('placeholder', expect.stringMatching(/^write to go on in this conversation/));
    await userEvent.type(screen.getByLabelText('Message'), 'and a remote?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: EARLIER.id, text: 'and a remote?' },
    }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'START_HELP', expect.anything());
  });

  /** One whose own conversation is lost takes the word too: the driver goes on in a new part handed Daoris's brief. */
  it('sends the next word to one that cannot resume its own conversation', async () => {
    withHistory([row({ resumable: false })]);
    show();

    expect(await screen.findByText('Your next word continues this conversation.')).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('Message'), 'and a remote?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: EARLIER.id, text: 'and a remote?' },
    }));
  });

  /**
   * A conversation its account's limit holds says so above the box, read from its events; its *Go on now
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

  /** A row's *New conversation from it*, from the history's menu. */
  async function startFromRow(title: string) {
    await userEvent.click(await screen.findByRole('button', { name: 'Back to history' }));
    screen.getByRole('button', { name: i18n.t('help.history.menu', { title }) }).focus();
    await userEvent.keyboard('{Enter}');
    const press = await screen.findByRole('menuitem', { name: 'New conversation from it' });
    SESSIONS = [EARLIER, { ...HELP, id: 'fr0m0000', created: '2026-09-29T01:00:00Z' }];
    await userEvent.click(press);
  }

  /** A new conversation from an earlier one's words, its brief handed with the first message. */
  it('starts a new conversation from the words of an earlier one', async () => {
    withHistory([row({ resumable: false })], { sessionId: 'fr0m0000', message: 'opened', from: EARLIER.id });
    show();

    await startFromRow('what is a workspace?');

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_START_FROM', { payload: { id: EARLIER.id } }));
    expect(await screen.findByText('This conversation starts from “what is a workspace?”: Daoris\'s brief of it goes with your first message.'))
      .toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('Message'), 'and a remote?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 'fr0m0000', text: 'and a remote?' },
    }));
  });

  /** A title too long for the dock, a pasted URL, breaks at the panel's edge where the conversation says it. */
  it('says which conversation one started from at the dock’s edge, however long its title', async () => {
    const title = 'to complete this https://example.atlassian.net/browse/TK-2205?focusedCommentId=1234567&page=com.example.plugin.tabpanels';
    withHistory([row({ resumable: false, title, opening: title })], { sessionId: 'fr0m0000', message: 'opened', from: EARLIER.id });
    show();

    await startFromRow(title);

    expect(await screen.findByText(`This conversation starts from “${title}”: Daoris's brief of it goes with your first message.`))
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
    // It starts from the title shown.
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
    // The delete declares the history: the state hub lists it again, the conversation asks nothing by hand.
    await waitFor(() => expect(listed()).toBeGreaterThan(before));
  });
});

/**
 * The history and the open conversation, made right after the owner looked at the window. Each conversation keeps
 * its own draft, words and files, and a new one its own; one the person chose that cannot go on takes no words until they
 * choose a new conversation from it, or a blank one, and one being checked takes none either; the keys go from the list to a
 * conversation and back to the row it is opened from; and the list says what it is doing.
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

  it('takes words under every conversation it chose, one that cannot resume its own included, and sends them to it', async () => {
    historyBridge(() => ({ conversations: [row(A.id, 'about A?', { resumable: false }), row(B.id, 'about B?')], cut: false }));
    show();

    await open('about A?');
    expect(screen.getByText(i18n.t('help.continues'))).toBeInTheDocument();
    await userEvent.type(box()!, 'more on A');
    await userEvent.click(screen.getByRole('button', { name: i18n.t('work.composer.send') }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', { payload: { id: A.id, text: 'more on A' } }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_START_FROM', expect.anything());
  });

  // A search reaches every conversation, past the pages the whole list has read; the one it opens takes words at once.
  it('takes words under one a search found while the whole list has not answered', async () => {
    historyBridge((words) => (words
      ? { conversations: [row(A.id, 'about A?', { found: 'about A?', resumable: false })], total: 1, next: null }
      : new Promise(() => {})));
    show();

    await userEvent.click(await back());
    await userEvent.type(screen.getByRole('searchbox', { name: i18n.t('help.history.search.label') }), 'about');
    await userEvent.click(await door('about A?'));

    expect(await screen.findByText(i18n.t('help.continues'))).toBeInTheDocument();
    expect(box()).not.toBeNull();
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
 * Ask Daoris's access on its panel. The chip beside the box shows the mode
 * the conversation in view runs under, its record's, and a switch that waits for its next turn; restricted is set at
 * once and full access asked once; *What it has* reads what its start handed, with the door that grants each thing it lacks.
 */
describe('Ask Daoris’s access and what it has', () => {
  let MODE = 'restricted';
  // The conversations the driver says run under another access than the one chosen.
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
    at: '2026-10-19T10:00:00+00:00', start: 'new', mode: 'restricted', door: 'claude-code-acp', version: '0.9.1', account: null,
    posture: 'default', permissionMode: null, folder: 'C:/daoris/help/room', tools: [], skills: [],
    servers: [{ name: 'browser', plugin: 'browser', state: 'withheld', tools: null, why: 'not-approved' }],
    rules: { allow: ['Read'], ask: [], deny: [] }, guarded: true, checkouts: [],
    lacks: [
      { kind: 'server', name: 'browser', door: 'plugins', why: 'not-approved', plugin: 'browser' },
      { kind: 'server', name: 'deploy', door: 'mode', why: 'not-for-help', plugin: 'deploy' },
      { kind: 'shell', name: 'shell', door: 'mode', why: 'restricted', plugin: null },
    ],
  };
  const STATE = () => ({
    drivable: ['engine'], holds: [], trees: [], helperAgent: HELPER ?? '', helpMode: MODE, helpSwapping: SWAPPING,
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
   * The driver ends a conversation whose access changed at its turn's end, and its next words resume the
   * same conversation under the new access. The panel keeps it in view as it ends, so those words go on in it, never in a new
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
    await act(() => moved());
    act(() => heard.get('STATE_CHANGED')?.({ scopes: ['sessions', 'driver', 'help'] }));

    expect(await screen.findByText('Your next word continues this conversation.')).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('Message'), 'and now?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: HELP.id, text: 'and now?' },
    }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', { payload: expect.objectContaining({ id: 'n3xt0000' }) });
  });

  it('keeps one that ended for no access change open too, its next word continuing it', async () => {
    SESSIONS = [HELP];
    ROWS = [row({ mode: 'restricted' })];
    access({ sessionId: 'n3xt0000', message: 'opened' });
    show();
    await chip('Restricted');

    SESSIONS = [{ ...HELP, state: 'completed' }];
    ROWS = [row({ live: false, mode: 'restricted' })];
    await act(() => moved());
    act(() => heard.get('STATE_CHANGED')?.({ scopes: ['sessions', 'driver', 'help'] }));

    expect(await screen.findByText('Your next word continues this conversation.')).toBeInTheDocument();
    expect(screen.getByLabelText('Message')).toBeEnabled();
  });

  it('shows the mode chosen before any start, and says what full access does above the box', async () => {
    MODE = 'full';
    access();
    show();

    expect(await chip('Full access')).toBeInTheDocument();
    expect(screen.getByText(/runs on claude-code-acp with full access: it runs commands and edits files/)).toBeInTheDocument();
  });

  it('offers no choice where the snapshot names no mode', async () => {
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

  // Full access's refusal is said inside its ask, open to be pressed again, and a switch
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
