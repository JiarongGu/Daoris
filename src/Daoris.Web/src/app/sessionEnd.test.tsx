import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient } from '@tanstack/react-query';

// PAGELOOP1: on the install the page threw React's update-depth error (#185) seven seconds before a driven session ended, twice, a
// session that closed its quest done and landed by its rule, and with no boundary the window went blank. The whole window, as
// `main.tsx` mounts it, over a mocked shell that delivers each event to every handler the page holds, through what such an end
// changes: the turn ends, the quest is done, the work lands, the session ends and says so, and the next look reads it all again.
// None of these cases met the install's loop: they hold the window through such an end on Sessions and on Overview, its state
// pulled and pushed, with jsdom's empty boxes and with a box for every element, so a loop a later change brings to these paths
// fails here, and the error screen names the component the next time the install meets one (D217).
// The install's log puts the failure about ten seconds after the quest closed done and four seconds into the agent's last message,
// which streams in as some hundreds of one-line `message` events of one id: the end below streams such a message, with the quest
// done and the session still working, before the turn ends. It met no loop either.

const { invoke, notifyReady, shenora, handlers } = vi.hoisted(() => {
  const notifyReady = vi.fn(() => Promise.resolve());
  return {
    invoke: vi.fn(),
    notifyReady,
    // One bridge for the page's life, as the kit's is.
    shenora: { isAvailable: true, bridge: { notifyReady } },
    // Every handler each event has, as the kit's pump delivers to all of them.
    handlers: new Map<string, Set<(payload: unknown) => void>>(),
  };
});

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  const React = await import('react');
  return {
    ...actual,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
    useShenora: () => shenora,
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      const latest = React.useRef(handler);
      latest.current = handler;
      React.useEffect(() => {
        const key = `${module}.${type}`;
        const held = handlers.get(key) ?? new Set();
        handlers.set(key, held);
        const each = (payload: unknown) => latest.current(payload);
        held.add(each);
        return () => { held.delete(each); };
      }, [module, type]);
    },
    useWindowMaximized: () => false,
  };
});

import '../locales/i18n';
import { PageRoot } from './PageRoot';
import { AT_START } from '../help/setupGuide';

const SESSION = 'd71e7e00';
const STARTED = '2026-10-28T21:40:00Z';
const ENDING = '2026-10-28T21:50:54Z';

/** What the service answers now: the session and its quest as the end has reached them. */
const world = {
  state: 'working' as 'working' | 'completed',
  quest: 'Taken' as 'Taken' | 'Done',
  running: [SESSION] as string[],
  events: [] as Record<string, unknown>[],
};

const session = () => ({
  id: SESSION, quest: 'q1', repository: 'engine', adapter: 'claude-code', kind: 'driven', state: world.state,
  created: STARTED, updated: world.state === 'working' ? STARTED : ENDING, workspace: 'default',
  ...(world.state === 'completed' ? { note: 'Closed q1 done; landed by its rule.' } : {}),
});
const quest = () => ({
  id: 'q1', from: 'game', to: 'engine', title: 'Expose a streaming budget', body: 'A per-frame cap.', status: world.quest,
  filed: '2026-10-28T09:00:00Z', updated: world.quest === 'Done' ? ENDING : STARTED, workspace: 'default',
});

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) {
    return Response.json([{ repository: 'engine', workspace: 'default', registered: true, adopted: true, owns: [], accepts: [], packs: [] }]);
  }
  if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
  if (url.startsWith('/api/sessions')) return Response.json(url.includes('daoris%3Ahelp') ? [] : [session()]);
  if (url.startsWith('/api/quests')) return Response.json([quest()]);
  if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
  return Response.json([]);
}

const event = (seq: number, extra: Record<string, unknown>) => ({ seq, at: STARTED, ...extra });

