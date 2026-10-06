import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSyncStanding } from '../queries';
import type { LandingRule } from '../settings/Landings';
import { namer } from '../settings/namer';
import { sayDiscard, sweepKey, SweepList } from '../settings/Sweep';
import { SyncSection } from '../settings/Sync';
import {
  stoppedWaiting, useAccounts, useAcross, useDriver, useHarnesses, usePlugins, useRemotes, useRuleAction, useRules,
  useDiscardSessionBranch, useSetLanding, useSetLanguage, useSetLine, useSetReadAcross, useStarts, useSweep, useSweepPlan,
  useTreesSync, useTreesSyncPlan, useTreesSyncScope, useUnwireRemote, useWireRemote,
} from '../shell';
import { byTool } from '../tools';
import { failure, type Notify, useErrorNotify } from '../ui';
import type { WorkspaceSection, WorkspaceTab } from './tabs';
import { accountsHere, hostOf } from './workspace';
import { WorkspaceDetails, WorkspacePage } from './WorkspacePage';
import { WorkspaceSetup, type WorkspaceSetupProps } from './WorkspaceSetup';

/** Matched without case, as the driver matches a workspace's name. */
const same = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();

/** The rows of a driver answer that are this workspace's. */
const mine = <R extends { workspace: string }>(rows: readonly R[], workspace: string) => rows.filter((row) => same(row.workspace, workspace));

/** A workspace's landing rule as the driver answers it, without the name it is answered under. */
const ruleOf = ({ form, pattern, tidy, plugin, autoAccept }: LandingRule): LandingRule => ({
  form, ...(pattern ? { pattern } : {}), ...(tidy ? { tidy } : {}), ...(plugin ? { plugin } : {}), ...(autoAccept ? { autoAccept } : {}),
});

/**
 * **A workspace's page, with its data** (UX6g, D150 §4.3): the organism that holds what the page reads and every press it
 * makes, so the page, its Details and its Setup below it hold none (components §2). Each tab is a part of its own, drawn
 * only while it shows, so a tab asks the driver nothing until it is open: the starts and the accounts on Details, the
 * clean-up's list and the look's scope on Branches, reading across, the rules and the plugins on Setup.
 *
 * @remarks
 * **Every control is the screen's half of a terminal verb** (D50): `daoris driver line|landing|language|across --workspace`,
 * `daoris remote add|remove`, `daoris agent rules … --workspace`, `daoris-driver trees clean|sync`, and a failed attempt's
 * branch discarded as `daoris-driver trees remove <branch> --repository <name> --force` (LAND3b). A refusal is the driver's
 * sentence, in a toast. They are where Settings → Workspace and Settings → Permissions held them (§3.1).
 *
 * **A browser is handed what it may know** (D47 §4): its repositories, and whether it syncs, read from this machine's own
 * host without a host's name (D48 §5). No tab, no branch, no setting and no account.
 */
export function WorkspaceView({
  workspace, repositories, attached, tab, onTab, asked = null, notify, onOpenRepository, onOpenAgent, onSyncNow, syncing = false,
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
  /** Open an agent's page, where a workspace's accounts are set (D130 §3.2, UX6e). */
  onOpenAgent?: (agent: string) => void;
  /** The status bar's *Sync now* (SYNC6b), the application's, which says the pass's own words. */
  onSyncNow?: (workspace: string) => void;
  syncing?: boolean;
}) {
  const remotes = useRemotes();
  // Where it syncs, for a browser, from this machine's own host: wired or not, never where (D48 §5).
  const standing = useSyncStanding(attached ? null : workspace);
  // The header's *Wire to a remote…*: Setup at its remote, the form open.
  const [wiring, setWiring] = useState(false);
  const map = remotes.data && Array.isArray(remotes.data.remotes) ? remotes.data : null;
  const wired = map?.remotes.find((row) => same(row.workspace, workspace)) ?? null;
  const remote = attached
    ? map ? wired ? { host: hostOf(wired.url) } : null : undefined
    : standing.data ? standing.data.wired ? {} : null : undefined;

  const chooseTab = (next: WorkspaceTab) => {
    setWiring(false);
    onTab(next);
  };

  const body = !attached
    ? <WorkspaceDetails repositories={repositories} onOpen={onOpenRepository} />
    : tab === 'branches'
      ? <BranchesPart workspace={workspace} notify={notify} />
      : tab === 'setup'
        ? <SetupPart workspace={workspace} notify={notify} open={wiring ? 'remote' : asked ?? undefined} wiring={wiring} />
        : (
          <DetailsPart
            workspace={workspace}
            repositories={repositories}
            notify={notify}
            onOpenRepository={onOpenRepository}
            onOpenAgent={onOpenAgent}
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
      onSync={attached && wired && onSyncNow ? () => onSyncNow(workspace) : undefined}
      onWire={attached && map && !wired ? () => { setWiring(true); onTab('setup'); } : undefined}
    >
      {body}
    </WorkspacePage>
  );
}

