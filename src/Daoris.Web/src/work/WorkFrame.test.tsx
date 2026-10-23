import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The Work frame as a PAGE, in the shape `shell.test.tsx` already uses: the bridge mocked as present, because the frame
// does not exist without one (D55). Everything a person does to a session happens here now — starting it, talking to it,
// ending it, and watching its console — which is what "one home for the stream" (design §3) means as a test file.
// WEBSPEED1 cut the one file, which ran 181 s alone, into parts that run beside each other: this part holds the frame
// itself, a park's go-aheads and a session's rule proposal; `WorkFrame.conversation`, `.words`, `.landed` and `.geometry`
// hold the rest, and what they share is in `test/workFrameBridge.ts` and `test/workFrameKit.tsx`.
vi.mock('@shenora/react', async () => (await import('../test/workFrameBridge')).shenoraMock());

import { watchScreen } from '../test/onScreen';
import { eventHandlers, invoke } from '../test/workFrameBridge';
import {
  CHAT, DRIVEN, DRIVER_STATE, Hub, PARKED, QUESTS, REGISTRY, carried, going, show, userEvent,
} from '../test/workFrameKit';
import { WorkFrame } from './WorkFrame';

let SESSIONS: unknown[] = [DRIVEN];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
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
   * TITLEONE1: one piece of work, one name. A line of work an ask began is named in its head as its row names it, by the ask's
   * short title, not by the quest's title cut from the ask's words.
   */
  it('names the head of a line of work an ask began as its row names it', async () => {
    const ask = { id: 'a5k0t1', sentence: 'Expose a streaming budget on the level loader please', short: 'Streaming budget', state: 'Published', quests: ['abc123'] };
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/asks')) return Response.json([ask]);
      return respond(url);
    }));
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_GROUPS'
      ? {
        sessions: [],
        entries: [{
          entry: `ask:${ask.id}/engine`, origin: `ask:${ask.id}`, repository: 'engine', group: 'working', leading: DRIVEN.id,
          parts: [{ session: DRIVEN.id, quest: DRIVEN.quest, shown: 'working', group: 'working', began: 'opened', after: null, askedBy: null }],
        }],
      }
      : DRIVER_STATE));
    show(DRIVEN.id);

    expect(await screen.findByRole('heading', { level: 1, name: 'Streaming budget' })).toBeInTheDocument();
    const list = await screen.findByRole('complementary', { name: 'Sessions' });
    expect(await within(list).findByText('Streaming budget')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeNull();
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

    // Folded to its line (HEADFIT1), opened in place on the press.
    const worked = await screen.findByRole('region', { name: 'Who it worked with' });
    expect(worked).toHaveTextContent('asked 1 of another repository');
    await userEvent.click(within(worked).getByRole('button', { name: 'Show who it worked with' }));
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

  /** The box's go-ahead lines, where the box answers the park (PARKVIEW1): each with its own *Approve* and *Refuse*. */
  const boxLines = async () => within(await screen.findByRole('region', { name: 'It asks you' })).getAllByRole('listitem');

  /**
   * PARKVIEW1 (the park's ask design §3): the park's box carries every press of what it asks, so the person answers in one place: the
   * head lists the go-aheads with none, and the strip draws none of them.
   */
  it('shows the go-aheads the park asked in its box, each with Approve and Refuse, the head listing them, and not another session\'s', async () => {
    show('g0ah3ad0');

    const items = await boxLines();
    expect(items.map((item) => item.textContent)).toEqual([
      expect.stringContaining('dashboard configuration'),
      expect.stringContaining('comparison report'),
    ]);
    expect(screen.getByRole('region', { name: 'It asks you' })).not.toHaveTextContent('the release branch');
    for (const item of items) {
      expect(within(item).getByRole('button', { name: 'Approve' })).toBeInTheDocument();
      expect(within(item).getByRole('button', { name: 'Refuse' })).toBeInTheDocument();
    }
    // The head lists them, saying where they are answered, with no press and no field of its own.
    const head = screen.getByRole('region', { name: 'Go-aheads it asked' });
    expect(within(head).getAllByRole('listitem')).toHaveLength(2);
    expect(within(head).queryByRole('button', { name: 'Approve' })).toBeNull();
    expect(within(head).queryByLabelText('your words, if any')).toBeNull();
    expect(head).toHaveTextContent('Each is answered in the box below, with your words there.');
    // The park's own card and box stay: with no question recorded, it is answered in words.
    expect(screen.getByText(/I need go-aheads 1 and 2/)).toBeInTheDocument();
    expect(screen.getByLabelText('Message')).toBeInTheDocument();
  });

  it('approves one with the box\'s words through one door, the words leaving the box, and says the session goes on', async () => {
    const notify = vi.fn();
    show('g0ah3ad0', notify);
    const second = (await boxLines())[1]!;
    const before = asksRead();

    await userEvent.type(screen.getByLabelText('Message'), 'dev first, then production');
    await userEvent.click(within(second).getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GO_AHEAD', {
      payload: { id: 'g0ah3ad0', ask: 'a5k001', number: 2, approved: true, words: 'dev first, then production' },
    }));
    expect(notify).toHaveBeenCalledWith("Approved go-ahead #2; the same session goes on with it at the driver's next look.");
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue(''));
    // One press: neither the ask's own door nor the box's was asked as well.
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());
    expect(vi.mocked(fetch)).not.toHaveBeenCalledWith('/api/asks/a5k001/go-aheads/2', expect.anything());
    // The ask is read again, so the go-ahead shows what became of it.
    await waitFor(() => expect(asksRead()).toBeGreaterThan(before));
  });

  it('refuses one with no words, sending none, and says the same session goes on with the refusal', async () => {
    const notify = vi.fn();
    show('g0ah3ad0', notify);
    const first = (await boxLines())[0]!;

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
    const first = (await boxLines())[0]!;

    await userEvent.click(within(first).getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Approved go-ahead #1. It still waits on you for go-ahead #2, and goes on once that is answered.',
    ));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());
    const second = (await boxLines())[1]!;
    expect(within(second).getByRole('button', { name: 'Approve' })).toBeInTheDocument();
  });

  it('says when the session was no longer waiting, so only the go-ahead was answered', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_GO_AHEAD'
      ? { message: 'Go-ahead answered.', sent: false }
      : DRIVER_STATE));
    const notify = vi.fn();
    show('g0ah3ad0', notify);
    const first = (await boxLines())[0]!;

    await userEvent.click(within(first).getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Answered go-ahead #1. The session was no longer waiting on you, so the ask keeps your answer for every later start.',
    ));
  });

  it('says a refused go-ahead in the service\'s words in the box (UXFIX2b3b), keeping its words and the go-aheads to answer', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type !== 'SESSION_GO_AHEAD') return DRIVER_STATE;
      throw Object.assign(new Error('fallback'), {
        code: 'DRIVER_REFUSED', parameters: { message: 'Ask `#a5k001` holds no go-ahead 2: it holds 1.' },
      });
    });
    const notify = vi.fn();
    show('g0ah3ad0', notify);
    const second = (await boxLines())[1]!;

    await userEvent.type(screen.getByLabelText('Message'), 'only the menu entry');
    await userEvent.click(within(second).getByRole('button', { name: 'Approve' }));

    // The box says a refusal word for word, as it says every other.
    expect(await screen.findByRole('alert')).toHaveTextContent('Ask `#a5k001` holds no go-ahead 2: it holds 1.');
    expect(notify).not.toHaveBeenCalled();
    expect(screen.getByLabelText('Message')).toHaveValue('only the menu entry');
    expect(within(screen.getByRole('region', { name: 'It asks you' })).getAllByRole('button', { name: 'Approve' })).toHaveLength(2);
    expect(within((await boxLines())[1]!).getByRole('button', { name: 'Approve' })).toBeEnabled();
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
   * WAITFOOT1b (D168) and PARKVIEW1 (the park's ask design §3): the person reads the conversation's foot, where the park's question
   * is, so what it asks stands there, in the box below the conversation's scroll, with every press of it; the strip, which counted
   * its question and drew its go-aheads beside the box's own field, draws none of the park's waits, whether or not the head's are on
   * screen, and the head lists the go-aheads with no press.
   */
  it('answers a go-ahead in the box at the foot through the same door, no strip beside it, whether or not the head is on screen', async () => {
    const watched = watchScreen();
    try {
      const notify = vi.fn();
      show('g0ah3ad0', notify);
      const ask = await screen.findByRole('region', { name: 'It asks you' });
      // Outside the conversation's scroll, above the box's field.
      expect(screen.getByRole('main').contains(ask)).toBe(false);
      expect(ask.compareDocumentPosition(screen.getByLabelText('Message'))).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
      const head = screen.getByRole('region', { name: 'Go-aheads it asked' });
      await waitFor(() => expect(watched.observers().flatMap((one) => one.targets)).toContain(head));
      for (const onScreen of [true, false]) {
        watched.show(onScreen);
        expect(screen.queryByRole('region', { name: /waiting on you$/ })).toBeNull();
        expect(screen.getAllByRole('button', { name: 'Approve' })).toHaveLength(2);
        expect(ask.contains(screen.getAllByRole('button', { name: 'Approve' })[0]!)).toBe(true);
      }

      const second = within(ask).getAllByRole('listitem')[1]!;
      await userEvent.click(within(second).getByRole('button', { name: 'Approve' }));

      await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GO_AHEAD', {
        payload: { id: 'g0ah3ad0', ask: 'a5k001', number: 2, approved: true },
      }));
      expect(notify).toHaveBeenCalledWith("Approved go-ahead #2; the same session goes on with it at the driver's next look.");
    } finally {
      watched.restore();
    }
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
    const ask = await screen.findByRole('region', { name: 'It asks you' });
    const call = await screen.findByText('ask for a go-ahead');

    // From the box: go-ahead 1's call, outlined; go-ahead 2 asked by no call this conversation holds has no such press.
    const [first, second] = within(ask).getAllByRole('listitem');
    expect(within(second!).queryByRole('button', { name: 'In the conversation' })).toBeNull();
    await userEvent.click(within(first!).getByRole('button', { name: 'In the conversation' }));
    await waitFor(() => expect(call.closest('[data-block]')!.className).toContain('outline-accent'));

    // From the head: the park's card, to its last words.
    const card = screen.getByText('This one is waiting on you').closest('section')!;
    await userEvent.click(within(card).getByRole('button', { name: 'In the conversation' }));
    const words = screen.getAllByText('I need go-aheads 1 and 2 before I can finish.')
      .map((said) => said.closest<HTMLElement>('[data-block]')).find(Boolean)!;
    await waitFor(() => expect(words.className).toContain('outline-accent'));

    // And back: the call's mark names what waits from it, and *To the wait* leads to its line in the box (PARKVIEW1).
    const block = call.closest<HTMLElement>('[data-block]')!;
    await userEvent.click(within(block).getByRole('button', { name: 'To the wait: go-ahead #1' }));
    await waitFor(() => expect(within(ask).getAllByRole('listitem')[0]).toHaveFocus());

    // Away and back, the press is spent: the box drawn again takes no focus and outlines nothing.
    const attend = (selected: string) => view.rerender(
      <QueryClientProvider client={view.client}>
        <Hub />
        <Tooltip.Provider><WorkFrame selected={selected} onSelect={view.onSelect} notify={() => {}} /></Tooltip.Provider>
      </QueryClientProvider>,
    );
    attend('n0th3r00');
    await waitFor(() => expect(screen.queryByRole('region', { name: 'It asks you' })).toBeNull());
    attend('g0ah3ad0');
    const again = await screen.findByRole('region', { name: 'It asks you' });
    expect(within(again).getAllByRole('listitem')[0]).not.toHaveFocus();
    expect(within(again).getAllByRole('listitem')[0]!.className).not.toContain('outline-accent');
  });

  // PARKVIEW1: the box that answers a park is always drawn, so *To the wait* leads to its line there, the head on screen or not.
  it('leads a raising block\'s *To the wait* to the box\'s line while the head is on screen', async () => {
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
      expect(screen.queryByRole('region', { name: /waiting on you$/ })).toBeNull();

      const block = call.closest<HTMLElement>('[data-block]')!;
      await userEvent.click(within(block).getByRole('button', { name: 'To the wait: go-ahead #1' }));
      const line = within(screen.getByRole('region', { name: 'It asks you' })).getAllByRole('listitem')[0]!;
      await waitFor(() => expect(line).toHaveFocus());
      expect(line.className).toContain('outline-accent');
      // No strip stands for the park's waits: the box holds them.
      expect(screen.queryByRole('region', { name: /waiting on you$/ })).toBeNull();
    } finally {
      watched.restore();
    }
  });
});

