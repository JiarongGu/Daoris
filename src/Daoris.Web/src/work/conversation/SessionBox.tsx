import { useTranslation } from 'react-i18next';
import type { Session } from '../../data/api';
import { Composer } from './Composer';
import type { ChatMessage, Level } from './conversation';
import type { Answered } from '../InlineConfirm';
import { type Box, neverSentence } from '../say';
import type { HeadLink } from '../head/SessionHead';

/**
 * The box at the foot of a session's page (MSG1f, D137 §5.1): offered on every session that takes words, saying what the
 * words will do, and absent on the rest, with the line saying why (D119 §3.2). A live chat's own composer is the frame's,
 * with its turns and its endings (CONV4); this is every other box.
 *
 * @remarks
 * - **The running door** (D136): a working driven session whose door listens. What it holds waits above the box, each
 *   word with its level's menu, and *Send* sends the box's words at the person's default, the level menu beside it at
 *   another (COWORK1a). Where words wait for the turn's end on a door that can stop a turn keeping the session, the keeping
 *   stop stands beside them, its own press; never on the native door's run, which has no such stop.
 * - **The park's answer** (D126 §3.1): its box is the answer, as *Answer…* opens it; a second word joins the first.
 * - **A session that ended**: the same session goes on with the words. Nothing to attach: the resumed run is handed the
 *   words alone (MSG1d), so a file would be promised and never read.
 * - **Words waiting on the record** (SAIDBACK1b, D76): on a park or a session that ended, the words it will go on with wait
 *   above the box, each with *Edit* and *Remove* by its id until a session takes them, as a running turn's queue does.
 * - **Winding up**: words said as its run ends wait for its record to end and then reopen it (D137 §2.1), shown above the
 *   box until its record keeps them and the conversation shows them.
 * - **The line**: nothing takes words, in the page's own words by the code; a conversation that went on in another says
 *   that one's title with *Open session* where the frame holds it (HANDWORDS1-ii).
 * - **Words on their way** (SENDLOST1b): above every box, as sending, and kept where the page stopped waiting on their send.
 *
 * A molecule: the box it is handed, the words in and a press out.
 */
