import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { BranchPage, type BranchGraph, type BranchRead } from './BranchPage';
import { GIT_BRANCH_PARTED, GIT_BRANCH_PARTED_PAGES, GIT_BRANCHES_ENGINE } from '../fixtures';

// A branch's page (UX6h3b, the built-in Git design §2.3) in Repositories' main area, opened from a branch's name in Branches:
// each kind (a session's with its state and title, landed in step with an open pull request, landed and gone from origin after
// it merged, landed and never pushed, yours, origin's), its walk a page at a time with the foot reading and refused, a branch
// at its line's own commit, its states (the list or the walk on its way, refused, gone from the list, its line with no commit
// here), dark, 中文 and narrow.

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/** The list beside the main area a strip and the side bar closed: the main area at 34 rem. */
const narrow: Decorator = (Story) => <div className="flex h-[44rem] w-[34rem] max-w-full border border-line bg-page"><Story /></div>;

const PHYSICS = 'daoris/engine-physics-step';
const LINE_COMMIT = GIT_BRANCHES_ENGINE.line.commit!;
const branch = (name: string) => GIT_BRANCHES_ENGINE.branches.find((each) => each.name === name)!;

/** Its walk since it parted from its line, the line's tip drawn first, as its holder hands it. */
const walked = (name: string, more: Partial<Extract<BranchGraph, { state: 'read' }>['value']> = {}): BranchGraph =>
  ({ state: 'read', value: { tips: [LINE_COMMIT, branch(name).commit], commits: GIT_BRANCH_PARTED, more: false, ...more } });

/** The branch as the list holds it, beside `main`, with its walk. */
const listed = (name: string, graph: BranchGraph = walked(name), title: string | null = null): BranchRead =>
  ({ state: 'read', value: { branch: branch(name), line: 'main', title }, graph });

const meta: Meta<typeof BranchPage> = {
  title: 'Repositories/BranchPage',
  component: BranchPage,
  args: {
    repository: 'engine',
    name: PHYSICS,
    read: listed(PHYSICS, walked(PHYSICS), 'Expose a streaming budget'),
    onBack: nothing,
    onMore: nothing,
  },
  // The main area's height, and a width between the side bar's two states, as a repository's page's stories have.
  decorators: [(Story) => (
    <div className="flex h-[44rem] w-[52rem] max-w-full border border-line bg-page">
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof BranchPage>;

/**
 * A session's branch: its name, *session branch* and its session's state as pills, its session's title, two commits ahead of
 * `main` and two behind; *Back to Branches*; then its two commits beside the line's two since they parted, in three lanes.
 */
export const Session: Story = {};

/** Landed, in step with its name on origin, its pull request's link, and its plugin's answer that it is open, asked 2h ago. */
export const Landed: Story = { args: { name: 'review/q6-asset-cache', read: listed('review/q6-asset-cache') } };

/** Landed, pushed and gone from origin since, its pull request merged 30h ago. */
export const LandedGone: Story = { args: { name: 'review/q4-input-map', read: listed('review/q4-input-map') } };

/** Landed and never pushed: no link. */
export const LandedNotPushed: Story = { args: { name: 'review/q2-shader-cache', read: listed('review/q2-shader-cache') } };

/** Yours: four commits ahead of `main` and nine behind. */
export const Yours: Story = { args: { name: 'spike/renderer', read: listed('spike/renderer') } };

/** Origin's, a branch only origin holds: its kind, and no count, as git counted none. */
export const Origins: Story = { args: { name: 'origin/release', read: listed('origin/release') } };

/** The first page of its walk, which answered `more`: the foot's count and *Show more*. */
export const More: Story = { args: { read: listed(PHYSICS, walked(PHYSICS, { commits: GIT_BRANCH_PARTED_PAGES[0], more: true })) } };

/** The next page on its way: the foot's status says so. */
export const MoreLoading: Story = {
  args: { read: listed(PHYSICS, walked(PHYSICS, { commits: GIT_BRANCH_PARTED_PAGES[0], more: true, loadingMore: true })) },
};

/** The next page refused: its sentence in the foot, over *Show more*, the rows kept. */
export const MoreFailed: Story = {
  args: {
    read: listed(PHYSICS, walked(PHYSICS, {
      commits: GIT_BRANCH_PARTED_PAGES[0], more: true, moreError: 'Git could not answer for this. Try again, or ask a terminal.',
    })),
  },
};

/** At its line's own commit: nothing since they parted, said in a sentence. */
export const Same: Story = { args: { read: listed(PHYSICS, walked(PHYSICS, { commits: [] })) } };

/** The list on its way: the header names it, with its way back, and the body says what it reads. */
export const Reading: Story = { args: { read: { state: 'reading' } } };

/** Listed, its walk on its way: the header says where it stands already. */
export const WalkReading: Story = { args: { read: listed(PHYSICS, { state: 'reading' }) } };

/** The list refused in the driver's words. */
export const Refused: Story = { args: { read: { state: 'refused', sentence: 'git could not list the branches here: git did not start.' } } };

/** Gone from the list since its row was pressed: said, with the way back. */
export const Gone: Story = { args: { read: { state: 'gone' } } };

/** Its line has no commit here, so it cannot be drawn beside it. */
export const NoLine: Story = { args: { read: listed(PHYSICS, { state: 'noLine' }) } };

export const SessionDark: Story = { decorators: [dark] };

export const LandedDark: Story = { args: Landed.args, decorators: [dark] };

export const SessionChinese: Story = { decorators: [chinese] };

export const LandedChinese: Story = { args: Landed.args, decorators: [chinese] };

export const MoreChinese: Story = { args: MoreFailed.args, decorators: [chinese] };

export const LandedNarrow: Story = { args: Landed.args, decorators: [narrow] };

export const MoreNarrowDarkChinese: Story = { args: MoreFailed.args, decorators: [narrow, dark, chinese] };
