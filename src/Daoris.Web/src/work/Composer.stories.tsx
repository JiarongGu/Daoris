import type { Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { Composer } from './Composer';

// The states the inventory names, and the reason each is a state: sending is not idle, an ending is
// not a disabled box, a refusal is a sentence rather than a shrug, and a running turn is not idle
// either — what waits behind it and its own stop are shown (CONV4b).

const meta: Meta<typeof Composer> = {
  title: 'Work/Composer',
  component: Composer,
  args: { live: true, onSend: () => {}, onFinish: () => {}, onStop: () => {} },
  decorators: [(Story) => (
    <Tooltip.Provider><div className="max-w-2xl border border-line"><Story /></div></Tooltip.Provider>
  )],
};
export default meta;

type Story = StoryObj<typeof Composer>;

/** Listening. Two endings side by side, and they are never one button. */
export const Idle: Story = {};

/** A message in flight: send is held, the endings are not. */
export const Sending: Story = { args: { sending: true } };

/**
 * The session ended while somebody was typing. What they wrote stays — a box that swallowed a
 * paragraph they were halfway through gives them no way to get it back.
 */
export const Ended: Story = { args: { live: false } };

/** The ledger's own sentence, verbatim, where the person is looking rather than in a toast. */
export const Refused: Story = {
  args: { refusal: 'Nothing is listening: this session ended while you were typing.' },
};

/**
 * A turn is running (CONV4b): send says *queue*, what waits behind the turn is shown in the order
 * sent, and the turn's own stop stands beside the two endings.
 */
export const TurnRunning: Story = {
  args: {
    taking: true, stoppable: true, onStopTurn: () => {},
    queued: ['and then run the streaming tests', 'if they pass, commit it — do not push'],
  },
};

/** The stop was asked for; the button holds until the driver answers. */
export const StoppingTheTurn: Story = { args: { taking: true, stoppable: true, stopping: true, onStopTurn: () => {} } };

/** A door that carries only text cannot see a turn end, so it offers no stop — never one that is refused. */
export const TurnRunningOnATextDoor: Story = { args: { taking: true, stoppable: false, onStopTurn: () => {} } };
