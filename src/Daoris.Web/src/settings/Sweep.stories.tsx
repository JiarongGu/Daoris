import type { Meta, StoryObj } from '@storybook/react-vite';
import { DiscardBranchAsk, SweepList, type LandedBranch, type SweepBranch } from './Sweep';

// Session branches (WSR3, D88), in the shape the driver's SWEEP_PLAN answer takes: every kind a
// branch can be, and a machine with none.

const branch = (extra: Partial<SweepBranch> & Pick<SweepBranch, 'branch' | 'kind'>): SweepBranch => ({
  repository: 'engine', workspace: 'aurora', hasTree: true, commits: 0, removable: false, ...extra,
});

const meta = {
  title: 'Settings/Sweep',
  component: SweepList,
  args: { onLook: () => {}, onClean: () => {} },
} satisfies Meta<typeof SweepList>;

export default meta;
type Story = StoryObj<typeof meta>;

export const EveryKind: Story = {
  args: {
    branches: [
      branch({ branch: 'daoris/s-1f2e3d4c', kind: 'empty', where: 'main', removable: true }),
      branch({ branch: 'daoris/s-5a6b7c8d', kind: 'landed', where: 'feature/0fda18-fix-the-api-gap', removable: true }),
      branch({ branch: 'daoris/s-9e0f1a2b', kind: 'unlanded', commits: 2, detail: 'a1b2c3d the work\ne4f5a6b more work' }),
      branch({ branch: 'daoris/s-3c4d5e6f', kind: 'dirty', detail: '3 path(s) uncommitted' }),
      branch({ repository: 'game', branch: 'daoris/s-7a8b9c0d', kind: 'in-use' }),
    ],
  },
};

const landed = (extra: Partial<LandedBranch> & Pick<LandedBranch, 'branch' | 'kind'>): LandedBranch => ({
  repository: 'engine', workspace: 'aurora', files: [], commits: 1, removable: false, ...extra,
});

/** WSR5: the branches landings made, beside the session branches — every kind one can be. */
export const WithLandedBranches: Story = {
  args: {
    ...EveryKind.args,
    landed: [
      landed({ branch: 'feature/0fda18-fix-the-api-gap', kind: 'on-line', where: 'origin/main', removable: true }),
      landed({ branch: 'feature/0fda17-first-part', kind: 'inside', where: 'feature/0fda18-fix-the-api-gap', removable: true }),
      landed({ branch: 'feature/0fda19-merged', kind: 'merged', where: 'main', removable: true }),
      landed({ branch: 'feature/0fda20-still-open', kind: 'differs', where: 'main', files: ['src/report.ts', 'src/report.test.ts'] }),
      landed({ repository: 'game', branch: 'feature/0fda21-checked-out', kind: 'checked-out' }),
      landed({ repository: 'game', branch: 'feature/0fda22-pushed-then-moved', kind: 'ahead-of-remote', commits: 2 }),
      landed({ repository: 'game', branch: 'feature/0fda23-leaned-on', kind: 'leaned-on', where: 'daoris/s-7a8b9c0d' }),
      landed({ repository: 'game', branch: 'feature/0fda24-unknown', kind: 'unknown', detail: 'there is no `develop` here, nor `origin/develop`, to compare it with' }),
    ],
  },
};

/**
 * LAND3b: failed or superseded attempts' branches, which no clean-up takes, each offering *Discard branch…* where the
 * driver says it may: one whose tree is gone, one whose tree is still here. Neither the empty nor the unsure offers it.
 */
export const FailedAttempts: Story = {
  args: {
    onDiscard: () => {},
    branches: [
      branch({ branch: 'daoris/s-1f2e3d4c', kind: 'empty', where: 'main', removable: true }),
      branch({ branch: 'daoris/s-9e0f1a2b', hasTree: false, kind: 'unlanded', commits: 2, detail: 'a1b2c3d the work\ne4f5a6b more work', discardable: true }),
      branch({ branch: 'daoris/s-4a5b6c7d', kind: 'unlanded', commits: 1, detail: 'c3d4e5f a superseded attempt', discardable: true }),
      branch({ branch: 'daoris/s-8e9f0a1b', hasTree: false, kind: 'unlanded', commits: 0 }),
    ],
  },
};

/** A discard on its way: that row's press waits for it. */
export const Discarding: Story = { args: { ...FailedAttempts.args, discarding: 'engine:daoris/s-9e0f1a2b' } };

/** The ask a discard opens under its row, naming the branch and its commits; with its tree where it still has one. */
export const DiscardAsk: Story = {
  render: () => (
    <div className="grid max-w-2xl gap-3">
      <DiscardBranchAsk
        branch={{ repository: 'engine', branch: 'daoris/s-9e0f1a2b', commits: 2, hasTree: false }}
        onDiscard={() => {}}
        onClose={() => {}}
      />
      <DiscardBranchAsk
        branch={{ repository: 'engine', branch: 'daoris/s-4a5b6c7d', commits: 1, hasTree: true }}
        onDiscard={() => {}}
        onClose={() => {}}
      />
    </div>
  ),
};

export const Cleaning: Story = { args: { ...EveryKind.args, busy: true } };

export const Asking: Story = { args: { branches: undefined } };

export const NoneHere: Story = { args: { branches: [] } };
