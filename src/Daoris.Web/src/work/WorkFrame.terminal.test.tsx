import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// CONSOLE4b (D96): the terminal as a view of the Work frame's regions — in the panel beside the console
// by default, opened where the frame says a terminal starts. The bridge is mocked as present, as the rest
// of the frame's suite mocks it; the renderer is stood in for, as the view's own suite does.

const { invoke, eventHandlers, createScreen } = vi.hoisted(() => ({
  invoke: vi.fn(),
  eventHandlers: new Map<string, (payload: unknown) => void>(),
  createScreen: vi.fn(() => ({
    attach: () => {}, write: () => {}, onData: () => () => {}, fit: () => ({ cols: 80, rows: 24 }),
    look: () => {}, focus: () => {}, dispose: () => {},
  })),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));
vi.mock('./terminalScreen', () => ({ createScreen }));

import '../i18n';
import { WorkFrame } from './WorkFrame';

const REGISTRY = [
  { repository: 'zeta', adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 0, root: 'C:/somewhere/zeta' },
  { repository: 'mirror', adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 0 },
  { repository: 'engine', adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 1, root: 'C:/somewhere/engine' },
];

const DRIVEN = {
  id: 's1a2b3c4', quest: null, repository: 'zeta', adapter: 'claude-code', state: 'working',
  kind: 'driven', created: '2026-09-02T00:00:00Z', updated: '2026-09-02T01:00:00Z',
};
const IN_A_TREE = { ...DRIVEN, id: 'tr33tr33', tree: 'C:/somewhere/.daoris/trees/zeta/q1' };

let SESSIONS: unknown[] = [];

function frame(selected: string | null, terminal = true) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkFrame selected={selected} onSelect={vi.fn()} notify={() => {}} terminal={terminal} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

const opens = () => invoke.mock.calls.filter(([module, type]) => module === 'DAORIS.TERMINAL' && type === 'OPEN');

describe('the terminal in the Work frame (CONSOLE4b)', () => {
  beforeEach(() => {
    SESSIONS = [DRIVEN, IN_A_TREE];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
      if (url.startsWith('/api/quests')) return Response.json([]);
      if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
      throw new Error(`unstubbed request: ${url}`);
    }));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module === 'DAORIS.TERMINAL' && type === 'OPEN') return { id: 't-frame', shell: 'pwsh', cwd: 'C:/somewhere/zeta' };
      if (module === 'DAORIS.TERMINAL') return {};
      return { drivable: [], holds: [], trees: [], running: [] };
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    window.localStorage.clear();
  });

  it('stands in the panel beside the console, and opens nothing until it is shown', async () => {
    frame(null);

    const panel = await screen.findByRole('tablist', { name: 'views in the panel' });
    expect(within(panel).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Console', 'Terminal']);
    expect(opens()).toEqual([]);
  });

  it('is not a view at all where no shell handed it in', async () => {
    frame(null, false);

    await screen.findByRole('region', { name: 'the panel' });
    expect(screen.queryByRole('tab', { name: 'Terminal' })).toBeNull();
  });

  it('opens in the attended session\'s repository when its record names no tree of its own', async () => {
    frame('s1a2b3c4');

    await userEvent.click(await screen.findByRole('tab', { name: 'Terminal' }));

    await waitFor(() => expect(opens()).toEqual([['DAORIS.TERMINAL', 'OPEN', { payload: { cwd: 'C:/somewhere/zeta' } }]]));
  });

  it('opens in the attended session\'s own tree when it has one', async () => {
    frame('tr33tr33');
    // The record must have arrived before the tab is pressed, as it has on the window.
    await screen.findAllByText(/zeta/);

    await userEvent.click(await screen.findByRole('tab', { name: 'Terminal' }));

    await waitFor(() => expect(opens()).toEqual([
      ['DAORIS.TERMINAL', 'OPEN', { payload: { cwd: 'C:/somewhere/.daoris/trees/zeta/q1' } }],
    ]));
  });

  it('opens in the first repository with a checkout here when nothing is attended', async () => {
    frame(null);
    // The registry must have answered before the tab is pressed, as it has on the window.
    await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).startsWith('/api/registry'))).toBe(true));
    await new Promise((resolve) => { setTimeout(resolve, 50); });

    await userEvent.click(await screen.findByRole('tab', { name: 'Terminal' }));

    await waitFor(() => expect(opens()).toEqual([['DAORIS.TERMINAL', 'OPEN', { payload: { cwd: 'C:/somewhere/engine' } }]]));
  });

  it('moves to the right side bar from its tab\'s menu, and keeps its shell', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    frame('s1a2b3c4');
    await userEvent.click(await screen.findByRole('tab', { name: 'Terminal' }));
    await waitFor(() => expect(opens()).toHaveLength(1));

    await userEvent.pointer({ keys: '[MouseRight]', target: screen.getByRole('tab', { name: 'Terminal' }) });
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Move Terminal to the right side bar' }));

    expect(within(screen.getByRole('tablist', { name: 'right side bar' })).getByRole('tab', { name: 'Terminal', selected: true }))
      .toBeInTheDocument();
    // The same shell, drawn in its new region: no second opened, none closed.
    expect(opens()).toHaveLength(1);
    expect(invoke.mock.calls.filter(([module, type]) => module === 'DAORIS.TERMINAL' && type === 'CLOSE')).toEqual([]);
  });
});
