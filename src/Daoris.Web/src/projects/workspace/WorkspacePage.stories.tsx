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
import { GIT_BRANCHES_ENGINE, GIT_BRANCHES_GAME, LANGUAGES } from '../fixtures';
import { KeptHistory } from './KeptHistory';
import { BranchesTab } from '../git/RepositoryGit';
import type { WorkspaceTab } from '../tabs';
import { WorkspaceBranches } from './WorkspaceBranches';
import { WorkspaceDetails, WorkspacePage } from './WorkspacePage';
import { WorkspaceTidied } from './WorkspaceTidied';
import { WorkspaceSetup, type WorkspaceSetupProps } from '../setup/WorkspaceSetup';

// A workspace's page (UX6g, D150 §4.3) in Repositories' main area, every tab and the states the design names: Details
// with its repositories, what a start runs on and its accounts; Branches with each repository's branches (UX6h2b2), the
// clean-up and bringing up to date; Setup
// at Daoris's defaults, with the workspace's own values, with its process (DOORS1b: commits, helpers, the landing's ticket key
// and squash) at Daoris's defaults, set and in 中文, with the wiring form open, and with the environment naming the remote; a
// browser's page; and the header wired and local. Drawn as `WorkspaceView` draws them, from props.

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
 * Each repository's list as `GIT_BRANCHES` answers it for the workspace (UX6h2b2): the engine's whole list, then lists of one
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
 * its page and the line's opens History, as the window's do (UX6h3b).
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

const DEFAULTS_DAORIS: WorkspaceSetupProps = {
  workspace: 'work',
  defaults: {
    line: { onSet: nothing },
    landing: { landers: ['azure-devops-pull-request'], onSet: nothing },
    language: { table: LANGUAGES, onSet: nothing },
    read: { onSet: nothing },
  },
  remote: {
    wiring: { remote: null, fromEnvironment: false, onWire: nothing, onUnwire: nothing },
    rules: { lists: { allow: [], ask: [], deny: [] }, onAdd: nothing, onRemove: nothing },
  },
};

/** Its process at Daoris's defaults (DOORS1b): commits as the work goes, and each helper waiting for the person's yes. */
const DEFAULTS_PROCESS: WorkspaceSetupProps = {
  ...DEFAULTS_DAORIS,
  defaults: { ...DEFAULTS_DAORIS.defaults, commits: { onChange: nothing }, helpers: { onChange: nothing } },
};

/**
 * Its process set for its repositories (DOORS1b): branches named by a ticket and squashed, one commit at the end under the
 * person's convention, helpers started at once from two agents in order.
 */
const DEFAULTS_PROCESS_OWN: WorkspaceSetupProps = {
  ...DEFAULTS_DAORIS,
  defaults: {
    ...DEFAULTS_DAORIS.defaults,
    landing: {
      set: { form: 'branch', pattern: 'feature/{ticket}-{slug}', key: 'TK-\\d+', squash: true, plugin: 'azure-devops-pull-request' },
      landers: ['azure-devops-pull-request'],
      onSet: nothing,
    },
    commits: { set: { mode: 'at-end', convention: 'One commit per ticket: its key, then an imperative summary.' }, onChange: nothing },
    helpers: { set: { mode: 'on', agents: ['codex-acp', 'dsh'] }, onChange: nothing },
  },
};

const DEFAULTS_OWN: WorkspaceSetupProps = {
  ...DEFAULTS_DAORIS,
  defaults: {
    line: { set: 'develop', onSet: nothing },
    landing: {
      set: { form: 'branch', pattern: 'feature/{slug}-{quest}', plugin: 'azure-devops-pull-request', tidy: true, autoAccept: true },
      landers: ['azure-devops-pull-request'],
      onSet: nothing,
    },
    language: { set: 'zh', table: LANGUAGES, onSet: nothing },
    read: { set: false, onSet: nothing },
  },
  remote: {
    wiring: { remote: { url: 'https://work.example.com', key: 'dk_abcd1234…' }, fromEnvironment: false, onWire: nothing, onUnwire: nothing },
    rules: { lists: { allow: ['Bash(npm run test:*)'], ask: ['Bash(az repos pr create:*)'], deny: [] }, onAdd: nothing, onRemove: nothing },
  },
};

