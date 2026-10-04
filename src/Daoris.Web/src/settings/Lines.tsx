import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Button, Card, Chip, Prose, SectionTitle, SettingRow } from '../ui';

/** Where a repository's line came from (WSR2): set for it, set for its workspace, the checkout's guess, or nothing. */
export type LineSource = 'repository' | 'workspace' | 'checkout' | 'none';

/** One repository's line on this machine, as the driver resolves it — the answer every door reads. */
export type RepositoryLine = { repository: string; workspace: string; branch?: string; source: LineSource };

/** A change to a line: a repository's or a workspace's, cleared when it names no branch. */
export type LineChange = { repository?: string; workspace?: string; branch?: string };

/**
 * The line each repository's work grows from and lands on (WSR2), and the default each workspace
 * gives the repositories in it that set none of their own.
 *
 * @remarks
 * A line a person sets: it was only ever git's own
 * guess, so a repository whose work lands on `develop` had sessions grown from, and merged into,
 * `main`. **What each row shows is the driver's own resolution**, read rather than recomputed — so
 * the page cannot name a line a session would not take. A row's control is the screen's half of
 * `daoris driver line` (D50). A name git would refuse comes back as the driver's own sentence.
 */
export function LineList({ lines, workspaceLines, busy, onSet }: {
  lines: RepositoryLine[];
  /** What each workspace sets, by name. A workspace with no repository here still shows its own. */
  workspaceLines: { workspace: string; branch: string }[];
  busy?: boolean;
  onSet: (change: LineChange) => void;
}) {
  const { t } = useTranslation();
  const circles = [...new Set([...lines.map((line) => line.workspace), ...workspaceLines.map((w) => w.workspace)])]
    .sort((a, b) => a.localeCompare(b));

  return (
    <Card id="settings-lines" className="mt-3.5 scroll-mt-3">
      <SectionTitle>{t('settings.lines.title')}</SectionTitle>
      <Prose className="mt-1 text-small text-ink-soft">{t('settings.lines.body')}</Prose>

      {circles.length === 0 && <Prose className="mt-3">{t('settings.lines.none')}</Prose>}

      {circles.map((workspace) => {
        const shared = workspaceLines.find((w) => w.workspace === workspace)?.branch;
        return (
          <section key={workspace} aria-label={workspace} className="mt-3 border-t border-line pt-3">
            <SettingRow
              label={<Chip accent>{workspace}</Chip>}
              hint={t('settings.lines.workspaceHint')}
              control={(
                <LineField
                  label={t('settings.lines.workspaceField', { workspace })}
                  set={shared}
                  placeholder={t('settings.lines.eachCheckout')}
                  busy={busy}
                  onSave={(branch) => onSet({ workspace, branch })}
                />
              )}
            />
            {lines.filter((line) => line.workspace === workspace).map((line) => (
              <SettingRow
                key={line.repository}
                label={line.repository}
                hint={line.branch
                  ? t(`settings.lines.from.${line.source}`, { branch: line.branch, workspace })
                  : t('settings.lines.from.none')}
                control={(
                  <LineField
                    label={t('settings.lines.repositoryField', { repository: line.repository })}
                    set={line.source === 'repository' ? line.branch : undefined}
                    placeholder={line.branch ?? t('settings.lines.unnamed')}
                    busy={busy}
                    onSave={(branch) => onSet({ repository: line.repository, branch })}
                  />
                )}
              />
            ))}
          </section>
        );
      })}
    </Card>
  );
}

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
      className="flex items-center gap-2"
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
        // one shade apart (seen on the window).
        className="w-44 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint"
      />
      <Button type="submit" disabled={busy || !typed || typed === set}>{t('settings.lines.set')}</Button>
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
