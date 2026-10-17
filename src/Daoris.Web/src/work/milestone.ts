// The milestone of a unit of work (FABRIC-MILESTONE1b; the fabric events design §D0): whether its end was reached, the result kept,
// its cleanup (pending, with what waits and why, or completed, with what went) and the recovery retained and until when. The
// driver derives it at each read from the records it keeps (`WORK_STATE`, `daoris-driver work state`); the page stores nothing.
import type { TFunction } from 'i18next';

/** One thing a unit owns, with the role it has and what became of it (the inventory's entry). */
export type MilestoneEntry = {
  unit: string;
  /** `tree`, `session-branch`, `landing-branch`, `line`, `moved-aside`, `recovery-ref`, `auto-landing`, `history` or `end`. */
  kind: string;
  resource: string;
  /** `temporary`, `result`, `evidence` or `recovery`. */
  role: string;
  /** `kept`, `retained`, `removed`, `blocked` or `pending`. */
  disposition: string;
  /** Why, in the driver's clause: what waits, what blocks, what the proof was. Shown as recorded. */
  detail: string;
  /** A recovery's purge date, where one stands. */
  until?: string | null;
  proof?: string | null;
};

/** Whether the end was reached, and on what evidence. */
export type MilestoneEnd = {
  /** `ended`, `handed`, `not-ended`, `dropped` (over without its result, CLEANUP1) or `unknown`. */
  state: string;
  /** The proof's code (`merged`, `empty`, `on-line`, `pull-request`, `landed`, `carried`) where ended. */
  by?: string | null;
  on?: string | null;
  revision?: string | null;
  at?: string | null;
  /** The state in the driver's sentence: what a code the page has no words for is said by. */
  said: string;
};

export type MilestoneUnit = {
  unit: string;
  repository: string;
  branch?: string | null;
  session?: string | null;
  end: MilestoneEnd;
  result: MilestoneEntry[];
  cleanup: { state: string; waits: MilestoneEntry[]; gone: MilestoneEntry[] };
  recovery: MilestoneEntry[];
  entries: MilestoneEntry[];
};

/** What `WORK_STATE` answers: the units, and what could not be read (a repository with no checkout here). */
export type WorkState = { units: MilestoneUnit[]; unread: string[] };

/** The kinds of thing a unit owns that have a word of the page's; a `line` or an `end` is its resource's own name. */
const KINDS = ['tree', 'session-branch', 'landing-branch', 'moved-aside', 'recovery-ref', 'auto-landing', 'history'] as const;

/** An entry by what it is: its kind in the reader's language, where the page has a word, and its name. */
export function entryName(t: TFunction, entry: MilestoneEntry): string {
  const kind = KINDS.find((one) => one === entry.kind);
  return [kind ? t(`work.milestone.kind.${kind}`) : null, entry.resource].filter(Boolean).join(' ');
}

/**
 * What of a unit waits on the person (design §D0, *What needs you* only where something waits on them): the entries its cleanup
 * is blocked on, and, where no record names its owner, the unit itself. A wait that is pending (the cycle's own, a guard's) is
 * not the person's: it ends by itself.
 */
export function waitsOnPerson(unit: MilestoneUnit): MilestoneEntry[] {
  const blocked = unit.cleanup.waits.filter((entry) => entry.disposition === 'blocked');
  if (blocked.length > 0 || unit.end.state !== 'unknown') return blocked;
  return unit.entries.slice(0, 1);
}

/** The units whose branch, session or id is `name`: the milestone a branch row says (a session branch or a landing branch). */
export function unitOfBranch(units: readonly MilestoneUnit[], repository: string, branch: string): MilestoneUnit | undefined {
  return units.find((unit) => unit.repository.toLowerCase() === repository.toLowerCase()
    && (unit.branch === branch || unit.entries.some((entry) => (entry.kind === 'session-branch' || entry.kind === 'landing-branch') && entry.resource === branch)));
}

/** Older shells answer nothing of the kind, and a malformed answer reads as none. */
export function readWorkState(answer: unknown): WorkState {
  const held = answer as { units?: unknown; unread?: unknown } | null | undefined;
  return {
    units: Array.isArray(held?.units) ? held.units as MilestoneUnit[] : [],
    unread: Array.isArray(held?.unread) ? held.unread.filter((each): each is string => typeof each === 'string') : [],
  };
}
