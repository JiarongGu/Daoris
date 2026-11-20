import type { GoAhead, Session } from '../../data/api';
import type { GoAheadAnswer } from '../../asks/GoAheadList';
import type { StepWaitRow } from '../attention/attentionPushed';
import type { RunWaitsHead } from '../head/RunWaitsHead';
import type { WorkflowRun } from '../../workflow/run';
import type { WaitEvents } from './anchors';
import type { AnswerPick, QuestClose, Resolution } from './AwaitingPerson';
import type { StepWait } from './dock';
import type { LineBehind } from '../review/diff';
import type { DiscardOffer, LandOffer } from '../groups';
import type { HelperWaits } from './HelperAsks';
import type { HandOverAskWait } from './HandOverAgentAsk';
import type { Answered } from '../InlineConfirm';
import type { SayRebase } from '../review/LandingSync';
import type { HeadOpinion, HeadReview, LandingPlan } from '../review/Lands';
import type { PermissionWaits } from './PermissionAsks';
import type { AnswerWaits } from './AnswerAlone';
import type { SessionGitAccessHold } from '../head/GitAccessFact';
import type { GitAskWaits } from './GitAsk';
import type { RuleWaits } from './ProposedRules';
import type { ParkQuestion } from './parkQuestion';

/**
 * What the frame hands the dock: every wait's press and its pending, one object, so a press is the same press wherever it is
 * made and its answer settles every place that names it.
 */
export type WaitProps = {
  session: Session;
  resolving?: boolean;
  /** A parked session's moves, its card's: the dock reads it only to know the park is the person's here. */
  onResolve?: (state: Resolution, note: string | null, close?: QuestClose, answered?: Answered) => void;
  /**
   * And its answer's door, the card's: the box's own say door where the box below answers the park too, else the park's answer door.
   * Settles with a refusal's sentence, or nothing.
   */
  onAnswerSession?: (answer: string) => Promise<string | null> | void;
  onAnswerChoice?: (label: string, words: string) => Promise<string | null> | void;
  answerPick?: AnswerPick;
  onAnswerAsk?: (ask: string) => void;
  goAheads?: GoAhead[];
  /** The ask its go-aheads are on, by which the count keys each as *What needs you* does. */
  goAheadsOn?: string | null;
  onGoAhead?: GoAheadAnswer;
  /** The park's question as its agent asked it, with its answers as presses; null where it asked none through the connector. */
  question?: ParkQuestion | null;
  /**
   * The box below is a driven park's answer box, which draws what the park asks with every press: the dock draws none of the
   * park's go-aheads. A parked chat's composer draws none of it, so is no such box.
   */
  parkBox?: boolean;
  /** The tool calls held for the person's answer. Absent where nothing reads them (a browser). */
  permissions?: PermissionWaits | null;
  /** The answers the answers-alone bound holds for the person. Absent where nothing reads them (a browser). */
  answers?: AnswerWaits | null;
  /** The git go-aheads its git door holds. Absent where nothing reads them (a browser). */
  gitAsks?: GitAskWaits | null;
  gitAccess?: SessionGitAccessHold | null;
  /** The rules it proposed that the person still answers. Absent where nothing reads the rules (a browser). */
  rules?: RuleWaits | null;
  /** The helpers it asked for. Absent where nothing reads them (a browser, a teammate's record). */
  helpers?: HelperWaits | null;
  /** Its agent's own ask to hand it over. Absent where nothing reads it (a browser, a teammate's record). */
  handOver?: HandOverAskWait | null;
  lands?: LandOffer | null;
  landing?: LandingPlan | null;
  onReviewRun?: () => void;
  onSayRebase?: SayRebase;
  review?: HeadReview | null;
  behind?: LineBehind | null;
  opinion?: HeadOpinion | null;
  onReview?: () => void;
  discards?: DiscardOffer | null;
  onDiscardTree?: (answered: Answered) => void;
  workBusy?: boolean;
  /** The way from each wait to the event in the conversation that raised it, where the conversation finds one. */
  toEvent?: WaitEvents;
};

/** The run's steps held for the person or left failed, as Sessions placed the session, with *What needs you*'s rows and their doors. */
export type RunWaitDoors = {
  waits: readonly StepWait[];
  steps: readonly StepWaitRow[];
  onWord?: Parameters<typeof RunWaitsHead>[0]['onWord'];
  onHeld?: Parameters<typeof RunWaitsHead>[0]['onHeld'];
  hold?: Parameters<typeof RunWaitsHead>[0]['hold'];
  release?: Parameters<typeof RunWaitsHead>[0]['release'];
  /** The session's runs as their one reading says them, whose ask's parts the dock names (`RunParts`), with a quest's door; none where unread. */
  parts?: { runs: readonly WorkflowRun[]; onQuest?: (id: string) => void } | null;
};
