import { useEffect, useSyncExternalStore } from 'react';
import { type Query, type QueryClient, type QueryKey, hashKey, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';

/**
 * A slice of the machine's state the shell pushes with its data (PUSH1a, D182): which cache entry its partitions feed, and how.
 * The page's store stays its query cache (D182 point 7): a pushed entry is written by the pushes and read from memory, and every
 * other family keeps its pulls until its own slice moves.
 */
export type SliceSpec = {
  /** The slice's name, as the shell's owner names it. */
  name: string;
  /** The one cache entry its partitions feed: this key exactly, never what is read beneath it. */
  key: QueryKey;
  /** Its snapshot, as the shell answers it (`readSlice`): a host older than the push refuses it, or answers nothing. */
  read: () => Promise<unknown>;
  /** The entry's data from the partitions held as judged, or undefined while one it needs is not judged yet. */
  compose: (partitions: ReadonlyMap<string, unknown>) => unknown;
  /** A delta's data applied to its partition; absent where the slice pushes whole replacements only, and a delta is read again whole. */
  merge?: (held: unknown, delta: unknown, removed: readonly string[]) => unknown;
};

const READY = 'ready';
const GONE = 'gone';

/** At most this many pushes wait for a slice's baseline; past it the oldest go, which the baseline's read covers. */
const WAITING = 64;

type Push = {
  slice: string; epoch: string; partition: string; seq: number; status: string; data: unknown; baseSeq: number | null; removed: string[];
};
type Partition = { partition: string; seq: number; status: string; data: unknown };
type Snapshot = { epoch: string; partitions: Partition[] };
type Manifest = { slice: string; epoch: string; partitions: { partition: string; seq: number }[] };

type Held = { seq: number; status: string; data: unknown };

/**
 * One slice in one window: unknown until its snapshot answers, then live (installed, fed by pushes) or pulled (an older host, or
 * one that pushes no such slice).
 */
type Slice = {
  spec: SliceSpec;
  mode: 'unknown' | 'live' | 'pull';
  epoch: string | null;
  partitions: Map<string, Held>;
  /** What was heard before the baseline, applied after it where newer. */
  waiting: Push[];
  asking: Promise<void> | null;
};

const named = (value: unknown): value is string => typeof value === 'string' && value.length > 0;
const numbered = (value: unknown): value is number => typeof value === 'number' && Number.isSafeInteger(value) && value >= 0;

function pushOf(value: unknown): Push | null {
  const push = value as Record<string, unknown> | null | undefined;
  if (!push || !named(push.slice) || !named(push.epoch) || !named(push.partition) || !numbered(push.seq) || !named(push.status)) return null;
  return {
    slice: push.slice, epoch: push.epoch, partition: push.partition, seq: push.seq, status: push.status, data: push.data,
    baseSeq: numbered(push.baseSeq) ? push.baseSeq : null,
    removed: Array.isArray(push.removed) ? push.removed.filter(named) : [],
  };
}

function partitionOf(value: unknown): Partition | null {
  const partition = value as Record<string, unknown> | null | undefined;
  if (!partition || !named(partition.partition) || !numbered(partition.seq) || !named(partition.status)) return null;
  return { partition: partition.partition, seq: partition.seq, status: partition.status, data: partition.data };
}

/** A snapshot of the slice asked for, or null: an answer of any other shape is a host that pushes no such slice. */
function snapshotOf(value: unknown, slice: string): Snapshot | null {
  const snapshot = value as Record<string, unknown> | null | undefined;
  if (!snapshot || snapshot.slice !== slice || !named(snapshot.epoch) || !Array.isArray(snapshot.partitions)) return null;
  return { epoch: snapshot.epoch, partitions: snapshot.partitions.map(partitionOf).filter((each): each is Partition => each !== null) };
}

function manifestOf(value: unknown): Manifest | null {
  const manifest = value as Record<string, unknown> | null | undefined;
  if (!manifest || !named(manifest.slice) || !named(manifest.epoch) || !Array.isArray(manifest.partitions)) return null;
  return {
    slice: manifest.slice,
    epoch: manifest.epoch,
    partitions: manifest.partitions.flatMap((each: unknown) => {
      const version = each as { partition?: unknown; seq?: unknown } | null;
      return version && named(version.partition) && numbered(version.seq) ? [{ partition: version.partition, seq: version.seq }] : [];
    }),
  };
}

/**
 * What one window holds of the slices it reads pushed (D182 points 3 and 4): kept beside its query client, so every window keeps
 * its own and a second window reads its own snapshot once.
 */
class Pushed {
  private readonly slices = new Map<string, Slice>();
  private readonly listeners = new Set<() => void>();

  constructor(private readonly client: QueryClient) {}

  /** Read a slice pushed from now: what is heard of it is kept until its snapshot answers. */
  register(spec: SliceSpec) {
    if (!this.slices.has(spec.name)) {
      this.slices.set(spec.name, { spec, mode: 'unknown', epoch: null, partitions: new Map(), waiting: [], asking: null });
    }
  }

  /** For `useSyncExternalStore`: told when a slice becomes live or pulled. */
  readonly subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => { this.listeners.delete(listener); };
  };

  private told() {
    for (const listener of [...this.listeners]) listener();
  }

  /** Whether the slice is installed and fed by pushes in this window. */
  live(name: string): boolean {
    return this.slices.get(name)?.mode === 'live';
  }

  /** The slice's first snapshot, or a read of it again, while one is on its way. */
  asking(name: string): Promise<void> | null {
    return this.slices.get(name)?.asking ?? null;
  }

  /** The pushed entry's data from memory, or undefined where the slice is not live or a partition it needs is not judged yet. */
  data(name: string): unknown {
    const slice = this.slices.get(name);
    return slice?.mode === 'live' ? slice.spec.compose(ready(slice)) : undefined;
  }

  /** Whether `queryKey` is a live slice's own entry, which the pushes write and no broad invalidation asks again. */
  feeds(queryKey: QueryKey): boolean {
    const hash = hashKey(queryKey);
    for (const slice of this.slices.values()) {
      if (slice.mode === 'live' && hashKey(slice.spec.key) === hash) return true;
    }
    return false;
  }

  /** A `STATE_PUSH`: applied where newer, kept where the baseline has not answered, and ignored where the slice is pulled. */
  heard(value: unknown) {
    const push = pushOf(value);
    const slice = push ? this.slices.get(push.slice) : undefined;
    if (!push || !slice || slice.mode === 'pull') return;
    if (slice.mode === 'unknown' || slice.asking) {
      slice.waiting.push(push);
      if (slice.waiting.length > WAITING) slice.waiting.shift();
      return;
    }
    if (this.apply(slice, push)) this.write(slice);
  }

  /**
   * A `STATE_MANIFEST`: read once the batch it came in has been applied, since the pump keeps a coalesced push in the later place,
   * and where it names a number this window does not hold, the slice is read again (a dropped batch, its final update among them).
   */
  manifest(value: unknown) {
    const manifest = manifestOf(value);
    const slice = manifest ? this.slices.get(manifest.slice) : undefined;
    if (!manifest || slice?.mode !== 'live') return;
    queueMicrotask(() => {
      if (slice.asking) return;
      const behind = manifest.epoch !== slice.epoch
        || manifest.partitions.some((version) => (slice.partitions.get(version.partition)?.seq ?? 0) < version.seq);
      if (behind) void this.resync(manifest.slice);
    });
  }

  /**
   * Read the slice's snapshot and install it as the baseline (D182 point 3): asked once its pushes are heard, as a window mounts,
   * and again where the manifest or a push says this window missed one. A host that knows no such route or pushes no such slice
   * leaves it pulled; a live slice whose read failed keeps what it holds, and the next manifest asks again.
   */
  resync(name: string): Promise<void> {
    const slice = this.slices.get(name);
    if (!slice) return Promise.resolve();
    if (slice.asking) return slice.asking;
    const asking = (async () => {
      let snapshot: Snapshot | null = null;
      try {
        snapshot = snapshotOf(await slice.spec.read(), name);
      } catch {
        snapshot = null;
      }
      if (snapshot) {
        this.install(slice, snapshot);
      } else if (slice.mode !== 'live') {
        slice.mode = 'pull';
        slice.waiting = [];
      }
    })();
    slice.asking = asking.finally(() => {
      slice.asking = null;
      this.told();
    });
    return slice.asking;
  }

  private install(slice: Slice, snapshot: Snapshot) {
    if (snapshot.epoch !== slice.epoch) {
      slice.epoch = snapshot.epoch;
      slice.partitions = new Map();
    }
    const judged = new Set(snapshot.partitions.map((each) => each.partition));
    for (const [partition, held] of slice.partitions) {
      // Not in the owner's snapshot: no longer judged, kept by its number so an older push of it changes nothing.
      if (!judged.has(partition)) slice.partitions.set(partition, { seq: held.seq, status: GONE, data: undefined });
    }
    for (const each of snapshot.partitions) {
      const held = slice.partitions.get(each.partition);
      if (!held || each.seq > held.seq) slice.partitions.set(each.partition, { seq: each.seq, status: each.status, data: each.data });
    }
    slice.mode = 'live';
    const waiting = slice.waiting.sort((a, b) => a.seq - b.seq);
    slice.waiting = [];
    // A push of another owner's life than this answer's is settled by the next manifest.
    for (const push of waiting) if (push.epoch === slice.epoch) this.apply(slice, push);
    this.write(slice);
  }

  /** Whether `push` changed what is held: an older or equal number never does, however late it came. */
  private apply(slice: Slice, push: Push): boolean {
    if (push.epoch !== slice.epoch) {
      void this.resync(slice.spec.name);
      return false;
    }
    const held = slice.partitions.get(push.partition);
    if (held && push.seq <= held.seq) return false;
    if (push.baseSeq === null) {
      slice.partitions.set(push.partition, { seq: push.seq, status: push.status, data: push.status === GONE ? undefined : push.data });
      return true;
    }
    // A delta needs its base: a missed one, or a slice that pushes none, is read again whole.
    if (!held || held.seq !== push.baseSeq || held.status !== READY || !slice.spec.merge) {
      void this.resync(slice.spec.name);
      return false;
    }
    slice.partitions.set(push.partition, { seq: push.seq, status: push.status, data: slice.spec.merge(held.data, push.data, push.removed) });
    return true;
  }

  private write(slice: Slice) {
    const data = slice.spec.compose(ready(slice));
    if (data !== undefined) this.client.setQueryData(slice.spec.key, data);
  }
}

