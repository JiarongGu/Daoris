import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import type { AgentActs } from './AgentPage';
import { AgentMainNotice, AgentPage } from './AgentPage';
import {
  CLAUDE_TOOL, CLAUDE_USE, CODEX_OWN_TOOL, CODEX_OWN_USE, CODEX_TOOL, CODEX_USE, DSH_TOOL, INSTALL_TOOL, INSTALL_USE,
  INSTALL_WORKSPACES, READ, READINGS_TOOL, READINGS_USE, RULES, USAGE, WORKSPACES,
} from './agentsFixtures';

// An agent's page (UX7b, D152 §4; first UX6e, D150 §5.2) in the main area: the design's Claude Code, three accounts of which
// two read signed out and one cools, your own sign-in never read, each section folded to its line; the install's shape, a
// row per state with its one act; the add flow's last step; *Read again* on its way; a proposal waiting, which opens *What
// it may do*; an agent with one account and no settings Daoris knows; Codex's own sign-in with its windows read (CODEXUSE3);
// one not installed; an API key; what each row knows (ACCTUX1), in both themes; and the main area with no page. Each at the main area's two widths: the frame's 52rem, and the 680 px window's main area, where rows stack.

const nothing = () => {};
const ACTS: AgentActs = {
  onReadAgain: nothing, onReadOne: nothing, onSignInNew: nothing, onAddKey: nothing, onSignIn: nothing, onTryNow: nothing, onDefault: nothing,
  onRemove: nothing, onRename: nothing, onJoin: nothing, onAddedAnswer: nothing, onSaveSettings: nothing, onDoor: nothing, onPin: nothing,
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

/**
 * The install's shape (the UX7 design §4.7): a row per state with its one act, *Read* on the account a list holds that reads
 * unknown, *Try now* on the one cooling, *Use in a workspace…* on the new account no list holds, and your own sign-in last.
 */
export const TheInstall: Story = { args: { tool: INSTALL_TOOL, use: INSTALL_USE, workspaces: INSTALL_WORKSPACES, usage: [] } };

/** The same, at the 680 px window's main area: each row stacks to three short lines, and the columns' names are gone. */
export const TheInstallNarrow: Story = {
  args: TheInstall.args,
  decorators: [(Story) => <div className="w-[37rem] max-w-full"><Story /></div>],
};

/** The add flow's last step, under the header: the email offered as its name, and the lists it may join. */
export const AddedAskingWhere: Story = {
  args: { ...TheInstall.args, added: { account: 'acct-5e1f0a2b', who: 'spare@example.invalid' } },
};

/** *Read again* on its way: the press is the only thing that asks an account (§5.3). */
export const Reading: Story = { args: { reading: true } };

/** A proposal waiting opens *What it may do*, the one thing in it that waits on the person. */
export const ProposalWaiting: Story = { args: { rules: RULES } };

/** Opened at its usage by the Agents menu's *Usage* (D150 §2.4). */
export const AtUsage: Story = { args: { part: 'usage' } };

/** Codex: one account signed in, no rules file and no settings Daoris knows, so neither section is drawn (§5.1). */
export const OneAccount: Story = { args: { tool: CODEX_TOOL, use: CODEX_USE, rules: QUIET_RULES, usage: [] } };

/**
 * Codex with no account of Daoris's (CODEXUSE3): your own sign-in's row says its five-hour and weekly use, read at a press
 * of Codex's own server, as an account's row says its own; its ⋯ reads it again.
 */
export const OwnSignInRead: Story = { args: { tool: CODEX_OWN_TOOL, use: CODEX_OWN_USE, rules: QUIET_RULES, usage: [] } };

/** The same in 中文. */
export const OwnSignInReadChinese: Story = { args: OwnSignInRead.args, decorators: [chinese] };

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

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/**
 * What each row knows (ACCTUX1): a key its provider refused says *key refused* with *Add an API key*; *reserve*, cooling for
 * Daoris's default because its agent named no reset, says until when and *reset unknown*; *team*, cooling until the reset its
 * agent named, says each window's share in the ink and its reset beside it in the soft ink; and an account named for its
 * email says it once.
 */
export const Readings: Story = { args: { tool: READINGS_TOOL, use: READINGS_USE, workspaces: ['work'], usage: [] } };

/** The same in dark, where the faint ink the readings wore was the hardest to read. */
export const ReadingsDark: Story = { args: Readings.args, decorators: [dark] };

/** The same at the 680 px window's main area, each row stacked, in dark. */
export const ReadingsNarrowDark: Story = {
  args: Readings.args,
  decorators: [(Story) => <div className="w-[37rem] max-w-full"><Story /></div>, dark],
};

/** The same in 中文. */
export const ReadingsChinese: Story = { args: Readings.args, decorators: [chinese] };

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
