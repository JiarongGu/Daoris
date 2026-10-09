import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// WORKFLOW1c (the workflow design §7): the session's side bar gains a view of its own, *Workflow*, beside the timeline and the
// review: the attended session's run, asked of the driver only while the view shows, with *this session* on its own step. The Work
// frame as a page over a mocked bridge, as the rest of its suite runs.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import '../i18n';
import type { FrameIntent } from '../commands';
import { answerOf, IN_REVIEW, WORKING } from '../workflow/runFixtures';
import { WorkFrame } from './WorkFrame';

const REGISTRY = [{
  repository: 'engine', adopted: true, registered: true, summary: 'the engine',
  owns: [], accepts: [], packs: [], entries: 1, root: 'C:/somewhere/engine',
}];
const QUESTS = [{
  id: 'q1', from: 'ask #a1', to: 'engine', title: 'Fix the header', body: '', status: 'Taken',
  filed: '2026-10-09T08:00:00Z', updated: '2026-10-09T09:00:00Z',
}];
const DRIVEN = {
  id: 's1', quest: 'q1', repository: 'engine', adapter: 'claude-code', state: 'working',
  kind: 'driven', created: '2026-10-09T09:00:00Z', updated: '2026-10-09T09:05:00Z',
};

let RUN = answerOf(WORKING);

const client = () => new QueryClient({ defaultOptions: { queries: { retry: false } } });
const asked = () => invoke.mock.calls.filter(([module, route]) => module === 'DAORIS.DRIVER' && route === 'WORKFLOW_RUN');

describe("the session's Workflow view", () => {
  beforeEach(() => {
    RUN = answerOf(WORKING);
    // The side bar open, as a viewer who opened it once keeps it (UX5 U7).
    window.localStorage.setItem('daoris.dockClosed', '0');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/sessions')) return Response.json([DRIVEN]);
      if (url.startsWith('/api/quests')) return Response.json(QUESTS);
      if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
      if (url.startsWith('/api/asks')) return Response.json([]);
      throw new Error(`unstubbed request: ${url}`);
    }));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module === 'DAORIS.DRIVER' && type === 'WORKFLOW_RUN') return RUN;
      return { drivable: ['engine'], holds: [], trees: [], running: ['s1'] };
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('stands beside the timeline and the review, and asks the driver only once it is shown', async () => {
    render(
      <QueryClientProvider client={client()}>
        <Tooltip.Provider><WorkFrame selected="s1" onSelect={vi.fn()} notify={() => {}} /></Tooltip.Provider>
      </QueryClientProvider>,
    );

    const side = await screen.findByRole('complementary', { name: 'right side bar' });
    const tabs = within(side).getAllByRole('tab').map((tab) => tab.getAttribute('aria-label'));
    expect(tabs.slice(0, 3)).toEqual(['Timeline', 'Review', 'Workflow']);
    expect(asked()).toHaveLength(0);

    await userEvent.click(within(side).getByRole('tab', { name: 'Workflow' }));

    await waitFor(() => expect(asked()[0]?.[2]).toEqual(expect.objectContaining({ payload: { session: 's1' } })));
    expect(await within(side).findByText('Workflow: the agent is working')).toBeInTheDocument();
    const work = within(side).getAllByRole('listitem').find((item) => item.getAttribute('data-step') === 'work')!;
    expect(work).toHaveTextContent('this session');
    expect(work).toHaveTextContent('Being worked on by claude-code.');
  });

  it('opens on the run when a door asks for it, as the application hands it a frame intent', async () => {
    RUN = answerOf(IN_REVIEW);
    // The application's intent, held as App holds it and cleared once taken; a door sends it once its session is attended.
    let ask: (intent: FrameIntent) => void = () => {};
    function Held() {
      const [intent, setIntent] = useState<FrameIntent | null>(null);
      ask = setIntent;
      return <WorkFrame selected="s1" onSelect={vi.fn()} notify={() => {}} intent={intent} onIntentTaken={() => setIntent(null)} />;
    }
    render(
      <QueryClientProvider client={client()}>
        <Tooltip.Provider><Held /></Tooltip.Provider>
      </QueryClientProvider>,
    );

    const side = await screen.findByRole('complementary', { name: 'right side bar' });
    // The session attended, its head drawn, the side bar on the timeline it opens on (FRAME6).
    await screen.findByRole('heading', { level: 1 });
    expect(within(side).getByRole('tab', { name: 'Timeline' })).toHaveAttribute('aria-selected', 'true');
    act(() => ask('workflow'));

    await waitFor(() => expect(within(side).getByRole('tab', { name: 'Workflow' })).toHaveAttribute('aria-selected', 'true'));
    expect(await within(side).findByText('Workflow: waits for your look')).toBeInTheDocument();
    const look = within(side).getAllByRole('listitem').find((item) => item.getAttribute('data-step') === 'look')!;
    expect(look).toHaveAttribute('aria-current', 'step');
  });
});
