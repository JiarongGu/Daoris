import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The Work frame as a PAGE, in the shape `shell.test.tsx` already uses: the bridge mocked as
// present, because the frame does not exist without one (D55). Everything a person does to a
// session happens here now — starting it, talking to it, ending it, and watching its console —
// which is what "one home for the stream" (design §3) means as a test file.

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import i18n from '../i18n';
import { WorkFrame } from './WorkFrame';
import { DiffPane } from './DiffPane';

const DRIVER_STATE = { drivable: ['engine'], holds: [], trees: [], running: ['s1a2b3c4'] };

const ROSTER = {
  settingsPath: 'C:/somewhere/.daoris/harnesses.json',
  adapter: 'claude-code',
  harnesses: [
    {
      harness: 'claude-code', present: true, version: 'claude 9.9.9', problem: null,
      machineDefault: 'personal',
      profiles: [
        { name: 'personal', home: 'C:/somewhere/.daoris/harnesses/claude-code/personal', login: 'in' },
        { name: 'work', home: 'C:/somewhere/.daoris/harnesses/claude-code/work', login: 'out' },
      ],
    },
    {
      harness: 'codex', present: false, version: null,
      problem: '`codex` is not on this machine\'s PATH', machineDefault: null, profiles: [],
    },
  ],
};

/** A roster whose chat harness reads a structured wire — a door where a turn can be stopped (CONV4). */
const STRUCTURED_ROSTER = {
  ...ROSTER,
  harnesses: [{ harness: 'stub', present: true, version: 'stub 1.0.0', problem: null, machineDefault: null, profiles: [], structured: true }],
};

const REGISTRY = [{
  repository: 'engine', adopted: true, registered: true, summary: 'the engine',
  owns: [], accepts: [], packs: [], entries: 1, root: 'C:/somewhere/engine',
}];

const QUESTS = [{
  id: 'abc123', from: 'game', to: 'engine',
  title: 'Expose a streaming budget', body: 'World streaming needs a per-frame cap.',
  status: 'Open', filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
}];

const DRIVEN = {
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'working',
  kind: 'driven', created: '2026-09-02T00:00:00Z', updated: '2026-09-02T01:00:00Z',
};

const CHAT = {
  id: 'c0ffee11', quest: null, repository: 'engine', adapter: 'stub', state: 'working',
  kind: 'chat', created: '2026-09-02T00:00:00Z', updated: '2026-09-02T00:05:00Z',
};

const PARKED = {
  ...DRIVEN, id: 'p4rk3d00', state: 'awaiting-person',
  note: 'Two ways forward; I recommend the second.',
};

/** An intake that could not settle whose an ask is, parked asking the person (INT4b). */
const PARKED_INTAKE = {
  id: 'i9n8t7k6', quest: null, repository: 'ask #0fda18', ask: '0fda18', adapter: 'stub',
  state: 'awaiting-person', kind: 'chat', tree: 'C:/somewhere/data/intake/default',
  note: 'published nothing: it asks you rather than guess.',
  created: '2026-09-02T00:00:00Z', updated: '2026-09-02T00:05:00Z',
};

/** An intake still at work on its ask (INT4h) — one turn, with no message box. */
const RUNNING_INTAKE = { ...PARKED_INTAKE, id: 'r7n8t7k6', state: 'working', note: undefined };

let SESSIONS: unknown[] = [DRIVEN];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

