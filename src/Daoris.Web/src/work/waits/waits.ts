import type { GoAhead, Session } from '../../data/api';
import { answeredPark } from '../../design/ui';
import type { RuleProposal } from '../../settings/repository/AgentRules';
import { OPEN_STATES } from '../../settings/repository/proposals';
import { workflowHolds } from '../../workflow/gate';
import type { LineBehind, SessionDiff } from '../review/diff';
import type { DiscardOffer, LandOffer } from '../groups';
import { heldDiscardable } from '../review/HeldDiscard';
import { type HelperAskRow, waitsOnWord } from './HelperAsks';
import { type HandOverAskRow, waitsOnHandOverWord } from './HandOverAgentAsk';
import { isIntake } from '../identity';
import type { LandingPlan } from '../review/Lands';
import { acceptAnswers, opinionAsked, opinionHolds, opinionState, opinionTone } from '../gates/opinion';
import { heldFor, type PermissionWait } from './PermissionAsks';
import { answerFor, type AnswerWait } from './AnswerAlone';
import { gitAsksFor, type GitAskRow } from './GitAsk';
import { REVIEW_TONE, reviewHolds, reviewState } from '../gates/review';

/**
 * What waits on the person in a session, one at a time: its question, an intake's ask, each
 * go-ahead still open, each tool call it holds for their answer under asking, a third answer in a row with nobody
 * between that the answers-alone bound holds for them, each rule it proposed that the
 * person still answers, each helper it asked for that waits on their word, and what its work offers
 * them, *Accept…*, the rebase words, a gate in their hue, or a discard the line holds. The foot's strip names them; the head
 * draws the same.
 */
export type Wait =
  | { kind: 'question'; below: boolean }
  | { kind: 'intake'; ask: string }
  | { kind: 'goAhead'; number: number }
  | { kind: 'permission'; request: string }
  | { kind: 'gitAsk'; id: string }
  | { kind: 'answerAlone' }
  | { kind: 'rule'; id: string }
  | { kind: 'helper'; id: string }
  | { kind: 'handOver'; id: string }
  | { kind: 'accept' }
  | { kind: 'rebase' }
  | { kind: 'opinion' }
  | { kind: 'review' }
  | { kind: 'discard' };

/** The kinds that are the work's, drawn by the work's own line (`Lands`), whose first is its *Accept…*. */
export const WORK_WAITS: ReadonlySet<Wait['kind']> = new Set(['accept', 'rebase', 'opinion', 'review']);

/**
 * How a session's work stands for its landing: the gates that hold it,
 * the token *Accept…* sends back where it answers the second opinion, and whether *Accept…* is offered, or the rebase words in
 * its place. One rule, read by the head's and the foot's `Lands` and by the waits, so the two places agree.
 */
export function landingStands({ lands, landing, behind }: {
  lands: LandOffer;
  landing?: LandingPlan | null;
  behind?: LineBehind | null;
}) {
  // While the review's gate holds the work, *Review in `<environment>`* stands where *Accept…*
  // would, and no *Accept…* is offered: the landing door would refuse it.
  const review = reviewHolds(landing);
  const workflow = workflowHolds(landing);
  // The second opinion's gate comes first: while it holds, no *Accept…*, unless *Accept…*
  // is the press that answers it, drawn with the token it sends back.
  const second = opinionAsked(landing);
  const opinionWaits = opinionHolds(landing);
  const answers = opinionWaits && acceptAnswers(opinionWaits) ? opinionWaits.answers ?? null : null;
  // Nothing beyond the line, committed or not, is nothing to land: the review's own condition.
  const nothingBeyond = behind?.ahead === 0 && !(typeof lands.uncommitted === 'number' && lands.uncommitted > 0);
  const acceptable = !workflow && !review && !nothingBeyond && (!opinionWaits || answers !== null);
  // Where replaying the work onto the line conflicts, the words its session is asked to rebase with stand in *Accept…*'s
  // place; *Accept…* comes back once the tree changes and the plan reads it again.
  const conflict = acceptable ? landing?.conflict ?? null : null;
  return { review, workflow, second, opinionWaits, answers, acceptable, conflict };
}

/**
 * What an open conversation's own tree offers to land once its turn has ended: the driver's own offer,
 * which the review's answer carries (`offer`), so the page offers what a press
 * would land. Its record stays `working` between turns, so the driver's look, which reads records alone, offers it nothing; the
 * turn's state is the driver's word the page holds. None while a turn runs or words wait to start one, before that word is
 * known, until a review read after the turn ended is in (an earlier read is another turn's work), in the person's checkout, where
 * the tree is detached (`treeBranch` null: no branch for a press to land), where the line is not read or nothing is beyond it,
 * once the review reads as landed, or from a host whose answer carries no offer. The press judges again.
 */
