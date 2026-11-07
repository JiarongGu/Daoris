/**
 * A session's conversation, as the driver keeps it: typed events in Daoris's own small
 * vocabulary, mapped by each door from its own wire — never parsed out of a console line.
 *
 * @remarks
 * **Machine-local material**. These arrive over the shell's bridge and never over HTTP, so a
 * browser has none, exactly as it has no console.
 *
 * The shapes mirror the driver's `SessionEvent` record, camel-cased by the bridge. Every field but
 * `seq`, `at` and `kind` is optional, because each kind uses a few, and a null the bridge sends is
 * read the same as an absence.
 */

import { authorOf } from './sender';
import { type Ask, type Block, levelOf, type Receipts, type SessionEvent, type Turn } from './conversationTypes';

export { CONVERSATION_CODES, levelOf } from './conversationTypes';
export type {
  Ask, Block, ChatMessage, EventPage, HandedCut, HandedSection, InstructionAccount, Level, PlanEntry, Receipts, SessionEvent,
  SessionEventKind, ToolContent, Turn, TurnTokens,
} from './conversationTypes';

/** A transient episode's one line, by its code. */
const transientLine = (event: SessionEvent): boolean => event.kind === 'note' && event.parts?.[0]?.code === 'agent.transient-retrying';

/**
 * The message blocks a transient episode's failed attempts streamed, by key: each followed,
 * past its own chunks and the meters, by the episode's line, by a try or a wait, or by the driver's note at its end, never by an
 * answer; none before the episode's first failure, which its line follows.
 */
const failedAttempts = (events: readonly SessionEvent[], where: Record<number, string>): Set<string> => {
  const failed = new Set<string>();
  const lines = events.filter(transientLine).map((event) => event.seq);
  if (lines.length === 0) return failed;
  const first = Math.min(...lines);
  let block: string | undefined;
  let last = 0;
  for (const event of events) {
    if (event.kind === 'message') {
      block = where[event.seq];
      last = event.seq;
      continue;
    }
    if (event.kind === 'usage' || event.kind === 'receipt') continue;
    if (block !== undefined && (transientLine(event) || (last > first && (event.kind === 'transient' || event.kind === 'note')))) {
      failed.add(block);
    }
    block = undefined;
  }
  return failed;
};

/** Context as the harness reported it: the latest reading and the highest. */
export type Usage = { used: number; size: number; most: number };

/**
 * A session's events as the turns a conversation is drawn from — pure, so every state
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
 * - **A cancelled turn's last open calls are the ones its stop cut**: the tool calls at its
 *   end that never completed. Claude Code answers the cut call as failed and another agent leaves it
 *   running; either way the person stopped it, and a failure shown in the alarm's colour would say
 *   otherwise. A call that failed and is followed by more work failed on its own.
 * - **Words told to a working session wait where they are said**: an ask that carries
 *   `reaches` is a block in the turn it is said in, never a turn of its own, since the agent is still
 *   on that turn. The same words under the same id are the ask of the turn that took them, and the
 *   waiting block goes.
 * - **Words to a session that parked or ended wait at its foot**: `resume` words open
 *   a turn of their own after the run that ended, holding nothing of the agent's, since that run is
 *   over before they are said. A waiting turn the resumed run took every word from, and that holds
 *   nothing else but the driver's notes about them, goes, its notes at the head of the turn that took
 *   its first word: the resume's opening line would otherwise leave a ghost above every resume.
 * - **A word the door's failure took is never read**: a next-step word taken by the
 *   answer that is the door's failure, its turn holding nothing before the failure, waits where it
 *   is said again. And the harness's error streamed as its agent's words just before (`API Error: …`)
 *   is the failure the note says whole, so it reads as that.
 * - **A driver's note about the words is their line**: a note naming their ids, that they went
 *   to a new session or cannot go on here, settles the words it names, which stay as written and wait
 *   no longer, and carries what they said where the page holds every one. So does the note that a stop
 *   cut a word off on its way, which carries the code the page words it by. A later note that
 *   they went to another session takes them back from the note that carried them.
 */
