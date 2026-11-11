import type { Session } from '../data/api';
import type { WorkflowStep } from './current';

// Where a piece of work stands in its workflow, as the shell answers it: the driver's `WorkflowRunReader`, read from the records that already exist. The page draws it and derives nothing: where
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
   * On a run bound to a graph version, the node's own kind (`claimed`, `handed`, a plugin's `<plugin>/<node>`); its
   * detail is then its record's state (`GRAPH_DETAILS`). None on a steps run's step, and on a look the work added beside a graph.
   */
  node?: string | null;
  /** What a graph run's node observes: `quest` (its `quest`), `branch` (its `branch` at its `commit`) or `landing`; none for the run itself. */
  on?: string | null;
  /**
   * What the person may say of a graph run's node now, as the driver's one table takes it: `skip`, `stop`. None where it
   * offers neither, on a steps run's step, and from a shell before it.
   */
  acts?: string[] | null;
  /** The person's word on a graph run's node, kept and not applied yet; none where none waits. */
  asked?: NodeWordKept | null;
  /** The gate a failed check named; none on another step, and from a shell before it. */
  gate?: string | null;
  /** A check's attempt held unknown for the person, or settled by their stop; none on another step. */
  held?: CheckHeld | null;
  /**
   * The requests the work's sessions made, on the work step only: the quests they
   * published and the helpers they asked. None where there are none, and from a shell before it.
   */
  requests?: RunRequest[] | null;
  /**
   * The unit's integration state as the integration record says it, on the `integrate`
   * node only; none from a shell before it, or where nothing is asked.
   */
  integration?: { state: string; line: string; step?: string | null } | null;
  wait?: RunWait | null;
}

/** A graph run's node's wait (`WorkflowRunWait`): one of its kind's `waits`, worded as `workflow.wait.<kind>.<code>` with its facts. */
export interface RunWait {
  code: string;
  values?: Record<string, string> | null;
  since?: string | null;
  until?: string | null;
}

/** What a request inside the work is: a quest a session published, or a helper it asked (a request to an agent). */
export type RunRequestKind = 'quest' | 'agent';

/** One request the work's sessions made, as the driver draws it; its words are the page's. */
export interface RunRequest {
  /** One of `RunRequestKind`. */
  kind: string;
  /** The quest's id, or the helper's session. */
  id: string;
  /** The repository the quest is asked of; the helper's agent. */
  to: string;
  /** A quest's status lowercased (`open`, `taken`, `done`, `declined`); a helper's `working`, `done`, `failed` or `stopped`. */
  state: string;
  /** The session of the work that made it. */
  session?: string | null;
  /** Whether a session of the work waits on its answer. */
  awaits: boolean;
  /** What its close said, a done's note or a decline's reason, as written. */
  words?: string | null;
  at?: string | null;
}

/**
 * A check held unknown: its unit and attempt, for the person's word (`WORK_HELD`), the words they
 * may say (`again` is *Run again*, `stop` is *Not now*), and the word they gave that the driver's next look has not applied yet.
 */
export interface CheckHeld {
  unit: string;
  attempt: number;
  words: string[];
  resolution?: string | null;
}

/** The person's word on a held check (`WORK_HELD`): *Run again* is `again`, *Not now* is `stop`. */
export interface CheckHeldAsk {
  unit: string;
  node: string;
  attempt: number;
  resolution: 'again' | 'stop';
}

/** What `WORK_HELD` answers: kept, or refused by its code with the driver's sentence. */
export interface CheckHeldAnswer {
  done: boolean;
  refused?: string | null;
  message?: string | null;
}

/**
 * What the person may say of a graph run's node: pass it over for the run, or stop it; a declined request's ask again and go
 * on; a review's changes, with their words.
 */
export type NodeAct = 'skip' | 'stop' | 'send' | 'ask-again' | 'go-on' | 'changes' | 'done' | 'approve' | 'reject' | 'keep-yours' | 'take-session' | 'keep-both';

/** A word kept on a run's node, as the driver answers it: the act, the person's words where given, and when. */
export interface NodeWordKept {
  node: string;
  act: string;
  words?: string | null;
  at: string;
}

/** What a press of the person's word sends (`WORKFLOW_NODE`): the run, its node, the act, and their words. */
export interface NodeWordAsk {
  run: string;
  node: string;
  act: NodeAct;
  words: string | null;
}

/** What `WORKFLOW_NODE` answers: kept, or refused by its code with the driver's sentence. */
export interface NodeWordAnswer {
  run: string;
  node: string;
  act: string;
  done: boolean;
  refused?: string | null;
  message?: string | null;
}

