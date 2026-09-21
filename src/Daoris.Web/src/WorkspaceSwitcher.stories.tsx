import type { Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { WorkspaceSwitcher } from './WorkspaceSwitcher';

// Every state of the scope control, on the shipped component (D42 §5): the two the console shows
// daily, the crowded one, and the absence — because "renders nothing with one workspace" is a state a
// reviewer must be able to see too.

const meta: Meta<typeof WorkspaceSwitcher> = {
  title: 'Shell/WorkspaceSwitcher',
  component: WorkspaceSwitcher,
  decorators: [(Story) => <Tooltip.Provider><div className="w-52"><Story /></div></Tooltip.Provider>],
  args: { onChange: () => {} },
};
export default meta;

type Story = StoryObj<typeof WorkspaceSwitcher>;

/** Two circles, nothing chosen: the console spans both and says so. */
export const Every: Story = { args: { workspaces: ['default', 'aurora'], value: null } };

/** One circle chosen: every cross-repository query now carries it. */
export const Chosen: Story = { args: { workspaces: ['default', 'aurora'], value: 'aurora' } };

/** Many circles, long and CJK names among them — the menu, not the trigger, absorbs the length. */
export const Many: Story = {
  args: {
    workspaces: ['default', 'aurora', '工作区', 'platform-infrastructure-and-tooling', 'games'],
    value: '工作区',
  },
};

/** One circle: the control is absent, and this story is the place to see that it is. */
export const Single: Story = {
  args: { workspaces: ['default'], value: null },
  render: (args) => (
    <div className="text-[0.8rem] text-ink-faint">
      <WorkspaceSwitcher {...args} />
      <span>(one workspace — nothing to switch, so nothing renders above this line)</span>
    </div>
  ),
};
