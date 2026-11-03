import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { APPLIED, MOVED, NOTHING, planOf, UNDO_OFFERED } from './convertFixtures';
import { DEFAULT_CHOICES } from './convertPlan';
import { ConvertReview } from './ConvertReview';

// The conversion's review (WORKFLOWCLEAN1g-i; the cleanup design §4.3): what converting changes, the two choices, what stays and what it never
// touches, the terminal line and one Apply; the done state with its outcomes and Undo; a moved plan. At the main area's 80, 39 and 25 rem, in
// 中文 and in dark. Two columns at 80 rem and one below it.

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof ConvertReview> = {
  title: 'Agents/ConvertReview',
  component: ConvertReview,
  args: {
    plan: planOf(DEFAULT_CHOICES), choices: DEFAULT_CHOICES, reading: false, stale: false, problem: null, applying: false, undoing: false,
    result: null, undo: null, undone: null, onChoose: nothing, onApply: nothing, onUndo: nothing, onClose: nothing,
  },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof ConvertReview>;

export const Review: Story = {};
export const ReviewHalf: Story = { decorators: [storyWidths.half] };
export const ReviewNarrow: Story = { decorators: [storyWidths.narrow] };
export const ReviewChinese: Story = { decorators: [chinese] };
export const ReviewChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const ReviewChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };
export const ReviewDark: Story = { decorators: [dark] };
export const ReviewDarkHalf: Story = { decorators: [storyWidths.half, dark] };
export const ReviewDarkNarrow: Story = { decorators: [storyWidths.narrow, dark] };

/** The choices that lower your part: the lowering is said, in the driver's words. */
const LOWERING = { composed: 'review', landAutomatic: 'none' } as const;
export const Lowering: Story = { args: { plan: planOf(LOWERING), choices: LOWERING } };
export const LoweringNarrow: Story = { ...Lowering, decorators: [storyWidths.narrow] };

/** The last plan stays, marked updating, while the next is read; Apply waits. */
export const Updating: Story = { args: { reading: true, stale: true } };

/** Done: each operation with its outcome, and Undo while the driver offers it. */
const DONE = { plan: NOTHING, result: APPLIED, undo: UNDO_OFFERED };
export const Applied: Story = { args: DONE };
export const AppliedHalf: Story = { args: DONE, decorators: [storyWidths.half] };
export const AppliedNarrow: Story = { args: DONE, decorators: [storyWidths.narrow] };
export const AppliedChinese: Story = { args: DONE, decorators: [chinese] };
export const AppliedDark: Story = { args: DONE, decorators: [dark] };

/** The plan moved since it was read: the driver's sentence, and nothing written. */
export const Moved: Story = { args: { result: MOVED } };
export const MovedNarrow: Story = { args: { result: MOVED }, decorators: [storyWidths.narrow] };

/** The census has ended, and the conversion can still be undone. */
export const UndoOffered: Story = { args: { plan: NOTHING, undo: UNDO_OFFERED } };
export const UndoOfferedNarrow: Story = { ...UndoOffered, decorators: [storyWidths.narrow] };

/** The driver could not be asked. */
export const Refused: Story = { args: { plan: null, problem: 'The driver is still coming up — try again in a moment.' } };

/** Keyboard: focus rests on Apply, whose ring shows. */
export const ApplyFocused: Story = {
  play: async ({ canvasElement }) => {
    canvasElement.querySelector<HTMLButtonElement>('[data-review-apply]')?.focus();
  },
};
