import type { Block, Turn } from '../conversation/conversationTypes';
import type { ExchangeCard, Exchanges } from '../conversation/exchanges';
import type { ChannelItem } from '../channel';
import { type CallKind, callsByKind, failedCall, filesChanged } from '../catchup/turnFold';
import { type Mark, type Passage, type StepRow, stepRowsOf } from '../track/passage';

/**
 * Who said a line of the story: the person (`you`), Daoris itself, the supervisor on the person's behalf, the session's agent, or
 * another session, named by its repository.
 */
export type StoryWho = 'you' | 'daoris' | 'supervisor' | 'agent' | string;

/** One line of the session's story, in the order it happened (`at`, ms). */
export type StoryItem =
  | { kind: 'owner'; key: string; at: number; who: StoryWho; text: string; held: boolean }
  | { kind: 'agent'; key: string; at: number; text: string }
  | { kind: 'note'; key: string; at: number; text: string }
  | { kind: 'tools'; key: string; at: number; calls: { kind: CallKind; count: number }[]; failed: number; files: number; blocks: Block[] }
  | { kind: 'step'; key: string; at: number; node: string; edge: 'start' | 'end'; mark: Mark | 'ended'; attempt: number | null }
  | { kind: 'helper'; key: string; at: number; question: string; agent: string | null; state: string | null; helper: string | null;
      answer?: { text: string; at: number } }
  | { kind: 'opinion'; key: string; at: number; text: string; reviewer: string | null; model: string | null }
  | { kind: 'channel'; key: string; at: number; who: StoryWho; text: string };

/** Who gave a second opinion, where the page holds its row: the reviewer's maker and the model that ran. */
export type OpinionBy = { reviewer: string | null; model: string | null };

/** What the story is read from: the conversation's turns, the run's step rows, the helpers asked, the line's channel. */
export type StoryLine = {
  turns: readonly Turn[];
  steps: readonly StepRow[];
  exchanges: readonly ExchangeCard[];
  channel: readonly ChannelItem[];
  /** The opinion a turn's words carry, by its id; absent, the line names no reviewer. */
  opinionOf?: (id: string) => OpinionBy | null;
};

/**
 * The story's sources as the page already reads them: the conversation's settled turns, its run's passage (`SESSION_GROUPS`), the
 * exchanges its helpers' asks join (`exchangesOf`) and its line's channel, kept to its line of work by the caller.
 */
export function storyLineOf({ turns, passage, exchanges, channel, opinionOf }: {
  turns: readonly Turn[];
  passage: Passage | null | undefined;
  exchanges: Exchanges;
  channel: readonly ChannelItem[];
  opinionOf?: StoryLine['opinionOf'];
}): StoryLine {
  return { turns, steps: passage ? stepRowsOf(passage) : [], exchanges: Object.values(exchanges.cards), channel, opinionOf };
}

const time = (iso: string | null | undefined): number => {
  const parsed = iso ? Date.parse(iso) : Number.NaN;
  return Number.isFinite(parsed) ? parsed : Number.POSITIVE_INFINITY;
};

/** Blocks that fold into a burst's one line: the calls, the thinking, the plan, a task's end. */
const FOLDS: ReadonlySet<Block['kind']> = new Set(['tool', 'thought', 'plan', 'raw', 'beside']);

/** What a turn's words come from, as the story names it: the person, Daoris, the supervisor or another session. */
function whoAsked(turn: Turn): StoryWho {
  const from = turn.ask?.from;
  if (!from) return turn.ask?.origin === 'target' ? 'daoris' : 'you';
  return from === 'daoris' || from === 'supervisor' ? from : 'agent';
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
      lines.push({ kind: 'owner', key: `ask:${ask.key}`, at: time(ask.at), who: whoAsked(turn), text: ask.text, held: false });
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
    } else if (FOLDS.has(block.kind)) {
      run.push(block);
    } else {
      close();
      if (block.kind === 'message' && block.text) lines.push({ kind: 'agent', key: `said:${block.key}`, at: time(block.at), text: block.text });
      else if (block.kind === 'held' && block.text) {
        lines.push({ kind: 'owner', key: `held:${block.key}`, at: time(block.at), who: 'you', text: block.text, held: !block.settled });
      } else if (block.kind === 'note' && block.text) lines.push({ kind: 'note', key: `note:${block.key}`, at: time(block.at), text: block.text });
    }
  }
  close();
  return lines;
}

/** Of lines at one moment: a step's end, then the next step's start, then what happened inside it. */
const RANK: Readonly<Record<StoryItem['kind'], number>> = {
  step: 0, owner: 1, opinion: 1, agent: 1, note: 1, tools: 1, helper: 1, channel: 1,
};

/**
 * **The story**: one stream of every line of the session's work in the order it happened. The conversation's turns (the person's
 * words, the agent's, each burst of activity folded to one line that counts its calls), each step's start and end as a rule, each
 * helper asked with its answer joined (its answer's turn never drawn twice), each second opinion handed as a turn, and each message
 * on its line's channel. Read from the records alone, with no model.
 */
export function storyOf({ turns, steps, exchanges, channel, opinionOf }: StoryLine): StoryItem[] {
  const cards = new Map(exchanges.map((card) => [card.call, card]));
  const answered = new Set(exchanges.flatMap((card) => (card.answer ? [card.answer.key] : [])));
  const lines: StoryItem[] = [
    ...steps.map((row): StoryItem => ({
      kind: 'step', key: `step:${row.key}`, at: row.at, node: row.node, edge: row.edge, mark: row.mark, attempt: row.attempt,
    })),
    ...turns.flatMap((turn) => turnLines(turn, cards, answered, opinionOf)),
    ...channel.filter((item) => item.kind === 'message' && item.text).map((item, index): StoryItem => ({
      kind: 'channel', key: `channel:${item.id ?? index}`, at: time(item.at), who: item.person ? 'you' : item.repository ?? item.agent ?? 'agent',
      text: item.text ?? '',
    })),
  ];
  const order = (item: StoryItem) => (item.kind === 'step' ? (item.edge === 'end' ? 0 : 1) : 2);
  return lines
    .map((item, index) => ({ item, index }))
    .sort((a, b) => a.item.at - b.item.at || RANK[a.item.kind] - RANK[b.item.kind] || order(a.item) - order(b.item) || a.index - b.index)
    .map(({ item }) => item);
}
