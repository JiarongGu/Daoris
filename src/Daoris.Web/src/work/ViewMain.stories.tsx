import type { Meta, StoryObj } from '@storybook/react-vite';
import { Button, Card, Icon, PageHeader } from '../ui';
import { ViewMain } from './ViewMain';

// A view's main area (D118 §3b, §5) in each of its four states, and as a container its page lays out by:
// the two cards below sit side by side only where the main area itself is wide, whatever the window is.

const HEADER = <PageHeader title="Expose a streaming budget" description="game → engine · filed 3 days ago" action={<Button variant="primary">Take</Button>} />;

function Split() {
  return (
    <div className="grid items-start gap-3.5 @4xl/main:grid-cols-2">
      <Card><p className="m-0 text-body text-ink-soft">World streaming needs a per-frame cap, read from the level file.</p></Card>
      <Card><p className="m-0 text-body text-ink-soft">Two cards side by side where the main area is 56rem or wider, and one above the other where it is not.</p></Card>
    </div>
  );
}

const meta: Meta<typeof ViewMain> = {
  title: 'Work/ViewMain',
  component: ViewMain,
  args: {
    header: HEADER,
    children: <Split />,
    none: {
      headline: 'Nothing chosen',
      body: 'Choose a quest in the list and it opens here, with its acts in its header.',
      action: <Button><Icon name="plus" size={14} />New quest</Button>,
    },
    gone: { headline: 'No record of that quest', body: 'It is not in this workspace any more: it may have been deleted, or its workspace retired.' },
  },
  // The frame's height, beside a stand-in list, as the main area sits on the window.
  decorators: [(Story) => (
    <div className="flex h-[26rem] w-[64rem] max-w-full border border-line bg-page">
      <div className="w-[17.5rem] shrink-0 border-r border-line p-3 text-meta text-ink-faint">The list pane</div>
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof ViewMain>;

/** The chosen item: its page header, and its page laid out by the main area's own width. */
export const Chosen: Story = {};

/** Nothing chosen: how to choose, and the list's `＋`. */
export const NothingChosen: Story = { args: { state: 'none', header: undefined } };

/** The chosen item loading: skeleton rows, never the empty state (audit SE11). */
export const Loading: Story = { args: { state: 'loading', header: undefined } };

/** The chosen item has gone: retired, deleted, or no longer on this machine. */
export const Gone: Story = { args: { state: 'gone', header: undefined } };

/** Narrowed by a side bar at a wide window: the split follows the main area, so the cards stack. */
export const Narrowed: Story = {
  decorators: [(Story) => (
    <div className="flex w-[36rem] max-w-full"><Story /></div>
  )],
};

/** A view with no list: the view's own header, as Overview has. */
export const ViewPage: Story = {
  args: { header: <PageHeader title="Overview" description="Is anything sitting, and for how long." /> },
};

/** Sessions' main area: the tighter gutters its conversation keeps, and a column that follows its tail. */
export const SessionGutters: Story = { args: { gutters: 'session', header: undefined } };
