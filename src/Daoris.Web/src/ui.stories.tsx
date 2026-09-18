import type { Meta, StoryObj } from '@storybook/react-vite';
import { Button, Chip, Drawer, EmptyState, Pill, SkeletonRows, Tile, Toasts } from './ui';

// Every state of every primitive, on the shipped components — including the states real data rarely
// shows. This is where the design is reviewed and kept (D42); the product cannot drift from it,
// because it IS the product's code.

const meta: Meta = { title: 'Design/Components' };
export default meta;

export const Buttons: StoryObj = {
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <Button variant="primary">publish quest</Button>
      <Button>take</Button>
      <Button variant="ghost">decline…</Button>
      <Button variant="danger">decline with this reason</Button>
      <Button disabled>disabled</Button>
    </div>
  ),
};

export const Pills: StoryObj = {
  render: () => (
    <div className="flex flex-wrap items-center gap-2">
      <Pill tone="open" title="published — nobody has taken it">Open</Pill>
      <Pill tone="taken">Taken</Pill>
      <Pill tone="done">Done</Pill>
      <Pill tone="declined">Declined</Pill>
      <Pill tone="declined">sat 12d</Pill>
      <Pill>Canonical</Pill>
      <Chip>the engine runtime — simulation, rendering, assets</Chip>
      <Chip accent>a failing case</Chip>
    </div>
  ),
};

export const Tiles: StoryObj = {
  render: () => (
    <div className="grid max-w-3xl grid-cols-[repeat(auto-fit,minmax(10.5rem,1fr))] gap-3">
      <Tile label="Adopted projects" value={2} note="of 17 in the family" />
      <Tile label="Open quests" value={3} note="oldest has sat 12d" warn />
      <Tile label="Knowledge entries" value="12.9K" note="across 14 repositories" />
    </div>
  ),
};

export const Empty: StoryObj = {
  render: () => (
    <EmptyState
      icon="inbox"
      headline="No open quests anywhere"
      body="The family owes itself nothing right now. When a project needs something from a sibling, it is asked for here — never edited across."
      action={<Button>ask for something</Button>}
    />
  ),
};

export const Loading: StoryObj = {
  render: () => <SkeletonRows rows={4} />,
};

export const ToastStates: StoryObj = {
  render: () => (
    <Toasts
      onClose={() => {}}
      items={[
        {
          id: 1,
          kind: 'ok',
          text: 'Published quest `#7a82cc` to `engine` — Open. It is held by the service, not written into that repository.',
        },
        { id: 2, kind: 'error', text: 'Declining needs a reason: it is the part the asker can act on.' },
      ]}
    />
  ),
};

export const DrawerDetail: StoryObj = {
  render: () => (
    <Drawer
      title="Expose a streaming budget on the chunk API"
      onClose={() => {}}
      meta={<><Pill tone="open">Open</Pill><span className="font-mono text-[0.72rem] text-ink-faint">#7a82cc</span></>}
      footer={
        <div className="flex flex-wrap gap-2">
          <Button>take</Button>
          <Button variant="primary">done</Button>
          <Button variant="ghost">decline…</Button>
        </div>
      }
    >
      <p className="m-0 whitespace-pre-wrap text-[0.9rem] leading-relaxed">
        World streaming needs to cap hydration work per frame; today the engine hydrates unbounded.
        Evidence: the seam appears whenever more than three chunks hydrate in one frame.
      </p>
    </Drawer>
  ),
};
