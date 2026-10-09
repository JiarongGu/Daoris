import type { Quest, QuestSetUp, QuestVerdict, SetUpRef } from '../api';

/**
 * Where a review's gate stands for a chain's work in one repository (REVIEWENV1c, D154 point 7; the review environment design
 * §3.1–§3.2), as the driver spells it (`ReviewStates`): `none`, `skipped` and `reviewed` let the work go; the rest hold it.
 * A newer driver may name another, which the page reads as holding and words as unread.
 */
export type ReviewState =
  | 'none' | 'skipped' | 'reviewed' | 'not-shown' | 'being-set-up' | 'shown' | 'not-yet' | 'not-held' | 'unread';

/** The states the page words; any other the driver names is said as unread, with the driver's sentence beneath. */
const KNOWN: ReadonlySet<string> = new Set<ReviewState>([
  'none', 'skipped', 'reviewed', 'not-shown', 'being-set-up', 'shown', 'not-yet', 'not-held', 'unread',
]);

/** The states that let the work land (`ReviewGateState.LetsGo`). */
export const LETS_GO: ReadonlySet<ReviewState> = new Set(['none', 'skipped', 'reviewed']);

/** A state the driver named, as the page words it: one it does not know is `unread`, holding, as the driver's own rule holds it. */
export const reviewState = (state: string | null | undefined): ReviewState =>
  state && KNOWN.has(state) ? state as ReviewState : 'unread';

/**
 * What the review's gate says beside a landing's plan or press (REVIEWENV1c, `DriverModule.Waits`): its state, the environment,
 * the level that decided, the set-up step where there is one, and the driver's sentence. Present only while it holds the work,
 * so a landing no review touches answers as it did.
 */
export type ReviewWaits = {
  state: string;
  environment?: string | null;
  level?: string | null;
  /** The set-up step, by its id; absent where none is published yet. */
  quest?: string | null;
  /** The driver's sentence, in English: the terminal's line, which the page passes through only where it words none. */
  says?: string | null;
};

/** The gate on a landing's plan where it holds the work; null where nothing waits for a review, as a plan from before says. */
export const reviewHolds = (landing: { review?: ReviewWaits | null } | null | undefined): ReviewWaits | null =>
  landing?.review && !LETS_GO.has(reviewState(landing.review.state)) ? landing.review : null;

/** A set-up step's newest set-up: the one a verdict answers, and the one the gate reads (design §3.2). */
export const newestSetUp = (step: Pick<Quest, 'setUps'>): QuestSetUp | null => step.setUps?.at(-1) ?? null;

/** A set-up's reference, whole, or null where the record carries half of one or none (REVIEWENV1b3: half is refused). */
export function setUpRef(setUp: QuestSetUp | null | undefined): SetUpRef | null {
  return setUp && typeof setUp.machine === 'string' && setUp.machine && typeof setUp.sequence === 'number'
    ? { machine: setUp.machine, sequence: setUp.sequence }
    : null;
}

/**
 * The person's verdict of one kind on a set-up: named by the machine and sequence that name it, else by its commit, as the driver
 * reads it (`ReviewGate.VerdictOn`).
 */
export function verdictOn(step: Pick<Quest, 'verdicts'>, setUp: QuestSetUp, said: QuestVerdict['said']): QuestVerdict | null {
  const ref = setUpRef(setUp);
  return [...(step.verdicts ?? [])].reverse().find((verdict) => verdict.said === said && (ref
    ? verdict.setUp?.machine === ref.machine && verdict.setUp?.sequence === ref.sequence
    : (verdict.commit ?? '').toLowerCase() === setUp.commit.toLowerCase())) ?? null;
}

/**
 * Where a set-up step stands, from its own record (design §3.2): declined is nothing to review; skipped; open, taken or with
 * nothing posted yet is being set up; then its newest set-up reviewed, said not yet to, or shown and waiting for the person's
 * look. Whether a reviewed set-up holds what would land is git's, the driver's to say (`not-held`), and never read here.
 */
export function stepState(step: Pick<Quest, 'status' | 'setUps' | 'verdicts'>): ReviewState {
  if (step.status === 'Declined') return 'none';
  if ((step.verdicts ?? []).some((verdict) => verdict.said === 'skipped')) return 'skipped';
  const newest = newestSetUp(step);
  if (step.status === 'Open' || step.status === 'Taken' || !newest) return 'being-set-up';
  if (verdictOn(step, newest, 'reviewed')) return 'reviewed';
  if (verdictOn(step, newest, 'not-yet')) return 'not-yet';
  return 'shown';
}

/** Whether a quest is a set-up step whose newest set-up waits for the person's look: *What needs you*'s row (design §3.3). */
export const waitsForLook = (quest: Quest): boolean =>
  Boolean(quest.setUpIn) && stepState(quest) === 'shown' && setUpRef(newestSetUp(quest)) !== null;

/** A press the gate offers (design §3.2–§3.3, §3.6), named as the page draws it. */
export type ReviewPress = 'reviewed' | 'notYet' | 'showAgain' | 'skip' | 'setUp';

/**
 * What each state offers, in the order it shows them (design §3.2's table): no set-up step, *Set it up* and *Skip…*; shown and
 * waiting, *Reviewed*, *Not yet…*, *Show it again* (a local set-up's alone, which Daoris serves) and *Skip…*; being set up,
 * said not yet to, or reviewed with work added since, *Skip…*, since the step's session acts next. *Show it again* serves the
 * set-up that was shown, which for work added since is not what would land, so it is not offered there. One rule, read by the
 * page and the tests.
 */
export function reviewPresses(state: ReviewState, { local = false }: { local?: boolean } = {}): ReviewPress[] {
  switch (state) {
    case 'not-shown': return ['setUp', 'skip'];
    case 'shown': return ['reviewed', 'notYet', ...(local ? ['showAgain' as const] : []), 'skip'];
    case 'being-set-up':
    case 'not-yet':
    case 'not-held': return ['skip'];
    default: return [];
  }
}

/**
 * Where a skip is given (design §3.6, `ReviewGate.Says`): on the set-up step while it is not reviewed, else on the chain's work,
 * the session's own quest, since a reviewed step refuses one.
 */
export function skipOn(state: ReviewState, step: string | null | undefined, work: string | null | undefined): string | null {
  return (state !== 'not-held' && step) || work || step || null;
}

/** A commit as a person reads it: its first eight characters, as the driver says it. */
export const shortCommit = (commit: string): string => (commit.length > 8 ? commit.slice(0, 8) : commit);
