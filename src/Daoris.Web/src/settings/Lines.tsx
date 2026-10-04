import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Button } from '../ui';

/** Where a repository's line came from (WSR2): set for it, set for its workspace, the checkout's guess, or nothing. */
export type LineSource = 'repository' | 'workspace' | 'checkout' | 'none';

/** One repository's line on this machine, as the driver resolves it — the answer every door reads. */
export type RepositoryLine = { repository: string; workspace: string; branch?: string; source: LineSource };

/** A change to a line: a repository's or a workspace's, cleared when it names no branch. */
export type LineChange = { repository?: string; workspace?: string; branch?: string };

/**
 * One line's field: what is set, with what would stand without it as the placeholder, and a clear
 * only where something is set — clearing an inherited line would change nothing and say it had.
 * A repository's Setup gives each value its own *Clear* beside the field (UX6f), so there it carries none.
 */
export function LineField({ label, set, placeholder, busy, clearable = true, onSave }: {
  label: string;
  set?: string;
  placeholder: string;
  busy?: boolean;
  clearable?: boolean;
  onSave: (branch?: string) => void;
}) {
  const { t } = useTranslation();
  const [draft, setDraft] = useState(set ?? '');
  // The answer moves when either door edits the file, and the field follows it.
  useEffect(() => setDraft(set ?? ''), [set]);
  const typed = draft.trim();

  return (
    <form
      className="flex min-w-0 items-center gap-2"
      onSubmit={(event) => {
        event.preventDefault();
        if (typed && typed !== set) onSave(typed);
      }}
    >
      <input
        aria-label={label}
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        placeholder={placeholder}
        spellCheck={false}
        // What would stand is a placeholder, and it must not read as what is set: in dark the two were
        // one shade apart (seen on the window). The field gives way to a narrow column, never its Save,
        // whose word broke over two lines in a repository's Setup at 544 px (UX6f, seen in its story).
        className="w-44 min-w-0 max-w-full rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint"
      />
      <Button type="submit" className="shrink-0 whitespace-nowrap" disabled={busy || !typed || typed === set}>{t('settings.lines.set')}</Button>
      {/* Always in the row, and hidden where nothing is set: a row that grew a Clear moved its field
          out of the column every other row's field sits in (seen on the window). */}
      {clearable && <Button
        variant="ghost"
        disabled={busy || set === undefined}
        aria-hidden={set === undefined}
        tabIndex={set === undefined ? -1 : undefined}
        className={cn(set === undefined && 'invisible')}
        onClick={() => onSave(undefined)}
      >
        {t('settings.lines.clear')}
      </Button>}
    </form>
  );
}
