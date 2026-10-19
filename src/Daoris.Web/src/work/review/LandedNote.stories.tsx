import type { Meta, StoryObj } from '@storybook/react-vite';
import type { LandedWork } from './diff';
import { LandedNote } from './LandedNote';

// Every state of where a landed session's work went (REVIEW2, D113), on the shipped component. A molecule, so
// each is reached by passing props (components plan §2) — including the ones a real record rarely shows.

const LANDED: LandedWork = {
  branch: 'feature/0fda18-fix-the-api-gap',
  repository: 'engine',
  line: 'main',
  landedAt: '2026-10-01T09:30:00.0000000+00:00',
  plugin: null,
  pushed: false,
  pullRequest: null,
  state: 'standing',
  asLanded: true,
  reads: null,
  removed: null,
  detail: null,
};

const meta: Meta<typeof LandedNote> = {
  title: 'Work/LandedNote',
  component: LandedNote,
  args: { landed: LANDED, source: 'branch' },
  decorators: [(Story) => <div className="w-[26rem] border border-line bg-raised"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof LandedNote>;

/** The installed window's case: the landing tidied the tree, and the changes are read from the branch. */
export const TidiedAndStanding: Story = {};

/** A plugin pushed it and opened the pull request: one press away. */
export const WithItsPullRequest: Story = {
  args: { landed: { ...LANDED, plugin: 'azure-devops', pushed: true, pullRequest: 'https://example.test/org/engine/pullrequest/7' } },
};

/** No tidy: the tree is still here, and the branch stands, so it is not accepted again. */
export const TreeStillHere: Story = { args: { source: 'tree' } };

/** The pull request merged by a squash, and the clean-up removed the branch. */
export const GoneOnTheLine: Story = {
  args: {
    landed: {
      ...LANDED, state: 'gone',
      removed: { kind: 'on-line', where: 'origin/main', at: '2026-10-01T11:00:00Z' },
      reads: { kind: 'on-line', where: 'main', files: [], detail: null },
    },
  },
};

/** Deleted by hand before its work reached the line. */
export const GoneAndDiffers: Story = {
  args: { landed: { ...LANDED, state: 'gone', reads: { kind: 'differs', where: 'main', files: ['src/api/gap.ts', 'src/api/gap.test.ts', 'docs/api.md', 'CHANGELOG.md'], detail: null } } },
};

/** Gone, and git has pruned its commits since. */
export const GoneAndPruned: Story = {
  args: { landed: { ...LANDED, state: 'gone', removed: { kind: 'merged', where: 'main', at: null }, reads: { kind: 'commits-gone', where: null, files: [], detail: null } } },
};

export const RebasedByHand: Story = { args: { landed: { ...LANDED, state: 'not-ours' } } };

export const NoCheckoutHere: Story = { args: { landed: { ...LANDED, state: 'no-checkout' } } };

/** A record with no moment, and a branch whose changes git could not read. */
export const Unreadable: Story = {
  args: { landed: { ...LANDED, landedAt: null, detail: 'git found no point where `feature/0fda18-fix-the-api-gap` left its line, so there is no range to read.' } },
};

/** A long branch name, at the side bar's floor: it wraps rather than widening the pane. */
export const LongBranch: Story = {
  args: { landed: { ...LANDED, branch: 'feature/TK-2202-align-the-streaming-budget-with-the-tile-loader-and-its-tests' } },
  decorators: [(Story) => <div className="w-[300px]"><Story /></div>],
};
