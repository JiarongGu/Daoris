import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { editingOfLine, type Editing, mayEditLine, newRoleEditing, RolesCard } from '../../agents/roles/Roles';
import { Button, Icon, SelectField } from '../../design/ui';
import { authoredText } from '../../locales/authored';
import type { EditorRole, StepStaffing } from './answers';

/** A choice left unset, and a step left to its work's own role: a select's item needs a value, and an empty one is none. */
const NONE = ':none';

/** The place a step's roles are given and named for: the repository or the workspace the editor opened from. */
export type RolePlace = { kind: 'repository' | 'workspace'; name: string };

const roleName = (role: Pick<EditorRole, 'id' | 'name'>, language: string) => authoredText(role.name, language) || role.id;

/**
 * **A step's *Taken by*** (SETUPUX1t; the Setup design's §2.4): the roles that take the step's work and cover this place, each named with its
 * place, and the door to give the place a role of its own for that work, or to edit the one it has. Both open the one role editor beside the
 * step, which saves the role over `SET_ROLE` at once and gives the draft back as it was; the step then names the role. A refused save leaves
 * the editor open with the driver's sentence, and the draft untouched.
 *
 * A molecule: the roles and the editor's reads arrive as props, and every press goes out.
 */
export function StepRole({ id, value, roleTakes, roles, place, staffing, onChoose }: {
  /** The id the label is tied to. */
  id: string;
  /** The role the step names; empty when it leaves the choice to its work's own role. */
  value: string;
  /** The works a role does this step's work by, its own first. */
  roleTakes: string[];
  roles: EditorRole[];
  /** The place the editor opened from; none, and no role is given or edited here. */
  place: RolePlace | null;
  staffing?: StepStaffing;
  /** The step names this role, or none. */
  onChoose: (role: string | undefined) => void;
}) {
  const { t, i18n } = useTranslation();
  const [editing, setEditing] = useState<Editing | null>(null);
  const [said, setSaid] = useState<string | null>(null);
  const keys = useRef(0);
  const nextKey = () => keys.current++;
  const work = roleTakes[0]!;
  const workName = t(`settings.ai.roles.work.${work}`, { defaultValue: work });
  const placeOf = (role: EditorRole) => (role.scope && role.scope.kind !== 'machine' && role.scope.id ? role.scope.id : t('workflow.edit.role.at.machine'));
  const named = (role: EditorRole) => `${roleName(role, i18n.language)} · ${placeOf(role)}`;

  const taking = roles.filter((role) => role.takes.some((each) => roleTakes.includes(each)) && role.covers !== false);
  // A role a saved version names that no longer does the work is kept in view, for the judge to say why.
  const offered = value && !taking.some((role) => role.id === value) ? [...taking, roles.find((role) => role.id === value) ?? { id: value, name: null, takes: [] }] : taking;
  // The role this place already has for the work, which is edited rather than given another.
  const own = place ? taking.find((role) => role.scope?.kind === place.kind && role.scope.id === place.name) : undefined;
  const catalogue = staffing?.catalogue ?? null;
  const kept = catalogue?.kept === true;

  const give = () => {
    if (!staffing || !catalogue || !place) return;
    // Named as the role the step has now, else as one that takes its work; the place makes the two apart.
    const nearest = taking.find((role) => role.id === value) ?? taking.find((role) => role.takes.includes(work));
    const base = newRoleEditing(catalogue.works, staffing.agents, { kind: place.kind, id: place.name }, nextKey());
    setSaid(null);
    setEditing({ ...base, work, takes: [work], name: nearest ? roleName(nearest, i18n.language) : workName });
  };
  const ownLine = own && catalogue && staffing
    ? { id: own.id, row: catalogue.roles.find((row) => row.id === own.id) ?? null, actor: staffing.actors.find((row) => row.id === own.id) ?? null }
    : null;
  const edit = () => {
    if (!staffing || !catalogue || !own || !ownLine) return;
    setSaid(null);
    setEditing(editingOfLine(ownLine, { kept, works: catalogue.works, agents: staffing.agents, name: roleName(own, i18n.language), key: nextKey }));
  };

  const set: StepStaffing['onSet'] = (press, answered) => staffing?.onSet(press, {
    refused: answered.refused,
    done: () => {
      answered.done();
      if (press.change !== 'set' || !press.role) return;
      onChoose(press.id);
      setSaid(t('workflow.edit.role.saved', {
        role: `${authoredText(press.role.name, i18n.language) || press.id} · ${press.role.scope.id ?? t('workflow.edit.role.at.machine')}`, place: place?.name ?? '',
      }));
    },
  });

  return (
    <div className="grid min-w-0 gap-1">
      <span id={id} className="text-meta text-ink-soft">{t('workflow.edit.role.label')}</span>
      <SelectField
        ariaLabel={t('workflow.edit.role.label')}
        value={value || NONE}
        onChange={(chosen) => onChoose(chosen === NONE ? undefined : chosen)}
        options={[
          { value: NONE, label: t('workflow.edit.role.own') },
          ...offered.map((role) => ({ value: role.id, label: named(role) })),
        ]}
      />
      {taking.length === 0 && <p className="m-0 text-meta text-ink-faint">{t('workflow.edit.role.none')}</p>}
      {staffing && catalogue && place && editing === null && (
        <div className="flex min-w-0 flex-wrap items-center gap-1.5">
          {own
            ? ownLine && mayEditLine(ownLine, kept) && (
              <Button variant="ghost" className="min-h-0 border border-line py-0.5 text-small" disabled={staffing.busy} onClick={edit}>
                {t('workflow.edit.role.edit', { role: named(own) })}
              </Button>
            )
            : (
              <Button variant="ghost" className="min-h-0 border border-line py-0.5 text-small" disabled={staffing.busy || !kept} onClick={give}>
                <Icon name="plus" size={12} />
                {t('workflow.edit.role.give', { place: place.name, work: workName })}
              </Button>
            )}
        </div>
      )}
      {said && <p role="status" className="m-0 text-meta text-ink-soft [overflow-wrap:anywhere]">{said}</p>}
      {staffing && place && editing && (
        <RolesCard
          editorOnly
          consequence={t(editing.isNew ? 'workflow.edit.role.thenGive' : 'workflow.edit.role.thenEdit', { place: place.name, work: workName })}
          catalogue={catalogue}
          read={staffing.read}
          staffing={staffing.actors}
          agents={staffing.agents}
          offers={staffing.offers}
          busy={staffing.busy}
          onSet={set}
          editor={editing}
          onEditor={setEditing}
        />
      )}
    </div>
  );
}
