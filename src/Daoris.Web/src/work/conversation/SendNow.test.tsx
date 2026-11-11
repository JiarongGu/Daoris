import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { Composer } from './Composer';
import { ConversationView } from './ConversationView';
import { type SessionEvent, toTurns } from './conversation';
import { levelsOf } from '../say';

// *Send now* beside the default send while a turn runs: steered into the turn where the agent steers, the turn stopped and
// the words its next where the door stops a turn keeping the conversation, and absent where the door does neither.

const show = (props: Partial<Parameters<typeof Composer>[0]> = {}) => render(
  <Tooltip.Provider>
    <Composer live taking onSend={() => {}} onFinish={() => {}} onStop={() => {}} {...props} />
  </Tooltip.Provider>,
);

describe('Send now', () => {
  it('is offered beside Send on a door that stops a turn keeping it, and sends the words at now', async () => {
    const send = vi.fn();
    show({ onSend: send, levels: { nextStep: false, steers: false, stops: true } });
    await userEvent.type(screen.getByLabelText('Message'), 'stop: the tests are in /spec');
    await userEvent.click(screen.getByRole('button', { name: 'Send now' }));
    expect(send).toHaveBeenLastCalledWith('stop: the tests are in /spec', [], 'now');
    expect(screen.getByLabelText('Message')).toHaveValue('');
  });

  it('is offered where the agent steers, and not where the door can do neither', () => {
    const { unmount } = show({ levels: { nextStep: true, steers: true, stops: false } });
    expect(screen.getByRole('button', { name: 'Send now' })).toBeInTheDocument();
    unmount();
    show({ levels: { nextStep: false, steers: false, stops: false } });
    expect(screen.queryByRole('button', { name: 'Send now' })).toBeNull();
  });

  it('reads a turn-end door that stops a turn keeping it, and never one that steers, as stopping', () => {
    expect(levelsOf({ reaches: 'turn-end', why: null, steers: false, keepingStop: true })).toEqual({ nextStep: false, steers: false, stops: true });
    expect(levelsOf({ reaches: 'next-step', why: null, steers: true, keepingStop: true })).toEqual({ nextStep: true, steers: true, stops: false });
    expect(levelsOf({ reaches: 'turn-end', why: null, steers: false })).toEqual({ nextStep: false, steers: false, stops: false });
  });
});

describe('words sent now that began the next turn', () => {
  const at = '2026-11-12T00:00:00Z';
  const events: SessionEvent[] = [
    { seq: 1, at, kind: 'user', origin: 'person', text: 'read the five files' },
    { seq: 2, at, kind: 'message', text: 'reading' },
    { seq: 3, at, kind: 'turn', text: 'cancelled' },
    { seq: 4, at, kind: 'user', origin: 'person', text: 'stop: start with the tests', level: 'now' },
  ];

  it('are the ask of the turn they began, said so', () => {
    const { turns } = toTurns(events);
    expect(turns[1].ask).toMatchObject({ text: 'stop: start with the tests', cutIn: true });
    expect(turns[0].ask?.cutIn).toBeUndefined();
  });

  it('show the line beside the words', () => {
    render(<Tooltip.Provider><ConversationView turns={toTurns(events).turns} /></Tooltip.Provider>);
    expect(screen.getByText('Sent now: its turn was stopped for this, which began the next.')).toBeInTheDocument();
  });

  it('show a word waiting to cut in as the next turn, not as held for the turn\'s end', () => {
    const held: SessionEvent[] = [
      events[0],
      { seq: 2, at, kind: 'user', origin: 'person', id: 'said-1', text: 'stop: start with the tests', reaches: 'next-turn', level: 'now' },
    ];
    render(<Tooltip.Provider><ConversationView turns={toTurns(held).turns} /></Tooltip.Provider>);
    expect(screen.getByText('Sent now: its turn is being stopped, and this goes as the next one.')).toBeInTheDocument();
  });
});
