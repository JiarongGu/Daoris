import type { Meta, StoryObj } from '@storybook/react-vite';
import { StartWiringList } from './StartWiring';
import type { StartWiring } from './wiring';

// What a start in each workspace would run on (MAP1b), in the shapes the driver answers with.

const start = (extra: Partial<StartWiring>): StartWiring => ({
  job: 'work', workspace: 'default', adapter: 'claude-code', owner: 'claude-code', product: 'Claude Code',
  profile: 'account-1', profileFrom: 'machine', version: '2.1.270', versionFrom: 'unset',
  commanded: false, refusal: null, ...extra,
});

const NAMES: Record<string, string> = { 'account-1': 'someone@example.invalid', 'account-2': 'API key …wxyz' };

const meta: Meta<typeof StartWiringList> = {
  title: 'Map/StartWiringList',
  component: StartWiringList,
  args: {
    nameOf: (_owner, profile) => NAMES[profile] ?? profile,
    starts: [
      start({ workspace: 'aurora', profile: 'account-2', profileFrom: 'workspace', versionFrom: 'machine' }),
      start({}),
    ],
  },
  decorators: [(Story) => <div className="max-w-xl"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof StartWiringList>;

/** Two circles, one with its own account and a pinned version. */
export const TwoWorkspaces: Story = {};

/** Nothing set anywhere: the agent's own sign-in, and whatever PATH has. */
export const NothingSet: Story = {
  args: { starts: [start({ profile: null, profileFrom: 'unset' })] },
};

/** A start the driver would hold, in its own words. */
export const Held: Story = {
  args: {
    starts: [start({
      version: '9.9.9', versionFrom: 'machine',
      refusal: '`claude-code` is pinned to 9.9.9 on this machine, and nothing is installed at that version — `daoris agent pin claude-code 9.9.9` installs it, and `daoris agent unpin claude-code` goes back to PATH.',
    })],
  },
};

/** An agent named for the intake (AGT6): the circle has two jobs, and each row says which it is. */
export const WithAnIntake: Story = {
  args: {
    starts: [
      start({}),
      start({ job: 'intake', adapter: 'claude-code-acp', version: '0.9.1' }),
    ],
  },
};

/** A door onto the tool (AGT7): the account is the tool's, the adapter is the door. */
export const ThroughTheProtocolDoor: Story = {
  args: { starts: [start({ adapter: 'claude-code-acp', version: '0.9.1' })] },
};
