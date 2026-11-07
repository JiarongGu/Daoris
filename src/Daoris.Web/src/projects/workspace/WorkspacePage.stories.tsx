import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import type { StartWiring } from '../../map/wiring';
import { InTheme } from '../../plugins/storyIcons';
import type { LogRow } from '../../settings/Logs';
import type { LandedBranch, SweepBranch } from '../../settings/Sweep';
import { SweepList } from '../../settings/Sweep';
import { SyncSection } from '../../settings/Sync';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import type { HistoryPlan } from '../../work/history/history';
import { WORKSPACE_EMPTY, WORKSPACE_PLAN } from '../../work/history/historyFixtures';
import { WORKSPACE } from '../../workflow/fixtures';
import { WorkflowTab } from '../../workflow/WorkflowTab';
import type { BranchListValue } from '../git/BranchList';
import { GIT_BRANCHES_ENGINE, GIT_BRANCHES_GAME } from '../fixtures';
import { KeptHistory } from './KeptHistory';
import { BranchesTab } from '../git/RepositoryGit';
import type { WorkspaceTab } from '../tabs';
import { WorkspaceBranches } from './WorkspaceBranches';
import { WorkspaceDetails, WorkspacePage } from './WorkspacePage';
import { WorkspaceTidied } from './WorkspaceTidied';

// A workspace's page in Repositories' main area, every tab and the states the design names: Details
// with its repositories, what a start runs on and its accounts; Branches with each repository's branches, the
// clean-up and bringing up to date; Workflow; a browser's page; and the header wired and local. Drawn as `WorkspaceView` draws
// them, from props.

const nothing = () => {};

const START = (job: 'work' | 'intake', over: Partial<StartWiring> = {}): StartWiring => ({
  job, workspace: 'work', adapter: 'claude-code', owner: 'claude-code', product: 'Claude Code', profile: 'account-2',
  profileFrom: 'workspace', version: 'claude 9.9.9', versionFrom: 'machine', commanded: false, refusal: null, ...over,
});

const REPOSITORIES = ['api', 'billing', 'gateway', 'portal', 'reports', 'search', 'workers'];

const branch = (over: Partial<SweepBranch> & Pick<SweepBranch, 'branch' | 'kind'>): SweepBranch => ({
  repository: 'api', workspace: 'work', hasTree: true, commits: 0, removable: false, ...over,
});
const landed = (over: Partial<LandedBranch> & Pick<LandedBranch, 'branch' | 'kind'>): LandedBranch => ({
  repository: 'api', workspace: 'work', files: [], commits: 1, removable: false, ...over,
});

/**
 * Each repository's list as `GIT_BRANCHES` answers it for the workspace: the engine's whole list, then lists of one
 * or two kinds; one apart, once included, is the game's (no branch of Daoris's).
 */
const [LINE_BRANCH, ...ENGINE_REST] = GIT_BRANCHES_ENGINE.branches;
const kinds = (...these: string[]) => [LINE_BRANCH!, ...ENGINE_REST.filter((each) => these.includes(each.kind))];
const LISTS: Record<string, BranchListValue> = {
  api: GIT_BRANCHES_ENGINE,
  billing: { ...GIT_BRANCHES_ENGINE, branches: kinds('session') },
  gateway: { ...GIT_BRANCHES_ENGINE, line: { ...GIT_BRANCHES_ENGINE.line, ahead: 0, behind: 3 }, branches: kinds('landed') },
  portal: { ...GIT_BRANCHES_ENGINE, fetch: null, branches: kinds('session', 'yours') },
  reports: { ...GIT_BRANCHES_ENGINE, branches: kinds('landed', 'origin') },
  search: { ...GIT_BRANCHES_ENGINE, branches: kinds('session') },
};
const TITLES = { 'daoris/engine-physics-step': 'Expose a streaming budget' };

/**
 * A group's list as its holder draws it: the read list, or the game's for one the person included; each branch's name opens
 * its page and the line's opens History, as the window's do.
 */
const group = (repository: string, included: boolean) => (
  <BranchesTab
    repository={repository}
    read={{ state: 'read', value: { ...(included ? GIT_BRANCHES_GAME : LISTS[repository] ?? GIT_BRANCHES_ENGINE), workspace: 'work' } }}
    titles={TITLES}
    onOpen={nothing}
    onOpenHistory={nothing}
  />
);

type Args = {
  tab: WorkspaceTab;
  /** A browser's page: what it may know, with no tab row. */
  browser?: boolean;
  /** Wired to a deployment, or local only. */
  wired?: boolean;
  /** What this machine keeps of its finished work, as `HISTORY_PLAN` answers it. */
  kept?: HistoryPlan;
  /** The repositories holding a branch of Daoris's, each a group on Branches; the rest are apart. */
  held?: string[];
  /** What the look cleaned up on its own, from the machine log. */
  tidied?: LogRow[];
};