export function conversationOffer({ tree, turn, review, endedAt, readAt }: {
  /** Its own tree's name (`ownTree`), null where it works in the person's checkout. */
  tree: string | null;
  /** Where its turns stand, as the driver holds them; absent until it answers. */
  turn?: { taking: boolean; queued: readonly unknown[] } | null;
  review?: SessionDiff | null;
  /** When the page saw this turn end (or first saw it at rest); null before. */
  endedAt?: number | null;
  /** When the review's answer is read (the query's `dataUpdatedAt`). */
  readAt?: number;
}): LandOffer | null {
  if (!tree || !turn || turn.taking || turn.queued.length > 0) return null;
  if (endedAt == null || readAt === undefined || readAt < endedAt) return null;
  if (!review || review.source === 'branch' || review.landed?.asLanded || review.treeBranch === null) return null;
  if ((review.behind?.ahead ?? 0) <= 0 || !review.offer) return null;
  return review.offer;
}

/**
 * The rules a session proposed that the person still answers: its own proposals in an open state, found by
 * the session the proposal file names. A pipe-door quest session files with none, so no session's are; an older
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
  /** The ask its go-aheads are on, by which *What needs you* keys each (`waitObligations`); absent, a go-ahead is keyed by its number. */
  goAheadsOn?: string | null;
  onGoAhead?: unknown;
  /** The permissions this machine's sessions hold for the person as the shell read them, and the door that answers one. */
  permissions?: { waits?: readonly PermissionWait[]; onAnswer?: unknown } | null;
  /** The answers the answers-alone bound holds for the person as the shell read them, and the door that answers one. */
  answers?: { waits?: readonly AnswerWait[]; onAnswer?: unknown } | null;
  /** The git go-aheads the shell pushed, and the door that answers one. */
  gitAsks?: { waits?: readonly GitAskRow[]; onAnswer?: unknown } | null;
  /** The machine's rule proposals as the shell read them, and the door that answers them. */
  rules?: { proposals?: unknown; onAnswer?: unknown } | null;
  /** The helpers it asked for as the driver's look left them, and the yes that answers one. */
  helpers?: { asks?: readonly HelperAskRow[]; onApprove?: unknown } | null;
  /** Its agent's own ask to hand it over as the driver's look left it, and the yes that answers it. */
  handOver?: { ask?: HandOverAskRow | null; onApprove?: unknown } | null;
  lands?: LandOffer | null;
  /** The session's own press where its parts offer work: how many offer it. */
  entry?: { offered: number } | null;
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
 * The waits on the person, in the order the head draws them. A wait is the person's to press here: nothing
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
  // A tool call it holds for the person's answer under asking: its turn stands still until they
  // answer, or the bound refuses it, so it comes before what only waits beside a running turn.
  if (facts.permissions?.onAnswer) {
    for (const held of heldFor(facts.permissions.waits ?? [], session.id)) waits.push({ kind: 'permission', request: held.request });
  }
  // A git go-ahead its git door holds: the act on the remote stands still until they answer, or its bound.
  if (facts.gitAsks?.onAnswer) {
    for (const ask of gitAsksFor(facts.gitAsks.waits ?? [], session.id)) waits.push({ kind: 'gitAsk', id: ask.id });
  }
  // A third answer in a row with nobody between, which the answers-alone bound holds for the person: its
  // line stands still until they answer, whatever the session's state, so it comes next.
  if (facts.answers?.onAnswer && answerFor(facts.answers.waits ?? [], session.id)) waits.push({ kind: 'answerAlone' });
  // Whatever its state: a session refused something files the proposal and goes on, or ends its turn.
  if (facts.rules?.onAnswer) {
    for (const proposal of rulesAsked(facts.rules.proposals, session.id)) waits.push({ kind: 'rule', id: proposal.id });
  }
  // An agent's ask for a helper the look holds waiting on the person's word, whatever the session's state, as a rule it proposed
  // does: one not read yet, or waiting on a slot or an agent, waits on no word of theirs.
  if (facts.helpers?.onApprove) {
    for (const row of facts.helpers.asks ?? []) if (waitsOnWord(row)) waits.push({ kind: 'helper', id: row.id });
  }
  // Its agent's own ask to hand it over, which the look holds waiting on the person's word under the `handOver` rule's `ask`,
  // whatever the session's state: one not read yet, one with their yes, taken or refused waits on none.
  const handing = facts.handOver?.ask;
  if (facts.handOver?.onApprove && handing && waitsOnHandOverWord(handing)) waits.push({ kind: 'handOver', id: handing.id });
  // The session's work to land stands on its offer, any of its parts: its one press, whichever part is attended.
  const entryStands = (facts.entry?.offered ?? 0) > 0;
  if (facts.lands) {
    const stands = landingStands({ lands: facts.lands, landing: facts.landing, behind: facts.behind });
    if (entryStands || (facts.onLand && stands.acceptable && !stands.conflict)) waits.push({ kind: 'accept' });
    if (facts.onLand && stands.conflict && facts.onSayRebase) waits.push({ kind: 'rebase' });
    if (stands.second && facts.opinion?.acts && opinionTone(opinionState(stands.second.state), { holds: stands.second.holds }) === 'open') {
      waits.push({ kind: 'opinion' });
    }
    if (stands.review && facts.review?.acts && REVIEW_TONE[reviewState(stands.review.state)] === 'open') {
      waits.push({ kind: 'review' });
    }
  } else {
    if (entryStands) waits.push({ kind: 'accept' });
    if (facts.discards && heldDiscardable(facts.discards, facts.onDiscardTree as Parameters<typeof heldDiscardable>[1])) {
      waits.push({ kind: 'discard' });
    }
  }
  return waits;
}
