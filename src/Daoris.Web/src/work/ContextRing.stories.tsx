import type { Meta, StoryObj } from '@storybook/react-vite';
import { ContextRing } from './ContextRing';

// How full a session's context is (CONV5), in each state the ring can be in. Absent is never zero:
// the three "not measured" states each say which absence they are.

const meta: Meta<typeof ContextRing> = {
  title: 'Work/ContextRing',
  component: ContextRing,
  args: { door: 'structured' },
  decorators: [(Story) => <div className="flex h-24 items-end p-4"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof ContextRing>;

/** A session a few turns in, against a million-token window. */
export const Measured: Story = { args: { usage: { used: 34_120, size: 1_000_000, most: 41_000 } } };

/** Barely started: under one percent is said as that, never as nothing. */
export const UnderOnePercent: Story = { args: { usage: { used: 4_000, size: 1_000_000, most: 4_000 } } };

/** Filling: from 80% the ring takes the warn tone, since the harness compacts or refuses near the top. */
export const Filling: Story = { args: { usage: { used: 170_000, size: 200_000, most: 170_000 } } };

/** The harness compacted: the latest reading fell, and the tip still says how close it came. */
export const AfterCompacting: Story = { args: { usage: { used: 22_000, size: 200_000, most: 188_000 } } };

/** A structured door that has not reported yet. */
export const NotReportedYet: Story = {};

/** A door that carries only text, and will never report. */
export const TextDoor: Story = { args: { door: 'text' } };

/** The roster has not said which door this is. */
export const DoorUnknown: Story = { args: { door: undefined } };
