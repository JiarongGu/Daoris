import type { Block, SessionEvent, Turn } from '../conversation/conversationTypes';
import type { Exchanges } from '../conversation/exchanges';
import { pathTo } from '../beside/besides';
import { isAfter, type MarkPlace, seqOf } from './readMark';
import { callKind, failedCall, filesChanged } from './turnFold';

/**
 * One line of *Since you left*, each counted from the record and the run with no model, and each a jump to the first row it names
 * (`to`, a block's key), where the conversation holds one.
 */
export type DigestLine =
  | { kind: 'turns'; count: number; to: string }
  | { kind: 'files'; count: number; to: string }
  | { kind: 'commands'; count: number; failed: string[]; to: string }
  | { kind: 'children'; started: number; ended: number; failed: number; nested?: number; to?: string }
  | { kind: 'tasks'; started: number; ended: number; failed: number; to?: string }
  | { kind: 'steps'; passed: number; failed: string[] }
  | { kind: 'waits'; open: number; asked: number; answered: number; to?: string }
  | { kind: 'words'; text: string; to: string };

/** The digest at the mark: the turn its row stands above, and its lines. */
export type Digest = { turn: string; lines: DigestLine[] };

/** How the run's steps stand now: how many passed, and the names of those that failed. */
export type StepsNow = { passed: number; failed: string[] };

/** How a session's run stands, read from its passage (`SESSION_GROUPS`' `passage`); none where its session runs no workflow. */
export const stepsNow = (passage?: { steps: readonly { node: string; mark: string }[] } | null): StepsNow | null => (passage
  ? {
    passed: passage.steps.filter((step) => step.mark === 'passed').length,
    failed: passage.steps.filter((step) => step.mark === 'failed').map((step) => step.node),
  }
  : null);

const FAILED_ENDS = new Set(['failed', 'lost']);

/** The longest of the agent's last words quoted, cut at a word. */
const QUOTE = 240;

const quoted = (text: string): string => {
  const first = text.trim().split('\n')[0]!.trim();
  if (first.length <= QUOTE) return first;
  const cut = first.slice(0, QUOTE);
  return `${cut.slice(0, Math.max(cut.lastIndexOf(' '), QUOTE / 2))}…`;
};

/**
 * What happened in the attended part `part` after the person's mark, as the lines of *Since you left*; null where nothing came after
 * it. A turn counts where it holds anything of the agent's or the person's after the mark; files
 * are those its edits changed, each once; a command failed where its call did; subagents and tasks count their starts and ends the
 * record keeps after the mark, the subagents another started among them counted too (`nested`); steps passed are those the run passed since the mark moved (`steps`, the mark's own count then);
 * waits are those open on the person now (`waiting`) and the helpers asked and answered since; the agent's last words are quoted as
 * its own where it said them after the mark.
 */
export function digestOf({ turns, part, mark, lifetimes, exchanges, waiting = 0, steps }: {
  turns: readonly Turn[];
  part: string;
  mark: MarkPlace & { steps?: number | null };
  lifetimes: readonly SessionEvent[];
  exchanges?: Exchanges;
  waiting?: number;
  steps?: StepsNow | null;
}): Digest | null {
  const timeOf = (block: Pick<Block, 'key' | 'at'>) => ({ seq: seqOf(block.key) ?? -1, at: block.at });
  const after = (block: Pick<Block, 'key' | 'at'>) => isAfter(mark, part, timeOf(block));
  const rows = turns.map((turn) => ({ turn, rows: [...(turn.ask ? [{ ...turn.ask, kind: 'ask' as const }] : []), ...turn.items].filter(after) }));
  const first = rows.find((each) => each.rows.length > 0);
  if (!first) return null;

  const lines: DigestLine[] = [];
  const blocks = rows.flatMap((each) => each.rows.filter((row): row is Block => row.kind !== 'ask'));
  // A part's boundary, or a task's end after the turn that started it, is no turn; nor are words waiting at a session's foot.
  const turned = rows.filter((each) => !each.turn.waiting && each.rows.some((row) => row.kind !== 'note' && row.kind !== 'beside'));
  if (turned.length > 0) lines.push({ kind: 'turns', count: turned.length, to: first.rows[0]!.key });

  const edits = blocks.filter((block) => block.kind === 'tool' && callKind(block) === 'edit' && block.status !== 'failed');
  const files = filesChanged(edits);
  if (files.length > 0) lines.push({ kind: 'files', count: files.length, to: edits[0]!.key });

  const commands = blocks.filter((block) => block.kind === 'tool' && callKind(block) === 'command');
  const failed = commands.filter(failedCall);
  if (commands.length > 0) {
    lines.push({ kind: 'commands', count: commands.length, failed: failed.map((block) => block.title ?? ''), to: (failed[0] ?? commands[0]!).key });
  }

  for (const kind of ['child', 'task'] as const) {
    const since = lifetimes.filter((event) => event.kind === kind && isAfter(mark, part, event));
    const started = since.filter((event) => !event.end);
    const ended = since.filter((event) => event.end);
    if (since.length === 0) continue;
    // A subagent another started has no row of the session's own: its jump is to the row of the one its way starts from.
    const first = (started[0] ?? ended[0])!.id ?? '';
    const id = kind === 'child' ? pathTo(lifetimes, first)[0] : first;
    const to = turns.flatMap((turn) => turn.items).find((block) => block.beside?.id === id && block.beside?.kind === kind)?.key;
    const nested = kind === 'child' ? started.filter((event) => event.parent).length : 0;
    lines.push({
      kind: kind === 'child' ? 'children' : 'tasks', started: started.length, ended: ended.length,
      failed: ended.filter((event) => FAILED_ENDS.has(event.end ?? '')).length, ...(nested ? { nested } : {}), ...(to ? { to } : {}),
    } as DigestLine);
  }

  const passed = steps && typeof mark.steps === 'number' ? Math.max(0, steps.passed - mark.steps) : 0;
  if (passed > 0 || (steps?.failed.length ?? 0) > 0) lines.push({ kind: 'steps', passed, failed: steps?.failed ?? [] });

  const cards = Object.values(exchanges?.cards ?? {});
  const asked = cards.filter((card) => after({ key: card.call, at: card.at }));
  const answered = cards.filter((card) => card.answer && Date.parse(card.answer.at) > Date.parse(mark.at));
  if (waiting > 0 || asked.length > 0 || answered.length > 0) {
    lines.push({ kind: 'waits', open: waiting, asked: asked.length, answered: answered.length, ...(asked[0] ? { to: asked[0].call } : {}) });
  }

  const words = [...blocks].reverse().find((block) => block.kind === 'message' && block.text?.trim());
  if (words) lines.push({ kind: 'words', text: quoted(words.text!), to: words.key });

  return { turn: first.turn.key, lines };
}
