// The machine's waiting items as `QUEUES` answers them, and each one's place: the page's twin of the terminal's reader
// (`QueueRows.Placed` in the driver), which reads the shared table `fixtures/queue-places.json` as `queuePlaces.test.ts` does. A row
// carries the sort inputs and never its place, so the window and `daoris queue` compose one place for one item from one reading.

/** The classes, first to go first; a word none of them names reads as `normal` (`PriorityClasses` in the driver). */
export const CLASSES = ['urgent', 'high', 'normal', 'low'] as const;
export type QueueClass = (typeof CLASSES)[number];

/** Where in its work an item waits, the second key: a landing first, a new start last (`QueueStage`). */
export const STAGES = ['landing', 'back-at-checks', 'resume', 'first-checks', 'new-start'] as const;
export type QueueStage = (typeof STAGES)[number];

/** The queues in the order they are shown: the planner's starts and resumes, then the checks waiting for a slot. */
export const QUEUES = ['starts', 'checks'] as const;

/** One waiting item, as `QueueRows.Json` writes it. */
export type QueueRow = {
  queue: string;
  /** The quest a start is of, or the run a check is of: the id a class is set on. */
  item: string;
  /** The class it is ordered by at the look, aged where its wait passed the bound. */
  class: QueueClass;
  stage: QueueStage;
  /** When it began to wait; null where that is not known, which sorts after every known one. */
  arrived: string | null;
  /** Its queue's own order where the comparator leaves it equal. */
  order: number;
  /** Where its class comes from: `quest`, `ask` or `unset`. */
  source: string;
  by?: string;
  why?: string;
  ask?: string;
  agedFrom?: string;
  repository: string;
  /** Why it waits: a start's sitting code, a check's slot code. */
  code: string;
  /** A start's verdict, whose sentence its code is. */
  verdict?: string;
  values: Record<string, string>;
  /** The same reason in the driver's English: what is said where the page has no words for its code. */
  reason: string;
};

/** A row with its place: in its queue, how many wait there, and the rows ahead of it, first first. */
export type QueueSpot = { row: QueueRow; position: number; of: number; ahead: QueueRow[] };

const text = (value: unknown): string | undefined => (typeof value === 'string' && value !== '' ? value : undefined);

const classOf = (value: unknown): QueueClass => (CLASSES as readonly string[]).includes(value as string) ? value as QueueClass : 'normal';

/** One row read field by field, or null where it names no queue, no item or a stage none of the five. */
export function readQueueRow(raw: unknown): QueueRow | null {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return null;
  const row = raw as Record<string, unknown>;
  const queue = text(row.queue);
  const item = text(row.item);
  const stage = text(row.stage);
  if (!queue || !item || !stage || !(STAGES as readonly string[]).includes(stage)) return null;
  const values = row.values && typeof row.values === 'object' && !Array.isArray(row.values)
    ? Object.fromEntries(Object.entries(row.values as Record<string, unknown>).filter((each): each is [string, string] => typeof each[1] === 'string'))
    : {};
  const optional = (name: 'by' | 'why' | 'ask' | 'agedFrom' | 'verdict') => (text(row[name]) !== undefined ? { [name]: text(row[name]) } : {});
  return {
    queue, item, class: classOf(row.class), stage: stage as QueueStage, arrived: text(row.arrived) ?? null,
    order: typeof row.order === 'number' ? row.order : 0, source: text(row.source) ?? 'unset',
    ...optional('by'), ...optional('why'), ...optional('ask'), ...optional('agedFrom'),
    repository: typeof row.repository === 'string' ? row.repository : '', code: typeof row.code === 'string' ? row.code : '',
    ...optional('verdict'), values, reason: typeof row.reason === 'string' ? row.reason : '',
  };
}

/** The rows of a `QUEUES` answer, each that reads; an answer of another shape is none. */
export function readQueueRows(raw: unknown): QueueRow[] {
  const rows = raw && typeof raw === 'object' && Array.isArray((raw as { rows?: unknown }).rows) ? (raw as { rows: unknown[] }).rows : [];
  return rows.map(readQueueRow).filter((row): row is QueueRow => row !== null);
}

const arrival = (row: QueueRow): number => {
  const at = row.arrived ? Date.parse(row.arrived) : Number.NaN;
  return Number.isNaN(at) ? Number.POSITIVE_INFINITY : at;
};

/** The one comparator: class, then stage, then first arrival (an unknown one last), then the queue's own order. */
const compare = (a: QueueRow, b: QueueRow): number =>
  CLASSES.indexOf(a.class) - CLASSES.indexOf(b.class)
  || STAGES.indexOf(a.stage) - STAGES.indexOf(b.stage)
  || (arrival(a) === arrival(b) ? 0 : arrival(a) < arrival(b) ? -1 : 1)
  || a.order - b.order;

/** Each row's place in its queue, the queues starts then checks and any other after them. */
export function placed(rows: readonly QueueRow[]): QueueSpot[] {
  const rank = (queue: string) => {
    const at = (QUEUES as readonly string[]).indexOf(queue);
    return at >= 0 ? at : QUEUES.length;
  };
  const queues = [...new Set(rows.map((row) => row.queue))].sort((a, b) => rank(a) - rank(b));
  return queues.flatMap((queue) => {
    const ordered = rows.filter((row) => row.queue === queue).sort(compare);
    return ordered.map((row, at) => ({ row, position: at + 1, of: ordered.length, ahead: ordered.slice(0, at) }));
  });
}
