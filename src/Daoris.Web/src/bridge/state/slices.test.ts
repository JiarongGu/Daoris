import { afterEach, describe, expect, it, vi } from 'vitest';
import { QueryClient } from '@tanstack/react-query';

// The push path's page half: a window installs a slice's snapshot as its baseline, applies the
// newer pushes and never an older one, and reads a partition again where the manifest says it missed one.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import { readSlice } from '../driver';
import { mergeKeyed, notPushed, pushedOf, readPushed, type SliceSpec } from './slices';

const KEY = ['test-slice'] as const;

/** A slice of two partitions, composed side by side, and merging a delta's keys into its partition. */
const spec: SliceSpec = {
  name: 'test',
  key: KEY,
  read: () => readSlice('test'),
  compose: (partitions) => {
    const a = partitions.get('a');
    const b = partitions.get('b');
    return a && b ? { ...(a as object), ...(b as object) } : undefined;
  },
  merge: (held, delta, removed) => {
    const merged: Record<string, unknown> = { ...(held as object), ...(delta as object) };
    for (const key of removed) delete merged[key];
    return merged;
  },
};

const snapshot = (a: number, b: number, epoch = 'e1') => ({
  slice: 'test', epoch, status: 'ready',
  partitions: [
    { partition: 'a', seq: a, status: 'ready', data: { a } },
    { partition: 'b', seq: b, status: 'ready', data: { b } },
  ],
});

const push = (partition: string, seq: number, data: unknown, more: Record<string, unknown> = {}) =>
  ({ slice: 'test', epoch: 'e1', partition, seq, status: 'ready', data, ...more });

/** How many first snapshots were read. */
const reads = () => invoke.mock.calls.filter(([, type]) => type === 'STATE_SLICE').length;

/** A window's store over a host answering `answers` in turn to its snapshot reads, its baseline installed. */
async function window(...answers: unknown[]) {
  for (const answer of answers) {
    invoke.mockImplementationOnce(async () => (answer instanceof Error ? Promise.reject(answer) : answer));
  }
  const client = new QueryClient();
  const store = pushedOf(client);
  store.register(spec);
  await store.resync('test');
  return { client, store, held: () => client.getQueryData(KEY) };
}

