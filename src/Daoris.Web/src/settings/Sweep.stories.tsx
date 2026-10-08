import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { ReactNode } from 'react';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import { DiscardBranchAsk, SweepList, type LandedBranch, type SweepBranch } from './Sweep';
import { SyncSection } from './Sync';

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
      // SWEEPCARRIED1: a squash left its commits on no branch of the person's; the landed branch's pull request carried them.
      branch({ branch: 'daoris/s-2d3e4f5a', kind: 'carried', commits: 2, where: 'feature/0fda16-squashed', removable: true }),
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

/**
 * The main area at a width (UXFIX4), as `ViewMain` draws it: its container and a page's gutters, which are 12 px under a
 * 768 px window and 24 px over it. 600 px is the main area at a 680 px window, the list a strip beside it; 400 px is the
 * main area's floor in a wider window, beside an open list and side bar, where the gutters are the wider.
 */
function Main({ width, narrowWindow, children }: { width: number; narrowWindow: boolean; children: ReactNode }) {
  return <div className={`@container/main max-w-full bg-page pb-6 pt-5 ${narrowWindow ? 'px-3' : 'px-6'}`} style={{ width }}>{children}</div>;
}

const at680: Decorator = (Story) => <Main width={600} narrowWindow><Story /></Main>;
const atFloor: Decorator = (Story) => <Main width={400} narrowWindow={false}><Story /></Main>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/**
 * Long names, as a chain's steps and a branch rule's slugs make them: bringing up to date after a look, then every kind of
 * session branch with a discard offered, then the landed group. The workspace's Branches tab draws the three lists so.
 */
const LONG_NAMES = {
  onDiscard: () => {},
  sync: (
    <SyncSection
      plan={{
        lines: [{ repository: 'engine', workspace: 'aurora', line: 'main', kind: 'fast-forward', commits: 1, moves: true }],
        rebases: [{
          repository: 'engine', workspace: 'aurora', branch: 'daoris/s-3c4d5e6f-the-next-step-of-the-api-gap-chain', landed: false,
          kind: 'replay', onto: 'main', commits: 1, cutBy: 'record', grewFrom: 'daoris/s-1f2e3d4c', replays: true,
        }],
        deletes: [landed({ branch: 'feature/0fda17-first-part-of-the-api-gap', kind: 'inside', where: 'feature/0fda18-fix-the-api-gap-before-the-quarter-closes', removable: true })],
      }}
      onLook={() => {}}
      onSync={() => {}}
    />
  ),
  branches: [
    branch({ branch: 'daoris/s-1f2e3d4c', kind: 'empty', where: 'main', removable: true }),
    branch({ branch: 'daoris/s-5a6b7c8d-a-chain-step-that-carries-a-long-slug', kind: 'landed', where: 'feature/0fda18-fix-the-api-gap-before-the-quarter-closes', removable: true }),
    branch({ branch: 'daoris/s-9e0f1a2b', hasTree: false, kind: 'unlanded', commits: 2, detail: 'a1b2c3d the work\ne4f5a6b more work', discardable: true }),
    branch({ repository: 'game', branch: 'daoris/s-7a8b9c0d', kind: 'in-use' }),
  ],
  landed: [
    landed({ branch: 'feature/0fda18-fix-the-api-gap-before-the-quarter-closes', kind: 'on-line', where: 'origin/main', removable: true }),
    landed({ branch: 'feature/0fda20-still-open', kind: 'differs', where: 'main', files: ['src/report.ts', 'src/report.test.ts'] }),
  ],
} satisfies Story['args'];

/** Opens the first row's *Discard branch…*, so its ask shows under the row, across it (UXFIX2's one confirmation). */
const askToDiscard: Story['play'] = async ({ canvasElement }) => {
  [...canvasElement.querySelectorAll('button')].find((button) => button.textContent === 'Discard branch…')?.click();
};

/** UXFIX4, at a 680 px window: three columns still, each long name whole over two lines in its own. */
export const At680: Story = { args: LONG_NAMES, decorators: [at680] };

/** The same, in dark. */
export const At680Dark: Story = { args: LONG_NAMES, decorators: [at680, dark] };

/** UXFIX4, at the main area's 400 px floor: each row's sentence under its name, and every name whole. */
export const AtTheFloor: Story = { args: LONG_NAMES, decorators: [atFloor] };

/** The same, in dark. */
export const AtTheFloorDark: Story = { args: LONG_NAMES, decorators: [atFloor, dark] };

/** The same, in 中文. */
export const AtTheFloorChinese: Story = { args: LONG_NAMES, decorators: [atFloor, chinese] };

/** At the floor with a discard asking: the ask under its row, across it, and the name above it whole. */
export const AtTheFloorAsking: Story = { args: LONG_NAMES, decorators: [atFloor], play: askToDiscard };

/** The same, in dark. */
export const AtTheFloorAskingDark: Story = { args: LONG_NAMES, decorators: [atFloor, dark], play: askToDiscard };

export const Cleaning: Story = { args: { ...EveryKind.args, busy: true } };

export const Asking: Story = { args: { branches: undefined } };

export const NoneHere: Story = { args: { branches: [] } };
