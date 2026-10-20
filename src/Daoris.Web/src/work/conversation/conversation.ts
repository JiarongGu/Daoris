/**
 * A session's conversation, as the driver keeps it (D76, CONV1): typed events in Daoris's own small
 * vocabulary, mapped by each door from its own wire — never parsed out of a console line.
 *
 * @remarks
 * **Machine-local material** (D47 §4). These arrive over the shell's bridge and never over HTTP, so a
 * browser has none, exactly as it has no console.
 *
 * The shapes mirror the driver's `SessionEvent` record, camel-cased by the bridge. Every field but
 * `seq`, `at` and `kind` is optional, because each kind uses a few, and a null the bridge sends is
 * read the same as an absence.
 */

import type { NotePart } from '../../data/api';

/**
 * The codes a driver's note in a conversation carries, each the key the page words it by (MSG1c3, D142 point 1): the
 * driver's `SessionEventCodes`, a twin held code for code by `conversation.test.ts`, which parses the driver's declarations.
 * A code not here leaves the driver's English line.
 */
export const CONVERSATION_CODES: Readonly<Record<string, string>> = {
  /** A word the person's stop cut off on its way (MSG1c, STEER1 §3): it may have been read, its answer not kept. */
  lost: 'work.conversation.lost',
  /** A word the person took back, or reworded, while it still waited (QUEUEEDIT1a): never taken by a turn. */
  withdrawn: 'work.conversation.withdrawn',
  /** Daoris's own word dropped where it was to be delivered (COWORK1b): what it said no longer held, so it was never said late. */
  dropped: 'work.conversation.dropped',
};

/** Daoris's event kinds — `SessionEventKind` in the driver. */
export type SessionEventKind =
  | 'user' | 'message' | 'thought' | 'tool' | 'plan' | 'usage' | 'turn' | 'note' | 'raw' | 'receipt';

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
   * For `user`: `daoris` on Daoris's own words to the session (COWORK1b, D188), a reopen told or a step's go-on. Its origin is
   * `target`, so a page that does not read this shows them as what Daoris handed, never as the person's. Absent on every other.
   */
  from?: string | null;
  /**
   * For the turn Daoris handed a session with another agent's findings (XAGENT1e, D155 point 7): the second opinion they are
   * from. Its origin is `target`, so a page that does not read this shows them as what Daoris handed, never as the person's.
   */
  opinion?: string | null;
  /**
   * For the person's words to a working driven session (STEER1, D136): when they reach it, `next-step` or `turn-end`, on
   * the event that shows them the moment they are said. The same words come again, under the same `id` and without
   * this, where the session took them. `resume` (MSG1d, D137 §3.1) is words to a session that parked or ended, which
   * its record keeps until the same session goes on with them. `now` (STEER2b) is words *Send now* steered into the running
   * turn: they come again under the same `id` as the ask of the turn that goes on, or a note settles them.
   */
  reaches?: string | null;
  /**
   * For the person's words to a running session (COWORK1a): the level they asked for, `ordinary`, `urgent` or `now`, beside
   * `reaches`, what the door gave. A word whose level changes while it waits is told again under the same `id`, and the latest
   * says where it stands. Absent on a record from before.
   */
  level?: string | null;
  /** For the person's words shown the moment they are said (MSG1d): the door they were said at, `screen` or `terminal`. */
  door?: string | null;
  /**
   * For the driver's note about the person's words (MSG1d, D137 §3.1): the ids of the words it speaks of, so it pairs
   * with where they were shown.
   */
  words?: string[] | null;
  /** For the driver's note that the words went to a new session (MSG1d): that session's id, which the page links. */
  to?: string | null;
  /**
   * For the driver's note about the person's words (MSG1d, D137 §5.1): why they did not go on in this session, by code.
   * A reason is chrome, so the page says it in its own words from the code; the note's line is the driver's English.
   */
  why?: string | null;
  /**
   * For a driver's note the page words itself (MSG1c3, D142 point 1): a code of the driver's `SessionEventCodes`, which
   * `CONVERSATION_CODES` keys. Daoris's own line is chrome, said in the reader's language; `text` beside it is the driver's
   * English, which stands for a code this page does not know.
   */
  code?: string | null;
  /**
   * For `receipt` (COWORK1c, the message-urgency design §7): what the door observed of the word under `id`, `sent` (the write to its
   * harness returned) or `answered` (the turn that took it ended so). Each written only where it was observed.
   */
  receipt?: string | null;
  /**
   * For a driver's note whose lines are worded by code (LAND2b, LANG1a; SIGNIN1b's refused sign-in): its parts, as a session
   * record's note carries them, which the page words in the reader's language (CONVNOTE1). `text` beside them is the
   * driver's whole English, for the console and a page that does not read parts.
   */
  parts?: NotePart[] | null;
  /** For `user`: the names of the files the person attached (CONV4c). */
  files?: string[] | null;
  title?: string | null;
  toolKind?: string | null;
  status?: string | null;
  locations?: string[] | null;
  /** The line the first of `locations` names, where the wire carried one (ACP's `locations[].line`, LEFT2). */
  line?: number | null;
  content?: ToolContent[] | null;
  input?: string | null;
  output?: string | null;
  entries?: PlanEntry[] | null;
  used?: number | null;
  size?: number | null;
  stopReason?: string | null;
  /** For `turn`: what it consumed, where the wire said (CONV5). */
  tokens?: TurnTokens | null;
  raw?: string | null;
  /**
   * For the target the driver composed (`origin: 'target'`): what it was handed, section by section (CONTEXT1, D143 point 1).
   * Absent on every other event, and on a target handed before the driver kept one.
   */
  account?: InstructionAccount | null;
};

