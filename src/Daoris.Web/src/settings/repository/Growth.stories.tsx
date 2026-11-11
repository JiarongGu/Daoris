import type { Meta, StoryObj } from '@storybook/react-vite';
import { storyWidths } from '../../design/storyWidths';
import { GrowthSetting } from './Growth';

// The growth switch, *New repositories and rooms*: whether an agent's press that opens a repository or a room runs as the access mode says, is asked
// even in full access, or is refused, a row of a workspace's Setup and of a repository's. The person's alone: no mode lets an agent press it.

const meta = {
  title: 'Settings/Growth',
  component: GrowthSetting,
  args: {
    name: 'engine', twin: '`daoris settings growth aurora --repository engine mode|asked|off`', value: 'mode', source: 'default', workspace: 'aurora', onChange: () => {},
  },
  // The main area at its wide width, as the frame gives it.
  decorators: [storyWidths.wide],
} satisfies Meta<typeof GrowthSetting>;
export default meta;

type Story = StoryObj<typeof meta>;

/** Nothing set: as the access mode says, Daoris's default. */
export const AsTheModeSays: Story = {};

/** Asked: a Confirm card even in full access, set for this repository. */
export const Asked: Story = { args: { value: 'asked', source: 'repository' } };

/** Off, from the workspace: refused even in full access, the repository inheriting it. */
export const OffFromTheWorkspace: Story = { args: { value: 'off', source: 'workspace' } };

/** A workspace's own row, off, at the 400 px floor. */
export const WorkspaceOffNarrow: Story = {
  args: { name: 'aurora', twin: '`daoris settings growth aurora mode|asked|off`', value: 'off', source: 'workspace', workspace: 'aurora' },
  decorators: [storyWidths.narrow],
};
