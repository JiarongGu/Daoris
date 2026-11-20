import { createContext, useContext } from 'react';
import type { Block } from '../conversation/conversationTypes';
import { ToolCard } from '../conversation/ToolCard';

/** The attended session's tree, so a call's file inside it reads relative to it and opens in the preview; null where none. */
export const StoryTree = createContext<string | null>(null);

/** A burst of activity opened: each call its `ToolCard`, the file it names a door into the preview where the frame opens one. */
export function FoldedCalls({ blocks }: { blocks: readonly Block[] }) {
  const tree = useContext(StoryTree);
  return (
    <ul className="m-0 grid list-none gap-0.5 border-l border-line pl-3">
      {blocks.filter((block) => block.kind === 'tool').map((block) => (
        <li key={block.key} className="min-w-0"><ToolCard call={block} tree={tree} /></li>
      ))}
    </ul>
  );
}