/**
 * What a session was handed, section by section (CONTEXT1): the driver's `InstructionAccount`, kept beside its instruction.
 * The sections handed, in the instruction's order, then what was handed beside it, then those not handed, each saying why.
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
  /** When its source was set (a standing answer). */
  at?: string | null;
  /** Why nothing of it was handed: a `work.handed.none.*` code. */
  none?: string | null;
  /** What was left out of it, each with why. */
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
 * What one turn consumed, as the harness reported it for the whole turn (CONV5) — `TurnTokens` in the
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
  /** What the session was first asked, where the page does not hold it (SESS1) — a long run reads from it. */
  opening?: SessionEvent | null;
  /** Where the session's first failed call began, wherever it is in the run (SESS1 S9), or null. */
  firstFailure?: number | null;
};

/**
 * One thing the conversation draws: a message, a thought, a tool call with its latest state, a plan,
 * a note or a raw update. Chunks are joined and a tool call's updates are merged before it gets here.
 */
export type Block = {
  /** Stable across renders: the sequence of the event that began it. */
  key: string;
  /** `held`: the person's words to a working session, waiting to reach it (STEER1). */
  kind: 'message' | 'thought' | 'tool' | 'plan' | 'note' | 'raw' | 'held';
  at: string;
  text?: string | null;
  id?: string | null;
  /** For `held`: when the words reach the session, `next-step`, `turn-end` or `resume` (MSG1f); `now` is taken already (STEER2b). */
  reaches?: string | null;
  /** For `held`: the level the person asked for (COWORK1a), beside the reach the door gave; absent on a record from before. */
  level?: Level;
  /** For `held`: Daoris's own words, never the person's (COWORK1b); absent on the person's. */
  from?: 'daoris';
  /** For `held`: the names of what the person attached. */
  files?: string[];
  /** For `held`: the session ended without taking the words (STEER1) — said, never left waiting for ever. */
  unreached?: boolean;
  /**
   * For `held`: a driver's note below says where the words went, or that they cannot go on here (MSG1f, D137 §3.1). The
   * words stay as written and wait no longer; the note is their line, said once.
   */
  settled?: boolean;
  /** For a `note` about the person's words (MSG1f): the session they went to, which the page links. */
  to?: string | null;
  /** For a `note` about the person's words (MSG1f): why they did not go on in this session, by code. */
  why?: string | null;
  /** For a `note` about the person's words (MSG1f): the ids it names. */
  words?: string[];
  /** For a `note` the page words itself (MSG1c3): its code, a key of `CONVERSATION_CODES`. */
  code?: string | null;
  /** For a `note` worded by its parts (CONVNOTE1): each line's code and values, or someone's words, as its record keeps them. */
  parts?: NotePart[];
  /**
   * For a `note` about the person's words (MSG1f): what they said, in the order said, where the page holds every word
   * the note names — what *Start a conversation with these words* starts with. Absent where it holds only some.
   */
  said?: string[];
  /**
   * For a `note` about the person's words (COWORK1c): every word it names was the person's own, shown where they said it, never
   * Daoris's nor another agent's; what *Say it again* offers, with `said`.
   */
  persons?: boolean;
  title?: string | null;
  toolKind?: string | null;
  status?: string | null;
  locations?: string[] | null;
  /** The line the first of `locations` names (LEFT2): replaced with them, never on its own. */
  line?: number | null;
  content?: ToolContent[] | null;
  input?: string | null;
  output?: string | null;
  entries?: PlanEntry[] | null;
  raw?: string | null;
  /**
   * A tool call the stop cut (CONV4b): still open when its turn ended cancelled. Beside the wire's
   * own status, never instead of it — the card opens to what the harness said about it.
   */
  stopped?: boolean;
  /**
   * A call this page holds only the updates of (SESS1 S5): it began on an earlier page, and an update
   * carries no title. Said so, rather than named by its id.
   */
  continued?: boolean;
  /** When the call finished, by the driver's clock: the update that said it completed or failed (SESS1). */
  finished?: string;
};

