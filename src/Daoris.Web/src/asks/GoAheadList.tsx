import { useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { GoAhead } from '../api';
import { ago } from '../format';
import { Button, Pill } from '../ui';
import { type Answered, Refused } from '../work/InlineConfirm';

/** A yes or no to a go-ahead: its number, the answer, the person's words if any, and how the answer ended (UXFIX2b3b). */
export type GoAheadAnswer = (number: number, approved: boolean, words: string | undefined, answered: Answered) => void;

/** A go-ahead's state on its pill: waiting is the person's to answer, approved done, refused declined. */
const GO_AHEAD_TONE: Record<GoAhead['state'], 'open' | 'done' | 'declined'> = {
  asked: 'open',
  approved: 'done',
  refused: 'declined',
};

/** The kinds the service names, which this page has words for; another is shown as the service wrote it. */
const KINDS = new Set(['write', 'release', 'push', 'sign-in', 'run']);

/**
 * **The go-aheads an ask holds** (KNOWUSE1a, D135 §2): what its sessions asked the person for, each once, by its number —
 * the act by its kind, where it lands and what it touches; what became of it, with the person's words on an answer
 * verbatim; why the first session needed it, in its words; and how many more asked again. One waiting is answered here,
 * yes or no with words if any; one answered is answered again only once asked to, since the first answer was meant.
 *
 * @remarks
 * Where it lands and what it touches are the session's words, shown as written; the kind is the service's word, in the
 * page's language. `daoris-driver ask --go-ahead` is the terminal's twin (D50). **Props only, no hook** (components §2):
 * without `onAnswer` there is no door, and nothing offers one.
 *
 * The same list stands on a parked session's page (KNOWUSE1a2), the go-aheads that session asked, where answering the
 * last one open sends the session on (GOAHEAD2b): its lead says so there, handed in as `lead`.
 */
export function GoAheadList({ goAheads, busy = false, onAnswer, lead }: {
  goAheads: GoAhead[];
  busy?: boolean;
  /**
   * The person's yes or no to go-ahead `number`, with their words where they gave any, and how to tell the item how it ended
   * (UXFIX2b3b); absent where there is no door.
   */
  onAnswer?: GoAheadAnswer;
  /** What the list's line above it says where it stands: the ask's own, absent. */
  lead?: string;
}) {
  const { t } = useTranslation();
  return (
    <>
      <p className="m-0 mb-2 text-small text-ink-soft">{lead ?? t('asks.goAhead.lead')}</p>
      <ul className="m-0 grid list-none gap-3 p-0">
        {goAheads.map((goAhead) => (
          <GoAheadItem key={goAhead.number} goAhead={goAhead} busy={busy} onAnswer={onAnswer} kindWord={
            KINDS.has(goAhead.kind) ? t(`asks.goAhead.kind.${goAhead.kind}`) : goAhead.kind
          } />
        ))}
      </ul>
    </>
  );
}

/**
 * One go-ahead. Its yes and no are direct presses, not an ask (platform UX §4: a yes or no every session is handed is not a
 * destructive edit), so they wait in place and a refused answer is said beside this item (UXFIX2b3b), the words kept and
 * both presses pressable again.
 */
function GoAheadItem({ goAhead, kindWord, busy, onAnswer }: {
  goAhead: GoAhead;
  kindWord: string;
  busy: boolean;
  onAnswer?: GoAheadAnswer;
}) {
  const { t } = useTranslation();
  const [changing, setChanging] = useState(false);
  const [words, setWords] = useState('');
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const item = useRef<HTMLLIElement>(null);
  const field = useRef<HTMLInputElement>(null);
  const alive = useRef(true);
  const turn = useRef(0);
  const refusalId = useId();
  const first = goAhead.asked[0];
  const more = goAhead.asked.length - 1;
  const answering = onAnswer !== undefined && (goAhead.state === 'asked' || changing);
  const waiting = busy || pending;
  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; };
  }, []);
  const answer = (approved: boolean) => {
    const mine = ++turn.current;
    // The presses wait disabled, which would drop the focus to the page: it stays in the item, on its words.
    if (item.current?.contains(document.activeElement)) field.current?.focus({ preventScroll: true });
    setRefusal(null);
    setPending(true);
    onAnswer!(goAhead.number, approved, words.trim() || undefined, {
      done: () => {
        if (mine !== turn.current || !alive.current) return;
        setPending(false);
        setChanging(false);
        setWords('');
        // The words and presses go as it closes: the focus stays on the item rather than falling to the page.
        if (item.current?.contains(document.activeElement)) item.current.focus({ preventScroll: true });
      },
      refused: (sentence) => {
        if (mine !== turn.current || !alive.current) return;
        setPending(false);
        setRefusal(sentence);
      },
    });
  };

  return (
    <li ref={item} tabIndex={-1} className="grid gap-1 rounded-control border border-line bg-raised px-3 py-2 outline-none">
      <p className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
        <span className="font-mono text-meta text-ink-faint">#{goAhead.number}</span>
        <Pill tone={GO_AHEAD_TONE[goAhead.state]}>{t(`asks.goAhead.state.${goAhead.state}`)}</Pill>
        <span className="text-body font-semibold">{t('asks.goAhead.act', { kind: kindWord, on: goAhead.on })}</span>
        <span className="min-w-0 break-words text-body">“{goAhead.act}”</span>
      </p>
      {first && <p className="m-0 text-small text-ink-soft">{first.why}</p>}
      <p className="m-0 font-mono text-meta text-ink-faint">
        {first && t('asks.goAhead.asked', { ago: ago(first.at) })}
        {more > 0 && ` · ${t('asks.goAhead.again', { count: more })}`}
        {goAhead.near != null && ` · ${t('asks.goAhead.near', { near: goAhead.near })}`}
      </p>
      {goAhead.answer && (
        <p className="m-0 flex flex-wrap items-baseline gap-x-2 text-small">
          <span className="text-ink-faint">{t(goAhead.answer.approved ? 'asks.goAhead.approvedAgo' : 'asks.goAhead.refusedAgo', { ago: ago(goAhead.answer.at) })}</span>
          {goAhead.answer.words && <q className="whitespace-pre-wrap text-ink">{goAhead.answer.words}</q>}
          {onAnswer && !changing && (
            <Button variant="ghost" disabled={busy} onClick={() => setChanging(true)}>{t('asks.goAhead.change')}</Button>
          )}
        </p>
      )}
      {answering && (
        <div className="mt-1 flex flex-wrap items-center gap-2">
          <input
            ref={field}
            aria-label={t('asks.goAhead.words')}
            placeholder={t('asks.goAhead.words')}
            value={words}
            onChange={(e) => setWords(e.target.value)}
            className="min-h-[1.9rem] flex-1 basis-56 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
          <Button disabled={waiting} aria-describedby={refusal ? refusalId : undefined} onClick={() => answer(true)}>
            {t('asks.goAhead.approve')}
          </Button>
          <Button variant="danger" disabled={waiting} aria-describedby={refusal ? refusalId : undefined} onClick={() => answer(false)}>
            {t('asks.goAhead.refuse')}
          </Button>
          {changing && <Button variant="ghost" disabled={waiting} onClick={() => setChanging(false)}>{t('common.cancel')}</Button>}
          {/* Drawn from the first, so it is a live region before it speaks (UXFIX2c). */}
          <span role="status" aria-live="polite" className={pending ? 'self-center text-small text-ink-faint' : 'sr-only'}>
            {pending ? t('work.confirm.pending') : null}
          </span>
          {refusal && <Refused id={refusalId} sentence={refusal} />}
        </div>
      )}
    </li>
  );
}
