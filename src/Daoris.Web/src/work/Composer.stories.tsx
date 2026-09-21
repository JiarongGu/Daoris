import type { Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { Composer } from './Composer';

// The four states the inventory names, and the reason each is a state: sending is not idle, an
// ending is not a disabled box, and a refusal is a sentence rather than a shrug.

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
