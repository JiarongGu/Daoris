import { createContext, useContext } from 'react';
import { BesideStart, TaskEnd } from '../beside/BesideRows';
import { PlanView, RawLine, ThoughtRow } from '../conversation/BlockLines';
import type { Block } from '../conversation/conversationTypes';
import { ToolCard } from '../conversation/ToolCard';

/** The attended session's tree, so a call's file inside it reads relative to it and opens in the preview; null where none. */
export const StoryTree = createContext<string | null>(null);

/** One folded block as the conversation draws it: a call its `ToolCard`, a thought, a plan, or what the record named no kind for. */
export function FoldedBlock({ block }: { block: Block }) {
  const tree = useContext(StoryTree);
  switch (block.kind) {
    case 'tool': return <ToolCard call={block} tree={tree} />;
    case 'thought': return <ThoughtRow text={block.text ?? ''} />;
    case 'plan': return <PlanView entries={block.entries ?? []} />;
    default: return <RawLine block={block} />;
  }
}

/**
 * A call that started a subagent or a background task, or the task's end: never folded, since it opens the record of what it started
 * (`BesideStart`, `TaskEnd`), each leading to the other.
 */
export function BesideCall({ block }: { block: Block }) {
  if (block.kind === 'beside') return <TaskEnd block={block} />;
  return (
    <div className="grid min-w-0 gap-1">
      <FoldedBlock block={block} />
      {block.beside && <BesideStart beside={block.beside} />}
    </div>
  );
}

/** A burst of activity opened: each call, thought and plan as the conversation draws it, a call's file a door into the preview. */
export function FoldedCalls({ blocks }: { blocks: readonly Block[] }) {
  return (
    <ul className="m-0 grid list-none gap-0.5 border-l border-line pl-3">
      {blocks.map((block) => <li key={block.key} className="min-w-0"><FoldedBlock block={block} /></li>)}
    </ul>
  );
}
