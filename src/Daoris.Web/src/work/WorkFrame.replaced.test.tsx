import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// MODELROLE1d2 (D205 §3.5(b)): a new conversation its agent refused at the open went on in another, which the driver says as
// `SESSION_REPLACED`; the Work frame showing the refused one follows to its replacement, as it follows a new part, and moves for no
// other. The bridge is mocked as present, as the rest of the frame's suite mocks it.

const { invoke, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import '../locales/i18n';
import { WorkFrame } from './WorkFrame';

const REGISTRY = [{ repository: 'engine', adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 0, root: 'C:/somewhere/engine' }];

const chat = (id: string, state: string, created: string) => ({
  id, quest: null, repository: 'engine', adapter: 'acp-stub', state, kind: 'chat', created, updated: created,
});

function frame(selected: string, onSelect: (id: string | null) => void) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkFrame selected={selected} onSelect={onSelect} notify={() => {}} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('the Work frame follows a conversation replaced at its open (MODELROLE1d2)', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/sessions')) {
        return Response.json([chat('c1', 'failed', '2026-10-25T08:00:00Z'), chat('c2', 'working', '2026-10-25T08:00:01Z')]);
      }
      if (url.startsWith('/api/quests')) return Response.json([]);
      if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
      throw new Error(`unstubbed request: ${url}`);
    }));
    invoke.mockImplementation(async () => ({ drivable: [], holds: [], trees: [], running: [] }));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    window.localStorage.clear();
  });

  it('shows the replacement where it showed the refused conversation, and moves for no other', async () => {
    const onSelect = vi.fn();
    frame('c1', onSelect);
    await screen.findAllByText(/engine/);
    const replaced = eventHandlers.get('DAORIS.SESSION_REPLACED');
    expect(replaced).toBeDefined();

    act(() => replaced!({ from: 'x1', to: 'x2' }));
    expect(onSelect).not.toHaveBeenCalledWith('x2');

    act(() => replaced!({ from: 'c1', to: 'c2' }));
    expect(onSelect).toHaveBeenCalledWith('c2');
  });
});
