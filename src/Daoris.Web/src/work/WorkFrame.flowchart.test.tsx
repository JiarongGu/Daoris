import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The attended session's run opens from its head's track, the side bar holding no Workflow view of its own: the run's flowchart in a
// drawer, asked of the driver only while it is open, with *this session* on its own step. The Work frame as a page over a mocked
// bridge, as the rest of its suite runs.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', async () => ({ ...(await import('../test/sliceShell')).mockedShell(invoke), useShenoraEvent: () => {} }));

import '../locales/i18n';
import type { FrameIntent } from '../menus/commands';
import { answerOf, GRAPH_RUN_GO_AHEAD, IN_REVIEW, WORKING } from '../workflow/runFixtures';
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
  id: 's1', quest: 'q1', repository: 'engine', door: 'claude-code', state: 'working',
  kind: 'driven', created: '2026-10-09T09:00:00Z', updated: '2026-10-09T09:05:00Z',
};

let RUN = answerOf(WORKING);

const client = () => new QueryClient({ defaultOptions: { queries: { retry: false } } });
const asked = () => invoke.mock.calls.filter(([module, route]) => module === 'DAORIS.DRIVER' && route === 'WORKFLOW_RUN');

/** The session's place in the groups' answer, with its run's passage, which its head's track draws. */
const GROUPED = {
  session: 's1', group: 'working', shown: 'working', archived: false, teammate: false,
  passage: { run: 'q1', workflow: 'release', version: 1, steps: [{ node: 'write', kind: 'work', mark: 'current', attempt: 1, since: '2026-10-09T09:00:00Z' }] },
};

/** Opens the run's flowchart from the head's track, and answers the drawer it opens in. */
async function openChart() {
  const track = await screen.findByRole('navigation', { name: 'Its workflow: release' });
  await userEvent.click(within(track).getByRole('button', { name: 'Flowchart' }));
  return screen.findByRole('dialog', { name: 'Its run’s flowchart' });
}

describe("the session's run, opened from its track", () => {
  beforeEach(() => {
    RUN = answerOf(WORKING);
    // The side bar open, as a viewer who opened it once keeps it.
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
      if (type === 'SESSION_GROUPS') return { sessions: [GROUPED] };
      return { drivable: ['engine'], holds: [], trees: [], running: ['s1'] };
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('is no view of the side bar, and opens from the track, asked of the driver only once open', async () => {
    render(
      <QueryClientProvider client={client()}>
        <Tooltip.Provider><WorkFrame selected="s1" onSelect={vi.fn()} notify={() => {}} /></Tooltip.Provider>
      </QueryClientProvider>,
    );

    const side = await screen.findByRole('complementary', { name: 'right side bar' });
    // The session's record, which the side bar follows, is pushed in as the window holds it: the tabs are pressed once it is here.
    await screen.findByRole('heading', { level: 1, name: 'Fix the header' });
    const tabs = within(side).getAllByRole('tab').map((tab) => tab.getAttribute('aria-label'));
    expect(tabs).not.toContain('Workflow');
    expect(asked()).toHaveLength(0);

    const chart = await openChart();

    expect(await within(chart).findByText('At The work · working')).toBeInTheDocument();
    expect(asked()[0]?.[2]).toEqual(expect.objectContaining({ payload: { session: 's1' } }));
    const work = within(chart).getAllByRole('listitem').find((item) => item.getAttribute('data-step') === 'work')!;
    expect(work).toHaveTextContent('this session');
    expect(work).toHaveTextContent('Being worked on by claude-code.');
  });

  it('attaches the session mark and owned review controls to saved step ids', async () => {
    RUN = answerOf({ ...IN_REVIEW, at: 'inspect', workflow: { ...IN_REVIEW.workflow, id: 'release', boundVersion: 1 },
      steps: IN_REVIEW.steps.map((step) => ({ ...step, step: { ...step.step,
        id: step.step.kind === 'work' ? 'write' : step.step.kind === 'look' ? 'inspect' : step.step.id } })) });
    const previous = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...args: unknown[]) => {
      if (type === 'SESSION_GROUPS') return { sessions: [{ ...GROUPED, group: 'review', shown: 'completed',
        lands: { branch: 'daoris/s1', tree: 's1', commits: 1, uncommitted: 0 } }] };
      if (type === 'LANDING') return { session: 's1', form: 'merge', target: 'main',
        review: { state: 'not-shown', environment: 'local', says: 'Waits for your look.' } };
      return previous(module, type, ...args);
    });
    render(<QueryClientProvider client={client()}><Tooltip.Provider>
      <WorkFrame selected="s1" onSelect={vi.fn()} notify={() => {}} />
    </Tooltip.Provider></QueryClientProvider>);
    const side = await openChart();
    await within(side).findByText('Release workflow v1');
    const rows = within(side).getAllByRole('listitem');
    expect(rows.find((row) => row.getAttribute('data-step') === 'write')).toHaveTextContent('this session');
    const inspect = rows.find((row) => row.getAttribute('data-step') === 'inspect')!;
    expect(await within(inspect).findByRole('button', { name: 'Set it up in local' })).toBeInTheDocument();
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
    // The session attended, its head drawn, the side bar on the timeline it opens on.
    await screen.findByRole('heading', { level: 1 });
    expect(within(side).getByRole('tab', { name: 'Timeline' })).toHaveAttribute('aria-selected', 'true');
    act(() => ask('workflow'));

    const chart = await screen.findByRole('dialog', { name: 'Its run’s flowchart' });
    expect(await within(chart).findByText((_, element) => element?.matches('p') === true
      && element.textContent === 'At Your look in dev · waiting on tester: you')).toBeInTheDocument();
    const look = within(chart).getAllByRole('listitem').find((item) => item.getAttribute('data-step') === 'look')!;
    expect(look).toHaveAttribute('aria-current', 'step');
  });

  // A graph run's go-ahead nobody raised is passed over from the view, the press the driver's `WORKFLOW_NODE`.
  it("passes a graph run's go-ahead over from the view, with the person's words, through the driver's press", async () => {
    RUN = answerOf(GRAPH_RUN_GO_AHEAD);
    const previous = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, ...args: unknown[]) => {
      if (type === 'WORKFLOW_NODE') return { run: 'q1', node: 'release', act: 'skip', done: true };
      return previous(module, type, ...args);
    });
    render(<QueryClientProvider client={client()}><Tooltip.Provider>
      <WorkFrame selected="s1" onSelect={vi.fn()} notify={() => {}} />
    </Tooltip.Provider></QueryClientProvider>);
    const side = await openChart();
    const release = await waitFor(() => within(side).getAllByRole('listitem').find((item) => item.getAttribute('data-step') === 'release')!);

    await userEvent.click(within(release).getByRole('button', { name: 'Not needed this run…' }));
    await userEvent.type(within(side).getByRole('textbox', { name: 'why it is not needed' }), 'nobody raises it');
    await userEvent.click(within(side).getByRole('button', { name: 'Not needed this run' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_NODE',
      { payload: { run: 'q1', node: 'release', act: 'skip', words: 'nobody raises it' } }));
  });
});
