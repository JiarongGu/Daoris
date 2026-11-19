import type { StepWait } from '../waits/dock';
import type { SessionGrouping } from '../groups';

// A session's run as its page reads it, from the run's one answer, `SESSION_GROUPS`' `passage` (`RunPassageReader`): the head's track,
// a chip a step, and the timeline's rows, each step's start and end, so the two cannot disagree. Pure: the page draws what this says.

/** A run's passage as the groups' answer gives it: its steps in its graph's order, each with its mark, role and attempts. */
export type Passage = NonNullable<SessionGrouping['passage']>;

export type PassageStep = Passage['steps'][number];

/** A mark the driver gives (`RunPassageReader`); one a newer driver adds is drawn as one to come. */
export type Mark = 'passed' | 'current' | 'next' | 'failed';

const MARKS: ReadonlySet<string> = new Set(['passed', 'current', 'next', 'failed']);

export const markOf = (mark: string): Mark => (MARKS.has(mark) ? mark as Mark : 'next');

/** One chip of the track: a step, its mark, the role that takes it, its attempt where the run stands at it, and its dock wait. */
export type TrackChip = {
  node: string;
  mark: Mark;
  role: string | null;
  attempt: number | null;
  /** The dock's wait on this step, where the person settles it or an agent holds it: the chip reads whom it waits on. */
  wait: StepWait | null;
};

/** The track: its run and workflow, its chips in the graph's order, and how far it came of how many. */
export type Track = { run: string; workflow: string; chips: TrackChip[]; at: number; of: number };

/**
 * **The track** of a run's passage: each step a chip with its mark, the standing (current or failed) one with its attempt, a step the
 * dock holds with its wait. *At* counts the steps passed and the one the run stands at.
 */
export function trackOf(passage: Passage, waits: readonly StepWait[]): Track {
  const chips = passage.steps.map((step): TrackChip => {
    const mark = markOf(step.mark);
    return {
      node: step.node,
      mark,
      role: step.role ?? null,
      attempt: (mark === 'current' || mark === 'failed') && step.attempt ? step.attempt : null,
      wait: waits.find((wait) => wait.run === passage.run && wait.node === step.node) ?? null,
    };
  });
  const passed = chips.filter((chip) => chip.mark === 'passed').length;
  const standing = chips.some((chip) => chip.mark === 'current' || chip.mark === 'failed') ? 1 : 0;
  return { run: passage.run, workflow: passage.workflow, chips, at: Math.min(passed + standing, chips.length), of: chips.length };
}

/** A row of the timeline: a step's attempt starting or ending, with what it came to and what failed it. */
export type StepRow = {
  key: string;
  run: string;
  node: string;
  edge: 'start' | 'end';
  attempt: number | null;
  /** A start's `current`; an end's `passed` or `failed`, or `ended` where its plugin said neither. */
  mark: Mark | 'ended';
  code?: string | null;
  /** When it happened, in ms: an end the record keeps no moment for is placed where its next attempt or step started, else last. */
  at: number;
  /** Its step's place in the graph's order, which orders rows at one moment. */
  order: number;
};

const time = (iso: string | null | undefined): number | null => {
  const ms = iso ? Date.parse(iso) : Number.NaN;
  return Number.isFinite(ms) ? ms : null;
};

const outcomeMark = (outcome: string | null | undefined): StepRow['mark'] =>
  outcome === 'reached' ? 'passed' : outcome === 'failed' ? 'failed' : 'ended';

/**
 * **The steps' rows**, in time: each attempt's start where its record clocked it, and its end where it ended. The record keeps no
 * moment for an outcome, so an end stands where the step's next attempt started, else where a later step first started, else at
 * the timeline's foot; a step to come has none.
 */
export function stepRowsOf(passage: Passage): StepRow[] {
  const starts = passage.steps.map((step) => [
    ...(step.earlier ?? []).map((each) => time(each.at)),
    time(step.since),
  ].filter((ms): ms is number => ms !== null));
  const laterStart = (order: number): number => {
    const later = starts.slice(order + 1).flat();
    return later.length > 0 ? Math.min(...later) : Number.POSITIVE_INFINITY;
  };
  const rows: StepRow[] = [];
  passage.steps.forEach((step, order) => {
    const mark = markOf(step.mark);
    const row = (edge: StepRow['edge'], attempt: number | null, said: StepRow['mark'], at: number, code?: string | null): StepRow => ({
      key: `${passage.run}/${step.node}/${attempt ?? 0}/${edge}`, run: passage.run, node: step.node, edge, attempt, mark: said, code: code ?? null, at, order,
    });
    const newestStart = time(step.since);
    (step.earlier ?? []).forEach((each, index, all) => {
      const start = time(each.at);
      if (start === null) return;
      rows.push(row('start', each.attempt, 'current', start));
      const next = index + 1 < all.length ? time(all[index + 1]!.at) : newestStart;
      rows.push(row('end', each.attempt, outcomeMark(each.outcome), next ?? laterStart(order), each.code));
    });
    if (newestStart !== null) rows.push(row('start', step.attempt ?? null, 'current', newestStart));
    if (mark === 'passed' || mark === 'failed') {
      rows.push(row('end', step.attempt ?? null, mark, Math.max(laterStart(order), newestStart ?? Number.NEGATIVE_INFINITY), mark === 'failed' ? step.code : null));
    }
  });
  return rows.sort((a, b) => a.at - b.at || a.order - b.order || (a.attempt ?? 0) - (b.attempt ?? 0) || (a.edge === b.edge ? 0 : a.edge === 'end' ? -1 : 1));
}

/** The rows from `from` (none: from the first) until `until` (none: to the foot): what stands between a turn's words and the next's. */
export function rowsWithin(rows: readonly StepRow[], from: string | undefined, until: string | undefined): StepRow[] {
  const low = from === undefined ? Number.NEGATIVE_INFINITY : time(from) ?? Number.NEGATIVE_INFINITY;
  const high = until === undefined ? Number.POSITIVE_INFINITY : time(until) ?? Number.POSITIVE_INFINITY;
  return rows.filter((row) => row.at >= low && (row.at < high || (high === Number.POSITIVE_INFINITY && row.at === high)));
}

/** The dock's key for a step's wait (`dockOf`'s), which a failed step's row leads to. */
export const dockKeyOf = (run: string, node: string) => `step:${run}/${node}`;

/** The attribute a step's rows carry, which a chip's press scrolls to. */
export const rowMark = (run: string, node: string) => `${run}/${node}`;
