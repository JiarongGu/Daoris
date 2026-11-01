import type { EdgeStyle } from '../../design/flowLayout';

// The one label a way on wears (FLOWCHART2a; the flowchart design §2): a word or two on the page's colour, drawn where it clears every
// node and every other chip. A decision's route (`when`) has none, since its port names it; a return wears its mark, a failure and a
// refusal their words, a data wire the name of what it carries as code. An edge is ink and never a status hue (D186 point 6): a way the
// driver found a problem in wears a ⚠ chip in the danger ink beside its ordinary ink.

export function EdgeChip({ style, text, problem }: { style: EdgeStyle; text: string; count?: number; problem?: string | undefined }) {
  const said = style === 'when' || text === '' ? null : style === 'data' ? <code className="font-mono">{text}</code> : text;
  if (said === null && !problem) return null;
  return (
    <>
      {said}
      {problem && <span data-flow-refusal-chip="" className="text-ink-danger">{said === null ? '' : ' '}⚠ {problem}</span>}
    </>
  );
}
