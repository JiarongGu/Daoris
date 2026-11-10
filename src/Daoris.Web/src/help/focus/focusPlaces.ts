import { MACHINE, type Place, placeSaid, samePlace } from '../focus';

/** A repository the focus picker may offer: its workspace, and whether it has a checkout here. */
export type PlaceRepository = { name: string; workspace: string; checkout: boolean };

/**
 * One row of the focus picker. `first` is the window's place, `recent` the places last chosen, `machine` this machine, `other`
 * the other workspaces with their repositories (and, in a search, every match). A repository names its workspace `beside` it
 * where no workspace row above says it; one under its unfolded workspace is `nested`.
 */
export type FocusRow = {
  key: string;
  place: Place;
  group: 'first' | 'recent' | 'machine' | 'other';
  mark?: 'this-window' | 'every-workspace' | 'no-checkout';
  count?: number;
  beside?: string;
  nested?: boolean;
  open?: boolean;
};

/** The workspace `default`, whose room is the whole machine's. */
const DEFAULT = 'default';

/** The registry's rows as the pickers' repositories: a row naming no workspace is `default`'s, and a checkout is a root. */
export const repositoriesOf = (rows: readonly { repository: string; workspace?: string | null; root?: string | null }[]): PlaceRepository[] =>
  rows.map((row) => ({ name: row.repository, workspace: row.workspace || DEFAULT, checkout: Boolean(row.root) }));

const lower = (text: string) => text.toLowerCase();

/**
 * The picker's rows, in an order that never changes with what is chosen: the window's workspace (this machine in a window on every
 * workspace, or on a machine whose only workspace is `default`), *Recent* (five at most, never the first row's), this machine, then
 * the other workspaces by name, each with its count and, unfolded, its repositories by name. A machine whose only workspace is
 * `default` offers no workspace level: its repositories follow directly. Typing flattens the list into one, no place twice:
 * *Recent*'s matches, then workspaces, then repositories, a workspace's name matching its repositories.
 *
 * @param window The window's workspace; null for a window on every workspace.
 * @param open The workspaces unfolded, by name in lower case.
 */
export function focusRows({ repositories, window, recent, open, typed }: {
  repositories: readonly PlaceRepository[];
  window: string | null;
  recent: readonly Place[];
  open: ReadonlySet<string>;
  typed: string;
}): FocusRow[] {
  const workspaces = [...new Set(repositories.map((each) => each.workspace))].sort((a, b) => a.localeCompare(b));
  const flat = workspaces.every((workspace) => lower(workspace) === DEFAULT);
  const known = (place: Place) => place.kind === 'machine'
    || (place.kind === 'workspace' && workspaces.some((workspace) => lower(workspace) === lower(place.workspace)))
    || (place.kind === 'repository' && repositories.some((each) => samePlace(repoPlace(each), place)));
  const home = window === null || flat || lower(window) === DEFAULT ? MACHINE : { kind: 'workspace' as const, workspace: window };
  const repositoryRow = (each: PlaceRepository, group: FocusRow['group'], nested: boolean): FocusRow => ({
    key: placeSaid(repoPlace(each)), place: repoPlace(each), group, ...(nested ? { nested } : flat ? {} : { beside: each.workspace }),
    ...(each.checkout ? {} : { mark: 'no-checkout' as const }),
  });
  const reposOf = (workspace: string) => repositories
    .filter((each) => each.workspace === workspace).sort((a, b) => a.name.localeCompare(b.name));
  const recentRows = recent.filter((place) => known(place) && !samePlace(place, home)).slice(0, 5)
    .map((place): FocusRow => {
      const repository = place.kind === 'repository' ? repositories.find((each) => samePlace(repoPlace(each), place)) : undefined;
      return repository ? repositoryRow(repository, 'recent', false) : { key: placeSaid(place), place, group: 'recent' };
    });

  const word = lower(typed.trim());
  if (word) {
    const seen = new Set<string>();
    const once = (row: FocusRow) => (seen.has(lower(row.key)) ? false : (seen.add(lower(row.key)), true));
    const matches = (place: Place) => place.kind !== 'machine' && (lower(place.workspace).includes(word)
      || (place.kind === 'repository' && lower(place.repository).includes(word)));
    return [
      ...recentRows.filter((row) => matches(row.place)),
      ...(flat ? [] : workspaces.map((workspace): FocusRow => ({ key: workspace, place: { kind: 'workspace', workspace }, group: 'other' }))),
      ...[...repositories].sort((a, b) => a.name.localeCompare(b.name) || a.workspace.localeCompare(b.workspace))
        .map((each) => ({ ...repositoryRow(each, 'other', false), beside: each.workspace })),
    ].filter((row) => matches(row.place)).filter(once);
  }

  const workspaceRows = (workspace: string, group: FocusRow['group']): FocusRow[] => {
    const unfolded = open.has(lower(workspace));
    return [
      { key: workspace, place: { kind: 'workspace', workspace }, group, ...(group === 'first' ? { mark: 'this-window' as const } : { count: reposOf(workspace).length }), ...(unfolded ? { open: true } : {}) },
      ...(unfolded ? reposOf(workspace).map((each) => repositoryRow(each, group, true)) : []),
    ];
  };
  const firstRows = home.kind === 'machine'
    ? [{ key: placeSaid(MACHINE), place: MACHINE, group: 'first' as const, mark: 'this-window' as const }]
    : workspaceRows(home.workspace, 'first');
  return [
    ...firstRows,
    ...recentRows,
    ...(home.kind === 'machine' ? [] : [{ key: placeSaid(MACHINE), place: MACHINE, group: 'machine' as const, mark: 'every-workspace' as const }]),
    ...(flat
      ? repositories.slice().sort((a, b) => a.name.localeCompare(b.name)).map((each) => repositoryRow(each, 'other', false))
      : workspaces.filter((workspace) => home.kind === 'machine' || lower(workspace) !== lower(home.workspace))
        .flatMap((workspace) => workspaceRows(workspace, 'other'))),
  ];
}

function repoPlace(each: PlaceRepository): Place {
  return { kind: 'repository', workspace: each.workspace, repository: each.name };
}
