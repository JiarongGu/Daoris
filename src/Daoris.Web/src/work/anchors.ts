import type { GoAhead } from '../data/api';
import type { RuleProposal } from '../settings/AgentRules';
import { type Block, lastWords, type Turn } from './conversation';
import { inputObject } from './preview';
import type { Wait } from './waits';

/**
 * How far a call's event may lie from the time the service took its request and still be that request's (WAITFOOT1c): ten
 * minutes. The driver stamps the call as it arrives and the service stamps the request as it takes it, on clocks that agree on
 * one machine and may drift a little against a shared host; the input's words do the joining, and the time only picks among
 * calls that say the same, so a bound this wide never joins a call to an ask made in another hour.
 */
export const ANCHOR_BOUND_MS = 10 * 60_000;

/**
 * A wait the conversation can show the raising event of (WAITFOOT1c, D168 point 4): a park's question (an intake's too), the work
 * to land (*Accept…* or the rebase words), the second opinion handed to the session, each go-ahead by its number, and each rule
 * proposal by its id (PROPOSEHERE1b), and each agent's ask for a helper by its id (WAITFOOT1e). A review's gate and a discard the line holds are raised by no event of the session's.
 */
export type AnchorWait = 'question' | 'work' | 'opinion' | `goAhead:${number}` | `rule:${string}` | `helper:${string}`;

/** The anchor a wait is found by, or null for one with no raising event. */
export function anchorOf(wait: Wait): AnchorWait | null {
  switch (wait.kind) {
    case 'question':
    case 'intake': return 'question';
    case 'accept':
    case 'rebase': return 'work';
    case 'opinion': return 'opinion';
    case 'goAhead': return `goAhead:${wait.number}`;
    case 'rule': return `rule:${wait.id}`;
    case 'helper': return `helper:${wait.id}`;
    default: return null;
  }
}

/** The key the end of a turn is drawn under (WAITFOOT1c): where the work it did waits, after everything it holds. */
export const turnEnd = (turn: string) => `end-${turn}`;

/** What counts as the agent working in a turn: its words, its thinking, its calls and its plan (WAITSHOWN1's own). */
const WORKED = new Set<Block['kind']>(['message', 'thought', 'tool', 'plan']);

/**
 * The work to land is raised by the end of the turn that did it: the last turn the agent worked in, past the driver's notes
 * after it and words waiting at the foot for it to go on (MSG1f), which are no run of the agent's.
 */
export function workKey(turns: readonly Turn[]): string | undefined {
  for (let at = turns.length - 1; at >= 0; at -= 1) {
    const turn = turns[at]!;
    if (!turn.waiting && turn.items.some((item) => WORKED.has(item.kind))) return turnEnd(turn.key);
  }
  return undefined;
}

/**
 * A second opinion's findings handed to the session (XAGENT1e) are raised by the turn they were handed as: its ask, which names
 * the opinion it is from. None for a gate that names no opinion, or one whose findings the page does not hold.
 */
export function opinionKey(turns: readonly Turn[], opinion: string | null | undefined): string | undefined {
  if (!opinion) return undefined;
  for (let at = turns.length - 1; at >= 0; at -= 1) {
    const ask = turns[at]!.ask;
    if (ask?.origin === 'target' && ask.opinion === opinion) return ask.key;
  }
  return undefined;
}

const text = (value: unknown): string => (typeof value === 'string' ? value.trim() : '');
const words = (value: string): Set<string> => new Set(value.toLowerCase().split(/[^\p{L}\p{N}]+/u).filter(Boolean));
const time = (value: string): number => Date.parse(value);

/** Every tool call the page holds, in order. */
function* calls(turns: readonly Turn[]): Generator<Block> {
  for (const turn of turns) for (const item of turn.items) if (item.kind === 'tool') yield item;
}

/** The call nearest `at` among those `fits` takes, within the bound; undefined where none is. */
function nearest(turns: readonly Turn[], at: string, fits: (input: Record<string, unknown>) => boolean): string | undefined {
  const asked = time(at);
  if (Number.isNaN(asked)) return undefined;
  let best: { key: string; gap: number } | undefined;
  for (const block of calls(turns)) {
    const input = inputObject(block.input);
    if (!input || !fits(input)) continue;
    const gap = Math.abs(time(block.at) - asked);
    if (!(gap <= ANCHOR_BOUND_MS)) continue;
    if (!best || gap < best.gap) best = { key: block.key, gap };
  }
  return best?.key;
}

