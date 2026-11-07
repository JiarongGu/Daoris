import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import type { GitLogCommit, GitRepositoryBranches } from '../bridge/repositories/git';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import {
  CJK, COUNTS, ELSEWHERE, ENGINE, GAME, GIT_BRANCHES_ENGINE, GIT_HISTORY_ENGINE, GIT_HISTORY_PAGES, LINE, MIRRORED, NEWBIE, UNDECLARED,
} from './fixtures';
import { ProjectPage, ProjectsMainNotice } from './ProjectPage';
import { BranchesTab, type GitRead, HistoryTab, type HistoryRead } from './git/RepositoryGit';
import type { ProjectTab } from './tabs';

// A repository's page in Repositories' main area: its Details tab (adopted, driven here, held, with its
// line, with unlanded branches, a teammate's with no checkout here, one that declared nothing, one not adopted with a root
// here, one not adopted with none, a browser's, a 中文 name);
// since UX6h1 (§4.4) its Branches and History tabs, each reading, with no checkout here, refused, with nothing yet and read,
// History's lanes for a merged branch and for several, dark, in 中文 and narrow; since UX6h2a Branches by kind, its line's
// and its landed branches' other standings with origin, its session records unread, dark and narrow; and the main area with
// no page: nothing chosen, gone, loading, an error with no answer ever.

const nothing = () => {};

// A flex item of the main area's height frame, with no padding of its own, so the page's scroll box stays bounded.
const dark: Decorator = (Story) => <InTheme theme="dark" className="flex min-h-0 min-w-0 flex-1 p-0"><Story /></InTheme>;

// The main area at the frame's widths (the UI/UX design R6, STORYGAP1): wide by default, `storyWidths.half` a 680 px window
// with the list a strip beside it, and `storyWidths.narrow` the main area's 400 px floor.
const narrow = storyWidths.narrow;

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
    // Its branches and its line's history (UX6h1): a shell's, as Git's reads answer them.
    branches: branchesRead({ state: 'read', value: GIT_BRANCHES_ENGINE }),
    history: lineHistory(GIT_HISTORY_ENGINE),
    onManage: nothing,
    onOpenCode: nothing,
  },
  // The main area's height, inside the main area at its wide width; a story's own width is drawn instead of it.
  decorators: [(Story) => <div className="flex h-[44rem] border border-line bg-page"><Story /></div>, storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof Tabbed>;

/**
 * Adopted, on Details: its summary as its one line, *Open code map* and *Manage* in its header; what the index holds and
 * the commit it was fed from, its workspace, its line read-only with its door to Setup, and its declaration as chips.
 */
export const Adopted: Story = {};

/** Held by the person: *held* outranks *drives here*, which it suspends; the hold is lifted on Setup. */
export const Held: Story = { args: { held: true } };

/** Its own line: the branch its work grows from and lands on, and that it was set for this repository. */
export const WithALine: Story = { args: { line: LINE } };

/** Session branches holding work no branch of the person's holds: named here, listed in Settings. */
export const UnlandedBranches: Story = { args: { line: LINE, unlanded: 2 } };

/** A teammate's registration with no checkout here: *not on this machine*, and nothing printed of anyone's path. */
export const NoRootHere: Story = {
  args: { registration: MIRRORED, counts: COUNTS['studio-tools'], here: false, drivable: false, line: null },
};

/** Adopted, and declared nothing: the warning that an asker would be guessing. */
export const Undeclared: Story = { args: { registration: UNDECLARED, counts: undefined } };

/** The game: drivable, with no feed, since this deployment reads its own checkouts. */
export const AnotherAdopter: Story = { args: { registration: GAME, counts: COUNTS.game } };

/**
 * Not adopted, with a root here: *not adopted* in its head, no *Manage*, and the steps to adopt as text with the
 * reasoning on the info glyph; Setup holds its driving.
 */
export const NotAdopted: Story = { args: { registration: NEWBIE, counts: COUNTS.newbie, drivable: false, onManage: undefined } };

/** Not adopted, with no root here: nowhere to start it. */
export const NotAdoptedNoRoot: Story = {
  args: { registration: ELSEWHERE, counts: undefined, here: false, drivable: false, onManage: undefined, line: null },
};

/** A browser's page: Details alone, no tab row, no *Manage* and no setting, which only a shell's driver says. */
export const InABrowser: Story = {
  args: {
    here: undefined, drivable: undefined, line: undefined, branches: undefined, history: undefined,
    onManage: undefined,
  },
};

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

/** Branches by kind at the main area's 400 px floor: a long name and a commit's subject wrap, never cut. */
export const BranchesNarrow: Story = { args: { start: 'branches' }, decorators: [narrow] };

/** Branches by kind at a 680 px window, in dark. */
export const BranchesHalfDark: Story = { args: { start: 'branches' }, decorators: [storyWidths.half, dark] };

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