/** The frame with the application's selection held for it, as `App` holds it. */
function show(selected: string | null = null, notify: (text: string) => void = () => {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const onSelect = vi.fn();
  const view = render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkFrame selected={selected} onSelect={onSelect} notify={notify} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
  return { ...view, onSelect, client };
}

describe('the Work frame', () => {
  beforeEach(() => {
    SESSIONS = [DRIVEN];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    // A draft outlives its test the way it outlives a reload (CONV4b).
    window.localStorage.removeItem('daoris.drafts');
  });

  it('is the rail and the attended session, bound by one selection', async () => {
    show('s1a2b3c4');

    // The rail's row and the head's title are the same derived identity, from one implementation.
    await screen.findByRole('heading', { level: 2, name: 'Expose a streaming budget' });
    await vi.waitFor(() =>
      expect(screen.getAllByText('Expose a streaming budget')).toHaveLength(2));
  });

  it('lands on a designed nothing, not on a session it chose for the person', async () => {
    show(null);
    expect(await screen.findByText('Nothing attended')).toBeInTheDocument();
  });

  /**
   * 🔴 Seen on the window (CONV3's look): a session just started is attended before the list has
   * fetched it, and the frame cleared the selection as a record that was gone, leaving "Nothing
   * attended" beside a running chat. Only a record the frame has seen can be gone.
   */
  it('keeps a just-started session attended until the list catches up with it', async () => {
    const { onSelect } = show('c0ffee11');

    await screen.findByText(/Expose a streaming budget/);
    expect(onSelect).not.toHaveBeenCalledWith(null);
  });

  it('lets go of a session whose record it saw and that is gone', async () => {
    const { onSelect, client } = show(DRIVEN.id);
    await screen.findAllByText(/Expose a streaming budget/);

    SESSIONS = [];
    await act(() => client.invalidateQueries());

    await waitFor(() => expect(onSelect).toHaveBeenCalledWith(null));
  });

  /**
   * The console (D49 §2): the transcript capture, streaming. It reaches the page over the shell's
   * bridge and has no HTTP route at all, because output is transcript-class material and never
   * leaves the machine that produced it (D47 §4). Its home is the output panel now (D55).
   */
  it('asks the driver for the attended session\'s console and renders it verbatim', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? {
          session: 's1a2b3c4',
          lines: [{ sequence: 1, text: '$ npm test' }, { sequence: 2, text: 'ok 1337 passing' }],
          sequence: 2, live: true, dropped: 0,
        }
      : DRIVER_STATE));

    show('s1a2b3c4');

    expect(await screen.findByText(/ok 1337 passing/)).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TAIL_SESSION', {
      payload: { id: 's1a2b3c4' },
    });
  });

  /** Live lines arrive as events and join the backlog as one stream, keyed by the driver's sequence. */
  it('joins live output to the backlog without repeating what was already shown', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? { session: 's1a2b3c4', lines: [{ sequence: 1, text: 'first' }], sequence: 1, live: true, dropped: 0 }
      : DRIVER_STATE));

    show('s1a2b3c4');
    await screen.findByText(/first/);

    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_OUTPUT')!({
        session: 's1a2b3c4',
        lines: [{ sequence: 1, text: 'first' }, { sequence: 2, text: 'second' }],
      });
    });

    expect((await screen.findByText(/second/)).textContent).toBe('first\nsecond');
  });

  /** Another session's output is not this panel's — the event carries whose it is. */
  it('ignores a batch for a different session', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? { session: 's1a2b3c4', lines: [{ sequence: 1, text: 'mine' }], sequence: 1, live: true, dropped: 0 }
      : DRIVER_STATE));

    show('s1a2b3c4');
    await screen.findByText(/mine/);

    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_OUTPUT')!({
        session: 'somebody-else', lines: [{ sequence: 1, text: 'theirs' }],
      });
    });

    expect(screen.queryByText(/theirs/)).toBeNull();
  });

  /**
   * The console is the part that may be missing — a shell older than this surface answers something
   * else entirely. It degrades to an empty panel; the RECORD is what the region is for.
   */
  it('leaves the record standing when the answer is not a console', async () => {
    invoke.mockImplementation(async () => DRIVER_STATE); // no `lines` anywhere in it

    show('s1a2b3c4');

    expect(await screen.findByRole('heading', { level: 2, name: 'Expose a streaming budget' }))
      .toBeInTheDocument();
  });

  /** The panel is a region a person owns: it grows, it shrinks, and only they close it (D55). */
  it('resizes the output panel from the keyboard and remembers the height', async () => {
    show('s1a2b3c4');

    const handle = await screen.findByRole('separator', { name: 'console height' });
    const before = Number(handle.getAttribute('aria-valuenow'));
    // A resize that needs a mouse is a resize some people do not have.
    handle.focus();
    await userEvent.keyboard('{ArrowUp}');

    const grown = screen.getByRole('separator', { name: 'console height' });
    expect(Number(grown.getAttribute('aria-valuenow'))).toBeGreaterThan(before);
    expect(window.localStorage.getItem('daoris.panelHeight')).toBe(String(before + 48));
  });

  it('hides the panel when the person hides it, and nothing else reopens it', async () => {
    show('s1a2b3c4');

    await userEvent.click(await screen.findByRole('button', { name: 'hide the console' }));
    expect(screen.queryByRole('separator', { name: 'console height' })).toBeNull();
    expect(screen.getByRole('button', { name: 'show the console' })).toBeInTheDocument();
  });
});

