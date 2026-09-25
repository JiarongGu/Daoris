// The circle a registry row belongs to (D48): its own, or `default` for a row from a host older than
// workspaces, so one such family stays one circle. Every picker derives its list here (REV3 CLEAN1:
// six wrote it out, and a seventh dropped the rows naming none, so it offered no `default` at all).

/** The circle a row that names none is in. */
export const DEFAULT_WORKSPACE = 'default';

/** The circle one row belongs to. */
export const workspaceOf = (row: { workspace?: string | null }): string => row.workspace ?? DEFAULT_WORKSPACE;

/** Every circle among these rows, once each, sorted. */
export const workspacesOf = (rows: readonly { workspace?: string | null }[]): string[] =>
  [...new Set(rows.map(workspaceOf))].sort();