/** What the pump's batch hands the page in one go, and the microtasks after it. */
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('a pushed slice (PUSH1a)', () => {
  afterEach(() => invoke.mockReset());

  it('installs the snapshot it reads as its baseline, in the cache entry it feeds', async () => {
    const { store, held } = await window(snapshot(1, 1));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STATE_SLICE', { payload: { slice: 'test' } });
    expect(store.live('test')).toBe(true);
    expect(held()).toEqual({ a: 1, b: 1 });
  });

  it('never regresses on reversed delivery: an older push, however late, changes nothing', async () => {
    const { store, held } = await window(snapshot(1, 1));

    store.heard(push('a', 3, { a: 3 }));
    store.heard(push('a', 2, { a: 2 }));
    store.heard(push('a', 3, { a: 'again' }));

    expect(held()).toEqual({ a: 3, b: 1 });
  });

  it('buffers what it hears before its baseline, then applies the newer and drops the older', async () => {
    let answer: (value: unknown) => void = () => {};
    invoke.mockImplementationOnce(() => new Promise((resolve) => { answer = resolve; }));
    const client = new QueryClient();
    const store = pushedOf(client);
    store.register(spec);
    const asking = store.resync('test');

    store.heard(push('b', 4, { b: 4 }));
    store.heard(push('a', 1, { a: 'older' }));
    expect(client.getQueryData(KEY)).toBeUndefined();
    answer(snapshot(2, 2));
    await asking;

    expect(client.getQueryData(KEY)).toEqual({ a: 2, b: 4 });
  });

  it('reads again at once a delta heard before its baseline that does not fit it, instead of waiting for a manifest', async () => {
    let answer: (value: unknown) => void = () => {};
    invoke.mockImplementationOnce(() => new Promise((resolve) => { answer = resolve; }));
    invoke.mockImplementationOnce(async () => snapshot(7, 1));
    const client = new QueryClient();
    const store = pushedOf(client);
    store.register(spec);
    const asking = store.resync('test');

    store.heard(push('a', 8, { more: true }, { baseSeq: 7 }));
    answer(snapshot(2, 1));
    await asking;
    await store.asking('test');

    expect(reads()).toBe(2);
    expect(client.getQueryData(KEY)).toEqual({ a: 7, b: 1 });
  });

  it('repairs a dropped final update when the manifest names a newer number: the partition is read again once', async () => {
    const { store, held } = await window(snapshot(1, 1), snapshot(2, 1));

    // Push a@2 never arrives; the manifest after it says it exists.
    store.manifest({ slice: 'test', epoch: 'e1', partitions: [{ partition: 'a', seq: 2 }, { partition: 'b', seq: 1 }] });
    await settle();
    await store.asking('test');

    expect(reads()).toBe(2);
    expect(held()).toEqual({ a: 2, b: 1 });
  });

  it('asks nothing where the manifest matches what it holds, or is behind it', async () => {
    const { store } = await window(snapshot(2, 1));

    store.manifest({ slice: 'test', epoch: 'e1', partitions: [{ partition: 'a', seq: 2 }, { partition: 'b', seq: 1 }] });
    store.manifest({ slice: 'test', epoch: 'e1', partitions: [{ partition: 'a', seq: 1 }, { partition: 'b', seq: 1 }] });
    await settle();

    expect(reads()).toBe(1);
  });

  it('reads a manifest after the batch it came in: a push the pump coalesced past it asks nothing', async () => {
    const { store, held } = await window(snapshot(1, 1));

    // The pump kept a@3 in a@2's place after the manifest that named a@2.
    store.manifest({ slice: 'test', epoch: 'e1', partitions: [{ partition: 'a', seq: 2 }, { partition: 'b', seq: 1 }] });
    store.heard(push('a', 3, { a: 3 }));
    await settle();

    expect(reads()).toBe(1);
    expect(held()).toEqual({ a: 3, b: 1 });
  });

  it('reads the slice again whole where an owner of another epoch speaks', async () => {
    const { store, held } = await window(snapshot(5, 5), snapshot(1, 1, 'e2'));

    store.heard({ ...push('a', 1, { a: 'new owner' }), epoch: 'e2' });
    await store.asking('test');

    expect(reads()).toBe(2);
    expect(held()).toEqual({ a: 1, b: 1 });
  });

  it('applies a delta on its base through the slice, and reads again a delta on another base', async () => {
    const { store, held } = await window(snapshot(1, 1), snapshot(4, 1));

    store.heard(push('a', 2, { more: true }, { baseSeq: 1 }));
    expect(held()).toEqual({ a: 1, more: true, b: 1 });
    store.heard(push('a', 3, { gone: 1 }, { baseSeq: 2, removed: ['more'] }));
    expect(held()).toEqual({ a: 1, gone: 1, b: 1 });

    store.heard(push('a', 9, { lost: true }, { baseSeq: 7 }));
    await store.asking('test');

    expect(reads()).toBe(2);
    expect(held()).toEqual({ a: 4, b: 1 });
  });

  it('keeps a slice whose snapshot failed waiting, with no pull beside it, and reads it again at the next manifest (ONEWAY1ab-ii)', async () => {
    for (const failed of [new Error('NOT_READY'), null]) {
      invoke.mockReset();
      const { client, store } = await window(failed, snapshot(1, 1));
      expect(store.live('test')).toBe(false);
      expect(notPushed(client)({ queryKey: KEY } as never)).toBe(true);
      let read: unknown = 'waiting';
      void readPushed(client, 'test').then((data) => { read = data; });
      await settle();
      expect(read).toBe('waiting');

      store.manifest({ slice: 'test', epoch: 'e1', partitions: [] });
      await vi.waitFor(() => expect(store.live('test')).toBe(true));

      expect(reads()).toBe(2);
      await vi.waitFor(() => expect(read).toEqual({ a: 1, b: 1 }));
    }
  });

  it('reads the pushed entry from memory once live, and its key is the one a broad invalidation passes over', async () => {
    const { client } = await window(snapshot(1, 1));

    await expect(readPushed(client, 'test')).resolves.toEqual({ a: 1, b: 1 });
    expect(notPushed(client)({ queryKey: KEY } as never)).toBe(false);
    expect(notPushed(client)({ queryKey: [...KEY, 'beneath'] } as never)).toBe(true);
  });

  it('waits while the owner has not judged yet, and answers with its first push', async () => {
    const { client, store } = await window({ slice: 'test', epoch: 'e1', status: 'notJudged', partitions: [] });

    const reading = readPushed(client, 'test');
    store.heard(push('a', 1, { a: 1 }));
    store.heard(push('b', 1, { b: 1 }));

    await expect(reading).resolves.toEqual({ a: 1, b: 1 });
    expect(client.getQueryData(KEY)).toEqual({ a: 1, b: 1 });
  });

  it('answers a read made before the slice is registered, once its snapshot lands', async () => {
    const client = new QueryClient();
    const reading = readPushed(client, 'test');
    invoke.mockImplementationOnce(async () => snapshot(2, 3));
    pushedOf(client).register(spec);
    await pushedOf(client).resync('test');

    await expect(reading).resolves.toEqual({ a: 2, b: 3 });
  });

  it('merges a keyed delta: its keys over the held, the keys removed taken away, the held left as it was (PUSH1d2)', () => {
    const held = { s1: { state: 'working' }, s2: { state: 'working' } };

    const merged = mergeKeyed(held, { s1: { state: 'completed' }, s3: { state: 'queued' } }, ['s2']);

    expect(merged).toEqual({ s1: { state: 'completed' }, s3: { state: 'queued' } });
    expect(held.s2).toEqual({ state: 'working' });
    // A delta of any other shape changes nothing but what it removes.
    expect(mergeKeyed(held, null, ['s1'])).toEqual({ s2: { state: 'working' } });
  });

  it('tells its compose whether the owner judged: a judged slice of no partition composes, a slice not judged waits (PUSH1d2)', async () => {
    /** A list of every partition's rows, which is empty and true once the owner judged none. */
    const listed: SliceSpec = {
      name: 'listed', key: ['listed'], read: () => readSlice('listed'),
      compose: (partitions, judged) => (judged ? [...partitions.values()].flat() : undefined),
    };
    const open = async (answer: unknown) => {
      invoke.mockImplementationOnce(async () => answer);
      const client = new QueryClient();
      pushedOf(client).register(listed);
      await pushedOf(client).resync('listed');
      return client;
    };

    const none = await open({ slice: 'listed', epoch: 'e1', status: 'ready', partitions: [] });
    await expect(readPushed(none, 'listed')).resolves.toEqual([]);
    expect(none.getQueryData(['listed'])).toEqual([]);

    const notYet = await open({ slice: 'listed', epoch: 'e1', status: 'notJudged', partitions: [] });
    expect(pushedOf(notYet).data('listed')).toBeUndefined();
    expect(notYet.getQueryData(['listed'])).toBeUndefined();

    // Its only partition gone: judged, and empty, never the last rows held.
    const one = await open({ slice: 'listed', epoch: 'e1', status: 'ready', partitions: [{ partition: 'w', seq: 1, status: 'ready', data: ['s1'] }] });
    expect(one.getQueryData(['listed'])).toEqual(['s1']);
    pushedOf(one).heard({ slice: 'listed', epoch: 'e1', partition: 'w', seq: 2, status: 'gone', data: null });
    expect(one.getQueryData(['listed'])).toEqual([]);
  });

  it('waits for the first snapshot under way', async () => {
    let answer: (value: unknown) => void = () => {};
    invoke.mockImplementationOnce(() => new Promise((resolve) => { answer = resolve; }));
    const client = new QueryClient();
    const store = pushedOf(client);
    store.register(spec);
    void store.resync('test');

    const reading = readPushed(client, 'test');
    answer(snapshot(1, 1));

    await expect(reading).resolves.toEqual({ a: 1, b: 1 });
    expect(invoke.mock.calls.map(([, type]) => type)).toEqual(['STATE_SLICE']);
  });

  it('says a live slice read again is resyncing, holding what it held, and a first read is not (PUSHUX1)', async () => {
    let answer: (value: unknown) => void = () => {};
    invoke.mockImplementationOnce(() => new Promise((resolve) => { answer = resolve; }));
    const client = new QueryClient();
    const store = pushedOf(client);
    store.register(spec);
    const told = vi.fn();
    store.subscribe(told);

    const first = store.resync('test');
    expect(store.resyncing('test')).toBe(false);
    answer(snapshot(1, 1));
    await first;

    invoke.mockImplementationOnce(() => new Promise((resolve) => { answer = resolve; }));
    told.mockClear();
    store.heard({ ...push('a', 1, { a: 'new owner' }), epoch: 'e2' });
    expect(store.resyncing('test')).toBe(true);
    expect(told).toHaveBeenCalled();
    expect(client.getQueryData(KEY)).toEqual({ a: 1, b: 1 });

    answer(snapshot(2, 2, 'e2'));
    await store.asking('test');
    expect(store.resyncing('test')).toBe(false);
    expect(client.getQueryData(KEY)).toEqual({ a: 2, b: 2 });
  });
});
