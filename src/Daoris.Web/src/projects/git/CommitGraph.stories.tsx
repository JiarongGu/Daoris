import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { GIT_BRANCH_PARTED, GIT_HISTORY_ENGINE } from '../fixtures';
import { CommitGraph, type GraphCommitRow } from './CommitGraph';

// A commit graph (UX6h1, the built-in Git design §2.3) as a repository's History tab and a branch's page draw it
// (`RepositoryGit.tsx`, `BranchPage.tsx`), on its own (STORYGAP1, the UI/UX design §1.2 item 6): the line with a branch merged
// in, a branch beside the line since they parted, three lanes at once, a subject long enough to truncate, a CJK one, and a
// single commit; at the main area's three widths, where the author's column gives way first by the main area's width, in 中文
// and dark.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const hoursAgo = (hours: number) => new Date(Date.now() - hours * 3_600_000).toISOString();
const row = (commit: string, parents: string[], subject: string, author: string, hours: number): GraphCommitRow => ({
  commit: commit.padEnd(40, '0'), parents: parents.map((parent) => parent.padEnd(40, '0')), subject, author,
  authoredAt: hoursAgo(hours), committedAt: hoursAgo(hours),
});

/** Two branches merged into the line one after the other, a third still open: three lanes at once. */
const THREE_LANES: GraphCommitRow[] = [
  row('e01a7c', ['d93b11', 'b7c402'], 'Merge the audio mixer', 'Mei Lin', 1),
  row('f4a9d0', ['a5e7f3'], 'Try a ring buffer for the voice queue', 'Priya Natarajan', 2),
  row('b7c402', ['a5e7f3'], 'Mix the voices on the audio thread', 'Priya Natarajan', 3),
  row('d93b11', ['c2f8e6', '8e1d55'], 'Merge the input remap', 'Mei Lin', 5),
  row('8e1d55', ['a5e7f3'], 'Remap the gamepad from the settings', 'Sam Ortiz', 9),
  row('c2f8e6', ['a5e7f3'], 'Name the frame budget in the settings', 'Sam Ortiz', 20),
  row('a5e7f3', [], 'Begin the engine', 'Mei Lin', 400),
];

/** A subject far longer than any row: it truncates, and the short id and the time stay in view. */
const LONG: GraphCommitRow[] = [
  row('9a0b1c', ['3d4e5f'], 'Cap the chunk streamer\'s hydration per frame so a cold cache never stalls the first frame after a fresh install on the test rig', 'Mei Lin', 1),
  row('3d4e5f', ['6a7b8c'], '在冷缓存时限制每帧的区块加载量，使首帧不再卡顿', '林美', 4),
  row('6a7b8c', [], 'Begin the engine', 'Mei Lin', 400),
];

const meta = {
  title: 'Repositories/CommitGraph',
  component: CommitGraph,
  args: { label: 'History of main', commits: GIT_HISTORY_ENGINE },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof CommitGraph>;

export default meta;
type Story = StoryObj<typeof meta>;

/** The line's history: a branch merged in beside it, its merge's dot hollow, to its root. */
export const TheLine: Story = {};

/** A branch beside the line since they parted, walked from both tips, the branch's first. */
export const SinceParted: Story = {
  args: {
    label: 'daoris/engine-physics-step beside main',
    commits: GIT_BRANCH_PARTED,
    tips: [GIT_BRANCH_PARTED[0]!.commit, GIT_BRANCH_PARTED[1]!.commit],
  },
};

/** Three lanes at once: two merges and a branch still open. */
export const ThreeLanes: Story = { args: { commits: THREE_LANES, tips: [THREE_LANES[0]!.commit, THREE_LANES[1]!.commit] } };

/** A long subject truncates, and a CJK one beside it. */
export const LongSubjects: Story = { args: { commits: LONG } };

/** A single commit: one dot, no line. */
export const OneCommit: Story = { args: { commits: GIT_HISTORY_ENGINE.slice(-1) } };

/** Half: a 680 px window with the list a strip beside it; the author's column is still shown. */
export const Half: Story = { args: { commits: THREE_LANES }, decorators: [storyWidths.half] };

/** The main area's 400 px floor: the author's column gives way, the subject truncates, the id and the time stay. */
export const Narrow: Story = { args: { commits: THREE_LANES }, decorators: [storyWidths.narrow] };

/** The floor with long subjects. */
export const NarrowLong: Story = { args: { commits: LONG }, decorators: [storyWidths.narrow] };

/** In 中文, wide: the times in 中文. */
export const Chinese: Story = { args: { commits: LONG }, decorators: [chinese] };

/** In 中文, at the floor. */
export const ChineseNarrow: Story = { args: { commits: THREE_LANES }, decorators: [chinese, storyWidths.narrow] };

/** In dark: the hollow merge dots on the dark page. */
export const Dark: Story = { args: { commits: THREE_LANES }, decorators: [dark] };

/** In 中文 and dark, half. */
export const ChineseDarkHalf: Story = { args: { commits: THREE_LANES }, decorators: [chinese, dark, storyWidths.half] };
