import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useTidiedBranches } from '../../bridge/log';
import { sentence } from '../../locales/format';
import { useSessions, useSyncStanding } from '../../data/queries';
import { sessionTitle } from '../../work/identity';
import { accessToast, isFull, resolveAccess } from '../../settings/repository/Access';
import { commitToast } from '../../settings/repository/Commits';
import { helperToast } from '../../settings/repository/Helpers';
import { handOverToast } from '../../settings/repository/HandOver';
import type { LandingRule } from '../../settings/repository/Landings';
import { opinionToast } from '../../settings/repository/Opinions';
import { reviewToast } from '../../settings/repository/Reviews';
import { namer } from '../../settings/accounts/namer';
import { sayDiscard, sweepKey, SweepList } from '../../settings/Sweep';
import { SyncSection } from '../../settings/Sync';
import {
  stoppedWaiting, useAccounts, useAcross, useDriver, useHarnesses, useHistoryPlan, usePlugins, useRemotes, useRuleAction, useRules,
  useDiscardSessionBranch, useDropRecoveries, useSetAccess, useSetCommits, useSetHandOver, useSetHelpers, useSetLanding, useSetLanguage, useSetLine, useSetOpinion,
  useSetReadAcross,
  useSetReview, useStarts,
  useSweep, useSweepLook, useSweepPlan, useTreesSync, useTreesSyncPlan, useTreesSyncScope, useUnwireRemote, useWireRemote, useWorkflowCurrent,
  useWorkState,
} from '../../bridge/shell';
import { byTool } from '../../agents/tools';
import { failure, type Notify, useErrorNotify } from '../../design/ui';
import type { KeptDoors } from '../../work/history/ClearAsk';
import { useHistoryActs } from '../../work/history/historyActs';
import { WorkflowTab } from '../../workflow/WorkflowTab';
import { KeptHistory } from './KeptHistory';
import { WorkspaceBranchGroups } from '../git/RepositoryGitView';
import { InclusionContext, useInclusion } from './WorkspaceBranches';
import { WorkspaceRecoveries } from './WorkspaceRecoveries';
import { WorkspaceTidied } from './WorkspaceTidied';
import type { WorkspaceSection, WorkspaceTab } from '../tabs';
import { accountsHere, hostOf } from './workspace';
import { WorkspaceDetails, WorkspacePage } from './WorkspacePage';
import { WorkspaceSetup, type WorkspaceSetupProps } from '../setup/WorkspaceSetup';

/** Matched without case, as the driver matches a workspace's name. */
const same = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();

/** The rows of a driver answer that are this workspace's. */
const mine = <R extends { workspace: string }>(rows: readonly R[], workspace: string) => rows.filter((row) => same(row.workspace, workspace));

/** A workspace's landing rule as the driver answers it, without the name it is answered under, its key and squash kept (DOORS1b). */
const ruleOf = ({ form, pattern, tidy, plugin, autoAccept, key, squash }: LandingRule): LandingRule => ({
  form, ...(pattern ? { pattern } : {}), ...(tidy ? { tidy } : {}), ...(plugin ? { plugin } : {}), ...(autoAccept ? { autoAccept } : {}),
  ...(key ? { key } : {}), ...(squash ? { squash } : {}),
});

/**
 * **A workspace's page, with its data** (UX6g, D150 §4.3): the organism that holds what the page reads and every press it
 * makes, so the page, its Details and its Setup below it hold none (components §2). Each tab is a part of its own, drawn
 * only while it shows, so a tab asks the driver nothing until it is open: the starts and the accounts on Details, the
 * clean-up's list and the look's scope on Branches, its Current workflow on Workflow (WORKFLOW1b), reading across, the rules
 * and the plugins on Setup.
 *
 * @remarks
 * **Every control is the screen's half of a terminal verb** (D50): `daoris driver line|landing|language|across --workspace`,
 * `daoris remote add|remove`, `daoris agent rules … --workspace`, `daoris-driver trees clean|sync`, and a failed attempt's
 * branch discarded as `daoris-driver trees remove <branch> --repository <name> --force` (LAND3b). A refusal is the driver's
 * sentence, in a toast. They are where Settings → Workspace and Settings → Permissions held them (§3.1).
 *
 * **A browser is handed what it may know** (D47 §4): its repositories, and whether it syncs, read from this machine's own
 * host without a host's name (D48 §5). No tab, no branch, no setting and no account.
 *
 * **Its Details reads what this machine keeps of its finished work, and clears it** (HIST1e, D153 §6.1): the reading, and
 * *Clear history…* while anything may go, `daoris-driver history clear --workspace`'s screen twin (D50). A kept unit's door
 * opens its Branches, *Sync now*, or the quest's, the ask's or the session's page where the application hands those doors.
 */
