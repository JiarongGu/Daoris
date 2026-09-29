import { type KeyboardEvent, useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MAX_FILE_BYTES, MAX_FILES } from '../attachments';
import { type Carry, NO_CARRY, useCarry, useFileChooser } from '../compose/carry';
import { size } from '../format';
import { cn } from '../lib/cn';
import { Button, Icon, Tip } from '../ui';
import { ContextRing } from './ContextRing';
import type { ChatMessage, Usage } from './conversation';
import { MentionList } from './MentionList';
import { mentionAt, rankMentions, withMention } from './mentions';

/**
 * What the frame knows of the session's tree, for `@` (CONV4d): its files, or null while they are
 * being asked for; how many the host's bound left out; and the host's sentence when it cannot list them.
 */
export type MentionSource = { files: string[] | null; unlisted: number; refusal: string | null };

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
 * **`@` names a file in the session's tree** (CONV4d). The mention is text, and both doors expand it
 * as typed, so the form only helps write it: after an `@` it offers the tree's files (`mentions`),
 * arrows move, Enter or Tab writes the one chosen — quoted when it holds a space, the spelling both
 * doors read — and Escape leaves what was typed. While the list offers something, Enter is the list's
 * and not a send. The frame is told when a mention is being written (`onMentioning`), so the tree is
 * listed only then.
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
  queued = [], queuedLabel, taking = false, opening = false, stoppable = false, stopping = false, mentions, onMentioning, context, placeholder,
  attachments = true, sendLabel, stopTurnLabel, stopTurnTip, onSend, onFinish, onStop, onStopTurn,
}: {
  /**
   * What stopping the turn says, where it does something else (SESS3): on a driven session it stops the
   * turn so what waits goes now, rather than handing it back.
   */
  stopTurnLabel?: string;
  stopTurnTip?: string;
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
  /** What the waiting words are waiting for, when it is not the turn in hand (HELP4: the conversation opening). */
  queuedLabel?: string;
  /** A turn is on its way to the harness or running there. */
  taking?: boolean;
  /** The conversation is still opening (HELP4): what waits, waits for it, not for a turn to end. */
  opening?: boolean;
  /** Whether this session's door can stop a turn at all. */
  stoppable?: boolean;
  stopping?: boolean;
  /** The session tree's files, for `@`. Absent, an `@` is only text. */
  mentions?: MentionSource;
  /** Told whether a mention is being written, so the frame asks for the files only then. */
  onMentioning?: (writing: boolean) => void;
  /** What the box says while empty, where the conversation is not a repository's (HELP1a). */
  placeholder?: string;
  /** Whether files go with a message. An answer to a parked session is words, and takes none. */
  attachments?: boolean;
  /** What the send button says, where sending is a move of its own (an answer that carries a session on). */
  sendLabel?: string;
  /** How full the session's context is, for the ring under the box (CONV5). Absent, no ring. */
  context?: { usage?: Usage; door?: 'structured' | 'text' };
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
  const chooser = useFileChooser(attach.attach, t('carry.choose'));
  const files = carry.files;

  // `@` a file (CONV4d): where the caret is decides whether a mention is being written.
  const field = useRef<HTMLTextAreaElement>(null);
  const listId = useId();
  const [caret, setCaret] = useState(0);
  const [active, setActive] = useState(0);
  // The mention the person pressed Escape on, by where its `@` stands. It stays closed until the caret
  // leaves it, and a new `@` is a new question.
  const [dismissed, setDismissed] = useState<number | null>(null);
  // Where the caret goes once a chosen file is in the box — after the box holds the new text.
  const placeCaret = useRef<number | null>(null);

  const mention = mentions && live ? mentionAt(text, caret) : null;
  const open = mention !== null && mention.start !== dismissed;
  const options = open && mentions?.files ? rankMentions(mentions.files, mention.query) : [];
  // The selection follows the list, as the palette's does: an index past the end would take nothing.
  const chosen = Math.min(active, Math.max(0, options.length - 1));
  const offering = open && options.length > 0;

  useEffect(() => {
    if (!open) return undefined;
    onMentioning?.(true);
    return () => onMentioning?.(false);
  }, [open, onMentioning]);

  useLayoutEffect(() => {
    if (placeCaret.current === null) return;
    field.current?.setSelectionRange(placeCaret.current, placeCaret.current);
    placeCaret.current = null;
  }, [text]);

  const follow = (box: HTMLTextAreaElement) => {
    // 🔴 While a taken file is on its way into the box, the box still holds the text before it. React
    // reads the selection on the same keydown that takes one (seen on the window), and that reading,
    // taken as the caret, put it back inside the mention and opened the list again.
    if (placeCaret.current !== null) return;
    setCaret(box.selectionStart);
    if (!mentionAt(box.value, box.selectionStart)) setDismissed(null);
  };

  const pick = (path: string) => {
    if (!mention) return;
    const next = withMention(text, mention, path);
    if (next.text === text) {
      // The file was already written in full: no new text will arrive to place the caret after, so it
      // is placed now — left pending, it would jump back here on the next keystroke.
      field.current?.setSelectionRange(next.caret, next.caret);
    } else {
      placeCaret.current = next.caret;
      setText(next.text);
    }
    setCaret(next.caret);
    setActive(0);
  };

  // The list's keys first, while it offers something; then the form's own.
  const onKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (offering && (event.key === 'ArrowDown' || event.key === 'ArrowUp')) {
      event.preventDefault();
      const by = event.key === 'ArrowDown' ? 1 : -1;
      // Wraps, because a list you can leave by the bottom is one you have to scroll back up.
      setActive(((chosen + by) % options.length + options.length) % options.length);
      return;
    }
    if (offering && ((event.key === 'Enter' || event.key === 'Tab') && !event.shiftKey)) {
      event.preventDefault();
      pick(options[chosen]!);
      return;
    }
    if (open && event.key === 'Escape') {
      // The list's Escape, and nobody else's.
      event.preventDefault();
      event.stopPropagation();
      setDismissed(mention.start);
      return;
    }
    // Enter sends, because this is a conversation; a newline needs the modifier, because a paste of
    // several lines is one message and should stay one.
    if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); say(); }
  };

  // 🔴 No box where nothing listens (UX5 U9, INT4h's rule): an ended session keeps a box only while it
  // holds words the person wrote, read-only so they can be copied out. An empty box and a send under
  // "what you typed is still here" was the window's view of every ended session opened from the rail.
  const writing = live || text.trim() !== '';

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
      // The box follows the centre's width, as the conversation above it does (UX5 U16:
      // capped, it was half a maximized window).
      className="grid gap-2 border-t border-line px-4 py-3"
      onSubmit={(event) => { event.preventDefault(); say(); }}
      {...(live && attachments ? attach.handlers : {})}
    >
      {refusal && <p className="m-0 text-small text-st-declined">{refusal}</p>}
      {!live && writing && <p className="m-0 text-small text-ink-soft">{t('work.composer.over')}</p>}

      {live && queued.length > 0 && (
        <div className="grid gap-1">
          <span id={waitingLabel} className="text-meta text-ink-faint">{queuedLabel ?? t(taking && !opening ? 'work.composer.queued' : 'work.composer.opening')}</span>
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

      {writing && <div className="relative grid">
        {mention && open && mentions && (
          <MentionList
            id={listId}
            options={options}
            active={chosen}
            query={mention.query}
            listing={mentions.files === null && !mentions.refusal}
            refusal={mentions.refusal}
            empty={mentions.files?.length === 0}
            unlisted={mentions.unlisted}
            onPick={pick}
            onActive={setActive}
          />
        )}
        <label className="grid gap-1 text-small text-ink-faint">
          <span className="sr-only">{t('work.composer.label')}</span>
          <textarea
            ref={field}
            value={text}
            readOnly={!live}
            aria-label={t('work.composer.label')}
            // The files are the box's list, and the box keeps the focus: a screen reader follows the
            // arrows through `aria-activedescendant`, as the palette's does.
            aria-autocomplete={mentions ? 'list' : undefined}
            aria-controls={offering ? listId : undefined}
            aria-activedescendant={offering ? `${listId}-${chosen}` : undefined}
            onChange={(event) => {
              setText(event.target.value);
              setActive(0);
              follow(event.target);
            }}
            onSelect={(event) => follow(event.currentTarget)}
            onKeyDown={onKeyDown}
            placeholder={placeholder ?? t('work.composer.placeholder')}
            className={cn(
              'min-h-14 resize-y rounded-control border bg-raised px-2.5 py-1.5 text-body text-ink transition-colors duration-(--speed) read-only:bg-page read-only:text-ink-soft',
              // Where a dragged file will go: the whole form takes it, and the box lights up to say so.
              attach.dragging ? 'border-accent bg-accent-soft' : 'border-line-strong',
            )}
          />
        </label>
      </div>}

      <div className="flex flex-wrap items-center gap-2">
        {/* With no box, the sentence saying why stands where the send would, on the meter's line. */}
        {!writing && <p className="m-0 text-small text-ink-soft">{t('work.composer.ended')}</p>}
        {/* The form's submit, and nothing else: its submit handler is the one path a press takes.
            While a turn runs it says *queue*, because that is what a press does then. Absent once
            the session has ended, where a send could only ever be a dead press (UX5 U9). */}
        {live && (taking
          ? (
            <Tip content={t('work.composer.queueTip')}>
              <Button type="submit" variant="primary" disabled={(!text.trim() && files.length === 0) || sending}>
                {t('work.composer.queue')}
              </Button>
            </Tip>
          )
          : (
            <Button type="submit" variant="primary" disabled={(!text.trim() && files.length === 0) || sending}>
              {sendLabel ?? t('work.composer.send')}
            </Button>
          ))}
        {live && attachments && (
          <>
            <Tip content={t('work.composer.attachTip')}>
              <Button
                type="button" variant="ghost"
                aria-label={t('work.composer.attach')}
                onClick={chooser.open}
                className="h-7 w-7 justify-center px-0"
              >
                <Icon name="attach" size={15} />
              </Button>
            </Tip>
            {chooser.input}
          </>
        )}
        {live && taking && stoppable && onStopTurn && (
          <Tip content={stopTurnTip ?? t('work.composer.stopTurnTip')}>
            <Button type="button" disabled={stopping} onClick={onStopTurn}>{stopTurnLabel ?? t('work.composer.stopTurn')}</Button>
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
        {/* How full the context is (CONV5), at the row's far end, where the reference keeps it. */}
        {context && <span className="ml-auto"><ContextRing usage={context.usage} door={context.door} /></span>}
      </div>
    </form>
  );
}
