import { afterEach, describe, expect, it, vi } from 'vitest';
import { hashKey, QueryClient } from '@tanstack/react-query';

// NOBLOCK1: the page's half of a read the shell answers from what it holds and tells by event.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));
vi.mock('@shenora/react', () => ({ getBridge: () => ({ isAvailable: true, invoke }) }));

import { answered, askHeld, isPending } from './held';

const key = ['sessions', 'where', 'a1'] as const;

describe('a read told by event (NOBLOCK1)', () => {
  afterEach(() => invoke.mockReset());

  it('names its query in the payload, and keeps what the query holds while nothing is held yet', async () => {
    const client = new QueryClient();
    client.setQueryData(key, { sessions: [{ session: 'a1', treeGone: false }] });
    invoke.mockResolvedValue({ pending: true });

    const kept = await askHeld('SESSION_WHERE', client, key, { sessions: [] });

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_WHERE', { payload: { sessions: [], held: hashKey(key) } });
    expect(kept).toEqual({ sessions: [{ session: 'a1', treeGone: false }] });
  });

  it('reads nothing held as the pending answer itself, and an answer as the answer', async () => {
    const client = new QueryClient();
    invoke.mockResolvedValueOnce({ pending: true }).mockResolvedValueOnce({ sessions: [] });

    expect(isPending(await askHeld('SESSION_WHERE', client, key, {}))).toBe(true);
    expect(await askHeld('SESSION_WHERE', client, key, {})).toEqual({ sessions: [] });
  });

  it('writes an event’s answer into the query it names, and only that one', () => {
    const client = new QueryClient();
    client.setQueryData(key, { sessions: [] });
    client.setQueryData(['sessions', 'where', 'b2'], { sessions: [] });

    answered(client, { held: hashKey(key), answer: { sessions: [{ session: 'a1', treeGone: true }] } });
    // A query this window does not hold, and a shape that is no telling, move nothing.
    answered(client, { held: hashKey(['nowhere']), answer: { sessions: ['x'] } });
    answered(client, { answer: {} });

    expect(client.getQueryData(key)).toEqual({ sessions: [{ session: 'a1', treeGone: true }] });
    expect(client.getQueryData(['sessions', 'where', 'b2'])).toEqual({ sessions: [] });
  });

  it('asks the query again when its work failed, so the shell’s refusal is read', async () => {
    const client = new QueryClient();
    const queryFn = vi.fn(async () => ({ sessions: [] }));
    await client.fetchQuery({ queryKey: key, queryFn });
    const invalidate = vi.spyOn(client, 'invalidateQueries');

    answered(client, { held: hashKey(key), failed: true });

    expect(invalidate).toHaveBeenCalledWith({ queryKey: key, exact: true });
  });
});
