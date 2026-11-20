import { useTranslation } from 'react-i18next';
import { SelectField } from '../../design/ui';
import type { EditorRole, StepStaffing, WorkflowPaletteAnswer } from './answers';
import type { DraftNode, GraphDraft } from './graphDraftTypes';
import { type RolePlace, StepRole } from './StepRole';
import { STEP_ROLES, namedRole, roleSaid } from '../stepRoles';

/** A role left to the step's kind: a select's item needs a value, and an empty one is none. */
const KIND = ':kind';

/** The role the palette gives a step of `kind` that names none; null for a plugin's step or a driver before the field. */
export const kindRole = (palette: WorkflowPaletteAnswer, kind: string): string | null =>
  palette.kinds.find((each) => each.kind === kind)?.role ?? null;

/**
 * The draft with step `id` naming `role` in its `role` field, or its kind's where `role` is none: the field is the one way, so the `role`
 * option an older version named it by is taken off as the step is set.
 */
export function setRole(draft: GraphDraft, id: string, role: string | undefined): GraphDraft {
  return {
    ...draft,
    nodes: draft.nodes.map((step) => {
      if (step.id !== id) return step;
      const { role: _role, ...node } = step.node;
      const options = node.options !== null && typeof node.options === 'object' && !Array.isArray(node.options) ? { ...node.options as Record<string, unknown> } : null;
      if (options) delete options.role;
      return { ...step, node: { ...node, ...(options ? { options } : {}), ...(role ? { role } : {}) } };
    }),
  };
}

/**
 * **The role that takes a step**, which the editor sets: an agent step's from the working roles that do its work (`StepRole`), any
 * other's from Daoris's roles, its kind's offered first as the default. A role, never who fills it: the place says that.
 */
export function StepRoleField({ id, node, palette, roleTakes, roles, place = null, staffing, onSet }: {
  id: string;
  node: DraftNode;
  palette: WorkflowPaletteAnswer;
  roleTakes: string[];
  roles: EditorRole[];
  place?: RolePlace | null;
  staffing?: StepStaffing;
  onSet: (role: string | undefined) => void;
}) {
  const { t, i18n } = useTranslation();
  const named = namedRole(node.node);
  if (roleTakes.length > 0) {
    return <StepRole id={id} value={named ?? ''} roleTakes={roleTakes} roles={roles} place={place} staffing={staffing} onChoose={onSet} />;
  }

  const own = kindRole(palette, node.kind);
  const offered = palette.roles ?? [...STEP_ROLES];
  const label = t('workflow.role.label');
  return (
    <div className="grid min-w-0 gap-1">
      <span className="text-caption text-ink-soft">{label}</span>
      <SelectField
        ariaLabel={label}
        value={named ?? KIND}
        onChange={(chosen) => onSet(chosen === KIND ? undefined : chosen)}
        options={[
          { value: KIND, label: own ? t('workflow.role.default', { role: roleSaid(t, i18n, own) }) : t('workflow.role.none') },
          ...offered.filter((role) => role !== own).map((role) => ({ value: role, label: roleSaid(t, i18n, role) })),
          ...(named && !offered.includes(named) ? [{ value: named, label: named }] : []),
        ]}
      />
      <p className="m-0 text-caption text-ink-faint">{t('workflow.role.hint')}</p>
    </div>
  );
}
