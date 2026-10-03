import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { SessionBox } from './SessionBox';

// The box at the foot of a session's page (MSG1f, D137 §5.1), props in: the running door's box, the park's answer, the
// box where the same session goes on, the words held while a session winds up, and the line where nothing takes words.

const show = (props: Partial<Parameters<typeof SessionBox>[0]> & Pick<Parameters<typeof SessionBox>[0], 'box'>) =>
  render(<Tooltip.Provider><SessionBox onSend={() => {}} {...props} /></Tooltip.Provider>);

describe('SessionBox', () => {
  /** D136: a working session's running door, its words held behind its turn and sent now on a press. */
  it('offers a working session the running door’s box, and Send now only with something held', async () => {
    const onSend = vi.fn();
    const onSendNow = vi.fn();
    const { unmount } = show({ box: { kind: 'steer' }, held: { queued: [], taking: true }, onSend, onSendNow });

    const field = screen.getByLabelText('Message');
    expect(field).toHaveAttribute('placeholder', expect.stringMatching(/tell it something while it works/));
    expect(screen.queryByRole('button', { name: 'Attach files' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send now' })).toBeNull();
    await userEvent.type(field, 'the budget is in level.json');
    await userEvent.click(screen.getByRole('button', { name: 'Queue' }));
    expect(onSend).toHaveBeenCalledWith('the budget is in level.json');
    unmount();

    show({ box: { kind: 'steer' }, held: { queued: [{ text: 'cap it', files: [] }], taking: true }, onSendNow });
    expect(screen.getByText('cap it')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Send now' }));
    expect(onSendNow).toHaveBeenCalled();
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
