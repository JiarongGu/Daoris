import type { NotePart } from '../../data/api';
import type { Author } from './sender';

/**
 * The codes a driver's note in a conversation carries, each the key the page words it by: the
 * driver's `SessionEventCodes`, a twin held code for code by `conversation.test.ts`, which parses the driver's declarations.
 * A code not here leaves the driver's English line.
 */
export const CONVERSATION_CODES: Readonly<Record<string, string>> = {
  /** A word the person's stop cut off on its way: it may have been read, its answer not kept. */
  lost: 'work.conversation.lost',
  /** A word the person took back, or reworded, while it still waited: never taken by a turn. */
  withdrawn: 'work.conversation.withdrawn',
  /** Daoris's own word dropped at its delivery: what it said no longer held, so it is never said late. */
  dropped: 'work.conversation.dropped',
};

/** Daoris's event kinds — `SessionEventKind` in the driver. */
export type SessionEventKind =
  | 'user' | 'message' | 'thought' | 'tool' | 'plan' | 'usage' | 'turn' | 'note' | 'raw' | 'receipt' | 'transient';

/** What a tool call carries: text, a diff, or a terminal — ACP's three shapes. */
export type ToolContent = {
  type: string;
  text?: string | null;
  path?: string | null;
  oldText?: string | null;
  newText?: string | null;
};

export type PlanEntry = { content: string; status?: string | null; priority?: string | null };

export type SessionEvent = {
  /** Monotonic within a session, from 1. */
  seq: number;
  at: string;
  kind: SessionEventKind | string;
  /** A tool call's id: its updates carry the same one. */
  id?: string | null;
  text?: string | null;
  /** For `user`: `person`, or `target` for the prompt the driver composed and every word that is not the person's. */
  origin?: string | null;
  /**
   * For `user`: `daoris` on Daoris's own words to the session, a reopen told or a step's go-on. Its origin is
   * `target`, so a page that does not read this shows them as what Daoris handed, never as the person's. Absent on every other.
   */
  from?: string | null;
  /**
   * For `user`: the helper or partner session whose answer these words are. Its origin is `target`,
   * so a page that does not read this shows them as the target Daoris composed. Which of the two it is, is the work of the record it
   * names (`sender.ts`). Absent on every other.
   */
  helper?: string | null;
  /**
   * For the turn Daoris handed a session with another agent's findings: the second opinion they are
   * from. Its origin is `target`, so a page that does not read this shows them as what Daoris handed, never as the person's.
   */
  opinion?: string | null;
  /**
   * For `user`: the ask's channel message these words carry, by its id: another session's
   * claim where the origin is `target`, the person's where it is theirs. Read before the claim's opening, which a record from before
   * is still told by. Absent on every other.
   */
  channel?: string | null;
  /**
   * For `user`: the answer of its park's question the person pressed, as the question offered it: the
   * words begin with this label, then the person's own after a blank line. Drawn as that answer chosen. Absent on every other.
   */
  choice?: string | null;
  /**
   * For `user`: on the brief a new part is handed as its first prompt, the session it
   * continues. Its origin is `target` and it carries its account, as an instruction does. Absent on every other.
   */
  continues?: string | null;
  /**
   * For the person's words to a working driven session: when they reach it, `next-step` or `turn-end`, on
   * the event that shows them the moment they are said. The same words come again, under the same `id` and without
   * this, where the session took them. `resume` is words to a session that parked or ended, which
   * its record keeps until the same session goes on with them. `now` is words *Send now* steered into the running
   * turn: they come again under the same `id` as the ask of the turn that goes on, or a note settles them.
   */
  reaches?: string | null;
  /**
   * For the person's words to a running session: the level they asked for, `ordinary`, `urgent` or `now`, beside
   * `reaches`, what the door gave. A word whose level changes while it waits is told again under the same `id`, and the latest
   * says where it stands. Absent on a record from before.
   */
  level?: string | null;
  /** For the person's words shown the moment they are said: the door they were said at, `screen` or `terminal`. */
  door?: string | null;
  /**
   * For the driver's note about the person's words: the ids of the words it speaks of, so it pairs
   * with where they were shown.
   */
  words?: string[] | null;
  /** For the driver's note that the words went to a new session: that session's id, which the page links. */
  to?: string | null;
  /**
   * For the driver's note about the person's words: why they did not go on in this session, by code.
   * A reason is chrome, so the page says it in its own words from the code; the note's line is the driver's English.
   */
  why?: string | null;
  /**
   * For a driver's note the page words itself: a code of the driver's `SessionEventCodes`, which
   * `CONVERSATION_CODES` keys. Daoris's own line is chrome, said in the reader's language; `text` beside it is the driver's
   * English, which stands for a code this page does not know.
   */
  code?: string | null;
  /**
   * For `receipt`: what the door observed of the word under `id`, `sent` (the write to its
   * harness returned) or `ended` (the turn that took it ended so). Each written only where observed.
   */
  receipt?: string | null;
  /**
   * For a driver's note whose lines are worded by code (a refused sign-in among them): its parts, as a session
   * record's note carries them, which the page words in the reader's language. `text` beside them is the
   * driver's whole English, for the console and a page that does not read parts.
   */
  parts?: NotePart[] | null;
  /** For `user`: the names of the files the person attached. */
  files?: string[] | null;
  title?: string | null;
  toolKind?: string | null;
  status?: string | null;
  locations?: string[] | null;
  /** The line the first of `locations` names, where the wire carried one (ACP's `locations[].line`). */
  line?: number | null;
  content?: ToolContent[] | null;
  input?: string | null;
  output?: string | null;
  entries?: PlanEntry[] | null;
  used?: number | null;
  /** For `transient`: the try going, counted from one; absent on a wait. */
  try?: number | null;
  /** For `transient`: when the wait beginning ends and the next try goes; absent on a try. */
  next?: string | null;
  size?: number | null;
  stopReason?: string | null;
  /** For `turn`: what it consumed, where the wire said. */
  tokens?: TurnTokens | null;
  raw?: string | null;
  /**
   * For the target the driver composed (`origin: 'target'`): what it is handed, section by section.
   * Absent on every other event, and on a target handed before the driver kept one.
   */
  account?: InstructionAccount | null;
};

