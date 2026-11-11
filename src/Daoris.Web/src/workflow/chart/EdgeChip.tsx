import type { EdgeStyle } from '../../design/flowLayout';

// The one label a way on wears: a word or two on the page's colour, drawn where it clears every
// node and every other chip. A decision's route (`when`) has none, since its port names it; a return wears its mark, a failure and a
// refusal their words, a data wire the name of what it carries as code; a way with a problem wears a ⚠ chip in the danger ink.

export function EdgeChip({ style, text, problem }: {
  style: EdgeStyle; text: string; count?: number; problem?: string | undefined;
}) {
  const said = style === 'when' || text === '' ? null : style === 'data' ? <code className="font-mono">{text}</code> : text;
  if (said === null && !problem) return null;
  return (
    <>
      {said}
      {problem && <span data-flow-refusal-chip="" className="text-ink-danger">{said === null ? '' : ' '}⚠ {problem}</span>}
    </>
  );
}
