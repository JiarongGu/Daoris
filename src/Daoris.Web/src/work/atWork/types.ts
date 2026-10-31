// Who is at work on a session (COWORKVIEW1a, the cowork view design §3.8): what the shell's read answers and the view draws.

/** A child's state: its latest run's, as the driver's fold read it. */
export type AtWorkChildState =
  | 'working' | 'unsaid' | 'completed' | 'failed' | 'cancelled' | 'disconnected' | 'stopped' | 'finished' | 'ended' | 'lost'
  | 'unrecorded' | 'uncertain' | 'agentEnded';

export const AT_WORK_CHILD_STATES: readonly AtWorkChildState[] = [
  'working', 'unsaid', 'completed', 'failed', 'cancelled', 'disconnected', 'stopped', 'finished', 'ended', 'lost', 'unrecorded',
  'uncertain', 'agentEnded',
];

export type AtWorkChild = {
  /** Its native id. */
  key: string;
  type?: string;
  /** The harness's announcement: the agent's words. */
  label?: { text: string; claim: true };
  /** Absent with `depth` where no harness said: *parent not said*. */
  parentKey?: string;
  depth?: number;
  state: AtWorkChildState;
  /** Its run's start, or its first line where none was said (`startSaid` false): *first seen*. */
  since?: string;
  startSaid: boolean;
  /** The moment its end was said. */
  ended?: string;
  /** Its last line, for a run read not known, unrecorded or whose agent ended. */
  lastSeen?: string;
};

export type AtWorkSubagents = {
  read: 'read' | 'none' | 'unreadable' | 'elsewhere';
  unreadable?: string;
  harness?: string;
  /** A harness whose children appear only when they call Daoris (Codex). */
  seenByCalls?: boolean;
  working: number;
  turnWaits?: boolean;
  /** Agent ids named that no announcement matched: never children. */
  observed?: number;
  /** Agent ids named by lines tied to no process: never children. */
  unplaced?: number;
  children: AtWorkChild[];
  folded?: number;
  more?: Record<string, number>;
};

export type AtWorkPart = {
  key: string;
  agent?: string;
  /** The record's own state, never the strip's shown word. */
  state?: string;
  children: { count?: number; working?: number; observed?: number; unplaced?: number; unreadable?: boolean; elsewhere?: boolean };
};

export type AtWorkA = {
  of: { session: string };
  attended: string;
  parts: AtWorkPart[];
  moreParts?: Record<string, number>;
  subagents: AtWorkSubagents;
};

/** The delivery's number: the shell's epoch and its landing's, kept in the query's own data. */
export type AtWorkAt = { epoch: string; n: number };

/** What the query holds: reading, failed with its words, or the answer, stale where a refresh failed over it. */
export type AtWorkHeld =
  | { reading: true; at?: AtWorkAt }
  | { failed: { why: string }; at?: AtWorkAt }
  | { answer: AtWorkA; stale?: { why: string }; at?: AtWorkAt };
