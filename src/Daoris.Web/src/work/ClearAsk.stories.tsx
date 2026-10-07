import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { ComponentProps, ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import { ClearAsk, type KeptDoors } from './ClearAsk';
import { clearRefusal } from './history';
import {
  ASK_PLAN, FAILED_PLAN, QUEST_ALONE, QUEST_FORGOTTEN, QUEST_PLAN, WORKSPACE_ALL_KEPT, WORKSPACE_FILES_ONLY, WORKSPACE_KEPT,
  WORKSPACE_LEFT_OVER, WORKSPACE_PLAN,
} from './historyFixtures';

// A clear's first press (HIST1e, D153; the history-clearing design §5, §6.1), as it asks under a quest's or an ask's header
// and in a workspace's Details: what it takes and that nothing brings it back, or that the remote keeps the team's copy;
// for a workspace the counts by kind and every unit kept with its reason and door; a teammate's failed session kept; only
// left-over files, said as only those (HIST1o); nothing to take; on its way; pressed and every unit kept (HIST1n). At the
// main area's width beside the install's 1546 px window and at 680 px, in both languages and both themes.

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
  args: { target: QUEST, plan: QUEST_PLAN, meanIt: 'Clear quest', onClear: nothing, onClose: nothing },
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

/** A workspace's clear whose press the driver answers as every unit kept (HIST1n), as `useHistoryActs` says it. */
function KeptAtThePress(props: ComponentProps<typeof ClearAsk>) {
  const { t } = useTranslation();
  return <ClearAsk {...props} onClear={(_units, answered) => answered.refused(clearRefusal(t, WORKSPACE_ALL_KEPT))} />;
}

/** Presses the clear's move, so the answer it gets is said inside it. */
const pressClear: Story['play'] = async ({ canvasElement }) => {
  [...canvasElement.querySelectorAll('button')].find((button) => /^(Clear|清除) 3/.test(button.textContent ?? ''))?.click();
};

/**
 * Pressed, where every unit it listed changed since the list and was kept (HIST1n): nothing went, so it stays open, saying
 * so and each kept unit by its name and reason, a line each, in the refusal's rail.
 */
export const WorkspaceKeptAtThePress: Story = { ...Workspace, render: (args) => <KeptAtThePress {...args} />, play: pressClear };

/** The same in 中文. */
export const WorkspaceKeptAtThePressChinese: Story = { ...WorkspaceKeptAtThePress, args: WorkspaceChinese.args, decorators: [chinese] };

/** At 680 px, in dark. */
export const WorkspaceKeptAtThePressNarrowDark: Story = { ...WorkspaceKeptAtThePress, decorators: [dark, narrow] };

/** Only what records already gone left, and the intake's room: the press sends no unit. */
export const LeftOverOnly: Story = { args: { target: WORKSPACE, plan: WORKSPACE_LEFT_OVER, meanIt: 'Clear 5' } };

/** Only left-over files, as the install had them (HIST1o): the opening says only those, never the finished work's words. */
export const LeftOverFilesOnly: Story = { args: { target: WORKSPACE, plan: WORKSPACE_FILES_ONLY, meanIt: 'Clear' } };

/** The same in 中文, in dark. */
export const LeftOverFilesOnlyChineseDark: Story = {
  ...LeftOverFilesOnly, args: { ...LeftOverFilesOnly.args, meanIt: '清除' }, decorators: [chinese, dark],
};

/** Nothing may go: said, with only *Close*, and each unit kept with its reason. */
export const NothingToClear: Story = { args: { target: WORKSPACE, plan: WORKSPACE_KEPT, meanIt: 'Clear 0', doors: DOORS } };

/** A clear on its way: the presses wait. */
export const OnItsWay: Story = { args: { busy: true } };
