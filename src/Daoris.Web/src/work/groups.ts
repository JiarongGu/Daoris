import type { Session } from '../data/api';
import { answeredPark, SESSION_ACTIVE, type ShownState, shownState } from '../design/ui';
import { sessionOrigin } from './identity';

// The session list by state: the groups the driver's one reader answers, as the list
// draws them; the list by repository; the strip; what the list remembers; and what *Archive what ended…*
// would take. Pure, so every list a person could have is an argument, and the list, the strip and the badge
// read one answer.

/** The five states a session is in, in the order the person acts on them. */
export type SessionStateName = 'needs-you' | 'working' | 'pending' | 'failed' | 'done';

/**
 * The groups the session list shows by state, and Archived, the fold a done or failed session is moved to by its mark: the
 * host's spelling, which `daoris session list --group` shares.
 */
export type SessionGroupName = SessionStateName | 'archived';

/** The groups in the order a list shows them, as the reader orders them (`SessionGroup.Order`). */
export const GROUP_ORDER: readonly SessionGroupName[] = ['needs-you', 'working', 'pending', 'failed', 'done', 'archived'];

/**
 * Where one session is listed and what its row's second line says, as `SESSION_GROUPS` answers it.
 *
 * @remarks
 * **One reader, in the driver.** It needs the planner's verdicts and a git judgement per tree, which only the driver has,
 * and the terminal prints the same answer, so the page reads it rather than deriving a group of its own. `state` is the
 * one of the five the session is in and `reason` the finer why inside it; `group` is its state, or `archived` where its mark
 * moved it to the fold. `shown` is the
 * record's state or a derived word, `parked`, `awaiting-reply`, `answered`, `going-on` or `limit-waits` (an ended record held for an account's reset); the page keeps its own *idle* for a live chat between
 * turns. `work` is what an ended session's own tree holds, a count null where git could not say. `archived`
 * says the mark stands: a session that needs the person stays in its group whatever the mark says.
 */
export type SessionGrouping = {
  session: string;
  group: SessionGroupName;
  state?: SessionStateName;
  reason?: string;
  shown: string;
  archived: boolean;
  /** The name this machine's person gave its conversation, which its row and head wear in place of its first question; null for none. */
  name?: string | null;
  /** Whether the person pinned its conversation: its row is marked and listed first within its group. */
  pinned?: boolean;
  teammate: boolean;
  strikes?: number | null;
  awaits?: string | null;
  awaitsOf?: string | null;
  work?: {
    commits: number | null; uncommitted: number | null;
    /** The branch the tree is on where the agent moved it onto one of its own. */
    branch?: string | null;
  } | null;
  /**
   * Whether this session holds its quest on this machine for the person: the person stopped it, or
   * it ended past its transient bound, and nothing starts the quest here until *Try again* releases it. Its row says so, and offers *Try again*.
   */
  holdsQuest?: boolean;
  /**
   * Whose pause holds this session's quest on this machine: an ask's or a quest's own. Its row says so,
   * and offers *Resume* in *Try again*'s place. Absent where none does.
   */
  pausedBy?: { scope: 'ask' | 'quest'; id: string } | null;
  /**
   * What holds the person's words on an ended record that resumes later, by the planner's verdict: the
   * work's pause (`paused`), the person's hold on its repository (`hold`), the cap (`cap`), its account cooling until
   * `until` (`cooling`), a session in its way (`busy`), or anything else (`waits`), with the planner's sentence. Its row
   * says so. Absent where nothing holds words. A `cooling` hold names the account that resets
   * first at `until`: `account` its id, null for the agent's own sign-in, and
   * `accountName` the name the person gave it, where it has one.
   */
  holds?: {
    why: string; reason: string; repository?: string | null; until?: string | null; account?: string | null; accountName?: string | null;
  } | null;
  /**
   * Whether *Delete…* would be taken: a conversation that served no quest, whose record the ledger
   * would delete and whose tree and landing this machine no longer holds. Offered only where true.   */
  deletable?: boolean;
  /**
   * What its own tree offers to land, whatever group it rests in bar Working: the commits on it no
   * branch of the person's holds, by the clean-up's proof, on its branch in its tree (the tree's name, never its path), with
   * its uncommitted paths, null where git could not say. Its page offers *Accept…* beside it. Absent where it offers none,
   * and where the tree holds none.
   */
  lands?: LandOffer | null;
  /**
   * What its own tree offers where the line holds its commits by content: a squash merge or a
   * cherry-pick put its work elsewhere, so its page offers no *Accept…* but says where the work is and offers the review's
   * *Discard*, unforced. Never beside `lands`. Absent where it offers none.
   */
  discards?: DiscardOffer | null;
  /**
   * The call a working session's agent is inside, as the driver heard it from the session's events:
   * its title, and when it began by the driver's clock, a call begun before this window opened among them. Its
   * row and its head say *running … for …*. Held in the driver's memory, never the record; null where none is open, and
   * absent otherwise. `waiting`: its permission request is held for the person's answer,
   * timed from the hold, its title empty where the agent named none. `told`:
   * Daoris told the session this call ran past its pipeline's bound.
   */
  calling?: { title: string; since: string; waiting?: boolean; told?: boolean; quiet?: string; quietTold?: boolean; heard?: string } | null;
  waits?: { run: string; node: string; why: string; since?: string | null }[] | null;
  /** Its line's state (`LineStandings`): `by` the part or co-worker it is read from, `role` `part` or which co-worker. */
  line?: { state: SessionStateName; group: SessionGroupName; reason: string; by: string; role: string } | null;
};

