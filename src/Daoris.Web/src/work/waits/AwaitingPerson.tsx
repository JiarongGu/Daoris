import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { NotePart, Quest } from '../../data/api';
import { Button, Segmented, WaitingCard } from '../../design/ui';
import { type Answered, InlineConfirm } from '../InlineConfirm';
import { Note } from '../Note';
import type { ParkQuestion } from './parkQuestion';
import { QuestionCard } from './QuestionCard';
import { InConversation } from './WaitAnchor';

/**
 * The answer selected in a park's question: the frame's, one per session, so a row chosen in
 * the head's card or the box below is chosen in both; null for none.
 */
export type AnswerPick = { selected: string | null; onSelect: (label: string | null) => void };

/** What the ledger lets a person do from `awaiting-person`, and nothing more. */
export type Resolution = 'completed' | 'declined' | 'stopped';

/**
 * What a finish does with the quest its session holds: marks it done as the person's, with their
 * words or none. Absent leaves it as it is, the default.
 */
export type QuestClose = { as: 'done'; note: string | null };

/**
 * A session parked at a checkpoint, and the person's answer to it.
 *
 * @remarks
 * **`AwaitingPerson` means "only the person can clear this".** This is its surface: the analysis at the top, verbatim — `autonomous-development` asks a park for
 * options, a recommendation and a reason, and none of those survive rewording — and beneath it
 * exactly the three moves the ledger already allows.
 *
 * **Three, not four, and no new states.** The ledger also allows `awaiting-person → working`; that
 * one is the driver observing a session that carried on, which a person causes by *answering* it
 * in the composer. A button for it would be a second way to say the same thing, and the surface
 * says so instead. **The third, `stopped`, is the page header's**: a session's stop has one
 * owner, so this card keeps *Answer and carry on…*, *Finish* and *Decline…*.
 *
 * **Unless nothing is left to take a message.** A driven session that took its quest and
 * ended its turn to ask the person has no process: its answer is a move here, handed `onAnswer`. The
 * record stays parked with the words, and the same session goes on with them at the driver's
 * next look; until then the head shows `AnsweredPark` in this card's place. Then the
 * composer hint would send the person nowhere, and it is not shown.
 *
 * **🔴 And where the box below answers it too.** Every park the frame can answer is answered here as well: its question's
 * answers rows to select once *Answer and carry on…* is open, else the words under its closing message, through the frame's same door,
 * and the hint names both places. Its one *Send* sends the selected answer with the card's words, naming the question, or the words
 * alone. The selection is the frame's, shared with the box below (the card's own where none is handed), tied to the question's id.
 * A refusal is said here, where it is pressed, the words and the selection kept; the frame refuses an answer of either kind while
 * the question's go-aheads are open.
 *
 * **Declining asks for its reason in place**, the same two-step a quest's page uses and for the
 * same reason: the note is the only part whoever reads the record later can act on, and a decline
 * that slipped out on one click would routinely carry nothing.
 *
 * **A finish asks what becomes of its quest, in the same act**: where the session holds a
 * quest still open or taken, *Finish…* opens the choice in place, *leave it as it is* by default or *mark it done as
 * yours* with the person's note. Nothing carries a finished session's quest on, so one left taken would have
 * nothing to close it. With no such quest, *Finish* is one press.
 *
 * A molecule: it is handed the note and reports a move, so a parked session with a three-paragraph
 * analysis and one with none are both reachable by passing them.
 */
