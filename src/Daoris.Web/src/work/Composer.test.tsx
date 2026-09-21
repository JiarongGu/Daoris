import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
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
