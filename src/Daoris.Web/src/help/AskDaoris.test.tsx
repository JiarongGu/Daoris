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

import '../i18n';
import { keys } from '../queries';
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
    await waitFor(() => expect(screen.getAllByText(words)).toHaveLength(1));
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
    expect(within(cards).getByText('daoris driver drive engine', { selector: 'code' })).toBeInTheDocument();
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
      id: 'g1o2t3o4', kind: 'go', describe: 'Open Settings → Get started at step 2, Daoris\'s own agent.', terminal: '',
      why: 'the person asked where to name its agent',
    }];
    APPLIED = {
      message: 'Applied: `#g1o2t3o4` — Open Settings → Get started at step 2, Daoris\'s own agent. Nothing else changed.',
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
