/** The ways a `land` lands (`LandNodes.Methods`); a push is a `push` step after a branch landing, a pull request that step and the plugin's own. */
export const LAND_METHODS = ['merge', 'branch', 'integrate'] as const;

/** The steps a method brings after the landing, in order (`NodeGraphs`' `land-tail`). */
const TAILS: Readonly<Record<string, readonly string[]>> = { integrate: ['integrate'] };

export const tailOf = (method: string | null): readonly string[] => (method === null ? [] : TAILS[method] ?? []);

export const TAIL_KINDS: readonly string[] = ['integrate'];

/** The options a method refuses (`LandNodes.Problem`): a merge makes no branch to name, an integrate no squash. */
export const REFUSED: Readonly<Record<string, readonly string[]>> = { merge: ['pattern'], integrate: ['squash'] };

/** Whether a land option means something under the method it is held beside; `method` unset leaves each to the repository's rule. */
export function landOptionApplies(method: string | null, name: string): boolean {
  return method === null || !(REFUSED[method] ?? []).includes(name);
}
