import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, renderHook, screen, within } from '@testing-library/react';
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
import { AT_START, useSetupAtStart } from './setupGuide';

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

function show(onGo = vi.fn(), onAskSetup = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <SettingsView notify={() => {}} section="start" onGo={onGo} onAskSetup={onAskSetup} />
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
    window.localStorage.clear();
  });

  it('leads the list of domains', async () => {
    show();

    const domains = screen.getByRole('navigation', { name: 'Settings domains' });
    await within(domains).findByRole('button', { name: 'Driver' });
    expect(within(domains).getAllByRole('button')[0]).toHaveTextContent('Setup');
    expect(within(domains).getByRole('button', { name: 'Setup' })).toHaveAttribute('aria-current', 'page');
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

  /** SETUP1b: *Don't open at start* is the viewer's own convenience, kept in this browser's storage. */
  it('keeps "Don\'t open at start" as this viewer\'s choice', async () => {
    show();

    const box = await screen.findByRole('checkbox', { name: "Don't open at start" });
    expect(box).not.toBeChecked();
    await userEvent.click(box);
    expect(window.localStorage.getItem(AT_START)).toBe('off');
    expect(box).toBeChecked();
    await userEvent.click(box);
    expect(window.localStorage.getItem(AT_START)).toBeNull();
  });

  /** SETUP1b: the head hands Ask Daoris a first message, once it has an agent to run on. */
  it('hands Ask Daoris the steps not yet done, once it has an agent', async () => {
    machine({ ...FRESH, state: { ...FRESH.state, helperAdapter: 'claude-code-acp' } });
    const onAskSetup = vi.fn();
    show(vi.fn(), onAskSetup);

    await userEvent.click(await screen.findByRole('button', { name: 'Set up with Ask Daoris' }));
    expect(onAskSetup).toHaveBeenCalledWith(expect.stringMatching(
      /^Walk me through setting up Daoris on this machine, one step at a time\. Not done yet: 1\. An agent; 3\. A workspace/));
  });
});

/**
 * SETUP1b (D97 §2): a machine missing any of the first three steps opens on Get started at start —
 * once per start, when the machine has been read, and never pulling the person from where they went.
 */
describe('opening at start', () => {
  afterEach(() => window.localStorage.clear());

  const hook = (initial: { settled: boolean; needed: boolean; view: string }) => {
    const open = vi.fn();
    const result = renderHook((props: typeof initial) => useSetupAtStart({ ...props, open }), { initialProps: initial });
    return { open, rerender: result.rerender };
  };

  it('opens once the machine is read, and never again that start', () => {
    const { open, rerender } = hook({ settled: false, needed: true, view: 'overview' });
    expect(open).not.toHaveBeenCalled();

    rerender({ settled: true, needed: true, view: 'overview' });
    expect(open).toHaveBeenCalledOnce();

    // The person leaves it, and it stays left: nothing brings them back.
    rerender({ settled: true, needed: true, view: 'quests' });
    rerender({ settled: true, needed: true, view: 'overview' });
    expect(open).toHaveBeenCalledOnce();
  });

  it('does not open where the first steps are done, nor later that start when one comes undone', () => {
    const { open, rerender } = hook({ settled: true, needed: false, view: 'overview' });
    rerender({ settled: true, needed: true, view: 'overview' });
    expect(open).not.toHaveBeenCalled();
  });

  it('never pulls the person from where they went before the machine was read', () => {
    const { open, rerender } = hook({ settled: false, needed: true, view: 'overview' });
    rerender({ settled: false, needed: true, view: 'projects' });
    rerender({ settled: true, needed: true, view: 'projects' });
    expect(open).not.toHaveBeenCalled();
  });

  it('does not open for a viewer who turned it off', () => {
    window.localStorage.setItem(AT_START, 'off');
    const { open, rerender } = hook({ settled: false, needed: true, view: 'overview' });
    rerender({ settled: true, needed: true, view: 'overview' });
    expect(open).not.toHaveBeenCalled();
  });

  /** Storage is a convenience: refused, the guide still opens as though nothing was chosen. */
  it('opens where storage is refused, as though nothing was chosen', () => {
    const refuse = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('refused'); });
    try {
      const { open, rerender } = hook({ settled: false, needed: true, view: 'overview' });
      rerender({ settled: true, needed: true, view: 'overview' });
      expect(open).toHaveBeenCalledOnce();
    } finally {
      refuse.mockRestore();
    }
  });
});
