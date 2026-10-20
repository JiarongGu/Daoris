import { type KeyboardEvent, useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MAX_FILE_BYTES, MAX_FILES } from '../../compose/attachments';
import { type Carry, NO_CARRY, useCarry, useFileChooser } from '../../compose/carry';
import { size } from '../../locales/format';
import { cn } from '../../lib/cn';
import { isComposing } from '../../lib/composing';
import { Button, Chip, Icon, Menu, Tip } from '../../design/ui';
import { type Answered, InlineConfirm } from '../InlineConfirm';
import { ContextRing } from './ContextRing';
import type { ChatMessage, Level, Usage } from './conversation';
import { MentionList } from './MentionList';
import { mentionAt, rankMentions, withMention } from './mentions';
import { type SessionOption, SessionOptions } from './SessionOptions';

/**
 * What the frame knows of the session's tree, for `@` (CONV4d): its files, or null while they are
 * being asked for; how many the host's bound left out; and the host's sentence when it cannot list them.
 */
export type MentionSource = { files: string[] | null; unlisted: number; refusal: string | null };

/** One row of words above the box: waiting behind a turn, or on their way (SENDLOST1b). */
const WORDS_ROW = 'flex items-start gap-2 rounded-control border border-dashed border-line-strong px-2.5 py-1 text-small text-ink-soft';

/** A message's words above the box, with its files' names. */
function Words({ message }: { message: ChatMessage }) {
  return (
    <span className="min-w-0 flex-1">
      <span className="line-clamp-2 whitespace-pre-wrap wrap-anywhere">{message.text}</span>
      {message.files.length > 0 && (
        <span className="mt-0.5 flex min-w-0 items-center gap-1 text-meta text-ink-faint">
          <Icon name="attach" size={11} className="shrink-0" />
          <span className="truncate">{message.files.join(', ')}</span>
        </span>
      )}
    </span>
  );
}

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
 * **A message sent while a turn runs reaches it at the level the person chooses** (COWORK1a, D188): one press, *Send*, at
 * their default, its tip saying what the door does, and a menu of the three levels beside it, each sending the box's words at
 * its level; *now* only where the agent steers, shown disabled with the reason elsewhere. No level stops a turn: where words
 * wait for the turn's end and the door can stop a turn keeping the session, the keeping stop stands beside them, its own press
 * that asks once. A composer told no levels queues as it did (*Queue*). What is waiting is shown above the box in the order
 * sent, each word of the person's with its own level menu.
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
 *
 * **The conversation's model and effort sit on its row** (AGT6b, D98), where the agent offered them on
 * the protocol door — the console's `/model`, which typed as a message cut a turn short. The options
 * are handed in: this form sends nothing itself.
 */
