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

/** WSR4 (D100): a branch rule handed to a plugin installed here, which pushes it and opens the pull request. */
export const HandedToAPlugin: Story = {
  args: {
    landers: ['github-pull-request', 'azure-devops-pull-request'],
    workspaceLandings: [{ workspace: 'aurora', form: 'branch', pattern: 'feature/{quest}-{slug}', plugin: 'github-pull-request' }],
    landings: [
      { repository: 'engine', workspace: 'aurora', form: 'branch', pattern: 'feature/{quest}-{slug}', plugin: 'github-pull-request', source: 'workspace' },
      { repository: 'game', workspace: 'aurora', form: 'branch', pattern: 'review/{session}', source: 'repository' },
    ],
  },
};

export const NothingHere: Story = {};
