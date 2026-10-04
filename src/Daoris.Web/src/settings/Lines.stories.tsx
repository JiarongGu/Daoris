import type { Meta, StoryObj } from '@storybook/react-vite';
import { LineList } from './Lines';

// Each workspace's line (WSR2), in the shape the driver's LINES answer takes, and since UX6f the line naming the
// repositories that set their own, each a door to its Setup, or that none does; and a machine with nothing to set a line
// for.

const meta = {
  title: 'Settings/Lines',
  component: LineList,
  args: { onSet: () => {}, onOpen: () => {}, workspaceLines: [], lines: [] },
} satisfies Meta<typeof LineList>;

export default meta;
type Story = StoryObj<typeof meta>;

export const EverySource: Story = {
  args: {
    workspaceLines: [{ workspace: 'aurora', branch: 'develop' }],
    lines: [
      { repository: 'engine', workspace: 'aurora', branch: 'release/2026.09', source: 'repository' },
      { repository: 'game', workspace: 'aurora', branch: 'develop', source: 'workspace' },
      { repository: 'tools', workspace: 'default', branch: 'main', source: 'checkout' },
      { repository: 'remote-only', workspace: 'default', source: 'none' },
    ],
  },
};

export const Saving: Story = { args: { ...EverySource.args, busy: true } };

export const NothingHere: Story = {};
