import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import type { GitDeletedRef } from './DeletedRefs';
import { DeletedRefs } from './DeletedRefs';

// A repository's deletes as the journal holds them (ONEWAY1r, D204): who pressed each, its kept commits, and its Undo open, shut or refused,
// at the main area's three widths (80, 39 and 25rem) in English and 中文, in both themes.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;
const at = (hours: number) => new Date(Date.now() - hours * 3_600_000).toISOString();
const ROWS: GitDeletedRef[] = [
  { act: 'd1', kind: 'branch', name: 'daoris/engine-physics-step', commit: 'a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2', at: at(2), press: 'daoris', recovery: 'refs/daoris/discarded/engine-physics-step', closed: null },
  { act: 'd2', kind: 'branch', name: 'spike/renderer', commit: 'b1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2', at: at(30), press: 'window', recovery: null, closed: 'name-taken' },
  { act: 'd3', kind: 'tag', name: 'v0.9-rc1', commit: 'c1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2', at: at(80), press: 'terminal', recovery: null, closed: 'commit-gone' },
];
const undo = async () => ({ refused: false, outcome: 'done', said: '' });

const meta: Meta<typeof DeletedRefs> = {
  title: 'Repositories/DeletedRefs',
  component: DeletedRefs,
  args: { rows: ROWS, acts: { undo } },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof DeletedRefs>;

/** At 80rem. */
export const Wide: Story = {};
export const WideChinese: Story = { decorators: [chinese] };
export const WideDark: Story = { decorators: [dark] };
export const WideChineseDark: Story = { decorators: [chinese, dark] };

/** At 39rem, a 680 px window. */
export const Half: Story = { decorators: [storyWidths.half] };
export const HalfChinese: Story = { decorators: [storyWidths.half, chinese] };

/** At 25rem, the main area's floor: a long name wraps anywhere. */
export const Narrow: Story = {
  args: { rows: [{ ...ROWS[0]!, name: 'feature/a-name-long-enough-to-meet-the-floor-of-the-main-area' }] },
  decorators: [storyWidths.narrow],
};
export const NarrowChineseDark: Story = { args: Narrow.args, decorators: [storyWidths.narrow, chinese, dark] };

/** An Undo the driver refused, in its sentence. */
export const Refused: Story = {
  args: { acts: { undo: async () => ({ refused: true, said: 'a branch named `spike/renderer` already exists, so act `d1` was not undone' }) } },
};
