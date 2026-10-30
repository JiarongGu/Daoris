import { afterEach, describe, expect, it, vi } from 'vitest';
import { hashKey, QueryClient, QueryObserver } from '@tanstack/react-query';

// COWORKVIEW1a: who is at work, the shell's one numbered read (the cowork view design §4.1, rule 15).

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));
vi.mock('@shenora/react', () => ({ getBridge: () => ({ isAvailable: true, invoke }) }));

import { answered } from '../state/held';
import { askAtWork, atWorkKey, type AtWorkHeld } from './atWork';

const key = atWorkKey('s1');
const held = hashKey(key);

const answer = (rows: number, n: number, epoch = 'e1') => ({
  answer: { of: { session: 's1' }, attended: 's1', parts: [], subagents: { read: 'read', working: rows, children: [] } },
  at: { epoch, n },
});

const fetch = (client: QueryClient) => client.fetchQuery({ queryKey: key, queryFn: () => askAtWork(client, key, 's1'), retry: false });

const working = (client: QueryClient) => {
  const data = client.getQueryData<AtWorkHeld>(key);
  return data && 'answer' in data ? data.answer.subagents.working : null;
};

describe('who is at work, a numbered read (COWORKVIEW1a)', () => {
  afterEach(() => invoke.mockReset());

  it('keeps the newest whatever order the deliveries come in, and an equal number changes nothing', async () => {
    const client = new QueryClient();
    invoke.mockResolvedValue(answer(1, 5));
    await fetch(client);

    answered(client, { held, answer: answer(3, 7) });
    answered(client, { held, answer: answer(2, 6) });
    answered(client, { held, answer: answer(9, 7) });

    expect(working(client)).toBe(3);
  });

  it('never writes an older direct reply over a newer event, nor an old failure over a recovery', async () => {
    const client = new QueryClient();
    invoke.mockResolvedValue(answer(1, 5));
    await fetch(client);
    answered(client, { held, answer: answer(4, 8) });

    invoke.mockResolvedValue({ failed: { why: 'refused' }, at: { epoch: 'e1', n: 6 } });
    await fetch(client);

    expect(working(client)).toBe(4);
  });

  it('holds an event beside its first fetch and writes the reply first, so E1’s answer is never drawn after E2’s', async () => {
    const client = new QueryClient();
    let reply: (value: unknown) => void = () => {};
    invoke.mockReturnValueOnce(new Promise((resolve) => { reply = resolve; }));
    invoke.mockResolvedValue(answer(2, 1, 'e2'));
    // Mounted, as the view's query is, so asking again fetches.
    const mounted = new QueryObserver(client, { queryKey: key, queryFn: () => askAtWork(client, key, 's1'), retry: false });
    const drawn: (number | null)[] = [];
    const stop = mounted.subscribe(({ data }) => {
      const now = data && 'answer' in data ? data.answer.subagents.working : null;
      if (now !== null && drawn.at(-1) !== now) drawn.push(now);
    });

    await vi.waitFor(() => expect(invoke).toHaveBeenCalledTimes(1));
    answered(client, { held, answer: answer(2, 1, 'e2') });
    expect(client.getQueryData(key)).toBeUndefined();
    reply(answer(1, 5, 'e1'));

    await vi.waitFor(() => expect(drawn).toEqual([1, 2]));
    stop();
  });

  it('takes an event of n 7 held beside a fetch whose reply says n 5', async () => {
    const client = new QueryClient();
    let reply: (value: unknown) => void = () => {};
    invoke.mockReturnValueOnce(new Promise((resolve) => { reply = resolve; }));

    const first = fetch(client);
    await Promise.resolve();
    answered(client, { held, answer: answer(7, 7) });
    reply(answer(5, 5));
    await first;

    expect(working(client)).toBe(7);
  });

  it('reads a failure with its words, a stale answer over its rows, and the composition’s pending answer numbered', async () => {
    const client = new QueryClient();
    invoke.mockResolvedValue({ failed: { why: 'the sessions could not be read' }, at: { epoch: 'e1', n: 1 } });
    await fetch(client);
    expect(client.getQueryData(key)).toEqual({ failed: { why: 'the sessions could not be read' }, at: { epoch: 'e1', n: 1 } });

    answered(client, { held, answer: { ...answer(2, 2), stale: { why: 'timed out' } } });
    expect(client.getQueryData<AtWorkHeld>(key)).toMatchObject({ stale: { why: 'timed out' } });

    answered(client, { held, answer: { pending: true, waits: 'record', at: { epoch: 'e1', n: 3 } } });
    answered(client, { held, answer: answer(2, 2) });
    expect(client.getQueryData(key)).toEqual({ reading: true, at: { epoch: 'e1', n: 3 } });
  });

  it('keeps what it holds when the shell has nothing held yet, which carries no number', async () => {
    const client = new QueryClient();
    invoke.mockResolvedValue(answer(1, 5));
    await fetch(client);
    invoke.mockResolvedValue({ pending: true });
    await fetch(client);

    expect(working(client)).toBe(1);
  });
});
