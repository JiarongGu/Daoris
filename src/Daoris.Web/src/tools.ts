/**
 * The agent tools a person has, assembled from the doors Daoris can reach them over.
 *
 * @remarks
 * 🔴 **"Harness account" and `*-acp` hid what the roster was for.** The reference project is a
 * *reference* — its design and its logic are worth taking, its vocabulary is not.
 *
 * The surface had been listing what the driver calls adapters: `claude-code`, `claude-code-acp`,
 * `codex-acp`, `dsh` — four rows, four install buttons, four version pins, and, read by a person,
 * four tools to have opinions about. **It is not four tools.** It is three, two of which can be
 * reached two ways, and a person has **one** Claude Code with **one** account however Daoris chooses
 * to hold a session on it.
 *
 * Nothing here is invented to make that true: `accountOf` has said *"this entry runs as that one's
 * account"* since ACP2, and `wire` says which door an entry is. The grouping is those two fields
 * read the way they were written — which is why this is a pure function over the payload rather
 * than a table somebody maintains.
 */

/** One door Daoris can hold a session over — an adapter, in `driver.json`'s vocabulary. */
export interface ToolDoor {
  /** The adapter id. Still shown, because `driver.json` and `daoris driver` name exactly this. */
  harness: string;
  present: boolean;
  version?: string | null;
  problem?: string | null;
  wire?: string | null;
  accountOf?: string | null;
  /** What a person calls the tool this door runs, and who makes it (AGT1). Absent from an older shell. */
  product?: string | null;
  maker?: string | null;
  /**
   * Whether this harness can be pinned at all — false where it declares no package for Daoris to
   * fetch. The control is **absent** there rather than present and refusing: half a control is
   * worse than none, which is the same rule the palette and the parked session's moves follow.
   */
  pinnable?: boolean;
  /** Whether this door can run the tool's sign-in — false where it declares no login flow. Absent from an older shell. */
  signsIn?: boolean;
  /**
   * Which Update this door has (USE1a): `pin` moves its pin to the newest release, `tool` runs the
   * tool's own updater, and absent means none — so no Update is offered, rather than one that can
   * only be refused.
   */
  updates?: 'pin' | 'tool' | null;
  /** Whether this agent takes an API key from Daoris (AGT3) — only where its key variable was measured. */
  takesKey?: boolean;
  /** The version this machine pinned, or null for whatever is on `PATH` (TOOL2/D57). */
  pinned?: string | null;
  /**
   * The managed binary actually installed at that pin, or null. `pinned` without `managed` is a pin
   * naming a version nobody installed — which **refuses every spawn** rather than quietly running
   * `PATH`, so a surface must say so rather than imply the pin is in force.
   */
  managed?: string | null;
  /**
   * Whether a session on this door keeps a conversation's structure (D76 §1) — the protocol door, or
   * a native door whose adapter reads the harness's own structured output. What an empty record is
   * read by (CONV3b).
   */
  structured?: boolean;
  /**
   * Whether a session on this door is handed Daoris's rules file (PERM1, D72): an agent's page has *What it may do* only
   * where a door onto it is (UX6e, D150 §5.1). Absent from an older shell.
   */
  takesRules?: boolean;
  machineDefault?: string | null;
  profiles?: Account[];
  /** What the tool says about logging in to its OWN configuration home — the account a person has before naming any. */
  ownLogin?: string | null;
  /** Who is signed in to that home, when the tool says (D66 §3). */
  ownAccount?: string | null;
  /** When that home's state was last read (UX6e): only at a person's press (TOOL6g), so null before one. */
  ownRead?: string | null;
  /** Which circles run this door as which account (D49 §4). */
  workspaceDefaults?: { workspace: string; profile: string }[];
  /** The plugin this door was declared by (D64), or null for one the build carries. */
  plugin?: string | null;
  /**
   * What the tool itself offers for an account's model and effort (AGT6, D98): its own aliases and the
   * efforts its settings keep. Null where Daoris does not know the tool's settings, and nothing is then
   * offered. Absent from an older shell.
   */
  settingsChoices?: SettingsChoices | null;
}

/** The choices a tool offers for an account's own settings, in its own words (AGT6). */
export interface SettingsChoices {
  models: string[];
  efforts: string[];
}

/**
 * An account's own model and effort, as the tool's settings file under it says (AGT6). Null is not set:
 * the tool's own default. `problem` is the driver's sentence when the file could not be read.
 */
export interface AccountSettings {
  model: string | null;
  effort: string | null;
  /** The efforts set per model, which the tool reads before the account's for that model. */
  perModel: { model: string; effort: string }[];
  problem: string | null;
}

/**
 * An account's settings as the bridge sent them, read defensively (AGT6, looked at): the bridge leaves a
 * null field out, so an account whose file sets nothing arrived with no `model` at all, and a form that
 * tested for null read the missing value as a model id and crashed the page. Missing and null are one.
 */
export function settingsOf(raw: Partial<AccountSettings> | null | undefined): AccountSettings {
  const text = (value: unknown) => (typeof value === 'string' && value !== '' ? value : null);
  return {
    model: text(raw?.model),
    effort: text(raw?.effort),
    perModel: Array.isArray(raw?.perModel)
      ? raw.perModel.filter((entry): entry is { model: string; effort: string } =>
        typeof entry?.model === 'string' && typeof entry?.effort === 'string')
      : [],
    problem: text(raw?.problem),
  };
}

/**
 * A change to an account's settings: a key left out is untouched, a value sets it, null clears it —
 * the three a person can mean, kept apart on the wire too.
 */
export interface AccountSettingsChange {
  model?: string | null;
  effort?: string | null;
  perModel?: Record<string, string | null>;
}

/**
 * One scope an account runs in (ACCT1): a workspace, or null for this machine's; whether its own list holds the account,
 * and whether its own default names it.
 */
export interface AccountPlace {
  workspace: string | null;
  list: boolean;
  default: boolean;
}

/**
 * One account: the directory's name, where it is, its state — and who is signed in there, by the
 * tool's own answer (D66 §3). A person knows an account by its name (ACCT2); `name` is its id, what a terminal types.
 */
export interface Account {
  /** Its stable id (ACCT2): its folder's name, an old `account-N` or a fresh `acct-` and eight hex characters. */
  name: string;
  /** The person's name for it (ACCT2), null where they gave none; absent from a shell older than that. */
  displayName?: string | null;
  /** Where it runs (ACCT1): each scope whose own list holds it or whose own default names it; absent from an older shell. */
  places?: AccountPlace[] | null;
  /** No list and no default holds it, so no start runs on it (ACCT1); absent from an older shell. */
  nowhere?: boolean;
  home: string;
  /** Asked of the tool, and one of three answers (SES3): only a definite `out` refuses. */
  login: 'in' | 'out' | 'unknown';
  account?: string | null;
  /** An account that is an API key (AGT3): its handle, never the key. */
  key?: string | null;
  /** Its own model and effort (AGT6), where the tool's settings are known; absent or null otherwise. */
  settings?: AccountSettings | null;
  /** When its state was last read (UX6e, D150 §5.3); null where it never was, and absent from an older shell. */
  read?: string | null;
}

/** One tool, with every door onto it and the one account list they share. */
export interface Tool {
  /** The tool's id — `claude-code`, `codex`, `dsh` — which is what a terminal types. */
  name: string;
  /**
   * What a person calls it and whose it is (AGT1), from the first door that says — or null, and a
   * surface shows the id. `dsh` meant nothing to the owner until it said it was DeepSeek's.
   */
  product: string | null;
  maker: string | null;
  /** Its doors, the one that owns the account first. */
  doors: ToolDoor[];
  /** The accounts, deduplicated: every door onto one tool reads one configuration home. */
  accounts: Account[];
  /** Which account the next session runs as, where any door says so. */
  machineDefault: string | null;
  /** Whether any door onto this tool is installed — "have I got this tool" in one word. */
  present: boolean;
  /**
   * The tool's own home's login state — `in`, `out` or `unknown`. 🔴 The account a person actually
   * has: a machine with no named profile read "No accounts" while its owner was logged in.
   */
  ownLogin: string;
  /** Who is signed in to the tool's own home, where any door says. */
  ownAccount: string | null;
  /** When the tool's own home was last read, by the door whose word on it stands (UX6e); null where it never was. */
  ownRead: string | null;
  /** Whether any door onto it is handed Daoris's rules file (UX6e, D150 §5.1). */
  takesRules: boolean;
  /** Which circles use which account, once per circle however many doors report it. */
  workspaceDefaults: { workspace: string; profile: string }[];
  /**
   * What the tool offers for an account's model and effort (AGT6), from the first door that says; null
   * where the doors say none, and a surface then offers nothing and says so; undefined from an older
   * shell that never said, and a surface then says nothing either way.
   */
  settingsChoices?: SettingsChoices | null;
}

/**
 * Which door this machine's starts ride: the configured adapter's own `wire`, as the roster says it
 * (INT3c). Null when it cannot be read — no adapter, one the roster does not carry, or a wire it does
 * not name — and a surface then says nothing rather than guess.
 */
export function doorOf(adapter: string | undefined, doors: readonly ToolDoor[]): 'acp' | 'pipe' | null {
  const wire = doors.find((door) => door.harness === adapter)?.wire;
  return wire === 'acp' || wire === 'pipe' ? wire : null;
}