/**
 * PARKVIEW1 (the park's ask design §2-§4): a session parked on a production go-ahead and a question its agent asked through
 * `question_ask`. What it asks stands in its box, every answer a press the box's words go with, each reaching the session through
 * a door the box already uses: a choice its label with the question's id, the free answer as before, and a go-ahead through the ask's
 * door, kept from sending the park on while its question waits.
 */
describe('a park\'s question', () => {
  const QUESTION = {
    id: 'q5t10n00', question: 'Which quarterly target should the dashboard tile show?', at: '2026-09-02T00:45:00Z', recommend: 1,
    by: 'before Friday\'s release',
    choices: [{ label: '1,200', does: 'Write 1,200, the planning sheet\'s value.' }, { label: '1,350', does: 'Write 1,350.' }],
  };
  let ASKS: unknown[] = [];
  let answered: Record<string, unknown> = { sent: true, reaches: 'resume' };

  beforeEach(() => {
    SESSIONS = [PARKED_ASKING];
    ASKS = [{ ...ASK_WITH_GO_AHEADS, goAheads: [] }];
    answered = { sent: true, reaches: 'resume' };
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/asks')) return Response.json(ASKS);
      if (url.startsWith('/api/quests')) return Response.json([...QUESTS, ASKED_QUEST]);
      return respond(url);
    }));
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUESTION') return { question: QUESTION };
      if (type === 'SESSION_INPUT') return answered;
      if (type === 'SESSION_GO_AHEAD') return { message: 'Go-ahead 1 on ask `#a5k001` is approved.', sent: false, waits: true };
      return DRIVER_STATE;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    window.localStorage.removeItem('daoris.drafts');
  });

  it('shows its question in the box with its answers as rows to select, and in the head\'s card in place of its last message', async () => {
    show('g0ah3ad0');

    const ask = await screen.findByRole('region', { name: 'It asks you' });
    expect(await within(ask).findByText('Which quarterly target should the dashboard tile show?')).toBeInTheDocument();
    expect(within(ask).getByRole('radio', { name: /^1,200 recommended/ })).not.toBeChecked();
    expect(within(ask).queryByRole('button', { name: /1,200/ })).toBeNull();
    expect(within(ask).getByText('If you choose this: Write 1,350.')).toBeInTheDocument();
    expect(within(ask).getByText('It says it needs your answer by: before Friday\'s release')).toBeInTheDocument();
    expect(within(ask).getByText(/^Meanwhile its quest #q4sk00 stays taken/)).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUESTION', { payload: { id: 'g0ah3ad0' } });
    // The head's card says the question, with no press of its own: the box below holds them.
    const card = screen.getByText('This one is waiting on you').closest('section')!;
    expect(within(card).getByText('Which quarterly target should the dashboard tile show?')).toBeInTheDocument();
    expect(within(card).queryByRole('button', { name: '1,200' })).toBeNull();
    expect(within(card).queryByRole('radio')).toBeNull();
    expect(within(card).queryByText(/I need go-aheads 1 and 2/)).toBeNull();
  });

  /** Each SESSION_INPUT the page sent, by its payload. */
  const inputs = () => invoke.mock.calls
    .filter(([, type]) => type === 'SESSION_INPUT')
    .map(([, , request]) => (request as { payload: unknown }).payload);

  /** QUESTIONUI1a (the supervisor design §8.4): a row selected, then the box's own *Send* sends it with the box's words, once. */
  it('sends the selected answer with the box\'s words through the box\'s one *Send*, once, naming the question', async () => {
    let settle: (answer: unknown) => void = () => {};
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_QUESTION') return { question: QUESTION };
      if (type === 'SESSION_INPUT') return new Promise((resolve) => { settle = resolve; });
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('g0ah3ad0', notify);
    const ask = await screen.findByRole('region', { name: 'It asks you' });
    await within(ask).findByText('Which quarterly target should the dashboard tile show?');

    await userEvent.click(within(ask).getByRole('radio', { name: /^1,350/ }));
    expect(screen.getByLabelText('Message')).toHaveAttribute('placeholder', 'your words, sent with “1,350” — or send it alone');
    await userEvent.type(screen.getByLabelText('Message'), 'but only on the dashboard');
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));

    await waitFor(() => expect(inputs()).toEqual([{ id: 'g0ah3ad0', text: '1,350\n\nbut only on the dashboard', question: 'q5t10n00' }]));
    // While it is on its way nothing sends again, by the press or by Enter.
    expect(screen.getByRole('button', { name: 'Carry on with this answer' })).toBeDisabled();
    await userEvent.type(screen.getByLabelText('Message'), '{Enter}');
    expect(inputs()).toHaveLength(1);

    settle({ sent: true, reaches: 'resume' });
    await waitFor(() => expect(notify).toHaveBeenCalledWith("Answered “1,350”: the same session goes on with it at the driver's next look."));
    expect(screen.getByLabelText('Message')).toHaveValue('');
    await waitFor(() => expect(within(ask).getByRole('radio', { name: /^1,350/ })).not.toBeChecked());
    expect(inputs()).toHaveLength(1);
  });

  it('sends the selected answer alone on Enter in the empty box, and the words alone once the selection is cleared', async () => {
    show('g0ah3ad0');
    const ask = await screen.findByRole('region', { name: 'It asks you' });
    await within(ask).findByText('Which quarterly target should the dashboard tile show?');

    expect(screen.getByRole('button', { name: 'Carry on with this answer' })).toBeDisabled();
    await userEvent.click(within(ask).getByRole('radio', { name: /^1,200/ }));
    await userEvent.type(screen.getByLabelText('Message'), '{Enter}');
    await waitFor(() => expect(inputs()).toEqual([{ id: 'g0ah3ad0', text: '1,200', question: 'q5t10n00' }]));

    await userEvent.click(within(ask).getByRole('radio', { name: /^1,350/ }));
    await userEvent.click(within(ask).getByRole('button', { name: 'Clear choice' }));
    await userEvent.type(screen.getByLabelText('Message'), 'Neither yet.{Enter}');
    await waitFor(() => expect(inputs()).toEqual([
      { id: 'g0ah3ad0', text: '1,200', question: 'q5t10n00' },
      { id: 'g0ah3ad0', text: 'Neither yet.' },
    ]));
  });

  /** PARKANSWER1, QUESTIONUI1a: the head's card selects a row once its answer is opened, and its one *Send* combines it with its words. */
  it('answers the question from the head\'s card, its *Send* combining the selected answer with the card\'s words, once', async () => {
    show('g0ah3ad0');
    const card = (await screen.findByText('This one is waiting on you')).closest('section')!;
    await within(card).findByText('Which quarterly target should the dashboard tile show?');

    await userEvent.click(within(card).getByRole('button', { name: 'Answer and carry on…' }));
    await userEvent.click(within(card).getByRole('radio', { name: /^1,200/ }));
    await userEvent.type(within(card).getByLabelText('your answer — the same session goes on with these words'), 'tile only');
    await userEvent.click(within(card).getByRole('button', { name: 'Carry on with this answer' }));

    await waitFor(() => expect(inputs()).toEqual([{ id: 'g0ah3ad0', text: '1,200\n\ntile only', question: 'q5t10n00' }]));
  });

  it('sends the head card\'s words alone where no answer is selected, naming no question', async () => {
    show('g0ah3ad0');
    const card = (await screen.findByText('This one is waiting on you')).closest('section')!;
    await within(card).findByText('Which quarterly target should the dashboard tile show?');

    await userEvent.click(within(card).getByRole('button', { name: 'Answer and carry on…' }));
    await userEvent.type(within(card).getByLabelText('your answer — the same session goes on with these words'), 'Hold the tile.');
    await userEvent.click(within(card).getByRole('button', { name: 'Carry on with this answer' }));

    await waitFor(() => expect(inputs()).toEqual([{ id: 'g0ah3ad0', text: 'Hold the tile.' }]));
  });

  it('says a refusal in the box, keeping the words and the selection, and a question that changed, keeping the words', async () => {
    answered = { sent: false, why: 'not-listening' };
    const notify = vi.fn();
    show('g0ah3ad0', notify);
    const ask = await screen.findByRole('region', { name: 'It asks you' });
    await within(ask).findByText('Which quarterly target should the dashboard tile show?');

    await userEvent.click(within(ask).getByRole('radio', { name: /^1,200/ }));
    await userEvent.type(screen.getByLabelText('Message'), 'and say so');
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));
    await screen.findByRole('alert');
    expect(screen.getByLabelText('Message')).toHaveValue('and say so');
    expect(within(ask).getByRole('radio', { name: /^1,200/ })).toBeChecked();

    answered = { sent: false, why: 'question-changed' };
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(
      'Its question changed since this page read it: read the new one and answer it. Nothing was sent.',
    ));
    expect(screen.getByLabelText('Message')).toHaveValue('and say so');
    expect(notify).not.toHaveBeenCalled();
    expect(inputs()).toHaveLength(2);
  });

  it('refuses in the head\'s card while the go-aheads are open, a selected answer and words alone alike, keeping both', async () => {
    ASKS = [{ ...ASK_WITH_GO_AHEADS, goAheads: [ASK_WITH_GO_AHEADS.goAheads[0]] }];
    show('g0ah3ad0');
    const card = (await screen.findByText('This one is waiting on you')).closest('section')!;
    await within(card).findByText('Which quarterly target should the dashboard tile show?');
    await within(await screen.findByRole('region', { name: 'It asks you' })).findByText('write on production');

    await userEvent.click(within(card).getByRole('button', { name: 'Answer and carry on…' }));
    const words = within(card).getByLabelText('your answer — the same session goes on with these words');
    await userEvent.type(words, 'tile only');
    await userEvent.click(within(card).getByRole('radio', { name: /^1,200/ }));
    await userEvent.click(within(card).getByRole('button', { name: 'Carry on with this answer' }));
    expect(await within(card).findByRole('status')).toHaveTextContent('Answer its go-aheads above first: then this answer sends it on with them.');
    expect(words).toHaveValue('tile only');
    expect(within(card).getByRole('radio', { name: /^1,200/ })).toBeChecked();

    await userEvent.click(within(card).getByRole('button', { name: 'Clear choice' }));
    await userEvent.click(within(card).getByRole('button', { name: 'Carry on with this answer' }));
    expect(within(card).getByRole('status')).toHaveTextContent('Answer its go-aheads above first: then this answer sends it on with them.');
    expect(words).toHaveValue('tile only');
    expect(inputs()).toEqual([]);
  });

  it('sends the free answer through the box as before, naming no question', async () => {
    show('g0ah3ad0');
    const ask = await screen.findByRole('region', { name: 'It asks you' });
    await within(ask).findByText('Which quarterly target should the dashboard tile show?');

    await userEvent.type(screen.getByLabelText('Message'), 'Neither: hold the tile until the review.');
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 'g0ah3ad0', text: 'Neither: hold the tile until the review.' },
    }));
  });

  /**
   * The owner's case, in neutral words: parked on a production go-ahead beside its question, with the person's words in the box. The
   * go-ahead comes first and takes the words, keeping the park for the question (`goesOn: false`); an answer selected and the free
   * answer wait for it alike, through *Send* and Enter (Codex's round-2 finding 14), said so, and nothing goes to the park meanwhile.
   */
  it('answers a production go-ahead first with the box\'s words, keeping the park for its question, whose answers wait for it', async () => {
    ASKS = [{ ...ASK_WITH_GO_AHEADS, goAheads: [ASK_WITH_GO_AHEADS.goAheads[0]] }];
    const notify = vi.fn();
    show('g0ah3ad0', notify);
    const ask = await screen.findByRole('region', { name: 'It asks you' });
    await within(ask).findByText('write on production');
    expect(within(ask).getByText(
      'Answer its go-aheads first: each keeps it waiting for its question, whose answer then sends it on with them.',
    )).toBeInTheDocument();

    // A selected answer, by *Send* and by Enter: refused, the selection kept.
    await userEvent.click(within(ask).getByRole('radio', { name: /^1,200/ }));
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Answer its go-aheads above first: then this answer sends it on with them.');
    await userEvent.type(screen.getByLabelText('Message'), '{Enter}');
    expect(within(ask).getByRole('radio', { name: /^1,200/ })).toBeChecked();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());

    // Words alone, by *Send* and by Enter: refused, the words kept.
    await userEvent.click(within(ask).getByRole('button', { name: 'Clear choice' }));
    await userEvent.type(screen.getByLabelText('Message'), 'only the menu entry, not the report');
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));
    await userEvent.type(screen.getByLabelText('Message'), '{Enter}');
    expect(screen.getByRole('alert')).toHaveTextContent('Answer its go-aheads above first: then this answer sends it on with them.');
    expect(screen.getByLabelText('Message')).toHaveValue('only the menu entry, not the report');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());

    await userEvent.click(within(within(ask).getByText('write on production').closest('li')!).getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GO_AHEAD', {
      payload: { id: 'g0ah3ad0', ask: 'a5k001', number: 1, approved: true, words: 'only the menu entry, not the report', goesOn: false },
    }));
    expect(notify).toHaveBeenCalledWith('Approved go-ahead #1. It still waits on you: answer it below to send it on.');
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue(''));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.anything());
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
