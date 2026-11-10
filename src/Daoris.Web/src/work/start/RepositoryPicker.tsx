import { type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ChipCombobox, type ComboGroup } from '../../design/atoms/ChipCombobox';
import { Icon } from '../../design/ui';

/** A repository a conversation may be started in, as the composer lists it: its workspace, and whether it has a checkout here. */
export type StartRepository = { name: string; workspace: string; checkout: boolean };

/** One row of the picker: the repository, and whether it is the Recent group's (which names its workspace beside it). */
type Row = StartRepository & { recent: boolean };

/** A run of rows under one name: *Recent*, then each workspace. */
type Group = { name: string; rows: Row[] };

/** The groups listed for what is typed: *Recent* first, then each workspace's by name, none twice, matched by name or workspace. */
export function pickerGroups(repositories: readonly StartRepository[], recent: readonly string[], typed: string, recentLabel: string): Group[] {
  const word = typed.trim().toLowerCase();
  const matches = (row: StartRepository) =>
    !word || row.name.toLowerCase().includes(word) || row.workspace.toLowerCase().includes(word);
  const byName = new Map(repositories.map((row) => [row.name, row]));
  const recentRows = recent.flatMap((name) => {
    const row = byName.get(name);
    return row && matches(row) ? [{ ...row, recent: true }] : [];
  });
  const kept = new Set(recentRows.map((row) => row.name));
  const workspaces = [...new Set(repositories.map((row) => row.workspace))].sort((a, b) => a.localeCompare(b));
  const groups: Group[] = recentRows.length > 0 ? [{ name: recentLabel, rows: recentRows }] : [];
  for (const workspace of workspaces) {
    const rows = repositories
      .filter((row) => row.workspace === workspace && !kept.has(row.name) && matches(row))
      .sort((a, b) => a.name.localeCompare(b.name))
      .map((row) => ({ ...row, recent: false }));
    if (rows.length > 0) groups.push({ name: workspace, rows });
  }
  return groups;
}

/**
 * The repository chip and its picker, on the composers' one combobox (`ChipCombobox`): *Recent* first, then each workspace's
 * repositories. One with no checkout here is listed, not choosable, and says why; one a session holds is marked busy and stays
 * choosable, since its own tree starts beside it. Its foot is the caller's (*Add repository…*, *Import a folder…*).
 */
export function RepositoryPicker({ repositories, busy = {}, recent = [], value, onChoose, foot, reveal, defaultOpen = false }: {
  repositories: readonly StartRepository[];
  /** Each repository whose checkout an active session holds, with that session. */
  busy?: Readonly<Record<string, string>>;
  /** This machine's last starts, newest first. */
  recent?: readonly string[];
  value: string | null;
  onChoose: (repository: string) => void;
  /** What the picker offers below its rows, after a rule, given what puts the list down: a press there closes it first. */
  foot?: (close: () => void) => ReactNode;
  /** Opens the list on a workspace's group after an import that registered several: each new `n` is one ask. */
  reveal?: { workspace: string; n: number } | null;
  /** Drawn open, for a story. */
  defaultOpen?: boolean;
}) {
  const { t } = useTranslation();
  const groups = (typed: string): ComboGroup[] => pickerGroups(repositories, recent, typed, t('work.start.recent'))
    .map((group) => ({
      key: group.name,
      name: group.name,
      rows: group.rows.map((row) => ({
        id: `${row.recent ? 'recent' : 'all'}-${row.name}`,
        value: row.name,
        choosable: row.checkout,
        content: (
          <>
            <span className="min-w-0 truncate">{row.name}</span>
            {row.recent && <>{' '}<span className="shrink-0 text-meta text-ink-faint">{row.workspace}</span></>}
            {busy[row.name] && <>{' '}<span className="shrink-0 text-meta text-ink-open">{t('work.start.busyMark')}</span></>}
            {!row.checkout && <>{' '}<span className="ml-auto shrink-0 text-meta">{t('work.start.noCheckout')}</span></>}
          </>
        ),
      })),
    }));

  const label = t('work.start.repository');
  return (
    <ChipCombobox
      chip={(
        <>
          <Icon name="projects" size={13} className="shrink-0 text-ink-faint" />
          <span className="min-w-0 truncate">{value ?? t('work.start.chooseRepository')}</span>
          <Icon name="chevronDown" size={12} className="shrink-0 text-ink-faint" />
        </>
      )}
      chipLabel={t('work.start.chip', { field: label, value: value ?? t('work.start.chooseRepository') })}
      listLabel={label}
      find={t('work.start.find')}
      groups={groups}
      empty={(typed) => t('work.start.nothingFound', { typed })}
      // Opened on the one chosen, so Enter alone keeps it.
      onOpen={() => value}
      onChoose={onChoose}
      foot={foot}
      reveal={reveal ? { typed: reveal.workspace, active: value, n: reveal.n } : null}
      defaultOpen={defaultOpen}
    />
  );
}
