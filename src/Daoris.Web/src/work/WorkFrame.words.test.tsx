import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The box on every session that takes words, and clearing a parked session, in the Work frame, one part of its suite
// (WEBSPEED1 cut the one file, which ran 181 s alone, into parts that run beside each other). The bridge is mocked as
// present, because the frame does not exist without one (D55); what the parts share is in `test/workFrameBridge.ts`
// and `test/workFrameKit.tsx`.
vi.mock('@shenora/react', async () => (await import('../test/workFrameBridge')).shenoraMock());

import { eventHandlers, invoke } from '../test/workFrameBridge';
import {
  CHAT, DRIVEN, DRIVER_STATE, PARKED, PARKED_INTAKE, QUESTS, REGISTRY, ROSTER, RUNNING_INTAKE, UNANSWERED, show, timedOut,
  userEvent,
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
 * MSG1f (D137 §5.1): the box on every session that takes words, by what a word said now would do (`SESSION_QUEUE`'s
 * `reaches` and `why`, MSG1d), and the line saying why on the rest; the words shown at once, where they went, and the one
 * press where they cannot go on; *Send back…* opening the box.
 */
describe('the box on every session that takes words', () => {
  const ENDED = { ...DRIVEN, state: 'completed', note: 'Done; committed as a1b2c3d.' };

  /** A driver that answers `SESSION_QUEUE` with these, `SESSION_INPUT` with `said`, and the rest as the frame needs. */
  const driver = (reach: object, said: object = { sent: true, reaches: 'resume' }, more: Record<string, unknown> = {}) =>
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type in more) return more[type];
      if (type === 'SESSION_QUEUE') return { session: 's1a2b3c4', queued: [], taking: false, ...reach };
      if (type === 'SESSION_INPUT') return said;
      return DRIVER_STATE;
    });

  beforeEach(() => {
    SESSIONS = [ENDED];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    window.localStorage.removeItem('daoris.drafts');
    window.localStorage.removeItem('daoris.dockClosed');
  });

  /** §2.2: a session that ended goes on with the person's words, and the box says so; the log counts where they reach. */
  it('offers a session that ended the box where the same session goes on, and says through it', async () => {
    driver({ reaches: 'resume' });
    show('s1a2b3c4');

    const box = await screen.findByLabelText('Message');
    expect(box).toHaveAttribute('placeholder', 'write to it — the same session goes on with your words');
    expect(screen.queryByRole('button', { name: 'Attach files' })).toBeNull();
    await userEvent.type(box, 'also say so in the release notes');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 's1a2b3c4', text: 'also say so in the release notes' },
    });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.LOG', 'EVENT', {
      payload: { event: 'message.sent', data: { session: 's1a2b3c4', kind: 'say', length: 32, files: 0, reach: 'resume' } },
    }));
    expect((box as HTMLTextAreaElement).value).toBe('');
  });

  /** Nothing known is nothing claimed: a driver that answers no reach (an older shell) offers an ended session no box. */
  it('offers an ended session no box until the driver says words go on', async () => {
    driver({});
    show('s1a2b3c4');

    await screen.findByRole('heading', { level: 1 });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE', { payload: { id: 's1a2b3c4' } }));
    expect(screen.queryByLabelText('Message')).toBeNull();
  });

  /** MSG1d: where nothing takes words the driver says why by a code, and the page draws the line instead of the box. */
  it('draws the line the driver’s code names instead of a box', async () => {
    driver({ why: 'superseded' });
    show('s1a2b3c4');

    expect(await screen.findByText('#abc123 went on in a later session here, so write to that one.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Message')).toBeNull();
  });

  /**
   * HANDWORDS1-ii (D165's HANDWORDS1-i note): a conversation handed over or moved names the one it went on in by that record's
   * title, and *Open session* attends it.
   */
  it('names the conversation a moved one went on in by its title, and attends it', async () => {
    const old = { ...CHAT, state: 'completed' };
    SESSIONS = [old, { ...CHAT, id: 'c2c2c2c2', from: 'c0ffee11', fromWhy: 'curve', fromBy: 'daoris' }];
    driver({}, undefined, { SESSION_QUEUE: { session: 'c0ffee11', queued: [], taking: false, why: 'continued', to: 'c2c2c2c2' } });
    const { onSelect } = show('c0ffee11');

    const line = await screen.findByText('This conversation went on in Chat.');
    expect(screen.queryByText(/say it to that one/)).toBeNull();
    fireEvent.click(within(line.parentElement!).getByRole('button', { name: 'Open session' }));
    expect(onSelect).toHaveBeenCalledWith('c2c2c2c2');
  });

  it('draws the line under a teammate’s record without asking the driver', async () => {
    SESSIONS = [{ ...ENDED, id: 'person@machine-b/s1a2b3c4' }];
    driver({ reaches: 'resume' });
    show('person@machine-b/s1a2b3c4');

    expect(await screen.findByText('This session ran on another machine, where its conversation is.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Message')).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE', expect.anything());
  });

  /** REV3, MSG1d: words nothing took come back into the box, and the line says why in the page's words. */
  it('hands back words nothing took, saying why by the driver’s code', async () => {
    driver({ reaches: 'resume' }, { sent: false, why: 'stood-down' });
    show('s1a2b3c4');

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'one more thing');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText('It stood down: #abc123 is someone else\'s, so it has nothing to go on with.')).toBeInTheDocument();
    await waitFor(() => expect((box as HTMLTextAreaElement).value).toBe('one more thing'));
  });

  /** D137 §2.4: a driven session on the native door takes its words as one argument, so longer ones are refused first. */
  it('refuses words longer than the native door takes, before sending, and keeps them', async () => {
    const PIPE = { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], wire: 'pipe' }] };
    driver({ reaches: 'resume' }, undefined, { HARNESSES: PIPE });
    show('s1a2b3c4');

    const box = await screen.findByLabelText('Message');
    const words = 'x'.repeat(24_001);
    fireEvent.change(box, { target: { value: words } });
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText(/takes at most 24000 characters at once/)).toBeInTheDocument();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());
    expect((box as HTMLTextAreaElement).value).toBe(words);
  });

  /** D137 §2.1: words said as a session winds up wait for its record to end, then reopen it — never refused. */
  it('holds words said as a session winds up above its box', async () => {
    SESSIONS = [DRIVEN];
    driver({ reaches: 'resume' });
    show('s1a2b3c4');

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'keep the old flag for a release');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText('held until this run ends — then the same session goes on with them')).toBeInTheDocument();
    expect(screen.getByText('keep the old flag for a release')).toBeInTheDocument();
  });

  /**
   * SENDLOST1b: words to a session that ended, whose send the page stopped waiting on, stay above its box saying the machine
   * has not answered yet, with no error and nothing back in the box, until its record holds them.
   */
  it('keeps words to a session that ended whose send timed out, until its record holds them', async () => {
    const words = 'also say so in the release notes';
    let stop: () => void = () => {};
    let recorded = false;
    let read: () => void = () => {};
    const reading = new Promise<void>((resolve) => { read = resolve; });
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUEUE') return { session: 's1a2b3c4', queued: [], taking: false, reaches: 'resume' };
      if (type === 'SESSION_INPUT') {
        return new Promise((_, reject) => { stop = () => { recorded = true; reject(timedOut()); }; });
      }
      if (type === 'SESSION_HISTORY' && recorded) {
        await reading;
        return {
          session: 's1a2b3c4', earlier: false, latest: 1,
          events: [{ seq: 1, at: new Date().toISOString(), kind: 'user', origin: 'person', text: words }],
        };
      }
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, words);
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    expect(await screen.findByRole('list', { name: 'sending…' })).toHaveTextContent(words);

    await act(async () => { stop(); });
    expect(await screen.findByRole('list', { name: UNANSWERED })).toHaveTextContent(words);
    expect(box).toHaveValue('');
    expect(notify).not.toHaveBeenCalledWith(expect.anything(), 'error');

    await act(async () => { read(); });
    await waitFor(() => expect(screen.queryByRole('list', { name: UNANSWERED })).toBeNull());
    expect(screen.getByLabelText('Message')).toHaveValue('');
    expect(inputs()).toHaveLength(1);
  });

  /**
   * SENDLOST1c: the page stopped waiting on a send, and the driver then says nothing takes words (the box becomes the line):
   * the words are not left *not answered yet*, they are said not to have gone and are kept for the box.
   */
  it('says words whose send timed out did not go when the reach turns to a code, and keeps them', async () => {
    const words = 'also say so in the release notes';
    let stop: () => void = () => {};
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUEUE') return { session: 's1a2b3c4', queued: [], taking: false, reaches: 'resume' };
      if (type === 'SESSION_INPUT') return new Promise((_, reject) => { stop = () => reject(timedOut()); });
      if (type === 'SESSION_HISTORY') return { session: 's1a2b3c4', earlier: false, latest: 0, events: [] };
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, words);
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    await act(async () => { stop(); });
    expect(await screen.findByRole('list', { name: UNANSWERED })).toHaveTextContent(words);

    // The shell tells the session's reach live: nothing takes words for it now.
    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_QUEUED')!({
        session: 's1a2b3c4', queued: [], taking: false, listening: false, reach: { why: 'stood-down' },
      });
    });
    expect(await screen.findByText(/did not go/)).toHaveTextContent(words);
    expect(screen.queryByRole('list', { name: UNANSWERED })).toBeNull();
    expect(inputs()).toHaveLength(1);
  });

  /**
   * SAIDBACK1b (D76): the words waiting on a session that ended are listed above its box by their ids, taken back or reworded
   * over the driver, and the list is read again from the queue the shell tells, where a reword's new id stands for the old.
   */
  it('lists the words waiting on a session that ended, and removes or rewords them over the driver', async () => {
    driver(
      { reaches: 'resume', queued: [{ id: 'w1', text: 'use the blue one', files: [] }, { id: 'w2', text: 'and the red', files: [] }] },
      undefined,
      { SESSION_QUEUE_EDIT: { done: true } },
    );
    show('s1a2b3c4');

    expect(await screen.findByRole('list', { name: 'waiting for the same session to go on with them' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Remove the waiting message: use the blue one' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE_EDIT', { payload: { session: 's1a2b3c4', id: 'w1' } });

    await userEvent.click(screen.getByRole('button', { name: 'Edit the waiting message: and the red' }));
    const box = screen.getByLabelText('Message');
    await userEvent.clear(box);
    await userEvent.type(box, 'use the green one');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE_EDIT', {
      payload: { session: 's1a2b3c4', id: 'w2', text: 'use the green one' },
    });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());

    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({
      session: 's1a2b3c4', queued: [{ id: 'w3', text: 'use the green one', files: [] }], taking: false, reach: { reaches: 'resume' },
    }));
    expect(await screen.findByRole('button', { name: 'Remove the waiting message: use the green one' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remove the waiting message: use the blue one' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Remove the waiting message: and the red' })).toBeNull();
  });

  /** §3.2: a record going on with the person's words shows it, while its run opens with them. */
  it('shows a record going on with the person’s words as going on', async () => {
    SESSIONS = [{ ...DRIVEN, answer: 'also cap it' }];
    driver({});
    show('s1a2b3c4');

    const header = await screen.findByRole('heading', { level: 1 });
    expect(within(header.closest('header')!).getByText('going on')).toBeInTheDocument();
  });

  /** §3.1: the words shown at once, then where they went, the session a door to it. */
  it('says where the words went, and opens that session from the line', async () => {
    driver({ reaches: 'resume' }, undefined, {
      SESSION_HISTORY: {
        session: 's1a2b3c4', earlier: false, latest: 4,
        events: [
          { seq: 1, at: '2026-10-03T10:00:00Z', kind: 'user', origin: 'target', text: 'take quest #abc123' },
          { seq: 2, at: '2026-10-03T10:00:05Z', kind: 'turn', stopReason: 'end_turn' },
          { seq: 3, at: '2026-10-03T11:00:00Z', kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'also cap it' },
          { seq: 4, at: '2026-10-03T11:00:30Z', kind: 'note', text: '— your words went to session `n3wn3w00`, because its tree is gone.', words: ['w1'], to: 'n3wn3w00', why: 'tree' },
        ],
      },
    });
    const { onSelect } = show('s1a2b3c4');

    await userEvent.click(await screen.findByRole('button', { name: 'n3wn3w00' }));
    expect(onSelect).toHaveBeenCalledWith('n3wn3w00');
  });

  /**
   * MSG1f2: a shell that tells the reach live is followed by its events: the box goes as the session's quest goes on in a
   * later session here, said by the event alone, and the queue is asked once.
   */
  it('follows the reach the shell tells live, without asking again', async () => {
    driver({ reaches: 'resume', reach: { reaches: 'resume' } });
    show('s1a2b3c4');

    await screen.findByLabelText('Message');
    await waitFor(() => expect(eventHandlers.has('DAORIS.SESSION_QUEUED')).toBe(true));
    // Asked once by the reach and once by the list of what waits on the record (SAIDBACK1b), each on opening; never on a move.
    const asked = invoke.mock.calls.filter(([, type]) => type === 'SESSION_QUEUE').length;
    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 's1a2b3c4', queued: [], taking: false, reach: { why: 'superseded' } }));

    expect(await screen.findByText(/went on in a later session here, so write to that one\./)).toBeInTheDocument();
    expect(screen.queryByLabelText('Message')).toBeNull();
    expect(invoke.mock.calls.filter(([, type]) => type === 'SESSION_QUEUE')).toHaveLength(asked);
    expect(asked).toBeLessThanOrEqual(2);
  });

  /** Words a closed quest's session cannot go on with: two said, and the driver's line saying why. */
  const CANNOT = {
    session: 's1a2b3c4', earlier: false, latest: 5,
    events: [
      { seq: 1, at: '2026-10-03T10:00:00Z', kind: 'user', origin: 'target', text: 'take quest #abc123' },
      { seq: 2, at: '2026-10-03T10:00:05Z', kind: 'turn', stopReason: 'end_turn' },
      { seq: 3, at: '2026-10-03T11:00:00Z', kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'first' },
      { seq: 4, at: '2026-10-03T11:00:01Z', kind: 'user', origin: 'person', id: 'w2', reaches: 'resume', text: 'second' },
      { seq: 5, at: '2026-10-03T11:00:30Z', kind: 'note', text: '— It cannot go on in this session, because its conversation could not be resumed.', words: ['w1', 'w2'], why: 'refused' },
    ],
  };

  /**
   * §2.2, MSG1f2: words a closed quest's session cannot go on with offer one press, the driver's one act: a new conversation
   * in the same repository whose first message is the words, taken off this session, attended once it opens. The page sends
   * the session alone; the driver reads the words off its record, and nothing else is asked.
   */
  it('starts a conversation with the words that cannot go on, and attends it', async () => {
    driver({ reaches: 'resume' }, { sent: true }, {
      SESSION_START_FROM: { sessionId: 'c4a7c4a7', sent: true, message: 'started' },
      SESSION_HISTORY: CANNOT,
    });
    const { onSelect } = show('s1a2b3c4');

    expect(await screen.findByText('It cannot go on in this session, because its conversation could not be continued.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Start a conversation with these words' }));

    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('c4a7c4a7'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_START_FROM', { payload: { id: 's1a2b3c4' } });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'START_CHAT', expect.anything());
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());
  });

  /** MSG1f2: a press the driver refuses is said by its code in the page's words, and attends nothing. */
  it('says a refused press by its code', async () => {
    driver({ reaches: 'resume' }, { sent: true }, {
      SESSION_START_FROM: { sent: false, why: 'no-words', message: 'no words wait on s1a2b3c4 to start a conversation with.' },
      SESSION_HISTORY: CANNOT,
    });
    const notify = vi.fn();
    const { onSelect } = show('s1a2b3c4', notify);

    await userEvent.click(await screen.findByRole('button', { name: 'Start a conversation with these words' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'No words wait on this session now, so there is nothing to start a conversation with.', 'error'));
    expect(onSelect).not.toHaveBeenCalledWith(expect.stringMatching(/^c/));
  });

  /**
   * SESSCTX1c part B (D189 point 3): beside the fresh start, *Go on with Daoris's brief* asks the driver for a new part of the same
   * session, handed the brief and the words, and attends it once it opens. The page sends the session alone.
   */
  it('goes on with Daoris’s brief in a new part of the session, and attends it', async () => {
    driver({ reaches: 'resume' }, { sent: true }, {
      SESSION_GO_ON_BRIEF: { sessionId: 'c5b8c5b8', sent: true, message: 'went on' },
      SESSION_HISTORY: CANNOT,
    });
    const { onSelect } = show('s1a2b3c4');

    await userEvent.click(await screen.findByRole('button', { name: "Go on with Daoris's brief" }));

    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('c5b8c5b8'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GO_ON_BRIEF', { payload: { id: 's1a2b3c4' } });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_START_FROM', expect.anything());
  });

  /** SESSCTX1c part B: a press the driver refuses is said by its code in the page's words, its own codes and the fresh start's. */
  it.each([
    ['agent-gone', 'The agent it ran on is no longer on this machine: hand it over to another agent to go on with these words.'],
    ['tree-gone', 'The folder it worked in is gone, so there is nothing to go on in.'],
    ['no-words', 'No words wait on this session now, so there is nothing to start a conversation with.'],
  ])('says a refused brief press by its code %s', async (why, said) => {
    driver({ reaches: 'resume' }, { sent: true }, {
      SESSION_GO_ON_BRIEF: { sent: false, why, message: 'the driver’s sentence' },
      SESSION_HISTORY: CANNOT,
    });
    const notify = vi.fn();
    const { onSelect } = show('s1a2b3c4', notify);

    await userEvent.click(await screen.findByRole('button', { name: "Go on with Daoris's brief" }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(said, 'error'));
    expect(onSelect).not.toHaveBeenCalledWith(expect.stringMatching(/^c/));
  });

  /**
   * MSG1g2 (§2.2, MSG1g's note): words a resume holds while the account the record ran on cools say so with its reset,
   * read from the accounts' facts, and *Go on in a new session* asks the driver; a closed quest's answer points at a
   * conversation with the words, which MSG1f's press starts, as the driver's one act (MSG1f2).
   */
  it('says words wait for a cooling account, asks the driver for a new session, and words its answer', async () => {
    SESSIONS = [{ ...ENDED, profile: 'personal' }];
    const until = new Date(Date.now() + 3 * 3_600_000).toISOString();
    const cooling = { until, stated: true, window: 'session', seen: until, assumedZone: false, notBelieved: false };
    driver({ reaches: 'resume' }, { sent: true }, {
      ACCOUNTS: { agents: [{ agent: 'claude-code', speaks: true, own: {}, scopes: [], accounts: [{ name: 'personal', cooling }] }] },
      SESSION_GO_ON_NEW: { sent: false, why: 'closed', message: '#abc123 has closed, so nothing carries its session’s words on by itself' },
      SESSION_START_FROM: { sessionId: 'c4a7c4a7', sent: true, message: 'started' },
      SESSION_HISTORY: {
        session: 's1a2b3c4', earlier: false, latest: 3,
        events: [
          { seq: 1, at: '2026-10-03T10:00:00Z', kind: 'user', origin: 'target', text: 'take quest #abc123' },
          { seq: 2, at: '2026-10-03T10:00:05Z', kind: 'turn', stopReason: 'end_turn' },
          { seq: 3, at: '2026-10-03T11:00:00Z', kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'also cap it' },
        ],
      },
    });
    const { onSelect } = show('s1a2b3c4');

    expect(await screen.findByText(/^Held: its account is cooling until .+; it goes on with this then\.$/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Go on in a new session' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GO_ON_NEW', { payload: { id: 's1a2b3c4' } });
    expect(await screen.findByText('#abc123 has closed, so nothing carries its words on by itself; start a conversation with them instead.'))
      .toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Start a conversation with these words' }));
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('c4a7c4a7'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_START_FROM', { payload: { id: 's1a2b3c4' } });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'START_CHAT', expect.anything());
  });

  /** MSG1g2: an account that is ready, or a shell that answers no accounts, leaves the held line as it was, and no press. */
  it('offers no new session where the record’s account is not cooling', async () => {
    SESSIONS = [{ ...ENDED, profile: 'personal' }];
    driver({ reaches: 'resume' }, undefined, {
      ACCOUNTS: { agents: [{ agent: 'claude-code', speaks: true, own: {}, scopes: [], accounts: [{ name: 'personal' }] }] },
      SESSION_HISTORY: {
        session: 's1a2b3c4', earlier: false, latest: 1,
        events: [{ seq: 1, at: '2026-10-03T11:00:00Z', kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'also cap it' }],
      },
    });
    show('s1a2b3c4');

    expect(await screen.findByText("Held: the same session goes on with this at the driver's next look.")).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Go on in a new session' })).toBeNull();
  });

  /**
   * §5.1: *Send back…* in a review opens the box on that session, with the focus in it, instead of the quest composer —
   * what the glossary's *send back* means. Where the session takes no words, the quest composer's door stays.
   */
  it('opens the box from Send back… in a review, and the quest composer only where nothing takes words', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    const DIFF = { session: 's1a2b3c4', base: 'abc1234567890', truncated: null, files: [{ path: 'src/chunk.ts', status: 'modified', added: 1, removed: 1, patch: '@@ -1 +1 @@\n-a\n+b' }] };
    driver({ reaches: 'resume' }, undefined, { SESSION_DIFF: DIFF });
    const onSendBack = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <Tooltip.Provider>
          <WorkFrame selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}} onSendBack={onSendBack} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    const box = await screen.findByLabelText('Message');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Send back…' }));
    await waitFor(() => expect(box).toHaveFocus());
    expect(onSendBack).not.toHaveBeenCalled();
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

  /**
   * 🔴 On the first real parked session there was nowhere to type the answer: the door was a button in
   * the card at the top of a record of 1,800 events, and the question is read at its foot. A parked driven session is answered from the box at the foot, where a
   * chat is, and the card says so — one owner for the answer (D56).
   */
  it('answers a parked driven session from the box at the foot, and the card says the box carries it on', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_INPUT'
      ? { sent: true, reaches: 'resume' }
      : DRIVER_STATE));
    show('p4rk3d00');

    const box = await screen.findByLabelText('Message');
    expect(screen.queryByRole('button', { name: 'Answer and carry on…' })).toBeNull();
    expect(screen.getByText(/Answering it in the box below lets it carry on/)).toBeInTheDocument();
    // An answer is words: nothing to attach, and no ending of the box's own (the card holds those, and
    // since NAME1b the card's Finish and the box's are one name for one act, so the box is asked).
    expect(screen.queryByRole('button', { name: 'Attach files' })).toBeNull();
    expect(within(box.closest('form')!).queryByRole('button', { name: 'Finish' })).toBeNull();

    await userEvent.type(box, 'go ahead with the PUT');
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));

    // Through the one door every box says through (MSG1f): the driver keeps it on the record, shows it at once in the
    // conversation and nudges its loop (MSG1d), where the service's answer door did none of the last two.
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 'p4rk3d00', text: 'go ahead with the PUT' },
    }));
    expect(vi.mocked(fetch)).not.toHaveBeenCalledWith('/api/sessions/p4rk3d00/answer', expect.anything());
  });

  /**
   * SENDLOST1b: a park's answer whose send the page stopped waiting on stays above its box, saying the machine has not
   * answered yet, never said answered and never back in the box, until the words waiting on its record list it.
   */
  it('keeps a park\'s answer whose send timed out, until the words waiting on its record list it', async () => {
    const words = 'go ahead with the PUT';
    let stop: () => void = () => {};
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUEUE') return { session: 'p4rk3d00', queued: [], taking: false, reaches: 'resume' };
      if (type === 'SESSION_INPUT') return new Promise((_, reject) => { stop = () => reject(timedOut()); });
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('p4rk3d00', notify);

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, words);
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));
    expect(await screen.findByRole('list', { name: 'sending…' })).toHaveTextContent(words);

    await act(async () => { stop(); });
    expect(await screen.findByRole('list', { name: UNANSWERED })).toHaveTextContent(words);
    expect(box).toHaveValue('');
    expect(notify).not.toHaveBeenCalled();

    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_QUEUED')!({
        session: 'p4rk3d00', queued: [{ id: 'said-1', text: words, files: [] }], taking: false, reaches: 'resume',
      });
    });
    expect(screen.queryByRole('list', { name: UNANSWERED })).toBeNull();
    // Folded to a line with its count (WAITFOOT2c); a press lists the words.
    await userEvent.click(screen.getByRole('button', { name: 'Show them' }));
    expect(screen.getByRole('list', { name: 'waiting for the same session to go on with them' })).toHaveTextContent(words);
    expect(inputs()).toHaveLength(1);
  });

  /**
   * ANSWER1c (D131): the answer keeps the park (ANSWER1b), and the same session goes on at the driver's next look. Until
   * then the record is still parked, with the answer set, and the frame shows it going on: the answer and when, and none
   * of the card's moves. The toast says the same. MSG1f (D137 §2.4): a second word joins the first, so the box stays,
   * saying the same session goes on with it.
   */
  it('shows an answered park as going on at the driver\'s next look, its box taking a second word and no moves', async () => {
    const notify = vi.fn();
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') {
        SESSIONS = [{ ...PARKED, answer: 'go ahead with the PUT', note: `${PARKED.note}\n\nAnswered: go ahead with the PUT` }];
        return { sent: true, reaches: 'resume' };
      }
      if (type === 'SESSION_QUEUE') return { session: 'p4rk3d00', queued: [], taking: false, reaches: 'resume' };
      return DRIVER_STATE;
    });
    show('p4rk3d00', notify);

    await userEvent.type(await screen.findByLabelText('Message'), 'go ahead with the PUT');
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));

    expect(await screen.findByRole('heading', { name: 'Your answer' })).toBeInTheDocument();
    expect(screen.getByText("The same session goes on with this answer at the driver's next look.")).toBeInTheDocument();
    expect(notify).toHaveBeenCalledWith("Answered — the same session goes on with your words at the driver's next look.");
    expect(await screen.findByLabelText('Message')).toHaveAttribute('placeholder', 'write to it — the same session goes on with your words');
    expect(screen.queryByText('This one is waiting on you')).toBeNull();
    for (const name of ['Finish', 'Decline…', 'Carry on with this answer']) expect(screen.queryByRole('button', { name })).toBeNull();
    // Its stop stays the page header's, saying what a driven session's stop says, never that it stops unanswered.
    await userEvent.click(within(screen.getByRole('group', { name: 'Session actions' })).getByRole('button', { name: 'Stop…' }));
    const ask = screen.getByRole('group', { name: 'stop this session' });
    expect(ask).toHaveTextContent('Stops the session now.');
    expect(ask).not.toHaveTextContent('unanswered');
  });

  /** The card keeps finish and decline; the stop is the page header's, which asks once (SESSUX1d, D126 §3.3). */
  it('shows the analysis and its moves on the attended session, its stop in the page header', async () => {
    show('p4rk3d00');

    expect(await screen.findByText(/I recommend the second/)).toBeInTheDocument();
    // Its quest is still open, so its finish asks what becomes of it first (QUESTCLOSE1).
    expect(screen.getByRole('button', { name: 'Finish…' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Decline…' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Stop session' })).toBeNull();
    expect(within(screen.getByRole('group', { name: 'Session actions' })).getByRole('button', { name: 'Stop…' })).toBeInTheDocument();
  });

  /** §3.3: a session waiting on you, with no process left, stops unanswered through its resolve, as its card's stop did. */
  it('stops a parked session from its page header through its resolve, saying it stops unanswered', async () => {
    show('p4rk3d00');
    await userEvent.click(await screen.findByRole('button', { name: 'Stop…' }));

    const ask = screen.getByRole('group', { name: 'stop this session' });
    expect(ask).toHaveTextContent('Stops it unanswered. Its quest stays taken here until you choose Try again.');
    await userEvent.click(within(ask).getByRole('button', { name: 'Stop session' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', { payload: { id: 'p4rk3d00', state: 'stopped' } });
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

    // The band's Finish and the composer's are one name for one act (NAME1b), so the box is asked.
    const box = () => screen.getByLabelText('Message').closest('form')!;
    await screen.findByRole('button', { name: 'Finish' });
    expect(screen.getByRole('button', { name: 'Send' })).toBeInTheDocument();
    // The band's verbs, and only the band's.
    expect(screen.getAllByRole('button', { name: 'Finish' })).toHaveLength(1);
    expect(within(box()).queryByRole('button', { name: 'Finish' })).toBeNull();
    expect(within(box()).queryByRole('button', { name: 'Stop' })).toBeNull();

    // Working again: no band, and the composer owns Finish; the stop is the page header's at every state (D126 §3.3).
    SESSIONS = [{ ...PARKED, kind: 'chat', state: 'working' }];
    cleanup();
    show('p4rk3d00');

    await within(await screen.findByLabelText('Message').then((field) => field.closest('form')!))
      .findByRole('button', { name: 'Finish' });
    expect(within(box()).queryByRole('button', { name: 'Stop' })).toBeNull();
    expect(screen.getAllByRole('button', { name: 'Finish' })).toHaveLength(1);
    expect(screen.getAllByRole('button', { name: 'Stop…' })).toHaveLength(1);
  });

  it('finishes it over the driver, with no note the person did not write, and leaves its quest as it is', async () => {
    const fetched = vi.fn(async (input: RequestInfo | URL) => respond(String(input)));
    vi.stubGlobal('fetch', fetched);
    show('p4rk3d00');
    // Its quest is still open, so the finish asks what becomes of it first (QUESTCLOSE1), leaving it as it is by default.
    await userEvent.click(await screen.findByRole('button', { name: 'Finish…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Finish' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', {
      payload: { id: 'p4rk3d00', state: 'completed' },
    });
    expect(fetched.mock.calls.some(([url]) => String(url).endsWith('/done'))).toBe(false);
  });

  /**
   * QUESTCLOSE1 (D126's note): on the install a finish at a checkpoint left its quest taken with nothing to close it. Marked
   * done in the same act, the finish goes over the driver first, then the person's done goes to its quest's own door with
   * their words, and both are said.
   */
  it("finishes it over the driver, then marks its quest done as the person's with their note", async () => {
    const posted: { url: string; body: unknown }[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (init?.method === 'POST' && url === '/api/quests/abc123/done') {
        posted.push({ url, body: JSON.parse(String(init.body)) });
        return Response.json({ quest: { ...QUESTS[0], status: 'Done' }, message: 'Quest `#abc123` is now Done: you marked it done.' });
      }
      return respond(url);
    }));
    const notify = vi.fn();
    show('p4rk3d00', notify);

    await userEvent.click(await screen.findByRole('button', { name: 'Finish…' }));
    await userEvent.click(screen.getByRole('radio', { name: 'Mark it done as yours' }));
    await userEvent.type(screen.getByLabelText(/your note on the quest/), 'The write-up is in the shared folder.');
    await userEvent.click(screen.getByRole('button', { name: 'Finish' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', {
      payload: { id: 'p4rk3d00', state: 'completed' },
    });
    await waitFor(() => expect(posted).toEqual([{ url: '/api/quests/abc123/done', body: { note: 'The write-up is in the shared folder.' } }]));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Quest `#abc123` is now Done: you marked it done.'));
  });

  /** The finish stood; a refusal of the quest's done is the service's sentence, said as an error. */
  it("says the service's refusal of its quest's done, the finish having stood", async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (init?.method === 'POST' && url === '/api/quests/abc123/done') {
        return Response.json({ error: 'Quest `#abc123` is Done — a closed quest does not move; a new ask is a new title.' }, { status: 409 });
      }
      return respond(url);
    }));
    const notify = vi.fn();
    show('p4rk3d00', notify);

    await userEvent.click(await screen.findByRole('button', { name: 'Finish…' }));
    await userEvent.click(screen.getByRole('radio', { name: 'Mark it done as yours' }));
    await userEvent.click(screen.getByRole('button', { name: 'Finish' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('a closed quest does not move'), 'error'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', { payload: { id: 'p4rk3d00', state: 'completed' } });
  });

  it('carries the reason a decline was given', async () => {
    show('p4rk3d00');
    await userEvent.click(await screen.findByRole('button', { name: 'Decline…' }));
    await userEvent.type(screen.getByLabelText(/the reason/), 'the chunk API is being replaced');
    await userEvent.click(screen.getByRole('button', { name: 'Decline with this reason' }));

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
    await userEvent.click(await screen.findByRole('button', { name: 'Finish…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Finish' }));

    // Said inside the ask that was pressed, and in no toast (UXFIX2b2b); the ask stays, to be pressed again.
    const ask = screen.getByRole('group', { name: 'finish this session' });
    expect(await within(ask).findByRole('alert')).toHaveTextContent('Declining needs a reason');
    expect(notify).not.toHaveBeenCalled();
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
    expect(screen.queryByRole('button', { name: 'Finish' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Stop session' })).toBeNull();
    expect(screen.queryByLabelText('Message')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
    // Its page header offers only what reaches no process: its ⋯, with its id (SESSUX1d, D126 §3.2).
    const acts = within(screen.getByRole('group', { name: 'Session actions' }));
    expect(acts.getAllByRole('button').map((button) => button.getAttribute('aria-label'))).toEqual(['More actions']);
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

    // The head's card, in the main area; the strip at the foot offers the same door (WAITFOOT1b, D168).
    await userEvent.click(await within(screen.getByRole('main')).findByRole('button', { name: 'Answer ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledWith('0fda18');
    await userEvent.click(within(screen.getByRole('region', { name: 'Waiting on you' })).getByRole('button', { name: 'Answer ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledTimes(2);
    expect(screen.queryByRole('button', { name: 'Finish' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
    expect(screen.queryByLabelText('Message')).toBeNull();
  });

  /**
   * Stopping stays: the person may end the intake and settle the ask later, as a proposal. Its stop is the page
   * header's since SESSUX1d, and asks with the sentence its card said (D126 §3.3).
   */
  it('stops a parked intake over the driver, and nothing more', async () => {
    SESSIONS = [PARKED_INTAKE];
    show('i9n8t7k6');
    await userEvent.click(await screen.findByRole('button', { name: 'Stop…' }));
    expect(screen.getByRole('group', { name: 'stop this session' })).toHaveTextContent(/the ask stays a proposal/);
    await userEvent.click(screen.getByRole('button', { name: 'Stop session' }));

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
    expect(screen.queryByLabelText('Message')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Finish' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Open ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledWith('0fda18');
  });

  /** The stop the composer used to carry, then its card: the process is cut off, over the driver, from the page header. */
  it('stops a running intake over the driver', async () => {
    SESSIONS = [RUNNING_INTAKE];
    show('r7n8t7k6');
    await userEvent.click(await screen.findByRole('button', { name: 'Stop…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Stop session' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', { payload: { id: 'r7n8t7k6' } });
  });

  /** A conversation keeps its composer: it is the one kind of session that takes turns. */
  it('keeps the composer for a conversation', async () => {
    SESSIONS = [{ ...RUNNING_INTAKE, id: 'c0nv0000', ask: undefined, repository: 'engine' }];
    show('c0nv0000');

    expect(await screen.findByLabelText('Message')).toBeInTheDocument();
    expect(screen.queryByText(/takes no messages/)).toBeNull();
  });

  /** A driven session that is not parked gets no moves: there is nothing waiting on anybody. */
  it('offers no moves on a session nobody is waiting for', async () => {
    SESSIONS = [DRIVEN];
    show('s1a2b3c4');

    await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' });
    expect(screen.queryByRole('button', { name: 'Finish' })).toBeNull();
  });
});
