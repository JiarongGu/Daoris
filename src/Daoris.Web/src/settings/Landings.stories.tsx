import type { Meta, StoryObj } from '@storybook/react-vite';
import { LandingList } from './Landings';

// How work lands in each workspace (WSR1, D87), in the shape the driver's LINES answer takes, and since UX6f the line
// naming the repositories that set their own rule, each a door to its Setup, or that none does; and a machine with
// nothing to set one for.

const meta = {
  title: 'Settings/Landings',
  component: LandingList,
  args: { onSet: () => {}, onOpen: () => {}, workspaceLandings: [], landings: [] },
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

/**
 * LAND2a (D145): a workspace that accepts automatically through its plugin, and a repository whose own rule accepts
 * automatically with no plugin, named as a door to its Setup, where that rule is said.
 */
export const AcceptsAutomatically: Story = {
  args: {
    landers: ['azure-devops-pull-request'],
    workspaceLandings: [
      { workspace: 'work', form: 'branch', pattern: 'feature/{slug}-{quest}', tidy: true, plugin: 'azure-devops-pull-request', autoAccept: true },
    ],
    landings: [
      { repository: 'report-ui', workspace: 'work', form: 'branch', pattern: 'feature/{slug}-{quest}', tidy: true, plugin: 'azure-devops-pull-request', autoAccept: true, source: 'workspace' },
      { repository: 'reports-db', workspace: 'work', form: 'branch', pattern: 'review/{session}', autoAccept: true, source: 'repository' },
    ],
  },
};

export const NothingHere: Story = {};