/** The page as `WorkspaceView` draws it, its tab chosen here so a story can move between them. */
function Page({ tab: first, browser = false, wired = true, kept = WORKSPACE_PLAN, held = REPOSITORIES.slice(0, 3), tidied }: Args) {
  const [tab, setTab] = useState<WorkspaceTab>(first);
  const details = (
    <>
      <WorkspaceDetails
        repositories={REPOSITORIES}
        onOpen={nothing}
        starts={browser ? null : {
          list: [START('work'), START('intake', { profile: null, profileFrom: 'unset', refusal: null })],
          nameOf: (_owner, profile) => (profile ? `${profile === 'account-2' ? 'home' : 'work'}@example.invalid` : 'own@example.invalid'),
        }}
        accounts={browser ? null : [
          { agent: 'claude-code', product: 'Claude Code', accounts: ['home@example.invalid', 'work@example.invalid'] },
          { agent: 'codex', product: 'Codex', accounts: ['me@example.invalid'] },
        ]}
        onOpenAgent={nothing}
      />
      {/* What this machine keeps of its finished work, and its clear: a shell's, so a browser's page has none. */}
      {!browser && (
        <KeptHistory
          workspace="work"
          plan={{ ...kept, id: 'work', reading: kept.reading && { ...kept.reading, workspace: 'work' } }}
          doors={{ branches: () => setTab('branches'), sync: wired ? nothing : undefined, quest: nothing, ask: nothing, session: nothing }}
          onClear={nothing}
        />
      )}
    </>
  );
  const body = browser || tab === 'details'
    ? details
    : tab === 'branches'
      ? (
        <>
          {/* Each repository's branches, a group per repository, above the clean-up and the look. */}
          <WorkspaceBranches
            workspace="work"
            read={{ state: 'read', value: { held, apart: REPOSITORIES.filter((repository) => !held.includes(repository)) } }}
            group={group}
            onOpen={nothing}
          />
          <WorkspaceTidied lines={tidied ?? null} />
          <SweepList
            branches={[
              branch({ branch: 'daoris/s-1f2e3d4c', kind: 'empty', where: 'develop', removable: true }),
              branch({ repository: 'billing', branch: 'daoris/s-9e0f1a2b', kind: 'unlanded', commits: 2 }),
              branch({ repository: 'reports', branch: 'daoris/s-7a8b9c0d', kind: 'in-use' }),
            ]}
            landed={[
              landed({ branch: 'feature/report-totals-0fda18', kind: 'on-line', where: 'origin/develop', removable: true }),
              // A long name, as a branch rule's slug makes one: whole at every width.
              landed({ branch: 'feature/report-totals-by-region-for-the-quarter-close-0fda19', kind: 'inside', where: 'feature/report-totals-0fda18', removable: true }),
            ]}
            onLook={nothing}
            onClean={nothing}
            sync={(
              <SyncSection
                scope={REPOSITORIES.map((repository) => ({ repository, workspace: 'work', holds: held.includes(repository) }))}
                // The groups' fold above is the one list apart: Updates says how many it fetches, and draws none.
                apartFolded
                onLook={nothing}
                onSync={nothing}
              />
            )}
          />
        </>
      )
      : (
          // Its Current, two of its repositories setting rules of their own.
          <WorkflowTab
            page="workspace"
            name="work"
            current={{
              ...WORKSPACE,
              workspace: 'work',
              repositories: REPOSITORIES.map((repository) => ({ repository, own: repository === 'billing' || repository === 'reports' })),
            }}
            doors={{ setup: nothing, repository: nothing }}
          />
        );
  return (
    <WorkspacePage
      workspace="work"
      repositories={REPOSITORIES.length}
      remote={browser ? {} : wired ? { host: 'work.example.com' } : null}
      tab={browser ? undefined : tab}
      onTab={browser ? undefined : setTab}
      onSync={!browser && wired ? nothing : undefined}
      onWire={!browser && !wired ? nothing : undefined}
    >
      {body}
    </WorkspacePage>
  );
}

