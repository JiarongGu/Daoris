import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import type { Quest, QuestConflict, SubmissionRef } from '../data/api';
import { ago } from '../locales/format';
import { Button, Icon, SectionTitle } from '../design/ui';
import { PageSection } from '../work/frame/ViewMain';

/**
 * Why *Bring mine forward* is not offered on this machine's lost take (SYNCMERGE1c2, D178 point 3), in the order the service refuses
 * it: the step closed; taken, by this machine's later take or by another machine (named where the host says which); its source held
 * by the reopen.
 */
export type ForwardWait = 'closed' | 'heldHere' | 'heldBy' | 'taken' | 'source';

/** One open conflict as its row reads it. */
export type ConflictRow = {
  conflict: QuestConflict;
  /** This machine's own lost take, whose two choices the row offers: never where the host names no machine (a browser, an older host). */
  mine: boolean;
  /** Its bring-forward lost again (a second loss): when it was made, and whether the reopen, not another machine's take, beat it. */
  lostAgain: { at: string; reopened: boolean } | null;
  /** The conflicts of the fresh takes its lost bring-forwards made, read as part of this row and dropped with it. */
  also: SubmissionRef[];
  /** On this machine's lost take: true where the bring-forward can stand, or why it waits. Null on any other conflict. */
  forward: true | ForwardWait | null;
};

const sameName = (a: SubmissionRef, b: SubmissionRef) => a.machine === b.machine && a.sequence === b.sequence;
const isMine = (conflict: QuestConflict, here: string | null) =>
  here !== null && conflict.machine === here && conflict.attempted === 'Taken';
/** Brought forward offline: the remote has not answered, so it is said as it stands and never as settled (D178 point 8). */
const unconfirmed = (conflict: QuestConflict) => conflict.ended === 'brought-forward' && conflict.forward?.standing === 'unconfirmed';

/** Whether this machine's lost take can be brought forward as the quest stands, or why not, in the service's refusal order. */
function forwardStands(quest: Quest, here: string): true | ForwardWait {
  if (quest.status === 'Done' || quest.status === 'Declined') return 'closed';
  if (quest.status === 'Taken') return quest.holder === here ? 'heldHere' : quest.holder ? 'heldBy' : 'taken';
  if (quest.held === true && quest.hold === 'source-reopened') return 'source';
  return true;
}

/**
 * A quest's conflicts as the page reads them (SYNCMERGE1c2, from what SYNCMERGE1b0 answers): the open ones by row, a second loss folded
 * into the conflict whose bring-forward lost again rather than said as a new conflict; the bring-forwards still unconfirmed, which wait
 * on the remote; and the settled ones, each with how it ended.
 */
export function readConflicts(quest: Quest): { rows: ConflictRow[]; unconfirmed: QuestConflict[]; settled: QuestConflict[] } {
  const here = quest.here ?? null;
  const open = quest.conflicts ?? [];
  const secondLoss = (conflict: QuestConflict) => conflict.forward?.standing === 'lost'
    ? open.find((other) => other !== conflict && sameName(other, conflict.forward!))
    : undefined;
  const folded = new Set(open.map(secondLoss).filter((other) => other !== undefined));
  const rows = open.filter((conflict) => !folded.has(conflict)).map((conflict): ConflictRow => {
    const mine = isMine(conflict, here);
    const second = secondLoss(conflict);
    return {
      conflict,
      mine,
      lostAgain: conflict.forward?.standing === 'lost' ? { at: conflict.forward.at, reopened: second?.lostToTheReopen === true } : null,
      also: second ? [{ machine: second.machine, sequence: second.sequence }] : [],
      forward: mine ? forwardStands(quest, here!) : null,
    };
  });
  const settled = quest.settled ?? [];
  return { rows, unconfirmed: settled.filter(unconfirmed), settled: settled.filter((conflict) => !unconfirmed(conflict)) };
}

/** Whose move lost, and to what: this machine's take, another machine's, either lost to the person's reopen. */
function lineOf(t: TFunction, conflict: QuestConflict, here: string | null): string {
  const mine = isMine(conflict, here);
  if (conflict.lostToTheReopen) return mine ? t('quests.conflict.mineReopened') : t('quests.conflict.theirsReopened', { machine: conflict.machine });
  if (mine) return t('quests.conflict.mine');
  return t('quests.detail.conflictLine', { machine: conflict.machine, attempted: t(`status.${conflict.attempted}`) });
}

/** How a settled conflict ended (D178 point 7): dropped, or brought forward and confirmed or lost again. */
function endedOf(t: TFunction, conflict: QuestConflict): string {
  if (conflict.ended === 'dropped') {
    return conflict.forward?.standing === 'lost' ? t('quests.conflict.ended.droppedFirst') : t('quests.conflict.ended.dropped');
  }
  if (conflict.forward?.standing === 'held') return t('quests.conflict.ended.held');
  if (conflict.forward?.standing === 'lost') return t('quests.conflict.ended.lost');
  return t('quests.conflict.ended.broughtForward');
}

/** A conflict's line: what lost and when, its mark in the warning's hue while it waits on the person and quiet once settled. */
function Line({ conflict, here, settled = false }: { conflict: QuestConflict; here: string | null; settled?: boolean }) {
  const { t } = useTranslation();
  return (
    <span className="inline-flex flex-wrap items-center gap-1.5">
      <Icon name="conflict" size={12} className={settled ? 'text-ink-faint' : 'text-warn'} />
      {lineOf(t, conflict, here)}
      <span className="text-meta text-ink-faint">· {ago(conflict.at)}</span>
    </span>
  );
}

