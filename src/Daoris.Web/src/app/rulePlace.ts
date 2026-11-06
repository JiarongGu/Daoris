import { type OpenPart, workspaceItem } from './opener';

/** Where a door opens: a view, the item its list chooses, and the part of it. */
export type RuleDoor = { view: 'agents' | 'projects'; item: string | null; part: OpenPart };

/**
 * The page where the person changes the supervisor rule that acted (*Change the rule…*): the place a rule names, as its Roles page holds
 * the supervisor's rules. This machine's page, a workspace's, a repository's; a line's own rule is changed on the repository it runs in
 * (`repository`), which holds the rules a line reads, and none where that is not known.
 */
export function ruleDoor(place: string, repository?: string | null): RuleDoor | null {
  if (place === 'machine') return { view: 'agents', item: null, part: { agentsMode: 'setup' } };
  const at = place.indexOf(':');
  const kind = at < 0 ? place : place.slice(0, at);
  const id = at < 0 ? '' : place.slice(at + 1);
  if (kind === 'workspace' && id) return { view: 'projects', item: workspaceItem(id), part: { workspaceSection: 'supervisor' } };
  const named = kind === 'repository' && id ? id : repository;
  return named ? { view: 'agents', item: null, part: { placeOf: named, placeSection: 'roles', placeRow: 'supervisor' } } : null;
}
