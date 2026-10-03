import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// CTX1 (D138), the way the application holds it: the whole window over a mocked shell, so what a right-click offers and
// where each door leads are what a person meets on the window — a quest's page, its link, selected words, the strip.

const { invoke, notifyReady, systemMenu } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  systemMenu: vi.fn(() => Promise.resolve()),
}));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  class WindowCommands {
    showSystemMenu = systemMenu;
    setCaptionButtons = () => Promise.resolve();
    setTheme = () => Promise.resolve();
    startDrag = () => Promise.resolve();
    toggleMaximize = () => Promise.resolve();
    startResize = () => Promise.resolve();
  }
  return {
    ...actual,
    WindowCommands,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
    useShenoraEvent: () => {},
    useWindowMaximized: () => false,
  };
});

import './i18n';
import { App } from './App';
import { AT_START } from './setupGuide';
import { WorkspaceScopeProvider } from './scope';
import { menuActs, rightClick } from './test/contextMenu';

const TICKET = 'https://tickets.example/browse/T-1';

/** A quest whose receiver nobody here registered, so Overview's band lists it and its row opens it. */
const QUEST = {
  id: '7a82cc', from: 'engine', to: 'retired', title: 'Expose a streaming budget', body: 'a per-frame cap on hydration.',
  status: 'Open', filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z', links: [TICKET],
};

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) {
    return Response.json([{ repository: 'engine', workspace: 'default', registered: true, adopted: true, owns: [], accepts: [], packs: [] }]);
  }
  if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
  if (url.startsWith('/api/quests')) return Response.json([QUEST]);
  if (url.startsWith('/api/sessions')) return Response.json([]);
  if (url.startsWith('/api/search')) return Response.json([]);
  if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
  return Response.json([]);
}

function machine() {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.BROWSER') return { favorites: [], extensions: 'offer', browser: 'daoris', edgeFound: true, links: 'system' };
    if (module === 'DAORIS.WINDOWS') return { opened: true, windows: [] };
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module !== 'DAORIS.DRIVER') return {};
    if (type === 'STATE') return { drivable: ['engine'], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {} };
    return {};
  });
}

function start() {
  window.localStorage.setItem(AT_START, 'off');
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <App />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

/** The quest's page, opened from Overview's band, in Quests' main area. */
async function questPage() {
  await userEvent.click((await screen.findAllByRole('button', { name: /Expose a streaming budget/ }))[0]!);
  await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' });
  return screen.getByText('a per-frame cap on hydration.');
}

function select(element: Element) {
  const range = document.createRange();
  range.selectNodeContents(element);
  window.getSelection()!.removeAllRanges();
  window.getSelection()!.addRange(range);
}

describe('the right-click menu, on the window', () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    systemMenu.mockClear();
    window.localStorage.clear();
    window.getSelection()?.removeAllRanges();
  });

  it('offers a quest’s acts on its page, and its link’s before them, opening it in Daoris’s browser on the press', async () => {
    machine();
    start();
    const body = await questPage();

    rightClick(body);
    expect(await menuActs('Actions for Expose a streaming budget')).toEqual(['Take', 'Mark done', 'Decline…', 'Copy quest ID']);
    await userEvent.keyboard('{Escape}');

    rightClick(within(screen.getByRole('main')).getByRole('link', { name: TICKET }));
    expect((await menuActs()).slice(0, 3)).toEqual(['Open', "Open in Daoris's browser", 'Copy link']);
    await userEvent.click(screen.getByRole('menuitem', { name: "Open in Daoris's browser" }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.WINDOWS', 'OPEN_BROWSER', { payload: { url: TICKET } }));
  });

  it('searches Daoris for selected words: Search in front, the words in its box', async () => {
    machine();
    start();
    const body = await questPage();
    select(body);
    rightClick(body);
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Search Daoris for it' }));
    expect(await screen.findByRole('searchbox', { name: 'search knowledge' })).toHaveValue('a per-frame cap on hydration.');
  });

  it('asks Daoris about selected words in Quick Ask', async () => {
    machine();
    start();
    const body = await questPage();
    select(body);
    rightClick(body);
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Ask Daoris about it' }));
    expect(await screen.findByRole('dialog', { name: 'Quick Ask' })).toBeInTheDocument();
  });

  it('copies through the clipboard, and says what it copied', async () => {
    machine();
    const writeText = vi.fn(() => Promise.resolve());
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    start();
    const body = await questPage();
    rightClick(body);
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Copy quest ID' }));
    expect(writeText).toHaveBeenCalledWith('7a82cc');
    expect(await screen.findByText('Copied 7a82cc.')).toBeInTheDocument();
  });

  it('opens the window’s own system menu on the strip’s own space, and not on its controls', async () => {
    machine();
    start();
    const space = await waitFor(() => {
      const group = document.querySelector<HTMLElement>('[data-strip-space="center"]');
      expect(group).not.toBeNull();
      return group!;
    });
    expect(fireEvent.contextMenu(space)).toBe(false);
    expect(systemMenu).toHaveBeenCalledOnce();
    expect(screen.queryByRole('menu')).toBeNull();

    // The command center is a control, not the strip's space: nothing of the system's there, and nothing of ours.
    rightClick(screen.getByRole('button', { name: /Commands/ }));
    expect(systemMenu).toHaveBeenCalledOnce();
    expect(screen.queryByRole('menu')).toBeNull();
  });
});