export function SessionBox({
  box, quest, draft, onDraft, sending = false, refusal, held, ending = [], waiting = [], stopping = false, focus = 0, accepts, onSend,
  levels, onLevel, onKeepingStop, onRemove, onEdit, going, continued, onOpenSession,
}: {
  /** What the running door does with a word while its turn runs (COWORK1a), which the box offers the levels by; absent, none. */
  levels?: { nextStep: boolean; steers: boolean };
  /** A held word's level changed by its id (COWORK1a); absent, none is offered. */
  onLevel?: (id: string, level: Level) => Promise<{ done: boolean } | void>;
  /** The keeping stop (COWORK1a), offered where the running door can stop a turn keeping the session; absent, none. */
  onKeepingStop?: (answered: Answered) => void;
  /**
   * The conversation a handed-over or moved one went on in (HANDWORDS1-ii), as the frame holds it: its record and its title.
   * The line names it only where it is the one the driver's `continued` names.
   */
  continued?: HeadLink | null;
  /** Attend the conversation it went on in, from the line's *Open session*. */
  onOpenSession?: (session: Session) => void;
  /**
   * Words on their way to the session (SENDLOST1b), shown above every box that takes words until it holds them: as sending,
   * or `late` where the page stopped waiting on their send.
   */
  going?: { words: ChatMessage[]; late: boolean };
  /** Take a held or waiting word back out, or reword it, by its id (QUEUEEDIT1b, SAIDBACK1b); absent, none offers a control. */
  onRemove?: (id: string) => Promise<{ done: boolean } | void>;
  onEdit?: (id: string, text: string) => Promise<{ done: boolean } | void>;
  box: Box;
  /** Whether these words may go at all: false keeps them in the box, unsent, the caller saying why (D137 §2.4). */
  accepts?: (text: string) => boolean;
  /** The session's quest, which the line for a stand-down and a superseded session names. */
  quest?: string | null;
  draft?: string;
  onDraft?: (text: string) => void;
  sending?: boolean;
  /** The last refusal, said where the person is looking. */
  refusal?: string | null;
  /** What the running door holds behind its turn, as the driver says (SESS3). */
  held?: { queued: ChatMessage[]; taking: boolean };
  /** Words said as the session winds up, held for its record to end (D137 §2.1). */
  ending?: string[];
  /** The words waiting on a parked or ended record for the same session to go on with, as the driver lists them (SAIDBACK1b). */
  waiting?: ChatMessage[];
  /** The keeping stop was asked for, and the driver has not answered. */
  stopping?: boolean;
  /** Bumped to put the focus in the box: *Answer…* from a row, *Send back…* from a review. */
  focus?: number;
  /** The words, and the level chosen from the running door's menu (COWORK1a); none is the person's default. */
  onSend: (text: string, level?: Level) => void;
}) {
  const { t } = useTranslation();

  if (box.kind === 'line') {
    // The conversation it went on in, where the frame holds it, by its title with its door (HANDWORDS1-ii); else its id.
    const went = box.why === 'continued' && continued && continued.session.id === box.to ? continued : null;
    return (
      <div className="border-t border-line px-4 py-3">
        {went ? (
          <p className="m-0 text-small text-ink-soft">
            <span>{t('work.say.continuedIn', { title: went.title })}</span>
            {onOpenSession && (
              <button
                type="button"
                onClick={() => onOpenSession(went.session)}
                className="ml-2 cursor-pointer border-0 bg-transparent p-0 font-sans text-small text-accent hover:underline"
              >
                {t('work.head.openSession')}
              </button>
            )}
          </p>
        ) : <p className="m-0 text-small text-ink-soft">{neverSentence(t, box.why, { quest, to: box.to })}</p>}
        {refusal && <p role="status" className="m-0 mt-1 text-small text-ink-soft [overflow-wrap:anywhere]">{refusal}</p>}
      </div>
    );
  }
  if (box.kind !== 'steer' && box.kind !== 'say') return null;

  // Every box here: words alone, no endings of its own (the page header and the parked card own those, D126 §3.3).
  const shared = {
    live: true, endings: false, attachments: false, sending, refusal, draft, onDraft, focus, accepts, going,
    onSend: (text: string, _files: File[], level?: Level) => (level ? onSend(text.trim(), level) : onSend(text.trim())), onFinish: () => {},
  };

  if (box.kind === 'steer') {
    return (
      <Composer
        {...shared}
        placeholder={t('work.steer.placeholder')}
        queued={held?.queued ?? []}
        taking={held?.taking ?? false}
        // The levels beside *Send* and each held word's (COWORK1a); the keeping stop beside what waits for the turn's end,
        // its own press, which only something held can ask (with nothing held, stopping the turn would end the work).
        levels={levels}
        onLevel={onLevel}
        onKeepingStop={onKeepingStop}
        stopping={stopping}
        onRemove={onRemove}
        onEdit={onEdit}
      />
    );
  }

  // What waits on the record, each with its controls (SAIDBACK1b); a reword goes as the edit, never as more words.
  const resting = { queued: waiting, queuedLabel: t('work.say.waiting'), onRemove, onEdit };

  if (box.mode === 'answer') {
    return (
      <Composer
        {...shared}
        {...resting}
        // One box with its waiting words folded to a line (WAITFOOT2c).
        fold
        placeholder={t('work.awaiting.answerPlaceholder')}
        sendLabel={t('work.awaiting.answerConfirm')}
      />
    );
  }

  return (
    <Composer
      {...shared}
      {...(box.mode === 'ending'
        ? { queued: ending.map((text) => ({ text, files: [] })), queuedLabel: t('work.say.heldEnding') }
        : resting)}
      placeholder={t('work.say.placeholder')}
    />
  );
}
