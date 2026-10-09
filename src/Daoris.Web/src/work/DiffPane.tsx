import { type ReactNode, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Session } from '../api';
import { sentence } from '../format';
import { cn } from '../lib/cn';
import { ExternalLink } from '../links';
import { store, stored } from '../lib/stored';
import {
  useDiscardSessionTree, useHandOff, useHandOffPress, useLandSessionTree, useLanding, useReadingSince, useSessionDiff,
} from '../shell';
import { Button, EmptyState, Inline, SESSION_ACTIVE } from '../ui';
import { reviewKnown } from './diff';
import { LandedNote } from './LandedNote';
import type { DiffLayout } from './PatchView';
import { reviewHolds, reviewState } from './review';
import { ReviewFailed, ReviewFiles, ReviewHead, ReviewReading } from './ReviewFrame';

/** How a reader likes the changes laid out (REVIEW2) — a per-viewer convenience, like the frame's widths. */
const LAYOUT = 'daoris.reviewLayout';

/**
 * Review: what the session actually did, and what the person does about it (SURF6, design §5).
 *
 * @remarks
 * **A multibuffer, not a tree beside a pane** (IDE study §2): every changed file is one row in one
 * scroll and opens in place. That is the shape that makes a forty-file review finishable, and the
 * per-file *viewed* mark is the other half of it.
 *
 * **Reading and acting are separate routes.** The diff route is read-only by construction; the acts
 * are their own, so a surface built to *show* the work cannot change it by accident.
 *
 * **The verbs are Daoris's, never an editor's**: *accept* — land the work as the repository's rule
 * says (WSR1, D87): merged into its line, or put on a branch for the person to push, and the pane says
 * which before the press — *discard the tree*, and *send it back as a quest*. Never keep/reject per hunk: the
 * session already committed, and reaching in to fix what you are reviewing is what D32 forbids.
 * Sending it back is the one move Daoris has that an editor does not, and it is a door into the
 * platform's own composer rather than a second publish path.
 *
 * **Discard asks twice, and the second sentence is the host's.** The first press is unforced, which
 * is what makes the tree layer refuse and name what would be lost; only then is the destructive press
 * offered. Destroying work is never a side effect of tidying (D51 rule 7).
 *
 * **An act's refusal renders verbatim** — "the checkout is not clean" and "the tree holds commits
 * nobody merged" are different facts with different next moves, and only the host knows which. The
 * review's own refusal ("no tree here", "no range recorded") is worded by its code (REVIEW4).
 *
 * **A landed session reads as landed** (REVIEW2, D113). Where this machine's landing record holds the
 * session's landing, the review says where the work landed, above what it reads; once a tidy took the
 * tree, what it reads is the landed branch, in the repository's own checkout. While the review reads as
 * landed — the branch stands, or the tree is gone — accepting and sending back are not offered, and
 * discarding only where a tree is still here; the hand-off stays where one applies.
 *
 * **It says what it is doing while git reads** (REVIEW4). The frame stands at once with what the record holds —
 * the title, the repository, its own branch, the base and the commits it reported — and the files and the patch as
 * a skeleton under words that say what is read and, after a moment, for how long. A refusal is worded from its code
 * with the person's next move. An ended session's answer is kept, so a second open is the first's; a live one's is
 * read again behind the last answer, held dimmed. Leaving cancels the wait; what git answers after is kept.
 *
 * Desktop-only, structurally: the hook is gated on the bridge, so in a browser this never asks.
 */
