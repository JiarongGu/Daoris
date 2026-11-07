import type * as React from 'react';
import { type Quest } from '../../data/api';
import { type WorkDoor, type WorkPlan, type WorkTarget, type Line } from '../../work/pausing/pausing';
import { ClearAsk } from '../../work/history/ClearAsk';
import { type HistoryDoor, type HistoryTarget } from '../../work/history/history';
import { type Answered, InlineConfirm } from '../../work/InlineConfirm';
import { AbandonAsk, PauseAsk } from '../../work/pausing/WorkAsks';
import { type TFunction } from 'i18next';
import { type QuestRequirementResponse } from '../../data/generated/service';

export function QuestAsks({ asking, work, pauseLines, target, t, waiting, setAsking, listed, setListed, history, clearTarget, deleting, deletable, busy, onDelete, moving, requirements, lostHere, quest, onRespond, doneNote, setDoneNote, reopenable, reopen, reopenNote, setReopenNote, declining, reason, setReason }: {
  asking: "done" | "pause" | "decline" | "abandon" | "delete" | "clear" | "clearFailed" | "reopen" | null;
  work: WorkDoor | undefined;
  pauseLines: Line[] | null;
  target: WorkTarget;
  t: TFunction<"translation", undefined>;
  waiting: boolean;
  setAsking: React.Dispatch<React.SetStateAction<"done" | "pause" | "decline" | "abandon" | "delete" | "clear" | "clearFailed" | "reopen" | null>>;
  listed: WorkPlan | null;
  setListed: React.Dispatch<React.SetStateAction<WorkPlan | null>>;
  history: HistoryDoor | undefined;
  clearTarget: (scope: "clear" | "clearFailed") => HistoryTarget;
  deleting: boolean;
  deletable: boolean;
  busy: boolean;
  onDelete: ((answered: Answered) => void) | undefined;
  moving: boolean;
  requirements: QuestRequirementResponse[];
  lostHere: boolean;
  quest: Quest;
  onRespond: (action: "take" | "done" | "decline", reason?: string | null | undefined, answered?: Answered | undefined) => void;
  doneNote: string;
  setDoneNote: React.Dispatch<React.SetStateAction<string>>;
  reopenable: boolean;
  reopen: { step: string; onReopen: (note: string | null, answered: Answered) => void; } | undefined;
  reopenNote: string;
  setReopenNote: React.Dispatch<React.SetStateAction<string>>;
  declining: boolean;
  reason: string;
  setReason: React.Dispatch<React.SetStateAction<string>>;
}) {
  return (
    <>
{asking === 'pause' && work && pauseLines && (
        <PauseAsk
          className="mb-4"
          target={target}
          lines={pauseLines}
          meanIt={t('quests.detail.pauseMeanIt')}
          busy={waiting}
          onPause={(answered) => work.onPause(answered)}
          onClose={() => setAsking(null)}
        />
      )}

      {asking === 'abandon' && work && listed && (
        <AbandonAsk
          target={target}
          plan={listed}
          meanIt={t('quests.detail.abandonMeanIt')}
          placeholder={t('quests.detail.abandonWhy')}
          busy={waiting}
          onAbandon={(why, answered) => work.onAbandon(why, listed.abandon.pieces, answered)}
          onClose={() => { setAsking(null); setListed(null); }}
        />
      )}

      {history && (asking === 'clear' || asking === 'clearFailed') && (asking === 'clear' ? history.plan : history.failed) && (
        /* 🔴 A clear removes what this machine kept, which nothing gives back: the first press lists what goes and
           what stays, held from here, and the second sends exactly that (§5). */
        <ClearAsk
          key={asking}
          className="mb-4"
          target={clearTarget(asking)}
          plan={(asking === 'clear' ? history.plan : history.failed)!}
          meanIt={asking === 'clear'
            ? t('quests.detail.clearMeanIt')
            : (list) => t('quests.detail.clearFailedMeanIt', { count: list.sessions })}
          busy={waiting}
          onClear={(units, answered) => history.onClear(clearTarget(asking), units, answered)}
          onClose={() => setAsking(null)}
        />
      )}

      {deleting && deletable && (
        /* 🔴 A delete removes the record, which nothing gives back — so the first press only asks, the
           way removing an account does, and the second is the one that deletes. It stays open until the service
           answers, and says a refusal inside itself (UXFIX2). */
        <InlineConfirm
          className="mb-4"
          label={t('quests.detail.deleteTitle')}
          says={t('quests.detail.deleteConfirm')}
          meanIt={t('quests.detail.deleteMeanIt')}
          busy={busy}
          onConfirm={onDelete!}
          onClose={() => setAsking((was) => (was === 'delete' ? null : was))}
        />
      )}

      {asking === 'done' && moving && (
        /* The person's done: it asks once, since a done does not move again, with their note, which the
           record keeps after the sentence saying the done was theirs. Open until the service answers, a refusal said inside
           it (UXFIX2). */
        <InlineConfirm
          className="mb-4"
          label={t('quests.detail.doneTitle')}
          says={[
            ...(requirements.length > 0 ? [t('quests.detail.doneRequirements')] : []),
            ...(lostHere ? [t('quests.conflict.doneChoices')] : []),
          ].reduce((first, second) => t('quests.requirements.join', { first, second }), t('quests.detail.doneConfirm', { id: quest.id }))}
          meanIt={t('quests.detail.doneMeanIt')}
          busy={busy}
          onConfirm={(answered) => onRespond('done', doneNote.trim() || null, answered)}
          onClose={() => {
            setAsking((was) => (was === 'done' ? null : was));
            setDoneNote('');
          }}
        >
          <input
            aria-label={t('quests.detail.doneNote')}
            placeholder={t('quests.detail.doneNote')}
            value={doneNote}
            onChange={(e) => setDoneNote(e.target.value)}
            className="min-h-[1.9rem] min-w-0 basis-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
        </InlineConfirm>
      )}

      {asking === 'reopen' && reopenable && (
        /* The person's reopen of a sealed submission (FABRIC-CLOSE1c3, D156): it asks once, naming the step it holds, and sends
           the submission this page showed, which the door refuses once another is current. Open until the service answers, a
           refusal said inside it (UXFIX2). */
        <InlineConfirm
          className="mb-4"
          label={t('quests.detail.reopenTitle')}
          says={t('quests.detail.reopenConfirm', { id: quest.id, step: reopen!.step })}
          meanIt={t('quests.detail.reopenMeanIt')}
          busy={busy}
          onConfirm={(answered) => reopen!.onReopen(reopenNote.trim() || null, answered)}
          onClose={() => {
            setAsking((was) => (was === 'reopen' ? null : was));
            setReopenNote('');
          }}
        >
          <input
            aria-label={t('quests.detail.reopenNote')}
            placeholder={t('quests.detail.reopenNote')}
            value={reopenNote}
            onChange={(e) => setReopenNote(e.target.value)}
            className="min-h-[1.9rem] min-w-0 basis-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
        </InlineConfirm>
      )}

      {declining && moving && (
        /* Declining without a reason is refused by the service; the ask does not offer the mistake. It stays open until
           the service answers, a refusal said inside it (UXFIX2b2b). */
        <InlineConfirm
          className="mb-4"
          label={t('quests.detail.declineTitle')}
          says={t('quests.detail.declineSays', { id: quest.id })}
          meanIt={t('quests.detail.declineConfirm')}
          busy={busy}
          ready={reason.trim() !== ''}
          onConfirm={(answered) => onRespond('decline', reason, answered)}
          onClose={() => {
            setAsking((was) => (was === 'decline' ? null : was));
            setReason('');
          }}
        >
          <input
            aria-label={t('quests.detail.declinePlaceholder')}
            placeholder={t('quests.detail.declinePlaceholder')}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            className="min-h-[1.9rem] min-w-0 basis-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
        </InlineConfirm>
      )}
    </>
  );
}