/** A wire of a graph run's graph: from a node to a node, by its kind (`after`, `failed`, `rejected`, `when`, …). */
export interface GraphWire {
  from: string;
  to: string;
  kind: string;
  /** The outcome a `when` wire follows; none on every other kind. */
  outcome?: string | null;
}

/** A way back to a node before its source, taken `on` the source's `rejected`, its `failed` or one of its outcomes. */
export interface GraphBack {
  from: string;
  to: string;
  on: string;
}

/**
 * A run bound to a graph version, drawn from its record: its graph's entries, end and wires, the
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
  /** Its `back` wires, apart from `wires`, which the run is read along; none from a shell before it. */
  backs?: GraphBack[] | null;
  /** The nodes no route can start any more given the outcomes reached; none from a shell before it. */
  notTaken?: string[] | null;
  /** Each reached decision's outcome, by node; none from a shell before it. */
  outcomes?: Record<string, string> | null;
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
  /** The problem as a code and its values, which the page words from its catalogue; absent or null where the driver has none. */
  problemCoded?: { code: string; values: Record<string, string> } | null;
  workflowGate?: import('./gate').WorkflowGateState | null;
  /**
   * Its graph digest, bound identity when named, start plugins and each limit's sentence; and the route that chose its workflow, as its
   * binding keeps it: the level, and the kind with its label where one chose it.
   */
  workflow: {
    version: string; startHolds: string[]; limits: Record<string, string>; id?: string | null; boundVersion?: number | null;
    route?: { level: string; kind?: string | null; label?: string | null } | null;
  };
  steps: RunStep[];
  /** A run bound to a graph version, drawn from its record; none for a steps run, and from a shell before it. */
  graph?: GraphRun | null;
  /**
   * A chat run: its key, `chat-<first>`, its checks where its repository sets them, and whether
   * they are enforced for a conversation, which they are not yet. None for a quest's run, and from a shell before it.
   */
  chat?: { run: string; checks?: string | null; enforced: boolean } | null;
  /** A verification run's commit, branch and verdict; none for a chain's or a chat's, and from a shell before it. */
  line?: RunLine | null;
}

/**
 * A verification run as a repository's line reads it: the commit it judged, the branch
 * asked of, who asked it and when, its verdict, and each gate as its checks' newest results say.
 */
export interface RunLine {
  run: string;
  commit: string;
  branch?: string | null;
  /** Who chose it: `person` for a person's verification of a commit or a branch, else the landed route's level that bound it for its line. */
  level: string;
  /** The line a landed run's batch landed on; absent for a person's run. */
  line?: string | null;
  /** Where a landed run's batch begins, the tip of the line's newest run with a verdict; absent for its first verification. */
  base?: string | null;
  at: string;
  /** `verified`, `failed`, `not-now`, `unknown`, `running` or `waiting`; one the page does not know is said as the driver spelled it. */
  verdict: string;
  /** The gate a failed check named. */
  gate?: string | null;
  /** Why it is not known: `changed`, `bound`, `stopped`, `busy`, or another the page says as the general. */
  why?: string | null;
  /** Each gate as its check ran it, its verdict in the runner's own word. */
  gates: { name: string; verdict: string; exit?: number | null; wallSeconds?: number | null }[];
}

/**
 * What `WORKFLOW_RUN` answers: the runs, or none with the driver's English sentence and, from a driver that sends one, its code, which the page words in the reader's language, with the record's id and the workflow's id its words name.
 */
export interface WorkflowRunAnswer {
  runs: WorkflowRun[];
  problem?: string | null;
  code?: string | null;
  id?: string | null;
  workflow?: string | null;
}

/** Whose runs are read: a session's, a quest's or an ask's. */
export type RunScope = { session: string } | { quest: string } | { ask: string } | { line: string };

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

/**
 * Whether the driver's record says the run finished: it has no step to stand at, and nothing in it is unread or held, or a graph
 * Daoris has not judged. *Finished* is drawn from this alone, never from a step's being done.
 */
export const finishedBy = (run: WorkflowRun): boolean =>
  (run.at ?? null) === null && !run.problem && !run.workflowGate?.holds
  && !(run.graph && (run.graph.unread || (!run.graph.judged && !run.graph.ended)));

/** The step a run's view opens when the person has chosen none: the one it stands at, else the last the work came to. */
export function openedBy(run: WorkflowRun): string | null {
  if (run.at) return run.at;
  return [...run.steps].reverse().find((step) => step.state !== 'not-reached')?.step.id ?? null;
}

/** Whether the outcomes a graph run reached leave no route to this step (the driver's `notTaken`). */
export const notTakenBy = (run: WorkflowRun, step: RunStep): boolean => run.graph?.notTaken?.includes(step.step.id) ?? false;

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
