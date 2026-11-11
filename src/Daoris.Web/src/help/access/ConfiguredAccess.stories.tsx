import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { Card } from '../../design/ui';
import { chinese } from '../../design/storyLanguage';
import { InTheme } from '../../plugins/storyIcons';
import { ConfiguredAccess } from './ConfiguredAccess';
import { HelpModeChip } from './HelpModePicker';
import type { HelpGrantAgent, HelpGrantChoice, HelpGrantId } from './wired';

// Restricted mode's configured access, the mock the owner reviews: a machine as it arrives, each grant closed; each opened with
// some chosen, on Claude Code and on Codex, and side by side, where Codex cannot be held to the browser; the browser changed since
// it is approved; in 中文; at the 680 px window's main area; in dark; and the access chip's menu. The browser is
// drawn read only from its plugin's approval, its *Manage* the plugin's tab, with no boxes; and with no plugin offering it. The
// checkouts are a box each, held on Codex too; the acts a box per family.

const nothing = () => {};

const CLAUDE: HelpGrantAgent = { name: 'Claude Code', rules: true };
const CODEX: HelpGrantAgent = { name: 'Codex', rules: false };

/** What the driver serves: the readable checkouts by their repositories' names, and the families of acts. */
const SERVED = { checkouts: ['console-ui', 'engine', 'reports-db'], acts: ['pause', 'clean-up', 'discard', 'settings'] };

/** As a machine arrives: nothing chosen, the browser's plugin waiting for approval, every checkout on by inheritance. */
const ARRIVED: HelpGrantChoice[] = [
  { id: 'browser', chosen: [], plugin: 'in-app-browser' }, { id: 'checkouts', chosen: SERVED.checkouts },
];

/**
 * Some chosen: the clean-up and settings families, which this conversation did not start with; the browser's reads and two
 * writes, approved on its plugin's page; and the checkouts, `reports-db` turned off.
 */
const CHOSEN: HelpGrantChoice[] = [
  { id: 'browser', chosen: ['reads', 'browser_click', 'browser_type'], plugin: 'in-app-browser' },
  { id: 'checkouts', chosen: ['console-ui', 'engine'] },
  { id: 'acts', chosen: ['clean-up', 'settings'], pending: true },
];

const EVERY: HelpGrantId[] = ['browser', 'checkouts', 'acts', 'shell'];

/** A Settings card at the frame's main area: 52rem, less its padding. */
const inCard: Decorator = (Story) => <Card className="w-[48rem] max-w-full"><Story /></Card>;
/** The 680 px window's main area. */
const narrow: Decorator = (Story) => <Card className="w-[37rem] max-w-full"><Story /></Card>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof ConfiguredAccess> = {
  title: 'Help/ConfiguredAccess',
  component: ConfiguredAccess,
  args: { agent: CLAUDE, grants: ARRIVED, built: SERVED, onChoose: nothing, onManage: nothing },
};
export default meta;

type Story = StoryObj<typeof ConfiguredAccess>;

/** As a machine arrives, on Claude Code: each grant closed, as the design's sketch draws it. */
export const AsItArrives: Story = { decorators: [inCard] };

/** Each grant opened, some chosen, on Claude Code: every grant but the shell can be held. */
export const OpenedOnClaude: Story = { args: { grants: CHOSEN, opened: EVERY }, decorators: [inCard] };

/**
 * The same on Codex: the browser says *unavailable on this agent* and why, until Daoris serves a mediated tool; the families of acts
 * and the checkouts, which Daoris holds itself, hold as on Claude Code.
 */
export const OpenedOnCodex: Story = { args: { agent: CODEX, grants: CHOSEN, opened: EVERY }, decorators: [inCard] };

/** No checkout here lets agents outside it read: the checkouts' row says so, with no box. */
export const NoCheckoutToRead: Story = {
  args: { grants: [{ id: 'checkouts', chosen: [] }], built: { checkouts: [] }, opened: ['checkouts'] },
  decorators: [inCard],
};

/** Claude Code and Codex side by side, the same choices, each grant opened. */
export const ClaudeAndCodex: Story = {
  args: { grants: CHOSEN, opened: EVERY },
  render: (args) => (
    <div className="grid w-[84rem] max-w-full grid-cols-2 items-start gap-4">
      {[CLAUDE, CODEX].map((agent) => (
        <Card key={agent.name}>
          <p className="mb-3 mt-0 text-small font-semibold text-ink">{agent.name}</p>
          <ConfiguredAccess {...args} agent={agent} />
        </Card>
      ))}
    </div>
  ),
};

/** The same in 中文. */
export const ClaudeAndCodexChinese: Story = { ...ClaudeAndCodex, decorators: [chinese] };

/**
 * The browser changed by its plugin's update since it is approved, said apart from never approved, with a choice the
 * conversation in view did not start with.
 */
export const ChangedSinceApproved: Story = {
  args: {
    grants: [
      { id: 'browser', chosen: ['reads', 'browser_click'], changed: true, pending: true, plugin: 'in-app-browser' },
      { id: 'checkouts', chosen: SERVED.checkouts },
    ],
    opened: ['browser'],
  },
  decorators: [inCard],
};

/** No plugin here offers Ask Daoris Daoris's browser: its row says so, and *Manage* opens Plugins. */
export const NoBrowserPlugin: Story = { args: { grants: [{ id: 'checkouts', chosen: SERVED.checkouts }] }, decorators: [inCard] };

/** The same in 中文. */
export const NoBrowserPluginChinese: Story = { args: NoBrowserPlugin.args, decorators: [inCard, chinese] };

/** Each grant opened on Claude Code in 中文. */
export const OpenedChinese: Story = { args: OpenedOnClaude.args, decorators: [inCard, chinese] };

/** At the 680 px window's main area, on Codex, where the reasons wrap. */
export const OpenedNarrow: Story = { args: OpenedOnCodex.args, decorators: [narrow] };

/** Each grant opened on Claude Code, in dark. */
export const OpenedDark: Story = { args: OpenedOnClaude.args, decorators: [inCard, dark] };

/** The access chip's menu opened: the two modes, then *Configure restricted access…*. */
export const PickerMenu: Story = {
  render: () => (
    <div className="h-48 w-[24rem] max-w-full">
      <HelpModeChip selected="restricted" open={1} onPick={nothing} onConfigure={nothing} />
    </div>
  ),
};

/** The same in 中文. */
export const PickerMenuChinese: Story = { ...PickerMenu, decorators: [chinese] };
