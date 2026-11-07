import { afterEach, describe, expect, it } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import { answerOf, GRAPH_RUN_DECIDED } from './runFixtures';
import { WorkflowRunView } from './WorkflowRunView';

// NODEKIT1: the run view says a step's outcome and its routes in the words of the kind that declares them.

const row = (id: string) => screen.getAllByRole('listitem').find((item) => item.getAttribute('data-step') === id)!;

describe("the run view's outcome words", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const monitored = () => {
    const decided = GRAPH_RUN_DECIDED;
    return answerOf({
      ...decided,
      steps: decided.steps.map((each) => (each.step.id === 'find' ? { ...each, step: { ...each.step, kind: 'monitor' } } : each)),
      graph: {
        ...decided.graph!,
        wires: decided.graph!.wires.map((wire) => (wire.from === 'find' ? { ...wire, outcome: 'merged' } : wire)),
        outcomes: { find: 'closed' },
      },
    });
  };

  it("says a monitor's outcome and its routes in the kind's words, and a kind with none as it spelled it", () => {
    const { unmount } = rtlRender(<Tooltip.Provider><WorkflowRunView answer={monitored()} /></Tooltip.Provider>);
    expect(row('find')).toHaveTextContent('Its outcome: closed without a merge.');
    const [merged, closed] = within(within(row('find')).getByRole('list', { name: 'Routes' })).getAllByRole('listitem');
    expect(merged).toHaveTextContent(/merged → release/);
    expect(closed).toHaveTextContent('closed without a merge → No further step');
    unmount();

    rtlRender(<Tooltip.Provider><WorkflowRunView answer={answerOf(GRAPH_RUN_DECIDED)} /></Tooltip.Provider>);
    expect(row('find')).toHaveTextContent('Its outcome: none.');
  });

  it('says them in 中文', async () => {
    await i18n.changeLanguage('zh');
    rtlRender(<Tooltip.Provider><WorkflowRunView answer={monitored()} /></Tooltip.Provider>);
    expect(row('find')).toHaveTextContent('它的结果：未合并即关闭。');
    expect(within(within(row('find')).getByRole('list')).getAllByRole('listitem')[0]).toHaveTextContent(/已合并 → /);
  });
});
