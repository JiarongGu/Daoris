/**
 * What an Ask Daoris conversation is about, its focus: this machine, a workspace, or a repository with its
 * workspace, since one name may stand in two. One shape on every side: the twin is the driver's `Knowledge/HelpFocus.cs`, and
 * `focus.test.ts` and its `HelpFocusTests` hold the same spellings. Its reach stays every workspace whatever it is.
 */
export type Place =
  | { kind: 'machine' }
  | { kind: 'workspace'; workspace: string }
  | { kind: 'repository'; workspace: string; repository: string };

export const MACHINE: Place = { kind: 'machine' };

/** The workspace `default`, whose room is the whole machine's. */
const DEFAULT = 'default';

const name = (value: unknown): string | null => (typeof value === 'string' && value.trim() ? value.trim() : null);

/** The place a value spells, or null for a shape that is none: an unknown kind, or a name it needs left blank. */
export const readPlace = (value: unknown): Place | null => {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return null;
  const { kind, workspace, repository } = value as Record<string, unknown>;
  const space = name(workspace);
  const repo = name(repository);
  if (kind === 'machine') return MACHINE;
  if (kind === 'workspace' && space) return { kind, workspace: space };
  if (kind === 'repository' && space && repo) return { kind, workspace: space, repository: repo };
  return null;
};

/** The focus a conversation homed in `workspace` holds until one is chosen: that workspace, or this machine for `default`'s. */
export const homeFocus = (workspace: string | null | undefined): Place =>
  !workspace?.trim() || workspace.trim().toLowerCase() === DEFAULT ? MACHINE : { kind: 'workspace', workspace: workspace.trim() };

/** The terminal's spelling: `machine`, `<workspace>` or `<workspace>/<repository>`. */
export const placeSaid = (place: Place): string =>
  place.kind === 'machine' ? 'machine' : place.kind === 'workspace' ? place.workspace : `${place.workspace}/${place.repository}`;

const same = (a: string | undefined, b: string | undefined) => (a ?? '').toLowerCase() === (b ?? '').toLowerCase();

const parts = (place: Place) => ({
  workspace: place.kind === 'machine' ? undefined : place.workspace,
  repository: place.kind === 'repository' ? place.repository : undefined,
});

/** Whether two places are one, their names compared whatever their letters' case, as the driver compares them. */
export const samePlace = (a: Place, b: Place): boolean =>
  a.kind === b.kind && same(parts(a).workspace, parts(b).workspace) && same(parts(a).repository, parts(b).repository);

/** Whether `inner` is `outer` or inside it: this machine holds every place, a workspace its repositories. */
export const holds = (outer: Place, inner: Place): boolean =>
  outer.kind === 'machine' ? true
    : outer.kind === 'workspace' ? inner.kind !== 'machine' && same(outer.workspace, inner.workspace)
    : samePlace(outer, inner);
