import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { RenameRef } from './RenameRef';

// Rename a branch on a Yours row: the press, the new name asked for, what will happen confirmed,
// and refused in the driver's sentence. At the main area's three widths (80, 39 and 25rem) in English and 中文, in both themes.

const nothing = async () => ({ refused: false, said: '' });
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;
const COMMIT = '5d2e77a1c4f9b03e8d6a2b7c1f4e9d05a3b8c6e2';
const LONG = 'feature/a-name-long-enough-to-meet-the-floor-of-the-main-area';
const TAKEN = 'a branch named `spike/physics` already exists, so nothing was renamed';

const meta: Meta<typeof RenameRef> = {
  title: 'Repositories/RenameRef',
  component: RenameRef,
  args: {
    name: 'spike/renderer', commit: COMMIT, acts: { make: nothing, remove: nothing, rename: nothing, finish: nothing, undo: nothing }, onRenamed: () => {},
  },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof RenameRef>;

/** The press on a row, before anything is asked. */
export const Idle: Story = {};

/** At 80rem: the new name asked for, then what will happen before the press that sends it. */
export const Form: Story = { args: { initial: { step: 'form', to: 'spike/renderer-2' } } };
export const Confirm: Story = { args: { initial: { step: 'confirm', to: 'spike/renderer-2' } } };
export const ConfirmChinese: Story = { args: { initial: { step: 'confirm', to: 'spike/renderer-2' } }, decorators: [chinese] };
export const ConfirmDark: Story = { args: { initial: { step: 'confirm', to: 'spike/renderer-2' } }, decorators: [dark] };
export const ConfirmChineseDark: Story = { args: { initial: { step: 'confirm', to: 'spike/renderer-2' } }, decorators: [chinese, dark] };

/** At 39rem, a 680 px window. */
export const ConfirmHalf: Story = { args: { initial: { step: 'confirm', to: 'spike/renderer-2' } }, decorators: [storyWidths.half] };
export const ConfirmHalfChinese: Story = { args: { initial: { step: 'confirm', to: 'spike/renderer-2' } }, decorators: [storyWidths.half, chinese] };
export const ConfirmHalfDark: Story = { args: { initial: { step: 'confirm', to: 'spike/renderer-2' } }, decorators: [storyWidths.half, dark] };
export const ConfirmHalfChineseDark: Story = { args: { initial: { step: 'confirm', to: 'spike/renderer-2' } }, decorators: [storyWidths.half, chinese, dark] };

/** At 25rem, the main area's floor: a long name wraps anywhere, and the presses wrap under it. */
export const ConfirmNarrow: Story = { args: { name: LONG, initial: { step: 'confirm', to: `${LONG}-2` } }, decorators: [storyWidths.narrow] };
export const ConfirmNarrowChinese: Story = { args: { name: LONG, initial: { step: 'confirm', to: `${LONG}-2` } }, decorators: [storyWidths.narrow, chinese] };
export const ConfirmNarrowDark: Story = { args: { name: LONG, initial: { step: 'confirm', to: `${LONG}-2` } }, decorators: [storyWidths.narrow, dark] };
export const ConfirmNarrowChineseDark: Story = { args: { name: LONG, initial: { step: 'confirm', to: `${LONG}-2` } }, decorators: [storyWidths.narrow, chinese, dark] };

/** Refused in the driver's own sentence, the name kept to change. */
export const Refused: Story = { args: { initial: { step: 'said', to: 'spike/physics', sentence: TAKEN } } };
export const RefusedNarrowChineseDark: Story = {
  args: { name: LONG, initial: { step: 'said', to: 'spike/physics', sentence: TAKEN } },
  decorators: [storyWidths.narrow, chinese, dark],
};
