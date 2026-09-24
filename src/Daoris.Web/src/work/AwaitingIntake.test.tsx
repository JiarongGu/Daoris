import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import { AwaitingIntake } from './AwaitingIntake';

// A parked INTAKE (INT4g): the declarations did not settle whose an ask is, so the intake asked the
// person rather than guess. Its answer is on the ask, not on the session — which is why the three
// moves a parked session offers are not offered here.

const QUESTION = 'published nothing: two repositories could own the loading screen, so it asks you rather than guess.';

const show = (props: Partial<Parameters<typeof AwaitingIntake>[0]> = {}) =>
  render(<AwaitingIntake ask="0fda18" note={QUESTION} onAnswer={() => {}} onStop={() => {}} {...props} />);

describe('a parked intake', () => {
  it('shows the intake\'s own words, verbatim', () => {
    show();
    expect(screen.getByText(QUESTION)).toBeInTheDocument();
  });

  /** The answer is on the ask: publishing it, or closing it, ends this session at the next tick. */
  it('leads to its ask as the answer, naming it', async () => {
    const onAnswer = vi.fn();
    show({ onAnswer });

    await userEvent.click(screen.getByRole('button', { name: 'answer ask #0fda18' }));
    expect(onAnswer).toHaveBeenCalledWith('0fda18');
  });

  /**
   * 🔴 Each of the three ends the record without answering the ask (found by INT4d). *Finish* would
   * even record `completed` for an intake that published nothing, which a person reading it later
   * would take for a publish.
   */
  it('offers none of the moves that end the record without answering the ask', () => {
    show();

    expect(screen.queryByRole('button', { name: 'finish it' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'decline…' })).toBeNull();
    // Nothing is listening: an intake is one turn, and a parked one has no process left.
    expect(screen.queryByText(/Answering it in the box below/)).toBeNull();
  });

  it('can still be stopped, and says the ask then stays a proposal', async () => {
    const onStop = vi.fn();
    show({ onStop });

    expect(screen.getByText(/the ask stays a proposal/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'stop it' }));
    expect(onStop).toHaveBeenCalledTimes(1);
  });

  it('holds the stop while a move is in flight, and never the door', () => {
    show({ pending: true });

    expect(screen.getByRole('button', { name: 'stop it' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'answer ask #0fda18' })).toBeEnabled();
  });

  /** Where nothing can open the ask or stop the session, it still says where the answer lives. */
  it('offers no door and no stop where nothing can act, and still says where the answer is', () => {
    render(<AwaitingIntake ask="0fda18" note={QUESTION} />);

    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.getByText(/publish it to a repository, or close it/)).toBeInTheDocument();
  });

  it('renders with nothing said — a park with no words is still waiting on the ask', () => {
    show({ note: null });
    expect(screen.getByRole('button', { name: 'answer ask #0fda18' })).toBeInTheDocument();
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    show();
    expect(screen.getByRole('button', { name: '答复请求 #0fda18' })).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });
});
