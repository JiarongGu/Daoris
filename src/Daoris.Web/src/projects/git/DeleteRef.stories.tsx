import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { DeleteRef } from './DeleteRef';

// Delete a branch (GITAPP2d, D204, the git-writes design §6) on a Yours row: the press, what will go confirmed, and refused in the driver's
// sentence. At the main area's three widths (80, 39 and 25rem) in English and 中文, in both themes.

const nothing = async () => ({ refused: false, said: '' });
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;
const COMMIT = '5d2e77a1c4f9b03e8d6a2b7c1f4e9d05a3b8c6e2';
const LONG = 'feature/a-name-long-enough-to-meet-the-floor-of-the-main-area';
const CHECKED_OUT = '`spike/renderer` is checked out in `engine-tree`, so nothing was deleted';

const meta: Meta<typeof DeleteRef> = {
  title: 'Repositories/DeleteRef',
  component: DeleteRef,
  args: { name: 'spike/renderer', commit: COMMIT, acts: { make: nothing, remove: nothing, undo: nothing }, onDeleted: () => {} },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof DeleteRef>;

/** The press on a row, before anything is asked. */
export const Idle: Story = {};

/** At 80rem: what will go and where its commits stay, before the press that sends it. */
export const Confirm: Story = { args: { initial: { step: 'confirm' } } };
export const ConfirmChinese: Story = { args: { initial: { step: 'confirm' } }, decorators: [chinese] };
export const ConfirmDark: Story = { args: { initial: { step: 'confirm' } }, decorators: [dark] };
export const ConfirmChineseDark: Story = { args: { initial: { step: 'confirm' } }, decorators: [chinese, dark] };

/** At 39rem, a 680 px window. */
export const ConfirmHalf: Story = { args: { initial: { step: 'confirm' } }, decorators: [storyWidths.half] };
export const ConfirmHalfChinese: Story = { args: { initial: { step: 'confirm' } }, decorators: [storyWidths.half, chinese] };
export const ConfirmHalfDark: Story = { args: { initial: { step: 'confirm' } }, decorators: [storyWidths.half, dark] };
export const ConfirmHalfChineseDark: Story = { args: { initial: { step: 'confirm' } }, decorators: [storyWidths.half, chinese, dark] };

/** At 25rem, the main area's floor: a long name wraps anywhere, and the presses wrap under it. */
export const ConfirmNarrow: Story = { args: { name: LONG, initial: { step: 'confirm' } }, decorators: [storyWidths.narrow] };
export const ConfirmNarrowChinese: Story = { args: { name: LONG, initial: { step: 'confirm' } }, decorators: [storyWidths.narrow, chinese] };
export const ConfirmNarrowDark: Story = { args: { name: LONG, initial: { step: 'confirm' } }, decorators: [storyWidths.narrow, dark] };
export const ConfirmNarrowChineseDark: Story = { args: { name: LONG, initial: { step: 'confirm' } }, decorators: [storyWidths.narrow, chinese, dark] };

/** Refused in the driver's own sentence. */
export const Refused: Story = { args: { initial: { step: 'said', sentence: CHECKED_OUT } } };
export const RefusedNarrowChineseDark: Story = {
  args: { name: LONG, initial: { step: 'said', sentence: CHECKED_OUT } },
  decorators: [storyWidths.narrow, chinese, dark],
};
