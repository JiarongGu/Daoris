// The milestone of a unit of work (FABRIC-MILESTONE1b; the fabric events design §D0): whether its end was reached, the result kept,
// its cleanup (pending, with what waits and why, or completed, with what went) and the recovery retained and until when. The
// driver derives it at each read from the records it keeps (`WORK_STATE`, `daoris-driver work state`); the page stores nothing.
import type { TFunction } from 'i18next';

/** One thing a unit owns, with the role it has and what became of it (the inventory's entry). */
export type MilestoneEntry = {
  unit: string;
  /** `tree`, `session-branch`, `landing-branch`, `line`, `moved-aside`, `recovery-ref`, `auto-landing`, `history` or `end`. */
  kind: string;
  resource: string;
  /** `temporary`, `result`, `evidence` or `recovery`. */
  role: string;
  /** `kept`, `retained`, `removed`, `blocked` or `pending`. */
  disposition: string;
  /** Why, in the driver's clause: what waits, what blocks, what the proof was. Shown as recorded. */
  detail: string;
  /** A recovery's purge date, where one stands. */
  until?: string | null;
  proof?: string | null;
  /** The repository a recovery belongs to (RECOVERYVIEW1); a shell older than it answers none. */
  repository?: string | null;
  /** The commit a recovery ref names (RECOVERYDROP1), which a delete is verified against; a shell older than it answers none. */
  commit?: string | null;
};

/** The recovery refs of one repository a press of the delete names, each with the commit the page showed (RECOVERYDROP1). */
export type RecoveryDropGroup = { repository: string; refs: { ref: string; commit: string }[] };

/** What a press of the delete answered across its repositories: what went, what was kept and why, and what refused the whole. */
export type RecoveryDropResult = {
  dropped: { repository: string; ref: string; commit: string }[];
  skipped: { repository: string; ref: string; why: string }[];
  /** A repository's whole press refused: not the lease's holder, or no checkout here. The driver's own sentence. */
  refused: string[];
};

/** Whether the end was reached, and on what evidence. */
export type MilestoneEnd = {
  /** `ended`, `handed`, `not-ended`, `dropped` (over without its result, CLEANUP1) or `unknown`. */
  state: string;
  /** The proof's code (`merged`, `empty`, `on-line`, `pull-request`, `landed`, `carried`) where ended. */
  by?: string | null;
  on?: string | null;
  revision?: string | null;
  at?: string | null;
  /** The state in the driver's sentence: what a code the page has no words for is said by. */
  said: string;
};

/** The person's words on a held attempt (FABRIC-PERFORM1g): it happened, it did not (a new attempt), or stop the step. */
export type HeldResolution = 'happened' | 'did-not' | 'stop';

/**
 * A plugin step's attempt held for the person (FABRIC-PERFORM1g, the fabric events design §D6): nothing else can say what became of it,
 * so its gate holds until their word. `why` is a code the page words (`unknown`, `no-reconcile`, `no-outcome`, `not-current`,
 * `not-on-word`, or the one table's refusal of its outcome); `said` is the driver's sentence for a code the page has no words for.
 */
export type HeldAttempt = {
  unit: string;
  node: string;
  attempt: number;
  plugin: string;
  step: string;
  nodeState: string;
  why: string;
  outcome?: string | null;
  revision?: string | null;
  at: string;
  /** The person's word, recorded and not yet applied by the driver's next look. */
  resolution?: HeldResolution | null;
  resolvedAt?: string | null;
  said: string;
};

export type MilestoneUnit = {
  unit: string;
  repository: string;
  branch?: string | null;
  session?: string | null;
  end: MilestoneEnd;
  result: MilestoneEntry[];
  cleanup: { state: string; waits: MilestoneEntry[]; gone: MilestoneEntry[] };
  recovery: MilestoneEntry[];
  entries: MilestoneEntry[];
  /** Its plugin steps' attempts held for the person (FABRIC-PERFORM1g), joined from the answer's `held` by the page. */
  held?: HeldAttempt[];
};