/** A row's line's state: its `line`, else its own row as the line's. */
export type LineStanding = NonNullable<SessionGrouping['line']>;

export const lineOf = (row: SessionGrouping): LineStanding =>
  row.line ?? { state: row.state ?? (row.group === 'archived' ? 'done' : row.group), group: row.group, reason: row.reason ?? row.shown, by: row.session, role: 'part' };

/** Where a row is listed: its line's group, so a part whose own record ended sits where its line works. */
export const listedGroup = (row: SessionGrouping): SessionGroupName => lineOf(row).group;

/** The characters a running call's head is said in at most, its ellipsis among them. */
export const CALL_HEAD = 40;

/**
 * A running call said in a few words: its title's first line, its spaces run together, cut to `CALL_HEAD` characters
 * with an ellipsis where more followed. A shell call's title is its whole command, three lines on the install's head and the
 * whole of a rail row's line; the row and the head say this, and the whole title in their tips.
 */
export function callHead(title: string): string {
  const lines = title.split(/\r?\n/).map((line) => line.replace(/\s+/g, ' ').trim()).filter(Boolean);
  const first = Array.from(lines[0] ?? '');
  if (first.length <= CALL_HEAD && lines.length <= 1) return first.join('');
  return `${first.slice(0, CALL_HEAD - 1).join('').trimEnd()}…`;
}

/** What an ended session's tree offers to land: the driver's `LandOffer`, as `SESSION_GROUPS` answers it. */
export type LandOffer = { branch: string; tree: string; commits: number; uncommitted: number | null };

/**
 * What a tree whose commits the line holds by content offers: the driver's `DiscardOffer`, as `SESSION_GROUPS`
 * answers it. `says` is the driver's sentence where *Accept…* would be: where its work is, in Discard's own clause, and why
 * its tree stays where an unforced discard would not go. `keeps` is the ref an unforced discard keeps its commits at, and
 * `keptAt` the ask's sentence naming it; both null where the discard would not go now. Shown as they are.
 */
export type DiscardOffer = { branch: string; tree: string; says: string; keeps: string | null; keptAt: string | null };

/**
 * How the list is arranged: by state, the default, by repository, or by ask, each
 * ask's entries under its name.
 */
export type SessionArrangement = 'state' | 'repository' | 'ask';

/** What Sessions' list remembers of its own ⋯: its arrangement, and whether archived is shown. */
export type SessionFilters = { group: SessionArrangement; archived: boolean };

/**
 * The filters as `listPanes.ts` kept them (`daoris.list.sessions.filters`), read defensively: anything unreadable is
 * the default, by state with archived hidden, so a first open with nothing remembered is by state.
 */
export function sessionFilters(kept: Record<string, unknown>): SessionFilters {
  return {
    group: kept.group === 'repository' ? 'repository' : kept.group === 'ask' ? 'ask' : 'state',
    archived: kept.archived === true,
  };
}

