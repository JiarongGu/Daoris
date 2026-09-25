import type { Meta, StoryObj } from '@storybook/react-vite';
import { RightDock } from './RightDock';

// The dock in each of its modes (FRAME6, components plan §3a): beside the session, at its floor asking
// to be closed, over the whole frame, and closed to the strip that opens it again. The frame decides
// which from the window and the person's choices (`layout.ts`); here each is passed.

const surface = (
  <div className="grid gap-2 p-3 text-small text-ink-soft">
    <p className="m-0">session opened · 12m ago</p>
    <p className="m-0">reached working · 11m ago</p>
    <p className="m-0">commit a1b2c3d landed · 2m ago</p>
  </div>
);

const meta: Meta<typeof RightDock> = {
  title: 'Work/RightDock',
  component: RightDock,
  args: {
    tab: 'timeline', mode: 'docked', width: 420, range: { min: 300, max: 700 },
    onTab: () => {}, onResize: () => {}, onResetWidth: () => {}, onClose: () => {}, onOpen: () => {}, onFull: () => {},
    children: surface,
  },
  decorators: [(Story) => (
    <div className="relative flex h-96 border border-line">
      <div className="flex-1 p-3 text-small text-ink-faint">the attended session</div>
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof RightDock>;

/** Beside the session, resized by its left edge. */
export const Docked: Story = {};

/** At its floor with the session already squeezed: it asks to be closed rather than squeezing further. */
export const Cramped: Story = { args: { mode: 'cramped', width: 300 } };

/** Over the whole frame — the same surface drawn larger, with the way back beside the session. */
export const Full: Story = { args: { mode: 'full', tab: 'review' } };

/** Over the whole frame because the window is narrow: only widening undoes it, so no way back is offered. */
export const FullInANarrowWindow: Story = { args: { mode: 'full', autoFull: true } };

/** Closed: a strip of its tabs, since nothing but the person opens it again. */
export const Closed: Story = { args: { mode: 'closed', width: 32 } };
