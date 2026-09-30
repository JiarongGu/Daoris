import type { Meta, StoryObj } from '@storybook/react-vite';
import { SyncSection, type LinePull, type RebaseBranch } from './Sync';
import type { LandedBranch } from './Sweep';

// Bring up to date (WSR6), in the shape the driver's TREES_SYNC_PLAN answer takes: the owner's case after a
// squash-merged pull request, every reason a row stays, and the section before anyone has looked.

const pull = (extra: Partial<LinePull> & Pick<LinePull, 'kind'>): LinePull => ({
  repository: 'engine', workspace: 'aurora', line: 'main', commits: 0, moves: false, ...extra,
});

const rebase = (extra: Partial<RebaseBranch> & Pick<RebaseBranch, 'branch' | 'kind'>): RebaseBranch => ({
  repository: 'engine', workspace: 'aurora', landed: false, onto: 'main', commits: 0, replays: false, ...extra,
});

const landed = (extra: Partial<LandedBranch> & Pick<LandedBranch, 'branch' | 'kind'>): LandedBranch => ({
  repository: 'engine', workspace: 'aurora', files: [], commits: 1, removable: false, ...extra,
});

const meta = {
  title: 'Settings/Sync',
  component: SyncSection,
  args: { onLook: () => {}, onSync: () => {} },
} satisfies Meta<typeof SyncSection>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Before a look: nothing is fetched until the person asks. */
export const NotLookedYet: Story = { args: { plan: undefined } };

/** The owner's case: the line takes the squash, the step replays its own commit, the parent's branch goes. */
export const AfterASquashMerge: Story = {
  args: {
    plan: {
      lines: [pull({ kind: 'fast-forward', commits: 1, moves: true, from: 'b10be2c1', to: 'b0b311d4' })],
      rebases: [
        rebase({ branch: 'daoris/s-3c4d5e6f', kind: 'replay', commits: 1, cutBy: 'record', grewFrom: 'daoris/s-1f2e3d4c', replays: true }),
        rebase({ branch: 'feature/0fda19-second-part', kind: 'replay', landed: true, commits: 1, cutBy: 'record', replays: true }),
      ],
      deletes: [landed({ branch: 'feature/0fda18-fix-the-api-gap', kind: 'on-line', where: 'origin/main', removable: true })],
    },
  },
};

/** Every reason a row stays as it is. */
export const EveryReasonToStay: Story = {
  args: {
    plan: {
      lines: [
        pull({ kind: 'dirty' }),
        pull({ repository: 'game', kind: 'diverged', commits: 2 }),
        pull({ repository: 'tools', line: 'develop', kind: 'up-to-date', fetch: 'fatal: could not read from remote repository' }),
      ],
      rebases: [
        rebase({ branch: 'daoris/s-9e0f1a2b', kind: 'in-use' }),
        rebase({ branch: 'daoris/s-3c4d5e6f', kind: 'dirty' }),
        rebase({ repository: 'game', branch: 'feature/0fda20-still-open', kind: 'pushed', landed: true }),
        rebase({ repository: 'game', branch: 'daoris/s-7a8b9c0d', kind: 'grew-from-unlanded', grewFrom: 'daoris/s-5a6b7c8d' }),
        rebase({ repository: 'game', branch: 'daoris/s-0a1b2c3d', kind: 'up-to-date' }),
        rebase({ repository: 'tools', branch: 'daoris/s-4e5f6a7b', kind: 'unknown', detail: 'there is no `develop` here, nor `origin/develop`' }),
      ],
      deletes: [landed({ branch: 'feature/0fda21-leaned-on', kind: 'leaned-on', where: 'daoris/s-9e0f1a2b' })],
    },
  },
};

/**
 * The owner's workspace (WSR7, D112): three repositories hold Daoris's branches and the look fetches those; the rest
 * are listed apart, collapsed, to be ticked and included.
 */
export const RepositoriesApart: Story = {
  args: {
    plan: undefined,
    scope: [
      ...['engine', 'game', 'tools'].map((repository) => ({ repository, workspace: 'aurora', holds: true })),
      ...['atlas', 'beacon', 'cinder', 'delta', 'ember', 'fjord', 'grove'].map((repository) => ({ repository, workspace: 'aurora', holds: false })),
    ],
  },
};

export const UpToDate: Story = {
  args: { plan: { lines: [pull({ kind: 'up-to-date' })], rebases: [rebase({ branch: 'daoris/s-1f2e3d4c', kind: 'up-to-date' })], deletes: [] } },
};

/** A press under way: its button reads as busy, and the section says what it is doing. */
export const Working: Story = { args: { ...AfterASquashMerge.args, bringing: true } };

/** A look under way (WSR7): how many repositories it is fetching, said where the person pressed. */
export const Looking: Story = { args: { ...RepositoriesApart.args, looking: true, lookingAt: 3 } };

/** A look the page stopped waiting for, said in the section rather than only in a toast. */
export const StoppedWaiting: Story = { args: { ...RepositoriesApart.args, stopped: 'look' } };
