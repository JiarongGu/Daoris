import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { QueryClient } from '@tanstack/react-query';

// Leaving Review stops its git (GITAPP1a-iii, the git-app design P1.6): the review's reader counts its waiters, and once the last
// has gone for READ_LEAVE_GRACE it sends READ_LEAVE with the read's id; a return within the grace joins the read still running.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import { keys } from '../../data/queries';
import { READ_LEAVE_GRACE, readDiff } from './trees';

const DIFF = { session: 's1a2b3c4', base: '0123456789abcdef', truncated: null, files: [], source: 'tree' };

/** The host: each review read held until the test answers it, each leave answered at once. */
function host() {
  const answers: Array<(value: unknown) => void> = [];
  invoke.mockImplementation((_module: string, type: string) => {
    if (type === 'SESSION_DIFF') return new Promise((resolve) => answers.push(resolve));
    if (type === 'READ_LEAVE') return Promise.resolve({ left: true });
    return Promise.resolve({});
  });
  return { answer: (at: number, value: unknown) => answers[at](value) };
}

const asked = (type: string) => invoke.mock.calls.filter((each) => each[1] === type);
const readOf = (at: number) => (asked('SESSION_DIFF')[at][2] as { payload: { read: string } }).payload.read;

/** One waiter on the review, as a query is: its signal, and its promise, whose leaving is observed so it never goes unheard. */
function wait(client: QueryClient) {
  const waiter = new AbortController();
  const answer = readDiff(client, 's1a2b3c4', waiter.signal);
  answer.catch(() => {});
  return { leave: () => waiter.abort(), answer };
}

describe('the review’s reader leaves a read nobody waits on', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => {
    vi.useRealTimers();
    invoke.mockReset();
  });

  it('sends READ_LEAVE with the read’s own id once its last waiter has been gone for the grace', async () => {
    host();
    const client = new QueryClient();
    const one = wait(client);
    expect(asked('SESSION_DIFF')).toHaveLength(1);
    expect(asked('SESSION_DIFF')[0][2]).toEqual({ payload: { id: 's1a2b3c4', read: expect.any(String) }, timeoutMs: 180_000 });

    one.leave();
    await vi.advanceTimersByTimeAsync(READ_LEAVE_GRACE - 1);
    expect(asked('READ_LEAVE')).toHaveLength(0);
    await vi.advanceTimersByTimeAsync(1);
    expect(asked('READ_LEAVE')).toEqual([['DAORIS.DRIVER', 'READ_LEAVE', { payload: { read: readOf(0) } }]]);

    // Left, so a return after the grace asks afresh, under an id of its own.
    wait(client);
    expect(asked('SESSION_DIFF')).toHaveLength(2);
    expect(readOf(1)).not.toBe(readOf(0));
  });

  it('keeps the read while another waiter waits, and leaves it only after the last', async () => {
    host();
    const client = new QueryClient();
    const one = wait(client);
    const two = wait(client);
    expect(asked('SESSION_DIFF')).toHaveLength(1);

    one.leave();
    await vi.advanceTimersByTimeAsync(READ_LEAVE_GRACE * 3);
    expect(asked('READ_LEAVE')).toHaveLength(0);

    two.leave();
    await vi.advanceTimersByTimeAsync(READ_LEAVE_GRACE);
    expect(asked('READ_LEAVE')).toHaveLength(1);
  });

  it('joins a read still running on a return within the grace, and leaves nothing', async () => {
    const shell = host();
    const client = new QueryClient();
    wait(client).leave();
    await vi.advanceTimersByTimeAsync(READ_LEAVE_GRACE - 1_000);

    const back = wait(client);
    await vi.advanceTimersByTimeAsync(READ_LEAVE_GRACE * 3);
    expect(asked('SESSION_DIFF')).toHaveLength(1);
    expect(asked('READ_LEAVE')).toHaveLength(0);

    shell.answer(0, DIFF);
    await expect(back.answer).resolves.toEqual(DIFF);
  });

  it('keeps a read answered within the grace for a return, and never leaves it', async () => {
    const shell = host();
    const client = new QueryClient();
    wait(client).leave();
    await vi.advanceTimersByTimeAsync(1_000);

    shell.answer(0, DIFF);
    await vi.advanceTimersByTimeAsync(READ_LEAVE_GRACE * 2);
    expect(asked('READ_LEAVE')).toHaveLength(0);
    // Put where a return reads first (REVIEW4).
    expect(client.getQueryData(keys.diff('s1a2b3c4'))).toEqual(DIFF);
  });
});
