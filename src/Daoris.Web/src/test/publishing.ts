/**
 * The shell's state publisher, for a mocked shell (STATEHUB1f, D169): a test that presses a hook reads what the press moved
 * through the state hub, as a window does, since the hooks ask nothing again themselves. What each press declares it changes is
 * D169's tables, held route by route by the modules' `StatePublisherTests`; the presses the tests make are named here, and
 * every press STATEHUB1h declared, whose hooks ask nothing again themselves since it.
 *
 * A copy of the routes' `[Publishes]` marks, which `StatePublisherTests` reads from this file and holds press by press to the
 * mark (PUBLISHTWIN1), and every mark to a copy here (PUBLISHTWIN2): change a press's scopes or session field on both sides, or
 * declare a route and leave it out here, and that test fails. A press added here keeps
 * this shape, `ROUTE: { scopes: [...], session: '...' }`, which the test reads.
 */
export const DECLARED: Readonly<Record<string, { scopes: readonly string[]; session?: string; subject?: string }>> = {
  LAND_SESSION_TREE: { scopes: ['sessions', 'landing', 'diff', 'handOff', 'sweep', 'treesSync'], session: 'id' },
  // A line of work's one Accept and its ask's (ACCEPTALL1): every part's landing, so no one session's; under way for its entry or
  // its ask (ACCEPTALL1e).
  LAND_ENTRY: { scopes: ['sessions', 'landing', 'diff', 'handOff', 'sweep', 'treesSync'], subject: 'entry' },
  LAND_ASK: { scopes: ['sessions', 'landing', 'diff', 'handOff', 'sweep', 'treesSync'], subject: 'ask' },
  DISCARD_SESSION_TREE: { scopes: ['sessions', 'landing', 'diff', 'sweep', 'treesSync'], session: 'id' },
  HANDOFF: { scopes: ['handOff', 'sweep'], session: 'id' },
  SWEEP: { scopes: ['sessions', 'landing', 'diff', 'handOff', 'sweep', 'treesSync'] },
  DISCARD_SESSION_BRANCH: { scopes: ['sessions', 'landing', 'diff', 'handOff', 'sweep', 'treesSync'] },
  // RECOVERYDROP1: the person's delete of the recovery refs nothing holds; the work state asks again with the landings and the sweep.
  RECOVERY_DROP: { scopes: ['sessions', 'landing', 'diff', 'handOff', 'sweep', 'treesSync'] },
  TREES_SYNC: { scopes: ['sessions', 'landing', 'diff', 'handOff', 'sweep', 'treesSync'] },
  // FABRIC-PERFORM1g: the person's word on a held attempt; the work state, which lists what is held, asks again with the sessions.
  WORK_HELD: { scopes: ['sessions'] },
  // FABRIC-CUTOVER1f: the person's skip or stop of a run's node; the run's view, read under the driver's key, asks again.
  WORKFLOW_NODE: { scopes: ['driver'] },
  // The second opinion's presses, the landing's setting, the sessions' and work's moves, the rules' presses, a history clear and
  // a sync (PUBLISHTWIN2: every mark has its copy here, held two ways by `StatePublisherTests`).
  ASK_OPINION: { scopes: ['sessions', 'landing'], session: 'id' },
  OPINION_ANYWAY: { scopes: ['sessions', 'landing'], session: 'id' },
  OPINION_MYSELF: { scopes: ['sessions', 'landing'], session: 'id' },
  STOP_OPINION: { scopes: ['sessions', 'landing'] },
  SET_LANDING: { scopes: ['landing', 'lines'] },
  START_CHAT: { scopes: ['sessions', 'driver'] },
  SESSION_START_FROM: { scopes: ['sessions', 'driver'], session: 'id' },
  SESSION_GO_ON_BRIEF: { scopes: ['sessions', 'driver'], session: 'id' },
  SESSION_STOP_WAITING: { scopes: ['sessions', 'driver'], session: 'id' },
  SESSION_HAND_OVER: { scopes: ['sessions', 'driver'], session: 'id' },
  STOP_SESSION: { scopes: ['sessions', 'driver'], session: 'id' },
  RESOLVE_SESSION: { scopes: ['sessions', 'quests', 'driver'], session: 'id' },
  SESSION_GO_ON_NEW: { scopes: ['sessions', 'driver'], session: 'id' },
  WORK_PAUSE: { scopes: ['sessions', 'quests', 'asks', 'driver'] },
  WORK_RESUME: { scopes: ['sessions', 'quests', 'asks', 'driver'] },
  WORK_ABANDON: { scopes: ['sessions', 'quests', 'asks', 'driver'] },
  RULE_ACTION: { scopes: ['rules'] },
  RULE_PROPOSAL: { scopes: ['rules'] },
  HISTORY_CLEAR: { scopes: ['sessions', 'quests', 'asks', 'driver', 'history'] },
  SYNC_NOW: { scopes: ['sessions', 'quests', 'sync', 'registry'] },
  SESSION_ARCHIVE: { scopes: ['sessions'] },
  SESSION_INPUT: { scopes: ['sessions', 'help'], session: 'id' },
  SESSION_DELETE: { scopes: ['sessions', 'help'], session: 'id' },
  END_CHAT: { scopes: ['sessions'], session: 'id' },
  // A helper asked of a session, and the person's yes to an agent's ask (MIXAGENT2b): files the loop's next look takes; and
  // their no (HELPERNO1), the ask refused and its note written at once.
  SESSION_HELPER_ASK: { scopes: ['sessions', 'driver'], session: 'id' },
  SESSION_HELPER_APPROVE: { scopes: ['sessions', 'driver'], session: 'session' },
  SESSION_HELPER_DECLINE: { scopes: ['sessions', 'driver'], session: 'session' },
  // A piece asked of another agent for a session (COAGENT1b): a file the loop's next look takes.
  SESSION_PARTNER_ASK: { scopes: ['sessions', 'driver'], session: 'id' },
  // The person's word on a third answer alone held for them (COWORK1e): the mark taken away or kept, the look nudged.
  SESSION_ANSWER_ALONE: { scopes: ['sessions', 'driver'], session: 'session' },
  // The plugins' presses (STATEHUB1d), which the Plugins view's tests publish; and since HELPMODE1c3 the driver's, whose
  // helpGrants draws Ask Daoris's browser grant from the plugin's approval.
  PLUGIN_ACTION: { scopes: ['driver', 'plugins', 'harnesses'] },
  PLUGIN_UPDATE: { scopes: ['driver', 'plugins', 'harnesses'] },
  PLUGIN_INSTALL: { scopes: ['driver', 'plugins', 'harnesses'] },
  PLUGIN_ADD: { scopes: ['driver', 'plugins', 'harnesses'] },
  // The remaining presses (STATEHUB1h): the standing choices, a go-ahead, a turn stopped and a word taken back, and what the
  // accounts, agents, asks, across, registry, tools and help domains press.
  SET_DRIVABLE: { scopes: ['driver'] },
  SET_HOLD: { scopes: ['driver'] },
  SET_TREES: { scopes: ['driver'] },
  SET_STANDING: { scopes: ['driver'] },
  SET_HEAVY_GATES: { scopes: ['driver'] },
  SET_SESSION_CHECKS: { scopes: ['driver'] },
  SET_NOTIFY: { scopes: ['driver'] },
  SET_STRIKES: { scopes: ['driver'] },
  SET_COOLOFF: { scopes: ['driver'] },
  SET_CAP: { scopes: ['driver'] },
  SET_KEEP: { scopes: ['driver', 'wiring'] },
  SET_ACTOR: { scopes: ['driver', 'harnesses'] },
  SET_HELP_MODE: { scopes: ['driver'] },
  SET_HELP_GRANT: { scopes: ['driver'] },
  TRUST_FOLDER: { scopes: ['driver'] },
  RETRY_QUEST: { scopes: ['sessions', 'driver'] },
  SHOW_REVIEW_AGAIN: { scopes: ['driver'] },
  SET_LINE: { scopes: ['driver'] },
  SET_LANGUAGE: { scopes: ['driver'] },
  SET_REVIEW: { scopes: ['driver'] },
  SET_OPINION: { scopes: ['driver'] },
  SET_COMMITS: { scopes: ['driver'] },
  SET_HELPERS: { scopes: ['driver'] },
  SET_HAND_OVER: { scopes: ['driver'] },
  SESSION_HAND_OVER_APPROVE: { scopes: ['sessions', 'driver'], session: 'session' },
  SESSION_HAND_OVER_DECLINE: { scopes: ['sessions', 'driver'], session: 'session' },
  SESSION_QUIET_CARRY_ON: { scopes: ['sessions', 'driver'], session: 'id' },
  SESSION_QUIET_LEAVE: { scopes: ['sessions'], session: 'id' },
  SET_ACCESS: { scopes: ['driver'] },
  // TERMTRUST1a (D201 point 5): the person's grant or revoke of the standing confirmation, in the window alone.
  SET_STANDING_CONFIRMATION: { scopes: ['driver'] },
  SET_READ_ACROSS: { scopes: ['driver'] },
  SET_WRITE_ACROSS: { scopes: ['driver'] },
  SESSION_GO_AHEAD: { scopes: ['sessions', 'asks'], session: 'id' },
  CANCEL_TURN: { scopes: ['sessions'], session: 'id' },
  SESSION_QUEUE_EDIT: { scopes: ['sessions'], session: 'session' },
  ACCOUNT_USE: { scopes: ['harnesses'] },
  // The accounts' *Read now* and the read as Agents opens (SWITCH1d): each account's use read into its windows.
  ACCOUNTS_READ: { scopes: ['harnesses'] },
  HARNESS_ACTION: { scopes: ['harnesses'] },
  HARNESSES_REFRESH: { scopes: ['harnesses'] },
  SET_AGENT_SETTINGS: { scopes: ['harnesses'] },
  CHANNEL_POST: { scopes: ['asks'] },
  CHANNEL_MARK: { scopes: ['asks'] },
  // NOTIFY2f: the person's notices read or dismissed, which the notices slice pushes.
  NOTICES_READ: { scopes: ['notices'] },
  NOTICE_DISMISS: { scopes: ['notices'] },
  // COWORK1d: the person's *Send to its session*, whose receipts the ask's channel shows.
  CHANNEL_FORWARD: { scopes: ['asks'] },
  REGISTRY_REFRESH: { scopes: ['registry'] },
  // A set-up's press (LAYOUT8): one ask and its quest, and the repository's rule.
  SETUP_PRESS: { scopes: ['quests', 'asks', 'rules'] },
  TOOLS_USE: { scopes: ['tools'] },
  TOOLS_DOWNLOAD: { scopes: ['tools'] },
  TOOLS_DELETE: { scopes: ['tools'] },
  // CACHECLEAR1: the Downloads row's press; the agents' and tools' pages and This Daoris's sizes ask again.
  SYSTEM_DOWNLOADS_CLEAR: { scopes: ['harnesses', 'tools', 'wiring'] },
  SYSTEM_SPACE_FREE: { scopes: ['harnesses', 'tools', 'wiring'] },
  TOOLS_LOOK: { scopes: ['tools'] },
  TOOLS_LOCATION: { scopes: ['tools'] },
  START_HELP: { scopes: ['sessions'] },
  HELP_RENAME: { scopes: ['sessions', 'help'], session: 'id' },
  HELP_PIN: { scopes: ['sessions', 'help'], session: 'id' },
  HELP_START_FROM: { scopes: ['sessions', 'help'], session: 'id' },
  HELP_DISMISS: { scopes: ['help'] },
  HELP_APPLY: {
    scopes: ['sessions', 'quests', 'asks', 'driver', 'rules', 'history', 'registry', 'plugins', 'harnesses', 'help', 'browser'],
  },
  // The browser module's presses (BROWSERPUBLISH1), `DAORIS.BROWSER`'s, which the publisher reads beside the driver's.
  ADD_FAVORITE: { scopes: ['browser'] },
  REMOVE_FAVORITE: { scopes: ['browser'] },
  SET_EXTENSIONS: { scopes: ['browser'] },
  SET_BROWSER: { scopes: ['browser'] },
  SET_LINKS: { scopes: ['browser'] },
};

