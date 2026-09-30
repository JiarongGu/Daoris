import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// An ORGANISM, so this is the layer that holds the hooks and the layer a mocked bridge is for
// (components plan §2). The bridge is mocked as present, because the Work frame is desktop-only
// (D55) and the group header's driver facts exist nowhere else.

const { invoke, notifyReady } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => {},
}));

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
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('groups what is running by repository, in a stable order', async () => {
    show(<SessionRail notify={() => {}} />);

    // The repository groups' headings — not the ended section's, which is a peer of the groups
    // rather than one of them, and lists what is no longer running.
    const ended = await screen.findByRole('region', { name: 'ended' });
    const headings = screen.getAllByRole('heading', { level: 3 })
      .filter((heading) => !ended.contains(heading));
    expect(headings.map((heading) => heading.textContent)).toEqual(['engine', 'tools']);
  });

  /**
   * FRAME6: closed to its 56px strip, the rail keeps every running session one press away — its
   * repository's initial and its mark, with the title, the repository and the state as its name, since
   * a strip has no room for the words and a mark is never hue alone (D41 §6). What ended is left to
   * the open rail; the attended session stays, whatever state it reached.
   */
  it('keeps every running session one press away as a strip', async () => {
    const select = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <SessionRail notify={() => {}} compact selected="c3d4e5f6" onSelect={select} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    const strip = await screen.findByRole('navigation', { name: 'sessions' });
    await within(strip).findAllByRole('button');
    const rows = within(strip).getAllByRole('button');
    expect(rows.map((row) => row.getAttribute('aria-label'))).toEqual([
      'Expose a streaming budget on the chunk API · engine · working',
      'conversation · tools · awaiting person',
      'conversation · tools · working',
    ]);
    expect(rows.map((row) => row.textContent)).toEqual(['e', 't', 't']);
    expect(rows[2]).toHaveAttribute('aria-current', 'true');

    await userEvent.click(rows[1]!);
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
        extra(type, options?.payload ?? {}) ?? DRIVER_STATE);
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
      const byName = await screen.findByRole('region', { name: 'by name' });
      expect(within(byName).getByText('Cap the hydration per frame')).toBeInTheDocument();
      expect(screen.queryByRole('heading', { name: 'engine' })).toBeNull();

      await userEvent.clear(screen.getByRole('searchbox', { name: 'search sessions' }));
      await userEvent.type(screen.getByRole('searchbox', { name: 'search sessions' }), 'streamer');
      const said = await screen.findByRole('region', { name: 'in what was said' });
      expect(within(said).getByText('streamer').tagName).toBe('MARK');
      // Its tint adds no space beside the word (UX5 U61).
      expect(within(said).getByText('streamer')).toHaveClass('-mx-0.5');
      await userEvent.click(within(said).getByRole('button', { name: /…belongs in the streamer/ }));
      expect(select).toHaveBeenCalledWith('c3d4e5f6');

      await userEvent.type(screen.getByRole('searchbox', { name: 'search sessions' }), '{Escape}');
      expect(await screen.findByRole('heading', { name: 'engine' })).toBeInTheDocument();
    });

    it('opens a session in its own window from its row\'s menu', async () => {
      answer(() => undefined);
      rail({ onReview: () => {} });
      await screen.findByText('Expose a streaming budget on the chunk API');

      const user = userEvent.setup();
      screen.getByRole('button', { name: 'more for Expose a streaming budget on the chunk API' }).focus();
      await user.keyboard('{Enter}');
      await user.click(screen.getByRole('menuitem', { name: 'Open in its own window' }));

      expect(invoke).toHaveBeenCalledWith('DAORIS.WINDOWS', 'OPEN', { payload: { name: 'session:s1a2b3c4' } });
    });
  });

  it('carries the machine\'s standing choices on the header, where the repository\'s facts live', async () => {
    show(<SessionRail notify={() => {}} />);

    await screen.findByText('engine');
    expect(screen.getByText('drives here')).toBeInTheDocument();
    expect(screen.getByText('held')).toBeInTheDocument();
  });

  /** D51: the tree is the unit of exclusion, so "busy" is a question about a tree, not a repository. */
  it('names the tree an active session is holding', async () => {
    show(<SessionRail notify={() => {}} />);
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
    show(<SessionRail notify={() => {}} />);

    const group = (await screen.findByText('ask #0fda18')).closest('section')!;
    expect(within(group).queryByText('busy')).toBeNull();
  });

  /** HELP1a: Ask Daoris's conversations group under its own name, never the record's `daoris:help`. */
  it('names Ask Daoris\'s group by its name, not by the repository its records are kept in', async () => {
    SESSIONS = [{ ...base, id: 'h1e1p000', quest: null, kind: 'chat', repository: 'daoris:help', state: 'working' }];
    show(<SessionRail notify={() => {}} />);

    expect(await screen.findByRole('heading', { name: 'Ask Daoris' })).toBeInTheDocument();
    expect(screen.queryByText('daoris:help')).toBeNull();
  });

  it('says busy without a name when the session holds the registered root', async () => {
    show(<SessionRail notify={() => {}} />);

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
    show(<SessionRail notify={() => {}} />);

    await screen.findByText('engine');
    // engine: the one live session. tools: two. The completed one is not in either group…
    const engine = screen.getByText('engine').closest('section')!;
    expect(rows(engine)).toHaveLength(1);
    // …it is in the ended section, reachable from a fresh window with nothing selected.
    const ended = screen.getByRole('region', { name: 'ended' });
    expect(within(ended).getByText('completed')).toBeInTheDocument();
    expect(rows(ended)).toHaveLength(1);
  });

  it('opens an ended session when it is chosen, like any other', async () => {
    const onSelect = vi.fn();
    show(<SessionRail notify={() => {}} onSelect={onSelect} />);

    const ended = await screen.findByRole('region', { name: 'ended' });
    await userEvent.click(rows(ended)[0]!);
    expect(onSelect).toHaveBeenCalledWith('d4e5f6a7');
  });

  /** A machine with no live session but an ended one is not empty, and must not say it is. */
  it('does not show the full empty state while there is a record to read', async () => {
    SESSIONS = LIVE.filter((s) => (s as { state: string }).state === 'completed');
    show(<SessionRail notify={() => {}} />);

    await screen.findByRole('region', { name: 'ended' });
    expect(screen.queryByText(/Sessions appear here/)).not.toBeInTheDocument();
    expect(screen.getByText('Nothing is running')).toBeInTheDocument();
  });

  /**
   * A session that finishes while you are reading it must not vanish out from under you — the rail
   * is where attention lives, and losing the attended session on a state change is the one thing a
   * list of running things must not do.
   */
  it('keeps the attended session listed after it ends', async () => {
    show(<SessionRail selected="d4e5f6a7" notify={() => {}} />);

    expect(await screen.findByText('completed')).toBeInTheDocument();
    const engine = screen.getByText('engine').closest('section')!;
    expect(rows(engine)).toHaveLength(2);
  });

  it('puts the session that needs a person at the top of its group', async () => {
    show(<SessionRail notify={() => {}} />);

    const tools = (await screen.findByText('tools')).closest('section')!;
    expect(within(rows(tools)[0]!).getByText('awaiting person')).toBeInTheDocument();
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
    show(<SessionRail notify={() => {}} />);

    expect(await screen.findByText('not adopted')).toBeInTheDocument();
  });

  it('offers a designed nothing rather than an empty column', async () => {
    SESSIONS = [];
    show(<SessionRail notify={() => {}} />);

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

    show(<SessionRail notify={notify} />);

    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('the index is rebuilding', 'error'));
  });
});
