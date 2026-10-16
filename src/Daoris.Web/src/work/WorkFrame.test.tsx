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
  // 🔴 Two mounted instances of one hook share a source, so here only the last rendered hears: the frame's own second
  // reading of a record (SENDLOST1b's `SentSettler`) is mounted only while words wait on it, never beside every conversation.
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

vi.mock('@shenora/react', async () => {
  // What a press moved reaches the frame's state hub as the shell publishes it (STATEHUB1f, D169), never the press's hook.
  const { publishing } = await import('../test/publishing');
  const published = publishing(invoke, (type, payload) => eventHandlers.get(`DAORIS.${type}`)?.(payload));
  return {
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke: published, notifyReady }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      eventHandlers.set(`${module}.${type}`, handler);
    },
  };
});

import i18n from '../locales/i18n';
import { useStateHub } from '../bridge/stateHub';
import { ContextMenus } from '../menus/ContextMenu';
import type { OpenGroup } from '../app/opener';
import { menuActs, rightClick } from '../test/contextMenu';
import { watchScreen } from '../test/onScreen';
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

/** Every `SESSION_INPUT` the page sent, in order (SENDLOST1b: a send the page stopped waiting on is never said twice). */
const inputs = () => invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT');
/** What words on their way say once the page stopped waiting on their send (SENDLOST1b). */
const UNANSWERED = 'The machine has not answered yet; it may still take them.';
/** The bridge's own end of a call the page stopped waiting on (`stoppedWaiting`). */
const timedOut = () => Object.assign(new Error('The driver did not answer in time.'), { code: 'TIMEOUT' });

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

/** The window's state hub, as `ShellSignals` mounts it beside the frame: what a press moved is asked again here. */
function Hub() {
  useStateHub();
  return null;
}