/** Each pushed slice's partitions as the shell's owner holds them now, by slice, then partition (PUSH1a, D182). */
const partitionsOf = (): Record<string, Record<string, unknown>> => ({
  driver: {
    shell: { ready: true },
    choices: { drivable: ['engine'], holds: [], trees: [], notify: false, strikes: 3, forgiven: {} },
    running: { running: world.running },
  },
  sessions: { default: { [SESSION]: session() } },
  groups: {
    entries: {
      [`session:${SESSION}/engine`]: {
        entry: `session:${SESSION}/engine`, origin: `session:${SESSION}`, repository: 'engine', group: world.state === 'working' ? 'working' : 'ended',
        leading: SESSION,
        parts: [{
          session: SESSION, quest: 'q1', shown: world.state, group: world.state === 'working' ? 'working' : 'ended', began: 'opened', after: null,
          askedBy: null,
        }],
      },
    },
    rows: {
      [SESSION]: {
        session: SESSION, group: world.state === 'working' ? 'working' : 'ended', shown: world.state, archived: false, teammate: false,
        repository: 'engine', created: STARTED, updated: world.state === 'working' ? STARTED : ENDING,
      },
    },
  },
  attention: {
    rows: {},
    sessions: {},
    working: world.state === 'working' ? { [`session:${SESSION}`]: { workspace: 'default' } } : {},
  },
  quests: { default: { q1: { ...quest(), links: [], attachments: [] } } },
  work: {
    '<held>': { held: [] },
    '<no checkout>': { units: [], recovery: [], unread: [] },
    engine: {
      units: [{
        unit: `u-${SESSION}`, repository: 'engine', session: SESSION,
        end: world.quest === 'Done' ? { state: 'landed', said: 'landed by its rule' } : { state: 'unknown', said: '' },
        result: [], cleanup: { state: 'pending', waits: [], gone: [] }, recovery: [], entries: [],
      }],
      recovery: [], unread: [],
    },
  },
});

/** What each slice's partitions were pushed at, so a push is newer than what the window holds. */
const seqs = new Map<string, number>();

const snapshot = (slice: string) => ({
  slice, epoch: `${slice}-1`, status: 'ready',
  partitions: Object.entries(partitionsOf()[slice] ?? {}).map(([partition, data]) =>
    ({ partition, seq: seqs.get(`${slice}/${partition}`) ?? 1, status: 'ready', data })),
});

/** The shell pushing what one slice's partition holds now, whole. */
const push = async (slice: string, partition: string) => {
  const seq = (seqs.get(`${slice}/${partition}`) ?? 1) + 1;
  seqs.set(`${slice}/${partition}`, seq);
  await tell('STATE_PUSH', {
    slice, epoch: `${slice}-1`, partition, seq, baseSeq: null, status: 'ready', data: partitionsOf()[slice]![partition], removed: [],
  });
};

/** Whether the shell pushes its slices with their data, or the page pulls them (an older shell's, which answers no snapshot). */
let pushes = false;

/** The shell, telling `type` to every handler the page holds for it. */
const tell = (type: string, payload: unknown) => act(async () => {
  for (const each of [...(handlers.get(`DAORIS.${type}`) ?? [])]) each(payload);
  await new Promise((resolve) => setTimeout(resolve, 20));
});

/** What React said of an update loop, as an error or as its development warning. */
const loops = () => vi.mocked(console.error).mock.calls.map((call) => call.map(String).join(' '))
  .filter((said) => /Maximum update depth|getSnapshot should be cached/.test(said));

