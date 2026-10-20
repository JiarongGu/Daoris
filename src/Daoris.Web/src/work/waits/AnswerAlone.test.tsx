import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { AnswerAlone, type AnswerWait, answerAloneSaid, answerFor } from './AnswerAlone';

// COWORK1e (D188 point 4; the message-urgency design §8.3): a third answer in a row with nobody between waits for the person, with
// *Go on with this answer* and *Not now*. Props only: the frame holds the answer and its pending.

const WAIT: AnswerWait = { session: 's1a2b3c4', agent: 'codex-acp', words: ['w3'], since: '2026-10-21T09:00:00Z' };

describe('a third answer alone, held for the person', () => {
  it('says whose answer it is and what it would do, and answers with the press made', async () => {
    const onAnswer = vi.fn();
    render(<AnswerAlone wait={WAIT} nameOf={(agent) => (agent === 'codex-acp' ? 'Codex' : agent)} onAnswer={onAnswer} />);

    expect(screen.getByText('Codex’s answer would take this work on a third time with nobody between.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Go on with this answer' }));
    expect(onAnswer).toHaveBeenLastCalledWith(WAIT, true, { done: expect.any(Function), refused: expect.any(Function) });
    onAnswer.mock.calls.at(-1)![2].done();
    await userEvent.click(screen.getByRole('button', { name: 'Not now' }));
    expect(onAnswer).toHaveBeenLastCalledWith(WAIT, false, { done: expect.any(Function), refused: expect.any(Function) });
  });

  it('says a refused answer beside it, and offers no press where nothing can answer', async () => {
    const onAnswer = vi.fn((_: AnswerWait, __: boolean, answered: { refused: (said: string) => void }) => answered.refused('It no longer waits.'));
    const { unmount } = render(<AnswerAlone wait={WAIT} onAnswer={onAnswer} />);
    await userEvent.click(screen.getByRole('button', { name: 'Go on with this answer' }));
    expect(await screen.findByText('It no longer waits.')).toBeInTheDocument();
    unmount();

    render(<AnswerAlone wait={{ ...WAIT, agent: null }} />);
    expect(screen.getByText('Another agent’s answer would take this work on a third time with nobody between.')).toBeInTheDocument();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('finds the session’s own wait, and words a refusal by its code', () => {
    expect(answerFor([{ ...WAIT, session: 'o7h3r000' }, WAIT], 's1a2b3c4')).toBe(WAIT);
    expect(answerFor([WAIT], 'o7h3r000')).toBeNull();
    const t = i18n.t.bind(i18n);
    expect(answerAloneSaid(t, 'unheld', 'the driver')).toBe('It no longer waits: it was answered, or its words went on. Nothing was answered.');
    expect(answerAloneSaid(t, 'later', 'The driver’s own sentence.')).toBe('The driver’s own sentence.');
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    render(<AnswerAlone wait={WAIT} onAnswer={vi.fn()} />);
    expect(screen.getByText('codex-acp 的回答会让这项工作在无人参与的情况下第三次继续。')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '带着这个回答继续' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '暂不' })).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });
});
