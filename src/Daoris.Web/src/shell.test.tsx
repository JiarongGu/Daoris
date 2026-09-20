import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The views in SHELL mode — the bridge mocked as present, so the controls that only a desktop with a
// driver may render actually render, and land on the DAORIS.DRIVER module. Browser mode needs no
// twin suite: every other test in this project runs with no transport, and the absence of these
// controls there is asserted by their queries never firing (an unstubbed fetch throws).

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  // The push channel's seam: handlers land here by "module.type", and a test fires them as the host.
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import { ProjectsView } from './ProjectsView';
import { QuestsView } from './QuestsView';
import { ShellSignals } from './ShellSignals';
import { keys } from './queries';

const DRIVER_STATE = { drivable: [], holds: [], running: ['s1a2b3c4'] };

const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 1 },
];
const QUESTS = [{
  id: 'abc123', from: 'game', to: 'engine',
  title: 'Expose a streaming budget', body: 'World streaming needs a per-frame cap.',
  status: 'Open', filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
}];
const SESSIONS = [{
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'working',
  created: '2026-09-02T00:00:00Z', updated: '2026-09-02T01:00:00Z',
}];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  if (url.startsWith('/api/repositories')) return Response.json([]);
  throw new Error(`unstubbed request: ${url}`);
}

function show(node: React.ReactElement, client = new QueryClient({ defaultOptions: { queries: { retry: false } } })) {
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>{node}</Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('the shell-attached platform', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    notifyReady.mockClear();
    eventHandlers.clear();
  });

  it('projects grow the per-machine driver controls, landing on DAORIS.DRIVER', async () => {
    show(<ProjectsView notify={() => {}} />);

    const drive = await screen.findByLabelText('drive on this machine');
    await userEvent.click(drive);

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_DRIVABLE', {
      payload: { repository: 'engine', drivable: true },
    });
  });

  it('hold appears only once a repository is drivable — a hold on nothing is noise', async () => {
    show(<ProjectsView notify={() => {}} />);

    await screen.findByLabelText('drive on this machine');
    expect(screen.queryByLabelText('hold')).not.toBeInTheDocument();
  });

  it('holding a drivable repository lands on DAORIS.DRIVER with its own payload key', async () => {
    invoke.mockImplementation(async () => ({ ...DRIVER_STATE, drivable: ['engine'] }));
    show(<ProjectsView notify={() => {}} />);

    await userEvent.click(await screen.findByLabelText('hold'));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HOLD', {
      payload: { repository: 'engine', held: true },
    });
  });

  it('a running session offers stop, and stop names the session', async () => {
    show(<QuestsView notify={() => {}} />);

    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: 'stop session' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', {
      payload: { id: 's1a2b3c4' },
    });
  });
});

describe('the shell push channel (ShellSignals)', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    notifyReady.mockClear();
    eventHandlers.clear();
  });

  it('announces readiness once — the kit buffers host events until this handshake', () => {
    show(<ShellSignals notify={() => {}} />);
    expect(notifyReady).toHaveBeenCalledTimes(1);
  });

  it('a tick becomes toasts, and everything a tick can change refetches', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const notify = vi.fn();
    show(<ShellSignals notify={notify} />, client);

    eventHandlers.get('DAORIS.DRIVER_TICK')!({ events: ['engine  spawned s1a2b3c4', 'sync  fed 2'] });

    expect(notify).toHaveBeenCalledWith('engine  spawned s1a2b3c4');
    expect(notify).toHaveBeenCalledWith('sync  fed 2');
    for (const key of [keys.allSessions, keys.allQuests, keys.driver]) {
      expect(invalidate).toHaveBeenCalledWith({ queryKey: key });
    }
  });

  it("a driver error arrives as an error toast, the driver's own sentence verbatim", () => {
    const notify = vi.fn();
    show(<ShellSignals notify={notify} />);

    eventHandlers.get('DAORIS.DRIVER_ERROR')!({ message: 'the loop hit a wall' });

    expect(notify).toHaveBeenCalledWith('the loop hit a wall', 'error');
  });
});
