import { CHANNEL_CLAIM_OPENING } from '../channel';
import type { Ask, Block, Turn } from '../conversation/conversationTypes';
import type { Author } from '../conversation/sender';
import type { ExchangeCard, Exchanges } from '../conversation/exchanges';
import type { LineRow } from '../exchanges/rows';
import { type CallKind, callsByKind, failedCall, filesChanged } from '../catchup/turnFold';
import { type Mark, type Passage, type StepRow, stepRowsOf } from '../track/passage';
import { turnEnd } from '../waits/anchors';

/**
 * Who said a line of the story: the person (`you`), an author by its mark (Daoris, the supervisor on the person's behalf, a helper's
 * or a partner's answer, one not attributed), or another session's claim on the ask's channel (`claim`), named by its poster.
 */
export type StoryWho = 'you' | Author | 'claim';

/**
 * One line of the session's story, in the order it happened (`at`, ms). A turn's last line carries its end's key (`turnEnd`, `ends`),
 * where the work it did waits.
 */
export type StoryItem = { ends?: string } & (
  | { kind: 'owner'; key: string; at: number; who: StoryWho; text: string; held: boolean; reaches?: Block['reaches'];
      /** The channel message these words carry, by its id: its sender chip names its poster and opens the channel. */
      channel?: string }
  | { kind: 'agent'; key: string; at: number; text: string }
  /** A driver's note, its block kept for the doors it offers (*Say it again*, *Start a conversation*, the session it went to). */
  | { kind: 'note'; key: string; at: number; text: string; block: Block }
  /** Words a cooling account holds at a turn's end, with *Go on in a new session*. */
  | { kind: 'waiting'; key: string; at: number; turn: Turn }
  | { kind: 'tools'; key: string; at: number; calls: { kind: CallKind; count: number }[]; failed: number; files: number; blocks: Block[] }
  /** A call that started a subagent or a background task, or the task's end: alone, never folded, since it opens what it started. */
  | { kind: 'beside'; key: string; at: number; block: Block }
  | { kind: 'step'; key: string; at: number; run: string; node: string; edge: 'start' | 'end'; mark: Mark | 'ended'; attempt: number | null }
  | { kind: 'helper'; key: string; at: number; question: string; agent: string | null; state: string | null; helper: string | null;
      answer?: { text: string; at: number } }
  | { kind: 'opinion'; key: string; at: number; text: string; reviewer: string | null; model: string | null }
  /** One of its line's requests, quests or channel messages (`lineRowsOf`): an untimed one under the last line. */
  | { kind: 'line'; key: string; at: number; row: LineRow });

/** Who gave a second opinion, where the page holds its row: the reviewer's maker and the model that ran. */
export type OpinionBy = { reviewer: string | null; model: string | null };

/**
 * What the story is read from: the conversation's turns, the run's step rows, the helpers its agent asked, and its line's rows (each
 * request each way, each quest it published, the session that published its own, the question it resumed after, its channel's
 * messages).
 */
export type StoryLine = {
  turns: readonly Turn[];
  steps: readonly StepRow[];
  exchanges: readonly ExchangeCard[];
  rows: readonly LineRow[];
  /** The opinion a turn's words carry, by its id; absent, the line names no reviewer. */
  opinionOf?: (id: string) => OpinionBy | null;
};

/**
 * The story's sources as the page already reads them: the conversation's settled turns, its run's passage (`SESSION_GROUPS`), the
 * exchanges its helpers' asks join (`exchangesOf`) and its line's rows (`useLineRows`).
 */
export function storyLineOf({ turns, passage, exchanges, rows = [], opinionOf }: {
  turns: readonly Turn[];
  passage: Passage | null | undefined;
  exchanges: Exchanges;
  rows?: readonly LineRow[];
  opinionOf?: StoryLine['opinionOf'];
}): StoryLine {
  return { turns, steps: passage ? stepRowsOf(passage) : [], exchanges: Object.values(exchanges.cards), rows, opinionOf };
}

const time = (iso: string | null | undefined): number => {
  const parsed = iso ? Date.parse(iso) : Number.NaN;
  return Number.isFinite(parsed) ? parsed : Number.POSITIVE_INFINITY;
};

/** Blocks that fold into a burst's one line: the calls, the thinking, the plan. */
const FOLDS: ReadonlySet<Block['kind']> = new Set(['tool', 'thought', 'plan', 'raw']);

/** A call that started a subagent or a task, or the task's end: a line of its own. */
const standsAlone = (block: Block) => block.kind === 'beside' || (block.kind === 'tool' && Boolean(block.beside));

/**
 * What a turn's words come from, by the one sender rule the conversation's chip reads (`AskView`): the author its mark names, another
 * session's claim on the ask's channel, the target Daoris composed, else the person.
 */
function whoAsked(ask: Ask): StoryWho {
  if (ask.from) return ask.from;
  if (ask.origin !== 'target') return 'you';
  return ask.channel || (ask.text ?? '').startsWith(CHANNEL_CLAIM_OPENING) ? 'claim' : 'daoris';
}