/**
 * What a person calls an account (ACCTNAME1, D152 §4.2): by its owner, or a door onto it, and its id, or none for the tool's
 * own home. `settings/namer.ts` makes one from the roster; a molecule is handed it, since it holds no roster of its own.
 */
export type AccountNamer = (owner: string, profile?: string | null) => string;

/** Which tool this door belongs to: the account it borrows, or itself. */
export function toolOf(door: ToolDoor): string {
  return door.accountOf?.trim() || door.harness;
}

/**
 * What a person calls a way in: the tool, the door, and the id a terminal types — *Claude Code —
 * protocol (claude-code-acp)*. One wording for every place that offers a choice of them (UX5 U67:
 * the start form listed bare ids where Settings named them so). A door with no product is its id.
 */
export function doorLabel(t: (key: string, options?: Record<string, unknown>) => string, door: ToolDoor): string {
  if (!door.product) return door.harness;
  return t('settings.ai.intake.choice', {
    product: door.product,
    door: t(door.wire === 'acp' ? 'harness.wire.acp' : 'harness.wire.pipe'),
    agent: door.harness,
  });
}

/**
 * Group the roster into the tools a person actually has.
 *
 * @remarks
 * **Order is the roster's**, by first appearance, so a surface does not reshuffle when a tool is
 * installed. Within a tool the **account-owning door leads** — `claude-code` before
 * `claude-code-acp` — because that is the one whose name is the tool's.
 *
 * 🔴 **A tool can have no door of its own and that is not a gap.** `codex-acp` runs as `codex`'s
 * account and the driver has no native `codex` adapter, so the tool `codex` exists on this surface
 * with exactly one door whose name is not its own. Naming the group after the ACCOUNT rather than
 * after any door is what makes that case ordinary instead of special.
 */
export function byTool(doors: readonly ToolDoor[]): Tool[] {
  const order: string[] = [];
  const grouped = new Map<string, ToolDoor[]>();

  for (const door of doors) {
    const name = toolOf(door);
    if (!grouped.has(name)) {
      grouped.set(name, []);
      order.push(name);
    }
    grouped.get(name)!.push(door);
  }

  return order.map((name) => {
    // The door whose name IS the tool's leads; the rest keep the roster's order behind it.
    const own = grouped.get(name)!;
    const doorsInOrder = [
      ...own.filter((door) => door.harness === name),
      ...own.filter((door) => door.harness !== name),
    ];

    // One configuration home per tool by declaration, so the same account can be reported by more
    // than one door. Deduplicated on the HOME, which is what a profile actually is — two doors
    // naming one directory are one account, whatever either of them called it.
    const accounts: Tool['accounts'] = [];
    for (const door of doorsInOrder) {
      for (const profile of door.profiles ?? []) {
        if (accounts.some((held) => held.home === profile.home)) continue;
        accounts.push(profile.settings === undefined || profile.settings === null
          ? profile
          : { ...profile, settings: settingsOf(profile.settings) });
      }
    }

    // One circle, one answer: the doors read one file, so the first door to name a circle speaks
    // for it — like the accounts above, the second door saying it again is the same fact.
    const workspaceDefaults: Tool['workspaceDefaults'] = [];
    for (const door of doorsInOrder) {
      for (const circle of door.workspaceDefaults ?? []) {
        if (workspaceDefaults.some((held) => held.workspace === circle.workspace)) continue;
        workspaceDefaults.push(circle);
      }
    }

    return {
      name,
      product: doorsInOrder.map((door) => door.product).find(Boolean) ?? null,
      maker: doorsInOrder.map((door) => door.maker).find(Boolean) ?? null,
      doors: doorsInOrder,
      accounts,
      machineDefault: doorsInOrder.find((door) => door.machineDefault)?.machineDefault ?? null,
      present: doorsInOrder.some((door) => door.present),
      // The account-owning door's word; any door's definite answer beats every door's silence.
      ownLogin: doorsInOrder.map((door) => door.ownLogin).find((state) => state && state !== 'unknown')
        ?? 'unknown',
      ownAccount: doorsInOrder.map((door) => door.ownAccount).find(Boolean) ?? null,
      // Read where the word on it stands: the door whose definite answer the state above takes, else any door's reading.
      ownRead: (doorsInOrder.find((door) => door.ownLogin && door.ownLogin !== 'unknown')
        ?? doorsInOrder.find((door) => door.ownRead))?.ownRead ?? null,
      takesRules: doorsInOrder.some((door) => door.takesRules === true),
      workspaceDefaults,
      // One file per account whichever door reads it (AGT7), so the first door to offer choices speaks for
      // all; a door that said none says none, and a shell that never said leaves it unsaid.
      settingsChoices: doorsInOrder.map((door) => door.settingsChoices).find(Boolean)
        ?? (doorsInOrder.some((door) => door.settingsChoices === null) ? null : undefined),
    };
  });
}
