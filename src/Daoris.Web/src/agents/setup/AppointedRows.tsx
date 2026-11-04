import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, SettingList, SettingRow } from '../../design/ui';
import { appointedRows, holdLine } from '../roles/appointedHold';
import { type Editing, editingOfLine, mayEditLine, roleName, RolesCard } from '../roles/Roles';
import type { PlaceRoleEditor } from './PlaceRoles';

/**
 * **A place's appointed roles** (ROLEONE1b): the supervisor and the steward as the catalogue holds them, one row each for the roles placed
 * here, with who holds it and its acts. *Edit* opens the role editor of All roles over `SET_ROLE`: the acts, the holder and the supervisor's
 * window are the record's, and the page judges nothing.
 */
export function AppointedRows({ placed, editor }: { placed: (scope: { kind: string; id?: string }) => boolean; editor: PlaceRoleEditor }) {
  const { t, i18n } = useTranslation();
  const [open, setOpen] = useState<Editing | null>(null);
  const keys = useRef(0);
  const { catalogue, staffing } = editor;
  const kept = catalogue?.kept === true;
  const rows = appointedRows(catalogue).filter((row) => placed({ kind: row.scopeKind ?? 'machine', id: row.scopeId ?? undefined }));
  if (rows.length === 0) return null;
  return (
    <SettingList>
      {rows.map((row) => {
        const name = roleName(t, i18n.language, row);
        const line = { id: row.id, row, actor: null };
        const editing = open?.id === row.id;
        return (
          <SettingRow
            key={row.id}
            label={<span data-place-row={row.id}>{name}</span>}
            control={!editing && (
              <>
                <span className="text-body text-ink wrap-anywhere">{holdLine(t, row.hold)}</span>
                {mayEditLine(line, kept) && (
                  <Button
                    variant="ghost" disabled={editor.busy || open !== null} aria-label={t('settings.ai.roles.editOf', { role: name })}
                    onClick={() => setOpen(editingOfLine(line, { kept, works: catalogue?.works ?? [], agents: editor.agents, name, key: () => --keys.current, policy: null }))}
                  >
                    {t('settings.ai.role.edit')}
                  </Button>
                )}
              </>
            )}
          >
            {editing && (
              <RolesCard
                editorOnly
                catalogue={catalogue} read={editor.read} staffing={staffing} agents={editor.agents} offers={editor.offers} busy={editor.busy}
                onSet={editor.onSet} editor={open} onEditor={setOpen}
              />
            )}
          </SettingRow>
        );
      })}
    </SettingList>
  );
}
