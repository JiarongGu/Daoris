import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { Renamed } from './Renamed';

// What a rename left, with its Finish and Undo: kept by the list beyond the renamed row. Renamed, stopped before the
// branch's settings moved, undone, refused in the driver's sentence and not known, at the main area's three widths (80, 39 and 25rem)
// in English and 中文, in both themes.

const nothing = async () => ({ refused: false, said: '' });
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;
const LONG = 'feature/a-name-long-enough-to-meet-the-floor-of-the-main-area';

const meta: Meta<typeof Renamed> = {
  title: 'Repositories/Renamed',
  component: Renamed,
  args: {
    name: 'spike/renderer', to: 'spike/renderer-2', act: 'r1',
    acts: { make: nothing, remove: nothing, rename: nothing, finish: nothing, undo: nothing }, onClose: () => {},
  },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof Renamed>;

/** At 80rem: a done rename with its Undo. */
export const Wide: Story = {};
export const WideChinese: Story = { decorators: [chinese] };
export const WideDark: Story = { decorators: [dark] };
export const WideChineseDark: Story = { decorators: [chinese, dark] };

/** A rename stopped between the branch and its settings: Finish and Undo. */
export const Partial: Story = { args: { partial: true } };
export const PartialChinese: Story = { args: { partial: true }, decorators: [chinese] };
export const PartialDark: Story = { args: { partial: true }, decorators: [dark] };
export const PartialChineseDark: Story = { args: { partial: true }, decorators: [chinese, dark] };

/** At 39rem, a 680 px window. */
export const Half: Story = { args: { partial: true }, decorators: [storyWidths.half] };
export const HalfChinese: Story = { args: { partial: true }, decorators: [storyWidths.half, chinese] };
export const HalfDark: Story = { args: { partial: true }, decorators: [storyWidths.half, dark] };
export const HalfChineseDark: Story = { args: { partial: true }, decorators: [storyWidths.half, chinese, dark] };

/** At 25rem, the main area's floor: a long name wraps anywhere, and the presses wrap under it. */
export const Narrow: Story = { args: { name: LONG, to: `${LONG}-2`, partial: true }, decorators: [storyWidths.narrow] };
export const NarrowChinese: Story = { args: { name: LONG, to: `${LONG}-2`, partial: true }, decorators: [storyWidths.narrow, chinese] };
export const NarrowDark: Story = { args: { name: LONG, to: `${LONG}-2`, partial: true }, decorators: [storyWidths.narrow, dark] };
export const NarrowChineseDark: Story = { args: { name: LONG, to: `${LONG}-2`, partial: true }, decorators: [storyWidths.narrow, chinese, dark] };

/** Undone, and refused in the driver's own sentence. */
export const Undone: Story = { args: { initial: { step: 'undone' } } };
export const Refused: Story = {
  args: { partial: true, initial: { step: 'said', sentence: 'the `branch.spike/renderer` section is gone or changed since act `r1` read it, so it was not moved' } },
};
export const RefusedNarrowChineseDark: Story = {
  args: { name: LONG, to: `${LONG}-2`, partial: true, initial: { step: 'said', sentence: 'the section changed, so it was not moved' } },
  decorators: [storyWidths.narrow, chinese, dark],
};
export const Unknown: Story = { args: { initial: { step: 'unknown' } } };
