import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// Starting and holding a conversation in the Work frame, one part of its suite (WEBSPEED1 cut the one file, which ran
// 181 s alone, into parts that run beside each other). The bridge is mocked as present, because the frame does not exist
// without one (D55); what the parts share is in `test/workFrameBridge.ts` and `test/workFrameKit.tsx`.
vi.mock('@shenora/react', async () => (await import('../test/workFrameBridge')).shenoraMock());

import i18n from '../locales/i18n';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import { eventHandlers, invoke } from '../test/workFrameBridge';
import {
  CHAT, DRIVEN, DRIVER_STATE, Hub, REGISTRY, ROSTER, STRUCTURED_ROSTER, QUESTS, UNANSWERED, show, timedOut, userEvent,
} from '../test/workFrameKit';
import { WorkFrame } from './WorkFrame';

let SESSIONS: unknown[] = [DRIVEN];

/** Every `SESSION_INPUT` the page sent, in order (SENDLOST1b: a send the page stopped waiting on is never said twice). */
const inputs = () => invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT');

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
    // A draft outlives its test the way it outlives a reload (CONV4b), and so do the starts this machine remembers (NEW1b).
    window.localStorage.removeItem('daoris.drafts');
    window.localStorage.removeItem('daoris.start.recent');
    window.localStorage.removeItem('daoris.start.chosen');
  });

  /** *New conversation*'s prompt (NEW1b, D162 point 1), which stands in the main area while nothing is attended. */
  const promptBox = () => screen.findByRole('textbox', { name: 'What would you like to work on?' });

  /** The frame with its selection held as `App` holds it, so attending a session shows it. */
  function held(selected: string | null = null) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const onSelect = vi.fn();
    const notify = vi.fn();
    function Held() {
      const [chosen, setChosen] = useState<string | null>(selected);
      return (
        <WorkFrame selected={chosen} onSelect={(id) => { onSelect(id); setChosen(id); }} notify={notify} />
      );
    }
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider><Held /></Tooltip.Provider>
      </QueryClientProvider>,
    );
    return { onSelect, notify };
  }

  /** What each START_CHAT carried, in order. */
  const starts = () => invoke.mock.calls
    .filter(([, type]) => type === 'START_CHAT')
    .map(([, , options]) => (options as { payload: Record<string, unknown> }).payload);

  /**
   * 🔴 The palette's *New conversation* arrives as an EVENT — `intent`, cleared by `onIntentTaken`
   * the way `App` clears it. Consumed by setting state during render, React re-ran the frame with
   * the same prop on every pass: the quest composer's opening draft failed exactly this way and
   * opened nothing on the window (FIX-LOG 2026-09-23). Held here as App holds it, with a session
   * attended, which *New conversation* puts down for the composer.
   */
  it('opens the composer when the palette asks, once, putting the attended session down', async () => {
    const onSelect = vi.fn();
    function Held() {
      const [intent, setIntent] = useState<'start' | 'review' | null>('start');
      const [selected, setSelected] = useState<string | null>('s1a2b3c4');
      return (
        <WorkFrame
          selected={selected} onSelect={(id) => { onSelect(id); setSelected(id); }} notify={() => {}}
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

    await waitFor(async () => expect(await promptBox()).toHaveFocus());
    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect).toHaveBeenCalledWith(null);
  });

  /**
   * NEW1f (D162's window note): pressed from a session in another repository, the composer opens on that repository, though
   * the press puts the session down; and *Recent* lists where the person has been working, not only what the composer started.
   */
  it('opens the composer on the repository of the session it was pressed from, and lists the sessions’ repositories as Recent', async () => {
    const game = { ...REGISTRY[0]!, repository: 'game', root: 'C:/somewhere/game' };
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => (String(input).startsWith('/api/registry')
      ? Response.json([...REGISTRY, game])
      : respond(String(input)))));
    const ELSEWHERE = { ...CHAT, id: 'decaf222', repository: 'game', updated: '2026-09-02T02:00:00Z' };
    const ASK = { ...CHAT, id: 'a5k0a5k0', repository: 'daoris:help', updated: '2026-09-02T00:20:00Z' };
    SESSIONS = [DRIVEN, ELSEWHERE, ASK];
    const onSelect = vi.fn();
    function Held() {
      const [intent, setIntent] = useState<'start' | 'review' | null>(null);
      const [selected, setSelected] = useState<string | null>('decaf222');
      press = () => setIntent('start');
      return (
        <WorkFrame
          selected={selected} onSelect={(id) => { onSelect(id); setSelected(id); }} notify={() => {}}
          intent={intent} onIntentTaken={() => setIntent(null)}
        />
      );
    }
    let press: () => void = () => {};
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider><Held /></Tooltip.Provider>
      </QueryClientProvider>,
    );
    await screen.findByLabelText('Message');

    act(() => press());

    expect(await screen.findByRole('button', { name: 'Repository: game' })).toBeInTheDocument();
    expect(onSelect).toHaveBeenCalledWith(null);
    await userEvent.click(screen.getByRole('button', { name: 'Repository: game' }));
    const groups = within(screen.getByRole('listbox', { name: 'Repository' })).getAllByRole('group');
    expect(groups[0]).toHaveAttribute('aria-label', 'Recent');
    // Newest first by the session's own update: game, then engine; Ask Daoris's chat is not a repository.
    expect(within(groups[0]!).getAllByRole('option').map((row) => row.textContent?.split(' ')[0])).toEqual(['game', 'engine']);
  });

  /**
   * NEW1d-b (D162): Ask Daoris's go to New conversation arrives as an EVENT too, a `compose` with an id, consumed once by that
   * id (FIX-LOG 2026-09-23): the attended session is put down, the repository chosen, the words put in above any already there,
   * and the prompt owes the focus. Nothing is sent.
   */
  it('opens the composer filled by a go, once by its id, keeping the words already typed below its own', async () => {
    const game = { ...REGISTRY[0]!, repository: 'game', root: 'C:/somewhere/game' };
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => (String(input).startsWith('/api/registry')
      ? Response.json([...REGISTRY, game])
      : respond(String(input)))));
    SESSIONS = [DRIVEN, CHAT];
    const onSelect = vi.fn();
    let go: (next: { repository?: string; sentence?: string; id: number }) => void = () => {};
    function Held() {
      const [selected, setSelected] = useState<string | null>('c0ffee11');
      const [compose, setCompose] = useState<{ repository?: string; sentence?: string; id: number } | null>(null);
      go = setCompose;
      return <WorkFrame selected={selected} onSelect={(id) => { onSelect(id); setSelected(id); }} notify={() => {}} compose={compose} />;
    }
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider><Held /></Tooltip.Provider>
      </QueryClientProvider>,
    );
    await screen.findByLabelText('Message');

    act(() => go({ repository: 'game', sentence: 'fix the build', id: 1 }));

    const prompt = await promptBox();
    expect(prompt).toHaveValue('fix the build');
    expect(await screen.findByRole('button', { name: 'Repository: game' })).toBeInTheDocument();
    await waitFor(() => expect(prompt).toHaveFocus());
    expect(onSelect).toHaveBeenCalledWith(null);
    expect(window.localStorage.getItem('daoris.start.chosen')).toBe('game');
    expect(invoke.mock.calls.some(([, type]) => type === 'START_CHAT')).toBe(false);

    // The same go again is not a new one; a new id puts its words above what is there.
    act(() => go({ sentence: 'fix the build', id: 1 }));
    expect(prompt).toHaveValue('fix the build');
    act(() => go({ sentence: 'and the tests', id: 2 }));
    await waitFor(() => expect(prompt).toHaveValue('and the tests\n\nfix the build'));
    // A go naming neither opens the composer as it was.
    act(() => go({ id: 3 }));
    expect(prompt).toHaveValue('and the tests\n\nfix the build');
    expect(screen.getByRole('button', { name: 'Repository: game' })).toBeInTheDocument();
  });

  /** The prompt takes the focus for a press, and not when nothing is attended again by another path. */
  it('takes the focus for a press, and not when it comes back by another path', async () => {
    SESSIONS = [DRIVEN, CHAT];
    let put: (id: string | null) => void = () => {};
    function Held() {
      const [chosen, setChosen] = useState<string | null>('c0ffee11');
      put = setChosen;
      return <WorkFrame selected={chosen} onSelect={setChosen} notify={() => {}} />;
    }
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider><Held /></Tooltip.Provider>
      </QueryClientProvider>,
    );
    await screen.findByLabelText('Message');

    await userEvent.click(screen.getByRole('button', { name: 'New conversation' }));
    await waitFor(async () => expect(await promptBox()).toHaveFocus());

    act(() => put('c0ffee11'));
    await screen.findByLabelText('Message');
    act(() => (document.activeElement as HTMLElement | null)?.blur());
    act(() => put(null));
    expect(await promptBox()).not.toHaveFocus();
  });

  /**
   * D162 point 2, NEW1a: the words go with the start, with a request id the driver never starts twice, and the page
   * attends what comes back. The repository is remembered as a recent start on this machine.
   */
  it('starts on the words with a request id, attends what comes back, and remembers the repository', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') return { sessionId: 'c0ffee11', sent: true, message: 'conversation `c0ffee11` starts with your words.' };
      if (type === 'HARNESSES') return ROSTER;
      return DRIVER_STATE;
    });

    const { onSelect } = show(null);
    await userEvent.type(await promptBox(), 'what is this repository for?{Enter}');

    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('c0ffee11'));
    expect(starts()).toEqual([
      { repository: 'engine', text: 'what is this repository for?', requestId: expect.stringMatching(/^[A-Za-z0-9-]{1,64}$/) },
    ]);
    expect(JSON.parse(window.localStorage.getItem('daoris.start.recent') ?? '[]')).toEqual(['engine']);
  });

  /**
   * Every choice may be left alone, and silence means the driver's own resolution rather than a
   * default this surface invented (D49 §4, D51): the omitted field IS the answer. The checkout is said
   * exactly, as false (D162 point 2).
   */
  it('carries the account and the tree chosen, and omits what was not chosen', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') return { sessionId: null, sent: false, message: 'busy' };
      if (type === 'HARNESSES') return ROSTER;
      return DRIVER_STATE;
    });

    show(null);
    const box = await promptBox();
    const pick = async (chip: RegExp, row: RegExp) => {
      (await screen.findByRole('button', { name: chip })).focus();
      await userEvent.keyboard('{Enter}');
      await userEvent.click(await screen.findByRole('menuitemradio', { name: row }));
    };
    // Signed out, so labelled so (the spawn's refusal then names the login action).
    await pick(/^Agent and account:/, /^work/);
    await pick(/^Tree:/, /^The checkout/);
    await userEvent.type(box, 'hello{Enter}');

    await waitFor(() => expect(starts()).toHaveLength(1));
    expect(starts()[0]).toEqual({
      repository: 'engine', text: 'hello', requestId: expect.any(String), profile: 'work', ownTree: false,
    });
  });

  /**
   * A refusal is the ledger's own sentence, verbatim — and nothing is attended on one: a
   * conversation that was refused has no session to show. The fixture holds a live DRIVEN session
   * in `engine`, which is the case worth pinning. The words and the choices stay (UX5 U68).
   */
  it('says a refusal under Start, whole, keeps the words, and attends nothing', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'START_CHAT'
      ? { sessionId: null, sent: false, message: '`engine` already has an active session — `s1a2b3c4` (working, quest `#abc123`).' }
      : DRIVER_STATE));
    const { onSelect, notify } = held(null);
    const box = await promptBox();
    await userEvent.type(box, 'Keep these words{Enter}');

    // UX5 U68: where Start was pressed, not a corner toast cut mid-sentence.
    expect(await within(screen.getByRole('main')).findByRole('alert'))
      .toHaveTextContent('engine already has an active session — s1a2b3c4 (working, quest #abc123).');
    expect(notify).not.toHaveBeenCalled();
    expect(onSelect).not.toHaveBeenCalled();
    expect(box).toHaveValue('Keep these words');
  });

  /**
   * NEW1a's `sent: false`: the conversation opened and the words did not reach it. The page attends it, and the words wait
   * in its box, with the driver's sentence beside them: the person's paragraph is never lost to a start (REV3).
   */
  it('attends a conversation that opened without the words, and keeps them in its box with the driver’s sentence', async () => {
    SESSIONS = [DRIVEN, CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') {
        return { sessionId: 'c0ffee11', sent: false, message: 'conversation `c0ffee11` opened, but your words did not reach it.' };
      }
      return DRIVER_STATE;
    });
    const { onSelect } = held(null);
    await userEvent.type(await promptBox(), 'what is this repository for?{Enter}');

    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('c0ffee11'));
    expect(await screen.findByLabelText('Message')).toHaveValue('what is this repository for?');
    expect(screen.getByText(/your words did not reach it/)).toBeInTheDocument();
  });

  /**
   * NEW1a: the same request id never starts twice. A press whose answer never came is pressed again with its id, so a start
   * that did open is answered rather than repeated; a press after an answer is a new start.
   */
  it('presses again with the same request id after a failure, and a new one after an answer', async () => {
    let answer = 0;
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') {
        answer += 1;
        if (answer === 1) throw new Error('the bridge did not answer');
        return { sessionId: null, sent: false, message: 'busy' };
      }
      return DRIVER_STATE;
    });
    held(null);
    const box = await promptBox();
    await userEvent.type(box, 'once{Enter}');
    expect(await within(screen.getByRole('main')).findByRole('alert')).toHaveTextContent('the bridge did not answer');

    await userEvent.click(screen.getByRole('button', { name: 'Start' }));
    await waitFor(() => expect(starts()).toHaveLength(2));
    await within(screen.getByRole('main')).findByText('busy');
    await userEvent.click(screen.getByRole('button', { name: 'Start' }));
    await waitFor(() => expect(starts()).toHaveLength(3));

    const [first, again, next] = starts().map((payload) => payload.requestId);
    expect(again).toBe(first);
    expect(next).not.toBe(first);
  });

  it('sends one message over the bridge and clears the box', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') return { sent: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'what is this repository for?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 'c0ffee11', text: 'what is this repository for?' },
    });
    expect((box as HTMLTextAreaElement).value).toBe('');
  });

  /**
   * LOG1b (D94): a message sent is counted in the machine log, its length and how many files it
   * carried, and its words go only where they always went.
   */
  it('counts a message it sends in the machine log, and never its words', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') return { sent: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    await userEvent.type(await screen.findByLabelText('Message'), 'what is this repository for?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    const logged = invoke.mock.calls.filter(([module]) => module === 'DAORIS.LOG');
    expect(logged).toEqual([['DAORIS.LOG', 'EVENT', {
      payload: { event: 'message.sent', data: { session: 'c0ffee11', kind: 'chat', length: 28, files: 0 } },
    }]]);
    expect(JSON.stringify(logged)).not.toContain('repository for');
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
    await userEvent.upload(await screen.findByLabelText('Choose files…'), new File(['exit 3'], 'run.log', { type: 'text/plain' }));
    const box = screen.getByLabelText('Message');
    await userEvent.type(box, 'a long paragraph worth keeping');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText(/went nowhere/)).toBeTruthy();
    await waitFor(() => expect((box as HTMLTextAreaElement).value).toBe('a long paragraph worth keeping'));
    expect(notify).toHaveBeenCalledWith(expect.stringContaining('run.log'), 'error');
  });

  /**
   * SENDLOST1b: on a loaded machine the page stopped waiting on a chat's send (the bridge's TIMEOUT): the person's words
   * vanished behind the error, though the driver may still take them. They show the moment they are sent, as sending; a send
   * the page stopped waiting on keeps them, saying the machine has not answered yet, with nothing put back in the box to send
   * twice, until the conversation's queue holds them.
   */
  it('shows a chat\'s words as sending, keeps them when the send times out, and lets them go once its queue holds them', async () => {
    SESSIONS = [CHAT];
    let stop: () => void = () => {};
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: true };
      if (type === 'SESSION_INPUT') return new Promise((_, reject) => { stop = () => reject(timedOut()); });
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('c0ffee11', notify);

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'and the line?{Enter}');
    expect(await screen.findByRole('list', { name: 'sending…' })).toHaveTextContent('and the line?');

    await act(async () => { stop(); });
    expect(await screen.findByRole('list', { name: UNANSWERED })).toHaveTextContent('and the line?');
    expect(box).toHaveValue('');
    expect(notify).not.toHaveBeenCalledWith(expect.anything(), 'error');

    // The driver took them after all: they wait behind the turn as any word does, and are not said twice.
    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_QUEUED')!({
        session: 'c0ffee11', queued: [{ id: 'q1', text: 'and the line?', files: [] }], taking: true,
      });
    });
    expect(screen.queryByRole('list', { name: UNANSWERED })).toBeNull();
    expect(screen.getByRole('list', { name: 'waiting for this turn to end' })).toHaveTextContent('and the line?');
    expect(inputs()).toHaveLength(1);
  });

  /** SENDLOST1b: words whose send timed out to a chat that then ended without them never arrived, so they come back, said so. */
  it('gives a chat\'s words back, said so, once the conversation ended without them', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') {
        SESSIONS = [{ ...CHAT, state: 'completed' }];
        throw timedOut();
      }
      return DRIVER_STATE;
    });
    show('c0ffee11');

    await userEvent.type(await screen.findByLabelText('Message'), 'are you there?{Enter}');

    expect(await screen.findByText('The conversation ended before this reached it. It is back in the box.')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue('are you there?'));
    expect(screen.queryByRole('list', { name: UNANSWERED })).toBeNull();
    expect(inputs()).toHaveLength(1);
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
    // REV3 chat F8: another Daoris process on this machine runs it — a terminal's — and the record
    // still says working, so "its record says how it ended" was untrue.
    [{ stopped: false, elsewhere: true }, "session c0ffee11 is run by another Daoris process on this machine, a terminal's — stop it there."],
  ])('says what the stop did when the driver answers %j', async (answer, sentence) => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'STOP_SESSION' ? answer : DRIVER_STATE));
    const notify = vi.fn();

    show('c0ffee11', notify);
    // The stop is the page header's, and asks once (SESSUX1d, D126 §3.3).
    await userEvent.click(await screen.findByRole('button', { name: 'Stop…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Stop session' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(sentence));
  });

  /**
   * Finish and stop are still two endings, and never one button (SES2): *Finish* stays the composer's, the chat's own
   * ending (D49), and the session's stop is the page header's, its one owner (D126 §3.3), which says what Finish does
   * instead before it stops anything.
   */
  it('offers finishing in the composer and stopping in the page header, apart', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'END_CHAT') return { ended: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    const box = (await screen.findByLabelText('Message')).closest('form')!;
    expect(within(box).queryByRole('button', { name: 'Stop' })).toBeNull();
    await userEvent.click(within(box).getByRole('button', { name: 'Finish' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'END_CHAT', { payload: { id: 'c0ffee11' } });

    await userEvent.click(screen.getByRole('button', { name: 'Stop…' }));
    expect(screen.getByRole('group', { name: 'stop this session' })).toHaveTextContent(/Finish lets its agent wind up instead/);
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', expect.anything());
  });

  /**
   * CTX1 (D138, design §4): a right-click on the attended session's page offers its header's acts, the loud act and
   * *Stop…* with its ⋯'s, each carried out by the one owner: *Stop…* asks under the header as its button does.
   */
  it('offers the page header’s acts on a right-click anywhere on the session’s page', async () => {
    SESSIONS = [CHAT];
    show('c0ffee11');
    render(<ContextMenus doors={{ copy: () => {} }} />);

    const title = await screen.findByRole('heading', { level: 1 });
    rightClick(title);
    const acts = await menuActs();
    expect(acts[0]).toBe('Stop…');
    expect(acts).toContain('Copy session ID');
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());

    // Below the header, on the page itself, the same acts.
    rightClick(title.closest('main')!);
    expect(await menuActs()).toEqual(acts);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Stop…' }));
    expect(await screen.findByRole('group', { name: 'stop this session' })).toBeInTheDocument();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', expect.anything());
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
    await userEvent.type(screen.getByLabelText('Message'), 'and push nothing');
    await userEvent.click(await screen.findByRole('button', { name: 'Stop turn' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', { payload: { id: 'c0ffee11' } });
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue('and then test it\n\nand push nothing'));
    expect(notify).toHaveBeenCalledWith(
      'Asked the agent to stop this turn; the conversation stays open. 1 waiting message came back to the box, unsent.');
  });

  /** QUEUEEDIT1b: a waiting message is removed over the driver by its id, and a reword names the session, the id and the text. */
  it('removes and rewords a waiting message over the driver, by its id', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [{ id: 'q1', text: 'and then test it', files: [] }], taking: true };
      if (type === 'SESSION_QUEUE_EDIT') return { done: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    await userEvent.click(await screen.findByRole('button', { name: 'Remove the waiting message: and then test it' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE_EDIT', { payload: { session: 'c0ffee11', id: 'q1' } });

    await userEvent.click(screen.getByRole('button', { name: 'Edit the waiting message: and then test it' }));
    await userEvent.type(screen.getByLabelText('Message'), ' twice');
    await userEvent.keyboard('{Enter}');
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE_EDIT', {
      payload: { session: 'c0ffee11', id: 'q1', text: 'and then test it twice' },
    });
  });

  /**
   * SAIDBACK1b (D76): a parked conversation's composer lists the words waiting on its record as what the same session goes on
   * with, never as waiting for a turn or a door, and offers each one's *Remove* by its id.
   */
  it('lists the words waiting on a parked conversation as what it goes on with', async () => {
    SESSIONS = [{ ...CHAT, state: 'awaiting-person' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [{ id: 'w1', text: 'use the blue one', files: [] }], taking: false };
      if (type === 'SESSION_QUEUE_EDIT') return { done: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    // Folded to one line with its count inside the answer box's frame (WAITFOOT2c), a press opens it.
    await userEvent.click(await screen.findByRole('button', { name: 'Show them' }));
    expect(await screen.findByRole('list', { name: 'waiting for the same session to go on with them' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Remove the waiting message: use the blue one' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE_EDIT', { payload: { session: 'c0ffee11', id: 'w1' } });
  });

  // AGT6b (D98): one conversation's model and effort, as its agent offered them on the protocol door —
  // asked of the driver once, followed as news, and changed over it. The console's `/model`, typed as a
  // message, cut a turn short; this is its door.
  const MODEL = {
    id: 'model', name: 'Model', category: 'model', current: 'default',
    choices: [{ value: 'default', name: 'Default (recommended)' }, { value: 'sonnet', name: 'Sonnet' }],
  };
  const EFFORT = {
    id: 'effort', name: 'Effort', category: 'thought_level', current: 'high',
    choices: [{ value: 'low', name: 'Low' }, { value: 'high', name: 'High' }],
  };

  it("offers a conversation's model and effort as its agent offered them, and sets one over the driver", async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: false };
      if (type === 'SESSION_OPTIONS') return { session: 'c0ffee11', options: [MODEL, EFFORT] };
      if (type === 'SET_SESSION_OPTION') return { session: 'c0ffee11', options: [{ ...MODEL, current: 'sonnet' }, EFFORT] };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    const model = await screen.findByRole('combobox', { name: "this conversation's Model" });
    expect(model).toHaveTextContent('Default (recommended)');
    expect(screen.getByRole('combobox', { name: "this conversation's Effort" })).toHaveTextContent('High');

    model.focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'Sonnet' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_SESSION_OPTION', {
      payload: { id: 'c0ffee11', option: 'model', value: 'sonnet' },
    });
    await waitFor(() => expect(screen.getByRole('combobox', { name: "this conversation's Model" })).toHaveTextContent('Sonnet'));
  });

  it("follows the agent's own change to its options as news", async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: false };
      if (type === 'SESSION_OPTIONS') return { session: 'c0ffee11', options: [MODEL] };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    await screen.findByRole('combobox', { name: "this conversation's Model" });
    await waitFor(() => expect(eventHandlers.has('DAORIS.SESSION_OPTIONS_CHANGED')).toBe(true));
    act(() => eventHandlers.get('DAORIS.SESSION_OPTIONS_CHANGED')!({
      session: 'c0ffee11',
      options: [{ ...MODEL, current: 'haiku', choices: [...MODEL.choices, { value: 'haiku', name: 'Haiku' }] }],
    }));

    await waitFor(() => expect(screen.getByRole('combobox', { name: "this conversation's Model" })).toHaveTextContent('Haiku'));
  });

  it('offers nothing where the agent offered nothing, and asks nothing of a driven session', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: false };
      if (type === 'SESSION_OPTIONS') return { session: 'c0ffee11', options: [] };
      return DRIVER_STATE;
    });

    const { unmount } = show('c0ffee11');
    await screen.findByLabelText('Message');
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_OPTIONS', { payload: { id: 'c0ffee11' } }));
    expect(screen.queryByRole('combobox', { name: "this conversation's Model" })).not.toBeInTheDocument();
    unmount();

    invoke.mockClear();
    SESSIONS = [DRIVEN];
    show('s1a2b3c4');
    await screen.findAllByText(/Expose a streaming budget/);
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_OPTIONS', expect.anything());
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
    await userEvent.upload(await screen.findByLabelText('Choose files…'), new File(['exit 3'], 'run.log', { type: 'text/plain' }));
    await userEvent.type(screen.getByLabelText('Message'), 'what does this say?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 'c0ffee11', text: 'what does this say?', files: [{ name: 'run.log', content: btoa('exit 3') }] },
    }));
  });

  /**
   * CONV4d: the tree's files are asked for only once a mention is being written, so a conversation
   * nobody mentions a file in costs no `git` listing at all — and then they are offered under the `@`.
   */
  it('asks the driver for the tree\'s files only once an @ is written, and offers them', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_FILES') return { session: 'c0ffee11', files: ['README.md', 'src/engine.cs'], unlisted: 0 };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    await userEvent.type(await screen.findByLabelText('Message'), 'look at ');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_FILES', expect.anything());

    await userEvent.type(screen.getByLabelText('Message'), '@eng');
    await userEvent.click(await screen.findByRole('option', { name: 'src/engine.cs' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_FILES', { payload: { id: 'c0ffee11' } });
    expect(screen.getByLabelText('Message')).toHaveValue('look at @src/engine.cs ');
  });

  /**
   * CONV5: the context ring under the composer reads the conversation's own record — the one the
   * conversation above it is drawn from, never a second fetch — and follows it live.
   */
  it('shows how full the conversation\'s context is, from its record, and follows it live', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_HISTORY') {
        return {
          session: 'c0ffee11', earlier: false, latest: 2,
          events: [
            { seq: 1, at: '2026-09-26T10:00:00Z', kind: 'user', origin: 'person', text: 'read it' },
            { seq: 2, at: '2026-09-26T10:00:02Z', kind: 'usage', used: 34_120, size: 1_000_000 },
          ],
        };
      }
      return DRIVER_STATE;
    });

    show('c0ffee11');
    expect(await screen.findByRole('meter', { name: 'context' })).toHaveTextContent('3%');

    await waitFor(() => expect(eventHandlers.has('DAORIS.SESSION_EVENTS')).toBe(true));
    act(() => eventHandlers.get('DAORIS.SESSION_EVENTS')!({
      session: 'c0ffee11', events: [{ seq: 3, at: '2026-09-26T10:00:03Z', kind: 'usage', used: 850_000, size: 1_000_000 }],
    }));
    expect(await screen.findByText('85%')).toBeInTheDocument();
  });

  /**
   * The agent's words after its turn ended (its background work finished) carry no *working…* while
   * the driver says no turn is in flight, which looking at CONSOLE2 found under them for good.
   */
  it('says nothing is working under words the agent said after its turn, when no turn is in flight', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: false };
      if (type === 'SESSION_HISTORY') {
        return {
          session: 'c0ffee11', earlier: false, latest: 4,
          events: [
            { seq: 1, at: '2026-09-28T10:00:00Z', kind: 'user', origin: 'person', text: 'start the ticker' },
            { seq: 2, at: '2026-09-28T10:00:02Z', kind: 'message', id: 'msg_1', text: 'DONE' },
            { seq: 3, at: '2026-09-28T10:00:03Z', kind: 'turn', stopReason: 'end_turn' },
            { seq: 4, at: '2026-09-28T10:00:30Z', kind: 'message', id: 'msg_2', text: 'The background ticker finished.' },
          ],
        };
      }
      return DRIVER_STATE;
    });

    show('c0ffee11');
    expect(await screen.findByText('The background ticker finished.')).toBeInTheDocument();
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE', expect.anything()));
    await waitFor(() => expect(screen.queryByText('working…')).toBeNull());
  });

  /** A structured door that has not reported yet says so, never 0%. */
  it('says a conversation has not reported its context yet, rather than showing it empty', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'HARNESSES' ? STRUCTURED_ROSTER : DRIVER_STATE));

    show('c0ffee11');
    expect(await screen.findByRole('img', { name: 'context: not measured' })).toBeInTheDocument();
    expect(screen.queryByText('0%')).not.toBeInTheDocument();
  });

  /** Unlisted is information: the host's sentence, where the files would be, and the typed path still goes. */
  it('says why the tree cannot be listed, in the host\'s words, where the files would be', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type !== 'SESSION_FILES') return DRIVER_STATE;
      throw Object.assign(new Error('fallback'), {
        code: 'SESSION_TREE_UNLISTED', parameters: { session: 'c0ffee11' },
      });
    });

    show('c0ffee11');
    await userEvent.type(await screen.findByLabelText('Message'), '@eng');

    expect(await screen.findByText(/A path you type after @ still reaches the agent/)).toBeInTheDocument();
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
    await userEvent.click(await screen.findByRole('button', { name: 'Stop turn' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      '1 waiting message came back to the box, unsent. Not sent with it: plan.md, shot.png — attach them again.'));
    expect(screen.getByLabelText('Message')).toHaveValue('then this');
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
    expect(await screen.findByRole('button', { name: 'Send' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Stop turn' })).not.toBeInTheDocument();

    await waitFor(() => expect(eventHandlers.has('DAORIS.SESSION_QUEUED')).toBe(true));
    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 'c0ffee11', queued: [], taking: true }));
    expect(await screen.findByRole('button', { name: 'Stop turn' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Queue' })).toBeInTheDocument();

    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 'c0ffee11', queued: [], taking: false }));
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Stop turn' })).not.toBeInTheDocument());
  });

  /**
   * UX5 U17, decided by the reference console: a live chat between turns is idle, in the rail and
   * the head, and working again the moment the driver says a turn is in flight. It read *working*
   * the whole time, as a turn does.
   */
  it('says a live chat is idle between turns, in the rail and the head, as the driver says', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: false };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    const rail = await screen.findByRole('navigation', { name: 'Sessions' });
    await waitFor(() => expect(within(rail).getByText('idle')).toBeInTheDocument());
    // The word is the page header's since SESSUX1d (D126 §3.2): its pill moved up from the record head.
    const head = screen.getByRole('heading', { level: 1 }).closest('header')!;
    expect(within(head).getByText('idle')).toBeInTheDocument();

    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 'c0ffee11', queued: [], taking: true }));
    await waitFor(() => expect(within(rail).getByText('working')).toBeInTheDocument());
    expect(within(head).getByText('working')).toBeInTheDocument();
    // The composer follows the same answer: a turn in flight offers its stop.
    expect(screen.getByRole('button', { name: 'Stop turn' })).toBeInTheDocument();
  });

  /** CONV4b: each conversation keeps its own draft as the person moves between them. */
  it('keeps each conversation its own draft', async () => {
    const OTHER = { ...CHAT, id: 'decaf222', repository: 'game' };
    SESSIONS = [CHAT, OTHER];
    const view = show('c0ffee11');
    await userEvent.type(await screen.findByLabelText('Message'), 'half a thought for the engine');

    view.rerender(
      <QueryClientProvider client={view.client}>
        <Hub />
        <Tooltip.Provider>
          <WorkFrame selected="decaf222" onSelect={view.onSelect} notify={() => {}} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue(''));

    view.rerender(
      <QueryClientProvider client={view.client}>
        <Hub />
        <Tooltip.Provider>
          <WorkFrame selected="c0ffee11" onSelect={view.onSelect} notify={() => {}} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue('half a thought for the engine'));
  });

  /**
   * A driven session holds a tree but was given its whole target at once — there is no channel to
   * speak into, so it gets no composer. An input box nothing is listening to is worse than none.
   */
  it('offers no composer for a driven session', async () => {
    show('s1a2b3c4');

    await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' });
    expect(screen.queryByLabelText('Message')).toBeNull();
  });

  /**
   * SESS3, COWORK1a: a driven session the driver says listens — the protocol door — has a box; what is sent goes at the
   * person's default, waiting for its turn's end on a door that takes words only then. No word stops the turn: the keeping stop
   * beside what waits does, asked once (`CANCEL_TURN` with `keep`). One the driver says does not listen keeps none.
   */
  it('lets the person tell a working driven session something, held for its turn\'s end, and stop its turn on their own press', async () => {
    const reach = { reaches: 'turn-end', steers: false, keepingStop: true };
    let queue: object = { session: 's1a2b3c4', queued: [], taking: true, listening: true, reach };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUEUE') return queue;
      if (type === 'SESSION_INPUT') return { sent: true, reaches: 'turn-end' };
      if (type === 'CANCEL_TURN') return { cancelled: true, withdrawn: [] };
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const box = await screen.findByLabelText('Message');
    expect(box).toHaveAttribute('placeholder', expect.stringMatching(/tell it something while it works/));
    await userEvent.type(box, 'the budget is in level.json');
    await userEvent.click(await screen.findByRole('button', { name: 'Send' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', { payload: { id: 's1a2b3c4', text: 'the budget is in level.json' } });

    // What the driver holds shows as waiting, and only then is the keeping stop offered; never a word's own press.
    expect(screen.queryByRole('button', { name: 'Send now' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Stop its turn so this goes next…' })).toBeNull();
    queue = { session: 's1a2b3c4', queued: [{ id: 'said-1', text: 'the budget is in level.json', files: [], level: 'urgent' }], taking: true, listening: true };
    await act(async () => { eventHandlers.get('DAORIS.SESSION_QUEUED')!(queue); });
    expect(screen.getByText('the budget is in level.json')).toBeInTheDocument();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', expect.anything());

    await userEvent.click(screen.getByRole('button', { name: 'Stop its turn so this goes next…' }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', expect.anything());
    await userEvent.click(screen.getByRole('button', { name: 'Stop its turn' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', { payload: { id: 's1a2b3c4', keep: true } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Asked the agent to stop this turn; the conversation stays open.'));

    // It stops listening as it ends: the box goes.
    await act(async () => { eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 's1a2b3c4', queued: [], taking: false, listening: false }); });
    expect(screen.queryByLabelText('Message')).toBeNull();
  });

  /** A Radix menu opens on the keyboard, as the other pages' tests open theirs. */
  const openMenu = async (name: string) => {
    const user = userEvent.setup();
    (await screen.findByRole('button', { name })).focus();
    await user.keyboard('{Enter}');
  };

  /** Words at a level, chosen from the menu beside *Send* (COWORK1a). */
  const sendAt = async (item: RegExp) => {
    await openMenu('When it reads this');
    await userEvent.click(await screen.findByRole('menuitem', { name: item }));
  };

  /**
   * STEER2b, COWORK1a: *Now, cutting its step*, from the level menu beside *Send*, sends the typed words with `level: now`
   * (and `now: true` for a shell from before). The answer says what came of it: taken at once in the running turn (nothing to
   * say), or that the door could not, the words going at its next step or held for its turn's end, said in one line. No turn is
   * stopped for them.
   */
  it('sends typed words to a working driven session now, and says what the door did', async () => {
    let answer: object = { sent: true, reaches: 'now', why: null, now: true };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUEUE') {
        return { session: 's1a2b3c4', queued: [], taking: true, listening: true, reach: { reaches: 'next-step', steers: true, keepingStop: true } };
      }
      if (type === 'SESSION_INPUT') return answer;
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'drop the cache');
    await sendAt(/Now, cutting its step/);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 's1a2b3c4', text: 'drop the cache', level: 'now', now: true },
    });
    expect(box).toHaveValue('');
    expect(notify).not.toHaveBeenCalled();

    answer = { sent: true, reaches: 'turn-end', why: 'no-interrupt', now: false };
    await userEvent.type(box, 'and the tests');
    await sendAt(/Now, cutting its step/);
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Its door cannot take words in a running turn, so these are held for its turn to end.'));
    // Held, not lost: nothing is put back in the box.
    expect(box).toHaveValue('');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', expect.anything());

    // No turn was running: an ordinary send, with nothing said.
    notify.mockClear();
    answer = { sent: true, reaches: null, why: null, now: true };
    await userEvent.type(box, 'idle');
    await sendAt(/Now, cutting its step/);
    await waitFor(() => expect(box).toHaveValue(''));
    expect(notify).not.toHaveBeenCalled();
  });

  // Each code has its line: a next-step door that never steers, a session still opening, and a code not known
  // (one case per code, WEBSPEED1: the four sends in one test were most of that test's four seconds).
  it.each<[object, string]>([
    [{ sent: true, reaches: 'next-step', why: 'no-interrupt', now: false }, 'Its agent cannot be steered mid-turn, so these go at its next step.'],
    [{ sent: true, reaches: 'next-step', why: 'behind', now: false }, 'Other words are already on their way to it, so these wait behind them.'],
    [{ sent: true, reaches: 'resume', why: 'opening', now: false }, 'It is still opening, so these go as soon as it has opened.'],
    [{ sent: true, reaches: 'turn-end', why: 'something-new', now: false }, 'It could not take these now, so they are held as queued words are.'],
  ])('says in one line what a working driven session did with words sent now: %j', async (reply, line) => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUEUE') {
        return { session: 's1a2b3c4', queued: [], taking: true, listening: true, reach: { reaches: 'next-step', steers: true, keepingStop: true } };
      }
      if (type === 'SESSION_INPUT') return reply;
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'more');
    await sendAt(/Now, cutting its step/);
    await waitFor(() => expect(notify).toHaveBeenCalledWith(line));
    // Held, not lost: nothing is put back in the box.
    expect(box).toHaveValue('');
  });

  /**
   * COWORK1a: a conversation's words go at the level chosen from the menu (`level: ordinary` here); with words waiting for the
   * turn's end on a door that can stop a turn keeping the conversation, the keeping stop stops it (`CANCEL_TURN` with `keep`),
   * withdrawing nothing, once asked. *Stop turn* stays the withdrawal, beside it.
   */
  it('sends a conversation’s words at a level, and stops its turn keeping what waits on the person’s own press', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') {
        return {
          session: 'c0ffee11', queued: [{ id: 'q1', text: 'and then test it', files: [], level: 'urgent' }], taking: true,
          reach: { reaches: 'turn-end', steers: false, keepingStop: true },
        };
      }
      if (type === 'SESSION_INPUT') return { sent: true, reaches: 'turn-end', why: null, now: false };
      if (type === 'CANCEL_TURN') return { cancelled: true, withdrawn: [] };
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('c0ffee11', notify);

    await screen.findByRole('list', { name: 'waiting for this turn to end' });
    expect(screen.getByRole('button', { name: 'Stop turn' })).toBeInTheDocument();
    await userEvent.click(await screen.findByRole('button', { name: 'Stop its turn so this goes next…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Stop its turn' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', { payload: { id: 'c0ffee11', keep: true } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Asked the agent to stop this turn; the conversation stays open.'));
    expect(screen.getByLabelText('Message')).toHaveValue('');

    await userEvent.type(screen.getByLabelText('Message'), 'use the blue one');
    await sendAt(/When its turn ends/);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', { payload: { id: 'c0ffee11', text: 'use the blue one', level: 'ordinary' } });
  });

  /**
   * SENDLOST1b: words to a working driven session's running door show as sending; a send the page stopped waiting on keeps
   * them, saying the machine has not answered yet, with no error and nothing back in the box, until the driver holds them.
   */
  it('keeps words to a working driven session whose send timed out, until the driver holds them behind its turn', async () => {
    let stop: () => void = () => {};
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUEUE') return { session: 's1a2b3c4', queued: [], taking: true, listening: true };
      if (type === 'SESSION_INPUT') return new Promise((_, reject) => { stop = () => reject(timedOut()); });
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'the budget is in level.json');
    await userEvent.click(screen.getByRole('button', { name: 'Queue' }));
    expect(await screen.findByRole('list', { name: 'sending…' })).toHaveTextContent('the budget is in level.json');

    await act(async () => { stop(); });
    expect(await screen.findByRole('list', { name: UNANSWERED })).toHaveTextContent('the budget is in level.json');
    expect(box).toHaveValue('');
    expect(notify).not.toHaveBeenCalledWith(expect.anything(), 'error');

    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_QUEUED')!({
        session: 's1a2b3c4', queued: [{ id: 'said-1', text: 'the budget is in level.json', files: [] }], taking: true, listening: true,
      });
    });
    expect(screen.queryByRole('list', { name: UNANSWERED })).toBeNull();
    expect(screen.getByRole('list', { name: 'waiting for this turn to end' })).toHaveTextContent('the budget is in level.json');
    expect(inputs()).toHaveLength(1);
  });

  it('offers no box to a driven session the driver says nothing could hear', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_QUEUE'
      ? { session: 's1a2b3c4', queued: [], taking: false, listening: false }
      : DRIVER_STATE));
    show('s1a2b3c4');

    await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE', { payload: { id: 's1a2b3c4' } }));
    expect(screen.queryByLabelText('Message')).toBeNull();
  });
});
