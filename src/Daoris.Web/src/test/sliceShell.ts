/**
 * The shell's pushed slices, for a mocked shell (ONEWAY1ab-ii, D182 point 3): the page reads every slice from its snapshot alone, so
 * a test that answers the routes a slice is judged from has its shell answer `STATE_SLICE` from those same answers, one way to stand
 * a shell up. A test that answers `STATE_SLICE` itself is answered as it says. The service's lists (the sessions, the quests, the
 * asks) are judged from what the test's `fetch` answers for them; a list nothing answers is judged empty. The attention, the notices,
 * the considerations and where each circle stands are judged from nothing a route answers, so a test that reads them answers their
 * slice itself.
 *
 * This file imports nothing of the page, so a test's `vi.mock` factory may load it lazily, before the module it mocks exists.
 */

type Invoke = (module: string, type: string, options?: { payload?: unknown }) => Promise<unknown>;
type Ask = (type: string, payload?: unknown) => Promise<unknown>;
type Partitions = Record<string, unknown> | null;

const object = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);
const list = (value: unknown): unknown[] => (Array.isArray(value) ? value : []);
const byKey = (rows: unknown[], key: (row: Record<string, unknown>) => unknown) =>
  Object.fromEntries(rows.filter(object).map((row) => [String(key(row)), row]));

/** What the test's `fetch` answers for one of the service's lists, every workspace's with the closed; none where it answers none. */
async function served(path: string): Promise<unknown[]> {
  try {
    const answer = await fetch(`${path}?includeClosed=true`);
    return answer.ok ? list(await answer.json()) : [];
  } catch {
    return [];
  }
}

/** Each slice's partitions, from the answers of the route the shell's owner judges it from; null where it is not judged. */
const SLICES: Record<string, (ask: Ask) => Promise<Partitions>> = {
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
    const rows = object(groups) ? list(groups.sessions) : [];
    const entries = object(groups) ? list(groups.entries) : [];
    return { rows: byKey(rows, (row) => row.session), entries: byKey(entries, (entry) => entry.entry) };
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
    return object(rules) ? { rules } : null;
  },
  sessions: async () => ({ default: byKey(await served('/api/sessions'), (record) => record.id) }),
  quests: async () => ({ default: byKey(await served('/api/quests'), (record) => record.id) }),
  asks: async () => ({ default: byKey(await served('/api/asks'), (record) => record.id) }),
  sync: async () => ({}),
};

/** A slice's snapshot as the shell answers it: each partition ready at its first number, or not judged where it has none. */
function snapshotOf(slice: string, partitions: Partitions) {
  return {
    slice, epoch: 'test', status: partitions ? 'ready' : 'notJudged',
    partitions: Object.entries(partitions ?? {}).map(([partition, data]) => ({ partition, seq: 1, status: 'ready', data })),
  };
}

/**
 * `invoke` with the shell's `STATE_SLICE` answered: as the test answers it where it does, else from the answers of the routes the
 * slice is judged from. A route the test refuses reads as answering nothing.
 */
export function answeringSlices(invoke: Invoke): Invoke {
  return async (module, type, options) => {
    if (module !== 'DAORIS.DRIVER' || type !== 'STATE_SLICE') return invoke(module, type, options);
    const slice = String((options?.payload as { slice?: unknown } | undefined)?.slice ?? '');
    const own = await invoke(module, type, options).catch(() => undefined);
    if (object(own) && own.slice === slice) return own;
    const judge = SLICES[slice];
    if (!judge) return snapshotOf(slice, null);
    const ask: Ask = async (route, payload) => {
      try {
        return await invoke('DAORIS.DRIVER', route, payload === undefined ? undefined : { payload });
      } catch {
        return undefined;
      }
    };
    return snapshotOf(slice, await judge(ask));
  };
}