/**
 * What `WORK_STATE` answers: the units, Daoris's own recoveries no unit names (RECOVERYVIEW1, CLEANUP1: a folder moved aside, a
 * recovery ref), what could not be read (a repository with no checkout here), and every attempt held for the person, of any unit
 * (FABRIC-PERFORM1g: a quest's too, which no milestone names).
 */
export type WorkState = { units: MilestoneUnit[]; recovery: MilestoneEntry[]; unread: string[]; held: HeldAttempt[] };

/** The why codes the page has words for; any other is the one table's refusal of an outcome, said with its code. */
const HELD_WHY = ['unknown', 'no-reconcile', 'no-outcome', 'not-current', 'not-on-word'] as const;

/** Why an attempt is held, in the reader's language (FABRIC-PERFORM1g). */
export function heldWhy(t: TFunction, held: HeldAttempt): string {
  const code = HELD_WHY.find((one) => one === held.why);
  return code ? t(`work.milestone.held.why.${code}`) : t('work.milestone.held.why.other', { code: held.why });
}

/** A held attempt in one line: its step, its attempt, and why, or the person's word waiting for the next look. */
export function heldSentence(t: TFunction, held: HeldAttempt): string {
  return held.resolution
    ? t('work.milestone.held.worded', { node: held.node, attempt: held.attempt, word: t(`work.milestone.held.word.${held.resolution}`) })
    : t('work.milestone.held.step', { node: held.node, attempt: held.attempt, why: heldWhy(t, held) });
}

/** The kinds of thing a unit owns that have a word of the page's; a `line` or an `end` is its resource's own name. */
const KINDS = ['tree', 'session-branch', 'landing-branch', 'moved-aside', 'recovery-ref', 'auto-landing', 'history'] as const;

/** An entry by what it is: its kind in the reader's language, where the page has a word, and its name. */
export function entryName(t: TFunction, entry: MilestoneEntry): string {
  const kind = KINDS.find((one) => one === entry.kind);
  return [kind ? t(`work.milestone.kind.${kind}`) : null, entry.resource].filter(Boolean).join(' ');
}

/**
 * What of a unit waits on the person (design §D0, *What needs you* only where something waits on them): the entries its cleanup
 * is blocked on, and, where no record names its owner, the unit itself. A wait that is pending (the cycle's own, a guard's) is
 * not the person's: it ends by itself.
 */
export function waitsOnPerson(unit: MilestoneUnit): MilestoneEntry[] {
  const blocked = unit.cleanup.waits.filter((entry) => entry.disposition === 'blocked');
  if (blocked.length > 0 || unit.end.state !== 'unknown') return blocked;
  return unit.entries.slice(0, 1);
}

/** The units whose branch, session or id is `name`: the milestone a branch row says (a session branch or a landing branch). */
export function unitOfBranch(units: readonly MilestoneUnit[], repository: string, branch: string): MilestoneUnit | undefined {
  return units.find((unit) => unit.repository.toLowerCase() === repository.toLowerCase()
    && (unit.branch === branch || unit.entries.some((entry) => (entry.kind === 'session-branch' || entry.kind === 'landing-branch') && entry.resource === branch)));
}

/** Older shells answer nothing of the kind, and a malformed answer reads as none. */
export function readWorkState(answer: unknown): WorkState {
  const read = answer as { units?: unknown; recovery?: unknown; unread?: unknown; held?: unknown } | null | undefined;
  // FABRIC-PERFORM1g: a shell older than the field answers none held.
  const held = Array.isArray(read?.held) ? read.held as HeldAttempt[] : [];
  const units = Array.isArray(read?.units) ? read.units as MilestoneUnit[] : [];
  return {
    units: units.map((unit) => ({ ...unit, held: held.filter((each) => each.unit === unit.unit) })),
    recovery: Array.isArray(read?.recovery) ? read.recovery as MilestoneEntry[] : [],
    unread: Array.isArray(read?.unread) ? read.unread.filter((each): each is string => typeof each === 'string') : [],
    held,
  };
}
