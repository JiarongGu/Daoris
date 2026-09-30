import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The Work frame as a PAGE, in the shape `shell.test.tsx` already uses: the bridge mocked as
// present, because the frame does not exist without one (D55). Everything a person does to a
// session happens here now — starting it, talking to it, ending it, and watching its console —
// which is what "one home for the stream" (design §3) means as a test file.

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => {
  // Every hook listening to an event hears it, as in the shell: each hook's latest handler is kept,
  // told apart by its source, since a render hands over a new closure each time. One handler per
  // event name meant a second listener (the console's, CONSOLE3a) silenced the first (the tabs').
  const byName = new Map<string, Map<string, (payload: unknown) => void>>();
  return {
    invoke: vi.fn(),
    notifyReady: vi.fn(() => Promise.resolve()),
    eventHandlers: {
      set: (name: string, handler: (payload: unknown) => void) => {
        const all = byName.get(name) ?? new Map<string, (payload: unknown) => void>();
        all.set(handler.toString(), handler);
        byName.set(name, all);
      },
      get: (name: string) => (byName.has(name)
        ? (payload: unknown) => { for (const handler of byName.get(name)!.values()) handler(payload); }
        : undefined),
      has: (name: string) => byName.has(name),
      clear: () => byName.clear(),
    },
  };
});

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import i18n from '../i18n';
import { WorkFrame } from './WorkFrame';
import { DiffPane } from './DiffPane';

const DRIVER_STATE = { drivable: ['engine'], holds: [], trees: [], running: ['s1a2b3c4'] };

const ROSTER = {
  settingsPath: 'C:/somewhere/.daoris/harnesses.json',
  adapter: 'claude-code',
  harnesses: [
    {
      harness: 'claude-code', present: true, version: 'claude 9.9.9', problem: null,
      machineDefault: 'personal',
      profiles: [
        { name: 'personal', home: 'C:/somewhere/.daoris/harnesses/claude-code/personal', login: 'in' },
        { name: 'work', home: 'C:/somewhere/.daoris/harnesses/claude-code/work', login: 'out' },
      ],
    },
    {
      harness: 'codex', present: false, version: null,
      problem: '`codex` is not on this machine\'s PATH', machineDefault: null, profiles: [],
    },
  ],
};

/** A roster whose chat harness reads a structured wire — a door where a turn can be stopped (CONV4). */
const STRUCTURED_ROSTER = {
  ...ROSTER,
  harnesses: [{ harness: 'stub', present: true, version: 'stub 1.0.0', problem: null, machineDefault: null, profiles: [], structured: true }],
};

const REGISTRY = [{
  repository: 'engine', adopted: true, registered: true, summary: 'the engine',
  owns: [], accepts: [], packs: [], entries: 1, root: 'C:/somewhere/engine',
}];

const QUESTS = [{
  id: 'abc123', from: 'game', to: 'engine',
  title: 'Expose a streaming budget', body: 'World streaming needs a per-frame cap.',
  status: 'Open', filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
}];

const DRIVEN = {
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'working',
  kind: 'driven', created: '2026-09-02T00:00:00Z', updated: '2026-09-02T01:00:00Z',
};

const CHAT = {
  id: 'c0ffee11', quest: null, repository: 'engine', adapter: 'stub', state: 'working',
  kind: 'chat', created: '2026-09-02T00:00:00Z', updated: '2026-09-02T00:05:00Z',
};

const PARKED = {
  ...DRIVEN, id: 'p4rk3d00', state: 'awaiting-person',
  note: 'Two ways forward; I recommend the second.',
};

/** An intake that could not settle whose an ask is, parked asking the person (INT4b). */
const PARKED_INTAKE = {
  id: 'i9n8t7k6', quest: null, repository: 'ask #0fda18', ask: '0fda18', adapter: 'stub',
  state: 'awaiting-person', kind: 'chat', tree: 'C:/somewhere/data/intake/default',
  note: 'published nothing: it asks you rather than guess.',
  created: '2026-09-02T00:00:00Z', updated: '2026-09-02T00:05:00Z',
};

/** An intake still at work on its ask (INT4h) — one turn, with no message box. */
const RUNNING_INTAKE = { ...PARKED_INTAKE, id: 'r7n8t7k6', state: 'working', note: undefined };

let SESSIONS: unknown[] = [DRIVEN];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

/** A drag's data as a page sees it (DOCK1e): jsdom has no `DataTransfer`, and a region reads its types. */
function carried() {
  const data: Record<string, string> = {};
  return {
    setData: (type: string, value: string) => { data[type] = value; },
    getData: (type: string) => data[type] ?? '',
    get types() { return Object.keys(data); },
    effectAllowed: 'all',
    dropEffect: 'none',
  };
}

