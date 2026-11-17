import { afterEach, describe, expect, it } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import type { WorkflowRun } from './run';
import { answerOf, IN_REVIEW } from './runFixtures';
import { WorkflowRunView } from './WorkflowRunView';

// A run's step says the role that takes it beside its state.

const row = (id: string) => screen.getAllByRole('listitem').find((item) => item.getAttribute('data-step') === id)!;

describe('the role a step is taken by', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("is said beside its state, Daoris's own by its word and a catalogue role as written", async () => {
    const roled: WorkflowRun = {
      ...IN_REVIEW,
      steps: IN_REVIEW.steps.map((step) => step.step.id === 'work' ? { ...step, role: 'builder' } : step.step.id === 'look' ? { ...step, role: 'approver' } : step),
    };
    const draw = () => rtlRender(<Tooltip.Provider><WorkflowRunView answer={answerOf(roled)} here="work" /></Tooltip.Provider>);
    const { unmount } = draw();
    expect(row('work')).toHaveTextContent('role builder');
    expect(row('look')).toHaveTextContent('role Approver');
    expect(row('landing')).not.toHaveTextContent('role');
    unmount();
    await i18n.changeLanguage('zh');
    draw();
    expect(row('look')).toHaveTextContent('角色 审批人');
  });
});
