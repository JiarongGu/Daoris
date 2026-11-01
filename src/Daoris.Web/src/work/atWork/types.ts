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

/** What a part waits on as the strip places it (COWORKVIEW1b-i): a quest, with the repository it was asked of and its lanes, or its helper. */
export type AtWorkWait = { quest?: string; of?: string; lanes?: string[]; helper?: true };

/** How a part joined the entry (the design §3.4): the strip's word for its start and what its record and quest say of the routing. */
export type AtWorkJoined = {
  began: string;
  /** The strip's number of the part it follows. */
  after?: number;
  quest?: string;
  lanes?: string[];
  /** The strip's number of the part that published its quest. */
  byPart?: number;
  askedBy?: string;
  ask?: string;
  /** The quest whose close published its quest: it is that chain's next step. */
  parent?: string;
  took?: true;
  /** The repository it ran in. */
  of?: string;
};

export type AtWorkPart = {
  key: string;
  agent?: string;
  /** The record's own state, never the strip's shown word. */
  state?: string;
  children: { count?: number; working?: number; observed?: number; unplaced?: number; unreadable?: boolean; elsewhere?: boolean };
  /** Its place in the strip, from one. Absent from a host before the routing half. */
  number?: number;
  waits?: AtWorkWait;
  joined?: AtWorkJoined;
  /** It runs in a tree no earlier part of the entry worked in. */
  alongside?: true;
};

/** Who took a quest (COWORKVIEW1b-ii): a session here, a chat through its connector, another machine, no session recorded here, or nobody yet. */
export type AtWorkTaker = {
  session?: string;
  /** The taking session's agent and its record's own state. */
  agent?: string;
  state?: string;
  conversation?: string;
  elsewhere?: true;
  unrecorded?: true;
  untaken?: true;
};

/** A quest a part of the entry published that opened no part of it, or a step of its chain (the cowork view design §3.1). */
export type AtWorkQuest = {
  id: string;
  title: string;
  /** The service's status: Open, Taken, Done or Declined. */
  status: string;
  /** The repository it was sent to, where that is not the attended part's own. */
  of?: string;
  lanes?: string[];
  /** The quest whose close published this one: the chain's step before it. */
  step?: string;
  waits?: AtWorkWait;
  /** Absent for a quest declined that nobody took. */
  taker?: AtWorkTaker;
};

/** What asked the attended part's quest: the publishing session, the step before, the ask, or the person. */
export type AtWorkAsker = {
  quest: string;
  by: { session?: string; step?: string; ask?: string; person?: true };
};

/** The routing standing the hint says: the driver's judge, in its own words. */
export type AtWorkHint = 'not-driven' | 'no-lanes' | 'one-at-a-time';

export type AtWorkA = {
  of: { session: string };
  attended: string;
  parts: AtWorkPart[];
  moreParts?: Record<string, number>;
  /** Ended parts that wait on nothing, past the newest three, which the list leaves out. */
  earlier?: number;
  /** How many quests the entry's parts published. */
  published?: number;
  /** The quests the entry's parts asked that opened no part of it, a chain's steps after each; absent from a host before the quests half. */
  quests?: AtWorkQuest[];
  /** How many more the bound left out. */
  moreQuests?: number;
  askedBy?: AtWorkAsker;
  /** Where its newest generation started two subagents and the repository's routing standing is not open (the design §6.4 point 3). */
  hint?: AtWorkHint;
  subagents: AtWorkSubagents;
  /** When the shell's process began; absent from an older host. */
  windowOpened?: string;
};

/** The delivery's number: the shell's epoch and its landing's, kept in the query's own data. */
export type AtWorkAt = { epoch: string; n: number };

/** What the query holds: reading, failed with its words, or the answer, stale where a refresh failed over it. */
export type AtWorkHeld =
  | { reading: true; at?: AtWorkAt }
  | { failed: { why: string }; at?: AtWorkAt }
  | { answer: AtWorkA; stale?: { why: string }; at?: AtWorkAt };
