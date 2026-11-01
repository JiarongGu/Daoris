import type { Meta, StoryObj } from '@storybook/react-vite';
import { atWidths, inDark, inLight } from '../../design/flowFrames';
import { chinese } from '../../design/storyLanguage';
import { AtWorkAskView } from './AtWorkAskView';
import { ASK_EMPTY, ASK_SAID, ASK_TEAMMATE, ASK_TWO_ENTRIES, CONSIDERED, FAILED, READING } from './atWork.fixtures';
import type { AtWorkHeld } from './types';

// Who is at work on an ask (COWORKVIEW1d, the cowork view design §3.5, §5.3): the section above the ask page's quests, at the main area's
// three widths (80, 39 and 25rem: `storyWidths.tsx`) and the side bar's 300 px floor, each in English in light and in 中文 in dark.

const nothing = () => {};

const meta: Meta<typeof AtWorkAskView> = {
  title: 'Work/AtWorkAskView',
  component: AtWorkAskView,
  args: { held: ASK_TWO_ENTRIES, considered: CONSIDERED, onOpenEntry: nothing, onOpenQuest: nothing, onRetry: nothing },
};
export default meta;

type Story = StoryObj<typeof AtWorkAskView>;

const WIDTHS = [300, 400, 624, 1280] as const;
const light = [atWidths(WIDTHS), inLight];
const zhDark = [chinese, atWidths(WIDTHS), inDark];

/** An ask with two lines of work, each folded to a count that opens its own view, and a quest untaken with the tick's reason. */
export const TwoEntries: Story = { decorators: light };
export const TwoEntriesChinese: Story = { decorators: zhDark };

/** The same, with the ask's channel: a part's message on its line of work, the person's under *On the channel*. */
export const TwoEntriesAndChannel: Story = { args: { channel: { words: ASK_SAID, onOpen: nothing } }, decorators: light };
export const TwoEntriesAndChannelChinese: Story = { args: { channel: { words: ASK_SAID, onOpen: nothing } }, decorators: zhDark };

/** An ask the asks slice holds with nothing at work: valid and empty. */
export const NothingAtWork: Story = { args: { held: ASK_EMPTY }, decorators: light };
export const NothingAtWorkChinese: Story = { args: { held: ASK_EMPTY }, decorators: zhDark };

/** A teammate's part among a line's: its subagents are not visible on this machine. */
export const TeammatePart: Story = { args: { held: ASK_TEAMMATE }, decorators: light };

/** Reading, failed, and stale over a held answer. */
export const Reading: Story = { args: { held: READING }, decorators: light };
export const Failed: Story = { args: { held: FAILED }, decorators: light };
export const Stale: Story = {
  args: { held: { ...(ASK_TWO_ENTRIES as Extract<AtWorkHeld, { askAnswer: unknown }>), stale: { why: 'the service timed out' } } },
  decorators: light,
};
