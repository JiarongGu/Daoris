import type { Meta, StoryObj } from '@storybook/react-vite';
import { useMemo, useState } from 'react';
import { inDark } from '../flowFrames';
import { chinese } from '../storyLanguage';
import { storyWidths } from '../storyWidths';
import { syntheticScene } from './canvasFixtures';
import { PositionedCanvas } from './PositionedCanvas';

// The large positioned worlds: synthetic scenes of 100, 1,000, 5,000 and 10,000 nodes with 50,000
// edges, in the viewport at the main area's 80, 39 and 25rem, in English and 中文 and both themes. At 25rem, below the 36rem floor, the
// graph is the grouped list. Past 1,000 nodes a grouped world is its overview: a card per group, the lines between groups counted, one
// group opened at a time. Keyboard: Tab reaches the toolbar, find (from 40 nodes), then the world's one stop; the arrows walk to the
// nearest node on that side, mounted or not, and the view follows. Frame times are measured on these (`measured/large.spec.ts`).

function LargeWorld({ count, groups = 1, edgesPerNode = 1.5, feature = 'story' }: { count: number; groups?: number; edgesPerNode?: number; feature?: string }) {
  const scene = useMemo(() => syntheticScene(count, { groups, edgesPerNode }), [count, groups, edgesPerNode]);
  const [selected, setSelected] = useState<string | null>(null);
  return (
    <PositionedCanvas scene={scene} label={`${count} steps`} feature={feature} storeKey={`${count}-${groups}`} selected={selected} onSelect={setSelected} />
  );
}

const meta: Meta<typeof LargeWorld> = {
  title: 'Design/Canvas/Large',
  component: LargeWorld,
  args: { count: 100 },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof LargeWorld>;

/** A hundred nodes: every one at the full tier, nothing culled, find, the minimap by its rule. */
export const Hundred: Story = {};
export const HundredHalf: Story = { decorators: [storyWidths.half] };
export const HundredNarrow: Story = { decorators: [storyWidths.narrow] };
export const HundredChinese: Story = { decorators: [storyWidths.wide, chinese] };
export const HundredChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const HundredDark: Story = { decorators: [storyWidths.wide, inDark] };
export const HundredChineseDarkNarrow: Story = { decorators: [storyWidths.narrow, chinese, inDark] };

/** A thousand nodes in one world: culled past 400, chips and dots as the zoom drops. */
export const Thousand: Story = { args: { count: 1000 } };
export const ThousandHalf: Story = { args: { count: 1000 }, decorators: [storyWidths.half] };
export const ThousandChineseDark: Story = { args: { count: 1000 }, decorators: [storyWidths.wide, chinese, inDark] };

/** Five thousand nodes in fifty groups: the overview, a card per group. */
export const Five: Story = { args: { count: 5000, groups: 50 } };
export const FiveHalf: Story = { args: { count: 5000, groups: 50 }, decorators: [storyWidths.half] };
export const FiveNarrow: Story = { args: { count: 5000, groups: 50 }, decorators: [storyWidths.narrow] };
export const FiveChineseDark: Story = { args: { count: 5000, groups: 50 }, decorators: [storyWidths.wide, chinese, inDark] };

/** Ten thousand nodes and fifty thousand edges in a hundred groups. */
export const Ten: Story = { args: { count: 10_000, groups: 100, edgesPerNode: 5 } };
