// The circle a registry row belongs to: its own, or `default` for a row that names none,
// so one such family stays one circle. Every picker derives its list here, so none can drop the rows naming none.

/** The circle a row that names none is in. */
export const DEFAULT_WORKSPACE = 'default';

/** The circle one row belongs to. */
export const workspaceOf = (row: { workspace?: string | null }): string => row.workspace ?? DEFAULT_WORKSPACE;

/** Every circle among these rows, once each, sorted. */
export const workspacesOf = (rows: readonly { workspace?: string | null }[]): string[] =>
  [...new Set(rows.map(workspaceOf))].sort();
