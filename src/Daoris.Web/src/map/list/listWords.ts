import type { TFunction } from 'i18next';
import { lineName } from '../MapLines';
import type { MapLine } from '../mapLayout';
import type { MapNode } from '../topology';

// What each row of the Map's list says: the drawing's own words for a reader, with the counts and the
// states a person scans the list for.

/** A repository's row: its name, its open count, and a session waiting on the person or working there. */
export function listNode(t: TFunction, node: MapNode): string {
  const there = node.parked ? 'Parked' : node.working ? 'Working' : '';
  return t(`map.list.node${there}`, { repository: node.id, count: node.open });
}

/** A line's row: the two ends and what it carries, and how many of its quests are open; the declarations as the drawing names them. */
export function listLine(t: TFunction, line: MapLine): string {
  switch (line.kind) {
    case 'quests': return t('map.list.quests', { from: line.edge.from, to: line.edge.to, count: line.edge.quests.length, open: line.edge.open });
    case 'asks': return t('map.list.asks', { to: line.edge.to, count: line.edge.quests.length, open: line.edge.open });
    default: return lineName(t, line);
  }
}