/**
 * **A quest's conflicts** (D68 §5; SYNCMERGE1c2, D178 points 2, 7 and 8): moves that reached the remote second, kept for a person and
 * never merged, so above the body, as what the quest waits on. The note is that session's own words.
 *
 * @remarks
 * A molecule: every state is its quest's, and both presses go out.
 * - **This machine's lost take offers its two choices**: *Drop mine*, the dismissal, which puts the conflict away on every machine and
 *   moves nothing else; and *Bring mine forward*, a fresh take its kept session goes on from, offered only where it can stand. Where it
 *   cannot, the row says why: the step closed, held by another machine or by this one's later take, or its source reopened.
 * - **Another machine's conflict offers what it did before**, its dismissal, as does every conflict where the host names no machine.
 * - **A second loss is the bring-forward lost again**, said on the conflict it was to settle; its fresh take's own conflict is read as
 *   part of that row, and dropped with it.
 * - **A bring-forward the remote has not answered is unconfirmed**, said here as it stands and never as settled (D178 point 8).
 */
export function QuestConflicts({
  quest, busy = false, onDismiss, onForward, className,
}: {
  quest: Quest;
  /** A dismissal or a bring-forward is on its way: both presses wait. */
  busy?: boolean;
  /** Dismiss the conflict by its name, then the fresh takes' conflicts folded into its row. */
  onDismiss: (machine: string, sequence: number, also?: SubmissionRef[]) => void;
  /** Bring this machine's lost take forward by the conflict's name. Absent where there is no door to press it through. */
  onForward?: (machine: string, sequence: number) => void;
  className?: string;
}) {
  const { t } = useTranslation();
  const here = quest.here ?? null;
  const { rows, unconfirmed: waiting } = readConflicts(quest);
  if (rows.length === 0 && waiting.length === 0) return null;
  const choices = rows.some((row) => row.mine);
  return (
    <section
      aria-label={t('quests.detail.conflicts')}
      className={`${className ?? ''} rounded-card border border-line border-l-[3px] border-l-st-open bg-raised px-3 py-2.5`}
    >
      <SectionTitle>{t('quests.detail.conflicts')}</SectionTitle>
      <p className="m-0 mb-2 text-small text-ink-soft">{t('quests.detail.conflictsHint')}</p>
      <ul className="m-0 grid list-none gap-1.5 p-0">
        {rows.map(({ conflict, mine, lostAgain, also, forward }) => (
          <li key={`${conflict.machine}-${conflict.sequence}`} className="flex flex-wrap items-start justify-between gap-x-3 gap-y-1.5 text-body">
            <span className="grid min-w-0 gap-0.5">
              <Line conflict={conflict} here={here} />
              {conflict.note && <span className="text-small text-ink-soft">{conflict.note}</span>}
              {lostAgain && (
                <span className="text-small text-ink-soft">
                  {t(lostAgain.reopened ? 'quests.conflict.lostAgainReopened' : 'quests.conflict.lostAgain')}
                  <span className="text-meta text-ink-faint"> · {ago(lostAgain.at)}</span>
                </span>
              )}
              {forward !== null && forward !== true && (
                <span className="text-small text-ink-soft">{t(`quests.conflict.why.${forward}`, { machine: quest.holder ?? '' })}</span>
              )}
            </span>
            <span className="flex shrink-0 flex-wrap justify-end gap-1.5">
              {forward === true && onForward && (
                <Button disabled={busy} onClick={() => onForward(conflict.machine, conflict.sequence)}>
                  {t('quests.conflict.forward')}
                </Button>
              )}
              {/* The dismissal travels, so every machine stops showing it (SYNC6c); on this machine's take it is *Drop mine*. */}
              <Button
                variant="ghost"
                disabled={busy}
                onClick={() => (also.length > 0
                  ? onDismiss(conflict.machine, conflict.sequence, also)
                  : onDismiss(conflict.machine, conflict.sequence))}
              >
                {t(mine ? 'quests.conflict.drop' : 'quests.detail.dismiss')}
              </Button>
            </span>
          </li>
        ))}
        {waiting.map((conflict) => (
          <li key={`${conflict.machine}-${conflict.sequence}`} className="grid min-w-0 gap-0.5 text-body">
            <Line conflict={conflict} here={here} />
            <span className="text-small text-ink-soft">
              {t('quests.conflict.unconfirmed')}
              <span className="text-meta text-ink-faint"> · {ago(conflict.forward!.at)}</span>
            </span>
          </li>
        ))}
      </ul>
      {choices && <p className="m-0 mt-2 text-small text-ink-soft">{t('quests.conflict.choices')}</p>}
    </section>
  );
}

/** **How its conflicts ended** (SYNCMERGE1c2, D178 point 7): each settled one, dropped or brought forward, and nothing to press. */
export function SettledConflicts({ quest }: { quest: Quest }) {
  const { t } = useTranslation();
  const { settled } = readConflicts(quest);
  if (settled.length === 0) return null;
  return (
    <div className="mt-4">
      <PageSection title={t('quests.detail.settled')}>
        <ul className="m-0 grid list-none gap-1 p-0">
          {settled.map((conflict) => (
            <li key={`${conflict.machine}-${conflict.sequence}`} className="grid min-w-0 gap-0.5 text-body">
              <Line conflict={conflict} here={quest.here ?? null} settled />
              <span className="text-small text-ink-soft">{endedOf(t, conflict)}</span>
            </li>
          ))}
        </ul>
      </PageSection>
    </div>
  );
}
