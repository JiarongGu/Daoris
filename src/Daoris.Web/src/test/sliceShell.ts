import { useContext } from 'react';
import { type QueryClient, QueryClientContext } from '@tanstack/react-query';
import { vi } from 'vitest';
import { PUBLISHES } from '../bridge/generated/routes';

/**
 * The shell's pushed slices, for a mocked shell (ONEWAY1ab-ii, D182 point 3): the page reads every slice from its snapshot alone, so
 * a test that answers the routes a slice is judged from has its shell answer `STATE_SLICE` from those same answers, one way to stand
 * a shell up. A test that answers `STATE_SLICE` itself is answered as it says. The service's lists (the sessions, the quests, the
 * asks) and where each wired circle stands are judged from what the test's `fetch` answers for them; a list nothing answers is judged
 * empty. The attention, the notices and the considerations are judged from nothing a route answers, so a test that reads them answers
 * their slice itself.
 *
 * The shell's owner reads those answers itself, never through the page: a mocked `invoke` or `fetch` is read by its own implementation,
 * so its reads are not the page's calls and take no answer a test queued for the page. After a press the shell declares, it pushes
 * each partition whose judgement moved, as the shell's owner does; a test that moves an answer without a press says so (`moved`).
 *
 * A mocked shell's `useShenora` is `holdingSlices`: each window it is asked from holds every slice as `ShellSignals` mounts them in a
 * window, its snapshots read once, so a test renders what it tests and no more.
 *
 * This file imports nothing of the page at its load, so a test's `vi.mock` factory may load it lazily, before the module it mocks
 * exists; the page's slices are read once a window asks for them.
 */

type Invoke = (module: string, type: string, options?: { payload?: unknown }) => Promise<unknown>;
type Ask = (type: string, payload?: unknown) => Promise<unknown>;
type Partitions = Record<string, unknown> | null;

/** Each window's store of pushed slices, and the slices `ShellSignals` mounts, read once the mocked module exists. */
const page = () => Promise.all([import('../bridge/state/slices'), import('../app/ShellSignals')])
  .then(([{ pushedOf }, { PUSHED }]) => ({ pushedOf, PUSHED }));

/** What the shell holds of a partition it judged once and judges no longer. */
const GONE = '<gone>';

const object = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);
const list = (value: unknown): unknown[] => (Array.isArray(value) ? value : []);
const byKey = (rows: unknown[], key: (row: Record<string, unknown>) => unknown) =>
  Object.fromEntries(rows.filter(object).map((row) => [String(key(row)), row]));

/** A mock answered by its own implementation, unrecorded; anything else as it is. */
function quietly<F extends (...args: never[]) => unknown>(answer: F): (...args: Parameters<F>) => unknown {
  if (!vi.isMockFunction(answer)) return answer;
  return (...args) => (answer.getMockImplementation() as ((...all: Parameters<F>) => unknown) | undefined)?.(...args);
}

/** An answer read whatever it is: a value, a promise, or a throw, which reads as nothing. */
async function settled(read: () => unknown): Promise<unknown> {
  try {
    return await read();
  } catch {
    return undefined;
  }
}

/** What the test's `fetch` answers for `url`, read from a copy so the page may read the same answer; nothing where it answers none. */
async function served(url: string): Promise<unknown> {
  const answer = await settled(() => quietly(globalThis.fetch)(url));
  if (!(answer instanceof Response) || !answer.ok) return undefined;
  return settled(() => answer.clone().json());
}

/** One of the service's lists, every workspace's with the closed. */
const servedList = async (path: string) => list(await served(`${path}?includeClosed=true`));

/** Where a session goes by its record alone, as the reader's first step places it (`SessionGroups.Place`). */
const placed = (session: Record<string, unknown>) =>
  !['queued', 'starting', 'working', 'awaiting-person'].includes(String(session.state)) ? 'ended'
    : session.state === 'awaiting-person' ? 'you' : 'working';

/**
 * The entries where the test answers none: each session its own line of work, as the reader answers a session nothing else joins,
 * in the group its row says or its record places it.
 */
