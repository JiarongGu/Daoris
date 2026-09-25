import { useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MAX_FILE_BYTES, MAX_FILES } from '../attachments';
import { type Carry, NO_CARRY, useCarry } from '../compose/carry';
import { size } from '../format';
import { cn } from '../lib/cn';
import { Button, Icon, Tip } from '../ui';
import type { ChatMessage } from './conversation';

/**
 * The input a person talks to a session through (SES2), with its **two distinct endings** and, since
 * CONV4b, the **turn's own stop** beside them.
 *
 * @remarks
 * **Finish and stop mean different things and are never one button.** Finishing closes the input
 * and lets the harness wind up on its own — the record ends `completed`. Stopping cuts it off, and
 * the record says the person did (`stopped`). Collapsing them would make the ledger's two endings
 * indistinguishable in the one place a person chooses between them.
 *
 * **Stopping the turn ends neither** (CONV4a). It stops what the agent is doing now and keeps the
 * conversation, and it is offered only while a turn runs on a door that can stop one: a text door
 * cannot see where a turn ends, and a button that could only be refused is worse than none.
 *
 * **A message sent while a turn runs waits for it**, so the button says *queue* then, and what is
 * waiting is shown above the box in the order sent. It is in no record until it goes, and nothing
 * else on the screen shows it.
 *
 * **A session that ends mid-sentence keeps what was typed.** The draft stays, disabled, with the
 * ending stated — because the alternative is a box that silently swallows a paragraph somebody was
 * halfway through, and they have no way to get it back. The frame may hold the draft instead
 * (`draft`, `onDraft`), which is how each session keeps its own across a switch and a reload.
 *
 * **Files go with the message** (CONV4c): chosen, dropped anywhere on the form, or pasted, through the
 * `carry` molecule the quest and ask composers use, so the three cannot drift on how a file arrives or
 * what a message carries. They wait above the box as chips, go with the next send — alone, if there
 * are no words — and are let go of once sent. They are the form's own and last one message: switching
 * sessions starts a fresh form, and a reload loses them, because a browser's file cannot be kept.
 *
 * **A refusal renders verbatim**, as every service and driver sentence does: "nothing is listening"
 * is the ledger's answer, and rewriting it here would be the second copy of a sentence.
 *
 * **`endings` is the verb-ownership rule** (D56). A parked session used to render the attention
 * band's *finish it · decline… · stop it* and this form's *send · finish · stop* at the same time,
 * 400px apart — two owners for one set of moves, which is worse than either. So the state decides:
 * while a session is `awaiting-person` the band owns the endings and this form keeps `send` alone,
 * under the band's own sentence saying that answering is a message rather than one of those moves.
 * No capability is lost; every move stays reachable wherever it is legal, from exactly one place.
 */
