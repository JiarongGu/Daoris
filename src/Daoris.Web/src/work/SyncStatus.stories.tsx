import type { ReactNode } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { SyncStatus } from './SyncStatus';

// Where a circle stands with its remote (SYNC6b), as the status bar's sync item. Each state is one a
// real machine reaches: level, work waiting to go up, a quest in conflict, a remote that could not be
// reached, a circle never synced — and a browser, which reads the standing but has no *Sync now*.

const meta: Meta = { title: 'Work/SyncStatus' };
export default meta;

const minutes = (count: number) => new Date(Date.now() - count * 60_000).toISOString();

/** A bar to sit in, and the tooltip provider the application mounts once — a `Tip` outside one throws. */
const Bar = ({ children }: { children: ReactNode }) => (
  <Tooltip.Provider>
    <div className="flex h-6 items-stretch border-t border-line bg-raised text-meta text-ink-soft">{children}</div>
  </Tooltip.Provider>
);

const noop = () => {};

export const Level: StoryObj = {
  render: () => (
    <Bar>
      <SyncStatus
        workspace="aurora" standing={{ ahead: 0, behind: [], synced: minutes(2), tried: minutes(2), problem: null }}
        conflicts={[]} onSyncNow={noop} onOpenQuest={noop} onRemotes={noop}
      />
    </Bar>
  ),
};

export const AheadAndInConflict: StoryObj = {
  render: () => (
    <Bar>
      <SyncStatus
        workspace="aurora" standing={{ ahead: 3, behind: ['9f2c1a0b44de'], synced: minutes(4), tried: minutes(4), problem: null }}
        conflicts={[{ id: '4b7e21aa90c3', title: 'Cut the release branch' }, { id: '0c11de5f2a77' }]}
        onSyncNow={noop} onOpenQuest={noop} onRemotes={noop} defaultOpen
      />
    </Bar>
  ),
};

/** The remote could not be reached: the last time it was, and the wall in the host's own words. */
export const Walled: StoryObj = {
  render: () => (
    <Bar>
      <SyncStatus
        workspace="aurora"
        standing={{
          ahead: 2, behind: [], synced: minutes(95), tried: minutes(1),
          problem: 'the remote could not be reached (No connection could be made because the target machine actively refused it.)',
        }}
        conflicts={[]} onSyncNow={noop} onOpenQuest={noop} onRemotes={noop} defaultOpen
      />
    </Bar>
  ),
};

export const NeverSynced: StoryObj = {
  render: () => (
    <Bar>
      <SyncStatus
        workspace="工作区" standing={{ ahead: 1, behind: [], synced: null, tried: null, problem: null }}
        conflicts={[]} onSyncNow={noop} onOpenQuest={noop} onRemotes={noop}
      />
    </Bar>
  ),
};

export const Syncing: StoryObj = {
  render: () => (
    <Bar>
      <SyncStatus
        workspace="aurora" standing={{ ahead: 1, behind: [], synced: minutes(3), tried: minutes(3), problem: null }}
        conflicts={[]} syncing onSyncNow={noop} onOpenQuest={noop} onRemotes={noop} defaultOpen
      />
    </Bar>
  ),
};

/** A browser: the standing is the host's to tell anyone on this machine, and the pass is the shell's. */
export const InABrowser: StoryObj = {
  render: () => (
    <Bar>
      <SyncStatus
        workspace="aurora" standing={{ ahead: 0, behind: [], synced: minutes(1), tried: minutes(1), problem: null }}
        conflicts={[{ id: '4b7e21aa90c3', title: 'Cut the release branch' }]} onOpenQuest={noop} defaultOpen
      />
    </Bar>
  ),
};
