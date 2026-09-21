import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
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

let fetchMock: ReturnType<typeof vi.fn>;

function respond(url: string): Response {
  if (url.startsWith('/api/status')) return Response.json(STATUS);
  if (url.startsWith('/api/repositories')) return Response.json(REPOSITORIES);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  if (url.startsWith('/api/quests')) return Response.json([]);
  if (url.startsWith('/api/sessions')) return Response.json([]);
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

  it('offers the scope in the sidebar foot, stating that it spans every workspace', async () => {
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
 * The two frames (D55). In a browser there is exactly one: Work's centre is a stream, its rows
 * carry tree paths, and neither may leave the machine that produced them (D47 §4) — so the switch
 * that would offer it is absent rather than disabled, and a remembered `work` cannot resurrect it.
 */
describe('the frames, in a browser', () => {
  beforeEach(() => {
    fetchMock = vi.fn(async (input: RequestInfo | URL) => respond(String(input)));
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    window.localStorage.removeItem('daoris.mode');
  });

  it('offers no mode switch where there is no shell to hold the other frame', async () => {
    shell();
    await screen.findByRole('combobox', { name: 'workspace' });

    expect(screen.queryByRole('group', { name: 'mode' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Work' })).toBeNull();
  });

  it('falls back to Manage when the browser remembers a frame this deployment does not have', async () => {
    window.localStorage.setItem('daoris.mode', 'work');
    shell();

    // Manage's landing view, not an empty Work frame.
    expect(await screen.findByRole('heading', { name: 'Overview' })).toBeInTheDocument();
    expect(screen.queryByLabelText('sessions')).toBeNull();
  });

  /** Ambient truth is true in both frames, so the bar belongs to the application, not to Work. */
  it('carries the status bar, saying plainly that this machine has no driver', async () => {
    shell();

    const bar = await screen.findByLabelText('state of this machine');
    expect(bar).toHaveTextContent('none here');
    // The remote question cannot be asked without the map, so it is not answered either.
    expect(bar).not.toHaveTextContent('remote');
  });
});
