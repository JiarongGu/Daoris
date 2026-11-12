import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Session } from '../../data/api';
import { Button, Inline, PathText } from '../../design/ui';
import { WorkflowHold } from '../../workflow/WorkflowHold';
import type { LineBehind } from './diff';
import type { DiscardOffer, LandOffer } from '../groups';
import { HeldDiscardAsk, HeldDiscardButton, heldDiscardable } from './HeldDiscard';
import type { Answered } from '../InlineConfirm';
import { type LandingConflict, RebaseAsk, type SayRebase } from './LandingSync';
import { type OpinionDetail, type OpinionGate as OpinionGateState } from '../gates/opinion';
import { OpinionGate, type OpinionGateActs } from '../gates/OpinionGate';
import type { FileOpen } from './preview';
import { reviewState, type ReviewWaits } from '../gates/review';
import { ReviewGate, type ReviewGateActs } from '../gates/ReviewGate';
import type { WaitEvents } from '../waits/anchors';
import { InConversation } from '../waits/WaitAnchor';
import { landingStands } from '../waits/waits';

/**
 * What the review's gate holds and where the run's review stands, from the driver's `LANDING` answer: the review's gate while it
 * holds the work, absent where nothing waits for a review.
 */
export type LandingPlan = {
  workflow?: import('../../workflow/gate').WorkflowGateState | null;
  session?: string; form?: string; target?: string; source?: string; plugin?: string; problem?: string;
  /** The branch the press lands on already stands, an entry's or a chain's, so the press moves it on. */
  stands?: boolean;
  /** The target is the branch the session's own agent moved its tree onto, which lands as it stands. */
  own?: boolean;
  review?: ReviewWaits | null;
  /** The second opinion's gate where a level asks one, which the head draws before the review's. */
  opinion?: OpinionGateState | null;
  /** A conflict the replay onto the line would be refused for. */
  conflict?: LandingConflict | null;
  /** The person's Accept is kept on the session, carried on to clear its tree, which lands the work once it is clean. */
  carried?: { carries: number; awaiting: boolean } | null;
};

/** The review's gate as the head draws it: the set-up step's record where the frame holds it, and the presses. */
export type HeadReview = { step?: Quest | null; served?: boolean | null; acts?: ReviewGateActs; busy?: boolean };

/**
 * The second opinion as the head draws it: its findings beside their answers where the frame read them, the presses,
 * and the side bar's preview a finding's place opens.
 */
export type HeadOpinion = {
  detail?: OpinionDetail | null; acts?: OpinionGateActs; busy?: boolean; onOpenFile?: (open: FileOpen) => void;
};

/**
 * Where the work's ask (*Discard branch…*) is open, held by the frame so that it is open in one place at a time,
 * the head's or the foot's: opening it in one puts the other down. Absent, each line holds its own.
 */
export type WorkAsk = { open: boolean; onOpen: (open: boolean) => void };

type Quest = import('../../data/api').Quest;

/** The ask's open state: the frame's where it holds it, else the line's own. */
function useAsking(ask?: WorkAsk): [boolean, (open: boolean) => void] {
  const [own, setOwn] = useState(false);
  return ask ? [ask.open, ask.onOpen] : [own, setOwn];
}

/**
 * What its own tree offers to land, and the door to the run's review: the commits no branch of the person's holds, on its
 * branch in its tree, with *Review*, which opens `review/<run>` where the work could be accepted now and the session's review
 * otherwise. The Accept is the review page's alone. The gates before it (the workflow's hold, the second opinion, the review's look)
 * and the rebase words a conflict asks for stand under the line.
 *
 * @remarks
 * **One molecule, two places**: the session's head draws it, and so does the strip of what waits at the foot, from the same props.
 */
