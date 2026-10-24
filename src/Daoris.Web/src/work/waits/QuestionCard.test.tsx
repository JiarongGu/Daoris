import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { ParkQuestion } from './parkQuestion';
import { QuestionCard } from './QuestionCard';

// QUESTIONUI1a (D208, the supervisor design §8.1-§8.2): a park's question with each answer a row that reads whole, a native radio group
// labelled by the question, nothing selected at first, the recommended row marked with the agent's reason, its name the label,
// *recommended* once, and what it does. A molecule: the selection is handed in and reported out; the sends are the frame's
// (WorkFrame.test.tsx's *a park's question*).

const QUESTION: ParkQuestion = {
  id: 'q1',
  question: 'Which quarterly target should the dashboard tile show?',
  choices: [
    { label: '1,200', does: 'Write 1,200, the planning sheet\'s value.' },
    { label: '1,350', does: 'Write 1,350, the stand-up note\'s value.' },
    { label: 'Leave it', does: 'Ship the tile reading the configuration.' },
  ],
  recommend: 1,
  why: 'The ticket links the planning sheet.',
  by: null,
  at: '2026-10-22T09:05:00Z',
};

/** The card with the selection held as the frame holds it, each change reported. */
function Held({ question = QUESTION, onSelect }: { question?: ParkQuestion; onSelect?: (label: string | null) => void }) {
  const [selected, setSelected] = useState<string | null>(null);
  return (
    <>
      <button type="button">before</button>
      <QuestionCard question={question} selected={selected} onSelect={(label) => { setSelected(label); onSelect?.(label); }} />
      <button type="button">after</button>
    </>
  );
}

afterEach(() => { void i18n.changeLanguage('en'); });

