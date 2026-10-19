import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import type { GitLogCommit, GitRepositoryBranches } from '../bridge/repositories/git';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { FULL, NOTHING_SET, PLUGIN_UNREADY } from '../workflow/fixtures';
import {
  CJK, COUNTS, ELSEWHERE, ENGINE, GAME, GIT_BRANCHES_ENGINE, GIT_HISTORY_ENGINE, GIT_HISTORY_PAGES, LINE, MIRRORED, NEWBIE, NEWBIE_SETUP,
  SETUP_DEFAULTS, SETUP_OWN, SETUP_PROCESS, SETUP_PROCESS_OWN, UNDECLARED,
} from './fixtures';
import { ProjectPage, ProjectsMainNotice } from './ProjectPage';
import type { SetUpForAgentsProps } from './setup/SetUpForAgents';
import { BranchesTab, type GitRead, HistoryTab, type HistoryRead } from './git/RepositoryGit';
import type { RepositorySetupProps } from './setup/RepositorySetup';
import type { ProjectTab } from './tabs';

// A repository's page (FRAME1e, D118 §2) in Repositories' main area: its Details tab (adopted, driven here, held, with its
// line, with unlanded branches, a teammate's with no checkout here, one that declared nothing, one not adopted with a root
// here and on a direct door, one not adopted with none, a browser's, a 中文 name) and since UX6f (D150 §4.2) its Setup tab,
// each section folded to what Daoris decides and open: Driving, Line and landing, Sessions, Reach, the person's own values, narrow and
// in 中文; since WORKFLOW1b its Workflow tab, with everything at once, nothing set, a limit, in 中文, narrow and in a browser;
// since DOORS1b its process on Setup (Commits and Helpers, the landing's ticket key and squash), from above, its own, narrow
// and in 中文;
// since UX6h1 (§4.4) its Branches and History tabs, each reading, with no checkout here, refused, with nothing yet and read,
// History's lanes for a merged branch and for several, dark, in 中文 and narrow; since UX6h2a Branches by kind, its line's
// and its landed branches' other standings with origin, its session records unread, dark and narrow; and the main area with
// no page: nothing chosen, gone, loading, an error with no answer ever.

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/** The list beside the main area a strip and the side bar closed: the main area at 34 rem. */
const narrow: Decorator = (Story) => <div className="flex h-[44rem] w-[34rem] max-w-full border border-line bg-page"><Story /></div>;

/** The title its holder joins for the session branch a record names (the rail's rule): its quest's name. */
const TITLES = { 'daoris/engine-physics-step': 'Expose a streaming budget' };

type BranchesValue = GitRepositoryBranches & { sessionsUnread?: boolean };
// Each branch's name opens its page and the line's opens History, as the window's do (UX6h3b): `BranchPage`'s stories.
const branchesRead = (read: GitRead<BranchesValue>) =>
  <BranchesTab repository="engine" read={read} titles={TITLES} onOpen={nothing} onOpenHistory={nothing} />;
const branchesAs = (more: Partial<BranchesValue>) => branchesRead({ state: 'read', value: { ...GIT_BRANCHES_ENGINE, ...more } });

/** The engine's landed branches in their other standings, as `GIT_BRANCHES` answers them. */
const LANDED_OTHERWISE = (() => {
  const [, , , open, merged] = GIT_BRANCHES_ENGINE.branches;
  const asked = (hours: number) => new Date(Date.now() - hours * 3_600_000).toISOString();
  const as = (name: string, landed: Partial<NonNullable<NonNullable<typeof open>['landed']>>) =>
    ({ ...open!, name, landed: { ...open!.landed!, ...landed } });
  return [
    as('review/q9-level-streaming', {
      origin: 'diverged', originAhead: 2, originBehind: 3,
      pullRequestAskFailed: { code: 'timeout', plugin: 'azure', at: asked(1) },
    }),
    as('review/q8-audio-bus', { origin: 'ahead', originAhead: 1, originBehind: 0, pullRequestState: { ...open!.landed!.pullRequestState!, state: 'unknown' } }),
    as('review/q7-save-slots', {
      origin: 'behind', originAhead: 0, originBehind: 4,
      pullRequestState: { ...open!.landed!.pullRequestState!, state: 'abandoned', at: asked(20) },
    }),
    { ...merged!, landed: { ...merged!.landed!, origin: 'unknown', pullRequestState: null, pullRequest: null } },
  ];
})();
const historyRead = (read: HistoryRead) => <HistoryTab repository="engine" read={read} />;
const lineHistory = (commits: GitLogCommit[], more: Partial<Extract<HistoryRead, { state: 'read' }>['value']> = {}, tips = [commits[0]!.commit]) =>
  // `onMore` is the page's: a story draws the foot, and its press is the holder's.
  <HistoryTab repository="engine" read={{ state: 'read', value: { line: 'main', tips, commits, more: false, ...more } }} onMore={nothing} />;
