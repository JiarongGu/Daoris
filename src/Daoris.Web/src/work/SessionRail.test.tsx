import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// An ORGANISM, so this is the layer that holds the hooks and the layer a mocked bridge is for
// (components plan §2). The bridge is mocked as present, because the Work frame is desktop-only
// (D55) and the group header's driver facts exist nowhere else; one case takes it away, as a browser has none.

const { invoke, notifyReady, bridge } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  bridge: { available: true },
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => bridge.available,
  getBridge: () => ({ isAvailable: bridge.available, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: bridge.available, bridge: { notifyReady } }),
  useShenoraEvent: () => {},
}));

import type { OpenGroup } from '../opener';
import { SessionRail } from './SessionRail';

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const DRIVER_STATE = {
  drivable: ['engine'],
  holds: ['tools'],
  trees: ['engine'],
  running: ['s1a2b3c4'],
};

const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 4, root: 'C:/somewhere/engine' },
  { repository: 'tools', adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 2, root: 'C:/somewhere/tools' },
  { repository: 'sandbox', adopted: false, registered: true, owns: [], accepts: [], packs: [], entries: 0, root: 'C:/somewhere/sandbox' },
];

const QUESTS = [{
  id: '7a82cc', from: 'platform', to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs a per-frame cap.',
  status: 'Taken', filed: at(4000), updated: at(200),
}];

let SESSIONS: unknown[] = [];

const base = {
  adapter: 'claude-code', kind: 'driven', created: at(120), updated: at(4),
};

const LIVE = [
  { ...base, id: 's1a2b3c4', quest: '7a82cc', repository: 'engine', state: 'working', tree: 'C:/somewhere/.daoris/trees/default/engine/streaming-budget' },
  { ...base, id: 'b2c3d4e5', quest: null, kind: 'chat', repository: 'tools', state: 'awaiting-person', created: at(30), updated: at(2) },
  { ...base, id: 'c3d4e5f6', quest: null, kind: 'chat', repository: 'tools', state: 'working', created: at(9), updated: at(1) },
  { ...base, id: 'd4e5f6a7', quest: null, repository: 'engine', state: 'completed', created: at(300), updated: at(240) },
];

/** The driver's one reader on LIVE (SESSUX1a), in its order: what waits on you, what runs, what ended. */
let GROUPS: unknown[] = [];
const LIVE_GROUPS = [
  { session: 'b2c3d4e5', group: 'you', shown: 'awaiting-person', archived: false, teammate: false },
  { session: 's1a2b3c4', group: 'working', shown: 'working', archived: false, teammate: false },
  { session: 'c3d4e5f6', group: 'working', shown: 'working', archived: false, teammate: false },
  { session: 'd4e5f6a7', group: 'ended', shown: 'completed', archived: false, teammate: false },
];

/** The bridge's answer to each call: the reader's groups, and the driver's state for everything else. */
const drive = async (_module: string, type: string) => (type === 'SESSION_GROUPS' ? { sessions: GROUPS } : DRIVER_STATE);

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

/** The session rows in a region — its buttons less each row's menu trigger (RAIL1). */
const rows = (region: HTMLElement) => within(region).getAllByRole('button')
  .filter((button) => !button.getAttribute('aria-label')?.startsWith('more for'));

function show(node: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{node}</QueryClientProvider>);
}

