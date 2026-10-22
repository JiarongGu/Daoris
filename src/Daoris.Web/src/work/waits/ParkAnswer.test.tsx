import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { GoAhead } from '../../data/api';
import i18n from '../../locales/i18n';
import { ParkAnswer } from './ParkAnswer';
import type { ParkQuestion } from './parkQuestion';

// PARKVIEW1 (the park's ask design §3-§4): what a parked session asks, in its box above the words: its question with each answer a
// press beside what the agent does on it, the go-aheads it still waits on with *Approve* and *Refuse*, by when, what it holds
// meanwhile and what an answer does. A molecule: props in, presses out.

const QUESTION: ParkQuestion = {
  id: 'a1b2c3d4',
  question: 'Which quarterly target should the dashboard tile show?',
  choices: [
    { label: '1,200', does: 'Write 1,200, the planning sheet\'s value, which the ticket links.' },
    { label: '1,350', does: 'Write 1,350, the last stand-up note\'s value.' },
  ],
  recommend: 1,
  by: 'before Friday\'s release',
  at: '2026-10-22T09:05:00Z',
};

const GO_AHEAD: GoAhead = {
  number: 1, kind: 'write', on: 'production', act: 'dashboard configuration', state: 'asked',
  asked: [{ session: 's1a2b3c4', quest: '7a82cc', at: '2026-10-22T09:04:00Z', why: 'The tile reads its target from it.' }],
};

const SESSION = { quest: '7a82cc', tree: 's-1a2b3c4', since: '2026-10-22T09:06:00Z' };

afterEach(() => { void i18n.changeLanguage('en'); });

describe('a park\'s ask in its box', () => {
  it('says its question, and answers with the press of each answer it offered, the recommended one marked', async () => {
    const onChoice = vi.fn();
    render(<ParkAnswer {...SESSION} question={QUESTION} goAheads={[]} onChoice={onChoice} onGoAhead={vi.fn()} />);

    const ask = screen.getByRole('region', { name: 'It asks you' });
    expect(within(ask).getByText('Which quarterly target should the dashboard tile show?')).toBeInTheDocument();
    expect(within(ask).getByText('Write 1,200, the planning sheet\'s value, which the ticket links.')).toBeInTheDocument();
    expect(within(ask).getByText('recommended')).toBeInTheDocument();
    await userEvent.click(within(ask).getByRole('button', { name: '1,350' }));
    expect(onChoice).toHaveBeenLastCalledWith('1,350');
    await userEvent.click(within(ask).getByRole('button', { name: '1,200' }));
    expect(onChoice).toHaveBeenLastCalledWith('1,200');
  });

  it('says what it holds meanwhile, that no deadline runs, and by when the agent says it needs the answer', () => {
    vi.useFakeTimers({ now: new Date('2026-10-22T09:18:00Z'), shouldAdvanceTime: true });
    try {
      render(<ParkAnswer {...SESSION} question={QUESTION} goAheads={[]} onChoice={vi.fn()} />);

      expect(screen.getByText('No deadline here: it has waited 12m, and waits until you answer.')).toBeInTheDocument();
      expect(screen.getByText('It says it needs your answer by: before Friday\'s release')).toBeInTheDocument();
      expect(screen.getByText(
        'Meanwhile its quest #7a82cc stays taken, its tree s-1a2b3c4 is kept, and it runs nothing until you answer.',
      )).toBeInTheDocument();
      expect(screen.getByText(
        'Your answer goes to the same session as your words, and it goes on with it at the driver\'s next look.',
      )).toBeInTheDocument();
    } finally {
      vi.useRealTimers();
    }
  });

  it('answers each go-ahead still open with its own press, and holds its question until they are answered, saying so', async () => {
    const onGoAhead = vi.fn();
    const onChoice = vi.fn();
    render(<ParkAnswer {...SESSION} question={QUESTION} goAheads={[GO_AHEAD]} onChoice={onChoice} onGoAhead={onGoAhead} />);

    const line = screen.getByText('write on production').closest('li')!;
    expect(within(line).getByText('“dashboard configuration”')).toBeInTheDocument();
    expect(within(line).getByText('The tile reads its target from it.')).toBeInTheDocument();
    await userEvent.click(within(line).getByRole('button', { name: 'Approve' }));
    expect(onGoAhead).toHaveBeenLastCalledWith(1, true);
    await userEvent.click(within(line).getByRole('button', { name: 'Refuse' }));
    expect(onGoAhead).toHaveBeenLastCalledWith(1, false);

    expect(screen.getByText('Answer its go-aheads first: each keeps it waiting for its question, whose answer then sends it on with them.'))
      .toBeInTheDocument();
    expect(screen.getByRole('button', { name: '1,200' })).toBeDisabled();
    expect(onChoice).not.toHaveBeenCalled();
  });

  it('with no question recorded, says it asked in its last message, and answers its go-aheads as before', async () => {
    const onGoAhead = vi.fn();
    render(<ParkAnswer {...SESSION} tree={null} question={null} goAheads={[GO_AHEAD]} onGoAhead={onGoAhead} />);

    expect(screen.getByText('It asked in its last message, above: answer it below.')).toBeInTheDocument();
    expect(screen.getByText('Meanwhile its quest #7a82cc stays taken, and it runs nothing until you answer.')).toBeInTheDocument();
    expect(screen.queryByText(/Answer its go-aheads first/)).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Approve' }));
    expect(onGoAhead).toHaveBeenLastCalledWith(1, true);
  });

  it('draws no press where nothing answers, and none while a press is on its way', () => {
    const { unmount } = render(<ParkAnswer {...SESSION} question={QUESTION} goAheads={[GO_AHEAD]} />);
    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.getByText('1,200')).toBeInTheDocument();
    unmount();

    render(<ParkAnswer {...SESSION} question={QUESTION} goAheads={[]} busy onChoice={vi.fn()} />);
    expect(screen.getByRole('button', { name: '1,200' })).toBeDisabled();
  });

  it('says the chrome in 中文 and the agent\'s words as written', async () => {
    await i18n.changeLanguage('zh');
    render(<ParkAnswer {...SESSION} question={QUESTION} goAheads={[GO_AHEAD]} onChoice={vi.fn()} onGoAhead={vi.fn()} />);

    expect(screen.getByRole('region', { name: '它在问你' })).toBeInTheDocument();
    expect(screen.getByText('Which quarterly target should the dashboard tile show?')).toBeInTheDocument();
    expect(screen.getByText('推荐')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '放行' })).toBeInTheDocument();
    expect(screen.getByText(/^在此期间，它的委托 #7a82cc 仍由它接下/)).toBeInTheDocument();
  });
});
