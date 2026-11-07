import type { Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { sessionWidths } from '../../design/storyWidths';
import type { HelperAskRow } from '../waits/HelperAsks';
import { BringInAgent } from './BringInAgent';
import { CoWorkers } from './CoWorkers';
import { MemberThread } from './MemberThread';

// The head's *Co-workers*: the lead, a member working, one answered to the lead, one answered to
// the person with *Pass to the lead*, and an agent's ask that waits on the person's yes with its presses; *Bring in another agent…*'s
// dialog with a door the rules turn off; a member's thread with *Back to lead*, *Ask again…* and the pass. In English, 中文 and
// dark, at the frame's widths.

const row = (over: Partial<HelperAskRow> & Pick<HelperAskRow, 'id'>): HelperAskRow => ({
  question: 'Does the hydrator read the budget before or after the queue drains?', agent: 'codex-acp', askedBy: 'person',
  at: '2026-11-06T09:00:00Z', state: null, code: null, helper: null, startedOn: null, ...over,
});

const NAMES: Record<string, string> = { 'codex-acp': 'Codex', 'claude-code': 'Claude Code' };

const ASKS: HelperAskRow[] = [
  row({ id: 'a1', state: 'started', helper: 'h1', startedOn: 'codex-acp' }),
  row({ id: 'a2', state: 'answered', helper: 'h2', startedOn: 'codex-acp' }),
  row({ id: 'a3', state: 'answered', helper: 'h3', startedOn: 'codex-acp', to: 'person', kept: true }),
  row({ id: 'a4', askedBy: 'agent', agent: null, state: 'waiting', question: 'Is the save format versioned anywhere else?' }),
];

const meta: Meta<typeof CoWorkers> = {
  title: 'Work/Team/CoWorkers',
  component: CoWorkers,
  args: {
    session: 's1',
    lead: 'Claude Code',
    helpers: { asks: ASKS, nameOf: (agent) => NAMES[agent] ?? agent, onApprove: () => {}, onDecline: () => {}, onOpen: () => {} },
    onPass: () => {},
    choices: [{ door: 'question', onChoose: () => {} }, { door: 'goOn', onChoose: () => {} }],
  },
  decorators: [sessionWidths.half],
};
export default meta;

type Story = StoryObj<typeof CoWorkers>;

/** A member per state, the pass on the answer kept for the person, and an agent's ask waiting on the yes. */
export const Team: Story = {};

/** The same at the main area's 400 px floor. */
export const TeamNarrow: Story = { decorators: [sessionWidths.narrow] };

/** The same in 中文. */
export const TeamChinese: Story = { decorators: [chinese] };

/** The same in dark. */
export const TeamDark: Story = { decorators: [(Story) => <InTheme theme="dark"><Story /></InTheme>] };

const OUTSIDE = [
  { id: 'o1a2b3c4', agent: 'codex-acp', repository: 'checker', joined: '2026-11-06T09:00:00Z', seen: '2026-11-06T09:30:00Z' },
  { id: 'o5d6e7f8', agent: 'other', repository: null, joined: '2026-11-06T09:10:00Z', seen: null },
];

/** The agents outside Daoris on the line's channel beside the co-workers, each as itself with *Remove*. */
export const OutsideAgents: Story = { args: { outside: OUTSIDE, onRemoveOutside: () => {} } };

/** The same in 中文. */
export const OutsideAgentsChinese: Story = { ...OutsideAgents, decorators: [chinese] };

/** The same at the main area's 400 px floor. */
export const OutsideAgentsNarrow: Story = { ...OutsideAgents, decorators: [sessionWidths.narrow] };

/** *Bring in another agent…*: the doors in order, one the rules turn off with its reason. */
export const BringIn: Story = {
  render: () => (
    <BringInAgent
      onClose={() => {}}
      choices={[
        { door: 'question', onChoose: () => {} },
        { door: 'opinion', off: 'Second opinions are off for this repository.' },
        { door: 'goOn', onChoose: () => {} },
      ]}
    />
  ),
};

/** A member's thread while it works: one turn, *Back to lead*. */
export const ThreadWorking: Story = {
  render: () => <MemberThread lead="Expose a streaming budget" ask={ASKS[0]!} working onBack={() => {}} onAskAgain={() => {}} />,
};

/** A member's thread once it answered the person: where it went, *Pass to the lead* and *Ask again…*. */
export const ThreadAnswered: Story = {
  render: () => <MemberThread lead="Expose a streaming budget" ask={ASKS[2]!} working={false} onBack={() => {}} onAskAgain={() => {}} onPass={() => {}} />,
};

/** The same in 中文. */
export const ThreadAnsweredChinese: Story = { ...ThreadAnswered, decorators: [chinese] };
