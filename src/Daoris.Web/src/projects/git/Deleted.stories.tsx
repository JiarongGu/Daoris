import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { Deleted } from './Deleted';

// What a delete left, with its Undo (GITAPP2d, D204): kept by the list beyond the deleted row. Deleted, undone, refused in the driver's
// sentence and not known, at the main area's three widths (80, 39 and 25rem) in English and 中文, in both themes.

const nothing = async () => ({ refused: false, said: '' });
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;
const LONG = 'feature/a-name-long-enough-to-meet-the-floor-of-the-main-area';

const meta: Meta<typeof Deleted> = {
  title: 'Repositories/Deleted',
  component: Deleted,
  args: { name: 'spike/renderer', act: 'd1', acts: { make: nothing, remove: nothing, rename: nothing, finish: nothing, undo: nothing }, onClose: () => {} },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof Deleted>;

/** At 80rem. */
export const Wide: Story = {};
export const WideChinese: Story = { decorators: [chinese] };
export const WideDark: Story = { decorators: [dark] };
export const WideChineseDark: Story = { decorators: [chinese, dark] };

/** At 39rem, a 680 px window. */
export const Half: Story = { decorators: [storyWidths.half] };
export const HalfChinese: Story = { decorators: [storyWidths.half, chinese] };
export const HalfDark: Story = { decorators: [storyWidths.half, dark] };
export const HalfChineseDark: Story = { decorators: [storyWidths.half, chinese, dark] };

/** At 25rem, the main area's floor: a long name wraps anywhere, and the presses wrap under it. */
export const Narrow: Story = { args: { name: LONG }, decorators: [storyWidths.narrow] };
export const NarrowChinese: Story = { args: { name: LONG }, decorators: [storyWidths.narrow, chinese] };
export const NarrowDark: Story = { args: { name: LONG }, decorators: [storyWidths.narrow, dark] };
export const NarrowChineseDark: Story = { args: { name: LONG }, decorators: [storyWidths.narrow, chinese, dark] };

/** Undone: the branch is back, no Undo left. */
export const Undone: Story = { args: { initial: { step: 'undone' } } };

/** The undo refused, in the driver's sentence. */
export const Refused: Story = {
  args: { initial: { step: 'said', sentence: 'a branch named `spike/renderer` already exists, so act `d1` was not undone' } },
  decorators: [storyWidths.narrow, dark],
};

/** Not known: said, and nothing retried. */
export const Unknown: Story = { args: { initial: { step: 'unknown' } }, decorators: [chinese] };
