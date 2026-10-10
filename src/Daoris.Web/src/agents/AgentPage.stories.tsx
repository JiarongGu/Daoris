import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import type { AgentActs } from './AgentPage';
import { AgentMainNotice, AgentPage } from './AgentPage';
import {
  CLAUDE_TOOL, CLAUDE_USE, CODEX_OWN_TOOL, CODEX_OWN_USE, CODEX_READ_TOOL, CODEX_READ_USE, CODEX_TOOL, CODEX_USE, DSH_TOOL,
  INSTALL_TOOL, INSTALL_USE, INSTALL_WORKSPACES, READ, READINGS_TOOL, READINGS_USE, RULES, USAGE, WORKSPACES,
} from './agentsFixtures';

// An agent's page (UX7b, D152 §4; first UX6e, D150 §5.2) in the main area: the design's Claude Code, three accounts of which
// two read signed out and one cools, your own sign-in never read, each section folded to its line; the install's shape, a
// row per state with its one act; the add flow's last step; *Read again* on its way; a proposal waiting, which opens *What
// it may do*; an agent with one account and no settings Daoris knows; Codex's own sign-in with its windows read (CODEXUSE3);
// Codex's accounts read, in the same window cells as Claude Code's (ACCTUX4), at each width and in 中文; one not installed; an API key; what each row knows (ACCTUX1), in both themes; the same read without a pointer (ACCTUX2), each
// row's explanation opened, at 680 px and the main area's 400 px floor in both themes; a key added asking its name and lists,
// and a key refused in its field (ACCTUX3), at the same widths; and the main area with no page. Each at
// the main area's two widths: the frame's 52rem, and the 680 px window's main area, where rows stack. Above the accounts,
// this machine's next start (ACCTUX4b): a wait in `ThreeAccounts`, the list's first taken in `Readings`, and the list's
// first passed over in `CodexWindows`, its row marked first.

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

/**
 * The design's page: two accounts signed out that hold work, one cooling, the own sign-in never read, sections folded. Above
 * the accounts, the next start waits for the cooling one's reset, sooner for a sign-in (ACCTUX4b).
 */
export const ThreeAccounts: Story = {};

/** The same at the 680 px window's main area, where the wait's two sentences wrap. */
export const ThreeAccountsNarrow: Story = {
  decorators: [(Story) => <div className="w-[37rem] max-w-full"><Story /></div>],
};

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

/**
 * Codex's accounts read (ACCTUX4): the same cells as Claude Code's `Readings`, the five hours then the week on every row, each
 * with its exact share, a bar of that window's own allowance, its reset and its own reading's age; a window not reported is
 * unknown in words with no bar, and a window of another length follows the two.
 */
export const CodexWindows: Story = { args: { tool: CODEX_READ_TOOL, use: CODEX_READ_USE, rules: QUIET_RULES, usage: [] } };

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

const narrow: Decorator = (Story) => <div className="w-[37rem] max-w-full"><Story /></div>;
/** The main area's floor, 400 px beside an open list and side bar (UXFIX2c). */
const atFloor: Decorator = (Story) => <div className="w-[25rem] max-w-full"><Story /></div>;

/** Codex's windows at the 680 px window's main area, each row stacked, its two cells side by side, in dark. */
export const CodexWindowsNarrowDark: Story = { args: CodexWindows.args, decorators: [narrow, dark] };

/** The same in 中文, at the frame's width. */
export const CodexWindowsChinese: Story = { args: CodexWindows.args, decorators: [chinese] };

/** At the 400 px floor: each cell's times wrap under its share, and the two cells keep their columns. */
export const CodexWindowsFloor: Story = { args: CodexWindows.args, decorators: [atFloor] };

/** Each row's *About … state* pressed (ACCTUX2): why it cools, why a key reads unchecked or refused, said under the row. */
const explainEach: Story['play'] = async ({ canvasElement }) => {
  canvasElement.querySelectorAll<HTMLButtonElement>('li button[aria-label^="About "][aria-expanded="false"]')
    .forEach((about) => about.click());
};

/**
 * Read without a pointer (ACCTUX2), at the 680 px window's main area: who signed in said whole beside each name, and each
 * row's explanation opened from its press, where it was a tip on hover alone.
 */
export const ReadingsExplainedNarrow: Story = { args: Readings.args, decorators: [narrow], play: explainEach };

/** The same, in dark. */
export const ReadingsExplainedNarrowDark: Story = { args: Readings.args, decorators: [narrow, dark], play: explainEach };

/** At the main area's 400 px floor: who signed in wraps under its name rather than being cut, each explanation opened. */
export const ReadingsExplainedFloor: Story = { args: Readings.args, decorators: [atFloor], play: explainEach };

/** The same, in dark. */
export const ReadingsExplainedFloorDark: Story = { args: Readings.args, decorators: [atFloor, dark], play: explainEach };

/** The install's shape at the 400 px floor, in dark: each row stacked, who signed in wrapping, the terms' line a 28 px target. */
export const TheInstallFloorDark: Story = { args: TheInstall.args, decorators: [atFloor, dark] };

/**
 * A key added asks its name and lists (ACCTUX3, the UX7 design §4.5), where it ended in a toast: *Added the API key …*, its
 * name left empty to be called by its handle, and the lists it may join.
 */
export const KeyAddedAskingWhere: Story = {
  args: { ...TheInstall.args, added: { account: 'acct-9c8d7e6f', who: null, key: '…k3y9' } },
};

/** The same at the 680 px window's main area. */
export const KeyAddedAskingWhereNarrow: Story = { args: KeyAddedAskingWhere.args, decorators: [narrow] };

/** The same at the 400 px floor, in dark. */
export const KeyAddedAskingWhereFloorDark: Story = { args: KeyAddedAskingWhere.args, decorators: [atFloor, dark] };

/** A key the driver refused, said in its field (ACCTUX3): every press on the page refuses it with this sentence. */
const REFUSING: AgentActs = {
  ...ACTS,
  onAddKey: (_key, answered) => answered.refused('`claude-code` takes no key while a sign-in runs: let it end, then save the key again.'),
};

/** The header's *Add an API key* pressed, a key typed and saved; the story's act refuses it. */
const saveARefusedKey: Story['play'] = async ({ canvasElement }) => {
  [...canvasElement.querySelectorAll('button')].find((button) => button.textContent?.trim() === 'Add an API key')?.click();
  const field = canvasElement.querySelector<HTMLInputElement>('input[type="password"]');
  if (!field) return;
  Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set?.call(field, 'sk-ant-api03-story-k3y9');
  field.dispatchEvent(new Event('input', { bubbles: true }));
  field.form?.requestSubmit();
};

/**
 * Refused, the key's field stays with the key in it and says why under it (ACCTUX3), at the 680 px window's main area: it
 * emptied and closed on the press, so the refusal had nowhere to be said.
 */
export const KeyRefusedNarrow: Story = { args: { acts: REFUSING }, decorators: [narrow], play: saveARefusedKey };

/** The same at the 400 px floor, in dark. */
export const KeyRefusedFloorDark: Story = { args: { acts: REFUSING }, decorators: [atFloor, dark], play: saveARefusedKey };

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
