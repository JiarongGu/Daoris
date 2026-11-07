import { useTranslation } from 'react-i18next';
import { roleName } from '../roles/Roles';
import { SupervisorRulesFold } from '../roles/SupervisorRulesFold';
import { setOf } from '../roles/supervisorRulesDraft';
import type { PlaceRoleEditor } from './PlaceRoles';

/**
 * **A place's supervisor rules**: the fold under the place's Roles, over the supervisor's rules `ROLES` answers and the
 * one press of the role door. It is drawn once the driver answers the rules; a driver before them shows none. Keyed by the revision it was
 * read at, so a saved or moved set opens as stored.
 */
export function PlaceSupervisorRules({ place, placeName, editor }: { place: string; placeName: string; editor: PlaceRoleEditor }) {
  const { i18n, t } = useTranslation();
  const read = editor.catalogue?.supervisor;
  if (!read || editor.catalogue?.kept !== true) return null;
  const roles = editor.catalogue.roles
    .filter((row) => row.state !== 'removed' && row.state !== 'unreadable' && !row.hold && row.takes.length > 0)
    .map((row) => ({ value: row.id, label: roleName(t, i18n.language, row) }));
  return (
    <SupervisorRulesFold
      key={`${place}:${setOf(read, place)?.revision ?? 0}`}
      place={place} placeName={placeName} read={read} roles={roles} busy={editor.busy} refused={null} onSave={editor.onSet}
    />
  );
}