// *All branches* ticked (UX6h3c): the ⋯ is the page's, so a story draws it ticked, with the limit's sentence where given.
const allBranchesHistory = (commits: GitLogCommit[], limit?: { shown: number; total: number }) => (
  <HistoryTab
    repository="engine"
    read={{ state: 'read', value: { line: 'main', tips: [commits[0]!.commit], commits, more: false } }}
    onMore={nothing}
    all={{ on: true, onToggle: nothing, limit }}
  />
);

/** A line three branches were merged into, one still open beside it: four lanes at their widest. */
const SEVERAL: GitLogCommit[] = (() => {
  const at = (hours: number) => new Date(Date.now() - hours * 3_600_000).toISOString();
  const row = (commit: string, parents: string[], subject: string, hours: number): GitLogCommit => ({
    commit: commit.padEnd(40, '0'), parents: parents.map((parent) => parent.padEnd(40, '0')), author: 'Mei Lin',
    authorEmail: 'mei@example.invalid', subject, authoredAt: at(hours), committedAt: at(hours), side: null,
  });
  return [
    row('f10', ['f09', 'e03'], 'Merge the audio mixer', 1),
    row('e03', ['e02'], 'Mix the music under the effects', 2),
    row('f09', ['f08', 'd02'], 'Merge the input remapping', 3),
    row('e02', ['f05'], 'Add an audio bus per channel', 4),
    row('d02', ['d01'], 'Save remapped keys per player', 5),
    row('f08', ['f07', 'c02'], 'Merge the shader cache', 6),
    row('c02', ['c01'], 'Key the cache by the driver version', 7),
    row('d01', ['f05'], 'Read the keys from the settings', 8),
    row('f07', ['f05'], 'Name the frame budget', 9),
    row('c01', ['f05'], 'Cache compiled shaders on disk', 10),
    row('f05', ['f04'], 'Load assets on a worker', 30),
    row('f04', [], 'Begin the engine', 400),
  ];
})();

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
    // How its work moves (WORKFLOW1b): a shell's, beside its Setup.
    workflow: { current: FULL, doors: { workspaceSetup: nothing } },
    // Its branches and its line's history (UX6h1): a shell's, as Git's reads answer them.
    branches: branchesRead({ state: 'read', value: GIT_BRANCHES_ENGINE }),
    history: lineHistory(GIT_HISTORY_ENGINE),
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

/** *Set up for agents* on a not adopted repository's Details (LAYOUT8), its holder's state handed in: nothing read yet. */
const setUp = (more: Partial<SetUpForAgentsProps> = {}): SetUpForAgentsProps => ({
  repository: 'newbie', plan: null, reading: false, pressing: false, pressed: null, problem: null,
  onLook: nothing, onPress: nothing, onClose: nothing, ...more,
});

/** Not adopted, with *Set up for agents* beneath its steps (LAYOUT8, D117 §6.1): nothing is read until the person asks. */
export const NotAdoptedSetUp: Story = {
  args: { registration: NEWBIE, counts: COUNTS.newbie, drivable: false, onManage: undefined, setUp: setUp() },
};

/** The plan, `--plan`'s twin, before the press: what was read on its line, the rule it adds and the quest, folded. */
export const NotAdoptedSetUpPlan: Story = {
  args: { ...NotAdoptedSetUp.args, setUp: setUp({ plan: NEWBIE_SETUP }) },
};