describe('the session rail', () => {
  beforeEach(() => {
    SESSIONS = LIVE;
    GROUPS = LIVE_GROUPS;
    bridge.available = true;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(drive);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('groups what is running by repository, in a stable order', async () => {
    show(<SessionRail notify={() => {}} arrangement="repository" />);

    // The repository groups' headings — not the ended section's, which is a peer of the groups
    // rather than one of them, and lists what is no longer running.
    const ended = await screen.findByRole('region', { name: 'Ended' });
    const headings = screen.getAllByRole('heading', { level: 3 })
      .filter((heading) => !ended.contains(heading));
    expect(headings.map((heading) => heading.textContent)).toEqual(['engine', 'tools']);
  });

  /**
   * FRAME6: closed to its 56px strip, the rail keeps every running session one press away — its
   * repository's initial and its mark, with the title, the repository and the state as its name, since
   * a strip has no room for the words and a mark is never hue alone (D41 §6). What ended is left to
   * the open rail; the attended session stays, whatever state it reached. By state (D126 §2.5), what
   * waits on the person comes first, then what runs, in the open list's order.
   */
  it('keeps every running session one press away as a strip, what waits on you first', async () => {
    const select = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <SessionRail notify={() => {}} compact selected="c3d4e5f6" onSelect={select} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    const strip = await screen.findByRole('navigation', { name: 'Sessions' });
    await waitFor(() => expect(within(strip).getAllByRole('button')[0]).toHaveAccessibleName('Chat · tools · waiting on you'));
    const rows = within(strip).getAllByRole('button');
    expect(rows.map((row) => row.getAttribute('aria-label'))).toEqual([
      'Chat · tools · waiting on you',
      'Expose a streaming budget on the chunk API · engine · working',
      'Chat · tools · working',
    ]);
    expect(rows.map((row) => row.textContent)).toEqual(['t', 'e', 't']);
    expect(rows[2]).toHaveAttribute('aria-current', 'true');

    await userEvent.click(rows[0]!);
    expect(select).toHaveBeenCalledWith('b2c3d4e5');
  });

  /**
   * RAIL1: a conversation is named by the first thing the person said, from this machine's record over
   * the bridge; searching finds sessions by name at once and by what was said once a word is typed,
   * with the words marked; and each row's menu does what it says.
   */
  describe('names, search and the row menu (RAIL1)', () => {
    const answer = (extra: (type: string, payload: Record<string, unknown>) => unknown) =>
      invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: Record<string, unknown> }) =>
        extra(type, options?.payload ?? {}) ?? drive(_module, type));
    const rail = (props: Partial<Parameters<typeof SessionRail>[0]> = {}) => {
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      return render(
        <QueryClientProvider client={client}>
          <Tooltip.Provider><SessionRail notify={() => {}} {...props} /></Tooltip.Provider>
        </QueryClientProvider>,
      );
    };

    it('names a conversation by what was first said in it', async () => {
      answer((type) => (type === 'SESSION_OPENINGS'
        ? { openings: { b2c3d4e5: 'Cap the hydration per frame', c3d4e5f6: 'Read README.md and tell me its first heading' } }
        : undefined));
      rail();

      expect(await screen.findByText('Cap the hydration per frame')).toBeInTheDocument();
      expect(screen.getByText('Read README.md and tell me its first heading')).toBeInTheDocument();
      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_OPENINGS', {
        payload: { ids: ['b2c3d4e5', 'c3d4e5f6', 'd4e5f6a7', 's1a2b3c4'] },
      });
    });

    /**
     * LOOK2b: the rail said *in s-2394e5d9* of a session whose landing had tidied that tree away. Where each row's work
     * is now is asked once for the rows the rail shows, naming the tree each record holds, and a landed row says where
     * the work landed.
     */
    it('says where a landed session’s work went, asked once for the rows the rail shows', async () => {
      const tidied = 'C:/somewhere/.daoris/trees/default/engine/s-2394e5d9';
      SESSIONS = [...LIVE, {
        ...base, id: 'e5f6a7b8', quest: '7a82cc', repository: 'engine', state: 'completed',
        created: at(400), updated: at(300), tree: tidied,
      }];
      answer((type) => (type === 'SESSION_WHERE'
        ? { sessions: [{ session: 'e5f6a7b8', treeGone: true, landed: { repository: 'engine', branch: 'feature/7a82cc-streaming-budget', state: 'standing' } }] }
        : undefined));
      rail();

      expect(await screen.findByText(/landed on feature\/7a82cc-streaming-budget/)).toBeInTheDocument();
      expect(screen.queryByText(/in s-2394e5d9/)).toBeNull();
      // The live session's own tree is still named: nothing was said of it but that it is there.
      expect(screen.getByText(/in streaming-budget/)).toBeInTheDocument();
      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_WHERE', {
        payload: {
          sessions: [
            { id: 'e5f6a7b8', tree: tidied },
            { id: 's1a2b3c4', tree: 'C:/somewhere/.daoris/trees/default/engine/streaming-budget' },
          ],
        },
      });
    });

    it('finds sessions by name at once, and by what was said with the words marked', async () => {
      const select = vi.fn();
      answer((type, payload) => {
        if (type === 'SESSION_OPENINGS') return { openings: { b2c3d4e5: 'Cap the hydration per frame' } };
        if (type === 'SESSION_SEARCH' && payload.q === 'streamer') {
          return { query: 'streamer', cut: false, hits: [{ session: 'c3d4e5f6', seq: 4, kind: 'message', snippet: '…belongs in the streamer, not the loader.' }] };
        }
        return undefined;
      });
      rail({ onSelect: select });
      await screen.findByText('Cap the hydration per frame');

      await userEvent.type(screen.getByRole('searchbox', { name: 'search sessions' }), 'hydration');
      const byName = await screen.findByRole('region', { name: 'By name' });
      expect(within(byName).getByText('Cap the hydration per frame')).toBeInTheDocument();
      expect(screen.queryByRole('heading', { name: 'Working (2)' })).toBeNull();

      await userEvent.clear(screen.getByRole('searchbox', { name: 'search sessions' }));
      await userEvent.type(screen.getByRole('searchbox', { name: 'search sessions' }), 'streamer');
      const said = await screen.findByRole('region', { name: 'In what was said' });
      expect(within(said).getByText('streamer').tagName).toBe('MARK');
      // Its tint adds no space beside the word (UX5 U61).
      expect(within(said).getByText('streamer')).toHaveClass('-mx-0.5');
      await userEvent.click(within(said).getByRole('button', { name: /…belongs in the streamer/ }));
      expect(select).toHaveBeenCalledWith('c3d4e5f6');

      await userEvent.type(screen.getByRole('searchbox', { name: 'search sessions' }), '{Escape}');
      expect(await screen.findByRole('heading', { name: 'Working (2)' })).toBeInTheDocument();
    });

    it('searches by one Han character, and asks nothing of one other letter (RAILSRCH1b)', async () => {
      answer((type, payload) => {
        if (type === 'SESSION_OPENINGS') return { openings: {} };
        if (type === 'SESSION_SEARCH' && payload.q === '区') {
          return { query: '区', cut: false, hits: [{ session: 'c3d4e5f6', seq: 4, kind: 'message', snippet: '…the 区 holds the streamer.' }] };
        }
        return undefined;
      });
      rail({});
      await screen.findByText('Expose a streaming budget on the chunk API');
      const box = screen.getByRole('searchbox', { name: 'search sessions' });

      await userEvent.type(box, 'g');
      await new Promise((done) => setTimeout(done, 400));
      expect(invoke.mock.calls.some((call) => call[1] === 'SESSION_SEARCH')).toBe(false);

      await userEvent.clear(box);
      await userEvent.type(box, '区');
      const said = await screen.findByRole('region', { name: 'In what was said' });
      expect(within(said).getByText('区').tagName).toBe('MARK');
    });

    it('opens a session in its own window from its row\'s menu', async () => {
      answer(() => undefined);
      rail({ doors: { review: () => {} } });
      await screen.findByText('Expose a streaming budget on the chunk API');

      const user = userEvent.setup();
      screen.getByRole('button', { name: 'more for Expose a streaming budget on the chunk API' }).focus();
      await user.keyboard('{Enter}');
      await user.click(screen.getByRole('menuitem', { name: 'Open in its own window' }));

      await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.WINDOWS', 'OPEN', { payload: { name: 'session:s1a2b3c4' } }));
    });

    /**
     * SESSUX1d, D126 §3.1: a running driven session's row offers its acts where its row is, as the one rule offers them;
     * what only the frame can do (ask to stop it, open a terminal in its tree) goes to the frame's door with the session.
     */
    it('offers a running session its acts on its row, and hands the frame what is the frame’s', async () => {
      answer(() => undefined);
      const stop = vi.fn();
      const terminal = vi.fn();
      rail({ doors: { stop, terminal, review: () => {}, answer: () => {} } });
      await screen.findByText('Expose a streaming budget on the chunk API');

      const user = userEvent.setup();
      screen.getByRole('button', { name: 'more for Expose a streaming budget on the chunk API' }).focus();
      await user.keyboard('{Enter}');
      expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual([
        'Stop…', 'Review', 'Open folder', 'Open a terminal here', 'Open in its own window', 'Copy session ID',
      ]);
      await user.click(screen.getByRole('menuitem', { name: 'Stop…' }));
      expect(stop).toHaveBeenCalledWith('s1a2b3c4');

      screen.getByRole('button', { name: 'more for Expose a streaming budget on the chunk API' }).focus();
      await user.keyboard('{Enter}');
      await user.click(screen.getByRole('menuitem', { name: 'Open a terminal here' }));
      expect(terminal).toHaveBeenCalledWith('C:/somewhere/.daoris/trees/default/engine/streaming-budget');
      // Nothing was stopped on the first press: the frame asks.
      expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', expect.anything());
    });

    it('offers no act the frame has not handed a door for', async () => {
      answer(() => undefined);
      rail();
      await screen.findByText('Expose a streaming budget on the chunk API');

      const user = userEvent.setup();
      screen.getByRole('button', { name: 'more for Expose a streaming budget on the chunk API' }).focus();
      await user.keyboard('{Enter}');
      expect(screen.getAllByRole('menuitem').map((item) => item.textContent))
        .toEqual(['Open folder', 'Open in its own window', 'Copy session ID']);
    });
  });

  it('carries the machine\'s standing choices on the header, where the repository\'s facts live', async () => {
    show(<SessionRail notify={() => {}} arrangement="repository" />);

    await screen.findByText('engine');
    expect(screen.getByText('drives here')).toBeInTheDocument();
    expect(screen.getByText('held')).toBeInTheDocument();
  });

  /** D51: the tree is the unit of exclusion, so "busy" is a question about a tree, not a repository. */
  it('names the tree an active session is holding', async () => {
    show(<SessionRail notify={() => {}} arrangement="repository" />);
    expect(await screen.findByText('busy · streaming-budget')).toBeInTheDocument();
  });

  /**
   * INT4g: a parked intake has asked and ended — no process, so it holds no room (INT4b: "the room's
   * lock is the process"). Its group claims nothing busy; a parked REPOSITORY session still does,
   * because it holds its working tree.
   */
  it('claims no busy room for a parked intake', async () => {
    SESSIONS = [{
      ...base, id: 'i9n8t7k6', quest: null, kind: 'chat', repository: 'ask #0fda18', ask: '0fda18',
      state: 'awaiting-person',
    }];
    show(<SessionRail notify={() => {}} arrangement="repository" />);

    const group = (await screen.findByText('ask #0fda18')).closest('section')!;
    expect(within(group).queryByText('busy')).toBeNull();
  });

  /** HELP1a: Ask Daoris's conversations group under its own name, never the record's `daoris:help`. */
  it('names Ask Daoris\'s group by its name, not by the repository its records are kept in', async () => {
    SESSIONS = [{ ...base, id: 'h1e1p000', quest: null, kind: 'chat', repository: 'daoris:help', state: 'working' }];
    show(<SessionRail notify={() => {}} arrangement="repository" />);

    expect(await screen.findByRole('heading', { name: 'Ask Daoris' })).toBeInTheDocument();
    expect(screen.queryByText('daoris:help')).toBeNull();
  });

  it('says busy without a name when the session holds the registered root', async () => {
    show(<SessionRail notify={() => {}} arrangement="repository" />);

    const tools = (await screen.findByText('tools')).closest('section')!;
    expect(within(tools).getByText('busy')).toBeInTheDocument();
  });

  /**
   * 🔴 Rewritten after the deployed application showed four empty-state sentences on a machine with
   * four real session records (D62). This test used to assert that a finished record was absent and
   * "left to the views that review them" — and there was no such view: the rail was the only door
   * to a session, and it listed live ones only, so after a restart the driver's own record was
   * reachable exactly never. The working-surface design's §7 promises the opposite.
   *
   * The live groups are unchanged — engine still holds one button — and the finished record is
   * listed beneath them, not among them, because the group headers carry live facts an ended
   * session has none of.
   */
  it('lists what is still running in the groups, and what ended beneath them', async () => {
    show(<SessionRail notify={() => {}} arrangement="repository" />);

    await screen.findByText('engine');
    // engine: the one live session. tools: two. The completed one is not in either group…
    const engine = screen.getByText('engine').closest('section')!;
    expect(rows(engine)).toHaveLength(1);
    // …it is in the ended section, reachable from a fresh window with nothing selected.
    const ended = screen.getByRole('region', { name: 'Ended' });
    expect(within(ended).getByText('completed')).toBeInTheDocument();
    expect(rows(ended)).toHaveLength(1);
  });

  it('opens an ended session when it is chosen, like any other', async () => {
    const onSelect = vi.fn();
    show(<SessionRail notify={() => {}} onSelect={onSelect} arrangement="repository" />);

    const ended = await screen.findByRole('region', { name: 'Ended' });
    await userEvent.click(rows(ended)[0]!);
    expect(onSelect).toHaveBeenCalledWith('d4e5f6a7');
  });

  /** A machine with no live session but an ended one is not empty, and must not say it is. */
  it('does not show the full empty state while there is a record to read', async () => {
    SESSIONS = LIVE.filter((s) => (s as { state: string }).state === 'completed');
    show(<SessionRail notify={() => {}} arrangement="repository" />);

    await screen.findByRole('region', { name: 'Ended' });
    expect(screen.queryByText(/Sessions appear here/)).not.toBeInTheDocument();
    expect(screen.getByText('Nothing is running')).toBeInTheDocument();
  });

  /**
   * A session that finishes while you are reading it must not vanish out from under you — the rail
   * is where attention lives, and losing the attended session on a state change is the one thing a
   * list of running things must not do.
   */
  it('keeps the attended session listed after it ends', async () => {
    show(<SessionRail selected="d4e5f6a7" notify={() => {}} arrangement="repository" />);

    expect(await screen.findByText('completed')).toBeInTheDocument();
    const engine = screen.getByText('engine').closest('section')!;
    expect(rows(engine)).toHaveLength(2);
  });

  it('puts the session that needs a person at the top of its group', async () => {
    show(<SessionRail notify={() => {}} arrangement="repository" />);

    const tools = (await screen.findByText('tools')).closest('section')!;
    expect(within(rows(tools)[0]!).getByText('waiting on you')).toBeInTheDocument();
  });

  it('marks the attended row and reports a choice, holding neither itself', async () => {
    const select = vi.fn();
    show(<SessionRail selected="s1a2b3c4" onSelect={select} notify={() => {}} />);

    const attended = await screen.findByText('Expose a streaming budget on the chunk API');
    expect(attended.closest('button')).toHaveAttribute('aria-current', 'true');

    await userEvent.click(attended);
    expect(select).toHaveBeenCalledWith('s1a2b3c4');
  });

  it('marks a repository that never adopted, where a session is running anyway', async () => {
    SESSIONS = [{ ...base, id: 'e5f6a7b8', quest: null, kind: 'chat', repository: 'sandbox', state: 'working' }];
    show(<SessionRail notify={() => {}} arrangement="repository" />);

    expect(await screen.findByText('not adopted')).toBeInTheDocument();
  });

  it('offers a designed nothing rather than an empty column', async () => {
    SESSIONS = [];
    show(<SessionRail notify={() => {}} arrangement="repository" />);

    expect(await screen.findByText('Nothing is running')).toBeInTheDocument();
  });

  it('surfaces a failed read as the sentence it came with', async () => {
    const notify = vi.fn();
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      if (String(input).startsWith('/api/sessions')) {
        return new Response(JSON.stringify({ error: 'the index is rebuilding' }), { status: 503 });
      }
      return respond(String(input));
    }));

    show(<SessionRail notify={notify} arrangement="repository" />);

    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('the index is rebuilding', 'error'));
  });
});