export function WorkspaceView({
  workspace, repositories, attached, tab, onTab, asked = null, notify, onOpenRepository, onOpenRepositoryWorkflow, onOpenBranch,
  onOpenRepositoryHistory, onOpenAgent, onOpenPlugin, onSyncNow, syncing = false, onOpenQuest, onOpenAsk, onAttend,
}: {
  workspace: string;
  /** Its repositories in the list's registry, by name. */
  repositories: string[];
  /** A shell answers: only then are its branches, its setup and its accounts this page's. */
  attached: boolean;
  tab: WorkspaceTab;
  onTab: (tab: WorkspaceTab) => void;
  /** The Setup section a door asked to see open. */
  asked?: WorkspaceSection | null;
  notify: Notify;
  onOpenRepository: (repository: string) => void;
  /** Open a repository's page at its Workflow tab: the door from a repository that sets rules of its own (WORKFLOW1b). */
  onOpenRepositoryWorkflow?: (repository: string) => void;
  /** Open a branch's page on its repository's, whose way back is its Branches (UX6h3b): a branch's name in Branches. */
  onOpenBranch?: (repository: string, branch: string) => void;
  /** Open a repository's page at its History: its line's row in Branches. */
  onOpenRepositoryHistory?: (repository: string) => void;
  /** Open an agent's page, where a workspace's accounts are set (D130 §3.2, UX6e). */
  onOpenAgent?: (agent: string) => void;
  /** Open a plugin's page, from the plugin a landing rule names (PLUGUI1c2c). */
  onOpenPlugin?: (plugin: string) => void;
  /** The status bar's *Sync now* (SYNC6b), the application's, which says the pass's own words. */
  onSyncNow?: (workspace: string) => void;
  syncing?: boolean;
  /** Open a quest's page, an ask's, or a session in Sessions: the doors that free a unit its clear keeps (HIST1e). */
  onOpenQuest?: (id: string) => void;
  onOpenAsk?: (id: string) => void;
  onAttend?: (session: string) => void;
}) {
  const remotes = useRemotes();
  // Where it syncs, for a browser, from this machine's own host: wired or not, never where (D48 §5).
  const standing = useSyncStanding(attached ? null : workspace);
  // The header's *Wire to a remote…*: Setup at its remote, the form open.
  const [wiring, setWiring] = useState(false);
  // A Workflow step's door: Setup at its defaults, where the workspace's rules are set (WORKFLOW1b).
  const [askedHere, setAskedHere] = useState<WorkspaceSection | null>(null);
  const map = remotes.data && Array.isArray(remotes.data.remotes) ? remotes.data : null;
  const wired = map?.remotes.find((row) => same(row.workspace, workspace)) ?? null;
  const remote = attached
    ? map ? wired ? { host: hostOf(wired.url) } : null : undefined
    : standing.data ? standing.data.wired ? {} : null : undefined;

  const chooseTab = (next: WorkspaceTab) => {
    setWiring(false);
    setAskedHere(null);
    onTab(next);
  };
  const onSync = attached && wired && onSyncNow ? () => onSyncNow(workspace) : undefined;

  const body = !attached
    ? <WorkspaceDetails repositories={repositories} onOpen={onOpenRepository} />
    : tab === 'branches'
      ? (
        <BranchesPart
          workspace={workspace}
          repositories={repositories}
          notify={notify}
          onOpenRepository={onOpenRepository}
          onOpenBranch={onOpenBranch}
          onOpenHistory={onOpenRepositoryHistory}
        />
      )
      : tab === 'workflow'
        ? (
          <WorkflowPart
            workspace={workspace}
            onSetup={() => { setWiring(false); setAskedHere('defaults'); onTab('setup'); }}
            onOpenRepository={onOpenRepositoryWorkflow}
          />
        )
      : tab === 'setup'
        ? <SetupPart workspace={workspace} notify={notify} open={wiring ? 'remote' : askedHere ?? asked ?? undefined} wiring={wiring} onOpenPlugin={onOpenPlugin} />
        : (
          <DetailsPart
            workspace={workspace}
            repositories={repositories}
            notify={notify}
            onOpenRepository={onOpenRepository}
            onOpenAgent={onOpenAgent}
            doors={{ branches: () => chooseTab('branches'), sync: onSync, quest: onOpenQuest, ask: onOpenAsk, session: onAttend }}
          />
        );

  return (
    <WorkspacePage
      workspace={workspace}
      repositories={repositories.length}
      remote={remote}
      tab={attached ? tab : undefined}
      onTab={attached ? chooseTab : undefined}
      syncing={syncing}
      onSync={onSync}
      onWire={attached && map && !wired ? () => { setWiring(true); onTab('setup'); } : undefined}
    >
      {body}
    </WorkspacePage>
  );
}

