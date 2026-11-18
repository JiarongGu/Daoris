import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient } from '@tanstack/react-query';

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
    useShenoraEvent: () => {},
    useWindowMaximized: () => false,
  };
});

import '../../locales/i18n';
import type { Session } from '../../data/api';
import { keys } from '../../data/queries';
import { composeAttention } from '../../bridge/attention';
import { groupsKey } from '../../bridge/sessions/sessionGroups';
import { ORIGIN, sentTo, wireRelease } from '../../test/doubles/releaseShell';
import { show } from '../../test/shellHarness';
import { answerOf, GRAPH_RUN_PROD_APPROVAL, PROD_APPROVAL_PAGE, PROD_APPROVAL_WORDS } from '../../workflow/runFixtures';
import { WaitStrip } from '../waits/WaitStrip';
import { useRunWaitDoors } from './useRunWaitDoors';

// The session page's dock at the work workspace's run waiting on its production stage's approval, read as the page reads it: the
// session's run waits as Sessions places them, *What needs you*'s step row, the session's run as `WORKFLOW_RUN` answers it, the plugin
// as `PLUGINS` lists it and the run's repository's checkouts as `GIT_BRANCHES` lists them, the work workspace's after another's.

const SESSION: Session = {
  id: 's1', quest: 'q7', repository: 'portal', door: 'claude-code', state: 'working', kind: 'driven',
  created: '2026-11-19T08:00:00Z', updated: '2026-11-19T09:30:00Z',
};
const WAIT = { run: 'q7', node: 'release-approve', why: 'actor', role: 'approver', since: '2026-11-19T09:20:00Z' };
const ROW = {
  kind: 'step', id: 'q7/release-approve', since: '2026-11-19T09:20:00Z', workspace: 'work', where: 'portal',
  step: { run: 'q7', node: 'release-approve', why: 'actor', role: 'approver', holder: 'person', presses: ['skip', 'stop'], words: PROD_APPROVAL_WORDS, url: PROD_APPROVAL_PAGE, work: 'q7' },
};

function Dock() {
  return <WaitStrip session={SESSION} runWaits={useRunWaitDoors(SESSION.id)} />;
}

function draw() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(groupsKey(), { sessions: [{ session: SESSION.id, group: 'needs-you', state: 'working', waits: [WAIT] }], entries: undefined });
  client.setQueryData(keys.attention, composeAttention(new Map<string, unknown>([['rows', { 'step:q7/release-approve': ROW }], ['sessions', {}]]), true));
  return show(<Dock />, client);
}

describe('the dock, at the work workspace’s run waiting on its production stage’s approval', () => {
  beforeEach(() => { invoke.mockReset(); });

  it('draws the approval card beside the step’s presses, and Approve sends the page’s release and the stage', async () => {
    wireRelease(invoke, { workspace: 'work' });
    const shell = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, options?: unknown) =>
      type === 'WORKFLOW_RUN' ? answerOf(GRAPH_RUN_PROD_APPROVAL) : shell(module, type, options));
    draw();

    const line = screen.getByRole('group', { name: 'release-approve: waiting on approver: you' });
    expect(within(line).getByRole('button', { name: 'Not needed this run…' })).toBeInTheDocument();
    await userEvent.click(await within(line).findByRole('button', { name: 'Approve' }));
    expect(sentTo(invoke, 'WORKFLOW_RUN')).toEqual([{ session: 's1' }]);
    await waitFor(() => expect(sentTo(invoke, 'PLUGIN_COMMAND')).toEqual([{
      id: 'daoris-azure-devops', command: 'approve-release', args: { origin: ORIGIN, release: '13515', stage: 'Prod', decision: 'approve', comment: '' },
      answers: {}, place: 'portal', language: 'en',
    }]));
  });
});