type Args = {
  tab: WorkspaceTab;
  setup?: WorkspaceSetupProps;
  /** A browser's page: what it may know, with no tab row. */
  browser?: boolean;
  /** Wired to a deployment, or local only. */
  wired?: boolean;
  /** What this machine keeps of its finished work (HIST1e), as `HISTORY_PLAN` answers it. */
  kept?: HistoryPlan;
  /** The repositories holding a branch of Daoris's, each a group on Branches (UX6h2b2); the rest are apart. */
  held?: string[];
  /** What the look cleaned up on its own, from the machine log (AUTOTIDY1a). */
  tidied?: LogRow[];
};

/** The page as `WorkspaceView` draws it, its tab chosen here so a story can move between them. */
function Page({ tab: first, setup = DEFAULTS_DAORIS, browser = false, wired = true, kept = WORKSPACE_PLAN, held = REPOSITORIES.slice(0, 3), tidied }: Args) {
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
          { agent: 'claude-code', product: 'Claude Code', own: true, accounts: ['home@example.invalid', 'work@example.invalid'] },
          { agent: 'codex', product: 'Codex', own: false, accounts: ['me@example.invalid'] },
        ]}
        onOpenAgent={nothing}
      />
      {/* What this machine keeps of its finished work, and its clear (HIST1e): a shell's, so a browser's page has none. */}
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
          {/* Each repository's branches, a group per repository (UX6h2b2), above the clean-up and the look. */}
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
              // A long name, as a branch rule's slug makes one: whole at every width (UXFIX4).
              landed({ branch: 'feature/report-totals-by-region-for-the-quarter-close-0fda19', kind: 'inside', where: 'feature/report-totals-0fda18', removable: true }),
            ]}
            onLook={nothing}
            onClean={nothing}
            sync={(
              <SyncSection
                scope={REPOSITORIES.map((repository) => ({ repository, workspace: 'work', holds: held.includes(repository) }))}
                // The groups' fold above is the one list apart (UX6h2b3): Updates says how many it fetches, and draws none.
                apartFolded
                onLook={nothing}
                onSync={nothing}
              />
            )}
          />
        </>
      )
      : tab === 'workflow'
        ? (
          // Its Current (WORKFLOW1b), two of its repositories setting rules of their own.
          <WorkflowTab
            page="workspace"
            name="work"
            current={{
              ...WORKSPACE,
              workspace: 'work',
              repositories: REPOSITORIES.map((repository) => ({ repository, own: repository === 'billing' || repository === 'reports' })),
            }}
            doors={{ setup: () => setTab('setup'), repository: nothing }}
          />
        )
      : <WorkspaceSetup {...setup} />;
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
 * *Kept on this machine* with *Clear history…* (HIST1e).
 */
export const Details: Story = {};

/** Details in 中文: *本机保留的记录* and *清除记录…*. */
export const DetailsChinese: Story = { decorators: [chinese] };

/** Details on a workspace with nothing finished: the reading says so, and no press is offered. */
export const DetailsNothingKept: Story = { args: { kept: WORKSPACE_EMPTY } };

/**
 * Branches: each of the three repositories holding a branch of Daoris's as a group headed by its door, its line and its
 * branches by kind (UX6h2b2); the four others with a checkout here folded apart, each drawn once ticked and included (the
 * game's list stands in for one), the one fold of them (UX6h2b3: *Updates* below draws no list apart of its own and says how
 * many it will fetch); then the clean-up across its repositories, and bringing them up to date before anyone has looked.
 */