export function DiffPane({ session, record, title = null, hasTree = false, onSendBack, onPreview }: {
  session: string | null;
  /**
   * The attended session's record (REVIEW4): what the review's frame says before git answers, and whether its answer
   * is final — an ended session's range does not move. Absent, the frame says what the answer says.
   */
  record?: Session | null;
  /** The session's title for the frame, or null where something above the review already names it. */
  title?: string | null;
  /**
   * Whether this session holds a working tree OF ITS OWN on this machine (D51) — which is what the
   * acts act on. The record carries it and only a loopback caller is told, so the page can ask this
   * without the driver: a mirrored record from another machine has no tree here, and a session in
   * the repository's own checkout has no tree of its own. Offering to merge or discard either would
   * be offering something that can only ever refuse (UX5 U66, seen on the window).
   */
  hasTree?: boolean;
  /** The door into the quest composer. Absent where there is nowhere to send it. */
  onSendBack?: () => void;
  /** Opens one file's preview in the side bar (PREVIEW1, D111), by its path in the review. */
  onPreview?: (path: string) => void;
}) {
  const { t } = useTranslation();
  // The record is the attended one only while it is this session's: a frame never says another session's facts.
  const own = record && record.id === session ? record : null;
  const diff = useSessionDiff(session, own ? { updated: own.updated, live: SESSION_ACTIVE.has(own.state) } : null);
  // A first read, or a read again after a refusal: the review is reading, not failed (REVIEW4).
  const reading = diff.isPending || (diff.isError && diff.isFetching);
  // A newer read behind an answer already shown: that answer is held, dimmed (D41's loading rule).
  const refreshing = diff.isSuccess && diff.isFetching;
  const started = useReadingSince(session);
  const since = diff.isFetching ? started : null;
  // Where this session's work landed, where this machine's landing record holds it (REVIEW2, D113).
  const landed = diff.data?.landed ?? null;
  // The host decides whether the review reads as landed — its landed branch stands, or its tree is gone — and
  // the acts follow it: no second landing (refused while the branch stands, D87), no sending back.
  const asLanded = Boolean(landed?.asLanded);
  // A tree the host could not read, or says is gone: nothing to land or discard there (UX5 U66).
  const treeGone = diff.data?.source === 'branch'
    || (diff.error as { code?: unknown } | null)?.code === 'SESSION_TREE_GONE';
  const treeHere = hasTree && !reading && !treeGone;
  const plannable = treeHere && !asLanded;
  const sendBack = asLanded ? undefined : onSendBack;
  const land = useLandSessionTree();
  // What a press would do, asked only where there is a tree to land and the review does not read as landed.
  const landing = useLanding(plannable ? session : null);
  // While the review's gate holds the work (REVIEWENV1g, design §3.1), the review says it waits where *Accept* would be, and
  // offers no *Accept*, which the landing door would refuse; the session's head carries the verdict's presses.
  const waits = reviewHolds(landing.data);
  const canAccept = plannable && !waits;
  const discard = useDiscardSessionTree();
  // The branch this session's landing made, handed to a landing plugin afterwards (WSR5b) — asked whether
  // or not a tree is still here, since a tidy removes it and the branch stands.
  const handOff = useHandOff(session);
  const hand = useHandOffPress();
  const handable = handOff.data?.branch && handOff.data.plugin ? handOff.data : null;

  // Per-reader, per-session, and never written down: which files this person has opened and which
  // they have ticked off. Keyed by path, reset by attending a different session.
  const [open, setOpen] = useState<Record<string, boolean>>({});
  const [viewed, setViewed] = useState<Record<string, boolean>>({});
  // One column or the old side beside the new (REVIEW2): the reader's, for every review.
  const [layout, setLayout] = useState<DiffLayout>(() => (stored(LAYOUT) === 'split' ? 'split' : 'unified'));
  const chooseLayout = (next: DiffLayout) => {
    setLayout(next);
    store(LAYOUT, next);
  };
  const [shown, setShown] = useState<string | null>(null);
  // Whatever the tree layer last said, done or refused. Its sentence is the contract.
  const [said, setSaid] = useState<string | null>(null);
  // The pull request the rule's plugin opened, where it opened one (D100) — one press from the sentence.
  const [opened, setOpened] = useState<string | null>(null);
  // The second press. It exists only because the first one produced the sentence above.
  const [confirmingDiscard, setConfirmingDiscard] = useState(false);

  if (shown !== session) {
    setShown(session);
    setOpen({});
    setViewed({});
    // A pending confirm belongs to the session it was asked about. Carrying it across would arm a
    // destructive second press against work the person never looked at.
    setConfirmingDiscard(false);
    setSaid(null);
    setOpened(null);
  }

  // 🔴 Which session an answer is FOR. A refusal that landed after the person moved on armed the forced
  // press against the session attended now — a tree nobody had looked at (REV3). The reset above
  // runs at render; an answer arrives whenever git finishes.
  const attended = useRef(session);
  attended.current = session;

  const act = (
    run: Promise<{ done: boolean; message: string; plugin?: { pullRequest?: string } | null }>,
    onDone?: () => void,
    onRefused?: () => void,
  ) => {
    const askedFor = session;
    const current = () => attended.current === askedFor;
    setSaid(null);
    setOpened(null);
    void run.then(
      (result) => {
        if (!current()) return;
        setSaid(result.message);
        setOpened(result.plugin?.pullRequest ?? null);
        if (result.done) onDone?.();
        else onRefused?.();
      },
      // A thrown refusal is the driver not being up, or the record naming no tree — a different
      // class from "the tree layer declined", and it never arms a destructive confirm.
      (error: unknown) => {
        if (current()) setSaid(sentence(error));
      },
    );
  };

  if (!session) {
    return (
      <EmptyState
        icon="diff"
        headline={t('work.review.none.headline')}
        body={t('work.review.none.body')}
      />
    );
  }

  // What the frame says (REVIEW4): the record's facts from the first frame, and the range the answer measured from
  // once it has one, since bringing a tree up to date moves it (WSR6).
  const known = own
    ? reviewKnown(own, { title, ownTree: hasTree })
    : { title, repository: null, branch: null, base: null, commits: null, machine: null };
  const head = <ReviewHead {...known} base={diff.data?.base || known.base} refreshing={refreshing ? since : null} />;

  // Git is reading: the frame, then what is read and for how long over the skeleton. Nothing acts on a tree whose
  // state the answer has not said yet.
  if (reading) {
    return (
      <section className="flex min-h-0 flex-col" aria-busy="true">
        {head}
        <ReviewReading repository={known.repository} since={since} />
      </section>
    );
  }

  const files = diff.data?.files ?? [];

  // Gated on the TREE, not on the diff: a session whose range git cannot read may still hold a tree
  // worth discarding, and one whose record travelled here holds none at all. Sending the work back
  // is about the work, not the tree, so it stands wherever there is a door for it (UX5 U66) — but for
  // work that already landed (REVIEW2), whose next move is its branch's, not the session's.
  const acts = !treeHere && !sendBack && !handable ? null : (
    <footer className="grid shrink-0 gap-2 border-t border-line px-3 py-2">
      {said && <p className="m-0 text-small text-ink-soft">{said}</p>}
      {said && opened && (
        <p className="m-0 text-small">
          <ExternalLink href={opened} className="text-accent underline underline-offset-2">
            {t('work.review.pullRequest')}
          </ExternalLink>
        </p>
      )}
      {/* Where a press sends the work, before it is pressed (D87) — and who pushes it where a plugin
          does (D100). A shell older than the rule answers no form, and the pane then claims nothing. */}
      {!said && canAccept && landing.data?.form === 'branch' && landing.data.target && (
        <p className="m-0 text-small text-ink-faint">
          <Inline text={landing.data.plugin
            ? t('work.review.landsOnBranchPlugin', { branch: landing.data.target, plugin: landing.data.plugin })
            : t('work.review.landsOnBranch', { branch: landing.data.target })} />
        </p>
      )}
      {!said && canAccept && landing.data?.problem && (
        <p className="m-0 border-l-[3px] border-warn pl-2 text-small text-ink-soft">
          {t('work.review.landingProblem', { problem: landing.data.problem })}
        </p>
      )}
      {!said && canAccept && landing.data?.form === 'merge' && landing.data.target && (
        <p className="m-0 text-small text-ink-faint">
          <Inline text={t('work.review.landsOnLine', { line: landing.data.target })} />
        </p>
      )}
      {!said && plannable && waits && (
        <p className="m-0 border-l-[3px] border-st-open pl-2 text-small text-ink-soft">
          <Inline text={waits.environment ? t('review.waits', { environment: waits.environment }) : t('review.titleAny')} />
          {' · '}{t(`review.state.${reviewState(waits.state)}`)}
        </p>
      )}
      {/* The branch the landing made, and who can push it now (WSR5b) — and what stands in the way. */}
      {!said && handable && (
        <p className="m-0 text-small text-ink-faint">
          <Inline text={t('work.review.handOn', { branch: handable.branch, plugin: handable.plugin })} />
        </p>
      )}
      {!said && handable?.problem && (
        <p className="m-0 border-l-[3px] border-warn pl-2 text-small text-ink-soft">
          {t('work.review.handProblem', { problem: handable.problem })}
        </p>
      )}

      <div className="flex flex-wrap items-center gap-2">
        {canAccept && (
          <Button
            variant="primary"
            disabled={land.isPending}
            onClick={() => act(land.mutateAsync(session))}
          >
            {land.isPending ? t('work.review.accepting') : t('work.review.accept')}
          </Button>
        )}

        {handable && (
          <Button
            // A press that could only be refused is not offered (UX5 U66): the sentence above says why.
            disabled={hand.isPending || Boolean(handable.problem)}
            onClick={() => act(hand.mutateAsync(session))}
          >
            {hand.isPending ? t('work.review.handing') : t('work.review.hand', { plugin: handable.plugin })}
          </Button>
        )}

        {sendBack && <Button onClick={sendBack}>{t('work.review.sendBack')}</Button>}

        {!treeHere ? null : confirmingDiscard
          ? (
            <>
              <Button
                variant="danger"
                disabled={discard.isPending}
                onClick={() => act(
                  discard.mutateAsync({ id: session, force: true }),
                  () => setConfirmingDiscard(false),
                )}
              >
                {t('work.review.discardMeanIt')}
              </Button>
              <Button variant="ghost" onClick={() => setConfirmingDiscard(false)}>
                {t('common.cancel')}
              </Button>
            </>
          )
          : (
            <Button
              variant="ghost"
              disabled={discard.isPending}
              // Unforced. A tree with nothing to lose is simply removed; one holding uncommitted
              // work or unmerged commits comes back REFUSED with the sentence naming what would go —
              // and that refusal is what arms the second press.
              onClick={() => act(
                discard.mutateAsync({ id: session }),
                undefined,
                () => setConfirmingDiscard(true),
              )}
            >
              {t('work.review.discard')}
            </Button>
          )}
      </div>
    </footer>
  );

  // Why there is no diff, worded from its code with what the person can do next (REVIEW4) — and the acts beneath it
  // where a tree is still here, because "I cannot read the range" and "there is nothing to discard" are different.
  if (diff.error) {
    return (
      <section className="flex min-h-0 flex-col">
        {head}
        <div className="min-h-0 flex-1">
          <ReviewFailed
            error={diff.error}
            machine={known.machine}
            onRetry={() => void diff.refetch()}
            retrying={diff.isFetching}
          />
        </div>
        {acts}
      </section>
    );
  }

  // Where the work landed (REVIEW2), above whatever is read: the landed branch's changes, or the tree's.
  const note = landed ? <LandedNote landed={landed} source={diff.data?.source ?? 'tree'} /> : null;

  // The answer below the frame: held at reduced opacity while a newer read is on its way, so nothing jumps (D41).
  const body = (content: ReactNode) => (
    <div
      aria-busy={refreshing ? true : undefined}
      className={cn('flex min-h-0 flex-1 flex-col', refreshing && 'opacity-60')}
    >
      {content}
    </div>
  );

  // A landed branch with nothing to read — gone since, rebased, no checkout here — is said in the note alone:
  // "nothing landed" would be false, since it did.
  if (landed && diff.data?.source === 'branch' && files.length === 0) {
    return (
      <section className="flex min-h-0 flex-col">
        {head}
        {body(<div className="min-h-0 flex-1">{note}</div>)}
        {acts}
      </section>
    );
  }

  // The acts are about the TREE, not the diff — so a session that landed NOTHING still gets them.
  // Its tree is real, it is holding a slot, and discarding it is exactly what a person wants next.
  if (files.length === 0) {
    return (
      <section className="flex min-h-0 flex-col">
        {head}
        {body(
          <>
            {note}
            <div className="min-h-0 flex-1">
              <EmptyState
                icon="check"
                headline={t('work.review.empty.headline')}
                body={t('work.review.empty.body')}
              />
            </div>
          </>,
        )}
        {acts}
      </section>
    );
  }

  return (
    <section className="flex min-h-0 flex-col">
      {head}
      {body(
        <>
          {note}
          <ReviewFiles
            files={files}
            open={open}
            viewed={viewed}
            layout={layout}
            onLayout={chooseLayout}
            onToggle={(path) => setOpen((was) => ({ ...was, [path]: !was[path] }))}
            onViewed={(path, next) => setViewed((was) => ({ ...was, [path]: next }))}
            onPreview={onPreview}
            truncated={diff.data?.truncated}
          />
        </>,
      )}
      {acts}
    </section>
  );
}