/**
 * SESSUX1c, D126 §2.1, §4: by state, the default, each session where the driver's one reader places it, asked once
 * under the sessions' key; a record the reader has not answered for yet by its record alone; and with no driver to ask,
 * the arrangement the list always had.
 */
describe('the session rail by state', () => {
  beforeEach(() => {
    SESSIONS = LIVE;
    GROUPS = LIVE_GROUPS;
    bridge.available = true;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(drive);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    bridge.available = true;
  });

  const groupHeadings = () => screen.getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent);

  it("lists by state by default, in the groups the driver's reader answers", async () => {
    show(<SessionRail notify={() => {}} />);

    await screen.findByRole('heading', { level: 3, name: 'Waiting on you (1)' });
    expect(groupHeadings()).toEqual(['Waiting on you (1)', 'Working (2)', 'Ended (1)']);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GROUPS', {});
    // No group header names a repository by state, so each row's line does (§4.2).
    const working = screen.getByRole('heading', { level: 3, name: 'Working (2)' }).closest('section')!;
    expect(within(working).getByText(/^engine · driven/)).toBeInTheDocument();
    expect(screen.queryByText('drives here')).toBeNull();
  });

  /** D126's 1 October, on the list (audit M2): a quest parked on its failed sessions waits on the person, with its count. */
  it("shows a parked quest's last session where it waits on you, in the reader's words", async () => {
    SESSIONS = [...LIVE, { ...base, id: 'f41led00', quest: '7a82cc', repository: 'engine', state: 'failed', created: at(60), updated: at(20) }];
    GROUPS = [
      LIVE_GROUPS[0],
      { session: 'f41led00', group: 'you', shown: 'parked', archived: false, teammate: false, strikes: 3 },
      ...LIVE_GROUPS.slice(1),
    ];
    show(<SessionRail notify={() => {}} />);

    const waiting = (await screen.findByRole('heading', { level: 3, name: 'Waiting on you (2)' })).closest('section')!;
    expect(within(waiting).getByText('parked')).toBeInTheDocument();
    expect(within(waiting).getByText(/after 3 failed sessions/)).toBeInTheDocument();
    expect(screen.queryByText('failed')).toBeNull();
  });

  /** A session that started after the reader looked is placed by its record until the next answer, never left out. */
  it('places a session the reader has not answered for by its record alone', async () => {
    SESSIONS = [...LIVE, { ...base, id: 'e9f8a7b6', quest: null, kind: 'chat', repository: 'engine', state: 'awaiting-person', created: at(3), updated: at(1) }];
    show(<SessionRail notify={() => {}} />);

    const waiting = (await screen.findByRole('heading', { level: 3, name: 'Waiting on you (2)' })).closest('section')!;
    expect(within(waiting).getAllByText('waiting on you')).toHaveLength(2);
  });

  /** A browser has no driver to ask (D47 §4): the list is the arrangement it always had, and asks no reader. */
  it('keeps the arrangement by repository where there is no driver to ask', async () => {
    bridge.available = false;
    show(<SessionRail notify={() => {}} />);

    expect(await screen.findByRole('region', { name: 'Ended' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 3, name: 'engine' })).toBeInTheDocument();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GROUPS', expect.anything());
  });

  /** The monitor's list is the present tense only and draws no group, so it asks the reader nothing. */
  it("asks the reader nothing for the monitor's list", async () => {
    show(<SessionRail notify={() => {}} live />);

    expect(await screen.findByRole('heading', { level: 3, name: 'engine' })).toBeInTheDocument();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GROUPS', expect.anything());
  });

  /**
   * SESSUX1e, D126 §5.2: an ended row archived from its ⋯ goes to the driver as one session, and the person is told
   * where it went; refused, the host's code is said in the person's words from the catalogue, naming which group kept it.
   */
  describe('archive (SESSUX1e)', () => {
    const DONE = 'd4e5f6a7';
    const ENDED_TWO = { ...base, id: 'e0e0e0e0', quest: null, repository: 'engine', state: 'declined', created: at(500), updated: at(400) };

    /** The bridge's answer, with SESSION_ARCHIVE's own; every other call as the rail's suite answers it. */
    const answering = (archive: (payload: Record<string, unknown>) => unknown) =>
      invoke.mockImplementation(async (module: string, type: string, options?: { payload?: Record<string, unknown> }) =>
        (type === 'SESSION_ARCHIVE' ? archive(options?.payload ?? {}) : drive(module, type)));

    const rail = (props: Partial<Parameters<typeof SessionRail>[0]> & { notify?: (text: string, kind?: string) => void } = {}) => {
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      return render(
        <QueryClientProvider client={client}>
          <Tooltip.Provider><SessionRail notify={() => {}} {...props} /></Tooltip.Provider>
        </QueryClientProvider>,
      );
    };

    const menuOf = async (section: string) => {
      const group = (await screen.findByRole('heading', { level: 3, name: section })).closest('section')!;
      const user = userEvent.setup();
      within(group).getAllByRole('button', { name: /^more for / })[0]!.focus();
      await user.keyboard('{Enter}');
      return user;
    };

    it('archives an ended row from its ⋯, and says Show archived brings it back', async () => {
      const notify = vi.fn();
      answering(() => ({ archived: [{ session: DONE, at: '2026-10-02T12:00:00Z' }], kept: [] }));
      rail({ notify });

      const user = await menuOf('Ended (1)');
      await user.click(screen.getByRole('menuitem', { name: 'Archive' }));

      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_ARCHIVE', { payload: { ids: [DONE], archived: true } });
      await waitFor(() => expect(notify).toHaveBeenCalledWith('Archived. Show archived, in the list’s ⋯, brings it back.'));
    });

    /** Asked of one session, a refusal is the answer: said in the catalogue's words, with the group that kept it. */
    it('says a refusal in the catalogue, naming the group that kept the session', async () => {
      const notify = vi.fn();
      answering(() => {
        throw Object.assign(new Error('refused'), {
          code: 'SESSION_NEEDS_YOU', parameters: { session: DONE, group: 'review', context: 'review' },
        });
      });
      rail({ notify });

      const user = await menuOf('Ended (1)');
      await user.click(screen.getByRole('menuitem', { name: 'Archive' }));

      await waitFor(() => expect(notify).toHaveBeenCalledWith(
        `${DONE} has work to review, so it was not archived: archive never hides what needs you. Land its work or discard its tree first.`,
        'error',
      ));
    });

    it('says a live session refused as the host names it', async () => {
      const notify = vi.fn();
      answering(() => {
        throw Object.assign(new Error('refused'), { code: 'SESSION_LIVE', parameters: { session: DONE } });
      });
      rail({ notify });

      const user = await menuOf('Ended (1)');
      await user.click(screen.getByRole('menuitem', { name: 'Archive' }));

      await waitFor(() => expect(notify).toHaveBeenCalledWith(`${DONE} is still running, so it was not archived. Stop it first.`, 'error'));
    });

    /** Unarchive brings a session back to the group its state puts it in, as the reader answers once asked again. */
    it('brings an archived row back to its group with Unarchive', async () => {
      const notify = vi.fn();
      GROUPS = [...LIVE_GROUPS.slice(0, 3), { session: DONE, group: 'archived', shown: 'completed', archived: true, teammate: false }];
      answering((payload) => {
        expect(payload).toEqual({ ids: [DONE], archived: false });
        GROUPS = LIVE_GROUPS;
        return { archived: [], notArchived: [] };
      });
      rail({ notify, archived: true });

      const user = await menuOf('Archived (1)');
      await user.click(screen.getByRole('menuitem', { name: 'Unarchive' }));

      expect(await screen.findByRole('heading', { level: 3, name: 'Ended (1)' })).toBeInTheDocument();
      // Archived is still shown, and now says it holds nothing.
      expect(screen.getByRole('heading', { level: 3, name: 'Archived (0)' })).toBeInTheDocument();
      expect(notify).toHaveBeenCalledWith('Unarchived. It is back in the list.');
    });

    /** One that was not archived is information, never a refusal (D48 §6). */
    it('says a session that was not archived was not, as information', async () => {
      const notify = vi.fn();
      GROUPS = [...LIVE_GROUPS.slice(0, 3), { session: DONE, group: 'archived', shown: 'completed', archived: true, teammate: false }];
      answering(() => ({ archived: [], notArchived: [DONE] }));
      rail({ notify, archived: true });

      const user = await menuOf('Archived (1)');
      await user.click(screen.getByRole('menuitem', { name: 'Unarchive' }));

      await waitFor(() => expect(notify).toHaveBeenCalledWith('It was not archived, so nothing changed.'));
    });

    /** §4.5: a search finds archived sessions too, each marked archived, since a search is how one is found without the tick. */
    it('finds an archived session in a search, marked archived', async () => {
      GROUPS = [...LIVE_GROUPS.slice(0, 3), { session: DONE, group: 'archived', shown: 'completed', archived: true, teammate: false }];
      answering(() => undefined);
      rail();
      await screen.findByRole('heading', { level: 3, name: 'Working (2)' });
      expect(screen.queryByRole('heading', { level: 3, name: /^Ended/ })).toBeNull();

      await userEvent.type(screen.getByRole('searchbox', { name: 'search sessions' }), 'Session');
      const byName = await screen.findByRole('region', { name: 'By name' });
      expect(within(byName).getByText(/^archived · driven · /)).toBeInTheDocument();
    });

    /** §5.3: the first press lists under the list's header; the second archives what it listed, and the ask closes. */
    it('archives what ended on the second press of Archive what ended, and says where they went', async () => {
      const notify = vi.fn();
      const closed = vi.fn();
      SESSIONS = [...LIVE, ENDED_TWO];
      GROUPS = [...LIVE_GROUPS, { session: ENDED_TWO.id, group: 'ended', shown: 'declined', archived: false, teammate: false }];
      answering(() => ({ archived: [{ session: DONE, at: 'x' }, { session: ENDED_TWO.id, at: 'x' }], kept: [] }));
      rail({ notify, archiveEnded: true, onArchiveEnded: closed });

      const ask = await screen.findByRole('group', { name: 'Archive what ended…' });
      expect(ask).toHaveTextContent('Archives 2 sessions that ended. Kept in the list: 1 waiting on you.');
      await userEvent.click(within(ask).getByRole('button', { name: 'Archive 2' }));

      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_ARCHIVE', { payload: { ids: [DONE, ENDED_TWO.id], archived: true } });
      await waitFor(() => expect(notify).toHaveBeenCalledWith('Archived 2 sessions that ended. Show archived, in the list’s ⋯, brings them back.'));
      expect(closed).toHaveBeenCalled();
    });

    /** Each is judged again as it goes (§5.3): what changed since the list stays, and the person is told how many went. */
    it('says how many of the listed went where some changed since the list', async () => {
      const notify = vi.fn();
      SESSIONS = [...LIVE, ENDED_TWO];
      GROUPS = [...LIVE_GROUPS, { session: ENDED_TWO.id, group: 'ended', shown: 'declined', archived: false, teammate: false }];
      answering(() => ({ archived: [{ session: DONE, at: 'x' }], kept: [{ session: ENDED_TWO.id, code: 'SESSION_NEEDS_YOU', group: 'review' }] }));
      rail({ notify, archiveEnded: true });

      await userEvent.click(await screen.findByRole('button', { name: 'Archive 2' }));
      await waitFor(() => expect(notify).toHaveBeenCalledWith(
        'Archived 1 of 2; the rest changed since the list. Show archived, in the list’s ⋯, brings them back.',
      ));
    });

    /** UXFIX2b2b: a refusal is said inside the ask, which stays open to be pressed again, and nothing is toasted. */
    it('says a refused archive inside its ask, and keeps it open', async () => {
      const notify = vi.fn();
      const closed = vi.fn();
      answering(() => { throw new Error('The driver is not running.'); });
      rail({ notify, archiveEnded: true, onArchiveEnded: closed });

      const ask = await screen.findByRole('group', { name: 'Archive what ended…' });
      await userEvent.click(within(ask).getByRole('button', { name: /^Archive \d+$/ }));

      expect(await within(ask).findByRole('alert')).toHaveTextContent('The driver is not running.');
      expect(within(ask).getByRole('button', { name: /^Archive \d+$/ })).toBeEnabled();
      expect(closed).not.toHaveBeenCalled();
      expect(notify).not.toHaveBeenCalled();
    });

    it('closes the ask on Never mind, archiving nothing', async () => {
      const closed = vi.fn();
      answering(() => { throw new Error('archived on Never mind'); });
      rail({ archiveEnded: true, onArchiveEnded: closed });

      await userEvent.click(await screen.findByRole('button', { name: 'Never mind' }));
      expect(closed).toHaveBeenCalled();
      expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_ARCHIVE', expect.anything());
    });
  });

  /** Group by repository, chosen in the list's ⋯, is today's arrangement with the reader's words (§4.3). */
  it('draws the list by repository when that is chosen, parked with waiting on you', async () => {
    SESSIONS = [...LIVE, { ...base, id: 'f41led00', quest: '7a82cc', repository: 'engine', state: 'failed', created: at(60), updated: at(20) }];
    GROUPS = [...LIVE_GROUPS, { session: 'f41led00', group: 'you', shown: 'parked', archived: false, teammate: false, strikes: 3 }];
    show(<SessionRail notify={() => {}} arrangement="repository" />);

    const engine = (await screen.findByRole('heading', { level: 3, name: 'engine' })).closest('section')!;
    await within(engine).findByText('parked');
    expect(within(rows(engine)[0]!).getByText('parked')).toBeInTheDocument();
  });
});

