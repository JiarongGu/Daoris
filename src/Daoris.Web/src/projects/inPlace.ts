import type { PlaceApplied, PlaceAsk, PlacePlanned } from '../agents/setup/placePlan';
import { call } from '../bridge/call';

/** The shipped workflow whose work is done in its department's own place and lands nothing: every kind of department can follow it. */
export const IN_PLACE = 'in-place';

/** A department's kind, as the add registers it. */
export type DepartmentKind = 'git' | 'folder' | 'system';

/**
 * Whether the add chooses *Work in place*: a folder and a system always, since no other shipped workflow runs there, and a
 * repository where the person ticks it.
 */
export const choosesInPlace = (kind: DepartmentKind, ticked: boolean): boolean => kind !== 'git' || ticked;

/** As long as a set-up's plan and apply may take, as the set-up's own page waits. */
const SETUP_BOUND = 2 * 60_000;

/**
 * The add's choice of *Work in place* for a department it just registered: a set-up's plan of one (`PLACE_PLAN`) and its apply by
 * the plan's digest (`PLACE_APPLY`), the add's press being the person's, the twin of `daoris place <department> --preset in-place
 * --apply`. A plan that refuses is answered as an apply not done, with its refusals' sentences, and nothing is applied.
 */
export async function chooseInPlace(repository: string): Promise<PlaceApplied> {
  const ask: PlaceAsk = { repositories: [repository], preset: IN_PLACE, opinion: false, look: false, adoption: false, drive: false };
  const plan = await (call('PLACE_PLAN', ask, { timeoutMs: SETUP_BOUND }) as Promise<PlacePlanned>);
  if (plan.refusals.length > 0) {
    return { done: false, refused: plan.refusals[0]!.code, message: plan.refusals.map((each) => each.sentence).join(' '), digest: plan.digest, operations: [] };
  }
  return call('PLACE_APPLY', { ...ask, digest: plan.digest, accept: [] }, { timeoutMs: SETUP_BOUND }) as Promise<PlaceApplied>;
}
