import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { NotePart, Quest } from '../api';
import { Button, Segmented, WaitingCard } from '../ui';
import { Note } from './Note';

/** What the ledger lets a person do from `awaiting-person` — and nothing this surface invented. */
export type Resolution = 'completed' | 'declined' | 'stopped';

/**
 * What a finish does with the quest its session holds (QUESTCLOSE1, D126's note): marks it done as the person's, with their
 * words or none. Absent leaves it as it is, the default.
 */
export type QuestClose = { as: 'done'; note: string | null };

/**
 * A session parked at a checkpoint, and the person's answer to it (design §4).
 *
 * @remarks
 * **`AwaitingPerson` has meant "only the person can clear this" since D46 and had no surface at
 * all.** This is it: the analysis at the top, verbatim — `autonomous-development` asks a park for
 * options, a recommendation and a reason, and none of those survive rewording — and beneath it
 * exactly the three moves the ledger already allows.
 *
 * **Three, not four, and no new states.** The ledger also allows `awaiting-person → working`; that
 * one is the driver observing a session that carried on, which a person causes by *answering* it
 * in the composer. A button for it would be a second way to say the same thing, and the surface
 * says so instead. **The third, `stopped`, is the page header's** (SESSUX1d, D126 §3.3): a session's stop has one
 * owner, so this card keeps *Answer and carry on…*, *Finish* and *Decline…*.
 *
 * **Unless nothing is left to take a message (STANDDOWN2).** A driven session that took its quest and
 * ended its turn to ask the person has no process: its answer is a move here, handed `onAnswer`. The
 * record stays parked with the words (ANSWER1b), and the same session goes on with them at the driver's
 * next look (D131); until then the head shows `AnsweredPark` in this card's place (ANSWER1c). Then the
 * composer hint would send the person nowhere, and it is not shown.
 *
 * **Declining asks for its reason in place**, the same two-step a quest's page uses and for the
 * same reason: the note is the only part whoever reads the record later can act on, and a decline
 * that slipped out on one click would routinely carry nothing.
 *
 * **A finish asks what becomes of its quest, in the same act** (QUESTCLOSE1, D126's note): where the session holds a
 * quest still open or taken, *Finish…* opens the choice in place, *leave it as it is* by default or *mark it done as
 * yours* with the person's note. On the install two sessions finished at a checkpoint left their quests taken with
 * nothing to close them, since nothing carries a finished session's quest on. With no such quest, *Finish* is one press.
 *
 * A molecule: it is handed the note and reports a move, so a parked session with a three-paragraph
 * analysis and one with none are both reachable by passing them.
 */
