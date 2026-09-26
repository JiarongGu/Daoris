/**
 * The wiring answer's shape (MAP1b) — held here, beside what draws it, and imported by the shell's
 * query the way `work/diff.ts` is: a molecule may not import the shell module at all, types included.
 */

/** Which rung of the resolution answered (D49 §4) — the order a start asks in. */
export type ChoiceFrom = 'picked' | 'workspace' | 'machine' | 'unset';

/**
 * What a driven start in one workspace would run on, and where each part came from — the driver's
 * own `SelectAsync`, read as names. No home, no binary path and no key ever arrive here.
 */
export type StartWiring = {
  /**
   * The job this start is for: a driven session's work, or — once an agent is named for it — the
   * intake an ask in this circle opens (INT4b, AGT6). Each circle's jobs arrive together, work first.
   */
  job: 'work' | 'intake';
  workspace: string;
  adapter: string;
  /** Whose accounts it runs as (AGT7): the adapter itself, or the tool a door opens onto. */
  owner: string;
  product?: string | null;
  /** The account by its directory name; null is the tool's own home, the account it signs in to itself. */
  profile?: string | null;
  profileFrom: ChoiceFrom;
  /** The probed version, else the pin asked for; null when neither is known — never a guess. */
  version?: string | null;
  /** Which rung pinned it; `unset` is PATH. */
  versionFrom: ChoiceFrom;
  /** `driver.json` names the command outright, which outranks any pin. */
  commanded: boolean;
  /** Why a start would be held, in the driver's own words. */
  refusal?: string | null;
};

export type WiringAnswer = { adapter: string; starts: StartWiring[] };