describe('QuestionCard', () => {
  it('draws each answer as a radio in a group named by the question, none selected, the recommendation marked and not chosen', () => {
    render(<Held />);

    const group = screen.getByRole('radiogroup', { name: 'Which quarterly target should the dashboard tile show?' });
    const radios = within(group).getAllByRole('radio');
    expect(radios).toHaveLength(3);
    for (const radio of radios) expect(radio).not.toBeChecked();
    expect(within(group).getByText('If you choose this: Write 1,350, the stand-up note\'s value.')).toBeInTheDocument();
    expect(within(group).getByText('Why: The ticket links the planning sheet.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Clear choice' })).toBeNull();
  });

  it('names each row by its label, *recommended* once on the recommended one, and what it does; its reason is the description', () => {
    render(<Held />);

    const recommended = screen.getByRole('radio', {
      name: '1,200 recommended If you choose this: Write 1,200, the planning sheet\'s value.',
    });
    expect(recommended).toHaveAccessibleDescription('Why: The ticket links the planning sheet.');
    const other = screen.getByRole('radio', { name: '1,350 If you choose this: Write 1,350, the stand-up note\'s value.' });
    expect(other).not.toHaveAccessibleDescription();
    expect(screen.getAllByText('recommended')).toHaveLength(1);
  });

  it('is reached by Tab once, the arrows moving and selecting, Space selecting, and Tab going on past it', async () => {
    const onSelect = vi.fn();
    render(<Held onSelect={onSelect} />);
    const user = userEvent.setup();

    screen.getByRole('button', { name: 'before' }).focus();
    await user.tab();
    expect(screen.getByRole('radio', { name: /^1,200/ })).toHaveFocus();
    await user.keyboard(' ');
    expect(onSelect).toHaveBeenLastCalledWith('1,200');
    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('radio', { name: /^1,350/ })).toHaveFocus();
    expect(screen.getByRole('radio', { name: /^1,350/ })).toBeChecked();
    expect(onSelect).toHaveBeenLastCalledWith('1,350');
    await user.tab();
    expect(screen.getByRole('button', { name: 'Clear choice' })).toHaveFocus();
    await user.tab();
    expect(screen.getByRole('button', { name: 'after' })).toHaveFocus();
  });

  /** Codex's round-2 finding 15: a native radio cannot be unselected, so the card offers its own press to return to none. */
  it('clears its selection by its own press, the focus back on its first row, so the words can go alone', async () => {
    const onSelect = vi.fn();
    render(<Held onSelect={onSelect} />);

    await userEvent.click(screen.getByRole('radio', { name: /^Leave it/ }));
    expect(screen.getByRole('radio', { name: /^Leave it/ })).toBeChecked();
    await userEvent.click(screen.getByRole('button', { name: 'Clear choice' }));

    expect(onSelect).toHaveBeenLastCalledWith(null);
    for (const radio of screen.getAllByRole('radio')) expect(radio).not.toBeChecked();
    expect(screen.getByRole('radio', { name: /^1,200/ })).toHaveFocus();
    expect(screen.queryByRole('button', { name: 'Clear choice' })).toBeNull();
  });

  it('draws a selection it is handed, and nothing selected for a label the question does not offer', () => {
    const { rerender } = render(<QuestionCard question={QUESTION} selected="1,350" onSelect={vi.fn()} />);
    expect(screen.getByRole('radio', { name: /^1,350/ })).toBeChecked();

    rerender(<QuestionCard question={{ ...QUESTION, id: 'q2' }} selected="1,500" onSelect={vi.fn()} />);
    for (const radio of screen.getAllByRole('radio')) expect(radio).not.toBeChecked();
  });

  it('lists the answers with no radio where nothing selects, and draws no group for a question that offers none', () => {
    const { unmount } = render(<QuestionCard question={QUESTION} />);
    expect(screen.queryByRole('radio')).toBeNull();
    expect(screen.getAllByRole('listitem')).toHaveLength(3);
    expect(screen.getByText('Why: The ticket links the planning sheet.')).toBeInTheDocument();
    unmount();

    render(<QuestionCard question={{ ...QUESTION, choices: [], recommend: null, why: null, by: 'before Friday' }} onSelect={vi.fn()} />);
    expect(screen.queryByRole('radiogroup')).toBeNull();
    expect(screen.getByText('It says it needs your answer by: before Friday')).toBeInTheDocument();
  });

  /** SUPERVISOR1b (D208, the supervisor design §4.2): the supervisor's defer, above the choices, the bound in words and each reason with its source. */
  it('says above the answers that the supervisor left it to you, its bound in words and each reason with its source', () => {
    render(<Held question={{
      ...QUESTION,
      left: { bound: 'sign-in', reasons: [{ says: 'It needs your account.', source: 'D191 point 3' }, { says: 'Nothing records it.', source: 'docs/x.md:4' }] },
    }} />);

    const left = screen.getByRole('note', { name: 'The supervisor left this to you: a sign-in' });
    expect(within(left).getByText('It needs your account.')).toBeInTheDocument();
    expect(within(left).getByText('Source: D191 point 3')).toBeInTheDocument();
    expect(within(left).getByText('Source: docs/x.md:4')).toBeInTheDocument();
    // Above the choices, which stay the person's to press.
    expect(left.compareDocumentPosition(screen.getByRole('radiogroup')) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    for (const radio of screen.getAllByRole('radio')) expect(radio).not.toBeChecked();
  });

  /** SUPERVISOR1b (§4.1, §4.2): the agent's mark that the question is the owner's alone, by its words; a mark this build does not know, unnamed. */
  it('says a question its agent marked as yours alone, by the mark\'s words, and a mark it does not know without naming it', () => {
    const { unmount } = render(<QuestionCard question={{ ...QUESTION, owner: 'spending' }} />);
    expect(screen.getByText('Asked of you alone: spending')).toBeInTheDocument();
    unmount();

    const { unmount: gone } = render(<QuestionCard question={{ ...QUESTION, owner: 'a-later-mark' }} />);
    expect(screen.getByText('Asked of you alone')).toBeInTheDocument();
    expect(screen.queryByText(/a-later-mark/)).toBeNull();
    gone();

    render(<QuestionCard question={QUESTION} />);
    expect(screen.queryByText(/Asked of you alone/)).toBeNull();
    expect(screen.queryByRole('note')).toBeNull();
  });

  it('says the supervisor\'s defer and the mark in 中文, the reasons as written', async () => {
    await i18n.changeLanguage('zh');
    render(<QuestionCard question={{
      ...QUESTION, owner: 'sign-in', left: { bound: 'unrecorded', reasons: [{ says: 'Nothing records it.', source: 'D135 point 5' }] },
    }} />);

    expect(screen.getByText('只问你：登录')).toBeInTheDocument();
    const left = screen.getByRole('note', { name: '主管把这个留给你：记录里没有答案' });
    expect(within(left).getByText('Nothing records it.')).toBeInTheDocument();
    expect(within(left).getByText('来源：D135 point 5')).toBeInTheDocument();
  });

  it('says its chrome in 中文 and the agent\'s words as written', async () => {
    await i18n.changeLanguage('zh');
    render(<Held />);

    expect(screen.getByRole('radio', { name: '1,200 推荐 选这个的话：Write 1,200, the planning sheet\'s value.' }))
      .toHaveAccessibleDescription('理由：The ticket links the planning sheet.');
    await userEvent.click(screen.getByRole('radio', { name: /^1,350/ }));
    expect(screen.getByRole('button', { name: '清除选择' })).toBeInTheDocument();
  });
});
