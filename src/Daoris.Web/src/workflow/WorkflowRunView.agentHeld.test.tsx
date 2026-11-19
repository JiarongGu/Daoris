import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../locales/i18n';
import type { WorkflowStep } from './current';
import type { RunStep, WorkflowRun } from './run';
import { answerOf, IN_REVIEW } from './runFixtures';
import { WorkflowRunView } from './WorkflowRunView';

// A `task` step cast to the role's agent reads as the agent's, with *Take* its one press and none of the person's task presses;
// the step a person holds keeps its presses.

const TASK: WorkflowStep = {
  id: 'config-change', kind: 'task', participation: 'you', executor: 'daoris', press: null,
  source: { rule: 'workflow', level: 'workspace' }, settings: { role: 'config-changer' }, runtime: 'built', limit: null,
};

const runWith = (fields: Partial<RunStep>): WorkflowRun => ({
  ...IN_REVIEW,
  at: 'config-change',
  graph: { run: 'q7', entries: ['work'], end: 'config-change', wires: [{ kind: 'after', from: 'work', to: 'config-change' }], judged: true, unread: false },
  steps: [...IN_REVIEW.steps.slice(0, 1), { step: TASK, state: 'waiting', detail: 'actor', role: 'config-changer', node: 'task', ...fields }],
});

const draw = (run: WorkflowRun) => {
  const stepHold = {
    useCast: () => ({ data: { roster: [] } }),
    useHold: () => ({ mutateAsync: vi.fn().mockResolvedValue({ done: true, refused: null, message: null }), isPending: false }),
  };
  return render(<Tooltip.Provider><WorkflowRunView answer={answerOf(run)} onNodeWord={vi.fn()} stepHold={stepHold} /></Tooltip.Provider>);
};

const rowOf = () => screen.getAllByRole('listitem').find((item) => item.getAttribute('data-step') === 'config-change')!;

describe('a task step by who holds it', () => {
  it('reads an agent-held step as the agent’s with Take alone, none of the person’s presses', () => {
    draw(runWith({ holder: { kind: 'agent', name: 'builder-1' }, acts: ['done', 'skip', 'stop'], holds: ['take', 'hand'], epoch: 1 }));
    const row = rowOf();
    expect(row).toHaveTextContent('config-changer');
    expect(row).toHaveTextContent('builder-1');
    // The step's part reads the agent's, never the *you* a `task` declares.
    expect(screen.queryAllByText('you')).toHaveLength(0);
    expect(screen.getAllByText('agent alone').length).toBeGreaterThan(1);
    expect(within(row).getByRole('button', { name: /take/i })).toBeInTheDocument();
    for (const name of ['Done…', 'Not needed this run…', 'End this work…']) expect(within(row).queryByRole('button', { name })).toBeNull();
  });

  it('keeps the presses of a step the person holds', () => {
    draw(runWith({ holder: { kind: 'person' }, acts: ['done', 'skip', 'stop'], holds: ['hand'], epoch: 1 }));
    const row = rowOf();
    expect(screen.getAllByText('you').length).toBeGreaterThan(0);
    expect(within(row).getByRole('button', { name: 'Not needed this run…' })).toBeInTheDocument();
    expect(within(row).getByRole('button', { name: 'End this work…' })).toBeInTheDocument();
  });
});
