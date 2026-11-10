import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { WorkflowRun } from './run';
import { answerOf, GRAPH_RUN } from './runFixtures';
import { WorkflowRunView } from './WorkflowRunView';

// A stopped step is drawn as stopped: its state's word, and never the filled mark of the live step a run stands at, though the
// driver names it as where the run stands.

const stoppedAt = (run: WorkflowRun): WorkflowRun => ({
  ...run,
  steps: run.steps.map((each) => (each.step.id === run.at ? { ...each, state: 'stopped' as const, detail: 'stopped' } : each)),
});

const draw = (run: WorkflowRun) => render(<Tooltip.Provider><WorkflowRunView answer={answerOf(run)} /></Tooltip.Provider>);

describe('a stopped step in the run view', () => {
  it('is drawn stopped, with no live mark on it', () => {
    draw(stoppedAt(GRAPH_RUN));

    expect(document.querySelectorAll('[aria-current="step"]')).toHaveLength(0);
    expect(screen.getAllByText('stopped').length).toBeGreaterThan(0);
  });

  it('leaves the live mark on a step at work', () => {
    draw(GRAPH_RUN);

    expect(document.querySelectorAll('[aria-current="step"]').length).toBeGreaterThan(0);
  });
});