/**
 * Conversations (D49 §3): a session a person entered rather than the driver planned. Starting one
 * moved here from Projects (design §3) — where the result appears, and where a person can be told
 * what the harness said.
 */
describe('starting and holding a conversation', () => {
  beforeEach(() => {
    SESSIONS = [DRIVEN];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    // A draft outlives its test the way it outlives a reload (CONV4b).
    window.localStorage.removeItem('daoris.drafts');
  });

  /**
   * New is behind one control (D56) — the form used to hold 287×200 of the rail permanently, for
   * something a person does occasionally. Opening it is now a step, and every test that starts a
   * session takes it.
   */
  const openStart = async () => {
    await userEvent.click(await screen.findByRole('button', { name: 'Start a session' }));
    return screen.findByRole('dialog');
  };

  /**
   * 🔴 The palette's "start a session" arrives as an EVENT — `intent`, cleared by `onIntentTaken`
   * the way `App` clears it. Consumed by setting state during render, React re-ran the frame with
   * the same prop on every pass: the quest composer's opening draft failed exactly this way and
   * opened nothing on the window (FIX-LOG 2026-09-23). Held here as App holds it.
   */
  it('opens the start form when the palette asks, once', async () => {
    function Held() {
      const [intent, setIntent] = useState<'start' | 'review' | null>('start');
      return (
        <WorkFrame
          selected={null} onSelect={() => {}} notify={() => {}}
          intent={intent} onIntentTaken={() => setIntent(null)}
        />
      );
    }
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider><Held /></Tooltip.Provider>
      </QueryClientProvider>,
    );

    expect(await screen.findByRole('dialog')).toBeTruthy();
    expect(await screen.findByLabelText('repository')).toBeTruthy();
  });

  it('keeps the form behind one control, and the rail a list of sessions', async () => {
    show(null);
    await screen.findByRole('navigation', { name: 'sessions' });

    expect(screen.queryByLabelText('repository')).toBeNull();
    expect(screen.queryByRole('button', { name: 'start' })).toBeNull();

    await openStart();
    expect(screen.getByLabelText('repository')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'start' })).toBeTruthy();
  });

  it('closes the form on the session it opened — and keeps it up on a refusal', async () => {
    let refuse = true;
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') {
        return refuse ? { sessionId: null, message: 'busy' } : { sessionId: 'c0ffee11', message: 'ok' };
      }
      if (type === 'HARNESSES') return ROSTER;
      return DRIVER_STATE;
    });

    show(null);
    await openStart();
    await userEvent.click(screen.getByRole('button', { name: 'start' }));
    // A refusal leaves the form open: the choices are still on screen to correct.
    expect(screen.getByRole('dialog')).toBeTruthy();

    refuse = false;
    await userEvent.click(screen.getByRole('button', { name: 'start' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  });

  it('starts one in a repository with a checkout here, and attends what comes back', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') return { sessionId: 'c0ffee11', message: 'Chat `c0ffee11` opened in `engine`.' };
      if (type === 'HARNESSES') return ROSTER;
      return DRIVER_STATE;
    });

    const { onSelect } = show(null);
    await openStart();
    await userEvent.click(await screen.findByRole('button', { name: 'start' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'START_CHAT', {
      payload: { repository: 'engine' },
    });
    expect(onSelect).toHaveBeenCalledWith('c0ffee11');
  });

  /**
   * Every choice may be left alone, and silence means the driver's own resolution rather than a
   * default this surface invented (D49 §4, D51): the omitted field IS the answer.
   */
  it('carries the account and the own-tree choice, and omits what was not chosen', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') return { sessionId: null, message: 'busy' };
      if (type === 'HARNESSES') return ROSTER;
      return DRIVER_STATE;
    });

    show(null);
    await openStart();
    await screen.findByLabelText('account');

    await userEvent.selectOptions(screen.getByLabelText('account'), 'work');
    await userEvent.click(screen.getByLabelText('in a working tree of its own'));
    await userEvent.click(screen.getByRole('button', { name: 'start' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'START_CHAT', {
      payload: { repository: 'engine', profile: 'work', ownTree: true },
    });
  });

  /**
   * A refusal is the ledger's own sentence, verbatim — and nothing is attended on one: a
   * conversation that was refused has no session to show. The fixture holds a live DRIVEN session
   * in `engine`, which is the case worth pinning.
   */
  it('surfaces a refusal and attends nothing', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'START_CHAT'
      ? { sessionId: null, message: '`engine` already has an active session — `s1a2b3c4` (working, quest `#abc123`).' }
      : DRIVER_STATE));
    const notify = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const onSelect = vi.fn();

    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected={null} onSelect={onSelect} notify={notify} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await openStart();
    await userEvent.click(await screen.findByRole('button', { name: 'start' }));

    expect(notify).toHaveBeenCalledWith(expect.stringContaining('already has an active session'), 'error');
    expect(onSelect).not.toHaveBeenCalled();
  });

  it('sends one message over the bridge and clears the box', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') return { sent: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    const box = await screen.findByLabelText('message');
    await userEvent.type(box, 'what is this repository for?');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 'c0ffee11', text: 'what is this repository for?' },
    });
    expect((box as HTMLTextAreaElement).value).toBe('');
  });

  /**
   * REV3: the composer lets go of the words when it sends. A send that did not arrive — the session
   * ended while they typed, or the driver refused — handed back nothing, so a paragraph and its files
   * were gone. They come back into the box now, and the files are named.
   */
  it('hands the words back into the box, and names the files, when a send does not arrive', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') return { sent: false };
      return DRIVER_STATE;
    });
    const notify = vi.fn();

    show('c0ffee11', notify);
    await userEvent.upload(await screen.findByLabelText('choose files…'), new File(['exit 3'], 'run.log', { type: 'text/plain' }));
    const box = screen.getByLabelText('message');
    await userEvent.type(box, 'a long paragraph worth keeping');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));

    expect(await screen.findByText(/went nowhere/)).toBeTruthy();
    await waitFor(() => expect((box as HTMLTextAreaElement).value).toBe('a long paragraph worth keeping'));
    expect(notify).toHaveBeenCalledWith(expect.stringContaining('run.log'), 'error');
  });

  /**
   * Finishing and stopping are different verbs and mean different things: end-of-input lets the
   * harness wind up, a stop cuts it off and the record says the person did.
   */
  /**
   * 🔴 What a stop says is what the driver answered (2026-09-25). A stop that found nothing running
   * here ended an orphan's record, and the notice still said the person had ended it; a stop on a
   * session that had already finished said it was "being stopped".
   */
  it.each([
    [{ stopped: true }, 'session c0ffee11 is being stopped — the record will say the person ended it.'],
    [{ stopped: true, orphan: true }, 'session c0ffee11 had nothing running it on this machine — its record now says so.'],
    [{ stopped: false }, 'session c0ffee11 was not running here — its record says how it ended.'],
  ])('says what the stop did when the driver answers %j', async (answer, sentence) => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'STOP_SESSION' ? answer : DRIVER_STATE));
    const notify = vi.fn();

    show('c0ffee11', notify);
    await userEvent.click(await screen.findByRole('button', { name: 'stop' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(sentence));
  });

  it('offers finishing and stopping separately', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'END_CHAT') return { ended: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    await userEvent.click(await screen.findByRole('button', { name: 'finish' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'END_CHAT', { payload: { id: 'c0ffee11' } });
    expect(screen.getByRole('button', { name: 'stop' })).toBeTruthy();
  });

  /**
   * CONV4b: stopping the turn over the driver. What was waiting comes back into the box — the person
   * wrote it, and a stop that threw it away would lose it — and the notice claims only the asking:
   * the record says whether the turn ended stopped.
   */
  it('stops the turn over the driver, and hands what was waiting back to the box', async () => {
    SESSIONS = [CHAT];
    const notify = vi.fn();
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [{ text: 'and then test it', files: [] }], taking: true };
      if (type === 'CANCEL_TURN') return { cancelled: true, withdrawn: [{ text: 'and then test it', files: [] }] };
      return DRIVER_STATE;
    });

    show('c0ffee11', notify);
    const waiting = await screen.findByRole('list', { name: 'waiting for this turn to end' });
    expect(waiting.textContent).toBe('and then test it');
    await userEvent.type(screen.getByLabelText('message'), 'and push nothing');
    await userEvent.click(await screen.findByRole('button', { name: 'stop turn' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', { payload: { id: 'c0ffee11' } });
    await waitFor(() => expect(screen.getByLabelText('message')).toHaveValue('and then test it\n\nand push nothing'));
    expect(notify).toHaveBeenCalledWith(
      'Asked the agent to stop this turn; the conversation stays open. 1 waiting message came back to the box, unsent.');
  });

  /** Seen on the window (CONV4b): two sentences joined by a space read wrong after a Chinese full stop. */
  it('joins the stop\'s two sentences the way the language does', async () => {
    SESSIONS = [CHAT];
    const notify = vi.fn();
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [{ text: '然后测试', files: [] }], taking: true };
      if (type === 'CANCEL_TURN') return { cancelled: true, withdrawn: [{ text: '然后测试', files: [] }] };
      return DRIVER_STATE;
    });
    await i18n.changeLanguage('zh');
    try {
      show('c0ffee11', notify);
      await userEvent.click(await screen.findByRole('button', { name: '停止本轮' }));
      await waitFor(() => expect(notify).toHaveBeenCalledWith(
        '已请智能体停止这一轮；对话仍然开着。1 条等待中的消息已退回输入框，未发送。'));
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /** CONV4c: a message's files go over the bridge as names and their bytes, the way a quest's uploads do. */
  it('sends a message\'s files over the bridge as names and bytes', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') return { sent: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    await userEvent.upload(await screen.findByLabelText('choose files…'), new File(['exit 3'], 'run.log', { type: 'text/plain' }));
    await userEvent.type(screen.getByLabelText('message'), 'what does this say?');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 'c0ffee11', text: 'what does this say?', files: [{ name: 'run.log', content: btoa('exit 3') }] },
    }));
  });

  /**
   * CONV4c: a file cannot come back into the box — the page no longer holds its bytes — so a stop that
   * hands back a message with files says which were not sent, for the person to attach again.
   */
  it('names the files a stop kept from being sent', async () => {
    SESSIONS = [CHAT];
    const notify = vi.fn();
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [{ text: 'then this', files: ['plan.md'] }], taking: true };
      if (type === 'CANCEL_TURN') return { cancelled: false, withdrawn: [{ text: 'then this', files: ['plan.md', 'shot.png'] }] };
      return DRIVER_STATE;
    });

    show('c0ffee11', notify);
    await userEvent.click(await screen.findByRole('button', { name: 'stop turn' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      '1 waiting message came back to the box, unsent. Not sent with it: plan.md, shot.png — attach them again.'));
    expect(screen.getByLabelText('message')).toHaveValue('then this');
  });

  /** CONV4b: the stop is there while the driver says a turn is in flight, and the send button says *queue*. */
  it('follows the driver live: a stop while a turn runs, and none between turns', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: false };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    expect(await screen.findByRole('button', { name: 'send' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'stop turn' })).not.toBeInTheDocument();

    await waitFor(() => expect(eventHandlers.has('DAORIS.SESSION_QUEUED')).toBe(true));
    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 'c0ffee11', queued: [], taking: true }));
    expect(await screen.findByRole('button', { name: 'stop turn' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'queue' })).toBeInTheDocument();

    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 'c0ffee11', queued: [], taking: false }));
    await waitFor(() => expect(screen.queryByRole('button', { name: 'stop turn' })).not.toBeInTheDocument());
  });

  /** CONV4b: each conversation keeps its own draft as the person moves between them. */
  it('keeps each conversation its own draft', async () => {
    const OTHER = { ...CHAT, id: 'decaf222', repository: 'game' };
    SESSIONS = [CHAT, OTHER];
    const view = show('c0ffee11');
    await userEvent.type(await screen.findByLabelText('message'), 'half a thought for the engine');

    view.rerender(
      <QueryClientProvider client={view.client}>
        <Tooltip.Provider>
          <WorkFrame selected="decaf222" onSelect={view.onSelect} notify={() => {}} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await waitFor(() => expect(screen.getByLabelText('message')).toHaveValue(''));

    view.rerender(
      <QueryClientProvider client={view.client}>
        <Tooltip.Provider>
          <WorkFrame selected="c0ffee11" onSelect={view.onSelect} notify={() => {}} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await waitFor(() => expect(screen.getByLabelText('message')).toHaveValue('half a thought for the engine'));
  });

  /**
   * A driven session holds a tree but was given its whole target at once — there is no channel to
   * speak into, so it gets no composer. An input box nothing is listening to is worse than none.
   */
  it('offers no composer for a driven session', async () => {
    show('s1a2b3c4');

    await screen.findByRole('heading', { level: 2, name: 'Expose a streaming budget' });
    expect(screen.queryByLabelText('message')).toBeNull();
  });
});

