import type { TFunction } from 'i18next';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Session } from '../data/api';
import { Button, Inline, PathText } from '../design/ui';
import { WorkflowHold } from '../workflow/WorkflowHold';
import type { LineBehind } from './diff';
import type { DiscardOffer, LandOffer } from './groups';
import { HeldDiscardAsk, HeldDiscardButton, heldDiscardable } from './HeldDiscard';
import { type Answered, InlineConfirm } from './InlineConfirm';
import { type LandingConflict, type LandsCommit, type LeftOutCommit, LeftOutLines, RebaseAsk, type SayRebase } from './LandingSync';
import { type OpinionDetail, type OpinionGate as OpinionGateState, opinionState } from './opinion';
import { OpinionGate, type OpinionGateActs, unsettledWords } from './OpinionGate';
import type { FileOpen } from './preview';
import { reviewState, type ReviewWaits } from './review';
import { ReviewGate, type ReviewGateActs } from './ReviewGate';
import type { WaitEvents } from './anchors';
import { InConversation } from './WaitAnchor';
import { landingStands } from './waits';

/** How long a typed branch name rests before the landing plan is asked with it (LANDNAME1b). */
const NAME_PAUSE = 300;

/**
 * Where accepting a session's work would put it (D87, D100): the driver's `LANDING` answer, which the review reads too; and the
 * review's gate while it holds the work (REVIEWENV1c), absent where nothing waits for a review.
 */
export type LandingPlan = {
  workflow?: import('../workflow/gate').WorkflowGateState | null;
  session?: string; form?: string; target?: string; source?: string; plugin?: string; problem?: string;
  /** The branch the press lands on already stands, an entry's or a chain's, so the press moves it on (ACCEPTALL1g). */
  stands?: boolean;
  /** The target is the branch the session's own agent moved its tree onto, which lands as it stands (TREEFOLLOW1, TREEFOLLOWWEB1). */
  own?: boolean;
  review?: ReviewWaits | null;
  /** The second opinion's gate where a level asks one (XAGENT1f), which the head draws before the review's (XAGENT1g). */
  opinion?: OpinionGateState | null;
  /** What the press leaves out because the line holds it, what it replays, and a conflict it would be refused for (LANDSYNC1). */
  leftOut?: LeftOutCommit[] | null;
  lands?: LandsCommit[] | null;
  conflict?: LandingConflict | null;
};

/**
 * Where Accept puts the work under a branch rule, said before the press (D87, D100): a new branch, or one that already stands, which
 * the press moves on (ACCEPTALL1g, D167's 2026-10-15 amendment point 2, D145 §3): an entry's or a chain's, an earlier landing made.
 * The session's own branch, which its agent moved its tree onto, lands as it stands and makes none (TREEFOLLOWWEB1, D164 point 4).
 */
export function landsOnBranch(
  t: TFunction, plan: { target: string; plugin?: string | null; stands?: boolean | null; own?: boolean | null },
): string {
  if (plan.own) {
    return plan.plugin
      ? t('work.review.landsOwnPlugin', { branch: plan.target, plugin: plan.plugin })
      : t('work.review.landsOwn', { branch: plan.target });
  }
  if (plan.stands) {
    return plan.plugin
      ? t('work.review.landsOnStandingPlugin', { branch: plan.target, plugin: plan.plugin })
      : t('work.review.landsOnStanding', { branch: plan.target });
  }
  return plan.plugin
    ? t('work.review.landsOnBranchPlugin', { branch: plan.target, plugin: plan.plugin })
    : t('work.review.landsOnBranch', { branch: plan.target });
}

/** The review's gate as the head draws it (REVIEWENV1g): the set-up step's record where the frame holds it, and the presses. */
export type HeadReview = { step?: Quest | null; served?: boolean | null; acts?: ReviewGateActs; busy?: boolean };

/**
 * The second opinion as the head draws it (XAGENT1g): its findings beside their answers where the frame read them, the presses,
 * and the side bar's preview a finding's place opens.
 */
export type HeadOpinion = {
  detail?: OpinionDetail | null; acts?: OpinionGateActs; busy?: boolean; onOpenFile?: (open: FileOpen) => void;
};

/**
 * Where the work's ask (*Accept…*, *Discard branch…*) is open, held by the frame so that it is open in one place at a time,
 * the head's or the foot's (D168 point 3): opening it in one puts the other down. Absent, each line holds its own.
 */
export type WorkAsk = { open: boolean; onOpen: (open: boolean) => void };

type Quest = import('../data/api').Quest;

/** The ask's open state: the frame's where it holds it (D168 point 3), else the line's own. */
function useAsking(ask?: WorkAsk): [boolean, (open: boolean) => void] {
  const [own, setOwn] = useState(false);
  return ask ? [ask.open, ask.onOpen] : [own, setOwn];
}

