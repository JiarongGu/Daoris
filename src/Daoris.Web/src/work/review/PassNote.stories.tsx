import type { Meta, StoryObj } from '@storybook/react-vite';
import { PassNote } from './PassNote';

// The cause and the bound of an Accept's wait (LANDHOLD2b), on the shipped component: the pass, never a tree's hold.

const meta: Meta<typeof PassNote> = {
  title: 'Work/PassNote',
  component: PassNote,
  decorators: [(Story) => <div className="w-[26rem] border border-line bg-raised p-2"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof PassNote>;

/** The driver's bound: twenty seconds for the pass to land the work. */
export const AtItsBound: Story = {};

/** A shorter bound, as the sentence reads for another count of seconds. */
export const ShorterBound: Story = { args: { seconds: 5 } };