/** The frame with the application's selection held for it, as `App` holds it. */
function show(selected: string | null = null, notify: (text: string) => void = () => {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const onSelect = vi.fn();
  const view = render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkFrame selected={selected} onSelect={onSelect} notify={notify} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
  return { ...view, onSelect, client };
}

// 🔴 Every test starts from a viewer with nothing remembered. The frame keeps its layout per viewer,
// and a test that hid the console left it hidden for whichever console test ran next: four failed
// in a shuffled order (UX5, 2026-09-26), and passed in file order only by luck of the order.
afterEach(() => window.localStorage.clear());

describe('the Work frame', () => {
  beforeEach(() => {
    SESSIONS = [DRIVEN];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    // A draft outlives its test the way it outlives a reload (CONV4b).
    window.localStorage.removeItem('daoris.drafts');
  });

  /**
   * UX5 U16: a maximized window showed the content, and the chat box, at half its width. U10 had held the head, the conversation and the
   * composer to a 768px measure, and on a maximized window that is half of it. They follow the
   * centre's width, as content does everywhere (platform language §4: content is shown as it is).
   * jsdom lays nothing out, so this holds the absence of a cap and the window holds the look.
   */
  /**
   * One right region: on
   * Sessions, Ask Daoris is a tab of the right dock beside the timeline and the review, as VS Code's
   * chat is a view of its one right side bar — never a second column beside the dock.
   */
  it('holds Ask Daoris as a tab of the right dock, and opens the dock on it when asked', async () => {
    SESSIONS = [DRIVEN];
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const view = (askFocus: number) => (
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}} ask={<p>the ask panel</p>} askFocus={askFocus} />
        </Tooltip.Provider>
      </QueryClientProvider>
    );
    const { rerender } = render(view(0));

    // Closed, the dock's strip offers it beside the other two.
    expect(await screen.findByRole('button', { name: 'Open Ask Daoris' })).toBeInTheDocument();
    expect(screen.queryByText('the ask panel')).toBeNull();

    rerender(view(1));
    expect(await screen.findByRole('tab', { name: 'Ask Daoris', selected: true })).toBeInTheDocument();
    expect(screen.getByText('the ask panel')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('tab', { name: /timeline/i }));
    expect(screen.queryByText('the ask panel')).toBeNull();
  });

  /**
   * DOCK1b: a view moves
   * between the right side bar and the panel from its tab's menu, as VS Code's *Move to Panel* does,
   * is shown where it went, and stays there for this viewer.
   */
  it('moves Ask Daoris to the panel from its tab\'s menu, shows it there, and remembers it', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}} ask={<p>the ask panel</p>} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    // A right-click on its tab selects it and offers where it can go.
    await userEvent.pointer({ keys: '[MouseRight]', target: await screen.findByRole('tab', { name: 'Ask Daoris' }) });
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Move Ask Daoris to the panel' }));

    const panel = screen.getByRole('tablist', { name: 'views in the panel' });
    expect(within(panel).getByRole('tab', { name: 'Ask Daoris', selected: true })).toBeInTheDocument();
    expect(screen.getByText('the ask panel')).toBeInTheDocument();
    expect(within(screen.getByRole('tablist', { name: 'right side bar' })).queryByRole('tab', { name: 'Ask Daoris' })).toBeNull();
    expect(JSON.parse(window.localStorage.getItem('daoris.viewPlaces')!)).toEqual({ ask: 'panel' });

    // Back again from the panel's own list, and nothing is left to remember.
    screen.getByRole('button', { name: 'views in the panel' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Move Ask Daoris to the right side bar' }));
    expect(screen.queryByRole('tablist', { name: 'views in the panel' })).toBeNull();
    expect(screen.getByRole('tab', { name: 'Ask Daoris', selected: true })).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.viewPlaces')).toBeNull();
  });

  /**
   * DOCK1a: another view in the centre keeps the side bar and the panel,
   * with their views, and has no session rail — the list is Sessions' own.
   */
  it('frames another view: its content in the centre, the side bar and the panel beside it, no rail', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const onOpenSessions = vi.fn();
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame
            selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}}
            ask={<p>the ask panel</p>}
            content={<main><h1>Overview</h1></main>}
            onOpenSessions={onOpenSessions}
          />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    // Its session views say whose they are, since nothing else on this screen does, and lead back.
    const side = screen.getByRole('complementary', { name: 'right side bar' });
    expect(await within(side).findByText('Attending Expose a streaming budget')).toBeInTheDocument();
    await userEvent.click(within(side).getByRole('button', { name: 'open in Sessions' }));
    expect(onOpenSessions).toHaveBeenCalled();

    expect(await screen.findByRole('heading', { level: 1, name: 'Overview' })).toBeInTheDocument();
    expect(screen.getByRole('tablist', { name: 'right side bar' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'the panel' })).toBeInTheDocument();
    // No rail, and not the session's own centre either.
    expect(screen.queryByRole('separator', { name: 'rail width' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Hide the session list' })).toBeNull();
    expect(screen.queryByRole('heading', { level: 2, name: 'Expose a streaming budget' })).toBeNull();
    // The attended session's timeline is still a tab away, as VS Code's panel is whatever the editor shows.
    expect(screen.getByRole('tab', { name: 'Timeline' })).toBeInTheDocument();
  });

  /**
   * 🔴 USE1: every flexible box between a framed view and the frame's own root may shrink below its
   * content. A flex item is otherwise no narrower than what it holds, and the frame's root had no
   * `min-w-0`: a long title that should truncate widened every view but Sessions past the window on the
   * install. jsdom has no layout, so the rule is held on the boxes themselves.
   */
  it('lets every flexible box around a framed view shrink below its content', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { container } = render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame
            selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}}
            ask={<p>the ask panel</p>}
            content={<main><h1>Quests</h1></main>}
          />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    const heading = await screen.findByRole('heading', { level: 1, name: 'Quests' });
    const unshrinkable: string[] = [];
    for (let box = heading.closest('main')!.parentElement; box && box !== container; box = box.parentElement) {
      if (box.classList.contains('flex-1') && !box.classList.contains('min-w-0')) unshrinkable.push(box.className);
    }
    expect(unshrinkable).toEqual([]);
  });

  /** DOCK1e: dragging a tab to the other region is the same move as the menu's, by the pointer. */
  it('moves a view dragged by its tab to the panel', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}} ask={<p>the ask panel</p>} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    const drag = carried();
    fireEvent.dragStart(await screen.findByRole('tab', { name: 'Ask Daoris' }), { dataTransfer: drag });
    const panel = screen.getByRole('region', { name: 'the panel' });
    fireEvent.dragEnter(panel, { dataTransfer: drag });
    fireEvent.dragOver(panel, { dataTransfer: drag });
    fireEvent.drop(panel, { dataTransfer: drag });

    expect(within(screen.getByRole('tablist', { name: 'views in the panel' })).getByRole('tab', { name: 'Ask Daoris', selected: true }))
      .toBeInTheDocument();
    expect(screen.getByText('the ask panel')).toBeInTheDocument();
    expect(JSON.parse(window.localStorage.getItem('daoris.viewPlaces')!)).toEqual({ ask: 'panel' });
    // And the machine log hears where it went (LOG1b): which view, which region.
    expect(invoke).toHaveBeenCalledWith('DAORIS.LOG', 'EVENT', {
      payload: { event: 'panel.moved', data: { view: 'ask', region: 'panel' } },
    });
  });

  it('draws an emptied side bar while a view is dragged, so it can be dropped on', async () => {
    // Everything moved to the panel and the side bar closed: it is not drawn at all.
    window.localStorage.setItem('daoris.viewPlaces', JSON.stringify({ timeline: 'panel', review: 'panel' }));
    window.localStorage.setItem('daoris.dockClosed', '1');
    show('s1a2b3c4');
    const tab = await screen.findByRole('tab', { name: 'Console' });
    expect(screen.queryByRole('complementary', { name: 'right side bar' })).toBeNull();

    const drag = carried();
    fireEvent.dragStart(tab, { dataTransfer: drag });
    // The frame is told a beat after the drag starts (Chromium drops a drag the page changes under).
    await act(async () => { await new Promise((done) => setTimeout(done, 0)); });
    const side = screen.getByRole('complementary', { name: 'right side bar' });
    fireEvent.dragEnter(side, { dataTransfer: drag });
    fireEvent.drop(side, { dataTransfer: drag });

    expect(within(screen.getByRole('tablist', { name: 'right side bar' })).getByRole('tab', { name: 'Console', selected: true }))
      .toBeInTheDocument();
    expect(JSON.parse(window.localStorage.getItem('daoris.viewPlaces')!)).toEqual({ timeline: 'panel', review: 'panel', console: 'right' });
  });

  it('opens the region that holds Ask Daoris when asked for it, wherever it stands', async () => {
    window.localStorage.setItem('daoris.viewPlaces', JSON.stringify({ ask: 'panel' }));
    window.localStorage.setItem('daoris.panelClosed', '1');
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}} ask={<p>the ask panel</p>} askFocus={1} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    const panel = await screen.findByRole('tablist', { name: 'views in the panel' });
    expect(within(panel).getByRole('tab', { name: 'Ask Daoris', selected: true })).toBeInTheDocument();
    expect(screen.getByText('the ask panel')).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.panelClosed')).toBe('0');
  });

  it('closes a region a move leaves empty, and the reset puts every view back', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    show('s1a2b3c4');

    screen.getByRole('button', { name: 'views in the panel' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Move Console to the right side bar' }));

    // The console stands in the side bar now, and the panel it left held nothing else: it is gone.
    expect(within(screen.getByRole('tablist', { name: 'right side bar' })).getByRole('tab', { name: 'Console', selected: true }))
      .toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /the panel$/ })).toBeNull();

    screen.getByRole('button', { name: 'views in the right side bar' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Reset view locations' }));

    expect(screen.queryByRole('tab', { name: 'Console' })).toBeNull();
    // Back where it started, and still hidden: only the person opens it again.
    expect(screen.getByRole('button', { name: 'Show the panel' })).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.viewPlaces')).toBeNull();
  });

  /**
   * SESS1: who a session worked with, from what the records declare — a quest it published names it
   * (`publishedBy`), so its head says what it asked of another repository, and the quest opens where
   * quests are read.
   */
  it('says what the attended session asked of another repository, and opens that quest', async () => {
    const asked = {
      id: 'def456', from: 'engine', to: 'game', title: 'Read the budget from the level file', body: 'b',
      status: 'Done', note: 'Done: it reads level.budget now.', filed: '2026-09-02T00:30:00Z', updated: '2026-09-02T00:50:00Z',
      publishedBy: 's1a2b3c4',
    };
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => (String(input).startsWith('/api/quests')
      ? Response.json([...QUESTS, asked])
      : respond(String(input)))));
    const onOpenQuest = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}} onOpenQuest={onOpenQuest} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    const worked = await screen.findByRole('region', { name: 'Who it worked with' });
    expect(within(worked).getByText('Answered: Done: it reads level.budget now.')).toBeInTheDocument();
    await userEvent.click(within(worked).getByRole('button', { name: 'Read the budget from the level file' }));
    expect(onOpenQuest).toHaveBeenCalledWith('def456');
  });

  it('lets the head and the composer follow the centre\'s width, however wide the window', async () => {
    SESSIONS = [DRIVEN, CHAT];
    show('c0ffee11');

    // The conversation's own width is ConversationView's, held beside its other tests.
    const head = (await screen.findByRole('heading', { level: 2 })).closest('article')!;
    const composer = screen.getByRole('textbox', { name: 'message' }).closest('form')!;
    expect(head.className).not.toMatch(/max-w-/);
    expect(composer.className).not.toMatch(/max-w-/);
  });

  it('is the rail and the attended session, bound by one selection', async () => {
    show('s1a2b3c4');

    // The rail's row and the head's title are the same derived identity, from one implementation.
    await screen.findByRole('heading', { level: 2, name: 'Expose a streaming budget' });
    await vi.waitFor(() =>
      expect(screen.getAllByText('Expose a streaming budget')).toHaveLength(2));
  });

  /**
   * RAIL1: a conversation's name is what was first said in it — the rail's row and the head read the
   * same one, from one implementation — and a row's menu reviews that session's work in the dock.
   */
  it('names a conversation by its first line in the rail and the head, and reviews it from its row', async () => {
    SESSIONS = [DRIVEN, CHAT];
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_OPENINGS' ? { openings: { c0ffee11: 'Cap the hydration per frame' } } : DRIVER_STATE));
    const { onSelect } = show('c0ffee11');

    await screen.findByRole('heading', { level: 2, name: 'Cap the hydration per frame' });
    await vi.waitFor(() => expect(screen.getAllByText('Cap the hydration per frame')).toHaveLength(2));

    const user = userEvent.setup();
    screen.getByRole('button', { name: 'more for Cap the hydration per frame' }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Review its work' }));
    expect(onSelect).toHaveBeenCalledWith('c0ffee11');
    expect(screen.getByRole('tab', { name: 'Review' })).toHaveAttribute('aria-selected', 'true');
  });

  it('lands on a designed nothing, not on a session it chose for the person', async () => {
    show(null);
    expect(await screen.findByText('Nothing attended')).toBeInTheDocument();
  });

  /**
   * 🔴 Seen on the window (CONV3's look): a session just started is attended before the list has
   * fetched it, and the frame cleared the selection as a record that was gone, leaving "Nothing
   * attended" beside a running chat. Only a record the frame has seen can be gone.
   */
  it('keeps a just-started session attended until the list catches up with it', async () => {
    const { onSelect } = show('c0ffee11');

    await screen.findByText(/Expose a streaming budget/);
    expect(onSelect).not.toHaveBeenCalledWith(null);
  });

  it('lets go of a session whose record it saw and that is gone', async () => {
    const { onSelect, client } = show(DRIVEN.id);
    await screen.findAllByText(/Expose a streaming budget/);

    SESSIONS = [];
    await act(() => client.invalidateQueries());

    await waitFor(() => expect(onSelect).toHaveBeenCalledWith(null));
  });

  /**
   * The console (D49 §2): the transcript capture, streaming. It reaches the page over the shell's
   * bridge and has no HTTP route at all, because output is transcript-class material and never
   * leaves the machine that produced it (D47 §4). Its home is the output panel now (D55).
   */
  it('asks the driver for the attended session\'s console and renders it verbatim', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? {
          session: 's1a2b3c4',
          lines: [{ sequence: 1, text: '$ npm test' }, { sequence: 2, text: 'ok 1337 passing' }],
          sequence: 2, live: true, dropped: 0,
        }
      : DRIVER_STATE));

    show('s1a2b3c4');

    expect(await screen.findByText(/ok 1337 passing/)).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TAIL_SESSION', {
      payload: { id: 's1a2b3c4' },
    });
  });

  /** Live lines arrive as events and join the backlog as one stream, keyed by the driver's sequence. */
  it('joins live output to the backlog without repeating what was already shown', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? { session: 's1a2b3c4', lines: [{ sequence: 1, text: 'first' }], sequence: 1, live: true, dropped: 0 }
      : DRIVER_STATE));

    show('s1a2b3c4');
    await screen.findByText(/first/);

    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_OUTPUT')!({
        session: 's1a2b3c4',
        lines: [{ sequence: 1, text: 'first' }, { sequence: 2, text: 'second' }],
      });
    });

    expect((await screen.findByText(/second/)).textContent).toBe('first\nsecond');
  });

  /**
   * CONSOLE2c: what the session runs beside itself is a tab of its own, and appears as it starts. The
   * panel shows the session's console until the person picks a stream, then tails that stream's key.
   */
  it('grows a tab for each stream the session runs, and a picked tab shows that stream', async () => {
    let streams: object[] = [];
    invoke.mockImplementation(async (_module: string, type: string, request?: { payload?: { id?: string } }) => {
      if (type === 'SESSION_STREAMS') return { session: 's1a2b3c4', streams };
      if (type === 'TAIL_SESSION') {
        return request?.payload?.id === 's1a2b3c4/task/bs00'
          ? { session: 's1a2b3c4/task/bs00', lines: [{ sequence: 1, text: 'listening on 4200' }], sequence: 1, live: true, dropped: 0 }
          : { session: 's1a2b3c4', lines: [{ sequence: 1, text: '→ background: dev server' }], sequence: 1, live: true, dropped: 0 };
      }
      return DRIVER_STATE;
    });

    show('s1a2b3c4');
    await screen.findByText(/→ background: dev server/);
    expect(screen.queryByRole('tablist')).toBeNull();

    streams = [{ key: 's1a2b3c4/task/bs00', kind: 'task', name: 'dev server', live: true, state: null }];
    await act(async () => { eventHandlers.get('DAORIS.SESSION_STREAMS')!({ session: 's1a2b3c4' }); });

    const tab = await screen.findByRole('tab', { name: /dev server — background · running/ });
    await userEvent.click(tab);

    expect(await screen.findByText(/listening on 4200/)).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TAIL_SESSION', { payload: { id: 's1a2b3c4/task/bs00' } });
    expect(screen.getByRole('tab', { name: /dev server/ }).getAttribute('aria-selected')).toBe('true');
  });

  /**
   * CONSOLE3a: the task in view carries its stop after the tabs, as VS Code's panel carries *Kill
   * Terminal*, and it asks the driver to stop that task of that session. The session's own tab, a task
   * its harness cannot stop, and one that has ended carry none.
   */
  it('stops the background task in view, and offers no stop where its harness gave none', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_STREAMS') {
        return {
          session: 's1a2b3c4',
          streams: [
            { key: 's1a2b3c4/task/bs00', kind: 'task', name: 'dev server', live: true, state: null, canStop: true },
            { key: 's1a2b3c4/task/bs01', kind: 'task', name: 'a probe', live: true, state: null, canStop: false },
            { key: 's1a2b3c4/task/bs02', kind: 'task', name: 'old build', live: false, state: 'completed', canStop: false },
          ],
        };
      }
      if (type === 'STOP_TASK') return { stopped: true };
      if (type === 'TAIL_SESSION') return { session: 's1a2b3c4', lines: [], sequence: 0, live: true, dropped: 0 };
      return DRIVER_STATE;
    });

    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: /a probe/ }));
    expect(screen.queryByRole('button', { name: /^Stop / })).toBeNull();
    await userEvent.click(screen.getByRole('tab', { name: /old build/ }));
    expect(screen.queryByRole('button', { name: /^Stop / })).toBeNull();

    await userEvent.click(screen.getByRole('tab', { name: /dev server/ }));
    await userEvent.click(screen.getByRole('button', { name: 'Stop dev server' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_TASK', { payload: { id: 's1a2b3c4', key: 's1a2b3c4/task/bs00' } });
  });

  /**
   * A stream's end reaches the page as its session's streams changing, never as a line (CONSOLE2c):
   * the console tailing that stream asks again and stops saying it is live. Found stopping a task on
   * the window (CONSOLE3a): the tab said stopped, and the console above its output still said live.
   */
  it('stops calling a stream live once its session says it ended', async () => {
    let live = true;
    invoke.mockImplementation(async (_module: string, type: string, request?: { payload?: { id?: string } }) => {
      if (type === 'SESSION_STREAMS') {
        return {
          session: 's1a2b3c4',
          streams: [{ key: 's1a2b3c4/task/bs00', kind: 'task', name: 'dev server', live, state: live ? null : 'stopped' }],
        };
      }
      if (type === 'TAIL_SESSION') {
        return request?.payload?.id === 's1a2b3c4/task/bs00'
          ? { session: 's1a2b3c4/task/bs00', lines: [{ sequence: 1, text: 'listening on 4200' }], sequence: 1, live, dropped: 0 }
          : { session: 's1a2b3c4', lines: [], sequence: 0, live: true, dropped: 0 };
      }
      return DRIVER_STATE;
    });

    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: /dev server/ }));
    await screen.findByText(/listening on 4200/);
    expect(screen.getByText('live')).toBeInTheDocument();

    live = false;
    await act(async () => { eventHandlers.get('DAORIS.SESSION_STREAMS')!({ session: 's1a2b3c4' }); });

    await waitFor(() => expect(screen.queryByText('live')).toBeNull());
    expect(screen.getByText(/listening on 4200/)).toBeInTheDocument();
  });

  /**
   * A session watched to its end stops being called live: its console asks again when the driver says
   * it ended. Found looking at CONSOLE3c: a completed chat's console still said live beside its
   * `completed` badge.
   */
  it('stops calling a session live once the driver says it ended', async () => {
    let live = true;
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? { session: 's1a2b3c4', lines: [{ sequence: 1, text: 'DONE' }], sequence: 1, live, dropped: 0 }
      : DRIVER_STATE));

    show('s1a2b3c4');
    await screen.findByText(/DONE/);
    expect(screen.getByText('live')).toBeInTheDocument();

    live = false;
    await act(async () => { eventHandlers.get('DAORIS.SESSION_ENDED')!({ session: 's1a2b3c4', state: 'completed' }); });

    await waitFor(() => expect(screen.queryByText('live')).toBeNull());
  });

  /** A session's last words arrive after it closed, and their batch says so: they do not make it live. */
  it('takes a batch that says the console ended as ended', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? { session: 's1a2b3c4', lines: [{ sequence: 1, text: 'a line printed' }], sequence: 1, live: true, dropped: 0 }
      : DRIVER_STATE));

    show('s1a2b3c4');
    // A line only the console prints: `working` is also the session's state, drawn before the tail.
    await screen.findByText(/a line printed/);
    expect(await screen.findByText('live')).toBeInTheDocument();

    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_OUTPUT')!({
        session: 's1a2b3c4', lines: [{ sequence: 2, text: '— the conversation ended' }], live: false,
      });
    });

    await screen.findByText(/the conversation ended/);
    expect(screen.queryByText('live')).toBeNull();
  });

  /** Another session's output is not this panel's — the event carries whose it is. */
  it('ignores a batch for a different session', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? { session: 's1a2b3c4', lines: [{ sequence: 1, text: 'mine' }], sequence: 1, live: true, dropped: 0 }
      : DRIVER_STATE));

    show('s1a2b3c4');
    await screen.findByText(/mine/);

    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_OUTPUT')!({
        session: 'somebody-else', lines: [{ sequence: 1, text: 'theirs' }],
      });
    });

    expect(screen.queryByText(/theirs/)).toBeNull();
  });

  /**
   * The console is the part that may be missing — a shell older than this surface answers something
   * else entirely. It degrades to an empty panel; the RECORD is what the region is for.
   */
  it('leaves the record standing when the answer is not a console', async () => {
    invoke.mockImplementation(async () => DRIVER_STATE); // no `lines` anywhere in it

    show('s1a2b3c4');

    expect(await screen.findByRole('heading', { level: 2, name: 'Expose a streaming budget' }))
      .toBeInTheDocument();
  });

  /** The panel is a region a person owns: it grows, it shrinks, and only they close it (D55). */
  it('resizes the output panel from the keyboard and remembers the height', async () => {
    show('s1a2b3c4');

    const handle = await screen.findByRole('separator', { name: 'panel height' });
    const before = Number(handle.getAttribute('aria-valuenow'));
    // A resize that needs a mouse is a resize some people do not have.
    handle.focus();
    await userEvent.keyboard('{ArrowUp}');

    const grown = screen.getByRole('separator', { name: 'panel height' });
    expect(Number(grown.getAttribute('aria-valuenow'))).toBeGreaterThan(before);
    expect(window.localStorage.getItem('daoris.panelHeight')).toBe(String(before + 48));
  });

  it('hides the panel when the person hides it, and nothing else reopens it', async () => {
    show('s1a2b3c4');

    await userEvent.click(await screen.findByRole('button', { name: 'Hide the panel' }));
    expect(screen.queryByRole('separator', { name: 'panel height' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Show the panel' })).toBeInTheDocument();
  });
});

/**
 * Conversations (D49 §3): a session a person entered rather than the driver planned. Starting one
 * moved here from Projects (design §3) — where the result appears, and where a person can be told
 * what the harness said.
 */
describe('starting and holding a conversation', () => {
  beforeEach(() => {
    SESSIONS = [DRIVEN];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    // A draft outlives its test the way it outlives a reload (CONV4b).
    window.localStorage.removeItem('daoris.drafts');
  });

  /**
   * New is behind one control (D56) — the form used to hold 287×200 of the rail permanently, for
   * something a person does occasionally. Opening it is now a step, and every test that starts a
   * session takes it.
   */
  const openStart = async () => {
    await userEvent.click(await screen.findByRole('button', { name: 'Start a session' }));
    return screen.findByRole('dialog');
  };

  /**
   * 🔴 The palette's "start a session" arrives as an EVENT — `intent`, cleared by `onIntentTaken`
   * the way `App` clears it. Consumed by setting state during render, React re-ran the frame with
   * the same prop on every pass: the quest composer's opening draft failed exactly this way and
   * opened nothing on the window (FIX-LOG 2026-09-23). Held here as App holds it.
   */
  it('opens the start form when the palette asks, once', async () => {
    function Held() {
      const [intent, setIntent] = useState<'start' | 'review' | null>('start');
      return (
        <WorkFrame
          selected={null} onSelect={() => {}} notify={() => {}}
          intent={intent} onIntentTaken={() => setIntent(null)}
        />
      );
    }
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider><Held /></Tooltip.Provider>
      </QueryClientProvider>,
    );

    expect(await screen.findByRole('dialog')).toBeTruthy();
    expect(await screen.findByLabelText('repository')).toBeTruthy();
  });

  it('keeps the form behind one control, and the rail a list of sessions', async () => {
    show(null);
    await screen.findByRole('navigation', { name: 'sessions' });

    expect(screen.queryByLabelText('repository')).toBeNull();
    expect(screen.queryByRole('button', { name: 'start' })).toBeNull();

    await openStart();
    expect(screen.getByLabelText('repository')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'start' })).toBeTruthy();
  });

  it('closes the form on the session it opened — and keeps it up on a refusal', async () => {
    let refuse = true;
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') {
        return refuse ? { sessionId: null, message: 'busy' } : { sessionId: 'c0ffee11', message: 'ok' };
      }
      if (type === 'HARNESSES') return ROSTER;
      return DRIVER_STATE;
    });

    show(null);
    await openStart();
    await userEvent.click(screen.getByRole('button', { name: 'start' }));
    // A refusal leaves the form open: the choices are still on screen to correct.
    expect(screen.getByRole('dialog')).toBeTruthy();

    refuse = false;
    await userEvent.click(screen.getByRole('button', { name: 'start' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  });

  it('starts one in a repository with a checkout here, and attends what comes back', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') return { sessionId: 'c0ffee11', message: 'Chat `c0ffee11` opened in `engine`.' };
      if (type === 'HARNESSES') return ROSTER;
      return DRIVER_STATE;
    });

    const { onSelect } = show(null);
    await openStart();
    await userEvent.click(await screen.findByRole('button', { name: 'start' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'START_CHAT', {
      payload: { repository: 'engine' },
    });
    expect(onSelect).toHaveBeenCalledWith('c0ffee11');
  });

  /**
   * Every choice may be left alone, and silence means the driver's own resolution rather than a
   * default this surface invented (D49 §4, D51): the omitted field IS the answer.
   */
  it('carries the account and the own-tree choice, and omits what was not chosen', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') return { sessionId: null, message: 'busy' };
      if (type === 'HARNESSES') return ROSTER;
      return DRIVER_STATE;
    });

    show(null);
    await openStart();
    // The platform's own select (UX5 U5), opened from the keyboard as StartSession's tests explain.
    (await screen.findByRole('combobox', { name: 'account' })).focus();
    await userEvent.keyboard('{Enter}');
    // Signed out, so labelled so (the spawn's refusal then names the login action).
    await userEvent.click(await screen.findByRole('option', { name: /^work/ }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'in a working tree of its own' }));
    await userEvent.click(screen.getByRole('button', { name: 'start' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'START_CHAT', {
      payload: { repository: 'engine', profile: 'work', ownTree: true },
    });
  });

  /**
   * A refusal is the ledger's own sentence, verbatim — and nothing is attended on one: a
   * conversation that was refused has no session to show. The fixture holds a live DRIVEN session
   * in `engine`, which is the case worth pinning.
   */
  it('says a refusal in the form it answers, whole, and attends nothing', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'START_CHAT'
      ? { sessionId: null, message: '`engine` already has an active session — `s1a2b3c4` (working, quest `#abc123`).' }
      : DRIVER_STATE));
    const notify = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const onSelect = vi.fn();

    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected={null} onSelect={onSelect} notify={notify} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await openStart();
    await userEvent.click(await screen.findByRole('button', { name: 'start' }));

    // UX5 U68: in the drawer, where start was pressed, not a corner toast cut mid-sentence.
    expect(await within(screen.getByRole('dialog')).findByRole('alert'))
      .toHaveTextContent('engine already has an active session — s1a2b3c4 (working, quest #abc123).');
    expect(notify).not.toHaveBeenCalledWith(expect.stringContaining('already has an active session'), 'error');
    expect(onSelect).not.toHaveBeenCalled();
  });

  it('sends one message over the bridge and clears the box', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') return { sent: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    const box = await screen.findByLabelText('message');
    await userEvent.type(box, 'what is this repository for?');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 'c0ffee11', text: 'what is this repository for?' },
    });
    expect((box as HTMLTextAreaElement).value).toBe('');
  });

  /**
   * LOG1b (D94): a message sent is counted in the machine log, its length and how many files it
   * carried, and its words go only where they always went.
   */
  it('counts a message it sends in the machine log, and never its words', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') return { sent: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    await userEvent.type(await screen.findByLabelText('message'), 'what is this repository for?');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));

    const logged = invoke.mock.calls.filter(([module]) => module === 'DAORIS.LOG');
    expect(logged).toEqual([['DAORIS.LOG', 'EVENT', {
      payload: { event: 'message.sent', data: { session: 'c0ffee11', kind: 'chat', length: 28, files: 0 } },
    }]]);
    expect(JSON.stringify(logged)).not.toContain('repository for');
  });

  /**
   * REV3: the composer lets go of the words when it sends. A send that did not arrive — the session
   * ended while they typed, or the driver refused — handed back nothing, so a paragraph and its files
   * were gone. They come back into the box now, and the files are named.
   */
  it('hands the words back into the box, and names the files, when a send does not arrive', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') return { sent: false };
      return DRIVER_STATE;
    });
    const notify = vi.fn();

    show('c0ffee11', notify);
    await userEvent.upload(await screen.findByLabelText('choose files…'), new File(['exit 3'], 'run.log', { type: 'text/plain' }));
    const box = screen.getByLabelText('message');
    await userEvent.type(box, 'a long paragraph worth keeping');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));

    expect(await screen.findByText(/went nowhere/)).toBeTruthy();
    await waitFor(() => expect((box as HTMLTextAreaElement).value).toBe('a long paragraph worth keeping'));
    expect(notify).toHaveBeenCalledWith(expect.stringContaining('run.log'), 'error');
  });

  /**
   * Finishing and stopping are different verbs and mean different things: end-of-input lets the
   * harness wind up, a stop cuts it off and the record says the person did.
   */
  /**
   * 🔴 What a stop says is what the driver answered (2026-09-25). A stop that found nothing running
   * here ended an orphan's record, and the notice still said the person had ended it; a stop on a
   * session that had already finished said it was "being stopped".
   */
  it.each([
    [{ stopped: true }, 'session c0ffee11 is being stopped — the record will say the person ended it.'],
    [{ stopped: true, orphan: true }, 'session c0ffee11 had nothing running it on this machine — its record now says so.'],
    [{ stopped: false }, 'session c0ffee11 was not running here — its record says how it ended.'],
    // REV3 chat F8: another Daoris process on this machine runs it — a terminal's — and the record
    // still says working, so "its record says how it ended" was untrue.
    [{ stopped: false, elsewhere: true }, "session c0ffee11 is run by another Daoris process on this machine, a terminal's — stop it there."],
  ])('says what the stop did when the driver answers %j', async (answer, sentence) => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'STOP_SESSION' ? answer : DRIVER_STATE));
    const notify = vi.fn();

    show('c0ffee11', notify);
    await userEvent.click(await screen.findByRole('button', { name: 'stop' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(sentence));
  });

  it('offers finishing and stopping separately', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'END_CHAT') return { ended: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    await userEvent.click(await screen.findByRole('button', { name: 'finish' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'END_CHAT', { payload: { id: 'c0ffee11' } });
    expect(screen.getByRole('button', { name: 'stop' })).toBeTruthy();
  });

  /**
   * CONV4b: stopping the turn over the driver. What was waiting comes back into the box — the person
   * wrote it, and a stop that threw it away would lose it — and the notice claims only the asking:
   * the record says whether the turn ended stopped.
   */
  it('stops the turn over the driver, and hands what was waiting back to the box', async () => {
    SESSIONS = [CHAT];
    const notify = vi.fn();
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [{ text: 'and then test it', files: [] }], taking: true };
      if (type === 'CANCEL_TURN') return { cancelled: true, withdrawn: [{ text: 'and then test it', files: [] }] };
      return DRIVER_STATE;
    });

    show('c0ffee11', notify);
    const waiting = await screen.findByRole('list', { name: 'waiting for this turn to end' });
    expect(waiting.textContent).toBe('and then test it');
    await userEvent.type(screen.getByLabelText('message'), 'and push nothing');
    await userEvent.click(await screen.findByRole('button', { name: 'stop turn' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', { payload: { id: 'c0ffee11' } });
    await waitFor(() => expect(screen.getByLabelText('message')).toHaveValue('and then test it\n\nand push nothing'));
    expect(notify).toHaveBeenCalledWith(
      'Asked the agent to stop this turn; the conversation stays open. 1 waiting message came back to the box, unsent.');
  });

  // AGT6b (D98): one conversation's model and effort, as its agent offered them on the protocol door —
  // asked of the driver once, followed as news, and changed over it. The console's `/model`, typed as a
  // message, cut a turn short; this is its door.
  const MODEL = {
    id: 'model', name: 'Model', category: 'model', current: 'default',
    choices: [{ value: 'default', name: 'Default (recommended)' }, { value: 'sonnet', name: 'Sonnet' }],
  };
  const EFFORT = {
    id: 'effort', name: 'Effort', category: 'thought_level', current: 'high',
    choices: [{ value: 'low', name: 'Low' }, { value: 'high', name: 'High' }],
  };

  it("offers a conversation's model and effort as its agent offered them, and sets one over the driver", async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: false };
      if (type === 'SESSION_OPTIONS') return { session: 'c0ffee11', options: [MODEL, EFFORT] };
      if (type === 'SET_SESSION_OPTION') return { session: 'c0ffee11', options: [{ ...MODEL, current: 'sonnet' }, EFFORT] };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    const model = await screen.findByRole('combobox', { name: "this conversation's Model" });
    expect(model).toHaveTextContent('Default (recommended)');
    expect(screen.getByRole('combobox', { name: "this conversation's Effort" })).toHaveTextContent('High');

    model.focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'Sonnet' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_SESSION_OPTION', {
      payload: { id: 'c0ffee11', option: 'model', value: 'sonnet' },
    });
    await waitFor(() => expect(screen.getByRole('combobox', { name: "this conversation's Model" })).toHaveTextContent('Sonnet'));
  });

  it("follows the agent's own change to its options as news", async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: false };
      if (type === 'SESSION_OPTIONS') return { session: 'c0ffee11', options: [MODEL] };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    await screen.findByRole('combobox', { name: "this conversation's Model" });
    await waitFor(() => expect(eventHandlers.has('DAORIS.SESSION_OPTIONS_CHANGED')).toBe(true));
    act(() => eventHandlers.get('DAORIS.SESSION_OPTIONS_CHANGED')!({
      session: 'c0ffee11',
      options: [{ ...MODEL, current: 'haiku', choices: [...MODEL.choices, { value: 'haiku', name: 'Haiku' }] }],
    }));

    await waitFor(() => expect(screen.getByRole('combobox', { name: "this conversation's Model" })).toHaveTextContent('Haiku'));
  });

  it('offers nothing where the agent offered nothing, and asks nothing of a driven session', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: false };
      if (type === 'SESSION_OPTIONS') return { session: 'c0ffee11', options: [] };
      return DRIVER_STATE;
    });

    const { unmount } = show('c0ffee11');
    await screen.findByLabelText('message');
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_OPTIONS', { payload: { id: 'c0ffee11' } }));
    expect(screen.queryByRole('combobox', { name: "this conversation's Model" })).not.toBeInTheDocument();
    unmount();

    invoke.mockClear();
    SESSIONS = [DRIVEN];
    show('s1a2b3c4');
    await screen.findAllByText(/Expose a streaming budget/);
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_OPTIONS', expect.anything());
  });

  /** Seen on the window (CONV4b): two sentences joined by a space read wrong after a Chinese full stop. */
  it('joins the stop\'s two sentences the way the language does', async () => {
    SESSIONS = [CHAT];
    const notify = vi.fn();
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [{ text: '然后测试', files: [] }], taking: true };
      if (type === 'CANCEL_TURN') return { cancelled: true, withdrawn: [{ text: '然后测试', files: [] }] };
      return DRIVER_STATE;
    });
    await i18n.changeLanguage('zh');
    try {
      show('c0ffee11', notify);
      await userEvent.click(await screen.findByRole('button', { name: '停止本轮' }));
      await waitFor(() => expect(notify).toHaveBeenCalledWith(
        '已请智能体停止这一轮；对话仍然开着。1 条等待中的消息已退回输入框，未发送。'));
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /** CONV4c: a message's files go over the bridge as names and their bytes, the way a quest's uploads do. */
  it('sends a message\'s files over the bridge as names and bytes', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') return { sent: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    await userEvent.upload(await screen.findByLabelText('choose files…'), new File(['exit 3'], 'run.log', { type: 'text/plain' }));
    await userEvent.type(screen.getByLabelText('message'), 'what does this say?');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 'c0ffee11', text: 'what does this say?', files: [{ name: 'run.log', content: btoa('exit 3') }] },
    }));
  });

  /**
   * CONV4d: the tree's files are asked for only once a mention is being written, so a conversation
   * nobody mentions a file in costs no `git` listing at all — and then they are offered under the `@`.
   */
  it('asks the driver for the tree\'s files only once an @ is written, and offers them', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_FILES') return { session: 'c0ffee11', files: ['README.md', 'src/engine.cs'], unlisted: 0 };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    await userEvent.type(await screen.findByLabelText('message'), 'look at ');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_FILES', expect.anything());

    await userEvent.type(screen.getByLabelText('message'), '@eng');
    await userEvent.click(await screen.findByRole('option', { name: 'src/engine.cs' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_FILES', { payload: { id: 'c0ffee11' } });
    expect(screen.getByLabelText('message')).toHaveValue('look at @src/engine.cs ');
  });

  /**
   * CONV5: the context ring under the composer reads the conversation's own record — the one the
   * conversation above it is drawn from, never a second fetch — and follows it live.
   */
  it('shows how full the conversation\'s context is, from its record, and follows it live', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_HISTORY') {
        return {
          session: 'c0ffee11', earlier: false, latest: 2,
          events: [
            { seq: 1, at: '2026-09-26T10:00:00Z', kind: 'user', origin: 'person', text: 'read it' },
            { seq: 2, at: '2026-09-26T10:00:02Z', kind: 'usage', used: 34_120, size: 1_000_000 },
          ],
        };
      }
      return DRIVER_STATE;
    });

    show('c0ffee11');
    expect(await screen.findByRole('meter', { name: 'context' })).toHaveTextContent('3%');

    await waitFor(() => expect(eventHandlers.has('DAORIS.SESSION_EVENTS')).toBe(true));
    act(() => eventHandlers.get('DAORIS.SESSION_EVENTS')!({
      session: 'c0ffee11', events: [{ seq: 3, at: '2026-09-26T10:00:03Z', kind: 'usage', used: 850_000, size: 1_000_000 }],
    }));
    expect(await screen.findByText('85%')).toBeInTheDocument();
  });

  /**
   * The agent's words after its turn ended (its background work finished) carry no *working…* while
   * the driver says no turn is in flight, which looking at CONSOLE2 found under them for good.
   */
  it('says nothing is working under words the agent said after its turn, when no turn is in flight', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: false };
      if (type === 'SESSION_HISTORY') {
        return {
          session: 'c0ffee11', earlier: false, latest: 4,
          events: [
            { seq: 1, at: '2026-09-28T10:00:00Z', kind: 'user', origin: 'person', text: 'start the ticker' },
            { seq: 2, at: '2026-09-28T10:00:02Z', kind: 'message', id: 'msg_1', text: 'DONE' },
            { seq: 3, at: '2026-09-28T10:00:03Z', kind: 'turn', stopReason: 'end_turn' },
            { seq: 4, at: '2026-09-28T10:00:30Z', kind: 'message', id: 'msg_2', text: 'The background ticker finished.' },
          ],
        };
      }
      return DRIVER_STATE;
    });

    show('c0ffee11');
    expect(await screen.findByText('The background ticker finished.')).toBeInTheDocument();
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE', expect.anything()));
    await waitFor(() => expect(screen.queryByText('working…')).toBeNull());
  });

  /** A structured door that has not reported yet says so, never 0%. */
  it('says a conversation has not reported its context yet, rather than showing it empty', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'HARNESSES' ? STRUCTURED_ROSTER : DRIVER_STATE));

    show('c0ffee11');
    expect(await screen.findByRole('img', { name: 'context: not measured' })).toBeInTheDocument();
    expect(screen.queryByText('0%')).not.toBeInTheDocument();
  });

  /** Unlisted is information: the host's sentence, where the files would be, and the typed path still goes. */
  it('says why the tree cannot be listed, in the host\'s words, where the files would be', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type !== 'SESSION_FILES') return DRIVER_STATE;
      throw Object.assign(new Error('fallback'), {
        code: 'SESSION_TREE_UNLISTED', parameters: { session: 'c0ffee11' },
      });
    });

    show('c0ffee11');
    await userEvent.type(await screen.findByLabelText('message'), '@eng');

    expect(await screen.findByText(/A path you type after @ still reaches the agent/)).toBeInTheDocument();
  });

  /**
   * CONV4c: a file cannot come back into the box — the page no longer holds its bytes — so a stop that
   * hands back a message with files says which were not sent, for the person to attach again.
   */
  it('names the files a stop kept from being sent', async () => {
    SESSIONS = [CHAT];
    const notify = vi.fn();
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [{ text: 'then this', files: ['plan.md'] }], taking: true };
      if (type === 'CANCEL_TURN') return { cancelled: false, withdrawn: [{ text: 'then this', files: ['plan.md', 'shot.png'] }] };
      return DRIVER_STATE;
    });

    show('c0ffee11', notify);
    await userEvent.click(await screen.findByRole('button', { name: 'stop turn' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      '1 waiting message came back to the box, unsent. Not sent with it: plan.md, shot.png — attach them again.'));
    expect(screen.getByLabelText('message')).toHaveValue('then this');
  });

  /** CONV4b: the stop is there while the driver says a turn is in flight, and the send button says *queue*. */
  it('follows the driver live: a stop while a turn runs, and none between turns', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: false };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    expect(await screen.findByRole('button', { name: 'send' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'stop turn' })).not.toBeInTheDocument();

    await waitFor(() => expect(eventHandlers.has('DAORIS.SESSION_QUEUED')).toBe(true));
    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 'c0ffee11', queued: [], taking: true }));
    expect(await screen.findByRole('button', { name: 'stop turn' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'queue' })).toBeInTheDocument();

    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 'c0ffee11', queued: [], taking: false }));
    await waitFor(() => expect(screen.queryByRole('button', { name: 'stop turn' })).not.toBeInTheDocument());
  });

  /**
   * UX5 U17, decided by the reference console: a live chat between turns is idle, in the rail and
   * the head, and working again the moment the driver says a turn is in flight. It read *working*
   * the whole time, as a turn does.
   */
  it('says a live chat is idle between turns, in the rail and the head, as the driver says', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: false };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    const rail = await screen.findByRole('navigation', { name: 'sessions' });
    await waitFor(() => expect(within(rail).getByText('idle')).toBeInTheDocument());
    const head = screen.getByRole('heading', { level: 2 }).parentElement!;
    expect(within(head).getByText('idle')).toBeInTheDocument();

    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 'c0ffee11', queued: [], taking: true }));
    await waitFor(() => expect(within(rail).getByText('working')).toBeInTheDocument());
    expect(within(head).getByText('working')).toBeInTheDocument();
    // The composer follows the same answer: a turn in flight offers its stop.
    expect(screen.getByRole('button', { name: 'stop turn' })).toBeInTheDocument();
  });

  /** CONV4b: each conversation keeps its own draft as the person moves between them. */
  it('keeps each conversation its own draft', async () => {
    const OTHER = { ...CHAT, id: 'decaf222', repository: 'game' };
    SESSIONS = [CHAT, OTHER];
    const view = show('c0ffee11');
    await userEvent.type(await screen.findByLabelText('message'), 'half a thought for the engine');

    view.rerender(
      <QueryClientProvider client={view.client}>
        <Tooltip.Provider>
          <WorkFrame selected="decaf222" onSelect={view.onSelect} notify={() => {}} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await waitFor(() => expect(screen.getByLabelText('message')).toHaveValue(''));

    view.rerender(
      <QueryClientProvider client={view.client}>
        <Tooltip.Provider>
          <WorkFrame selected="c0ffee11" onSelect={view.onSelect} notify={() => {}} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await waitFor(() => expect(screen.getByLabelText('message')).toHaveValue('half a thought for the engine'));
  });

  /**
   * A driven session holds a tree but was given its whole target at once — there is no channel to
   * speak into, so it gets no composer. An input box nothing is listening to is worse than none.
   */
  it('offers no composer for a driven session', async () => {
    show('s1a2b3c4');

    await screen.findByRole('heading', { level: 2, name: 'Expose a streaming budget' });
    expect(screen.queryByLabelText('message')).toBeNull();
  });

  /**
   * SESS3: a driven
   * session the driver says listens — the protocol door — has a box; what is sent waits for its turn to
   * end, and *send now* stops the turn so it goes at once. One the driver says does not listen keeps none.
   */
  it('lets the person tell a working driven session something, held for its turn\'s end or sent now', async () => {
    let queue: object = { session: 's1a2b3c4', queued: [], taking: true, listening: true };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUEUE') return queue;
      if (type === 'SESSION_INPUT') return { sent: true };
      if (type === 'CANCEL_TURN') return { cancelled: true, withdrawn: [] };
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const box = await screen.findByLabelText('message');
    expect(box).toHaveAttribute('placeholder', expect.stringMatching(/tell it something while it works/));
    await userEvent.type(box, 'the budget is in level.json');
    await userEvent.click(screen.getByRole('button', { name: 'queue' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', { payload: { id: 's1a2b3c4', text: 'the budget is in level.json' } });

    // What the driver holds shows as waiting, and only then can it be sent now.
    expect(screen.queryByRole('button', { name: 'send now' })).toBeNull();
    queue = { session: 's1a2b3c4', queued: [{ text: 'the budget is in level.json', files: [] }], taking: true, listening: true };
    await act(async () => { eventHandlers.get('DAORIS.SESSION_QUEUED')!(queue); });
    expect(screen.getByText('the budget is in level.json')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'send now' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', { payload: { id: 's1a2b3c4' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Stopping its turn: what you added goes next.'));

    // It stops listening as it ends: the box goes.
    await act(async () => { eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 's1a2b3c4', queued: [], taking: false, listening: false }); });
    expect(screen.queryByLabelText('message')).toBeNull();
  });

  it('offers no box to a driven session the driver says nothing could hear', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_QUEUE'
      ? { session: 's1a2b3c4', queued: [], taking: false, listening: false }
      : DRIVER_STATE));
    show('s1a2b3c4');

    await screen.findByRole('heading', { level: 2, name: 'Expose a streaming budget' });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE', { payload: { id: 's1a2b3c4' } }));
    expect(screen.queryByLabelText('message')).toBeNull();
  });
});