/**
 * What a session is handed, section by section: the driver's `InstructionAccount`, kept beside its instruction.
 * The sections handed, in the instruction's order, then what is handed beside it, then those not handed, each saying why.
 */
export type InstructionAccount = {
  /** The instruction's length in characters. */
  chars: number;
  sections: HandedSection[];
};

/**
 * One section (the driver's `HandedSection`): codes the page words, and the driver's own English (`said`) for a code it
 * does not know. A count the record does not give is absent, never zero.
 */
export type HandedSection = {
  /** What part it is: a `work.handed.section.*` code. */
  name: string;
  /** Where it came from: a `work.handed.source.*` code. */
  source: string;
  /** The driver's English for it. */
  said: string;
  /** Its characters in the instruction: 0 for one not handed, absent for what is handed beside it (the rules). */
  chars?: number | null;
  /** What its source names: a quest's or an ask's id, a repository's name, a repository-relative file, a branch, a language. */
  from?: string | null;
  /** How many of its items it handed, of how many. */
  shown?: number | null;
  of?: number | null;
  /** When its source is set (a standing answer). */
  at?: string | null;
  /** Why nothing of it is handed: a `work.handed.none.*` code. */
  none?: string | null;
  /** What is left out of it, each with why. */
  cuts?: HandedCut[] | null;
};

/** What a section's bound left out (the driver's `HandedCut`): a `work.handed.cut.*` code and the facts its entry says. */
export type HandedCut = {
  code: string;
  said: string;
  /** How many were left out; the catalogue calls it `n`. */
  count?: number | null;
  limit?: number | null;
  from?: string | null;
  to?: string | null;
};