/** The filters as they are kept: the defaults are nothing kept at all, as Quests' are. */
export function keptSessionFilters(filters: SessionFilters): Record<string, unknown> | null {
  if (filters.group === 'state' && !filters.archived) return null;
  return { ...(filters.group !== 'state' ? { group: filters.group } : {}), ...(filters.archived ? { archived: true } : {}) };
}

/** The words the reader derives that the page shows as its own. */
const DERIVED: ReadonlySet<string> = new Set(['parked', 'awaiting-reply', 'answered', 'going-on', 'awaiting-helper', 'awaiting-check', 'limit-waits', 'idle']);

/**
 * A row's shown state: the reader's derived word where it said one (*parked*, *awaiting reply*, *answered*, *going on*),
 * and otherwise the page's own reading of the record, *idle*, *answered* and *going on* among it. A word a newer host
 * derives that this page does not know reads as the record's own, never as nothing.
 */
export function shownOf(
  session: Pick<Session, 'kind' | 'state' | 'said' | 'limit' | 'noteParts'>, grouping: Pick<SessionGrouping, 'shown'> | null | undefined,
  taking: boolean | undefined,
): ShownState {
  if (grouping && DERIVED.has(grouping.shown)) return grouping.shown as ShownState;
  return shownState(session, taking);
}

/**
 * Whether a row waits on the person: a session parked to ask them, or the last session of a quest parked on its
 * failures. Not a park they answered, which the same session goes on from at the driver's next look.
 */
export function waitsOnYou(
  session: Pick<Session, 'state' | 'said'>, grouping: Pick<SessionGrouping, 'shown'> | null | undefined,
): boolean {
  return (session.state === 'awaiting-person' && !answeredPark(session)) || grouping?.shown === 'parked';
}

/**
 * Where a session goes until the reader answers for it: a record that started after the reader's last look. Its record
 * alone says only the reader's first step (`SessionStateReader`): a live session of this machine's parked to ask the
 * person needs you, a teammate's waits on them and is working, one the person answered goes on and is working, a queued
 * one is pending, a failed one failed, and any other ended one is done. Nothing here says parked, to review or held,
 * which only the reader can.
 */
function provisional(session: Session): SessionStateName {
  if (session.state === 'queued') return 'pending';
  if (session.state === 'failed') return 'failed';
  if (!SESSION_ACTIVE.has(session.state)) return 'done';
  return session.state === 'awaiting-person' && !answeredPark(session) && !sessionOrigin(session) ? 'needs-you' : 'working';
}

const newestFirst = (a: Session, b: Session) => b.updated.localeCompare(a.updated);

/** The states whose groups list newest first, so a record that ended after the reader's look comes before its rows. */
const ENDED_STATES: ReadonlySet<SessionGroupName> = new Set(['failed', 'done']);

/** One of the list's groups by state, its sessions in the reader's order. */
export type StateGroup = { group: SessionGroupName; sessions: Session[] };

/**
 * The list by state: the groups the reader answered, each in its order, the empty ones left out.
 *
 * @remarks
 * - **Only what the page holds.** The reader answers for every record on this machine; the page's list is the scope the
 *   person chose, so a session it does not hold is not drawn.
 * - **Archived is hidden unless shown**, but the attended session is never taken out from under its reader: it is
 *   listed under Archived alone.
 * - **A record the reader has not answered for yet** goes where its record alone puts it, after the reader's rows, or
 * for an ended one before them, since it ended after the reader looked and the group goes newest first. The next
 * answer places it, as every tick asks the reader again.
 */
