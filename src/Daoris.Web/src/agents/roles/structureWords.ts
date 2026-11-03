import type { TFunction } from 'i18next';
import type { GroupFold } from '../../design/groupFolds';
import { entryText } from './Roles';
import { type DiagramEdge, type DiagramNode } from './roleStructure';
import { nameOf, positionOf, stateText, workList } from './StructureSections';

// The words the structure's nodes and edges say (COMPANYVIEW1f, kept by CANVAS1c): a node's title and lines, and an edge as its source's
// foot says it and as its target hears it. Pure; the bands decide where they stand, never what they say.

/** A node as drawn: its title, its lines, and whether it is drawn dashed (a place or an agent as written, an agent not installed). */
export type Drawn = { id: string; kind: DiagramNode['kind'] | 'fold'; title: string; lines: string[]; dashed: boolean; fold: GroupFold | null };

export function drawnOf(t: TFunction, language: string, node: DiagramNode): Omit<Drawn, 'fold'> {
  switch (node.kind) {
    case 'place': {
      const lines: string[] = [];
      if (node.repository !== null && node.workspace !== null) lines.push(t('agents.roles.diagram.inWorkspace', { workspace: node.workspace }));
      if (!node.known) lines.push(t('agents.roles.diagram.unknownPlace'));
      else if (node.repository === null && node.workspace !== null) {
        if (node.ownRoles) lines.push(t('agents.roles.diagram.own'));
        if (node.counted > 0) lines.push(t(node.ownRoles ? 'agents.roles.diagram.countedHere' : 'agents.roles.diagram.counted', { count: node.counted }));
      }
      if (node.reasons.includes('held')) lines.push(t('agents.roles.held'));
      return { id: node.id, kind: 'place', title: node.repository ?? node.workspace ?? t('agents.roles.appointed.machine'), lines, dashed: !node.known };
    }
    case 'role': {
      const lines = [
        ...(node.takes.length > 0 ? [workList(t, language, node.takes)] : []),
        node.first
          ? `${entryText(t, node.first)}${node.more > 0 ? ` ${t('settings.ai.role.more', { count: node.more })}` : ''}`
          : t('agents.roles.choices.none'),
      ];
      return { id: node.id, kind: 'role', title: nameOf(t, language, node.role), lines, dashed: false };
    }
    case 'appointed':
      return {
        id: node.id, kind: 'appointed', title: t(`agents.roles.diagram.${node.role}`),
        lines: node.offered ? [] : [t('agents.roles.diagram.notOfferedHere')], dashed: false,
      };
    case 'agent': {
      const said = !node.known ? t('agents.roles.diagram.agentUnknown') : node.installed === false ? t('harness.absent') : node.phrase;
      return { id: node.id, kind: 'agent', title: node.product, lines: said ? [said] : [], dashed: !node.known || node.installed === false };
    }
  }
}

/** An edge as its source's foot says it. */
export function outText(t: TFunction, edge: DiagramEdge, title: (id: string) => string): string {
  if (edge.kind === 'placed') return t('agents.roles.diagram.edge.placed', { role: title(edge.to) });
  if (edge.kind === 'appointed') return t(edge.dashed ? 'agents.roles.diagram.edge.appointedDormant' : 'agents.roles.diagram.edge.appointed', { role: title(edge.to) });
  return t('agents.roles.diagram.edge.staffed', { agent: title(edge.to), position: positionOf(t, edge.position), state: stateText(t, edge.state) });
}

/** An edge as its target hears it. */
export function inText(t: TFunction, edge: DiagramEdge, title: (id: string) => string): string {
  if (edge.kind === 'placed') return t('agents.roles.diagram.edge.placedIn', { place: title(edge.from) });
  if (edge.kind === 'appointed') return t(edge.dashed ? 'agents.roles.diagram.edge.appointedAtDormant' : 'agents.roles.diagram.edge.appointedAt', { place: title(edge.from) });
  return t('agents.roles.diagram.edge.staffedBy', { role: title(edge.from), position: positionOf(t, edge.position) });
}
