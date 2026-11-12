import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import type { RunStep } from './run';
import { NodeWord } from './NodeWord';

const FAILED = { step: { id: 'pull-request' }, state: 'failed', acts: ['try-again', 'stop'] } as unknown as RunStep;

describe('a failed plugin step’s Try again', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', 'Try again…', 'Try pull-request again', 'Try again'],
    ['zh', '重试…', '重试 pull-request', '重试'],
  ])('is offered where the step failed and sent as the run’s node word, in %s', async (language, open, title, meanIt) => {
    await i18n.changeLanguage(language);
    const onWord = vi.fn().mockResolvedValue({ run: 'q1', node: 'pull-request', act: 'try-again', done: true });
    render(<NodeWord run="q1" step={FAILED} onWord={onWord} />);

    await userEvent.click(screen.getByRole('button', { name: open }));
    await userEvent.click(within(screen.getByRole('group', { name: title })).getByRole('button', { name: meanIt }));

    expect(onWord).toHaveBeenCalledWith({ run: 'q1', node: 'pull-request', act: 'try-again', words: null });
  });

  it('says a word kept for it until the next look applies it', () => {
    render(<NodeWord run="q1" step={{ ...FAILED, asked: { act: 'try-again', words: null, at: '2026-11-12T09:00:00Z' } } as RunStep} />);

    expect(screen.getByText("Your word to try it again is kept, and Daoris's next look sends its plugin a new attempt.")).toBeTruthy();
  });
});
