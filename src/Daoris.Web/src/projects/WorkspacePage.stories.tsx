import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import type { StartWiring } from '../map/wiring';
import { InTheme } from '../plugins/storyIcons';
import type { LandedBranch, SweepBranch } from '../settings/Sweep';
import { SweepList } from '../settings/Sweep';
import { SyncSection } from '../settings/Sync';
import { chinese } from '../storyLanguage';
import type { HistoryPlan } from '../work/history';
import { WORKSPACE_EMPTY, WORKSPACE_PLAN } from '../work/historyFixtures';
import { WORKSPACE } from '../workflow/fixtures';
import { WorkflowTab } from '../workflow/WorkflowTab';
import { LANGUAGES } from './fixtures';
import { KeptHistory } from './KeptHistory';
import type { WorkspaceTab } from './tabs';
import { WorkspaceDetails, WorkspacePage } from './WorkspacePage';
import { WorkspaceSetup, type WorkspaceSetupProps } from './WorkspaceSetup';

// A workspace's page (UX6g, D150 §4.3) in Repositories' main area, every tab and the states the design names: Details
// with its repositories, what a start runs on and its accounts; Branches with the clean-up and bringing up to date; Setup
// at Daoris's defaults, with the workspace's own values, with the wiring form open, and with the environment naming the
// remote; a browser's page; and the header wired and local. Drawn as `WorkspaceView` draws them, from props.

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
};

/** The page as `WorkspaceView` draws it, its tab chosen here so a story can move between them. */
function Page({ tab: first, setup = DEFAULTS_DAORIS, browser = false, wired = true, kept = WORKSPACE_PLAN }: Args) {
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
              scope={REPOSITORIES.map((repository, at) => ({ repository, workspace: 'work', holds: at < 3 }))}
              onLook={nothing}
              onSync={nothing}
            />
          )}
        />
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

/** Branches: the clean-up across its repositories, and bringing them up to date before anyone has looked. */
export const Branches: Story = { args: { tab: 'branches' } };

/** The main area at a width: 600 px at a 680 px window, the list a strip beside it, and 400 px, its floor. */
const mainAt = (width: number): Decorator => (Story) => <div className="flex" style={{ width }}><Story /></div>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/** Branches at a 680 px window (UXFIX4): each row's three columns, its long name whole over two lines. */
export const BranchesAt680: Story = { args: { tab: 'branches' }, decorators: [mainAt(600)] };

/** The same, in dark. */
export const BranchesAt680Dark: Story = { args: { tab: 'branches' }, decorators: [mainAt(600), dark] };

/** Branches at the main area's 400 px floor (UXFIX4): each row's sentence under its name, every name whole. */
export const BranchesAtTheFloor: Story = { args: { tab: 'branches' }, decorators: [mainAt(400)] };

/** The same, in dark. */
export const BranchesAtTheFloorDark: Story = { args: { tab: 'branches' }, decorators: [mainAt(400), dark] };

/**
 * **Workflow** (WORKFLOW1b, the workflow design §6.1): its Current, its second opinion, its look in `dev` and a branch for
 * the person to push, each step's door opening its own Setup; then its repositories, two with rules of their own.
 */
export const Workflow: Story = { args: { tab: 'workflow' } };

/** Workflow in 中文. */
export const WorkflowChinese: Story = { args: { tab: 'workflow' }, decorators: [chinese] };

/** Workflow at a 680 px window, in dark. */
export const WorkflowAt680Dark: Story = { args: { tab: 'workflow' }, decorators: [mainAt(600), dark] };

/** Workflow at the main area's 400 px floor. */
export const WorkflowAtTheFloor: Story = { args: { tab: 'workflow' }, decorators: [mainAt(400)] };

/** Setup at Daoris's defaults: both sections fold, each value marked as Daoris's. */
export const SetupFolded: Story = { args: { tab: 'setup', wired: false } };

/** Setup with the workspace's own values: a line, a branch rule a plugin pushes and accepts, 中文 sessions, no reading, a remote and two rules. */
export const SetupOwn: Story = { args: { tab: 'setup', setup: DEFAULTS_OWN } };

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