/** A plan the press refuses: every refusal at once, each naming its door, and no press. */
export const NotAdoptedSetUpRefused: Story = {
  args: {
    ...NotAdoptedSetUp.args,
    setUp: setUp({
      plan: {
        ...NEWBIE_SETUP, pressable: false,
        refusals: [
          {
            code: 'not-driven', sentence: '`newbie` is not driven here: it is not opted into driving on this machine, and only a '
              + 'driven session carries a set-up. Drive it under Repositories → the repository\'s page → Setup → Driving, or with '
              + '`daoris driver drive newbie`.',
          },
          {
            code: 'no-doctrine-tool', sentence: 'the doctrine tool cannot run here: no `daoris` on the PATH Daoris\'s children start '
              + 'with. An install that does not carry the doctrine tool puts none there, and a republish of Daoris that carries it does.',
          },
        ],
      },
    }),
  },
};

/** What the press published: the service's sentence, and that the repository's own session takes it. */
export const NotAdoptedSetUpPublished: Story = {
  args: {
    ...NotAdoptedSetUp.args,
    setUp: setUp({
      plan: NEWBIE_SETUP,
      pressed: {
        published: true, message: 'Asked as `#a1b2c3d4` in `default`.', askId: 'a1b2c3d4', questId: 'q1a2b3c4',
        added: NEWBIE_SETUP.rules, plan: NEWBIE_SETUP,
      },
    }),
  },
};

/** The plan in 中文: the chrome translates, the quest, the rule and the names stay as written. */
export const NotAdoptedSetUpChinese: Story = { args: NotAdoptedSetUpPlan.args, decorators: [chinese] };

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
export const InABrowser: Story = {
  args: {
    here: undefined, drivable: undefined, line: undefined, setup: null, workflow: null, branches: undefined, history: undefined,
    onManage: undefined,
  },
};

/**
 * **Workflow** (WORKFLOW1b, the workflow design §6.1, §6.2): how its work moves, drawn from its rules as they stand, between
 * Details and Setup. Everything at once: a plugin that may hold a start, its standing answer, its workspace's second opinion,
 * its own look in `dev`, an automatic landing on a branch and the pull request the person merges.
 */
export const Workflow: Story = { args: { start: 'workflow' } };

/** Its Workflow with nothing set anywhere: the work, then a merge the person accepts, by Daoris's default. */
export const WorkflowNothingSet: Story = { args: { start: 'workflow', workflow: { current: NOTHING_SET } } };

/** Its Workflow with a limit: the branch rule's plugin is not on this machine, said whole on both steps it reaches. */
export const WorkflowWithALimit: Story = { args: { start: 'workflow', workflow: { current: PLUGIN_UNREADY } } };

/** Its Workflow in 中文. */
export const WorkflowChinese: Story = { args: { start: 'workflow' }, decorators: [chinese] };

/** Its Workflow, narrow: the list a strip and the side bar closed, each step's words wrapping under its title. */
export const WorkflowNarrow: Story = {
  args: { start: 'workflow' },
  decorators: [(Story) => <div className="flex h-[44rem] w-[34rem] max-w-full border border-line bg-page"><Story /></div>],
};

/**
 * **The bundle without a shell** (D47 §4, the workflow design §6.1): a browser's page remembered at Workflow is Details
 * alone, with no tab row, since a repository's workflows are this machine's.
 */
export const WorkflowInABrowser: Story = { args: { ...InABrowser.args, start: 'workflow' } };

/**
 * **Branches** (UX6h1, D150 §4.2, §4.4; by kind since UX6h2a, the built-in Git design §2.2): its line at the head, set for
 * the workspace, in step with origin and fetched three hours ago; *Sessions'*, one named by its session's title and state and
 * one no record names, each ahead of and behind the line; *Landed*, in step with an open pull request, gone from origin after
 * it merged, and never pushed; and *Yours*, five of seven with *Show 2 more*. The branch only origin holds is not drawn.
 * Each name is a press (UX6h3b): a branch's opens its page, the line's History.
 */
export const Branches: Story = { args: { start: 'branches' } };

/** Branches with origin's shown (UX6h2b1): the ⋯ after Yours pressed and its ticked item chosen, *Origin's (1)* under Yours. */
export const BranchesOriginShown: Story = {
  args: Branches.args,
  play: async ({ canvasElement }) => {
    const more = canvasElement.querySelector<HTMLButtonElement>('button[aria-haspopup="menu"][aria-label^="Show origin"]');
    more?.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, button: 0, ctrlKey: false }));
    const item = await new Promise<HTMLElement | null>((resolve) => {
      const started = Date.now();
      const look = () => {
        const found = document.querySelector<HTMLElement>('[role="menuitemcheckbox"]');
        if (found || Date.now() - started > 2000) resolve(found);
        else requestAnimationFrame(look);
      };
      look();
    });
    item?.click();
  },
};

