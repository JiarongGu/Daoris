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

import { useSessionEvents } from '../shell';
import { mergeEvents, type SessionEvent, toTurns } from './conversation';

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

  it('keeps what happened before any ask in a turn of its own', () => {
    const { turns } = toTurns([e(1, { kind: 'note', text: 'no connector here' })]);

    expect(turns).toHaveLength(1);
    expect(turns[0]!.ask).toBeUndefined();
    expect(turns[0]!.items[0]).toMatchObject({ kind: 'note', text: 'no connector here' });
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
