import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import { I18nextProvider } from 'react-i18next';
import i18n from '../i18n';
import { CJK, COUNTS, ELSEWHERE, ENGINE, GAME, LINE, MIRRORED, NEWBIE, SETUP_DEFAULTS, SETUP_OWN, UNDECLARED } from './fixtures';
import { ProjectPage, ProjectsMainNotice } from './ProjectPage';
import type { RepositorySetupProps } from './RepositorySetup';
import type { ProjectTab } from './tabs';

// A repository's page (FRAME1e, D118 §2) in Repositories' main area: its Details tab (adopted, driven here, held, with its
// line, with unlanded branches, a teammate's with no checkout here, one that declared nothing, one not adopted with a root
// here and on a direct door, one not adopted with none, a browser's, a 中文 name) and since UX6f (D150 §4.2) its Setup tab,
// each section folded to what Daoris decides and open: Driving, Line and landing, Sessions, Reach, the person's own values, narrow and
// in 中文; and the main area with no page: nothing chosen, gone, loading, an error with no answer ever.

const nothing = () => {};

/** A reader of 中文, whatever the window's language, sharing the catalogues. */
const zh = i18n.cloneInstance({ lng: 'zh' });
const chinese: Decorator = (Story) => <I18nextProvider i18n={zh}><Story /></I18nextProvider>;

/** The page with its tab held, as Repositories holds it, so a story's tabs press as the window's do. */
function Tabbed({ start = 'details', ...props }: Parameters<typeof ProjectPage>[0] & { start?: ProjectTab }) {
  const [tab, setTab] = useState<ProjectTab>(start);
  return <ProjectPage {...props} tab={tab} onTab={setTab} />;
}