function ownEntries(sessions: unknown[], rows: Record<string, unknown>) {
  return sessions.filter(object).map((session) => {
    const id = String(session.id);
    const group = object(rows[id]) ? String(rows[id].group) : placed(session);
    const repository = String(session.repository);
    return {
      entry: `session:${id}/${repository}`, origin: `session:${id}`, repository, group, leading: id,
      parts: [{
        session: id, quest: typeof session.quest === 'string' ? session.quest : null, shown: String(session.state), group,
        began: 'opened', after: null, askedBy: null,
      }],
    };
  });
}

/** Each slice's partitions, from the answers of the route the shell's owner judges it from; null where it is not judged. */
const SLICES: Record<string, (ask: Ask, remotes: Ask) => Promise<Partitions>> = {
  driver: async (ask) => {
    const state = await ask('STATE');
    return object(state) ? { shell: state, choices: {}, running: {} } : null;
  },
  lines: async (ask) => {
    const lines = await ask('LINES');
    return { all: object(lines) ? lines : { lines: [] } };
  },
  sweep: async (ask) => {
    const sweep = await ask('SWEEP_PLAN', {});
    return { all: object(sweep) ? sweep : { branches: [], landed: [] } };
  },
  work: async (ask) => {
    const work = await ask('WORK_STATE', {});
    const whole = object(work) ? work : {};
    return { all: whole, '<held>': whole };
  },
  groups: async (ask) => {
    const groups = await ask('SESSION_GROUPS', {});
    const rows = byKey(object(groups) ? list(groups.sessions) : [], (row) => row.session);
    const entries = object(groups) && Array.isArray(groups.entries)
      ? groups.entries : ownEntries(await servedList('/api/sessions'), rows);
    return { rows, entries: byKey(entries, (entry) => entry.entry) };
  },
  calls: async (ask) => {
    const groups = await ask('SESSION_GROUPS', {});
    const rows = (object(groups) ? list(groups.sessions) : []).filter(object).filter((row) => object(row.calling));
    return { calling: Object.fromEntries(rows.map((row) => [String(row.session), row.calling])) };
  },
  accounts: async (ask) => {
    const accounts = await ask('ACCOUNTS');
    return byKey(object(accounts) ? list(accounts.agents) : [], (agent) => agent.agent);
  },
  usage: async (ask) => {
    const usage = await ask('USAGE');
    return {
      sessions: byKey(object(usage) ? list(usage.sessions) : [], (used) => used.session),
      accounts: byKey(object(usage) ? list(usage.accounts) : [], (used) => `${String(used.harness)}/${String(used.profile ?? '')}`),
    };
  },
  rules: async (ask) => {
    const rules = await ask('RULES');
    return object(rules) && Array.isArray(rules.scopes) ? { rules } : null;
  },
  sessions: async () => ({ default: byKey(await servedList('/api/sessions'), (record) => record.id) }),
  quests: async () => ({ default: byKey(await servedList('/api/quests'), (record) => record.id) }),
  asks: async () => ({ default: byKey(await servedList('/api/asks'), (record) => record.id) }),
  // Each wired circle's standing, as `/api/sync` answers it for that circle; a circle it answers none for is unwired.
  sync: async (_ask, remotes) => {
    const wiring = await remotes('STATE');
    const circles = (object(wiring) ? list(wiring.remotes) : []).filter(object).map((remote) => String(remote.workspace));
    const standings = await Promise.all(circles.map(async (circle) => [circle, await served(`/api/sync?workspace=${encodeURIComponent(circle)}`)] as const));
    return Object.fromEntries(standings.filter(([, standing]) => object(standing)));
  },
};

/** A slice's snapshot as the shell answers it: each partition ready at its number, or not judged where it has none. */
function snapshotOf(slice: string, partitions: Partitions, seq: (partition: string) => number) {
  return {
    slice, epoch: 'test', status: partitions ? 'ready' : 'notJudged',
    partitions: Object.entries(partitions ?? {}).map(([partition, data]) => ({ partition, seq: seq(partition), status: 'ready', data })),
  };
}

/** A mocked shell's `invoke` with its slices answered, and the push of what moved. */
export type SliceShell = Invoke & {
  /** Push each partition whose judgement moved since the page last read it, as the shell's owner does when an answer moves. */
  moved: () => Promise<void>;
  /** Hold every slice in the window of `client`, as `ShellSignals` mounts them: each snapshot read once. */
  hold: (client: QueryClient) => void;
};

