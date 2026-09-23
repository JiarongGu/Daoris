import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { App } from './App';
import { WorkspaceScopeProvider } from './scope';

// The shell in BROWSER mode — no bridge, so nothing shell-only renders — over a stubbed service that
// holds two circles. The switcher is global chrome (platform language §2: global state lives in
// exactly one place), so it is the shell's to test, not any view's.

const STATUS = { semantic: false, tier: 'lexical', note: 'no embedder configured' };
const REPOSITORIES = [
  { name: 'engine', total: 3, local: 3, canonical: 0, workspace: 'default' },
  { name: 'studio', total: 1, local: 1, canonical: 0, workspace: 'aurora' },
];
const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 3, workspace: 'default' },
  { repository: 'studio', adopted: true, registered: true, summary: 'the studio', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora' },
];

/** What Overview's band is made of, when the fixtures below hand it something to show. */
let QUESTS: unknown[] = [];
let SESSIONS: unknown[] = [];

let fetchMock: ReturnType<typeof vi.fn>;

function respond(url: string): Response {
  if (url.startsWith('/api/status')) return Response.json(STATUS);
  if (url.startsWith('/api/repositories')) return Response.json(REPOSITORIES);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  throw new Error(`unstubbed request: ${url}`);
}

function shell(initial: string | null = null) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={initial}>
          <App />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

const requested = () => fetchMock.mock.calls.map((call) => String(call[0]));

describe('the shell in a browser, over two workspaces', () => {
  beforeEach(() => {
    fetchMock = vi.fn(async (input: RequestInfo | URL) => respond(String(input)));
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => vi.unstubAllGlobals());

  it('offers the scope in the app strip, stating that it spans every workspace', async () => {
    shell();
    const scope = await screen.findByRole('combobox', { name: 'workspace' });
    expect(scope).toHaveTextContent('every workspace · 2');
    // Nothing chosen: the page asks the doors for everything, and says so above rather than picking.
    for (const url of requested()) expect(url).not.toContain('workspace=');
  });

  it('choosing a workspace scopes every door the shell and its views ask', async () => {
    shell();
    await userEvent.click(await screen.findByRole('combobox', { name: 'workspace' }));
    await userEvent.click(await screen.findByRole('option', { name: 'aurora' }));

    await waitFor(() => {
      const urls = requested();
      // The badge's quests, the foot's count, the landing view's registry — all scoped, none mixed.
      expect(urls.some((url) => url.startsWith('/api/quests') && url.includes('workspace=aurora'))).toBe(true);
      expect(urls.some((url) => url.startsWith('/api/repositories') && url.includes('workspace=aurora'))).toBe(true);
      expect(urls.some((url) => url.startsWith('/api/registry') && url.includes('workspace=aurora'))).toBe(true);
    });
    expect(screen.getByRole('combobox', { name: 'workspace' })).toHaveTextContent('aurora');
  });

  it('a remembered workspace that no longer exists falls back to every, out loud', async () => {
    shell('gone');
    await waitFor(() => {
      expect(screen.getByRole('combobox', { name: 'workspace' })).toHaveTextContent('every workspace · 2');
    });
    // …and the queries settle on every circle, not on a scope nobody holds any more.
    await waitFor(() => {
      const latest = requested().filter((url) => url.startsWith('/api/registry')).at(-1);
      expect(latest).not.toContain('workspace=');
    });
  });
});

/**
 * One navigation (D66), in a browser. Sessions' centre is a stream and its rows carry tree paths,
 * and neither may leave the machine that produced them (D47 §4) — so the view is absent from the bar
 * rather than disabled, and a remembered `sessions` cannot resurrect it. Settings is there: a browser
 * has appearance to set.
 */
describe('the views, in a browser', () => {
  beforeEach(() => {
    fetchMock = vi.fn(async (input: RequestInfo | URL) => respond(String(input)));
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    window.localStorage.removeItem('daoris.view');
  });

  it('offers no Sessions and no mode switch — and does offer Settings', async () => {
    shell();
    const bar = await screen.findByRole('navigation', { name: 'Views' });

    expect(within(bar).queryByRole('button', { name: 'Sessions' })).toBeNull();
    expect(screen.queryByRole('group', { name: /mode/i })).toBeNull();
    expect(within(bar).getByRole('button', { name: 'Settings' })).toBeInTheDocument();
  });

  it('falls back to Overview when the browser remembers a view this deployment does not have', async () => {
    window.localStorage.setItem('daoris.view', 'sessions');
    shell();

    // The landing view, not an empty Sessions.
    expect(await screen.findByRole('heading', { name: 'Overview' })).toBeInTheDocument();
    expect(screen.queryByLabelText('sessions')).toBeNull();
  });

  /** In a browser, Settings is appearance and nothing of a machine (D47 §4). */
  it('opens Settings on appearance alone', async () => {
    shell();
    await userEvent.click(await screen.findByRole('button', { name: 'Settings' }));

    expect(await screen.findByRole('radiogroup', { name: 'Theme' })).toBeInTheDocument();
    expect(screen.getByRole('radiogroup', { name: 'Language' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'This machine' })).toBeNull();
  });

  /** Ambient truth is true on every view, so the bar belongs to the application. */
  it('carries the status bar, saying plainly that this machine has no driver', async () => {
    shell();

    const bar = await screen.findByLabelText('state of this machine');
    expect(bar).toHaveTextContent('none here');
    // The remote question cannot be asked without the map, so it is not answered either.
    expect(bar).not.toHaveTextContent('remote');
  });
});

/**
 * *What needs you* (design §4). It is service data, so a browser sees it — knowing is the half
 * that travels — but the doors into Work are not offered where Work does not exist.
 */
describe('the attention band', () => {
  beforeEach(() => {
    QUESTS = [];
    SESSIONS = [];
    fetchMock = vi.fn(async (input: RequestInfo | URL) => respond(String(input)));
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    QUESTS = [];
    SESSIONS = [];
  });

  it('is absent entirely when nothing is waiting — an always-there all-clear is not read', async () => {
    shell();
    await screen.findByRole('heading', { name: 'Overview' });

    expect(screen.queryByText('What needs you')).toBeNull();
  });

  it('names a parked session and a quest nobody here can take', async () => {
    SESSIONS = [{
      id: 'p4rk3d00', quest: null, repository: 'engine', adapter: 'stub', kind: 'chat',
      state: 'awaiting-person', note: 'two ways forward.',
      created: '2026-09-21T09:00:00Z', updated: '2026-09-21T10:00:00Z',
    }];
    QUESTS = [{
      id: '7a82cc', from: 'engine', to: 'retired', title: 'Expose a streaming budget',
      body: 'a per-frame cap.', status: 'Open',
      filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
    }];

    shell();

    expect(await screen.findByText('What needs you')).toBeInTheDocument();
    expect(screen.getByText('parked at a checkpoint')).toBeInTheDocument();
    expect(screen.getByText('nobody here can take this')).toBeInTheDocument();
    // The category the design names third is not invented: it needs SURF6's viewed mark.
    expect(screen.getByText(/once review exists/)).toBeInTheDocument();
  });
});