describe("the window through a driven session's end (PAGELOOP1)", () => {
  beforeEach(() => {
    world.state = 'working';
    world.quest = 'Taken';
    world.running = [SESSION];
    world.events = [
      event(1, { kind: 'user', origin: 'target', text: 'Your target is the quest q1.' }),
      event(2, { kind: 'message', id: 'm1', text: 'Working on the streaming budget.' }),
    ];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string, options?: { payload?: { slice?: string } }) => {
      if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
      if (module === 'DAORIS.DRIVER' && type === 'STATE_SLICE') {
        const slice = options?.payload?.slice ?? '';
        if (pushes && slice in partitionsOf()) return snapshot(slice);
        throw Object.assign(new Error('NO_ROUTE'), { code: 'NO_ROUTE' });
      }
      if (module === 'DAORIS.DRIVER' && type === 'STATE') {
        return { drivable: ['engine'], holds: [], trees: [], running: world.running, notify: false, strikes: 3, forgiven: {} };
      }
      if (module === 'DAORIS.DRIVER' && type === 'SESSION_HISTORY') {
        return { session: SESSION, earlier: false, latest: world.events.length, events: world.events };
      }
      if (module === 'DAORIS.DRIVER' && type === 'SESSION_QUEUE') return { queued: [], taking: world.state === 'working' };
      return {};
    });
    window.localStorage.setItem(AT_START, 'off');
    window.localStorage.setItem('daoris.view', 'sessions');
    window.localStorage.setItem('daoris.attending', SESSION);
    vi.spyOn(console, 'error');
    // The setup's observer never reports, so jsdom cannot show a loop through one: this one reports a size that changes each time
    // it is told to observe, as a region that moves under a session's end does.
    let size = 0;
    vi.stubGlobal('ResizeObserver', class {
      constructor(private readonly told: ResizeObserverCallback) {}
      observe(target: Element) {
        size += 1;
        const rect = { width: 300 + size, height: 40 + size, x: 0, y: 0, top: 0, left: 0, right: 300 + size, bottom: 40 + size };
        queueMicrotask(() => this.told([{ target, contentRect: rect } as unknown as ResizeObserverEntry], this as unknown as ResizeObserver));
      }
      unobserve() {}
      disconnect() {}
    });
  });
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    invoke.mockReset();
    handlers.clear();
    seqs.clear();
    pushes = false;
    window.localStorage.clear();
  });

  /** Where the person may be while it ends: what each place shows of the session before, and after, its end. */
  const PLACES: { place: string; open: () => Promise<void>; after: () => Promise<void> }[] = [
    {
      place: 'on Sessions, attending it',
      open: async () => { expect(await screen.findByText('Working on the streaming budget.')).toBeInTheDocument(); },
      after: async () => { expect(await screen.findByText('Done: the cap is in, the gates passed.')).toBeInTheDocument(); },
    },
    {
      place: 'on Overview, its side bar following it',
      open: async () => {
        await userEvent.click(within(screen.getByRole('navigation', { name: 'Views' })).getByRole('button', { name: 'Overview' }));
        const side = await screen.findByRole('complementary', { name: 'right side bar' });
        expect(await within(side).findByText(/^Attending/)).toBeInTheDocument();
      },
      // Off Sessions an ended session is followed no more, and the side bar opens on Ask Daoris (UX6b).
      after: async () => {
        const side = await screen.findByRole('complementary', { name: 'right side bar' });
        expect(await within(side).findByRole('tab', { name: 'Ask Daoris', selected: true })).toBeInTheDocument();
      },
    },
  ];

  /**
   * The end, in the order the install's sessions met it: the turn ends with the call that closes the quest done; the quest is done
   * and the work lands by its rule; seconds later the session ends, its record, its group, what runs and its attention row moved,
   * and says so; and the next look reads it all again. Where the shell pushes, each slice is pushed as its owner moves it.
   */
  async function throughTheEnd() {
    const closing = [
      event(3, { kind: 'tool', id: 't1', title: 'quest_done', status: 'completed', input: JSON.stringify({ quest: 'q1' }) }),
      event(4, { kind: 'message', id: 'm2', text: 'Done: the cap is in, the gates passed.' }),
    ];
    world.events = [...world.events, ...closing];
    await tell('SESSION_EVENTS', { session: SESSION, events: closing });
    world.quest = 'Done';
    if (pushes) {
      await push('quests', 'default');
      await push('work', 'engine');
    }
    // The agent's last message streams in chunk by chunk, each a `message` event of one id, with the quest done and the session
    // still working (the install's log: the page failed while they came).
    let seq = 10;
    for (const text of ['`', 'It is', ' done', ', the cap', ' is in', ' and the', ' gates', ' passed.']) {
      seq += 1;
      const chunk = [event(seq, { kind: 'message', id: 'm3', text }), event(seq + 100, { kind: 'usage', used: 1000 + seq, size: 1000000 })];
      world.events = [...world.events, ...chunk];
      await tell('SESSION_EVENTS', { session: SESSION, events: chunk });
    }
    await tell('DRIVER_TICK', { considered: [], untrusted: [], waits: [] });
    world.state = 'completed';
    world.running = [];
    if (pushes) {
      await push('sessions', 'default');
      await push('groups', 'rows');
      await push('groups', 'entries');
      await push('attention', 'working');
      await push('driver', 'running');
    }
    await tell('SESSION_ENDED', { session: SESSION, state: 'completed' });
    await tell('SESSION_ATTENTION', { kind: 'Ended', session: SESSION, headline: 'engine finished q1' });
    await tell('DRIVER_TICK', { considered: [], untrusted: [], waits: [] });
  }

  /**
   * A box for every element, where jsdom lays out none: as wide as a side bar, as tall as a pane, and its content as long as its
   * words, so what measures itself (a tab row's fit, the waiting panel's room, a clamped title, the flowchart's ink) takes the branch
   * a laid-out window takes. Not the window's geometry, only enough of one that a measure can disagree with itself.
   */
  function layOut() {
    const proto = HTMLElement.prototype;
    const words = (element: HTMLElement) => element.textContent?.length ?? 0;
    const define = (name: string, get: (element: HTMLElement) => number) =>
      vi.spyOn(proto, name as 'clientWidth', 'get').mockImplementation(function (this: HTMLElement) { return get(this); });
    define('clientWidth', () => 320);
    define('offsetWidth', () => 320);
    define('clientHeight', () => 200);
    define('offsetHeight', (element) => Math.max(18, Math.ceil(words(element) / 40) * 18));
    define('scrollWidth', (element) => Math.max(320, words(element) * 7));
    define('scrollHeight', (element) => Math.max(200, Math.ceil(words(element) / 40) * 18));
    vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(function (this: Element) {
      const height = Math.max(18, Math.ceil((this.textContent?.length ?? 0) / 40) * 18);
      return { x: 0, y: 0, top: 0, left: 0, width: 320, height, right: 320, bottom: height, toJSON: () => ({}) } as DOMRect;
    });
  }

  for (const laidOut of [false, true]) for (const pushed of [false, true]) {
    for (const { place, open, after } of PLACES) {
      const how = `its state ${pushed ? 'pushed' : 'pulled'}${laidOut ? ', every element laid out' : ''}`;
      it(`draws the window ${place} through the session's end with no update loop, ${how}`, async () => {
        pushes = pushed;
        if (laidOut) layOut();
        window.localStorage.setItem('daoris.dockClosed', '0');
        render(<PageRoot client={new QueryClient({ defaultOptions: { queries: { retry: false } } })} window={null} />);
        await screen.findByRole('navigation', { name: 'Views' });
        // The session's page drew it first, which attends it on every view.
        expect(await screen.findByText('Working on the streaming budget.')).toBeInTheDocument();
        await open();

        await throughTheEnd();

        await after();
        // Where pushed, the ended record its push wrote is what the window read.
        if (pushed) expect(invoke.mock.calls.filter(([, type]) => type === 'STATE_SLICE').length).toBeGreaterThanOrEqual(6);
        expect(loops()).toEqual([]);
        expect(screen.queryByRole('heading', { level: 1, name: "This window's page stopped on an error" })).toBeNull();
        expect(screen.getByRole('navigation', { name: 'Views' })).toBeInTheDocument();
      });
    }
  }
});
