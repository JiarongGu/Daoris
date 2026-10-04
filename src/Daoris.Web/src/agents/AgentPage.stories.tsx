import type { Meta, StoryObj } from '@storybook/react-vite';
import type { AgentActs } from './AgentPage';
import { AgentMainNotice, AgentPage } from './AgentPage';
import {
  CLAUDE_TOOL, CLAUDE_USE, CODEX_TOOL, CODEX_USE, DSH_TOOL, READ, RULES, USAGE, WORKSPACES,
} from './agentsFixtures';

// An agent's page (UX6e, D150 §5.2) in the main area: the design's Claude Code, three accounts of which two read signed out
// and one cools, the tool's own sign-in never read, each section folded to its line; *Read again* on its way; a proposal
// waiting, which opens *What it may do*; an agent with one account and no settings Daoris knows; one not installed; an
// API key; and the main area with no page.

const nothing = () => {};
const ACTS: AgentActs = {
  onReadAgain: nothing, onSignInNew: nothing, onAddKey: nothing, onSignIn: nothing, onTryNow: nothing, onDefault: nothing,
  onRemove: nothing, onSaveSettings: nothing, onDoor: nothing, onPin: nothing,
  scope: { onOrder: nothing, onUse: nothing, onInherit: nothing },
  rules: { onSwitchDefault: nothing, onRemove: nothing, onAdd: nothing, onAnswer: nothing },
};
const QUIET_RULES = { ...RULES, proposals: [] };

const meta: Meta<typeof AgentPage> = {
  title: 'Agents/AgentPage',
  component: AgentPage,
  args: {
    tool: CLAUDE_TOOL, use: CLAUDE_USE, rules: QUIET_RULES, usage: USAGE, workspaces: WORKSPACES, adapter: 'claude-code',
    acts: ACTS,
  },
  // A width between the side bar's two states (D119 §3.6), and the page's whole height, so a shot shows every section.
  decorators: [(Story) => (
    <div className="flex min-h-[52rem] w-[52rem] max-w-full border border-line bg-page">
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof AgentPage>;

/** The design's page: two accounts signed out that hold work, one cooling, the own sign-in never read, sections folded. */
export const ThreeAccounts: Story = {};

/** *Read again* on its way: the press is the only thing that asks an account (§5.3). */
export const Reading: Story = { args: { reading: true } };

/** A proposal waiting opens *What it may do*, the one thing in it that waits on the person. */
export const ProposalWaiting: Story = { args: { rules: RULES } };

/** Opened at its usage by the Agents menu's *Usage* (D150 §2.4). */
export const AtUsage: Story = { args: { part: 'usage' } };

/** Codex: one account signed in, no rules file and no settings Daoris knows, so neither section is drawn (§5.1). */
export const OneAccount: Story = { args: { tool: CODEX_TOOL, use: CODEX_USE, rules: QUIET_RULES, usage: [] } };

/** An agent not installed: the header says so, and *Ways in* offers its installer. */
export const NotInstalled: Story = { args: { tool: DSH_TOOL, use: null, rules: null, usage: [] } };

/** An account that is an API key: its handle, *unchecked*, and no sign-in. */
export const ApiKey: Story = {
  args: {
    tool: {
      ...CLAUDE_TOOL,
      accounts: [{ name: 'account-4', home: 'C:/somewhere/data/harnesses/claude-code/account-4', login: 'in', key: '…wxyz', read: READ }],
    },
    use: { ...CLAUDE_USE, accounts: [{ name: 'account-4', running: 0 }], scopes: [CLAUDE_USE.scopes[0]!] },
  },
};

/** The main area with nothing chosen. */
export const NothingChosen: StoryObj<typeof AgentMainNotice> = {
  render: () => (
    <div className="flex h-[20rem] w-[40rem] border border-line bg-page"><AgentMainNotice state="none" /></div>
  ),
};

/** The chosen agent gone from this machine. */
export const Gone: StoryObj<typeof AgentMainNotice> = {
  render: () => (
    <div className="flex h-[20rem] w-[40rem] border border-line bg-page"><AgentMainNotice state="gone" /></div>
  ),
};
