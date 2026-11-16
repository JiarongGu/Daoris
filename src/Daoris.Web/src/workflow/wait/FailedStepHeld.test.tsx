import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { RunStep } from '../run';
import { FailedStepHeld } from './FailedStepHeld';
import { NodeWord } from '../NodeWord';

const FAILED = {
  step: { id: 'pull-request' }, node: 'acme.deploy/pull-request', state: 'failed', acts: ['stop'],
  held: { unit: 'run-q1', attempt: 2, words: ['again'] },
} as unknown as RunStep;

describe('a failed plugin step’s Try again', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', 'Try again'],
    ['zh', '重试'],
  ])('presses the held attempt’s again, in %s', async (language, label) => {
    await i18n.changeLanguage(language);
    const onHeld = vi.fn().mockResolvedValue({ done: true });
    render(<FailedStepHeld step={FAILED} onHeld={onHeld} />);

    await userEvent.click(screen.getByRole('button', { name: label }));

    expect(onHeld).toHaveBeenCalledWith({ unit: 'run-q1', node: 'pull-request', attempt: 2, resolution: 'again' });
  });

  it('says the word given until the next look applies it, and offers it no more', () => {
    render(<FailedStepHeld step={{ ...FAILED, held: { unit: 'run-q1', attempt: 2, words: [], resolution: 'again' } } as RunStep} onHeld={vi.fn()} />);

    expect(screen.getByText("Your word to try it again is kept, and Daoris's next look sends its plugin a new attempt.")).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull();
  });

  it('says the driver’s refusal where it was pressed', async () => {
    const onHeld = vi.fn().mockResolvedValue({ done: false, refused: 'worded', message: 'It already has your word.' });
    render(<FailedStepHeld step={FAILED} onHeld={onHeld} />);

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect((await screen.findByRole('alert')).textContent).toBe('It already has your word.');
  });

  it.each([
    ['en', 'Done outside Daoris…', 'link to what was done', 'Done outside Daoris'],
    ['zh', '已在 Daoris 外完成…', '指向已完成内容的链接', '已在 Daoris 外完成'],
  ])('settles a failed step as done outside Daoris with its link, in %s', async (language, press, link, meanIt) => {
    await i18n.changeLanguage(language);
    const onHeld = vi.fn().mockResolvedValue({ done: true });
    const step = { ...FAILED, held: { unit: 'run-q1', attempt: 2, words: ['again', 'done-outside'] } } as RunStep;
    render(<FailedStepHeld step={step} onHeld={onHeld} />);

    await userEvent.click(screen.getByRole('button', { name: press }));
    await userEvent.type(screen.getByRole('textbox', { name: link }), 'https://deploy.example/releases/42');
    await userEvent.click(screen.getByRole('button', { name: meanIt }));

    expect(onHeld).toHaveBeenCalledWith({ unit: 'run-q1', node: 'pull-request', attempt: 2, resolution: 'done-outside', link: 'https://deploy.example/releases/42' });
  });

  it('is not one of the node’s own words', () => {
    render(<NodeWord run="q1" step={FAILED} onWord={vi.fn()} />);

    expect(screen.queryByRole('button', { name: /Try again/ })).toBeNull();
  });
});
