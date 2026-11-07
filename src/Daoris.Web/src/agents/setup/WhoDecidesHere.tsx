import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { StructurePlaceAsk } from '../../data/wiring';
import { SelectField } from '../../design/ui';
import { newSupervisorDraft } from '../roles/appointedHold';
import { type Editing, newRoleEditing, roleName, RolesCard } from '../roles/Roles';
import type { PlaceRoleEditor } from './PlaceRoles';
import { decidersAt, deciderText, offeredKinds, scopeOf } from './whoDecides';

/**
 * **Who decides here**: one line per kind of decision at the place, from `ROLES`' `deciders`, naming the supervisor and where it is set
 * or *you* with why; and *Add a supervisor for…*, offering the kinds no supervisor placed here decides, which opens the role editor with
 * that kind, this place and nothing ticked. The driver resolves; the page words what it answers.
 */
export function WhoDecidesHere({ place, editor }: { place: StructurePlaceAsk; editor: PlaceRoleEditor }) {
  const { t, i18n } = useTranslation();
  const [open, setOpen] = useState<Editing | null>(null);
  const keys = useRef(0);
  const { catalogue } = editor;
  if (!catalogue?.deciders) return null;
  const nameOf = (id: string) => {
    const row = catalogue.roles.find((each) => each.id === id);
    return row ? roleName(t, i18n.language, row) : id;
  };
  const scope = scopeOf(place);
  const offered = catalogue.kept ? offeredKinds(catalogue, scope) : [];
  const add = (kind: string) => setOpen({
    ...newRoleEditing(catalogue.works, editor.agents, scope.kind === 'machine' ? null : { kind: scope.kind, id: scope.id ?? '' }, --keys.current),
    drafts: [], work: `${kind}-supervisor`, hold: newSupervisorDraft(kind),
  });
  const title = t('agents.setup.roles.decides.title');
  return (
    <section aria-label={title} className="grid gap-1">
      <p className="m-0 text-small font-medium text-ink-soft">{title}</p>
      <ul className="m-0 grid list-none gap-0.5 p-0">
        {decidersAt(catalogue.deciders, place).map((line) => (
          <li key={line.kind} className="text-small text-ink wrap-anywhere">{deciderText(t, line, nameOf)}</li>
        ))}
      </ul>
      {offered.length > 0 && !open && (
        <SelectField
          value="" placeholder={t('agents.setup.roles.decides.add')} ariaLabel={t('agents.setup.roles.decides.add')} disabled={editor.busy} onChange={add}
          options={offered.map((kind) => ({ value: kind, label: t(`agents.setup.roles.hold.decides.${kind}`) }))}
        />
      )}
      {open && (
        <RolesCard
          editorOnly
          catalogue={catalogue} read={editor.read} staffing={editor.staffing} agents={editor.agents} offers={editor.offers} busy={editor.busy}
          onSet={editor.onSet} editor={open} onEditor={setOpen}
        />
      )}
    </section>
  );
}
