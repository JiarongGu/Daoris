import type { Session } from '../api';
import { answeredPark, SESSION_ACTIVE, type ShownState, shownState } from '../ui';
import { sessionOrigin } from './identity';

// The session list by state (SESSUX1c, D126 §2.1, §4): the groups the driver's one reader answers, as the list
// draws them; the list by repository as it was; the strip; what the list remembers; and what *Archive what ended…*
// would take (SESSUX1e). Pure, so every list a person could have is an argument, and the list, the strip and the badge
// read one answer.

/**
 * The groups the session list shows by state (D126 §2.1), in the order the person acts on them, and Archived: the
 * host's spelling, which `daoris-driver sessions --group` shares.
 */
export type SessionGroupName = 'you' | 'review' | 'working' | 'later' | 'ended' | 'archived';

/** The groups in the order a list shows them, as the reader orders them (`SessionGroup.Order`). */
export const GROUP_ORDER: readonly SessionGroupName[] = ['you', 'review', 'working', 'later', 'ended', 'archived'];

/**
 * Where one session is listed and what its row's second line says (D126 §2.2, §2.4), as `SESSION_GROUPS` answers it.
 *
 * @remarks
 * **One reader, in the driver.** It needs the planner's verdicts and a git judgement per tree, which only the driver has,
 * and the terminal prints the same answer, so the page reads it rather than deriving a group of its own. `shown` is the
 * record's state or a derived word, `parked`, `awaiting-reply` or `answered` (ANSWER1c); the page keeps its own *idle* for a live chat between
 * turns (UX5 U17). `work` is what an ended session's own tree holds, a count null where git could not say. `archived`
 * says the mark stands: a session that needs the person stays in its group whatever the mark says.
 */
export type SessionGrouping = {
  session: string;
  group: SessionGroupName;
  shown: string;
  archived: boolean;
  teammate: boolean;
  strikes?: number | null;
  awaits?: string | null;
  awaitsOf?: string | null;
  work?: { commits: number | null; uncommitted: number | null } | null;
  /**
   * Whether this session's stop holds its quest on this machine (SESSUX1b, D126 §3.3): the person stopped it, and nothing
   * starts the quest here until *Try again* releases it. Its row says so, and offers *Try again* (SESSUX1d). Absent on a
   * host older than the fact.
   */
  holdsQuest?: boolean;
  /**
   * Whether *Delete…* would be taken (SESSUX1f, D126 §5.4): a conversation that served no quest, whose record the ledger
   * would delete and whose tree and landing this machine no longer holds. Offered only where true, D95's way. Absent on a
   * host older than the fact, which offers none.
   */
  deletable?: boolean;
};

/** How the list is arranged (D126 §4.1): by state, the default, or by repository, the arrangement it had before. */
export type SessionArrangement = 'state' | 'repository';

/** What Sessions' list remembers of its own ⋯ (D126 §4.1, D118 §3f): its arrangement, and whether archived is shown. */
export type SessionFilters = { group: SessionArrangement; archived: boolean };

/**
 * The filters as `listPanes.ts` kept them (`daoris.list.sessions.filters`), read defensively: anything unreadable is
 * the default, by state with archived hidden, so a first open with nothing remembered is by state.
 */
export function sessionFilters(kept: Record<string, unknown>): SessionFilters {
  return { group: kept.group === 'repository' ? 'repository' : 'state', archived: kept.archived === true };
}

/** The filters as they are kept: the defaults are nothing kept at all, as Quests' are. */
export function keptSessionFilters(filters: SessionFilters): Record<string, unknown> | null {
  if (filters.group === 'state' && !filters.archived) return null;
  return { ...(filters.group === 'repository' ? { group: 'repository' } : {}), ...(filters.archived ? { archived: true } : {}) };
}

/**
 * A row's shown state: the reader's derived word where it said one (*parked*, *awaiting reply*, *answered*), and
 * otherwise the page's own reading of the record, *idle* and *answered* among it. A word a newer host derives that this
 * page does not know reads as the record's own, never as nothing.
 */
export function shownOf(
  session: Pick<Session, 'kind' | 'state' | 'answer'>, grouping: Pick<SessionGrouping, 'shown'> | null | undefined,
  taking: boolean | undefined,
): ShownState {
  if (grouping?.shown === 'parked' || grouping?.shown === 'awaiting-reply' || grouping?.shown === 'answered') return grouping.shown;
  return shownState(session, taking);
}

/**
 * Whether a row waits on the person: a session parked to ask them, or the last session of a quest parked on its
 * failures. Not a park they answered, which the same session goes on from at the driver's next look (ANSWER1c).
 */
export function waitsOnYou(
  session: Pick<Session, 'state' | 'answer'>, grouping: Pick<SessionGrouping, 'shown'> | null | undefined,
): boolean {
  return (session.state === 'awaiting-person' && !answeredPark(session)) || grouping?.shown === 'parked';
}

