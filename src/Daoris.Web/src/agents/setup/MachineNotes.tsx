import { useTranslation } from 'react-i18next';
import { useActors, useHarnesses, useRoles, useStaffingFound } from '../../bridge/shell';
import { Button } from '../../design/ui';
import { suggestedRoles } from '../roles/SuggestedChoicesCard';

/**
 * **What waits on This machine's staffing** (SETUPUX1m, the setup-UX design §2.4): one line for the choices Daoris suggests and one for the
 * changes found outside Daoris, each a door to *All roles*, where the suggested-choices card and the found changes are answered. Nothing is
 * drawn where neither stands.
 *
 * A molecule: the counts arrive, and the press goes out.
 */
export function MachineNotesView({ suggested, found, onShow }: { suggested: number; found: number; onShow: () => void }) {
  const { t } = useTranslation();
  if (suggested === 0 && found === 0) return null;
  const lines = [
    ...(suggested > 0 ? [t('agents.setup.allRoles.suggested', { count: suggested })] : []),
    ...(found > 0 ? [t('agents.setup.allRoles.found', { count: found })] : []),
  ];
  return (
    <ul aria-label={t('agents.setup.allRoles.notes')} className="m-0 mb-1 grid list-none gap-0.5 p-0 text-small text-ink-soft">
      {lines.map((line) => (
        <li key={line} className="flex flex-wrap items-center gap-x-2">
          <span className="wrap-anywhere">{line}</span>
          <Button variant="ghost" className="px-0 py-0 text-small" aria-label={t('agents.setup.allRoles.showOf', { what: line })} onClick={onShow}>
            {t('agents.setup.allRoles.show')}
          </Button>
        </li>
      ))}
    </ul>
  );
}

/** The lines of the machine's page, read from the catalogue and the staffing the Roles section reads too. */
export function MachineNotes({ onShow }: { onShow: () => void }) {
  const roles = useRoles();
  const actors = useActors();
  const found = useStaffingFound();
  const roster = useHarnesses();
  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  const agents = harnesses.filter((door) => door.present).map((door) => door.harness);
  const suggested = roles.data ? suggestedRoles(roles.data, actors.data ?? [], agents) : 0;
  return <MachineNotesView suggested={suggested} found={found.data?.length ?? 0} onShow={onShow} />;
}
