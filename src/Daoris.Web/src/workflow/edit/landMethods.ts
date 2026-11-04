/** The five ways a `land` lands (`LandNodes.Methods`). */
export const LAND_METHODS = ['merge', 'branch', 'push', 'pull-request', 'integrate'] as const;

/** The steps a method brings after the landing, in order (`NodeGraphs`' `land-tail`): a pull request is pushed, then completed on the host. */
const TAILS: Readonly<Record<string, readonly string[]>> = { push: ['pushed'], 'pull-request': ['pushed', 'pull-request'], integrate: ['integrate'] };

export const tailOf = (method: string | null): readonly string[] => (method === null ? [] : TAILS[method] ?? []);

export const TAIL_KINDS: readonly string[] = ['pushed', 'pull-request', 'integrate'];

/** The options a method refuses (`LandNodes.Problem`): a merge makes no branch to name nor plugin to push it, an integrate neither a plugin nor a squash. */
export const REFUSED: Readonly<Record<string, readonly string[]>> = { merge: ['pattern', 'plugin'], integrate: ['plugin', 'squash'] };

/** Whether a land option means something under the method it is held beside; `method` unset leaves each to the repository's rule. */
export function landOptionApplies(method: string | null, name: string): boolean {
  return method === null || !(REFUSED[method] ?? []).includes(name);
}
