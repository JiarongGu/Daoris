import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import type { Session } from '../../data/api';
import { Tip } from '../../design/ui';
import { cn } from '../../lib/cn';
import { choiceChip } from '../actors';
import { AccessChip, startedFull } from '../head/AccessFact';
import { ChoiceChip } from '../head/ChoiceChip';
import type { SupervisionChip } from '../head/lineSupervision';

/** Whether a session's head wears any chip: a supervisor's count, a choice past the first, or a full-access start. */
export function wearsChips(t: TFunction, session: Pick<Session, 'choice' | 'chosen' | 'access'>, supervision: SupervisionChip | null): boolean {
  return supervision !== null || choiceChip(t, session) !== null || startedFull(session);
}

/**
 * **The head's chips**, last on its where-and-who line so the line is cut before them, never them: the count a supervisor's rule
 * holds the line at or one short of it (`supervisionChip`, its rule in its tip), the choice its actor ran where it is not the first
 * (`ChoiceChip`, its sentence in an ⓘ where the line is narrow), and *Full access* where its record says it started so
 * (`AccessChip`). Text, not controls; none is drawn where none holds.
 */
export function HeadChips({ session, supervision, narrow = false }: {
  session: Pick<Session, 'choice' | 'chosen' | 'access'>;
  supervision: SupervisionChip | null;
  narrow?: boolean;
}) {
  const { t } = useTranslation();
  if (!wearsChips(t, session, supervision)) return null;
  return (
    <span data-head-chips="" className="inline-flex shrink-0 items-center gap-1.5">
      {supervision && (
        <Tip content={t('work.supervision.chipTip', { at: supervision.at, n: supervision.n })}>
          <span
            data-supervision-chip={supervision.held ? 'held' : 'near'}
            className={cn('text-small', supervision.held ? 'font-medium text-ink' : 'text-ink-faint')}
          >
            {supervision.text}
          </span>
        </Tip>
      )}
      <ChoiceChip session={session} narrow={narrow} />
      {startedFull(session) && <AccessChip />}
    </span>
  );
}
