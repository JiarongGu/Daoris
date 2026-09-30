import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
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

  it('offers exactly the three moves, and never the fourth the ledger allows', async () => {
    const resolve = vi.fn();
    show({ onResolve: resolve });

    expect(screen.getByRole('button', { name: 'finish it' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'decline…' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'stop it' })).toBeInTheDocument();
    // `working` is the driver observing a session that carried on — a message, not a button.
    expect(screen.queryByRole('button', { name: /resume|continue|working/i })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'finish it' }));
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

    await userEvent.click(screen.getByRole('button', { name: 'answer and carry on…' }));
    await userEvent.type(screen.getByLabelText(/your answer/), 'Signed in; apply it to dev.');
    await userEvent.click(screen.getByRole('button', { name: 'carry on with this answer' }));

    expect(answer).toHaveBeenCalledWith('Signed in; apply it to dev.');
    // Its process is gone, so "answer it in the box below" would send the person nowhere.
    expect(screen.queryByText(/in the box below/)).toBeNull();
  });

  it('carries on with no words when the person has nothing to add', async () => {
    const answer = vi.fn();
    show({ onAnswer: answer });

    await userEvent.click(screen.getByRole('button', { name: 'answer and carry on…' }));
    await userEvent.click(screen.getByRole('button', { name: 'carry on with this answer' }));

    expect(answer).toHaveBeenCalledWith(null);
  });

  it('stops it as the person, with nothing they have to write', async () => {
    const resolve = vi.fn();
    show({ onResolve: resolve });

    await userEvent.click(screen.getByRole('button', { name: 'stop it' }));
    expect(resolve).toHaveBeenCalledWith('stopped', null);
  });

  /**
   * The same two-step the quest drawer uses: the note is the only part whoever reads the record
   * later can act on, and a decline that slipped out on one click would routinely carry nothing.
   */
  it('asks for a reason before declining, and will not decline without one', async () => {
    const resolve = vi.fn();
    show({ onResolve: resolve });

    await userEvent.click(screen.getByRole('button', { name: 'decline…' }));
    const confirm = screen.getByRole('button', { name: 'decline with this reason' });
    expect(confirm).toBeDisabled();
    expect(resolve).not.toHaveBeenCalled();

    await userEvent.type(screen.getByLabelText(/the reason/), 'the chunk API is being replaced');
    await userEvent.click(screen.getByRole('button', { name: 'decline with this reason' }));
    expect(resolve).toHaveBeenCalledWith('declined', 'the chunk API is being replaced');
  });

  it('lets a person back out of declining without having declined', async () => {
    const resolve = vi.fn();
    show({ onResolve: resolve });

    await userEvent.click(screen.getByRole('button', { name: 'decline…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Never mind' }));

    expect(screen.getByRole('button', { name: 'finish it' })).toBeInTheDocument();
    expect(resolve).not.toHaveBeenCalled();
  });

  it('holds every move while one is in flight', () => {
    show({ pending: true });
    for (const name of ['finish it', 'decline…', 'stop it']) {
      expect(screen.getByRole('button', { name })).toBeDisabled();
    }
  });

  it('renders with no analysis at all — a park with nothing said is still a park', () => {
    show({ note: null });
    expect(screen.getByRole('button', { name: 'finish it' })).toBeInTheDocument();
  });

  /** The fourth move exists and is reachable — by answering, which the surface says out loud. */
  it('says how a person makes it carry on instead', () => {
    show();
    expect(screen.getByText(/Answering it in the box below/)).toBeInTheDocument();
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    show();
    expect(screen.getByRole('button', { name: '就此完成' })).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });
});