export function Composer({
  live, sending = false, refusal, endings = true, draft, onDraft,
  queued = [], taking = false, stoppable = false, stopping = false,
  onSend, onFinish, onStop, onStopTurn,
}: {
  /** Whether anything is listening. False is an ending, not a disabled state. */
  live: boolean;
  sending?: boolean;
  /** The last refusal, shown inline and word for word. */
  refusal?: string | null;
  /** Whether this form owns the two endings, or something else on screen does. */
  endings?: boolean;
  /** The draft, when the frame holds it. Absent, the form holds its own. */
  draft?: string;
  onDraft?: (text: string) => void;
  /** What the person sent that has not reached the harness, in the order sent (CONV4a), with its files' names. */
  queued?: ChatMessage[];
  /** A turn is on its way to the harness or running there. */
  taking?: boolean;
  /** Whether this session's door can stop a turn at all. */
  stoppable?: boolean;
  stopping?: boolean;
  /** The words and the files attached to them. */
  onSend: (text: string, files: File[]) => void;
  onFinish: () => void;
  onStop: () => void;
  onStopTurn?: () => void;
}) {
  const { t } = useTranslation();
  const waitingLabel = useId();
  const [own, setOwn] = useState('');
  const text = draft ?? own;
  const setText = onDraft ?? setOwn;
  const [carry, setCarry] = useState<Carry>(NO_CARRY);
  const attach = useCarry(carry, setCarry, live);
  const chooser = useRef<HTMLInputElement>(null);
  const files = carry.files;

  const say = () => {
    const message = text.trim();
    if ((!message && files.length === 0) || !live || sending) return;
    onSend(message, files);
    setText('');
    setCarry(NO_CARRY);
    attach.forget();
  };

  return (
    <form
      className="grid gap-2 border-t border-line px-4 py-3"
      onSubmit={(event) => { event.preventDefault(); say(); }}
      {...(live ? attach.handlers : {})}
    >
      {refusal && <p className="m-0 text-small text-st-declined">{refusal}</p>}
      {!live && <p className="m-0 text-small text-ink-soft">{t('work.composer.over')}</p>}

      {live && queued.length > 0 && (
        <div className="grid gap-1">
          <span id={waitingLabel} className="text-meta text-ink-faint">{t('work.composer.queued')}</span>
          <ol aria-labelledby={waitingLabel} className="m-0 grid list-none gap-1 p-0">
            {queued.map((message, index) => (
              <li
                key={`${index}:${message.text}`}
                className="rounded-control border border-dashed border-line-strong px-2.5 py-1 text-small text-ink-soft"
              >
                <span className="line-clamp-2 whitespace-pre-wrap">{message.text}</span>
                {message.files.length > 0 && (
                  <span className="mt-0.5 flex min-w-0 items-center gap-1 text-meta text-ink-faint">
                    <Icon name="attach" size={11} className="shrink-0" />
                    <span className="truncate">{message.files.join(', ')}</span>
                  </span>
                )}
              </li>
            ))}
          </ol>
        </div>
      )}

      {files.length > 0 && (
        <ul aria-label={t('work.composer.attached')} className="m-0 flex list-none flex-wrap gap-1.5 p-0">
          {files.map((file, index) => (
            <li
              key={`${index}:${file.name}`}
              className="inline-flex min-w-0 max-w-full items-center gap-1.5 rounded-control border border-line-strong bg-raised py-0.5 pl-2 pr-0.5 text-small text-ink"
            >
              <Icon name="attach" size={12} className="shrink-0 text-ink-faint" />
              <span className="min-w-0 truncate">{file.name}</span>
              <span className="shrink-0 font-mono text-meta text-ink-faint">{size(file.size)}</span>
              <Button
                type="button" variant="ghost"
                aria-label={t('carry.remove', { name: file.name })}
                onClick={() => attach.remove(index)}
                className="h-5 w-5 justify-center px-0"
              >
                <Icon name="x" size={11} />
              </Button>
            </li>
          ))}
        </ul>
      )}
      {attach.leftOff && (
        <p role="status" className="m-0 text-small text-st-declined">
          {t(`carry.${attach.leftOff}`, { max: MAX_FILES, size: size(MAX_FILE_BYTES) })}
        </p>
      )}

      <label className="grid gap-1 text-small text-ink-faint">
        <span className="sr-only">{t('work.composer.label')}</span>
        <textarea
          value={text}
          disabled={!live}
          aria-label={t('work.composer.label')}
          onChange={(event) => setText(event.target.value)}
          // Enter sends, because this is a conversation; a newline needs the modifier, because a
          // paste of several lines is one message and should stay one.
          onKeyDown={(event) => {
            if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); say(); }
          }}
          placeholder={t('work.composer.placeholder')}
          className={cn(
            'min-h-14 resize-y rounded-control border bg-raised px-2.5 py-1.5 text-body text-ink transition-colors duration-(--speed) disabled:opacity-55',
            // Where a dragged file will go: the whole form takes it, and the box lights up to say so.
            attach.dragging ? 'border-accent bg-accent-soft' : 'border-line-strong',
          )}
        />
      </label>

      <div className="flex flex-wrap items-center gap-2">
        {/* The form's submit, and nothing else: its submit handler is the one path a press takes.
            While a turn runs it says *queue*, because that is what a press does then. */}
        {taking && live
          ? (
            <Tip content={t('work.composer.queueTip')}>
              <Button type="submit" variant="primary" disabled={(!text.trim() && files.length === 0) || sending}>
                {t('work.composer.queue')}
              </Button>
            </Tip>
          )
          : (
            <Button type="submit" variant="primary" disabled={!live || (!text.trim() && files.length === 0) || sending}>
              {t('work.composer.send')}
            </Button>
          )}
        {live && (
          <>
            <Tip content={t('work.composer.attachTip')}>
              <Button
                type="button" variant="ghost"
                aria-label={t('work.composer.attach')}
                onClick={() => chooser.current?.click()}
                className="h-7 w-7 justify-center px-0"
              >
                <Icon name="attach" size={15} />
              </Button>
            </Tip>
            <input
              ref={chooser} type="file" multiple className="sr-only" tabIndex={-1}
              aria-label={t('carry.choose')}
              onChange={(event) => {
                attach.attach(Array.from(event.target.files ?? []));
                // Cleared, so choosing the same file again after removing it is a change.
                event.target.value = '';
              }}
            />
          </>
        )}
        {live && taking && stoppable && onStopTurn && (
          <Tip content={t('work.composer.stopTurnTip')}>
            <Button type="button" disabled={stopping} onClick={onStopTurn}>{t('work.composer.stopTurn')}</Button>
          </Tip>
        )}
        {live && endings && (
          <>
            <Tip content={t('work.composer.finishTip')}>
              <Button type="button" onClick={onFinish}>{t('work.composer.finish')}</Button>
            </Tip>
            <Tip content={t('work.composer.stopTip')}>
              <Button type="button" variant="danger" onClick={onStop}>{t('work.composer.stop')}</Button>
            </Tip>
          </>
        )}
      </div>
    </form>
  );
}
