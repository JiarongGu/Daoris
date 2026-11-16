import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { NodeWord } from '../NodeWord';
import type { RunStep } from '../run';

// Under a run's decision, each of its outcomes is a press, the decide of that outcome with the reason the person may give; an answer
// already kept is said in place of the presses.

const step = (acts: string[], asked?: RunStep['asked']) => ({ step: { id: 'config-needed' }, acts, ...(asked ? { asked } : {}) }) as unknown as RunStep;

afterEach(async () => { await i18n.changeLanguage('en'); });

describe("a decision on the run's view", () => {
  it.each([
    ['en', 'Answer not-needed…', 'Answer'],
    ['zh', '回答 not-needed…', '回答'],
  ])('in %s, offers each outcome and presses decide with it and the reason given', async (language, open, meanIt) => {
    await i18n.changeLanguage(language);
    const onWord = vi.fn().mockResolvedValue({ run: 'q7', node: 'config-needed', act: 'decide', done: true });
    render(<NodeWord run="q7" step={step(['decide:needed', 'decide:not-needed', 'skip', 'stop'])} onWord={onWord} />);

    expect(screen.getByRole('button', { name: open.replace('not-needed', 'needed') })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: open }));
    const asking = screen.getByRole('group', { name: open });
    await userEvent.type(within(asking).getByRole('textbox'), 'no setting changes');
    await userEvent.click(within(asking).getByRole('button', { name: meanIt }));
    expect(onWord).toHaveBeenCalledWith({ run: 'q7', node: 'config-needed', act: 'decide', outcome: 'not-needed', words: 'no setting changes' });
  });

  it('says an answer already kept in place of the presses', () => {
    render(<NodeWord run="q7" step={step(['decide:needed'], { node: 'config-needed', act: 'decide', words: null } as RunStep['asked'])} onWord={vi.fn()} />);
    expect(screen.getByText('Your answer is kept, and Daoris\'s next look applies it.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Answer needed…' })).toBeNull();
  });
});