/**
 * What was asked — by the person, or the target the driver composed — and what the person attached. A target carries what
 * it was composed of (CONTEXT1): its account, or `null` for one handed before the driver kept one.
 */
export type Ask = {
  key: string; text: string; origin: string; at: string; files?: string[]; account?: InstructionAccount | null;
  /** Another agent's findings, handed as the session's next turn (XAGENT1e): the second opinion they are from. */
  opinion?: string;
  /** Daoris's own words (COWORK1b): a reopen told, a step's go-on; never the person's, and no target it composed. */
  from?: 'daoris';
  /**
   * What the door observed of these words (COWORK1c, the message-urgency design §7, §8.2), each moment only where it was: `sent` when
   * the write to the harness returned, `taken` the ask's own moment, `answered` when the turn that took them ended so. Absent on
   * words whose door told no receipt, a record from before among them.
   */
  receipts?: Receipts;
};

/** A word's receipts as its conversation folds them (COWORK1c): each the driver's moment it was observed. */
export type Receipts = { sent?: string; taken?: string; answered?: string };

/**
 * A message as the driver tells the page of one it has not sent yet, or handed back (CONV4a/c): the
 * words, and the names of its files — never where they are kept.
 */
export type ChatMessage = {
  /** What a waiting word is named by, to remove or reword it (QUEUEEDIT1b); absent for one that has none. */
  id?: string;
  text: string;
  files: string[];
  /** The level a waiting word asked for (COWORK1a), which its menu changes; absent from a shell before it. */
  level?: Level;
};

/**
 * A message's urgency (COWORK1a, D188 point 1): when the sender asked it to reach a working agent. `ordinary` at a natural point,
 * never inside a turn in flight; `urgent`, the person's default, at its next step where its door takes words then; `now`
 * cutting its step where its agent steers. A request: the door gives what it can and says so, and no level stops a turn.
 */
export type Level = 'ordinary' | 'urgent' | 'now';

/** A level from the driver's word, read defensively: any other is none. */
export const levelOf = (word: unknown): Level | undefined =>
  (word === 'ordinary' || word === 'urgent' || word === 'now' ? word : undefined);

/**
 * One turn: what was asked, what the agent did about it, and how it ended. A turn with no `ended`
 * is still running — or ended without the wire saying, which reads the same.
 *
 * What it consumed is the harness's report (`tokens`). How long it took is the DRIVER's clock, which
 * stamps every event as it arrives: from the ask to the first thing the agent did (`firstAfter`) and
 * to the turn's end (`took`), in milliseconds — the same measure on both doors, since neither wire
 * reports when the first word came (CONV5).
 */
export type Turn = {
  key: string;
  ask?: Ask;
  /**
   * The person's words to a session that parked or ended, waiting at its foot for the same session to go on (MSG1f, D137
   * §3.1): no run of the agent's, so never *working…* and never a turn the session ended inside.
   */
  waiting?: boolean;
  /** Events between the ask and the items are not held yet (SESS1): the page began past the ask. */
  gap?: boolean;
  /** The session ended inside this turn, with the wire never saying the turn did (SESS1 S4). */
  cut?: boolean;
  items: Block[];
  ended?: string;
  tokens?: TurnTokens;
  took?: number;
  firstAfter?: number;
  /**
   * The ask's time, while the agent has done nothing in answer (WAITSHOWN1): set on the last turn only, while it is open,
   * not waiting, and holds no message, thought, call or plan. A resumed 700k-token conversation waited 25 minutes with
   * nothing on the page saying so.
   */
  waitingSince?: string;
};

/** Context as the harness reported it: the latest reading and the highest (TOOL3's high-water mark). */
export type Usage = { used: number; size: number; most: number };