/** The frame with the application's selection held for it, as `App` holds it. */
function show(selected: string | null = null, notify: (text: string) => void = () => {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const onSelect = vi.fn();
  const view = render(
    <QueryClientProvider client={client}>
      <Hub />
      <Tooltip.Provider>
        <WorkFrame selected={selected} onSelect={onSelect} notify={notify} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
  return { ...view, onSelect, client };
}

/** The frame on a go's group (ENTRY1g), held as `App` holds it: let go once brought, which `brought` is told. */
function going(group: OpenGroup) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const brought = vi.fn();
  function Going() {
    const [held, setHeld] = useState<OpenGroup | null>(group);
    const letGo = () => {
      brought();
      setHeld(null);
    };
    return <WorkFrame selected={null} onSelect={vi.fn()} notify={() => {}} group={held} onGroupBrought={letGo} />;
  }
  render(
    <QueryClientProvider client={client}>
      <Hub />
      <Tooltip.Provider>
        <Going />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
  return brought;
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
   * SESSUX1c, D126 §4.1: Sessions' list gains its ⋯, which it never had. *Group by* is a choice of two, *State* the
   * default and *Repository* the arrangement the list had before, ticked, and remembered for this viewer in the list's
   * filters (D118 §3f); the default is nothing kept.
   */
  it('groups the session list by state or by repository from its ⋯, and remembers the choice', async () => {
    SESSIONS = [DRIVEN, PARKED];
    show();
    const list = await screen.findByRole('complementary', { name: 'Sessions' });
    expect(await within(list).findByRole('heading', { level: 3, name: 'Waiting on you (1)' })).toBeInTheDocument();

    const user = userEvent.setup();
    within(list).getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    expect(screen.getByRole('group', { name: 'Group by' })).toBeInTheDocument();
    expect(screen.getByRole('menuitemradio', { name: 'State' })).toHaveAttribute('aria-checked', 'true');
    await user.click(screen.getByRole('menuitemradio', { name: 'Repository' }));

    expect(await within(list).findByRole('heading', { level: 3, name: 'engine' })).toBeInTheDocument();
    expect(JSON.parse(window.localStorage.getItem('daoris.list.sessions.filters')!)).toEqual({ group: 'repository' });

    within(list).getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitemradio', { name: 'State' }));
    expect(await within(list).findByRole('heading', { level: 3, name: 'Working (1)' })).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.list.sessions.filters')).toBeNull();
  });

  /**
   * SESSONE1c, D167 point 5: *Ask* joins the ⋯'s *Group by* only where the reader answers entries, which it groups by; chosen,
   * it is remembered beside the arrangement, and what no ask began is listed under *Not on an ask*.
   */
  it('offers Group by ask only where the reader answers entries, and remembers the choice', async () => {
    SESSIONS = [DRIVEN, PARKED];
    const entry = (leading: string) => ({
      entry: `quest:${leading}/engine`, origin: `quest:${leading}`, repository: 'engine', group: 'working', leading,
      parts: [{ session: leading, quest: null, shown: 'working', group: 'working', began: 'opened', after: null, askedBy: null }],
    });
    const answerEntries = (withEntries: boolean) => invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_GROUPS'
      ? { sessions: [], ...(withEntries ? { entries: [entry(DRIVEN.id), entry(PARKED.id)] } : {}) }
      : DRIVER_STATE));
    answerEntries(false);
    const first = show();
    const user = userEvent.setup();
    let list = await screen.findByRole('complementary', { name: 'Sessions' });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GROUPS', {}));
    within(list).getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    expect(screen.queryByRole('menuitemradio', { name: 'Ask' })).toBeNull();
    await user.keyboard('{Escape}');
    first.unmount();

    answerEntries(true);
    show();
    list = await screen.findByRole('complementary', { name: 'Sessions' });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GROUPS', {}));
    within(list).getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitemradio', { name: 'Ask' }));

    expect(await within(list).findByRole('heading', { level: 3, name: 'Not on an ask' })).toBeInTheDocument();
    expect(JSON.parse(window.localStorage.getItem('daoris.list.sessions.filters')!)).toEqual({ group: 'ask' });
  });

  /**
   * ENTRY1b (D161's ENTRY1 note): a door into what waits on the person names a group of Sessions' list, which the frame
   * hands its rail: the group's heading is brought into view and focused, and the door told.
   */
  it('hands its rail the group a door names, which brings it into view and tells the door', async () => {
    SESSIONS = [DRIVEN, PARKED];
    const brought = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected={null} onSelect={vi.fn()} notify={() => {}} group="you" onGroupBrought={brought} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    const list = await screen.findByRole('complementary', { name: 'Sessions' });
    const heading = await within(list).findByRole('heading', { level: 3, name: 'Waiting on you (1)' });
    await waitFor(() => expect(brought).toHaveBeenCalled());
    expect(heading).toHaveFocus();
  });

  /**
   * ENTRY1g (D161's ENTRY1b note): where the person keeps the list by repository, a go shows it by state for the go, as the
   * ⋯ says, and remembers nothing: their arrangement is back once they choose it, or leave Sessions.
   */
  it('shows the list by state for a go where the person keeps it by repository, and remembers nothing', async () => {
    SESSIONS = [DRIVEN, PARKED];
    window.localStorage.setItem('daoris.list.sessions.filters', JSON.stringify({ group: 'repository' }));
    const brought = going('you');

    const list = await screen.findByRole('complementary', { name: 'Sessions' });
    const heading = await within(list).findByRole('heading', { level: 3, name: 'Waiting on you (1)' });
    await waitFor(() => expect(brought).toHaveBeenCalledTimes(1));
    // Still by state once the go is let go, so the heading it focused stays.
    expect(heading).toHaveFocus();
    expect(JSON.parse(window.localStorage.getItem('daoris.list.sessions.filters')!)).toEqual({ group: 'repository' });

    const user = userEvent.setup();
    within(list).getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    expect(screen.getByRole('menuitemradio', { name: 'State' })).toHaveAttribute('aria-checked', 'true');
    await user.click(screen.getByRole('menuitemradio', { name: 'Repository' }));
    expect(await within(list).findByRole('heading', { level: 3, name: 'engine' })).toBeInTheDocument();
    expect(JSON.parse(window.localStorage.getItem('daoris.list.sessions.filters')!)).toEqual({ group: 'repository' });
  });

  /**
   * SESSUX1e, D126 §4.1: *Show archived* is a toggle in the list's ⋯, ticked as it stands and remembered beside the
   * arrangement; below a rule, *Archive what ended…* lists under the list's header before anything is archived.
   */
  it('shows archived sessions from the list’s ⋯, remembered, and says when nothing is archived', async () => {
    SESSIONS = [DRIVEN, PARKED];
    show();
    const list = await screen.findByRole('complementary', { name: 'Sessions' });
    await within(list).findByRole('heading', { level: 3, name: 'Waiting on you (1)' });

    const user = userEvent.setup();
    within(list).getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    expect(screen.getByRole('menuitemcheckbox', { name: 'Show archived' })).toHaveAttribute('aria-checked', 'false');
    await user.click(screen.getByRole('menuitemcheckbox', { name: 'Show archived' }));

    expect(await within(list).findByRole('heading', { level: 3, name: 'Archived (0)' })).toBeInTheDocument();
    expect(within(list).getByText('Nothing archived')).toBeInTheDocument();
    expect(JSON.parse(window.localStorage.getItem('daoris.list.sessions.filters')!)).toEqual({ archived: true });

    within(list).getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    expect(screen.getByRole('menuitemcheckbox', { name: 'Show archived' })).toHaveAttribute('aria-checked', 'true');
    await user.click(screen.getByRole('menuitemcheckbox', { name: 'Show archived' }));
    await waitFor(() => expect(within(list).queryByText('Nothing archived')).toBeNull());
    expect(window.localStorage.getItem('daoris.list.sessions.filters')).toBeNull();
  });

  it('lists what Archive what ended would take under the list’s header, and closes it on Close', async () => {
    SESSIONS = [DRIVEN, PARKED];
    show();
    const list = await screen.findByRole('complementary', { name: 'Sessions' });
    await within(list).findByRole('heading', { level: 3, name: 'Waiting on you (1)' });

    const user = userEvent.setup();
    within(list).getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Archive what ended…' }));

    // The reader here answers no session, so nothing it placed in Ended is listed; what waits on you is kept.
    const ask = await within(list).findByRole('group', { name: 'Archive what ended…' });
    expect(ask).toHaveTextContent('Nothing that ended is left to archive. Kept in the list: 1 waiting on you.');
    await user.click(within(ask).getByRole('button', { name: 'Close' }));
    expect(within(list).queryByRole('group', { name: 'Archive what ended…' })).toBeNull();
  });

  it('opens on the arrangement this viewer chose last', async () => {
    window.localStorage.setItem('daoris.list.sessions.filters', JSON.stringify({ group: 'repository' }));
    SESSIONS = [DRIVEN, PARKED];
    show();

    const list = await screen.findByRole('complementary', { name: 'Sessions' });
    expect(await within(list).findByRole('heading', { level: 3, name: 'engine' })).toBeInTheDocument();
    expect(within(list).queryByRole('heading', { level: 3, name: /^Waiting on you/ })).toBeNull();
  });

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
            layout={{ main: <main><h1>Overview</h1></main> }}
            onOpenSessions={onOpenSessions}
          />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    // Its session views say whose they are, since nothing else on this screen does, and lead back.
    const side = screen.getByRole('complementary', { name: 'right side bar' });
    expect(await within(side).findByText('Attending Expose a streaming budget')).toBeInTheDocument();
    await userEvent.click(within(side).getByRole('button', { name: 'Open in Sessions' }));
    expect(onOpenSessions).toHaveBeenCalled();

    expect(await screen.findByRole('heading', { level: 1, name: 'Overview' })).toBeInTheDocument();
    expect(screen.getByRole('tablist', { name: 'right side bar' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'the panel' })).toBeInTheDocument();
    // No rail, and not the session's own centre either.
    expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Hide the session list' })).toBeNull();
    expect(screen.queryByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeNull();
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
            layout={{ main: <main><h1>Quests</h1></main> }}
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

  /**
   * D118 §3c, audit SE11: off Sessions the side bar's timeline spoke for the attended session with the
   * main area's sentence, *Choose a session in the session list*, on a view with no such list. It says
   * nothing is attended, and offers Sessions, where one is chosen.
   */
  it('says off Sessions that nothing is attended without naming a list the view does not have', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    const onOpenSessions = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected={null} onSelect={vi.fn()} notify={() => {}} layout={{ main: <main><h1>Overview</h1></main> }} onOpenSessions={onOpenSessions} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    const side = await screen.findByRole('complementary', { name: 'right side bar' });
    expect(within(side).getByText(/No session is attended/)).toBeInTheDocument();
    expect(side).not.toHaveTextContent(/session list/);
    await userEvent.click(within(side).getByRole('button', { name: 'Open Sessions' }));
    expect(onOpenSessions).toHaveBeenCalled();
  });

  /**
   * UX6b (design §2.5, D150 §8): off Sessions the side bar kept an ended session from the day before, its whole done note
   * in italics running past the window's foot. Its views follow the attended session there only while it runs or waits on
   * the person: ended, they say so, its note is not drawn, and the side bar opens on Ask Daoris.
   */
  it('follows off Sessions no session that has ended, and opens the side bar on Ask Daoris', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    SESSIONS = [{ ...DRIVEN, state: 'completed', note: 'verify passed: 3798 tests, every gate green' }];
    const onOpenSessions = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame
            selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}} ask={<p>the ask panel</p>}
            layout={{ main: <main><h1>Overview</h1></main> }} onOpenSessions={onOpenSessions}
          />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    const side = await screen.findByRole('complementary', { name: 'right side bar' });
    expect(await within(side).findByRole('tab', { name: 'Ask Daoris', selected: true })).toBeInTheDocument();
    expect(within(side).getByText('the ask panel')).toBeInTheDocument();

    await userEvent.click(within(side).getByRole('tab', { name: 'Timeline' }));
    expect(within(side).getByText(/The session attended in Sessions has ended/)).toBeInTheDocument();
    expect(side).not.toHaveTextContent('every gate green');
    expect(side).not.toHaveTextContent('Attending Expose a streaming budget');
    await userEvent.click(within(side).getByRole('button', { name: 'Open Sessions' }));
    expect(onOpenSessions).toHaveBeenCalled();
  });

  it('keeps following on Sessions the attended session that has ended, its timeline first', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    SESSIONS = [{ ...DRIVEN, state: 'completed', note: 'verify passed: every gate green' }];
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}} ask={<p>the ask panel</p>} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    const side = await screen.findByRole('complementary', { name: 'right side bar' });
    expect(await within(side).findByRole('tab', { name: 'Timeline', selected: true })).toBeInTheDocument();
    expect(within(side).queryByText(/has ended, so nothing follows it here/)).toBeNull();
  });

  /**
   * D118 §3b, audit SE11: a relaunch into Sessions with a session attended said *Nothing attended* while
   * the list that holds it was still on its way, since the frame finds the attended session in that list.
   * A first load is skeleton rows.
   */
  it('draws the attended session loading as skeleton rows, never as Nothing attended, while the list first loads', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) =>
      (String(input).startsWith('/api/sessions') ? new Promise<Response>(() => {}) : respond(String(input)))));
    show('s1a2b3c4');

    const main = await screen.findByRole('main');
    expect(main).toHaveAttribute('aria-busy', 'true');
    expect(screen.queryByText('Nothing attended')).toBeNull();
  });

  /** NEW1b: nothing attended is where a new conversation is asked for; the list's ＋ puts the focus in its prompt. */
  it('asks what to work on where nothing is chosen, and the list\'s ＋ puts the focus there', async () => {
    show(null);
    const main = await screen.findByRole('main');
    const prompt = await within(main).findByRole('textbox', { name: 'What would you like to work on?' });
    expect(prompt).not.toHaveFocus();
    expect(within(main).queryByText('Nothing attended')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'New conversation' }));
    expect(prompt).toHaveFocus();
  });

  /**
   * NEW1c, the entry design §1: a repository is added, or a folder imported, from the picker's foot without leaving Sessions,
   * and the words typed before are kept. The drawers are Projects' own, mounted here; registering starts nothing.
   */
  describe('adding a repository from the composer', () => {
    const PICKED = {
      path: 'D:/repos/borealis', name: 'borealis', exists: true, adopted: false, git: true,
      owns: [], accepts: [], packs: [], join: false, shareKnowledge: false, uses: [],
    };
    let registry: unknown[];
    let imported: { folder: string; imported: number; repositories: string[]; message: string; workspace?: string };

    const listed = (name: string, workspace: string) => ({
      repository: name, adopted: false, registered: true, owns: [], accepts: [], packs: [], entries: 0,
      root: `D:/repos/${name}`, workspace,
    });

    beforeEach(() => {
      registry = [...REGISTRY];
      imported = { folder: 'D:/repos', imported: 0, repositories: [], message: 'Nothing imported.' };
      vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input);
        if (url === '/api/registry/import' && init?.method === 'POST') {
          registry = [...registry, ...imported.repositories.map((name) => listed(name, imported.workspace ?? 'default'))];
          return Response.json(imported);
        }
        if (url === '/api/registry' && init?.method === 'POST') {
          registry = [...registry, listed('borealis', 'default')];
          return Response.json({ repository: 'borealis', workspace: 'default' });
        }
        if (url.startsWith('/api/registry')) return Response.json(registry);
        return respond(url);
      }));
      invoke.mockImplementation(async (_module: string, type: string) => (type === 'PICK_FOLDER' ? PICKED : DRIVER_STATE));
    });

    const promptBox = () => screen.findByRole('textbox', { name: 'What would you like to work on?' });
    const openFoot = async (name: string) => {
      await userEvent.click(await screen.findByRole('button', { name: /^Repository: / }));
      await userEvent.click(await screen.findByRole('button', { name }));
    };

    it('adds from the picker, keeps the words, chooses the repository and puts the focus back in the prompt', async () => {
      show(null);
      const prompt = await promptBox();
      await userEvent.type(prompt, 'Fix the cursor');

      await openFoot('Add repository…');
      expect(screen.queryByRole('listbox')).toBeNull();
      const drawer = await screen.findByRole('dialog', { name: 'Add a repository to this machine' });
      expect(prompt).toHaveValue('Fix the cursor');
      await userEvent.click(within(drawer).getByRole('button', { name: 'Choose a folder…' }));
      await userEvent.click(await within(drawer).findByRole('button', { name: 'Register' }));

      expect(await screen.findByRole('button', { name: 'Repository: borealis' })).toBeInTheDocument();
      await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
      expect(prompt).toHaveValue('Fix the cursor');
      expect(prompt).toHaveFocus();
      expect(window.localStorage.getItem('daoris.start.chosen')).toBe('borealis');
      // Registering starts nothing.
      expect(invoke.mock.calls.some(([, type]) => type === 'START_CHAT')).toBe(false);
    });

    it('puts the focus back in the prompt when the drawer is cancelled', async () => {
      show(null);
      const prompt = await promptBox();
      await userEvent.type(prompt, 'Words');

      await openFoot('Import a folder…');
      const drawer = await screen.findByRole('dialog', { name: 'Import a folder as a workspace' });
      await userEvent.click(within(drawer).getByRole('button', { name: 'Never mind' }));

      await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
      expect(prompt).toHaveFocus();
      expect(prompt).toHaveValue('Words');
    });

    it('chooses the one repository an import registered', async () => {
      imported = { folder: 'D:/repos', imported: 1, repositories: ['borealis'], message: 'Imported borealis.', workspace: 'repos' };
      show(null);
      await openFoot('Import a folder…');
      const drawer = await screen.findByRole('dialog', { name: 'Import a folder as a workspace' });
      await userEvent.click(within(drawer).getByRole('button', { name: 'Choose a folder…' }));
      await userEvent.click(await within(drawer).findByRole('button', { name: 'Import' }));

      expect(await screen.findByRole('button', { name: 'Repository: borealis' })).toBeInTheDocument();
      expect(await promptBox()).toHaveFocus();
    });

    it('opens the picker on the workspace an import of several registered', async () => {
      imported = { folder: 'D:/repos', imported: 2, repositories: ['borealis', 'cirrus'], message: 'Imported 2.', workspace: 'repos' };
      show(null);
      await openFoot('Import a folder…');
      const drawer = await screen.findByRole('dialog', { name: 'Import a folder as a workspace' });
      await userEvent.click(within(drawer).getByRole('button', { name: 'Choose a folder…' }));
      await userEvent.click(await within(drawer).findByRole('button', { name: 'Import' }));

      const list = await screen.findByRole('listbox', { name: 'Repository' });
      expect(screen.getByRole('combobox', { name: 'find a repository' })).toHaveValue('repos');
      await waitFor(() => expect(within(list).getAllByRole('option').map((row) => row.textContent)).toEqual(['borealis', 'cirrus']));
    });
  });

  /**
   * D118 §3d, audit F10: a full side bar was `z-20` in the page's own stacking order, above the drawer's
   * `z-10`, so below 768 px a drawer could open under it. The frame is a stacking context of its own, so
   * every overlay drawn at the page's root — a drawer, a menu, the palette, Quick Ask — lies above all of it. The main
   * area's own drawer went with NEW1b; the composer's chip menus are drawn there the same way.
   */
  it('opens an overlay above a full side bar: the frame keeps its layers to itself', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    Object.defineProperty(window, 'innerWidth', { configurable: true, value: 700 });
    try {
      show(null);
      const side = await screen.findByRole('complementary', { name: 'right side bar' });
      const frame = side.parentElement!;
      expect(frame).toHaveClass('isolate');
      expect(side.className).toMatch(/\bz-20\b/);

      within(await screen.findByRole('main')).getByRole('button', { name: /^Tree:/ }).focus();
      await userEvent.keyboard('{Enter}');
      const menu = await screen.findByRole('menu');
      // Drawn at the page's root, outside the frame's own layers.
      expect(frame.contains(menu)).toBe(false);
    } finally {
      Object.defineProperty(window, 'innerWidth', { configurable: true, value: 1024 });
    }
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
    // Everything moved to the panel, the workflow (WORKFLOW1c) among it, and the side bar closed: it is not drawn at all.
    window.localStorage.setItem('daoris.viewPlaces', JSON.stringify({ timeline: 'panel', review: 'panel', workflow: 'panel' }));
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
    expect(JSON.parse(window.localStorage.getItem('daoris.viewPlaces')!))
      .toEqual({ timeline: 'panel', review: 'panel', workflow: 'panel', console: 'right' });
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

  /**
   * TRACE1b (D143, D50): how the attended session came to be, folded on its page and read through `TRACE` only on the
   * press, for that session; its quest opens where quests are read.
   */
  it('folds how the attended session came to be, reads it only on the press, and opens its quest', async () => {
    const chain = {
      chain: {
        kind: 'session', id: 's1a2b3c4', unread: [],
        links: [{
          kind: 'quest',
          quest: {
            id: 'abc123', source: 'quests', address: 'engine', title: 'Expose a streaming budget', status: 'Taken',
            from: 'game', requirements: [], then: [], records: [{ id: 's1a2b3c4', state: 'working' }],
          },
        }],
      },
      unread: [],
    };
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TRACE' ? chain : DRIVER_STATE));
    const onOpenQuest = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}} onOpenQuest={onOpenQuest} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    const section = await screen.findByRole('region', { name: 'How this came to be' });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'TRACE', expect.anything());
    await userEvent.click(within(section).getByRole('button', { name: /How this came to be/ }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TRACE', { payload: { kind: 'session', id: 's1a2b3c4' } }));
    expect(await within(section).findByText(/On no ask/)).toBeInTheDocument();
    await userEvent.click(within(section).getByRole('button', { name: 'Quest #abc123' }));
    expect(onOpenQuest).toHaveBeenCalledWith('abc123');
  });

  it('lets the head and the composer follow the centre\'s width, however wide the window', async () => {
    SESSIONS = [DRIVEN, CHAT];
    show('c0ffee11');

    // The conversation's own width is ConversationView's, held beside its other tests. The record under the page header
    // says no title of its own (UX7c, D152 §7), so it is found by its Details.
    const head = (await screen.findByRole('button', { name: /^Details/ })).closest('article')!;
    const composer = screen.getByRole('textbox', { name: 'Message' }).closest('form')!;
    expect(head.className).not.toMatch(/max-w-/);
    expect(composer.className).not.toMatch(/max-w-/);
  });

  it('is the rail and the attended session, bound by one selection', async () => {
    show('s1a2b3c4');

    // The rail's row and the page header's title are the same derived identity, from one implementation (D126 §3.2); the
    // record under the header does not say it a third time (UX7c, D152 §7).
    await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' });
    await vi.waitFor(() =>
      expect(screen.getAllByText('Expose a streaming budget')).toHaveLength(2));
    expect(screen.queryByRole('heading', { level: 2, name: 'Expose a streaming budget' })).toBeNull();
  });

  /**
   * RAIL1: a conversation's name is what was first said in it — the rail's row and the head read the
   * same one, from one implementation — and a row's menu reviews that session's work in the dock.
   */
  it('names a conversation by its first line in the rail and the head, and reviews it from its row', async () => {
    // A conversation's record names the checkout it works in (D51), which is work to read: Review is offered where
    // there is some (SESSUX1d, D126 §3.1).
    SESSIONS = [DRIVEN, { ...CHAT, tree: 'C:/somewhere/engine' }];
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_OPENINGS' ? { openings: { c0ffee11: 'Cap the hydration per frame' } } : DRIVER_STATE));
    const { onSelect } = show('c0ffee11');

    await screen.findByRole('heading', { level: 1, name: 'Cap the hydration per frame' });
    await vi.waitFor(() => expect(screen.getAllByText('Cap the hydration per frame')).toHaveLength(2));

    const user = userEvent.setup();
    screen.getByRole('button', { name: 'more for Cap the hydration per frame' }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Review' }));
    expect(onSelect).toHaveBeenCalledWith('c0ffee11');
    expect(screen.getByRole('tab', { name: 'Review' })).toHaveAttribute('aria-selected', 'true');
  });

  it('lands on a new conversation, not on a session it chose for the person', async () => {
    const { onSelect } = show(null);
    expect(await screen.findByRole('textbox', { name: 'What would you like to work on?' })).toBeInTheDocument();
    expect(onSelect).not.toHaveBeenCalled();
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

    expect(await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' }))
      .toBeInTheDocument();
    expect(await screen.findByRole('button', { name: /^Details/ })).toBeInTheDocument();
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
    // A draft outlives its test the way it outlives a reload (CONV4b), and so do the starts this machine remembers (NEW1b).
    window.localStorage.removeItem('daoris.drafts');
    window.localStorage.removeItem('daoris.start.recent');
    window.localStorage.removeItem('daoris.start.chosen');
  });

  /** *New conversation*'s prompt (NEW1b, D162 point 1), which stands in the main area while nothing is attended. */
  const promptBox = () => screen.findByRole('textbox', { name: 'What would you like to work on?' });

  /** The frame with its selection held as `App` holds it, so attending a session shows it. */
  function held(selected: string | null = null) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const onSelect = vi.fn();
    const notify = vi.fn();
    function Held() {
      const [chosen, setChosen] = useState<string | null>(selected);
      return (
        <WorkFrame selected={chosen} onSelect={(id) => { onSelect(id); setChosen(id); }} notify={notify} />
      );
    }
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider><Held /></Tooltip.Provider>
      </QueryClientProvider>,
    );
    return { onSelect, notify };
  }

  /** What each START_CHAT carried, in order. */
  const starts = () => invoke.mock.calls
    .filter(([, type]) => type === 'START_CHAT')
    .map(([, , options]) => (options as { payload: Record<string, unknown> }).payload);

  /**
   * 🔴 The palette's *New conversation* arrives as an EVENT — `intent`, cleared by `onIntentTaken`
   * the way `App` clears it. Consumed by setting state during render, React re-ran the frame with
   * the same prop on every pass: the quest composer's opening draft failed exactly this way and
   * opened nothing on the window (FIX-LOG 2026-09-23). Held here as App holds it, with a session
   * attended, which *New conversation* puts down for the composer.
   */
  it('opens the composer when the palette asks, once, putting the attended session down', async () => {
    const onSelect = vi.fn();
    function Held() {
      const [intent, setIntent] = useState<'start' | 'review' | null>('start');
      const [selected, setSelected] = useState<string | null>('s1a2b3c4');
      return (
        <WorkFrame
          selected={selected} onSelect={(id) => { onSelect(id); setSelected(id); }} notify={() => {}}
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

    await waitFor(async () => expect(await promptBox()).toHaveFocus());
    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect).toHaveBeenCalledWith(null);
  });

  /**
   * NEW1f (D162's window note): pressed from a session in another repository, the composer opens on that repository, though
   * the press puts the session down; and *Recent* lists where the person has been working, not only what the composer started.
   */
  it('opens the composer on the repository of the session it was pressed from, and lists the sessions’ repositories as Recent', async () => {
    const game = { ...REGISTRY[0]!, repository: 'game', root: 'C:/somewhere/game' };
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => (String(input).startsWith('/api/registry')
      ? Response.json([...REGISTRY, game])
      : respond(String(input)))));
    const ELSEWHERE = { ...CHAT, id: 'decaf222', repository: 'game', updated: '2026-09-02T02:00:00Z' };
    const ASK = { ...CHAT, id: 'a5k0a5k0', repository: 'daoris:help', updated: '2026-09-02T00:20:00Z' };
    SESSIONS = [DRIVEN, ELSEWHERE, ASK];
    const onSelect = vi.fn();
    function Held() {
      const [intent, setIntent] = useState<'start' | 'review' | null>(null);
      const [selected, setSelected] = useState<string | null>('decaf222');
      press = () => setIntent('start');
      return (
        <WorkFrame
          selected={selected} onSelect={(id) => { onSelect(id); setSelected(id); }} notify={() => {}}
          intent={intent} onIntentTaken={() => setIntent(null)}
        />
      );
    }
    let press: () => void = () => {};
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider><Held /></Tooltip.Provider>
      </QueryClientProvider>,
    );
    await screen.findByLabelText('Message');

    act(() => press());

    expect(await screen.findByRole('button', { name: 'Repository: game' })).toBeInTheDocument();
    expect(onSelect).toHaveBeenCalledWith(null);
    await userEvent.click(screen.getByRole('button', { name: 'Repository: game' }));
    const groups = within(screen.getByRole('listbox', { name: 'Repository' })).getAllByRole('group');
    expect(groups[0]).toHaveAttribute('aria-label', 'Recent');
    // Newest first by the session's own update: game, then engine; Ask Daoris's chat is not a repository.
    expect(within(groups[0]!).getAllByRole('option').map((row) => row.textContent?.split(' ')[0])).toEqual(['game', 'engine']);
  });

  /**
   * NEW1d-b (D162): Ask Daoris's go to New conversation arrives as an EVENT too, a `compose` with an id, consumed once by that
   * id (FIX-LOG 2026-09-23): the attended session is put down, the repository chosen, the words put in above any already there,
   * and the prompt owes the focus. Nothing is sent.
   */
  it('opens the composer filled by a go, once by its id, keeping the words already typed below its own', async () => {
    const game = { ...REGISTRY[0]!, repository: 'game', root: 'C:/somewhere/game' };
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => (String(input).startsWith('/api/registry')
      ? Response.json([...REGISTRY, game])
      : respond(String(input)))));
    SESSIONS = [DRIVEN, CHAT];
    const onSelect = vi.fn();
    let go: (next: { repository?: string; sentence?: string; id: number }) => void = () => {};
    function Held() {
      const [selected, setSelected] = useState<string | null>('c0ffee11');
      const [compose, setCompose] = useState<{ repository?: string; sentence?: string; id: number } | null>(null);
      go = setCompose;
      return <WorkFrame selected={selected} onSelect={(id) => { onSelect(id); setSelected(id); }} notify={() => {}} compose={compose} />;
    }
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider><Held /></Tooltip.Provider>
      </QueryClientProvider>,
    );
    await screen.findByLabelText('Message');

    act(() => go({ repository: 'game', sentence: 'fix the build', id: 1 }));

    const prompt = await promptBox();
    expect(prompt).toHaveValue('fix the build');
    expect(await screen.findByRole('button', { name: 'Repository: game' })).toBeInTheDocument();
    await waitFor(() => expect(prompt).toHaveFocus());
    expect(onSelect).toHaveBeenCalledWith(null);
    expect(window.localStorage.getItem('daoris.start.chosen')).toBe('game');
    expect(invoke.mock.calls.some(([, type]) => type === 'START_CHAT')).toBe(false);

    // The same go again is not a new one; a new id puts its words above what is there.
    act(() => go({ sentence: 'fix the build', id: 1 }));
    expect(prompt).toHaveValue('fix the build');
    act(() => go({ sentence: 'and the tests', id: 2 }));
    await waitFor(() => expect(prompt).toHaveValue('and the tests\n\nfix the build'));
    // A go naming neither opens the composer as it was.
    act(() => go({ id: 3 }));
    expect(prompt).toHaveValue('and the tests\n\nfix the build');
    expect(screen.getByRole('button', { name: 'Repository: game' })).toBeInTheDocument();
  });

  /** The prompt takes the focus for a press, and not when nothing is attended again by another path. */
  it('takes the focus for a press, and not when it comes back by another path', async () => {
    SESSIONS = [DRIVEN, CHAT];
    let put: (id: string | null) => void = () => {};
    function Held() {
      const [chosen, setChosen] = useState<string | null>('c0ffee11');
      put = setChosen;
      return <WorkFrame selected={chosen} onSelect={setChosen} notify={() => {}} />;
    }
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider><Held /></Tooltip.Provider>
      </QueryClientProvider>,
    );
    await screen.findByLabelText('Message');

    await userEvent.click(screen.getByRole('button', { name: 'New conversation' }));
    await waitFor(async () => expect(await promptBox()).toHaveFocus());

    act(() => put('c0ffee11'));
    await screen.findByLabelText('Message');
    act(() => (document.activeElement as HTMLElement | null)?.blur());
    act(() => put(null));
    expect(await promptBox()).not.toHaveFocus();
  });

  /**
   * D162 point 2, NEW1a: the words go with the start, with a request id the driver never starts twice, and the page
   * attends what comes back. The repository is remembered as a recent start on this machine.
   */
  it('starts on the words with a request id, attends what comes back, and remembers the repository', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') return { sessionId: 'c0ffee11', sent: true, message: 'conversation `c0ffee11` starts with your words.' };
      if (type === 'HARNESSES') return ROSTER;
      return DRIVER_STATE;
    });

    const { onSelect } = show(null);
    await userEvent.type(await promptBox(), 'what is this repository for?{Enter}');

    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('c0ffee11'));
    expect(starts()).toEqual([
      { repository: 'engine', text: 'what is this repository for?', requestId: expect.stringMatching(/^[A-Za-z0-9-]{1,64}$/) },
    ]);
    expect(JSON.parse(window.localStorage.getItem('daoris.start.recent') ?? '[]')).toEqual(['engine']);
  });

  /**
   * Every choice may be left alone, and silence means the driver's own resolution rather than a
   * default this surface invented (D49 §4, D51): the omitted field IS the answer. The checkout is said
   * exactly, as false (D162 point 2).
   */
  it('carries the account and the tree chosen, and omits what was not chosen', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') return { sessionId: null, sent: false, message: 'busy' };
      if (type === 'HARNESSES') return ROSTER;
      return DRIVER_STATE;
    });

    show(null);
    const box = await promptBox();
    const pick = async (chip: RegExp, row: RegExp) => {
      (await screen.findByRole('button', { name: chip })).focus();
      await userEvent.keyboard('{Enter}');
      await userEvent.click(await screen.findByRole('menuitemradio', { name: row }));
    };
    // Signed out, so labelled so (the spawn's refusal then names the login action).
    await pick(/^Agent and account:/, /^work/);
    await pick(/^Tree:/, /^The checkout/);
    await userEvent.type(box, 'hello{Enter}');

    await waitFor(() => expect(starts()).toHaveLength(1));
    expect(starts()[0]).toEqual({
      repository: 'engine', text: 'hello', requestId: expect.any(String), profile: 'work', ownTree: false,
    });
  });

  /**
   * A refusal is the ledger's own sentence, verbatim — and nothing is attended on one: a
   * conversation that was refused has no session to show. The fixture holds a live DRIVEN session
   * in `engine`, which is the case worth pinning. The words and the choices stay (UX5 U68).
   */
  it('says a refusal under Start, whole, keeps the words, and attends nothing', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'START_CHAT'
      ? { sessionId: null, sent: false, message: '`engine` already has an active session — `s1a2b3c4` (working, quest `#abc123`).' }
      : DRIVER_STATE));
    const { onSelect, notify } = held(null);
    const box = await promptBox();
    await userEvent.type(box, 'Keep these words{Enter}');

    // UX5 U68: where Start was pressed, not a corner toast cut mid-sentence.
    expect(await within(screen.getByRole('main')).findByRole('alert'))
      .toHaveTextContent('engine already has an active session — s1a2b3c4 (working, quest #abc123).');
    expect(notify).not.toHaveBeenCalled();
    expect(onSelect).not.toHaveBeenCalled();
    expect(box).toHaveValue('Keep these words');
  });

  /**
   * NEW1a's `sent: false`: the conversation opened and the words did not reach it. The page attends it, and the words wait
   * in its box, with the driver's sentence beside them: the person's paragraph is never lost to a start (REV3).
   */
  it('attends a conversation that opened without the words, and keeps them in its box with the driver’s sentence', async () => {
    SESSIONS = [DRIVEN, CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') {
        return { sessionId: 'c0ffee11', sent: false, message: 'conversation `c0ffee11` opened, but your words did not reach it.' };
      }
      return DRIVER_STATE;
    });
    const { onSelect } = held(null);
    await userEvent.type(await promptBox(), 'what is this repository for?{Enter}');

    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('c0ffee11'));
    expect(await screen.findByLabelText('Message')).toHaveValue('what is this repository for?');
    expect(screen.getByText(/your words did not reach it/)).toBeInTheDocument();
  });

  /**
   * NEW1a: the same request id never starts twice. A press whose answer never came is pressed again with its id, so a start
   * that did open is answered rather than repeated; a press after an answer is a new start.
   */
  it('presses again with the same request id after a failure, and a new one after an answer', async () => {
    let answer = 0;
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') {
        answer += 1;
        if (answer === 1) throw new Error('the bridge did not answer');
        return { sessionId: null, sent: false, message: 'busy' };
      }
      return DRIVER_STATE;
    });
    held(null);
    const box = await promptBox();
    await userEvent.type(box, 'once{Enter}');
    expect(await within(screen.getByRole('main')).findByRole('alert')).toHaveTextContent('the bridge did not answer');

    await userEvent.click(screen.getByRole('button', { name: 'Start' }));
    await waitFor(() => expect(starts()).toHaveLength(2));
    await within(screen.getByRole('main')).findByText('busy');
    await userEvent.click(screen.getByRole('button', { name: 'Start' }));
    await waitFor(() => expect(starts()).toHaveLength(3));

    const [first, again, next] = starts().map((payload) => payload.requestId);
    expect(again).toBe(first);
    expect(next).not.toBe(first);
  });

  it('sends one message over the bridge and clears the box', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') return { sent: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'what is this repository for?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

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
    await userEvent.type(await screen.findByLabelText('Message'), 'what is this repository for?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

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
    await userEvent.upload(await screen.findByLabelText('Choose files…'), new File(['exit 3'], 'run.log', { type: 'text/plain' }));
    const box = screen.getByLabelText('Message');
    await userEvent.type(box, 'a long paragraph worth keeping');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText(/went nowhere/)).toBeTruthy();
    await waitFor(() => expect((box as HTMLTextAreaElement).value).toBe('a long paragraph worth keeping'));
    expect(notify).toHaveBeenCalledWith(expect.stringContaining('run.log'), 'error');
  });

  /**
   * SENDLOST1b: on a loaded machine the page stopped waiting on a chat's send (the bridge's TIMEOUT): the person's words
   * vanished behind the error, though the driver may still take them. They show the moment they are sent, as sending; a send
   * the page stopped waiting on keeps them, saying the machine has not answered yet, with nothing put back in the box to send
   * twice, until the conversation's queue holds them.
   */
  it('shows a chat\'s words as sending, keeps them when the send times out, and lets them go once its queue holds them', async () => {
    SESSIONS = [CHAT];
    let stop: () => void = () => {};
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: true };
      if (type === 'SESSION_INPUT') return new Promise((_, reject) => { stop = () => reject(timedOut()); });
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('c0ffee11', notify);

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'and the line?{Enter}');
    expect(await screen.findByRole('list', { name: 'sending…' })).toHaveTextContent('and the line?');

    await act(async () => { stop(); });
    expect(await screen.findByRole('list', { name: UNANSWERED })).toHaveTextContent('and the line?');
    expect(box).toHaveValue('');
    expect(notify).not.toHaveBeenCalledWith(expect.anything(), 'error');

    // The driver took them after all: they wait behind the turn as any word does, and are not said twice.
    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_QUEUED')!({
        session: 'c0ffee11', queued: [{ id: 'q1', text: 'and the line?', files: [] }], taking: true,
      });
    });
    expect(screen.queryByRole('list', { name: UNANSWERED })).toBeNull();
    expect(screen.getByRole('list', { name: 'waiting for this turn to end' })).toHaveTextContent('and the line?');
    expect(inputs()).toHaveLength(1);
  });

  /** SENDLOST1b: words whose send timed out to a chat that then ended without them never arrived, so they come back, said so. */
  it('gives a chat\'s words back, said so, once the conversation ended without them', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') {
        SESSIONS = [{ ...CHAT, state: 'completed' }];
        throw timedOut();
      }
      return DRIVER_STATE;
    });
    show('c0ffee11');

    await userEvent.type(await screen.findByLabelText('Message'), 'are you there?{Enter}');

    expect(await screen.findByText('The conversation ended before this reached it. It is back in the box.')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue('are you there?'));
    expect(screen.queryByRole('list', { name: UNANSWERED })).toBeNull();
    expect(inputs()).toHaveLength(1);
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
    // The stop is the page header's, and asks once (SESSUX1d, D126 §3.3).
    await userEvent.click(await screen.findByRole('button', { name: 'Stop…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Stop session' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(sentence));
  });

  /**
   * Finish and stop are still two endings, and never one button (SES2): *Finish* stays the composer's, the chat's own
   * ending (D49), and the session's stop is the page header's, its one owner (D126 §3.3), which says what Finish does
   * instead before it stops anything.
   */
  it('offers finishing in the composer and stopping in the page header, apart', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'END_CHAT') return { ended: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    const box = (await screen.findByLabelText('Message')).closest('form')!;
    expect(within(box).queryByRole('button', { name: 'Stop' })).toBeNull();
    await userEvent.click(within(box).getByRole('button', { name: 'Finish' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'END_CHAT', { payload: { id: 'c0ffee11' } });

    await userEvent.click(screen.getByRole('button', { name: 'Stop…' }));
    expect(screen.getByRole('group', { name: 'stop this session' })).toHaveTextContent(/Finish lets its agent wind up instead/);
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', expect.anything());
  });

  /**
   * CTX1 (D138, design §4): a right-click on the attended session's page offers its header's acts, the loud act and
   * *Stop…* with its ⋯'s, each carried out by the one owner: *Stop…* asks under the header as its button does.
   */
  it('offers the page header’s acts on a right-click anywhere on the session’s page', async () => {
    SESSIONS = [CHAT];
    show('c0ffee11');
    render(<ContextMenus doors={{ copy: () => {} }} />);

    const title = await screen.findByRole('heading', { level: 1 });
    rightClick(title);
    const acts = await menuActs();
    expect(acts[0]).toBe('Stop…');
    expect(acts).toContain('Copy session ID');
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());

    // Below the header, on the page itself, the same acts.
    rightClick(title.closest('main')!);
    expect(await menuActs()).toEqual(acts);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Stop…' }));
    expect(await screen.findByRole('group', { name: 'stop this session' })).toBeInTheDocument();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', expect.anything());
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
    await userEvent.type(screen.getByLabelText('Message'), 'and push nothing');
    await userEvent.click(await screen.findByRole('button', { name: 'Stop turn' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', { payload: { id: 'c0ffee11' } });
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue('and then test it\n\nand push nothing'));
    expect(notify).toHaveBeenCalledWith(
      'Asked the agent to stop this turn; the conversation stays open. 1 waiting message came back to the box, unsent.');
  });

  /** QUEUEEDIT1b: a waiting message is removed over the driver by its id, and a reword names the session, the id and the text. */
  it('removes and rewords a waiting message over the driver, by its id', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [{ id: 'q1', text: 'and then test it', files: [] }], taking: true };
      if (type === 'SESSION_QUEUE_EDIT') return { done: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    await userEvent.click(await screen.findByRole('button', { name: 'Remove the waiting message: and then test it' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE_EDIT', { payload: { session: 'c0ffee11', id: 'q1' } });

    await userEvent.click(screen.getByRole('button', { name: 'Edit the waiting message: and then test it' }));
    await userEvent.type(screen.getByLabelText('Message'), ' twice');
    await userEvent.keyboard('{Enter}');
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE_EDIT', {
      payload: { session: 'c0ffee11', id: 'q1', text: 'and then test it twice' },
    });
  });

  /**
   * SAIDBACK1b (D76): a parked conversation's composer lists the words waiting on its record as what the same session goes on
   * with, never as waiting for a turn or a door, and offers each one's *Remove* by its id.
   */
  it('lists the words waiting on a parked conversation as what it goes on with', async () => {
    SESSIONS = [{ ...CHAT, state: 'awaiting-person' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [{ id: 'w1', text: 'use the blue one', files: [] }], taking: false };
      if (type === 'SESSION_QUEUE_EDIT') return { done: true };
      return DRIVER_STATE;
    });

    show('c0ffee11');
    // Folded to one line with its count inside the answer box's frame (WAITFOOT2c), a press opens it.
    await userEvent.click(await screen.findByRole('button', { name: 'Show them' }));
    expect(await screen.findByRole('list', { name: 'waiting for the same session to go on with them' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Remove the waiting message: use the blue one' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE_EDIT', { payload: { session: 'c0ffee11', id: 'w1' } });
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
    await screen.findByLabelText('Message');
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
    await userEvent.upload(await screen.findByLabelText('Choose files…'), new File(['exit 3'], 'run.log', { type: 'text/plain' }));
    await userEvent.type(screen.getByLabelText('Message'), 'what does this say?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

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
    await userEvent.type(await screen.findByLabelText('Message'), 'look at ');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_FILES', expect.anything());

    await userEvent.type(screen.getByLabelText('Message'), '@eng');
    await userEvent.click(await screen.findByRole('option', { name: 'src/engine.cs' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_FILES', { payload: { id: 'c0ffee11' } });
    expect(screen.getByLabelText('Message')).toHaveValue('look at @src/engine.cs ');
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
    await userEvent.type(await screen.findByLabelText('Message'), '@eng');

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
    await userEvent.click(await screen.findByRole('button', { name: 'Stop turn' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      '1 waiting message came back to the box, unsent. Not sent with it: plan.md, shot.png — attach them again.'));
    expect(screen.getByLabelText('Message')).toHaveValue('then this');
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
    expect(await screen.findByRole('button', { name: 'Send' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Stop turn' })).not.toBeInTheDocument();

    await waitFor(() => expect(eventHandlers.has('DAORIS.SESSION_QUEUED')).toBe(true));
    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 'c0ffee11', queued: [], taking: true }));
    expect(await screen.findByRole('button', { name: 'Stop turn' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Queue' })).toBeInTheDocument();

    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 'c0ffee11', queued: [], taking: false }));
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Stop turn' })).not.toBeInTheDocument());
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
    const rail = await screen.findByRole('navigation', { name: 'Sessions' });
    await waitFor(() => expect(within(rail).getByText('idle')).toBeInTheDocument());
    // The word is the page header's since SESSUX1d (D126 §3.2): its pill moved up from the record head.
    const head = screen.getByRole('heading', { level: 1 }).closest('header')!;
    expect(within(head).getByText('idle')).toBeInTheDocument();

    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 'c0ffee11', queued: [], taking: true }));
    await waitFor(() => expect(within(rail).getByText('working')).toBeInTheDocument());
    expect(within(head).getByText('working')).toBeInTheDocument();
    // The composer follows the same answer: a turn in flight offers its stop.
    expect(screen.getByRole('button', { name: 'Stop turn' })).toBeInTheDocument();
  });

  /** CONV4b: each conversation keeps its own draft as the person moves between them. */
  it('keeps each conversation its own draft', async () => {
    const OTHER = { ...CHAT, id: 'decaf222', repository: 'game' };
    SESSIONS = [CHAT, OTHER];
    const view = show('c0ffee11');
    await userEvent.type(await screen.findByLabelText('Message'), 'half a thought for the engine');

    view.rerender(
      <QueryClientProvider client={view.client}>
        <Hub />
        <Tooltip.Provider>
          <WorkFrame selected="decaf222" onSelect={view.onSelect} notify={() => {}} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue(''));

    view.rerender(
      <QueryClientProvider client={view.client}>
        <Hub />
        <Tooltip.Provider>
          <WorkFrame selected="c0ffee11" onSelect={view.onSelect} notify={() => {}} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue('half a thought for the engine'));
  });

  /**
   * A driven session holds a tree but was given its whole target at once — there is no channel to
   * speak into, so it gets no composer. An input box nothing is listening to is worse than none.
   */
  it('offers no composer for a driven session', async () => {
    show('s1a2b3c4');

    await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' });
    expect(screen.queryByLabelText('Message')).toBeNull();
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

    const box = await screen.findByLabelText('Message');
    expect(box).toHaveAttribute('placeholder', expect.stringMatching(/tell it something while it works/));
    await userEvent.type(box, 'the budget is in level.json');
    await userEvent.click(screen.getByRole('button', { name: 'Queue' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', { payload: { id: 's1a2b3c4', text: 'the budget is in level.json' } });

    // What the driver holds shows as waiting, and only then can it be sent now.
    expect(screen.queryByRole('button', { name: 'Send now' })).toBeNull();
    queue = { session: 's1a2b3c4', queued: [{ text: 'the budget is in level.json', files: [] }], taking: true, listening: true };
    await act(async () => { eventHandlers.get('DAORIS.SESSION_QUEUED')!(queue); });
    expect(screen.getByText('the budget is in level.json')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Send now' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', { payload: { id: 's1a2b3c4' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Stopping its turn: what you added goes next.'));

    // It stops listening as it ends: the box goes.
    await act(async () => { eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 's1a2b3c4', queued: [], taking: false, listening: false }); });
    expect(screen.queryByLabelText('Message')).toBeNull();
  });

  /**
   * STEER2b (D136): *Send now* beside *Queue* sends the typed words with `now: true`. The answer says what came of it: taken
   * at once in the running turn (nothing to say), the turn stopping so they go next, or that the door could not, the words
   * held as a queue's are, said in one line.
   */
  it('sends typed words to a working driven session now, and says what the door did', async () => {
    let answer: object = { sent: true, reaches: 'now', why: null, now: true };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUEUE') return { session: 's1a2b3c4', queued: [], taking: true, listening: true };
      if (type === 'SESSION_INPUT') return answer;
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'drop the cache');
    await userEvent.click(screen.getByRole('button', { name: 'Send now' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', { payload: { id: 's1a2b3c4', text: 'drop the cache', now: true } });
    expect(box).toHaveValue('');
    expect(notify).not.toHaveBeenCalled();

    answer = { sent: true, reaches: 'turn-end', why: null, now: true };
    await userEvent.type(box, 'then the build');
    await userEvent.click(screen.getByRole('button', { name: 'Send now' }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Stopping its turn: what you added goes next.'));

    answer = { sent: true, reaches: 'turn-end', why: 'no-interrupt', now: false };
    await userEvent.type(box, 'and the tests');
    await userEvent.click(screen.getByRole('button', { name: 'Send now' }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Its door cannot take words in a running turn, so these are held for its turn to end.'));
    // Held, not lost: nothing is put back in the box.
    expect(box).toHaveValue('');
  });

  /**
   * STEER2b (D136): a conversation's *Send now* with words in the box sends them with `now: true`; with the box empty and
   * words waiting it stops the turn and keeps them (`CANCEL_TURN` with `keep`), which comes back with nothing withdrawn.
   * *Stop turn* stays the withdrawal.
   */
  it('sends a conversation’s words now, and with an empty box stops the turn keeping what waits', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [{ id: 'q1', text: 'and then test it', files: [] }], taking: true };
      if (type === 'SESSION_INPUT') return { sent: true, reaches: 'now', why: null, now: true };
      if (type === 'CANCEL_TURN') return { cancelled: true, withdrawn: [] };
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('c0ffee11', notify);

    await screen.findByRole('list', { name: 'waiting for this turn to end' });
    // Empty box, words waiting: the stop that keeps them.
    await userEvent.click(await screen.findByRole('button', { name: 'Send now' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', { payload: { id: 'c0ffee11', keep: true } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Stopping its turn: what you added goes next.'));
    expect(screen.getByLabelText('Message')).toHaveValue('');

    await userEvent.type(screen.getByLabelText('Message'), 'use the blue one');
    await userEvent.click(screen.getByRole('button', { name: 'Send now' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', { payload: { id: 'c0ffee11', text: 'use the blue one', now: true } });
  });

  /**
   * SENDLOST1b: words to a working driven session's running door show as sending; a send the page stopped waiting on keeps
   * them, saying the machine has not answered yet, with no error and nothing back in the box, until the driver holds them.
   */
  it('keeps words to a working driven session whose send timed out, until the driver holds them behind its turn', async () => {
    let stop: () => void = () => {};
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUEUE') return { session: 's1a2b3c4', queued: [], taking: true, listening: true };
      if (type === 'SESSION_INPUT') return new Promise((_, reject) => { stop = () => reject(timedOut()); });
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'the budget is in level.json');
    await userEvent.click(screen.getByRole('button', { name: 'Queue' }));
    expect(await screen.findByRole('list', { name: 'sending…' })).toHaveTextContent('the budget is in level.json');

    await act(async () => { stop(); });
    expect(await screen.findByRole('list', { name: UNANSWERED })).toHaveTextContent('the budget is in level.json');
    expect(box).toHaveValue('');
    expect(notify).not.toHaveBeenCalledWith(expect.anything(), 'error');

    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_QUEUED')!({
        session: 's1a2b3c4', queued: [{ id: 'said-1', text: 'the budget is in level.json', files: [] }], taking: true, listening: true,
      });
    });
    expect(screen.queryByRole('list', { name: UNANSWERED })).toBeNull();
    expect(screen.getByRole('list', { name: 'waiting for this turn to end' })).toHaveTextContent('the budget is in level.json');
    expect(inputs()).toHaveLength(1);
  });

  it('offers no box to a driven session the driver says nothing could hear', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_QUEUE'
      ? { session: 's1a2b3c4', queued: [], taking: false, listening: false }
      : DRIVER_STATE));
    show('s1a2b3c4');

    await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE', { payload: { id: 's1a2b3c4' } }));
    expect(screen.queryByLabelText('Message')).toBeNull();
  });
});

/**
 * MSG1f (D137 §5.1): the box on every session that takes words, by what a word said now would do (`SESSION_QUEUE`'s
 * `reaches` and `why`, MSG1d), and the line saying why on the rest; the words shown at once, where they went, and the one
 * press where they cannot go on; *Send back…* opening the box.
 */
describe('the box on every session that takes words', () => {
  const ENDED = { ...DRIVEN, state: 'completed', note: 'Done; committed as a1b2c3d.' };

  /** A driver that answers `SESSION_QUEUE` with these, `SESSION_INPUT` with `said`, and the rest as the frame needs. */
  const driver = (reach: object, said: object = { sent: true, reaches: 'resume' }, more: Record<string, unknown> = {}) =>
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type in more) return more[type];
      if (type === 'SESSION_QUEUE') return { session: 's1a2b3c4', queued: [], taking: false, ...reach };
      if (type === 'SESSION_INPUT') return said;
      return DRIVER_STATE;
    });

  beforeEach(() => {
    SESSIONS = [ENDED];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    window.localStorage.removeItem('daoris.drafts');
    window.localStorage.removeItem('daoris.dockClosed');
  });

  /** §2.2: a session that ended goes on with the person's words, and the box says so; the log counts where they reach. */
  it('offers a session that ended the box where the same session goes on, and says through it', async () => {
    driver({ reaches: 'resume' });
    show('s1a2b3c4');

    const box = await screen.findByLabelText('Message');
    expect(box).toHaveAttribute('placeholder', 'write to it — the same session goes on with your words');
    expect(screen.queryByRole('button', { name: 'Attach files' })).toBeNull();
    await userEvent.type(box, 'also say so in the release notes');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 's1a2b3c4', text: 'also say so in the release notes' },
    });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.LOG', 'EVENT', {
      payload: { event: 'message.sent', data: { session: 's1a2b3c4', kind: 'say', length: 32, files: 0, reach: 'resume' } },
    }));
    expect((box as HTMLTextAreaElement).value).toBe('');
  });

  /** Nothing known is nothing claimed: a driver that answers no reach (an older shell) offers an ended session no box. */
  it('offers an ended session no box until the driver says words go on', async () => {
    driver({});
    show('s1a2b3c4');

    await screen.findByRole('heading', { level: 1 });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE', { payload: { id: 's1a2b3c4' } }));
    expect(screen.queryByLabelText('Message')).toBeNull();
  });

  /** MSG1d: where nothing takes words the driver says why by a code, and the page draws the line instead of the box. */
  it('draws the line the driver’s code names instead of a box', async () => {
    driver({ why: 'superseded' });
    show('s1a2b3c4');

    expect(await screen.findByText('#abc123 went on in a later session here, so write to that one.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Message')).toBeNull();
  });

  /**
   * HANDWORDS1-ii (D165's HANDWORDS1-i note): a conversation handed over or moved names the one it went on in by that record's
   * title, and *Open session* attends it.
   */
  it('names the conversation a moved one went on in by its title, and attends it', async () => {
    const old = { ...CHAT, state: 'completed' };
    SESSIONS = [old, { ...CHAT, id: 'c2c2c2c2', from: 'c0ffee11', fromWhy: 'curve', fromBy: 'daoris' }];
    driver({}, undefined, { SESSION_QUEUE: { session: 'c0ffee11', queued: [], taking: false, why: 'continued', to: 'c2c2c2c2' } });
    const { onSelect } = show('c0ffee11');

    const line = await screen.findByText('This conversation went on in Chat.');
    expect(screen.queryByText(/say it to that one/)).toBeNull();
    fireEvent.click(within(line.parentElement!).getByRole('button', { name: 'Open session' }));
    expect(onSelect).toHaveBeenCalledWith('c2c2c2c2');
  });

  it('draws the line under a teammate’s record without asking the driver', async () => {
    SESSIONS = [{ ...ENDED, id: 'person@machine-b/s1a2b3c4' }];
    driver({ reaches: 'resume' });
    show('person@machine-b/s1a2b3c4');

    expect(await screen.findByText('This session ran on another machine, where its conversation is.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Message')).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE', expect.anything());
  });

  /** REV3, MSG1d: words nothing took come back into the box, and the line says why in the page's words. */
  it('hands back words nothing took, saying why by the driver’s code', async () => {
    driver({ reaches: 'resume' }, { sent: false, why: 'stood-down' });
    show('s1a2b3c4');

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'one more thing');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText('It stood down: #abc123 is someone else\'s, so it has nothing to go on with.')).toBeInTheDocument();
    await waitFor(() => expect((box as HTMLTextAreaElement).value).toBe('one more thing'));
  });

  /** D137 §2.4: a driven session on the native door takes its words as one argument, so longer ones are refused first. */
  it('refuses words longer than the native door takes, before sending, and keeps them', async () => {
    const PIPE = { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], wire: 'pipe' }] };
    driver({ reaches: 'resume' }, undefined, { HARNESSES: PIPE });
    show('s1a2b3c4');

    const box = await screen.findByLabelText('Message');
    const words = 'x'.repeat(24_001);
    fireEvent.change(box, { target: { value: words } });
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText(/takes at most 24000 characters at once/)).toBeInTheDocument();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());
    expect((box as HTMLTextAreaElement).value).toBe(words);
  });

  /** D137 §2.1: words said as a session winds up wait for its record to end, then reopen it — never refused. */
  it('holds words said as a session winds up above its box', async () => {
    SESSIONS = [DRIVEN];
    driver({ reaches: 'resume' });
    show('s1a2b3c4');

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, 'keep the old flag for a release');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText('held until this run ends — then the same session goes on with them')).toBeInTheDocument();
    expect(screen.getByText('keep the old flag for a release')).toBeInTheDocument();
  });

  /**
   * SENDLOST1b: words to a session that ended, whose send the page stopped waiting on, stay above its box saying the machine
   * has not answered yet, with no error and nothing back in the box, until its record holds them.
   */
  it('keeps words to a session that ended whose send timed out, until its record holds them', async () => {
    const words = 'also say so in the release notes';
    let stop: () => void = () => {};
    let recorded = false;
    let read: () => void = () => {};
    const reading = new Promise<void>((resolve) => { read = resolve; });
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUEUE') return { session: 's1a2b3c4', queued: [], taking: false, reaches: 'resume' };
      if (type === 'SESSION_INPUT') {
        return new Promise((_, reject) => { stop = () => { recorded = true; reject(timedOut()); }; });
      }
      if (type === 'SESSION_HISTORY' && recorded) {
        await reading;
        return {
          session: 's1a2b3c4', earlier: false, latest: 1,
          events: [{ seq: 1, at: new Date().toISOString(), kind: 'user', origin: 'person', text: words }],
        };
      }
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, words);
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    expect(await screen.findByRole('list', { name: 'sending…' })).toHaveTextContent(words);

    await act(async () => { stop(); });
    expect(await screen.findByRole('list', { name: UNANSWERED })).toHaveTextContent(words);
    expect(box).toHaveValue('');
    expect(notify).not.toHaveBeenCalledWith(expect.anything(), 'error');

    await act(async () => { read(); });
    await waitFor(() => expect(screen.queryByRole('list', { name: UNANSWERED })).toBeNull());
    expect(screen.getByLabelText('Message')).toHaveValue('');
    expect(inputs()).toHaveLength(1);
  });

  /**
   * SENDLOST1c: the page stopped waiting on a send, and the driver then says nothing takes words (the box becomes the line):
   * the words are not left *not answered yet*, they are said not to have gone and are kept for the box.
   */
  it('says words whose send timed out did not go when the reach turns to a code, and keeps them', async () => {
    const words = 'also say so in the release notes';
    let stop: () => void = () => {};
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUEUE') return { session: 's1a2b3c4', queued: [], taking: false, reaches: 'resume' };
      if (type === 'SESSION_INPUT') return new Promise((_, reject) => { stop = () => reject(timedOut()); });
      if (type === 'SESSION_HISTORY') return { session: 's1a2b3c4', earlier: false, latest: 0, events: [] };
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, words);
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    await act(async () => { stop(); });
    expect(await screen.findByRole('list', { name: UNANSWERED })).toHaveTextContent(words);

    // The shell tells the session's reach live: nothing takes words for it now.
    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_QUEUED')!({
        session: 's1a2b3c4', queued: [], taking: false, listening: false, reach: { why: 'stood-down' },
      });
    });
    expect(await screen.findByText(/did not go/)).toHaveTextContent(words);
    expect(screen.queryByRole('list', { name: UNANSWERED })).toBeNull();
    expect(inputs()).toHaveLength(1);
  });

  /**
   * SAIDBACK1b (D76): the words waiting on a session that ended are listed above its box by their ids, taken back or reworded
   * over the driver, and the list is read again from the queue the shell tells, where a reword's new id stands for the old.
   */
  it('lists the words waiting on a session that ended, and removes or rewords them over the driver', async () => {
    driver(
      { reaches: 'resume', queued: [{ id: 'w1', text: 'use the blue one', files: [] }, { id: 'w2', text: 'and the red', files: [] }] },
      undefined,
      { SESSION_QUEUE_EDIT: { done: true } },
    );
    show('s1a2b3c4');

    expect(await screen.findByRole('list', { name: 'waiting for the same session to go on with them' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Remove the waiting message: use the blue one' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE_EDIT', { payload: { session: 's1a2b3c4', id: 'w1' } });

    await userEvent.click(screen.getByRole('button', { name: 'Edit the waiting message: and the red' }));
    const box = screen.getByLabelText('Message');
    await userEvent.clear(box);
    await userEvent.type(box, 'use the green one');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE_EDIT', {
      payload: { session: 's1a2b3c4', id: 'w2', text: 'use the green one' },
    });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());

    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({
      session: 's1a2b3c4', queued: [{ id: 'w3', text: 'use the green one', files: [] }], taking: false, reach: { reaches: 'resume' },
    }));
    expect(await screen.findByRole('button', { name: 'Remove the waiting message: use the green one' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remove the waiting message: use the blue one' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Remove the waiting message: and the red' })).toBeNull();
  });

  /** §3.2: a record going on with the person's words shows it, while its run opens with them. */
  it('shows a record going on with the person’s words as going on', async () => {
    SESSIONS = [{ ...DRIVEN, answer: 'also cap it' }];
    driver({});
    show('s1a2b3c4');

    const header = await screen.findByRole('heading', { level: 1 });
    expect(within(header.closest('header')!).getByText('going on')).toBeInTheDocument();
  });

  /** §3.1: the words shown at once, then where they went, the session a door to it. */
  it('says where the words went, and opens that session from the line', async () => {
    driver({ reaches: 'resume' }, undefined, {
      SESSION_HISTORY: {
        session: 's1a2b3c4', earlier: false, latest: 4,
        events: [
          { seq: 1, at: '2026-10-03T10:00:00Z', kind: 'user', origin: 'target', text: 'take quest #abc123' },
          { seq: 2, at: '2026-10-03T10:00:05Z', kind: 'turn', stopReason: 'end_turn' },
          { seq: 3, at: '2026-10-03T11:00:00Z', kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'also cap it' },
          { seq: 4, at: '2026-10-03T11:00:30Z', kind: 'note', text: '— your words went to session `n3wn3w00`, because its tree is gone.', words: ['w1'], to: 'n3wn3w00', why: 'tree' },
        ],
      },
    });
    const { onSelect } = show('s1a2b3c4');

    await userEvent.click(await screen.findByRole('button', { name: 'n3wn3w00' }));
    expect(onSelect).toHaveBeenCalledWith('n3wn3w00');
  });

  /**
   * MSG1f2: a shell that tells the reach live is followed by its events: the box goes as the session's quest goes on in a
   * later session here, said by the event alone, and the queue is asked once.
   */
  it('follows the reach the shell tells live, without asking again', async () => {
    driver({ reaches: 'resume', reach: { reaches: 'resume' } });
    show('s1a2b3c4');

    await screen.findByLabelText('Message');
    await waitFor(() => expect(eventHandlers.has('DAORIS.SESSION_QUEUED')).toBe(true));
    // Asked once by the reach and once by the list of what waits on the record (SAIDBACK1b), each on opening; never on a move.
    const asked = invoke.mock.calls.filter(([, type]) => type === 'SESSION_QUEUE').length;
    act(() => eventHandlers.get('DAORIS.SESSION_QUEUED')!({ session: 's1a2b3c4', queued: [], taking: false, reach: { why: 'superseded' } }));

    expect(await screen.findByText(/went on in a later session here, so write to that one\./)).toBeInTheDocument();
    expect(screen.queryByLabelText('Message')).toBeNull();
    expect(invoke.mock.calls.filter(([, type]) => type === 'SESSION_QUEUE')).toHaveLength(asked);
    expect(asked).toBeLessThanOrEqual(2);
  });

  /** Words a closed quest's session cannot go on with: two said, and the driver's line saying why. */
  const CANNOT = {
    session: 's1a2b3c4', earlier: false, latest: 5,
    events: [
      { seq: 1, at: '2026-10-03T10:00:00Z', kind: 'user', origin: 'target', text: 'take quest #abc123' },
      { seq: 2, at: '2026-10-03T10:00:05Z', kind: 'turn', stopReason: 'end_turn' },
      { seq: 3, at: '2026-10-03T11:00:00Z', kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'first' },
      { seq: 4, at: '2026-10-03T11:00:01Z', kind: 'user', origin: 'person', id: 'w2', reaches: 'resume', text: 'second' },
      { seq: 5, at: '2026-10-03T11:00:30Z', kind: 'note', text: '— It cannot go on in this session, because its conversation could not be resumed.', words: ['w1', 'w2'], why: 'refused' },
    ],
  };

  /**
   * §2.2, MSG1f2: words a closed quest's session cannot go on with offer one press, the driver's one act: a new conversation
   * in the same repository whose first message is the words, taken off this session, attended once it opens. The page sends
   * the session alone; the driver reads the words off its record, and nothing else is asked.
   */
  it('starts a conversation with the words that cannot go on, and attends it', async () => {
    driver({ reaches: 'resume' }, { sent: true }, {
      SESSION_START_FROM: { sessionId: 'c4a7c4a7', sent: true, message: 'started' },
      SESSION_HISTORY: CANNOT,
    });
    const { onSelect } = show('s1a2b3c4');

    expect(await screen.findByText('It cannot go on in this session, because its conversation could not be continued.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Start a conversation with these words' }));

    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('c4a7c4a7'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_START_FROM', { payload: { id: 's1a2b3c4' } });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'START_CHAT', expect.anything());
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());
  });

  /** MSG1f2: a press the driver refuses is said by its code in the page's words, and attends nothing. */
  it('says a refused press by its code', async () => {
    driver({ reaches: 'resume' }, { sent: true }, {
      SESSION_START_FROM: { sent: false, why: 'no-words', message: 'no words wait on s1a2b3c4 to start a conversation with.' },
      SESSION_HISTORY: CANNOT,
    });
    const notify = vi.fn();
    const { onSelect } = show('s1a2b3c4', notify);

    await userEvent.click(await screen.findByRole('button', { name: 'Start a conversation with these words' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'No words wait on this session now, so there is nothing to start a conversation with.', 'error'));
    expect(onSelect).not.toHaveBeenCalledWith(expect.stringMatching(/^c/));
  });

  /**
   * MSG1g2 (§2.2, MSG1g's note): words a resume holds while the account the record ran on cools say so with its reset,
   * read from the accounts' facts, and *Go on in a new session* asks the driver; a closed quest's answer points at a
   * conversation with the words, which MSG1f's press starts, as the driver's one act (MSG1f2).
   */
  it('says words wait for a cooling account, asks the driver for a new session, and words its answer', async () => {
    SESSIONS = [{ ...ENDED, profile: 'personal' }];
    const until = new Date(Date.now() + 3 * 3_600_000).toISOString();
    const cooling = { until, stated: true, window: 'session', seen: until, assumedZone: false, notBelieved: false };
    driver({ reaches: 'resume' }, { sent: true }, {
      ACCOUNTS: { agents: [{ agent: 'claude-code', speaks: true, own: {}, scopes: [], accounts: [{ name: 'personal', cooling }] }] },
      SESSION_GO_ON_NEW: { sent: false, why: 'closed', message: '#abc123 has closed, so nothing carries its session’s words on by itself' },
      SESSION_START_FROM: { sessionId: 'c4a7c4a7', sent: true, message: 'started' },
      SESSION_HISTORY: {
        session: 's1a2b3c4', earlier: false, latest: 3,
        events: [
          { seq: 1, at: '2026-10-03T10:00:00Z', kind: 'user', origin: 'target', text: 'take quest #abc123' },
          { seq: 2, at: '2026-10-03T10:00:05Z', kind: 'turn', stopReason: 'end_turn' },
          { seq: 3, at: '2026-10-03T11:00:00Z', kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'also cap it' },
        ],
      },
    });
    const { onSelect } = show('s1a2b3c4');

    expect(await screen.findByText(/^Held: its account is cooling until .+; it goes on with this then\.$/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Go on in a new session' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GO_ON_NEW', { payload: { id: 's1a2b3c4' } });
    expect(await screen.findByText('#abc123 has closed, so nothing carries its words on by itself; start a conversation with them instead.'))
      .toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Start a conversation with these words' }));
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('c4a7c4a7'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_START_FROM', { payload: { id: 's1a2b3c4' } });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'START_CHAT', expect.anything());
  });

  /** MSG1g2: an account that is ready, or a shell that answers no accounts, leaves the held line as it was, and no press. */
  it('offers no new session where the record’s account is not cooling', async () => {
    SESSIONS = [{ ...ENDED, profile: 'personal' }];
    driver({ reaches: 'resume' }, undefined, {
      ACCOUNTS: { agents: [{ agent: 'claude-code', speaks: true, own: {}, scopes: [], accounts: [{ name: 'personal' }] }] },
      SESSION_HISTORY: {
        session: 's1a2b3c4', earlier: false, latest: 1,
        events: [{ seq: 1, at: '2026-10-03T11:00:00Z', kind: 'user', origin: 'person', id: 'w1', reaches: 'resume', text: 'also cap it' }],
      },
    });
    show('s1a2b3c4');

    expect(await screen.findByText("Held: the same session goes on with this at the driver's next look.")).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Go on in a new session' })).toBeNull();
  });

  /**
   * §5.1: *Send back…* in a review opens the box on that session, with the focus in it, instead of the quest composer —
   * what the glossary's *send back* means. Where the session takes no words, the quest composer's door stays.
   */
  it('opens the box from Send back… in a review, and the quest composer only where nothing takes words', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    const DIFF = { session: 's1a2b3c4', base: 'abc1234567890', truncated: null, files: [{ path: 'src/chunk.ts', status: 'modified', added: 1, removed: 1, patch: '@@ -1 +1 @@\n-a\n+b' }] };
    driver({ reaches: 'resume' }, undefined, { SESSION_DIFF: DIFF });
    const onSendBack = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <Tooltip.Provider>
          <WorkFrame selected="s1a2b3c4" onSelect={vi.fn()} notify={() => {}} onSendBack={onSendBack} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    const box = await screen.findByLabelText('Message');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Send back…' }));
    await waitFor(() => expect(box).toHaveFocus());
    expect(onSendBack).not.toHaveBeenCalled();
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
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_INPUT'
      ? { sent: true, reaches: 'resume' }
      : DRIVER_STATE));
    show('p4rk3d00');

    const box = await screen.findByLabelText('Message');
    expect(screen.queryByRole('button', { name: 'Answer and carry on…' })).toBeNull();
    expect(screen.getByText(/Answering it in the box below lets it carry on/)).toBeInTheDocument();
    // An answer is words: nothing to attach, and no ending of the box's own (the card holds those, and
    // since NAME1b the card's Finish and the box's are one name for one act, so the box is asked).
    expect(screen.queryByRole('button', { name: 'Attach files' })).toBeNull();
    expect(within(box.closest('form')!).queryByRole('button', { name: 'Finish' })).toBeNull();

    await userEvent.type(box, 'go ahead with the PUT');
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));

    // Through the one door every box says through (MSG1f): the driver keeps it on the record, shows it at once in the
    // conversation and nudges its loop (MSG1d), where the service's answer door did none of the last two.
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 'p4rk3d00', text: 'go ahead with the PUT' },
    }));
    expect(vi.mocked(fetch)).not.toHaveBeenCalledWith('/api/sessions/p4rk3d00/answer', expect.anything());
  });

  /**
   * SENDLOST1b: a park's answer whose send the page stopped waiting on stays above its box, saying the machine has not
   * answered yet, never said answered and never back in the box, until the words waiting on its record list it.
   */
  it('keeps a park\'s answer whose send timed out, until the words waiting on its record list it', async () => {
    const words = 'go ahead with the PUT';
    let stop: () => void = () => {};
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUEUE') return { session: 'p4rk3d00', queued: [], taking: false, reaches: 'resume' };
      if (type === 'SESSION_INPUT') return new Promise((_, reject) => { stop = () => reject(timedOut()); });
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('p4rk3d00', notify);

    const box = await screen.findByLabelText('Message');
    await userEvent.type(box, words);
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));
    expect(await screen.findByRole('list', { name: 'sending…' })).toHaveTextContent(words);

    await act(async () => { stop(); });
    expect(await screen.findByRole('list', { name: UNANSWERED })).toHaveTextContent(words);
    expect(box).toHaveValue('');
    expect(notify).not.toHaveBeenCalled();

    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_QUEUED')!({
        session: 'p4rk3d00', queued: [{ id: 'said-1', text: words, files: [] }], taking: false, reaches: 'resume',
      });
    });
    expect(screen.queryByRole('list', { name: UNANSWERED })).toBeNull();
    // Folded to a line with its count (WAITFOOT2c); a press lists the words.
    await userEvent.click(screen.getByRole('button', { name: 'Show them' }));
    expect(screen.getByRole('list', { name: 'waiting for the same session to go on with them' })).toHaveTextContent(words);
    expect(inputs()).toHaveLength(1);
  });

  /**
   * ANSWER1c (D131): the answer keeps the park (ANSWER1b), and the same session goes on at the driver's next look. Until
   * then the record is still parked, with the answer set, and the frame shows it going on: the answer and when, and none
   * of the card's moves. The toast says the same. MSG1f (D137 §2.4): a second word joins the first, so the box stays,
   * saying the same session goes on with it.
   */
  it('shows an answered park as going on at the driver\'s next look, its box taking a second word and no moves', async () => {
    const notify = vi.fn();
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') {
        SESSIONS = [{ ...PARKED, answer: 'go ahead with the PUT', note: `${PARKED.note}\n\nAnswered: go ahead with the PUT` }];
        return { sent: true, reaches: 'resume' };
      }
      if (type === 'SESSION_QUEUE') return { session: 'p4rk3d00', queued: [], taking: false, reaches: 'resume' };
      return DRIVER_STATE;
    });
    show('p4rk3d00', notify);

    await userEvent.type(await screen.findByLabelText('Message'), 'go ahead with the PUT');
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));

    expect(await screen.findByRole('heading', { name: 'Your answer' })).toBeInTheDocument();
    expect(screen.getByText("The same session goes on with this answer at the driver's next look.")).toBeInTheDocument();
    expect(notify).toHaveBeenCalledWith("Answered — the same session goes on with your words at the driver's next look.");
    expect(await screen.findByLabelText('Message')).toHaveAttribute('placeholder', 'write to it — the same session goes on with your words');
    expect(screen.queryByText('This one is waiting on you')).toBeNull();
    for (const name of ['Finish', 'Decline…', 'Carry on with this answer']) expect(screen.queryByRole('button', { name })).toBeNull();
    // Its stop stays the page header's, saying what a driven session's stop says, never that it stops unanswered.
    await userEvent.click(within(screen.getByRole('group', { name: 'Session actions' })).getByRole('button', { name: 'Stop…' }));
    const ask = screen.getByRole('group', { name: 'stop this session' });
    expect(ask).toHaveTextContent('Stops the session now.');
    expect(ask).not.toHaveTextContent('unanswered');
  });

  /** The card keeps finish and decline; the stop is the page header's, which asks once (SESSUX1d, D126 §3.3). */
  it('shows the analysis and its moves on the attended session, its stop in the page header', async () => {
    show('p4rk3d00');

    expect(await screen.findByText(/I recommend the second/)).toBeInTheDocument();
    // Its quest is still open, so its finish asks what becomes of it first (QUESTCLOSE1).
    expect(screen.getByRole('button', { name: 'Finish…' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Decline…' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Stop session' })).toBeNull();
    expect(within(screen.getByRole('group', { name: 'Session actions' })).getByRole('button', { name: 'Stop…' })).toBeInTheDocument();
  });

  /** §3.3: a session waiting on you, with no process left, stops unanswered through its resolve, as its card's stop did. */
  it('stops a parked session from its page header through its resolve, saying it stops unanswered', async () => {
    show('p4rk3d00');
    await userEvent.click(await screen.findByRole('button', { name: 'Stop…' }));

    const ask = screen.getByRole('group', { name: 'stop this session' });
    expect(ask).toHaveTextContent('Stops it unanswered. Its quest stays taken here until you choose Try again.');
    await userEvent.click(within(ask).getByRole('button', { name: 'Stop session' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', { payload: { id: 'p4rk3d00', state: 'stopped' } });
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

    // The band's Finish and the composer's are one name for one act (NAME1b), so the box is asked.
    const box = () => screen.getByLabelText('Message').closest('form')!;
    await screen.findByRole('button', { name: 'Finish' });
    expect(screen.getByRole('button', { name: 'Send' })).toBeInTheDocument();
    // The band's verbs, and only the band's.
    expect(screen.getAllByRole('button', { name: 'Finish' })).toHaveLength(1);
    expect(within(box()).queryByRole('button', { name: 'Finish' })).toBeNull();
    expect(within(box()).queryByRole('button', { name: 'Stop' })).toBeNull();

    // Working again: no band, and the composer owns Finish; the stop is the page header's at every state (D126 §3.3).
    SESSIONS = [{ ...PARKED, kind: 'chat', state: 'working' }];
    cleanup();
    show('p4rk3d00');

    await within(await screen.findByLabelText('Message').then((field) => field.closest('form')!))
      .findByRole('button', { name: 'Finish' });
    expect(within(box()).queryByRole('button', { name: 'Stop' })).toBeNull();
    expect(screen.getAllByRole('button', { name: 'Finish' })).toHaveLength(1);
    expect(screen.getAllByRole('button', { name: 'Stop…' })).toHaveLength(1);
  });

  it('finishes it over the driver, with no note the person did not write, and leaves its quest as it is', async () => {
    const fetched = vi.fn(async (input: RequestInfo | URL) => respond(String(input)));
    vi.stubGlobal('fetch', fetched);
    show('p4rk3d00');
    // Its quest is still open, so the finish asks what becomes of it first (QUESTCLOSE1), leaving it as it is by default.
    await userEvent.click(await screen.findByRole('button', { name: 'Finish…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Finish' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', {
      payload: { id: 'p4rk3d00', state: 'completed' },
    });
    expect(fetched.mock.calls.some(([url]) => String(url).endsWith('/done'))).toBe(false);
  });

  /**
   * QUESTCLOSE1 (D126's note): on the install a finish at a checkpoint left its quest taken with nothing to close it. Marked
   * done in the same act, the finish goes over the driver first, then the person's done goes to its quest's own door with
   * their words, and both are said.
   */
  it("finishes it over the driver, then marks its quest done as the person's with their note", async () => {
    const posted: { url: string; body: unknown }[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (init?.method === 'POST' && url === '/api/quests/abc123/done') {
        posted.push({ url, body: JSON.parse(String(init.body)) });
        return Response.json({ quest: { ...QUESTS[0], status: 'Done' }, message: 'Quest `#abc123` is now Done: you marked it done.' });
      }
      return respond(url);
    }));
    const notify = vi.fn();
    show('p4rk3d00', notify);

    await userEvent.click(await screen.findByRole('button', { name: 'Finish…' }));
    await userEvent.click(screen.getByRole('radio', { name: 'Mark it done as yours' }));
    await userEvent.type(screen.getByLabelText(/your note on the quest/), 'The write-up is in the shared folder.');
    await userEvent.click(screen.getByRole('button', { name: 'Finish' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', {
      payload: { id: 'p4rk3d00', state: 'completed' },
    });
    await waitFor(() => expect(posted).toEqual([{ url: '/api/quests/abc123/done', body: { note: 'The write-up is in the shared folder.' } }]));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Quest `#abc123` is now Done: you marked it done.'));
  });

  /** The finish stood; a refusal of the quest's done is the service's sentence, said as an error. */
  it("says the service's refusal of its quest's done, the finish having stood", async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (init?.method === 'POST' && url === '/api/quests/abc123/done') {
        return Response.json({ error: 'Quest `#abc123` is Done — a closed quest does not move; a new ask is a new title.' }, { status: 409 });
      }
      return respond(url);
    }));
    const notify = vi.fn();
    show('p4rk3d00', notify);

    await userEvent.click(await screen.findByRole('button', { name: 'Finish…' }));
    await userEvent.click(screen.getByRole('radio', { name: 'Mark it done as yours' }));
    await userEvent.click(screen.getByRole('button', { name: 'Finish' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('a closed quest does not move'), 'error'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', { payload: { id: 'p4rk3d00', state: 'completed' } });
  });

  it('carries the reason a decline was given', async () => {
    show('p4rk3d00');
    await userEvent.click(await screen.findByRole('button', { name: 'Decline…' }));
    await userEvent.type(screen.getByLabelText(/the reason/), 'the chunk API is being replaced');
    await userEvent.click(screen.getByRole('button', { name: 'Decline with this reason' }));

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
    await userEvent.click(await screen.findByRole('button', { name: 'Finish…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Finish' }));

    // Said inside the ask that was pressed, and in no toast (UXFIX2b2b); the ask stays, to be pressed again.
    const ask = screen.getByRole('group', { name: 'finish this session' });
    expect(await within(ask).findByRole('alert')).toHaveTextContent('Declining needs a reason');
    expect(notify).not.toHaveBeenCalled();
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
    expect(screen.queryByRole('button', { name: 'Finish' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Stop session' })).toBeNull();
    expect(screen.queryByLabelText('Message')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
    // Its page header offers only what reaches no process: its ⋯, with its id (SESSUX1d, D126 §3.2).
    const acts = within(screen.getByRole('group', { name: 'Session actions' }));
    expect(acts.getAllByRole('button').map((button) => button.getAttribute('aria-label'))).toEqual(['More actions']);
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

    // The head's card, in the main area; the strip at the foot offers the same door (WAITFOOT1b, D168).
    await userEvent.click(await within(screen.getByRole('main')).findByRole('button', { name: 'Answer ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledWith('0fda18');
    await userEvent.click(within(screen.getByRole('region', { name: 'Waiting on you' })).getByRole('button', { name: 'Answer ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledTimes(2);
    expect(screen.queryByRole('button', { name: 'Finish' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
    expect(screen.queryByLabelText('Message')).toBeNull();
  });

  /**
   * Stopping stays: the person may end the intake and settle the ask later, as a proposal. Its stop is the page
   * header's since SESSUX1d, and asks with the sentence its card said (D126 §3.3).
   */
  it('stops a parked intake over the driver, and nothing more', async () => {
    SESSIONS = [PARKED_INTAKE];
    show('i9n8t7k6');
    await userEvent.click(await screen.findByRole('button', { name: 'Stop…' }));
    expect(screen.getByRole('group', { name: 'stop this session' })).toHaveTextContent(/the ask stays a proposal/);
    await userEvent.click(screen.getByRole('button', { name: 'Stop session' }));

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
    expect(screen.queryByLabelText('Message')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Finish' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Open ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledWith('0fda18');
  });

  /** The stop the composer used to carry, then its card: the process is cut off, over the driver, from the page header. */
  it('stops a running intake over the driver', async () => {
    SESSIONS = [RUNNING_INTAKE];
    show('r7n8t7k6');
    await userEvent.click(await screen.findByRole('button', { name: 'Stop…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Stop session' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', { payload: { id: 'r7n8t7k6' } });
  });

  /** A conversation keeps its composer: it is the one kind of session that takes turns. */
  it('keeps the composer for a conversation', async () => {
    SESSIONS = [{ ...RUNNING_INTAKE, id: 'c0nv0000', ask: undefined, repository: 'engine' }];
    show('c0nv0000');

    expect(await screen.findByLabelText('Message')).toBeInTheDocument();
    expect(screen.queryByText(/takes no messages/)).toBeNull();
  });

  /** A driven session that is not parked gets no moves: there is nothing waiting on anybody. */
  it('offers no moves on a session nobody is waiting for', async () => {
    SESSIONS = [DRIVEN];
    show('s1a2b3c4');

    await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' });
    expect(screen.queryByRole('button', { name: 'Finish' })).toBeNull();
  });
});

/**
 * The session's page header, and every act where its session is (SESSUX1d, D126 §3): a running driven session is
 * stopped where it is read, asking once; a parked quest is tried again from its session; a row's acts attend their
 * session and do their part there.
 */
describe('a session’s page header and its acts', () => {
  beforeEach(() => {
    SESSIONS = [DRIVEN];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    window.localStorage.removeItem('daoris.drafts');
  });

  /** The frame with the application's selection held in state, as `App` holds it, so a row's act can attend another. */
  function Attending({ initial, terminal = false }: { initial: string; terminal?: boolean }) {
    const [selected, setSelected] = useState<string | null>(initial);
    return <WorkFrame selected={selected} onSelect={setSelected} notify={() => {}} terminal={terminal} />;
  }
  const attending = (initial: string, terminal = false) => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return render(<QueryClientProvider client={client}><Tooltip.Provider><Attending initial={initial} terminal={terminal} /></Tooltip.Provider></QueryClientProvider>);
  };

  /**
   * M1, the audit's first finding: a running driven session had no stop in Sessions, and its one stop was on its quest's
   * page. Its page header's *Stop…* asks once, saying what follows for a session that has not taken its quest yet, and
   * stops nothing until its second press.
   */
  it('stops a running driven session from its page header, asking once and saying what follows', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'STOP_SESSION' ? { stopped: true } : DRIVER_STATE));
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    await userEvent.click(acts.getByRole('button', { name: 'Stop…' }));
    const ask = screen.getByRole('group', { name: 'stop this session' });
    expect(ask).toHaveTextContent(
      'Stops the session now. The driver will not start #abc123 again on this machine until you choose Try again; another machine may still take it.');
    await userEvent.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(screen.queryByRole('group', { name: 'stop this session' })).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', expect.anything());

    await userEvent.click(acts.getByRole('button', { name: 'Stop…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Stop session' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', { payload: { id: 's1a2b3c4' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('session s1a2b3c4 is being stopped — the record will say the person ended it.'));
    // UXFIX2: it closes once the stop lands, never on the press.
    await waitFor(() => expect(screen.queryByRole('group', { name: 'stop this session' })).toBeNull());
  });

  /**
   * UXFIX2 (the second-opinion review): the stop's ask takes the focus to what follows, gives it back to *Stop…* when put
   * down, and a refused stop is said inside it, word for word, where it was pressed, rather than in a toast; it stays open.
   */
  it('gives the focus back to Stop… when put down, and says a refused stop inside its ask', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type !== 'STOP_SESSION') return DRIVER_STATE;
      throw Object.assign(new Error('fallback'), { code: 'DRIVER_REFUSED', parameters: { message: 'Session `s1a2b3c4` already ended.' } });
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    await user.click(acts.getByRole('button', { name: 'Stop…' }));
    const ask = screen.getByRole('group', { name: 'stop this session' });
    await waitFor(() => expect(within(ask).getByText(/^Stops the session now/)).toHaveFocus());
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('group', { name: 'stop this session' })).toBeNull();
    expect(acts.getByRole('button', { name: 'Stop…' })).toHaveFocus();

    await user.click(acts.getByRole('button', { name: 'Stop…' }));
    await user.click(screen.getByRole('button', { name: 'Stop session' }));
    const refused = await within(screen.getByRole('group', { name: 'stop this session' })).findByRole('alert');
    expect(refused).toHaveTextContent('Session s1a2b3c4 already ended.');
    expect(notify).not.toHaveBeenCalledWith(expect.stringContaining('already ended'), 'error');
    expect(screen.getByRole('button', { name: 'Stop session' })).toBeEnabled();
  });

  /**
   * MIXAGENT1b-i (D163): *Go on with another agent…* folds into the head's ⋯ and asks under the head: the agent chip lists the
   * agents but the conversation's own; *Hand over* asks `SESSION_HAND_OVER`, and the page attends the conversation it opened.
   */
  const TWO_AGENTS = {
    ...ROSTER,
    harnesses: [
      { harness: 'stub', present: true, version: 'stub 1.0.0', problem: null, machineDefault: null, profiles: [], structured: true },
      { harness: 'codex', present: true, version: 'codex 1.0.0', problem: null, machineDefault: null, profiles: [{ name: 'team', home: 'x', login: 'in' }] },
    ],
  };

  const openHandOver = async (name: string) => {
    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    expect(acts.queryByRole('button', { name: 'Stop and hand over…' })).toBeNull();
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name }));
    return { user, ask: await screen.findByRole('group', { name: 'go on with another agent' }) };
  };

  it('hands an idle conversation over from the head: the ask lists the other agents, and the page attends the new one', async () => {
    SESSIONS = [{ ...CHAT, state: 'completed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return TWO_AGENTS;
      if (type === 'SESSION_HAND_OVER') return { sessionId: 'n3wn3w00', kept: true, why: null, message: 'goes on.' };
      return DRIVER_STATE;
    });
    const { onSelect } = show('c0ffee11');

    const { user, ask } = await openHandOver('Go on with another agent…');
    expect(within(ask).getByRole('button', { name: /Agent and account: codex · / })).toBeInTheDocument();
    await user.click(within(ask).getByRole('button', { name: 'Hand over' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', { payload: { id: 'c0ffee11', agent: 'codex', why: 'press' } });
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('n3wn3w00'));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', expect.anything());
  });

  it('stops and hands over a conversation that works: the hand-over is asked first, then its turn is stopped', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return TWO_AGENTS;
      if (type === 'SESSION_HAND_OVER') return { sessionId: null, kept: true, why: null, message: 'kept.' };
      if (type === 'CANCEL_TURN') return { cancelled: true, withdrawn: [] };
      return DRIVER_STATE;
    });
    show('c0ffee11');

    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    expect(acts.getByRole('button', { name: 'Stop…' })).toBeInTheDocument();
    const user = userEvent.setup();
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Stop and hand over…' }));
    const ask = await screen.findByRole('group', { name: 'go on with another agent' });
    await user.click(within(ask).getByRole('button', { name: 'Hand over' }));

    await waitFor(() => expect(within(screen.getByRole('group', { name: 'go on with another agent' }))
      .getByText('It goes on with codex when its turn ends, in a conversation of its own.')).toBeInTheDocument());
    expect(invoke.mock.calls.map((call) => call[1]).filter((type) => type === 'SESSION_HAND_OVER' || type === 'CANCEL_TURN'))
      .toEqual(['SESSION_HAND_OVER', 'CANCEL_TURN']);
  });

  it('says a refused hand-over in the ask and keeps the choice', async () => {
    SESSIONS = [{ ...CHAT, state: 'completed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return TWO_AGENTS;
      if (type === 'SESSION_HAND_OVER') return { sessionId: null, kept: false, why: 'tree-gone', message: 'x' };
      return DRIVER_STATE;
    });
    show('c0ffee11');

    const { user, ask } = await openHandOver('Go on with another agent…');
    await user.click(within(ask).getByRole('button', { name: 'Hand over' }));

    expect(await within(ask).findByRole('alert')).toHaveTextContent('The folder it worked in is gone, so there is nothing to go on in.');
    expect(within(ask).getByRole('button', { name: 'Hand over' })).toBeEnabled();
    expect(within(ask).getByRole('button', { name: /Agent and account: codex · / })).toBeInTheDocument();
  });

  /**
   * MIXAGENT2b (D163 points 2 and 3; the mixed-agents design §3, §5): the session's asks of helpers stand in its head under
   * *Helpers*, an agent's that waits on the person with *Let it ask*, which sends `SESSION_HELPER_APPROVE`; *Ask another agent…*
   * folds into the head's ⋯ and asks under the head, its *Ask* sending `SESSION_HELPER_ASK` with the question, the agent left to
   * Daoris, and the ask closes once it is on file.
   */
  it('lists a session’s asks of helpers with the person’s yes, and asks another agent from the head', async () => {
    SESSIONS = [{ ...CHAT, state: 'completed' }];
    const helpers = [{
      id: 'a1', question: 'Is the cap right?', agent: null, askedBy: 'agent', at: '2026-10-16T09:00:00Z', state: 'waiting', code: null,
      helper: null, startedOn: null,
    }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return TWO_AGENTS;
      if (type === 'SESSION_HELPERS') return { helpers };
      if (type === 'SESSION_HELPER_APPROVE') return { approved: true, why: null, message: 'yes' };
      if (type === 'SESSION_HELPER_ASK') return { asked: 'a2', why: null, message: 'asked' };
      return DRIVER_STATE;
    });
    show('c0ffee11');

    const asks = await screen.findByRole('region', { name: 'Helpers' });
    expect(within(asks).getByText('The agent asked · waits for your yes')).toBeInTheDocument();
    const user = userEvent.setup();
    await user.click(within(asks).getByRole('button', { name: 'Let it ask' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HELPER_APPROVE', { payload: { session: 'c0ffee11', ask: 'a1' } });

    const acts = within(screen.getByRole('group', { name: 'Session actions' }));
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Ask another agent…' }));
    const ask = await screen.findByRole('group', { name: 'ask another agent' });
    await user.type(within(ask).getByRole('textbox', { name: 'Your question' }), 'Which test covers the loader?');
    await user.click(within(ask).getByRole('button', { name: 'Ask' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HELPER_ASK', { payload: { id: 'c0ffee11', question: 'Which test covers the loader?' } });
    await waitFor(() => expect(screen.queryByRole('group', { name: 'ask another agent' })).toBeNull());
  });

  /**
   * MIXAGENT1b-ii (D163 point 3): when the conversation's own account is near its limit by its scope's near, a line under the head
   * offers *Go on with codex (team)* and *Choose…*; the press asks `SESSION_HAND_OVER` with the reason `near` as Daoris's, *Choose…*
   * opens the ask with that choice made, and *Not now* puts it down for the run. Nothing is taken without the press.
   */
  const RUNNING_LOW = {
    ...ROSTER,
    harnesses: [
      { ...ROSTER.harnesses[0], profiles: [{ name: 'personal', home: 'x', login: 'in' }] },
      { harness: 'codex', present: true, version: 'codex 1.0.0', problem: null, machineDefault: null, profiles: [{ name: 'team', home: 'x', login: 'in' }] },
    ],
  };
  const scopeOf = (over: Record<string, unknown>) => ({
    workspace: null, default: null, list: [], begins: null, unknown: [], problem: null, near: [],
    use: { use: 'goal', keep: null, early: true, near: 90 }, ...over,
  });
  const LOW_ACCOUNTS = (cooling: unknown = null) => ({
    agents: [
      {
        agent: 'claude-code', speaks: true, own: {}, accounts: [{ name: 'personal', ...(cooling ? { cooling } : {}) }],
        scopes: [scopeOf({ near: [{ account: 'personal', window: 'session', by: 'number' }] })],
      },
      { agent: 'codex', speaks: true, own: {}, accounts: [{ name: 'team' }], scopes: [scopeOf({ next: { account: 'team', reason: 'onlyReady', others: [] } })] },
    ],
  });
  const LOW_CHAT = { ...CHAT, adapter: 'claude-code', profile: 'personal', state: 'completed' };
  const lowRespond = (handOver: unknown = { sessionId: 'n3wn3w00', kept: true, why: null, message: 'goes on.' }, accounts = LOW_ACCOUNTS()) =>
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return RUNNING_LOW;
      if (type === 'ACCOUNTS') return accounts;
      if (type === 'SESSION_HAND_OVER') return handOver;
      return DRIVER_STATE;
    });

  it('offers a conversation whose account is near its limit to go on with another agent, taken only by its press', async () => {
    SESSIONS = [LOW_CHAT];
    lowRespond();
    const { onSelect } = show('c0ffee11');

    const line = await screen.findByRole('group', { name: 'hand-over offer' });
    expect(line).toHaveTextContent('This conversation’s account is near its limit.');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', expect.anything());
    await userEvent.setup().click(within(line).getByRole('button', { name: 'Go on with codex (team)' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', {
      payload: { id: 'c0ffee11', agent: 'codex', account: 'team', why: 'near', askedBy: 'daoris' },
    });
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('n3wn3w00'));
  });

  it('says a cut account’s offer with when it is offered again, and sends the reason limit', async () => {
    SESSIONS = [LOW_CHAT];
    const until = new Date(Date.now() + 3 * 3600_000).toISOString();
    lowRespond(undefined, LOW_ACCOUNTS({ until, stated: true, window: 'session', seen: until, assumedZone: false, notBelieved: false }));
    show('c0ffee11');

    const line = await screen.findByRole('group', { name: 'hand-over offer' });
    expect(line).toHaveTextContent(/This conversation’s account is cut until /);
    await userEvent.setup().click(within(line).getByRole('button', { name: 'Go on with codex (team)' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', expect.objectContaining({
      payload: expect.objectContaining({ why: 'limit', askedBy: 'daoris' }),
    }));
  });

  it('opens the hand-over’s ask with that choice made from *Choose…*, and puts the offer down for the run with *Not now*', async () => {
    SESSIONS = [LOW_CHAT];
    lowRespond();
    show('c0ffee11');

    const user = userEvent.setup();
    const line = await screen.findByRole('group', { name: 'hand-over offer' });
    await user.click(within(line).getByRole('button', { name: 'Choose…' }));
    const ask = await screen.findByRole('group', { name: 'go on with another agent' });
    expect(within(ask).getByRole('button', { name: /Agent and account: codex · / })).toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'hand-over offer' })).toBeNull();
    await user.click(within(ask).getByRole('button', { name: 'Hand over' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', {
      payload: { id: 'c0ffee11', agent: 'codex', account: 'team', why: 'near', askedBy: 'daoris' },
    });
  });

  it('puts the offer down for the run with Not now, and offers nothing while the account says nothing is wrong', async () => {
    SESSIONS = [LOW_CHAT];
    lowRespond();
    const view = show('c0ffee11');

    const user = userEvent.setup();
    await user.click(within(await screen.findByRole('group', { name: 'hand-over offer' })).getByRole('button', { name: 'Not now' }));
    expect(screen.queryByRole('group', { name: 'hand-over offer' })).toBeNull();
    view.unmount();

    lowRespond(undefined, { agents: [{ ...LOW_ACCOUNTS().agents[0], scopes: [scopeOf({})] }, LOW_ACCOUNTS().agents[1]] });
    show('c0ffee11');
    await screen.findByRole('group', { name: 'Session actions' });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ACCOUNTS', expect.anything()));
    expect(screen.queryByRole('group', { name: 'hand-over offer' })).toBeNull();
  });

  /**
   * SESSUX1f (D126 §5.4): an ended conversation the reader says is deletable offers *Delete…* in its header's ⋯; it asks
   * once under the header, deletes nothing until its second press, and a driven session's ⋯ offers none.
   */
  it('deletes an ended conversation from its header’s ⋯, asking once, and offers a driven session none', async () => {
    const ended = { ...CHAT, state: 'completed' };
    SESSIONS = [ended, { ...DRIVEN, state: 'completed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [
          { session: 'c0ffee11', group: 'ended', shown: 'completed', archived: false, teammate: false, deletable: true },
          { session: 's1a2b3c4', group: 'ended', shown: 'completed', archived: false, teammate: false, deletable: false },
        ] };
      }
      if (type === 'SESSION_DELETE') return { deleted: 'c0ffee11', removed: ['record'] };
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('c0ffee11', notify);

    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GROUPS', {}));
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Delete…' }));

    const ask = screen.getByRole('group', { name: 'delete this session' });
    expect(ask).toHaveTextContent('Nothing brings it back.');
    // UXFIX2: opened from the ⋯, its sentence takes the focus, and *Never mind* gives it back to the ⋯.
    await waitFor(() => expect(within(ask).getByText(/^Deletes this conversation’s record/)).toHaveFocus());
    await user.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(screen.queryByRole('group', { name: 'delete this session' })).toBeNull();
    expect(acts.getByRole('button', { name: 'More actions' })).toHaveFocus();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DELETE', expect.anything());

    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Delete…' }));
    await user.click(screen.getByRole('button', { name: 'Delete session' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DELETE', { payload: { id: 'c0ffee11' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^Deleted c0ffee11/)));
  });

  /**
   * PAUSE1e (D132 §2.6, §7.1): a running driven session's ⋯ offers *Pause quest…*; it asks once under the header, saying
   * from the driver's plan what it stops, and pauses on its second press. Where a pause holds its quest, *Resume quest* is
   * the header's loud act, in *Try again*'s place.
   */
  it('pauses a running session’s quest from its header’s ⋯, asking once and saying what it stops', async () => {
    const plan = {
      scope: 'quest', id: 'abc123', pausable: true, paused: null, trees: [], landings: [],
      quests: [{ quest: 'abc123', title: 'Expose a streaming budget', to: 'engine', status: 'Open', joined: 'named', pause: 'paused', key: 'quest:abc123', abandon: 'decline', whileOpen: true }],
      sessions: [{ session: 's1a2b3c4', repository: 'engine', state: 'working', quest: 'abc123', pause: 'stopped', key: 'session:s1a2b3c4', abandon: 'stop', archive: true }],
      abandon: { abandonable: true, pieces: ['quest:abc123', 'session:s1a2b3c4'], closes: null, abandoned: null },
    };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'WORK_PLAN') return plan;
      if (type === 'WORK_PAUSE') return { scope: 'quest', id: 'abc123', did: 'paused', already: false, stopped: [{ session: 's1a2b3c4', quest: 'abc123' }], kept: [] };
      if (type === 'SESSION_GROUPS') return { sessions: [{ session: 's1a2b3c4', group: 'working', shown: 'working', archived: false, teammate: false }] };
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('s1a2b3c4', notify);

    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GROUPS', {}));
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Pause quest…' }));

    const ask = await screen.findByRole('group', { name: 'pause this work' });
    await waitFor(() => expect(ask).toHaveTextContent('Stops 1 running session now and starts nothing of quest #abc123 on this machine'));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_PAUSE', expect.anything());
    await user.click(within(ask).getByRole('button', { name: 'Pause quest' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_PAUSE', expect.objectContaining({ payload: { quest: 'abc123' } })));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^Paused quest #abc123 and stopped 1 session/), 'ok'));
    await waitFor(() => expect(screen.queryByRole('group', { name: 'pause this work' })).toBeNull());
  });

  it('leads a stopped session’s header with Resume where a pause holds its quest, and resumes over the driver', async () => {
    SESSIONS = [{ ...DRIVEN, state: 'stopped' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 's1a2b3c4', group: 'ended', shown: 'stopped', archived: false, teammate: false, pausedBy: { scope: 'quest', id: 'abc123' } }] };
      }
      if (type === 'WORK_RESUME') return { scope: 'quest', id: 'abc123', did: 'resumed', released: [{ quest: 'abc123', session: 's1a2b3c4' }], holds: [] };
      return DRIVER_STATE;
    });
    show('s1a2b3c4');

    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    const resume = await acts.findByRole('button', { name: 'Resume quest' });
    expect(resume.className).toContain('bg-accent');
    expect(acts.queryByRole('button', { name: 'Try again' })).toBeNull();
    await userEvent.click(resume);
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_RESUME', expect.objectContaining({ payload: { quest: 'abc123' } })));
  });

  it('offers a driven session that served a quest no Delete… in its header', async () => {
    SESSIONS = [{ ...DRIVEN, state: 'completed' }];
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_GROUPS'
      ? { sessions: [{ session: 's1a2b3c4', group: 'ended', shown: 'completed', archived: false, teammate: false, deletable: false }] }
      : DRIVER_STATE));
    show('s1a2b3c4');

    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GROUPS', {}));
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await screen.findByRole('menuitem', { name: 'Archive' });
    expect(screen.queryByRole('menuitem', { name: 'Delete…' })).toBeNull();
  });

  /**
   * UX7c (D152 §7, amending D126 §3.2): the header says its word, its title and its facts; its id is its ⋯'s and its
   * record's *Details*, and the record under it says neither its word nor its title again.
   */
  it('names the session in its header with its word and its facts, its id in Details alone', async () => {
    show('s1a2b3c4');

    const header = (await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' })).closest('header')!;
    expect(within(header).getByText('working')).toBeInTheDocument();
    expect(within(header).getByText(/^engine · /)).toBeInTheDocument();
    expect(within(header).queryByText('s1a2b3c4')).toBeNull();
    const head = screen.getByRole('button', { name: /^Details/ }).closest('header')!;
    expect(within(head).queryByText('working')).toBeNull();
    expect(within(head).queryByRole('heading', { level: 2 })).toBeNull();
    fireEvent.click(within(head).getByRole('button', { name: /^Details/ }));
    expect(within(head).getByText('s1a2b3c4')).toBeInTheDocument();
  });

  /**
   * PROCESS1e (D164's amendment point 3): the attended session's *Details* says the process its own tree runs under, as the driver
   * answers it for that session, with where each rule was set.
   */
  it('says in Details how the session commits and lands, as the driver answers for it', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_PROCESS'
      ? {
          session: 's1a2b3c4',
          process: {
            commits: { repository: 'engine', workspace: 'default', rule: { mode: 'at-end' }, source: 'repository' },
            landing: { repository: 'engine', workspace: 'default', source: 'default', form: 'merge' },
          },
        }
      : DRIVER_STATE));
    show('s1a2b3c4');

    const head = (await screen.findByRole('button', { name: /^Details/ })).closest('header')!;
    fireEvent.click(within(head).getByRole('button', { name: /^Details/ }));
    expect(await within(head).findByText('Commits')).toBeInTheDocument();
    expect(within(head).getByText('Commits').nextElementSibling)
      .toHaveTextContent('One commit is made when the work is done. Set for this repository.');
    expect(within(head).getByText('Landing').nextElementSibling).toHaveTextContent('Merged into its line — the default.');
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_PROCESS', expect.objectContaining({ payload: { id: 's1a2b3c4' } }));
  });

  /**
   * MIXAGENT3a-i (the mixed-agents design §4): a session that continues another says which agent's in its facts, by the roster's
   * name for it, and why in the hand-over's words; its *Details* names that session and attends it on a press.
   */
  it('says whom a session continues in its header, and attends that session from Details', async () => {
    const earlier = { ...CHAT, id: 'e4r1i3r0', adapter: 'codex', state: 'completed' };
    SESSIONS = [earlier, { ...CHAT, from: 'e4r1i3r0', fromWhy: 'near', fromBy: 'daoris' }];
    // The roster's codex, named as its product.
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: ROSTER.harnesses.map((row) => (row.harness === 'codex' ? { ...row, product: 'Codex' } : row)) }
      : DRIVER_STATE));
    const { onSelect } = show('c0ffee11');

    const header = (await screen.findByRole('heading', { level: 1, name: 'Chat' })).closest('header')!;
    expect(await within(header).findByText(/^engine · continues from Codex · its account was near its limit · /)).toBeInTheDocument();
    const head = screen.getByRole('button', { name: /^Details/ }).closest('header')!;
    fireEvent.click(within(head).getByRole('button', { name: /^Details/ }));
    fireEvent.click(within(within(head).getByText('Continues').nextElementSibling as HTMLElement).getByRole('button', { name: 'Open session' }));
    expect(onSelect).toHaveBeenCalledWith('e4r1i3r0');
  });

  /** And, as a reviewer's, a helper's record takes one turn and no words (D163's MIXAGENT2a-i note): no box is offered. */
  it('titles a helper as one, says whom it helps by that session’s title, and offers it no box', async () => {
    SESSIONS = [DRIVEN, { ...CHAT, id: 'h3lp3r00', serves: 's1a2b3c4' }];
    show('h3lp3r00');

    const header = (await screen.findByRole('heading', { level: 1, name: 'Helper' })).closest('header')!;
    expect(await within(header).findByText(/^engine · helping Expose a streaming budget · /)).toBeInTheDocument();
    expect(screen.queryByLabelText('Message')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
  });

  /** M2: a quest parked on its failed sessions is tried again where its session is, the loud act in its header. */
  it('tries a parked quest again from its session’s page header', async () => {
    SESSIONS = [{ ...DRIVEN, id: 'f41led00', state: 'failed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 'f41led00', group: 'you', shown: 'parked', archived: false, teammate: false, strikes: 3 }] };
      }
      if (type === 'RETRY_QUEST') return { ...DRIVER_STATE, strikes: 3, retried: { quest: 'abc123', did: 'marked', session: null } };
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('f41led00', notify);

    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    const retry = await acts.findByRole('button', { name: 'Try again' });
    expect(acts.queryByRole('button', { name: 'Stop…' })).toBeNull();
    await userEvent.click(retry);

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RETRY_QUEST', { payload: { quest: 'abc123' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^#abc123 will be tried again/)));
  });

  /** A row's *Stop…* attends its session, then opens the same ask under its header (§3.1). */
  it('asks to stop a session from its row: attended, the ask under its header', async () => {
    SESSIONS = [DRIVEN, { ...CHAT }];
    attending('c0ffee11');
    await screen.findByLabelText('Message');

    const user = userEvent.setup();
    const list = await screen.findByRole('complementary', { name: 'Sessions' });
    within(list).getByRole('button', { name: 'more for Expose a streaming budget' }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Stop…' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'stop this session' })).toBeInTheDocument();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', expect.anything());
  });

  /** A row's *Answer…* attends a session waiting on you and puts the focus in the box at its foot (§3.1). */
  it('answers a session from its row: attended, the focus in the box at its foot', async () => {
    SESSIONS = [{ ...CHAT }, PARKED];
    attending('c0ffee11');
    await screen.findByLabelText('Message');

    const user = userEvent.setup();
    const list = await screen.findByRole('complementary', { name: 'Sessions' });
    const waiting = within(list).getByRole('heading', { level: 3, name: 'Waiting on you (1)' }).closest('section')!;
    within(waiting).getByRole('button', { name: /^more for / }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Answer…' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Carry on with this answer' })).toBeInTheDocument());
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveFocus());
  });

  /** *Open a terminal here* opens the panel's terminal in the session's folder, the panel shown (§3.5). */
  it('opens a terminal in the session’s folder from its header, and shows it', async () => {
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module === 'DAORIS.TERMINAL' && type === 'OPEN') return { id: 't1', shell: 'pwsh', cwd: 'C:/somewhere/engine' };
      if (module === 'DAORIS.TERMINAL' && type === 'SHELLS') return { shells: [{ shell: 'pwsh' }], default: 'pwsh' };
      return DRIVER_STATE;
    });
    // A viewer who hid the panel: the press shows it again, on the terminal.
    window.localStorage.setItem('daoris.panelClosed', '1');
    attending('s1a2b3c4', true);

    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Open a terminal here' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.TERMINAL', 'OPEN', { payload: { cwd: 'C:/somewhere/engine' } }));
    expect(await screen.findByRole('tab', { name: 'Terminal', selected: true })).toBeInTheDocument();
    window.localStorage.removeItem('daoris.panelClosed');
  });

  /** *Open folder* names the session alone: the module names its folder (§3.5). */
  it('opens the session’s folder from its header by its id', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_OPEN_FOLDER' ? { opened: true } : DRIVER_STATE));
    show('s1a2b3c4');

    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Open folder' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_OPEN_FOLDER', { payload: { id: 's1a2b3c4' } }));
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

    const layout = screen.getByRole('radiogroup', { name: 'Layout' });
    expect(within(layout).getByRole('radio', { name: 'Unified' })).toHaveAttribute('aria-checked', 'true');
    expect(within(screen.getAllByRole('row')[0]!).getAllByRole('cell').map((cell) => cell.textContent))
      .toEqual(['1', '', '−', 'old']);

    await userEvent.click(within(layout).getByRole('radio', { name: 'Side by side' }));
    expect(within(screen.getAllByRole('row')[0]!).getAllByRole('cell').map((cell) => cell.textContent))
      .toEqual(['1', 'old', '1', 'new']);
    expect(window.localStorage.getItem('daoris.reviewLayout')).toBe('split');
    window.localStorage.removeItem('daoris.reviewLayout');
  });

  it('reads the landed work off the checkout when the review tab is opened', async () => {
    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText('src/chunk.ts')).toBeTruthy();
    // Within the review's own bound (REVIEW4): the bridge's 30 seconds gave up on a diff git took most of a minute over.
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DIFF', {
      payload: { id: 's1a2b3c4' }, timeoutMs: 180_000,
    });
    // The range it is measured from, stated — a review that does not say so is an opinion.
    expect(screen.getByText(/abc12345/)).toBeTruthy();
    // And a binary file is listed as uncounted rather than as an empty change.
    expect(screen.getByText('binary')).toBeTruthy();
  });

  /**
   * REVIEW4: while git reads, the review's frame is the attended record's — its own branch, the commit it began at, the
   * commits it reported — above the skeleton and the words saying what is read. The installed window sat blank here.
   */
  it('stands the review’s frame from the attended record at once while git reads', async () => {
    SESSIONS = [{
      ...DRIVEN, state: 'completed', tree: 'C:/somewhere/.daoris/trees/default/engine/s-2394e5d9',
      baseCommit: 'abc1234567890', evidence: 'commits landed:\nabc1234 Cap the streaming budget',
    }];
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? new Promise(() => {}) : DRIVER_STATE));

    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText('Reading the changes in engine…')).toBeTruthy();
    expect(screen.getByText('daoris/s-2394e5d9')).toBeTruthy();
    expect(screen.getByText('since abc12345')).toBeTruthy();
    expect(screen.getByText('1 commit')).toBeTruthy();
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

  /** REVIEWDIRTY1-b: a tree holding only uncommitted work shows it, and does not say nothing landed. */
  it('shows a session\'s uncommitted work where it committed nothing', async () => {
    const uncommitted = { files: [{ path: 'src/draft.ts', status: 'modified', added: 1, removed: 0, patch: '@@ -1 +1 @@\n-a\n+b' }], truncated: false };
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? { ...DIFF, files: [], uncommitted } : DRIVER_STATE));

    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));

    expect(await screen.findByText('src/draft.ts')).toBeTruthy();
    expect(screen.queryByText('Nothing landed')).toBeNull();
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
    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));

    expect(await screen.findByText(/Plugin `github-pull-request`: pushed it/)).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Open the pull request' })).toHaveAttribute('href', 'https://example.test/example-org/engine/pull/7');
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
    await userEvent.click(screen.getByRole('button', { name: 'Hand to github-pull-request' }));

    // LEFT3 a: a hand-off waits as long as its plugin may (`pluginBound`, six minutes), not the bridge's 30 seconds.
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HANDOFF', { payload: { id: 's1a2b3c4' }, timeoutMs: 6 * 60_000 });
    expect(await screen.findByText(/Plugin `github-pull-request`: pushed it/)).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Open the pull request' })).toHaveAttribute('href', 'https://example.test/example-org/engine/pull/8');
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
    expect(screen.getByRole('button', { name: 'Hand to github-pull-request' })).toBeDisabled();
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
    await screen.findByRole('button', { name: 'Accept' });
    expect(screen.queryByRole('button', { name: /Hand to/ })).toBeNull();
    cleanup();

    plan = { session: 's1a2b3c4', branch: 'feature/0fda18-fix', repository: 'engine', plugin: null, problem: 'no plugin is named', commits: 1 };
    await review();
    await screen.findByRole('button', { name: 'Accept' });
    expect(screen.queryByRole('button', { name: /Hand to/ })).toBeNull();
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

    expect(await screen.findByRole('button', { name: 'Hand to github-pull-request' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
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

  /**
   * LAND3b (D102's LAND3 note): a failed session whose tree is gone left a branch only the terminal's
   * `trees remove … --force` reached. Its head offers the discard, asks once, and presses the driver's forced removal of
   * that branch by its name and repository; the driver's sentence for a branch it kept is said whole, as a refusal is.
   */
  it("discards the branch a failed session left once its tree is gone, after asking once", async () => {
    SESSIONS = [{ ...IN_A_TREE, state: 'failed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-abc12345' }];
    let answer = { repository: 'engine', branch: 'daoris/s-abc12345', done: true, message: 'removed the session branch `daoris/s-abc12345` from `engine`.' };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'SWEEP_PLAN') {
        return { branches: [{ repository: 'engine', workspace: 'default', branch: 'daoris/s-abc12345', hasTree: false,
          kind: 'unlanded', commits: 2, removable: false, discardable: true }] };
      }
      if (type === 'DISCARD_SESSION_BRANCH') return answer;
      return DRIVER_STATE;
    });
    const notify = vi.fn();

    show('s1a2b3c4', notify);
    await userEvent.click(await screen.findByRole('button', { name: 'Discard branch…' }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'DISCARD_SESSION_BRANCH', expect.anything());
    const ask = screen.getByRole('group', { name: 'discard daoris/s-abc12345' });
    expect(ask).toHaveTextContent('2 commits no branch of yours holds. Nothing brings them back.');
    const asked = invoke.mock.calls.filter(([, type]) => type === 'SWEEP_PLAN').length;

    await userEvent.click(within(ask).getByRole('button', { name: 'Discard branch' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'DISCARD_SESSION_BRANCH', {
      payload: { repository: 'engine', branch: 'daoris/s-abc12345', force: true },
    });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('Discarded `daoris/s-abc12345` in engine.'));
    // The clean-up's list is read again, so the head says what is left.
    await vi.waitFor(() => expect(invoke.mock.calls.filter(([, type]) => type === 'SWEEP_PLAN').length).toBeGreaterThan(asked));

    // UXFIX2: it closed once the discard landed; a branch the driver kept is said inside the ask, whole, which stays open.
    await vi.waitFor(() => expect(screen.queryByRole('group', { name: 'discard daoris/s-abc12345' })).toBeNull());
    answer = { repository: 'engine', branch: 'daoris/s-abc12345', done: false, message: 'there is no branch `daoris/s-abc12345` in `engine` — it is gone already.' };
    await userEvent.click(await screen.findByRole('button', { name: 'Discard branch…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Discard branch' }));
    const kept = await within(screen.getByRole('group', { name: 'discard daoris/s-abc12345' })).findByRole('alert');
    expect(kept).toHaveTextContent('there is no branch daoris/s-abc12345 in engine — it is gone already.');
    expect(notify).not.toHaveBeenCalledWith(expect.stringContaining('it is gone already'), 'error');
  });

  /**
   * LAND4 (D102's LAND4 note, the owner's case): a failed session's commits stayed in its tree and nothing offered to land
   * them. Where the driver's reader says what its tree offers to land, its page says the commits, the branch and the tree
   * beside what it left, and *Accept…* asks once, saying where the rule puts it, then presses the review's own landing; the
   * driver's sentence is said once it landed, and a refusal inside the ask.
   */
  it('offers a failed session’s commits to land on its page, and lands them through the review’s own door', async () => {
    SESSIONS = [{ ...IN_A_TREE, state: 'failed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
    let landed = { session: 's1a2b3c4', done: true, message: 'put its work on `feature/delta` — 1 commit(s).' };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 's1a2b3c4', group: 'ended', shown: 'failed', archived: false, teammate: false,
          lands: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', commits: 1, uncommitted: 0 } }] };
      }
      if (type === 'LANDING') return { session: 's1a2b3c4', form: 'branch', target: 'feature/delta', source: 'workspace' };
      if (type === 'LAND_SESSION_TREE') return landed;
      if (type === 'SESSION_DIFF') return DIFF;
      return DRIVER_STATE;
    });
    const notify = vi.fn();

    show('s1a2b3c4', notify);
    // The head's, in the main area: the strip at the foot draws the same line (WAITFOOT1b, D168).
    const head = within(screen.getByRole('main'));
    await userEvent.click(await head.findByRole('button', { name: 'Accept…' }));
    expect(head.getByText('in its tree s-4e6837ed')).toBeTruthy();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_SESSION_TREE', expect.anything());
    const ask = screen.getByRole('group', { name: 'accept daoris/s-4e6837ed' });
    await within(ask).findByText(/on a new branch/);
    expect(ask).toHaveTextContent('Accepting puts this work on a new branch, feature/delta, for you to push and open a pull request from.');

    await userEvent.click(within(ask).getByRole('button', { name: 'Accept' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_SESSION_TREE', { payload: { id: 's1a2b3c4' }, timeoutMs: 6 * 60_000 });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('put its work on `feature/delta` — 1 commit(s).'));
    await vi.waitFor(() => expect(screen.queryByRole('group', { name: 'accept daoris/s-4e6837ed' })).toBeNull());

    // A refusal is the driver's sentence, said inside the ask, which stays open.
    landed = { session: 's1a2b3c4', done: false, message: 'the session\'s tree has uncommitted work — 2 path(s) — which a branch would leave behind.' };
    await userEvent.click(await head.findByRole('button', { name: 'Accept…' }));
    await userEvent.click(within(screen.getByRole('group', { name: 'accept daoris/s-4e6837ed' })).getByRole('button', { name: 'Accept' }));
    expect(await within(screen.getByRole('group', { name: 'accept daoris/s-4e6837ed' })).findByRole('alert'))
      .toHaveTextContent('which a branch would leave behind.');
  });

  /**
   * ACCEPTALL1 (D167's 2026-10-15 amendment, the owner's *"I cannot accept for 2 sessions/quests at the same time"*): where more
   * than one part of the attended line of work offers work to land, its page offers one Accept under the Parts strip; the plan is
   * read only once its ask opens, and the press lands the entry.
   */
  it('offers one Accept for every part of a line of work that has work to land', async () => {
    const tree = (name: string) => `C:\\somewhere\\.daoris\\trees\\default\\engine\\${name}`;
    SESSIONS = [
      { ...IN_A_TREE, state: 'completed', tree: tree('s-1a2b3c4') },
      { ...IN_A_TREE, id: 's5e6f7a8', quest: 'abc124', state: 'completed', tree: tree('s-5e6f7a8'), created: '2026-09-02T02:00:00Z', updated: '2026-09-02T03:00:00Z' },
    ];
    const lands = (name: string) => ({ branch: `daoris/${name}`, tree: name, commits: 1, uncommitted: 0 });
    const part = (session: string, quest: string) => ({ session, quest, shown: 'completed', group: 'review', began: 'opened', after: null, askedBy: null });
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return {
          sessions: [
            { session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false, lands: lands('s-1a2b3c4') },
            { session: 's5e6f7a8', group: 'review', shown: 'completed', archived: false, teammate: false, lands: lands('s-5e6f7a8') },
          ],
          entries: [{ entry: 'ask:a3/engine', origin: 'ask:a3', repository: 'engine', group: 'review', leading: 's5e6f7a8',
            parts: [part('s1a2b3c4', 'abc123'), part('s5e6f7a8', 'abc124')] }],
        };
      }
      if (type === 'LANDING') return { session: 's1a2b3c4', form: 'branch', target: 'feature/a3', source: 'workspace' };
      if (type === 'ENTRY_LANDING') {
        return { entry: 'ask:a3/engine', repository: 'engine', form: 'branch', target: 'feature/a3',
          parts: [{ session: 's1a2b3c4', target: 'feature/a3' }, { session: 's5e6f7a8', target: 'feature/a3' }] };
      }
      if (type === 'LAND_ENTRY') return { entry: 'ask:a3/engine', repository: 'engine', done: true, message: 'Landed 2 part(s) on `feature/a3`.', parts: [] };
      if (type === 'SESSION_DIFF') return DIFF;
      return DRIVER_STATE;
    });
    const notify = vi.fn();

    show('s1a2b3c4', notify);
    const main = within(screen.getByRole('main'));
    const press = await main.findByRole('button', { name: 'Accept all 2 parts…' });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'ENTRY_LANDING', expect.anything());

    await userEvent.click(press);
    const ask = screen.getByRole('group', { name: 'accept every part of this line of work' });
    expect(await within(ask).findByText('Lands 2 parts on feature/a3, one after another, in the order they ran.')).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ENTRY_LANDING', { payload: { entry: 'ask:a3/engine' } });

    await userEvent.click(within(ask).getByRole('button', { name: 'Accept all' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_ENTRY', { payload: { entry: 'ask:a3/engine' }, timeoutMs: expect.any(Number) });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('Landed 2 part(s) on `feature/a3`.'));
  });

  /**
   * ACCEPTALL1h (D169's ACCEPTALL1e note): while a line of work's one Accept lands it, its part's own *Accept…* in the head and its
   * review's *Accept* wait, read by the entry the press names, and come back once it answered.
   */
  it('holds a part’s own Accept in the head and in its review while its line of work lands', async () => {
    const tree = (name: string) => `C:\\somewhere\\.daoris\\trees\\default\\engine\\${name}`;
    SESSIONS = [
      { ...IN_A_TREE, state: 'completed', tree: tree('s-1a2b3c4') },
      { ...IN_A_TREE, id: 's5e6f7a8', quest: 'abc124', state: 'completed', tree: tree('s-5e6f7a8'), created: '2026-09-02T02:00:00Z', updated: '2026-09-02T03:00:00Z' },
    ];
    const lands = (name: string) => ({ branch: `daoris/${name}`, tree: name, commits: 1, uncommitted: 0 });
    const part = (session: string, quest: string) => ({ session, quest, shown: 'completed', group: 'review', began: 'opened', after: null, askedBy: null });
    let landed: (answer: unknown) => void = () => {};
    invoke.mockImplementation((_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return Promise.resolve({
          sessions: [
            { session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false, lands: lands('s-1a2b3c4') },
            { session: 's5e6f7a8', group: 'review', shown: 'completed', archived: false, teammate: false, lands: lands('s-5e6f7a8') },
          ],
          entries: [{ entry: 'ask:a3/engine', origin: 'ask:a3', repository: 'engine', group: 'review', leading: 's5e6f7a8',
            parts: [part('s1a2b3c4', 'abc123'), part('s5e6f7a8', 'abc124')] }],
        });
      }
      if (type === 'LANDING') return Promise.resolve({ session: 's1a2b3c4', form: 'branch', target: 'feature/a3', source: 'workspace' });
      if (type === 'ENTRY_LANDING') {
        return Promise.resolve({ entry: 'ask:a3/engine', repository: 'engine', form: 'branch', target: 'feature/a3',
          parts: [{ session: 's1a2b3c4', target: 'feature/a3' }, { session: 's5e6f7a8', target: 'feature/a3' }] });
      }
      if (type === 'LAND_ENTRY') return new Promise((resolve) => { landed = resolve; });
      if (type === 'SESSION_DIFF') return Promise.resolve(DIFF);
      return Promise.resolve(DRIVER_STATE);
    });

    show('s1a2b3c4', vi.fn());
    const main = within(screen.getByRole('main'));
    expect(await main.findByRole('button', { name: 'Accept…' })).toBeEnabled();
    await userEvent.click(await main.findByRole('button', { name: 'Accept all 2 parts…' }));
    const ask = screen.getByRole('group', { name: 'accept every part of this line of work' });
    await within(ask).findByText('Lands 2 parts on feature/a3, one after another, in the order they ran.');
    await userEvent.click(within(ask).getByRole('button', { name: 'Accept all' }));

    await vi.waitFor(() => expect(main.getByRole('button', { name: 'Accept…' })).toBeDisabled());
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    const review = within(await screen.findByRole('tabpanel'));
    await review.findByText('src/chunk.ts');
    expect(await review.findByRole('button', { name: 'Landing…' })).toBeDisabled();

    await act(async () => landed({ entry: 'ask:a3/engine', repository: 'engine', done: true, message: 'Landed 2 part(s) on `feature/a3`.', parts: [] }));
    await vi.waitFor(() => expect(review.queryByRole('button', { name: 'Landing…' })).toBeNull());
  });

  /**
   * STATEHUB1c (D169 point 7, the owner's 2026-10-15 采纳): a landing pressed in the head was the head's alone, so the review
   * offered *Accept* while it ran. Under way, every view of the session says so: the review's *Accept* reads *Landing…* and
   * waits, as it would for its own press, until the landing answers.
   */
  it('holds the review’s Accept as Landing… while the head’s landing is under way', async () => {
    SESSIONS = [{ ...IN_A_TREE, state: 'completed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
    let landed: (answer: unknown) => void = () => {};
    invoke.mockImplementation((_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return Promise.resolve({ sessions: [{ session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false,
          lands: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', commits: 1, uncommitted: 0 } }] });
      }
      if (type === 'LANDING') return Promise.resolve({ session: 's1a2b3c4', form: 'branch', target: 'feature/delta', source: 'workspace' });
      if (type === 'LAND_SESSION_TREE') return new Promise((resolve) => { landed = resolve; });
      if (type === 'SESSION_DIFF') return Promise.resolve(DIFF);
      return Promise.resolve(DRIVER_STATE);
    });

    show('s1a2b3c4', vi.fn());
    await userEvent.click(await within(screen.getByRole('main')).findByRole('button', { name: 'Accept…' }));
    const ask = screen.getByRole('group', { name: 'accept daoris/s-4e6837ed' });
    await vi.waitFor(() => expect(within(ask).getByRole('button', { name: 'Accept' })).toBeEnabled());
    await userEvent.click(within(ask).getByRole('button', { name: 'Accept' }));

    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    const review = within(await screen.findByRole('tabpanel'));
    await review.findByText('src/chunk.ts');
    expect(await review.findByRole('button', { name: 'Landing…' })).toBeDisabled();
    expect(review.queryByRole('button', { name: 'Accept' })).toBeNull();

    await act(async () => landed({ session: 's1a2b3c4', done: true, message: 'put its work on `feature/delta` — 1 commit(s).' }));
    await vi.waitFor(() => expect(review.queryByRole('button', { name: 'Landing…' })).toBeNull());
  });

  /**
   * LANDNAME1b (D145's LANDNAME1a note): a branch name entered in the head's ask asks the plan again with it, so the driver's
   * refusal shows before the press, and the press sends it; a press with no name stays `{ id }` (the test above).
   */
  it('asks the plan again with an entered branch name, and the press sends it', async () => {
    SESSIONS = [{ ...IN_A_TREE, state: 'completed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
    invoke.mockImplementation(async (_module: string, type: string, args?: { payload?: { branch?: string } }) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false,
          lands: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', commits: 1, uncommitted: 0 } }] };
      }
      if (type === 'LANDING') {
        return args?.payload?.branch === 'main'
          ? { session: 's1a2b3c4', form: 'branch', target: 'main', problem: 'a branch named `main` stands, which Daoris did not make.' }
          : { session: 's1a2b3c4', form: 'branch', target: 'feature/delta', source: 'workspace' };
      }
      if (type === 'LAND_SESSION_TREE') return { session: 's1a2b3c4', done: true, message: 'put its work on `mine/x` — 1 commit(s).' };
      if (type === 'SESSION_DIFF') return DIFF;
      return DRIVER_STATE;
    });

    show('s1a2b3c4', vi.fn());
    await userEvent.click(await within(screen.getByRole('main')).findByRole('button', { name: 'Accept…' }));
    const ask = screen.getByRole('group', { name: 'accept daoris/s-4e6837ed' });
    const field = await within(ask).findByRole('textbox', { name: 'Branch name' });
    expect(field).toHaveAttribute('placeholder', 'feature/delta');
    await userEvent.type(field, 'main');
    // Asked once after the pause, not per letter (the driver's plan reads git).
    expect(await within(ask).findByText(/which Daoris did not make/)).toBeTruthy();
    expect(invoke.mock.calls.filter(([, type, args]) => type === 'LANDING' && args?.payload?.branch)).toEqual([
      ['DAORIS.DRIVER', 'LANDING', { payload: { id: 's1a2b3c4', branch: 'main' } }],
    ]);

    await userEvent.clear(field);
    await userEvent.type(field, 'mine/x');
    // Held until the plan of that exact name has answered.
    await vi.waitFor(() => expect(within(ask).getByRole('button', { name: 'Accept' })).toBeEnabled());
    await userEvent.click(within(ask).getByRole('button', { name: 'Accept' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_SESSION_TREE', { payload: { id: 's1a2b3c4', branch: 'mine/x' }, timeoutMs: 6 * 60_000 });
  });

  /**
   * WAITFOOT1b (D168 point 3): *Accept…* stands in the head and in the strip at the foot, one press, and its ask is open in one
   * of them at a time: opening it in one puts the other down, a branch name typed there with it.
   */
  it('opens the work\'s ask in one place at a time, the head\'s or the foot\'s', async () => {
    SESSIONS = [{ ...IN_A_TREE, state: 'completed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false,
          lands: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', commits: 1, uncommitted: 0 } }] };
      }
      if (type === 'LANDING') return { session: 's1a2b3c4', form: 'branch', target: 'feature/delta', source: 'workspace' };
      if (type === 'SESSION_DIFF') return DIFF;
      return DRIVER_STATE;
    });
    const asks = () => screen.queryAllByRole('group', { name: 'accept daoris/s-4e6837ed' });

    show('s1a2b3c4', vi.fn());
    const strip = await screen.findByRole('region', { name: 'Waiting on you' });
    const presses = await screen.findAllByRole('button', { name: 'Accept…' });
    expect(presses).toHaveLength(2);
    const head = presses.find((press) => !strip.contains(press))!;

    await userEvent.click(within(strip).getByRole('button', { name: 'Accept…' }));
    expect(asks()).toHaveLength(1);
    expect(strip.contains(asks()[0]!)).toBe(true);
    await userEvent.type(within(asks()[0]!).getByRole('textbox', { name: 'Branch name' }), 'mine/x');

    await userEvent.click(head);
    expect(asks()).toHaveLength(1);
    expect(strip.contains(asks()[0]!)).toBe(false);
    expect(within(strip).getByRole('button', { name: 'Accept…' })).toBeInTheDocument();
    // The name typed at the foot went with its ask.
    expect(within(asks()[0]!).getByRole('textbox', { name: 'Branch name' })).toHaveValue('');
  });

  // WAITFOOT2a: the strip stands aside while the head's waits are on screen, never from under an ask open in it.
  it('keeps the strip up while the work\'s ask is open in it, though the head came on screen', async () => {
    const watched = watchScreen();
    try {
      SESSIONS = [{ ...IN_A_TREE, state: 'completed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
      invoke.mockImplementation(async (_module: string, type: string) => {
        if (type === 'SESSION_GROUPS') {
          return { sessions: [{ session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false,
            lands: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', commits: 1, uncommitted: 0 } }] };
        }
        if (type === 'LANDING') return { session: 's1a2b3c4', form: 'branch', target: 'feature/delta', source: 'workspace' };
        if (type === 'SESSION_DIFF') return DIFF;
        return DRIVER_STATE;
      });
      show('s1a2b3c4', vi.fn());
      await screen.findAllByRole('button', { name: 'Accept…' });
      await waitFor(() => expect(watched.observers()).not.toHaveLength(0));
      watched.show(false);
      const strip = screen.getByRole('region', { name: 'Waiting on you' });
      await userEvent.click(within(strip).getByRole('button', { name: 'Accept…' }));
      await userEvent.click(screen.getByRole('main'));

      watched.show(true);
      expect(within(strip).getByRole('group', { name: 'accept daoris/s-4e6837ed' })).toBeInTheDocument();
      // Put down, and the person gone from it, the strip stands aside: the head's *Accept…* is the one on screen.
      await userEvent.click(within(strip).getByRole('button', { name: 'Never mind' }));
      await userEvent.click(screen.getByRole('main'));
      expect(screen.queryByRole('region', { name: 'Waiting on you' })).toBeNull();
      expect(screen.getAllByRole('button', { name: 'Accept…' })).toHaveLength(1);
    } finally {
      watched.restore();
    }
  });

  /**
   * XAGENT1g (the second-agent design §7–§9): where a level asks a second opinion, the session's page draws it before *Accept…*
   * from the landing's plan, its findings read from the gate, and *Accept…*, which answers a dispute, sends the gate's token back.
   * *Ask again* is the driver's own press.
   */
  it('draws the second opinion on a session’s page, and Accept… answers its dispute with the gate’s token', async () => {
    SESSIONS = [{ ...IN_A_TREE, state: 'completed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
    const token = 'o1:4f9c2a7e:disputed:1:0';
    const disputed = {
      state: 'disputed', holds: true, required: false, opinion: 'o1', reviewer: 'codex-acp', product: 'Codex', maker: 'OpenAI',
      label: 'another-maker', reviewing: 'r7c1', findings: 1, disputes: 1, since: 0, answers: token,
    };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false,
          lands: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', commits: 1, uncommitted: 0 } }] };
      }
      if (type === 'LANDING') return { session: 's1a2b3c4', form: 'merge', target: 'main', source: 'workspace', opinion: disputed };
      if (type === 'OPINION_GATE') {
        return { session: 's1a2b3c4', done: true, message: '', opinion: disputed, detail: { tier: 'agent', first: {
          id: 'o1', reviewer: 'codex-acp', product: 'Codex', maker: 'OpenAI', label: 'another-maker', read: 'src/',
          findings: [{ number: 1, weight: 'must', where: 'src/a.ts:3', claim: 'Drops the last row.',
            beside: { answer: { said: 'rejected', evidence: 'The bound is exclusive.' }, disputed: true } }] } } };
      }
      if (type === 'ASK_OPINION') return { session: 's1a2b3c4', done: true, message: 'A second opinion on session s1a2b3c4\'s work is asked.' };
      if (type === 'LAND_SESSION_TREE') return { session: 's1a2b3c4', done: true, message: 'merged into `main`.' };
      if (type === 'SESSION_DIFF') return DIFF;
      return DRIVER_STATE;
    });
    const notify = vi.fn();

    show('s1a2b3c4', notify);
    // The head's, in the main area: the strip at the foot draws the same gate (WAITFOOT1b, D168).
    const head = within(screen.getByRole('main'));
    const gate = await head.findByRole('region', { name: 'Second opinion' });
    expect(gate).toHaveTextContent('1 finding by Codex (OpenAI) is disputed');
    await within(gate).findByText('Drops the last row.');
    expect(gate).toHaveTextContent('Rejected by the working session: The bound is exclusive.');
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'OPINION_GATE', { payload: { id: 's1a2b3c4' } });

    await userEvent.click(within(gate).getByRole('button', { name: 'Ask again' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ASK_OPINION', { payload: { id: 's1a2b3c4' } });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('A second opinion on session s1a2b3c4\'s work is asked.'));

    await userEvent.click(head.getByRole('button', { name: 'Accept…' }));
    const ask = screen.getByRole('group', { name: 'accept daoris/s-4e6837ed' });
    expect(ask).toHaveTextContent('Accepting answers what the second opinion left unsettled: 1 disputed finding.');
    await userEvent.click(within(ask).getByRole('button', { name: 'Accept' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_SESSION_TREE', { payload: { id: 's1a2b3c4', answers: token }, timeoutMs: 6 * 60_000 });
  });

  /**
   * SQUASHTIDY1b (D102's SQUASHTIDY1b note, the owner's case): a session whose work a squash-merged pull request carried, its
   * tree still standing, was offered *Accept…* again. Where the driver's reader says the line holds its commits by content,
   * its page offers no *Accept…*: the driver's sentence says where the work is, and *Discard branch…* asks once with the
   * driver's sentence naming the ref the commits stay at, then presses the review's Discard, unforced.
   */
  it('offers a squash-merged session’s discard on its page, never its landing, and discards through the review’s door unforced', async () => {
    const kept = 'refs/daoris/discarded/daoris/s-4e6837ed';
    SESSIONS = [{ ...IN_A_TREE, state: 'completed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false, lands: null,
          discards: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', says: 'Its work is on `main` by content (a squash merge).',
            keeps: kept, keptAt: `Its commits stay at \`${kept}\` until you delete that ref; \`git branch daoris/s-4e6837ed ${kept}\` brings the branch back.` } }] };
      }
      if (type === 'DISCARD_SESSION_TREE') {
        return { session: 's1a2b3c4', done: true,
          message: `removed the session tree at s-4e6837ed (branch \`daoris/s-4e6837ed\`): its work is on \`main\` by content (a squash merge). Its commits stay at \`${kept}\` until you delete that ref.` };
      }
      if (type === 'SESSION_DIFF') return DIFF;
      return DRIVER_STATE;
    });
    const notify = vi.fn();

    show('s1a2b3c4', notify);
    // The head's, in the main area: the strip at the foot draws the same line (WAITFOOT1b, D168).
    const head = within(screen.getByRole('main'));
    await userEvent.click(await head.findByRole('button', { name: 'Discard branch…' }));
    expect(screen.queryByRole('button', { name: 'Accept…' })).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'LANDING', expect.anything());
    expect(head.getByText('in its tree s-4e6837ed')).toBeTruthy();
    const ask = screen.getByRole('group', { name: 'discard daoris/s-4e6837ed' });
    expect(ask).toHaveTextContent(`Its commits stay at ${kept} until you delete that ref`);
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'DISCARD_SESSION_TREE', expect.anything());

    await userEvent.click(within(ask).getByRole('button', { name: 'Discard branch' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'DISCARD_SESSION_TREE', { payload: { id: 's1a2b3c4' } });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('its work is on `main` by content (a squash merge)')));
    await vi.waitFor(() => expect(screen.queryByRole('group', { name: 'discard daoris/s-4e6837ed' })).toBeNull());
  });

  /**
   * SQUASHTIDY1f (D102's SQUASHTIDY1f note): the review's own *Accept* was still offered for that session, and the landing
   * door would land a second copy. Its review offers no *Accept*: the driver's sentence stands in its place, and the head's
   * *Discard branch…* asks once and presses the review's Discard unforced, through the head's own press.
   */
  it('offers a squash-merged session’s discard in its review where Accept stood, through the head’s own press', async () => {
    const kept = 'refs/daoris/discarded/daoris/s-4e6837ed';
    SESSIONS = [{ ...IN_A_TREE, state: 'completed', tree: 'C:\\somewhere\\.daoris\\trees\\default\\engine\\s-4e6837ed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [{ session: 's1a2b3c4', group: 'review', shown: 'completed', archived: false, teammate: false, lands: null,
          discards: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', says: 'Its work is on `main` by content (a squash merge).',
            keeps: kept, keptAt: `Its commits stay at \`${kept}\` until you delete that ref; \`git branch daoris/s-4e6837ed ${kept}\` brings the branch back.` } }] };
      }
      if (type === 'DISCARD_SESSION_TREE') {
        return { session: 's1a2b3c4', done: true,
          message: `removed the session tree at s-4e6837ed (branch \`daoris/s-4e6837ed\`): its work is on \`main\` by content (a squash merge). Its commits stay at \`${kept}\` until you delete that ref.` };
      }
      if (type === 'SESSION_DIFF') return DIFF;
      return DRIVER_STATE;
    });
    const notify = vi.fn();

    show('s1a2b3c4', notify);
    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    const review = await screen.findByRole('tabpanel');
    await within(review).findByText('src/chunk.ts');
    await within(review).findByRole('button', { name: 'Discard branch…' });
    expect(review).toHaveTextContent('Its work is on main by content (a squash merge).');
    expect(within(review).queryByRole('button', { name: 'Accept' })).toBeNull();
    expect(within(review).queryByRole('button', { name: 'Discard tree' })).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'LANDING', expect.anything());

    await userEvent.click(within(review).getByRole('button', { name: 'Discard branch…' }));
    const ask = within(review).getByRole('group', { name: 'discard daoris/s-4e6837ed' });
    expect(ask).toHaveTextContent(`Its commits stay at ${kept} until you delete that ref`);
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'DISCARD_SESSION_TREE', expect.anything());
    await userEvent.click(within(ask).getByRole('button', { name: 'Discard branch' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'DISCARD_SESSION_TREE', { payload: { id: 's1a2b3c4' } });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_SESSION_TREE', expect.anything());
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('its work is on `main` by content (a squash merge)')));
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
    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));

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
    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));

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
    await userEvent.click(screen.getByRole('button', { name: 'Discard tree' }));

    // No `force` anywhere in the first call.
    expect(forced).toEqual({ id: 's1a2b3c4' });
    // The host's warning is what the person now reads, naming what would go.
    expect(await screen.findByText(/has not taken/)).toBeTruthy();
    // And only now is the destructive press available.
    const again = screen.getByRole('button', { name: 'Discard anyway' });

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
    await userEvent.click(await screen.findByRole('button', { name: 'Discard tree' }));
    rerender(pane('s9f8e7d6'));
    await screen.findByRole('button', { name: 'Discard tree' });
    await act(async () => answer({ session: 's1a2b3c4', done: false, message: 'the tree holds commits `main` has not taken.' }));

    expect(screen.queryByRole('button', { name: 'Discard anyway' })).toBeNull();
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
    await userEvent.click(screen.getByRole('button', { name: 'Discard tree' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Never mind' }));

    expect(screen.queryByRole('button', { name: 'Discard anyway' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Discard tree' })).toBeTruthy();
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
    await userEvent.click(await screen.findByRole('button', { name: 'Send back…' }));

    expect(onSendBack).toHaveBeenCalledWith('engine');
  });

  it('offers no send-back door where none was given', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SESSION_DIFF' ? DIFF : DRIVER_STATE));

    await review();
    expect(screen.queryByRole('button', { name: 'Send back…' })).toBeNull();
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

    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Discard tree' })).toBeNull();
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

    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Discard tree' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Send back…' }));
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
    const edge = await screen.findByRole('separator', { name: 'session list width' });
    expect(edge).toHaveAttribute('aria-valuenow', '280');

    edge.focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(screen.getByRole('separator', { name: 'session list width' })).toHaveAttribute('aria-valuenow', '304');
    expect(window.localStorage.getItem('daoris.railWidth')).toBe('304');
  });

  it('closes the rail to a strip that still reaches every session, and only the person opens it again', async () => {
    const { onSelect } = show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('button', { name: 'Hide the session list' }));

    expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull();
    await userEvent.click(await screen.findByRole('button', { name: 'Chat · engine · working' }));
    expect(onSelect).toHaveBeenCalledWith('c0ffee11');

    // A wider window is not the person asking for it back.
    widen(2400);
    expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull();
    expect(window.localStorage.getItem('daoris.railClosed')).toBe('1');

    await userEvent.click(screen.getByRole('button', { name: 'Show the session list' }));
    expect(await screen.findByRole('separator', { name: 'session list width' })).toBeInTheDocument();
  });

  it('draws the rail as a strip in a narrow window, and gives it back when the window widens', async () => {
    widen(1000);
    show('s1a2b3c4');
    expect(await screen.findByRole('button', { name: 'Chat · engine · working' })).toBeInTheDocument();
    expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull();

    widen(1600);
    expect(await screen.findByRole('separator', { name: 'session list width' })).toBeInTheDocument();
  });

  /**
   * D118 §3a: the rail is a strip by room, not below a fixed 1024 px. At 900 px beside a closed side bar
   * the conversation has 540 px beside the rail, so the rail stays open; the side bar opened takes the room.
   */
  it('keeps the rail open at 900 px beside a closed side bar, and draws its strip beside an open one', async () => {
    widen(900);
    window.localStorage.setItem('daoris.dockClosed', '1');
    show('s1a2b3c4');
    expect(await screen.findByRole('separator', { name: 'session list width' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Open Timeline' }));
    await waitFor(() => expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull());
    expect(screen.getByRole('button', { name: 'Chat · engine · working' })).toBeInTheDocument();
  });

  /**
   * D118 §3a, amending FRAME6: a strip the window drew opens the rail OVER the conversation, so an ended
   * session and the rail's search are within reach at 900 px. It closes on a choice, on Escape and on a
   * press outside it, and is never remembered.
   */
  it('lays the rail over the conversation from a strip the window drew, and closes it on a choice, Escape or a press outside', async () => {
    widen(900);
    const { onSelect } = show('s1a2b3c4');
    const open = await screen.findByRole('button', { name: 'Show the session list' });

    await userEvent.click(open);
    let over = screen.getByRole('region', { name: 'Sessions' });
    expect(within(over).getByRole('searchbox', { name: 'search sessions' })).toBeInTheDocument();
    // The strip stays beside it, and the conversation keeps its room: nothing was pushed aside.
    expect(screen.getByRole('button', { name: 'Chat · engine · working' })).toBeInTheDocument();
    // A row is its button, named by what it holds; its menu is named for it.
    const chat = within(over).getAllByRole('button').find((button) => !button.hasAttribute('aria-label') && button.textContent?.includes('Chat'));
    await userEvent.click(chat!);
    expect(onSelect).toHaveBeenCalledWith('c0ffee11');
    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Show the session list' }));
    over = screen.getByRole('region', { name: 'Sessions' });
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Show the session list' })).toHaveFocus();

    await userEvent.click(screen.getByRole('button', { name: 'Show the session list' }));
    fireEvent.pointerDown(await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' }));
    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();
    expect(window.localStorage.getItem('daoris.railClosed')).toBeNull();
  });

  it('gives the rail back beside the conversation, not over it, when the room returns', async () => {
    widen(900);
    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('button', { name: 'Show the session list' }));
    expect(screen.getByRole('region', { name: 'Sessions' })).toBeInTheDocument();

    widen(1600);
    expect(await screen.findByRole('separator', { name: 'session list width' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();
    // Narrowed again, the window draws its strip: the laying over was not kept.
    widen(900);
    await waitFor(() => expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull());
    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();
  });

  /**
   * ENTRY1g (D161's ENTRY1b note): a go to a group of the rail lays its strip over the conversation, whoever drew the strip,
   * and the rail laid over brings the group into view there; the strip beside it lets nothing go. Laid over, it stays until
   * the person lets it go, and nothing is remembered.
   */
  it('lays the strip the window drew over the conversation for a go, and brings its group into view there', async () => {
    SESSIONS = [DRIVEN, PARKED];
    widen(900);
    const brought = going('you');

    const over = await screen.findByRole('region', { name: 'Sessions' });
    const heading = await within(over).findByRole('heading', { level: 3, name: 'Waiting on you (1)' });
    await waitFor(() => expect(brought).toHaveBeenCalledTimes(1));
    expect(heading).toHaveFocus();
    expect(screen.getByRole('region', { name: 'Sessions' })).toBeInTheDocument();

    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();
    expect(window.localStorage.getItem('daoris.railClosed')).toBeNull();
  });

  it('lays the strip the person closed over the conversation for a go, and their closing stands once it goes', async () => {
    SESSIONS = [DRIVEN, PARKED];
    window.localStorage.setItem('daoris.railClosed', '1');
    const brought = going('you');

    const over = await screen.findByRole('region', { name: 'Sessions' });
    const heading = await within(over).findByRole('heading', { level: 3, name: 'Waiting on you (1)' });
    await waitFor(() => expect(brought).toHaveBeenCalledTimes(1));
    expect(heading).toHaveFocus();

    fireEvent.pointerDown(document.body);
    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Show the session list' })).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.railClosed')).toBe('1');
  });

  /** FRAME6's keys are still read, so nothing a person closed or widened changes on the upgrade (D118 §3f). */
  it('reads the rail\'s old keys: its width and its closing', async () => {
    window.localStorage.setItem('daoris.railWidth', '330');
    const { unmount } = show(null);
    expect(await screen.findByRole('separator', { name: 'session list width' })).toHaveAttribute('aria-valuenow', '330');
    // The rail's rows are drawn, so what the list asked of the driver has been answered.
    await screen.findByText('Expose a streaming budget');
    unmount();

    window.localStorage.setItem('daoris.railClosed', '1');
    show(null);
    expect(await screen.findByRole('button', { name: 'Show the session list' })).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Chat · engine · working' })).toBeInTheDocument();
    expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull();
  });

  /** D118 §3e (audit A7): the rail moves by ↑ and ↓, where it was walked by Tab. */
  it('moves between the rail\'s rows by the arrows', async () => {
    show('s1a2b3c4');
    const rail = await screen.findByRole('navigation', { name: 'Sessions' });
    await within(rail).findAllByText('Expose a streaming budget');
    // Each row is its button, named by what it holds, and its menu, named for it.
    const rows = within(rail).getAllByRole('button').filter((button) => !button.hasAttribute('aria-label'));
    expect(rows).toHaveLength(2);
    const [first, second] = rows;
    first!.focus();
    await userEvent.keyboard('{ArrowDown}');
    expect(second).toHaveFocus();
    await userEvent.keyboard('{ArrowUp}');
    expect(first).toHaveFocus();
  });

  /**
   * D118 §1, §5: every view hands the frame a `ViewLayout`, its list pane where it has one and its main
   * area, and the frame draws the list for every view that hands one. Sessions' rail is one such list.
   */
  describe('a view with a list, and a view without one', () => {
    const ROWS = ['Expose a streaming budget', 'Read the budget from the level file'];
    /** A view's list as it hands it in, with its chosen item held for it, as the application holds it. */
    function Quests({ initial = null }: { initial?: string | null }) {
      const [chosen, setChosen] = useState<string | null>(initial);
      return (
        <WorkFrame
          selected={null}
          onSelect={vi.fn()}
          notify={() => {}}
          layout={{
            list: {
              view: 'quests',
              name: 'Quests',
              labels: { open: 'Show the quest list', close: 'Hide the quest list', resize: 'quest list width' },
              chosen,
              body: (
                <ul>
                  {ROWS.map((title) => (
                    <li key={title} data-list-row=""><button type="button" onClick={() => setChosen(title)}>{title}</button></li>
                  ))}
                </ul>
              ),
            },
            main: <main><h1>{chosen ?? 'Nothing chosen'}</h1></main>,
          }}
        />
      );
    }
    const frame = () => {
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      return render(<QueryClientProvider client={client}><Tooltip.Provider><Quests /></Tooltip.Provider></QueryClientProvider>);
    };

    it('draws its list beside its main area, with the side bar and the panel, and no session rail', async () => {
      frame();
      expect(await screen.findByRole('separator', { name: 'quest list width' })).toHaveAttribute('aria-valuenow', '280');
      expect(screen.getByRole('heading', { level: 1, name: 'Nothing chosen' })).toBeInTheDocument();
      expect(screen.getByRole('complementary', { name: 'right side bar' })).toBeInTheDocument();
      expect(screen.getByRole('region', { name: 'the panel' })).toBeInTheDocument();
      expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull();

      await userEvent.click(screen.getByRole('button', { name: 'Read the budget from the level file' }));
      expect(screen.getByRole('heading', { level: 1, name: 'Read the budget from the level file' })).toBeInTheDocument();
    });

    it('closes and widens as its own, in its own keys, leaving Sessions\' rail as it was', async () => {
      frame();
      const edge = await screen.findByRole('separator', { name: 'quest list width' });
      edge.focus();
      await userEvent.keyboard('{ArrowRight}');
      expect(window.localStorage.getItem('daoris.list.quests.width')).toBe('304');

      await userEvent.click(screen.getByRole('button', { name: 'Hide the quest list' }));
      expect(screen.queryByRole('separator', { name: 'quest list width' })).toBeNull();
      expect(window.localStorage.getItem('daoris.list.quests.closed')).toBe('1');
      expect(window.localStorage.getItem('daoris.railClosed')).toBeNull();
      expect(window.localStorage.getItem('daoris.railWidth')).toBeNull();

      await userEvent.click(screen.getByRole('button', { name: 'Show the quest list' }));
      expect(await screen.findByRole('separator', { name: 'quest list width' })).toHaveAttribute('aria-valuenow', '304');
      window.localStorage.removeItem('daoris.list.quests.width');
    });

    it('lays its list over the main area from a strip the window drew, and a choice closes it', async () => {
      widen(900);
      frame();
      await userEvent.click(await screen.findByRole('button', { name: 'Show the quest list' }));
      const over = screen.getByRole('region', { name: 'Quests' });

      await userEvent.click(within(over).getByRole('button', { name: 'Expose a streaming budget' }));
      expect(screen.getByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeInTheDocument();
      await waitFor(() => expect(screen.queryByRole('region', { name: 'Quests' })).toBeNull());
      expect(window.localStorage.getItem('daoris.list.quests.closed')).toBeNull();
    });

    it('draws no list pane for a view that hands none', async () => {
      // No live chat: its turns would be asked of the driver after this short test has let the bridge go.
      SESSIONS = [DRIVEN];
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      render(
        <QueryClientProvider client={client}>
          <Tooltip.Provider>
            <WorkFrame selected={null} onSelect={vi.fn()} notify={() => {}} layout={{ main: <main><h1>Overview</h1></main> }} />
          </Tooltip.Provider>
        </QueryClientProvider>,
      );
      expect(await screen.findByRole('heading', { level: 1, name: 'Overview' })).toBeInTheDocument();
      expect(document.querySelector('[data-region="list"]')).toBeNull();
    });
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
        <Hub />
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
    // D118: the list gives way to its strip first, so it is past that, at 800 px, that the side bar is cramped.
    widen(800);
    show('s1a2b3c4');
    expect(await screen.findByText(/too narrow to sit beside the session/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Close the side bar' }));
    expect(screen.queryByText(/too narrow to sit beside the session/)).toBeNull();
  });
});

/** A quest an ask asked (D65 §4): its sender names the ask, which is where the go-aheads its sessions asked are held. */
const ASKED_QUEST = {
  id: 'q4sk00', from: 'ask #a5k001', to: 'engine', title: 'Ship the comparison report', body: 'Both sites need it.',
  status: 'Taken', filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
};

/** A session on that quest, parked asking the person for two go-aheads. */
const PARKED_ASKING = {
  ...PARKED, id: 'g0ah3ad0', quest: 'q4sk00', note: 'I need go-aheads 1 and 2 before I can finish.',
};

/** The ask: two go-aheads the park asked, and one another session asked, which is not the park's to show. */
const ASK_WITH_GO_AHEADS = {
  id: 'a5k001', workspace: 'default', sentence: 'Ship the comparison report', state: 'Published', tier: 'declarations',
  asked: '2026-09-01T00:00:00Z', updated: '2026-09-02T00:00:00Z', links: [], attachments: [], proposal: [],
  quests: ['q4sk00'],
  goAheads: [
    {
      number: 1, kind: 'write', on: 'production', act: 'dashboard configuration', state: 'asked',
      asked: [{ session: 'g0ah3ad0', quest: 'q4sk00', at: '2026-09-02T00:30:00Z', why: 'The tile reads its target from it.' }],
    },
    {
      number: 2, kind: 'release', on: 'production', act: 'comparison report', state: 'asked',
      asked: [{ session: 'g0ah3ad0', quest: 'q4sk00', at: '2026-09-02T00:40:00Z', why: 'Ship it.' }],
    },
    {
      number: 3, kind: 'push', on: 'main', act: 'the release branch', state: 'asked',
      asked: [{ session: 'o7h3r000', quest: 'q4sk00', at: '2026-09-02T00:50:00Z', why: 'Another session’s.' }],
    },
  ],
};

/**
 * KNOWUSE1a2 and GOAHEAD2b (D135 §2, D131, D137): a park that asked go-aheads shows them on its page with *Approve* and
 * *Refuse*, and answering one there goes through one door, which sends the same session on once none of them is open, as
 * the ask's page does, and says which one it still waits on where one is. The go-aheads are read from its quest's ask, and
 * only where its quest was asked by one.
 */
describe('a park\'s go-aheads', () => {
  let ASKS: unknown[] = [ASK_WITH_GO_AHEADS];
  beforeEach(() => {
    SESSIONS = [PARKED_ASKING];
    ASKS = [ASK_WITH_GO_AHEADS];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/asks')) return Response.json(ASKS);
      if (url.startsWith('/api/quests')) return Response.json([...QUESTS, ASKED_QUEST]);
      return respond(url);
    }));
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_GO_AHEAD'
      ? { message: 'Go-ahead answered.', sent: true, reaches: 'resume' }
      : DRIVER_STATE));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    window.localStorage.removeItem('daoris.drafts');
  });

  const asksRead = () => vi.mocked(fetch).mock.calls.filter(([input]) => String(input).startsWith('/api/asks')).length;

  it('shows the go-aheads the park asked, each with Approve and Refuse, and not another session\'s', async () => {
    show('g0ah3ad0');

    const section = await screen.findByRole('region', { name: 'Go-aheads it asked' });
    const items = within(section).getAllByRole('listitem');
    expect(items.map((item) => item.textContent)).toEqual([
      expect.stringContaining('dashboard configuration'),
      expect.stringContaining('comparison report'),
    ]);
    expect(section).not.toHaveTextContent('the release branch');
    for (const item of items) {
      expect(within(item).getByRole('button', { name: 'Approve' })).toBeInTheDocument();
      expect(within(item).getByRole('button', { name: 'Refuse' })).toBeInTheDocument();
    }
    // The park's own card and box stay: its question can still be answered in words.
    expect(screen.getByText(/I need go-aheads 1 and 2/)).toBeInTheDocument();
    expect(screen.getByLabelText('Message')).toBeInTheDocument();
  });

  it('approves one with the person\'s words through one door, and says the session goes on where the door sent it on', async () => {
    const notify = vi.fn();
    show('g0ah3ad0', notify);
    const section = await screen.findByRole('region', { name: 'Go-aheads it asked' });
    const second = within(section).getAllByRole('listitem')[1]!;
    const before = asksRead();

    await userEvent.type(within(second).getByLabelText('your words, if any'), 'dev first, then production');
    await userEvent.click(within(second).getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GO_AHEAD', {
      payload: { id: 'g0ah3ad0', ask: 'a5k001', number: 2, approved: true, words: 'dev first, then production' },
    }));
    expect(notify).toHaveBeenCalledWith("Approved go-ahead #2; the same session goes on with it at the driver's next look.");
    // One press: neither the ask's own door nor the box's was asked as well.
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());
    expect(vi.mocked(fetch)).not.toHaveBeenCalledWith('/api/asks/a5k001/go-aheads/2', expect.anything());
    // The ask is read again, so the go-ahead shows what became of it.
    await waitFor(() => expect(asksRead()).toBeGreaterThan(before));
  });

  it('refuses one with no words, sending none, and says the same session goes on with the refusal', async () => {
    const notify = vi.fn();
    show('g0ah3ad0', notify);
    const first = within(await screen.findByRole('region', { name: 'Go-aheads it asked' })).getAllByRole('listitem')[0]!;

    await userEvent.click(within(first).getByRole('button', { name: 'Refuse' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GO_AHEAD', {
      payload: { id: 'g0ah3ad0', ask: 'a5k001', number: 1, approved: false },
    }));
    expect(notify).toHaveBeenCalledWith("Refused go-ahead #1; the same session goes on with it at the driver's next look.");
  });

  /** GOAHEAD2b: a go-ahead the park asked still open keeps it parked, as at the ask's door, and the page says which. */
  it('says which go-ahead the park still waits on where the door kept it parked, the other one still answerable', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_GO_AHEAD'
      ? { message: 'Go-ahead 1 on ask `#a5k001` is approved.', sent: false, waits: true }
      : DRIVER_STATE));
    const notify = vi.fn();
    show('g0ah3ad0', notify);
    const first = within(await screen.findByRole('region', { name: 'Go-aheads it asked' })).getAllByRole('listitem')[0]!;

    await userEvent.click(within(first).getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Approved go-ahead #1. It still waits on you for go-ahead #2, and goes on once that is answered.',
    ));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());
    const second = within(screen.getByRole('region', { name: 'Go-aheads it asked' })).getAllByRole('listitem')[1]!;
    expect(within(second).getByRole('button', { name: 'Approve' })).toBeInTheDocument();
  });

  it('says when the session was no longer waiting, so only the go-ahead was answered', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_GO_AHEAD'
      ? { message: 'Go-ahead answered.', sent: false }
      : DRIVER_STATE));
    const notify = vi.fn();
    show('g0ah3ad0', notify);
    const first = within(await screen.findByRole('region', { name: 'Go-aheads it asked' })).getAllByRole('listitem')[0]!;

    await userEvent.click(within(first).getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Answered go-ahead #1. The session was no longer waiting on you, so the ask keeps your answer for every later start.',
    ));
  });

  it('says a refused go-ahead in the service\'s words beside it (UXFIX2b3b), and keeps the go-aheads to answer', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type !== 'SESSION_GO_AHEAD') return DRIVER_STATE;
      throw Object.assign(new Error('fallback'), {
        code: 'DRIVER_REFUSED', parameters: { message: 'Ask `#a5k001` holds no go-ahead 2: it holds 1.' },
      });
    });
    const notify = vi.fn();
    show('g0ah3ad0', notify);
    const second = within(await screen.findByRole('region', { name: 'Go-aheads it asked' })).getAllByRole('listitem')[1]!;

    await userEvent.click(within(second).getByRole('button', { name: 'Approve' }));

    expect(await within(second).findByRole('alert')).toHaveTextContent('Ask #a5k001 holds no go-ahead 2: it holds 1.');
    expect(notify).not.toHaveBeenCalled();
    expect(within(screen.getByRole('region', { name: 'Go-aheads it asked' })).getAllByRole('button', { name: 'Approve' }))
      .toHaveLength(2);
    expect(within(second).getByRole('button', { name: 'Approve' })).toBeEnabled();
  });

  it('shows nothing new for a park whose ask holds none of its own, and asks for no asks where no ask asked its quest', async () => {
    ASKS = [{ ...ASK_WITH_GO_AHEADS, goAheads: [ASK_WITH_GO_AHEADS.goAheads[2]] }];
    const { unmount } = show('g0ah3ad0');
    expect(await screen.findByText(/I need go-aheads 1 and 2/)).toBeInTheDocument();
    await waitFor(() => expect(asksRead()).toBeGreaterThan(0));
    expect(screen.queryByRole('region', { name: 'Go-aheads it asked' })).toBeNull();
    unmount();

    vi.mocked(fetch).mockClear();
    SESSIONS = [PARKED];
    show('p4rk3d00');
    expect(await screen.findByText(/I recommend the second/)).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Go-aheads it asked' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull();
    expect(asksRead()).toBe(0);
  });

  /**
   * WAITFOOT1b (D168): the person reads the conversation's foot, where the park's question is, so what waits on them stands
   * there too, above the box and outside the conversation's scroll, answered through the head's one door; the head's stay.
   */
  it('answers a go-ahead from the strip at the foot through the same door, the head\'s list still there', async () => {
    const notify = vi.fn();
    show('g0ah3ad0', notify);
    const strip = await screen.findByRole('region', { name: '3 waiting on you' });
    // Outside the conversation's scroll, above the box, as Ask Daoris's note row is.
    expect(screen.getByRole('main').contains(strip)).toBe(false);
    expect(strip.compareDocumentPosition(screen.getByLabelText('Message'))).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    // The box right below is the park's answer door, so no line over it says so (WAITFOOT2c).
    expect(strip).not.toHaveTextContent('answer it in the box below');
    expect(screen.getByRole('region', { name: 'Go-aheads it asked' })).toBeInTheDocument();

    const second = within(strip).getAllByRole('listitem')[1]!;
    await userEvent.click(within(second).getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GO_AHEAD', {
      payload: { id: 'g0ah3ad0', ask: 'a5k001', number: 2, approved: true },
    }));
    expect(notify).toHaveBeenCalledWith("Approved go-ahead #2; the same session goes on with it at the driver's next look.");
  });

  /**
   * WAITFOOT2a (D168's WAITFOOT2 note): the owner, 2026-10-15, found "a lot of duplicated options and message boxes": each wait
   * in the head and again in the strip. The strip stands aside while the head's waits are wholly on screen, and stands again
   * once any of them leaves it, so no press is ever out of sight.
   */
  it('draws each go-ahead once while the head\'s waits are on screen, and at the foot too once they are not', async () => {
    const watched = watchScreen();
    try {
      show('g0ah3ad0');
      const head = await screen.findByRole('region', { name: 'Go-aheads it asked' });
      // Watched where the record scrolls, below the page head pinned at its top.
      await waitFor(() => expect(watched.observers().flatMap((one) => one.targets)).toContain(head));
      const [watching] = watched.observers();
      expect(watching!.root).toBe(screen.getByRole('main'));
      expect(watching!.rootMargin).toMatch(/^-\d+px 0px 0px 0px$/);
      // Not yet told, the strip waits for the observer's word rather than flash over a head that may be on screen.
      expect(screen.queryByRole('region', { name: '3 waiting on you' })).toBeNull();

      watched.show(true);
      expect(screen.queryByRole('region', { name: '3 waiting on you' })).toBeNull();
      expect(screen.getAllByRole('button', { name: 'Approve' })).toHaveLength(2);
      expect(head.contains(screen.getAllByRole('button', { name: 'Approve' })[0]!)).toBe(true);

      watched.show(false);
      expect(screen.getByRole('region', { name: '3 waiting on you' })).toBeInTheDocument();

      // Half of one on screen is not on screen: its press may be the half that is not.
      watched.show((section) => (section === head ? 0.5 : 1));
      expect(screen.getByRole('region', { name: '3 waiting on you' })).toBeInTheDocument();
    } finally {
      watched.restore();
    }
  });

  it('takes the person from the strip to the head\'s waits at the top', async () => {
    show('g0ah3ad0');
    const strip = await screen.findByRole('region', { name: '3 waiting on you' });
    const main = screen.getByRole('main');
    main.scrollTop = 900;

    await userEvent.click(within(strip).getByRole('button', { name: 'To the top' }));
    expect(main.scrollTop).toBe(0);
    // The focus goes with the person, to the record's head, where every wait stands.
    expect(document.activeElement).toHaveAttribute('data-record-head');
  });

  /**
   * WAITFOOT1c (D168 point 4): each wait leads to the event in the conversation that raised it, from the head and from the strip,
   * and the event leads back to its wait at the foot. A go-ahead's call is joined on its input, never its title; one whose call
   * the conversation does not hold has no such press.
   */
  it('goes between each wait, the event that raised it and back', async () => {
    const asked = '2026-09-02T00:30:00Z';
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GO_AHEAD') return { message: 'Go-ahead answered.', sent: true, reaches: 'resume' };
      if (type === 'SESSION_HISTORY') {
        return {
          session: 'g0ah3ad0', earlier: false, latest: 5, events: [
            { seq: 1, at: asked, kind: 'user', origin: 'target', text: 'Your target is the quest…' },
            { seq: 2, at: asked, kind: 'message', id: 'm1', text: 'The tile needs its target set.' },
            {
              seq: 3, at: asked, kind: 'tool', id: 'g1', title: 'ask for a go-ahead', status: 'completed',
              input: JSON.stringify({ kind: 'write', on: 'production', act: 'dashboard configuration', why: 'The tile reads its target from it.' }),
            },
            { seq: 4, at: asked, kind: 'message', id: 'm2', text: 'I need go-aheads 1 and 2 before I can finish.' },
            { seq: 5, at: asked, kind: 'turn', stopReason: 'end_turn' },
          ],
        };
      }
      return DRIVER_STATE;
    });
    const view = show('g0ah3ad0');
    const strip = await screen.findByRole('region', { name: '3 waiting on you' });
    const call = await screen.findByText('ask for a go-ahead');

    // From the strip: go-ahead 1's call, outlined; go-ahead 2 asked by no call this conversation holds has no such press.
    const [first, second] = within(strip).getAllByRole('listitem');
    expect(within(second!).queryByRole('button', { name: 'In the conversation' })).toBeNull();
    await userEvent.click(within(first!).getByRole('button', { name: 'In the conversation' }));
    await waitFor(() => expect(call.closest('[data-block]')!.className).toContain('outline-accent'));

    // From the head: the park's card, to its last words.
    const card = screen.getByText('This one is waiting on you').closest('section')!;
    await userEvent.click(within(card).getByRole('button', { name: 'In the conversation' }));
    const words = screen.getAllByText('I need go-aheads 1 and 2 before I can finish.')
      .map((said) => said.closest<HTMLElement>('[data-block]')).find(Boolean)!;
    await waitFor(() => expect(words.className).toContain('outline-accent'));

    // And back: the call's mark names what waits from it, and *To the wait* leads to its line at the foot.
    const block = call.closest<HTMLElement>('[data-block]')!;
    await userEvent.click(within(block).getByRole('button', { name: 'To the wait: go-ahead #1' }));
    await waitFor(() => expect(within(strip).getAllByRole('listitem')[0]).toHaveFocus());

    // Away and back, the press is spent: the strip drawn again takes no focus and outlines nothing.
    const attend = (selected: string) => view.rerender(
      <QueryClientProvider client={view.client}>
        <Hub />
        <Tooltip.Provider><WorkFrame selected={selected} onSelect={view.onSelect} notify={() => {}} /></Tooltip.Provider>
      </QueryClientProvider>,
    );
    attend('n0th3r00');
    await waitFor(() => expect(screen.queryByRole('region', { name: '3 waiting on you' })).toBeNull());
    attend('g0ah3ad0');
    const again = await screen.findByRole('region', { name: '3 waiting on you' });
    expect(within(again).getAllByRole('listitem')[0]).not.toHaveFocus();
    expect(within(again).getAllByRole('listitem')[0]!.className).not.toContain('outline-accent');
  });

  // WAITFOOT2b (D168's WAITFOOT2 note): while the head's waits are on screen the strip stands aside, so *To the wait* leads there.
  it('leads a raising block\'s *To the wait* to the head\'s line while the head is on screen', async () => {
    const watched = watchScreen();
    try {
      const asked = '2026-09-02T00:30:00Z';
      invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_HISTORY' ? {
        session: 'g0ah3ad0', earlier: false, latest: 4, events: [
          { seq: 1, at: asked, kind: 'user', origin: 'target', text: 'Your target is the quest…' },
          {
            seq: 2, at: asked, kind: 'tool', id: 'g1', title: 'ask for a go-ahead', status: 'completed',
            input: JSON.stringify({ kind: 'write', on: 'production', act: 'dashboard configuration', why: 'The tile reads its target from it.' }),
          },
          { seq: 3, at: asked, kind: 'message', id: 'm2', text: 'I need go-aheads 1 and 2 before I can finish.' },
          { seq: 4, at: asked, kind: 'turn', stopReason: 'end_turn' },
        ],
      } : DRIVER_STATE));
      show('g0ah3ad0');
      const call = await screen.findByText('ask for a go-ahead');
      await waitFor(() => expect(watched.observers()).not.toHaveLength(0));
      watched.show(true);
      expect(screen.queryByRole('region', { name: '3 waiting on you' })).toBeNull();

      const block = call.closest<HTMLElement>('[data-block]')!;
      await userEvent.click(within(block).getByRole('button', { name: 'To the wait: go-ahead #1' }));
      const line = within(screen.getByRole('region', { name: 'Go-aheads it asked' })).getAllByRole('listitem')[0]!;
      await waitFor(() => expect(line).toHaveFocus());
      expect(line.className).toContain('outline-accent');
      // The strip stays aside: the line it would have led to is the one on screen.
      expect(screen.queryByRole('region', { name: '3 waiting on you' })).toBeNull();
    } finally {
      watched.restore();
    }
  });
});

