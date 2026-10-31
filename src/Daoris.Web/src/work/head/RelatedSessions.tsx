import { Fragment } from 'react';
import { useTranslation } from 'react-i18next';
import { clockOf } from '../../locales/format';

/** One session beside another, as `SESSION_RELATED` answers it (the review-flow design §3). */
export type RelatedRow = {
  id: string;
  kind: string;
  quest: string | null;
  read: 'read' | 'not-read' | string;
  readAt: string | null;
  declaredOverlap: string[];
  changedOverlap: string[];
};

const DOOR = 'min-w-0 cursor-pointer border-0 bg-transparent p-0 font-mono text-meta underline decoration-line-strong underline-offset-2 hover:text-accent hover:decoration-accent';

/** Each path with the sessions that meet on it, in the order the read gave them. */
const byPath = (rows: readonly RelatedRow[], paths: (row: RelatedRow) => readonly string[]) => {
  const groups = new Map<string, string[]>();
  for (const row of rows) {
    if (row.read === 'not-read') continue;
    for (const path of paths(row)) groups.set(path, [...(groups.get(path) ?? []), row.id]);
  }
  return [...groups];
};

/**
 * The sessions beside one in its repository (LINESTOGETHER1c): the files they change and the paths they declare, told apart, each
 * session a door to its head where `onSession` is given and plain where it is not (the Integration tab's list is read-only), and a
 * session git could not read said *not read here* (D137). Nothing to say, and nothing is drawn.
 */
export function RelatedSessions({ repository, related, onSession }: {
  repository: string;
  related: readonly RelatedRow[];
  onSession?: (id: string) => void;
}) {
  const { t } = useTranslation();
  const changed = byPath(related, (row) => row.changedOverlap);
  const declared = byPath(related, (row) => row.declaredOverlap);
  const unread = related.filter((row) => row.read === 'not-read');
  if (changed.length + declared.length + unread.length === 0) return null;

  const door = (id: string) => (onSession
    ? <button type="button" aria-label={t('work.related.door', { id })} onClick={() => onSession(id)} className={DOOR}>{id}</button>
    : <span className="font-mono text-meta">{id}</span>);
  const line = (key: string, groups: [string, string[]][]) => groups.map(([path, ids]) => (
    <li key={`${key}:${path}`} className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-1.5">
      <span>{t(`work.related.${key}`, { count: ids.length })}</span>
      <code className="font-mono text-meta text-ink [overflow-wrap:anywhere]">{path}</code>
      <span className="flex flex-wrap gap-x-1.5">{ids.map((id) => <Fragment key={id}>{door(id)}</Fragment>)}</span>
    </li>
  ));

  return (
    <section aria-label={t('work.related.title', { repository })} className="grid min-w-0 gap-0.5 text-small text-ink-soft">
      <span className="font-semibold text-ink-faint">{t('work.related.title', { repository })}</span>
      <ul className="m-0 grid list-none gap-0.5 p-0">
        {line('changes', changed)}
        {line('declares', declared)}
        {unread.map((row) => (
          <li key={`unread:${row.id}`} className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-1.5">
            {door(row.id)}
            <span className="text-ink-faint">
              {row.readAt ? t('work.related.notReadSince', { at: clockOf(row.readAt) }) : t('work.related.notRead')}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}
