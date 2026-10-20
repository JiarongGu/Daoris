import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import { SessionBox } from './SessionBox';

// The box at the foot of a session's page (MSG1f, D137 §5.1), props in: the running door's box, the park's answer, the
// box where the same session goes on, the words held while a session winds up, and the line where nothing takes words.

const show = (props: Partial<Parameters<typeof SessionBox>[0]> & Pick<Parameters<typeof SessionBox>[0], 'box'>) =>
  render(<Tooltip.Provider><SessionBox onSend={() => {}} {...props} /></Tooltip.Provider>);

describe('SessionBox', () => {
  /**
   * D136, COWORK1a: a working session's running door, its words held behind its turn. *Send* goes at the person's default;
   * nothing presses now with nothing typed (the design's trap 2), and with no levels known the press queues as it did.
   */
  it('offers a working session the running door’s box, at its default', async () => {
    const onSend = vi.fn();
    const { unmount } = show({ box: { kind: 'steer' }, held: { queued: [], taking: true }, onSend, levels: { nextStep: true, steers: false } });

    const field = screen.getByLabelText('Message');
    expect(field).toHaveAttribute('placeholder', expect.stringMatching(/tell it something while it works/));
    expect(screen.queryByRole('button', { name: 'Attach files' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send now' })).toBeNull();
    await userEvent.type(field, 'the budget is in level.json');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    expect(onSend).toHaveBeenCalledWith('the budget is in level.json');
    unmount();

    show({ box: { kind: 'steer' }, held: { queued: [{ text: 'cap it', files: [] }], taking: true } });
    expect(screen.getByText('cap it')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Queue' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'When it reads this' })).toBeNull();
  });

  /**
   * COWORK1a (the message-urgency design §3.2, §8.1): the running door's words go at the level chosen from the menu beside
   * *Send*; the keeping stop stands beside what waits for the turn's end only where the door can stop a turn keeping the
   * session, which the native door's run cannot.
   */
  it('sends at a chosen level, and offers the keeping stop only where the door has one', async () => {
    const onSend = vi.fn();
    const onKeepingStop = vi.fn();
    const held = { queued: [{ id: 'said-1', text: 'cap it', files: [] }], taking: true };
    const { unmount } = show({ box: { kind: 'steer' }, held, onSend, onKeepingStop, levels: { nextStep: false, steers: false } });

    await userEvent.type(screen.getByLabelText('Message'), 'use the blue one');
    const user = userEvent.setup();
    screen.getByRole('button', { name: 'When it reads this' }).focus();
    await user.keyboard('{Enter}');
    await userEvent.click(screen.getByRole('menuitem', { name: /When its turn ends/ }));
    expect(onSend).toHaveBeenCalledWith('use the blue one', 'ordinary');
    expect(screen.getByRole('button', { name: 'Stop its turn so this goes next…' })).toBeInTheDocument();
    unmount();

    // The native door's run: its words wait for its turn's end, and nothing stops a turn keeping it.
    show({ box: { kind: 'steer' }, held, onSend, levels: { nextStep: false, steers: false } });
    expect(screen.queryByRole('button', { name: 'Stop its turn so this goes next…' })).toBeNull();
  });

  /** QUEUEEDIT1b: a held word with an id can be taken back out, or reworded, on the running door's box. */
  it('lets a held word be removed or reworded by its id', async () => {
    const onRemove = vi.fn().mockResolvedValue({ done: true });
    const onEdit = vi.fn().mockResolvedValue({ done: true });
    show({ box: { kind: 'steer' }, held: { queued: [{ id: 'a1', text: 'cap it', files: [] }], taking: true }, onRemove, onEdit });

    await userEvent.click(screen.getByRole('button', { name: 'Remove the waiting message: cap it' }));
    expect(onRemove).toHaveBeenCalledWith('a1');
    await userEvent.click(screen.getByRole('button', { name: 'Edit the waiting message: cap it' }));
    await userEvent.type(screen.getByLabelText('Message'), ' hard');
    await userEvent.click(screen.getByRole('button', { name: 'Queue' }));
    expect(onEdit).toHaveBeenCalledWith('a1', 'cap it hard');
  });

  /**
   * SENDLOST1b: words on their way show above every box that takes words, the running door's, the park's answer, the box
   * where the same session goes on and the one winding up, as sending or, once the page stopped waiting, as unanswered.
   */
  it('shows the words on their way above every box that takes words', () => {
    const going = { words: [{ text: 'and the line?', files: [] }], late: true };
    for (const box of [
      { kind: 'steer' }, { kind: 'say', mode: 'answer' }, { kind: 'say', mode: 'goOn' }, { kind: 'say', mode: 'ending' },
    ] as const) {
      const { unmount } = show({ box, going, held: { queued: [], taking: true } });
      expect(screen.getByRole('list', { name: 'The machine has not answered yet; it may still take them.' }))
        .toHaveTextContent('and the line?');
      unmount();
    }
    show({ box: { kind: 'say', mode: 'goOn' }, going: { ...going, late: false } });
    expect(screen.getByRole('list', { name: 'sending…' })).toHaveTextContent('and the line?');
  });

  /** D126 §3.1, D137 §5.1: on a parked session the box is the answer. */
  it('offers a parked session the answer', async () => {
    const onSend = vi.fn();
    show({ box: { kind: 'say', mode: 'answer' }, onSend });

    const field = screen.getByLabelText('Message');
    expect(field).toHaveAttribute('placeholder', 'your answer — the same session goes on with these words');
    await userEvent.type(field, 'go ahead with the PUT');
    await userEvent.click(screen.getByRole('button', { name: 'Carry on with this answer' }));
    expect(onSend).toHaveBeenCalledWith('go ahead with the PUT');
  });

  /** D137 §5.1: on a session that ended, the box says the same session goes on with the words; nothing to attach. */
  it('offers a session that ended the box where the same session goes on', async () => {
    const onSend = vi.fn();
    show({ box: { kind: 'say', mode: 'goOn' }, onSend });

    const field = screen.getByLabelText('Message');
    expect(field).toHaveAttribute('placeholder', 'write to it — the same session goes on with your words');
    expect(screen.queryByRole('button', { name: 'Attach files' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Finish' })).toBeNull();
    await userEvent.type(field, 'also cap it');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    expect(onSend).toHaveBeenCalledWith('also cap it');
  });

  /**
   * SAIDBACK1b (D76): the words waiting on a record that ended or parked are listed above its box, each by its id, and are
   * taken back or reworded there as a live queue's are; the reword goes as an edit, never as more words.
   */
  it('lists the words waiting on a record above its box, to remove or reword by their ids', async () => {
    const onSend = vi.fn();
    const onRemove = vi.fn().mockResolvedValue({ done: true });
    const onEdit = vi.fn().mockResolvedValue({ done: true });
    show({ box: { kind: 'say', mode: 'goOn' }, waiting: [{ id: 'w1', text: 'use the blue one', files: [] }], onSend, onRemove, onEdit });

    expect(screen.getByRole('list', { name: 'waiting for the same session to go on with them' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Remove the waiting message: use the blue one' }));
    expect(onRemove).toHaveBeenCalledWith('w1');
    await userEvent.click(screen.getByRole('button', { name: 'Edit the waiting message: use the blue one' }));
    const field = screen.getByLabelText('Message');
    await userEvent.clear(field);
    await userEvent.type(field, 'use the green one');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    expect(onEdit).toHaveBeenCalledWith('w1', 'use the green one');
    expect(onSend).not.toHaveBeenCalled();
  });

  /**
   * WAITFOOT2c (D168): a park's answer box and its waiting words are one box, so the words fold to one line with their count
   * inside the box's frame, and open to *Edit* and *Remove* on a press.
   */
  it('folds the words waiting on a park to one line with their count, open to remove or reword', async () => {
    const onRemove = vi.fn().mockResolvedValue({ done: true });
    const waiting = [{ id: 'w1', text: 'use the blue one', files: [] }, { id: 'w2', text: 'and the red', files: [] }];
    show({ box: { kind: 'say', mode: 'answer' }, waiting, onRemove, onEdit: vi.fn() });

    expect(screen.getByText('2 said')).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'waiting for the same session to go on with them' })).toBeNull();
    expect(screen.queryByRole('button', { name: /Remove the waiting message/ })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Show them' }));
    expect(screen.getByRole('list', { name: 'waiting for the same session to go on with them' })).toHaveTextContent('use the blue one');
    await userEvent.click(screen.getByRole('button', { name: 'Remove the waiting message: and the red' }));
    expect(onRemove).toHaveBeenCalledWith('w2');
    await userEvent.click(screen.getByRole('button', { name: 'Hide them' }));
    expect(screen.queryByRole('list', { name: 'waiting for the same session to go on with them' })).toBeNull();
  });

  /** D137 §2.1: words said as a session winds up wait for its record to end, and the box shows them waiting. */
  it('shows the words held while a session winds up above its box', () => {
    show({ box: { kind: 'say', mode: 'ending' }, ending: ['also cap it'] });

    expect(screen.getByText('held until this run ends — then the same session goes on with them')).toBeInTheDocument();
    expect(screen.getByText('also cap it')).toBeInTheDocument();
    expect(screen.getByLabelText('Message')).toBeInTheDocument();
  });

  /** D137 §2.4: words the door cannot take at once stay in the box, unsent, and the caller says why. */
  it('keeps words its door does not accept in the box, unsent', async () => {
    const onSend = vi.fn();
    show({ box: { kind: 'say', mode: 'goOn' }, onSend, accepts: (text) => text.length <= 5 });

    const field = screen.getByLabelText('Message');
    await userEvent.type(field, 'too long by far');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    expect(onSend).not.toHaveBeenCalled();
    expect(field).toHaveValue('too long by far');
  });

  it('says a refusal where the person is looking, and keeps the draft it is handed', () => {
    show({ box: { kind: 'say', mode: 'goOn' }, refusal: 'This agent\'s door takes at most 24000 characters at once; shorten it, or send it in parts.', draft: 'x', onDraft: () => {} });
    expect(screen.getByText(/takes at most 24000 characters/)).toBeInTheDocument();
    expect(screen.getByLabelText('Message')).toHaveValue('x');
  });

  /** D137 §5.1, D119 §3.2: no box where nothing takes words, and the line says why, by the code. */
  it.each([
    ['teammate', 'This session ran on another machine, where its conversation is.'],
    ['stood-down', 'It stood down: #q1 is someone else\'s, so it has nothing to go on with.'],
    ['intake', 'An intake takes no words: it is answered through its ask.'],
    ['help', 'Ask Daoris\'s conversations take words in its own panel, which starts a new one.'],
    ['superseded', '#q1 went on in a later session here, so write to that one.'],
    ['not-found', 'Daoris no longer has this session\'s record, so nothing can take words for it.'],
  ])('draws the line for %s instead of a box', (why, said) => {
    show({ box: { kind: 'line', why }, quest: 'q1' });
    expect(screen.getByText(said)).toBeInTheDocument();
    expect(screen.queryByLabelText('Message')).toBeNull();
  });

  /** HANDWORDS1: a conversation handed over or moved names the one it went on in, as the module answered it. */
  it('names the conversation a continued one went on in', () => {
    show({ box: { kind: 'line', why: 'continued', to: 'c2' } });
    expect(screen.getByText('It went on in conversation c2; say it to that one.')).toBeInTheDocument();
  });

  /**
   * HANDWORDS1-ii (D165's HANDWORDS1-i note, design §4): where the frame holds the conversation it went on in, the line names it
   * by its title and *Open session* attends it; one the frame does not hold is said by its id, as before.
   */
  it('names the conversation it went on in by its title, and opens it', async () => {
    const onOpenSession = vi.fn();
    const successor = { id: 'c2' } as Session;
    show({ box: { kind: 'line', why: 'continued', to: 'c2' }, continued: { session: successor, title: 'Tidy the build' }, onOpenSession });

    expect(screen.getByText('This conversation went on in Tidy the build.')).toBeInTheDocument();
    expect(screen.queryByText(/say it to that one/)).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Open session' }));
    expect(onOpenSession).toHaveBeenCalledWith(successor);
  });

  it('says the conversation by its id where the link names another one', () => {
    show({ box: { kind: 'line', why: 'continued', to: 'c3' }, continued: { session: { id: 'c2' } as Session, title: 'Tidy' }, onOpenSession: () => {} });
    expect(screen.getByText('It went on in conversation c3; say it to that one.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Open session' })).toBeNull();
  });

  it('names the conversation it went on in, in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      show({ box: { kind: 'line', why: 'continued', to: 'c2' }, continued: { session: { id: 'c2' } as Session, title: '整理构建' }, onOpenSession: () => {} });
      expect(screen.getByText('这个对话已在「整理构建」中继续。')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '打开会话' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('says the line in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      show({ box: { kind: 'line', why: 'teammate' } });
      expect(screen.getByText('这个会话在另一台机器上运行，它的对话在那里。')).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('draws nothing where nothing is offered', () => {
    const { container } = show({ box: { kind: 'none' } });
    expect(container.textContent).toBe('');
  });
});
