import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { GoAhead } from '../../data/api';
import i18n from '../../locales/i18n';
import { ParkAnswer } from './ParkAnswer';
import type { ParkQuestion } from './parkQuestion';

// PARKVIEW1 (the park's ask design §3-§4): what a parked session asks, in its box above the words: its question with each answer a
// press beside what the agent does on it, the go-aheads it still waits on with *Approve* and *Refuse*, one line saying where its
// question is and how long it has waited, and folded under it what it holds meanwhile and what an answer does (PARKWORDS1). A
// molecule: props in, presses out.

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

  // PARKWORDS1: one line says where its question is and how long it has waited; what it holds and how the answer travels fold.
  it('says in one line where its question is and how long it has waited, and folds what it holds meanwhile', async () => {
    vi.useFakeTimers({ now: new Date('2026-10-22T14:12:00Z'), shouldAdvanceTime: true });
    try {
      render(<ParkAnswer {...SESSION} question={QUESTION} goAheads={[]} onChoice={vi.fn()} />);

      expect(screen.getByText('It asks this, waiting 5h 6m: answer below.')).toBeVisible();
      expect(screen.getByText('It says it needs your answer by: before Friday\'s release')).toBeVisible();
      const held = [
        'No deadline here: it waits until you answer.',
        'Meanwhile its quest #7a82cc stays taken, its tree s-1a2b3c4 is kept, and it runs nothing until you answer.',
        'Your answer goes to the same session as your words, and it goes on with it at the driver\'s next look.',
      ];
      for (const line of held) expect(screen.getByText(line)).not.toBeVisible();

      await userEvent.click(screen.getByText('While it waits'));
      for (const line of held) expect(screen.getByText(line)).toBeVisible();
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

  it('with no question recorded, says in its one line that it asked above, and answers its go-aheads as before', async () => {
    vi.useFakeTimers({ now: new Date('2026-10-22T09:18:00Z'), shouldAdvanceTime: true });
    const onGoAhead = vi.fn();
    try {
      render(<ParkAnswer {...SESSION} tree={null} question={null} goAheads={[GO_AHEAD]} onGoAhead={onGoAhead} />);
    } finally {
      vi.useRealTimers();
    }

    expect(screen.getByText('It asked above, waiting 12m: answer below.')).toBeVisible();
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
    expect(screen.getByText(/^在此期间，它的委托 #7a82cc 仍由它接下/)).not.toBeVisible();
    await userEvent.click(screen.getByText('等待期间'));
    expect(screen.getByText(/^在此期间，它的委托 #7a82cc 仍由它接下/)).toBeVisible();
  });

  it('says its one line in 中文, where its question is and how long it has waited', async () => {
    await i18n.changeLanguage('zh');
    vi.useFakeTimers({ now: new Date('2026-10-22T14:12:00Z'), shouldAdvanceTime: true });
    try {
      const { unmount } = render(<ParkAnswer {...SESSION} question={null} goAheads={[]} />);
      expect(screen.getByText('它在上面提了问，已等 5 小时 6 分：在下面回答。')).toBeVisible();
      unmount();
      render(<ParkAnswer {...SESSION} question={QUESTION} goAheads={[]} onChoice={vi.fn()} />);
      expect(screen.getByText('它在问这个，已等 5 小时 6 分：在下面回答。')).toBeVisible();
      expect(screen.getByText('这里没有期限：它会一直等到你回答。')).not.toBeVisible();
    } finally {
      vi.useRealTimers();
    }
  });
});