export const Branches: Story = { args: { tab: 'branches' } };

const HOURS = 3_600_000;
const tidiedLine = (event: 'branch.tidied' | 'branch.kept', hoursAgo: number, data: LogRow['data']): LogRow => ({
  time: new Date(Date.now() - hoursAgo * HOURS).toISOString(), source: 'driver', level: event === 'branch.kept' ? 'warn' : 'info', event,
  data: { workspace: 'work', ...data },
});

/** What the look cleaned up on its own, newest first (AUTOTIDY1a): seven rows, five shown and *Show 2 more*; one has a tree set aside. */
const TIDIED: LogRow[] = [
  tidiedLine('branch.tidied', 1, { repository: 'billing', branch: 'daoris/s-1f2e3d4c', tree: true, folder: 'trees/.tidied/work/billing/s-1' }),
  tidiedLine('branch.kept', 2, { repository: 'api', branch: 'daoris/s-9e0f1a2b', why: 'busy' }),
  tidiedLine('branch.tidied', 5, { repository: 'reports', branch: 'daoris/s-7a8b9c0d', tree: false, folder: null }),
  tidiedLine('branch.kept', 9, { repository: 'billing', branch: null, why: 'starting' }),
  tidiedLine('branch.tidied', 26, { repository: 'api', branch: 'daoris/s-3c4d5e6f', tree: true, folder: 'trees/.tidied/work/api/s-3' }),
  tidiedLine('branch.kept', 30, { repository: 'reports', branch: 'daoris/s-2b3c4d5e', why: 'hidden' }),
  tidiedLine('branch.tidied', 50, { repository: 'billing', branch: 'daoris/s-0a1b2c3d', tree: false, folder: null }),
];

/** Branches with what the look cleaned up on its own, and left, after the groups and above the clean-up (AUTOTIDY1a). */
export const BranchesCleanedUp: Story = { args: { tab: 'branches', tidied: TIDIED } };

/** The installed window's case (AUTOTIDY1a2): one branch kept `unread` and logged at each of seven starts is one row, with its cause and since. */
const KEPT_EVERY_START: LogRow[] = Array.from({ length: 7 }, (_, index) =>
  tidiedLine('branch.kept', 3 + index * 24, { repository: 'api', branch: 'daoris/s-9e0f1a2b', why: 'unread', cause: 'folder' }));

export const BranchesKeptEveryStart: Story = { args: { tab: 'branches', tidied: KEPT_EVERY_START } };

/** Branches of six repositories holding Daoris's: five groups, the sixth one press away (*Show 1 more*), and one apart. */
export const BranchesManyRepositories: Story = { args: { tab: 'branches', held: REPOSITORIES.slice(0, 6) } };

/** Branches in 中文: the groups' words, the fold apart and its *纳入*. */
export const BranchesChinese: Story = { args: { tab: 'branches' }, decorators: [chinese] };

// The main area at the frame's widths (the UI/UX design R6, UIFIT1): `storyWidths.half` is about 630 px, a 680 px
// window with the list a strip beside it, and `storyWidths.narrow` the 400 px floor.
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/** Branches at a 680 px window (UXFIX4): each row's three columns, its long name whole over two lines. */
export const BranchesAt680: Story = { args: { tab: 'branches' }, decorators: [storyWidths.half] };

/** The same, in dark. */
export const BranchesAt680Dark: Story = { args: { tab: 'branches' }, decorators: [storyWidths.half, dark] };

/**
 * Branches at the main area's 400 px floor (UXFIX4): each row's sentence under its name, every name whole; each group's
 * branch names and subjects wrap rather than widen the tab, and the fold apart lays its boxes one to a line (UX6h2b2).
 */
export const BranchesAtTheFloor: Story = { args: { tab: 'branches' }, decorators: [storyWidths.narrow] };

/** The same, in dark. */
export const BranchesAtTheFloorDark: Story = { args: { tab: 'branches' }, decorators: [storyWidths.narrow, dark] };

