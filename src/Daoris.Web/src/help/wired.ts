// Ask Daoris's mode and its receipts as shapes (HELPMODE1b, HELPWIRED1; D183 points 1 and 4): the bridge answers them, and the
// panel's molecules draw them. Types alone, so a molecule takes them here and never from a bridge domain (the presentational
// boundary, `design/presentational.test.ts`).

/** Ask Daoris's access modes (D183 point 1): restricted, which reads and proposes, or full, which acts itself. */
export type HelpMode = 'restricted' | 'full';

/**
 * A server on a receipt (HELPWIRED1, `HelpWiredServer`): its name, the plugin that declares it (null for Daoris's own
 * connector), its state (`handed`, `full-access`, `withheld`), its tools where known, and why it was withheld.
 */
export type HelpWiredServer = { name: string; plugin: string | null; state: string; tools: string[] | null; why: string | null };

/** A plugin's skill on a receipt (`HelpWiredSkill`): `plugin:skill`, its state (`handed`, `withheld`, `unverified`) and why. */
export type HelpWiredSkill = { name: string; state: string; why: string | null };

/** The permission rules a start handed, list for list as the settings file holds them (`HelpWiredRules`). */
export type HelpWiredRules = { allow: string[]; ask: string[]; deny: string[] };

/** A checkout reading across lets it read (D107, `HelpRead`): its name, its workspace and its path on this machine. */
export type HelpWiredCheckout = { name: string; workspace: string; path: string };

/**
 * What restricted mode lacks (`HelpWiredLack`): its kind (`server`, `shell`, `edits`), its name, the door that grants it
 * (`plugins`, `mode`, `host`, `harness`), why, and the plugin whose page holds the door where it is one.
 */
export type HelpWiredLack = { kind: string; name: string; door: string; why: string; plugin: string | null };

/**
 * What one start of an Ask Daoris conversation handed its agent (HELPWIRED1, D183 point 4, the modes design §6), as the driver
 * keeps it (`HelpReceipt`), field for field: recorded at the start, never worked out again. Its paths are this machine's.
 */
export type HelpReceipt = {
  at: string;
  /** `new` for a conversation's first start, `going-on` for one going on. */
  start: string;
  /** The mode it ran under, the effective one. */
  mode: string;
  harness: string;
  version: string | null;
  account: string | null;
  /** `pipe` (the native door) or `acp` (the protocol door). */
  door: string;
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
};

/** `HELP_WIRED`'s answer: a conversation's receipts, the newest first; `recorded: false` for one started before they were kept. */
export type HelpWired = { session: string; recorded: boolean; receipts: HelpReceipt[] };

/** What the chip reads of a plugin to say what full access would add: whether it runs, and the servers it offers Ask Daoris. */
export type HelpPluginOffer = {
  enabled: boolean;
  problem?: unknown;
  helpServers?: readonly { name: string; state?: string }[] | null;
};