/**
 * ENTRY1b (D161's ENTRY1 note): a go to what waits on the person names a group of the list, and the rail brings it into
 * view once it and the driver's reader have answered: its heading scrolled to and focused, then the door told. A rail that
 * does not draw the group then lets the door go, rather than taking the focus when the group appears later.
 */
describe('a group a door brings into view', () => {
  beforeEach(() => {
    SESSIONS = LIVE;
    GROUPS = LIVE_GROUPS;
    bridge.available = true;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(drive);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it("scrolls to the group's heading and focuses it, then tells the door", async () => {
    const scroll = vi.spyOn(Element.prototype, 'scrollIntoView');
    const brought = vi.fn();
    try {
      show(<SessionRail notify={() => {}} group="you" onGroupBrought={brought} />);

      const heading = await screen.findByRole('heading', { level: 3, name: 'Waiting on you (1)' });
      await waitFor(() => expect(brought).toHaveBeenCalledTimes(1));
      expect(heading).toHaveFocus();
      expect(scroll.mock.instances).toContain(heading);
    } finally {
      scroll.mockRestore();
    }
  });

  /** A parked quest's session is the reader's alone to place in *Waiting on you*: the rail waits for its answer. */
  it("waits for the reader's answer before it looks for the group", async () => {
    SESSIONS = [{ ...base, id: 'f41led00', quest: '7a82cc', repository: 'engine', state: 'failed', created: at(60), updated: at(20) }];
    GROUPS = [{ session: 'f41led00', group: 'you', shown: 'parked', archived: false, teammate: false, strikes: 3 }];
    let answer: (value: unknown) => void = () => {};
    invoke.mockImplementation((_module: string, type: string) => (type === 'SESSION_GROUPS'
      ? new Promise((resolve) => { answer = resolve; })
      : Promise.resolve(DRIVER_STATE)));
    const brought = vi.fn();
    show(<SessionRail notify={() => {}} group="you" onGroupBrought={brought} />);

    // By its record alone it ended; the reader has not said yet where it waits.
    await screen.findByRole('heading', { level: 3, name: 'Ended (1)' });
    expect(brought).not.toHaveBeenCalled();
    answer({ sessions: GROUPS });
    const heading = await screen.findByRole('heading', { level: 3, name: 'Waiting on you (1)' });
    await waitFor(() => expect(brought).toHaveBeenCalledTimes(1));
    expect(heading).toHaveFocus();
  });

  /** ENTRY1g (D161's ENTRY1b note): a search typed in the rail hides its groups, so a go clears it; it is the rail's alone. */
  it('clears a search typed in the rail for a go, and brings the group into view', async () => {
    const brought = vi.fn();
    function Searched() {
      const [group, setGroup] = useState<OpenGroup | null>(null);
      const letGo = () => {
        brought();
        setGroup(null);
      };
      return (
        <>
          <button type="button" onClick={() => setGroup('you')}>go</button>
          <SessionRail notify={() => {}} group={group} onGroupBrought={letGo} />
        </>
      );
    }
    show(<Searched />);
    await userEvent.type(await screen.findByRole('searchbox', { name: 'search sessions' }), 'zzz');
    expect(screen.queryByRole('heading', { level: 3, name: 'Waiting on you (1)' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'go' }));
    const heading = await screen.findByRole('heading', { level: 3, name: 'Waiting on you (1)' });
    await waitFor(() => expect(brought).toHaveBeenCalledTimes(1));
    expect(heading).toHaveFocus();
    expect(screen.getByRole('searchbox', { name: 'search sessions' })).toHaveValue('');
  });

  it('lets the door go where the list draws no such group: nothing to review, by repository, the strip', async () => {
    const nothing = vi.fn();
    const first = show(<SessionRail notify={() => {}} group="review" onGroupBrought={nothing} />);
    await screen.findByRole('heading', { level: 3, name: 'Waiting on you (1)' });
    await waitFor(() => expect(nothing).toHaveBeenCalledTimes(1));
    expect(screen.getByRole('heading', { level: 3, name: 'Waiting on you (1)' })).not.toHaveFocus();
    first.unmount();

    const byRepository = vi.fn();
    const second = show(<SessionRail notify={() => {}} arrangement="repository" group="you" onGroupBrought={byRepository} />);
    await screen.findByRole('heading', { level: 3, name: 'engine' });
    await waitFor(() => expect(byRepository).toHaveBeenCalledTimes(1));
    expect(document.activeElement).toBe(document.body);
    second.unmount();

    const strip = vi.fn();
    show(<Tooltip.Provider><SessionRail notify={() => {}} compact group="you" onGroupBrought={strip} /></Tooltip.Provider>);
    await waitFor(() => expect(strip).toHaveBeenCalledTimes(1));
    expect(document.activeElement).toBe(document.body);
  });
});