export function sessionsByState(
  sessions: readonly Session[], groupings: readonly SessionGrouping[] | undefined,
  { selected, archived }: { selected: string | null | undefined; archived: boolean },
): StateGroup[] {
  const held = new Map(sessions.map((row): [string, Session] => [row.id, row]));
  const rows = new Map<SessionGroupName, Session[]>(GROUP_ORDER.map((group) => [group, []]));
  const answered = new Set<string>();

  for (const row of groupings ?? []) {
    const record = held.get(row.session);
    if (!record || answered.has(row.session)) continue;
    answered.add(row.session);
    const group = listedGroup(row);
    if (group === 'archived' && !archived && row.session !== selected) continue;
    rows.get(GROUP_ORDER.includes(group) ? group : provisional(record))!.push(record);
  }

  const unanswered = sessions.filter((row) => !answered.has(row.id));
  for (const group of ENDED_STATES) {
    const since = unanswered.filter((row) => provisional(row) === group).sort(newestFirst);
    rows.set(group, [...since, ...rows.get(group)!]);
  }
  for (const row of unanswered) {
    if (!ENDED_STATES.has(provisional(row))) rows.get(provisional(row))!.push(row);
  }

  return GROUP_ORDER.map((group) => ({ group, sessions: rows.get(group)! })).filter((group) => group.sessions.length > 0);
}

/** What *Archive what ended…* would take, and how many stay in the list because they need the person. */
export type EndedToArchive = { going: string[]; kept: number };

/**
 * What *Archive what ended…*'s first press lists: every session the reader placed in Failed or Done that is
 * not archived, in the list's order, and how many stay under *Needs you*.
 *
 * @remarks
 * A record the reader has not answered for is placed by its record alone, and the reader may yet say otherwise, so it is
 * left to the next look. Only what the page holds is taken: the person archives what they can see.
 * The host judges each again at the second press, so this is a list, never a promise.
 */
export function endedToArchive(sessions: readonly Session[], groupings: readonly SessionGrouping[] | undefined): EndedToArchive {
  const groups = sessionsByState(sessions, groupings, { selected: null, archived: false });
  const placed = new Map((groupings ?? []).map((row): [string, SessionGrouping] => [row.session, row]));
  const of = (group: SessionGroupName) => groups.find((row) => row.group === group)?.sessions ?? [];
  return {
    going: [...of('failed'), ...of('done')]
      .filter((row) => ENDED_STATES.has(placed.get(row.id)?.group ?? 'archived')).map((row) => row.id),
    kept: of('needs-you').length,
  };
}

/** The list by repository: a group per repository with live work, by name, and the ended beneath. */
export type RepositoryGroups = { live: { repository: string; sessions: Session[] }[]; ended: Session[] };

/**
 * The list by repository: a group per repository by name, so the list does
 * not reshuffle as states move, *waiting on you* first within it, and the ended beneath, newest first.
 *
 * @remarks
 * It takes the reader's words: a parked quest's last session sorts with *waiting on you* in its repository's group,
 * since only the person moves it, and an archived session is hidden unless shown. The attended session stays among the
 * live whatever state it reached: a session that ends while you read it must not
 * jump down the list out from under you.
 *
 * 🔴 **The ended are listed, never dropped**: a list of live sessions only would show a machine with
 * four real records four sentences saying there is nothing. The repository groups keep
 * their headers for the live facts they carry, which an ended session has none of.
 */
export function sessionsByRepository(
  sessions: readonly Session[], groupings: readonly SessionGrouping[] | undefined,
  { selected, archived }: { selected: string | null | undefined; archived: boolean },
): RepositoryGroups {
  const placed = new Map((groupings ?? []).map((row): [string, SessionGrouping] => [row.session, row]));
  const isLive = (row: Session) => SESSION_ACTIVE.has(row.state) || row.id === selected || placed.get(row.id)?.shown === 'parked';

  const groups = new Map<string, Session[]>();
  for (const row of sessions.filter(isLive)) groups.set(row.repository, [...(groups.get(row.repository) ?? []), row]);
  const waitingFirst = (a: Session, b: Session) =>
    Number(!waitsOnYou(a, placed.get(a.id))) - Number(!waitsOnYou(b, placed.get(b.id)));

  return {
    live: [...groups.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([repository, rows]) => ({ repository, sessions: [...rows].sort(waitingFirst) })),
    ended: sessions
      .filter((row) => !isLive(row) && (archived || placed.get(row.id)?.group !== 'archived'))
      .sort(newestFirst),
  };
}

/** A row the list draws as its own, with the helpers and reviewers drawn under it; `id` is its session's. */
export type NestedRow = { id: string; session: Session; under: Session[] };

