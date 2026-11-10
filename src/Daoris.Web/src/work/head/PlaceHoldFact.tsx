import type { TFunction } from 'i18next';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { api, type Session } from '../../data/api';
import type { DetailRow } from './DetailsFold';

/** The states a line is out of its turn in: only there does its mark keep the checkout, since a running turn holds it by itself. */
const PARKED = new Set(['awaiting-person', 'completed']);

/** The uncommitted paths of its own a line out of its turn keeps its checkout with, or null where it keeps none. */
export const placeHolds = (session: Pick<Session, 'state' | 'holdsPlace'>): readonly string[] | null =>
  PARKED.has(session.state) && session.holdsPlace?.length ? session.holdsPlace : null;

/**
 * **A line holding its repository's checkout**: one writer works a place at a time, and a parked line that kept uncommitted paths
 * of its own keeps the checkout until its next turn commits them, they are discarded, or the person lets others work here.
 *
 * @remarks
 * The paths are the record's, named in the fact's title; the press sends the ledger's `ReleasePlace`, after which the next session
 * there is told those paths are another line's. Props only: the row's press holds the mutation.
 */
export function PlaceHoldFact({ paths, onRelease, pending = false, refusal }: {
  paths: readonly string[];
  onRelease: () => void;
  pending?: boolean;
  refusal?: string | null;
}) {
  const { t } = useTranslation();
  return (
    <span data-place-hold className="grid gap-1">
      <span className="flex flex-wrap items-center gap-2">
        <span title={paths.join('\n')}>{t('work.placeHold.holds', { count: paths.length })}</span>
        <Button variant="ghost" disabled={pending} onClick={onRelease}>{t('work.placeHold.release')}</Button>
      </span>
      {refusal && <span role="alert">{refusal}</span>}
    </span>
  );
}

/** The fact with its press: the ledger clears the mark, and the pushed sessions carry the record back. */
function PlaceHoldPress({ session, paths }: { session: string; paths: readonly string[] }) {
  const release = useMutation({ mutationFn: () => api.releasePlace(session) });
  return (
    <PlaceHoldFact
      paths={paths}
      pending={release.isPending}
      refusal={release.error ? String(release.error.message) : null}
      onRelease={() => release.mutate()}
    />
  );
}

/** *Details*' *Checkout* row: said only where a parked line keeps its checkout with paths of its own. */
export function placeHoldRow(t: TFunction, session: Session): DetailRow {
  const paths = placeHolds(session);
  return { label: t('work.placeHold.details'), value: paths ? <PlaceHoldPress session={session.id} paths={paths} /> : null };
}
