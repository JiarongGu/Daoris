import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The views in SHELL mode — the bridge mocked as present, so the controls that only a desktop with a
// driver may render actually render, and land on the DAORIS.DRIVER module. Browser mode needs no
// twin suite: every other test in this project runs with no transport, and the absence of these
// controls there is asserted by their queries never firing (an unstubbed fetch throws).

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady: () => Promise.resolve() }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady: () => Promise.resolve() } }),
  useShenoraEvent: () => {},
}));

import { ProjectsView } from './ProjectsView';
import { QuestsView } from './QuestsView';

const DRIVER_STATE = {
  configPath: 'C:/home/.daoris/driver.json',
  drivable: [], holds: [], cap: 2, adapter: 'claude-code', pollSeconds: 15,
  running: ['s1a2b3c4'],
};

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

function show(node: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>{node}</Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('the shell-attached platform', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) =>
      type === 'STATE' ? DRIVER_STATE : DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
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
