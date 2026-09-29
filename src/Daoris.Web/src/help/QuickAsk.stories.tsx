import type { Meta, StoryObj } from '@storybook/react-vite';
import { AskPanel } from './AskPanel';
import { QuickAsk } from './QuickAsk';

// Quick Ask (DOCK1d): Ask Daoris's conversation in a box at the palette's place. The box holds the
// same unframed panel the side bar holds; here it holds the panel on a machine that lacks nothing.

const meta = {
  title: 'Help/QuickAsk',
  component: QuickAsk,
  args: {
    open: true,
    onClose: () => {},
    onExpand: () => {},
    children: <AskPanel framed={false} helper="claude-code-acp" starters={[]} onGo={() => {}} onClose={() => {}} />,
  },
} satisfies Meta<typeof QuickAsk>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Open, before anything is asked: the introduction, and the box its question goes in. */
export const Open: Story = {};