/**
 * A session's events as the turns a conversation is drawn from (D76, CONV2) — pure, so every state
 * the view can be in is an ordinary assertion.
 *
 * @remarks
 * - **An ask opens a turn**, and the wire's turn end closes it. Anything before the first ask sits in
 *   a turn of its own rather than being dropped.
 * - **Chunks join**: consecutive message chunks are one message, consecutive thought chunks one
 *   thought — the wire streams words, a reader reads sentences. Two messages in a row are two, by
 *   the ids the wire gives them.
 * - **A tool call is one card**, where it first appeared, carrying the latest of every field its
 *   updates set; an update's content replaces what came before, as the protocol says it does.
 * - **A plan is its latest entries**, in the place it first appeared in the turn.
 * - **Usage is a meter, never a block.**
 * - **A cancelled turn's last open calls are the ones its stop cut** (CONV4b): the tool calls at its
 *   end that never completed. Claude Code answers the cut call as failed and another agent leaves it
 *   running; either way the person stopped it, and a failure shown in the alarm's colour would say
 *   otherwise. A call that failed and was followed by more work failed on its own.
 * - **Words told to a working session wait where they were said** (STEER1, D136): an ask that carries
 *   `reaches` is a block in the turn it was said in, never a turn of its own, since the agent is still
 *   on that turn. The same words under the same id are the ask of the turn that took them, and the
 *   waiting block goes.
 * - **Words to a session that parked or ended wait at its foot** (MSG1f, D137 §3.1): `resume` words open
 *   a turn of their own after the run that ended, holding nothing of the agent's, since that run was
 *   over before they were said. A waiting turn the resumed run took every word from, and that holds
 *   nothing else but the driver's notes about them, goes, its notes at the head of the turn that took
 *   its first word (USERSHOWN1): the resume's opening line otherwise left a ghost above every resume.
 * - **A word the door's failure took was never read** (USERSHOWN1): a next-step word taken by the
 *   answer that was the door's failure, its turn holding nothing before the failure, waits where it
 *   was said again. And the harness's error streamed as its agent's words just before (`API Error: …`)
 *   is the failure the note says whole, so it reads as that (TURNFAIL1).
 * - **A driver's note about the words is their line** (MSG1f): a note naming their ids, that they went
 *   to a new session or cannot go on here, settles the words it names, which stay as written and wait
 *   no longer, and carries what they said where the page holds every one. So does the note that a stop
 *   cut a word off on its way (MSG1c3), which carries the code the page words it by. A later note that
 *   they went to another session takes them back from the note that carried them (MSG1f2).
 */
