import { size } from '../format';
import type { Translate } from './pausing';

// Clearing finished history from this machine on the screen (HIST1e, D153; `docs/2026-10-07-history-clearing-design.md`
// §2.4, §5, §6.1): the shapes the driver's `HISTORY_PLAN` and `HISTORY_CLEAR` answer (`bridge/history.ts` asks them),
// what the first press lists and the second sends, and what each says. Pure, so the quest's page, the ask's page and the
// workspace's read one rule, a molecule may read it, and every plan the driver could answer is a test's argument.

/** What a clear is asked of: a workspace's finished history, a closed quest's work, its failed sessions, or an ask's work. */
export type HistoryScopeName = 'workspace' | 'quest' | 'ask' | 'failed';

/** What a route names: one scope, by its id (a workspace by its name). */
export type HistoryTarget = { scope: HistoryScopeName; id: string };

/** A unit as the second press names it: exactly what the first press listed (design §5). */
export type HistoryUnitName = { kind: string; id: string };

/**
 * Why a unit stays, or a piece of it is kept: the catalogue's code, which of its sentences (`context`), and what it names.
 * The page says it in the reader's language from the code, never from a sentence: `message` comes only with
 * `DRIVER_REFUSED`, a word from a newer service said verbatim.
 */
export type HistoryReason = {
  code: string;
  context?: string | null;
  quest?: string | null;
  ask?: string | null;
  session?: string | null;
  machine?: string | null;
  workspace?: string | null;
  repository?: string | null;
  branch?: string | null;
  message?: string | null;
};

/** What the home holds of something, by kind, in bytes. */
export type HistorySizes = { conversations: number; transcripts: number; files: number; kept: number; other: number; total: number };

/** One unit as both halves judged it: what it takes, by id, what that holds on the disk, and why it stays, if it does. */
export type HistoryUnit = {
  kind: 'quest' | 'ask' | 'failed';
  id: string;
  workspace?: string | null;
  clearable: boolean;
  quests: string[];
  /** Of `quests`, those a remote numbered: forgotten here, and kept by the team (design §3.2). */
  forgotten: string[];
  asks: string[];
  /** This machine's session records it takes. */
  sessions: string[];
  /** This machine's copies of a teammate's records it takes. */
  teammates: string[];
  /** Why the whole unit stays; null when it may go. */
  keep: HistoryReason | null;
  /** Pieces listed and kept while the unit goes: a teammate's failed session. */
  kept: HistoryReason[];
  bytes: HistorySizes;
};

/** What the home keeps of one workspace's finished work, and what a clear would free (design §2.4). */
export type HistoryReading = {
  workspace: string;
  quests: number;
  asks: number;
  sessions: number;
  teammates: number;
  bytes: HistorySizes;
  /** The intake's room, in bytes. */
  intake: number;
  /** What a clear would take now, the left-over files and an intake's room it frees included. */
  takes: { quests: number; asks: number; sessions: number; teammates: number; bytes: number };
  /** How many units are kept, by the code that keeps each. */
  keptBy: Record<string, number>;
  /** This machine's conversations in it that served no quest, which only *Delete…* takes (D126 §5.4). */
  conversations: { count: number; bytes: number };
  /** For the whole home: files of records no store has any more, which every workspace's clear takes. */
  leftOver: { count: number; bytes: number };
  /** For the whole home: the machine log's size, which a clear never touches. */
  log: number;
};

/** `HISTORY_PLAN`'s answer: every unit, and for a workspace, the reading. */
export type HistoryPlan = { scope: HistoryScopeName; id: string; units: HistoryUnit[]; reading?: HistoryReading | null };

/** `HISTORY_CLEAR`'s answer: what went, in counts and bytes, and what changed since the list and stayed. */
export type HistoryClearAnswer = {
  scope: HistoryScopeName;
  id: string;
  /** How many units the press sent. */
  listed: number;
  cleared: HistoryUnitName[];
  quests: number;
  asks: number;
  sessions: number;
  teammates: number;
  forgotten: number;
  bytes: number;
  /** Left-over files a workspace's clear took. */
  leftOver: number;
  /** Whether a workspace's clear took its intake's room. */
  intake: boolean;
  /** Files the disk would not let go of: left over now, for the next clear of a workspace to take. */
  failed: number;
  changed: (HistoryUnitName & { keep: HistoryReason })[];
};

