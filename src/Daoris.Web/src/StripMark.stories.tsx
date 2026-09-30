import type { Meta, StoryObj } from '@storybook/react-vite';
import { StripMark } from './ui';

// One item on a list closed to its 56px strip (D118 §5): the first character of its name and its mark,
// with the words as its name and tip. Sessions' strip is its first user and Plugins' its second.

const meta: Meta<typeof StripMark> = {
  title: 'Design/StripMark',
  component: StripMark,
  args: { label: 'Cap the hydration per frame · engine · working', initialOf: 'engine', tone: 'live' },
  // The strip's real width, and the list a mark sits in.
  decorators: [(Story) => (
    <ul className="m-0 grid w-14 list-none justify-items-center gap-1 border border-line bg-raised px-0 py-1.5">
      <Story />
    </ul>
  )],
};
export default meta;

type Story = StoryObj<typeof StripMark>;

/** Working: the live mark. */
export const Working: Story = {};

/** The one chosen in the list, with the accent rail the activity bar gives its current place. */
export const Chosen: Story = { args: { current: true } };

/** Waiting on the person: the waiting hue. */
export const Waiting: Story = {
  args: { label: 'Expose a streaming budget · engine · waiting on you', tone: 'parked' },
};

/** Idle: a conversation between turns, a quiet mark in no outcome's hue. */
export const Idle: Story = { args: { label: 'Cap the hydration per frame · engine · idle', tone: 'idle' } };

/** On, with no mark: a plugin that is simply running (D119). */
export const Unmarked: Story = { args: { label: 'lint-on-save · on', initialOf: 'lint-on-save', tone: undefined } };

/** Off: a plugin switched off, its initial faint and no mark (D119). */
export const Off: Story = { args: { label: 'lint-on-save · off', initialOf: 'lint-on-save', tone: undefined, dimmed: true } };

/** A name in 中文: its first character, whole. */
export const ChineseName: Story = { args: { label: '引擎 · 工作中', initialOf: '引擎' } };
