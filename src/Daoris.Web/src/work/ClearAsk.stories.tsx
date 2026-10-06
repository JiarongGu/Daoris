import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { ReactNode } from 'react';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import { ClearAsk, type KeptDoors } from './ClearAsk';
import {
  ASK_PLAN, FAILED_PLAN, QUEST_ALONE, QUEST_FORGOTTEN, QUEST_PLAN, WORKSPACE_KEPT, WORKSPACE_LEFT_OVER, WORKSPACE_PLAN,
} from './historyFixtures';

// A clear's first press (HIST1e, D153; the history-clearing design §5, §6.1), as it asks under a quest's or an ask's header
// and in a workspace's Details: what it takes and that nothing brings it back, or that the remote keeps the team's copy;
// for a workspace the counts by kind and every unit kept with its reason and door; a teammate's failed session kept; only
// left-over files; nothing to take; on its way. At the main area's width beside the install's 1546 px window and at 680 px,
// in both languages and both themes.

const nothing = () => {};
const QUEST = { scope: 'quest', id: '9a8b7c' } as const;
const WORKSPACE = { scope: 'workspace', id: 'aurora' } as const;
const DOORS: KeptDoors = { quest: nothing, ask: nothing, session: nothing, branches: nothing, sync: nothing };

/** The main area at a width: 1180 px beside a 1546 px window's side bar and list, 600 px at a 680 px window. */
function Main({ width, children }: { width: number; children: ReactNode }) {
  return <div className="@container/main max-w-full bg-page p-4" style={{ width }}>{children}</div>;
}

const wide: Decorator = (Story) => <Main width={1180}><Story /></Main>;
const narrow: Decorator = (Story) => <Main width={600}><Story /></Main>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof ClearAsk> = {
  title: 'Work/Clear',
  component: ClearAsk,
  args: { target: QUEST, plan: QUEST_PLAN, meanIt: 'Clear quest', onClear: nothing, onCancel: nothing },
  decorators: [wide],
};
export default meta;

type Story = StoryObj<typeof ClearAsk>;

/** A done quest with three sessions here, never numbered by a remote: what goes, and that nothing brings it back. */
export const Quest: Story = {};

/** The same in 中文. */
export const QuestChinese: Story = { args: { meanIt: '确认清除委托' }, decorators: [chinese] };

/** The same in dark. */
export const QuestDark: Story = { decorators: [dark] };

/** A quest the remote numbered: forgotten here, and the remote keeps the team's copy. */
export const QuestForgotten: Story = { args: { plan: QUEST_FORGOTTEN } };

/** A declined quest no session of this machine served: it goes alone. */
export const QuestAlone: Story = { args: { plan: QUEST_ALONE } };

/** A closed quest's failed sessions: two go, and a teammate's is named and kept. */
export const FailedSessions: Story = { args: { target: { scope: 'failed', id: '9a8b7c' }, plan: FAILED_PLAN, meanIt: 'Clear 2' } };

/** An ask's work, whole: the ask, the quests it became and their sessions. */
export const Ask: Story = { args: { target: { scope: 'ask', id: 'a1b2c3' }, plan: ASK_PLAN, meanIt: 'Clear ask' } };

/** A workspace's: the counts by kind, then every unit kept with its reason and the door that frees it. */
export const Workspace: Story = { args: { target: WORKSPACE, plan: WORKSPACE_PLAN, meanIt: 'Clear 3', doors: DOORS } };

/** The same in 中文. */
export const WorkspaceChinese: Story = { ...Workspace, args: { ...Workspace.args, meanIt: '清除 3 项' }, decorators: [chinese] };

/** The same in dark. */
export const WorkspaceDark: Story = { ...Workspace, decorators: [dark] };

/** At 680 px: what goes above what stays, each reason under its unit. */
export const WorkspaceNarrow: Story = { ...Workspace, decorators: [narrow] };

/** At 680 px, in 中文. */
export const WorkspaceNarrowChinese: Story = { ...WorkspaceChinese, decorators: [chinese, narrow] };

/** At 680 px, in dark. */
export const WorkspaceNarrowDark: Story = { ...Workspace, decorators: [dark, narrow] };

/** Only what records already gone left, and the intake's room: the press sends no unit. */
export const LeftOverOnly: Story = { args: { target: WORKSPACE, plan: WORKSPACE_LEFT_OVER, meanIt: 'Clear 5' } };

/** Nothing may go: said, with only *Close*, and each unit kept with its reason. */
export const NothingToClear: Story = { args: { target: WORKSPACE, plan: WORKSPACE_KEPT, meanIt: 'Clear 0', doors: DOORS } };

/** A clear on its way: the presses wait. */
export const OnItsWay: Story = { args: { busy: true } };