export function toTurns(
  events: readonly SessionEvent[], { opening }: { opening?: SessionEvent | null } = {},
): { turns: Turn[]; usage?: Usage; where: Record<number, string> } {
  const turns: Turn[] = [];
  let usage: Usage | undefined;
  let current: Turn | null = null;
  // Which block each event is part of (SESS1 S9): a jump lands on an event, and shows its block.
  const where: Record<number, string> = {};

  const open = (key: string, ask?: Ask): Turn => {
    const turn: Turn = { key, ask, items: [] };
    turns.push(turn);
    current = turn;
    return turn;
  };
  const here = (key: string): Turn => current ?? open(key);
  // Where words said after the record ended wait (MSG1f): the turn already waiting at its foot, or a new one.
  const foot = (key: string): Turn => (current?.waiting ? current : Object.assign(open(key), { waiting: true }));
  const asked = (event: SessionEvent, key: string): Ask => ({
    key, text: event.text ?? '', origin: event.origin ?? 'person', at: event.at,
    ...(event.files?.length ? { files: event.files } : {}),
    // What a target was composed of (CONTEXT1), or null where its record keeps none: said missing, never left out. Another
    // agent's findings are no composed target (XAGENT1g): they name the opinion they are from instead. Nor are Daoris's own words
    // (COWORK1b), named by their mark, never their sentence.
    ...(event.origin === 'target' && event.from === 'daoris' ? { from: 'daoris' as const }
      : event.origin === 'target' && event.opinion ? { opinion: event.opinion }
        : event.origin === 'target' ? { account: event.account ?? null } : {}),
  });

  // What was asked first, where the page began past it (SESS1 S1): the run reads from its ask, and the
  // gap after it is where the earlier events belong.
  if (opening && !events.some((event) => event.seq === opening.seq)) {
    open(`e${opening.seq}`, asked(opening, `e${opening.seq}`)).gap = true;
    where[opening.seq] = `e${opening.seq}`;
  }

  // The person's words waiting to reach a session (STEER1, MSG1f), by their id: where each was said, its block, and whether they
  // were the person's own (COWORK1c), never Daoris's nor another agent's.
  const waiting = new Map<string, { turn: Turn; key: string; seq: number; block: Block; person?: boolean }>();
  // COWORK1c (the design §7): each word's receipts by its id, and the ask each word became, which they fold under.
  const receipts = new Map<string, Receipts>();
  const asks = new Map<string, Ask>();
  // The note that hands over what each word said (MSG1f), by the word's id: a later note saying the word went elsewhere
  // takes it back (MSG1f2), so no second conversation is offered with words already gone.
  const handing = new Map<string, Block>();
  // The turn that first took words from each waiting turn (USERSHOWN1): where the notes left there go once it holds no words.
  const takers = new Map<Turn, Turn>();
  // A word said at a next step, taken by the event just before this one (USERSHOWN1): the door's failure right after undoes it.
  let taken: { turn: Turn; word: string; seq: number; at: number; was: { turn: Turn; key: string; seq: number; block: Block } } | null = null;

  for (const event of events) {
    const key = `e${event.seq}`;
    const justTaken = taken;
    taken = null;
    switch (event.kind) {
      case 'user': {
        // A waiting word told again under its id (COWORK1a: its level changed while it waited): the latest says where it stands,
        // in its place, and no second copy is drawn.
        const told = event.reaches && event.id ? waiting.get(event.id) : undefined;
        if (told) {
          told.block.reaches = event.reaches;
          const again = levelOf(event.level);
          if (again) told.block.level = again;
          else delete told.block.level;
          where[event.seq] = told.key;
          break;
        }

        if (event.reaches) {
          // After the record ended (MSG1f): at its foot, in the turn already waiting there or a new one, never in the
          // run that ended before the words were said.
          const turn = event.reaches === 'resume' ? foot(key) : here(key);
          const level = levelOf(event.level);
          const block: Block = {
            key, kind: 'held', at: event.at, text: event.text ?? '', id: event.id, reaches: event.reaches,
            ...(level ? { level } : {}),
            ...(event.origin === 'target' && event.from === 'daoris' ? { from: 'daoris' as const } : {}),
            ...(event.files?.length ? { files: event.files } : {}),
          };
          turn.items.push(block);
          // Words sent now (STEER2b, D136) are taken where the steer is answered `injected`: the same words come again under
          // the same id as the ask of the turn that goes on. A refused or unreached steer is settled by its note instead.
          if (event.id) waiting.set(event.id, { turn, key, seq: event.seq, block, person: (event.origin ?? 'person') === 'person' });
          where[event.seq] = key;
          break;
        }

        const opened = open(key, asked(event, key));
        where[event.seq] = key;
        if (event.id && opened.ask) asks.set(event.id, opened.ask);
        // Taken: the words are this turn's ask now, and wait no longer where they were said.
        const was = event.id ? waiting.get(event.id) : undefined;
        if (was) {
          const at = was.turn.items.findIndex((item) => item.key === was.key);
          was.turn.items = was.turn.items.filter((item) => item.key !== was.key);
          where[was.seq] = key;
          waiting.delete(event.id!);
          if (was.block.reaches === 'next-step') taken = { turn: opened, word: event.id!, seq: event.seq, at, was };
          if (was.turn.waiting) {
            if (!takers.has(was.turn)) takers.set(was.turn, opened);
            // A turn that only waited, with nothing left in it but the driver's notes about its words (the resume's opening
            // line, a cool-off's), was the words' alone: its notes go with the turn that took them (USERSHOWN1).
            if (was.turn.items.every((item) => item.kind === 'note')) {
              takers.get(was.turn)!.items.unshift(...was.turn.items);
              turns.splice(turns.indexOf(was.turn), 1);
            }
          }
        }
        break;
      }
      case 'turn': {
        const turn = here(key);
        turn.ended = event.stopReason ?? 'unknown';
        if (event.tokens) turn.tokens = event.tokens;
        if (turn.ask) turn.took = since(turn.ask.at, event.at);
        if (turn.ended === 'cancelled') {
          for (let at = turn.items.length - 1; at >= 0; at -= 1) {
            const item = turn.items[at]!;
            // The calls at the turn's end, run side by side: a finished one among them hides nothing.
            if (item.kind !== 'tool') break;
            if (item.status !== 'completed' && item.status !== 'cut') item.stopped = true;
          }
        }
        current = null;
        break;
      }
      case 'message':
      case 'thought': {
        const turn = here(key);
        const last = turn.items[turn.items.length - 1];
        // Chunks of one message join; a chunk of another message, by the ids the wire gives them,
        // begins anew (found looking at CONSOLE2: "DONESubagent finished"). No id joins, as before.
        const same = last && last.kind === event.kind && (!last.id || !event.id || last.id === event.id);
        if (same) {
          last.text = `${last.text ?? ''}${event.text ?? ''}`;
          last.id ??= event.id;
          where[event.seq] = last.key;
        } else {
          turn.items.push({ key, kind: event.kind, at: event.at, text: event.text ?? '', ...(event.id ? { id: event.id } : {}) });
          where[event.seq] = key;
        }
        break;
      }
      case 'tool': {
        const turn = here(key);
        let card = event.id ? turn.items.find((b) => b.kind === 'tool' && b.id === event.id) : undefined;
        // The close of a call a steer cut (STEER2b, D136) is written when the turn's prompt is answered, after the steered
        // words were taken as the ask of the turn that goes on: so it finds its card by id in an earlier turn and closes it
        // there. Only `cut` does; any other late update keeps showing as a continued card, as it always has.
        if (!card && event.id && event.status === 'cut') {
          for (let at = turns.length - 1; at >= 0 && !card; at -= 1) {
            card = turns[at]!.items.find((b) => b.kind === 'tool' && b.id === event.id);
          }
        }
        if (card) {
          for (const field of ['title', 'toolKind', 'status', 'locations', 'content', 'input', 'output'] as const) {
            if (event[field] != null) (card as Record<string, unknown>)[field] = event[field];
          }
          // A line is said of the locations it came with (LEFT2): ACP replaces a present list whole, and a line
          // kept past its list would mark a place in a file nobody named it of.
          if (event.locations != null) card.line = event.line ?? null;
          if (event.status === 'completed' || event.status === 'failed' || event.status === 'cut') card.finished = event.at;
          where[event.seq] = card.key;
        } else {
          where[event.seq] = key;
          turn.items.push({
            key, kind: 'tool', at: event.at, id: event.id, title: event.title, toolKind: event.toolKind,
            status: event.status, locations: event.locations, line: event.line, content: event.content,
            input: event.input, output: event.output,
            // A call's first event names it; one with no name is an update to a call begun earlier.
            ...(event.title ? {} : { continued: true }),
            ...(event.status === 'completed' || event.status === 'failed' ? { finished: event.at } : {}),
          });
        }
        break;
      }
      case 'plan': {
        const turn = here(key);
        const plan = turn.items.find((b) => b.kind === 'plan');
        if (plan) plan.entries = event.entries ?? [];
        else turn.items.push({ key, kind: 'plan', at: event.at, entries: event.entries ?? [] });
        where[event.seq] = plan?.key ?? key;
        break;
      }
      case 'usage':
        if (typeof event.used === 'number' && typeof event.size === 'number') {
          usage = { used: event.used, size: event.size, most: Math.max(usage?.most ?? 0, event.used) };
        }
        break;
      case 'note': {
        if (doorFailed(event)) {
          // 🔴 USERSHOWN1: a word taken at a next step by the very answer that was the door's failure was never read. The
          // install's record kept "stuck?" as the ask of a turn holding only the failure; it waits where it was said again,
          // never reached once the session is over.
          if (justTaken && current === justTaken.turn && justTaken.turn.items.length === 0) {
            turns.splice(turns.indexOf(justTaken.turn), 1);
            const { was } = justTaken;
            was.turn.items.splice(Math.max(0, justTaken.at), 0, was.block);
            waiting.set(justTaken.word, was);
            where[was.seq] = was.key;
            where[justTaken.seq] = was.key;
            current = was.turn;
          }
          // The harness's own error, streamed as its agent's words just before its door failed (`API Error: …`), is the
          // failure the note says whole: it reads as that, never as the agent answering (TURNFAIL1).
          const turn = here(key);
          const said = lastAgent(turn);
          if (said?.kind === 'message' && said.text?.trim() && (event.text ?? '').includes(said.text.trim())) {
            turn.items = turn.items.filter((item) => item !== said);
            for (const [seq, block] of Object.entries(where)) if (block === said.key) where[Number(seq)] = key;
          }
        }
        // A note about the person's words (MSG1f) names them: they went elsewhere or cannot go on here, so they wait no
        // longer, and the note carries what they said where every one of them is held.
        const named = event.words ?? [];
        const held = named.map((id) => waiting.get(id));
        for (const [index, was] of held.entries()) {
          if (!was) continue;
          was.block.settled = true;
          waiting.delete(named[index]!);
        }
        const said = named.length > 0 && held.every(Boolean) ? held.map((was) => was!.block.text ?? '') : undefined;
        // COWORK1c: every word it names the person's own, so the page may offer to say them again (*Say it again*).
        const persons = said !== undefined && held.every((was) => was!.person === true);
        if (event.to) {
          for (const id of named) {
            const handed = handing.get(id);
            if (handed) delete handed.said;
            handing.delete(id);
          }
        }
        // A note about one call carries its id (SESS1 S6), so it folds with that call's run.
        const note: Block = {
          key, kind: 'note', at: event.at, text: event.text, ...(event.id ? { id: event.id } : {}),
          ...(event.to ? { to: event.to } : {}), ...(event.why ? { why: event.why } : {}),
          ...(named.length > 0 ? { words: named } : {}), ...(said ? { said } : {}), ...(persons ? { persons } : {}),
          ...(event.code ? { code: event.code } : {}), ...(event.parts?.length ? { parts: event.parts } : {}),
        };
        if (said) for (const id of named) handing.set(id, note);
        here(key).items.push(note);
        where[event.seq] = key;
        break;
      }
      case 'receipt': {
        // COWORK1c (the design §7, §8.2): no block of its own; it folds under the ask its word became, below.
        if (event.id && (event.receipt === 'sent' || event.receipt === 'answered')) {
          receipts.set(event.id, { ...receipts.get(event.id), [event.receipt]: event.at });
        }
        break;
      }
      default:
        here(key).items.push({ key, kind: 'raw', at: event.at, title: event.title, text: event.text, raw: event.raw });
        where[event.seq] = key;
    }
  }

  // Each word's receipts under the ask it became, taken at the ask's own moment (COWORK1c): only where a receipt was observed.
  for (const [id, ask] of asks) {
    const seen = receipts.get(id);
    if (seen) ask.receipts = { ...seen, taken: ask.at };
  }

  // The first thing the agent did, after the ask: the driver's own note is not the agent answering, nor are the person's
  // words waiting to reach it.
  for (const turn of turns) {
    const first = turn.items.find((item) => item.kind !== 'note' && item.kind !== 'held');
    if (turn.ask && first) turn.firstAfter = since(turn.ask.at, first.at);
  }

  // The wait for the first answer (WAITSHOWN1): the last turn, still open, asked and with nothing of the agent's yet.
  const tail = turns[turns.length - 1];
  if (tail?.ask && !tail.ended && !tail.waiting && !tail.items.some(AGENT)) tail.waitingSince = tail.ask.at;

  return { turns, usage, where };
}

