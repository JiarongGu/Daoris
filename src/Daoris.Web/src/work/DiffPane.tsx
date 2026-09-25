import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../format';
import { useDiscardSessionTree, useMergeSessionTree, useSessionDiff } from '../shell';
import { Button, EmptyState, SkeletonRows } from '../ui';
import { DiffFileRow } from './DiffFileRow';

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
 * **The verbs are Daoris's, never an editor's**: *accept* — merge the session's tree into the
 * canonical line — *discard the tree*, and *send it back as a quest*. Never keep/reject per hunk: the
 * session already committed, and reaching in to fix what you are reviewing is what D32 forbids.
 * Sending it back is the one move Daoris has that an editor does not, and it is a door into the
 * platform's own composer rather than a second publish path.
 *
 * **Discard asks twice, and the second sentence is the host's.** The first press is unforced, which
 * is what makes the tree layer refuse and name what would be lost; only then is the destructive press
 * offered. Destroying work is never a side effect of tidying (D51 rule 7).
 *
 * **A refusal renders verbatim** — "no tree here", "no range recorded" and "the checkout is not
 * clean" are different facts with different next moves, and only the host knows which.
 *
 * Desktop-only, structurally: the hook is gated on the bridge, so in a browser this never asks.
 */
export function DiffPane({ session, hasTree = false, onSendBack }: {
  session: string | null;
  /**
   * Whether this session holds a working tree ON THIS MACHINE (D51) — which is what the acts act on.
   * The record carries it and only a loopback caller is told, so the page can ask this without the
   * driver: a mirrored record from another machine has no tree here, and offering to merge or
   * discard one would be offering something that can only ever refuse.
   */
  hasTree?: boolean;
  /** The door into the quest composer. Absent where there is nowhere to send it. */
  onSendBack?: () => void;
}) {
  const { t } = useTranslation();
  const diff = useSessionDiff(session);
  const merge = useMergeSessionTree();
  const discard = useDiscardSessionTree();

  // Per-reader, per-session, and never written down: which files this person has opened and which
  // they have ticked off. Keyed by path, reset by attending a different session.
  const [open, setOpen] = useState<Record<string, boolean>>({});
  const [viewed, setViewed] = useState<Record<string, boolean>>({});
  const [shown, setShown] = useState<string | null>(null);
  // Whatever the tree layer last said, done or refused. Its sentence is the contract.
  const [said, setSaid] = useState<string | null>(null);
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
  }

  // 🔴 Which session an answer is FOR. A refusal that landed after the person moved on armed the forced
  // press against the session attended now — a tree nobody had looked at (REV3). The reset above
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

  if (!session) {
    return (
      <EmptyState
        icon="diff"
        headline={t('work.review.none.headline')}
        body={t('work.review.none.body')}
      />
    );
  }

  if (diff.isPending) return <div className="p-3"><SkeletonRows rows={4} /></div>;

  const files = diff.data?.files ?? [];
  const done = files.filter((file) => viewed[file.path]).length;

  // Gated on the TREE, not on the diff: a session whose range git cannot read may still hold a tree
  // worth discarding, and one whose record travelled here holds none at all.
  const acts = !hasTree ? null : (
    <footer className="grid shrink-0 gap-2 border-t border-line px-3 py-2">
      {said && <p className="m-0 text-small text-ink-soft">{said}</p>}

      <div className="flex flex-wrap items-center gap-2">
        <Button
          variant="primary"
          disabled={merge.isPending}
          onClick={() => act(merge.mutateAsync(session))}
        >
          {merge.isPending ? t('work.review.accepting') : t('work.review.accept')}
        </Button>

        {onSendBack && <Button onClick={onSendBack}>{t('work.review.sendBack')}</Button>}

        {confirmingDiscard
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

  // The host's own sentence about why there is no diff — and the acts beneath it where a tree is
  // still here, because "I cannot read the range" and "there is nothing to discard" are different.
  if (diff.error) {
    return (
      <section className="flex min-h-0 flex-col">
        <p className="m-0 min-h-0 flex-1 px-3 py-4 text-small text-ink-soft">{sentence(diff.error)}</p>
        {acts}
      </section>
    );
  }

  // The acts are about the TREE, not the diff — so a session that landed NOTHING still gets them.
  // Its tree is real, it is holding a slot, and discarding it is exactly what a person wants next.
  if (files.length === 0) {
    return (
      <section className="flex min-h-0 flex-col">
        <div className="min-h-0 flex-1">
          <EmptyState
            icon="check"
            headline={t('work.review.empty.headline')}
            body={t('work.review.empty.body')}
          />
        </div>
        {acts}
      </section>
    );
  }

  return (
    <section className="flex min-h-0 flex-col">
      <header className="flex shrink-0 items-baseline gap-2 border-b border-line px-3 py-1.5">
        <span className="text-meta uppercase tracking-[0.06em] text-ink-faint">
          {t('work.review.files', { count: files.length })}
        </span>
        {done > 0 && (
          <span className="text-meta tabular-nums text-ink-faint">
            {t('work.review.progress', { done, total: files.length })}
          </span>
        )}
        {/* The range, stated: a review that does not say what it is measured from is an opinion. */}
        <span className="ml-auto truncate font-mono text-meta text-ink-faint">
          {t('work.review.since', { base: diff.data!.base.slice(0, 8) })}
        </span>
      </header>

      <ul className="m-0 min-h-0 flex-1 list-none overflow-y-auto p-0">
        {files.map((file) => (
          <DiffFileRow
            key={file.path}
            file={file}
            open={open[file.path] ?? false}
            viewed={viewed[file.path] ?? false}
            onToggle={() => setOpen((was) => ({ ...was, [file.path]: !was[file.path] }))}
            onViewed={(next) => setViewed((was) => ({ ...was, [file.path]: next }))}
          />
        ))}
      </ul>

      {/* The bound is the host's sentence, shown rather than summarised — it names where the rest is. */}
      {diff.data?.truncated && (
        <p className="m-0 shrink-0 border-t border-line px-3 py-2 text-meta text-ink-faint">
          {diff.data.truncated}
        </p>
      )}

      {acts}
    </section>
  );
}
