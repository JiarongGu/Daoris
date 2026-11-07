import { useContext, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { NotePart } from '../../data/api';
import { Button, Inline } from '../../design/ui';
import { cn } from '../../lib/cn';
import { Note } from '../Note';
import { noteText } from '../noteLines';
import { briefOffered, newSessionSaid, reasonOf } from '../say';
import { type Block, CONVERSATION_CODES, type Turn } from './conversation';
import { SaidDoors } from './saidDoors';

/** Never in a sentence: where a session's id goes, so the sentence can be cut around its door. */
const DOOR = '⁣';

/** Words waiting at a record's foot that no driver's note has settled: what a cool-off holds. */
const waitingWords = (turn: Turn) =>
  turn.items.filter((item) => item.kind === 'held' && item.reaches === 'resume' && !item.settled && !item.unreached);

/**
 * The person's words went to a new session: said in the page's words, the session a door to it, once,
 * where the words were shown. A reason the page cannot word leaves the driver's own line, which names it too.
 */
export function WentLine({ block }: { block: Block }) {
  const { t } = useTranslation();
  const { onSession, reasons } = useContext(SaidDoors);
  const why = block.why ? reasonOf(t, block.why, reasons) : null;
  if (!why || !block.to) return <NoteLine text={block.text ?? ''} />;
  const to = block.to;
  const [before, ...after] = t('work.conversation.went', { id: DOOR, why }).split(DOOR);

  return (
    <p className="m-0 text-small text-ink-soft">
      {before}
      {onSession
        ? (
          <button
            type="button"
            onClick={() => onSession(to)}
            className="cursor-pointer border-0 bg-transparent p-0 font-mono text-small text-ink underline decoration-line-strong underline-offset-2 hover:text-accent hover:decoration-accent"
          >
            {to}
          </button>
        )
        : <span className="font-mono">{to}</span>}
      {after.join(to)}
    </p>
  );
}

/** The conversation a `chat.handed-over` line names, or null for any other note. */
export const handedTo = (block: Block): string | null => {
  const part = block.parts?.find((each) => each.code === 'chat.handed-over');
  const session = part && 'values' in part ? part.values?.session : undefined;
  return typeof session === 'string' && session ? session : null;
};

/**
 * A conversation that went on with another agent: its own last line, worded by its parts, and where this
 * page can open a session, one press to the conversation it went on in, as a started session is a door in *went*.
 */
export function HandedLine({ block, to }: { block: Block; to: string }) {
  const { t } = useTranslation();
  const { onSession } = useContext(SaidDoors);
  return (
    <div className="grid justify-items-start gap-1.5">
      <NoteLine text={block.text ?? ''} parts={block.parts} />
      {onSession && <Button onClick={() => onSession(to)}>{t('work.handOver.open', { session: to })}</Button>}
    </div>
  );
}

/**
 * The person's words cannot go on in this session: why, in the page's words, and one press that starts
 * a conversation with them — the person's, since a new conversation has none of this one's context. The press only where
 * the page holds every word the note names, so what it starts with is what they said. Where the words wait for the person on a
 * reason that leaves a tree to go on in, or a driven session's tree is gone, *Go on with Daoris's brief*
 * goes first beside it: a new part of the same session, handed Daoris's brief, so it keeps the context the fresh start does not.
 */
export function CannotLine({ block }: { block: Block }) {
  const { t } = useTranslation();
  const { chat, onStartFrom, onGoOnBrief, reasons } = useContext(SaidDoors);
  const why = block.why ? reasonOf(t, block.why, reasons) : null;
  if (!why) return <NoteLine text={block.text ?? ''} parts={block.parts} />;
  const said = block.said;
  const brief = onGoOnBrief && briefOffered(block.why ?? '', chat) && said && said.length > 0;

  return (
    <div className="grid justify-items-start gap-1.5">
      <p className="m-0 text-small text-ink-soft">{t('work.say.cannot', { why })}</p>
      {(brief || (onStartFrom && said && said.length > 0)) && (
        <div className="flex flex-wrap gap-2">
          {brief && <Button onClick={() => onGoOnBrief!()}>{t('work.say.goOnBrief')}</Button>}
          {onStartFrom && said && said.length > 0 && (
            <Button onClick={() => onStartFrom(said)}>{t('work.say.startChat')}</Button>
          )}
        </div>
      )}
    </div>
  );
}

/**
 * A driver's note the page words itself: its code's sentence in the reader's language, said as the
 * went line is, where the word it names is shown. A code this page does not know leaves the driver's English line.
 */
export function CodedLine({ block }: { block: Block }) {
  const { t } = useTranslation();
  const { onSayAgain } = useContext(SaidDoors);
  const key = block.code ? CONVERSATION_CODES[block.code] : undefined;
  if (!key) return <NoteLine text={block.text ?? ''} />;
  // *Say it again*: the person's own words that may have reached the agent were never sent again; saying
  // them anew is theirs to press. Never Daoris's words nor another agent's.
  const again = block.code === 'lost' && block.persons && block.said?.length && onSayAgain ? block.said : null;
  if (!again) return <p className="m-0 text-small text-ink-soft">{t(key)}</p>;
  return (
    <div className="grid justify-items-start gap-1.5">
      <p className="m-0 text-small text-ink-soft">{t(key)}</p>
      <Button onClick={() => onSayAgain!(again)}>{t('work.conversation.sayAgain')}</Button>
    </div>
  );
}

/**
 * *Go on in a new session*, under words a cooling account holds: what it costs (the new session starts
 * without this conversation's context, so it is the person's press), the press, and then what it came to in the page's
 * words. Where nothing carries the words on by itself, a conversation or a closed quest's session, its press starts a
 * conversation with every word waiting. The press is offered again only for a choice the driver could not keep.
 */
export function NewSessionDoor({ turn }: { turn: Turn }) {
  const { t } = useTranslation();
  const { cooling, onStartFrom } = useContext(SaidDoors);
  const words = waitingWords(turn);
  if (!cooling?.onGoOnNew || words.length === 0) return null;
  const { answer, onGoOnNew, pending, quest } = cooling;
  const said = answer ? newSessionSaid(t, answer, { quest }) : null;
  const again = !answer || (!answer.sent && !answer.why);

  return (
    <div className="grid justify-items-start gap-1.5">
      {said && <p className="m-0 text-small text-ink-soft">{said.sentence}</p>}
      {again && (
        <>
          {!said && <p className="m-0 text-small text-ink-soft">{t('work.say.newSession.loses')}</p>}
          <Button onClick={onGoOnNew} disabled={pending}>{t('work.say.goOnNew')}</Button>
        </>
      )}
      {said?.startChat && onStartFrom && (
        <Button onClick={() => onStartFrom(words.map((word) => word.text ?? ''))}>{t('work.say.startChat')}</Button>
      )}
    </div>
  );
}

/** How long a driver's note may be before it shows two lines and the rest on a press. */
const LONG_NOTE = 240;

/**
 * The driver's own sentence about the session — a refusal, a missing connector. A long one shows two
 * lines and the rest on a press: records written before the refusal named its call carry the
 * request's JSON, four lines each, and nothing reads that JSON to shorten it.
 *
 * A note with parts (a refused sign-in, a landing's line, the opening line)
 * is worded from them as a session record's note is (`Note`): each coded line in the reader's language, and a
 * line the page cannot word as its record keeps it, marked. Its English is shown only where it has none, its backticks
 * drawn as code as a worded line's are, never as backticks.
 */
export function NoteLine({ text, parts, under }: { text: string; parts?: readonly NotePart[]; under?: readonly Block[] }) {
  const { t, i18n } = useTranslation();
  const { names } = useContext(SaidDoors);
  const worded = parts?.length ? noteText(t, { parts }, i18n.language, names) : text;
  const long = worded.length > LONG_NOTE;
  const [open, setOpen] = useState(!long);
  const [unfolded, setUnfolded] = useState(false);
  return (
    <div className="text-small text-ink-soft">
      <p className={cn('m-0 wrap-anywhere', !open && 'line-clamp-2')}>
        <span className="mr-1.5 text-meta uppercase tracking-[0.06em] text-ink-faint">{t('work.conversation.driver')}</span>
        {parts?.length ? <Note parts={parts} names={names} compact /> : <Inline text={text} />}
      </p>
      {long && (
        <button
          type="button"
          aria-expanded={open}
          onClick={() => setOpen((was) => !was)}
          className="border-0 bg-transparent p-0 text-meta text-ink-faint hover:text-ink"
        >
          {open ? t('work.conversation.hide') : t('work.conversation.show')}
        </button>
      )}
      {/* The opening line of a part that began by a move, folded under its boundary as a brief is. */}
      {under && under.length > 0 && (
        <>
          <button
            type="button"
            aria-expanded={unfolded}
            onClick={() => setUnfolded((was) => !was)}
            className="ml-2 border-0 bg-transparent p-0 text-meta text-ink-faint hover:text-ink"
          >
            {unfolded ? t('work.conversation.hide') : t('work.conversation.show')}
          </button>
          {unfolded && under.map((line) => <NoteLine key={line.key} text={line.text ?? ''} parts={line.parts} />)}
        </>
      )}
    </div>
  );
}