export function Lands({ session, lands, landing, onReview, onReviewRun, onSayRebase, review, behind, opinion, toEvent }: {
  /**
   * The way to the event in the conversation that raised the work's wait and the second opinion's, where it holds them
   * *In the conversation* on the work's line and under the gate.
   */
  toEvent?: Pick<WaitEvents, 'work' | 'opinion'>;
  /** The press that says the rebase words to the session, held by the frame. */
  onSayRebase?: SayRebase;
  behind?: LineBehind | null;
  session: Session;
  lands: LandOffer;
  landing?: LandingPlan | null;
  /** The session's review. */
  onReview?: () => void;
  /** The review of the run whose Accept the work waits on, where one is drawn. */
  onReviewRun?: () => void;
  review?: HeadReview | null;
  opinion?: HeadOpinion | null;
}) {
  const { t } = useTranslation();
  const { review: waits, workflow, second, acceptable, conflict } = landingStands({ lands, landing, behind });
  const door = acceptable && !conflict && onReviewRun ? onReviewRun : onReview;
  return (
    <>
      <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small">
        <span className="text-ink-faint">{t('work.head.itsWork')}</span>
        <span className="text-ink-open">{t('work.head.landed.not', { count: lands.commits })}</span>
        <PathText path={lands.branch} className="min-w-0 text-meta text-ink-faint" />
        <span className="text-ink-faint">{t('work.head.inTree', { tree: lands.tree })}</span>
        {door && (
          <Button variant={door === onReviewRun ? 'primary' : 'default'} className="px-2 py-0.5 text-small" onClick={door}>{t('work.head.review')}</Button>
        )}
        {toEvent?.work && <InConversation onPress={toEvent.work} />}
      </p>
      {onReviewRun && conflict && onSayRebase && <RebaseAsk key={session.id} conflict={conflict} onSay={onSayRebase} />}
      {/* The Accept is kept on it, carried on to clear its tree: carried on, never shown as finished work. */}
      {landing?.carried && (
        <p className="m-0 border-l-[3px] border-st-open pl-2 text-small text-ink-soft">{t('work.head.carriedOn')}</p>
      )}
      {workflow && <WorkflowHold gate={workflow} />}
      {second && (
        <OpinionGate
          gate={second}
          detail={opinion?.detail ?? null}
          acts={opinion?.acts}
          busy={opinion?.busy}
          // The person's Accept on the review page answers a dispute here, or *Reviewed* while its look holds the work.
          pressComing={Boolean(!workflow && ((onReviewRun && acceptable) || waits))}
          working={session.id}
          onOpenFile={opinion?.onOpenFile}
        />
      )}
      {/* Where its findings are handed to the session: the turn they are handed as. */}
      {second && toEvent?.opinion && <p className="m-0"><InConversation onPress={toEvent.opinion} /></p>}
      {waits && (
        <ReviewGate
          environment={waits.environment ?? ''}
          state={reviewState(waits.state)}
          step={review?.step ?? null}
          stepId={waits.quest ?? null}
          served={review?.served ?? null}
          said={waits.says ?? null}
          chat={Boolean(waits.chatRun)}
          acts={review?.acts}
          busy={review?.busy}
        />
      )}
    </>
  );
}

/**
 * What its own tree offers where the line holds its commits by content: the driver's sentence where
 * the door to the review would be, its branch and tree, *Review*, and *Discard branch…* where an unforced discard would go. The ask says
 * the driver's sentence naming the ref the commits stay at; the press is the review's Discard, unforced, and its tree goes
 * with the branch. Open until the discard answers, a refusal said inside it. The driver's words are content here, as
 * the review's own sentence is (`DiffPane`'s `said`), so no catalogue words them again. The press and its ask are
 * `HeldDiscard`'s, which the review offers too; the head and the foot's strip draw this line alike.
 */
export function Discards({ discards, onReview, onDiscard, ask, busy = false }: {
  discards: DiscardOffer;
  onReview?: () => void;
  onDiscard?: (answered: Answered) => void;
  /** Where its ask is open, the frame's; absent, its own. */
  ask?: WorkAsk;
  /** A discard is on its way, from either place: *Discard branch…* waits for it. */
  busy?: boolean;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useAsking(ask);
  const discardable = heldDiscardable(discards, onDiscard);
  return (
    <>
      <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small">
        <span className="min-w-0 text-ink-soft"><Inline text={discards.says} /></span>
        <PathText path={discards.branch} className="min-w-0 text-meta text-ink-faint" />
        <span className="text-ink-faint">{t('work.head.inTree', { tree: discards.tree })}</span>
        {discardable && !asking && (
          <HeldDiscardButton className="px-2 py-0.5 text-small" disabled={busy} onAsk={() => setAsking(true)} />
        )}
        {onReview && <Button className="px-2 py-0.5 text-small" onClick={onReview}>{t('work.head.review')}</Button>}
      </p>
      {discardable && asking && (
        <HeldDiscardAsk branch={discards.branch} keptAt={discards.keptAt!} onDiscard={onDiscard!} onClose={() => setAsking(false)} />
      )}
    </>
  );
}
