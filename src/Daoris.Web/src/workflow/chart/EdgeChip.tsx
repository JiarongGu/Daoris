import type { EdgeStyle } from '../../design/flowLayout';

// The one label a way on wears: a word or two on the page's colour, drawn where it clears every
// node and every other chip. A decision's route (`when`) has none, since its port names it; a return wears its mark, a failure and a
// refusal their words, a data wire the name of what it carries as code. An edge is ink and never a status hue: a way the
// driver found a problem in wears a ⚠ chip in the danger ink beside its ordinary ink, and a way a proposal adds or removes
// its sign and its word, a `when` among them.

export function EdgeChip({ style, text, problem, mark }: {
  style: EdgeStyle; text: string; count?: number; problem?: string | undefined; mark?: { sign: string; word: string };
}) {
  const said = style === 'when' || text === '' ? null : style === 'data' ? <code className="font-mono">{text}</code> : text;
  if (said === null && !problem && !mark) return null;
  return (
    <>
      {said}
      {mark && (
        <span data-flow-change="" className="font-semibold text-ink">
          {said === null ? '' : ' '}<span aria-hidden>{mark.sign} </span>{mark.word}
        </span>
      )}
      {problem && <span data-flow-refusal-chip="" className="text-ink-danger">{said === null && !mark ? '' : ' '}⚠ {problem}</span>}
    </>
  );
}
