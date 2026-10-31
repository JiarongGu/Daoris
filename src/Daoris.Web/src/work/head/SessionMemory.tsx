import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';

/** One row of a session's memory as `SESSION_MEMORY` answers it (the memory coordinator design §3): a write, an end or a proposal. */
export type MemoryRow = {
  id: string;
  repository: string;
  state: string;
  at: string;
  models?: string | null;
  carried?: string | null;
  answer?: string | null;
  by?: { session: string; adapter?: string | null } | null;
  change: { kind: string; text?: string | null; target?: string | null; revision?: number | null; relation?: string | null };
};

const KNOWN_STATES = ['staged', 'ready', 'awaiting', 'applying', 'applied', 'dropped', 'declined', 'stale', 'held', 'answered'];

const PROPOSAL_STATES = ['awaiting', 'declined', 'answered', 'stale', 'dropped'];

/**
 * A session's memory changes (KNOWCENTRAL2e; the memory coordinator design §1, §4), each its own decision: a write and the entry it
 * disputes or amends, an end with who proposed it, its adapter and models and the revision it read, and a proposal with its standing on
 * the owner's quest. An end awaiting its approver is the person's to approve or decline; an ended session's staged changes are theirs to
 * keep or drop. Nothing to say, and nothing is drawn.
 */
export function SessionMemory({ rows, ended, busy = false, said, onDecide, onKeep }: {
  rows: readonly MemoryRow[];
  ended: boolean;
  busy?: boolean;
  said?: string | null;
  onDecide: (op: string, approve: boolean) => void;
  onKeep: (keep: boolean) => void;
}) {
  const { t } = useTranslation();
  if (rows.length === 0) return null;
  const staged = rows.filter((row) => row.state === 'staged').length;

  const what = (row: MemoryRow) => {
    const target = `${row.repository}:${row.change.target ?? ''}`;
    switch (row.change.kind) {
      case 'write':
        return row.change.relation === 'disputes' || row.change.relation === 'amends'
          ? t(`work.memory.write.${row.change.relation}`, { target })
          : t('work.memory.write.plain', { repository: row.repository });
      case 'end':
        return t('work.memory.end', { target, revision: row.change.revision ?? '?' });
      case 'propose':
        return t('work.memory.propose', { repository: row.repository, quest: row.carried ?? '?', change: row.change.relation ?? '', target });
      default:
        return row.change.kind;
    }
  };
  const standing = (row: MemoryRow) => {
    if (row.change.kind === 'propose') {
      const said = PROPOSAL_STATES.includes(row.state) ? row.state : 'other';
      return t(`work.memory.proposal.${said}`, { state: row.state, answer: row.answer ?? '' });
    }
    return KNOWN_STATES.includes(row.state) ? t(`work.memory.state.${row.state}`) : row.state;
  };

  return (
    <section aria-label={t('work.memory.title')} className="grid min-w-0 gap-0.5 text-small text-ink-soft">
      <span className="font-semibold text-ink-faint">{t('work.memory.title')}</span>
      <ul className="m-0 grid list-none gap-1 p-0">
        {rows.map((row) => (
          <li key={row.id} data-memory-row={row.id} className="m-0 grid min-w-0 gap-0.5">
            <span className="flex min-w-0 flex-wrap items-baseline gap-x-1.5">
              <span className="font-semibold text-ink">{what(row)}</span>
              <span>{standing(row)}</span>
            </span>
            {row.change.text && <span className="[overflow-wrap:anywhere]">{row.change.text}</span>}
            {row.change.kind === 'end' && (
              <span className="text-meta text-ink-faint">
                {t('work.memory.by', { session: row.by?.session ?? t('work.memory.recheck'), adapter: row.by?.adapter ?? '?', models: row.models ?? t('work.memory.unknown') })}
              </span>
            )}
            {row.change.kind === 'end' && row.state === 'awaiting' && (
              <span className="flex flex-wrap gap-1.5">
                <Button variant="default" className="px-2 py-0.5 text-small" disabled={busy} onClick={() => onDecide(row.id, true)}>
                  {t('work.memory.approve')}
                </Button>
                <Button variant="ghost" className="px-2 py-0.5 text-small" disabled={busy} onClick={() => onDecide(row.id, false)}>
                  {t('work.memory.decline')}
                </Button>
              </span>
            )}
          </li>
        ))}
      </ul>
      {ended && staged > 0 && (
        <span className="flex flex-wrap items-baseline gap-1.5">
          <span>{t('work.memory.staged', { count: staged })}</span>
          <Button variant="default" className="px-2 py-0.5 text-small" disabled={busy} onClick={() => onKeep(true)}>{t('work.memory.keep')}</Button>
          <Button variant="ghost" className="px-2 py-0.5 text-small" disabled={busy} onClick={() => onKeep(false)}>{t('work.memory.drop')}</Button>
        </span>
      )}
      {said && <span role="status" className="text-meta">{said}</span>}
    </section>
  );
}
