/**
 * The shell's state publisher, for a mocked shell (STATEHUB1f, D169): a test that presses a hook reads what the press moved
 * through the state hub, as a window does, since the hooks ask nothing again themselves. What each press declares it changes is
 * D169's tables, held route by route by the modules' `StatePublisherTests`; the presses the tests make are named here, and
 * every press STATEHUB1h declared, whose hooks ask nothing again themselves since it.
 *
 * A copy of the routes' `[Publishes]` marks, which `StatePublisherTests` reads from this file and holds press by press to the
 * mark (PUBLISHTWIN1): change a press's scopes or session field on both sides, or that test fails. A press added here keeps
 * this shape, `ROUTE: { scopes: [...], session: '...' }`, which the test reads.
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
  // The plugins' presses (STATEHUB1d), which the Plugins view's tests publish.
  PLUGIN_ACTION: { scopes: ['plugins', 'harnesses'] },
  PLUGIN_UPDATE: { scopes: ['plugins', 'harnesses'] },
  PLUGIN_INSTALL: { scopes: ['plugins', 'harnesses'] },
  PLUGIN_ADD: { scopes: ['plugins', 'harnesses'] },
  // The remaining presses (STATEHUB1h): the standing choices, a go-ahead, a turn stopped and a word taken back, and what the
  // accounts, agents, asks, across, registry, tools and help domains press.
  SET_DRIVABLE: { scopes: ['driver'] },
  SET_HOLD: { scopes: ['driver'] },
  SET_TREES: { scopes: ['driver'] },
  SET_STANDING: { scopes: ['driver'] },
  SET_NOTIFY: { scopes: ['driver'] },
  SET_STRIKES: { scopes: ['driver'] },
  SET_COOLOFF: { scopes: ['driver'] },
  SET_INTAKE: { scopes: ['driver', 'harnesses'] },
  SET_HELPER: { scopes: ['driver'] },
  TRUST_FOLDER: { scopes: ['driver'] },
  RETRY_QUEST: { scopes: ['sessions', 'driver'] },
  SHOW_REVIEW_AGAIN: { scopes: ['driver'] },
  SET_LINE: { scopes: ['driver'] },
  SET_LANGUAGE: { scopes: ['driver'] },
  SET_REVIEW: { scopes: ['driver'] },
  SET_OPINION: { scopes: ['driver'] },
  SET_COMMITS: { scopes: ['driver'] },
  SET_HELPERS: { scopes: ['driver'] },
  SET_READ_ACROSS: { scopes: ['driver'] },
  SET_WRITE_ACROSS: { scopes: ['driver'] },
  SESSION_GO_AHEAD: { scopes: ['sessions', 'asks'], session: 'id' },
  CANCEL_TURN: { scopes: ['sessions'], session: 'id' },
  SESSION_QUEUE_EDIT: { scopes: ['sessions'], session: 'session' },
  ACCOUNT_USE: { scopes: ['harnesses'] },
  HARNESS_ACTION: { scopes: ['harnesses'] },
  SET_AGENT_SETTINGS: { scopes: ['harnesses'] },
  CHANNEL_POST: { scopes: ['asks'] },
  CHANNEL_MARK: { scopes: ['asks'] },
  REGISTRY_REFRESH: { scopes: ['registry'] },
  TOOLS_USE: { scopes: ['tools'] },
  TOOLS_DOWNLOAD: { scopes: ['tools'] },
  TOOLS_DELETE: { scopes: ['tools'] },
  TOOLS_LOOK: { scopes: ['tools'] },
  TOOLS_LOCATION: { scopes: ['tools'] },
  START_HELP: { scopes: ['sessions'] },
  HELP_RENAME: { scopes: ['sessions', 'help'], session: 'id' },
  HELP_PIN: { scopes: ['sessions', 'help'], session: 'id' },
  HELP_START_FROM: { scopes: ['sessions', 'help'], session: 'id' },
  HELP_DISMISS: { scopes: ['help'] },
  HELP_APPLY: {
    scopes: ['sessions', 'quests', 'asks', 'driver', 'history', 'registry', 'plugins', 'harnesses', 'help', 'browser'],
  },
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
