import { useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Button } from '../../design/ui';
import { cn } from '../../lib/cn';
import { type Answered, Refused } from '../InlineConfirm';
import { InConversation, SOUGHT } from './WaitAnchor';

/**
 * One ask a session made of a helper (MIXAGENT2b, D163 point 2), as `SESSION_HELPERS` answers it: the ask as written, who asked
 * (`agent` through `helper_ask`, `person` through *Ask another agent…* or the terminal), and where the driver's look left it, its
 * state and code (MIXAGENT2a-i's shapes), the helper's session once started and the agent it started on. A state or a code a newer
 * driver writes is read as written.
 */
export type HelperAskRow = {
  id: string;
  question: string;
  /** The agent the ask named, one word; null where the driver chooses. */
  agent: string | null;
  askedBy: string;
  at: string;
  /** `waiting`, `approved`, `started`, `answered` or `refused`; null before the driver's look read it. */
  state: string | null;
  /** A refusal's code, or what a waiting ask waits on (`cap`, or the agent walk's); null where it waits on the person's yes. */
  code: string | null;
  helper: string | null;
  startedOn: string | null;
};

/**
 * One agent's ask that waits on the person's word, as `HELPER_WAITS` answers it across this machine's sessions (MIXAGENT2c):
 * *What needs you*'s row, with the session that asked.
 */
export type HelperWait = { id: string; session: string; question: string; agent: string | null; at: string };

/**
 * The person's yes or no to one ask, told how it ended (UXFIX2): `done` once the driver's file holds it, `refused` with why not.
 */
export type HelperApprove = (ask: string, answered: Answered) => void;

/**
 * Whether an ask waits on the person's yes (D163 point 3; design §5): an agent's ask under the `helpers` rule's `ask`, which the
 * look holds `waiting` with no code, or one it has not read yet, which the yes lets go all the same. Under `on` the look starts it
 * and under `off` refuses it, whatever the yes said.
 */
export const waitsOnYes = (row: Pick<HelperAskRow, 'askedBy' | 'state' | 'code'>) =>
  row.askedBy === 'agent' && (row.state === null || (row.state === 'waiting' && row.code === null));

/**
 * Whether the driver's look holds an ask waiting on the person's word (HELPERNO1, D163 point 3): an agent's ask under the rule's
 * `ask`, `waiting` with no code. Only there is a no offered, since one the look has not read yet may be starting under `on`; and
 * only there is it one of the session's waits (MIXAGENT2c, D168), since that is what the driver says waits on the person.
 */
export const waitsOnWord = (row: Pick<HelperAskRow, 'askedBy' | 'state' | 'code'>) =>
  row.askedBy === 'agent' && row.state === 'waiting' && row.code === null;

/**
 * The asks still on their way: not yet answered or refused. A settled one is said where it settled, the answer as the session's
 * next turn and a refusal in its timeline's note (MIXAGENT2a-ii), and its helper is a row under the session in Sessions' list
 * (MIXAGENT3a-ii), so the page does not keep a second copy of it.
 */
export const openAsks = (asks: readonly HelperAskRow[]) => asks.filter((row) => row.state !== 'answered' && row.state !== 'refused');

/** What the frame hands the head for a session's helpers: its asks, the agents' names, and the presses it can make. */
export type HelperWaits = {
  asks: readonly HelperAskRow[];
  nameOf: (agent: string) => string;
  onApprove?: HelperApprove;
  /** The person's no (HELPERNO1). */
  onDecline?: HelperApprove;
  onOpen?: (helper: string) => void;
  busy?: boolean;
};

/** The codes of a refusal the timeline's notes already word (`note.helper.why.*`, MIXAGENT2a-ii); any other is said as written. */
const WHY = new Set([
  'off', 'asker-gone', 'asker-unfit', 'too-many', 'unreadable', 'not-started', 'no-answer', 'conversation', 'cannot-take', 'declined',
]);

/** Where one ask stands, in a line: who asked, the agent it named until one started, and its state in the person's words. */
export function helperLine(t: TFunction, row: HelperAskRow, nameOf: (agent: string) => string): string {
  const by = t(row.askedBy === 'person' ? 'work.helper.by.person' : 'work.helper.by.agent');
  const named = row.agent && !row.startedOn ? t('work.helper.of', { agent: nameOf(row.agent) }) : null;
  const agent = row.startedOn ? nameOf(row.startedOn) : row.agent ? nameOf(row.agent) : t('work.helper.someAgent');
  const state = (() => {
    switch (row.state) {
      case 'started': return t('work.helper.state.started', { agent });
      case 'answered': return t('work.helper.state.answered', { agent });
      case 'refused': {
        const why = row.code && WHY.has(row.code) ? t(`note.helper.why.${row.code}`) : row.code ?? '';
        return t('work.helper.state.refused', { why });
      }
      case 'approved': return [t('work.helper.state.approved'), t('work.helper.state.next')].join(' · ');
      case 'waiting':
        if (row.code === null) return t(row.askedBy === 'agent' ? 'work.helper.state.yours' : 'work.helper.state.next');
        return t(row.code === 'cap' ? 'work.helper.state.cap' : 'work.helper.state.agent');
      default:
        return t(waitsOnYes(row) ? 'work.helper.state.yours' : 'work.helper.state.next');
    }
  })();
  return [by, named, state].filter(Boolean).join(' · ');
}

