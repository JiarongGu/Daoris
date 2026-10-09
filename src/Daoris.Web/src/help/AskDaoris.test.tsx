import { StrictMode, useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// HELP1a (D89): Ask Daoris's conversation, with the bridge mocked as present — the panel exists only in
// the shell, since most of what it reads is this machine's (D47 §4).

const { invoke, notifyReady } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => {},
}));

import i18n from '../i18n';
import { keys } from '../queries';
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
      default: return {};
    }
  });
}

/** How many times the page asked the driver to open Ask Daoris's conversation. */
const starts = () => invoke.mock.calls.filter(([, type]) => type === 'START_HELP').length;

function show(where?: Omit<HelpWhere, 'session'>, attending: string | null = null, onGo = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
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
    // Its conversations are read across every workspace: it belongs to none.
    expect(asked.some((url) => url.includes('repository=daoris%3Ahelp') && !url.includes('workspace='))).toBe(true);
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
    const hosts = (count: number) => (
      <QueryClientProvider client={client}>
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
   * HELP6: a go is applied by navigating, through the starters' own door, and nothing else changes. The
   * driver settles it and names the place; the page opens it — here the setup guide at its second step.
   */
  it('takes the person to the place a go names once they press go there', async () => {
    SESSIONS = [HELP];
    PROPOSALS = [{
      id: 'g1o2t3o4', kind: 'go', describe: 'Open Settings → Setup at step 2, Ask Daoris\'s agent.', terminal: '',
      why: 'the person asked where to name its agent',
    }];
    APPLIED = {
      message: 'Applied: `#g1o2t3o4` — Open Settings → Setup at step 2, Ask Daoris\'s agent. Nothing else changed.',
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

  it('goes on in an earlier conversation the person chose from the history, with their words', async () => {
    withHistory([row({})]);
    show();

    await userEvent.click(await screen.findByRole('button', { name: 'Back to history' }));
    const list = await screen.findByRole('region', { name: 'Conversation history' });
    await userEvent.click(within(list).getAllByRole('button', { name: /^what is a workspace\?/ })[0]);

    expect(await screen.findByText('This conversation has ended. Write to go on in it: it remembers what was said.')).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('Message'), 'and a remote?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: EARLIER.id, text: 'and a remote?' },
    }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', { payload: expect.objectContaining({ id: 'n3xt0000' }) });
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
    await user.click(await screen.findByRole('button', { name: 'Delete conversation' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DELETE', { payload: { id: EARLIER.id } }));
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

  /** The bridge as {@link bridge} answers it, with the history's routes answering by the words searched. */
  function historyBridge(listed: (words: string | undefined) => unknown, started?: { sessionId: string; message: string; from: string }) {
    bridge({ sessionId: 'n3xt0000', message: 'opened' });
    const answered = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, body?: { payload?: { q?: string } }, ...rest: unknown[]) => {
      if (type === 'HELP_CONVERSATIONS') return listed(body?.payload?.q);
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

  it('says it is checking whether a conversation can go on, and takes no words until it knows', async () => {
    // The whole list has not answered; a search has.
    historyBridge((words) => (words ? { conversations: [row(A.id, 'about A?', { found: 'about A?' })], cut: false } : new Promise(() => {})));
    show();

    await userEvent.click(await back());
    await userEvent.type(screen.getByRole('searchbox', { name: i18n.t('help.history.search.label') }), 'about');
    await userEvent.click(await door('about A?'));

    expect(await screen.findByRole('status')).toHaveTextContent(i18n.t('help.checking'));
    expect(box()).toBeNull();
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

    // One character is too few for the driver: said, and the whole list kept.
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
