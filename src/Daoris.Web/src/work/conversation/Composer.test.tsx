import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { Composer } from './Composer';

const show = (props: Partial<Parameters<typeof Composer>[0]> = {}) => render(
  <Tooltip.Provider>
    <Composer live onSend={() => {}} onFinish={() => {}} onStop={() => {}} {...props} />
  </Tooltip.Provider>,
);

const box = () => screen.getByLabelText('Message');

/** A Radix menu opens on the keyboard, as the other pages' tests open theirs. */
const openMenu = async (name: string) => {
  const user = userEvent.setup();
  screen.getByRole('button', { name }).focus();
  await user.keyboard('{Enter}');
};

describe('the composer', () => {
  it('sends what was typed and clears the box', async () => {
    const send = vi.fn();
    show({ onSend: send });

    await userEvent.type(box(), 'cap hydration per frame');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    // Once: the button is the form's submit, and a click that also called `say` sent every message
    // twice — seen as two "you" blocks once the record kept what was sent (CONV3).
    expect(send).toHaveBeenCalledTimes(1);
    expect(send).toHaveBeenCalledWith('cap hydration per frame', []);
    expect(box()).toHaveValue('');
  });

  it('sends on Enter and takes a newline on Shift+Enter — a pasted paragraph stays one message', async () => {
    const send = vi.fn();
    show({ onSend: send });

    await userEvent.type(box(), 'first{Shift>}{Enter}{/Shift}second');
    expect(send).not.toHaveBeenCalled();

    await userEvent.type(box(), '{Enter}');
    expect(send).toHaveBeenCalledWith('first\nsecond', []);
  });

  /**
   * IME1: the Enter that accepts an input method's candidate is the input method's. Sent, it was half a message in
   * every conversation (sessions, chats, Ask Daoris), since this box is all of theirs.
   */
  it('sends nothing on the Enter an input method is composing with, and sends on the plain one after', () => {
    const send = vi.fn();
    show({ onSend: send });
    const field = box();
    fireEvent.change(field, { target: { value: '你好' } });

    expect(fireEvent.keyDown(field, { key: 'Enter', isComposing: true })).toBe(true);
    // The press that ends a composition can come with the flag already down, and 229 for its code.
    expect(fireEvent.keyDown(field, { key: 'Enter', keyCode: 229 })).toBe(true);
    expect(send).not.toHaveBeenCalled();
    expect(field).toHaveValue('你好');

    fireEvent.keyDown(field, { key: 'Enter', keyCode: 13 });
    expect(send).toHaveBeenCalledWith('你好', []);
  });

  it('will not send nothing, nor send twice while one is in flight', async () => {
    const send = vi.fn();
    const { rerender } = show({ onSend: send });

    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    expect(send).not.toHaveBeenCalled();

    await userEvent.type(box(), 'something');
    rerender(
      <Tooltip.Provider>
        <Composer live sending onSend={send} onFinish={() => {}} onStop={() => {}} />
      </Tooltip.Provider>,
    );
    expect(screen.getByRole('button', { name: 'Send' })).toBeDisabled();
  });

  /**
   * Two endings, and they are never one button: finishing lets the harness wind up (`completed`),
   * stopping is the person's interrupt (`stopped`). The ledger can tell them apart and so must the
   * surface a person chooses between them on.
   */
  it('offers finishing and stopping separately, and only while something is listening', async () => {
    const finish = vi.fn();
    const stop = vi.fn();
    const { rerender } = show({ onFinish: finish, onStop: stop });

    await userEvent.click(screen.getByRole('button', { name: 'Finish' }));
    await userEvent.click(screen.getByRole('button', { name: 'Stop' }));
    expect(finish).toHaveBeenCalledOnce();
    expect(stop).toHaveBeenCalledOnce();

    rerender(
      <Tooltip.Provider>
        <Composer live={false} onSend={() => {}} onFinish={finish} onStop={stop} />
      </Tooltip.Provider>,
    );
    expect(screen.queryByRole('button', { name: 'Finish' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Stop' })).not.toBeInTheDocument();
  });

  /**
   * SESSUX1d (D126 §3.3): in Sessions the session's stop is its page header's, its one owner, so a chat's composer there
   * keeps *Finish* and *Stop turn* and is handed no stop. Ask Daoris's panel, which holds its conversation on every view,
   * still hands it one.
   */
  it('offers no session stop where it is handed none, and keeps Finish and the turn’s own stop', () => {
    render(
      <Tooltip.Provider>
        <Composer live taking stoppable onSend={() => {}} onFinish={() => {}} onStopTurn={() => {}} />
      </Tooltip.Provider>,
    );
    expect(screen.getByRole('button', { name: 'Finish' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Stop turn' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Stop' })).not.toBeInTheDocument();
  });

  /** *Answer…* from a session's row puts the focus in the box at its foot (D126 §3.1), once per press. */
  it('takes the focus when it is told to, and again on a second telling', () => {
    const { rerender } = render(
      <Tooltip.Provider><Composer live onSend={() => {}} onFinish={() => {}} focus={0} /></Tooltip.Provider>,
    );
    expect(box()).not.toHaveFocus();

    rerender(<Tooltip.Provider><Composer live onSend={() => {}} onFinish={() => {}} focus={1} /></Tooltip.Provider>);
    expect(box()).toHaveFocus();

    box().blur();
    rerender(<Tooltip.Provider><Composer live onSend={() => {}} onFinish={() => {}} focus={2} /></Tooltip.Provider>);
    expect(box()).toHaveFocus();
  });

  it('keeps a draft the session ended underneath, and says nothing is listening', async () => {
    const { rerender } = show();
    await userEvent.type(box(), 'half a thought');

    rerender(
      <Tooltip.Provider>
        <Composer live={false} onSend={() => {}} onFinish={() => {}} onStop={() => {}} />
      </Tooltip.Provider>,
    );

    expect(box()).toHaveValue('half a thought');
    expect(screen.getByText(/This session is over/)).toBeInTheDocument();
    // Read-only, not disabled: a disabled box's words cannot be selected, and they are the person's to
    // copy out. And no send, which could only ever be a dead press (UX5 U9).
    expect(box()).toHaveAttribute('readonly');
    expect(box()).not.toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
  });

  /**
   * UX5 U9, seen on the window: an ended session opened from the rail showed an empty box and a send
   * under "What you typed is still here", when nothing was typed. A box where nothing listens is the
   * one INT4h removed from the intake; here it is removed wherever nothing was written.
   */
  it('offers no box to an ended session nobody was writing in, and keeps the meter', () => {
    show({ live: false, context: { door: 'structured' } });

    expect(screen.queryByLabelText('Message')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
    expect(screen.getByText('This session has ended; nothing is listening for a message.')).toBeInTheDocument();
    expect(screen.queryByText(/What you typed/)).toBeNull();
    expect(screen.getByLabelText(/^context/)).toBeInTheDocument();
  });

  /**
   * CONV4b: the third verb. Stopping the turn keeps the conversation, and is offered only while a turn
   * runs on a door that can stop one — a text door cannot see where a turn ends, and a button that
   * could only be refused is worse than none.
   */
  it('offers to stop the turn only while one runs on a door that can stop it', async () => {
    const stopTurn = vi.fn();
    const { rerender } = show({ taking: true, stoppable: true, onStopTurn: stopTurn });

    await userEvent.click(screen.getByRole('button', { name: 'Stop turn' }));
    expect(stopTurn).toHaveBeenCalledOnce();
    // Beside the endings, never instead of them.
    expect(screen.getByRole('button', { name: 'Finish' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Stop' })).toBeInTheDocument();

    const again = (props: Partial<Parameters<typeof Composer>[0]>) => rerender(
      <Tooltip.Provider>
        <Composer live onSend={() => {}} onFinish={() => {}} onStop={() => {}} onStopTurn={stopTurn} {...props} />
      </Tooltip.Provider>,
    );
    again({ taking: true, stoppable: true, stopping: true });
    expect(screen.getByRole('button', { name: 'Stop turn' })).toBeDisabled();
    again({ taking: true, stoppable: false });
    expect(screen.queryByRole('button', { name: 'Stop turn' })).not.toBeInTheDocument();
    again({ taking: false, stoppable: true });
    expect(screen.queryByRole('button', { name: 'Stop turn' })).not.toBeInTheDocument();
  });

  /**
   * COWORK1a (D188; the message-urgency design §8.1, trap 2): while a turn runs, one press, *Send*, at the person's default,
   * and a menu of the three levels beside it, each sending the box's words at its level. Enter sends at the default. With
   * nothing typed nothing presses now: the stop that press was is the keeping stop's own (trap 2).
   */
  it('sends at the default while a turn runs, or at the level chosen from the menu beside it', async () => {
    const send = vi.fn();
    show({ taking: true, stoppable: true, levels: { nextStep: true, steers: true }, onSend: send, queued: [{ id: 'a', text: 'cap it', files: [] }] });

    expect(screen.queryByRole('button', { name: 'Queue' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send now' })).toBeNull();
    expect(screen.getByRole('button', { name: 'When it reads this' })).toBeDisabled();
    await userEvent.type(box(), 'the budget is 64 KiB{Enter}');
    expect(send).toHaveBeenLastCalledWith('the budget is 64 KiB', []);

    await userEvent.type(box(), 'tidy the readme after');
    await openMenu('When it reads this');
    await userEvent.click(screen.getByRole('menuitem', { name: /When its turn ends/ }));
    expect(send).toHaveBeenLastCalledWith('tidy the readme after', [], 'ordinary');
    expect(box()).toHaveValue('');

    await userEvent.type(box(), 'stop: the tests are in /spec');
    await openMenu('When it reads this');
    await userEvent.click(screen.getByRole('menuitem', { name: /Now, cutting its step/ }));
    expect(send).toHaveBeenLastCalledWith('stop: the tests are in /spec', [], 'now');

    await userEvent.type(box(), 'and the changelog');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    expect(send).toHaveBeenLastCalledWith('and the changelog', []);
  });

  /** COWORK1a (the design §8.1): *Now, cutting its step* is offered only where the agent steers; elsewhere it is disabled, saying why. */
  it.each([
    [{ nextStep: true, steers: false }, 'Its agent cannot be steered mid-turn, so these go at its next step.'],
    [{ nextStep: false, steers: false }, 'Its door cannot take words in a running turn, so these are held for its turn to end.'],
  ])('offers now only where the agent steers, and says why on %o', async (levels, why) => {
    const send = vi.fn();
    show({ taking: true, levels, onSend: send });

    await userEvent.type(box(), 'stop: the tests are in /spec');
    await openMenu('When it reads this');
    const now = screen.getByRole('menuitem', { name: /Now, cutting its step/ });
    expect(now).toHaveAttribute('aria-disabled', 'true');
    expect(now).toHaveTextContent(why);
    await userEvent.click(screen.getByRole('menuitem', { name: /At its next step/ }));
    expect(send).toHaveBeenCalledWith('stop: the tests are in /spec', [], 'urgent');
  });

  /** COWORK1a (the design §8.1, trap 23): a door that carries only text has no turns, so no level is offered, and its press says why. */
  it('offers no level on a door that carries only text, and its press says why', async () => {
    show({ taking: false, context: { door: 'text' } });

    expect(screen.queryByRole('button', { name: 'When it reads this' })).toBeNull();
    await userEvent.type(box(), 'hello');
    await userEvent.hover(screen.getByRole('button', { name: 'Send' }));
    expect(await screen.findByRole('tooltip')).toHaveTextContent(
      "This agent's door carries only text, so your words go at once, whatever their level.");
  });

  /**
   * COWORK1a (the design §3.2): where words wait for the turn's end and the door can stop a turn keeping the session, the
   * keeping stop stands beside them: it asks once, naming the step it cuts, and only the second press stops the turn. Not on a
   * door that takes words at its next step, where an urgent word reaches sooner, nor where nothing waits.
   */
  it('offers the keeping stop beside words held for the turn’s end, and stops only on the second press', async () => {
    const keep = vi.fn((answered: { done: () => void }) => answered.done());
    const queued = [{ id: 'a', text: 'start with the tests', files: [], level: 'now' as const }];
    const { unmount } = show({ taking: true, levels: { nextStep: false, steers: false }, queued, onKeepingStop: keep });

    expect(within(screen.getByRole('list', { name: 'waiting for this turn to end' })).getByText('now')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Stop its turn so this goes next…' }));
    expect(keep).not.toHaveBeenCalled();
    expect(screen.getByText('This stops the step it is on and its turn. What it has done stays, and what waits goes next.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Stop its turn' }));
    expect(keep).toHaveBeenCalledOnce();
    unmount();

    const stepping = show({ taking: true, levels: { nextStep: true, steers: true }, queued, onKeepingStop: keep });
    expect(screen.queryByRole('button', { name: 'Stop its turn so this goes next…' })).toBeNull();
    stepping.unmount();
    show({ taking: true, levels: { nextStep: false, steers: false }, queued: [], onKeepingStop: keep });
    expect(screen.queryByRole('button', { name: 'Stop its turn so this goes next…' })).toBeNull();
  });

  /**
   * COWORK1a (the design §8.2): each waiting word of the person's has its level's menu: *Make urgent*, *Make ordinary*, and
   * *Send now* where the agent steers, each changing the word by its id while it waits.
   */
  it('changes a waiting word’s level from its own menu', async () => {
    const level = vi.fn().mockResolvedValue({ done: true });
    show({
      taking: true, levels: { nextStep: true, steers: true }, onLevel: level, onRemove: vi.fn(), onEdit: vi.fn(),
      queued: [{ id: 'a1', text: 'tidy the readme after', files: [], level: 'ordinary' }],
    });

    await openMenu('When it reads: tidy the readme after');
    expect(screen.queryByRole('menuitem', { name: 'Make ordinary' })).toBeNull();
    await userEvent.click(screen.getByRole('menuitem', { name: 'Make urgent' }));
    expect(level).toHaveBeenLastCalledWith('a1', 'urgent');

    await openMenu('When it reads: tidy the readme after');
    await userEvent.click(screen.getByRole('menuitem', { name: 'Send now' }));
    expect(level).toHaveBeenLastCalledWith('a1', 'now');
  });

  it('speaks the levels in Chinese', async () => {
    await i18n.changeLanguage('zh');
    try {
      show({ taking: true, levels: { nextStep: true, steers: true } });
      await userEvent.type(screen.getByRole('textbox'), '预算是 64 KiB');
      await openMenu('何时读到');
      expect(screen.getByRole('menuitem', { name: /本轮结束时/ })).toBeInTheDocument();
      expect(screen.getByRole('menuitem', { name: /下一步时/ })).toBeInTheDocument();
      expect(screen.getByRole('menuitem', { name: /立即，打断当前这一步/ })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /**
   * HELP4: words waiting while no turn runs are waiting for the conversation to open, not for a turn:
   * the label says which. Seen on the window, where a new Ask Daoris conversation's first words said
   * they were waiting for a turn that did not exist yet.
   */
  it('says words wait for the conversation to open when no turn runs', () => {
    show({ taking: false, queued: [{ text: 'tidy the branches', files: [] }] });

    expect(screen.getByRole('list', { name: 'waiting for the conversation to open' })).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'waiting for this turn to end' })).not.toBeInTheDocument();
  });

  /**
   * HELP4: the driver counts words held for a door still opening as a turn on its way (`taking`), and
   * says `opening` beside it — so the label follows `opening`, not `taking`, until the door is open.
   */
  it('says words wait for the conversation to open while the driver says it is opening', () => {
    const opening = show({ taking: true, opening: true, queued: [{ text: 'tidy the branches', files: [] }] });

    expect(screen.getByRole('list', { name: 'waiting for the conversation to open' })).toBeInTheDocument();
    opening.unmount();
    show({ taking: true, opening: false, queued: [{ text: 'tidy the branches', files: [] }] });
    expect(screen.getByRole('list', { name: 'waiting for this turn to end' })).toBeInTheDocument();
  });

  /**
   * CONV4b: a message sent while a turn runs waits for it (CONV4a), so the button says so, and what is
   * waiting is shown, in the order sent — it is in no record until it goes.
   */
  it('says a message sent now will wait, and shows what is waiting in order', async () => {
    const send = vi.fn();
    show({
      taking: true, stoppable: true, onSend: send,
      queued: [{ text: 'and test it', files: [] }, { text: 'then commit', files: ['plan.md'] }],
    });

    const waiting = screen.getByRole('list', { name: 'waiting for this turn to end' });
    const items = within(waiting).getAllByRole('listitem');
    expect(items.map((item) => item.textContent)).toEqual(['and test it', 'then commitplan.md']);
    // A waiting message's files are named with it (CONV4c).
    expect(within(items[1]!).getByText('plan.md')).toBeInTheDocument();

    await userEvent.type(box(), 'and push nothing');
    await userEvent.click(screen.getByRole('button', { name: 'Queue' }));
    expect(send).toHaveBeenCalledWith('and push nothing', []);
  });

  /**
   * SENDLOST1b: words on their way show the moment they are sent, as sending, apart from what waits behind a turn. Where the
   * page stopped waiting on their send they say the machine has not answered yet, and offer no control: an edit or a removal
   * of words the machine may still take would send them twice.
   */
  it('shows words on their way as sending, then as unanswered, apart from what waits and with no controls', () => {
    const props = {
      taking: true, onRemove: vi.fn(), onEdit: vi.fn(),
      queued: [{ id: 'q1', text: 'and test it', files: [] }],
    };
    const sending = show({ ...props, going: { words: [{ text: 'and the line?', files: ['run.log'] }], late: false } });

    const going = screen.getByRole('list', { name: 'sending…' });
    expect(going.textContent).toBe('and the line?run.log');
    expect(within(going).queryByRole('button')).toBeNull();
    expect(screen.getByRole('list', { name: 'waiting for this turn to end' }).textContent).toContain('and test it');
    sending.unmount();

    show({ ...props, going: { words: [{ text: 'and the line?', files: [] }], late: true } });
    const late = screen.getByRole('list', { name: 'The machine has not answered yet; it may still take them.' });
    expect(late.textContent).toBe('and the line?');
    expect(within(late).queryByRole('button')).toBeNull();
    expect(screen.queryByRole('list', { name: 'sending…' })).toBeNull();
  });

  /**
   * ASKHIST1b: words waiting with a word wider than the box, a pasted URL as Ask Daoris's first words, stay inside it. Every
   * grid from them to the form bounds its column, or its widest word sets the width of the box and its presses too.
   */
  it('keeps waiting words with a word wider than the box inside it, broken at its edge', () => {
    const words = 'to complete this https://example.atlassian.net/browse/TK-2205?focusedCommentId=1234567&page=com.example.plugin.tabpanels';
    const { container } = show({ taking: false, queued: [{ text: words, files: [] }] });

    const waiting = screen.getByText(words);
    expect(waiting).toHaveClass('line-clamp-2', 'wrap-anywhere');
    const form = container.querySelector('form')!;
    for (let at: HTMLElement | null = waiting; at && at !== form.parentElement; at = at.parentElement) {
      if (at.classList.contains('grid')) expect(at).toHaveClass('grid-cols-[minmax(0,1fr)]');
    }
  });

  /**
   * CONV4c: files go with the message. Chosen, dropped or pasted, they wait above the box as chips a
   * person can take back off, travel with the next send, and are let go of once sent. A message may be
   * files alone.
   */
  it('attaches files, sends them with the words, and lets them go once sent', async () => {
    const send = vi.fn();
    show({ onSend: send });
    const log = new File(['exit 3'], 'run.log', { type: 'text/plain' });
    const shot = new File(['png'], 'shot.png', { type: 'image/png' });

    await userEvent.upload(screen.getByLabelText('Choose files…'), [log, shot]);
    const attached = screen.getByRole('list', { name: 'attached' });
    expect(within(attached).getAllByRole('listitem').map((item) => item.textContent)).toEqual(
      [expect.stringContaining('run.log'), expect.stringContaining('shot.png')]);

    await userEvent.click(screen.getByRole('button', { name: 'Remove shot.png' }));
    await userEvent.type(box(), 'what does this say?');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(send).toHaveBeenCalledWith('what does this say?', [log]);
    expect(screen.queryByRole('list', { name: 'attached' })).not.toBeInTheDocument();
  });

  it('sends files with no words', async () => {
    const send = vi.fn();
    show({ onSend: send });
    const shot = new File(['png'], 'shot.png', { type: 'image/png' });

    expect(screen.getByRole('button', { name: 'Send' })).toBeDisabled();
    await userEvent.upload(screen.getByLabelText('Choose files…'), shot);
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(send).toHaveBeenCalledWith('', [shot]);
  });

  it('says what a drop left off when it is past what a message carries', async () => {
    show();
    const files = Array.from({ length: 11 }, (_, i) => new File([`${i}`], `f${i}.txt`));

    await userEvent.upload(screen.getByLabelText('Choose files…'), files);

    expect(within(screen.getByRole('list', { name: 'attached' })).getAllByRole('listitem')).toHaveLength(10);
    expect(screen.getByRole('status')).toHaveTextContent('At most 10 files travel together.');
  });

  it('shows nothing waiting when nothing is', () => {
    show({ taking: true, stoppable: true, queued: [] });
    expect(screen.queryByRole('list', { name: 'waiting for this turn to end' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Queue' })).toBeInTheDocument();
  });

  /**
   * CONV4b: the draft can be held above the composer — the frame keeps one per session — and then
   * every keystroke is told, and sending empties it through the same door.
   */
  it('shows the draft it is given, and tells every change of it', async () => {
    const draft = vi.fn();
    const send = vi.fn();
    show({ draft: 'half a thought', onDraft: draft, onSend: send });

    expect(box()).toHaveValue('half a thought');
    await userEvent.type(box(), '!');
    expect(draft).toHaveBeenLastCalledWith('half a thought!');

    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    expect(send).toHaveBeenCalledWith('half a thought', []);
    expect(draft).toHaveBeenLastCalledWith('');
  });

  /**
   * ASKHIST1c: the files can be held above the composer too, so Ask Daoris keeps each conversation's attachments across its
   * history: shown as handed, every change told, and let go through the same door once sent.
   */
  it('shows the files it is given, and tells every change of them', async () => {
    const held = new File(['exit 3'], 'run.log', { type: 'text/plain' });
    const shot = new File(['png'], 'shot.png', { type: 'image/png' });
    const carry = vi.fn();
    const send = vi.fn();
    show({ carried: { links: '', files: [held] }, onCarry: carry, onSend: send });

    expect(within(screen.getByRole('list', { name: 'attached' })).getByText('run.log')).toBeInTheDocument();
    await userEvent.upload(screen.getByLabelText('Choose files…'), shot);
    expect(carry).toHaveBeenLastCalledWith({ links: '', files: [held, shot] });

    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    expect(send).toHaveBeenCalledWith('', [held]);
    expect(carry).toHaveBeenLastCalledWith({ links: '', files: [] });
  });

  it('shows a refusal word for word', () => {
    show({ refusal: '`engine` already has an active session — `s1a2b3c4`.' });
    expect(screen.getByText('`engine` already has an active session — `s1a2b3c4`.')).toBeInTheDocument();
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    show();
    expect(screen.getByRole('button', { name: '发送' })).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });

  /** CONV5: the context ring sits under the box, where the reference keeps it — and only where it is handed one. */
  it('carries the context ring it is handed, and none when it is not', () => {
    const { unmount } = show({ context: { usage: { used: 34_120, size: 1_000_000, most: 34_120 }, door: 'structured' } });
    expect(screen.getByRole('meter', { name: 'context' })).toHaveTextContent('3%');
    unmount();

    show();
    expect(screen.queryByRole('meter')).not.toBeInTheDocument();
    expect(screen.queryByRole('img', { name: 'context: not measured' })).not.toBeInTheDocument();
  });
});

/**
 * CONV4d: `@` a file in the session's tree. The mention is text and both doors expand it, so the
 * composer only helps write it — the files come from the frame, which asks for them only while one is
 * being written.
 */
describe('a mention', () => {
  const tree = { files: ['README.md', 'docs/design.md', 'docs/deep file.md', 'src/main.ts'], unlisted: 0, refusal: null };
  const files = () => screen.getByRole('listbox', { name: "files in this session's tree" });

  /**
   * UX5 U64, seen on the window: the list ran the composer's whole width, about 1,900px on a
   * maximized window, for rows about 450px wide. It is sized to its rows, within bounds.
   */
  it('sizes its list to its rows, not to the box it completes', async () => {
    show({ mentions: tree });
    await userEvent.type(box(), 'read @de');

    const panel = files().parentElement!;
    expect(panel.className).not.toContain('inset-x-0');
    expect(panel.className).toContain('w-max');
    expect(panel.className).toMatch(/max-w-/);
  });

  it('offers the tree\'s files after @, and Enter writes the one chosen instead of sending', async () => {
    const send = vi.fn();
    show({ onSend: send, mentions: tree });

    await userEvent.type(box(), 'read @de');
    expect(within(files()).getAllByRole('option').map((option) => option.getAttribute('aria-label')))
      .toEqual(['docs/design.md', 'docs/deep file.md', 'README.md']);
    // The box points at the one Enter would take, so a screen reader follows the arrows.
    expect(box()).toHaveAttribute('aria-activedescendant', screen.getByRole('option', { name: 'docs/design.md' }).id);

    await userEvent.keyboard('{Enter}');
    expect(send).not.toHaveBeenCalled();
    expect(box()).toHaveValue('read @docs/design.md ');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();

    await userEvent.type(box(), 'please{Enter}');
    expect(send).toHaveBeenCalledWith('read @docs/design.md please', []);
  });

  /**
   * Seen on the window (CONV4d): React reads the selection on the same keydown that takes a file, and
   * that reading is the box BEFORE the file is in it. Taken as the caret, it put the caret back inside
   * the mention, and the list opened again on the half-word it had just replaced.
   */
  it('closes once a file is taken, whatever the selection said on the way', () => {
    show({ mentions: tree });
    const field = box() as HTMLTextAreaElement;
    field.focus();
    fireEvent.change(field, { target: { value: 'read @des' } });
    fireEvent.keyDown(field, { key: 'Enter' });

    expect(field).toHaveValue('read @docs/design.md ');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  /** Taking the file already written in full changes no text — and must still close, and let go of the caret. */
  it('takes a file already written in full, and keeps following the caret after', async () => {
    show({ mentions: tree });

    await userEvent.type(box(), 'read @docs/design.md please');
    await userEvent.keyboard('{ArrowLeft>7/}');
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    await userEvent.keyboard('{Enter}');

    expect(box()).toHaveValue('read @docs/design.md please');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    await userEvent.keyboard('{End} @ma');
    expect(box()).toHaveValue('read @docs/design.md please @ma');
    expect(screen.getByRole('option', { name: 'src/main.ts' })).toBeInTheDocument();
  });

  it('moves with the arrows, takes Tab as well, and quotes a path with a space', async () => {
    show({ mentions: tree });

    await userEvent.type(box(), '@de');
    await userEvent.keyboard('{ArrowDown}');
    expect(screen.getByRole('option', { name: 'docs/deep file.md' })).toHaveAttribute('aria-selected', 'true');
    await userEvent.keyboard('{Tab}');

    // Both doors expand the quoted spelling and neither the escaped one (measured, CONV4d).
    expect(box()).toHaveValue('@"docs/deep file.md" ');
  });

  it('takes a click without losing the box', async () => {
    show({ mentions: tree });

    await userEvent.type(box(), 'see @ma');
    await userEvent.click(screen.getByRole('option', { name: 'src/main.ts' }));

    expect(box()).toHaveValue('see @src/main.ts ');
    expect(box()).toHaveFocus();
  });

  // IME1: while an input method composes, its Enter, Tab, arrows and Escape are its own, and the list keeps still.
  it('leaves the list alone while an input method composes: no pick, no move, no close', () => {
    const send = vi.fn();
    show({ onSend: send, mentions: tree });
    const field = box() as HTMLTextAreaElement;
    field.focus();
    fireEvent.change(field, { target: { value: 'read @de' } });
    expect(screen.getByRole('option', { name: 'docs/design.md' })).toHaveAttribute('aria-selected', 'true');

    expect(fireEvent.keyDown(field, { key: 'ArrowDown', isComposing: true })).toBe(true);
    expect(screen.getByRole('option', { name: 'docs/design.md' })).toHaveAttribute('aria-selected', 'true');
    expect(fireEvent.keyDown(field, { key: 'Enter', isComposing: true })).toBe(true);
    expect(fireEvent.keyDown(field, { key: 'Tab', keyCode: 229 })).toBe(true);
    expect(fireEvent.keyDown(field, { key: 'Escape', isComposing: true })).toBe(true);
    expect(field).toHaveValue('read @de');
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    expect(send).not.toHaveBeenCalled();

    // Composed, the same Enter is the list's again.
    fireEvent.keyDown(field, { key: 'Enter' });
    expect(field).toHaveValue('read @docs/design.md ');
  });

  it('closes on Escape, and Enter then sends what was typed', async () => {
    const send = vi.fn();
    show({ onSend: send, mentions: tree });

    await userEvent.type(box(), 'mail @me');
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();

    await userEvent.keyboard('{Enter}');
    expect(send).toHaveBeenCalledWith('mail @me', []);
  });

  it('tells the frame while one is being written, so the files are asked for only then', async () => {
    const mentioning = vi.fn();
    show({ mentions: tree, onMentioning: mentioning });

    await userEvent.type(box(), 'no mention yet');
    expect(mentioning).not.toHaveBeenCalledWith(true);

    await userEvent.type(box(), ' @');
    expect(mentioning).toHaveBeenLastCalledWith(true);
    await userEvent.type(box(), 'x ');
    expect(mentioning).toHaveBeenLastCalledWith(false);
  });

  it('says what it is doing when it has no files to offer', async () => {
    const { rerender } = show({ mentions: { files: null, unlisted: 0, refusal: null } });
    await userEvent.type(box(), '@eng');
    expect(screen.getByText("listing this tree's files…")).toBeInTheDocument();

    const again = (mentions: Parameters<typeof Composer>[0]['mentions']) => rerender(
      <Tooltip.Provider>
        <Composer live onSend={() => {}} onFinish={() => {}} onStop={() => {}} mentions={mentions} />
      </Tooltip.Provider>,
    );

    again({ files: ['README.md'], unlisted: 0, refusal: null });
    expect(screen.getByText('no file in this tree matches “eng”.')).toBeInTheDocument();

    // The host's sentence, verbatim: the typed path still reaches the agent.
    again({ files: null, unlisted: 0, refusal: 'git cannot list this session’s files here.' });
    expect(screen.getByText('git cannot list this session’s files here.')).toBeInTheDocument();

    again({ files: ['engine.cs'], unlisted: 3, refusal: null });
    expect(screen.getByRole('option', { name: 'engine.cs' })).toBeInTheDocument();
    expect(screen.getByText(/3 more files are not offered here/)).toBeInTheDocument();
  });

  it('is only text where nothing offers files, and never opens on an address', async () => {
    const { unmount } = show();
    await userEvent.type(box(), '@readme');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    unmount();

    show({ mentions: tree });
    await userEvent.type(box(), 'mail me@README');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  // AGT6b (D98): one conversation's model and effort, as its agent offered them on the protocol door.
  // The names are the agent's own words; the control only says whose they are.
  const OPTIONS = [
    { id: 'model', name: 'Model', category: 'model', current: 'default',
      choices: [{ value: 'default', name: 'Default (recommended)' }, { value: 'sonnet', name: 'Sonnet' }] },
    { id: 'effort', name: 'Effort', category: 'thought_level', current: 'high',
      choices: [{ value: 'low', name: 'Low' }, { value: 'high', name: 'High' }, { value: 'max', name: 'Max' }] },
  ];

  it("offers the conversation's model and effort as its agent names them, and says which was chosen", async () => {
    const onOption = vi.fn();
    show({ offered: OPTIONS, onOption });

    expect(screen.getByRole('combobox', { name: "this conversation's Model" })).toHaveTextContent('Default (recommended)');
    expect(screen.getByRole('combobox', { name: "this conversation's Effort" })).toHaveTextContent('High');

    screen.getByRole('combobox', { name: "this conversation's Model" }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'Sonnet' }));

    expect(onOption).toHaveBeenCalledWith('model', 'sonnet');
  });

  it('offers nothing where the agent offered nothing, or where nothing listens', () => {
    const { unmount } = show({ offered: [], onOption: () => {} });
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    unmount();

    show({ live: false, offered: OPTIONS, onOption: () => {} });
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
  });
});

/** QUEUEEDIT1b: a waiting message can be taken back out, or reworded in place, while it still waits. */
describe('the composer, waiting messages with an id', () => {
  const WAITING = [{ id: 'a1', text: 'and test it', files: [] }, { id: 'b2', text: 'then commit', files: ['plan.md'] }];

  it('removes one by its id, and shows controls only on a message that has one', async () => {
    const remove = vi.fn().mockResolvedValue({ done: true });
    show({
      taking: true, onRemove: remove, onEdit: vi.fn(),
      queued: [WAITING[0]!, { text: 'no id here', files: [] }],
    });

    expect(screen.getAllByRole('button', { name: /^Remove the waiting message/ })).toHaveLength(1);
    await userEvent.click(screen.getByRole('button', { name: 'Remove the waiting message: and test it' }));
    expect(remove).toHaveBeenCalledWith('a1');
  });

  it('puts its words back in the box, rewords it on send, and leaves the draft as it was', async () => {
    const edit = vi.fn().mockResolvedValue({ done: true });
    const send = vi.fn();
    show({ taking: true, onRemove: vi.fn(), onEdit: edit, onSend: send, queued: WAITING });

    await userEvent.type(box(), 'half a thought');
    await userEvent.click(screen.getByRole('button', { name: 'Edit the waiting message: then commit' }));
    expect(box()).toHaveValue('then commit');
    expect(screen.getByText(/Editing a waiting message/)).toBeInTheDocument();

    await userEvent.clear(box());
    await userEvent.type(box(), 'then commit and push nothing');
    await userEvent.click(screen.getByRole('button', { name: 'Queue' }));

    expect(edit).toHaveBeenCalledWith('b2', 'then commit and push nothing');
    expect(send).not.toHaveBeenCalled();
    expect(screen.queryByText(/Editing a waiting message/)).not.toBeInTheDocument();
    expect(box()).toHaveValue('half a thought');
  });

  /** REWORDBOUND1 (D137 §2.4): a reword is words the door takes too, so the box's own bound refuses it as it refuses a send. */
  it('refuses a reword the door could not take, keeping the text and the edit in the box', async () => {
    const edit = vi.fn().mockResolvedValue({ done: true });
    show({ taking: true, onRemove: vi.fn(), onEdit: edit, accepts: (text: string) => text.length <= 20, queued: WAITING });

    await userEvent.click(screen.getByRole('button', { name: 'Edit the waiting message: then commit' }));
    await userEvent.clear(box());
    await userEvent.type(box(), 'then commit and push everything');
    await userEvent.click(screen.getByRole('button', { name: 'Queue' }));

    expect(edit).not.toHaveBeenCalled();
    expect(box()).toHaveValue('then commit and push everything');
    expect(screen.getByText(/Editing a waiting message/)).toBeInTheDocument();
  });

  it('leaves the message as it was when Escape cancels the edit', async () => {
    const edit = vi.fn();
    show({ taking: true, onRemove: vi.fn(), onEdit: edit, queued: WAITING });

    await userEvent.click(screen.getByRole('button', { name: 'Edit the waiting message: and test it' }));
    await userEvent.type(box(), ' twice');
    await userEvent.keyboard('{Escape}');

    expect(edit).not.toHaveBeenCalled();
    expect(box()).toHaveValue('');
    expect(screen.queryByText(/Editing a waiting message/)).not.toBeInTheDocument();
  });

  it('says in words, not as an error, that a message was already sent', async () => {
    show({
      taking: true, onEdit: vi.fn(), queued: WAITING,
      onRemove: vi.fn().mockResolvedValue({ done: false, why: 'taken' }),
    });

    await userEvent.click(screen.getByRole('button', { name: 'Remove the waiting message: and test it' }));
    expect(await screen.findByText('Already sent: it went to the agent.')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows a driver refusal of a removal word for word', async () => {
    show({ taking: true, onEdit: vi.fn(), queued: WAITING, onRemove: vi.fn().mockRejectedValue(new Error('That session takes no input.')) });

    await userEvent.click(screen.getByRole('button', { name: 'Remove the waiting message: and test it' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('That session takes no input.');
  });

  it('draws no controls where nothing handles them', () => {
    show({ taking: true, queued: WAITING });
    expect(screen.queryByRole('button', { name: /waiting message/ })).not.toBeInTheDocument();
  });
});
