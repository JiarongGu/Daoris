import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../design/ui';
import { parseExcludes } from './FolderExcludesField';

/** What changes between the globs a folder leaves out and the globs typed: each new one to add, each gone one to take back. */
export const excludesChange = (held: readonly string[], typed: string): { add: string[]; remove: string[] } => {
  const next = parseExcludes(typed);
  return { add: next.filter((glob) => !held.includes(glob)), remove: held.filter((glob) => !next.includes(glob)) };
};

/**
 * A folder department's own excludes, edited in place: the files its manifest passes by beside the defaults, one glob to a line,
 * changed by a press the person confirms where a terminal would. Nothing is sent until the list differs from the one held.
 */
export function FolderExcludesRow({ held, busy = false, onSave }: {
  /** The globs its registration keeps. */
  held: readonly string[];
  busy?: boolean;
  onSave: (change: { add: string[]; remove: string[] }) => void;
}) {
  const { t } = useTranslation();
  const [typed, setTyped] = useState(held.join('\n'));
  const change = excludesChange(held, typed);
  const changed = change.add.length + change.remove.length > 0;
  return (
    <div className="grid gap-1.5">
      <textarea
        aria-label={t('projects.manage.excludes')}
        value={typed}
        rows={Math.max(3, held.length + 1)}
        onChange={(event) => setTyped(event.target.value)}
        placeholder={t('projects.manage.excludesPlaceholder')}
        className="w-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-body"
      />
      <p className="m-0 text-small text-ink-faint">{t('projects.manage.excludesNote')}</p>
      <div>
        <Button disabled={!changed || busy} onClick={() => onSave(change)}>{t('projects.folder.excludesSave')}</Button>
      </div>
    </div>
  );
}
