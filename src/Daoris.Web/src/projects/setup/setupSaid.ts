import { COMMIT_DEFAULT, commitSummary } from '../../settings/repository/Commits';
import { type LandedSetup, landedSummary } from '../../settings/repository/LandedRoute';
import { HELPER_DEFAULT, helperSummary } from '../../settings/repository/Helpers';
import { HAND_OVER_DEFAULT, handOverSummary } from '../../settings/repository/HandOver';
import { opinionSummary } from '../../settings/repository/Opinions';
import { reviewSummary } from '../../settings/repository/Reviews';
import { landingSays } from './SetupParts';
import { type TFunction } from 'i18next';
import type { AccessSetup, Driving, GitSetup, HeavySetup, LandsHere, ReachSetup, SessionsSetup, SetupSectionId, WorkSetup } from './RepositorySetup';
import { JOIN } from './setupBits';

export function setupSaid({ t, driving, work, landed, gatesApart, lands, sessions, reach, access, git, heavy }: {
  t: TFunction<"translation", undefined>;
  driving: Driving | null | undefined;
  work: WorkSetup | null | undefined;
  landed: (LandedSetup & { workspace: string; }) | null | undefined;
  gatesApart: boolean;
  lands: LandsHere | null;
  sessions: SessionsSetup | null | undefined;
  reach: ReachSetup | null | undefined;
  access: AccessSetup | null | undefined;
  git: GitSetup | null | undefined;
  heavy: HeavySetup | null | undefined;
}) {
  const marked = (value: string, source: 'repository' | 'workspace' | 'daoris') =>
    source === 'repository' ? value : t(`projects.setup.marked.${source}`, { value });

  // Driving: its three, in the words the session list uses.
  const drivingLine = driving && [
    t(driving.drivable ? 'projects.setup.summary.driven' : 'projects.setup.summary.notDriven'),
    t(driving.ownTree ? 'projects.setup.summary.ownTree' : 'projects.setup.summary.sharedTree'),
    ...(driving.drivable ? [t(driving.held ? 'projects.setup.summary.held' : 'projects.setup.summary.notHeld')] : []),
  ].join(JOIN);

  // Work: its line, then how its work lands, each with where it came from.
  const line = work?.line;
  const lineOwn = line?.source === 'repository';
  const landing = work?.landing;
  const landingOwn = landing?.source === 'repository';
  // Where its work is reviewed before it lands (REVIEWENV1a): its own, its workspace's, or none set, which is Daoris's.
  const review = work?.onReview ? work.review : undefined;
  const reviewOwn = review?.source === 'repository';
  // Which other agent reads its work before it lands (XAGENT1a): its own, its workspace's, or none set, which is Daoris's.
  const opinion = work?.onOpinion ? work.opinion : undefined;
  const opinionOwn = opinion?.source === 'repository';
  // How its work is committed (DOORS1b): its own, its workspace's, or Daoris's default, which the driver answers as a rule.
  const commits = work?.onCommits ? work.commits : undefined;
  const commitRule = commits?.rule ?? COMMIT_DEFAULT;
  const commitsOwn = commits?.source === 'repository';
  const landedOwn = landed?.start.landed.level === 'repository-landed';
  // The integration declaration at the line's tip waits for the person's acceptance (D215): never accepted, or changed since.
  const integrationOwed = !gatesApart && Boolean(landed?.onAcceptIntegration) && (landed?.start.integration?.state === 'unaccepted' || landed?.start.integration?.state === 'changed');
  const lineSaid = line
    ? marked(
        line.notReadYet
          ? t('projects.setup.summary.lineNotRead')
          : line.branch ? t('projects.setup.summary.line', { branch: line.branch }) : t('projects.setup.summary.noLine'),
        line.source === 'repository' ? 'repository' : line.source === 'workspace' ? 'workspace' : 'daoris')
    : null;
  const landingSaid = lands?.is === 'version' ? lands.summary
    : landing ? marked(landingSays(t, landing), landing.source === 'default' ? 'daoris' : landing.source) : null;
  const commitsSaid = commits ? marked(commitSummary(t, commitRule), commits.source === 'repository' || commits.source === 'workspace' ? commits.source : 'daoris') : null;
  const reviewSaid = review ? marked(reviewSummary(t, review.rule), review.rule && review.source ? review.source : 'daoris') : null;
  const opinionSaid = opinion ? marked(opinionSummary(t, opinion.rule), opinion.rule && opinion.source ? opinion.source : 'daoris') : null;
  // FABRIC-PIPELINE1a: what verifies the work that lands, marked where its workspace chose it.
  const landedSaid = landed ? marked(landedSummary(t, landed.start), landedOwn ? 'repository' : landed.start.landed.level === 'workspace-landed' ? 'workspace' : 'daoris') : null;
  const workParts = work && [lineSaid, landingSaid, commitsSaid, reviewSaid, opinionSaid, landedSaid].filter((each): each is string => each !== null);

  // Sessions: the language they write in, what a helper their agent asks for does (DOORS1b), then the standing answer.
  const language = sessions?.language;
  const languageOwn = language?.resolved?.source === 'repository';
  const helpers = sessions?.onHelpers ? sessions.helpers : undefined;
  const helperRule = helpers?.rule ?? HELPER_DEFAULT;
  const helpersOwn = helpers?.source === 'repository';
  const handOver = sessions?.onHandOver ? sessions.handOver : undefined;
  const handOverRule = handOver?.rule ?? HAND_OVER_DEFAULT;
  const handOverOwn = handOver?.source === 'repository';
  const standing = sessions?.standing ?? null;
  const sessionsParts = sessions && [
    ...(language
      ? [language.resolved?.source && language.resolved.language
          ? marked(language.resolved.name ?? language.resolved.language, language.resolved.source)
          : marked(t('projects.setup.summary.noLanguage'), 'daoris')]
      : []),
    ...(helpers ? [marked(helperSummary(t, helperRule), helpers.source === 'repository' || helpers.source === 'workspace' ? helpers.source : 'daoris')] : []),
    ...(handOver
      ? [marked(handOverSummary(t, handOverRule), handOver.source === 'repository' || handOver.source === 'workspace' ? handOver.source : 'daoris')]
      : []),
    ...(sessions.onStanding
      ? [standing ? t('projects.setup.summary.standing', { count: standing.says.split('\n').length }) : t('projects.setup.summary.noStanding')]
      : []),
  ];

  // Reach: whether it is read, what it writes into, its rules.
  const across = reach?.across;
  const readOwn = across?.source === 'repository';
  const writesTo = across?.writesTo ?? [];
  const ruleCount = reach?.rules ? reach.rules.allow.length + reach.rules.ask.length + reach.rules.deny.length : 0;
  const reachParts = reach && [
    ...(across
      ? [
          marked(
            t(across.read ? 'projects.setup.summary.read' : 'projects.setup.summary.unread'),
            across.source === 'default' ? 'daoris' : across.source,
          ),
          writesTo.length > 0
            ? t('projects.setup.summary.writes', { repositories: writesTo.join(t('projects.setup.summary.listJoin')) })
            : t('projects.setup.summary.writesNone'),
        ]
      : []),
    ...(reach.rules ? [ruleCount > 0 ? t('projects.setup.summary.rules', { count: ruleCount }) : t('projects.setup.summary.noRules')] : []),
    // Named only when on: off is the default, and a folded line names what the person set.
    ...(reach.checkout?.on ? [t('projects.setup.summary.checkout')] : []),
  ];

  // Sessions' access (AUTONOMY1d): full access, or each pause's value, marked where it came from above.
  const accessShown = access && access.pauses.length > 0 ? access : null;
  const accessOwn = Boolean(accessShown?.pauses.some((each) => each.source === 'repository'));

  // What opens on its own: a section holding a value set for this repository.
  const opensItself: Record<SetupSectionId, boolean> = {
    driving: false,
    // An unread landing rule opens it too (FABRIC-GRAPH1d): the person wrote something nothing reads.
    // A declaration owed acceptance opens it too (INTEGRATE1h): *What needs you*'s door brings the person here to read it and accept.
    work: lineOwn || landingOwn || Boolean(work?.landingUnread) || commitsOwn || reviewOwn || opinionOwn || landedOwn || integrationOwed,
    // A remote nothing serves opens it: the person has something to do there (GITAUTH1e); so does git access set here (GITAUTH1f).
    git: Boolean(git?.remotes.some((remote) => remote.serves === 'none') || git?.access?.classes.some((each) => each.source === 'repository')),
    access: accessOwn,
    // This machine's heavy gates are set for this repository alone, so a list set is its own; and so are its sessions' checks.
    heavy: (heavy?.gates.length ?? 0) > 0 || Boolean(heavy?.checks?.set),
    sessions: languageOwn || helpersOwn || handOverOwn || standing !== null,
    reach: readOwn || writesTo.length > 0 || ruleCount > 0 || Boolean(reach?.checkout?.on),
    // The Workflow folds open for a need alone (SETUPUX1o): a landing rule that reads as none, and a door that asked for one.
    landing: Boolean(work?.landingUnread),
    opinion: false,
    look: false,
    landedRoute: false,
    line: false,
    more: false,
  };
  return { drivingLine, line, lineOwn, landing, commits, commitRule, commitsOwn, review, reviewOwn, opinion, opinionOwn, workParts, accessShown, sessionsParts, language, languageOwn, helpers, helperRule, helpersOwn, handOver, handOverRule, handOverOwn, standing, reachParts, across, readOwn, writesTo, landingSaid, opinionSaid, reviewSaid, landedSaid, lineSaid, commitsSaid, opensItself };
}