/**
 * What a page is handed of this machine's driver for its clear (design §6.1): the plan, and the second press. Absent in a
 * browser, which has no driver and no home (D47 §4), so nothing is offered there.
 */
export type HistoryDoor = {
  /** The plan of a quest's or an ask's work; null while it is asked, or where the host answers none. */
  plan: HistoryPlan | null;
  /** A quest's failed sessions' plan (design §1.1's third scope); the ask's page has none. */
  failed?: HistoryPlan | null;
  /** A press on its way: the acts wait for it. */
  busy: boolean;
  /** The second press: exactly the units the first listed; `done` once the driver has answered. */
  onClear: (target: HistoryTarget, units: readonly HistoryUnitName[], done: () => void) => void;
};

/**
 * Where a kept unit's door leads (design §5 step 1): the session's page for *Stop…*, the quest's page for a conflict or an
 * acceptance, the ask's page, the workspace's Branches for *Clean up…*, *Sync now* for unpushed moves.
 */
export type KeptDoor = { to: 'quest' | 'ask' | 'session'; id: string } | { to: 'branches' | 'sync' };

/** The route's payload: one scope, by its name, and `failed` beside a quest for its failed sessions. */
export function historyPayload(target: HistoryTarget): Record<string, unknown> {
  switch (target.scope) {
    case 'workspace': return { workspace: target.id };
    case 'ask': return { ask: target.id };
    case 'failed': return { quest: target.id, failed: true };
    default: return { quest: target.id };
  }
}