/** The modules whose routes carry marks the publisher reads: the driver's, and the browser module's. */
const PUBLISHING = new Set(['DAORIS.DRIVER', 'DAORIS.BROWSER']);

type Invoke = (module: string, type: string, options?: { payload?: unknown }) => Promise<unknown>;

/** Where a test's mocked `useShenoraEvent` keeps the page's handler for a Daoris event, by its type. */
export type Hear = (type: 'STATE_CHANGED' | 'UNDER_WAY', payload: unknown) => void;

/**
 * `invoke` answered as the shell answers with `StatePublisher` in its pipeline: a declared press of the driver's is told under way,
 * whole and numbered, from its send until it answers or throws, and what it changed is published once it answered, a refusal
 * answered as data among it and one thrown not. Each reaches `hear` on a later turn, as the kit's pump delivers it.
 */
export function publishing(invoke: Invoke, hear: Hear): Invoke {
  const underWay: { route: string; session?: string; subject?: string }[] = [];
  let seq = 0;
  const tell = (type: 'STATE_CHANGED' | 'UNDER_WAY', payload: unknown) => { setTimeout(() => hear(type, payload), 0); };
  const telling = () => tell('UNDER_WAY', { seq: ++seq, presses: [...underWay] });
  return async (module, type, options) => {
    const declared = PUBLISHING.has(module) ? DECLARED[type] : undefined;
    if (!declared) return invoke(module, type, options);
    const field = (name?: string) => {
      const named = name ? (options?.payload as Record<string, unknown> | undefined)?.[name] : undefined;
      return typeof named === 'string' && named.length > 0 ? named : undefined;
    };
    const session = field(declared.session);
    const subject = field(declared.subject);
    const press = { route: type, ...(session ? { session } : {}), ...(subject ? { subject } : {}) };
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
