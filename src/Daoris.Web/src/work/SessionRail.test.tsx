import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

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

    const headings = await screen.findAllByRole('heading', { level: 3 });
    expect(headings.map((heading) => heading.textContent)).toEqual(['engine', 'tools']);
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

  it('says busy without a name when the session holds the registered root', async () => {
    show(<SessionRail notify={() => {}} />);

    const tools = (await screen.findByText('tools')).closest('section')!;
    expect(within(tools).getByText('busy')).toBeInTheDocument();
  });

  it('lists what is still running and leaves finished records to the views that review them', async () => {
    show(<SessionRail notify={() => {}} />);

    await screen.findByText('engine');
    expect(screen.queryByText('completed')).not.toBeInTheDocument();
    // engine: the one live session. tools: two.
    const engine = screen.getByText('engine').closest('section')!;
    expect(within(engine).getAllByRole('button')).toHaveLength(1);
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
    expect(within(engine).getAllByRole('button')).toHaveLength(2);
  });

  it('puts the session that needs a person at the top of its group', async () => {
    show(<SessionRail notify={() => {}} />);

    const tools = (await screen.findByText('tools')).closest('section')!;
    const rows = within(tools).getAllByRole('button');
    expect(within(rows[0]).getByText('awaiting person')).toBeInTheDocument();
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
