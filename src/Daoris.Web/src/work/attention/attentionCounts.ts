import type { Session } from '../../data/api';
import type { AttentionKind } from './AttentionRow';
import type { PushedAttention } from './attentionPushed';

/**
 * What *What needs you* lists and the bell counts: every row of the window's scope but a press's own,
 * whose card is drawn from the confirmation channel. The bell, the taskbar, *Notifications*' group and Overview's summary read this
 * one answer, so a count never holds a row the list does not.
 */
export function needsListed<T extends { kind: string; id?: string; obligation?: string }>(rows: readonly T[]): T[] {
  // Each obligation once, the first row that names it standing for it.
  const seen = new Set<string>();
  return rows.filter((row) => {
    if (row.kind === 'confirmation') return false;
    const key = obligationOf(row);
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

/**
 * The obligation a row is: the exchange's id .NET keys it by where it names one (a go-ahead's
 * `go-ahead:<ask>/<n>`), else its kind and id, which for a helper's and a hand-over's ask is that exchange's id already.
 */
export const obligationOf = (row: { kind: string; id?: string; obligation?: string }): string => row.obligation ?? `${row.kind}:${row.id ?? ''}`;

/**
 * The bell's count: each press of either source once, and each obligation of
 * *What needs you* once. A message unread in a channel is no obligation, and is marked by the channel's own dot, never counted here.
 */
export const bellCount = (presses: readonly unknown[], rows: readonly { kind: string; id?: string; obligation?: string }[]): number =>
  presses.length + needsListed(rows).length;

/** The places of the activity bar a wait can be counted at: those a row's door opens. */
export type AttentionPlace = 'sessions' | 'quests' | 'projects' | 'agents' | 'settings';

/**
 * The place each kind is counted at: the place its door opens (`App.tsx`'s `attentionDoors`),
 * where its step is taken, so a place's number is what the person finds to do there. A press is the bell's alone, its card in
 * *Notifications*. Typed over every kind, so a new kind is placed when it is added.
 */
export const ATTENTION_PLACE: Readonly<Record<AttentionKind, AttentionPlace | null>> = {
  // A session's page holds its answer, its review, its gate, its helper's and hand-over's yes and no, its held call and answer.
  parked: 'sessions',
  review: 'sessions',
  opinion: 'sessions',
  helper: 'sessions',
  'hand-over': 'sessions',
  permission: 'sessions',
  answer: 'sessions',
  // A quiet session's page holds its presses too.
  quiet: 'sessions',
  // A refused move back to the person opens its session, as its park would.
  'helper-ended': 'sessions',
  // The session's page holds its Memory section, where the person approves, declines, keeps or drops.
  memory: 'sessions',
  // An ask's page or a quest's: publishing, an intake's question, a quest nobody can take, *Try again*, a go-ahead on its ask, a
  // departure's yes, a set-up's review, a folder's trust for what it holds.
  proposal: 'quests',
  intake: 'quests',
  unanswerable: 'quests',
  'parked-quest': 'quests',
  'steward-stopped': 'quests',
  'policy-hold': 'quests',
  // The run's door opens its quest, where the work and its declined request are read.
  'request-declined': 'quests',
  // A person's task opens its run's work quest, as a declined request does.
  task: 'quests',
  'go-ahead': 'quests',
  departure: 'quests',
  'set-up': 'quests',
  trust: 'quests',
  // The workspace's Branches, where the clean-up's press is. Its row's door is the row's own (`placeOf`): a branch cut for a session
  // the page holds opens that session, and a held attempt's presses are on its row alone.
  work: 'projects',
  // The repository's Integration tab, where the decide, the answer and the request are pressed.
  integration: 'projects',
  // What agents may do, and an agent's accounts.
  rule: 'agents',
  'account-wait': 'agents',
  'signed-out': 'agents',
  // This Daoris's *Disk space* card.
  space: 'settings',
  confirmation: null,
  // A held git remote: *Try again* is on the row itself; the identity it waits for is set in Settings → Git, so the
  // place counts it there, as it counts Disk space.
  'git-held': 'settings',
  // A git go-ahead: its session's page draws it in its head and foot, where it is answered too.
  'git-ask': 'sessions',
};

/** The sessions a place is judged with: those the page holds, for the branch a unit of work is cut for (`workCutFor`). */
type PlaceSessions = readonly Pick<Session, 'id' | 'repository' | 'tree'>[] | undefined;

/**
 * The place a row is counted at: its kind's, but a unit of work's, whose door is the row's own (`App.tsx`'s
 * `attentionDoors.work`): a held attempt is settled on its row alone, in the bell's view, so no place counts
 * it; a branch cut for a session the page holds opens that session; any other opens the workspace's Branches. A kind this build
 * does not know is counted at none, though the bell counts it.
 */
export function placeOf(row: Pick<PushedAttention, 'kind' | 'held' | 'blocked' | 'session' | 'title' | 'where'>, sessions: PlaceSessions): AttentionPlace | null {
  if (row.kind === 'work') return row.held ? null : workCutFor(row, sessions) ? 'sessions' : 'projects';
  return (ATTENTION_PLACE as Readonly<Record<string, AttentionPlace | null>>)[row.kind] ?? null;
}

/**
 * How many waits each place counts: every listed row (`needsListed`) at its place (`placeOf`). A place with none is
 * absent, so it wears no number.
 */
export function placeCounts(rows: readonly PushedAttention[], sessions?: PlaceSessions): Partial<Record<AttentionPlace, number>> {
  const counted: Partial<Record<AttentionPlace, number>> = {};
  for (const row of needsListed(rows)) {
    const place = placeOf(row, sessions);
    if (place) counted[place] = (counted[place] ?? 0) + 1;
  }
  return counted;
}

/**
 * The session a `work` row's branch is cut for, where nothing records its owner and the page holds a session the
 * branch is named for, by the name the trees give it (`daoris/` and the tree's folder, as WorkFrame finds a session's own branch).
 * That session is the row's door, and the place it is counted at.
 */
export function workCutFor(
  row: Pick<PushedAttention, 'blocked' | 'session' | 'title' | 'where'>, sessions: readonly Pick<Session, 'id' | 'repository' | 'tree'>[] | undefined,
) {
  const branch = (row.blocked?.entries ?? []).find((entry) => entry.facts?.branch)?.facts?.branch ?? row.title;
  return row.blocked?.unknown && !row.session && branch?.startsWith('daoris/')
    ? sessions?.find((one) => one.repository === row.where && one.tree
      && branch === `daoris/${one.tree.split(/[\\/]/).filter(Boolean).pop()}`)
    : undefined;
}
