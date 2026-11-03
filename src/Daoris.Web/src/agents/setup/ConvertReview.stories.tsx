import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { APPLIED, FAILED_PART, MOVED, NOTHING, PARTED, planOf, UNDO_OFFERED } from './convertFixtures';
import { DEFAULT_CHOICES } from './convertPlan';
import { ConvertReview } from './ConvertReview';

// The conversion's review (WORKFLOWCLEAN1g-i, ONEWAY1ae; the cleanup design §4.3, the oneway design §1.1): what converting changes, the two choices,
// what stays and what it never touches, what is removed for good, the terminal line and one Apply; a second part and a part not read; the done
// state with its outcomes and Undo; a failed part pressed again or left; a moved plan. At the main area's 80, 39 and 25 rem, in 中文 and in dark.
// Two columns at 80 rem and one below it.

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof ConvertReview> = {
  title: 'Agents/ConvertReview',
  component: ConvertReview,
  args: {
    plan: planOf(DEFAULT_CHOICES), choices: DEFAULT_CHOICES, reading: false, stale: false, problem: null, applying: false, undoing: false,
    result: null, undo: null, undone: null, onChoose: nothing, onApply: nothing, onAgain: nothing, onLeave: nothing, onUndo: nothing, onClose: nothing,
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

/** ONEWAY1ae: a second part said by its lines, a part not read, and what is removed for good, counted, dated and sized. */
export const Parts: Story = { args: { plan: PARTED } };
export const PartsHalf: Story = { ...Parts, decorators: [storyWidths.half] };
export const PartsNarrow: Story = { ...Parts, decorators: [storyWidths.narrow] };
export const PartsChinese: Story = { ...Parts, decorators: [chinese] };
export const PartsChineseNarrow: Story = { ...Parts, decorators: [storyWidths.narrow, chinese] };

/** ONEWAY1ae: a receipt with a failed part: each part's outcome, Leave it on the failed one, and the receipt pressed again. */
export const FailedPart: Story = { args: { plan: PARTED, result: FAILED_PART } };
export const FailedPartNarrow: Story = { ...FailedPart, decorators: [storyWidths.narrow] };
export const FailedPartChinese: Story = { ...FailedPart, decorators: [chinese] };

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