const meta: Meta<typeof Tabbed> = {
  title: 'Repositories/ProjectPage',
  component: Tabbed,
  args: {
    registration: ENGINE,
    counts: COUNTS.engine,
    line: { ...LINE, branch: 'main', source: 'workspace' },
    here: true,
    drivable: true,
    setup: SETUP_DEFAULTS,
    onManage: nothing,
    onOpenCode: nothing,
  },
  // The main area's height, and a width between the side bar's two states.
  decorators: [(Story) => (
    <div className="flex h-[44rem] w-[52rem] max-w-full border border-line bg-page">
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof Tabbed>;

/** Setup with one section asked open, as the line's door opens Line and landing. */
const opened = (open: RepositorySetupProps['open'], setup: RepositorySetupProps = SETUP_DEFAULTS): RepositorySetupProps => ({ ...setup, open });

/**
 * Adopted, on Details: its summary as its one line, *Open code map* and *Manage* in its header; what the index holds and
 * the commit it was fed from, its workspace, its line read-only with its door to Setup, and its declaration as chips.
 */
export const Adopted: Story = {};

/** Held by the person: *held* outranks *drives here*, which it suspends; the hold is lifted on Setup. */
export const Held: Story = { args: { held: true, setup: { ...SETUP_DEFAULTS, driving: { ...SETUP_DEFAULTS.driving!, held: true } } } };

/** Its own line (WSR2): the branch its work grows from and lands on, and that it was set for this repository. */
export const WithALine: Story = { args: { line: LINE } };

/** Session branches holding work no branch of the person's holds (WSR3): named here, listed in Settings. */
export const UnlandedBranches: Story = { args: { line: LINE, unlanded: 2 } };

/** A teammate's registration with no checkout here: *not on this machine*, and nothing printed of anyone's path. */
export const NoRootHere: Story = {
  args: { registration: MIRRORED, counts: COUNTS['studio-tools'], here: false, drivable: false, line: null, setup: { ...SETUP_DEFAULTS, driving: null, sessions: null } },
};

/** Adopted, and declared nothing: the warning that an asker would be guessing (D34). */
export const Undeclared: Story = { args: { registration: UNDECLARED, counts: undefined } };

/** The game: drivable, with no feed, since this deployment reads its own checkouts. */
export const AnotherAdopter: Story = { args: { registration: GAME, counts: COUNTS.game } };

/**
 * Not adopted, with a root here (INT3c): *not adopted* in its head, no *Manage*, and the steps to adopt as text with the
 * reasoning on the info glyph; Setup holds its driving.
 */
export const NotAdopted: Story = { args: { registration: NEWBIE, counts: COUNTS.newbie, drivable: false, onManage: undefined } };

/** Not adopted, on a machine whose door is direct: its Driving says a quest there sits. */
export const NotAdoptedOnADirectDoor: Story = {
  args: {
    registration: NEWBIE, counts: COUNTS.newbie, drivable: false, onManage: undefined, start: 'setup',
    setup: opened('driving', {
      ...SETUP_DEFAULTS,
      driving: {
        ...SETUP_DEFAULTS.driving!, drivable: false, ownTree: false,
        note: 'This machine drives on a direct agent, so a quest here sits, saying why, until it drives on a protocol one.',
      },
    }),
  },
};

/** Not adopted, with no root here: nowhere to start it, so its Setup has no Driving and no Sessions. */
export const NotAdoptedNoRoot: Story = {
  args: {
    registration: ELSEWHERE, counts: undefined, here: false, drivable: false, onManage: undefined, line: null, start: 'setup',
    setup: { ...SETUP_DEFAULTS, driving: null, sessions: null },
  },
};

/** A browser's page: Details alone, no tab row, no *Manage* and no setting, which only a shell's driver says (D47 §4). */
export const InABrowser: Story = { args: { here: undefined, drivable: undefined, line: undefined, setup: null, onManage: undefined } };

/** A name in 中文: the title and the summary are content, shown as they are. */
export const ChineseName: Story = { args: { registration: CJK, counts: COUNTS['渲染管线'] } };

/**
 * **Setup, as Daoris decides it** (UX6f): every section folded to a line naming its values, each at its workspace's or
 * Daoris's default marked as such.
 */
export const SetupFolded: Story = { args: { start: 'setup' } };

/** Setup's Driving open: drive, hold and a tree per session, each its terminal twin. */
export const SetupDriving: Story = { args: { start: 'setup', setup: opened('driving') } };

/** Setup's Line and landing open: its line its workspace's and its landing Daoris's, each offering *Set for this repository*. */
export const SetupWork: Story = { args: { start: 'setup', setup: opened('work') } };

/** Setup's Sessions open: no session language and no standing answer yet. */
export const SetupSessions: Story = { args: { start: 'setup', setup: opened('sessions') } };

/** Setup's Reach open: read by agents outside it, writing into nothing else, and no rule of its own. */
export const SetupReach: Story = { args: { start: 'setup', setup: opened('reach') } };

/**
 * Setup with the person's own values: its line, a branch rule a plugin pushes and accepts automatically, its session
 * language, a standing answer, read by nobody outside it, writing into the game and two rules. Each section holding one
 * opens on its own, with its *Clear*; Driving folds.
 */
export const SetupOwn: Story = { args: { start: 'setup', setup: SETUP_OWN } };

/** Setup folded, narrow: the list a strip and the side bar closed, a section's line wrapping under its name. */
export const SetupNarrow: Story = {
  args: { start: 'setup' },
  decorators: [(Story) => <div className="flex h-[44rem] w-[34rem] max-w-full border border-line bg-page"><Story /></div>],
};

/** The person's own values, narrow: each row stacks below 26 rem (WSR4). */
export const SetupOwnNarrow: Story = { ...SetupNarrow, args: { start: 'setup', setup: SETUP_OWN } };

/** Setup in 中文: 驱动 · 工作 · 会话 · 边界, each default marked in Chinese. */
export const SetupChinese: Story = { args: { start: 'setup', setup: SETUP_OWN }, decorators: [chinese] };

/** Nothing chosen: how to choose, and the list's ＋ on a shell. */
export const NothingChosen: Story = {
  render: () => <ProjectsMainNotice state="none" action={{ label: 'Add repository', onAct: nothing }} />,
};

/** A repository the list chose and the registry no longer holds: retired, or in a workspace not shown. */
export const Gone: Story = { render: () => <ProjectsMainNotice state="gone" /> };

/** The chosen repository on its way: skeleton rows, never the empty state. */
export const Loading: Story = { render: () => <ProjectsMainNotice state="loading" /> };

/** A registry that never answered: its sentence in place, never a blank page. */
export const ErrorNoAnswer: Story = {
  render: () => <ProjectsMainNotice state="unanswered" sentence={'Daoris could not reach this machine\'s host. Is the service running?'} />,
};
