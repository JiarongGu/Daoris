import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { NodeWord } from './NodeWord';
import type { RunStep } from './run';

// NODEKIT1 HUMAN: under a run's step waiting on the person's task, its presses lead the skip and stop, each the run's node word with a note
// the person may give; a press already kept is said in place of them.

const step = (acts: string[], asked?: RunStep['asked']) => ({ step: { id: 'sign' }, acts, ...(asked ? { asked } : {}) }) as unknown as RunStep;

afterEach(async () => { await i18n.changeLanguage('en'); });

describe("a person's task on the run's view", () => {
  it.each([
    ['en', ['done', 'skip', 'stop'], 'Done…', 'Done', 'done'],
    ['en', ['approve', 'reject', 'skip', 'stop'], 'Reject…', 'Reject', 'reject'],
    ['zh', ['approve', 'reject', 'skip', 'stop'], '批准…', '批准', 'approve'],
  ])('in %s, offers %j and presses its act with the note given', async (language, acts, open, meanIt, act) => {
    await i18n.changeLanguage(language);
    const onWord = vi.fn().mockResolvedValue({ run: 'q7', node: 'sign', act, done: true });
    render(<NodeWord run="q7" step={step(acts)} onWord={onWord} />);

    await userEvent.click(screen.getByRole('button', { name: open }));
    const asking = screen.getByRole('group', { name: open });
    await userEvent.type(within(asking).getByRole('textbox'), 'checked by hand');
    await userEvent.click(within(asking).getByRole('button', { name: meanIt }));
    expect(onWord).toHaveBeenCalledWith({ run: 'q7', node: 'sign', act, words: 'checked by hand' });
  });

  it('says a press already kept in place of the presses', () => {
    render(<NodeWord run="q7" step={step(['approve', 'reject'], { node: 'sign', act: 'approve', words: null } as RunStep['asked'])} onWord={vi.fn()} />);
    expect(screen.getByText('Your Approve is kept, and Daoris\'s next look applies it.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve…' })).toBeNull();
  });
});