const NONE: HistorySizes = { conversations: 0, transcripts: 0, files: 0, kept: 0, other: 0, total: 0 };
const strings = (value: unknown): string[] => (Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string') : []);
const number = (value: unknown): number => (typeof value === 'number' && Number.isFinite(value) ? value : 0);

/**
 * An answer read as a plan only where it has a plan's shape: its units' list. Anything else (a host that answers the route
 * with something older) is no plan, and the pages offer no clear on it rather than guess one. The bridge leaves a null out,
 * so a unit's lists, its reason and its sizes are filled in where it did.
 */
export function historyPlanOf(answer: unknown): HistoryPlan | null {
  const plan = answer as Partial<HistoryPlan> | null;
  if (!plan || typeof plan !== 'object' || !Array.isArray(plan.units)) return null;
  const units = plan.units
    .filter((each): each is HistoryUnit => Boolean(each) && typeof each === 'object' && typeof each.id === 'string')
    .map((each): HistoryUnit => ({
      ...each,
      clearable: each.clearable === true,
      quests: strings(each.quests),
      forgotten: strings(each.forgotten),
      asks: strings(each.asks),
      sessions: strings(each.sessions),
      teammates: strings(each.teammates),
      keep: each.keep ?? null,
      kept: Array.isArray(each.kept) ? each.kept : [],
      bytes: { ...NONE, ...each.bytes },
    }));
  return {
    scope: plan.scope ?? 'workspace',
    id: plan.id ?? '',
    units,
    reading: plan.reading && typeof plan.reading === 'object' ? plan.reading : null,
  };
}

/** Whether a unit takes anything: a quest, an ask, or a session record. A failed-sessions unit with none takes nothing. */
const takesAny = (unit: HistoryUnit) => unit.quests.length + unit.asks.length + unit.sessions.length + unit.teammates.length > 0;
const sum = (units: readonly HistoryUnit[], of: (unit: HistoryUnit) => number) => units.reduce((total, unit) => total + of(unit), 0);

/** What the first press lists: what goes, what stays and why, and what the second press sends (design §5). */
export type ClearList = {
  /** The units that go: those the plan says may, that take anything. */
  going: HistoryUnit[];
  /** The units kept, each with its reason. */
  staying: HistoryUnit[];
  /** What the second press sends: exactly the units going, as the list held them. */
  units: HistoryUnitName[];
  quests: number;
  asks: number;
  sessions: number;
  teammates: number;
  /** Of the quests going, those the remote keeps (design §3.2). */
  forgotten: number;
  /** Pieces kept while their units go: a teammate's failed session. */
  kept: HistoryReason[];
  /** A workspace's: the home's left-over files, which its clear takes (design §2.3). */
  leftOver: { count: number; bytes: number };
  /** A workspace's: the intake's room, in bytes, where its clear takes it, else 0. */
  room: number;
  /** Everything the press frees, in bytes. */
  bytes: number;
  /** Whether the press would take anything at all. */
  takes: boolean;
  /** The workspace whose remote keeps the team's copy of what is forgotten here: the units', else the scope's own. */
  workspace: string | null;
};

/**
 * The first press's list (design §5 step 1). A workspace's reading says what its clear takes beyond its units: the left-over
 * files, and the intake's room where no ask stays, which is what is left of `takes.bytes` after them.
 */
export function clearList(plan: HistoryPlan): ClearList {
  const going = plan.units.filter((unit) => unit.clearable && takesAny(unit));
  const staying = plan.units.filter((unit) => !unit.clearable);
  const reading = plan.scope === 'workspace' ? plan.reading ?? null : null;
  const unitsBytes = sum(plan.units.filter((unit) => unit.clearable), (unit) => unit.bytes.total);
  const leftOver = reading ? { count: number(reading.leftOver?.count), bytes: number(reading.leftOver?.bytes) } : { count: 0, bytes: 0 };
  const room = reading ? Math.max(0, number(reading.takes?.bytes) - unitsBytes - leftOver.bytes) : 0;
  return {
    going,
    staying,
    units: going.map(({ kind, id }) => ({ kind, id })),
    quests: sum(going, (unit) => unit.quests.length),
    asks: sum(going, (unit) => unit.asks.length),
    sessions: sum(going, (unit) => unit.sessions.length),
    teammates: sum(going, (unit) => unit.teammates.length),
    forgotten: sum(going, (unit) => unit.forgotten.length),
    kept: going.flatMap((unit) => unit.kept),
    leftOver,
    room,
    bytes: sum(going, (unit) => unit.bytes.total) + leftOver.bytes + room,
    takes: going.length > 0 || leftOver.count > 0 || room > 0,
    workspace: going.find((unit) => unit.forgotten.length > 0)?.workspace ?? (plan.scope === 'workspace' ? plan.id : null),
  };
}

/** Whether a clear is offered (design §6.1): where its plan says something may go; absent, never disabled, elsewhere. */
export const clearOffered = (plan: HistoryPlan | null | undefined): boolean => (plan ? clearList(plan).takes : false);

/** A reason's facts, as its catalogue sentence names them: its variant as i18next's `context`, and nothing absent. */
export function reasonValues(reason: HistoryReason): Record<string, string> {
  return Object.fromEntries(Object.entries(reason)
    .filter((entry): entry is [string, string] => entry[0] !== 'code' && typeof entry[1] === 'string' && entry[1] !== ''));
}

/** Why a unit stays, in the reader's language: the refusal's own sentence for its code, the variant its context names. */
export const reasonSaid = (t: Translate, reason: HistoryReason) => t(`errors.${reason.code}`, reasonValues(reason));

/** The door that frees a kept unit, where this machine has one; a teammate's record has none here. */
export function keptDoor(reason: HistoryReason): KeptDoor | null {
  const { code, context, quest, ask, session } = reason;
  switch (code) {
    case 'HISTORY_TREE_HERE':
    case 'HISTORY_LANDING_STANDS':
      return { to: 'branches' };
    case 'HISTORY_UNPUSHED':
      return { to: 'sync' };
    case 'HISTORY_LIVE':
      return context === 'teammate' || !session ? null : { to: 'session', id: session };
    case 'HISTORY_NEEDS_YOU':
      if ((context === 'held' || context === 'conflict') && quest) return { to: 'quest', id: quest };
      if ((context === 'ask' || context === 'proposalAsk') && ask) return { to: 'ask', id: ask };
      return !context && session ? { to: 'session', id: session } : null;
    case 'HISTORY_OPEN':
    case 'HISTORY_AWAITED':
      return quest ? { to: 'quest', id: quest } : null;
    case 'HISTORY_ASKED':
      return ask ? { to: 'ask', id: ask } : null;
    default:
      return null;
  }
}

/** The counts a sentence names, each in its own plural, the empty ones left out, joined the reader's way. */
function counted(t: Translate, counts: { quests: number; asks: number; sessions: number; teammates: number }) {
  return ([
    ['history.count.closed', counts.quests],
    ['history.count.asks', counts.asks],
    ['history.count.sessions', counts.sessions],
    ['history.count.teammates', counts.teammates],
  ] as const)
    .filter(([, count]) => count > 0)
    .map(([key, count]) => t(key, { count }))
    .join(t('history.listJoin'));
}

/** The codes a reading may count, in the order a person meets them (design §1.2); a newer one is said as one this window does not know. */
const KEPT_CODES = [
  'HISTORY_OPEN', 'HISTORY_LIVE', 'HISTORY_NEEDS_YOU', 'HISTORY_AWAITED', 'HISTORY_ASKED', 'HISTORY_TREE_HERE',
  'HISTORY_LANDING_STANDS', 'HISTORY_UNPUSHED', 'HISTORY_NOT_OURS', 'HISTORY_UNKNOWN',
];

/** One reason's count in a reading, with its sentence and the door that frees it. */
export type KeptCount = { code: string; count: number; text: string; door: KeptDoor | null };

/** A workspace's reading said (design §2.4): each line, or null where it has nothing to say. */
export type ReadingSaid = {
  holds: string;
  takes: string;
  kept: KeptCount[];
  conversations: string | null;
  leftOver: string | null;
  log: string;
};

/**
 * What a clear would take now, said from the reading's counts as well as its bytes (HIST1k): a unit that goes takes its
 * records whether or not they hold a file, and every workspace's clear takes the left-over files however small, so "nothing"
 * is said only where `clearList` would offer no press. Records and files, records alone, or files alone each say so.
 */
function takesSaid(t: Translate, reading: HistoryReading): string {
  const what = counted(t, {
    quests: number(reading.takes?.quests), asks: number(reading.takes?.asks),
    sessions: number(reading.takes?.sessions), teammates: number(reading.takes?.teammates),
  });
  const bytes = number(reading.takes?.bytes);
  if (what) return t(bytes > 0 ? 'history.reading.takes' : 'history.reading.takesRecords', { what, size: size(bytes) });
  if (bytes > 0 || number(reading.leftOver?.count) > 0) return t('history.reading.takesFiles', { size: size(bytes) });
  return t('history.reading.takesNothing');
}

/**
 * What the home keeps of a workspace's finished work, in the reader's language (design §2.4): the closed quests and asks
 * and their sessions with their size, what a clear would take now, a count per reason that keeps the rest with its door
 * where the workspace's page holds one, the conversations only *Delete…* takes, and the home's own left-over files and log.
 */
export function readingSaid(t: Translate, reading: HistoryReading): ReadingSaid {
  const held = counted(t, reading);
  const keptBy = reading.keptBy ?? {};
  const codes = [...KEPT_CODES.filter((code) => code in keptBy), ...Object.keys(keptBy).filter((code) => !KEPT_CODES.includes(code))];
  return {
    holds: held ? t('history.reading.holds', { what: held, size: size(number(reading.bytes?.total)) }) : t('history.reading.none'),
    takes: takesSaid(t, reading),
    kept: codes.filter((code) => number(keptBy[code]) > 0).map((code) => {
      const known = KEPT_CODES.includes(code);
      const door = code === 'HISTORY_TREE_HERE' || code === 'HISTORY_LANDING_STANDS'
        ? { to: 'branches' } as const
        : code === 'HISTORY_UNPUSHED' ? { to: 'sync' } as const : null;
      return { code, count: keptBy[code]!, door, text: t(known ? `history.reading.kept.${code}` : 'history.reading.kept.other', { count: keptBy[code] }) };
    }),
    conversations: number(reading.conversations?.count) > 0
      ? t('history.reading.conversations', { count: reading.conversations.count, size: size(number(reading.conversations.bytes)) })
      : null,
    leftOver: number(reading.leftOver?.count) > 0 ? t('history.reading.leftOver', { size: size(number(reading.leftOver.bytes)) }) : null,
    log: t('history.reading.log', { size: size(number(reading.log)) }),
  };
}

/** What a first press says it takes, as one lead sentence, for a quest's, its failed sessions', an ask's or a workspace's. */
export function clearLead(t: Translate, target: HistoryTarget, list: ClearList): string {
  const bytes = size(list.bytes);
  const sessions = t('history.count.sessions', { count: list.sessions + list.teammates });
  switch (target.scope) {
    case 'quest':
      return list.sessions + list.teammates > 0
        ? t('history.lead.quest', { id: target.id, sessions, size: bytes })
        : t('history.lead.questAlone', { id: target.id, size: bytes });
    case 'failed':
      return t('history.lead.failed', { id: target.id, sessions: t('history.count.failed', { count: list.sessions }), size: bytes });
    case 'ask':
      return t('history.lead.ask', { id: target.id, quests: t('history.count.quests', { count: list.quests }), sessions, size: bytes });
    default:
      return t('history.lead.workspace', { workspace: target.id, size: bytes });
  }
}

/**
 * The last sentence of a first press (design §6.1): on a wired workspace, where a quest of it is forgotten here, the remote
 * keeps the team's copy and this machine will not fetch it again; otherwise nothing brings it back.
 */
export const clearEnd = (t: Translate, list: ClearList) => (list.forgotten > 0 && list.workspace
  ? t('history.end.remote', { workspace: list.workspace })
  : t('history.end.gone'));

/** What a workspace's first press lists as going, a line per kind (design §6.1: the counts by kind). */
export function goingSaid(t: Translate, list: ClearList): string[] {
  return [
    ...(list.quests > 0 ? [t('history.count.closed', { count: list.quests })] : []),
    ...(list.asks > 0 ? [t('history.count.asks', { count: list.asks })] : []),
    ...(list.sessions > 0 ? [t('history.count.sessions', { count: list.sessions })] : []),
    ...(list.teammates > 0 ? [t('history.count.teammates', { count: list.teammates })] : []),
    ...(list.leftOver.count > 0 ? [t('history.going.leftOver', { size: size(list.leftOver.bytes) })] : []),
    ...(list.room > 0 ? [t('history.going.room', { size: size(list.room) })] : []),
  ];
}

/** A notice the second press says: its text in the reader's language, and its tone. */
export type ClearNotice = { text: string; tone: 'ok' | 'error' };

/**
 * What the second press says (design §5, §6.1): a quest or an ask cleared from this machine; a quest's failed sessions by
 * how many went; a workspace by how many of the listed units went and what it freed, and how many changed since the list
 * and were kept. A file the disk would not let go of is said apart, since the next workspace's clear takes it.
 */
export function clearSaid(t: Translate, answer: HistoryClearAnswer): ClearNotice[] {
  const cleared = answer.cleared?.length ?? 0;
  const changed = answer.changed?.length ?? 0;
  const freed = size(number(answer.bytes));
  let text: string;
  if (answer.scope === 'workspace') {
    text = answer.listed > 0
      ? t('history.cleared.workspace', { cleared, listed: answer.listed, workspace: answer.id, size: freed })
      : t('history.cleared.leftOnly', { size: freed });
  } else if (cleared === 0) {
    text = t('history.cleared.none');
  } else if (answer.scope === 'failed') {
    text = t('history.cleared.failed', { count: answer.sessions, id: answer.id });
  } else {
    text = t(`history.cleared.${answer.scope}`, { id: answer.id });
  }
  if (changed > 0) text = t('history.join', { first: text, second: t('history.cleared.changed', { count: changed }) });
  return [
    { text, tone: 'ok' },
    ...(number(answer.failed) > 0 ? [{ text: t('history.cleared.failedFiles', { count: answer.failed }), tone: 'error' as const }] : []),
  ];
}
