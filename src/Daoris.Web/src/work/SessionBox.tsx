import { useTranslation } from 'react-i18next';
import { Composer } from './Composer';
import type { ChatMessage } from './conversation';
import { type Box, neverSentence } from './say';

/**
 * The box at the foot of a session's page (MSG1f, D137 §5.1): offered on every session that takes words, saying what the
 * words will do, and absent on the rest, with the line saying why (D119 §3.2). A live chat's own composer is the frame's,
 * with its turns and its endings (CONV4); this is every other box.
 *
 * @remarks
 * - **The running door** (D136): a working driven session whose door listens. What it holds waits above the box, and
 *   *Send now* stops the turn so it goes, offered only while something is held.
 * - **The park's answer** (D126 §3.1): its box is the answer, as *Answer…* opens it; a second word joins the first.
 * - **A session that ended**: the same session goes on with the words. Nothing to attach: the resumed run is handed the
 *   words alone (MSG1d), so a file would be promised and never read.
 * - **Words waiting on the record** (SAIDBACK1b, D76): on a park or a session that ended, the words it will go on with wait
 *   above the box, each with *Edit* and *Remove* by its id until a session takes them, as a running turn's queue does.
 * - **Winding up**: words said as its run ends wait for its record to end and then reopen it (D137 §2.1), shown above the
 *   box until its record keeps them and the conversation shows them.
 * - **The line**: nothing takes words, in the page's own words by the code.
 *
 * A molecule: the box it is handed, the words in and a press out.
 */
export function SessionBox({
  box, quest, draft, onDraft, sending = false, refusal, held, ending = [], waiting = [], stopping = false, focus = 0, accepts, onSend,
  onSendNow, onRemove, onEdit,
}: {
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
  /** *Send now* was asked for, and the driver has not answered. */
  stopping?: boolean;
  /** Bumped to put the focus in the box: *Answer…* from a row, *Send back…* from a review. */
  focus?: number;
  onSend: (text: string) => void;
  /** *Send now* on the running door: stop the turn so what is held goes now. */
  onSendNow?: () => void;
}) {
  const { t } = useTranslation();

  if (box.kind === 'line') {
    return (
      <div className="border-t border-line px-4 py-3">
        <p className="m-0 text-small text-ink-soft">{neverSentence(t, box.why, { quest, to: box.to })}</p>
      </div>
    );
  }
  if (box.kind !== 'steer' && box.kind !== 'say') return null;

  // Every box here: words alone, no endings of its own (the page header and the parked card own those, D126 §3.3).
  const shared = {
    live: true, endings: false, attachments: false, sending, refusal, draft, onDraft, focus, accepts,
    onSend: (text: string) => onSend(text.trim()), onFinish: () => {},
  };

  if (box.kind === 'steer') {
    return (
      <Composer
        {...shared}
        placeholder={t('work.steer.placeholder')}
        queued={held?.queued ?? []}
        taking={held?.taking ?? false}
        // Only something held can be sent now; with nothing held, stopping the turn would end the work.
        stoppable={(held?.queued.length ?? 0) > 0}
        stopping={stopping}
        stopTurnLabel={t('work.steer.now')}
        stopTurnTip={t('work.steer.nowTip')}
        onStopTurn={onSendNow}
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
