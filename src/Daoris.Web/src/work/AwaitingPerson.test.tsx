import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import { AwaitingPerson } from './AwaitingPerson';

const ANALYSIS = 'Two ways forward.\n1. Cap in the scheduler.\n2. Cap in the chunk API.\nI recommend the second.';

const show = (props: Partial<Parameters<typeof AwaitingPerson>[0]> = {}) =>
  render(<AwaitingPerson note={ANALYSIS} onResolve={() => {}} {...props} />);

describe('a session parked at a checkpoint', () => {
  it('renders the analysis word for word — options, recommendation and reason survive rewording badly', () => {
    show();
    expect(screen.getByText(/I recommend the second/)).toBeInTheDocument();
  });

  /**
   * Finish and decline, and never the fourth move the ledger allows. The third, a stop, is the page header's since
   * SESSUX1d (D126 §3.3): one owner for a session's stop.
   */
  it('offers finish and decline, never the move the ledger keeps for the driver, and no stop of its own', async () => {
    const resolve = vi.fn();
    show({ onResolve: resolve });

    expect(screen.getByRole('button', { name: 'Finish' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Decline…' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Stop/ })).toBeNull();
    // `working` is the driver observing a session that carried on — a message, not a button.
    expect(screen.queryByRole('button', { name: /resume|continue|working/i })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Finish' }));
    expect(resolve).toHaveBeenCalledWith('completed', null);
  });

  /**
   * STANDDOWN2: a driven session that took its quest and stopped to ask has no process left to take a
   * message, so the answer is a move here — written on the record, and the quest carried on in the same
   * tree, handed the words. FG5's verify session asked for a merge, a sign-in and a go-ahead.
   */
  it('answers a session that has no process left, and carries its quest on with the words', async () => {
    const answer = vi.fn();
    show({ onAnswer: answer });

    await userEvent.click(screen.getByRole('button', { name: 'Answer and carry on…' }));
    await userEvent.type(screen.getByLabelText(/your answer/), 'Signed in; apply it to dev.');
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));

    expect(answer).toHaveBeenCalledWith('Signed in; apply it to dev.');
    // Its process is gone, so "answer it in the box below" would send the person nowhere.
    expect(screen.queryByText(/in the box below/)).toBeNull();
  });

  it('carries on with no words when the person has nothing to add', async () => {
    const answer = vi.fn();
    show({ onAnswer: answer });

    await userEvent.click(screen.getByRole('button', { name: 'Answer and carry on…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));

    expect(answer).toHaveBeenCalledWith(null);
  });

  /**
   * The same two-step a quest's page uses: the note is the only part whoever reads the record
   * later can act on, and a decline that slipped out on one click would routinely carry nothing.
   */
  it('asks for a reason before declining, and will not decline without one', async () => {
    const resolve = vi.fn();
    show({ onResolve: resolve });

    await userEvent.click(screen.getByRole('button', { name: 'Decline…' }));
    const confirm = screen.getByRole('button', { name: 'Decline with this reason' });
    expect(confirm).toBeDisabled();
    expect(resolve).not.toHaveBeenCalled();

    await userEvent.type(screen.getByLabelText(/the reason/), 'the chunk API is being replaced');
    await userEvent.click(screen.getByRole('button', { name: 'Decline with this reason' }));
    expect(resolve).toHaveBeenCalledWith('declined', 'the chunk API is being replaced');
  });

  it('lets a person back out of declining without having declined', async () => {
    const resolve = vi.fn();
    show({ onResolve: resolve });

    await userEvent.click(screen.getByRole('button', { name: 'Decline…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Never mind' }));

    expect(screen.getByRole('button', { name: 'Finish' })).toBeInTheDocument();
    expect(resolve).not.toHaveBeenCalled();
  });

  it('holds every move while one is in flight', () => {
    show({ pending: true });
    for (const name of ['Finish', 'Decline…']) {
      expect(screen.getByRole('button', { name })).toBeDisabled();
    }
  });

  it('renders with no analysis at all — a park with nothing said is still a park', () => {
    show({ note: null });
    expect(screen.getByRole('button', { name: 'Finish' })).toBeInTheDocument();
  });

  /** The fourth move exists and is reachable — by answering, which the surface says out loud. */
  it('says how a person makes it carry on instead', () => {
    show();
    expect(screen.getByText(/Answering it in the box below/)).toBeInTheDocument();
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    show();
    expect(screen.getByRole('button', { name: '完成' })).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });
});

/**
 * QUESTCLOSE1 (D126's note): on the install the person finished two driven sessions at a checkpoint and each quest stayed
 * taken, with nothing to close it. A finish whose session holds a quest still open or taken asks, in the same act, what
 * becomes of that quest: left as it is, the default, or marked done as the person's, with their note.
 */
describe('finishing a session that holds a quest (QUESTCLOSE1)', () => {
  const QUEST = { id: 'e7c990e60493', status: 'Taken' as const };

  it('asks what becomes of its quest, leaving it as it is unless the person says otherwise', async () => {
    const resolve = vi.fn();
    show({ onResolve: resolve, quest: QUEST });

    expect(screen.queryByRole('button', { name: 'Finish' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Finish…' }));
    expect(screen.getByText('Finishing ends this session. Its quest #e7c990e60493 stays as it is unless you mark it done now.'))
      .toBeInTheDocument();
    const choice = screen.getByRole('radiogroup', { name: 'Its quest' });
    expect(within(choice).getByRole('radio', { name: 'Leave it as it is' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.queryByLabelText(/your note on the quest/)).toBeNull();
    expect(resolve).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Finish' }));
    expect(resolve).toHaveBeenCalledWith('completed', null);
  });

  it('marks its quest done as the person’s, with their note, in the same press', async () => {
    const resolve = vi.fn();
    show({ onResolve: resolve, quest: QUEST });

    await userEvent.click(screen.getByRole('button', { name: 'Finish…' }));
    await userEvent.click(screen.getByRole('radio', { name: 'Mark it done as yours' }));
    await userEvent.type(screen.getByLabelText(/your note on the quest/), '  The write-up is in the shared folder.  ');
    await userEvent.click(screen.getByRole('button', { name: 'Finish' }));

    expect(resolve).toHaveBeenCalledWith('completed', null, { as: 'done', note: 'The write-up is in the shared folder.' });
  });

  it('sends no words where none were written, and Never mind finishes nothing', async () => {
    const resolve = vi.fn();
    show({ onResolve: resolve, quest: QUEST });

    await userEvent.click(screen.getByRole('button', { name: 'Finish…' }));
    await userEvent.click(screen.getByRole('radio', { name: 'Mark it done as yours' }));
    await userEvent.click(screen.getByRole('button', { name: 'Never mind' }));
    expect(resolve).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Decline…' })).toBeInTheDocument();

    // Asked again, it starts from leaving the quest as it is.
    await userEvent.click(screen.getByRole('button', { name: 'Finish…' }));
    expect(screen.getByRole('radio', { name: 'Leave it as it is' })).toHaveAttribute('aria-checked', 'true');
    await userEvent.click(screen.getByRole('radio', { name: 'Mark it done as yours' }));
    await userEvent.click(screen.getByRole('button', { name: 'Finish' }));
    expect(resolve).toHaveBeenCalledWith('completed', null, { as: 'done', note: null });
  });

  it('asks in 中文', async () => {
    await i18n.changeLanguage('zh');
    const resolve = vi.fn();
    show({ onResolve: resolve, quest: QUEST });

    await userEvent.click(screen.getByRole('button', { name: '完成…' }));
    expect(screen.getByText('完成会了结这个会话。除非你现在把它标为完成，它的委托 #e7c990e60493 保持原样。')).toBeInTheDocument();
    const choice = screen.getByRole('radiogroup', { name: '它的委托' });
    expect(within(choice).getByRole('radio', { name: '保持原样' })).toHaveAttribute('aria-checked', 'true');
    await userEvent.click(within(choice).getByRole('radio', { name: '以你的名义标为完成' }));
    await userEvent.type(screen.getByLabelText(/你对委托的备注/), '已放在共享文件夹。');
    await userEvent.click(screen.getByRole('button', { name: '完成' }));
    expect(resolve).toHaveBeenCalledWith('completed', null, { as: 'done', note: '已放在共享文件夹。' });
    await i18n.changeLanguage('en');
  });
});