/**
 * Details: its repositories, what a start in it runs on (MAP1b), the accounts its work may run on (D130 §3.2), and what this
 * machine keeps of its finished work with its clear (HIST1e, D153 §6.1), read only while Details shows, since the reading
 * walks the home's files.
 */
function DetailsPart({ workspace, repositories, notify, onOpenRepository, onOpenAgent, doors }: {
  workspace: string;
  repositories: string[];
  notify: Notify;
  onOpenRepository: (repository: string) => void;
  onOpenAgent?: (agent: string) => void;
  /** The doors that free a unit the clear keeps. */
  doors: KeptDoors;
}) {
  const { t } = useTranslation();
  const roster = useHarnesses();
  const accounts = useAccounts();
  const answer = useStarts([workspace]);
  const history = useHistoryPlan({ scope: 'workspace', id: workspace });
  const historyActs = useHistoryActs({ notify });
  useErrorNotify(answer.error, notify);
  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  const nameOf = namer(t, harnesses);
  // An older shell has never heard of the question: the section is absent rather than the page blank.
  const starts = Array.isArray(answer.data?.starts) ? mine(answer.data.starts, workspace) : null;
  return (
    <>
      <WorkspaceDetails
        repositories={repositories}
        onOpen={onOpenRepository}
        starts={starts && { list: starts, nameOf }}
        accounts={roster.data ? accountsHere(byTool(harnesses), accounts.data, workspace, nameOf) : null}
        onOpenAgent={onOpenAgent}
      />
      <KeptHistory
        workspace={workspace}
        plan={history.plan}
        reading={history.loading}
        // The reading is always said (§6.1), so a refusal is said in its place rather than in a toast.
        refusal={history.error ? sentence(history.error) : null}
        busy={historyActs.busy}
        doors={doors}
        onClear={(units, answered) => historyActs.clear({ scope: 'workspace', id: workspace }, units, answered)}
      />
    </>
  );
}

/**
 * Branches: each repository's branches by kind, a group per repository (UX6h2b2, the built-in Git design §2.2), then the
 * clean-up (WSR3, D88) and bringing up to date (WSR6), moved from Settings → Workspace. The clean-up lists this workspace's
 * session branches, and its press removes only what it listed. A look reaches the network as the person, so it is asked by
 * its own press.
 *
 * @remarks
 * **The groups are Git's reads** (`WorkspaceBranchGroups`): read while the tab shows, each heading the door to its
 * repository's page, as Details' names are.
 *
 * **The repositories apart are said once** (UX6h2b3): the groups' fold is the one include, held here and handed to the groups
 * and to *Updates*, which draws no list apart of its own; its look takes what holds Daoris's branches and what the fold included.
 *
 * **Everything here is this workspace's** (BRSCOPE1, WSP5). The clean-up's list and the machine's reading of which
 * checkouts hold Daoris's branches are the machine's answers, read once for every page, and filtered to it. The look and
 * the press are asked for it, so the driver fetches and judges its checkouts alone, and what the look leaves apart is
 * its own. The rows are filtered again, so a host before BRSCOPE1, whose look is the machine's, still shows only its own.
 */