/** Details: its repositories, what a start in it runs on (MAP1b), and the accounts its work may run on (D130 §3.2). */
function DetailsPart({ workspace, repositories, notify, onOpenRepository, onOpenAgent }: {
  workspace: string;
  repositories: string[];
  notify: Notify;
  onOpenRepository: (repository: string) => void;
  onOpenAgent?: (agent: string) => void;
}) {
  const { t } = useTranslation();
  const roster = useHarnesses();
  const accounts = useAccounts();
  const answer = useStarts([workspace]);
  useErrorNotify(answer.error, notify);
  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  const nameOf = namer(t, harnesses);
  // An older shell has never heard of the question: the section is absent rather than the page blank.
  const starts = Array.isArray(answer.data?.starts) ? mine(answer.data.starts, workspace) : null;
  return (
    <WorkspaceDetails
      repositories={repositories}
      onOpen={onOpenRepository}
      starts={starts && { list: starts, nameOf }}
      accounts={roster.data ? accountsHere(byTool(harnesses), accounts.data, workspace, nameOf) : null}
      onOpenAgent={onOpenAgent}
    />
  );
}

/**
 * Branches: the clean-up (WSR3, D88) and bringing up to date (WSR6), moved from Settings → Workspace. The clean-up lists
 * this workspace's session branches, and its press removes only what it listed. A look reaches the network as the person,
 * so it is asked by its own press; the driver's look takes every repository holding Daoris's branches here, so what it
 * fetches and leaves apart is said as the machine's, and what it lists and brings up to date is this workspace's.
 */
function BranchesPart({ workspace, notify }: { workspace: string; notify: Notify }) {
  const { t } = useTranslation();
  const plan = useSweepPlan();
  const sweep = useSweep();
  // A failed or superseded attempt's branch, discarded by its own press (LAND3b): `daoris-driver trees remove … --force`.
  const discard = useDiscardSessionBranch();
  const syncScope = useTreesSyncScope();
  const syncPlan = useTreesSyncPlan();
  const sync = useTreesSync();
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

  return (
    <SweepList
      branches={Array.isArray(plan.data?.branches) ? mine(plan.data.branches, workspace) : undefined}
      landed={Array.isArray(plan.data?.landed) ? mine(plan.data.landed, workspace) : undefined}
      busy={sweep.isPending || plan.isFetching}
      onLook={() => void plan.refetch()}
      onClean={(only) => sweep.mutate(only, {
        onSuccess: (done) => notify(t('settings.sweep.done', { removed: done.removed, count: only.length })),
        onError: failure(notify),
      })}
      onDiscard={(branch) => discard.mutate(branch, { onSuccess: sayDiscard(notify, t), onError: failure(notify) })}
      discarding={discard.isPending && discard.variables ? sweepKey(discard.variables) : null}
      sync={(
        <SyncSection
          plan={answered
            ? {
                ...looked,
                lines: mine(looked.lines, workspace),
                rebases: mine(looked.rebases, workspace),
                deletes: mine(looked.deletes, workspace),
              }
            : undefined}
          scope={Array.isArray(scope) ? scope : undefined}
          included={syncPlan.asked?.include}
          looking={syncPlan.isFetching}
          lookingAt={syncPlan.asked?.count}
          bringing={sync.isPending}
          stopped={stopped}
          onLook={(include) => { sync.reset(); void syncPlan.look(include); }}
          onSync={(only) => sync.mutate(only, {
            onSuccess: (done) => {
              notify(t('settings.sync.done', { changed: done.changed, count: only.length }));
              // Look again, so the list shows what is left, with why — taking what the person included.
              void syncPlan.look(syncPlan.asked?.include ?? []);
            },
            onError: (error) => { if (!stoppedWaiting(error)) failure(notify)(error); },
          })}
        />
      )}
    />
  );
}

/**
 * Setup: its defaults (WSR1, WSR2, LANG1c, READ1) and its remote and rules (D48 §5, PERM1), each from the answer the driver
 * gives every door and each change said as the toast Settings' cards said it.
 */
function SetupPart({ workspace, notify, open, wiring }: {
  workspace: string;
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
    busy: setLine.isPending || setLanding.isPending || setLanguage.isPending || setRead.isPending || act.isPending
      || wire.isPending || unwire.isPending,
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

  return <WorkspaceSetup {...setup} />;
}
