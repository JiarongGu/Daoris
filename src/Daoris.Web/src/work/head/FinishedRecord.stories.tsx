import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { RECORD } from './sessionRecord.fixtures';
import { FinishedRecord } from './FinishedRecord';

// KEEPSIZE1: the record of a finished session's tree, shown on its page once the tree is let go: kept whole, then with no ref and its
// commits unread, at the half and the narrow width, in English and 中文.

const meta = {
  title: 'Work/FinishedRecord',
  component: FinishedRecord,
  args: { record: RECORD },
  decorators: [storyWidths.half],
} satisfies Meta<typeof FinishedRecord>;

export default meta;
type Story = StoryObj<typeof meta>;

/** The record kept whole: landing, tip, the recovery ref, the commits and what no commit holds. */
export const Kept: Story = {};

/** Where no ref holds the tip and the commits could no longer be read. */
export const Unread: Story = { args: { record: { ...RECORD, post: { ...RECORD.post, ref: null, commits: null, landing: null } } } };

/** At the 400 px floor. */
export const KeptNarrow: Story = { decorators: [storyWidths.narrow] };

/** In 中文, narrow. */
export const KeptChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };
