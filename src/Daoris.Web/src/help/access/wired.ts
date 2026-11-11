// Ask Daoris's mode and its receipts as shapes: the bridge answers them, the panel's molecules draw them. Types alone, so a molecule
// never takes them from a bridge domain (`design/presentational.test.ts`).

/** Ask Daoris's access modes: restricted, which reads and proposes, or full, which acts itself. */
export type HelpMode = 'restricted' | 'full';

/**
 * A server on a receipt (`HelpWiredServer`): its name, the plugin that declares it (null for Daoris's own
 * connector), its state (`handed`, `full-access`, `withheld`), its tools where known, and why it is withheld.
 */
export type HelpWiredServer = { name: string; plugin: string | null; state: string; tools: string[] | null; why: string | null };

/** A plugin's skill on a receipt (`HelpWiredSkill`): `plugin:skill`, its state (`handed`, `withheld`, `unverified`) and why. */
export type HelpWiredSkill = { name: string; state: string; why: string | null };

/** The permission rules a start handed, list for list as the settings file holds them (`HelpWiredRules`). */
export type HelpWiredRules = { allow: string[]; ask: string[]; deny: string[] };

/** A checkout reading across lets it read (`HelpRead`): its name, its workspace and its path on this machine. */
export type HelpWiredCheckout = { name: string; workspace: string; path: string };

/**
 * What restricted mode lacks (`HelpWiredLack`): its kind (`server`, `shell`, `edits`), its name, the door that grants it
 * (`plugins`, `mode`, `host`, `harness`), why, and the plugin whose page holds the door where it is one.
 */
export type HelpWiredLack = { kind: string; name: string; door: string; why: string; plugin: string | null };

/**
 * What the agent's door said it loaded at a start (`WireLoaded`): the tools, servers with the status the door gave them,
 * skills and plugins its init frame names. Kept apart from what is handed, so where the two differ it shows.
 */
export type HelpWiredLoaded = { tools: string[]; servers: { name: string; status: string }[]; skills: string[]; plugins: string[] };

/**
 * What one start of an Ask Daoris conversation handed its agent, as the driver
 * keeps it (`HelpReceipt`), field for field: recorded at the start, never worked out again. Its paths are this machine's.
 */
export type HelpReceipt = {
  at: string;
  /** `new` for a conversation's first start, `going-on` for one going on. */
  start: string;
  /** The mode it ran under, the effective one. */
  mode: string;
  /** The door it ran on, by its id. */
  door: string;
  version: string | null;
  account: string | null;
  /** `native` or `protocol`. */
  transport: string;
  posture: string | null;
  permissionMode: string | null;
  folder: string;
  /** Daoris's own tools its allow-list allows, as the rules spell them; none in full access. */
  tools: string[];
  skills: HelpWiredSkill[];
  servers: HelpWiredServer[];
  rules: HelpWiredRules | null;
  guarded: boolean;
  checkouts: HelpWiredCheckout[];
  /** In restricted mode, what it lacks with the door that grants each; none in full access. */
  lacks: HelpWiredLack[];
  /** What its door said it loaded; null until its init frame came, and on a start before it or a door that sends none. */
  loaded?: HelpWiredLoaded | null;
};

/** `HELP_WIRED`'s answer: a conversation's receipts, the newest first; `recorded: false` for one started before they were kept. */
export type HelpWired = { session: string; recorded: boolean; receipts: HelpReceipt[] };

/**
 * The grants of restricted mode's configured access, in the table's order:
 * the browser, reading checkouts, the families of acts (pauses, clean-up, discarding a session branch, the chosen settings), and a
 * confined shell, later.
 */
export type HelpGrantId = 'browser' | 'checkouts' | 'acts' | 'shell';

/**
 * A grant's state as its row says it: off or on by the operations chosen; the browser waiting for approval, or changed
 * since it is approved; unavailable where this agent cannot be held to it; and the confined shell, not available yet.
 */
export type HelpGrantState = 'off' | 'on' | 'needs-approval' | 'changed' | 'unavailable' | 'later';

/**
 * The agent Ask Daoris runs on, as the grants read it: its name as a person knows it, and whether it takes Claude's permission
 * rules (a receipt's `rules`), which hold the browser's chosen writes; the checkouts' read is Daoris's own.
 */
export type HelpGrantAgent = { name: string; rules: boolean };

/**
 * One grant as this machine has it configured: the operations chosen, by their
 * codes in the table (`help/access/grants.ts`); for the browser, whether its plugin's update changed it since it is approved; and
 * whether a conversation running started with another choice, which takes its next turn (a grant is
 * never shown in force before the agent is held to it).
 */
export type HelpGrantChoice = {
  id: HelpGrantId;
  chosen: readonly string[];
  /** The browser's server changed since it is approved: Ask Daoris is handed nothing of it until it is approved again. */
  changed?: boolean;
  pending?: boolean;
  /**
   * For a grant set on a plugin's own tab, the browser's: the plugin whose
   * approval it is, which its door opens; none where no plugin here offers it.
   */
  plugin?: string | null;
};

/** What the chip reads of a plugin to say what full access would add: whether it runs, and the servers it offers Ask Daoris. */
export type HelpPluginOffer = {
  enabled: boolean;
  problem?: unknown;
  helpServers?: readonly { name: string; state?: string }[] | null;
};