export function AwaitingPerson({ note, parts, quest = null, pending = false, onResolve, onAnswer }: {
  /** The session's note: its English, shown as kept where it has no parts. */
  note?: string | null;
  /**
   * Its lines (LANG1a): Daoris's lead-in worded in the reader's language, and the agent's analysis beneath it word for
   * word (LANG1b, `Note`).
   */
  parts?: readonly NotePart[] | null;
  /** The quest its session holds, where it is still open or taken: what a finish may close (QUESTCLOSE1). */
  quest?: Pick<Quest, 'id' | 'status'> | null;
  pending?: boolean;
  /**
   * The ledger's moves this card makes, finish and decline, where this surface can make them (the shell); a finish that
   * closes its quest says how (QUESTCLOSE1).
   */
  onResolve?: (state: Resolution, note: string | null, close?: QuestClose) => void;
  /** The answer to a session with no process left — its quest carried on with the words (STANDDOWN2). */
  onAnswer?: (answer: string | null) => void;
}) {
  const { t } = useTranslation();
  const [declining, setDeclining] = useState(false);
  const [answering, setAnswering] = useState(false);
  const [finishing, setFinishing] = useState(false);
  const [reason, setReason] = useState('');
  const [words, setWords] = useState('');
  // What the finish does with its quest: left as it is unless the person chooses (QUESTCLOSE1).
  const [questTo, setQuestTo] = useState<'leave' | 'done'>('leave');
  const [questNote, setQuestNote] = useState('');
  const closable = quest !== null && (quest.status === 'Open' || quest.status === 'Taken');
  const finish = () => {
    if (!onResolve) return;
    if (questTo === 'done' && closable) onResolve('completed', null, { as: 'done', note: questNote.trim() || null });
    else onResolve('completed', null);
  };

  const field = 'min-h-14 resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink';

  return (
    <WaitingCard title={t('work.head.waiting')}>

      <Note note={note} parts={parts} className="mt-1.5 text-body leading-relaxed" />

      <p className="m-0 mt-2 text-small text-ink-faint">{t('work.awaiting.hint')}</p>

      {answering && onAnswer
        ? (
          <div className="mt-2.5 grid gap-2">
            <label className="grid gap-1 text-small text-ink-faint">
              <span className="sr-only">{t('work.awaiting.answerPlaceholder')}</span>
              <textarea
                autoFocus
                value={words}
                aria-label={t('work.awaiting.answerPlaceholder')}
                onChange={(event) => setWords(event.target.value)}
                placeholder={t('work.awaiting.answerPlaceholder')}
                className={field}
              />
            </label>
            <div className="flex flex-wrap gap-2">
              <Button variant="primary" disabled={pending} onClick={() => onAnswer(words.trim() || null)}>
                {t('work.awaiting.answerConfirm')}
              </Button>
              <Button variant="ghost" onClick={() => setAnswering(false)}>{t('common.cancel')}</Button>
            </div>
          </div>
        )
        : finishing && onResolve && closable
        ? (
          <div className="mt-2.5 grid gap-2">
            <p className="m-0 text-small text-ink-soft">{t('work.awaiting.finishQuest', { quest: quest!.id })}</p>
            <div>
              <Segmented
                label={t('work.awaiting.questChoice')}
                value={questTo}
                options={[
                  { value: 'leave', label: t('work.awaiting.questLeave') },
                  { value: 'done', label: t('work.awaiting.questDone') },
                ]}
                onChange={setQuestTo}
              />
            </div>
            {questTo === 'done' && (
              <label className="grid gap-1 text-small text-ink-faint">
                <span className="sr-only">{t('work.awaiting.questNote')}</span>
                <textarea
                  autoFocus
                  value={questNote}
                  aria-label={t('work.awaiting.questNote')}
                  onChange={(event) => setQuestNote(event.target.value)}
                  placeholder={t('work.awaiting.questNote')}
                  className={field}
                />
              </label>
            )}
            <div className="flex flex-wrap gap-2">
              <Button variant="primary" disabled={pending} onClick={finish}>{t('work.awaiting.finish')}</Button>
              {/* Put down, it forgets the choice: the next finish asks again from leaving the quest as it is. */}
              <Button
                variant="ghost"
                onClick={() => {
                  setFinishing(false);
                  setQuestTo('leave');
                  setQuestNote('');
                }}
              >
                {t('common.cancel')}
              </Button>
            </div>
          </div>
        )
        : declining && onResolve
        ? (
          <div className="mt-2.5 grid gap-2">
            <label className="grid gap-1 text-small text-ink-faint">
              <span className="sr-only">{t('work.awaiting.declinePlaceholder')}</span>
              <textarea
                autoFocus
                value={reason}
                aria-label={t('work.awaiting.declinePlaceholder')}
                onChange={(event) => setReason(event.target.value)}
                placeholder={t('work.awaiting.declinePlaceholder')}
                className={field}
              />
            </label>
            <div className="flex flex-wrap gap-2">
              <Button
                variant="danger"
                disabled={!reason.trim() || pending}
                onClick={() => onResolve('declined', reason.trim())}
              >
                {t('work.awaiting.declineConfirm')}
              </Button>
              <Button variant="ghost" onClick={() => setDeclining(false)}>{t('common.cancel')}</Button>
            </div>
          </div>
        )
        : (
          <div className="mt-2.5 flex flex-wrap gap-2">
            {onAnswer && (
              <Button variant="primary" disabled={pending} onClick={() => setAnswering(true)}>
                {t('work.awaiting.carryOn')}
              </Button>
            )}
            {onResolve && (
              <>
                {/* With a quest still to close, the finish asks what becomes of it first (QUESTCLOSE1); with none, one press. */}
                <Button
                  variant={onAnswer ? 'default' : 'primary'}
                  disabled={pending}
                  onClick={() => (closable ? setFinishing(true) : onResolve('completed', null))}
                >
                  {t(closable ? 'work.awaiting.finishAsk' : 'work.awaiting.finish')}
                </Button>
                <Button disabled={pending} onClick={() => setDeclining(true)}>
                  {t('work.awaiting.decline')}
                </Button>
              </>
            )}
          </div>
        )}

      {!onAnswer && <p className="m-0 mt-2 text-small text-ink-faint">{t('work.awaiting.answer')}</p>}
    </WaitingCard>
  );
}