export function Composer({
  live, sending = false, refusal, endings = true, draft, onDraft, carried, onCarry,
  queued = [], queuedLabel, taking = false, opening = false, stoppable = false, stopping = false, mentions, onMentioning, context, placeholder,
  attachments = true, sendLabel, stopTurnLabel, stopTurnTip, offered, optionsBusy = false, onOption, onSend, onFinish, onStop, onStopTurn,
  levels, onLevel, onKeepingStop, focus = 0, accepts, onRemove, onEdit, going, fold = false,
}: {
  /**
   * What the session's door does with a word while a turn runs (COWORK1a; the message-urgency design §3.0, §8.1), which the
   * composer offers the levels by: `nextStep`, it takes words at the turn's next step; `steers`, its agent steers, so *now* cuts
   * its step. Absent, no level is offered and a press queues as it did: a door this page does not follow, or none known yet.
   */
  levels?: { nextStep: boolean; steers: boolean };
  /** Change a waiting word's level by its id (COWORK1a, the design §8.2), answering as `onRemove` does. Absent, its words offer none. */
  onLevel?: (id: string, level: Level) => Promise<{ done: boolean } | void>;
  /**
   * The keeping stop (COWORK1a, the design §3.2): stop the turn so what waits goes next, the person's own press beside words held
   * for the turn's end, offered only where the door can stop a turn keeping the session. It asks once; the caller tells it how
   * the stop ended. Absent, none is offered.
   */
  onKeepingStop?: (answered: Answered) => void;
  /**
   * Fold the waiting words to one line with their count, open to the list on a press (WAITFOOT2c): a park's answer box and
   * the words waiting on it read as one box. The words on their way (`going`) are never folded.
   */
  fold?: boolean;
  /**
   * Words on their way to the session (SENDLOST1b), in the order sent: shown the moment they are sent, as sending, until it
   * holds them. `late` where the page stopped waiting on their send (the bridge's TIMEOUT): the machine has not answered yet
   * and may still take them, so they offer no control, as an edit or a removal of them would say them twice.
   */
  going?: { words: ChatMessage[]; late: boolean };
  /**
   * Take a waiting message back out, by its id (QUEUEEDIT1b). Answers whether it was still waiting: `done: false` is a
   * turn having it already, said in words. Absent, no waiting message offers a control.
   */
  onRemove?: (id: string) => Promise<{ done: boolean } | void>;
  /** Reword a waiting message in place (QUEUEEDIT1b), answering as `onRemove` does. */
  onEdit?: (id: string, text: string) => Promise<{ done: boolean } | void>;
  /**
   * Whether these words may go (MSG1f, D137 §2.4): false keeps them in the box, unsent, and the caller says why — a door
   * that takes fewer characters at once than they hold. Absent, every message may go.
   */
  accepts?: (text: string) => boolean;
  /**
   * Bumped to put the focus in the box (D126 §3.1: *Answer…* from a session's row attends it and opens its box with the
   * focus). Zero is no ask.
   */
  focus?: number;
  /** The conversation's model and effort as its agent offered them (AGT6b). Absent or empty, none is offered. */
  offered?: SessionOption[];
  /** A change to one is on its way to the agent. */
  optionsBusy?: boolean;
  /** The person chose a value for one of them. */
  onOption?: (option: string, value: string) => void;
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
  /**
   * The files with the draft, where its holder keeps them (ASKHIST1c: Ask Daoris keeps each conversation's across its
   * history). Absent, the form holds its own, which go with the form.
   */
  carried?: Carry;
  onCarry?: (next: Carry) => void;
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
  /** The words and the files attached to them, and the level chosen from the menu (COWORK1a); none is the default. */
  onSend: (text: string, files: File[], level?: Level) => void;
  onFinish: () => void;
  /**
   * The session's stop, where this form owns it: Ask Daoris's panel, which holds its conversation on every view. In
   * Sessions the stop's one owner is the page header (D126 §3.3), so absent there, and no stop is drawn.
   */
  onStop?: () => void;
  onStopTurn?: () => void;
}) {
  const { t } = useTranslation();
  const waitingLabel = useId();
  const goingLabel = useId();
  const [own, setOwn] = useState('');
  const text = draft ?? own;
  const setText = onDraft ?? setOwn;
  const [ownCarry, setOwnCarry] = useState<Carry>(NO_CARRY);
  const carry = carried ?? ownCarry;
  const setCarry = onCarry ?? setOwnCarry;
  const attach = useCarry(carry, setCarry, live);
  const chooser = useFileChooser(attach.attach, t('carry.choose'));
  const files = carry.files;

  // A waiting message being reworded (QUEUEEDIT1b): its words are in the box, and what the box held is kept for after.
  const [editing, setEditing] = useState<{ id: string; stash: string } | null>(null);
  const [taken, setTaken] = useState(false);
  const [listed, setListed] = useState(false);
  // A list that moved on under the edit (the word went, or its id changed) ends the edit: its id would name nothing.
  const stale = editing !== null && !queued.some((message) => message.id === editing.id);
  useEffect(() => {
    if (stale && editing) { setText(editing.stash); setEditing(null); }
    // `setText` follows the draft's holder, which changes with every render of an uncontrolled box.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stale]);
  useEffect(() => { if (queued.length === 0) setTaken(false); }, [queued.length]);

  const startEdit = (message: ChatMessage) => {
    if (!message.id) return;
    setTaken(false);
    setEditing({ id: message.id, stash: editing ? editing.stash : text });
    setText(message.text);
    field.current?.focus();
  };
  const cancelEdit = () => {
    if (!editing) return;
    setText(editing.stash);
    setEditing(null);
  };
  // A driver refusal of the edit, word for word, like every driver sentence here.
  const [refused, setRefused] = useState<string | null>(null);
  const said = (error: unknown) => setRefused(error instanceof Error ? error.message : String(error));
  const remove = async (id: string) => {
    setTaken(false);
    setRefused(null);
    try {
      const answer = await onRemove?.(id);
      if (answer && !answer.done) setTaken(true);
    } catch (error) { said(error); }
  };

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

  // Told to take the focus (D126 §3.1's *Answer…*): once per telling, a new one each press.
  useEffect(() => {
    if (focus > 0) field.current?.focus();
  }, [focus]);

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

  // The list's keys first, while it offers something; then the form's own. An input method's first of all (IME1): the
  // Enter that accepts a candidate sent half a message, and its arrows, Tab and Escape are its own too.
  const onKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (isComposing(event)) return;
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
    if (editing && event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      cancelEdit();
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
    if (editing && onEdit) {
      // Rewording keeps the word's place and files, so the box's own files stay where they are. Blank is a removal, and
      // that is what *Remove* is for.
      if (!message || !live) return;
      // REWORDBOUND1: the door's bound holds for a reword as for a send; the words and the edit stay in the box.
      if (accepts && !accepts(message)) return;
      const { id, stash } = editing;
      setRefused(null);
      void onEdit(id, message).then((answer) => {
        if (answer && !answer.done) { setTaken(true); setEditing(null); return; }
        setText(stash);
        setEditing(null);
      }, said);
      return;
    }
    if ((!message && files.length === 0) || !live || sending) return;
    // Words the door could not take stay in the box, unsent; the caller says why (MSG1f, D137 §2.4).
    if (accepts && !accepts(message)) return;
    onSend(message, files);
    setText('');
    setCarry(NO_CARRY);
    attach.forget();
  };

  // COWORK1a (D188; the message-urgency design §8.1): while a turn runs, the box's words at a level chosen from the menu beside
  // *Send*. Enter and *Send* are the default; no level is remembered, so each word starts there. 🔴 With nothing typed the menu
  // presses nothing: *now* was once a stop with an empty box, and a stop is the keeping stop's own press (trap 2).
  const hasWords = text.trim() !== '' || files.length > 0;
  const leveled = live && taking && levels !== undefined && !editing;
  const sendAt = (level: Level) => {
    const message = text.trim();
    if (!hasWords || !live || sending) return;
    if (accepts && !accepts(message)) return;
    onSend(message, files, level);
    setText('');
    setCarry(NO_CARRY);
    attach.forget();
  };
  // A door that carries only text has no turns, so it honours no level and offers none (the design §3.0, trap 23).
  const textDoor = context?.door === 'text';
  // The keeping stop (the design §3.2): beside words held for the turn's end, where the door can stop a turn keeping the session.
  const keeps = live && taking && Boolean(onKeepingStop) && levels !== undefined && !levels.nextStep && queued.length > 0;
  const [asking, setAsking] = useState(false);
  useEffect(() => { if (!keeps) setAsking(false); }, [keeps]);
  // A level changed while the word waits (the design §8.2); one a turn took meanwhile is the agent's, said as a removal's is.
  const changeLevel = async (id: string, level: Level) => {
    setTaken(false);
    setRefused(null);
    try {
      const answer = await onLevel?.(id, level);
      if (answer && !answer.done) setTaken(true);
    } catch (error) {
      said(error);
    }
  };

  return (
    <form
      // The box follows the centre's width, as the conversation above it does (UX5 U16:
      // capped, it was half a maximized window). Its columns are bounded, here and in what waits: an `auto` column grows
      // to its widest word, and a pasted URL as Ask Daoris's first words ran the box past the dock (ASKHIST1b).
      className="grid grid-cols-[minmax(0,1fr)] gap-2 border-t border-line px-4 py-3"
      onSubmit={(event) => { event.preventDefault(); say(); }}
      {...(live && attachments ? attach.handlers : {})}
    >
      {(refusal ?? refused) && <p role="alert" className="m-0 text-small text-ink-danger">{refusal ?? refused}</p>}
      {!live && writing && <p className="m-0 text-small text-ink-soft">{t('work.composer.over')}</p>}

      {live && queued.length > 0 && (
        <div className="grid grid-cols-[minmax(0,1fr)] gap-1">
          {/* Folded (WAITFOOT2c): one line with the count, so a park's answer is one box; a press opens the list. */}
          {fold ? (
            <div className="flex min-w-0 items-center gap-2 text-meta text-ink-faint">
              <span className="shrink-0 font-semibold text-ink-soft">{t('work.composer.waitCount', { n: queued.length })}</span>
              <span id={waitingLabel} className="min-w-0 truncate">{queuedLabel ?? t(taking && !opening ? 'work.composer.queued' : 'work.composer.opening')}</span>
              <Button
                type="button" variant="ghost" aria-expanded={listed}
                onClick={() => setListed(!listed)}
                className="ml-auto h-6 shrink-0 px-1.5 text-meta"
              >
                {t(listed ? 'work.composer.waitHide' : 'work.composer.waitShow')}
              </Button>
            </div>
          ) : (
            <span id={waitingLabel} className="text-meta text-ink-faint">{queuedLabel ?? t(taking && !opening ? 'work.composer.queued' : 'work.composer.opening')}</span>
          )}
          {(!fold || listed) && (
          <ol aria-labelledby={waitingLabel} className="m-0 grid list-none grid-cols-[minmax(0,1fr)] gap-1 p-0">
            {queued.map((message, index) => (
              <li
                key={`${index}:${message.text}`}
                className={WORDS_ROW}
              >
                <Words message={message} />
                {/* The level named only where it is not quiet (the design §8.2): the person's own word sent now. A word, never a hue. */}
                {message.level === 'now' && <span className="shrink-0"><Chip>{t('work.level.now')}</Chip></span>}
                {message.id && (onRemove || onEdit || (onLevel && leveled)) && (
                  <span className="flex shrink-0 items-center gap-1">
                    {onLevel && leveled && (
                      <Menu.Root>
                        <Menu.Trigger asChild>
                          <Button
                            type="button" variant="ghost"
                            aria-label={t('work.composer.levelItem', { text: message.text })}
                            className="h-6 w-6 justify-center px-0"
                          >
                            <Icon name="chevronDown" size={12} />
                          </Button>
                        </Menu.Trigger>
                        <Menu.Content align="end" className="min-w-40">
                          {message.level === 'ordinary' && (
                            <Menu.Item onSelect={() => { void changeLevel(message.id!, 'urgent'); }}>{t('work.queue.makeUrgent')}</Menu.Item>
                          )}
                          {message.level !== 'ordinary' && (
                            <Menu.Item onSelect={() => { void changeLevel(message.id!, 'ordinary'); }}>{t('work.queue.makeOrdinary')}</Menu.Item>
                          )}
                          {levels?.steers && message.level !== 'now' && (
                            <Menu.Item onSelect={() => { void changeLevel(message.id!, 'now'); }}>{t('work.steer.now')}</Menu.Item>
                          )}
                        </Menu.Content>
                      </Menu.Root>
                    )}
                    {onEdit && (
                      <Button
                        type="button" variant="ghost"
                        aria-label={t('work.composer.editItem', { text: message.text })}
                        onClick={() => startEdit(message)}
                        className="h-6 px-1.5 text-meta"
                      >
                        {t('work.composer.edit')}
                      </Button>
                    )}
                    {onRemove && (
                      <Button
                        type="button" variant="ghost"
                        aria-label={t('work.composer.removeItem', { text: message.text })}
                        onClick={() => { void remove(message.id!); }}
                        className="h-6 px-1.5 text-meta"
                      >
                        {t('work.composer.remove')}
                      </Button>
                    )}
                  </span>
                )}
              </li>
            ))}
          </ol>
          )}
          {taken &&<p role="status" className="m-0 text-small text-ink-soft">{t('work.composer.taken')}</p>}
          {/* The keeping stop (the design §3.2): its own press beside what waits for the turn's end, asking once, as a stop does. */}
          {keeps && (asking
            ? (
              <InlineConfirm
                label={t('work.steer.stopKeeping')}
                says={t('work.steer.stopKeepingAsk')}
                meanIt={t('work.steer.stopKeepingConfirm')}
                busy={stopping}
                onConfirm={(answered) => onKeepingStop?.(answered)}
                onClose={() => setAsking(false)}
              />
            )
            : (
              <span>
                <Button type="button" variant="ghost" disabled={stopping} onClick={() => setAsking(true)} className="h-6 px-1.5 text-meta">
                  {t('work.steer.stopKeeping')}
                </Button>
              </span>
            ))}
        </div>
      )}

      {/* Words on their way (SENDLOST1b), nearest the box they left, and shown whatever the session does meanwhile: the
          frame lets them go once the session holds them, or puts them back in the box. No control: they are not the
          driver's to take back yet. */}
      {going && going.words.length > 0 && (
        <div className="grid grid-cols-[minmax(0,1fr)] gap-1">
          <span id={goingLabel} className="text-meta text-ink-faint">
            {t(going.late ? 'work.composer.unanswered' : 'work.composer.sending')}
          </span>
          <ol aria-labelledby={goingLabel} className="m-0 grid list-none grid-cols-[minmax(0,1fr)] gap-1 p-0">
            {going.words.map((message, index) => (
              <li key={`${index}:${message.text}`} className={WORDS_ROW}>
                <Words message={message} />
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
        <p role="status" className="m-0 text-small text-ink-danger">
          {t(`carry.${attach.leftOff}`, { max: MAX_FILES, size: size(MAX_FILE_BYTES) })}
        </p>
      )}

      {editing && <p role="status" className="m-0 text-small text-ink-soft">{t('work.composer.editing')}</p>}
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
            While a turn runs it is *Send* at the person's default, its tip saying what the door does (COWORK1a), or, with no
            levels known, *queue*, because that is what a press does then. Absent once the session has ended, where a send
            could only ever be a dead press (UX5 U9). */}
        {live && (taking
          ? (
            <Tip content={t(!levels ? 'work.composer.queueTip' : levels.nextStep ? 'work.composer.sendNextStepTip' : 'work.composer.sendTurnEndTip')}>
              <Button type="submit" variant="primary" disabled={(!text.trim() && files.length === 0) || sending}>
                {t(levels ? 'work.composer.send' : 'work.composer.queue')}
              </Button>
            </Tip>
          )
          : textDoor
            ? (
              <Tip content={t('work.composer.levelNoTurns')}>
                <Button type="submit" variant="primary" disabled={(!text.trim() && files.length === 0) || sending}>
                  {sendLabel ?? t('work.composer.send')}
                </Button>
              </Tip>
            )
            : (
              <Button type="submit" variant="primary" disabled={(!text.trim() && files.length === 0) || sending}>
                {sendLabel ?? t('work.composer.send')}
              </Button>
            ))}
        {/* The levels beside *Send* (COWORK1a, the design §8.1), each sending the box's words at its level. */}
        {leveled && levels && (
          <Menu.Root>
            <Menu.Trigger asChild>
              <Button type="button" aria-label={t('work.composer.level')} disabled={!hasWords || sending} className="h-7 w-7 justify-center px-0">
                <Icon name="chevronDown" size={14} />
              </Button>
            </Menu.Trigger>
            <Menu.Content align="start" className="w-72">
              <Menu.Label>{t('work.composer.level')}</Menu.Label>
              <Menu.Item onSelect={() => sendAt('ordinary')}>{t('work.composer.levelOrdinary')}</Menu.Item>
              <Menu.Item onSelect={() => sendAt('urgent')}>
                <span className="grid min-w-0">
                  <span>{t('work.composer.levelUrgent')}</span>
                  {!levels.nextStep && <span className="text-meta text-ink-faint">{t('work.steer.notNowTurnEnd')}</span>}
                </span>
              </Menu.Item>
              <Menu.Item disabled={!levels.steers} onSelect={() => sendAt('now')}>
                <span className="grid min-w-0">
                  <span>{t('work.composer.levelNow')}</span>
                  <span className="text-meta text-ink-faint">
                    {t(levels.steers ? 'work.steer.nowTip' : levels.nextStep ? 'work.steer.notNowNextStep' : 'work.steer.notNowTurnEnd')}
                  </span>
                </span>
              </Menu.Item>
            </Menu.Content>
          </Menu.Root>
        )}
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
            {onStop && (
              <Tip content={t('work.composer.stopTip')}>
                <Button type="button" variant="danger" onClick={onStop}>{t('work.composer.stop')}</Button>
              </Tip>
            )}
          </>
        )}
        {/* The conversation's model and effort (AGT6b), and how full its context is (CONV5), at the
            row's far end, where the reference keeps them. */}
        {(context || (live && offered && offered.length > 0 && onOption)) && (
          <span className="ml-auto flex flex-wrap items-center gap-2">
            {live && offered && onOption && <SessionOptions options={offered} busy={optionsBusy} onChange={onOption} />}
            {context && <ContextRing usage={context.usage} door={context.door} />}
          </span>
        )}
      </div>
    </form>
  );
}
