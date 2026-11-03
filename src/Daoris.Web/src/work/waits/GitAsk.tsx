import { useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { cn } from '../../lib/cn';
import { type GitAskWait, gitAskWorded } from '../attention/attention';
import { type Answered, Refused } from '../InlineConfirm';
import { SOUGHT } from './WaitAnchor';

/** A git go-ahead as *What needs you* pushes it (GITAUTH1f): its id, since when it waited, and the act its door holds. */
export type GitAskRow = { id: string; since?: string | null; gitAsk: GitAskWait };

/** The person's Allow (`allow`) or Refuse of one git go-ahead, and how it ended. */
export type GitAskAnswer = (ask: GitAskRow, allow: boolean, answered: Answered) => void;

/**
 * What the frame hands the head and the foot's strip for the git go-aheads (GITAUTH1f, the git auth design §7.4): every one the shell
 * pushed, of which the session's own are drawn, the answer's one door and its pending. Absent where nothing reads them (a browser).
 */
export type GitAskWaits = {
  waits: readonly GitAskRow[];
  onAnswer?: GitAskAnswer;
  busy?: boolean;
};

/** The git go-aheads a session's marker named, oldest first, as they were pushed. */
export const gitAsksFor = (waits: readonly GitAskRow[], session: string) => waits.filter((wait) => wait.gitAsk.session === session);

/**
 * **A git go-ahead, on its session** (GITAUTH1f, D198 point 12; the git auth design §6.6): Daoris's git door holds one act on a remote
 * whose class is ask, a push or a fetch, while it waits; *Allow* lets that one act through and *Refuse* refuses it with one line. Where
 * the act has gone, an Allow is kept for its identical retry. The row in *What needs you* says the same and presses the same door.
 *
 * @remarks
 * **Props only** (components §2): the frame holds the answer and its pending. Without `onAnswer` no press is drawn. A refusal of the
 * answer is said beside it (UXFIX2b3b). Its words never say "waiting on you", which a parked session's are (`person-waits`).
 */
export function GitAsk({ ask, onAnswer, busy = false, marked = false }: {
  ask: GitAskRow;
  onAnswer?: GitAskAnswer;
  busy?: boolean;
  /** A press led here: the wait is outlined. */
  marked?: boolean;
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
  const press = (allow: boolean) => {
    const mine = ++turn.current;
    setRefusal(null);
    setPending(true);
    onAnswer!(ask, allow, {
      done: () => { if (mine === turn.current && alive.current) setPending(false); },
      refused: (sentence) => {
        if (mine !== turn.current || !alive.current) return;
        setPending(false);
        setRefusal(sentence);
      },
    });
  };
  const worded = gitAskWorded(ask.gitAsk, ask.since, t);

  return (
    <div
      tabIndex={-1}
      data-wait="gitAsk"
      className={cn('grid gap-1 rounded-control border border-line bg-raised px-3 py-2', marked ? SOUGHT : 'outline-none')}
    >
      <p className="m-0 text-body">{worded.title}</p>
      {worded.detail && <p className="m-0 text-small text-ink-soft">{worded.detail}</p>}
      {onAnswer && (
        <div className="mt-1 flex flex-wrap items-center gap-2">
          <Button variant="primary" disabled={busy || pending} aria-describedby={refusal ? refusalId : undefined} onClick={() => press(true)}>
            {t('work.wait.gitAsk.allow')}
          </Button>
          <Button variant="ghost" disabled={busy || pending} aria-describedby={refusal ? refusalId : undefined} onClick={() => press(false)}>
            {t('work.wait.gitAsk.refuse')}
          </Button>
          <span role="status" aria-live="polite" className={pending ? 'self-center text-small text-ink-faint' : 'sr-only'}>
            {pending ? t('work.confirm.pending') : null}
          </span>
          {refusal && <Refused id={refusalId} sentence={refusal} />}
        </div>
      )}
    </div>
  );
}