/**
 * A go-ahead is raised by its `go_ahead_ask` call (KNOWUSE1a), which no id joins to it (D168 point 4): the call whose input says
 * the why this session's request was held with, word for word, and names every word of the act the go-ahead was first asked for
 * (the service's own join, so a request that joined it in more words is found too), nearest when the service took it, within
 * `ANCHOR_BOUND_MS`. Never by its title (D52), which each door words its own way. This session's latest request first: it is
 * the one its park waits on.
 */
export function goAheadKey(turns: readonly Turn[], goAhead: GoAhead, session: string): string | undefined {
  const act = words(goAhead.act);
  const requests = goAhead.asked.filter((request) => request.session === session).sort((a, b) => time(b.at) - time(a.at));
  for (const request of requests) {
    const why = request.why.trim();
    const said = (input: Record<string, unknown>) => {
      if (text(input.why) !== why) return false;
      const named = words(text(input.act));
      return act.size > 0 && [...act].every((word) => named.has(word));
    };
    const key = nearest(turns, request.at, said);
    if (key) return key;
  }
  return undefined;
}

/** What a rule proposal's file names (D74), which its call's input is joined on. */
export type ProposalFacts = Pick<RuleProposal, 'action' | 'scope' | 'rule' | 'default' | 'proposed'>;

/**
 * A rule proposal is raised by its `permission_propose` call where the call's input names what the proposal file names: its
 * action, its scope and its rule (a default's id), nearest when it was proposed, within the bound. None where no call says so:
 * a proposal's id is not the call's. The proposal is a wait (PROPOSEHERE1b); this is its anchor.
 */
export function proposalKey(turns: readonly Turn[], proposal: ProposalFacts): string | undefined {
  const named = proposal.action === 'default' ? text(proposal.default) : text(proposal.rule);
  if (!named) return undefined;
  return nearest(turns, proposal.proposed, (input) => text(input.action) === proposal.action
    && text(input.scope) === proposal.scope
    && (proposal.action === 'default' ? text(input.id) : text(input.rule)) === named);
}

/** What an agent's ask for a helper is joined on (WAITFOOT1e): the question as written, the agent it named, when it was made. */
export type HelperFacts = { question: string; agent: string | null; at: string };

/**
 * An agent's ask for a helper is raised by its `helper_ask` call (WAITFOOT1e, D168 point 4), which no id joins to it: the call
 * whose input carries the ask's question, word for word once trimmed, and the agent where the ask named one (as the tool's input
 * spells it, in either case); nearest when the ask was made, within `ANCHOR_BOUND_MS`. Never by the call's title (D52). None
 * where no call says so.
 */
export function helperKey(turns: readonly Turn[], ask: HelperFacts): string | undefined {
  const question = ask.question.trim();
  if (!question) return undefined;
  const agent = ask.agent?.trim().toLowerCase();
  return nearest(turns, ask.at, (input) => text(input.question) === question && (!agent || text(input.agent).toLowerCase() === agent));
}

/** What the anchors are read for: the waits that stand, and what their joins name. */
export type AnchorAsks = {
  /** The session the conversation is, whose go-ahead requests are its own. */
  session: string;
  waits: readonly Wait[];
  /** The go-aheads it asked, as the head is handed them. */
  goAheads?: readonly GoAhead[] | null;
  /** The opinion the landing's gate names (`OpinionGate.opinion`). */
  opinion?: string | null;
  /** The rules it proposed that wait on the person, as the frame read them (PROPOSEHERE1b): what each call is joined on. */
  proposals?: readonly (ProposalFacts & { id: string })[] | null;
  /** The helpers it asked for, as the frame read them (WAITFOOT1e): what each `helper_ask` call is joined on. */
  helpers?: readonly (HelperFacts & { id: string })[] | null;
};

/**
 * Where each wait was raised: the block's key where the page holds it, and the waits whose event may be on the pages before it
 * (`earlier`), which the way loads until it is held, as a jump to an event does (SESS1 S9).
 */
export type Anchors = { held: Partial<Record<AnchorWait, string>>; earlier: AnchorWait[] };

/**
 * Each standing wait's raising event (WAITFOOT1c, D168 point 4), read from the turns the page holds. `earlier` says pages before
 * them exist, and `from` when the first one held was said: a go-ahead asked after it would be on this page, so where it is not
 * found here it has none.
 */
