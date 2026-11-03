import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { KEPT, KEPT_ENDED } from './keptFixtures';
import { OldRecordsView } from './OldRecords';

// This machine's Old records (ONEWAY1ad-ii; the oneway design §1.3): the workflow part's census with the review's door, an old way ended and one
// kept for an older build, and the floor; then the part ended, a floor read and Undo refused. At the main area's 80, 39 and 25 rem, in 中文
// and in dark.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof OldRecordsView> = {
  title: 'Agents/OldRecords',
  component: OldRecordsView,
  args: { read: KEPT, onReview: () => {} },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof OldRecordsView>;

export const Kept: Story = {};
export const KeptHalf: Story = { decorators: [storyWidths.half] };
export const KeptNarrow: Story = { decorators: [storyWidths.narrow] };
export const KeptChinese: Story = { decorators: [chinese] };
export const KeptChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };
export const KeptDark: Story = { decorators: [dark] };

/** The workflow part has ended, a standing receipt gives the floor, and this build refuses the conversion's Undo. */
export const Ended: Story = { args: { read: KEPT_ENDED } };
export const EndedChinese: Story = { args: { read: KEPT_ENDED }, decorators: [chinese] };
