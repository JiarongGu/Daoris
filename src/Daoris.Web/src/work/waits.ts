import type { GoAhead, Session } from '../data/api';
import { answeredPark } from '../design/ui';
import type { RuleProposal } from '../settings/AgentRules';
import { OPEN_STATES } from '../settings/proposals';
import { workflowHolds } from '../workflow/gate';
import type { LineBehind } from './diff';
import type { DiscardOffer, LandOffer } from './groups';
import { heldDiscardable } from './HeldDiscard';
import { type HelperAskRow, waitsOnWord } from './HelperAsks';
import { isIntake } from './identity';
import type { LandingPlan } from './Lands';
import { acceptAnswers, opinionAsked, opinionHolds, opinionState, opinionTone } from './opinion';
import { REVIEW_TONE, reviewHolds, reviewState } from './review';

/**
 * What waits on the person in a session, one at a time (WAITFOOT1b, D168 point 2): its question, an intake's ask, each
 * go-ahead still open, each rule it proposed that the person still answers (PROPOSEHERE1b), each helper it asked for that waits
 * on their word (MIXAGENT2c), and what its work offers them, *Accept…*, the rebase words, a gate in their hue, or a discard the
 * line holds. The foot's strip names them; the head draws the same.
 */
export type Wait =
  | { kind: 'question'; below: boolean }
  | { kind: 'intake'; ask: string }
  | { kind: 'goAhead'; number: number }
  | { kind: 'rule'; id: string }
  | { kind: 'helper'; id: string }
  | { kind: 'accept' }
  | { kind: 'rebase' }
  | { kind: 'opinion' }
  | { kind: 'review' }
  | { kind: 'discard' };

/** The kinds that are the work's, drawn by the work's own line (`Lands`), whose first is its *Accept…*. */
export const WORK_WAITS: ReadonlySet<Wait['kind']> = new Set(['accept', 'rebase', 'opinion', 'review']);

/**
 * How a session's work stands for its landing (LAND4, REVIEWENV1g, XAGENT1g, REVIEWBASE5, LANDSYNC1): the gates that hold it,
 * the token *Accept…* sends back where it answers the second opinion, and whether *Accept…* is offered, or the rebase words in
 * its place. One rule, read by the head's and the foot's `Lands` and by the waits, so the two places agree.
 */
export function landingStands({ lands, landing, behind }: {
  lands: LandOffer;
  landing?: LandingPlan | null;
  behind?: LineBehind | null;
}) {
  // While the review's gate holds the work (REVIEWENV1g, design §3.1), *Review in `<environment>`* stands where *Accept…*
  // would, and no *Accept…* is offered: the landing door would refuse it.
  const review = reviewHolds(landing);
  const workflow = workflowHolds(landing);
  // The second opinion's gate comes first (XAGENT1g, the second-agent design §7): while it holds, no *Accept…*, unless *Accept…*
  // is the press that answers it, drawn with the token it sends back (§8.2–§8.3).
  const second = opinionAsked(landing);
  const opinionWaits = opinionHolds(landing);
  const answers = opinionWaits && acceptAnswers(opinionWaits) ? opinionWaits.answers ?? null : null;
  // Nothing beyond the line, committed or not, is nothing to land (REVIEWBASE5): the review's own condition, REVIEWBASE4.
  const nothingBeyond = behind?.ahead === 0 && !(typeof lands.uncommitted === 'number' && lands.uncommitted > 0);
  const acceptable = !workflow && !review && !nothingBeyond && (!opinionWaits || answers !== null);
  // LANDSYNC1 (D87's 2026-10-15 amendment point 5): replaying the work onto the line conflicts, so in *Accept…*'s place, the
  // words its session is asked to rebase with; *Accept…* comes back once the tree changes and the plan reads it again.
  const conflict = acceptable ? landing?.conflict ?? null : null;
  return { review, workflow, second, opinionWaits, answers, acceptable, conflict };
}

/**
 * The rules a session proposed that the person still answers (PROPOSEHERE1b, D74): its own proposals in an open state, found by
 * the session the proposal file names. A pipe-door quest session files with none, so no session's are (D74's limits); an older
 * shell, or one whose answer is no list, holds none.
 */
