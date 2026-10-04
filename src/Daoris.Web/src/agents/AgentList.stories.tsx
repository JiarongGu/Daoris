import type { Meta, StoryObj } from '@storybook/react-vite';
import { agentRows } from './agents';
import { AgentList, AgentStrip } from './AgentList';
import { CLAUDE_USE, CODEX_USE, TOOLS } from './agentsFixtures';

// The Agents place's list (UX6e, D150 §5.1): each agent once, its accounts in a phrase, the waiting mark on one with an
// account the person must act on, one not installed shown when the list's ⋯ asks; and the list closed to its strip.

const ROWS = agentRows(TOOLS, { agents: [CLAUDE_USE, CODEX_USE] });
const nothing = () => {};

const meta: Meta<typeof AgentList> = {
  title: 'Agents/AgentList',
  component: AgentList,
  args: { rows: ROWS.filter((row) => row.installed), chosen: 'claude-code', onChoose: nothing },
  decorators: [(Story) => <div className="w-[280px] border border-line bg-page"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof AgentList>;

/** Installed agents: Claude Code with two signed-out accounts that hold work, Codex with one, a plugin's agent. */
export const Installed: Story = {};

/** With the agents not installed shown, from the list's ⋯: dsh says so. */
export const WithNotInstalled: Story = { args: { rows: ROWS } };

/** Nothing chosen. */
export const NothingChosen: Story = { args: { chosen: null } };

/** Closed to its strip: each agent's initial, the waiting mark, one not installed faint. */
export const Strip: StoryObj<typeof AgentStrip> = {
  render: () => (
    <div className="w-[56px] border border-line bg-page">
      <AgentStrip rows={ROWS} chosen="claude-code" onChoose={nothing} />
    </div>
  ),
};