function burst(blocks: Block[]): StoryItem {
  return {
    kind: 'tools', key: `tools:${blocks[0]!.key}`, at: time(blocks[0]!.at), calls: callsByKind(blocks),
    failed: blocks.filter(failedCall).length, files: filesChanged(blocks).length, blocks,
  };
}

/** A turn's lines: its words (unless a card or an opinion takes them), then its blocks, each burst of activity folded to one line. */
function turnLines(turn: Turn, cards: ReadonlyMap<string, ExchangeCard>, answered: ReadonlySet<string>, opinionOf: StoryLine['opinionOf']): StoryItem[] {
  const lines: StoryItem[] = [];
  const ask = turn.ask;
  if (ask && !answered.has(ask.key)) {
    if (ask.opinion) {
      const by = opinionOf?.(ask.opinion) ?? null;
      lines.push({ kind: 'opinion', key: `opinion:${ask.key}`, at: time(ask.at), text: ask.text, reviewer: by?.reviewer ?? null, model: by?.model ?? null });
    } else {
      lines.push({
        kind: 'owner', key: `ask:${ask.key}`, at: time(ask.at), who: whoAsked(ask), text: ask.text, held: false,
        ...(ask.channel ? { channel: ask.channel } : {}),
      });
    }
  }
  let run: Block[] = [];
  const close = () => {
    if (run.length > 0) lines.push(burst(run));
    run = [];
  };
  for (const block of turn.items) {
    const card = cards.get(block.key);
    if (card) {
      close();
      lines.push({
        kind: 'helper', key: `helper:${card.ask}`, at: time(card.at), question: card.question, agent: card.agent, state: card.state,
        helper: card.helper, answer: card.answer ? { text: card.answer.text, at: time(card.answer.at) } : undefined,
      });
    } else if (standsAlone(block)) {
      close();
      lines.push({ kind: 'beside', key: `beside:${block.key}`, at: time(block.at), block });
    } else if (FOLDS.has(block.kind)) {
      run.push(block);
    } else {
      close();
      if (block.kind === 'message' && block.text) lines.push({ kind: 'agent', key: `said:${block.key}`, at: time(block.at), text: block.text });
      else if (block.kind === 'held' && block.text) {
        lines.push({
          kind: 'owner', key: `held:${block.key}`, at: time(block.at), who: 'you', text: block.text, held: !block.settled, reaches: block.reaches,
        });
      } else if (block.kind === 'note' && (block.text || block.code)) {
        lines.push({ kind: 'note', key: `note:${block.key}`, at: time(block.at), text: block.text ?? '', block });
      }
    }
  }
  close();
  if (turn.waiting) lines.push({ kind: 'waiting', key: `waiting:${turn.key}`, at: lines[lines.length - 1]?.at ?? time(ask?.at), turn });
  const last = lines[lines.length - 1];
  if (last) lines[lines.length - 1] = { ...last, ends: turnEnd(turn.key) };
  return lines;
}

/** Of lines at one moment: a step's end, then the next step's start, then what happened inside it. */
const RANK: Readonly<Record<StoryItem['kind'], number>> = {
  step: 0, owner: 1, opinion: 1, agent: 1, note: 1, tools: 1, beside: 1, helper: 1, line: 1, waiting: 1,
};

/**
 * **The story**: one stream of every line of the session's work in the order it happened. The conversation's turns (the person's
 * words, the agent's, each burst of activity folded to one line that counts its calls, a call that started a subagent or a task and
 * the task's end each a line of its own), each step's start and end as a rule, each
 * helper its agent asked with its answer joined (its answer's turn never drawn twice), each second opinion handed as a turn, and
 * each of its line's rows at its moment: a request asked and come back, a quest filed and closed, the session that published its
 * quest (first), the question it resumed after, and each message on its channel. Read from the records alone, with no model.
 */
export function storyOf({ turns, steps, exchanges, rows, opinionOf }: StoryLine): StoryItem[] {
  const cards = new Map(exchanges.map((card) => [card.call, card]));
  const answered = new Set(exchanges.flatMap((card) => (card.answer ? [card.answer.key] : [])));
  const lines: StoryItem[] = [
    ...steps.map((row): StoryItem => ({
      kind: 'step', key: `step:${row.key}`, at: row.at, run: row.run, node: row.node, edge: row.edge, mark: row.mark, attempt: row.attempt,
    })),
    ...turns.flatMap((turn) => turnLines(turn, cards, answered, opinionOf)),
    ...rows.map((row): StoryItem => ({ kind: 'line', key: `line:${row.key}`, at: row.at ?? Number.POSITIVE_INFINITY, row })),
  ];
  const order = (item: StoryItem) => (item.kind === 'step' ? (item.edge === 'end' ? 0 : 1) : 2);
  return lines
    .map((item, index) => ({ item, index }))
    .sort((a, b) => a.item.at - b.item.at || RANK[a.item.kind] - RANK[b.item.kind] || order(a.item) - order(b.item) || a.index - b.index)
    .map(({ item }) => item);
}
