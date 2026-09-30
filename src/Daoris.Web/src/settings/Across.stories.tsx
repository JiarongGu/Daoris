import type { Meta, StoryObj } from '@storybook/react-vite';
import { AcrossList } from './Across';

// Reading and writing across (D107), in the shape the driver's ACROSS answer takes: every source a reading
// can come from, a declared relationship, a repository with no checkout here, and a machine with nothing.

const meta = {
  title: 'Settings/Across',
  component: AcrossList,
  args: { onRead: () => {}, onWrite: () => {}, workspaceReads: [], repositories: [] },
} satisfies Meta<typeof AcrossList>;

export default meta;
type Story = StoryObj<typeof meta>;

export const EverySource: Story = {
  args: {
    workspaceReads: [{ workspace: 'forge', read: false }],
    repositories: [
      { repository: 'engine', workspace: 'aurora', checkout: true, read: false, source: 'repository', writesTo: [] },
      { repository: 'game', workspace: 'aurora', checkout: true, read: true, source: 'default', writesTo: [] },
      { repository: 'plugins', workspace: 'aurora', checkout: true, read: true, source: 'default', writesTo: ['engine'] },
      { repository: 'tools', workspace: 'forge', checkout: false, read: false, source: 'workspace', writesTo: [] },
    ],
  },
};

export const Saving: Story = { args: { ...EverySource.args, busy: true } };

export const NothingHere: Story = {};