export function AwaitingPerson({
  parts, question = null, quest = null, pending = false, below = false, onResolve, onAnswer, onChoice, pick, onConversation,
}: {
  /**
   * Its question as its agent asked it through the connector: shown in place of its whole last
   * message, which the conversation holds, its answers listed; their presses are the box's below, or this card's own answer.
   */
  question?: ParkQuestion | null;
  /** The way to the message that asked, in the conversation, where it holds it: *In the conversation*. */
  onConversation?: () => void;
  /**
   * Its lines: Daoris's lead-in worded in the reader's language, and the agent's analysis beneath it word for
   * word (`Note`).
   */
  parts?: readonly NotePart[] | null;
  /** The quest its session holds, where it is still open or taken: what a finish may close. */
  quest?: Pick<Quest, 'id' | 'status'> | null;
  pending?: boolean;
  /**
   * The ledger's moves this card makes, finish and decline, where this surface can make them (the shell); a finish that
   * closes its quest says how.
   */
  onResolve?: (state: Resolution, note: string | null, close?: QuestClose, answered?: Answered) => void;
  /**
   * The answer to a session with no process left, its words said through the say door. It may settle with the
   * sentence it is refused in, said here with the words kept; nothing, and the answer is taken.
   */
  onAnswer?: (answer: string) => Promise<string | null> | void;
  /** Its question's answer selected, sent with the card's words: settled as `onAnswer` is. */
  onChoice?: (label: string, words: string) => Promise<string | null> | void;
  /** The answer selected, the frame's and shared with the box below; absent, the card holds its own. */
  pick?: AnswerPick;
  /** The box at the foot answers it too: the hint names both places. */
  below?: boolean;
}) {
  const { t } = useTranslation();
  const [declining, setDeclining] = useState(false);
  const [answering, setAnswering] = useState(false);
  const [finishing, setFinishing] = useState(false);
  const [reason, setReason] = useState('');
  const [words, setWords] = useState('');
  // The answer on its way, and the sentence one is refused in.
  const [sending, setSending] = useState(false);
  const [refused, setRefused] = useState<string | null>(null);
  // The answer selected in its question, on the question it is selected on: a replaced one is drawn with none.
  const [choice, setChoice] = useState<{ question: string; label: string } | null>(null);
  const own = question && choice?.question === question.id && question.choices.some((one) => one.label === choice.label)
    ? choice.label : null;
  const selected = pick ? pick.selected : own;
  const select = (label: string | null) => {
    if (pick) pick.onSelect(label);
    else setChoice(label === null || !question ? null : { question: question.id, label });
  };
  const send = async (settle: Promise<string | null> | void) => {
    setSending(true);
    setRefused(null);
    try {
      const why = await settle;
      if (why) {
        setRefused(why);
        return;
      }
      setWords('');
      select(null);
      setAnswering(false);
    } finally {
      setSending(false);
    }
  };
  // What the finish does with its quest: left as it is unless the person chooses.
  const [questTo, setQuestTo] = useState<'leave' | 'done'>('leave');
  const [questNote, setQuestNote] = useState('');
  const closable = quest !== null && (quest.status === 'Open' || quest.status === 'Taken');
  const finish = (answered: Answered) => {
    if (!onResolve) return;
    onResolve('completed', null, questTo === 'done' && closable ? { as: 'done', note: questNote.trim() || null } : undefined, answered);
  };

  const field = 'min-h-14 resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink';

  return (
    <WaitingCard title={t('work.head.waiting')}>

      {/* Its answers are rows to select only once the person opens the card's answer, so its words go with them. */}
      {question
        ? (
          <div className="mt-1.5">
            <QuestionCard
              question={question}
              selected={selected}
              onSelect={answering && onChoice ? select : undefined}
            />
          </div>
        )
        : <Note parts={parts} className="mt-1.5 text-body leading-relaxed" />}

      <p className="m-0 mt-2 flex flex-wrap items-center gap-x-2 text-small text-ink-faint">
        <span>{t('work.awaiting.hint')}</span>
        {onConversation && <InConversation onPress={onConversation} />}
      </p>

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
            {refused && <p role="status" className="m-0 text-small text-ink-danger [overflow-wrap:anywhere]">{refused}</p>}
            <div className="flex flex-wrap gap-2">
              {/* The say door keeps words, so an answer with none waits for them, as the box below does. */}
              <Button
                variant="primary"
                disabled={pending || sending || (!(selected && onChoice) && !words.trim())}
                onClick={() => { void send(selected && onChoice ? onChoice(selected, words) : onAnswer(words.trim())); }}
              >
                {t('work.awaiting.answerConfirm')}
              </Button>
              <Button variant="ghost" onClick={() => { setAnswering(false); setRefused(null); }}>{t('common.cancel')}</Button>
            </div>
          </div>
        )
        : finishing && onResolve && closable
        ? (
          /* A finish that may close its quest asks once, open until the ledger answers, a refusal said inside it
             (UXFIX2b2b). Put down, it forgets the choice: the next finish asks again from leaving the quest as it is. */
          <InlineConfirm
            className="mt-2.5"
            label={t('work.awaiting.finishTitle')}
            says={t('work.awaiting.finishQuest', { quest: quest!.id })}
            meanIt={t('work.awaiting.finish')}
            busy={pending}
            onConfirm={finish}
            onClose={() => {
              setFinishing(false);
              setQuestTo('leave');
              setQuestNote('');
            }}
          >
            <div className="basis-full">
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
              <label className="grid basis-full gap-1 text-small text-ink-faint">
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
          </InlineConfirm>
        )
        : declining && onResolve
        ? (
          /* A decline needs its reason, the part whoever reads the record can act on: the move waits for it. */
          <InlineConfirm
            className="mt-2.5"
            label={t('work.awaiting.declineTitle')}
            says={t('work.awaiting.declineSays')}
            meanIt={t('work.awaiting.declineConfirm')}
            busy={pending}
            ready={reason.trim() !== ''}
            onConfirm={(answered) => onResolve('declined', reason.trim(), undefined, answered)}
            onClose={() => setDeclining(false)}
          >
            <label className="grid basis-full gap-1 text-small text-ink-faint">
              <span className="sr-only">{t('work.awaiting.declinePlaceholder')}</span>
              <textarea
                value={reason}
                aria-label={t('work.awaiting.declinePlaceholder')}
                onChange={(event) => setReason(event.target.value)}
                placeholder={t('work.awaiting.declinePlaceholder')}
                className={field}
              />
            </label>
          </InlineConfirm>
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
                {/* With a quest still to close, the finish asks what becomes of it first; with none, one press. */}
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

      {(!onAnswer || below) && (
        <p className="m-0 mt-2 text-small text-ink-faint">{t(onAnswer ? 'work.awaiting.answerHereOrBelow' : 'work.awaiting.answer')}</p>
      )}
    </WaitingCard>
  );
}
