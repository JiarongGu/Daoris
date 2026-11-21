import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The terminal as a view of the Work frame's regions, and what opens the panel it stands in: the menu's *New terminal* starts
// one where the frame says it starts. The bridge is mocked as present, as the rest of the frame's suite mocks it; the renderer
// is stood in for, as the view's own suite does.

const { invoke, eventHandlers, createScreen } = vi.hoisted(() => ({
  invoke: vi.fn(),
  eventHandlers: new Map<string, (payload: unknown) => void>(),
  createScreen: vi.fn(() => ({
    attach: () => {}, write: () => {}, onData: () => () => {}, fit: () => ({ cols: 80, rows: 24 }),
    look: () => {}, focus: () => {}, dispose: () => {},
  })),
}));

vi.mock('@shenora/react', async () => ({
  ...(await import('../test/sliceShell')).mockedShell(invoke),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));
vi.mock('./terminals/terminalScreen', () => ({ createScreen }));

import '../locales/i18n';
import { WorkFrame } from './WorkFrame';

const REGISTRY = [
  { repository: 'zeta', adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 0, root: 'C:/somewhere/zeta' },
  { repository: 'mirror', adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 0 },
  { repository: 'engine', adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 1, root: 'C:/somewhere/engine' },
];

const DRIVEN = {
  id: 's1a2b3c4', quest: null, repository: 'zeta', door: 'claude-code', state: 'working',
  kind: 'driven', created: '2026-09-02T00:00:00Z', updated: '2026-09-02T01:00:00Z',
};
const IN_A_TREE = { ...DRIVEN, id: 'tr33tr33', tree: 'C:/somewhere/.daoris/trees/zeta/q1' };

let SESSIONS: unknown[] = [];

function tree(selected: string | null, terminal: boolean, intent: 'newTerminal' | 'console' | null) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return (
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkFrame selected={selected} onSelect={vi.fn()} notify={() => {}} terminal={terminal} intent={intent} />
      </Tooltip.Provider>
    </QueryClientProvider>
  );
}

/** The frame as it starts, and the menu's *New terminal* pressed once what it reads has arrived. */
function frame(selected: string | null, terminal = true) {
  const view = render(tree(selected, terminal, null));
  return {
    ...view,
    newTerminal: () => view.rerender(tree(selected, terminal, 'newTerminal')),
    console: () => view.rerender(tree(selected, terminal, 'console')),
    attend: (next: string | null) => view.rerender(tree(next, terminal, null)),
  };
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

  it('has no panel at a start and opens nothing', async () => {
    frame('s1a2b3c4');

    await screen.findAllByText(/zeta/);
    expect(screen.queryByRole('region', { name: 'the panel' })).toBeNull();
    expect(opens()).toEqual([]);
  });

  it('opens with its first terminal at the height last kept, and its last closing shuts it', async () => {
    window.localStorage.setItem('daoris.panelHeight', '320');
    const { newTerminal } = frame('s1a2b3c4');
    await screen.findAllByText(/zeta/);

    newTerminal();

    expect(await screen.findByRole('separator', { name: 'panel height' })).toHaveAttribute('aria-valuenow', '320');
    expect(within(screen.getByRole('tablist', { name: 'views in the panel' })).getAllByRole('tab').map((tab) => tab.textContent))
      .toEqual(['Console', 'Terminal']);
    await userEvent.click(await screen.findByRole('button', { name: /^Close / }));
    await waitFor(() => expect(screen.queryByRole('region', { name: 'the panel' })).toBeNull());
    expect(window.localStorage.getItem('daoris.panelHeight')).toBe('320');
  });

  it('has no panel and starts nothing where no shell handed it in', async () => {
    const { newTerminal } = frame(null, false);
    await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).startsWith('/api/registry'))).toBe(true));

    newTerminal();

    expect(screen.queryByRole('region', { name: 'the panel' })).toBeNull();
    expect(opens()).toEqual([]);
  });

  it('shuts with its last terminal wherever the terminal stands', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    const { newTerminal } = frame('s1a2b3c4');
    await screen.findAllByText(/zeta/);
    newTerminal();
    await userEvent.pointer({ keys: '[MouseRight]', target: await screen.findByRole('tab', { name: 'Terminal' }) });
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Move Terminal to the right side bar' }));
    // The console is left in the panel, which stays open for it until the last terminal, now in the side bar, closes.
    expect(screen.getByRole('region', { name: 'the panel' })).toBeInTheDocument();

    await userEvent.click(within(screen.getByRole('complementary', { name: 'right side bar' })).getByRole('button', { name: /^Close PowerShell/ }));

    await waitFor(() => expect(screen.queryByRole('region', { name: 'the panel' })).toBeNull());
  });

  it('shuts a console that goes away instead of showing the terminal, which would start a shell nobody asked for', async () => {
    const { console: openConsole, attend } = frame('s1a2b3c4');
    await screen.findAllByText(/zeta/);
    openConsole();
    expect(await screen.findByRole('tab', { name: 'Console', selected: true })).toBeInTheDocument();

    attend(null);

    await waitFor(() => expect(screen.queryByRole('region', { name: 'the panel' })).toBeNull());
    expect(opens()).toEqual([]);
  });

  it('starts a shell when the person picks Terminal beside an open console with none running, and keeps the panel', async () => {
    const { console: openConsole } = frame('s1a2b3c4');
    await screen.findAllByText(/zeta/);
    openConsole();
    expect(await screen.findByRole('tab', { name: 'Console', selected: true })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('tab', { name: 'Terminal' }));

    await waitFor(() => expect(opens()).toHaveLength(1));
    expect(screen.getByRole('region', { name: 'the panel' })).toBeInTheDocument();
  });

  it("opens in the attended session's repository when its record names no tree of its own", async () => {
    const { newTerminal } = frame('s1a2b3c4');
    await screen.findAllByText(/zeta/);

    newTerminal();

    await waitFor(() => expect(opens()).toEqual([['DAORIS.TERMINAL', 'OPEN', { payload: { cwd: 'C:/somewhere/zeta' } }]]));
  });

  it("opens in the attended session's own tree when it has one", async () => {
    const { newTerminal } = frame('tr33tr33');
    // The record must have arrived before the terminal is asked for, as it has on the window.
    await screen.findAllByText(/zeta/);

    newTerminal();

    await waitFor(() => expect(opens()).toEqual([
      ['DAORIS.TERMINAL', 'OPEN', { payload: { cwd: 'C:/somewhere/.daoris/trees/zeta/q1' } }],
    ]));
  });

  it('opens in the first repository with a checkout here when nothing is attended', async () => {
    const { newTerminal } = frame(null);
    // The registry must have answered before the terminal is asked for, as it has on the window.
    await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).startsWith('/api/registry'))).toBe(true));
    await new Promise((resolve) => { setTimeout(resolve, 50); });

    newTerminal();

    await waitFor(() => expect(opens()).toEqual([['DAORIS.TERMINAL', 'OPEN', { payload: { cwd: 'C:/somewhere/engine' } }]]));
  });

  it('moves to the right side bar from its tab\'s menu, and keeps its shell', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    const { newTerminal } = frame('s1a2b3c4');
    await screen.findAllByText(/zeta/);
    newTerminal();
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
