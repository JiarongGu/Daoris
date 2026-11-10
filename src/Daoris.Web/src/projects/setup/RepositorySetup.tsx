import { type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { type AccessEdit, type ResolvedPause } from '../../settings/repository/Access';
import { type RepositoryAcross } from '../../settings/repository/Across';
import { type RuleListName } from '../../settings/repository/AgentRules';
import { type CommitEdit, type RepositoryCommits } from '../../settings/repository/Commits';
import { type HeavyGate, type HeavyMarks } from '../../settings/repository/HeavyGates';
import { type LandedSetup } from '../../settings/repository/LandedRoute';
import { type SessionChecksValue } from '../../settings/repository/SessionChecks';
import { type HelperEdit, type RepositoryHelpers } from '../../settings/repository/Helpers';
import { type HandOverEdit, type RepositoryHandOver } from '../../settings/repository/HandOver';
import { type LandingIntegrate } from '../../settings/repository/IntegratePolicy';
import { type LanguageOption, type RepositoryLanguage } from '../../settings/repository/Languages';
import { type RepositoryLine } from '../../settings/repository/Lines';
import { type OpinionEdit, type RepositoryOpinion } from '../../settings/repository/Opinions';
import { type RepositoryReview, type ReviewEdit } from '../../settings/repository/Reviews';
import { type Answered } from '../../work/InlineConfirm';
import { type RuleLists } from './RepositoryRules';
import { type FoldSpec, FoldRow } from './FoldRow';
import { SetupSection } from './SetupParts';
import { type GitIdentityShown } from '../../settings/git/Git';
import { type GitRemoteActs, type GitRemoteShown } from '../../settings/git/GitRemotes';
import { type GitAccessEdit, type ResolvedGitClass } from '../../settings/repository/GitAccess';
import { setupSections } from './setupSections';
import { setupRows } from './setupRows';
import { setupSaid } from './setupSaid';

/**
 * The sections of a repository's Setup, in
 * the order the page draws them. The last six are the place's Workflow folds, each a row `work` and `heavy` hold, drawn only where
 * `only` names them.
 */
export type SetupSectionId =
  | 'driving' | 'work' | 'git' | 'access' | 'heavy' | 'sessions' | 'reach'
  | 'landing' | 'opinion' | 'look' | 'landedRoute' | 'line' | 'more';

/**
 * A repository's git remotes with the identity each resolves to, and the presses that choose one
 * for a remote; left out where none is set or where the remotes are not read, and the section is not drawn.
 */
export type GitSetup = {
  remotes: GitRemoteShown[];
  /** The machine's identities, from which a remote's choice is offered. */
  identities: GitIdentityShown[];
  acts: GitRemoteActs;
  /** Its git access: each class as it resolves here, as the level above resolves it, and the change at this level. */
  access?: GitAccessSetup | null;
};

/** A level's git access as Setup draws it: the workspace a value from it names, the classes here and above, and a change here. */
export type GitAccessSetup = {
  workspace?: string;
  classes: readonly ResolvedGitClass[];
  above: readonly ResolvedGitClass[];
  onChange: (edit: GitAccessEdit, answered?: Answered) => void;
};

/**
 * This machine's heavy gates for it, used where its own `daoris.gates.json` marks none, and
 * the press that sets the whole list or clears it; left out where none is set, and the section is not drawn.
 */
export type HeavySetup = {
  /** In the order they run; none where none is set here. */
  gates: HeavyGate[];
  /** Whether its own file wins, as the driver read it; none until it has answered. */
  marks?: HeavyMarks | null;
  onSave: (gates: HeavyGate[] | null, answered: Answered) => void;
  /**
   * Which checks its sessions run, its row beside the gates, and the press that sets it; absent where the
   * answer lacks it, and the row is not drawn.
   */
  checks?: {
    /** The rule set for it; null where none is, which is today's. */
    set: SessionChecksValue | null;
    onChange: (checks: SessionChecksValue | null, answered: Answered) => void;
  } | null;
};

/**
 * The access setup its sessions start under, as the driver resolves it, and the press that changes it
 * here; left out where none is set, and the section is not drawn.
 */
export type AccessSetup = {
  /** Its workspace, which a value from it names. */
  workspace?: string;
  /** Each pause as it resolves, with where it is set (`LINES`' `access`). */
  pauses: ResolvedPause[];
  /** Whether what stands above it, its workspace's over this machine's, is full access. */
  aboveFull: boolean;
  onChange: (edit: AccessEdit, answered?: Answered) => void;
};

/** This machine's driver's standing choices for one repository, and the presses that change them. */
export type Driving = {
  drivable: boolean;
  held: boolean;
  ownTree: boolean;
  onDrive: (drivable: boolean) => void;
  onHold: (held: boolean) => void;
  /** What driving here does that the choice alone does not say: a direct door leaves a quest here sitting. */
  note?: string;
  /**
   * When the steward last set this repository's driving, from this run's memory: the newest of its drive,
   * undrive, trees, hold and resume here that still stands. Null or absent where none does.
   */
  stewardSet?: string | null;
};

/** Its line and how its work lands, as the driver resolves them, and the presses that set or clear them. */
export type WorkSetup = {
  /** Its line as the driver resolved it, and what said so; absent where the driver has not answered. */
  line?: RepositoryLine | null;
  onLine?: (branch: string | undefined) => void;
  /** Sets the line's policy, or with null withdraws it (`SET_LINE`'s `policy`); left out where none is set, and no policy is offered. */
  onLinePolicy?: (policy: LandingIntegrate | null) => void;
  /**
   * Where its work is reviewed before it lands, as the driver resolves it: its own, its
   * workspace's, or none set; left out where none is set, and no row is offered.
   */
  review?: RepositoryReview | null;
  onReview?: (edit: ReviewEdit) => void;
  /**
   * Which other agent reads its work before it lands, as the driver resolves it: its own, its
   * workspace's, or none set; left out where none is set, and no row is offered.
   */
  opinion?: RepositoryOpinion | null;
  onOpinion?: (edit: OpinionEdit) => void;
  /**
   * How its work is committed, as the driver resolves it: its own, its workspace's,
   * or Daoris's default; left out where none is set, and no row is offered.
   */
  commits?: RepositoryCommits | null;
  onCommits?: (edit: CommitEdit) => void;
};

/** The language its sessions write to the person in, what a helper its agent asks for does, and its standing answer. */
export type SessionsSetup = {
  /** As the driver resolved it, its workspace's by the table's name, and the table; left out where none is set. */
  language?: { resolved: RepositoryLanguage | null; inherited?: string; table: LanguageOption[]; onSet: (language: string | null) => void };
  /**
   * What a helper its agent asks for does, as the driver resolves it: its own, its workspace's, or
   * Daoris's default; left out where none is set, and no row is offered.
   */
  helpers?: RepositoryHelpers | null;
  onHelpers?: (edit: HelperEdit) => void;
  /**
   * What an agent's ask to hand its session over does, as the driver resolves it: its own, its
   * workspace's, or Daoris's default; left out where none is set, and no row is offered.
   */
  handOver?: RepositoryHandOver | null;
  onHandOver?: (edit: HandOverEdit) => void;
  /** Its standing answer, the person's words and when they set them; null for none. */
  standing?: { says: string; at?: string | null } | null;
  /** Keep a standing answer, or with null clear it; left out where none is set, and nothing is offered. */
  onStanding?: (says: string | null) => void;
};

/** Whether agents outside it read its checkout, what its sessions also write into, and its rules. */
export type ReachSetup = {
  /** As the driver resolved it; left out where none is set. */
  across?: RepositoryAcross | null;
  /** What stands under a reading of its own: its workspace's, else Daoris's, which reads. */
  readAbove?: { read: boolean; source: 'workspace' | 'default' };
  /** The other repositories of its workspace it does not write into yet: a relationship never crosses a workspace. */
  candidates?: string[];
  onRead?: (read: boolean | undefined) => void;
  onWrite?: (to: string, allow: boolean) => void;
  /** Claude Code's rules for this repository alone; absent where the rules were not answered. */
  rules?: RuleLists | null;
  onAddRule?: (list: RuleListName, rule: string, added: () => void) => void;
  onRemoveRule?: (rule: string) => void;
  /**
   * The switch *Daoris may change this checkout's files*: off by default, and on it says how many
   * sessions already run in the checkout (`running`: null where the sessions could not be read, absent while they are on their way).
   * Left out where none is set, and no row is drawn.
   */
  checkout?: { on: boolean; running?: number | null; onChange: (on: boolean) => void };
};

/**
 * How the route the place page shows lands: under a saved version, the summary the fold folds to and the editor of the
 * version's own `land` options; under Current, the conversion's door, since a rule not saved as a version has no editor.
 */
export type LandsHere =
  | { is: 'version'; summary: string; editor: ReactNode }
  | { is: 'current'; onConvert?: () => void };

export type RepositorySetupProps = {
  repository: string;
  /** How the chosen route lands, handed in by the place's Workflow section; absent, the row reads the rule as it stands. */
  lands?: LandsHere | null;
  /** Driving, where it has somewhere to start: absent for one with no root here. */
  driving?: Driving | null;
  work?: WorkSetup | null;
  /** Its sessions' access, where it can be driven. */
  access?: AccessSetup | null;
  /** This machine's heavy gates for it, where it can be driven. */
  heavy?: HeavySetup | null;
  /**
   * The landed route: what verifies the work that lands, from its Workflow answer's start node, with its workspace
   * and the person's press; absent until the driver has answered, and where the answer lacks it.
   */
  landed?: (LandedSetup & { workspace: string }) | null;
  /** Its sessions' language and standing answer, where it can be driven. */
  sessions?: SessionsSetup | null;
  reach?: ReachSetup | null;
  git?: GitSetup | null;
  /** A change on its way: every control waits for it. */
  busy?: boolean;
  /** A section a door asked to see open, beside those that open on their own. */
  open?: SetupSectionId;
  /** A count the door raises each time it asks: a section the person folded since opens again. */
  openAgain?: number;
  /**
   * The sections drawn, in the page's order; absent, all of them. The place's page draws its Driving in its head, its Line and landing
   * under Workflow and the rest under Sessions, each from the one molecule.
   */
  only?: readonly SetupSectionId[];
  /** The folds that stand before the others where `only` names the Workflow's folds alone: the place page's Gates. */
  lead?: readonly FoldSpec[];
  /** The integration declaration and its acceptance are the place page's Gates, drawn once there: this section draws neither. */
  gatesApart?: boolean;
};

/** The sections the place's page draws under its Sessions: the access, the sessions' own rows and the reach; the heavy gates went to *More*. */
export const SESSIONS_SECTIONS: readonly SetupSectionId[] = ['git', 'access', 'sessions', 'reach'];

/**
 * The folds the place's page draws under its Workflow beside Gates, each with its owner: *Landing*
 * (its `land`), *Second opinion*, *Your look*, *Landed route* (`WORKFLOW_LANDED`), *The line*, and *More* (commits and the heavy gates).
 */
export const FOLD_SECTIONS: readonly SetupSectionId[] = ['landing', 'opinion', 'look', 'landedRoute', 'line', 'more'];

/**
 * **A repository's Setup**: every setting the repository holds on this machine, in five sections, each
 * folding to a line that names its values. *Driving* (drive, hold, a tree per session), *Line and landing* (its line, how
 * its work lands, how it is committed), *Sessions' access* (full access or each pause), *Heavy gates* (this machine's builds
 * and suites for it where its own `daoris.gates.json` marks none, edited whole), *Sessions* (the language they
 * write in, what a helper their agent asks for does, the standing answer) and *Reach* (read by agents outside it, what its
 * sessions also write into, Claude Code's rules here).
 *
 * @remarks
 * **What Daoris decides folds; what the person decides shows**. A folded section's line names each value, one
 * at its workspace's or Daoris's default marked as such. A section opens on its own where it holds a value set for this
 * repository, which carries its *Clear*: a line, a landing rule, a session language or a reading of its own, a standing
 * answer, a relationship or a rule. *Driving* opens only on a press: its three are yes or no for this repository alone,
 * with nothing above them to fall back to, so none carries a *Clear*, and the page's header already says *drives here*
 * or *held*.
 *
 * **A value from above says so and offers *Set for this repository***, which opens its control in place; one set here
 * carries *Clear*, which hands it back to what stands above. **Each row's hint is its terminal twin**:
 * `daoris place <verb> <repository>`, the same file either door edits, so a refusal is the driver's sentence.
 *
 * A molecule: every state is reached by its props, and every press goes out.
 */
export function RepositorySetup({ repository, lands = null, driving, work, access, heavy, landed, sessions, reach, git, busy = false, open, openAgain, only, lead = [], gatesApart = false }: RepositorySetupProps) {
  const { t, i18n } = useTranslation();
  const { drivingLine, line, lineOwn, commits, commitRule, commitsOwn, review, reviewOwn, opinion, opinionOwn, workParts, accessShown, sessionsParts, language, languageOwn, helpers, helperRule, helpersOwn, handOver, handOverRule, handOverOwn, standing, reachParts, across, readOwn, writesTo, landingSaid, opinionSaid, reviewSaid, landedSaid, lineSaid, commitsSaid, opensItself } = setupSaid({ t, driving, work, landed, gatesApart, lands, sessions, reach, access, git, heavy });
  const { sections, lineRow, landingRow, commitsRow, reviewRow, opinionRow, landedRow, integrationRow, heavyRows, heavySaid } = setupRows({ driving, drivingLine, t, repository, busy, i18n, line, work, lineOwn, lands, commits, commitRule, commitsOwn, review, reviewOwn, opinion, opinionOwn, landed, gatesApart, heavy });
  setupSections({ work, workParts, sections, lineRow, landingRow, commitsRow, reviewRow, opinionRow, landedRow, integrationRow, git, t, repository, busy, accessShown, heavyRows, heavySaid, sessions, sessionsParts, language, languageOwn, helpers, helperRule, helpersOwn, handOver, handOverRule, handOverOwn, standing, reach, reachParts, across, readOwn, writesTo, landingSaid, opinionSaid, reviewSaid, landedSaid, lineSaid, commitsSaid });

  // The place's Workflow draws its folds as one row of names (`FoldRow`), Gates first, to keep the first screen.
  if (only && only.length > 0 && only.every((id) => FOLD_SECTIONS.includes(id))) {
    const drawn = sections.filter((section) => only.includes(section.id)).map((section): FoldSpec => ({
      id: section.id, title: t(`projects.setup.section.${section.id}`), summary: section.summary, body: section.body, opens: opensItself[section.id],
    }));
    // Keyed by the repository, so another repository's page opens its own folds afresh.
    return <FoldRow key={repository} folds={[...lead, ...drawn]} open={open} again={openAgain} />;
  }

  return (
    <div className="grid">
      {sections.filter((section) => (only ? only.includes(section.id) : !FOLD_SECTIONS.includes(section.id))).map((section) => (
        <SetupSection
          // Keyed by the repository, so another repository's page opens its own sections afresh.
          key={`${repository}:${section.id}`}
          title={t(`projects.setup.section.${section.id}`)}
          summary={section.summary}
          startsOpen={opensItself[section.id] || open === section.id}
        >
          {section.body}
        </SetupSection>
      ))}
    </div>
  );
}

