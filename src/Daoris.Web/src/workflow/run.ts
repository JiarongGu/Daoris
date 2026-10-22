import type { Session } from '../data/api';
import type { WorkflowStep } from './current';

// Where a piece of work stands in its workflow, as the shell answers it (WORKFLOW1c; D157 point 11, the workflow design §5.1–§5.2,
// §7): the driver's `WorkflowRunReader`, read from the records that already exist. The page draws it and derives nothing: where
// each step stands is the driver's, and only the words and the doors are the page's.

/** Where a step stands (design §5.2), as the driver spells it (`WorkflowRunStates`). */
export type RunState =
  | 'not-reached' | 'working' | 'waiting-on-you' | 'waiting-on-agent' | 'not-known' | 'cannot-start'
  | 'done' | 'skipped' | 'failed' | 'stopped';

/** The states this page words; one a newer driver names is said as recorded, never dropped. */
export const KNOWN_STATES: readonly RunState[] = [
  'not-reached', 'working', 'waiting-on-you', 'waiting-on-agent', 'not-known', 'cannot-start', 'done', 'skipped', 'failed', 'stopped',
];

/** One step of a run: the chosen graph's cell beside where it stands, and the facts its words and its door are made of. */
export interface RunStep {
  step: WorkflowStep;
  /** One of `RunState`; a state this page does not know is said as recorded. */
  state: string;
  /** Which row of the state's table, read with the step's kind (`WorkflowRunDetails`). */
  detail: string;
  /** Why a step Current does not draw is in this run: the review's level that asked for a look for this work. */
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
  /**
   * On a run bound to a graph version (FABRIC-CUTOVER1e), the node's own kind (`claimed`, `handed`, a plugin's `<plugin>/<node>`); its
   * detail is then its record's state (`GRAPH_DETAILS`). None on a steps run's step, and on a look the work added beside a graph.
   */
  node?: string | null;
  /** What a graph run's node observes: `quest` (its `quest`), `branch` (its `branch` at its `commit`) or `landing`; none for the run itself. */
  on?: string | null;
}

/** A wire of a graph run's graph: from a node to a node, by its kind (`after`, `failed`, `rejected`, …). */
export interface GraphWire {
  from: string;
  to: string;
  kind: string;
}

/**
 * A run bound to a graph version, drawn from its record (FABRIC-CUTOVER1e; D190 points 2 and 3): its graph's entries, end and wires, the
 * subjects its nodes observe, and what its record says of it. Its nodes are the run's steps, each with its `node`.
 */
export interface GraphRun {
  run: string;
  entries: string[];
  end: string;
  wires: GraphWire[];
  /** Its newest work quest here, which `claimed` observes. */
  quest?: string | null;
  /** The branch its newest session worked, which its gates and its landing observe. */
  branch?: string | null;
  /** Whether Daoris has judged its graph yet. */
  judged: boolean;
  /** Its record does not read. */
  unread: boolean;
  /** The quest it ended at, where its record keeps its end. */
  ended?: string | null;
}

/** A chain's work in one repository, read as steps. */
export interface WorkflowRun {
  repository: string;
  workspace: string;
  ask?: string | null;
  quests: string[];
  /** The run's newest session: the one its doors attend. */
  session?: string | null;
  /** Its unsettled step, or none when settled or unreadable; check `problem` before saying it finished. */
  at?: string | null;
  /** The driver's explanation where the bound process could not be read; an empty graph here is never a finished run. */
  problem?: string | null;
  workflowGate?: import('./gate').WorkflowGateState | null;
  /**
   * Its graph digest, bound identity when named, start plugins and each limit's sentence; and the route that chose its workflow, as its
   * binding keeps it (FABRIC-PIPELINE1a): the level, and the kind with its label where one chose it.
   */
  workflow: {
    version: string; startHolds: string[]; limits: Record<string, string>; id?: string | null; boundVersion?: number | null;
    route?: { level: string; kind?: string | null; label?: string | null } | null;
  };
  steps: RunStep[];
  /** A run bound to a graph version, drawn from its record (FABRIC-CUTOVER1e); none for a steps run, and from a shell before it. */
  graph?: GraphRun | null;
}

/** What `WORKFLOW_RUN` answers: the runs, or none with the driver's sentence. */
export interface WorkflowRunAnswer {
  runs: WorkflowRun[];
  problem?: string | null;
}

/** Whose runs are read: a session's, a quest's or an ask's. */
export type RunScope = { session: string } | { quest: string } | { ask: string };

/** What a quest's or an ask's page is handed of its runs (design §7): the runs, and the door that opens one. */
export type RunLineDoor = { runs: readonly WorkflowRun[]; onOpen?: (run: WorkflowRun) => void };

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
