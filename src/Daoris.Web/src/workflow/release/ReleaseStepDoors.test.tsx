import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';

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

import userEvent from '@testing-library/user-event';
import '../../locales/i18n';
import { ORIGIN, sentTo, wireRelease } from '../../test/doubles/releaseShell';
import { show } from '../../test/shellHarness';
import { answerOf, GRAPH_RUN_APPROVAL, GRAPH_RUN_PROD_APPROVAL } from '../runFixtures';
import { WorkflowRunView } from '../WorkflowRunView';
import { ReleaseStepDoors } from './ReleaseStepDoors';

// The run's view at a release step waiting on its approver, drawn from the run as the driver answers it: the wait's values carry the
// actor, its words and the release's page, and the run's repository's origin resolves, so the card offers the approver's press. It
// stood there alone: the session page drew the step from *What needs you*'s row, which names no step kind, until the dock read the run.

const card = () => screen.getByRole('group', { name: 'Approval' });

describe('the run’s view at a release’s approval', () => {
  beforeEach(() => { invoke.mockReset(); });

  const draw = () => show(<WorkflowRunView answer={answerOf(GRAPH_RUN_APPROVAL)} releaseDoors={(run, step) => <ReleaseStepDoors run={run} step={step} />} />);

  it('draws the approval card at the step with its Approve and Reject', async () => {
    wireRelease(invoke);
    draw();
    expect(await within(card()).findByRole('button', { name: 'Approve' })).toBeInTheDocument();
    expect(within(card()).getByRole('button', { name: 'Reject' })).toBeInTheDocument();
    expect(sentTo(invoke, 'GIT_BRANCHES')).toEqual([{ remotes: true, repository: 'portal' }]);
  });

  it('opens the stage’s page where the run’s repository has no origin to approve through', async () => {
    wireRelease(invoke, { origin: null });
    draw();
    await waitFor(() => expect([sentTo(invoke, 'PLUGINS').length, sentTo(invoke, 'GIT_BRANCHES').length]).toEqual([1, 1]));
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
    expect(within(card()).getByRole('button', { name: 'Open' })).toBeInTheDocument();
    expect(within(card()).queryByRole('button', { name: 'Approve' })).toBeNull();
  });

  it('offers the person no press where an agent holds the approver, the approval its tool: the card opens the stage’s page', async () => {
    wireRelease(invoke, { tool: true });
    const held = {
      ...GRAPH_RUN_APPROVAL,
      steps: GRAPH_RUN_APPROVAL.steps.map((step) => (step.step.id === 'release-approve' ? { ...step, holder: { kind: 'agent' as const, name: 'builder-1' } } : step)),
    };
    show(<WorkflowRunView answer={answerOf(held)} releaseDoors={(run, step) => <ReleaseStepDoors run={run} step={step} />} />);
    await waitFor(() => expect([sentTo(invoke, 'PLUGINS').length, sentTo(invoke, 'GIT_BRANCHES').length]).toEqual([1, 1]));
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
    expect(within(card()).getByRole('button', { name: 'Open' })).toBeInTheDocument();
    expect(within(card()).queryByRole('button', { name: 'Approve' })).toBeNull();
  });

  it('offers the person holding the approver the press though the plugin hands the approval to an agent holding it as its tool', async () => {
    wireRelease(invoke, { tool: true });
    draw();
    expect(await within(card()).findByRole('button', { name: 'Approve' })).toBeInTheDocument();
  });

  it('approves a production stage of the work workspace’s run by the page’s id, through its own checkout’s origin', async () => {
    wireRelease(invoke, { workspace: 'work' });
    show(<WorkflowRunView answer={answerOf(GRAPH_RUN_PROD_APPROVAL)} releaseDoors={(run, step) => <ReleaseStepDoors run={run} step={step} />} />);

    await userEvent.click(await within(card()).findByRole('button', { name: 'Approve' }));
    await waitFor(() => expect(sentTo(invoke, 'PLUGIN_COMMAND')).toEqual([{
      id: 'daoris-azure-devops', command: 'approve-release', args: { origin: ORIGIN, release: '13515', stage: 'Prod', decision: 'approve', comment: '' },
      answers: {}, place: 'portal', language: 'en',
    }]));
  });
});
