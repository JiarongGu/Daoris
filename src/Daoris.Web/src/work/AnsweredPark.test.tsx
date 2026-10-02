import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import i18n from '../i18n';
import { AnsweredPark } from './AnsweredPark';

// ANSWER1c (D131, answer-continues design §5): a park the person answered stays parked until the driver's next look,
// and the same session goes on then with the answer. Props-only, like every molecule here.

describe('a park the person answered', () => {
  it('shows the answer word for word, and that the same session goes on with it at the driver\'s next look', () => {
    render(<AnsweredPark answer={'The second.\nApply it to dev first.'} />);

    expect(screen.getByRole('heading', { name: 'Your answer' })).toBeInTheDocument();
    expect(screen.getByText(/The second\.\s+Apply it to dev first\./)).toBeInTheDocument();
    expect(screen.getByText("The same session goes on with this answer at the driver's next look.")).toBeInTheDocument();
  });

  /** Nothing waits on the person now: no box to answer in, no finish or decline, and not the waiting card's words. */
  it('asks the person nothing more', () => {
    render(<AnsweredPark answer="carry on." />);

    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.queryByRole('textbox')).toBeNull();
    expect(screen.queryByText('This one is waiting on you')).toBeNull();
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    render(<AnsweredPark answer="用第二种。" />);
    expect(screen.getByRole('heading', { name: '你的回答' })).toBeInTheDocument();
    expect(screen.getByText('同一个会话会在驱动的下一轮带着这个回答继续。')).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });
});