/** The partitions held as judged, by name. */
const ready = (slice: Slice): ReadonlyMap<string, unknown> =>
  new Map([...slice.partitions].filter(([, held]) => held.status === READY).map(([partition, held]) => [partition, held.data]));

const stores = new WeakMap<QueryClient, Pushed>();

/** What `client`'s window holds of its pushed slices. */
export function pushedOf(client: QueryClient): Pushed {
  let store = stores.get(client);
  if (!store) {
    store = new Pushed(client);
    stores.set(client, store);
  }
  return store;
}

/**
 * A pushed slice's entry as a query reads it: from memory where the slice is live, after its first snapshot where one is on its
 * way, so a window opening reads the snapshot and never the pull beside it; the pull otherwise (no shell, an older host, a slice
 * not judged yet).
 */
export async function readPushed<T>(client: QueryClient, name: string, pull: () => Promise<T>): Promise<T> {
  const store = pushedOf(client);
  const asking = store.asking(name);
  if (asking) await asking;
  const data = store.data(name);
  return data === undefined ? pull() : (data as T);
}

/**
 * For a broad invalidation: every query but a live slice's own entry, which its pushes write (D182 point 7), so a tick or a scope
 * asks again what is read beneath it and never the pushed entry.
 */
export const notPushed = (client: QueryClient) => (query: Pick<Query, 'queryKey'>) => !pushedOf(client).feeds(query.queryKey);

/** Whether `name` is live in this window: a pushed entry's query asks nothing on a focus or as it goes stale. */
export function usePushedLive(name: string): boolean {
  const store = pushedOf(useQueryClient());
  return useSyncExternalStore(store.subscribe, () => store.live(name), () => false);
}

/**
 * The page's half of the push path (PUSH1a, D182): each slice's pushes and manifests heard, then each slice's snapshot asked, in
 * that order, so nothing pushed while the snapshot is on its way is missed. Mounted by `ShellSignals` in both roots, so every
 * window holds its own; mounted again, it reads each snapshot again. A plain browser has no bus and keeps its pulls.
 */
export function useStateSlices(specs: readonly SliceSpec[]) {
  const client = useQueryClient();
  const { isAvailable } = useShenora();
  useShenoraEvent<unknown>('DAORIS', 'STATE_PUSH', (push) => pushedOf(client).heard(push));
  useShenoraEvent<unknown>('DAORIS', 'STATE_MANIFEST', (manifest) => pushedOf(client).manifest(manifest));
  useEffect(() => {
    if (!isAvailable) return;
    const store = pushedOf(client);
    for (const spec of specs) {
      store.register(spec);
      void store.resync(spec.name);
    }
  }, [client, isAvailable, specs]);
}
