import type { Block, Turn } from '../conversation/conversationTypes';

/** What a call did, as its one line counts it: the kinds its door reports, anything else `other`. */
export type CallKind = 'read' | 'edit' | 'search' | 'command' | 'fetch' | 'other';

const KINDS: readonly CallKind[] = ['read', 'edit', 'search', 'command', 'fetch', 'other'];

const KIND_OF: Readonly<Record<string, CallKind>> = {
  read: 'read', edit: 'edit', delete: 'edit', move: 'edit', search: 'search', execute: 'command', fetch: 'fetch',
};

export const callKind = (block: Block): CallKind => KIND_OF[block.toolKind ?? ''] ?? 'other';

/** The calls of `items` by kind, in one fixed order, each kind it holds once. */
export function callsByKind(items: readonly Block[]): { kind: CallKind; count: number }[] {
  const counts = new Map<CallKind, number>();
  for (const item of items) if (item.kind === 'tool') counts.set(callKind(item), (counts.get(callKind(item)) ?? 0) + 1);
  return KINDS.filter((kind) => counts.has(kind)).map((kind) => ({ kind, count: counts.get(kind)! }));
}

/** The files `items`' edits changed, each once, by the path its call names (its locations, else its diff's). */
export function filesChanged(items: readonly Block[]): string[] {
  const paths = new Set<string>();
  for (const item of items) {
    if (item.kind !== 'tool' || callKind(item) !== 'edit' || item.status === 'failed') continue;
    for (const path of item.locations ?? []) paths.add(path);
    for (const content of item.content ?? []) if (content.type === 'diff' && content.path) paths.add(content.path);
  }
  return [...paths];
}

/**
 * A call that acts outside the conversation, which never folds: a push, a pull request, a release, a publish, a landing. Read
 * from its command as the agent wrote it, since no door names such an act.
 */
const OUTSIDE = /\bgit\s+push\b|\bgh\s+(?:pr|release)\s+(?:create|merge)\b|\bnpm\s+publish\b|\bdotnet\s+nuget\s+push\b|\bmerge-branch\b/;

export const actsOutside = (block: Block): boolean =>
  block.kind === 'tool' && callKind(block) === 'command' && OUTSIDE.test(`${block.title ?? ''} ${block.input ?? ''}`);

/** A call that failed on its own: a stop's cut is said where the turn ended, and is no failure. */
export const failedCall = (block: Block): boolean => block.kind === 'tool' && block.status === 'failed' && !block.stopped;

/**
 * Whether a block of an ended turn stays in view while the turn is folded (design §6's never-folded list): the turn's closing
 * words; the person's words; a failure or a stopped call; a call that acts outside the conversation; a call standing alone
 * (a request's card, a subagent's or task's start, its header row); a task's end; the driver's notes about the session (a part's
 * boundary, a limit, a stop, where words went); and a block that raised a wait. A row folds only where it changed nothing outside
 * the conversation and failed nothing: thinking, successful calls, a plan, the agent's words before its last.
 */
export function staysOpen(block: Block, { closing, raised, alone }: {
  closing?: string; raised?: (key: string) => boolean; alone?: (block: Block) => boolean;
}): boolean {
  if (block.key === closing || raised?.(block.key)) return true;
  switch (block.kind) {
    case 'held':
    case 'beside':
      return true;
    case 'note':
      return !block.id || Boolean(block.code);
    case 'tool':
      return failedCall(block) || Boolean(block.stopped) || actsOutside(block) || Boolean(block.beside) || Boolean(alone?.(block));
    default:
      return false;
  }
}

/** An ended turn as its one line counts it: its calls by kind, how many failed, the files it changed and how long it took. */
export type FoldLine = { calls: { kind: CallKind; count: number }[]; failed: number; files: number; took?: number; thought: boolean };

/**
 * An ended turn folded: its one line and the blocks that stay in view under it, in order. Null for a turn that does not fold: one
 * still running or ended inside (`cut`), or one that holds nothing the fold would hide.
 */
export function foldOf(turn: Turn, rules: { raised?: (key: string) => boolean; alone?: (block: Block) => boolean } = {}):
  { line: FoldLine; shown: Block[] } | null {
  if (!turn.ended || turn.cut || turn.waiting) return null;
  const closing = [...turn.items].reverse().find((item) => item.kind === 'message')?.key;
  const shown = turn.items.filter((item) => staysOpen(item, { ...rules, closing }));
  if (shown.length === turn.items.length) return null;
  return {
    line: {
      calls: callsByKind(turn.items),
      failed: turn.items.filter(failedCall).length,
      files: filesChanged(turn.items).length,
      took: turn.took,
      thought: turn.items.some((item) => item.kind === 'thought'),
    },
    shown,
  };
}
