import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../locales/i18n';
import type { WorkflowStep } from './current';
import type { RunStep, WorkflowRun } from './run';
import { answerOf, IN_REVIEW } from './runFixtures';
import { WorkflowRunView } from './WorkflowRunView';

// While the holder acts, the run's view draws one row per act journaled on the step, under it: the fact, its state,
// its credit and page, and "waiting on `<role>`: <holder>" where a receipt waits on another role.

const RELEASE: WorkflowStep = {
  id: 'release', kind: 'task', participation: 'you', executor: 'daoris', press: null,
  source: { rule: 'workflow', level: 'workspace' }, settings: { role: 'release-manager' }, runtime: 'built', limit: null,
};

const runWith = (fields: Partial<RunStep>): WorkflowRun => ({
  ...IN_REVIEW,
  at: 'release',
  graph: { run: 'q7', entries: ['work'], end: 'release', wires: [{ kind: 'after', from: 'work', to: 'release' }], judged: true, unread: false },
  steps: [...IN_REVIEW.steps.slice(0, 1), { step: RELEASE, state: 'working', detail: 'actor', role: 'release-manager', node: 'task', ...fields }],
});

const draw = (run: WorkflowRun) =>
  render(<Tooltip.Provider><WorkflowRunView answer={answerOf(run)} onNodeWord={vi.fn()} /></Tooltip.Provider>);

describe('the acts journaled on a step', () => {
  it('draws one row per act under its step, a wait on another role naming that role and its holder', () => {
    draw(runWith({
      journal: [
        { fact: 'release-created', state: 'reached', message: 'Release-91 created by Daoris.', url: 'https://dev.example/r/91', by: 'Daoris', at: '2026-11-19T09:00:00Z' },
        { fact: 'stage-started', state: 'failed', message: 'The release identity’s sign-in expired.', at: '2026-11-19T09:01:00Z' },
        { fact: 'stage-ended', state: 'waiting', message: 'Stage 403 waits on its approver.', waitsOn: 'approver', holder: 'person', at: '2026-11-19T09:05:00Z' },
      ],
    }));
    const step = screen.getAllByRole('listitem').find((item) => item.getAttribute('data-step') === 'release')!;
    const acts = within(within(step).getByRole('list', { name: 'Acts' })).getAllByRole('listitem');

    expect(acts.map((act) => act.getAttribute('data-act'))).toEqual(['release-created', 'stage-started', 'stage-ended']);
    expect(acts[0]).toHaveTextContent('release-created: reached');
    expect(acts[0]).toHaveTextContent('Release-91 created by Daoris.');
    expect(acts[0]).toHaveTextContent('by Daoris');
    expect(within(acts[0]).getByRole('link', { name: 'Open its page' })).toHaveAttribute('href', 'https://dev.example/r/91');
    expect(acts[1]).toHaveTextContent('stage-started: failed');
    expect(acts[2]).toHaveTextContent('waiting on Approver: you');
  });

  it('draws no list where no act is journaled', () => {
    draw(runWith({ journal: null }));
    expect(screen.queryByRole('list', { name: 'Acts' })).not.toBeInTheDocument();
  });
});
