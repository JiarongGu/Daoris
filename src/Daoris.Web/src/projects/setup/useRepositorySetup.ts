import { useTranslation } from 'react-i18next';
import {
  useAcceptIntegration, useAcross, useDriver, useHarnesses, useHeavyMarks, useLines, useRuleAction, useRules, useSetAccess, useSetCheckoutWrites,
  useSetCommits, useSetDrivable, useSetHandOver, useSetHeavyGates, useSetHelpers, useSetHold, useSetLanded, useSetLanguage, useSetLine,
  useSetOpinion, useSetReadAcross, useSetReview, useSetSessionChecks, useSetStanding, useSetTrees, useSetWriteAcross, useWorkflowCurrent, standingOf,
  useGitRemotes, useSetGitAccess, useSetGitUse, useTools,
} from '../../bridge/shell';
import { type GitAccessEdit, type RepositoryGitAccess, resolveGitAccess } from '../../settings/repository/GitAccess';
import type { Answered } from '../../work/InlineConfirm';
import { canBeAsked, type Registration, SESSION_ACTIVE } from '../../data/api';
import { useSessions } from '../../data/queries';
import { failure, type Notify, useErrorNotify } from '../../design/ui';
import { sentence } from '../../locales/format';
import { stewardSetAt } from '../../person/standing';
import { doorOf } from '../../agents/tools';
import { type AccessEdit, accessToast, isFull, resolveAccess } from '../../settings/repository/Access';
import { type CommitEdit, commitToast } from '../../settings/repository/Commits';
import { heavyToast } from '../../settings/repository/HeavyGates';
import { type HandOverEdit, handOverToast } from '../../settings/repository/HandOver';
import { type HelperEdit, helperToast } from '../../settings/repository/Helpers';
import { type OpinionEdit, opinionToast } from '../../settings/repository/Opinions';
import { type ReviewEdit, reviewToast } from '../../settings/repository/Reviews';
import { sessionChecksToast, type SessionChecksValue } from '../../settings/repository/SessionChecks';
import type { Driving, RepositorySetupProps } from './RepositorySetup';

const list = <T,>(value: unknown): T[] => (Array.isArray(value) ? value as T[] : []);

/** One row of a list the driver's `LINES` answer holds, by the list's name. */
type LinesAnswer = NonNullable<ReturnType<typeof useLines>['data']>;
type Row<K extends keyof LinesAnswer> = NonNullable<LinesAnswer[K]>[number];

/**
 * **A repository's setup as its molecule takes it** (UX6f, D150 §4.2; moved out of the Repositories view by AGENTSPLACE1f so the Agents
 * place's page and the dark Setup tab hold the one reading): every setting it holds on this machine, each with the press its terminal twin
 * makes, and each change said as the toast Settings' lists said it. A section the driver did not answer is absent, never a control whose
 * save would be refused; Reach waits for reading across, so it opens on what it holds.
 *
 * @remarks
 * **What only the setup reads is asked only while `enabled`**: reading across, the sessions that run in a checkout, the heavy gates' marks,
 * the plugins that land work and its Workflow answer's start node. Null for a repository the driver does not know, or while it has not
 * answered. A hook, so a view that shows setup holds it and the molecule stays props.
 */