function BranchesPart({ workspace, repositories, notify, onOpenRepository, onOpenBranch, onOpenHistory }: {
  workspace: string;
  /** The workspace's repositories, by name: the recoveries kept for them are listed beneath what was cleaned up. */
  repositories: string[];
  notify: Notify;
  onOpenRepository: (repository: string) => void;
  onOpenBranch?: (repository: string, branch: string) => void;
  onOpenHistory?: (repository: string) => void;
}) {
  const { t } = useTranslation();
  const plan = useSweepPlan();
  // The person's *Look* (PUSH1c4b): one press of `SWEEP_PLAN`, the occasion its pull-request asks are made at (D148 point 2); what it
  // heard reaches the list as the shell's push where it pushes one, and as its answer where the list is pulled.
  const look = useSweepLook();
  const sweep = useSweep();
  // A failed or superseded attempt's branch, discarded by its own press (LAND3b): `daoris-driver trees remove … --force`.
  const discard = useDiscardSessionBranch();
  // The person's delete of the recovery refs nothing holds (RECOVERYDROP1): `daoris-driver work recovery drop`'s twin.
  const dropRecoveries = useDropRecoveries();
  const syncScope = useTreesSyncScope();
  const syncPlan = useTreesSyncPlan(workspace);
  const sync = useTreesSync(workspace);
  // What the look cleaned up on its own (AUTOTIDY1a): the machine log's, so a browser has none; a failed read draws nothing.
  const tidied = useTidiedBranches(workspace);
  // Where each unit's work stands (FABRIC-MILESTONE1b): a tidied or kept branch says it beneath itself; the band's answer, shared.
  const workState = useWorkState();
  // A landing's branch tidied names its session by id (TIDIEDLANDING1): the sessions are asked only while a line does, so the
  // row says it by name where the list holds the session.
  const tidiedLines = Array.isArray(tidied.data?.lines) ? tidied.data.lines : null;
  const landings = useSessions(null, true, Boolean(tidiedLines?.some((line) => line.event === 'branch.tidied' && !line.data.branch && line.data.session)));
  const titles = Object.fromEntries((landings.data ?? []).map((session) => [session.id, sessionTitle(session)]));
  useErrorNotify(plan.error, notify);
  // A look the page stopped waiting for is said in the section, where it was asked (WSR7); every other failure is the
  // driver's own sentence, in a toast.
  useErrorNotify(stoppedWaiting(syncPlan.error) ? null : syncPlan.error, notify);
  const looked = syncPlan.data;
  const scope = syncScope.data?.repositories;
  const stopped = stoppedWaiting(sync.error) && !syncPlan.isFetching ? 'press' as const
    : stoppedWaiting(syncPlan.error) ? 'look' as const
    : undefined;
  const answered = looked && Array.isArray(looked.lines) && Array.isArray(looked.rebases) && Array.isArray(looked.deletes);

  // The groups' fold is the one include (UX6h2b3): what it included is drawn there and taken by the next look below.
  const inclusion = useInclusion();
  const included = [...inclusion.included];

  return (
    <InclusionContext.Provider value={inclusion}>
      <WorkspaceBranchGroups workspace={workspace} onOpenRepository={onOpenRepository} onOpenBranch={onOpenBranch} onOpenHistory={onOpenHistory} />
      <WorkspaceTidied lines={tidiedLines} titles={titles} units={workState.data?.units} />
      {/* What Daoris keeps for recovery that no unit names (RECOVERYVIEW1): a shell older than the field answers none. */}
      <WorkspaceRecoveries recoveries={workState.data?.recovery} repositories={repositories} onDrop={(groups) => dropRecoveries.mutateAsync(groups)} />
      {/* The card's spacing is this container's, as the sections above space themselves (R1): a card has no margin of its own. */}
      <div className="pt-4">
      <SweepList
        branches={Array.isArray(plan.data?.branches) ? mine(plan.data.branches, workspace) : undefined}
        landed={Array.isArray(plan.data?.landed) ? mine(plan.data.landed, workspace) : undefined}
        busy={sweep.isPending || look.isPending || plan.isFetching}
        onLook={() => look.mutate(undefined, { onError: failure(notify) })}
        onClean={(only) => sweep.mutate(only, {
          onSuccess: (done) => notify(t('settings.sweep.done', { removed: done.removed, count: only.length })),
          onError: failure(notify),
        })}
        // Told back to its ask (UXFIX2): a branch kept, or a refusal, is said inside it.
        onDiscard={(branch, answered) => discard.mutate(branch, {
          onSuccess: sayDiscard(notify, t, answered),
          onError: (error) => answered.refused(sentence(error)),
        })}
        discarding={discard.isPending && discard.variables ? sweepKey(discard.variables) : null}
        sync={(
          <SyncSection
            plan={answered
              ? {
                  lines: mine(looked.lines, workspace),
                  rebases: mine(looked.rebases, workspace),
                  deletes: mine(looked.deletes, workspace),
                  ...(Array.isArray(looked.looked) ? { looked: mine(looked.looked, workspace) } : {}),
                  ...(Array.isArray(looked.apart) ? { apart: mine(looked.apart, workspace) } : {}),
                }
              : undefined}
            scope={Array.isArray(scope) ? mine(scope, workspace) : undefined}
            included={included}
            apartFolded
            looking={syncPlan.isFetching}
            lookingAt={syncPlan.asked?.count}
            bringing={sync.isPending}
            stopped={stopped}
            onLook={(include) => { sync.reset(); void syncPlan.look(include); }}
            onSync={(only) => sync.mutate(only, {
              onSuccess: (done) => {
                notify(t('settings.sync.done', { changed: done.changed, count: only.length }));
                // Look again, so the list shows what is left, with why — taking what the person included.
                void syncPlan.look(included);
              },
              onError: (error) => { if (!stoppedWaiting(error)) failure(notify)(error); },
            })}
          />
        )}
      />
      </div>
    </InclusionContext.Provider>
  );
}

