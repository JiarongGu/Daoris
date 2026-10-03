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

/**
 * The codes a driver's note in a conversation carries, each the key the page words it by (MSG1c3, D142 point 1): the
 * driver's `SessionEventCodes`, a twin held code for code by `conversation.test.ts`, which parses the driver's declarations.
 * A code not here leaves the driver's English line.
 */
export const CONVERSATION_CODES: Readonly<Record<string, string>> = {
  /** A word the person's stop cut off on its way (MSG1c, STEER1 §3): it may have been read, its answer not kept. */
  lost: 'work.conversation.lost',
};

/** Daoris's event kinds — `SessionEventKind` in the driver. */
export type SessionEventKind =
  | 'user' | 'message' | 'thought' | 'tool' | 'plan' | 'usage' | 'turn' | 'note' | 'raw';

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
  /** For `user`: `person`, or `target` for the prompt the driver composed. */
  origin?: string | null;
  /**
   * For the person's words to a working driven session (STEER1, D136): when they reach it, `next-step` or `turn-end`, on
   * the event that shows them the moment they are said. The same words come again, under the same `id` and without
   * this, where the session took them. `resume` (MSG1d, D137 §3.1) is words to a session that parked or ended, which
   * its record keeps until the same session goes on with them.
   */
  reaches?: string | null;
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
  /** For `held`: when the words reach the session, `next-step`, `turn-end` or `resume` (MSG1f). */
  reaches?: string | null;
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
  /**
   * For a `note` about the person's words (MSG1f): what they said, in the order said, where the page holds every word
   * the note names — what *Start a conversation with these words* starts with. Absent where it holds only some.
   */
  said?: string[];
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
};

/**
 * A message as the driver tells the page of one it has not sent yet, or handed back (CONV4a/c): the
 * words, and the names of its files — never where they are kept.
 */
export type ChatMessage = { text: string; files: string[] };

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
 *   nothing else, goes.
 * - **A driver's note about the words is their line** (MSG1f): a note naming their ids, that they went
 *   to a new session or cannot go on here, settles the words it names, which stay as written and wait
 *   no longer, and carries what they said where the page holds every one. So does the note that a stop
 *   cut a word off on its way (MSG1c3), which carries the code the page words it by.
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
    // What a target was composed of (CONTEXT1), or null where its record keeps none: said missing, never left out.
    ...(event.origin === 'target' ? { account: event.account ?? null } : {}),
  });

  // What was asked first, where the page began past it (SESS1 S1): the run reads from its ask, and the
  // gap after it is where the earlier events belong.
  if (opening && !events.some((event) => event.seq === opening.seq)) {
    open(`e${opening.seq}`, asked(opening, `e${opening.seq}`)).gap = true;
    where[opening.seq] = `e${opening.seq}`;
  }

  // The person's words waiting to reach a session (STEER1, MSG1f), by their id: where each was said, and its block.
  const waiting = new Map<string, { turn: Turn; key: string; seq: number; block: Block }>();

  for (const event of events) {
    const key = `e${event.seq}`;
    switch (event.kind) {
      case 'user': {
        if (event.reaches) {
          // After the record ended (MSG1f): at its foot, in the turn already waiting there or a new one, never in the
          // run that ended before the words were said.
          const turn = event.reaches === 'resume' ? foot(key) : here(key);
          const block: Block = {
            key, kind: 'held', at: event.at, text: event.text ?? '', id: event.id, reaches: event.reaches,
            ...(event.files?.length ? { files: event.files } : {}),
          };
          turn.items.push(block);
          if (event.id) waiting.set(event.id, { turn, key, seq: event.seq, block });
          where[event.seq] = key;
          break;
        }

        open(key, asked(event, key));
        where[event.seq] = key;
        // Taken: the words are this turn's ask now, and wait no longer where they were said.
        const was = event.id ? waiting.get(event.id) : undefined;
        if (was) {
          was.turn.items = was.turn.items.filter((item) => item.key !== was.key);
          where[was.seq] = key;
          waiting.delete(event.id!);
          // A turn that only waited, with nothing left in it, was the words' alone.
          if (was.turn.waiting && was.turn.items.length === 0) turns.splice(turns.indexOf(was.turn), 1);
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
            if (item.status !== 'completed') item.stopped = true;
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
        const card = event.id ? turn.items.find((b) => b.kind === 'tool' && b.id === event.id) : undefined;
        if (card) {
          for (const field of ['title', 'toolKind', 'status', 'locations', 'content', 'input', 'output'] as const) {
            if (event[field] != null) (card as Record<string, unknown>)[field] = event[field];
          }
          // A line is said of the locations it came with (LEFT2): ACP replaces a present list whole, and a line
          // kept past its list would mark a place in a file nobody named it of.
          if (event.locations != null) card.line = event.line ?? null;
          if (event.status === 'completed' || event.status === 'failed') card.finished = event.at;
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
        // A note about one call carries its id (SESS1 S6), so it folds with that call's run.
        here(key).items.push({
          key, kind: 'note', at: event.at, text: event.text, ...(event.id ? { id: event.id } : {}),
          ...(event.to ? { to: event.to } : {}), ...(event.why ? { why: event.why } : {}),
          ...(named.length > 0 ? { words: named } : {}), ...(said ? { said } : {}),
          ...(event.code ? { code: event.code } : {}),
        });
        where[event.seq] = key;
        break;
      }
      default:
        here(key).items.push({ key, kind: 'raw', at: event.at, title: event.title, text: event.text, raw: event.raw });
        where[event.seq] = key;
    }
  }

  // The first thing the agent did, after the ask: the driver's own note is not the agent answering, nor are the person's
  // words waiting to reach it.
  for (const turn of turns) {
    const first = turn.items.find((item) => item.kind !== 'note' && item.kind !== 'held');
    if (turn.ask && first) turn.firstAfter = since(turn.ask.at, first.at);
  }

  return { turns, usage, where };
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
    items: last.items.map((item) => (item.kind === 'tool' && item.status !== 'completed' && item.status !== 'failed'
      ? { ...item, stopped: true }
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
