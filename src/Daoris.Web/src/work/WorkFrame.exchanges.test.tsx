import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// Who the attended session worked with reads in its one timeline, the side bar holding no At work view and the page no fold of its
// own: a quest it published is a row at its asking and one at its close, between the turns it fell between. The Work frame as a
// page over a mocked bridge, as the rest of its suite runs.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', async () => ({ ...(await import('../test/sliceShell')).mockedShell(invoke), useShenoraEvent: () => {} }));

import '../locales/i18n';
import { WorkFrame } from './WorkFrame';

const REGISTRY = [{
  repository: 'engine', adopted: true, registered: true, summary: 'the engine',
  owns: [], accepts: [], packs: [], entries: 1, root: 'C:/somewhere/engine',
}];
const QUESTS = [
  {
    id: 'q1', from: 'ask #a1', to: 'engine', title: 'Fix the header', body: '', status: 'Taken',
    filed: '2026-10-09T08:00:00Z', updated: '2026-10-09T09:00:00Z',
  },
  {
    id: 'def456', from: 'engine', to: 'game', title: 'Read the budget from the level file', body: '', status: 'Done',
    filed: '2026-10-09T09:30:00Z', updated: '2026-10-09T09:50:00Z', publishedBy: 's1',
  },
];
const DRIVEN = {
  id: 's1', quest: 'q1', repository: 'engine', door: 'claude-code', state: 'working',
  kind: 'driven', created: '2026-10-09T09:00:00Z', updated: '2026-10-09T09:05:00Z',
};
const HISTORY = {
  session: 's1', earlier: false, latest: 4, events: [
    { seq: 1, at: '2026-10-09T09:20:00Z', kind: 'user', origin: 'person', text: 'Make the level file carry the budget.' },
    { seq: 2, at: '2026-10-09T09:25:00Z', kind: 'message', id: 'm1', text: 'Asking the game repository for it.' },
    { seq: 3, at: '2026-10-09T09:40:00Z', kind: 'user', origin: 'person', text: 'Go on.' },
    { seq: 4, at: '2026-10-09T09:55:00Z', kind: 'message', id: 'm2', text: 'It reads level.budget now.' },
  ],
};

describe('who the attended session worked with, in its timeline', () => {
  beforeEach(() => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/sessions')) return Response.json([DRIVEN]);
      if (url.startsWith('/api/quests')) return Response.json(QUESTS);
      if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
      if (url.startsWith('/api/asks')) return Response.json([]);
      throw new Error(`unstubbed request: ${url}`);
    }));
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_HISTORY'
      ? HISTORY
      : { drivable: ['engine'], holds: [], trees: [], running: ['s1'] }));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('draws a quest it published where it was asked and where it closed, opens it, and keeps no At work view or fold', async () => {
    const onOpenQuest = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <Tooltip.Provider><WorkFrame selected="s1" onSelect={vi.fn()} notify={() => {}} onOpenQuest={onOpenQuest} /></Tooltip.Provider>
      </QueryClientProvider>,
    );

    const asking = (await screen.findByText('Asked #def456 of game')).closest<HTMLElement>('[data-exchange-row]')!;
    expect(asking).toHaveAttribute('data-side', 'asked');
    expect(screen.getByText('#def456: done').closest('[data-exchange-row]')).toHaveAttribute('data-side', 'back');
    await userEvent.click(within(asking).getByRole('button', { name: 'Open the quest' }));
    expect(onOpenQuest).toHaveBeenCalledWith('def456');

    expect(screen.queryByRole('region', { name: 'Who it worked with' })).toBeNull();
    const side = screen.getByRole('complementary', { name: 'right side bar' });
    expect(within(side).getAllByRole('tab').map((tab) => tab.getAttribute('aria-label'))).not.toContain('At work');
  });
});
