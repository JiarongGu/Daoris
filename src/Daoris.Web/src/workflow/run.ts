import type { Session } from '../api';
import type { WorkflowStep } from './current';

// Where a piece of work stands in its workflow, as the shell answers it (WORKFLOW1c; D157 point 11, the workflow design §5.1–§5.2,
// §7): the driver's `WorkflowRunReader`, read from the records that already exist. The page draws it and derives nothing: where
// each step stands is the driver's, and only the words and the doors are the page's.

/** Where a step stands (design §5.2), as the driver spells it (`WorkflowRunStates`). */
export type RunState =
  | 'not-reached' | 'working' | 'waiting-on-you' | 'waiting-on-agent' | 'not-known' | 'cannot-start'
  | 'done' | 'skipped' | 'failed' | 'stopped' | 'declared';

/** The states this page words; one a newer driver names is said as recorded, never dropped. */
export const KNOWN_STATES: readonly RunState[] = [
  'not-reached', 'working', 'waiting-on-you', 'waiting-on-agent', 'not-known', 'cannot-start', 'done', 'skipped', 'failed', 'stopped',
  'declared',
];

/** One step of a run: Current's cell beside where it stands, and the facts its words and its door are made of. */
export interface RunStep {
  step: WorkflowStep;
  /** One of `RunState`; a state this page does not know is said as recorded. */
  state: string;
  /** Which row of the state's table, read with the step's kind (`WorkflowRunDetails`). */
  detail: string;
  /** Why a step Current does not draw is in this run: the review's level that asked for a look, or `opinion`. */
  added?: string | null;
  session?: string | null;
  quest?: string | null;
  /** Who acts, as the record names it: a session's adapter, or a reviewer as the person reads it. */
  agent?: string | null;
  at?: string | null;
  environment?: string | null;
  commit?: string | null;
  branch?: string | null;
  /** The pull request's address, as its plugin answered it. */
  pullRequest?: string | null;
  plugin?: string | null;
  count?: number | null;
  of?: number | null;
  /** A go-ahead's number on the run's ask. */
  goAhead?: number | null;
  /** Words the record kept: the person's, or a session's note. Shown as written. */
  words?: string | null;
  /** A code the record kept: a hold, a landing try's, a pass's failure, an ask's, who accepted. */
  code?: string | null;
}

/** A chain's work in one repository, read as steps. */
export interface WorkflowRun {
  repository: string;
  workspace: string;
  ask?: string | null;
  quests: string[];
  /** The run's newest session: the one its doors attend. */
  session?: string | null;
  /** The step it stands at, or none where every step is settled: the run finished. */
  at?: string | null;
  /** Its Current's version, the plugins that may hold a start and each limit's sentence, as `WORKFLOW_CURRENT` answers them. */
  workflow: { version: string; startHolds: string[]; limits: Record<string, string> };
  steps: RunStep[];
}

/** What `WORKFLOW_RUN` answers: the runs, or none with the driver's sentence. */
export interface WorkflowRunAnswer {
  runs: WorkflowRun[];
  problem?: string | null;
}

/** Whose runs are read: a session's, a quest's or an ask's. */
export type RunScope = { session: string } | { quest: string } | { ask: string };

/** A state as the page words it: one it does not know stays as the driver spelled it. */
export const knownState = (state: string): state is RunState => (KNOWN_STATES as readonly string[]).includes(state);

/**
 * A state's pill (design §7's hues): waiting on the person in open's hue, the person's alone (platform-ux §3); done's green for
 * a step done; red only for a failure, an outcome; every other wait, on an agent or on Daoris, neutral.
 */
export function runTone(state: string): 'open' | 'done' | 'declined' | 'neutral' {
  if (state === 'waiting-on-you') return 'open';
  if (state === 'done') return 'done';
  if (state === 'failed') return 'declined';
  return 'neutral';
}

/** The step a run stands at, or null where it finished. */
export const standsAt = (run: WorkflowRun): RunStep | null => run.steps.find((step) => step.step.id === run.at) ?? null;

/** Whether a run waits on the person now: the step it stands at waits for their press. */
export const waitsOnYou = (run: WorkflowRun): boolean => standsAt(run)?.state === 'waiting-on-you';

/**
 * The session a door into a run attends (design §7: *What needs you*'s row for a waiting step opens that step): a session the
 * row names, else the newest of the quest's sessions here. Null where nothing here ran it, which offers no door.
 */
export function runSessionOf(
  row: { session?: string | null; quest?: string | null },
  sessions: readonly Pick<Session, 'id' | 'quest' | 'created'>[],
): string | null {
  if (row.session) return row.session;
  if (!row.quest) return null;
  const mine = sessions.filter((session) => session.quest === row.quest);
  return mine.length === 0 ? null : [...mine].sort((a, b) => a.created.localeCompare(b.created)).at(-1)!.id;
}
