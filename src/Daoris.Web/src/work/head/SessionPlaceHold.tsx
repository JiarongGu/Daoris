import type { Session } from '../../data/api';
import { useReleasePlace } from '../../data/queries';
import { PlaceHoldFact, placeHolds } from './PlaceHoldFact';

/** A parked line's hold on its checkout with *Let others work here*: the release's mutation, so `PlaceHoldFact` is handed it and holds none. */
export function SessionPlaceHold({ session }: { session: Pick<Session, 'id' | 'state' | 'holdsPlace'> }) {
  const release = useReleasePlace();
  const paths = placeHolds(session);
  if (!paths) return null;
  return (
    <PlaceHoldFact
      paths={paths}
      pending={release.isPending}
      refusal={release.error ? String(release.error.message) : null}
      onRelease={() => release.mutate(session.id)}
    />
  );
}