/**
 * `awaiting-person` has meant "only the person can clear this" since D46 and had no surface at all
 * (design §4). The three moves land on the DRIVER, not on the service, because the process and the
 * record must move together — a record saying `completed` beside a process this machine still
 * holds is the lie the observed lifecycle exists to prevent.
 */
describe('clearing a parked session', () => {
  beforeEach(() => {
    SESSIONS = [PARKED];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    // A draft outlives its test the way it outlives a reload (CONV4b).
    window.localStorage.removeItem('daoris.drafts');
  });

  it('shows the analysis and the three moves on the attended session', async () => {
    show('p4rk3d00');

    expect(await screen.findByText(/I recommend the second/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'finish it' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'decline…' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'stop it' })).toBeInTheDocument();
  });

  /**
   * **One owner for the moves at a time** (D56). A parked conversation used to render the band's
   * *finish it · decline… · stop it* and the composer's *send · finish · stop* simultaneously, 400px
   * apart — two owners for one set of verbs, which is worse than either. The band owns them while a
   * session is parked, and the composer keeps `send` alone so answering stays possible.
   */
  it('gives the verbs to the band while parked, and back to the composer when live', async () => {
    SESSIONS = [{ ...PARKED, kind: 'chat' }];
    show('p4rk3d00');

    await screen.findByRole('button', { name: 'finish it' });
    expect(screen.getByRole('button', { name: 'send' })).toBeInTheDocument();
    // The band's verbs, and only the band's.
    expect(screen.queryByRole('button', { name: 'finish' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'stop' })).toBeNull();

    // Working again: no band, and the composer owns both endings.
    SESSIONS = [{ ...PARKED, kind: 'chat', state: 'working' }];
    cleanup();
    show('p4rk3d00');

    await screen.findByRole('button', { name: 'finish' });
    expect(screen.getByRole('button', { name: 'stop' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'finish it' })).toBeNull();
  });

  it('finishes it over the driver, with no note the person did not write', async () => {
    show('p4rk3d00');
    await userEvent.click(await screen.findByRole('button', { name: 'finish it' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', {
      payload: { id: 'p4rk3d00', state: 'completed' },
    });
  });

  it('carries the reason a decline was given', async () => {
    show('p4rk3d00');
    await userEvent.click(await screen.findByRole('button', { name: 'decline…' }));
    await userEvent.type(screen.getByLabelText(/the reason/), 'the chunk API is being replaced');
    await userEvent.click(screen.getByRole('button', { name: 'decline with this reason' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', {
      payload: { id: 'p4rk3d00', state: 'declined', note: 'the chunk API is being replaced' },
    });
  });

  it('shows the host\'s refusal word for word', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type !== 'RESOLVE_SESSION') return DRIVER_STATE;
      throw Object.assign(new Error('fallback'), {
        code: 'SESSION_DECLINE_NEEDS_REASON', parameters: {},
      });
    });
    const notify = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="p4rk3d00" onSelect={vi.fn()} notify={notify} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await userEvent.click(await screen.findByRole('button', { name: 'finish it' }));

    await vi.waitFor(() => expect(notify)
      .toHaveBeenCalledWith(expect.stringContaining('Declining needs a reason'), 'error'));
  });

  /**
   * A teammate's record came down with the sync keyed `origin/id` (SYNC4) and is READ-ONLY here: the
   * process is on their machine, so nothing this window sends could reach it. It shows what it is
   * waiting on — and offers no moves and no composer, because a button that cannot work is a lie.
   */
  it('offers no moves and no composer on a teammate\'s session', async () => {
    SESSIONS = [{ ...PARKED, id: 'person@machine-b/p4rk3d00', kind: 'chat' }];
    show('person@machine-b/p4rk3d00');

    expect((await screen.findAllByText(/I recommend the second/)).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'finish it' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'stop it' })).toBeNull();
    expect(screen.queryByLabelText('message')).toBeNull();
    expect(screen.queryByRole('button', { name: 'send' })).toBeNull();
  });

  /**
   * INT4g: a parked intake's answer is on its ask, which the application opens in Quests — so the
   * frame hands the door up. No composer: an intake is one turn (INT4b), and a parked one has no
   * process left to hear a message.
   */
  it('leads a parked intake to its ask, with no composer and none of the three moves', async () => {
    SESSIONS = [PARKED_INTAKE];
    const onAnswerAsk = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="i9n8t7k6" onSelect={vi.fn()} notify={() => {}} onAnswerAsk={onAnswerAsk} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    await userEvent.click(await screen.findByRole('button', { name: 'answer ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledWith('0fda18');
    expect(screen.queryByRole('button', { name: 'finish it' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'send' })).toBeNull();
    expect(screen.queryByLabelText('message')).toBeNull();
  });

  /** Stopping stays: the person may end the intake and settle the ask later, as a proposal. */
  it('stops a parked intake over the driver, and nothing more', async () => {
    SESSIONS = [PARKED_INTAKE];
    show('i9n8t7k6');
    await userEvent.click(await screen.findByRole('button', { name: 'stop it' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', {
      payload: { id: 'i9n8t7k6', state: 'stopped' },
    });
  });

  /**
   * INT4h: a RUNNING intake takes no messages either. It is one turn, framed as one prompt, and on
   * the protocol door its stdin is the driver's own frames — so a composer there sent a person's
   * words into nothing, or into the middle of the JSON-RPC stream. No composer: its stop and its
   * ask's door live in the head, with the line that says where an answer goes.
   */
  it('offers a running intake no composer, only its stop and its ask', async () => {
    SESSIONS = [RUNNING_INTAKE];
    const onAnswerAsk = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="r7n8t7k6" onSelect={vi.fn()} notify={() => {}} onAnswerAsk={onAnswerAsk} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    expect(await screen.findByText(/takes no messages/)).toBeInTheDocument();
    expect(screen.queryByLabelText('message')).toBeNull();
    expect(screen.queryByRole('button', { name: 'send' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'finish' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'open ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledWith('0fda18');
  });

  /** The stop the composer used to carry: the process is cut off, over the driver. */
  it('stops a running intake over the driver', async () => {
    SESSIONS = [RUNNING_INTAKE];
    show('r7n8t7k6');
    await userEvent.click(await screen.findByRole('button', { name: 'stop it' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', { payload: { id: 'r7n8t7k6' } });
  });

  /** A conversation keeps its composer: it is the one kind of session that takes turns. */
  it('keeps the composer for a conversation', async () => {
    SESSIONS = [{ ...RUNNING_INTAKE, id: 'c0nv0000', ask: undefined, repository: 'engine' }];
    show('c0nv0000');

    expect(await screen.findByLabelText('message')).toBeInTheDocument();
    expect(screen.queryByText(/takes no messages/)).toBeNull();
  });

  /** A driven session that is not parked gets no moves: there is nothing waiting on anybody. */
  it('offers no moves on a session nobody is waiting for', async () => {
    SESSIONS = [DRIVEN];
    show('s1a2b3c4');

    await screen.findByRole('heading', { level: 2, name: 'Expose a streaming budget' });
    expect(screen.queryByRole('button', { name: 'finish it' })).toBeNull();
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
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    // A draft outlives its test the way it outlives a reload (CONV4b).
    window.localStorage.removeItem('daoris.drafts');
  });

  /** The dock opens on the timeline: a running session is watched far more often than reviewed. */
  it('docks the timeline beside the session, and asks for no diff until someone looks', async () => {
    show('s1a2b3c4');
    await screen.findByRole('tab', { name: 'Review' });

    expect(screen.getByRole('tab', { name: 'Timeline' }).getAttribute('aria-selected')).toBe('true');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DIFF', expect.anything());
  });

  it('reads the landed work off the checkout when the review tab is opened', async () => {
    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText('src/chunk.ts')).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DIFF', {
      payload: { id: 's1a2b3c4' },
    });
    // The range it is measured from, stated — a review that does not say so is an opinion.
    expect(screen.getByText(/abc12345/)).toBeTruthy();
    // And a binary file is listed as uncounted rather than as an empty change.
    expect(screen.getByText('binary')).toBeTruthy();
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

    expect(await screen.findByText(/no diff to read for this session/)).toBeTruthy();
  });

  /** A session that committed nothing changed nothing — an answer, not an empty list. */
  it('says a session landed nothing rather than showing an empty diff', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? { ...DIFF, files: [] } : DRIVER_STATE));

    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText('Nothing landed')).toBeTruthy();
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
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    // A draft outlives its test the way it outlives a reload (CONV4b).
    window.localStorage.removeItem('daoris.drafts');
  });

  const review = async () => {
    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    await screen.findByText('src/chunk.ts');
  };

  it('accepts by asking the driver to merge, and renders whatever it says back', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'MERGE_SESSION_TREE') {
        return { session: 's1a2b3c4', done: true, message: 'merged `daoris/x` into `main` — 2 commit(s).' };
      }
      return DRIVER_STATE;
    });

    await review();
    await userEvent.click(screen.getByRole('button', { name: 'accept' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'MERGE_SESSION_TREE', {
      payload: { id: 's1a2b3c4' },
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
      if (type === 'MERGE_SESSION_TREE') {
        return {
          session: 's1a2b3c4', done: false,
          message: "the repository's own checkout is not clean (2 paths), and merging into somebody's work in flight is exactly what Daoris does not do.",
        };
      }
      return DRIVER_STATE;
    });

    await review();
    await userEvent.click(screen.getByRole('button', { name: 'accept' }));

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
    await userEvent.click(screen.getByRole('button', { name: 'discard the tree' }));

    // No `force` anywhere in the first call.
    expect(forced).toEqual({ id: 's1a2b3c4' });
    // The host's warning is what the person now reads, naming what would go.
    expect(await screen.findByText(/has not taken/)).toBeTruthy();
    // And only now is the destructive press available.
    const again = screen.getByRole('button', { name: 'discard it anyway' });

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
    await userEvent.click(await screen.findByRole('button', { name: 'discard the tree' }));
    rerender(pane('s9f8e7d6'));
    await screen.findByRole('button', { name: 'discard the tree' });
    await act(async () => answer({ session: 's1a2b3c4', done: false, message: 'the tree holds commits `main` has not taken.' }));

    expect(screen.queryByRole('button', { name: 'discard it anyway' })).toBeNull();
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
    await userEvent.click(screen.getByRole('button', { name: 'discard the tree' }));
    await userEvent.click(await screen.findByRole('button', { name: 'never mind' }));

    expect(screen.queryByRole('button', { name: 'discard it anyway' })).toBeNull();
    expect(screen.getByRole('button', { name: 'discard the tree' })).toBeTruthy();
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
    await userEvent.click(await screen.findByRole('button', { name: 'send it back…' }));

    expect(onSendBack).toHaveBeenCalledWith('engine');
  });

  it('offers no send-back door where none was given', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? DIFF : DRIVER_STATE));

    await review();
    expect(screen.queryByRole('button', { name: 'send it back…' })).toBeNull();
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

    expect(screen.queryByRole('button', { name: 'accept' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'discard the tree' })).toBeNull();
    // The diff itself is still readable — seeing the work never depended on holding the tree.
    expect(screen.getByText('src/chunk.ts')).toBeTruthy();
  });
});
