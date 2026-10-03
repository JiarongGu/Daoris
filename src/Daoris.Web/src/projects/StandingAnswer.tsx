import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ago, figure } from '../format';
import { Button } from '../ui';

/** The most characters a standing answer holds — the driver's `DriverConfig.StandingLimit`, a deliberate copy. */
const STANDING_LIMIT = 2_000;

/**
 * **A repository's standing answer on this machine** (KNOWUSE1b, D135 §3): what the person says holds for every session
 * there — which writes are allowed, where to test — in their own words, handed to each one beneath its quest. Its words
 * verbatim with when they were set, or that there is none; *Edit* or *Add* opens the words in place, and *Clear* takes them
 * back. The same `standing` `daoris driver standing` edits (D50), kept on this machine and never written into the
 * repository (D32).
 *
 * Props only, no hook from the query layer or the shell (components §2).
 */
export function StandingAnswer({ says, at = null, busy = false, onSave }: {
  /** The person's words as this machine keeps them, or null for none. */
  says: string | null;
  /** When they set them, or null where the file does not say. */
  at?: string | null;
  busy?: boolean;
  /** Keep these words, or with null clear them. */
  onSave: (says: string | null) => void;
}) {
  const { t } = useTranslation();
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState('');
  const words = draft.trim();
  const tooLong = words.length > STANDING_LIMIT;
  const open = () => { setDraft(says ?? ''); setEditing(true); };

  return (
    <div className="mt-3 grid gap-1.5">
      <p className="m-0 flex flex-wrap items-baseline gap-x-3 gap-y-1">
        <span className="min-w-12 text-meta text-ink-faint">{t('projects.standing.label')}</span>
        {!editing && says !== null && at && <span className="font-mono text-meta text-ink-faint">{t('projects.standing.set', { ago: ago(at) })}</span>}
        {!editing && (
          <span className="ml-auto flex gap-2">
            <Button variant="ghost" disabled={busy} onClick={open}>{t(says === null ? 'projects.standing.add' : 'projects.standing.edit')}</Button>
            {says !== null && <Button variant="ghost" disabled={busy} onClick={() => onSave(null)}>{t('projects.standing.clear')}</Button>}
          </span>
        )}
      </p>
      {!editing && says !== null && (
        <blockquote className="m-0 whitespace-pre-wrap border-l-[3px] border-line-strong pl-3 text-body text-ink">{says}</blockquote>
      )}
      {!editing && says === null && <p className="m-0 text-small text-ink-soft">{t('projects.standing.none')}</p>}
      {editing && (
        <div className="grid gap-2">
          <textarea
            autoFocus
            aria-label={t('projects.standing.label')}
            placeholder={t('projects.standing.placeholder')}
            rows={3}
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            className="resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
          />
          {tooLong && (
            <p className="m-0 text-small text-warn">
              {t('projects.standing.tooLong', { count: figure(words.length), most: figure(STANDING_LIMIT) })}
            </p>
          )}
          <div className="flex flex-wrap gap-2">
            <Button disabled={busy || words.length === 0 || tooLong} onClick={() => { onSave(words); setEditing(false); }}>
              {t('projects.standing.save')}
            </Button>
            <Button variant="ghost" disabled={busy} onClick={() => setEditing(false)}>{t('common.cancel')}</Button>
          </div>
        </div>
      )}
      <p className="m-0 text-small text-ink-soft">{t('projects.standing.hint')}</p>
    </div>
  );
}
