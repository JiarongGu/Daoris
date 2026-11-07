import { type RefObject, useMemo, useRef, useState } from 'react';
import type { CanvasHandle } from './Canvas';
import { type Findable, findMatches, foundBy, stepMatch } from './findNodes';
import type { View } from './viewMath';

/**
 * The state of find on a canvas: the query, the match shown, the matches by name and what dims. Stepping reveals
 * each match through the feature's `reveal`; clearing puts the view back where stepping began (Escape).
 */
export function useCanvasFind({ nodes, handle, reveal }: {
  nodes: readonly Findable[];
  handle: RefObject<CanvasHandle | null>;
  reveal: (id: string) => void;
}) {
  const [query, setQuery] = useState('');
  const [at, setAt] = useState(-1);
  const started = useRef<View | null>(null);
  const matches = useMemo(() => {
    const items = new Map(nodes.map((node) => [node.id, node]));
    // A result found by what the item declares says so after its name.
    return findMatches(nodes, query).map((id) => {
      const item = items.get(id)!;
      const by = foundBy(item, query);
      return { id, title: by?.by === 'also' ? `${item.title} — ${by.said}` : item.title };
    });
  }, [nodes, query]);
  const matching = useMemo(() => (query.trim() === '' ? null : new Set(matches.map((match) => match.id))), [matches, query]);
  const shown = at >= 0 ? matches[at]?.id ?? null : null;

  return {
    query, at, matches, matching, shown,
    onQuery: (next: string) => { setQuery(next); setAt(-1); },
    step: (back: boolean) => {
      started.current ??= handle.current?.view() ?? null;
      const next = stepMatch(at, matches.length, back);
      setAt(next);
      if (next >= 0) reveal(matches[next]!.id);
    },
    clear: () => {
      setQuery('');
      setAt(-1);
      if (started.current) handle.current?.setView(started.current);
      started.current = null;
    },
    pick: (id: string) => { setAt(matches.findIndex((match) => match.id === id)); reveal(id); },
  };
}