/**
 * Where a session goes until the reader answers for it: a record that started after the reader's last look. Its record
 * alone says only the reader's first step, live or ended (`SessionGroups.Place`): a live session of this machine's
 * parked to ask the person waits on you, a teammate's waits on them and is working, one the person answered goes on and
 * is working (ANSWER1c), and an ended one is ended. Nothing here says parked, to review or resumes later, which only
 * the reader can.
 */
function provisional(session: Session): SessionGroupName {
  if (!SESSION_ACTIVE.has(session.state)) return 'ended';
  return session.state === 'awaiting-person' && !answeredPark(session) && !sessionOrigin(session) ? 'you' : 'working';
}

const newestFirst = (a: Session, b: Session) => b.updated.localeCompare(a.updated);

/** One of the list's groups by state, its sessions in the reader's order. */
export type StateGroup = { group: SessionGroupName; sessions: Session[] };

/**
 * The list by state (D126 §2.1): the groups the reader answered, each in its order, the empty ones left out.
 *
 * @remarks
 * - **Only what the page holds.** The reader answers for every record on this machine; the page's list is the scope the
 *   person chose, so a session it does not hold is not drawn.
 * - **Archived is hidden unless shown** (§4.1), but the attended session is never taken out from under its reader: it is
 *   listed under Archived alone.
 * - **A record the reader has not answered for yet** goes where its record alone puts it, after the reader's rows, or
 *   for an ended one before them, since it ended after the reader looked and the group goes newest first. The next
 *   answer places it, as every tick asks the reader again.
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
    if (row.group === 'archived' && !archived && row.session !== selected) continue;
    rows.get(GROUP_ORDER.includes(row.group) ? row.group : provisional(record))!.push(record);
  }

  const unanswered = sessions.filter((row) => !answered.has(row.id));
  const endedSince = unanswered.filter((row) => provisional(row) === 'ended').sort(newestFirst);
  rows.set('ended', [...endedSince, ...rows.get('ended')!]);
  for (const row of unanswered) {
    if (provisional(row) !== 'ended') rows.get(provisional(row))!.push(row);
  }

  return GROUP_ORDER.map((group) => ({ group, sessions: rows.get(group)! })).filter((group) => group.sessions.length > 0);
}

/** What *Archive what ended…* would take, and how many stay in the list because they need the person (D126 §5.3). */
export type EndedToArchive = { going: string[]; kept: { you: number; review: number } };

/**
 * What *Archive what ended…*'s first press lists (SESSUX1e, D126 §5.3): every session the reader placed in Ended that is
 * not archived, in its order, and how many stay under *Waiting on you* and *To review*, counted as their headings are.
 *
 * @remarks
 * A record the reader has not answered for is placed in Ended by its record alone, and the reader may yet say it is to
 * review, so it is left to the next look. Only what the page holds is taken: the person archives what they can see.
 * The host judges each again at the second press, so this is a list, never a promise.
 */
export function endedToArchive(sessions: readonly Session[], groupings: readonly SessionGrouping[] | undefined): EndedToArchive {
  const groups = sessionsByState(sessions, groupings, { selected: null, archived: false });
  const placed = new Map((groupings ?? []).map((row): [string, SessionGrouping] => [row.session, row]));
  const of = (group: SessionGroupName) => groups.find((row) => row.group === group)?.sessions ?? [];
  return {
    going: of('ended').filter((row) => placed.get(row.id)?.group === 'ended').map((row) => row.id),
    kept: { you: of('you').length, review: of('review').length },
  };
}

/** The list by repository: a group per repository with live work, by name, and the ended beneath. */
export type RepositoryGroups = { live: { repository: string; sessions: Session[] }[]; ended: Session[] };

/**
 * The list by repository (D126 §4.3), the arrangement it had before: a group per repository by name, so the list does
 * not reshuffle as states move, *waiting on you* first within it, and the ended beneath, newest first.
 *
 * @remarks
 * It gains the reader's words: a parked quest's last session sorts with *waiting on you* in its repository's group,
 * since only the person moves it, and an archived session is hidden unless shown. The attended session stays among the
 * live whatever state it reached, the rule this arrangement always kept: a session that ends while you read it must not
 * jump down the list out from under you.
 *
 * 🔴 **The ended are listed, never dropped** (D62, found on the deployed application): the list once held live sessions
 * only, and a machine with four real records showed four sentences saying there was nothing. The repository groups keep
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

/** How many ended sessions a group lists before its *Show N more* (D126 §4.4). */
export const ENDED_SHOWN = 12;

/**
 * A long Ended group's first rows, and how many it leaves for its *Show N more* (D126 §4.4): counted, never silently
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
 * The strip's sessions (D126 §2.5): what waits on the person first, then what runs, in the open list's order, by state
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
  const shown = groups.filter((group) => group.group === 'you' || group.group === 'working').flatMap((group) => group.sessions);
  const attended = selected && !shown.some((row) => row.id === selected) ? sessions.filter((row) => row.id === selected) : [];
  return [...shown, ...attended];
}
