import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { Card } from '../../design/ui';
import { chinese } from '../../design/storyLanguage';
import { InTheme } from '../../plugins/storyIcons';
import { ConfiguredAccess } from './ConfiguredAccess';
import { HelpModeChip } from './HelpModePicker';
import type { HelpGrantAgent, HelpGrantChoice, HelpGrantId } from './wired';

// Restricted mode's configured access (HELPMODE1c1, D183 point 1; the modes design §4.1 and §5), the mock the owner reviews
// before any grant is built, all here so the review sees them together: a machine as it arrives, each grant closed as §5's
// sketch draws it; each opened with some chosen, on Claude Code and on Codex, and the two side by side, where Codex cannot be
// held to the browser's chosen writes or the checkouts' scoped reads; the browser changed since it was approved; in 中文; at
// the 680 px window's main area; in dark; and the access chip's menu with *Configure restricted access…*.

const nothing = () => {};

const CLAUDE: HelpGrantAgent = { name: 'Claude Code', rules: true };
const CODEX: HelpGrantAgent = { name: 'Codex', rules: false };

/** As a machine arrives: nothing chosen, the browser waiting for approval, the checkouts on by inheritance. */
const ARRIVED: HelpGrantChoice[] = [{ id: 'checkouts', chosen: ['read'] }];

/**
 * Some chosen: housekeeping's inspection and clean-up, which this conversation did not start with; the browser's reads and two
 * writes; the checkouts; and two settings.
 */
const CHOSEN: HelpGrantChoice[] = [
  { id: 'housekeeping', chosen: ['inspect', 'clean-up'], pending: true },
  { id: 'browser', chosen: ['reads', 'click', 'type'] },
  { id: 'checkouts', chosen: ['read'] },
  { id: 'settings', chosen: ['notify', 'cap'] },
];

const EVERY: HelpGrantId[] = ['housekeeping', 'browser', 'checkouts', 'settings', 'shell'];

/** A Settings card at the frame's main area: 52rem, less its padding. */
const inCard: Decorator = (Story) => <Card className="w-[48rem] max-w-full"><Story /></Card>;
/** The 680 px window's main area. */
const narrow: Decorator = (Story) => <Card className="w-[37rem] max-w-full"><Story /></Card>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof ConfiguredAccess> = {
  title: 'Help/ConfiguredAccess',
  component: ConfiguredAccess,
  args: { agent: CLAUDE, grants: ARRIVED, onChoose: nothing },
};
export default meta;

type Story = StoryObj<typeof ConfiguredAccess>;

/** As a machine arrives, on Claude Code: each grant closed, as the design's sketch draws it. */
export const AsItArrives: Story = { decorators: [inCard] };

/** Each grant opened, some chosen, on Claude Code: every grant but the shell can be held. */
export const OpenedOnClaude: Story = { args: { grants: CHOSEN, opened: EVERY }, decorators: [inCard] };

/**
 * The same on Codex: the browser and the checkouts say *unavailable on this agent* and why, their boxes disabled with the
 * choices kept; housekeeping and the settings, typed tools Daoris holds, hold as on Claude Code.
 */
export const OpenedOnCodex: Story = { args: { agent: CODEX, grants: CHOSEN, opened: EVERY }, decorators: [inCard] };

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
 * The browser changed by its plugin's update since it was approved, said apart from never approved, with a choice the
 * conversation in view did not start with.
 */
export const ChangedSinceApproved: Story = {
  args: {
    grants: [{ id: 'browser', chosen: ['reads', 'click'], changed: true, pending: true }, { id: 'checkouts', chosen: ['read'] }],
    opened: ['browser'],
  },
  decorators: [inCard],
};

/** Each grant opened on Claude Code in 中文. */
export const OpenedChinese: Story = { args: OpenedOnClaude.args, decorators: [inCard, chinese] };

/** At the 680 px window's main area, on Codex, where the reasons wrap. */
export const OpenedNarrow: Story = { args: OpenedOnCodex.args, decorators: [narrow] };

/** Each grant opened on Claude Code, in dark. */
export const OpenedDark: Story = { args: OpenedOnClaude.args, decorators: [inCard, dark] };

/** The access chip's menu opened (design §5): the two modes, then *Configure restricted access…*. */
export const PickerMenu: Story = {
  render: () => (
    <div className="h-48 w-[24rem] max-w-full">
      <HelpModeChip selected="restricted" open={1} onPick={nothing} onConfigure={nothing} />
    </div>
  ),
};

/** The same in 中文. */
export const PickerMenuChinese: Story = { ...PickerMenu, decorators: [chinese] };
