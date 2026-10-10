import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// SETUP1b (D97 §2), the way the application holds it: the whole window over a mocked shell, so the
// opening at start, the status bar's count and the hand-off to Ask Daoris are what a person meets.

const { invoke, notifyReady } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
}));

// The window's chrome is the library's own, as `windowChrome.test.tsx` has it; the bridge is the mock.
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

const HELP = {
  id: 'h1e1p000', quest: null, repository: 'daoris:help', adapter: 'claude-code-acp', state: 'working',
  kind: 'chat', created: '2026-09-30T00:00:00Z', updated: '2026-09-30T00:01:00Z', workspace: 'default',
};

type World = {
  registry: { repository: string; workspace: string }[];
  state: Record<string, unknown>;
  harnesses: unknown[];
  lines: unknown[];
  landings: unknown[];
};

const FRESH: World = {
  registry: [],
  state: { drivable: [], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {}, helperAdapter: '', intakeAdapter: '' },
  harnesses: [],
  lines: [],
  landings: [],
};

const DONE: World = {
  registry: [{ repository: 'engine', workspace: 'default' }],
  state: { ...FRESH.state, drivable: ['engine'], helperAdapter: 'claude-code-acp' },
  harnesses: [{ harness: 'claude-code', present: true, ownLogin: 'in', product: 'Claude Code' }],
  lines: [{ repository: 'engine', workspace: 'default', branch: 'main', source: 'workspace' }],
  landings: [{ repository: 'engine', workspace: 'default', source: 'workspace', form: 'merge' }],
};

let world: World = FRESH;
/**
 * Ask Daoris's conversation open before the window is, what it proposes, and what an Apply settles (ENTRY1d2b): none of
 * them, unless a test hands it one.
 */
let conversing = false;
let proposals: unknown[] = [];
let settled: unknown = { message: '', applied: false };

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) {
    return Response.json(world.registry.map((row) => ({ ...row, registered: true, adopted: true, owns: [], accepts: [], packs: [] })));
  }
  if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
  if (url.startsWith('/api/repositories')) return Response.json([]);
  if (url.startsWith('/api/quests')) return Response.json([]);
  if (url.startsWith('/api/asks')) return Response.json([]);
  if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
  if (url.startsWith('/api/sessions')) {
    return Response.json(url.includes('daoris%3Ahelp') && (conversing || sent().length > 0) ? [HELP] : []);
  }
  return Response.json([]);
}

const sent = () => invoke.mock.calls
  .filter(([, type]) => type === 'SESSION_INPUT')
  .map(([, , request]) => (request as { payload: { text: string } }).payload.text);

function machine(next: World) {
  world = next;
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module !== 'DAORIS.DRIVER') return {};
    switch (type) {
      case 'STATE': return world.state;
      case 'HARNESSES': return { settingsPath: 'harnesses.json', adapter: 'claude-code', harnesses: world.harnesses };
      case 'LINES': return { lines: world.lines, landings: world.landings };
      case 'START_HELP': return { sessionId: HELP.id, message: 'opened' };
      case 'SESSION_INPUT': return { sent: true };
      case 'SESSION_HISTORY': return { session: HELP.id, events: [], earlier: false, latest: 0 };
      case 'SESSION_QUEUE': return { session: HELP.id, queued: [], taking: false };
      case 'HELP_PROPOSALS': return { session: HELP.id, proposals };
      case 'HELP_APPLY': return settled;
      default: return {};
    }
  });
}

function start() {
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

const domains = () => screen.findByRole('navigation', { name: 'Settings domains' });

describe('the setup guide, at start', () => {
  beforeEach(() => machine(FRESH));
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
    conversing = false;
    proposals = [];
    settled = { message: '', applied: false };
  });

  it('opens a fresh machine on Get started, and counts the setup in the status bar', async () => {
    start();

    expect(within(await domains()).getByRole('button', { name: 'Get started' })).toHaveAttribute('aria-current', 'page');
    expect(await screen.findByRole('list', { name: 'setup steps' })).toBeInTheDocument();
    const bar = screen.getByRole('contentinfo', { name: 'state of this machine' });
    expect(await within(bar).findByText('setup: 0 of 5')).toBeInTheDocument();

    // The count leads back to it from wherever the person went.
    await userEvent.click(screen.getByRole('button', { name: 'Repositories' }));
    await waitFor(() => expect(screen.queryByRole('navigation', { name: 'Settings domains' })).toBeNull());
    await userEvent.click(within(bar).getByRole('button', { name: 'setup' }));
    expect(within(await domains()).getByRole('button', { name: 'Get started' })).toHaveAttribute('aria-current', 'page');
  });

  it('does not open for a viewer who turned it off, and still counts the setup', async () => {
    window.localStorage.setItem(AT_START, 'off');
    start();

    const bar = screen.getByRole('contentinfo', { name: 'state of this machine' });
    expect(await within(bar).findByText('setup: 0 of 5')).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Settings domains' })).toBeNull();
  });

  it('opens nothing and counts nothing once the setup is done', async () => {
    machine(DONE);
    start();

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'LINES', {}));
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(screen.queryByRole('navigation', { name: 'Settings domains' })).toBeNull();
    expect(screen.queryByText(/setup:/)).toBeNull();
  });

  it('hands Ask Daoris a first message asking to be walked through what is left', async () => {
    machine({ ...FRESH, state: { ...FRESH.state, helperAdapter: 'claude-code-acp' } });
    start();

    await userEvent.click(await screen.findByRole('button', { name: 'Set up with Ask Daoris' }));
    await waitFor(() => expect(sent()).toEqual([
      'Walk me through setting up Daoris on this machine, one step at a time. Not done yet: '
      + '1. An agent; 3. A workspace and its repositories; 4. What is driven; 5. How work lands; 6. What agents may do (optional).',
    ]));
  });

  /**
   * ENTRY1d2b (D161's ENTRY1d note): the setup's repositories through Ask Daoris. Its go to Add repository carries the
   * workspace the driver judged, and the window opens the drawer with it filled, before the folder, which stays the
   * person's to pick.
   */
  it("opens Add repository with the workspace Ask Daoris's go names filled", async () => {
    machine({ ...FRESH, state: { ...FRESH.state, helperAdapter: 'claude-code-acp' } });
    conversing = true;
    proposals = [{
      id: 'g5a6d7d8', kind: 'go', describe: 'Open Repositories → Add repository, its workspace `work` filled.', terminal: '',
      why: 'the person asked to add a repository to work',
    }];
    settled = {
      message: 'Applied: `#g5a6d7d8` — Open Repositories → Add repository, its workspace `work` filled. Nothing else changed.',
      applied: true, go: { view: 'projects', domain: null, part: 'add', item: null, workspace: 'work' },
    };
    start();

    await userEvent.click(await screen.findByRole('button', { name: 'Set up with Ask Daoris' }));
    const cards = await screen.findByRole('list', { name: 'what Ask Daoris proposes' });
    await userEvent.click(within(cards).getByRole('button', { name: 'Go there' }));

    const drawer = await screen.findByRole('dialog', { name: 'Add a repository to this machine' });
    expect(within(drawer).getByLabelText('Workspace')).toHaveValue('work');
    expect(within(drawer).getByRole('button', { name: 'Register' })).toBeDisabled();
  });
});
