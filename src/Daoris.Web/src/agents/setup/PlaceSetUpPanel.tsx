import { useEffect } from 'react';
import { useSetHold, usePlaces } from '../../bridge/driver';
import { usePlaceApply, usePlacePlan } from '../../bridge/shell';
import { useWorkflowList } from '../../bridge/repositories/workflowEdit';
import { useRegistry } from '../../data/queries';
import { workspaceOf } from '../../data/workspaces';
import type { Notify } from '../../design/ui';
import { sentence } from '../../locales/format';
import { askOf, deliveriesOf, isChosen, NOT_DRIVEN, offered, type SetUpDoor, type SetUpPage, type SetUpPlace, suggestionsOf } from './placePlan';
import { PlaceSetUp } from './PlaceSetUp';

/**
 * **A place's *Set up…*** (AGENTSPLACE1j, the Agents-place design §4), the organism over `PlaceSetUp`: it reads the plan for the draft the
 * application holds and presses *Apply* as the person's own (`PLACE_APPLY`), straight to the route.
 *
 * @remarks
 * The plan is first read as *Not driven*, which changes nothing, to learn what the driver suggests; the suggestion is then preselected where
 * every repository suggests the same, so the person reads a plan of a choice made for them and not of none, and where they differ nothing is
 * preselected (SETUPUX1j). The deliveries come from the data (`WORKFLOW_LIST`), a workspace's repositories from its whole list (`PLACES`
 * `members`). *Hold it after driving* sets the repository's hold before the apply (`SET_HOLD`), after which the plan reads no queued work as
 * released.
 */
export function PlaceSetUpPanel({ place, page, notify, doorFor }: { place: SetUpPlace; page: SetUpPage; notify: Notify; doorFor?: SetUpDoor }) {
  const draft = page.draftOf(place.id);
  const registry = useRegistry('machine');
  const workflows = useWorkflowList();
  const members = usePlaces(place.kind === 'workspace' ? { members: place.workspace } : {}, { enabled: place.kind === 'workspace' });
  const hold = useSetHold();
  const apply = usePlaceApply();
  const ask = draft && draft.repositories.length > 0 ? askOf(draft) : null;
  const planned = usePlacePlan(ask);
  const plan = planned.plan;
  const { id } = place;
  const { change } = page;

  // The suggestion is the plan's own, preselected once where the repositories agree, and left to the person after.
  const suggestion = suggestionsOf(plan);
  const waiting = draft?.preset === null && plan !== null && !planned.stale;
  const next = waiting ? (suggestion ? suggestion.preset : NOT_DRIVEN) : null;
  const adoption = suggestion?.adoption ?? false;
  useEffect(() => {
    if (next !== null) change(id, { preset: next, adoption });
  }, [next, adoption, id, change]);

  if (!draft) return null;
  const listed = members.data?.members?.rows;
  const choices = place.kind !== 'workspace'
    ? []
    : listed
      ? listed.map((row) => ({ repository: row.repository, driven: row.driven }))
      : (registry.data ?? []).filter((row) => workspaceOf(row) === place.workspace)
        .map((row) => ({ repository: row.repository, driven: place.driven.includes(row.repository) }));
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
      deliveries={deliveriesOf(workflows.data?.presets ?? [])}
      doorFor={doorFor}
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