/**
 * Branches where the line has gone its own way from origin and the last fetch heard nothing, and the landed branches stand
 * otherwise: diverged with an ask that failed since, ahead with no state answered, behind with its pull request abandoned,
 * and one git could not count.
 */
export const BranchesOriginStates: Story = {
  args: {
    start: 'branches',
    branches: branchesAs({
      line: { ...GIT_BRANCHES_ENGINE.line, ahead: 2, behind: 5 },
      fetch: { at: new Date(Date.now() - 26 * 3_600_000).toISOString(), heard: false },
      branches: [GIT_BRANCHES_ENGINE.branches[0]!, ...LANDED_OTHERWISE],
    }),
  },
};

/** Branches of a checkout never fetched, whose line origin holds no copy of. */
export const BranchesNeverFetched: Story = {
  args: { start: 'branches', branches: branchesAs({ line: { ...GIT_BRANCHES_ENGINE.line, origin: null, ahead: null, behind: null }, fetch: null }) },
};

/** Branches where the session records could not be read: said once beside the sessions', and none is named. */
export const BranchesSessionsUnread: Story = {
  args: {
    start: 'branches',
    branches: branchesAs({
      sessionsUnread: true,
      branches: GIT_BRANCHES_ENGINE.branches.map((branch) => (branch.kind === 'session' ? { ...branch, session: null } : branch)),
    }),
  },
};

/** Branches by kind, dark. */
export const BranchesDark: Story = { args: { start: 'branches' }, decorators: [dark] };

/** Branches by kind with the main area at 34 rem: a long name and a commit's subject wrap, never cut. */
export const BranchesNarrow: Story = { args: { start: 'branches' }, decorators: [narrow] };

/** Branches on their way: said, with rows drawn in their place. */
export const BranchesReading: Story = { args: { start: 'branches', branches: branchesRead({ state: 'reading' }) } };

/** Branches of a repository with no checkout on this machine: said at once, and Git is asked nothing. */
export const BranchesNoCheckout: Story = { args: { start: 'branches', branches: branchesRead({ state: 'noCheckout' }) } };

/** Branches Git could not read: the driver's sentence in the tab's place. */
export const BranchesRefused: Story = {
  args: {
    start: 'branches',
    branches: branchesRead({
      state: 'refused',
      sentence: "engine's checkout is not the top of a repository of its own, so git is not asked: it would answer for whichever repository holds it.",
    }),
  },
};

/** Branches of a checkout with no commit yet: none to list. */
export const BranchesNone: Story = {
  args: { start: 'branches', branches: branchesRead({ state: 'read', value: { ...GIT_BRANCHES_ENGINE, branches: [] } }) },
};

/**
 * Branches in 中文: the tab's name, its kinds and their facts; a branch's name, its commit and a session's title are content,
 * shown as they are.
 */
export const BranchesChinese: Story = { args: { start: 'branches' }, decorators: [chinese] };

/** Branches with no checkout here, in 中文. */
export const BranchesNoCheckoutChinese: Story = { args: BranchesNoCheckout.args, decorators: [chinese] };

/**
 * **History** (UX6h1, the built-in Git design §2.3): its line's commits newest first, each beside the lanes laid from its
 * parents: a branch merged into the line has a lane of its own until the commit it grew from, and the merge's dot is hollow.
 */
export const History: Story = { args: { start: 'history' } };

/** History with several branches merged in, one beside another: four lanes at their widest. */
export const HistorySeveralLanes: Story = { args: { start: 'history', history: lineHistory(SEVERAL) } };

/** History over *All branches* (UX6h3c): the ⋯ ticked in the head, whose sentence says the graph is over every local branch and origin's. */
export const AllBranches: Story = { args: { start: 'history', history: allBranchesHistory(SEVERAL) } };

/** *All branches* past the read's 256 tips: the head says it drew the first 256 of the branches. */
export const AllBranchesPastTheLimit: Story = { args: { start: 'history', history: allBranchesHistory(SEVERAL, { shown: 256, total: 300 }) } };
export const AllBranchesDark: Story = { args: AllBranches.args, decorators: [dark] };
export const AllBranchesChinese: Story = { args: AllBranchesPastTheLimit.args, decorators: [chinese] };
export const AllBranchesNarrow: Story = { args: AllBranchesPastTheLimit.args, decorators: [narrow] };

