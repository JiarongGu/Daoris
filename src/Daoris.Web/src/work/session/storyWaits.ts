import { anchorOf } from '../waits/anchors';
import type { Wait } from '../waits/waits';
import type { StoryItem } from './story';

/**
 * What the story says of the waits its events raised: the waits each line raised (a burst's, every call it folded), by the line's
 * key; the line a wait's *In the conversation* led to, outlined; and the way from a line's *To the wait* to the wait itself.
 */
export type StoryWaits = { marks: Readonly<Record<string, readonly Wait[]>>; sought: string | null; onToWait: (wait: Wait) => void };

/**
 * The story's line a block of the record stands in: its own line, the burst of activity it folded into, or, for a turn's end
 * (`turnEnd`), the turn's last line.
 */
export function lineOfBlock(story: readonly StoryItem[], block: string): string | undefined {
  return story.find((item) => item.ends === block
    || (item.kind === 'tools' ? item.blocks.some((each) => each.key === block) : item.key.endsWith(`:${block}`)))?.key;
}

/** The reading's marks by block and the block it revealed, read onto the story's lines, each wait once a line; none where nothing leads to a wait. */
export function storyWaitsOf(story: readonly StoryItem[], { marks, revealed, onToWait }: {
  marks?: Readonly<Record<string, readonly Wait[]>>;
  revealed?: string | null;
  onToWait?: (wait: Wait) => void;
}): StoryWaits | undefined {
  if (!onToWait) return undefined;
  const lines: Record<string, Map<string, Wait>> = {};
  for (const [block, waits] of Object.entries(marks ?? {})) {
    const line = lineOfBlock(story, block);
    if (!line) continue;
    const held = (lines[line] ??= new Map());
    for (const wait of waits) held.set(anchorOf(wait) ?? JSON.stringify(wait), wait);
  }
  return {
    marks: Object.fromEntries(Object.entries(lines).map(([line, held]) => [line, [...held.values()]])),
    sought: revealed ? lineOfBlock(story, revealed) ?? null : null,
    onToWait,
  };
}
