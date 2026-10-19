import type { Meta, StoryObj } from '@storybook/react-vite';
import { NothingFollowed, RightDock } from './RightDock';

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

/**
 * Holding what the person moved in (DOCK1b): Ask Daoris beside the session's two, and the console from
 * the panel, at the end. At the floor the unselected tabs are their icons alone (TABS1), and the list at
 * the row's end names every one.
 */
export const HoldingMovedViews: Story = {
  args: { views: ['timeline', 'review', 'ask', 'console'], tab: 'console', width: 300, onMove: () => {}, onReset: () => {} },
};

/**
 * The side bar the installed window showed (TABS1): 430px holding the timeline, the review, Ask Daoris
 * and a file's preview, which in 中文 needed 367 of its 349px. Each unselected tab was cut to one character
 * and an ellipsis; now each is its icon, its name in its tip, and the shown preview keeps its name. Widened
 * past what the names need, every name is whole again. Storybook lays it out, so the measure is real
 * here; the smoke render in vitest has no layout and draws the names.
 */
export const FourTabsAtANarrowSideBar: Story = {
  args: {
    views: ['timeline', 'review', 'ask'], tab: 'preview', width: 430, onMove: () => {},
    preview: { name: 'budget-report.md', path: 'docs/streaming/budget-report.md', onClose: () => {} },
  },
};

/** The same four with room for every name: each unselected tab keeps its whole name. */
export const FourTabsWithRoom: Story = {
  args: { ...FourTabsAtANarrowSideBar.args, width: 640 },
};

/** Every view moved out: it says how to fill it, as VS Code's empty container does. */
export const Emptied: Story = { args: { views: [], onReset: () => {} } };

const PREVIEW = { name: 'chunk.rs', path: 'src/world/streaming/chunk.rs', onClose: () => {} };

/** A file's preview (PREVIEW1): a tab after the views, named for the file, with its own ×. */
export const WithAPreview: Story = { args: { tab: 'preview', preview: PREVIEW } };

/**
 * At the floor with Ask Daoris and a preview: the preview keeps its name while it is shown, cut at its cap
 * when the name is longer, and the rest are their icons.
 */
export const APreviewAtTheFloor: Story = {
  args: { views: ['timeline', 'review', 'ask'], tab: 'preview', preview: PREVIEW, width: 300, onMove: () => {} },
};

/** Closed with a preview open: its strip carries the file's door after the views'. */
export const ClosedWithAPreview: Story = { args: { mode: 'closed', width: 32, preview: PREVIEW } };

/**
 * Off Sessions with no session followed (UX6b, design §2.5): the side bar opens on Ask Daoris. On the install it kept an
 * ended session from the day before, its whole done note running past the window's foot.
 */
export const OffSessionsOnAskDaoris: Story = {
  args: {
    views: ['timeline', 'review', 'ask'], tab: 'ask',
    children: <div className="p-3 text-small text-ink-soft">Ask Daoris: its conversation and its box.</div>,
  },
};

/** Its timeline there, once the session attended in Sessions has ended: it says so, and offers Sessions. */
export const OffSessionsTheAttendedSessionEnded: Story = {
  args: { views: ['timeline', 'review', 'ask'], tab: 'timeline', children: <NothingFollowed ended onOpenSessions={() => {}} /> },
};

/** And with nothing attended at all, as before: no session list is named on a view that has none (audit SE11). */
export const OffSessionsNothingAttended: Story = {
  args: { views: ['timeline', 'review', 'ask'], tab: 'timeline', children: <NothingFollowed ended={false} onOpenSessions={() => {}} /> },
};
