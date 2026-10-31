import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { GIT_CHANGES_ENGINE } from '../fixtures';
import { ChangesTab } from './RepositoryGit';

// A repository's Changes tab (GITAPP2b, D204, the git-writes design §1, §4, §11), drawn where the person let Daoris change the
// checkout's files: what is staged against the last commit, what the files hold beyond it (a file staged and then removed is in
// both), what is untracked and what a merge left in conflict, each with its mode and blob, and when it was read. At the main
// area's three widths (80, 39 and 25rem) in English and 中文, in both themes; and its states: on its way, no checkout here,
// refused, none, and read again.

const nothing = () => {};
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof ChangesTab> = {
  title: 'Repositories/ChangesTab',
  component: ChangesTab,
  args: { repository: 'engine', read: { state: 'read', value: GIT_CHANGES_ENGINE }, onRefresh: nothing },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof ChangesTab>;

/** At 80rem: the four lists, `notes.txt` in Staged as an addition and in Not staged as removed from disk. */
export const Wide: Story = {};
export const WideChinese: Story = { decorators: [chinese] };
export const WideDark: Story = { decorators: [dark] };
export const WideChineseDark: Story = { decorators: [chinese, dark] };

/** At 39rem, a 680 px window. */
export const Half: Story = { decorators: [storyWidths.half] };
export const HalfChinese: Story = { decorators: [storyWidths.half, chinese] };
export const HalfDark: Story = { decorators: [storyWidths.half, dark] };
export const HalfChineseDark: Story = { decorators: [storyWidths.half, chinese, dark] };

/** At 25rem, the main area's floor: paths wrap anywhere, and Refresh keeps its place beside the time. */
export const Narrow: Story = { decorators: [storyWidths.narrow] };
export const NarrowChinese: Story = { decorators: [storyWidths.narrow, chinese] };
export const NarrowDark: Story = { decorators: [storyWidths.narrow, dark] };
export const NarrowChineseDark: Story = { decorators: [storyWidths.narrow, chinese, dark] };

/** The held read shown while a fresh one is on its way: the time of the read stays, it says it reads again, and Refresh waits. */
export const ReadingAgain: Story = { args: { refreshing: true } };

/** The three observations agree. */
export const None: Story = { args: { read: { state: 'read', value: { ...GIT_CHANGES_ENGINE, staged: [], unstaged: [], untracked: [], unmerged: [] } } } };

/** The first read on its way. */
export const Reading: Story = { args: { read: { state: 'reading' } } };

/** A repository registered here by a teammate, with no checkout on this machine. */
export const NoCheckout: Story = { args: { read: { state: 'noCheckout' } } };

/** Git did not answer: the driver's sentence in the tab's place. */
export const Refused: Story = {
  args: { read: { state: 'refused', sentence: 'git could not read this checkout\'s changes: it may be busy, or git did not start.' } },
};
