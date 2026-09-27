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
  /** For `user`: the names of the files the person attached (CONV4c). */
  files?: string[] | null;
  title?: string | null;
  toolKind?: string | null;
  status?: string | null;
  locations?: string[] | null;
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
};

/**
 * One thing the conversation draws: a message, a thought, a tool call with its latest state, a plan,
 * a note or a raw update. Chunks are joined and a tool call's updates are merged before it gets here.
 */
export type Block = {
  /** Stable across renders: the sequence of the event that began it. */
  key: string;
  kind: 'message' | 'thought' | 'tool' | 'plan' | 'note' | 'raw';
  at: string;
  text?: string | null;
  id?: string | null;
  title?: string | null;
  toolKind?: string | null;
  status?: string | null;
  locations?: string[] | null;
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
};

/** What was asked — by the person, or the target the driver composed — and what the person attached. */
export type Ask = { key: string; text: string; origin: string; at: string; files?: string[] };

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
 */
export function toTurns(events: readonly SessionEvent[]): { turns: Turn[]; usage?: Usage } {
  const turns: Turn[] = [];
  let usage: Usage | undefined;
  let current: Turn | null = null;

  const open = (key: string, ask?: Ask): Turn => {
    const turn: Turn = { key, ask, items: [] };
    turns.push(turn);
    current = turn;
    return turn;
  };
  const here = (key: string): Turn => current ?? open(key);

  for (const event of events) {
    const key = `e${event.seq}`;
    switch (event.kind) {
      case 'user':
        open(key, {
          key, text: event.text ?? '', origin: event.origin ?? 'person', at: event.at,
          ...(event.files?.length ? { files: event.files } : {}),
        });
        break;
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
        } else {
          turn.items.push({ key, kind: event.kind, at: event.at, text: event.text ?? '', ...(event.id ? { id: event.id } : {}) });
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
        } else {
          turn.items.push({
            key, kind: 'tool', at: event.at, id: event.id, title: event.title, toolKind: event.toolKind,
            status: event.status, locations: event.locations, content: event.content,
            input: event.input, output: event.output,
          });
        }
        break;
      }
      case 'plan': {
        const turn = here(key);
        const plan = turn.items.find((b) => b.kind === 'plan');
        if (plan) plan.entries = event.entries ?? [];
        else turn.items.push({ key, kind: 'plan', at: event.at, entries: event.entries ?? [] });
        break;
      }
      case 'usage':
        if (typeof event.used === 'number' && typeof event.size === 'number') {
          usage = { used: event.used, size: event.size, most: Math.max(usage?.most ?? 0, event.used) };
        }
        break;
      case 'note':
        here(key).items.push({ key, kind: 'note', at: event.at, text: event.text });
        break;
      default:
        here(key).items.push({ key, kind: 'raw', at: event.at, title: event.title, text: event.text, raw: event.raw });
    }
  }

  // The first thing the agent did, after the ask: the driver's own note is not the agent answering.
  for (const turn of turns) {
    const first = turn.items.find((item) => item.kind !== 'note');
    if (turn.ask && first) turn.firstAfter = since(turn.ask.at, first.at);
  }

  return { turns, usage };
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
