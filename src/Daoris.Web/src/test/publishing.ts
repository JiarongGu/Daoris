/**
 * The shell's state publisher, for a mocked shell (STATEHUB1f, D169): a test that presses a hook reads what the press moved
 * through the state hub, as a window does, since the hooks ask nothing again themselves. What each press declares it changes is
 * D169's tables, held route by route by the modules' `StatePublisherTests`; only the presses the tests make are named here.
 */
export const DECLARED: Readonly<Record<string, { scopes: readonly string[]; session?: string }>> = {
  LAND_SESSION_TREE: { scopes: ['sessions', 'landing', 'diff', 'handOff', 'sweep', 'treesSync'], session: 'id' },
  DISCARD_SESSION_TREE: { scopes: ['sessions', 'landing', 'diff', 'sweep', 'treesSync'], session: 'id' },
  HANDOFF: { scopes: ['handOff', 'sweep'], session: 'id' },
  SWEEP: { scopes: ['sessions', 'landing', 'diff', 'handOff', 'sweep', 'treesSync'] },
  DISCARD_SESSION_BRANCH: { scopes: ['sessions', 'landing', 'diff', 'handOff', 'sweep', 'treesSync'] },
  TREES_SYNC: { scopes: ['sessions', 'landing', 'diff', 'handOff', 'sweep', 'treesSync'] },
  SESSION_ARCHIVE: { scopes: ['sessions'] },
  SESSION_INPUT: { scopes: ['sessions'], session: 'id' },
  END_CHAT: { scopes: ['sessions'], session: 'id' },
};

type Invoke = (module: string, type: string, options?: { payload?: unknown }) => Promise<unknown>;

/** Where a test's mocked `useShenoraEvent` keeps the page's handler for a Daoris event, by its type. */
export type Hear = (type: 'STATE_CHANGED' | 'UNDER_WAY', payload: unknown) => void;

/**
 * `invoke` answered as the shell answers with `StatePublisher` in its pipeline: a declared press of the driver's is told under way,
 * whole and numbered, from its send until it answers or throws, and what it changed is published once it answered, a refusal
 * answered as data among it and one thrown not. Each reaches `hear` on a later turn, as the kit's pump delivers it.
 */
export function publishing(invoke: Invoke, hear: Hear): Invoke {
  const underWay: { route: string; session?: string }[] = [];
  let seq = 0;
  const tell = (type: 'STATE_CHANGED' | 'UNDER_WAY', payload: unknown) => { setTimeout(() => hear(type, payload), 0); };
  const telling = () => tell('UNDER_WAY', { seq: ++seq, presses: [...underWay] });
  return async (module, type, options) => {
    const declared = module === 'DAORIS.DRIVER' ? DECLARED[type] : undefined;
    if (!declared) return invoke(module, type, options);
    const named = declared.session ? (options?.payload as Record<string, unknown> | undefined)?.[declared.session] : undefined;
    const session = typeof named === 'string' && named.length > 0 ? named : undefined;
    const press = session ? { route: type, session } : { route: type };
    underWay.push(press);
    telling();
    try {
      const answered = await invoke(module, type, options);
      tell('STATE_CHANGED', session ? { scopes: declared.scopes, sessions: [session] } : { scopes: declared.scopes });
      return answered;
    } finally {
      underWay.splice(underWay.indexOf(press), 1);
      telling();
    }
  };
}
