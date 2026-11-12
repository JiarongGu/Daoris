import { type ReactNode, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Session } from '../../data/api';
import { sentence } from '../../locales/format';
import { cn } from '../../lib/cn';
import { store, stored } from '../../lib/stored';
import { useDiscardSessionTree, useReadingSince, useSessionDiff, useUnderWay } from '../../bridge/shell';
import { Button, EmptyState, Inline, SESSION_ACTIVE } from '../../design/ui';
import { reviewBranch, reviewKnown } from './diff';
import type { DiscardOffer } from '../groups';
import { HeldDiscardAsk, HeldDiscardButton, heldDiscardable } from './HeldDiscard';
import { type Answered, InlineConfirm } from '../InlineConfirm';
import { LandedNote } from './LandedNote';
import type { DiffLayout } from './PatchView';
import { ReviewFailed, ReviewFiles, ReviewHead, ReviewReading } from './ReviewFrame';
import { UnattributedNote } from './UnattributedNote';

/** How a reader likes the changes laid out — a per-viewer convenience, like the frame's widths. */
const LAYOUT = 'daoris.reviewLayout';

/**
 * A session's own changes: what it did in its tree, and what the person does about the tree.
 *
 * @remarks
 * **Reading the work and deciding on it are different places.** Accepting, asking for changes and stopping a run are the
 * review page's (`review/<run>`, which reads the run's record); this pane only shows a session's changes where no run
 * governs them, and holds the tree's own discard.
 *
 * **A multibuffer, not a tree beside a pane**: every changed file is one row in one
 * scroll and opens in place. That is the shape that makes a forty-file review finishable, and the
 * per-file *viewed* mark is the other half of it. The diff route is read-only by construction.
 *
 * **Discard asks twice, and the second sentence is the host's.** The first press is unforced, which
 * is what makes the tree layer refuse and name what would be lost; only then is the destructive press
 * offered. Destroying work is never a side effect of tidying. An act's refusal renders verbatim: "the checkout is not
 * clean" and "the tree holds commits nobody merged" are different facts with different next moves, and only the host
 * knows which. The review's own refusal ("no tree here", "no range recorded") is worded by its code.
 *
 * **A landed session reads as landed**. Where this machine's landing record holds the session's landing, the pane says
 * where the work landed, above what it reads; once a tidy took the tree, what it reads is the landed branch, in the
 * repository's own checkout. Discarding is offered only where a tree is still here.
 *
 * **Work the line holds by content is discarded as the head offers it** (`discards`, the reader's fact the session's head
 * reads): the head's *Discard branch…* asks once with the ref the commits stay at, pressed through the head's own press. The
 * pane's two-step Discard stands only where that one would not go unforced, since the tree holds what no commit does. A tree
 * with nothing beyond its line leads with *Discard tree*. The head names the branch the tree stands on once the answer says
 * it, over the record's `daoris/` guess, and none where the tree is detached.
 *
 * **It says what it is doing while git reads**. The frame stands at once with what the record holds —
 * the title, the repository, its own branch, the base and the commits it reported — and the files and the patch as
 * a skeleton under words that say what is read and, after a moment, for how long. A refusal is worded from its code
 * with the person's next move. An ended session's answer is kept, so a second open is the first's; a live one's is
 * read again behind the last answer, held dimmed. Leaving cancels the wait; what git answers after is kept.
 *
 * Desktop-only, structurally: the hook is gated on the bridge, so in a browser this never asks.
 */
