import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { QueryClient } from '@tanstack/react-query';

// The page's root as `main.tsx` mounts it (PAGELOOP1): its error screen outside every provider, and a reload that is a page
// starting again, which hands the window its ready handshake and lifts its splash a second time. jsdom cannot reload a page, so a
// second load is the modules read afresh and the root mounted again with a client of its own, as a reloaded page's are.

const { invoke, notifyReady, closeSplash, shenora } = vi.hoisted(() => {
  const notifyReady = vi.fn(() => Promise.resolve());
  return {
    invoke: vi.fn(),
    notifyReady,
    closeSplash: vi.fn(() => Promise.resolve()),
    // One bridge for the page's life, as the kit's is: the handshake's effect is keyed on it, and a new one per render would be a
    // handshake per render.
    shenora: { isAvailable: true, bridge: { notifyReady } },
  };
});

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
    useShenora: () => shenora,
    useShenoraEvent: () => {},
    useWindowMaximized: () => false,
    // Every other window command a test's window has none of, and the chrome's calls are fire-and-forget.
    WindowCommands: class { closeSplash = closeSplash; },
  };
});

import { AT_START } from '../help/setupGuide';

function respond(url: string): Response {
  if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
  if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
  return Response.json([]);
}

/** What the page reported as `page.error`, each report's data. */
const pageErrors = () => invoke.mock.calls
  .filter(([module, type, request]) => module === 'DAORIS.LOG' && type === 'EVENT' && request?.payload?.event === 'page.error')
  .map(([, , request]) => request.payload.data as Record<string, unknown>);

/** The page's root, read as a fresh page reads it. */
const freshRoot = async () => {
  vi.resetModules();
  await import('../locales/i18n');
  return (await import('./PageRoot')).PageRoot;
};

describe("the page's root", () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
      if (module === 'DAORIS.DRIVER' && type === 'STATE') return { drivable: [], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {} };
      return {};
    });
    window.localStorage.setItem(AT_START, 'off');
    vi.spyOn(console, 'error').mockImplementation(() => {});
  });
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    invoke.mockReset();
    notifyReady.mockClear();
    closeSplash.mockClear();
    window.localStorage.clear();
  });

  it('catches a failure in its providers too, the query client among them', async () => {
    const PageRoot = await freshRoot();
    const client = new QueryClient();
    client.mount = () => { throw new Error('the query client broke'); };

    render(<PageRoot client={client} window={null} />);

    expect(await screen.findByRole('heading', { level: 1, name: "This window's page stopped on an error" })).toBeInTheDocument();
    expect(screen.getByText('the query client broke')).toBeInTheDocument();
  });

  /** PAGELOOP1 (D217): the view a failure's line names is the window's, a secondary window's from before anything is drawn. */
  it("names a secondary window's view in its failure's line", async () => {
    const PageRoot = await freshRoot();
    const client = new QueryClient();
    client.mount = () => { throw new Error('the query client broke'); };

    render(<PageRoot client={client} window={{ kind: 'monitor' }} />);

    await screen.findByRole('heading', { level: 1, name: "This window's page stopped on an error" });
    expect(pageErrors()).toEqual([expect.objectContaining({ where: 'render', view: 'window:monitor', message: 'the query client broke' })]);
  });

  it("names the main window's view, as the window shows it", async () => {
    const PageRoot = await freshRoot();
    render(<PageRoot client={new QueryClient({ defaultOptions: { queries: { retry: false } } })} window={null} />);
    await screen.findByRole('navigation', { name: 'Views' });

    (await import('../bridge/log')).reportPageStopped(new Error('a later failure'));

    expect(pageErrors()).toEqual([expect.objectContaining({ where: 'render', view: 'overview' })]);
  });

  it('hands the window its handshake and lifts its splash again on a second load', async () => {
    const First = await freshRoot();
    render(<First client={new QueryClient({ defaultOptions: { queries: { retry: false } } })} window={null} />);
    await waitFor(() => expect(closeSplash).toHaveBeenCalledTimes(1));
    expect(notifyReady).toHaveBeenCalledTimes(1);

    // The reload: the old page gone, and a new one read and mounted from the start.
    cleanup();
    const Second = await freshRoot();
    render(<Second client={new QueryClient({ defaultOptions: { queries: { retry: false } } })} window={null} />);

    await waitFor(() => expect(closeSplash).toHaveBeenCalledTimes(2));
    expect(notifyReady).toHaveBeenCalledTimes(2);
  });
});
