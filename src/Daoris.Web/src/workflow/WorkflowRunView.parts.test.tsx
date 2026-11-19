import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../locales/i18n';
import type { WorkflowRun } from './run';
import { answerOf } from './runFixtures';
import { JOINT_HOLDING, JOINT_RETURNED, WAITING_PART } from './parts/partFixtures';
import { WorkflowRunView } from './WorkflowRunView';

// A quest across repositories: the run's view says what its part waits on before it starts, with the other part's quest, and on a
// joint check which parts' receipts it holds, which it waits for, and the part a failed test went back to.

const draw = (run: WorkflowRun, quest = vi.fn()) =>
  render(<Tooltip.Provider><WorkflowRunView answer={answerOf(run)} doors={{ quest }} onNodeWord={vi.fn()} /></Tooltip.Provider>);

describe('a quest\'s parts in the run\'s view', () => {
  it('says what its part waits on, the other part\'s receipt with its quest', () => {
    const quest = vi.fn();
    draw(WAITING_PART, quest);
    const waits = within(screen.getByRole('list', { name: 'Waits before it starts' })).getAllByRole('listitem');

    expect(waits.map((wait) => wait.getAttribute('data-part'))).toEqual(['app']);
    expect(waits[0]).toHaveTextContent(/waits on app’s stage-ended/);
    expect(waits[0]).toHaveTextContent('waiting');
    fireEvent.click(within(waits[0]).getByRole('button', { name: 'Open quest #q7' }));
    expect(quest).toHaveBeenCalledWith('q7');
  });

  it('draws a joint check holding one part\'s receipt and waiting for the other\'s', () => {
    draw(JOINT_HOLDING);
    const step = screen.getAllByRole('listitem').find((item) => item.getAttribute('data-step') === 'together')!;
    const parts = within(within(step).getByRole('list', { name: 'Tested together' })).getAllByRole('listitem');

    expect(parts.map((part) => part.getAttribute('data-part'))).toEqual(['app', 'config']);
    expect(parts[0]).toHaveTextContent(/holds app’s stage-ended/);
    expect(parts[1]).toHaveTextContent(/waits for config’s config-written/);
    expect(within(step).queryByText(/sent back/)).toBeNull();
    expect(screen.queryByRole('list', { name: 'Waits before it starts' })).toBeNull();
  });

  it('names the part a failed joint check went back to, with the test\'s words and that part\'s quest', () => {
    const quest = vi.fn();
    draw(JOINT_RETURNED, quest);
    const step = screen.getAllByRole('listitem').find((item) => item.getAttribute('data-step') === 'together')!;
    const list = within(step).getByRole('list', { name: 'Tested together' });
    const returned = within(list).getAllByRole('listitem').find((item) => item.hasAttribute('data-returned'))!;

    expect(within(list).getAllByRole('listitem').filter((item) => item.textContent?.startsWith('holds'))).toHaveLength(2);
    expect(returned).toHaveTextContent(/failed on config, sent back to it/);
    expect(returned).toHaveTextContent('The app could not read its connection setting.');
    fireEvent.click(within(returned).getByRole('button', { name: 'Open quest #q9' }));
    expect(quest).toHaveBeenCalledWith('q9');
  });
});
