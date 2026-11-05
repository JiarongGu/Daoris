import type { ListGroup } from '../../design/canvas/CanvasList';
import type { MapLine } from '../mapLayout';

// The workspace map as a list (CANVAS1e; the canvas design §2.6, §3.3): the reading that is not a canvas, offered at every width by
// *Show as list*. Pure: the repositories in the map's reading order, then the lines by kind, the work before the declarations, each
// item named as the drawing names it for a reader.

/** The kinds in the order the list reads them: the work first, then what is declared and what is shared. */
const READING = ['quests', 'asks', 'chains', 'depends', 'knowledge'] as const;

export function mapListGroups(stops: readonly string[], lines: readonly MapLine[], words: {
  repositories: string;
  kind: (kind: MapLine['kind']) => string;
  node: (id: string) => string;
  line: (line: MapLine) => string;
}): ListGroup[] {
  const groups: ListGroup[] = [{ id: 'repositories', title: words.repositories, items: stops.map((id) => ({ id, title: words.node(id) })) }];
  for (const kind of READING) {
    const items = lines.filter((line) => line.kind === kind).map((line) => ({ id: line.id, title: words.line(line) }));
    if (items.length > 0) groups.push({ id: kind, title: words.kind(kind), items });
  }
  return groups;
}