export function useRepositorySetup({ registration, enabled, notify, onOpenPlugin, onOpenGit }: {
  registration: Registration | undefined;
  enabled: boolean;
  notify: Notify;
  onOpenPlugin?: (plugin: string) => void;
  /** Open Settings → Git, where a remote nothing serves is given an identity (GITAUTH1e); absent, the press is not drawn. */
  onOpenGit?: (host: string) => void;
}): RepositorySetupProps | null {
  const { t } = useTranslation();
  const driver = useDriver();
  const roster = useHarnesses();
  const setDrivable = useSetDrivable();
  const setHold = useSetHold();
  const setTrees = useSetTrees();
  const setCheckoutWrites = useSetCheckoutWrites();
  const setStanding = useSetStanding();
  const setLanguage = useSetLanguage();
  const setLine = useSetLine();
  const setReview = useSetReview();
  const setOpinion = useSetOpinion();
  const setCommits = useSetCommits();
  const setHelpers = useSetHelpers();
  const setHandOver = useSetHandOver();
  const setAccess = useSetAccess();
  const setHeavy = useSetHeavyGates();
  const setChecks = useSetSessionChecks();
  const setRead = useSetReadAcross();
  const setWrite = useSetWriteAcross();
  const ruleAction = useRuleAction();
  const setLanded = useSetLanded();
  const acceptIntegration = useAcceptIntegration();
  const lines = useLines();
  const rules = useRules();
  const setGitUse = useSetGitUse();
  const setGitAccess = useSetGitAccess();

  const target = registration?.repository ?? null;
  const shown = enabled && driver.data !== undefined && target !== null;
  // GITAUTH1e: its git remotes, with what serves each, and the machine's identities to choose from; shell-only, as the identities are.
  const identities = useTools().data?.identities?.identities;
  const remotes = useGitRemotes({ repository: target ?? undefined, enabled: shown });
  const across = useAcross({ enabled: shown });
  // GITAPP2b: the sessions the service lists, asked only while its switch is on, to say which already run in its checkout (a session with
  // no tree of its own runs in the registered root).
  const named = (names: string[] | undefined, name: string) => (names ?? []).some((each) => each.toLowerCase() === name.toLowerCase());
  const checkoutOn = target !== null && named(driver.data?.checkoutWrites, target);
  const checkoutSessions = useSessions(target, false, shown && checkoutOn);
  // Whether its own daoris.gates.json marks heavy gates, so wins over this machine's list (HEAVYRUN1b2c): the driver's reading.
  const heavyMarks = useHeavyMarks(target ?? '', { enabled: shown });
  // FABRIC-PIPELINE1a: the route row reads its Workflow answer's start node.
  const current = useWorkflowCurrent(target === null ? null : { repository: target }, { enabled: shown });
  useErrorNotify(shown ? across.error : null, notify);
  const onDriverError = failure(notify);
  // Which door this machine's starts ride (INT3c): an unadopted repository is carried by the protocol door only (D70).
  const door = doorOf(roster.data?.adapter, list(roster.data?.harnesses));

  if (!registration || !driver.data) return null;
  const state = driver.data;
  const { repository } = registration;

  const resolved = <R extends { repository: string }>(rows: unknown): R | undefined =>
    (Array.isArray(rows) ? rows as R[] : []).find((row) => row.repository === registration.repository);
  const ofWorkspace = <R extends { workspace: string }>(rows: R[] | undefined, workspace: string) =>
    (rows ?? []).find((row) => row.workspace.toLowerCase() === workspace.toLowerCase());

  /** When the steward last set this repository's driving and it still stands; else null. */
  const stewardSetOf = (): string | null => {
    const standing = standingOf(state.standingConfirmation);
    return standing ? stewardSetAt(standing.stewardActs, registration.repository) : null;
  };

  const same = (name: string) => name.toLowerCase() === registration.repository.toLowerCase();

  /**
   * This machine's driving for one repository — the same wherever it can be driven: every adopter, and since INT3c one not adopted with a
   * root here (D70). One with no root has nowhere to start, and gets none.
   */
  const drivingOf = (): Driving | null => {
    if (!registration.adopted && !canBeAsked(registration)) return null;
    const { repository: name } = registration;
    return {
      drivable: named(state.drivable, name),
      held: named(state.holds, name),
      ownTree: named(state.trees, name),
      onDrive: (next) => setDrivable.mutate({ repository: name, drivable: next }, { onError: onDriverError }),
      onHold: (next) => setHold.mutate({ repository: name, held: next }, { onError: onDriverError }),
      onTrees: (next) => setTrees.mutate({ repository: name, ownTree: next }, { onError: onDriverError }),
      note: !registration.adopted && door === 'pipe' ? t('projects.outside.directDoor') : undefined,
      // TERMTRUST1a: a hold a terminal made under the person's standing confirmation, as this run remembers it.
      heldStanding: named(standingOf(state.standingConfirmation)?.held, name),
      // STEWARDROLE1b (D209 §5.1): what the steward set here, from this run's memory.
      stewardSet: stewardSetOf(),
    };
  };

  /**
   * Its session language (LANG1c, D142 point 7): the driver's resolution from the lines, its workspace's by the table's name, and the table.
   * A shell older than it answers no table, and nothing is offered rather than a field whose save is refused.
   */
  const languageOfPage = (): NonNullable<RepositorySetupProps['sessions']>['language'] => {
    const table = state.languageTable;
    if (!Array.isArray(table)) return undefined;
    const { repository: name } = registration;
    const one = resolved<Row<'languages'>>(lines.data?.languages);
    const shared = ofWorkspace(state.workspaceLanguages, one?.workspace ?? registration.workspace ?? 'default')?.language;
    return {
      resolved: one?.language ? one : null,
      inherited: table.find((row) => row.code === shared)?.name,
      table,
      onSet: (language) => setLanguage.mutate(language === null ? { repository: name } : { repository: name, language }, {
        onSuccess: () => {
          const label = table.find((row) => row.code === language)?.name;
          notify(label
            ? t('settings.sessionLanguage.saved', { name, language: label })
            : t('settings.sessionLanguage.cleared', { name }));
        },
        onError: onDriverError,
      }),
    };
  };

  const driving = drivingOf();
  const line = resolved<Row<'lines'>>(lines.data?.lines);
  const landing = resolved<Row<'landings'>>(lines.data?.landings);
  const workspace = line?.workspace ?? landing?.workspace ?? registration.workspace ?? 'default';

  const work: RepositorySetupProps['work'] = lines.data === undefined ? null : {
    line: line ?? null,
    onLine: (branch) => setLine.mutate(branch ? { repository, branch } : { repository }, {
      onSuccess: () => notify(branch
        ? t('settings.lines.saved', { name: registration.repository, branch })
        : t('settings.lines.cleared', { name: registration.repository })),
      onError: onDriverError,
    }),
    onLinePolicy: (policy) => setLine.mutate({ repository, policy }, {
      onSuccess: () => notify(policy
        ? t('settings.lines.policySaved', { name: registration.repository })
        : t('settings.lines.policyCleared', { name: registration.repository })),
      onError: onDriverError,
    }),
    landing: landing ?? null,
    // FABRIC-GRAPH1d: a rule the file keeps that reads as no rule, its own, else its workspace's where Daoris's merge stands.
    landingUnread: (() => {
      const own = (state.unreadLandings ?? []).find((row) => same(row.repository));
      if (own) return { name: own.repository, why: own.why, said: own.said };
      const shared = landing?.source === 'default' ? ofWorkspace(state.workspaceUnreadLandings, workspace) : undefined;
      return shared ? { name: shared.workspace, why: shared.why, said: shared.said } : null;
    })(),
    // Where its work is reviewed before it lands (REVIEWENV1a); a shell older than it answers no reviews, and no row is offered.
    review: Array.isArray(lines.data?.reviews) ? resolved<NonNullable<RepositorySetupProps['work']>['review'] & object>(lines.data.reviews) ?? { repository, workspace } : undefined,
    onReview: Array.isArray(lines.data?.reviews)
      ? (edit: ReviewEdit) => setReview.mutate({ repository, ...edit }, {
          onSuccess: (done) => notify(reviewToast(t, registration.repository, edit, done.reviewed)),
          onError: onDriverError,
        })
      : undefined,
    // Which other agent reads its work before it lands (XAGENT1a); a shell older than it answers no opinions, and no row is offered.
    opinion: Array.isArray(lines.data?.opinions) ? resolved<NonNullable<RepositorySetupProps['work']>['opinion'] & object>(lines.data.opinions) ?? { repository, workspace } : undefined,
    onOpinion: Array.isArray(lines.data?.opinions)
      ? (edit: OpinionEdit) => setOpinion.mutate({ repository, ...edit }, {
          onSuccess: () => notify(opinionToast(t, registration.repository, edit)),
          onError: onDriverError,
        })
      : undefined,
    // How its work is committed (DOORS1b); a shell older than it answers no commits, and no row is offered.
    commits: Array.isArray(lines.data?.commits) ? resolved<NonNullable<RepositorySetupProps['work']>['commits'] & object>(lines.data.commits) ?? { repository, workspace } : undefined,
    onCommits: Array.isArray(lines.data?.commits)
      ? (edit: CommitEdit) => setCommits.mutate({ repository, ...edit }, {
          onSuccess: () => notify(commitToast(t, registration.repository, edit)),
          onError: onDriverError,
        })
      : undefined,
  };

  // Its sessions' access (AUTONOMY1d), where it can be driven; a shell older than it answers no access, and no section is drawn. What stands
  // above it is its workspace's over this machine's, as set, so turning off a full access from above sets today's pauses here rather than
  // clearing back into it. Full access answers its ask: done, or refused in the driver's sentence.
  const resolvedAccess = resolved<Row<'access'>>(lines.data?.access);
  const access: RepositorySetupProps['access'] = driving && resolvedAccess && Array.isArray(resolvedAccess.pauses) ? {
    workspace: resolvedAccess.workspace,
    pauses: resolvedAccess.pauses,
    aboveFull: isFull(resolveAccess([
      { setting: ofWorkspace(state.workspaceAccess, workspace)?.setting, source: 'workspace' },
      { setting: state.machineAccess, source: 'machine' },
    ])),
    onChange: (edit: AccessEdit, answered) => setAccess.mutate({ repository, ...edit }, {
      onSuccess: () => {
        notify(accessToast(t, registration.repository, edit, false));
        answered?.done();
      },
      onError: (error) => (answered ? answered.refused(sentence(error)) : onDriverError(error)),
    }),
  } : null;

  // This machine's heavy gates for it (HEAVYRUN1b2b), where it can be driven, matched without case as the driver keys them; a shell older
  // than them answers none, and no section is drawn. The whole list goes out, and a refusal is said where Save was pressed.
  const heavyGates = state.heavyGates;
  const heavy: RepositorySetupProps['heavy'] = driving && Array.isArray(heavyGates) ? {
    gates: heavyGates.find((row) => same(row.repository))?.gates ?? [],
    // Until the driver has answered (or on a shell older than the read) the section says only the general rule.
    marks: heavyMarks.data && typeof heavyMarks.data.marks !== 'undefined' ? heavyMarks.data : null,
    onSave: (gates, answered) => setHeavy.mutate({ repository, gates }, {
      onSuccess: () => {
        notify(heavyToast(t, registration.repository, gates));
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    }),
    // Which checks its sessions run (FABRIC-PIPELINE1b), matched without case as the driver keys them; a shell older than it answers none, and
    // no row is drawn. A press goes out at once, and a refusal is said in the row.
    checks: Array.isArray(state.sessionChecks) ? {
      set: (state.sessionChecks.find((row) => same(row.repository))?.checks as SessionChecksValue | undefined) ?? null,
      onChange: (checks, answered) => setChecks.mutate({ repository, checks }, {
        onSuccess: () => {
          notify(sessionChecksToast(t, registration.repository, checks));
          answered.done();
        },
        onError: (error) => answered.refused(sentence(error)),
      }),
    } : null,
  } : null;

  // Its standing answer (KNOWUSE1b), matched without case as the driver matches it; a shell older than it answers no `standing`, and nothing is
  // offered rather than a field whose save would be refused.
  const sessions: RepositorySetupProps['sessions'] = driving && {
    language: languageOfPage(),
    // What a helper its agent asks for does (DOORS1b); a shell older than it answers no helpers, and no row is offered.
    helpers: Array.isArray(lines.data?.helpers) ? resolved<NonNullable<NonNullable<RepositorySetupProps['sessions']>['helpers']>>(lines.data.helpers) ?? { repository, workspace } : undefined,
    onHelpers: Array.isArray(lines.data?.helpers)
      ? (edit: HelperEdit) => setHelpers.mutate({ repository, ...edit }, {
          onSuccess: () => notify(helperToast(t, registration.repository, edit)),
          onError: onDriverError,
        })
      : undefined,
    // What an agent's ask to hand its session over does (MIXAGENT1c); a shell older than it answers none, and no row is offered.
    handOver: Array.isArray(lines.data?.handOver) ? resolved<NonNullable<NonNullable<RepositorySetupProps['sessions']>['handOver']>>(lines.data.handOver) ?? { repository, workspace } : undefined,
    onHandOver: Array.isArray(lines.data?.handOver)
      ? (edit: HandOverEdit) => setHandOver.mutate({ repository, ...edit }, {
          onSuccess: () => notify(handOverToast(t, registration.repository, edit)),
          onError: onDriverError,
        })
      : undefined,
    standing: (state.standing ?? []).find((row) => same(row.repository)) ?? null,
    onStanding: state.standing === undefined
      ? undefined
      : (says) => setStanding.mutate(says === null ? { repository } : { repository, says }, { onError: onDriverError }),
  };

  const acrossRows = Array.isArray(across.data?.repositories) ? across.data.repositories : null;
  const mine = acrossRows?.find((row) => same(row.repository)) ?? null;
  const scope = rules.data && Array.isArray(rules.data.scopes)
    ? rules.data.scopes.find((row) => row.scope === 'repository' && row.name !== undefined && same(row.name))
    : undefined;
  const where = t('settings.rules.where.repository', { name: registration.repository });
  const reach: RepositorySetupProps['reach'] = across.data === undefined && !across.error ? null : {
    // GITAPP2b: the switch, where the shell answers it; on, it says which sessions already run in the checkout.
    checkout: state.checkoutWrites === undefined ? undefined : {
      on: named(state.checkoutWrites, registration.repository),
      running: checkoutSessions.error ? null
        : checkoutSessions.data === undefined ? undefined
        : checkoutSessions.data.filter((one) =>
          !one.tree && SESSION_ACTIVE.has(one.state) && !one.id.includes('/') && one.repository.toLowerCase() === registration.repository.toLowerCase()).length,
      onChange: (allow) => setCheckoutWrites.mutate({ repository: registration.repository, allow }, { onError: onDriverError }),
    },
    across: mine,
    candidates: mine && acrossRows
      ? acrossRows
        .filter((other) => other.workspace === mine.workspace && !same(other.repository)
          && !mine.writesTo.some((to) => to.toLowerCase() === other.repository.toLowerCase()))
        .map((other) => other.repository)
      : [],
    onRead: (read) => setRead.mutate(read === undefined ? { repository } : { repository, read }, {
      onSuccess: () => notify(read === undefined
        ? t('settings.across.cleared', { name: registration.repository })
        : t(read ? 'settings.across.savedOn' : 'settings.across.savedOff', { name: registration.repository })),
      onError: onDriverError,
    }),
    onWrite: (to, allow) => setWrite.mutate({ repository, to, allow }, {
      onSuccess: () => notify(t(allow ? 'settings.across.declared' : 'settings.across.withdrawn', { repository: registration.repository, to })),
      onError: onDriverError,
    }),
    // The rules this repository's scope holds, by the name the registry holds it under; none where it holds none, and the row absent where the
    // rules were not answered.
    rules: rules.data && Array.isArray(rules.data.scopes)
      ? { allow: scope?.allow ?? [], ask: scope?.ask ?? [], deny: scope?.deny ?? [] }
      : null,
    onAddRule: (listName, rule, added) => ruleAction.mutate({ action: 'add', list: listName, rule, scope: 'repository', name: registration.repository }, {
      onSuccess: () => {
        notify(t('settings.rules.added', { rule, list: t(`settings.rules.list.${listName}`), where }));
        added();
      },
      onError: onDriverError,
    }),
    onRemoveRule: (rule) => ruleAction.mutate({ action: 'remove', rule, scope: 'repository', name: registration.repository }, {
      onSuccess: () => notify(t('settings.rules.removed', { rule, where })),
      onError: onDriverError,
    }),
  };

  // The landed route (FABRIC-PIPELINE1a): what verifies the work that lands, read from its Workflow answer's start node, which a driver before
  // it does not answer; the choice is the person's press, and accepts the declaration at its line's tip.
  const start = current.data?.repository === registration.repository ? current.data.start ?? null : null;
  const landed: RepositorySetupProps['landed'] = start ? {
    start,
    workspace: current.data!.workspace,
    onChoose: (workflow, answered) => setLanded.mutate({ repository, workflow }, {
      onSuccess: (answer) => {
        notify(answer.lowered.length > 0
          ? t('projects.landed.lowered', { repository: registration.repository, lowered: answer.lowered.join('; ') })
          : t('projects.landed.set', { repository: registration.repository }));
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    }),
    // INTEGRATE1d: the person's Accept of the declaration they read, by its blob, or a withdrawal; a refusal comes back as an answer.
    onAcceptIntegration: (request, answered) => acceptIntegration.mutate({ repository, ...request }, {
      onSuccess: (answer) => {
        if (answer.done) {
          notify('clear' in request
            ? t('projects.integration.withdrawn', { repository: registration.repository })
            : t('projects.integration.accepted', { repository: registration.repository }));
          answered.done();
        } else answered.refused(answer.message ?? t('projects.integration.refused', { repository: registration.repository }));
      },
      onError: (error) => answered.refused(sentence(error)),
    }),
  } : null;

  // GITAUTH1f (§7.3): its git access as `LINES` resolves it, the level above as STATE sets its workspace's and this machine's; absent on a
  // shell older than it.
  const gitResolved = resolved<RepositoryGitAccess>(lines.data?.gitAccess);
  const gitAccess = gitResolved ? {
    workspace: gitResolved.workspace,
    classes: gitResolved.classes,
    above: resolveGitAccess([
      { classes: ofWorkspace(state.workspaceGitAccess, gitResolved.workspace)?.classes, source: 'workspace' },
      { classes: state.machineGitAccess, source: 'machine' },
    ]),
    onChange: (edit: GitAccessEdit, answered?: Answered) => setGitAccess.mutate({ repository, ...edit }, {
      onSuccess: () => answered?.done(),
      onError: (error) => (answered ? answered.refused(sentence(error)) : onDriverError(error)),
    }),
  } : null;
  const gitRows = remotes.data?.find((each) => each.repository === registration.repository)?.remotes;
  const git: RepositorySetupProps['git'] = (identities && gitRows) || gitAccess ? {
    remotes: gitRows ?? [],
    identities: identities ?? [],
    acts: {
      use: (remote, value) => setGitUse.mutateAsync({
        scope: 'repository', repository, remote: remote.name, ...(value ? { value } : {}),
      }),
      ...(onOpenGit ? { onSetUp: onOpenGit } : {}),
    },
    access: gitAccess,
  } : null;

  return {
    repository: registration.repository,
    driving,
    work,
    access,
    heavy,
    landed,
    sessions,
    reach,
    git,
    onOpenPlugin,
    busy: setLine.isPending || setReview.isPending || setOpinion.isPending || setLanguage.isPending
      || setCommits.isPending || setHelpers.isPending || setHandOver.isPending || setAccess.isPending || setHeavy.isPending || setStanding.isPending
      || setRead.isPending || setWrite.isPending || ruleAction.isPending || setLanded.isPending || acceptIntegration.isPending
      || setGitAccess.isPending,
  };
}