export function toTurns(
  events: readonly SessionEvent[],
  { opening, workOf }: { opening?: SessionEvent | null; workOf?: (session: string) => string | null | undefined } = {},
): { turns: Turn[]; usage?: Usage; where: Record<number, string> } {
  // The work of the record a helper's word names: what tells a partner's answer from a helper's.
  const helperWork = (event: SessionEvent) => (event.helper && workOf ? workOf(event.helper) : null);
  const turns: Turn[] = [];
  let usage: Usage | undefined;
  let current: Turn | null = null;
  // Which block each event is part of: a jump lands on an event, and shows its block.
  const where: Record<number, string> = {};
  // The provider's words each transient episode's line quotes, whose repeats as the agent's words read as that line.
  const transient: string[] = [];

  const open = (key: string, ask?: Ask): Turn => {
    const turn: Turn = { key, ask, items: [] };
    turns.push(turn);
    current = turn;
    return turn;
  };
  const here = (key: string): Turn => current ?? open(key);
  // Where words said after the record ended wait: the turn already waiting at its foot, or a new one.
  const foot = (key: string): Turn => (current?.waiting ? current : Object.assign(open(key), { waiting: true }));
  const asked = (event: SessionEvent, key: string): Ask => ({
    key, text: event.text ?? '', origin: event.origin ?? 'person', at: event.at,
    ...(event.files?.length ? { files: event.files } : {}),
    // A channel's message by its own mark, never by Daoris's words around it.
    ...(event.channel ? { channel: event.channel } : {}),
    // An answer of its park's question pressed, by its mark, never read out of the words.
    ...(event.choice && (event.origin ?? 'person') === 'person' ? { choice: event.choice } : {}),
    ...(event.origin === 'target' && event.continues ? { continues: event.continues } : {}),
    ...(event.origin === 'target' && event.helper ? { helper: event.helper } : {}),
    // What a target is composed of, or null where its record keeps none: said missing, never left out. Another
    // agent's findings are no composed target: they name the opinion they are from instead. Nor are Daoris's own words,
    // named by their mark, never their sentence.
    ...(authorOf(event, helperWork(event)) ? { from: authorOf(event, helperWork(event)) }
      : event.origin === 'target' && event.opinion ? { opinion: event.opinion }
        : event.origin === 'target' ? { account: event.account ?? null } : {}),
  });

  // Where the page began past the ask, the ask comes first: the run reads from its ask, and the
  // gap after it is where the earlier events belong.
  if (opening && !events.some((event) => event.seq === opening.seq)) {
    open(`e${opening.seq}`, asked(opening, `e${opening.seq}`)).gap = true;
    where[opening.seq] = `e${opening.seq}`;
  }

  // The person's words waiting to reach a session, by their id: where each is said, its block, and whether they
  // are the person's own, never Daoris's nor another agent's.
  const waiting = new Map<string, { turn: Turn; key: string; seq: number; block: Block; person?: boolean }>();
  // Each word's receipts by its id, and the ask each word became, which they fold under.
  const receipts = new Map<string, Receipts>();
  const asks = new Map<string, Ask>();
  // The note that hands over what each word said, by the word's id: a later note saying the word went elsewhere
  // takes it back, so no second conversation is offered with words already gone.
  const handing = new Map<string, Block>();
  // The turn that first took words from each waiting turn: where the notes left there go once it holds no words.
  const takers = new Map<Turn, Turn>();
  // A word said at a next step, taken by the event just before this one: the door's failure right after undoes it.
  let taken: { turn: Turn; word: string; seq: number; at: number; was: { turn: Turn; key: string; seq: number; block: Block } } | null = null;
  // A transient episode's try or wait parts the words on either side: each attempt's stream is its own block.
  let parted = false;

  for (const event of events) {
    const key = `e${event.seq}`;
    const justTaken = taken;
    taken = null;
    switch (event.kind) {
      case 'user': {
        // A waiting word told again under its id (its level changed while it waited): the latest says where it stands,
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
          // After the record ended: at its foot, in the turn already waiting there or a new one, never in the
          // run that ended before the words were said.
          const turn = event.reaches === 'resume' ? foot(key) : here(key);
          const level = levelOf(event.level);
          const block: Block = {
            key, kind: 'held', at: event.at, text: event.text ?? '', id: event.id, reaches: event.reaches,
            ...(level ? { level } : {}),
            ...(authorOf(event, helperWork(event)) ? { from: authorOf(event, helperWork(event)) } : {}),
            ...(event.choice && (event.origin ?? 'person') === 'person' ? { choice: event.choice } : {}),
            ...(event.files?.length ? { files: event.files } : {}),
          };
          turn.items.push(block);
          // Words sent now are taken where the steer is answered `injected`: the same words come again under
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
          // The answer the words were pressed as stays theirs where the session took them.
          if (was.block.choice && opened.ask && !opened.ask.choice) opened.ask.choice = was.block.choice;
          const at = was.turn.items.findIndex((item) => item.key === was.key);
          was.turn.items = was.turn.items.filter((item) => item.key !== was.key);
          where[was.seq] = key;
          waiting.delete(event.id!);
          if (was.block.reaches === 'next-step') taken = { turn: opened, word: event.id!, seq: event.seq, at, was };
          if (was.turn.waiting) {
            if (!takers.has(was.turn)) takers.set(was.turn, opened);
            // A turn that only waited, with nothing left in it but the driver's notes about its words (the resume's opening
            // line, a cool-off's), is the words' alone: its notes go with the turn that took them.
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
        // begins anew. No id joins.
        const same = !parted && last && last.kind === event.kind && (!last.id || !event.id || last.id === event.id);
        parted = false;
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
        // The close of a call a steer cut is written when the turn's prompt is answered, after the steered
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
          // A line is said of the locations it came with: ACP replaces a present list whole, and a line
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
        // A transient episode's one line quotes the provider's words its tries each stream.
        if (event.parts?.[0]?.code === 'agent.transient-retrying') {
          const words = event.parts.find((part) => part.words)?.words ?? event.text;
          if (words?.trim()) transient.push(words);
        }
        if (doorFailed(event)) {
          // 🔴 A word taken at a next step by the very answer that is the door's failure is never read. Else "stuck?" would be
          // the ask of a turn holding only the failure; it waits where it is said again,
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
          // failure the note says whole: it reads as that, never as the agent answering.
          const turn = here(key);
          const said = lastAgent(turn);
          if (said?.kind === 'message' && said.text?.trim() && (event.text ?? '').includes(said.text.trim())) {
            turn.items = turn.items.filter((item) => item !== said);
            for (const [seq, block] of Object.entries(where)) if (block === said.key) where[Number(seq)] = key;
          }
        }
        // A note about the person's words names them: they went elsewhere or cannot go on here, so they wait no
        // longer, and the note carries what they said where every one of them is held.
        const named = event.words ?? [];
        const held = named.map((id) => waiting.get(id));
        for (const [index, was] of held.entries()) {
          if (!was) continue;
          was.block.settled = true;
          waiting.delete(named[index]!);
        }
        const said = named.length > 0 && held.every(Boolean) ? held.map((was) => was!.block.text ?? '') : undefined;
        // Every word it names the person's own, so the page may offer to say them again (*Say it again*).
        const persons = said !== undefined && held.every((was) => was!.person === true);
        if (event.to) {
          for (const id of named) {
            const handed = handing.get(id);
            if (handed) delete handed.said;
            handing.delete(id);
          }
        }
        // A note about one call carries its id, so it folds with that call's run.
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
      case 'transient':
        // A try or a wait of a transient episode is quiet, never a line; the head reads it (`retryingAt`).
        parted = true;
        break;
      case 'receipt': {
        // No block of its own; it folds under the ask its word became, below.
        if (event.id && (event.receipt === 'sent' || event.receipt === 'ended')) {
          receipts.set(event.id, { ...receipts.get(event.id), [event.receipt]: event.at });
        }
        break;
      }
      default:
        here(key).items.push({ key, kind: 'raw', at: event.at, title: event.title, text: event.text, raw: event.raw });
        where[event.seq] = key;
    }
  }

  // 🔴 Each try of a transient episode streams the provider's error as its agent's words before the
  // door's failure, as the first failure did. The episode's one line says it, so each repeat reads as that line, never as the
  // agent answering, and the provider's error is shown once.
  if (transient.length > 0) {
    // As the door says it and as the agent streamed it: the words, and the words past the door's own preface (`Internal error: `).
    const quoted = transient.flatMap((words) => [words, words.slice(words.indexOf(': ') + 2)]).filter((text) => text.trim().length > 20);
    const failed = failedAttempts(events, where);
    for (const turn of turns) {
      const repeats: Block[] = [];
      for (const item of turn.items) {
        if (item.kind !== 'message' || !item.text || !failed.has(item.key)) continue;
        // 🔴 Only a failed attempt's stream, and only the error it ends on. Consecutive chunks are
        // one block, so a repeat may follow the agent's own words or another repeat; a quotation inside them, or in any other message
        // before or after the episode, stays as written.
        let left = item.text.trimEnd();
        for (let cut = true; cut;) {
          cut = false;
          for (const said of quoted) {
            if (left.endsWith(said.trim())) {
              left = left.slice(0, left.length - said.trim().length).trimEnd();
              cut = true;
            }
          }
        }
        if (left === item.text.trimEnd()) continue;
        if (left.trim()) item.text = left.trim();
        else repeats.push(item);
      }
      if (repeats.length === 0) continue;
      turn.items = turn.items.filter((item) => !repeats.includes(item));
      for (const [seq, block] of Object.entries(where)) if (repeats.some((item) => item.key === block)) where[Number(seq)] = turn.key;
    }
  }

  // Each word's receipts under the ask it became, taken at the ask's own moment: only where a receipt is observed.
  for (const [id, ask] of asks) {
    const seen = receipts.get(id);
    if (seen) ask.receipts = { ...seen, taken: ask.at };
  }

  foldOpening(turns, where);

  // The first thing the agent did, after the ask: the driver's own note is not the agent answering, nor are the person's
  // words waiting to reach it.
  for (const turn of turns) {
    const first = turn.items.find((item) => item.kind !== 'note' && item.kind !== 'held');
    if (turn.ask && first) turn.firstAfter = since(turn.ask.at, first.at);
  }

  // The wait for the first answer: the last turn, still open, asked and with nothing of the agent's yet.
  const tail = turns[turns.length - 1];
  if (tail?.ask && !tail.ended && !tail.waiting && !tail.items.some(AGENT)) tail.waitingSince = tail.ask.at;

  return { turns, usage, where };
}

/**
 * The codes of a part's first line where it began by a move or a hand-over: a briefed
 * move's, a curve move's and a hand-over's. The old part's last lines (`moved.on`, `chat.moved`, `chat.handed-over`) are the other side.
 */
const BOUNDARY = new Set(['moved.from', 'chat.moved-from', 'chat.taken-over']);

/** The codes of the opening line the runner writes at a part's open (`RotatedOpening.Say`: the account it opened on and why). */
const OPENING = new Set(['opening.opened', 'opening.carried-on', 'opening.rest']);

const noteCode = (block: Block) => (block.kind === 'note' ? block.parts?.[0]?.code ?? null : null);

/** Whether the part holds a first line of its own that a move or a hand-over wrote: the page then draws no *began* words of its own. */
export const beginsByMove = (turns: readonly Turn[]): boolean =>
  turns.some((turn) => turn.items.some((item) => BOUNDARY.has(noteCode(item) ?? '')));

/**
 * Folds the opening line under the boundary of a part that began by a move, as a briefed part's brief is folded, so a
 * move reads as one line on each side. A part with no boundary keeps its opening line as it is.
 */
function foldOpening(turns: Turn[], where: Record<number, string>): void {
  const boundary = turns.flatMap((turn) => turn.items).find((item) => BOUNDARY.has(noteCode(item) ?? ''));
  if (!boundary) return;
  const under: Block[] = [];
  for (const turn of [...turns]) {
    const folded = turn.items.filter((item) => OPENING.has(noteCode(item) ?? ''));
    if (folded.length === 0) continue;
    under.push(...folded);
    turn.items = turn.items.filter((item) => !folded.includes(item));
    // A turn that held nothing but the opening line is no turn once it is folded.
    if (turn.items.length === 0 && !turn.ask && !turn.ended) turns.splice(turns.indexOf(turn), 1);
  }
  if (under.length === 0) return;
  boundary.under = under;
  for (const [seq, block] of Object.entries(where)) if (under.some((item) => item.key === block)) where[Number(seq)] = boundary.key;
}

/** What counts as the agent answering: its words, its thinking, its calls and its plan. */
const AGENT = (item: Block) => item.kind === 'message' || item.kind === 'thought' || item.kind === 'tool' || item.kind === 'plan';

/** The last thing the agent did in a turn, past the driver's notes and the person's waiting words; undefined for none. */
const lastAgent = (turn: Turn): Block | undefined => [...turn.items].reverse().find(AGENT);

/**
 * The protocol door's failure (`door.failed`): by its code, or by the driver's English on a record kept before its
 * parts.
 */
const doorFailed = (event: SessionEvent): boolean =>
  event.parts?.[0]?.code === 'door.failed' || (!event.parts?.length && (event.text ?? '').startsWith('— the ACP session failed:'));

/**
 * When a transient episode tries again: the latest `transient` event's `next`, while no turn has ended
 * after it, so a try that works clears it and a later wait sets it again; undefined while a try runs, and where none waits. Whether it
 * is still ahead is the reader's, against its own clock.
 */
export function retryingAt(events: readonly SessionEvent[]): string | undefined {
  for (let at = events.length - 1; at >= 0; at -= 1) {
    const event = events[at]!;
    if (event.kind === 'turn') return undefined;
    if (event.kind === 'transient') return event.next ?? undefined;
  }
  return undefined;
}

/** When the turn the agent is on began, while it has not answered yet; undefined once it has, or when none is open. */
export const firstAnswerWait = (turns: readonly Turn[]): string | undefined => turns[turns.length - 1]?.waitingSince;

/** A call the agent is inside: its title, and when it began by the driver's clock. */
export type RunningCall = { title: string; since: string };

/**
 * The call the agent is inside, in the last turn while it is open: the last tool block that is pending or in progress,
 * neither finished nor cut. Read after `settle`, so a session that is not live says none — its open calls read stopped.
 * A call this page holds only updates of has no title, and is not named.
 */
export function runningCall(turns: readonly Turn[]): RunningCall | null {
  const turn = turns[turns.length - 1];
  if (!turn || turn.ended || turn.cut) return null;
  for (let at = turn.items.length - 1; at >= 0; at -= 1) {
    const item = turn.items[at]!;
    if (item.kind !== 'tool' || item.finished || item.stopped) continue;
    // A call the rules refused ended too; it waits on nobody, on either door.
    if (item.status === 'completed' || item.status === 'failed' || item.status === 'cut' || item.status === 'refused') continue;
    if (item.title) return { title: item.title, since: item.at };
  }
  return null;
}

/**
 * The key of the last thing the agent said, where it said anything: the jump to its last words, and what a parked
 * session asked the person, since its card keeps that message whole.
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
 * The turns as a session that has ended leaves them: the last one, if the wire never said
 * it ended, is cut — and the calls it left open never finished, so they read *stopped*, never
 * *running* for good. The person's words still waiting never reached it. A live session is
 * left as it is: its last turn may simply be going on.
 *
 * Words said after it ended (`resume`) wait for it to go on, so its end is not theirs, and the
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
  // The driver's lines written after the turn before ended (a move's last line) are no run the session left unfinished.
  if (!last.ask && last.items.length > 0 && last.items.every((item) => item.kind === 'note') && settled[at - 1]?.ended) return settled;
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
 * A turn's items as a reader reads them: every word the agent said stays in view, and the
 * work between two of them folds into one run that counts it. A run of one is shown as itself, since
 * folding it saves nothing; the last run stays open while the turn goes on (`tailOpen`), so the work in
 * hand is in view.
 *
 * @remarks
 * A driven session is one turn — one ask, then the run —
 * so folding the whole turn would hide the agent's narration of hundreds of calls behind one row, and a run that
 * never ended would fold nothing at all.
 */
export function segments(items: readonly Block[], tailOpen: boolean, standsAlone?: (block: Block) => boolean): Segment[] {
  const parts: Segment[] = [];
  let run: Block[] = [];
  const close = () => {
    if (run.length === 1) parts.push({ kind: 'block', block: run[0]! });
    else if (run.length > 1) parts.push({ kind: 'run', key: `run-${run[0]!.key}`, items: run, open: false });
    run = [];
  };

  for (const item of items) {
    // A call drawn as its exchange's card is never folded into a run: it is where the request is read.
    if (isWork(item) && !standsAlone?.(item)) run.push(item);
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
    // A call the stop or the session's end cut is not a failure, whatever the wire answered.
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