const meta: Meta<typeof Page> = {
  title: 'Repositories/WorkspacePage',
  component: Page,
  args: { tab: 'details' },
  // Repositories' main area beside a list: the page is its own column, as the frame lays it out.
  // Its height grows with the page, so a shot shows a tab whole.
  decorators: [(Story) => <div className="flex min-h-[48rem] max-w-full border border-line bg-page"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof Page>;

/**
 * Details: its seven repositories as doors, what a start and an intake run on, the accounts each agent may run here, and
 * *Kept on this machine* with *Clear history…*.
 */
export const Details: Story = {};

/** Details in 中文: *本机保留的记录* and *清除记录…*. */
export const DetailsChinese: Story = { decorators: [chinese] };

/** Details on a workspace with nothing finished: the reading says so, and no press is offered. */
export const DetailsNothingKept: Story = { args: { kept: WORKSPACE_EMPTY } };

/**
 * Branches: each of the three repositories holding a branch of Daoris's as a group headed by its door, its line and its
 * branches by kind; the four others with a checkout here folded apart, each drawn once ticked and included (the
 * game's list stands in for one), the one fold of them (*Updates* below draws no list apart of its own and says how
 * many it will fetch); then the clean-up across its repositories, and bringing them up to date before anyone has looked.
 */
export const Branches: Story = { args: { tab: 'branches' } };

const HOURS = 3_600_000;
const tidiedLine = (event: 'branch.tidied' | 'branch.kept', hoursAgo: number, data: LogRow['data']): LogRow => ({
  time: new Date(Date.now() - hoursAgo * HOURS).toISOString(), source: 'driver', level: event === 'branch.kept' ? 'warn' : 'info', event,
  data: { workspace: 'work', ...data },
});

/** What the look cleaned up on its own, newest first: seven rows, five shown and *Show 2 more*; one has a tree set aside. */
const TIDIED: LogRow[] = [
  tidiedLine('branch.tidied', 1, { repository: 'billing', branch: 'daoris/s-1f2e3d4c', tree: true, folder: 'trees/.tidied/work/billing/s-1' }),
  tidiedLine('branch.kept', 2, { repository: 'api', branch: 'daoris/s-9e0f1a2b', why: 'busy' }),
  tidiedLine('branch.tidied', 5, { repository: 'reports', branch: 'daoris/s-7a8b9c0d', tree: false, folder: null }),
  tidiedLine('branch.kept', 9, { repository: 'billing', branch: null, why: 'starting' }),
  tidiedLine('branch.tidied', 26, { repository: 'api', branch: 'daoris/s-3c4d5e6f', tree: true, folder: 'trees/.tidied/work/api/s-3' }),
  tidiedLine('branch.kept', 30, { repository: 'reports', branch: 'daoris/s-2b3c4d5e', why: 'hidden' }),
  tidiedLine('branch.tidied', 50, { repository: 'billing', branch: 'daoris/s-0a1b2c3d', tree: false, folder: null }),
];

/** Branches with what the look cleaned up on its own, and left, after the groups and above the clean-up. */
export const BranchesCleanedUp: Story = { args: { tab: 'branches', tidied: TIDIED } };

/** One branch kept `unread` and logged at each of seven starts is one row, with its cause and since. */
const KEPT_EVERY_START: LogRow[] = Array.from({ length: 7 }, (_, index) =>
  tidiedLine('branch.kept', 3 + index * 24, { repository: 'api', branch: 'daoris/s-9e0f1a2b', why: 'unread', cause: 'folder' }));

export const BranchesKeptEveryStart: Story = { args: { tab: 'branches', tidied: KEPT_EVERY_START } };

/** Branches of six repositories holding Daoris's: five groups, the sixth one press away (*Show 1 more*), and one apart. */
export const BranchesManyRepositories: Story = { args: { tab: 'branches', held: REPOSITORIES.slice(0, 6) } };

/** Branches in 中文: the groups' words, the fold apart and its *纳入*. */
export const BranchesChinese: Story = { args: { tab: 'branches' }, decorators: [chinese] };

// The main area at the frame's widths: `storyWidths.half` is about 630 px, a 680 px
// window with the list a strip beside it, and `storyWidths.narrow` the 400 px floor.
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/** Branches at a 680 px window: each row's three columns, its long name whole over two lines. */
export const BranchesAt680: Story = { args: { tab: 'branches' }, decorators: [storyWidths.half] };

/** The same, in dark. */
export const BranchesAt680Dark: Story = { args: { tab: 'branches' }, decorators: [storyWidths.half, dark] };

/**
 * Branches at the main area's 400 px floor: each row's sentence under its name, every name whole; each group's
 * branch names and subjects wrap rather than widen the tab, and the fold apart lays its boxes one to a line.
 */
export const BranchesAtTheFloor: Story = { args: { tab: 'branches' }, decorators: [storyWidths.narrow] };

/** The same, in dark. */
export const BranchesAtTheFloorDark: Story = { args: { tab: 'branches' }, decorators: [storyWidths.narrow, dark] };

/**
 * **Workflow**: its Current, its second opinion, its look in `dev` and a branch for
 * the person to push, each step's door opening its own Setup; then its repositories, two with rules of their own.
 */
export const Workflow: Story = { args: { tab: 'workflow' } };

/** Workflow in 中文. */
export const WorkflowChinese: Story = { args: { tab: 'workflow' }, decorators: [chinese] };

/** Workflow at a 680 px window, in dark. */
export const WorkflowAt680Dark: Story = { args: { tab: 'workflow' }, decorators: [storyWidths.half, dark] };

/** Workflow at the main area's 400 px floor. */
export const WorkflowAtTheFloor: Story = { args: { tab: 'workflow' }, decorators: [storyWidths.narrow] };

/** Local only: the header offers *Wire to a remote…* where a wired one offers *Sync now*. */
export const LocalOnly: Story = { args: { wired: false } };

/** A browser's page: its repositories and that it syncs, never where, with no tab row and no act. */
export const InABrowser: Story = { args: { browser: true } };
