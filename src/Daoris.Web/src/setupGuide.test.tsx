import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// SETUP1a (D97): Settings → Get started on the desktop, the bridge mocked as present, so the steps are
// read off the machine the driver describes — the same reading Ask Daoris's starters make.

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

import './i18n';
import { SettingsView } from './SettingsView';

type World = {
  registry: { repository: string; workspace?: string }[];
  state: Record<string, unknown>;
  harnesses: unknown[];
  lines: unknown[];
  landings: unknown[];
};

const FRESH: World = {
  registry: [],
  state: { drivable: [], holds: [], trees: [], running: [], helperAdapter: '', intakeAdapter: '' },
  harnesses: [{ harness: 'claude-code', present: false, ownLogin: 'unknown' }],
  lines: [],
  landings: [],
};

let world: World = FRESH;

function machine(next: World) {
  world = next;
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.startsWith('/api/registry')) return Response.json(world.registry.map((row) => ({ ...row, registered: true })));
    if (url.startsWith('/api/sessions')) return Response.json([]);
    if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical only', note: '' });
    throw new Error(`unstubbed request: ${url}`);
  }));
  invoke.mockImplementation(async (_module: string, type: string) => {
    switch (type) {
      case 'STATE': return world.state;
      case 'HARNESSES': return { settingsPath: 'harnesses.json', adapter: 'claude-code', harnesses: world.harnesses };
      case 'LINES': return { lines: world.lines, landings: world.landings };
      default: return {};
    }
  });
}

function show(onGo = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <SettingsView notify={() => {}} section="start" onGo={onGo} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
  return onGo;
}

describe('Get started, on the desktop', () => {
  beforeEach(() => machine(FRESH));
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('leads the list of domains', async () => {
    show();

    const domains = screen.getByRole('navigation', { name: 'Settings domains' });
    await within(domains).findByRole('button', { name: 'Driver' });
    expect(within(domains).getAllByRole('button')[0]).toHaveTextContent('Get started');
    expect(within(domains).getByRole('button', { name: 'Get started' })).toHaveAttribute('aria-current', 'page');
  });

  it('reads a fresh machine as every step to do, and opens the screen each is done on', async () => {
    const onGo = show();

    const steps = await screen.findByRole('list', { name: 'setup steps' });
    expect(within(steps).getAllByRole('listitem')).toHaveLength(6);
    expect(screen.getByText('0 of 5 required steps done')).toBeInTheDocument();
    const agent = within(steps).getByRole('listitem', { name: '1. An agent' });
    expect(within(agent).getByText('to do')).toBeInTheDocument();

    await userEvent.click(within(steps).getByRole('button', { name: 'Add repository…' }));
    expect(onGo).toHaveBeenCalledWith({ view: 'projects', drawer: 'add' });
  });

  it('reads an agent signed in and a repository registered as two steps done', async () => {
    machine({
      ...FRESH,
      registry: [{ repository: 'engine', workspace: 'aurora' }],
      harnesses: [{ harness: 'claude-code', present: true, ownLogin: 'in', product: 'Claude Code' }],
    });
    show();

    const steps = await screen.findByRole('list', { name: 'setup steps' });
    expect(await screen.findByText('2 of 5 required steps done')).toBeInTheDocument();
    expect(within(within(steps).getByRole('listitem', { name: '1. An agent' })).getByText('done')).toBeInTheDocument();
    expect(within(within(steps).getByRole('listitem', { name: '3. A workspace and its repositories' })).getByText('done'))
      .toBeInTheDocument();
  });
});