/**
 * Each helper and reviewer drawn under the row of the work it serves: `groups` are the rows an arrangement draws, in its order, and `serving` names, for each helper and reviewer the
 * rail joined, the session whose work it serves (a helper's `serves`, a reviewer's opinion's `working`).
 *
 * @remarks
 * - **Moved, never copied.** A row drawn under another leaves its own group, so no work has two rows, and a group it leaves
 * empty is the caller's to leave out. Each group keeps its own rows in its order; what is drawn under a row keeps the order
 * the list drew it in.
 * - **Its own place where its parent is not drawn**: a session the list does not hold, or does not draw (archived and hidden),
 *   and where the person must act on it (`keepOwn`), so nothing that waits on them is folded under something else.
 * - **The row that stands for it** (`standsFor`): one row per line of work, so a helper of a part that does not lead is
 *   drawn under its entry's row. A helper and a reviewer keep entries of their own.
 * - **One level**, as a helper cannot ask for a helper: a row drawn under another holds none, and whatever
 *   names it keeps its own place.
 */
export function nestServing(
  groups: readonly (readonly Session[])[],
  serving: Readonly<Record<string, string>>,
  { standsFor = (id) => id, keepOwn = () => false }: { standsFor?: (id: string) => string; keepOwn?: (id: string) => boolean } = {},
): NestedRow[][] {
  const drawn = groups.flat();
  const ids = new Set(drawn.map((row) => row.id));
  const parentOf = new Map<string, string>();
  for (const row of drawn) {
    const served = serving[row.id];
    if (!served || keepOwn(row.id)) continue;
    const parent = standsFor(served);
    if (parent !== row.id && ids.has(parent)) parentOf.set(row.id, parent);
  }
  for (const [child, parent] of [...parentOf]) {
    if (parentOf.has(parent)) parentOf.delete(child);
  }

  const under = new Map<string, Session[]>();
  for (const row of drawn) {
    const parent = parentOf.get(row.id);
    if (parent) under.set(parent, [...(under.get(parent) ?? []), row]);
  }
  return groups.map((group) => group
    .filter((row) => !parentOf.has(row.id))
    .map((row) => ({ id: row.id, session: row, under: under.get(row.id) ?? [] })));
}

/** How many ended sessions a group lists before its *Show N more*. */
export const ENDED_SHOWN = 12;

/**
 * A long Ended group's first rows, and how many it leaves for its *Show N more*: counted, never silently
 * cut. The attended session is never cut out of its group.
 */
export function cutEnded<T extends { id: string }>(
  rows: readonly T[], { limit, open, keep }: { limit: number; open: boolean; keep: string | null | undefined },
): { rows: T[]; hidden: number } {
  if (open || rows.length <= limit) return { rows: [...rows], hidden: 0 };
  const first = rows.slice(0, Math.max(0, limit));
  const kept = keep && !first.some((row) => row.id === keep) ? rows.filter((row) => row.id === keep) : [];
  return { rows: [...first, ...kept], hidden: rows.length - first.length - kept.length };
}

/**
 * The strip's sessions: what waits on the person first, then what runs, in the open list's order, by state
 * or by repository. What ended is the open list's; the attended session stays one press away, whatever it reached.
 */
export function stripSessions(
  sessions: readonly Session[], groupings: readonly SessionGrouping[] | undefined,
  { arrangement, selected, archived }: { arrangement: SessionArrangement; selected: string | null | undefined; archived: boolean },
): Session[] {
  if (arrangement === 'repository') {
    return sessionsByRepository(sessions, groupings, { selected, archived }).live.flatMap((group) => group.sessions);
  }
  const groups = sessionsByState(sessions, groupings, { selected, archived });
  const placed = new Map((groupings ?? []).map((row): [string, SessionGrouping] => [row.session, row]));
  const of = (name: SessionGroupName) => groups.find((group) => group.group === name)?.sessions ?? [];
  // A quest parked on its failures waits on the person as much as what needs them, so it leads with it.
  const shown = [
    ...of('needs-you'), ...of('failed').filter((row) => waitsOnYou(row, placed.get(row.id))), ...of('working'),
  ];
  const attended = selected && !shown.some((row) => row.id === selected) ? sessions.filter((row) => row.id === selected) : [];
  return [...shown, ...attended];
}