/**
 * `awaiting-person` has meant "only the person can clear this" since D46 and had no surface at all
 * (design §4). The three moves land on the DRIVER, not on the service, because the process and the
 * record must move together — a record saying `completed` beside a process this machine still
 * holds is the lie the observed lifecycle exists to prevent.
 */
describe('clearing a parked session', () => {
  beforeEach(() => {
    SESSIONS = [PARKED];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    // A draft outlives its test the way it outlives a reload (CONV4b).
    window.localStorage.removeItem('daoris.drafts');
  });

  /**
   * 🔴 On the first real parked session there was nowhere to type the answer: the door was a button in
   * the card at the top of a record of 1,800 events, and the question is read at its foot. A parked driven session is answered from the box at the foot, where a
   * chat is, and the card says so — one owner for the answer (D56).
   */
  it('answers a parked driven session from the box at the foot, and the card says the box carries it on', async () => {
    show('p4rk3d00');

    const box = await screen.findByLabelText('message');
    expect(screen.queryByRole('button', { name: 'answer and carry on…' })).toBeNull();
    expect(screen.getByText(/Answering it in the box below lets it carry on/)).toBeInTheDocument();
    // An answer is words: nothing to attach, and no ending of the box's own (the card holds those).
    expect(screen.queryByRole('button', { name: 'attach files' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'finish' })).toBeNull();

    await userEvent.type(box, 'go ahead with the PUT');
    await userEvent.click(screen.getByRole('button', { name: 'carry on with this answer' }));

    await waitFor(() => expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      '/api/sessions/p4rk3d00/answer', expect.objectContaining({ method: 'POST' })));
    const [, init] = vi.mocked(fetch).mock.calls.find(([url]) => String(url).endsWith('/answer'))!;
    expect(JSON.parse(String(init!.body))).toEqual({ answer: 'go ahead with the PUT' });
  });

  it('shows the analysis and the three moves on the attended session', async () => {
    show('p4rk3d00');

    expect(await screen.findByText(/I recommend the second/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'finish it' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'decline…' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'stop it' })).toBeInTheDocument();
  });

  /**
   * **One owner for the moves at a time** (D56). A parked conversation used to render the band's
   * *finish it · decline… · stop it* and the composer's *send · finish · stop* simultaneously, 400px
   * apart — two owners for one set of verbs, which is worse than either. The band owns them while a
   * session is parked, and the composer keeps `send` alone so answering stays possible.
   */
  it('gives the verbs to the band while parked, and back to the composer when live', async () => {
    SESSIONS = [{ ...PARKED, kind: 'chat' }];
    show('p4rk3d00');

    await screen.findByRole('button', { name: 'finish it' });
    expect(screen.getByRole('button', { name: 'send' })).toBeInTheDocument();
    // The band's verbs, and only the band's.
    expect(screen.queryByRole('button', { name: 'finish' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'stop' })).toBeNull();

    // Working again: no band, and the composer owns both endings.
    SESSIONS = [{ ...PARKED, kind: 'chat', state: 'working' }];
    cleanup();
    show('p4rk3d00');

    await screen.findByRole('button', { name: 'finish' });
    expect(screen.getByRole('button', { name: 'stop' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'finish it' })).toBeNull();
  });

  it('finishes it over the driver, with no note the person did not write', async () => {
    show('p4rk3d00');
    await userEvent.click(await screen.findByRole('button', { name: 'finish it' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', {
      payload: { id: 'p4rk3d00', state: 'completed' },
    });
  });

  it('carries the reason a decline was given', async () => {
    show('p4rk3d00');
    await userEvent.click(await screen.findByRole('button', { name: 'decline…' }));
    await userEvent.type(screen.getByLabelText(/the reason/), 'the chunk API is being replaced');
    await userEvent.click(screen.getByRole('button', { name: 'decline with this reason' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', {
      payload: { id: 'p4rk3d00', state: 'declined', note: 'the chunk API is being replaced' },
    });
  });

  it('shows the host\'s refusal word for word', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type !== 'RESOLVE_SESSION') return DRIVER_STATE;
      throw Object.assign(new Error('fallback'), {
        code: 'SESSION_DECLINE_NEEDS_REASON', parameters: {},
      });
    });
    const notify = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="p4rk3d00" onSelect={vi.fn()} notify={notify} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await userEvent.click(await screen.findByRole('button', { name: 'finish it' }));

    await vi.waitFor(() => expect(notify)
      .toHaveBeenCalledWith(expect.stringContaining('Declining needs a reason'), 'error'));
  });

  /**
   * A teammate's record came down with the sync keyed `origin/id` (SYNC4) and is READ-ONLY here: the
   * process is on their machine, so nothing this window sends could reach it. It shows what it is
   * waiting on — and offers no moves and no composer, because a button that cannot work is a lie.
   */
  it('offers no moves and no composer on a teammate\'s session', async () => {
    SESSIONS = [{ ...PARKED, id: 'person@machine-b/p4rk3d00', kind: 'chat' }];
    show('person@machine-b/p4rk3d00');

    expect((await screen.findAllByText(/I recommend the second/)).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'finish it' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'stop it' })).toBeNull();
    expect(screen.queryByLabelText('message')).toBeNull();
    expect(screen.queryByRole('button', { name: 'send' })).toBeNull();
  });

  /**
   * INT4g: a parked intake's answer is on its ask, which the application opens in Quests — so the
   * frame hands the door up. No composer: an intake is one turn (INT4b), and a parked one has no
   * process left to hear a message.
   */
  it('leads a parked intake to its ask, with no composer and none of the three moves', async () => {
    SESSIONS = [PARKED_INTAKE];
    const onAnswerAsk = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="i9n8t7k6" onSelect={vi.fn()} notify={() => {}} onAnswerAsk={onAnswerAsk} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    await userEvent.click(await screen.findByRole('button', { name: 'answer ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledWith('0fda18');
    expect(screen.queryByRole('button', { name: 'finish it' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'send' })).toBeNull();
    expect(screen.queryByLabelText('message')).toBeNull();
  });

  /** Stopping stays: the person may end the intake and settle the ask later, as a proposal. */
  it('stops a parked intake over the driver, and nothing more', async () => {
    SESSIONS = [PARKED_INTAKE];
    show('i9n8t7k6');
    await userEvent.click(await screen.findByRole('button', { name: 'stop it' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', {
      payload: { id: 'i9n8t7k6', state: 'stopped' },
    });
  });

  /**
   * INT4h: a RUNNING intake takes no messages either. It is one turn, framed as one prompt, and on
   * the protocol door its stdin is the driver's own frames — so a composer there sent a person's
   * words into nothing, or into the middle of the JSON-RPC stream. No composer: its stop and its
   * ask's door live in the head, with the line that says where an answer goes.
   */
  it('offers a running intake no composer, only its stop and its ask', async () => {
    SESSIONS = [RUNNING_INTAKE];
    const onAnswerAsk = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="r7n8t7k6" onSelect={vi.fn()} notify={() => {}} onAnswerAsk={onAnswerAsk} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    expect(await screen.findByText(/takes no messages/)).toBeInTheDocument();
    expect(screen.queryByLabelText('message')).toBeNull();
    expect(screen.queryByRole('button', { name: 'send' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'finish' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'open ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledWith('0fda18');
  });

  /** The stop the composer used to carry: the process is cut off, over the driver. */
  it('stops a running intake over the driver', async () => {
    SESSIONS = [RUNNING_INTAKE];
    show('r7n8t7k6');
    await userEvent.click(await screen.findByRole('button', { name: 'stop it' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', { payload: { id: 'r7n8t7k6' } });
  });

  /** A conversation keeps its composer: it is the one kind of session that takes turns. */
  it('keeps the composer for a conversation', async () => {
    SESSIONS = [{ ...RUNNING_INTAKE, id: 'c0nv0000', ask: undefined, repository: 'engine' }];
    show('c0nv0000');

    expect(await screen.findByLabelText('message')).toBeInTheDocument();
    expect(screen.queryByText(/takes no messages/)).toBeNull();
  });

  /** A driven session that is not parked gets no moves: there is nothing waiting on anybody. */
  it('offers no moves on a session nobody is waiting for', async () => {
    SESSIONS = [DRIVEN];
    show('s1a2b3c4');

    await screen.findByRole('heading', { level: 2, name: 'Expose a streaming budget' });
    expect(screen.queryByRole('button', { name: 'finish it' })).toBeNull();
  });
});

/**
 * Review (SURF6, design §5): what the session actually did. The dock's second occupant — which is
 * why the dock exists at all now, and why the timeline moved into it.
 */
describe('reviewing what a session landed', () => {
  const DIFF = {
    session: 's1a2b3c4',
    base: 'abc1234567890',
    truncated: null as string | null,
    files: [
      {
        path: 'src/chunk.ts', status: 'modified', added: 4, removed: 1,
        patch: '@@ -1 +1,2 @@\n-old\n+new',
      },
      { path: 'assets/logo.png', status: 'added', added: null, removed: null, patch: null },
    ],
  };

  beforeEach(() => {
    SESSIONS = [DRIVEN];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? DIFF : DRIVER_STATE));
    // A viewer who has opened the dock, where review lives: it opens on demand (UX5 U7).
    window.localStorage.setItem('daoris.dockClosed', '0');
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    // A draft outlives its test the way it outlives a reload (CONV4b).
    window.localStorage.removeItem('daoris.drafts');
    window.localStorage.removeItem('daoris.dockClosed');
  });

  /** The dock opens on the timeline: a running session is watched far more often than reviewed. */
  it('docks the timeline beside the session, and asks for no diff until someone looks', async () => {
    show('s1a2b3c4');
    await screen.findByRole('tab', { name: 'Review' });

    expect(screen.getByRole('tab', { name: 'Timeline' }).getAttribute('aria-selected')).toBe('true');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DIFF', expect.anything());
  });

  /**
   * REVIEW2: the changes laid out in one column or the old side beside the new — one choice for the
   * whole review, remembered per viewer like the frame's widths.
   */
  it('lays the changes out unified or side by side, and remembers which', async () => {
    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    await userEvent.click(await screen.findByText('src/chunk.ts'));

    const layout = screen.getByRole('radiogroup', { name: 'layout' });
    expect(within(layout).getByRole('radio', { name: 'unified' })).toHaveAttribute('aria-checked', 'true');
    expect(within(screen.getAllByRole('row')[0]!).getAllByRole('cell').map((cell) => cell.textContent))
      .toEqual(['1', '', '−', 'old']);

    await userEvent.click(within(layout).getByRole('radio', { name: 'side by side' }));
    expect(within(screen.getAllByRole('row')[0]!).getAllByRole('cell').map((cell) => cell.textContent))
      .toEqual(['1', 'old', '1', 'new']);
    expect(window.localStorage.getItem('daoris.reviewLayout')).toBe('split');
    window.localStorage.removeItem('daoris.reviewLayout');
  });

  it('reads the landed work off the checkout when the review tab is opened', async () => {
    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText('src/chunk.ts')).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DIFF', {
      payload: { id: 's1a2b3c4' },
    });
    // The range it is measured from, stated — a review that does not say so is an opinion.
    expect(screen.getByText(/abc12345/)).toBeTruthy();
    // And a binary file is listed as uncounted rather than as an empty change.
    expect(screen.getByText('binary')).toBeTruthy();
  });

  /**
   * The bound is the host's sentence and reaches the person word for word — the console's rule,
   * applied to a surface that has no upper size either.
   */
  it('shows the bound verbatim when the host truncated the patches', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_DIFF'
      ? { ...DIFF, truncated: '9 more files changed; their patches are not shown here. `git diff` in the tree has all of it.' }
      : DRIVER_STATE));

    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText(/9 more files changed/)).toBeTruthy();
  });

  /**
   * Unreviewable is INFORMATION, not a fault (D48 §6's class): a record that travelled from another
   * machine names no tree here and never will. The host's sentence says which of the three it is.
   */
  it('renders the host’s own sentence when there is nothing here to diff', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type !== 'SESSION_DIFF') return DRIVER_STATE;
      throw Object.assign(new Error('fallback'), {
        code: 'SESSION_NOT_REVIEWABLE', parameters: { session: 's1a2b3c4' },
      });
    });

    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText(/work is not on this machine/)).toBeTruthy();
  });

  /** A session that committed nothing changed nothing — an answer, not an empty list. */
  it('says a session landed nothing rather than showing an empty diff', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? { ...DIFF, files: [] } : DRIVER_STATE));

    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText('Nothing landed')).toBeTruthy();
  });
});