/**
 * What one turn consumed, as the harness reported it for the whole turn — `TurnTokens` in the
 * driver. A count the wire did not give is absent, never zero.
 */
export type TurnTokens = {
  input?: number | null;
  output?: number | null;
  cacheRead?: number | null;
  cacheWrite?: number | null;
};

/** What `SESSION_HISTORY` answers: a page, oldest first. */
export type EventPage = {
  session: string;
  events: SessionEvent[];
  /** Whether events older than this page exist. */
  earlier: boolean;
  /** The newest sequence the session has. */
  latest: number;
  /** What the session is first asked, where the page does not hold it — a long run reads from it. */
  opening?: SessionEvent | null;
  /** Where the session's first failed call began, wherever it is in the run, or null. */
  firstFailure?: number | null;
};

/**
 * One thing the conversation draws: a message, a thought, a tool call with its latest state, a plan,
 * a note or a raw update. Chunks are joined and a tool call's updates are merged before it gets here.
 */
export type Block = {
  /** Stable across renders: the sequence of the event that began it. */
  key: string;
  /** `held`: the person's words to a working session, waiting to reach it. */
  kind: 'message' | 'thought' | 'tool' | 'plan' | 'note' | 'raw' | 'held';
  at: string;
  text?: string | null;
  id?: string | null;
  /** For `held`: when the words reach the session, `next-step`, `turn-end` or `resume`; `now` is taken already. */
  reaches?: string | null;
  /** For `held`: the level the person asked for, beside the reach the door gave; absent on a record from before. */
  level?: Level;
  /** For `held`: whose words where not the person's ({@link Author}); absent on the person's. */
  from?: Author;
  /** For `held`: the answer of its park's question the words were pressed as; absent on any other. */
  choice?: string;
  /** For `held`: the names of what the person attached. */
  files?: string[];
  /** For `held`: the session ended without taking the words — said, never left waiting for ever. */
  unreached?: boolean;
  /**
   * For `held`: a driver's note below says where the words went, or that they cannot go on here. The
   * words stay as written and wait no longer; the note is their line, said once.
   */
  settled?: boolean;
  /** For a `note` about the person's words: the session they went to, which the page links. */
  to?: string | null;
  /** For a `note` about the person's words: why they did not go on in this session, by code. */
  why?: string | null;
  /** For a `note` about the person's words: the ids it names. */
  words?: string[];
  /** For a `note` the page words itself: its code, a key of `CONVERSATION_CODES`. */
  code?: string | null;
  /** For a `note` worded by its parts: each line's code and values, or someone's words, as its record keeps them. */
  parts?: NotePart[];
  /**
   * For a `note` about the person's words: what they said, in the order said, where the page holds every word
   * the note names — what *Start a conversation with these words* starts with. Absent where it holds only some.
   */
  said?: string[];
  /**
   * For a `note` about the person's words: every word it names is the person's own, shown where they said it, never
   * Daoris's nor another agent's; what *Say it again* offers, with `said`.
   */
  persons?: boolean;
  title?: string | null;
  toolKind?: string | null;
  status?: string | null;
  locations?: string[] | null;
  /** The line the first of `locations` names: replaced with them, never on its own. */
  line?: number | null;
  content?: ToolContent[] | null;
  input?: string | null;
  output?: string | null;
  entries?: PlanEntry[] | null;
  raw?: string | null;
  /**
   * A tool call the stop cut: still open when its turn ended cancelled. Beside the wire's
   * own status, never instead of it — the card opens to what the harness said about it.
   */
  stopped?: boolean;
  /**
   * A call this page holds only the updates of: it began on an earlier page, and an update
   * carries no title. Said so, rather than named by its id.
   */
  continued?: boolean;
  /** When the call finished, by the driver's clock: the update that said it completed or failed. */
  finished?: string;
  /**
   * For the note that is a part's first line where it began by a move or a hand-over: the opening line the runner
   * wrote beside it, folded under it so it never draws as a line of its own.
   */
  under?: Block[];
};