export function anchorsOf(
  turns: readonly Turn[], asks: AnchorAsks, { earlier = false, from }: { earlier?: boolean; from?: string } = {},
): Anchors {
  const held: Partial<Record<AnchorWait, string>> = {};
  const before: AnchorWait[] = [];
  const begins = from ? time(from) : Number.NaN;
  for (const wait of asks.waits) {
    const anchor = anchorOf(wait);
    if (!anchor || anchor in held || before.includes(anchor)) continue;
    let key: string | undefined;
    let older = earlier;
    if (wait.kind === 'goAhead') {
      const goAhead = asks.goAheads?.find((row) => row.number === wait.number);
      key = goAhead ? goAheadKey(turns, goAhead, asks.session) : undefined;
      older = earlier && Boolean(goAhead) && (Number.isNaN(begins) || goAhead!.asked.some((request) => request.session === asks.session
        && time(request.at) <= begins + ANCHOR_BOUND_MS));
    } else if (wait.kind === 'rule') {
      // As a go-ahead's: proposed after the page began, its call would be on it, so where it is not found here it has none.
      const proposal = asks.proposals?.find((row) => row.id === wait.id);
      key = proposal ? proposalKey(turns, proposal) : undefined;
      older = earlier && Boolean(proposal) && (Number.isNaN(begins) || time(proposal!.proposed) <= begins + ANCHOR_BOUND_MS);
    } else if (wait.kind === 'helper') {
      // As a rule's: asked after the page began, its call would be on it, so where it is not found here it has none.
      const helper = asks.helpers?.find((row) => row.id === wait.id);
      key = helper ? helperKey(turns, helper) : undefined;
      older = earlier && Boolean(helper) && (Number.isNaN(begins) || time(helper!.at) <= begins + ANCHOR_BOUND_MS);
    } else if (anchor === 'question') key = lastWords(turns);
    else if (anchor === 'work') key = workKey(turns);
    else if (anchor === 'opinion') {
      key = opinionKey(turns, asks.opinion);
      older = earlier && Boolean(asks.opinion);
    }
    if (key) held[anchor] = key;
    else if (older) before.push(anchor);
  }
  return { held, earlier: before };
}

/** The waits each raising block carries a mark for, by its key: what the conversation draws *To the wait* under. */
export function marksOf(waits: readonly Wait[], held: Partial<Record<AnchorWait, string>>): Record<string, Wait[]> {
  const marks: Record<string, Wait[]> = {};
  for (const wait of waits) {
    const anchor = anchorOf(wait);
    const key = anchor ? held[anchor] : undefined;
    if (key) (marks[key] ??= []).push(wait);
  }
  return marks;
}

/**
 * The way from each wait to the event that raised it (WAITFOOT1c, D168 point 4): *In the conversation* on the head's section and
 * the strip's line, a press the frame holds, where the conversation found the event or may find it on a page before.
 */
export type WaitEvents = {
  question?: () => void;
  work?: () => void;
  opinion?: () => void;
  /** Each go-ahead's, by its number. */
  goAheads?: Readonly<Record<number, () => void>>;
  /** Each rule proposal's, by its id (PROPOSEHERE1b). */
  rules?: Readonly<Record<string, () => void>>;
  /** Each agent's ask for a helper, by its id (WAITFOOT1e). */
  helpers?: Readonly<Record<string, () => void>>;
};

/** The presses for the anchored waits, each showing its own; undefined where none is anchored. */
export function waitEvents(anchored: readonly AnchorWait[], show: (wait: AnchorWait) => void): WaitEvents | undefined {
  if (anchored.length === 0) return undefined;
  const events: WaitEvents = {};
  const goAheads: Record<number, () => void> = {};
  const rules: Record<string, () => void> = {};
  const helpers: Record<string, () => void> = {};
  for (const anchor of anchored) {
    if (anchor.startsWith('goAhead:')) goAheads[Number(anchor.slice('goAhead:'.length))] = () => show(anchor);
    else if (anchor.startsWith('rule:')) rules[anchor.slice('rule:'.length)] = () => show(anchor);
    else if (anchor.startsWith('helper:')) helpers[anchor.slice('helper:'.length)] = () => show(anchor);
    else events[anchor as 'question' | 'work' | 'opinion'] = () => show(anchor);
  }
  if (Object.keys(goAheads).length > 0) events.goAheads = goAheads;
  if (Object.keys(rules).length > 0) events.rules = rules;
  if (Object.keys(helpers).length > 0) events.helpers = helpers;
  return events;
}
