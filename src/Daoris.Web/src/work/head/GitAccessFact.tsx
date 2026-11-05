import type { TFunction } from 'i18next';
import { useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { gitAccessSummary, looseness, type ResolvedGitClass, valueName, valuesOf } from '../../settings/repository/GitAccess';
import { type Answered, Refused } from '../InlineConfirm';
import type { DetailRow } from './DetailsFold';

/** What one running session holds of git access now (`STATE`'s `sessionGitAccess`): each class with its value and where it came from. */
export type SessionGitAccess = { session: string; repository?: string | null; workspace?: string | null; classes: ResolvedGitClass[] };

/**
 * What the frame hands the head for a session's git access (D241 point 7): every record the shell pushed, of which the session's own is
 * drawn, and the press that tightens it with its pending. Absent where nothing reads them (a browser).
 */
export type SessionGitAccessHold = {
  records: readonly SessionGitAccess[];
  onTighten?: (session: string, access: Record<string, string>, answered: Answered) => void;
  busy?: boolean;
};

const CLASSES = ['read', 'write', 'dangerous'] as const;

/**
 * **A session's git access, in one fact** (D241 point 7; the git design §3.1 G5): what the session holds now, as its record says it —
 * its repository's access narrowed by what the person tightened — in the words of Setup's git access summary, with *Tighten…* beside it.
 *
 * @remarks
 * **Only a narrowing is offered**: for each class, the values tighter than it holds, one press each, sent at once with no confirmation,
 * since a tightening gives nothing away; a loosening is a change of git access at its level, which Setup confirms. A refusal is said
 * under the fact in the driver's sentence. Props only (components §2): the frame holds the press and its pending.
 */
export function GitAccessFact({ record, onTighten, busy = false }: {
  record: SessionGitAccess;
  onTighten?: SessionGitAccessHold['onTighten'];
  busy?: boolean;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const alive = useRef(true);
  const refusalId = useId();
  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; };
  }, []);
  const held = (name: string) => record.classes.find((each) => each.class === name);
  const options = CLASSES.flatMap((name) => {
    const now = held(name);
    return now ? valuesOf(name).filter((value) => looseness(value) > looseness(now.value)).map((value) => ({ name, value })) : [];
  });
  const tightened = record.classes.some((each) => each.source === 'session');
  const press = (access: Record<string, string>) => {
    setRefusal(null);
    setPending(true);
    onTighten!(record.session, access, {
      done: () => { if (alive.current) { setPending(false); setOpen(false); } },
      refused: (sentence) => { if (alive.current) { setPending(false); setRefusal(sentence); } },
    });
  };

  return (
    <span data-git-access={record.session} className="grid gap-1">
      <span className="flex flex-wrap items-center gap-2">
        <span>{gitAccessSummary(t, record.classes)}{tightened ? ` · ${t('work.gitAccess.tightened')}` : ''}</span>
        {onTighten && options.length > 0 && !open && (
          <Button variant="ghost" disabled={busy || pending} onClick={() => setOpen(true)}>{t('work.gitAccess.tighten')}</Button>
        )}
      </span>
      {onTighten && open && (
        <span className="flex flex-wrap items-center gap-2">
          {options.map(({ name, value }) => (
            <Button key={`${name}:${value}`} variant="ghost" disabled={busy || pending} onClick={() => press({ [name]: value })}>
              {t('work.gitAccess.set', { class: t(`settings.git.access.class.${name}`, { defaultValue: name }), value: valueName(t, value) })}
            </Button>
          ))}
          <Button variant="ghost" disabled={pending} onClick={() => { setOpen(false); setRefusal(null); }}>{t('work.gitAccess.cancel')}</Button>
        </span>
      )}
      {refusal && <Refused id={refusalId} sentence={refusal} />}
    </span>
  );
}

/** *Details*' *Git access* row for a session: its record's fact; none where the shell pushed no record for it (a session begun before the record). */
export function gitAccessRow(t: TFunction, session: { id: string }, hold?: SessionGitAccessHold | null): DetailRow {
  const record = hold?.records.find((each) => each.session === session.id);
  return {
    label: t('work.gitAccess.details'),
    value: record ? <GitAccessFact record={record} onTighten={hold!.onTighten} busy={hold!.busy} /> : null,
  };
}