/**
 * **History, a page at a time** (UX6h3a, UXLOADMORE1): the first page's commits with the load-more foot: "{n} commits listed",
 * with no total, since the graph answers none, and *Show more* while it answers `more`. The foot is this story's, and its
 * twins': HistoryMoreLoading, HistoryMoreFailed, and the dark, 中文 and narrow ones.
 */
export const HistoryMore: Story = { args: { start: 'history', history: lineHistory(GIT_HISTORY_PAGES[0], { more: true }) } };

/** History's next page on its way: the status beside *Show more* says so, and the commits listed stay. */
export const HistoryMoreLoading: Story = { args: { start: 'history', history: lineHistory(GIT_HISTORY_PAGES[0], { more: true, loadingMore: true }) } };

/** History's next page refused: the sentence in the foot, the commits kept, *Show more* asking it again. */
export const HistoryMoreFailed: Story = {
  args: {
    start: 'history',
    history: lineHistory(GIT_HISTORY_PAGES[0], {
      more: true,
      moreError: 'Git could not answer for this. This repository may not hold that commit or file yet (a fetch may bring it), or Git did not start.',
    }),
  },
};

/** The foot, dark. */
export const HistoryMoreDark: Story = { args: HistoryMore.args, decorators: [dark] };

/** The foot in 中文. */
export const HistoryMoreChinese: Story = { args: HistoryMore.args, decorators: [chinese] };

/** The foot, narrow: its count, its button and its status wrap in the tab's width. */
export const HistoryMoreNarrow: Story = { args: HistoryMoreFailed.args, decorators: [narrow] };

/** History on its way. */
export const HistoryReading: Story = { args: { start: 'history', history: historyRead({ state: 'reading' }) } };

/** History of a repository with no checkout on this machine. */
export const HistoryNoCheckout: Story = { args: { start: 'history', history: historyRead({ state: 'noCheckout' }) } };

/** History Git did not answer: the refusal's code worded, never git's own sentence. */
export const HistoryRefused: Story = {
  args: {
    start: 'history',
    history: historyRead({
      state: 'refused',
      sentence: 'Git could not answer for this. This repository may not hold that commit or file yet (a fetch may bring it), or Git did not start.',
    }),
  },
};

/** History of a line with no commit in this checkout yet, and of a repository nothing names a line for. */
export const HistoryNoLine: Story = { args: { start: 'history', history: historyRead({ state: 'noLine', line: 'main' }) } };
export const HistoryNoLineNamed: Story = { args: { start: 'history', history: historyRead({ state: 'noLine', line: null }) } };

/** History, dark: the lanes in the theme's faint ink, the dots in its accent. */
export const HistoryDark: Story = { args: HistorySeveralLanes.args, decorators: [dark] };

/** History in 中文: the tab's name, its head and its states; the commits are content. */
export const HistoryChinese: Story = { args: HistorySeveralLanes.args, decorators: [chinese] };

/** History, narrow: the author gives way, the subject truncates, and each row stays one line beside its dot. */
export const HistoryNarrow: Story = { args: HistorySeveralLanes.args, decorators: [narrow] };

/** History, narrow and dark, in 中文. */
export const HistoryNarrowDarkChinese: Story = { args: HistorySeveralLanes.args, decorators: [narrow, dark, chinese] };

/** A browser's page remembered at History: Details alone, since a repository's branches and history are this machine's. */
export const HistoryInABrowser: Story = { args: { ...InABrowser.args, start: 'history' } };

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
 * Setup's *Heavy gates* (HEAVYRUN1b2b, D180's HEAVYRUN1b2a note): this machine's list for it where its own `daoris.gates.json`
 * marks none, open on its own as a list is set, each gate by name with its command and its folder, then *Edit* and *Clear*. Its
 * Save says it landed, so *Edit* shows the list editor whole.
 */
export const SetupHeavyGates: Story = {
  args: {
    start: 'setup',
    setup: {
      ...SETUP_DEFAULTS,
      heavy: {
        gates: [
          { name: 'build', run: 'dotnet build -nodeReuse:false' },
          { name: 'web', run: 'npm test -- --maxWorkers=2', cwd: 'src/web' },
          { name: 'e2e', run: 'npm run e2e', cwd: 'src/web' },
        ],
        onSave: (_gates, answered) => answered.done(),
      },
    },
  },
};