/**
 * What is asked — by the person, or the target the driver composed — and what the person attached. A target carries what
 * it is composed of: its account, or `null` for one handed before the driver kept one.
 */
export type Ask = {
  key: string; text: string; origin: string; at: string; files?: string[]; account?: InstructionAccount | null;
  /** Another agent's findings, handed as the session's next turn: the second opinion they are from. */
  opinion?: string;
  /**
   * Whose words where not the person's, by their mark ({@link Author}): Daoris's own, the
   * supervisor's on the owner's behalf, a helper's or a partner's answer, or an author the page does not know; never the person's,
   * and no target Daoris composed.
   */
  from?: Author;
  /** The ask's channel message these words carry, by its id: another session's claim where the origin is `target`. */
  channel?: string;
  /** The answer of its park's question the person's words were pressed as, drawn as that answer chosen. */
  choice?: string;
  /** For Daoris's brief of the line a new part continues: the session it continues. A target, with its account. */
  continues?: string;
  /**
   * What the door observed of these words, each moment only where observed: `sent` when
   * the write to the harness returned, `taken` the ask's own moment, `ended` when the turn that took them ended (what
   * Daoris saw, never an answer). Absent on words whose door told no receipt, a record from before among them.
   */
  receipts?: Receipts;
  /** For a helper's or a partner's answer: the session it is from, which its exchange card is joined on. */
  helper?: string;
};

/** A word's receipts as its conversation folds them: each the driver's moment of observing it. */
export type Receipts = { sent?: string; taken?: string; ended?: string };

/**
 * A message as the driver tells the page of one it has not sent yet, or handed back: the
 * words, and the names of its files — never where they are kept.
 */
export type ChatMessage = {
  /** What a waiting word is named by, to remove or reword it; absent for one that has none. */
  id?: string;
  text: string;
  files: string[];
  /** The level a waiting word asked for, which its menu changes; absent from a shell before it. */
  level?: Level;
};

/**
 * A message's urgency: when the sender asked it to reach a working agent. `ordinary` at a natural point,
 * never inside a turn in flight; `urgent`, the person's default, at its next step where its door takes words then; `now`
 * cutting its step where its agent steers. A request: the door gives what it can and says so, and no level stops a turn.
 */
export type Level = 'ordinary' | 'urgent' | 'now';

/** A level from the driver's word, read defensively: any other is none. */
export const levelOf = (word: unknown): Level | undefined =>
  (word === 'ordinary' || word === 'urgent' || word === 'now' ? word : undefined);

/**
 * One turn: what is asked, what the agent did about it, and how it ended. A turn with no `ended`
 * is still running — or ended without the wire saying, which reads the same.
 *
 * What it consumed is the harness's report (`tokens`). How long it took is the DRIVER's clock, which
 * stamps every event as it arrives: from the ask to the first thing the agent did (`firstAfter`) and
 * to the turn's end (`took`), in milliseconds — the same measure on both doors, since neither wire
 * reports when the first word came.
 */
export type Turn = {
  key: string;
  ask?: Ask;
  /**
   * The person's words to a session that parked or ended, waiting at its foot for the same session to go on:
   * no run of the agent's, so never *working…* and never a turn the session ended inside.
   */
  waiting?: boolean;
  /** Events between the ask and the items are not held yet: the page began past the ask. */
  gap?: boolean;
  /** The session ended inside this turn, with the wire never saying the turn did. */
  cut?: boolean;
  items: Block[];
  ended?: string;
  tokens?: TurnTokens;
  took?: number;
  firstAfter?: number;
  /**
   * The ask's time, while the agent has done nothing in answer: set on the last turn only, while it is open,
   * not waiting, and holds no message, thought, call or plan. A resumed 700k-token conversation waited 25 minutes with
   * nothing on the page saying so.
   */
  waitingSince?: string;
};