/**
 * The two acts on a reviewed session (SURF6b, D51 rules 6–7). The guards themselves live in the tree
 * layer and are tested against real git; what is asserted here is that the surface carries the
 * person's intent faithfully — and, above all, that it never sends `force` on a first press.
 */
describe('acting on what a session landed', () => {
  const DIFF = {
    session: 's1a2b3c4',
    base: 'abc1234567890',
    truncated: null as string | null,
    files: [{ path: 'src/chunk.ts', status: 'modified', added: 4, removed: 1, patch: '@@ -1 +1 @@\n-a\n+b' }],
  };

  // The acts act on a session TREE (D51), so the session under test holds one. A record that names
  // no tree here is the other case, asserted at the end.
  const IN_A_TREE = { ...DRIVEN, tree: 'C:/somewhere/.daoris/trees/default/engine-abc' };

  beforeEach(() => {
    SESSIONS = [IN_A_TREE];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    // A viewer who has opened the dock, where review lives: it opens on demand (UX5 U7).
    window.localStorage.setItem('daoris.dockClosed', '0');
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    // A draft outlives its test the way it outlives a reload (CONV4b).
    window.localStorage.removeItem('daoris.drafts');
    window.localStorage.removeItem('daoris.dockClosed');
  });

  const review = async () => {
    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    await screen.findByText('src/chunk.ts');
  };

  /** WSR1 (D87): the pane says where a press sends the work before it is pressed, in the driver's plan. */
  it('says before the press that accepting puts the work on the branch the rule names', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'LANDING') return { session: 's1a2b3c4', form: 'branch', target: 'feature/0fda18-fix', source: 'workspace' };
      return DRIVER_STATE;
    });

    await review();

    expect(await screen.findByText(/for you to push and open a pull request from/)).toBeTruthy();
    expect(screen.getByText('feature/0fda18-fix', { selector: 'code' })).toBeTruthy();
  });

  /** WSR4 (D100): who pushes it is said before the press — and what would refuse the press, where something would. */
  it('says before the press which plugin pushes the branch, and what stands in its way', async () => {
    let plan: Record<string, unknown> = {
      session: 's1a2b3c4', form: 'branch', target: 'feature/0fda18-fix', source: 'workspace', plugin: 'github-pull-request',
    };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'LANDING') return plan;
      return DRIVER_STATE;
    });

    await review();

    expect(await screen.findByText(/pushes it and opens the pull request\. Nothing is merged/)).toBeTruthy();
    expect(screen.getByText('github-pull-request', { selector: 'code' })).toBeTruthy();
    cleanup();

    plan = { ...plan, problem: 'plugin `github-pull-request` is switched off on this machine — `daoris plugin enable github-pull-request` switches it on.' };
    await review();
    expect(await screen.findByText(/switched off on this machine/)).toBeTruthy();
  });

  /** WSR4 (D100): the plugin's pull request, once opened, is one press away — a link that opens as the person's links do. */
  it('offers the pull request a plugin opened as a link beside the landing sentence', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'LAND_SESSION_TREE') {
        return {
          session: 's1a2b3c4', done: true, branch: 'feature/0fda18-fix',
          message: 'put the work on `feature/0fda18-fix` — 1 commit(s) from `main`. Plugin `github-pull-request`: pushed it.',
          plugin: { id: 'github-pull-request', pushed: true, pullRequest: 'https://example.test/example-org/engine/pull/7', message: 'pushed it.', failed: false },
        };
      }
      return DRIVER_STATE;
    });

    await review();
    await userEvent.click(screen.getByRole('button', { name: 'accept' }));

    expect(await screen.findByText(/Plugin `github-pull-request`: pushed it/)).toBeTruthy();
    expect(screen.getByRole('link', { name: 'open the pull request' })).toHaveAttribute('href', 'https://example.test/example-org/engine/pull/7');
  });

  /**
   * WSR5b: a session whose work already landed on a branch can hand that branch to a landing plugin
   * afterwards — the rule's, named before the press — and the pane renders what the driver says back.
   */
  it('hands the branch its landing made to the plugin, and renders what it says back', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'HANDOFF_PLAN') {
        return { session: 's1a2b3c4', branch: 'feature/0fda18-fix', repository: 'engine', plugin: 'github-pull-request', problem: null, commits: 1 };
      }
      if (type === 'HANDOFF') {
        return {
          session: 's1a2b3c4', done: true, branch: 'feature/0fda18-fix',
          message: 'handed `feature/0fda18-fix` to plugin `github-pull-request`. Plugin `github-pull-request`: pushed it.',
          plugin: { id: 'github-pull-request', pushed: true, pullRequest: 'https://example.test/example-org/engine/pull/8', message: 'pushed it.', failed: false },
        };
      }
      return DRIVER_STATE;
    });

    await review();

    expect(await screen.findByText(/can push it and open the pull request/)).toBeTruthy();
    expect(screen.getByText('feature/0fda18-fix', { selector: 'code' })).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'hand it to github-pull-request' }));

    // LEFT3 a: a hand-off waits as long as its plugin may (`pluginBound`, six minutes), not the bridge's 30 seconds.
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HANDOFF', { payload: { id: 's1a2b3c4' }, timeoutMs: 6 * 60_000 });
    expect(await screen.findByText(/Plugin `github-pull-request`: pushed it/)).toBeTruthy();
    expect(screen.getByRole('link', { name: 'open the pull request' })).toHaveAttribute('href', 'https://example.test/example-org/engine/pull/8');
  });

  /** WSR5b: what stands in the way is said, and the press that could only be refused is not offered (UX5 U66). */
  it('says what stands in the way of a hand-off, and offers no press that could only be refused', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'HANDOFF_PLAN') {
        return {
          session: 's1a2b3c4', branch: 'feature/0fda18-fix', repository: 'engine', plugin: 'github-pull-request', commits: 1,
          problem: '`feature/0fda18-fix` is already on its remote at this commit, and its pull request was answered: https://example.test/pr/8 Nothing to hand on.',
        };
      }
      return DRIVER_STATE;
    });

    await review();

    expect(await screen.findByText(/already on its remote at this commit/)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'hand it to github-pull-request' })).toBeDisabled();
  });

  /** WSR5b: a session whose landing made no branch, or whose rule names no plugin, has nothing to hand on. */
  it('offers no hand-off where the landing made no branch or no plugin is named', async () => {
    let plan: Record<string, unknown> = { session: 's1a2b3c4', branch: null };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'HANDOFF_PLAN') return plan;
      return DRIVER_STATE;
    });

    await review();
    await screen.findByRole('button', { name: 'accept' });
    expect(screen.queryByRole('button', { name: /hand it to/ })).toBeNull();
    cleanup();

    plan = { session: 's1a2b3c4', branch: 'feature/0fda18-fix', repository: 'engine', plugin: null, problem: 'no plugin is named', commits: 1 };
    await review();
    await screen.findByRole('button', { name: 'accept' });
    expect(screen.queryByRole('button', { name: /hand it to/ })).toBeNull();
  });

  /** WSR5b: a tidied landing leaves no tree, and its branch can still be handed on. */
  it('offers the hand-off for a session whose tree the landing tidied away', async () => {
    SESSIONS = [DRIVEN];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'HANDOFF_PLAN') {
        return { session: 's1a2b3c4', branch: 'feature/0fda18-fix', repository: 'engine', plugin: 'github-pull-request', problem: null, commits: 1 };
      }
      return DRIVER_STATE;
    });

    await review();

    expect(await screen.findByRole('button', { name: 'hand it to github-pull-request' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'accept' })).toBeNull();
  });

  /** SESS1 S10: the head finds its branch in the clean-up's list by the tree's folder, on a Windows path too. */
  it('names the branch its tree left and whether its work landed, in the head', async () => {
    SESSIONS = [{ ...IN_A_TREE, tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-abc12345' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'SWEEP_PLAN') {
        return { branches: [{ repository: 'engine', workspace: 'default', branch: 'daoris/s-abc12345', hasTree: true,
          kind: 'landed', commits: 2, where: 'feature/x', removable: true }] };
      }
      return DRIVER_STATE;
    });

    show('s1a2b3c4');

    expect(await screen.findByText('landed on feature/x')).toBeTruthy();
    expect(screen.getByText('daoris/s-abc12345')).toBeTruthy();
  });

  it('accepts by asking the driver to land the work, and renders whatever it says back', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'LAND_SESSION_TREE') {
        return { session: 's1a2b3c4', done: true, message: 'merged `daoris/x` into `main` — 2 commit(s).' };
      }
      return DRIVER_STATE;
    });

    await review();
    await userEvent.click(screen.getByRole('button', { name: 'accept' }));

    // LEFT3 a: a landing may hand its branch to a plugin, so it waits as long as the plugin may (`pluginBound`).
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_SESSION_TREE', {
      payload: { id: 's1a2b3c4' },
      timeoutMs: 6 * 60_000,
    });
    expect(await screen.findByText(/merged `daoris\/x` into `main`/)).toBeTruthy();
  });

  /**
   * A refusal is an ANSWER and its sentence is the contract — "the checkout is not clean" is the
   * `reaching-in` guard speaking, and rewording it here would be a second copy of the reason.
   */
  it('renders a refused merge verbatim and changes nothing', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'LAND_SESSION_TREE') {
        return {
          session: 's1a2b3c4', done: false,
          message: "the repository's own checkout is not clean (2 paths), and merging into somebody's work in flight is exactly what Daoris does not do.",
        };
      }
      return DRIVER_STATE;
    });

    await review();
    await userEvent.click(screen.getByRole('button', { name: 'accept' }));

    expect(await screen.findByText(/not clean \(2 paths\)/)).toBeTruthy();
  });

  /**
   * 🔴 The one that matters most: a first press NEVER forces. The unforced call is what produces the
   * sentence naming what would be lost, and only then is a destructive confirm offered.
   */
  it('never forces a discard on the first press, and arms the confirm with the host’s own warning', async () => {
    let forced: unknown = 'never called';
    invoke.mockImplementation(async (_module: string, type: string, body?: { payload?: unknown }) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'DISCARD_SESSION_TREE') {
        forced = body?.payload;
        const payload = body?.payload as { force?: boolean } | undefined;
        return payload?.force
          ? { session: 's1a2b3c4', done: true, message: 'removed the session tree.' }
          : {
            session: 's1a2b3c4', done: false,
            message: 'the tree holds commits `main` has not taken:\nabc1234 cap hydration\nMerge them from the root, or say it again with --force to discard them.',
          };
      }
      return DRIVER_STATE;
    });

    await review();
    await userEvent.click(screen.getByRole('button', { name: 'discard the tree' }));

    // No `force` anywhere in the first call.
    expect(forced).toEqual({ id: 's1a2b3c4' });
    // The host's warning is what the person now reads, naming what would go.
    expect(await screen.findByText(/has not taken/)).toBeTruthy();
    // And only now is the destructive press available.
    const again = screen.getByRole('button', { name: 'discard it anyway' });

    await userEvent.click(again);
    expect(forced).toEqual({ id: 's1a2b3c4', force: true });
    expect(await screen.findByText(/removed the session tree/)).toBeTruthy();
  });

  /**
   * 🔴 REV3: an answer belongs to the session it was asked about. A refusal that landed after the
   * person moved to another session armed *discard it anyway* THERE — and the forced press discards
   * whichever session is attended now, a tree nobody had looked at.
   */
  it('drops a discard answer that lands after the person moved to another session', async () => {
    let answer: (value: unknown) => void = () => {};
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'DISCARD_SESSION_TREE') return new Promise((resolve) => { answer = resolve; });
      return DRIVER_STATE;
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const pane = (session: string) => (
      <QueryClientProvider client={client}><DiffPane session={session} hasTree /></QueryClientProvider>
    );

    const { rerender } = render(pane('s1a2b3c4'));
    await userEvent.click(await screen.findByRole('button', { name: 'discard the tree' }));
    rerender(pane('s9f8e7d6'));
    await screen.findByRole('button', { name: 'discard the tree' });
    await act(async () => answer({ session: 's1a2b3c4', done: false, message: 'the tree holds commits `main` has not taken.' }));

    expect(screen.queryByRole('button', { name: 'discard it anyway' })).toBeNull();
    expect(screen.queryByText(/has not taken/)).toBeNull();
  });

  it('lets the person back out of a discard they have been warned about', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'DISCARD_SESSION_TREE') {
        return { session: 's1a2b3c4', done: false, message: 'the tree holds commits `main` has not taken.' };
      }
      return DRIVER_STATE;
    });

    await review();
    await userEvent.click(screen.getByRole('button', { name: 'discard the tree' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Never mind' }));

    expect(screen.queryByRole('button', { name: 'discard it anyway' })).toBeNull();
    expect(screen.getByRole('button', { name: 'discard the tree' })).toBeTruthy();
  });

  /** Sending it back is a door into the platform's own composer, never a second publish path. */
  it('hands the repository to whoever opens the quest composer', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? DIFF : DRIVER_STATE));
    const onSendBack = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}} onSendBack={onSendBack} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    await userEvent.click(await screen.findByRole('button', { name: 'send it back…' }));

    expect(onSendBack).toHaveBeenCalledWith('engine');
  });

  it('offers no send-back door where none was given', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? DIFF : DRIVER_STATE));

    await review();
    expect(screen.queryByRole('button', { name: 'send it back…' })).toBeNull();
  });

  /**
   * A record that travelled here from the machine that did the work names no tree on THIS one, so
   * there is nothing to merge and nothing to discard. Offering either would be offering something
   * that can only ever refuse — and one of them is destructive.
   */
  it('offers no acts at all on a session that holds no tree here', async () => {
    SESSIONS = [DRIVEN];
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? DIFF : DRIVER_STATE));

    await review();

    expect(screen.queryByRole('button', { name: 'accept' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'discard the tree' })).toBeNull();
    // The diff itself is still readable — seeing the work never depended on holding the tree.
    expect(screen.getByText('src/chunk.ts')).toBeTruthy();
  });

  /**
   * UX5 U66, seen on the window: a chat in the repository's OWN checkout names that checkout as its
   * tree, and Review offered to merge it and to discard it. The tree layer refuses both — a
   * checkout is never a side effect's to delete — so they were moves that could only refuse. The
   * work can still be sent back as a quest: that is about the work, not the tree.
   */
  it('offers no tree acts on a session in the repository\u2019s own checkout, and still sends it back', async () => {
    SESSIONS = [{ ...DRIVEN, tree: 'C:/somewhere/engine' }];
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? DIFF : DRIVER_STATE));
    const onSendBack = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <Tooltip.Provider>
          <WorkFrame selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}} onSendBack={onSendBack} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    await screen.findByText('src/chunk.ts');

    expect(screen.queryByRole('button', { name: 'accept' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'discard the tree' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'send it back…' }));
    expect(onSendBack).toHaveBeenCalledWith('engine');
  });
});

