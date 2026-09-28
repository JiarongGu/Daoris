import type { Meta, StoryObj } from '@storybook/react-vite';
import { LandingList } from './Landings';

// How work lands (WSR1, D87), in the shape the driver's LINES answer takes: every source a rule can
// come from, and a machine with nothing to set one for.

const meta = {
  title: 'Settings/Landings',
  component: LandingList,
  args: { onSet: () => {}, workspaceLandings: [], landings: [] },
} satisfies Meta<typeof LandingList>;

export default meta;
type Story = StoryObj<typeof meta>;

export const EverySource: Story = {
  args: {
    workspaceLandings: [{ workspace: 'aurora', form: 'branch', pattern: 'feature/{quest}-{slug}' }],
    landings: [
      { repository: 'engine', workspace: 'aurora', form: 'merge', source: 'repository' },
      { repository: 'game', workspace: 'aurora', form: 'branch', pattern: 'feature/{quest}-{slug}', source: 'workspace' },
      { repository: 'tools', workspace: 'default', form: 'merge', source: 'default' },
    ],
  },
};

export const Saving: Story = { args: { ...EverySource.args, busy: true } };

export const NothingHere: Story = {};