/**
 * **Workflow** (WORKFLOW1b, the workflow design §6.1): its Current, its second opinion, its look in `dev` and a branch for
 * the person to push, each step's door opening its own Setup; then its repositories, two with rules of their own.
 */
export const Workflow: Story = { args: { tab: 'workflow' } };

/** Workflow in 中文. */
export const WorkflowChinese: Story = { args: { tab: 'workflow' }, decorators: [chinese] };

/** Workflow at a 680 px window, in dark. */
export const WorkflowAt680Dark: Story = { args: { tab: 'workflow' }, decorators: [storyWidths.half, dark] };

/** Workflow at the main area's 400 px floor. */
export const WorkflowAtTheFloor: Story = { args: { tab: 'workflow' }, decorators: [storyWidths.narrow] };

/** Setup at Daoris's defaults: both sections fold, each value marked as Daoris's. */
export const SetupFolded: Story = { args: { tab: 'setup', wired: false } };

// Setup's open sections at the frame's widths (the UI/UX design R1, R6; UIFIT1): each section's rows on one label track,
// values beside their words at the half width and under them at the floor. `measured/settings.spec.ts` reads them.

/** Setup with its own values, at the half width. */
export const SetupOwnHalf: Story = { args: { tab: 'setup', setup: { ...DEFAULTS_OWN, onOpenPlugin: nothing } }, decorators: [storyWidths.half] };

/** Setup with its own values, at the 400 px floor. */
export const SetupOwnNarrow: Story = { args: { tab: 'setup', setup: { ...DEFAULTS_OWN, onOpenPlugin: nothing } }, decorators: [storyWidths.narrow] };

/** The process defaults open, in 中文, at the half width. */
export const SetupProcessChineseHalf: Story = { args: { tab: 'setup', wired: false, setup: DEFAULTS_PROCESS_OWN }, decorators: [storyWidths.half, chinese] };

/** Its process at Daoris's defaults (DOORS1b): *Commits* and *Helpers* among the Defaults, each offering *Set for this workspace*. */
export const SetupProcess: Story = { args: { tab: 'setup', wired: false, setup: { ...DEFAULTS_PROCESS, open: 'defaults' } } };

/** Its process set for its repositories: the landing's ticket key and squash, its commit rule and convention, its helpers. */
export const SetupProcessOwn: Story = { args: { tab: 'setup', wired: false, setup: DEFAULTS_PROCESS_OWN } };

/** Its process set, in 中文. */
export const SetupProcessChinese: Story = { args: { tab: 'setup', wired: false, setup: DEFAULTS_PROCESS_OWN }, decorators: [chinese] };

/** Setup with the workspace's own values: a line, a branch rule a plugin pushes and accepts, 中文 sessions, no reading, a remote and two rules. */
export const SetupOwn: Story = { args: { tab: 'setup', setup: { ...DEFAULTS_OWN, onOpenPlugin: nothing } } };

/** The header's *Wire to a remote…*: Setup at its remote, the form open. */
export const Wiring: Story = { args: { tab: 'setup', wired: false, setup: { ...DEFAULTS_DAORIS, wiring: true } } };

/** The environment names this machine's remote whole, and the file is not read: the row says which source decided. */
export const FromTheEnvironment: Story = {
  args: {
    tab: 'setup',
    setup: { ...DEFAULTS_OWN, open: 'remote', remote: { ...DEFAULTS_OWN.remote, wiring: { ...DEFAULTS_OWN.remote.wiring!, fromEnvironment: true } } },
  },
};

/** Local only: the header offers *Wire to a remote…* where a wired one offers *Sync now*. */
export const LocalOnly: Story = { args: { wired: false } };

/** A browser's page: its repositories and that it syncs, never where, with no tab row and no act (D47 §4, D48 §5). */
export const InABrowser: Story = { args: { browser: true } };