/**
 * FRAME6: the frame's geometry (components plan §3a) — a rail and a dock a person can resize, close to
 * a strip and open again, with the session in the middle keeping its floor. The numbers themselves are
 * `layout.ts`'s, asserted there; this is the frame doing what they say, and remembering what the person
 * chose.
 */
describe('the frame\'s geometry (FRAME6)', () => {
  const KEPT = ['daoris.railWidth', 'daoris.railClosed', 'daoris.dockWidth', 'daoris.dockShare', 'daoris.dockClosed'];
  const widen = (width: number) => act(() => {
    Object.defineProperty(window, 'innerWidth', { configurable: true, value: width });
    window.dispatchEvent(new Event('resize'));
  });

  beforeEach(() => {
    SESSIONS = [DRIVEN, CHAT];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
    widen(1600);
    // A viewer who has opened the dock: it opens on demand (UX5 U7), and these are about it open.
    window.localStorage.setItem('daoris.dockClosed', '0');
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    for (const key of KEPT) window.localStorage.removeItem(key);
    widen(1024);
  });

  it('resizes the rail from the keyboard within its bounds, and remembers the width', async () => {
    show('s1a2b3c4');
    const edge = await screen.findByRole('separator', { name: 'rail width' });
    expect(edge).toHaveAttribute('aria-valuenow', '280');

    edge.focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(screen.getByRole('separator', { name: 'rail width' })).toHaveAttribute('aria-valuenow', '304');
    expect(window.localStorage.getItem('daoris.railWidth')).toBe('304');
  });

  it('closes the rail to a strip that still reaches every session, and only the person opens it again', async () => {
    const { onSelect } = show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('button', { name: 'Hide the session list' }));

    expect(screen.queryByRole('separator', { name: 'rail width' })).toBeNull();
    await userEvent.click(await screen.findByRole('button', { name: 'conversation · engine · working' }));
    expect(onSelect).toHaveBeenCalledWith('c0ffee11');

    // A wider window is not the person asking for it back.
    widen(2400);
    expect(screen.queryByRole('separator', { name: 'rail width' })).toBeNull();
    expect(window.localStorage.getItem('daoris.railClosed')).toBe('1');

    await userEvent.click(screen.getByRole('button', { name: 'Show the session list' }));
    expect(await screen.findByRole('separator', { name: 'rail width' })).toBeInTheDocument();
  });

  it('draws the rail as a strip in a narrow window, and gives it back when the window widens', async () => {
    widen(1000);
    show('s1a2b3c4');
    expect(await screen.findByRole('button', { name: 'conversation · engine · working' })).toBeInTheDocument();
    expect(screen.queryByRole('separator', { name: 'rail width' })).toBeNull();

    widen(1600);
    expect(await screen.findByRole('separator', { name: 'rail width' })).toBeInTheDocument();
  });

  /**
   * LAYOUT1: a dragged dock was remembered in pixels and stayed 389px while the window grew
   * from 1518 to 1923 (measured on the shell). It is remembered as its share of the window now.
   */
  it('remembers a dragged dock as its share of the window, so it grows when the window does', async () => {
    show('s1a2b3c4');
    const edge = await screen.findByRole('separator', { name: 'side bar width' });
    edge.focus();
    await userEvent.keyboard('{ArrowRight}');
    const dragged = Number(screen.getByRole('separator', { name: 'side bar width' }).getAttribute('aria-valuenow'));
    expect(Number(window.localStorage.getItem('daoris.dockShare'))).toBeCloseTo(dragged / 1600, 5);

    widen(2400);
    expect(Number(screen.getByRole('separator', { name: 'side bar width' }).getAttribute('aria-valuenow')))
      .toBe(Math.round(2400 * (dragged / 1600)));
  });

  it('reads a width kept in pixels once, as its share of the window, and forgets the pixels', async () => {
    window.localStorage.setItem('daoris.dockWidth', '400');
    show('s1a2b3c4');

    expect(await screen.findByRole('separator', { name: 'side bar width' })).toHaveAttribute('aria-valuenow', '400');
    expect(window.localStorage.getItem('daoris.dockShare')).toBe(String(400 / 1600));
    expect(window.localStorage.getItem('daoris.dockWidth')).toBeNull();

    widen(2000);
    expect(screen.getByRole('separator', { name: 'side bar width' })).toHaveAttribute('aria-valuenow', '500');
  });

  /**
   * UX5 U7: the dock was open by default at 45%, where the reference's opens on demand, and on a
   * 1400px window the conversation started at 442px, near its floor. A viewer who never chose sees
   * the conversation with the dock closed to its strip; opening it is remembered like closing it.
   */
  it('opens the dock on demand: closed to its strip until the person opens it, and then remembered', async () => {
    window.localStorage.removeItem('daoris.dockClosed');
    show('s1a2b3c4');
    await screen.findByRole('button', { name: 'Open Review' });
    expect(screen.queryByRole('tablist', { name: 'right side bar' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Open Timeline' }));
    expect(await screen.findByRole('tab', { name: 'Timeline' })).toHaveAttribute('aria-selected', 'true');
    expect(window.localStorage.getItem('daoris.dockClosed')).toBe('0');
  });

  it('closes the dock to a strip, and opens it again only on the person\'s press, on the tab they chose', async () => {
    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('button', { name: 'Close the side bar' }));
    expect(screen.queryByRole('tablist', { name: 'right side bar' })).toBeNull();

    widen(2400);
    expect(screen.queryByRole('tablist', { name: 'right side bar' })).toBeNull();
    expect(window.localStorage.getItem('daoris.dockClosed')).toBe('1');

    await userEvent.click(screen.getByRole('button', { name: 'Open Review' }));
    expect(await screen.findByRole('tab', { name: 'Review' })).toHaveAttribute('aria-selected', 'true');
  });

  it('keeps each session\'s own tab in the dock', async () => {
    const { rerender, client, onSelect } = show('s1a2b3c4');
    const attend = (id: string) => rerender(
      <QueryClientProvider client={client}>
        <Tooltip.Provider><WorkFrame selected={id} onSelect={onSelect} notify={() => {}} /></Tooltip.Provider>
      </QueryClientProvider>,
    );

    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    attend('c0ffee11');
    expect(await screen.findByRole('tab', { name: 'Timeline' })).toHaveAttribute('aria-selected', 'true');
    attend('s1a2b3c4');
    expect(await screen.findByRole('tab', { name: 'Review' })).toHaveAttribute('aria-selected', 'true');
  });

  it('fills the frame when asked, drawing the same surface rather than a new one', async () => {
    show('s1a2b3c4');
    const surface = await screen.findByRole('tabpanel');

    await userEvent.click(screen.getByRole('button', { name: 'Fill the frame' }));
    expect(screen.getByRole('tabpanel')).toBe(surface);
    await userEvent.click(screen.getByRole('button', { name: 'Back beside the session' }));
    expect(screen.getByRole('tabpanel')).toBe(surface);
  });

  /**
   * The column used to carry the timeline on a window under 1024px, because the dock was hidden
   * there. FRAME6 keeps the dock at every width, so the column's copy drew the timeline twice.
   */
  it('draws the timeline once on a narrow window, in the dock', async () => {
    widen(900);
    show('s1a2b3c4');
    await screen.findByRole('tab', { name: 'Timeline' });
    expect(await screen.findAllByText('reached working')).toHaveLength(1);
  });

  it('asks to be closed rather than squeezing the session, when it cannot fit beside it', async () => {
    widen(1024);
    show('s1a2b3c4');
    expect(await screen.findByText(/too narrow to sit beside the session/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Close the side bar' }));
    expect(screen.queryByText(/too narrow to sit beside the session/)).toBeNull();
  });
});
