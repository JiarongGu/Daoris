import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField } from '../../design/ui';

/** One session bound into an operation, pinned at the press (`TOGETHER`). */
export type TogetherMemberView = { session: string; branch: string; tip: string };

/** An operation bringing sessions together as `TOGETHER` answers it for one session (LINESTOGETHER1d, the review-flow design §4). */
export type TogetherView = {
  id: string;
  phase: string;
  /** Whether the session asked about is the head the others are brought into, or one of them. */
  role: 'head' | 'member' | string;
  head: string;
  members: TogetherMemberView[];
  /** What its last check failed on, or why it went stale; null where nothing did. */
  why: string | null;
  /** Where it landed, a branch or the line, once known. */
  into: string | null;
  line: boolean;
};

const PHASES = ['asked', 'replaying', 'checking', 'ready', 'landing', 'uncertain', 'landed', 'stale', 'let-go'];

const BEFORE_LANDING = ['asked', 'replaying', 'checking', 'ready'];

const DOOR = 'min-w-0 cursor-pointer border-0 bg-transparent p-0 font-mono text-meta underline decoration-line-strong underline-offset-2 hover:text-accent hover:decoration-accent';

/**
 * *Bring them together…* on a head's Session step (LINESTOGETHER1d): the sessions beside it ticked and asked into it, then the
 * operation's phase on the head and on each member, *Let them go* before it lands, *Ask again* after a failed check, and the head's
 * one Accept once it is ready. A member reads where it was brought into, never that it landed. Nothing to say, and nothing is drawn.
 */
export function TogetherDoor({ candidates, operation, busy = false, said, onBring, onLetGo, onAskAgain, onAccept, onSession }: {
  candidates: readonly string[];
  operation: TogetherView | null;
  busy?: boolean;
  said?: string | null;
  onBring?: (members: string[]) => void;
  onLetGo?: () => void;
  onAskAgain?: () => void;
  onAccept?: () => void;
  onSession?: (id: string) => void;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [ticked, setTicked] = useState<string[]>([]);
  const door = (id: string) => (onSession
    ? <button type="button" aria-label={t('work.related.door', { id })} onClick={() => onSession(id)} className={DOOR}>{id}</button>
    : <span className="font-mono text-meta">{id}</span>);
  const phase = (code: string) => (PHASES.includes(code) ? t(`work.together.phase.${code}`) : code);
  const note = said ? <span role="status" className="text-ink-faint">{said}</span> : null;

  if (operation) {
    const live = operation.phase !== 'landed' && operation.phase !== 'stale' && operation.phase !== 'let-go';
    if (operation.role !== 'head') {
      return (
        <section aria-label={t('work.together.title')} className="flex min-w-0 flex-wrap items-baseline gap-x-1.5 text-small text-ink-soft">
          {operation.phase === 'landed' && operation.into
            ? <span>{t(operation.line ? 'work.together.broughtIntoLine' : 'work.together.broughtInto', { into: operation.into })}</span>
            : <span>{t('work.together.member', { phase: phase(operation.phase) })}</span>}
          {door(operation.head)}
          {note}
        </section>
      );
    }

    return (
      <section aria-label={t('work.together.title')} className="grid min-w-0 gap-0.5 text-small text-ink-soft">
        <span className="flex min-w-0 flex-wrap items-baseline gap-x-1.5">
          <span>{t('work.together.head', { count: operation.members.length, phase: phase(operation.phase) })}</span>
          {operation.members.map((member) => <span key={member.session}>{door(member.session)}</span>)}
        </span>
        {operation.why && live && <span className="text-ink-faint">{t('work.together.why', { why: operation.why })}</span>}
        {operation.phase === 'landed' && operation.into && (
          <span>{t(operation.line ? 'work.together.landedLine' : 'work.together.landed', { into: operation.into })}</span>
        )}
        <span className="flex flex-wrap gap-1.5">
          {operation.phase === 'ready' && onAccept && <Button className="px-1.5 py-0 text-small" disabled={busy} onClick={onAccept}>{t('work.together.accept')}</Button>}
          {operation.phase === 'checking' && operation.why && onAskAgain && (
            <Button variant="ghost" className="px-1.5 py-0 text-small" disabled={busy} onClick={onAskAgain}>{t('work.together.askAgain')}</Button>
          )}
          {BEFORE_LANDING.includes(operation.phase) && onLetGo && (
            <Button variant="ghost" className="px-1.5 py-0 text-small" disabled={busy} onClick={onLetGo}>{t('work.together.letGo')}</Button>
          )}
        </span>
        {note}
      </section>
    );
  }

  if (candidates.length === 0 || !onBring) return note;
  if (!open) {
    return (
      <span className="flex min-w-0 flex-wrap items-baseline gap-x-1.5 text-small">
        <Button variant="ghost" className="px-1.5 py-0 text-small" disabled={busy} onClick={() => setOpen(true)}>{t('work.together.open')}</Button>
        {note}
      </span>
    );
  }

  const tick = (id: string, on: boolean) => setTicked((now) => (on ? [...now, id] : now.filter((each) => each !== id)));
  return (
    <section aria-label={t('work.together.title')} className="grid min-w-0 gap-0.5 text-small text-ink-soft">
      <span>{t('work.together.choose')}</span>
      <ul className="m-0 grid list-none gap-0.5 p-0">
        {candidates.map((id) => (
          <li key={id} className="m-0">
            <CheckField checked={ticked.includes(id)} onChange={(on) => tick(id, on)} label={id} className="font-mono text-meta" />
          </li>
        ))}
      </ul>
      <span className="flex flex-wrap gap-1.5">
        <Button className="px-1.5 py-0 text-small" disabled={busy || ticked.length === 0} onClick={() => onBring(ticked)}>{t('work.together.bring')}</Button>
        <Button variant="ghost" className="px-1.5 py-0 text-small" onClick={() => { setOpen(false); setTicked([]); }}>{t('work.together.cancel')}</Button>
      </span>
      {note}
    </section>
  );
}
