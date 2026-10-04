import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// UX6b (design §1 rule 6, §2.5; D150 §8), the way the application holds it: the whole window over a mocked shell. On the
// owner's install Quests reopened yesterday's done quest, and Overview's side bar kept an ended session's whole note.
// A view opened by its place reopens its chosen item only while it still waits; off Sessions the side bar follows only a
// session that runs or waits on the person, and opens on Ask Daoris otherwise.

const { invoke, notifyReady } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
}));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
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

const OPEN = {
  id: 'abc123', from: 'game', to: 'engine', title: 'Expose a streaming budget', body: 'World streaming needs a cap.',
  status: 'Open', filed: '2026-10-03T00:00:00Z', updated: '2026-10-03T00:00:00Z', workspace: 'default',
};
const DONE = { ...OPEN, id: 'd0d0d0', title: 'An old one', status: 'Done' };

/** A session that ended yesterday, its done note a whole verify report, as the install's side bar showed it. */
const ENDED = {
  id: 's1a2b3c4', quest: null, repository: 'engine', adapter: 'claude-code', kind: 'chat', state: 'completed',
  note: 'verify passed:\nwhole report line 2\nline 3\nline 4\nline 5\nline 6',
  created: '2026-10-03T00:00:00Z', updated: '2026-10-03T00:01:00Z', workspace: 'default',
};
const RUNNING = { ...ENDED, id: 'r7n8t7k6', state: 'working', note: undefined };

let SESSIONS: unknown[] = [];

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) {
    return Response.json([{ repository: 'engine', workspace: 'default', registered: true, adopted: true, owns: [], accepts: [], packs: [] }]);
  }
  if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
  if (url.startsWith('/api/sessions')) return Response.json(url.includes('daoris%3Ahelp') ? [] : SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(url.includes('includeClosed=true') ? [OPEN, DONE] : [OPEN]);
  if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
  return Response.json([]);
}

/** The window as a relaunch lands on it, on Overview, over a machine whose driver answers. */
function start() {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module === 'DAORIS.DRIVER' && type === 'STATE') {
      return { drivable: ['engine'], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {} };
    }
    return {};
  });
  // Get started would open over the view on a machine this bare; this is about what each view reopens.
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

const bar = () => screen.getByRole('navigation', { name: 'Views' });
const go = (place: string) => userEvent.click(within(bar()).getByRole('button', { name: place }));
const main = () => screen.getByRole('main');

describe('what a view reopens, on the window', () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
    SESSIONS = [];
  });

  it('opens Quests by its place with nothing chosen once the quest read there has closed', async () => {
    window.localStorage.setItem('daoris.list.quests.filters', JSON.stringify({ closed: true }));
    start();
    await go('Quests');
    await userEvent.click(await within(screen.getByRole('complementary', { name: 'Quests' })).findByText('An old one'));
    expect(await screen.findByRole('heading', { level: 1, name: 'An old one' })).toBeInTheDocument();

    await go('Overview');
    await screen.findByRole('heading', { level: 1, name: 'Overview' });
    await go('Quests');

    await waitFor(() => expect(main()).toHaveTextContent('Choose a quest or an ask'));
    expect(screen.queryByRole('heading', { level: 1, name: 'An old one' })).toBeNull();
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBeNull();
  });

  it('reopens by its place a quest that still waits', async () => {
    start();
    await go('Quests');
    await userEvent.click(await within(screen.getByRole('complementary', { name: 'Quests' })).findByText('Expose a streaming budget'));
    await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' });

    await go('Overview');
    await screen.findByRole('heading', { level: 1, name: 'Overview' });
    await go('Quests');

    expect(await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeInTheDocument();
  });

  it("opens Overview's side bar on Ask Daoris while the attended session has ended, saying none is followed there", async () => {
    SESSIONS = [ENDED];
    window.localStorage.setItem('daoris.attending', ENDED.id);
    window.localStorage.setItem('daoris.dockClosed', '0');
    start();

    const side = await screen.findByRole('complementary', { name: 'right side bar' });
    expect(await within(side).findByRole('tab', { name: 'Ask Daoris', selected: true })).toBeInTheDocument();

    await userEvent.click(within(side).getByRole('tab', { name: 'Timeline' }));
    expect(await within(side).findByText(/The session attended in Sessions has ended/)).toBeInTheDocument();
    expect(side).not.toHaveTextContent('whole report line 2');
    expect(side).not.toHaveTextContent(/^Attending/);
  });

  it("follows a running session in Overview's side bar, its timeline first", async () => {
    SESSIONS = [RUNNING];
    window.localStorage.setItem('daoris.attending', RUNNING.id);
    window.localStorage.setItem('daoris.dockClosed', '0');
    start();

    const side = await screen.findByRole('complementary', { name: 'right side bar' });
    expect(await within(side).findByRole('tab', { name: 'Timeline', selected: true })).toBeInTheDocument();
    expect(await within(side).findByText(/^Attending/)).toBeInTheDocument();
  });
});
