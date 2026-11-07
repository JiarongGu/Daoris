import { useTranslation } from 'react-i18next';
import { holdLine } from '../../agents/roles/appointedHold';
import { placeSaid } from '../../agents/roles/SupervisorRulesFold';
import type { RoleHoldRead } from '../../data/wiring';

/** What the card of a supervisor shows: its names as written, its scope, and its record after the change and before it (none for a new one). */
export type HelpSupervisorShown = {
  id: string;
  names: string[];
  scope: { kind: string; id?: string | null };
  hold: RoleHoldRead;
  before?: RoleHoldRead | null;
};

/** A supervisor before and after a press, said as the role editor says an appointed role's line, in the person's language. */
export function SupervisorChange({ supervisor }: { supervisor: HelpSupervisorShown }) {
  const { t } = useTranslation();
  const place = supervisor.scope.kind === 'machine' ? 'machine' : `${supervisor.scope.kind}:${supervisor.scope.id ?? ''}`;
  const side = (label: string, hold: RoleHoldRead | null | undefined, which: 'before' | 'after') => (
    <section aria-label={label} data-side={which} className="m-0 mt-1">
      <p className="m-0 text-meta font-semibold text-ink-soft">{label}</p>
      <p className="m-0 text-meta text-ink-soft">{hold ? holdLine(t, hold) : t('help.proposal.supervisor.none')}</p>
    </section>
  );
  return (
    <div>
      <p className="m-0 mt-1 text-small text-ink">
        {t('help.proposal.supervisor.title', { name: supervisor.names.join(' · ') || supervisor.id, place: placeSaid(t, place) })}
      </p>
      {side(t('help.proposal.supervisor.before'), supervisor.before, 'before')}
      {side(t('help.proposal.supervisor.after'), supervisor.hold, 'after')}
    </div>
  );
}
