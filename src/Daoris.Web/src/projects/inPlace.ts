/** The shipped workflow whose work is done in its department's own place and lands nothing: every kind of department can follow it. */
export const IN_PLACE = 'in-place';

/** A department's kind, as the add registers it. */
export type DepartmentKind = 'git' | 'folder' | 'system';

/**
 * Whether the add chooses *Work in place*: a folder and a system always, since no other shipped workflow runs there, and a
 * repository where the person ticks it.
 */
export const choosesInPlace = (kind: DepartmentKind, ticked: boolean): boolean => kind !== 'git' || ticked;