/**
 * What its own tree offers to land, and its press (LAND4, D102's LAND4 note): the commits no branch of the person's holds,
 * on its branch in its tree, with *Review* where the review can open and *Accept…*, which asks once under the line. The ask
 * says where accepting puts the work by the repository's rule, what would refuse it now, the uncommitted work a landing
 * refuses, and for a session that did not finish that only what it committed lands. The move is the review's Accept, in
 * the primary's hue: a landing destroys nothing. Open until the landing answers, a refusal said inside it (UXFIX2).
 *
 * @remarks
 * **One molecule, two places** (WAITFOOT1b, D168): the session's head draws it, and so does the strip of what waits at the
 * foot, from the same props, so a press in either is the same press. The frame holds where its ask is open (`ask`), and
 * whether a landing is on its way (`busy`), which holds *Accept…* in both.
 */
export function Lands({ session, lands, landing, planning, onReview, onLand, onSayRebase, onName, review, behind, opinion, ask, busy = false, toEvent }: {
  /**
   * The way to the event in the conversation that raised the work's wait and the second opinion's, where it holds them
   * (WAITFOOT1c, D168 point 4): *In the conversation* on the work's line and under the gate.
   */
  toEvent?: Pick<WaitEvents, 'work' | 'opinion'>;
  /** The press that says the rebase words to the session, held by the frame (LANDSYNC1w). */
  onSayRebase?: SayRebase;
  behind?: LineBehind | null;
  /** The plan shown is the previous name's while the entered one is read (LANDNAME1b). */
  planning?: boolean;
  session: Session;
  lands: LandOffer;
  landing?: LandingPlan | null;
  onReview?: () => void;
  onLand?: (answered: Answered, answers?: string | null, branch?: string | null) => void;
  onName?: (name: string) => void;
  review?: HeadReview | null;
  opinion?: HeadOpinion | null;
  /** Where its ask is open, the frame's (D168 point 3); absent, its own. */
  ask?: WorkAsk;
  /** A landing is on its way, from either place: *Accept…* waits for it. */
  busy?: boolean;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useAsking(ask);
  // LANDNAME1b: a branch name entered here replaces the rule's whole name; the frame asks the plan again with it. The rule's own
  // name is kept from the plan while none is entered, as the field's placeholder.
  const [entered, setEntered] = useState('');
  // The name the plan was last asked with: the plan reads git, so it is asked after a pause or on blur, not per keystroke. The
  // press waits for the plan of exactly the typed name, so a refusal of it is never skipped by a fast press.
  const [asked, setAsked] = useState('');
  const typed = entered.trim();
  const rule = useRef<string | null>(null);
  // The session's own branch lands as it stands (TREEFOLLOWWEB1): a name entered would be refused, so none is asked for.
  if (!typed && landing?.form === 'branch' && landing.target) rule.current = landing.own ? null : landing.target;
  const askPlan = (name: string) => {
    setAsked(name);
    onName?.(name);
  };
  useEffect(() => {
    if (typed === asked) return undefined;
    const pause = window.setTimeout(() => askPlan(typed), NAME_PAUSE);
    return () => window.clearTimeout(pause);
  }, [typed, asked]);
  // Put down here or in the other place (D168 point 3): a name typed in it goes with it, so the plan is asked without it again.
  useEffect(() => {
    if (asking) return;
    setEntered('');
    if (asked) askPlan('');
  }, [asking]);
  const putDown = () => setAsking(false);
  const { review: waits, workflow, second, opinionWaits, answers, acceptable, conflict } = landingStands({ lands, landing, behind });
  // A session that did not finish lands what it committed before it ended (LAND4); one finished, at a checkpoint or by
  // itself, lands its work.
  const unfinished = session.state !== 'completed';
  const where = !landing?.target ? null
    : landing.form === 'merge' ? t('work.review.landsOnLine', { line: landing.target })
      : landsOnBranch(t, { target: landing.target, plugin: landing.plugin, stands: landing.stands, own: landing.own });
  return (
    <>
      <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small">
        <span className="text-ink-faint">{t('work.head.itsWork')}</span>
        <span className="text-ink-open">{t('work.head.landed.not', { count: lands.commits })}</span>
        <PathText path={lands.branch} className="min-w-0 text-meta text-ink-faint" />
        <span className="text-ink-faint">{t('work.head.inTree', { tree: lands.tree })}</span>
        {onLand && !asking && acceptable && !conflict && (
          <Button variant="primary" className="px-2 py-0.5 text-small" disabled={busy} onClick={() => setAsking(true)}>
            {t('work.head.accept')}
          </Button>
        )}
        {onReview && <Button className="px-2 py-0.5 text-small" onClick={onReview}>{t('work.head.review')}</Button>}
        {toEvent?.work && <InConversation onPress={toEvent.work} />}
      </p>
      {onLand && !asking && conflict && onSayRebase && <RebaseAsk key={session.id} conflict={conflict} onSay={onSayRebase} />}
      {workflow && <WorkflowHold gate={workflow} />}
      {second && (
        <OpinionGate
          gate={second}
          detail={opinion?.detail ?? null}
          acts={opinion?.acts}
          busy={opinion?.busy}
          // A press of the person's answers a dispute here: *Accept…*, or D154's *Reviewed* while its look holds the work.
          pressComing={Boolean(!workflow && ((onLand && acceptable) || waits))}
          working={session.id}
          onOpenFile={opinion?.onOpenFile}
        />
      )}
      {/* Where its findings were handed to the session (WAITFOOT1c): the turn they were handed as. */}
      {second && toEvent?.opinion && <p className="m-0"><InConversation onPress={toEvent.opinion} /></p>}
      {waits && (
        <ReviewGate
          environment={waits.environment ?? ''}
          state={reviewState(waits.state)}
          step={review?.step ?? null}
          stepId={waits.quest ?? null}
          served={review?.served ?? null}
          said={waits.says ?? null}
          acts={review?.acts}
          busy={review?.busy}
        />
      )}
      {onLand && asking && acceptable && (
        <InlineConfirm
          tone="primary"
          block
          label={t('work.head.landTitle', { branch: lands.branch })}
          says={(
            <>
              <p className="m-0 text-small text-ink-soft"><Inline text={where ?? t('work.head.landReading')} /></p>
              {/* What it leaves out because the line holds it, and what it replays (LANDSYNC1), said before the press. */}
              <LeftOutLines leftOut={landing?.leftOut} lands={landing?.lands} />
              {/* What the press answers, shown in it (the second-agent design §8.2–§8.3): making it is the person's answer. */}
              {answers && opinionWaits && (
                <p className="m-0 border-l-[3px] border-st-open pl-2 text-small text-ink-soft">
                  {t('opinion.accept.answers', { unsettled: unsettledWords(t, opinionState(opinionWaits.state), opinionWaits) })}
                </p>
              )}
              {landing?.problem && (
                <p className="m-0 border-l-[3px] border-warn pl-2 text-small text-ink-soft">
                  <Inline text={t('work.review.landingProblem', { problem: landing.problem })} />
                </p>
              )}
              {typeof lands.uncommitted === 'number' && lands.uncommitted > 0 && (
                <p className="m-0 border-l-[3px] border-warn pl-2 text-small text-ink-soft">
                  {t('work.head.landUncommitted', { count: lands.uncommitted })}
                </p>
              )}
              {unfinished && <p className="m-0 text-small text-ink-soft">{t('work.head.landUnfinished')}</p>}
            </>
          )}
          meanIt={t('work.head.landMeanIt')}
          // Held while the plan says the press would be refused (D100, STANDSREFUSE1): its sentence above says why.
          ready={typed === asked && !planning && !landing?.problem}
          onConfirm={(answered) => onLand?.(answered, answers, typed || null)}
          onClose={putDown}
        >
          {rule.current && (
            <label className="flex min-w-0 flex-1 basis-64 flex-col gap-1 text-small text-ink-soft">
              <input
                aria-label={t('work.head.landName')}
                placeholder={rule.current}
                value={entered}
                onChange={(event) => setEntered(event.target.value)}
                onBlur={() => { if (typed !== asked) askPlan(typed); }}
                className="min-h-[1.9rem] min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
              />
              <span>{t('work.head.landNameHint', { branch: rule.current })}</span>
            </label>
          )}
        </InlineConfirm>
      )}
    </>
  );
}

/**
 * What its own tree offers where the line holds its commits by content (SQUASHTIDY1b, D102's note): the driver's sentence where
 * *Accept…* would be, its branch and tree, *Review*, and *Discard branch…* where an unforced discard would go now. The ask says
 * the driver's sentence naming the ref the commits stay at; the press is the review's Discard, unforced, and its tree goes
 * with the branch. Open until the discard answers, a refusal said inside it (UXFIX2). The driver's words are content here, as
 * the review's own sentence is (`DiffPane`'s `said`), so no catalogue words them again. The press and its ask are
 * `HeldDiscard`'s, which the review offers in *Accept*'s place too (SQUASHTIDY1f); the head and the foot's strip draw this line
 * alike (D168), its ask open in one of them at a time.
 */
export function Discards({ discards, onReview, onDiscard, ask, busy = false }: {
  discards: DiscardOffer;
  onReview?: () => void;
  onDiscard?: (answered: Answered) => void;
  /** Where its ask is open, the frame's (D168 point 3); absent, its own. */
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