/**
 * Workflow (WORKFLOW1b, the workflow design §6.1): the workspace's Current, read only while the tab shows, as
 * `daoris driver workflow show --workspace` reads it. A refusal is said in its place, since the tab is the answer's whole
 * page; each step's door opens Setup at its defaults, and a repository that sets rules of its own opens at its own.
 */
function WorkflowPart({ workspace, onSetup, onOpenRepository }: {
  workspace: string;
  onSetup: () => void;
  onOpenRepository?: (repository: string) => void;
}) {
  const answer = useWorkflowCurrent({ workspace });
  return (
    <WorkflowTab
      page="workspace"
      name={workspace}
      current={answer.data}
      reading={answer.isPending}
      refusal={answer.error ? sentence(answer.error) : null}
      doors={{ setup: onSetup, repository: onOpenRepository }}
    />
  );
}

/**
 * Setup: its defaults (WSR1, WSR2, LANG1c, READ1) and its remote and rules (D48 §5, PERM1), each from the answer the driver
 * gives every door and each change said as the toast Settings' cards said it.
 */
function SetupPart({ workspace, notify, open, wiring, onOpenPlugin }: {
  workspace: string;
  onOpenPlugin?: (plugin: string) => void;
  notify: Notify;
  open?: WorkspaceSection;
  wiring: boolean;
}) {
  const { t } = useTranslation();
  const driver = useDriver();
  const across = useAcross();
  const rules = useRules();
  const catalog = usePlugins();
  const remotes = useRemotes();
  const wire = useWireRemote();
  const unwire = useUnwireRemote();
  const setLine = useSetLine();
  const setLanding = useSetLanding();
  const setLanguage = useSetLanguage();
  const setReview = useSetReview();
  const setOpinion = useSetOpinion();
  const setCommits = useSetCommits();
  const setHelpers = useSetHelpers();
  const setHandOver = useSetHandOver();
  const setAccess = useSetAccess();
  const setRead = useSetReadAcross();
  const act = useRuleAction();
  useErrorNotify(remotes.error ?? across.error ?? rules.error, notify);
  const onError = failure(notify);

  const state = driver.data;
  const ofWorkspace = <R extends { workspace: string }>(rows: R[] | undefined) => (rows ?? []).find((row) => same(row.workspace, workspace));
  // The plugins a branch rule may hand its branch to (D100): on, sound, and speaking on `work/land`.
  const landers = (catalog.data?.plugins ?? [])
    .filter((plugin) => plugin.enabled && !plugin.problem && plugin.points.includes('work/land'))
    .map((plugin) => plugin.id);
  const landingSet = ofWorkspace(state?.workspaceLandings);
  const table = state?.languageTable;
  const map = remotes.data && Array.isArray(remotes.data.remotes) ? remotes.data : null;
  const answered = rules.data && Array.isArray(rules.data.defaults) && Array.isArray(rules.data.scopes) ? rules.data : null;
  const scope = answered?.scopes.find((row) => row.scope === 'workspace' && row.name !== undefined && same(row.name, workspace));
  const where = t('settings.rules.where.workspace', { name: workspace });

  const setup: WorkspaceSetupProps = {
    workspace,
    open,
    wiring,
    busy: setLine.isPending || setLanding.isPending || setLanguage.isPending || setReview.isPending || setOpinion.isPending
      || setCommits.isPending || setHelpers.isPending || setHandOver.isPending || setAccess.isPending || setRead.isPending || act.isPending || wire.isPending
      || unwire.isPending,
    // The access its repositories' sessions start under (AUTONOMY1d): the workspace's over this machine's, resolved on the page from
    // the levels as set, since the driver resolves only a repository's; a shell older than it answers none, and nothing is drawn.
    access: state && Array.isArray(state.workspaceAccess)
      ? {
          pauses: resolveAccess([
            { setting: ofWorkspace(state.workspaceAccess)?.setting, source: 'workspace' },
            { setting: state.machineAccess, source: 'machine' },
          ]),
          aboveFull: isFull(resolveAccess([{ setting: state.machineAccess, source: 'machine' }])),
          onChange: (edit, answered) => setAccess.mutate({ workspace, ...edit }, {
            onSuccess: () => {
              notify(accessToast(t, workspace, edit, false));
              answered?.done();
            },
            onError: (error) => (answered ? answered.refused(sentence(error)) : onError(error)),
          }),
        }
      : null,
    defaults: {
      line: state && {
        set: ofWorkspace(state.workspaceLines)?.branch,
        onSet: (branch) => setLine.mutate(branch ? { workspace, branch } : { workspace }, {
          onSuccess: () => notify(branch
            ? t('settings.lines.saved', { name: workspace, branch })
            : t('settings.lines.cleared', { name: workspace })),
          onError,
        }),
      },
      landing: state && {
        set: landingSet && ruleOf(landingSet),
        landers,
        // FABRIC-GRAPH1d: a rule the file keeps for it that reads as no rule, said under the row.
        unread: ofWorkspace(state.workspaceUnreadLandings),
        onSet: (rule) => setLanding.mutate(rule ? { workspace, ...rule } : { workspace }, {
          onSuccess: () => notify(!rule
            ? t('settings.landing.cleared', { name: workspace })
            : rule.form === 'branch'
              ? rule.plugin
                ? t('settings.landing.savedBranchPlugin', { name: workspace, pattern: rule.pattern, plugin: rule.plugin })
                : t('settings.landing.savedBranch', { name: workspace, pattern: rule.pattern })
              : t('settings.landing.savedMerge', { name: workspace })),
          onError,
        }),
      },
      // A shell older than LANG1c answers no table, and nothing is offered rather than a field whose save is refused.
      language: state && Array.isArray(table)
        ? {
            set: ofWorkspace(state.workspaceLanguages)?.language,
            table,
            onSet: (code) => setLanguage.mutate(code ? { workspace, language: code } : { workspace }, {
              onSuccess: () => {
                const name = table.find((row) => row.code === code)?.name;
                notify(name
                  ? t('settings.sessionLanguage.saved', { name: workspace, language: name })
                  : t('settings.sessionLanguage.cleared', { name: workspace }));
              },
              onError,
            }),
          }
        : null,
      // A shell older than READ1 answers no reading across, and nothing is offered.
      read: state && Array.isArray(across.data?.repositories)
        ? {
            set: ofWorkspace(state.workspaceReadAcross)?.read,
            onSet: (read) => setRead.mutate(read === undefined ? { workspace } : { workspace, read }, {
              onSuccess: () => notify(read === undefined
                ? t('settings.across.cleared', { name: workspace })
                : t(read ? 'settings.across.savedOn' : 'settings.across.savedOff', { name: workspace })),
              onError,
            }),
          }
        : null,
      // Where its repositories' work is reviewed before it lands (REVIEWENV1a); a shell older than it answers no reviews.
      review: state && Array.isArray(state.workspaceReviews)
        ? {
            set: ofWorkspace(state.workspaceReviews)?.rule,
            onChange: (edit) => setReview.mutate({ workspace, ...edit }, {
              onSuccess: (answered) => notify(reviewToast(t, workspace, edit, answered.reviewed)),
              onError,
            }),
          }
        : null,
      // Which other agent reads its repositories' work before it lands (XAGENT1a); a shell older than it answers no opinions.
      opinion: state && Array.isArray(state.workspaceOpinions)
        ? {
            set: ofWorkspace(state.workspaceOpinions)?.rule,
            onChange: (edit) => setOpinion.mutate({ workspace, ...edit }, {
              onSuccess: () => notify(opinionToast(t, workspace, edit)),
              onError,
            }),
          }
        : null,
      // How its repositories' work is committed, and what a helper their agents ask for does (DOORS1b); a shell older than
      // it answers neither, and neither is offered.
      commits: state && Array.isArray(state.workspaceCommits)
        ? {
            set: ofWorkspace(state.workspaceCommits)?.rule,
            onChange: (edit) => setCommits.mutate({ workspace, ...edit }, {
              onSuccess: () => notify(commitToast(t, workspace, edit)),
              onError,
            }),
          }
        : null,
      helpers: state && Array.isArray(state.workspaceHelpers)
        ? {
            set: ofWorkspace(state.workspaceHelpers)?.rule,
            onChange: (edit) => setHelpers.mutate({ workspace, ...edit }, {
              onSuccess: () => notify(helperToast(t, workspace, edit)),
              onError,
            }),
          }
        : null,
      // What an agent's ask to hand its session over does in its repositories (MIXAGENT1c); none on a shell older than it.
      handOver: state && Array.isArray(state.workspaceHandOver)
        ? {
            set: ofWorkspace(state.workspaceHandOver)?.rule,
            onChange: (edit) => setHandOver.mutate({ workspace, ...edit }, {
              onSuccess: () => notify(handOverToast(t, workspace, edit)),
              onError,
            }),
          }
        : null,
    },
    remote: {
      wiring: map && {
        remote: map.remotes.find((row) => same(row.workspace, workspace)) ?? null,
        fromEnvironment: map.fromEnvironment,
        onWire: (url, key, done) => wire.mutate({ workspace, url, key }, {
          onSuccess: (wiringState) => {
            // "Wired" and "in effect" come apart only here: with the environment pair set no loader reads the file (D48
            // §5), so the sentence says what happened, by the answer's own flag.
            notify(t(wiringState.fromEnvironment ? 'settings.wiring.wiredButOverridden' : 'settings.wiring.wired', { workspace }));
            // The key never lingers in the form once it has landed in the file.
            done();
          },
          onError,
        }),
        onUnwire: () => unwire.mutate({ workspace }, {
          onSuccess: () => notify(t('settings.wiring.unwired', { workspace })),
          onError,
        }),
      },
      rules: answered && {
        lists: { allow: scope?.allow ?? [], ask: scope?.ask ?? [], deny: scope?.deny ?? [] },
        onAdd: (list, rule, added) => act.mutate({ action: 'add', list, rule, scope: 'workspace', name: workspace }, {
          onSuccess: () => {
            notify(t('settings.rules.added', { rule, list: t(`settings.rules.list.${list}`), where }));
            added();
          },
          onError,
        }),
        onRemove: (rule) => act.mutate({ action: 'remove', rule, scope: 'workspace', name: workspace }, {
          onSuccess: () => notify(t('settings.rules.removed', { rule, where })),
          onError,
        }),
      },
    },
  };

  return <WorkspaceSetup {...setup} onOpenPlugin={onOpenPlugin} />;
}
