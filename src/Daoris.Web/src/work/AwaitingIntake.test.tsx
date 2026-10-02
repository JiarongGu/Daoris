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
  render(<AwaitingIntake ask="0fda18" note={QUESTION} onAnswer={() => {}} {...props} />);

describe('a parked intake', () => {
  it('shows the intake\'s own words, verbatim', () => {
    show();
    expect(screen.getByText(QUESTION)).toBeInTheDocument();
  });

  /** The answer is on the ask: publishing it, or closing it, ends this session at the next tick. */
  it('leads to its ask as the answer, naming it', async () => {
    const onAnswer = vi.fn();
    show({ onAnswer });

    await userEvent.click(screen.getByRole('button', { name: 'Answer ask #0fda18' }));
    expect(onAnswer).toHaveBeenCalledWith('0fda18');
  });

  /**
   * 🔴 Each of the three ends the record without answering the ask (found by INT4d). *Finish* would
   * even record `completed` for an intake that published nothing, which a person reading it later
   * would take for a publish.
   */
  it('offers none of the moves that end the record without answering the ask', () => {
    show();

    expect(screen.queryByRole('button', { name: 'Finish' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Decline…' })).toBeNull();
    // Nothing is listening: an intake is one turn, and a parked one has no process left.
    expect(screen.queryByText(/Answering it in the box below/)).toBeNull();
  });

  /** SESSUX1d (D126 §3.3): the stop and its sentence moved to the page header, the stop's one owner. */
  it('offers no stop of its own: the page header’s stop asks with the sentence it said', () => {
    show();

    expect(screen.queryByRole('button', { name: /^Stop/ })).toBeNull();
    expect(screen.queryByText(/the ask stays a proposal/)).toBeNull();
  });

  /** Where nothing can open the ask, it still says where the answer lives. */
  it('offers no door where nothing can act, and still says where the answer is', () => {
    render(<AwaitingIntake ask="0fda18" note={QUESTION} />);

    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.getByText(/publish it to a repository, or close it/)).toBeInTheDocument();
  });

  it('renders with nothing said — a park with no words is still waiting on the ask', () => {
    show({ note: null });
    expect(screen.getByRole('button', { name: 'Answer ask #0fda18' })).toBeInTheDocument();
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    show();
    expect(screen.getByRole('button', { name: '答复需求 #0fda18' })).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });
});