/** What counts as the agent answering (WAITSHOWN1): its words, its thinking, its calls and its plan. */
const AGENT = (item: Block) => item.kind === 'message' || item.kind === 'thought' || item.kind === 'tool' || item.kind === 'plan';

/** The last thing the agent did in a turn, past the driver's notes and the person's waiting words; undefined for none. */
const lastAgent = (turn: Turn): Block | undefined => [...turn.items].reverse().find(AGENT);

/**
 * The protocol door's failure (`door.failed`, ACPEND1): by its code, or by the driver's English on a record kept before its
 * parts.
 */
const doorFailed = (event: SessionEvent): boolean =>
  event.parts?.[0]?.code === 'door.failed' || (!event.parts?.length && (event.text ?? '').startsWith('— the ACP session failed:'));

/** When the turn the agent is on was asked, while it has not answered yet (WAITSHOWN1); undefined once it has, or when none is open. */
export const firstAnswerWait = (turns: readonly Turn[]): string | undefined => turns[turns.length - 1]?.waitingSince;

/** A call the agent is inside: its title, and when it began by the driver's clock (WAITSAY1). */
export type RunningCall = { title: string; since: string };

/**
 * The call the agent is inside, in the last turn while it is open (WAITSAY1): the last tool block that neither finished nor was
 * cut, pending or in progress. Read after `settle`, so a session that is not live says none — its open calls read stopped.
 * A call this page holds only updates of has no title, and is not named.
 */