export function DiffPane({
  session, record, title = null, hasTree = false, onPreview, discards = null, onDiscardTree,
}: {
  session: string | null;
  /**
   * What its tree offers where the line holds its commits by content, as the driver's reader says it for the
   * session's head: shown in the discard's place. Absent where it offers none, or nothing has answered.
   */
  discards?: DiscardOffer | null;
  /** That discard's press, the head's own (the review's Discard, unforced), told back to its ask. Absent where nothing presses it. */
  onDiscardTree?: (answered: Answered) => void;
  /**
   * The attended session's record: what the review's frame says before git answers, and whether its answer
   * is final — an ended session's range does not move. Absent, the frame says what the answer says.
   */
  record?: Session | null;
  /** The session's title for the frame, or null where something above the review already names it. */
  title?: string | null;
  /**
   * Whether this session holds a working tree OF ITS OWN on this machine — which is what the
   * act acts on. The record carries it and only a loopback caller is told, so the page can ask this
   * without the driver: a mirrored record from another machine has no tree here, and a session in
   * the repository's own checkout has no tree of its own.
   */
  hasTree?: boolean;
  /** Opens one file's preview in the side bar, by its path in the review. */
  onPreview?: (path: string) => void;
}) {
  const { t } = useTranslation();
  // The record is the attended one only while it is this session's: a frame never says another session's facts.
  const own = record && record.id === session ? record : null;
  const diff = useSessionDiff(session, own ? { updated: own.updated, live: SESSION_ACTIVE.has(own.state) } : null);
  // A first read, or a read again after a refusal: the review is reading, not failed.
  const reading = diff.isPending || (diff.isError && diff.isFetching);
  // A newer read behind an answer already shown: that answer is held, dimmed.
  const refreshing = diff.isSuccess && diff.isFetching;
  const started = useReadingSince(session);
  const since = diff.isFetching ? started : null;
  // Where this session's work landed, where this machine's landing record holds it.
  const landed = diff.data?.landed ?? null;
  const asLanded = Boolean(landed?.asLanded);
  // A tree the host could not read, or says is gone: nothing to discard there.
  const treeGone = diff.data?.source === 'branch'
    || (diff.error as { code?: unknown } | null)?.code === 'SESSION_TREE_GONE';
  const treeHere = hasTree && !reading && !treeGone;
  // Work the line holds by content: its discard stands where the plain one would.
  const held = treeHere && !asLanded && discards ? discards : null;
  const heldPress = held && heldDiscardable(held, onDiscardTree) ? onDiscardTree! : null;
  // A tree holding nothing beyond its line, committed or not, has nothing to keep: discarding it is the next move.
  const answered = diff.data;
  const nothingBeyond = treeHere && !held && answered?.behind?.ahead === 0 && answered.files.length === 0
    && !answered.uncommitted?.files.length
    ? answered.behind
    : null;
  const discard = useDiscardSessionTree();
  const discarding = useUnderWay('DISCARD_SESSION_TREE', session);

  // Per-reader, per-session, and never written down: which files this person has opened and which
  // they have ticked off. Keyed by path, reset by attending a different session.
  const [open, setOpen] = useState<Record<string, boolean>>({});
  const [viewed, setViewed] = useState<Record<string, boolean>>({});
  // One column or the old side beside the new: the reader's, for every review.
  const [layout, setLayout] = useState<DiffLayout>(() => (stored(LAYOUT) === 'split' ? 'split' : 'unified'));
  const chooseLayout = (next: DiffLayout) => {
    setLayout(next);
    store(LAYOUT, next);
  };
  const [shown, setShown] = useState<string | null>(null);
  // Whatever the tree layer last said, done or refused. Its sentence is the contract.
  const [said, setSaid] = useState<string | null>(null);
  // The second press. It exists only because the first one produced the sentence above.
  const [confirmingDiscard, setConfirmingDiscard] = useState(false);
  // The held discard's ask, open under the acts until it answers.
  const [askingHeld, setAskingHeld] = useState(false);

  if (shown !== session) {
    setShown(session);
    setOpen({});
    setViewed({});
    // A pending confirm belongs to the session it is asked about. Carrying it across would arm a
    // destructive second press against work the person never looked at.
    setConfirmingDiscard(false);
    setAskingHeld(false);
    setSaid(null);
  }

  // Which session an answer is FOR. A refusal that lands after the person moves on must not arm the forced
  // press against the session attended now — a tree nobody has looked at. The reset above
  // runs at render; an answer arrives whenever git finishes.
  const attended = useRef(session);
  attended.current = session;

  const act = (
    run: Promise<{ done: boolean; message: string }>,
    onDone?: () => void,
    onRefused?: () => void,
  ) => {
    const askedFor = session;
    const current = () => attended.current === askedFor;
    setSaid(null);
    void run.then(
      (result) => {
        if (!current()) return;
        setSaid(result.message);
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

  // The forced discard's second press, told back to its ask: the driver's refusal and a thrown one are said inside
  // the ask, which stays; once the tree is gone, the foot says so and the ask closes. An answer for a session no longer
  // attended says nothing, as `act` does.
  const forceDiscard = (confirmed: Answered) => {
    const askedFor = session;
    if (!askedFor) return;
    discard.mutateAsync({ id: askedFor, force: true }).then(
      (result) => {
        if (attended.current !== askedFor) return;
        if (!result.done) {
          confirmed.refused(result.message);
          return;
        }
        setSaid(result.message);
        confirmed.done();
      },
      (error: unknown) => {
        if (attended.current === askedFor) confirmed.refused(sentence(error));
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

  // What the frame says: the record's facts from the first frame, and the range the answer measured from
  // once it has one, since bringing a tree up to date moves it.
  const known = own
    ? reviewKnown(own, { title, ownTree: hasTree })
    : { title, repository: null, branch: null, base: null, commits: null, machine: null };
  // The branch the tree stands on once the answer names it, over the record's guess; none where it is detached.
  const named = reviewBranch(known, diff.data);
  const head = (
    <ReviewHead
      {...known}
      branch={named.branch}
      detached={named.detached}
      base={diff.data?.base || known.base}
      refreshing={refreshing ? since : null}
      behind={diff.data?.behind}
    />
  );

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
  const uncommitted = diff.data?.uncommitted?.files ?? [];

  // One discard where the held one is offered: unforced, the pane's own would do the same without naming the ref.
  const discardTree = !treeHere || heldPress || confirmingDiscard
    ? null
    : (
      <Button
        // The next move where nothing is beyond the line, so it leads.
        variant={nothingBeyond ? 'primary' : 'ghost'}
        disabled={discarding}
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
    );

  // Gated on the TREE, not on the diff: a session whose range git cannot read may still hold a tree
  // worth discarding, and one whose record travelled here holds none at all.
  const acts = !treeHere ? null : (
    <footer className="grid shrink-0 gap-2 border-t border-line px-3 py-2">
      {said && !confirmingDiscard && <p className="m-0 text-small text-ink-soft">{said}</p>}
      {/* Where the discard would be, work the line holds by content: the driver's sentence, as the head says it. */}
      {!said && held && (
        <p className="m-0 text-small text-ink-soft"><Inline text={held.says} /></p>
      )}

      <div className="flex flex-wrap items-center gap-2">
        {heldPress && !askingHeld && <HeldDiscardButton onAsk={() => setAskingHeld(true)} />}
        {discardTree}
      </div>
      {!heldPress && confirmingDiscard && (
        /* The unforced press's refusal named what would go and armed this (above): it is the ask's own sentence, so the foot
           does not say it twice. The forced press stays open until the driver answers, and a refusal is said inside the ask
           (UXFIX2b2b), never in the foot. */
        <InlineConfirm
          label={t('work.review.discardTitle')}
          says={<p className="m-0 text-small text-ink-soft">{said}</p>}
          meanIt={t('work.review.discardMeanIt')}
          busy={discarding}
          onConfirm={forceDiscard}
          onClose={() => setConfirmingDiscard(false)}
        />
      )}
      {heldPress && askingHeld && held?.keptAt && (
        <HeldDiscardAsk branch={held.branch} keptAt={held.keptAt} onDiscard={heldPress} onClose={() => setAskingHeld(false)} />
      )}
    </footer>
  );

  // Why there is no diff, worded from its code with what the person can do next — and the acts beneath it
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

  // Where the work landed, above whatever is read: the landed branch's changes, or the tree's.
  const place = diff.data?.source === 'place';
  const note = (
    <>
      {!place && landed ? <LandedNote landed={landed} source={diff.data?.source === 'branch' ? 'branch' : 'tree'} /> : null}
      <UnattributedNote paths={diff.data?.unattributed} />
    </>
  );

  // The answer below the frame: held at reduced opacity while a newer read is on its way, so nothing jumps.
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

  // The acts are about the TREE, not the diff — so a session that changed NOTHING still gets them: its tree is real, it
  // is holding a slot, and discarding it is exactly what a person wants next.
  if (files.length === 0 && uncommitted.length === 0) {
    // A tree whose HEAD the line holds whole: its work merged and the line checked out, or nothing done yet.
    const even = diff.data?.behind?.ahead === 0 ? diff.data.behind : null;
    return (
      <section className="flex min-h-0 flex-col">
        {head}
        {body(
          <>
            {note}
            <div className="min-h-0 flex-1">
              {even && (
                <EmptyState icon="check" headline={t('work.review.even.headline')} body={t('work.review.even.body', { line: even.line })} />
              )}
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
            onPreview={place ? undefined : onPreview}
            truncated={diff.data?.truncated}
            uncommitted={diff.data?.uncommitted}
            place={place}
          />
        </>,
      )}
      {acts}
    </section>
  );
}
