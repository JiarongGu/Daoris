import { useEffect } from 'react';
import { useSetHold } from '../../bridge/driver';
import { usePlaceApply, usePlacePlan } from '../../bridge/shell';
import { useRegistry } from '../../data/queries';
import { workspaceOf } from '../../data/workspaces';
import type { Notify } from '../../design/ui';
import { sentence } from '../../locales/format';
import { NOT_DRIVEN, offered, isChosen, askOf, type SetUpPage, type SetUpPlace } from './placePlan';
import { PlaceSetUp } from './PlaceSetUp';

/**
 * **A place's *Set up…*** (AGENTSPLACE1j, the Agents-place design §4), the organism over `PlaceSetUp`: it reads the plan for the draft the
 * application holds and presses *Apply* as the person's own (`PLACE_APPLY`), straight to the route.
 *
 * @remarks
 * The plan is first read as *Not driven*, which changes nothing, to learn what the driver suggests; the suggestion is then preselected, so
 * the person reads a plan of a delivery chosen for them and not of none. *Hold it after driving* sets the repository's hold before the
 * apply (`SET_HOLD`), after which the plan reads no queued work as released.
 */
export function PlaceSetUpPanel({ place, page, notify }: { place: SetUpPlace; page: SetUpPage; notify: Notify }) {
  const draft = page.draftOf(place.id);
  const registry = useRegistry('machine');
  const hold = useSetHold();
  const apply = usePlaceApply();
  const ask = draft && draft.repositories.length > 0 ? askOf(draft) : null;
  const planned = usePlacePlan(ask);
  const plan = planned.plan;
  const { id } = place;
  const { change } = page;

  // The suggestion is the plan's own, preselected once and left to the person after.
  const waiting = draft?.preset === null && plan !== null && !planned.stale;
  const suggestion = plan?.repositories[0]?.suggested;
  useEffect(() => {
    if (waiting) change(id, { preset: suggestion?.preset ?? NOT_DRIVEN, adoption: suggestion?.adoption ?? false });
  }, [waiting, suggestion?.preset, suggestion?.adoption, id, change]);

  if (!draft) return null;
  const choices = place.kind === 'workspace'
    ? (registry.data ?? []).filter((row) => workspaceOf(row) === place.workspace)
      .map((row) => ({ repository: row.repository, driven: place.driven.includes(row.repository) }))
    : [];
  const failed = planned.error ?? apply.error;

  const onApply = () => {
    if (!plan || !ask) return;
    const accept = draft.accept.filter((one) => isChosen(offered(plan), one));
    apply.mutate({ ...ask, digest: plan.digest, accept }, {
      onSuccess: (result) => page.change(id, { result, ...(result.done ? { accept: [] } : {}) }),
    });
  };

  const onHold = async (repositories: string[]) => {
    try {
      for (const repository of repositories) await hold.mutateAsync({ repository, held: true });
      planned.again();
    } catch (error) {
      notify(sentence(error), 'error');
    }
  };

  return (
    <PlaceSetUp
      place={place.name}
      kind={place.kind}
      choices={choices}
      draft={draft}
      onChange={(patch) => page.change(id, patch)}
      plan={plan}
      reading={planned.reading}
      stale={planned.stale}
      problem={failed ? sentence(failed) : null}
      applying={apply.isPending}
      holding={hold.isPending}
      onApply={onApply}
      onHold={onHold}
      onClose={() => page.close(id)}
    />
  );
}
