import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { EVIDENCE_ENTRIES, EVIDENCE_SESSIONS } from '../entries.fixtures';
import { PartsStrip } from './PartsStrip';

// A session's parts strip says which part is the head and how many sit beside it (LINESTOGETHER1a), and marks a part beside the
// head; where the driver answers no head, as an ask's siblings, it says none. At the main area's wide and narrow, in 中文 and dark.

const APP = EVIDENCE_ENTRIES[0]!;
const rows = Object.fromEntries(EVIDENCE_SESSIONS.map((session) => [session.id, { session, root: 'C:/work/app' }]));
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta = {
  title: 'Work/PartsStrip',
  component: PartsStrip,
  args: { entry: APP, attended: 'ap000003', rows, onShow: () => {} },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof PartsStrip>;
export default meta;

type Story = StoryObj<typeof meta>;

/** Part 2 is the head; part 3, asked by another repository, is beside it. */
export const HeadAndBeside: Story = {};
export const HeadAndBesideNarrow: Story = { decorators: [storyWidths.narrow] };

/** No head: an ask's siblings, each part its own line's. */
export const NoHead: Story = { args: { entry: { ...APP, head: null } } };

export const ChineseHead: Story = { decorators: [chinese, storyWidths.wide] };
export const ChineseHeadNarrow: Story = { decorators: [chinese, storyWidths.narrow] };
export const DarkHead: Story = { decorators: [dark, storyWidths.wide] };
export const DarkHeadNarrow: Story = { decorators: [dark, storyWidths.narrow] };
