import { useTranslation } from 'react-i18next';
import { useActors, useHarnesses, useRoles } from '../../bridge/shell';
import { Button } from '../../design/ui';
import { suggestedRoles } from '../roles/SuggestedChoicesCard';

/**
 * **What waits on This machine's staffing** (SETUPUX1m, the setup-UX design §2.4): one line for the choices Daoris suggests, a door to
 * *All roles*, where the suggested-choices card is answered. Nothing is drawn where none stands.
 *
 * A molecule: the count arrives, and the press goes out.
 */
export function MachineNotesView({ suggested, onShow }: { suggested: number; onShow: () => void }) {
  const { t } = useTranslation();
  if (suggested === 0) return null;
  const line = t('agents.setup.allRoles.suggested', { count: suggested });
  return (
    <ul aria-label={t('agents.setup.allRoles.notes')} className="m-0 mb-1 grid list-none gap-0.5 p-0 text-small text-ink-soft">
      <li className="flex flex-wrap items-center gap-x-2">
        <span className="wrap-anywhere">{line}</span>
        <Button variant="ghost" className="px-0 py-0 text-small" aria-label={t('agents.setup.allRoles.showOf', { what: line })} onClick={onShow}>
          {t('agents.setup.allRoles.show')}
        </Button>
      </li>
    </ul>
  );
}

/** The line of the machine's page, read from the catalogue and the staffing the Roles section reads too. */
export function MachineNotes({ onShow }: { onShow: () => void }) {
  const roles = useRoles();
  const actors = useActors();
  const roster = useHarnesses();
  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  const agents = harnesses.filter((door) => door.present).map((door) => door.door);
  const suggested = roles.data ? suggestedRoles(roles.data, actors.data ?? [], agents) : 0;
  return <MachineNotesView suggested={suggested} onShow={onShow} />;
}
