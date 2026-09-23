/**
 * The agent tools a person has, assembled from the doors Daoris can reach them over.
 *
 * @remarks
 * 🔴 **Written from the owner's reading of the roster** (2026-09-22): *"'harness account'? and
 * `*-acp` really confusing of the scope of this project"*, with the correction that the reference
 * project is a *reference* — its design and its logic are worth taking, its vocabulary is not.
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
  pinnable?: boolean;
  /** Whether this door can run the tool's sign-in — false where it declares no login flow. Absent from an older shell. */
  signsIn?: boolean;
  pinned?: string | null;
  managed?: string | null;
  machineDefault?: string | null;
  profiles?: Account[];
  /** What the tool says about logging in to its OWN configuration home — the account a person has before naming any. */
  ownLogin?: string | null;
  /** Who is signed in to that home, when the tool says (D66 §3). */
  ownAccount?: string | null;
  /** Which circles run this door as which account (D49 §4). */
  workspaceDefaults?: { workspace: string; profile: string }[];
  /** The plugin this door was declared by (D64), or null for one the build carries. */
  plugin?: string | null;
}

/**
 * One account: the directory's name, where it is, its state — and who is signed in there, by the
 * tool's own answer (D66 §3). A person knows an account by who; `name` is what a terminal types.
 */
export interface Account {
  name: string;
  home: string;
  login: string;
  account?: string | null;
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
  /** Which circles use which account, once per circle however many doors report it. */
  workspaceDefaults: { workspace: string; profile: string }[];
}

/** Which tool this door belongs to: the account it borrows, or itself. */
export function toolOf(door: ToolDoor): string {
  return door.accountOf?.trim() || door.harness;
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
        accounts.push(profile);
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
      workspaceDefaults,
    };
  });
}