/**
 * PROPOSEHERE1b (D168 point 2, D74): auto mode refused a driven session's local branch delete, and its `permission_propose`
 * waited on the Agents page and the band while the person kept asking it in its conversation. Its own waiting proposals are a
 * wait where they talk to it, in the head and at the foot; *Allow* settles it through the band's own door, then tells the
 * session it is allowed, so its next turn, composed with the rule, goes on. A session that takes no words is said so instead.
 */
describe('a session\'s rule proposal', () => {
  const RESTING = { ...DRIVEN, id: 'r3st1ng0', state: 'completed', updated: '2026-10-15T10:21:00Z' };
  const proposed = '2026-10-15T10:20:00Z';
  const MINE = {
    id: 'p0000007', state: 'waiting', action: 'add', scope: 'repository', name: 'engine', list: 'allow',
    rule: 'Bash(git branch -D:*)', why: 'Auto mode refused the local branch delete you asked for.', session: 'r3st1ng0', proposed,
  };
  const OTHERS = { ...MINE, id: 'p0000008', rule: 'Bash(git push:*)', session: 'o7h3r000' };
  let reach: Record<string, unknown> = { reaches: 'resume' };
  let proposals: Record<string, unknown>[] = [MINE, OTHERS];

  beforeEach(() => {
    SESSIONS = [RESTING];
    reach = { reaches: 'resume' };
    proposals = [MINE, OTHERS];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string, request?: { payload?: { id?: string; accept?: boolean } }) => {
      if (type === 'RULES') return { path: 'C:/somewhere/.daoris/permissions.json', defaults: [], scopes: [], proposals };
      if (type === 'RULE_PROPOSAL') {
        proposals = proposals.map((one) => (one.id === request?.payload?.id
          ? { ...one, state: request?.payload?.accept ? 'accepted' : 'declined' } : one));
        return { path: 'C:/somewhere/.daoris/permissions.json', defaults: [], scopes: [], proposals };
      }
      if (type === 'SESSION_QUEUE') return reach;
      if (type === 'SESSION_INPUT') return { sent: true, reaches: 'resume' };
      if (type === 'SESSION_HISTORY') {
        return {
          session: 'r3st1ng0', earlier: false, latest: 4, events: [
            { seq: 1, at: proposed, kind: 'user', text: 'Delete the merged branch and its worktree.' },
            {
              seq: 2, at: proposed, kind: 'tool', id: 't2', title: 'permission_propose', status: 'completed',
              input: JSON.stringify({ action: 'add', scope: 'repository', list: 'allow', rule: 'Bash(git branch -D:*)', why: MINE.why }),
            },
            { seq: 3, at: proposed, kind: 'message', id: 'm3', text: 'I proposed a rule; once it is allowed I can delete it.' },
            { seq: 4, at: proposed, kind: 'turn', stopReason: 'end_turn' },
          ],
        };
      }
      return DRIVER_STATE;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
  });

  const said = () => invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT');

  it('stands in its head and at the foot, and not another session\'s', async () => {
    show('r3st1ng0');
    const strip = await screen.findByRole('region', { name: 'Waiting on you' });
    expect(within(strip).getByRole('listitem')).toHaveTextContent('allow Bash(git branch -D:*) for repository engine');
    const head = screen.getByRole('region', { name: 'Rules it proposed' });
    expect(within(head).getAllByRole('listitem')).toHaveLength(1);
    expect(screen.queryByText(/Bash\(git push:\*\)/)).toBeNull();
  });

  it('allows it after asking once, then tells the session it is allowed, so its next turn goes on', async () => {
    const notify = vi.fn();
    show('r3st1ng0', notify);
    const item = within(await screen.findByRole('region', { name: 'Waiting on you' })).getByRole('listitem');

    await userEvent.click(within(item).getByRole('button', { name: 'Allow…' }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', expect.anything());
    await userEvent.click(within(item).getByRole('button', { name: 'Allow' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000007', accept: true } }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: {
        id: 'r3st1ng0',
        text: 'The rule you proposed is allowed now: allow Bash(git branch -D:*) for repository engine. Go on with what you needed it for.',
      },
    }));
    // Settled before it is said: the turn the words start is composed with the rule.
    const order = invoke.mock.calls.map(([, type]) => type).filter((type) => type === 'RULE_PROPOSAL' || type === 'SESSION_INPUT');
    expect(order).toEqual(['RULE_PROPOSAL', 'SESSION_INPUT']);
    expect(notify).toHaveBeenCalledWith('Allowed: allow Bash(git branch -D:*) for repository engine. Sessions started from now on are handed it.');
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Session r3st1ng0 is told the rule is allowed, and goes on.', 'ok'));
    // Settled, it waits no more.
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Rules it proposed' })).toBeNull());
  });

  it('declines it in one press, telling the session nothing', async () => {
    const notify = vi.fn();
    show('r3st1ng0', notify);
    const head = await screen.findByRole('region', { name: 'Rules it proposed' });
    await userEvent.click(within(head).getByRole('button', { name: 'Decline' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000007', accept: false } }));
    expect(notify).toHaveBeenCalledWith('Declined: allow Bash(git branch -D:*) for repository engine. The rules are as they were.');
    expect(said()).toEqual([]);
  });

  it('says on the card where the session takes no words, and allows it without telling it', async () => {
    reach = { sent: false, why: 'stood-down' };
    show('r3st1ng0');
    const item = within(await screen.findByRole('region', { name: 'Waiting on you' })).getByRole('listitem');
    await waitFor(() => expect(item).toHaveTextContent('It takes no words now, so it is not told'));

    await userEvent.click(within(item).getByRole('button', { name: 'Allow…' }));
    await userEvent.click(within(item).getByRole('button', { name: 'Allow' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000007', accept: true } }));
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Rules it proposed' })).toBeNull());
    expect(said()).toEqual([]);
  });

  it('leads between the proposal and the call that raised it', async () => {
    show('r3st1ng0');
    const strip = await screen.findByRole('region', { name: 'Waiting on you' });
    const call = await screen.findByText('permission_propose');
    const block = call.closest<HTMLElement>('[data-block]')!;
    const mark = await within(block).findByRole('button', { name: 'To the wait: a rule it proposed' });

    await userEvent.click(within(within(strip).getByRole('listitem')).getByRole('button', { name: 'In the conversation' }));
    await waitFor(() => expect(block.className).toContain('outline-accent'));
    await userEvent.click(mark);
    await waitFor(() => expect(within(strip).getByRole('listitem')).toHaveFocus());
  });
});
