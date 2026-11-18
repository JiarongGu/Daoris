import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// *What needs you*'s row of a release step waiting on its approver draws the run's view's approval card, read from the run its work
// quest names, beside the step's own presses; over a mocked shell.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
    ...(await import('../../test/sliceShell')).mockedShell(invoke, { notifyReady: () => Promise.resolve() }),
    useShenoraEvent: () => {},
    useWindowMaximized: () => false,
  };
});

import '../../locales/i18n';
import { keys } from '../../data/queries';
import { WorkspaceScopeProvider } from '../../data/scope';
import { composeAttention } from '../../bridge/shell';
import { ORIGIN, sentTo, wireRelease } from '../../test/doubles/releaseShell';
import { answerOf, GRAPH_RUN_PROD_APPROVAL, PROD_APPROVAL_PAGE, PROD_APPROVAL_WORDS } from '../../workflow/runFixtures';
import { AttentionBand } from './AttentionBand';

/** The work workspace's run waiting on its production stage's approval, as .NET pushes its row. */
const ROW = {
  kind: 'step', id: 'q7/release-approve', since: '2026-11-19T09:20:00Z', workspace: 'work', where: 'portal',
  step: { run: 'q7', node: 'release-approve', why: 'actor', role: 'approver', holder: 'person', presses: ['skip', 'stop'], words: PROD_APPROVAL_WORDS, url: PROD_APPROVAL_PAGE, work: 'q7' },
};

function start() {
  vi.stubGlobal('fetch', vi.fn(async () => Response.json([])));
  wireRelease(invoke, { workspace: 'work' });
  const shell = invoke.getMockImplementation()!;
  invoke.mockImplementation(async (module: string, type: string, options?: unknown) =>
    type === 'WORKFLOW_RUN' ? answerOf(GRAPH_RUN_PROD_APPROVAL) : shell(module, type, options));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(keys.attention, composeAttention(new Map<string, unknown>([['rows', { 'step:q7/release-approve': ROW }], ['sessions', {}]]), true));
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <AttentionBand notify={vi.fn()} doors={{}} />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  invoke.mockReset();
});

describe('a release step waiting on its approver, on What needs you', () => {
  it('draws the approval card beside Not needed this run, and Approve sends the page’s release and the stage', async () => {
    start();
    const item = await screen.findByRole('listitem', { name: '#q7' });
    expect(within(item).getByRole('button', { name: 'Not needed this run…' })).toBeInTheDocument();

    await userEvent.click(await within(item).findByRole('button', { name: 'Approve' }));
    expect(sentTo(invoke, 'WORKFLOW_RUN')).toEqual([{ quest: 'q7' }]);
    await waitFor(() => expect(sentTo(invoke, 'PLUGIN_COMMAND')).toEqual([{
      id: 'daoris-azure-devops', command: 'approve-release', args: { origin: ORIGIN, release: '13515', stage: 'Prod', decision: 'approve', comment: '' },
      answers: {}, place: 'portal', language: 'en',
    }]));
  });
});