/**
 * Setup's *Heavy gates* where the repository's own `daoris.gates.json` marks heavy gates (HEAVYRUN1b2c): the section says those are run
 * and this list is not, and its folded line cues it "not used".
 */
export const SetupHeavyGatesFileWins: Story = {
  args: {
    start: 'setup',
    setup: {
      ...SETUP_DEFAULTS,
      heavy: {
        gates: [{ name: 'build', run: 'dotnet build -nodeReuse:false' }],
        marks: { marks: true, names: ['web', 'e2e'] },
        onSave: (_gates, answered) => answered.done(),
      },
    },
  },
};

/** Setup's *Heavy gates* where its file does not read: the driver's words, "cannot tell", never "marks none". */
export const SetupHeavyGatesCannotTell: Story = {
  args: {
    start: 'setup',
    setup: {
      ...SETUP_DEFAULTS,
      heavy: {
        gates: [{ name: 'build', run: 'dotnet build -nodeReuse:false' }],
        marks: { marks: null, names: [], refusal: 'daoris.gates.json is not valid JSON: unexpected end of input.' },
        onSave: (_gates, answered) => answered.done(),
      },
    },
  },
};

/** Setup's *Heavy gates* where none is set, folded to its line and offering *Add heavy gates* once opened; narrow, in 中文. */
export const SetupHeavyGatesNoneChinese: Story = {
  args: { start: 'setup', setup: { ...opened('heavy'), heavy: { gates: [], onSave: (_gates, answered) => answered.done() } } },
  decorators: [(Story) => <div className="flex h-[44rem] w-[34rem] max-w-full border border-line bg-page"><Story /></div>, chinese],
};

/**
 * Setup with the person's own values: its line, a branch rule a plugin pushes and accepts automatically, its session
 * language, a standing answer, read by nobody outside it, writing into the game and two rules. Each section holding one
 * opens on its own, with its *Clear*; Driving folds.
 */
export const SetupOwn: Story = { args: { start: 'setup', setup: { ...SETUP_OWN, onOpenPlugin: () => {} } } };

/** Setup folded, narrow: the list a strip and the side bar closed, a section's line wrapping under its name. */
export const SetupNarrow: Story = {
  args: { start: 'setup' },
  decorators: [(Story) => <div className="flex h-[44rem] w-[34rem] max-w-full border border-line bg-page"><Story /></div>],
};

/** The person's own values, narrow: each row stacks below 26 rem (WSR4). */
export const SetupOwnNarrow: Story = { ...SetupNarrow, args: { start: 'setup', setup: SETUP_OWN } };

/** Setup in 中文: 驱动 · 工作 · 会话 · 边界, each default marked in Chinese. */
export const SetupChinese: Story = { args: { start: 'setup', setup: SETUP_OWN }, decorators: [chinese] };

/**
 * **Its process** (DOORS1b, D164's amendment point 3): *Commits* on Line and landing at Daoris's default, and *Helpers* on
 * Sessions from its workspace, each with *Set for this repository*; Line and landing open.
 */
export const SetupProcess: Story = { args: { start: 'setup', setup: opened('work', SETUP_PROCESS) } };

/** Its process with Sessions open: its workspace's helpers rule, two agents in the order they are tried. */
export const SetupProcessSessions: Story = { args: { start: 'setup', setup: opened('sessions', SETUP_PROCESS) } };

/**
 * Its process set for it: a branch named by the ticket its key finds, squashed to one commit, the key's field and the squash
 * switch under the pattern; each commit waiting for the person's yes under their convention, quoted; no helper offered.
 */
export const SetupProcessOwn: Story = { args: { start: 'setup', setup: { ...SETUP_PROCESS_OWN, onOpenPlugin: () => {} } } };

/** Its process set for it, narrow: the landing's fields and the commit row's convention wrap under their rows. */
export const SetupProcessOwnNarrow: Story = { ...SetupNarrow, args: { start: 'setup', setup: SETUP_PROCESS_OWN } };

/** Its process in 中文: 提交方式 and 助手, the convention as written. */
export const SetupProcessChinese: Story = { args: { start: 'setup', setup: SETUP_PROCESS_OWN }, decorators: [chinese] };

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
