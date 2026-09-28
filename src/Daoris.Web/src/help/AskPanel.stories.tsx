import type { Meta, StoryObj } from '@storybook/react-vite';
import { AskPanel } from './AskPanel';
import { starters } from './starters';

// Ask Daoris (HELP1, D89): its panel on a machine that lacks everything, on one that lacks nothing,
// and in Chinese widths by the same props.

const meta = {
  title: 'Help/AskPanel',
  component: AskPanel,
  args: { onGo: () => {}, onClose: () => {}, helper: null, starters: [] },
} satisfies Meta<typeof AskPanel>;

export default meta;
type Story = StoryObj<typeof meta>;

export const LacksEverything: Story = {
  args: {
    starters: starters({
      repositories: [], drivable: [], waiting: 2, unnamedLines: [], helper: null,
      tools: [{
        name: 'claude-code', product: 'Claude Code', maker: 'Anthropic', doors: [], accounts: [], machineDefault: null,
        present: true, ownLogin: 'out', ownAccount: null, workspaceDefaults: [],
      }],
    }),
  },
};

export const OneLine: Story = {
  args: {
    helper: 'claude-code-acp',
    starters: starters({
      repositories: ['engine', 'game'], drivable: ['engine'], tools: [], waiting: 0,
      unnamedLines: ['newbie'], helper: 'claude-code-acp',
    }),
  },
};

export const LacksNothing: Story = { args: { helper: 'claude-code-acp' } };

/** HELP1a: the conversation takes the panel, and the composer sits at its foot. The record's own look is the conversation's stories'. */
export const Talking: Story = {
  args: {
    helper: 'claude-code-acp',
    conversation: {
      body: (
        <div className="grid gap-3 text-small text-ink">
          <p className="m-0 rounded-control bg-raised px-3 py-2">how do I make engine land on a feature branch?</p>
          <p className="m-0">Settings → Workspace → How work lands, or `daoris driver landing engine branch feature/&#123;slug&#125;`.</p>
        </div>
      ),
      composer: <div className="border-t border-line px-4 py-3 text-small text-ink-faint">the composer</div>,
      ended: false,
      onNew: () => {},
    },
  },
};

export const Ended: Story = {
  args: { ...Talking.args, conversation: { ...Talking.args!.conversation!, ended: true } },
};
