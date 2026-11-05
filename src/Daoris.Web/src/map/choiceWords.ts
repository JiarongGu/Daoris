import type { TFunction } from 'i18next';
import type { CodeDependency } from '../data/api';
import { lineName, type MapSelection } from './MapLines';
import { lineCounts } from './LinesMenu';
import type { LineKind, Topology, When } from './topology';

/** What the choice's line says: the choice's identity, so it is said once per choice, and its words. */
export type Choice = { key: string; words: string };

/** The workspace map's choice in words: a repository and its first fact, or a line as it is named for a reader; `null` where nothing is chosen or the data no longer holds it. */
export function mapChoice(t: TFunction, topology: Topology, selected: MapSelection | null): Choice | null {
  if (selected === null) return null;
  switch (selected.kind) {
    case 'node': {
      const node = topology.nodes.find((each) => each.id === selected.id);
      if (!node) return null;
      const there = node.parked ? 'Parked' : node.working ? 'Working' : '';
      return { key: `node:${node.id}`, words: t(`map.said.node${there}`, { repository: node.id, count: node.open }) };
    }
    case 'quests': {
      const edge = topology.quests.find((each) => each.from === selected.from && each.to === selected.to);
      return edge ? { key: `quests:${edge.from}>${edge.to}`, words: lineName(t, { kind: 'quests', edge }) } : null;
    }
    case 'asks': {
      const edge = topology.asks.find((each) => each.to === selected.to);
      return edge ? { key: `asks:${edge.to}`, words: lineName(t, { kind: 'asks', edge }) } : null;
    }
    case 'chains': {
      const edge = topology.chains.find((each) => each.from === selected.from && each.to === selected.to);
      return edge ? { key: `chains:${edge.from}>${edge.to}`, words: lineName(t, { kind: 'chains', edge }) } : null;
    }
    case 'depends': {
      const edge = topology.depends.find((each) => each.from === selected.from && each.to === selected.to);
      return edge ? { key: `depends:${edge.from}>${edge.to}`, words: lineName(t, { kind: 'depends', edge }) } : null;
    }
    case 'knowledge': {
      const edge = topology.knowledge.find((each) => each.a === selected.a && each.b === selected.b);
      return edge ? { key: `knowledge:${edge.a}~${edge.b}`, words: lineName(t, { kind: 'knowledge', edge }) } : null;
    }
  }
}

/**
 * What the line under the drawing says with nothing chosen (MAPUX1f): why the map shows no connection where it shows none, else the
 * hint. Hidden kinds come first, then a *Which quests* that leaves none (with what *All of them* would show), then no work yet moved.
 */
export function mapHint(t: TFunction, facts: {
  /** What the map holds under the person's choice of quests, before the kinds they hid. */
  topology: Topology;
  shown: ReadonlySet<LineKind>;
  when: When;
  /** The quests on the map's lines under *All of them*. */
  everything: number;
  hint: string;
}): string {
  const { topology, shown, when, everything, hint } = facts;
  if (shown.size === 0) return t('map.said.allHidden');
  const counts = lineCounts(topology);
  if (when !== 'all' && counts.quests + counts.asks + counts.chains === 0 && everything > 0) {
    return t(`map.said.none${when === 'open' ? 'Open' : when === 'week' ? 'Week' : 'Month'}`, { count: everything });
  }
  if (Object.values(counts).every((count) => count === 0)) return t('map.said.noConnections');
  return hint;
}

/** The code map's choice in words: a module, what it uses and what uses it. */
export function codeChoice(t: TFunction, dependencies: readonly CodeDependency[], selected: string | null): Choice | null {
  if (selected === null) return null;
  const uses = dependencies.filter((each) => each.from === selected).length;
  const usedBy = dependencies.filter((each) => each.to === selected).length;
  return {
    key: `module:${selected}`,
    words: uses + usedBy === 0 ? t('code.said.alone', { id: selected }) : t('code.said.module', { id: selected, uses, usedBy }),
  };
}
