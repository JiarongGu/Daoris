import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
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
    expect(send).toHaveBeenCalledWith('cap hydration per frame');
    expect(box()).toHaveValue('');
  });

  it('sends on Enter and takes a newline on Shift+Enter — a pasted paragraph stays one message', async () => {
    const send = vi.fn();
    show({ onSend: send });

    await userEvent.type(box(), 'first{Shift>}{Enter}{/Shift}second');
    expect(send).not.toHaveBeenCalled();

    await userEvent.type(box(), '{Enter}');
    expect(send).toHaveBeenCalledWith('first\nsecond');
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
    expect(box()).toBeDisabled();
    expect(screen.getByText(/This session is over/)).toBeInTheDocument();
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
    show({ taking: true, stoppable: true, queued: ['and test it', 'then commit'], onSend: send });

    const waiting = screen.getByRole('list', { name: 'waiting for this turn to end' });
    expect(within(waiting).getAllByRole('listitem').map((item) => item.textContent)).toEqual(['and test it', 'then commit']);

    await userEvent.type(box(), 'and push nothing');
    await userEvent.click(screen.getByRole('button', { name: 'queue' }));
    expect(send).toHaveBeenCalledWith('and push nothing');
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
    expect(send).toHaveBeenCalledWith('half a thought');
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
});
