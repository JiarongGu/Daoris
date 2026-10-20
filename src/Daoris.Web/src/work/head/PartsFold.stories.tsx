import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { EVIDENCE_ENTRIES, EVIDENCE_SESSIONS } from '../entries.fixtures';
import type { EntryAcceptPlan } from '../review/EntryAccept';
import { PartsFold } from './PartsFold';

// A session's parts folded to one line (HEADFIT1; the UI/UX design §3.7 and R4, the UX7 design §6.2): which part the page is on out of
// how many, how the newest began, and the press that opens the flowchart in its place. The flow's own stories (`PartsFlow`) draw the
// drawing at every width; these draw the fold at the main area's three (`storyWidths`) and the drawing opened from it.

const APP = EVIDENCE_ENTRIES[0]!;
const rows = Object.fromEntries(EVIDENCE_SESSIONS.map((session) => [session.id, { session, root: 'C:/work/app' }]));

const meta = {
  title: 'Work/PartsFold',
  component: PartsFold,
  args: { entry: APP, attended: 'ap000002', rows, onShow: () => {} },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof PartsFold>;
export default meta;

type Story = StoryObj<typeof meta>;

const PLAN: EntryAcceptPlan = {
  entry: APP.entry, repository: 'app', form: 'branch', target: 'daoris/t-one',
  parts: [{ session: 'ap000001' }, { session: 'ap000002' }, { session: 'ap000003', held: 'a gate holds it' }],
};

/** The line the page draws: one line where the main area is wide. */
export const Folded: Story = {};

/** At half, and at the main area's floor, where the line wraps. */
export const FoldedHalf: Story = { decorators: [storyWidths.half] };
export const FoldedNarrow: Story = { decorators: [storyWidths.narrow] };

/** The newest part is the one the page is on. */
export const FoldedOnTheNewest: Story = { args: { attended: 'ap000003' } };

/** In 中文, wide and at the floor. */
export const ChineseFolded: Story = { decorators: [chinese, storyWidths.wide] };
export const ChineseNarrow: Story = { decorators: [chinese, storyWidths.narrow] };

/** Opened in place: the flowchart where it fits, with the press back to the line under it. */
export const Opened: Story = { args: { defaultOpen: true } };

/** Opened at the floor, where the strip is the flow's rail. */
export const OpenedNarrow: Story = { args: { defaultOpen: true }, decorators: [storyWidths.narrow] };

/** Opened once Accept's ask is read: the dashed node where the parts would land. */
export const OpenedWithThePlan: Story = { args: { defaultOpen: true, plan: PLAN } };