export function rulesAsked(proposals: unknown, session: string): RuleProposal[] {
  if (!Array.isArray(proposals)) return [];
  return (proposals as RuleProposal[]).filter((proposal) => proposal?.session === session && OPEN_STATES.has(proposal.state));
}

/** What the waits are read from: the props the frame hands the head, and whether the foot's box takes a park's answer. */
export type WaitFacts = {
  session: Session;
  /** The foot's box takes the park's answer: a driven park's answer box, or a parked chat's composer. */
  answerBelow?: boolean;
  onResolve?: unknown;
  onAnswerSession?: unknown;
  onAnswerAsk?: unknown;
  goAheads?: GoAhead[];
  onGoAhead?: unknown;
  /** The machine's rule proposals as the shell read them, and the door that answers them (PROPOSEHERE1b). */
  rules?: { proposals?: unknown; onAnswer?: unknown } | null;
  /** The helpers it asked for as the driver's look left them, and the yes that answers one (MIXAGENT2c). */
  helpers?: { asks?: readonly HelperAskRow[]; onApprove?: unknown } | null;
  lands?: LandOffer | null;
  landing?: LandingPlan | null;
  behind?: LineBehind | null;
  onLand?: unknown;
  onSayRebase?: unknown;
  opinion?: { acts?: object } | null;
  review?: { acts?: object } | null;
  discards?: DiscardOffer | null;
  onDiscardTree?: unknown;
};

/**
 * The waits on the person, in the order the head draws them (D168 point 2). A wait is the person's to press here: nothing
 * stands where nothing can press it (a teammate's record, a browser), what waits on an agent or a workflow is not one, and an
 * answered park goes on at the driver's next look.
 */
export function waitsOf(facts: WaitFacts): Wait[] {
  const { session } = facts;
  const waits: Wait[] = [];
  const parked = session.state === 'awaiting-person';
  const waiting = parked && !answeredPark(session);
  const intake = isIntake(session);
  if (waiting && intake && facts.onAnswerAsk && session.ask) waits.push({ kind: 'intake', ask: session.ask });
  if (waiting && !intake && (facts.answerBelow || facts.onResolve || facts.onAnswerSession)) {
    waits.push({ kind: 'question', below: Boolean(facts.answerBelow) });
  }
  if (parked && !intake && facts.onGoAhead) {
    for (const goAhead of facts.goAheads ?? []) {
      if (goAhead.state === 'asked') waits.push({ kind: 'goAhead', number: goAhead.number });
    }
  }
  // Whatever its state: a session refused something files the proposal and goes on, or ends its turn (PROPOSEHERE1b).
  if (facts.rules?.onAnswer) {
    for (const proposal of rulesAsked(facts.rules.proposals, session.id)) waits.push({ kind: 'rule', id: proposal.id });
  }
  // An agent's ask for a helper the look holds waiting on the person's word, whatever the session's state, as a rule it proposed
  // (MIXAGENT2c, D163 point 3): one not read yet, or waiting on a slot or an agent, waits on no word of theirs.
  if (facts.helpers?.onApprove) {
    for (const row of facts.helpers.asks ?? []) if (waitsOnWord(row)) waits.push({ kind: 'helper', id: row.id });
  }
  if (facts.lands) {
    const stands = landingStands({ lands: facts.lands, landing: facts.landing, behind: facts.behind });
    if (facts.onLand && stands.acceptable && !stands.conflict) waits.push({ kind: 'accept' });
    if (facts.onLand && stands.conflict && facts.onSayRebase) waits.push({ kind: 'rebase' });
    if (stands.second && facts.opinion?.acts && opinionTone(opinionState(stands.second.state), { holds: stands.second.holds }) === 'open') {
      waits.push({ kind: 'opinion' });
    }
    if (stands.review && facts.review?.acts && REVIEW_TONE[reviewState(stands.review.state)] === 'open') {
      waits.push({ kind: 'review' });
    }
  } else if (facts.discards && heldDiscardable(facts.discards, facts.onDiscardTree as Parameters<typeof heldDiscardable>[1])) {
    waits.push({ kind: 'discard' });
  }
  return waits;
}
