import type { Meta, StoryObj } from '@storybook/react-vite';
import {
  Button, Chip, Dot, Drawer, EmptyState, MetaLine, MonoWell, Pill, SkeletonRows, Tile, Toasts,
} from './ui';

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

/**
 * The four meanings a liveness mark carries, each wearing its word. There is no story here for a dot
 * without a label, because the component cannot render one — which is the point (D41 §6).
 */
export const Dots: StoryObj = {
  render: () => (
    <div className="flex flex-wrap items-center gap-5">
      <Dot tone="live" label="working" />
      <Dot tone="parked" label="awaiting person" />
      <Dot tone="ended" label="completed" />
      <Dot tone="idle" label="queued" />
    </div>
  ),
};

/** 500 lines is the scroll case; 12k dropped is the one a person must never have to infer. */
const LONG_LOG = Array.from(
  { length: 500 },
  (_, index) => `[${String(index).padStart(4, '0')}] resolving dependency graph — 道衍 canon/core/rules`,
).join('\n');

export const Wells: StoryObj = {
  render: () => (
    <div className="grid max-w-3xl gap-6">
      <MonoWell label="empty — the well is there, with nothing in it yet" text="" />
      <MonoWell
        label="console"
        live
        text={'$ npm run verify\ndaoris: clean — 23862 of 26000 bytes of always-loaded core\nrelease-prep: 0.0.1 agrees across every shipped reference'}
      />
      <MonoWell label="console" tall dropped={12_043} text={LONG_LOG} />
    </div>
  ),
};

/**
 * A pair with no value is absent, never blank: on a session record an absence means something real
 * (no profile means the harness's own home), and a placeholder would look like a bug instead.
 */
export const MetaLines: StoryObj = {
  render: () => (
    <div className="grid max-w-2xl gap-5">
      <MetaLine items={[{ label: 'moved', value: '4m ago' }]} />
      <MetaLine
        items={[
          { label: 'repository', value: 'engine' },
          { label: 'tree', value: 'worktrees/streaming-budget', mono: true },
          { label: 'quest', value: '#7a82cc', mono: true },
          { label: 'tool', value: 'claude-code 2.1.4 · as owner' },
          { label: 'started', value: '2h ago' },
          { label: 'moved', value: '4m ago' },
        ]}
      />
      <MetaLine
        items={[
          { label: 'repository', value: 'engine' },
          { label: 'profile', value: null },
          { label: 'tree', value: 'a/deliberately/long/checkout/path/that/has/nowhere/to/wrap/worktrees/streaming', mono: true },
        ]}
      />
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
