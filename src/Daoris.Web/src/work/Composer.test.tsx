import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { Composer } from './Composer';

const show = (props: Partial<Parameters<typeof Composer>[0]> = {}) => render(
  <Tooltip.Provider>
    <Composer live onSend={() => {}} onFinish={() => {}} onStop={() => {}} {...props} />
  </Tooltip.Provider>,
);

const box = () => screen.getByLabelText('message');

describe('the composer', () => {
  it('sends what was typed and clears the box', async () => {
    const send = vi.fn();
    show({ onSend: send });

    await userEvent.type(box(), 'cap hydration per frame');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));

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

  it('will not send nothing, nor send twice while one is in flight', async () => {
    const send = vi.fn();
    const { rerender } = show({ onSend: send });

    await userEvent.click(screen.getByRole('button', { name: 'send' }));
    expect(send).not.toHaveBeenCalled();

    await userEvent.type(box(), 'something');
    rerender(
      <Tooltip.Provider>
        <Composer live sending onSend={send} onFinish={() => {}} onStop={() => {}} />
      </Tooltip.Provider>,
    );
    expect(screen.getByRole('button', { name: 'send' })).toBeDisabled();
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

    await userEvent.click(screen.getByRole('button', { name: 'finish' }));
    await userEvent.click(screen.getByRole('button', { name: 'stop' }));
    expect(finish).toHaveBeenCalledOnce();
    expect(stop).toHaveBeenCalledOnce();

    rerender(
      <Tooltip.Provider>
        <Composer live={false} onSend={() => {}} onFinish={finish} onStop={stop} />
      </Tooltip.Provider>,
    );
    expect(screen.queryByRole('button', { name: 'finish' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'stop' })).not.toBeInTheDocument();
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
    expect(screen.queryByRole('button', { name: 'send' })).toBeNull();
  });

  /**
   * UX5 U9, seen on the window: an ended session opened from the rail showed an empty box and a send
   * under "What you typed is still here", when nothing was typed. A box where nothing listens is the
   * one INT4h removed from the intake; here it is removed wherever nothing was written.
   */
  it('offers no box to an ended session nobody was writing in, and keeps the meter', () => {
    show({ live: false, context: { door: 'structured' } });

    expect(screen.queryByLabelText('message')).toBeNull();
    expect(screen.queryByRole('button', { name: 'send' })).toBeNull();
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

    await userEvent.click(screen.getByRole('button', { name: 'stop turn' }));
    expect(stopTurn).toHaveBeenCalledOnce();
    // Beside the endings, never instead of them.
    expect(screen.getByRole('button', { name: 'finish' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'stop' })).toBeInTheDocument();

    const again = (props: Partial<Parameters<typeof Composer>[0]>) => rerender(
      <Tooltip.Provider>
        <Composer live onSend={() => {}} onFinish={() => {}} onStop={() => {}} onStopTurn={stopTurn} {...props} />
      </Tooltip.Provider>,
    );
    again({ taking: true, stoppable: true, stopping: true });
    expect(screen.getByRole('button', { name: 'stop turn' })).toBeDisabled();
    again({ taking: true, stoppable: false });
    expect(screen.queryByRole('button', { name: 'stop turn' })).not.toBeInTheDocument();
    again({ taking: false, stoppable: true });
    expect(screen.queryByRole('button', { name: 'stop turn' })).not.toBeInTheDocument();
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
    await userEvent.click(screen.getByRole('button', { name: 'queue' }));
    expect(send).toHaveBeenCalledWith('and push nothing', []);
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

    await userEvent.upload(screen.getByLabelText('choose files…'), [log, shot]);
    const attached = screen.getByRole('list', { name: 'attached' });
    expect(within(attached).getAllByRole('listitem').map((item) => item.textContent)).toEqual(
      [expect.stringContaining('run.log'), expect.stringContaining('shot.png')]);

    await userEvent.click(screen.getByRole('button', { name: 'remove shot.png' }));
    await userEvent.type(box(), 'what does this say?');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));

    expect(send).toHaveBeenCalledWith('what does this say?', [log]);
    expect(screen.queryByRole('list', { name: 'attached' })).not.toBeInTheDocument();
  });

  it('sends files with no words', async () => {
    const send = vi.fn();
    show({ onSend: send });
    const shot = new File(['png'], 'shot.png', { type: 'image/png' });

    expect(screen.getByRole('button', { name: 'send' })).toBeDisabled();
    await userEvent.upload(screen.getByLabelText('choose files…'), shot);
    await userEvent.click(screen.getByRole('button', { name: 'send' }));

    expect(send).toHaveBeenCalledWith('', [shot]);
  });

  it('says what a drop left off when it is past what a message carries', async () => {
    show();
    const files = Array.from({ length: 11 }, (_, i) => new File([`${i}`], `f${i}.txt`));

    await userEvent.upload(screen.getByLabelText('choose files…'), files);

    expect(within(screen.getByRole('list', { name: 'attached' })).getAllByRole('listitem')).toHaveLength(10);
    expect(screen.getByRole('status')).toHaveTextContent('At most 10 files travel together.');
  });

  it('shows nothing waiting when nothing is', () => {
    show({ taking: true, stoppable: true, queued: [] });
    expect(screen.queryByRole('list', { name: 'waiting for this turn to end' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'queue' })).toBeInTheDocument();
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

    await userEvent.click(screen.getByRole('button', { name: 'send' }));
    expect(send).toHaveBeenCalledWith('half a thought', []);
    expect(draft).toHaveBeenLastCalledWith('');
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
});
