import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';

// A session's conversation over the bridge (D76, CONV1): the history read once, live batches merged
// by sequence, a gap closed by asking for what was missed, and earlier pages on request. The
// bridge is mocked as present; the host is the test.

const { invoke, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import { useChatTurns, useSessionEvents } from '../shell';
import { mergeEvents, runCount, segments, type SessionEvent, settle, toTurns } from './conversation';

const said = (seq: number, text = `m${seq}`): SessionEvent => ({ seq, at: '2026-09-25T00:00:00Z', kind: 'message', text });

const emit = (session: string, events: SessionEvent[]) =>
  act(() => { eventHandlers.get('DAORIS.SESSION_EVENTS')!({ session, events }); });

const texts = (events: SessionEvent[]) => events.map((e) => e.text);

afterEach(() => {
  invoke.mockReset();
  eventHandlers.clear();
});

const e = (seq: number, over: Partial<SessionEvent>): SessionEvent => ({ seq, at: `2026-09-25T00:00:0${seq % 10}Z`, kind: 'message', ...over });

describe('toTurns', () => {
  it('opens a turn at what was asked, joins chunks into one message, and closes it at the turn\'s end', () => {
    const { turns } = toTurns([
      e(1, { kind: 'user', origin: 'person', text: 'cap the hydration' }),
      e(2, { kind: 'thought', text: 'the cap belongs ' }),
      e(3, { kind: 'thought', text: 'in the streamer' }),
      e(4, { kind: 'message', text: 'Capped at ' }),
      e(5, { kind: 'message', text: '4 per frame.' }),
      e(6, { kind: 'turn', stopReason: 'end_turn' }),
    ]);

    expect(turns).toHaveLength(1);
    expect(turns[0]!.ask).toMatchObject({ text: 'cap the hydration', origin: 'person' });
    expect(turns[0]!.items.map((b) => [b.kind, b.text])).toEqual([
      ['thought', 'the cap belongs in the streamer'],
      ['message', 'Capped at 4 per frame.'],
    ]);
    expect(turns[0]!.ended).toBe('end_turn');
  });

  /**
   * Two messages in a row are two, by the ids the wire gives them: joined, the window read
   * `DONESubagent finished` (found looking at CONSOLE2). A record from before ids were kept joins as it
   * always did.
   */
  it('keeps two messages in a row apart by their ids, and joins chunks with no id as before', () => {
    const { turns } = toTurns([
      e(1, { kind: 'user', origin: 'person', text: 'do it' }),
      e(2, { kind: 'message', id: 'msg_1', text: 'DO' }),
      e(3, { kind: 'message', id: 'msg_1', text: 'NE' }),
      e(4, { kind: 'message', id: 'msg_2', text: 'Subagent finished.' }),
      e(5, { kind: 'message', text: ' More' }),
      e(6, { kind: 'message', text: ' words.' }),
    ]);

    expect(turns[0]!.items.map((b) => b.text)).toEqual(['DONE', 'Subagent finished. More words.']);
  });

  /** CONV4c: what the person attached travels with what they asked — names only, as the record keeps them. */
  it('keeps the names of what the person attached with what they asked', () => {
    const { turns } = toTurns([
      e(1, { kind: 'user', origin: 'person', text: 'what does this log say?', files: ['run.log', 'shot.png'] }),
      e(2, { kind: 'user', origin: 'person', text: 'and without files' }),
    ]);

    expect(turns[0]!.ask?.files).toEqual(['run.log', 'shot.png']);
    expect(turns[1]!.ask?.files).toBeUndefined();
  });

  /** A tool call and its updates are one card, where it first appeared, carrying its latest state. */
  it('merges a tool call\'s updates into the one card, in the place it began', () => {
    const { turns } = toTurns([
      e(1, { kind: 'tool', id: 'c1', title: 'Edit src/chunk.rs', toolKind: 'edit', status: 'pending', locations: ['src/chunk.rs'] }),
      e(2, { kind: 'message', text: 'editing' }),
      e(3, { kind: 'tool', id: 'c1', status: 'completed', content: [{ type: 'diff', path: 'src/chunk.rs', oldText: 'a', newText: 'b' }] }),
    ]);

    const [tool, message] = turns[0]!.items;
    expect(tool).toMatchObject({
      kind: 'tool', id: 'c1', title: 'Edit src/chunk.rs', toolKind: 'edit', status: 'completed', locations: ['src/chunk.rs'],
    });
    expect(tool!.content).toEqual([{ type: 'diff', path: 'src/chunk.rs', oldText: 'a', newText: 'b' }]);
    expect(message).toMatchObject({ kind: 'message', text: 'editing' });
  });

  /**
   * A location's line rides with the locations it was said of (LEFT2): an update that carries no locations keeps
   * the card's line, and one that replaces them (ACP replaces a present list whole) replaces the line too.
   */
  it('keeps a tool call\'s line with its locations, replaced only when they are', () => {
    const { turns } = toTurns([
      e(1, { kind: 'tool', id: 'c1', title: 'Edit src/chunk.rs', toolKind: 'edit', status: 'pending', locations: ['src/chunk.rs'], line: 12 }),
      e(2, { kind: 'tool', id: 'c1', status: 'in_progress' }),
    ]);
    expect(turns[0]!.items[0]).toMatchObject({ locations: ['src/chunk.rs'], line: 12 });

    const moved = toTurns([
      e(1, { kind: 'tool', id: 'c1', title: 'Edit src/chunk.rs', toolKind: 'edit', status: 'pending', locations: ['src/chunk.rs'], line: 12 }),
      e(2, { kind: 'tool', id: 'c1', status: 'completed', locations: ['src/level.rs'] }),
    ]).turns;
    expect(moved[0]!.items[0]!.locations).toEqual(['src/level.rs']);
    expect(moved[0]!.items[0]!.line ?? null).toBeNull();
  });

  it('starts a new turn at each ask, and keeps a plan as its latest entries', () => {
    const { turns } = toTurns([
      e(1, { kind: 'user', origin: 'target', text: 'take quest #q1' }),
      e(2, { kind: 'plan', entries: [{ content: 'read', status: 'in_progress' }] }),
      e(3, { kind: 'plan', entries: [{ content: 'read', status: 'completed' }, { content: 'edit', status: 'pending' }] }),
      e(4, { kind: 'turn', stopReason: 'end_turn' }),
      e(5, { kind: 'user', origin: 'person', text: 'and test it' }),
      e(6, { kind: 'message', text: 'tested' }),
    ]);

    expect(turns).toHaveLength(2);
    expect(turns[0]!.items).toHaveLength(1);
    expect(turns[0]!.items[0]!.entries).toEqual([{ content: 'read', status: 'completed' }, { content: 'edit', status: 'pending' }]);
    expect(turns[1]!.ask?.text).toBe('and test it');
    expect(turns[1]!.ended).toBeUndefined();
  });

  /** Usage is a meter, not a block: the latest reading and the highest are what CONV5 draws. */
  it('keeps usage out of the conversation, as the latest reading and the high-water mark', () => {
    const { turns, usage } = toTurns([
      e(1, { kind: 'usage', used: 900, size: 1000 }),
      e(2, { kind: 'usage', used: 300, size: 1000 }),
    ]);

    expect(turns.flatMap((t) => t.items)).toEqual([]);
    expect(usage).toEqual({ used: 300, size: 1000, most: 900 });
  });

  /**
   * CONV5: a turn carries what it consumed as the harness reported it, and how long it took by the
   * driver's clock — from the ask to the first thing the agent did, and to the turn's end. The driver's
   * own note is not the agent answering. A turn the wire told nothing of its tokens carries none.
   */
  it('carries each turn\'s tokens, and how long it took by the driver\'s clock', () => {
    const at = (seconds: number) => new Date(Date.UTC(2026, 8, 26, 10, 0, 0) + seconds * 1000).toISOString();
    const { turns } = toTurns([
      { seq: 1, at: at(0), kind: 'user', origin: 'person', text: 'read it' },
      { seq: 2, at: at(0.5), kind: 'note', text: 'the driver says something' },
      { seq: 3, at: at(1.5), kind: 'tool', id: 't1', title: 'Read a.md', status: 'in_progress' },
      { seq: 4, at: at(4), kind: 'message', text: 'hello' },
      { seq: 5, at: at(4.75), kind: 'turn', stopReason: 'end_turn', tokens: { input: 4, output: 80, cacheRead: 51061, cacheWrite: 16717 } },
      { seq: 6, at: at(10), kind: 'user', origin: 'person', text: 'again' },
      { seq: 7, at: at(11), kind: 'turn', stopReason: 'end_turn' },
    ]);

    expect(turns[0]).toMatchObject({
      tokens: { input: 4, output: 80, cacheRead: 51061, cacheWrite: 16717 }, took: 4750, firstAfter: 1500,
    });
    expect(turns[1]!.tokens).toBeUndefined();
    expect(turns[1]).toMatchObject({ took: 1000 });
    expect(turns[1]!.firstAfter).toBeUndefined();
  });

  /** A turn with nothing asked has no start to measure from, and a running one no end. */
  it('measures no span it has no ends for', () => {
    const { turns } = toTurns([
      e(1, { kind: 'message', text: 'before any ask' }),
      e(2, { kind: 'turn', stopReason: 'end_turn' }),
      e(3, { kind: 'user', origin: 'person', text: 'still going' }),
      e(4, { kind: 'message', text: 'working' }),
    ]);

    expect(turns[0]!.took).toBeUndefined();
    expect(turns[1]!.took).toBeUndefined();
    expect(turns[1]!.firstAfter).toBeDefined();
  });

  /**
   * CONV4b: the calls a stop cut are the ones still open when the turn ended cancelled — whatever the
   * harness called them. Claude Code answers the cut call as failed, another agent leaves it running,
   * and either way the person stopped it. A call that failed earlier and was followed by more work
   * failed on its own, and stays failed.
   */
  it('marks the calls a stop cut as stopped, and nothing else', () => {
    const cut = toTurns([
      e(1, { kind: 'user', origin: 'person', text: 'run the tests' }),
      e(2, { kind: 'tool', id: 'c1', title: 'Read a.rs', status: 'completed' }),
      e(3, { kind: 'tool', id: 'c2', title: 'Run the build', status: 'failed' }),
      e(4, { kind: 'message', text: 'the build failed; running the tests anyway' }),
      e(5, { kind: 'tool', id: 'c3', title: 'Run the tests', status: 'in_progress' }),
      e(6, { kind: 'tool', id: 'c3', status: 'failed', content: [{ type: 'text', text: "The user doesn't want to proceed" }] }),
      e(7, { kind: 'turn', stopReason: 'cancelled' }),
    ]).turns[0]!;

    expect(cut.items.filter((b) => b.kind === 'tool').map((b) => [b.id, b.status, Boolean(b.stopped)])).toEqual([
      ['c1', 'completed', false],
      ['c2', 'failed', false],
      ['c3', 'failed', true],
    ]);

    // Calls run side by side at the end: a finished one among them does not hide an open one.
    const side = toTurns([
      e(1, { kind: 'tool', id: 'c1', status: 'in_progress' }),
      e(2, { kind: 'tool', id: 'c2', status: 'completed' }),
      e(3, { kind: 'turn', stopReason: 'cancelled' }),
    ]).turns[0]!;
    expect(side.items.map((b) => Boolean(b.stopped))).toEqual([true, false]);

    const ended = toTurns([
      e(1, { kind: 'tool', id: 'c1', status: 'in_progress' }),
      e(2, { kind: 'turn', stopReason: 'end_turn' }),
    ]).turns[0]!;
    expect(ended.items[0]!.stopped).toBeFalsy();
  });

  it('keeps what happened before any ask in a turn of its own', () => {
    const { turns } = toTurns([e(1, { kind: 'note', text: 'no connector here' })]);

    expect(turns).toHaveLength(1);
    expect(turns[0]!.ask).toBeUndefined();
    expect(turns[0]!.items[0]).toMatchObject({ kind: 'note', text: 'no connector here' });
  });
});

/**
 * SESS1, looked at on the first real workspace: a driven session is one ask and then its whole run —
 * 1,855 events in the longest — so the page holds its newest 200, past the ask and past the start of
 * the calls it shows.
 */
describe('toTurns, on a page of a long run', () => {
  const target = e(1, { kind: 'user', origin: 'target', text: 'Your target is the quest…' });

  it('opens with what was asked when the page does not hold it, and marks the gap after it', () => {
    const { turns } = toTurns([e(901, { text: 'Clean. Now the gates:' })], { opening: target });

    expect(turns).toHaveLength(1);
    expect(turns[0]!.ask?.text).toBe('Your target is the quest…');
    expect(turns[0]!.gap).toBe(true);
    expect(turns[0]!.items.map((item) => item.text)).toEqual(['Clean. Now the gates:']);
  });

  it('holds the ask once, with no gap, when the page reaches it', () => {
    const { turns } = toTurns([target, e(2, { text: 'first' })], { opening: target });

    expect(turns).toHaveLength(1);
    expect(turns[0]!.gap).toBeUndefined();
  });

  it('says a call it holds only the updates of began earlier, never naming it by its id', () => {
    const { turns } = toTurns([e(900, { kind: 'tool', id: 'toolu_01Example', status: 'completed' })], { opening: target });

    const call = turns[0]!.items[0]!;
    expect(call.continued).toBe(true);
    expect(call.title).toBeUndefined();
  });
});

/** SESS1 S9: a jump lands on an event; the page opens and shows the block that event is part of. */
describe('where each event landed', () => {
  it('names the block each event is part of: a message\'s chunks, a call\'s updates, the ask', () => {
    const { where } = toTurns([
      e(1, { kind: 'user', text: 'go' }),
      e(2, { text: 'Look' }),
      e(3, { text: 'ing.' }),
      e(4, { kind: 'tool', id: 'c1', title: 'npm test', status: 'in_progress' }),
      e(5, { kind: 'usage', used: 1, size: 2 }),
      e(6, { kind: 'tool', id: 'c1', status: 'failed' }),
    ]);

    expect(where).toEqual({ 1: 'e1', 2: 'e2', 3: 'e2', 4: 'e4', 6: 'e4' });
  });
});

describe('settle', () => {
  const run = (...over: Partial<SessionEvent>[]) => toTurns(over.map((o, i) => e(i + 1, o))).turns;

  it('marks a turn the session ended inside as cut, and the calls it left open as stopped', () => {
    const turns = settle(run(
      { kind: 'user', origin: 'target', text: 'go' },
      { kind: 'tool', id: 'c1', title: 'npm test', status: 'failed' },
      { kind: 'tool', id: 'c2', title: 'npm run gates', status: 'in_progress' },
    ), false);

    expect(turns[0]!.cut).toBe(true);
    expect(turns[0]!.items.map((item) => item.stopped ?? false)).toEqual([false, true]);
  });

  it('leaves a live session and a finished turn as they are', () => {
    const open = run({ kind: 'user', text: 'go' }, { kind: 'tool', id: 'c1', title: 'npm test', status: 'in_progress' });
    expect(settle(open, true)[0]!.cut).toBeUndefined();

    const done = run({ kind: 'user', text: 'go' }, { kind: 'message', text: 'done' }, { kind: 'turn', stopReason: 'end_turn' });
    expect(settle(done, false)[0]!.cut).toBeUndefined();
  });
});

describe('segments', () => {
  const blocks = (...over: Partial<SessionEvent>[]) => toTurns(over.map((o, i) => e(i + 1, o))).turns[0]!.items;

  it('keeps every word the agent said, and folds the work between them into runs that count it', () => {
    const parts = segments(blocks(
      { kind: 'message', text: 'Looking at the loader.' },
      { kind: 'tool', id: 'c1', title: 'Read a', status: 'completed' },
      { kind: 'thought', text: 'hm' },
      { kind: 'tool', id: 'c2', title: 'Bash b', status: 'failed' },
      { kind: 'message', text: 'Fixed.' },
      { kind: 'tool', id: 'c3', title: 'Read c', status: 'completed' },
      { kind: 'message', text: 'Done.' },
    ), false);

    expect(parts.map((part) => (part.kind === 'run' ? `run ${part.items.length} ${part.open ? 'open' : 'folded'}` : part.block.text ?? part.block.title)))
      .toEqual(['Looking at the loader.', 'run 3 folded', 'Fixed.', 'Read c', 'Done.']);
    expect(runCount(parts[1]!.kind === 'run' ? parts[1]!.items : [])).toEqual({ tools: 2, failed: 1, thought: true });
  });

  it('keeps the last run open while the turn goes on, so the work in hand is in view', () => {
    const parts = segments(blocks(
      { kind: 'message', text: 'Running the gates.' },
      { kind: 'tool', id: 'c1', title: 'npm test', status: 'completed' },
      { kind: 'tool', id: 'c2', title: 'npm run e2e', status: 'in_progress' },
    ), true);

    expect(parts[1]).toMatchObject({ kind: 'run', open: true });
  });

  it('keeps a plan and the driver\'s notes out of the fold', () => {
    const parts = segments(blocks(
      { kind: 'tool', id: 'c1', title: 'Read a', status: 'completed' },
      { kind: 'note', text: 'permission refused: Bash' },
      { kind: 'tool', id: 'c2', title: 'Read b', status: 'completed' },
    ), false);

    expect(parts.map((part) => part.kind === 'run' ? 'run' : part.block.kind)).toEqual(['tool', 'note', 'tool']);
  });

  /** SESS1 S6: the driver's refusal of one call names it, and folds with the run that call is in. */
  it('folds the driver\'s note about one call with that call\'s run', () => {
    const parts = segments(blocks(
      { kind: 'tool', id: 'c1', title: 'Read a', status: 'completed' },
      { kind: 'note', id: 'c2', text: 'permission refused: `git stash list`' },
      { kind: 'tool', id: 'c2', title: 'git stash list', status: 'failed' },
    ), false);

    expect(parts).toHaveLength(1);
    expect(parts[0]).toMatchObject({ kind: 'run', open: false });
  });
});

describe('mergeEvents', () => {
  it('keeps one of each sequence, in order, whichever arrived first', () => {
    expect(mergeEvents([said(1), said(3)], [said(2), said(3), said(4)]).map((e) => e.seq)).toEqual([1, 2, 3, 4]);
  });
});

describe('useSessionEvents', () => {
  it('opens a session with its newest page, and says when earlier turns exist', async () => {
    invoke.mockResolvedValue({ session: 's1', events: [said(4), said(5)], earlier: true, latest: 5 });

    const { result } = renderHook(() => useSessionEvents('s1'));

    await waitFor(() => expect(texts(result.current.events)).toEqual(['m4', 'm5']));
    expect(result.current.earlier).toBe(true);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HISTORY', { payload: { id: 's1' } });
  });

  /** SESS1 S1: the page past the ask carries it, and the hook holds it for the conversation to open with. */
  it('holds what the session was first asked, where its newest page does not', async () => {
    const opening = { seq: 1, at: '2026-09-25T00:00:00Z', kind: 'user', origin: 'target', text: 'Your target is…' };
    invoke.mockResolvedValue({ session: 's1', events: [said(900)], earlier: true, latest: 900, opening });

    const { result } = renderHook(() => useSessionEvents('s1'));

    await waitFor(() => expect(result.current.opening?.text).toBe('Your target is…'));
  });

  it('merges a live batch after the history, never doubling what it already holds', async () => {
    invoke.mockResolvedValue({ session: 's1', events: [said(1), said(2)], earlier: false, latest: 2 });
    const { result } = renderHook(() => useSessionEvents('s1'));
    await waitFor(() => expect(result.current.events).toHaveLength(2));

    emit('s1', [said(2), said(3)]);

    expect(texts(result.current.events)).toEqual(['m1', 'm2', 'm3']);
  });

  /** A batch that skips ahead is a batch that missed something: it asks, rather than joining two halves. */
  it('asks for what it missed when a batch skips ahead, and merges it in order', async () => {
    invoke.mockImplementation(async (_module: string, _type: string, request: { payload: { after?: number } }) =>
      request.payload.after === 2
        ? { session: 's1', events: [said(3), said(4), said(5)], earlier: false, latest: 5 }
        : { session: 's1', events: [said(1), said(2)], earlier: false, latest: 2 });
    const { result } = renderHook(() => useSessionEvents('s1'));
    await waitFor(() => expect(result.current.events).toHaveLength(2));

    emit('s1', [said(5)]);

    await waitFor(() => expect(texts(result.current.events)).toEqual(['m1', 'm2', 'm3', 'm4', 'm5']));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HISTORY', { payload: { id: 's1', after: 2 } });
  });

  it('loads earlier turns before what it holds, until there are none', async () => {
    invoke.mockImplementation(async (_module: string, _type: string, request: { payload: { before?: number } }) =>
      request.payload.before === 3
        ? { session: 's1', events: [said(1), said(2)], earlier: false, latest: 4 }
        : { session: 's1', events: [said(3), said(4)], earlier: true, latest: 4 });
    const { result } = renderHook(() => useSessionEvents('s1'));
    await waitFor(() => expect(result.current.events).toHaveLength(2));

    await act(() => result.current.loadEarlier());

    expect(texts(result.current.events)).toEqual(['m1', 'm2', 'm3', 'm4']);
    expect(result.current.earlier).toBe(false);
  });

  it('holds one session at a time, and ignores another session\'s batch', async () => {
    invoke.mockImplementation(async (_module: string, _type: string, request: { payload: { id: string } }) => ({
      session: request.payload.id, events: [said(1, `${request.payload.id} said`)], earlier: false, latest: 1,
    }));
    const { result, rerender } = renderHook(({ id }) => useSessionEvents(id), { initialProps: { id: 's1' } });
    await waitFor(() => expect(texts(result.current.events)).toEqual(['s1 said']));

    rerender({ id: 's2' });
    await waitFor(() => expect(texts(result.current.events)).toEqual(['s2 said']));
    emit('s1', [said(2, 'late from s1')]);

    expect(texts(result.current.events)).toEqual(['s2 said']);
  });

  it('holds nothing with no session attended', () => {
    const { result } = renderHook(() => useSessionEvents(null));

    expect(result.current.events).toEqual([]);
    expect(invoke).not.toHaveBeenCalled();
  });
});

/**
 * CONV4b: where a conversation's turns stand — whether one is in flight, and what the person sent that
 * has not reached the harness — asked once and then followed live. The driver is the authority: the
 * record lags it, and a message that is only waiting is in no record at all. UX5 U17 made it every
 * live conversation's, one listener for them all, since the rail reads a chat between turns as idle.
 */
describe('useChatTurns', () => {
  const queued = (session: string, state: { queued: { text: string; files: string[] }[]; taking: boolean }) =>
    act(() => { eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session, ...state }); });

  it('asks once per conversation, then follows the driver live, for the ones it watches only', async () => {
    invoke.mockImplementation(async (_module: string, _type: string, request: { payload: { id: string } }) =>
      (request.payload.id === 's1'
        ? { session: 's1', queued: [{ text: 'second', files: ['plan.md'] }], taking: true }
        : { session: 's2', queued: [], taking: false }));
    const { result } = renderHook(() => useChatTurns(['s1', 's2']));

    await waitFor(() => expect(result.current).toEqual({
      s1: { queued: [{ text: 'second', files: ['plan.md'] }], taking: true },
      s2: { queued: [], taking: false },
    }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE', { payload: { id: 's1' } });
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE', { payload: { id: 's2' } });

    queued('s3', { queued: [{ text: 'not watched', files: [] }], taking: true });
    queued('s1', { queued: [], taking: false });
    expect(result.current).toEqual({ s1: { queued: [], taking: false }, s2: { queued: [], taking: false } });
  });

  /** RAIL2: when the last turn ended here, as the driver says it — and nothing claimed where it says none. */
  it('carries when the last turn ended, from the answer and from each change', async () => {
    invoke.mockResolvedValue({ session: 's1', queued: [], taking: false, lastTurn: null });
    const { result } = renderHook(() => useChatTurns(['s1']));
    await waitFor(() => expect(result.current).toEqual({ s1: { queued: [], taking: false } }));

    act(() => {
      eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 's1', queued: [], taking: false, lastTurn: '2026-09-30T01:02:03+00:00' });
    });
    expect(result.current.s1!.lastTurn).toBe('2026-09-30T01:02:03+00:00');
  });

  /** Nothing known is no claim: an answer that says nothing of a turn leaves the conversation absent. */
  it('records nothing for an answer that is not a queue', async () => {
    invoke.mockResolvedValue({ drivable: [] });
    const { result } = renderHook(() => useChatTurns(['s1']));

    await waitFor(() => expect(invoke).toHaveBeenCalled());
    expect(result.current).toEqual({});
  });

  it('forgets a conversation it no longer watches', async () => {
    invoke.mockResolvedValue({ session: 's1', queued: [], taking: true });
    const { result, rerender } = renderHook(({ ids }) => useChatTurns(ids), { initialProps: { ids: ['s1'] } });
    await waitFor(() => expect(result.current.s1?.taking).toBe(true));

    rerender({ ids: [] });
    await waitFor(() => expect(result.current).toEqual({}));
  });

  it('holds nothing with no conversation live', () => {
    const { result } = renderHook(() => useChatTurns([]));

    expect(result.current).toEqual({});
    expect(invoke).not.toHaveBeenCalled();
  });
});