export function runningCall(turns: readonly Turn[]): RunningCall | null {
  const turn = turns[turns.length - 1];
  if (!turn || turn.ended || turn.cut) return null;
  for (let at = turn.items.length - 1; at >= 0; at -= 1) {
    const item = turn.items[at]!;
    if (item.kind !== 'tool' || item.finished || item.stopped) continue;
    // WAITSAY1b: a call the rules refused (D52) ended too; it waits on nobody, on either door.
    if (item.status === 'completed' || item.status === 'failed' || item.status === 'cut' || item.status === 'refused') continue;
    if (item.title) return { title: item.title, since: item.at };
  }
  return null;
}

/**
 * The key of the last thing the agent said, where it said anything: the jump to its last words (SESS1 S9), and what a parked
 * session asked the person, since its card keeps that message whole (PARK1; WAITFOOT1c).
 */
export function lastWords(turns: readonly Turn[]): string | undefined {
  for (let turn = turns.length - 1; turn >= 0; turn -= 1) {
    const items = turns[turn]!.items;
    for (let item = items.length - 1; item >= 0; item -= 1) {
      if (items[item]!.kind === 'message') return items[item]!.key;
    }
  }
  return undefined;
}

/**
 * The turns as a session that has ended leaves them (SESS1 S4): the last one, if the wire never said
 * it ended, was cut — and the calls it left open never finished, so they read *stopped*, never
 * *running* for good. The person's words still waiting never reached it (STEER1). A live session is
 * left as it is: its last turn may simply be going on.
 *
 * Words said after it ended (MSG1f, `resume`) wait for it to go on, so its end is not theirs, and the
 * turn they wait in at its foot is no run: the last run before it is the one judged.
 */
