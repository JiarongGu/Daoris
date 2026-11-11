/** The shipped workflow whose work is done in its department's own place and lands nothing: every kind of department can follow it. */
export const IN_PLACE = 'in-place';

export type DepartmentKind = 'git' | 'folder' | 'system';

/** The add chooses it for a folder and a system always, since no other shipped workflow runs there, and for a repository where ticked. */
export const choosesInPlace = (kind: DepartmentKind, ticked: boolean): boolean => kind !== 'git' || ticked;