/**
 * **The helpers a session asked for, on its page** (MIXAGENT2b, D163 points 2 and 3; the mixed-agents design §3, §5): each ask's
 * question as it was written (content, shown as it is), then who asked and where the driver's look left it, in the person's
 * words. An agent's ask that waits on the person's yes offers *Let it ask*, the yes the look reads, and, once the look holds it
 * waiting on their word, *Not now*, their no (HELPERNO1), which refuses it with its note; a helper once started has a door to its
 * own session. Its answer is the asking session's next turn, in the conversation, so it is not said again here.
 *
 * @remarks
 * **Props only** (components §2): the frame reads the asks and holds the presses. Without `onApprove` no yes is offered, without
 * `onDecline` no no, and without `onOpen` no door: a browser, a story. Each waits in place and a refusal is said beside it
 * (UXFIX2b3b), as a rule's *Decline* does; nothing goes, so neither asks first.
 */
export function HelperAsks({ asks, nameOf, onApprove, onDecline, onOpen, busy = false, inConversation, marked }: {
  asks: readonly HelperAskRow[];
  /** An agent's name as the roster says it; its id where the roster does not hold it. */
  nameOf: (agent: string) => string;
  onApprove?: HelperApprove;
  onDecline?: HelperApprove;
  /** Attend a helper's own session. */
  onOpen?: (helper: string) => void;
  /** A yes or a no is on its way: every press waits for it. */
  busy?: boolean;
  /** The way from an ask to its `helper_ask` call in the conversation, by the ask's id (WAITFOOT1e). */
  inConversation?: Readonly<Record<string, () => void>>;
  /** The ask a raising block's *To the wait* led to, outlined (WAITFOOT1e). */
  marked?: string;
}) {
  return (
    <ul className="m-0 grid list-none gap-2 p-0">
      {asks.map((row) => (
        <HelperAskLine
          key={row.id} row={row} nameOf={nameOf} onApprove={onApprove} onDecline={onDecline} onOpen={onOpen} busy={busy}
          onConversation={inConversation?.[row.id]} marked={marked === row.id}
        />
      ))}
    </ul>
  );
}

function HelperAskLine({ row, nameOf, onApprove, onDecline, onOpen, busy, onConversation, marked }: {
  row: HelperAskRow;
  nameOf: (agent: string) => string;
  onApprove?: HelperApprove;
  onDecline?: HelperApprove;
  onOpen?: (helper: string) => void;
  busy: boolean;
  onConversation?: () => void;
  marked: boolean;
}) {
  const { t } = useTranslation();
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const alive = useRef(true);
  const turn = useRef(0);
  const refusalId = useId();
  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; };
  }, []);
  const yes = onApprove && waitsOnYes(row);
  const no = onDecline && waitsOnWord(row);
  const door = onOpen && row.helper && (row.state === 'started' || row.state === 'answered') ? row.helper : null;
  // The yes and the no share one pending and one refusal line: they answer the same ask.
  const press = (answer: HelperApprove) => {
    const mine = ++turn.current;
    setRefusal(null);
    setPending(true);
    answer(row.id, {
      done: () => { if (mine === turn.current && alive.current) setPending(false); },
      refused: (sentence) => {
        if (mine !== turn.current || !alive.current) return;
        setPending(false);
        setRefusal(sentence);
      },
    });
  };

  return (
    <li
      tabIndex={-1}
      data-helper={row.id}
      className={cn('grid gap-1 rounded-control border border-line bg-raised px-3 py-2', marked ? SOUGHT : 'outline-none')}
    >
      <p className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
        <span className="min-w-0 whitespace-pre-wrap break-words text-body">{row.question}</span>
        {onConversation && <span className="ml-auto"><InConversation onPress={onConversation} /></span>}
      </p>
      <p className="m-0 text-small text-ink-soft">{helperLine(t, row, nameOf)}</p>
      {(yes || no || door) && (
        <div className="mt-1 flex flex-wrap items-center gap-2">
          {yes && (
            <Button disabled={busy || pending} aria-describedby={refusal ? refusalId : undefined} onClick={() => press(onApprove!)}>
              {t('work.helper.approve')}
            </Button>
          )}
          {no && (
            <Button variant="ghost" disabled={busy || pending} aria-describedby={refusal ? refusalId : undefined} onClick={() => press(onDecline!)}>
              {t('work.helper.decline')}
            </Button>
          )}
          {door && <Button variant="ghost" onClick={() => onOpen!(door)}>{t('work.helper.open')}</Button>}
          {/* Drawn from the first, so it is a live region before it speaks (UXFIX2c). */}
          {(yes || no) && (
            <span role="status" aria-live="polite" className={pending ? 'self-center text-small text-ink-faint' : 'sr-only'}>
              {pending ? t('work.confirm.pending') : null}
            </span>
          )}
          {refusal && <Refused id={refusalId} sentence={refusal} />}
        </div>
      )}
    </li>
  );
}