export function settle(turns: Turn[], live: boolean): Turn[] {
  if (live) return turns;
  const missed = (item: Block) => item.kind === 'held' && item.reaches !== 'resume' && !item.settled;
  const unreached = (turn: Turn): Turn => (turn.items.some(missed)
    ? { ...turn, items: turn.items.map((item) => (missed(item) ? { ...item, unreached: true } : item)) }
    : turn);
  const settled = turns.some((turn) => turn.items.some(missed)) ? turns.map(unreached) : turns;
  let at = settled.length - 1;
  while (at >= 0 && settled[at]!.waiting) at -= 1;
  const last = settled[at];
  if (!last || last.ended) return settled;
  const cut: Turn = {
    ...last,
    cut: true,
    waitingSince: undefined,
    items: last.items.map((item) => (item.kind === 'tool' && item.status !== 'completed' && item.status !== 'failed' && item.status !== 'cut'
      ?{ ...item, stopped: true }
      : item)),
  };
  return [...settled.slice(0, at), cut, ...settled.slice(at + 1)];
}

/** What a turn draws, in order: a block on its own, or a run of work folded to a line that counts it. */
export type Segment =
  | { kind: 'block'; block: Block }
  | { kind: 'run'; key: string; items: Block[]; open: boolean };

/**
 * The work a run folds: calls, the agent's thinking, and the driver's note about one call (its id says
 * which). The agent's words, its plan and the driver's notes about the session stand alone.
 */
const WORK = new Set(['tool', 'thought', 'raw']);
const isWork = (item: Block) => WORK.has(item.kind) || (item.kind === 'note' && Boolean(item.id));

/**
 * A turn's items as a reader reads them (SESS1 S3): every word the agent said stays in view, and the
 * work between two of them folds into one run that counts it. A run of one is shown as itself, since
 * folding it saves nothing; the last run stays open while the turn goes on (`tailOpen`), so the work in
 * hand is in view.
 *
 * @remarks
 * This replaces folding a finished turn whole. A driven session is one turn — one ask, then the run —
 * so the whole-turn fold hid the agent's narration of hundreds of calls behind one row, and a run that
 * never ended folded nothing at all (the first real workspace, 2026-09-28).
 */
export function segments(items: readonly Block[], tailOpen: boolean): Segment[] {
  const parts: Segment[] = [];
  let run: Block[] = [];
  const close = () => {
    if (run.length === 1) parts.push({ kind: 'block', block: run[0]! });
    else if (run.length > 1) parts.push({ kind: 'run', key: `run-${run[0]!.key}`, items: run, open: false });
    run = [];
  };

  for (const item of items) {
    if (isWork(item)) run.push(item);
    else {
      close();
      parts.push({ kind: 'block', block: item });
    }
  }
  close();

  const last = parts[parts.length - 1];
  if (tailOpen && last?.kind === 'run') parts[parts.length - 1] = { ...last, open: true };
  return parts;
}

/** What a folded run holds, for the line that stands for it: its calls, how many failed, whether it thought. */
export function runCount(items: readonly Block[]): { tools: number; failed: number; thought: boolean } {
  const tools = items.filter((item) => item.kind === 'tool');
  return {
    tools: tools.length,
    // A call the stop or the session's end cut is not a failure (CONV4b), whatever the wire answered.
    failed: tools.filter((item) => item.status === 'failed' && !item.stopped).length,
    thought: items.some((item) => item.kind === 'thought'),
  };
}

/** Milliseconds from one stamp to a later one, or undefined when either is not a time. */
function since(from: string, to: string): number | undefined {
  const span = Date.parse(to) - Date.parse(from);
  return Number.isFinite(span) && span >= 0 ? span : undefined;
}

/**
 * Two runs of events as one, in sequence order, each sequence once — history and live batches
 * overlap at their seam, and whichever arrived first, the record says the same thing.
 */
export function mergeEvents(held: readonly SessionEvent[], incoming: readonly SessionEvent[]): SessionEvent[] {
  if (incoming.length === 0) return held as SessionEvent[];
  const bySeq = new Map<number, SessionEvent>();
  for (const e of held) bySeq.set(e.seq, e);
  for (const e of incoming) if (!bySeq.has(e.seq)) bySeq.set(e.seq, e);
  return [...bySeq.values()].sort((a, b) => a.seq - b.seq);
}