/**
 * `invoke` with the shell's `STATE_SLICE` answered: as the test answers it where it does, else from the answers of the routes the
 * slice is judged from, read from `routes` (by default `invoke` itself). A route the test refuses reads as answering nothing. Each
 * partition a declared press moved is pushed to every window held, once the press answers.
 */
export function answeringSlices(invoke: Invoke, routes: Invoke = invoke): SliceShell {
  const ask = (module: string): Ask => (route, payload) =>
    settled(() => quietly(routes)(module, route, payload === undefined ? undefined : { payload }));
  /** What the page was last answered of each judged partition, and at which number, by slice. */
  const told = new Map<string, Map<string, { seq: number; data: string }>>();
  let nextSeq = 0;
  /** The windows that hold the slices, as each test's own window is. */
  const windows = new Set<QueryClient>();

  const hold = (client: QueryClient) => {
    if (windows.has(client)) return;
    windows.add(client);
    void page().then(({ pushedOf, PUSHED }) => {
      const store = pushedOf(client);
      for (const spec of PUSHED) {
        store.register(spec);
        void store.resync(spec.name);
      }
    });
  };

  const hear = async (push: unknown) => {
    const { pushedOf } = await page();
    for (const client of windows) pushedOf(client).heard(push);
  };

  const judge = async (slice: string): Promise<Partitions> =>
    (SLICES[slice] ? SLICES[slice](ask('DAORIS.DRIVER'), ask('DAORIS.REMOTES')) : null);

  /** The slice's partitions numbered: a partition whose judgement is what the page was told keeps its number. */
  const numbered = (slice: string, partitions: Partitions) => {
    const held = told.get(slice) ?? new Map<string, { seq: number; data: string }>();
    told.set(slice, held);
    const moved: string[] = [];
    for (const [partition, data] of Object.entries(partitions ?? {})) {
      const now = JSON.stringify(data);
      if (held.get(partition)?.data === now) continue;
      held.set(partition, { seq: ++nextSeq, data: now });
      moved.push(partition);
    }
    // A partition no longer judged is gone, once: its number moves where it went.
    const gone = [...held].filter(([partition, was]) => was.data !== GONE && !(partitions && partition in partitions)).map(([partition]) => partition);
    for (const partition of gone) held.set(partition, { seq: ++nextSeq, data: GONE });
    return { seq: (partition: string) => held.get(partition)?.seq ?? 0, moved, gone };
  };

  const moved = async () => {
    for (const slice of [...told.keys()]) {
      const partitions = await judge(slice);
      if (!partitions) continue;
      const { seq, moved: changed, gone } = numbered(slice, partitions);
      for (const partition of changed) {
        await hear({ slice, epoch: 'test', partition, seq: seq(partition), status: 'ready', data: partitions[partition], baseSeq: null });
      }
      for (const partition of gone) {
        await hear({ slice, epoch: 'test', partition, seq: seq(partition), status: 'gone', data: null, baseSeq: null });
      }
    }
  };

  const shell = async (module: string, type: string, options?: { payload?: unknown }) => {
    if (module !== 'DAORIS.DRIVER' || type !== 'STATE_SLICE') {
      try {
        return await invoke(module, type, options);
      } finally {
        // What a declared press moved is pushed once it answered, after the state hub was told.
        if (PUBLISHES[module]?.[type]) setTimeout(() => void moved(), 0);
      }
    }
    const slice = String((options?.payload as { slice?: unknown } | undefined)?.slice ?? '');
    const own = await settled(() => invoke(module, type, options));
    if (object(own) && own.slice === slice) return own;
    const partitions = await judge(slice);
    return snapshotOf(slice, partitions, numbered(slice, partitions).seq);
  };
  return Object.assign(shell, { moved, hold });
}

/**
 * A mocked shell's `useShenora`: `answer` as the test gives it, and where it says a shell is here, the window it is asked from holds
 * the shell's slices (`SliceShell.hold`), as every window of a shell does.
 */
export function holdingSlices<T extends { isAvailable: boolean }>(shell: SliceShell, answer: T): T {
  const client = useContext(QueryClientContext);
  if (answer.isAvailable && client) shell.hold(client);
  return answer;
}
