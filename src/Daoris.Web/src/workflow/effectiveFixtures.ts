import type { WorkflowEffective, WorkflowEffectiveCell, WorkflowStep } from './current';
import { MERGE_GRAPH, RELEASE_GRAPH } from './graphFixtures';

// What `WORKFLOW_CURRENT` answers as `effective` for a route (SETUPUX1b), for the place's stories and tests: Current's graph and a saved
// workflow's newest version, each node with the cell the driver reads for it (`WorkflowGraphRuns.Cell`, whose step ids are the nodes').

type Node = { id: string; kind: string; options?: Record<string, unknown> };

/** A node as the step the driver reads for it: the kind a step is drawn as, the person's part, who acts, and its press. */
export function cellOf(node: Node, level = 'repository'): WorkflowStep {
  const base = {
    id: node.id, source: { rule: 'workflow' as const, level: level as WorkflowStep['source']['level'] },
    settings: Object.fromEntries(Object.entries(node.options ?? {}).map(([name, value]) => [name, value as string])),
    runtime: 'built' as const, limit: null,
  };
  switch (node.kind) {
    case 'claimed': return { ...base, kind: 'work', participation: 'agent', executor: 'agent', press: null };
    case 'review': return { ...base, kind: 'review', participation: 'you', executor: 'daoris', press: 'accept' };
    case 'land': return {
      ...base, kind: 'landing', participation: 'automatic', executor: 'daoris', press: null,
      settings: { form: String(node.options?.method ?? 'merge'), accept: 'automatic' },
    };
    case 'check': return { ...base, kind: 'check', participation: 'automatic', executor: 'daoris', press: null };
    default: return node.kind.includes('/')
      ? { ...base, kind: node.kind, participation: 'automatic', executor: 'plugin', press: null, settings: { ...base.settings, plugin: node.kind.split('/')[0]! } }
      : { ...base, kind: node.kind, participation: 'automatic', executor: 'daoris', press: null };
  }
}

const cellsOf = (graph: { nodes: Node[] }): WorkflowEffectiveCell[] => graph.nodes.map((node) => ({ node: node.id, step: cellOf(node) }));

/** Default work, read from the rules as they stand: Current's graph, drawn as the Merge workflow's. */
export const EFFECTIVE_CURRENT: WorkflowEffective = {
  route: 'default', workflow: 'current', name: null, version: null, digest: '4740fb017006', schema: null,
  source: { level: 'current', kind: null, label: null, paths: [] }, verifies: false, graph: MERGE_GRAPH, cells: cellsOf(MERGE_GRAPH), limits: {}, problem: null,
};

/** Default work following the release workflow's second version, the shipped release graph. */
export const EFFECTIVE_RELEASE: WorkflowEffective = {
  route: 'default', workflow: 'release', name: 'Release for engine', version: 2, digest: '9f3c1e07aa21', schema: 2,
  source: { level: 'repository', kind: null, label: null, paths: [] }, verifies: false, graph: RELEASE_GRAPH, cells: cellsOf(RELEASE_GRAPH), limits: {}, problem: null,
};

/** Documentation work following a saved workflow by its workspace's kind. */
export const EFFECTIVE_DOCS: WorkflowEffective = {
  ...EFFECTIVE_RELEASE, route: 'kind:docs', workflow: 'docs-to-pr', name: 'Documents workflow', version: 1,
  source: { level: 'workspace-kind', kind: 'docs', label: 'Documentation', paths: ['docs/**'] }, graph: MERGE_GRAPH, cells: cellsOf(MERGE_GRAPH),
};

/** Landed work under Current: no verification, so nothing is drawn. */
export const EFFECTIVE_LANDED_NONE: WorkflowEffective = {
  route: 'landed', workflow: 'current', name: null, version: null, digest: null, schema: null,
  source: { level: 'current', kind: null, label: null, paths: [] }, verifies: false, graph: null, cells: [], limits: {}, problem: null,
};

/** Landed work following a verification workflow. */
export const EFFECTIVE_LANDED_VERIFIES: WorkflowEffective = {
  ...EFFECTIVE_RELEASE, route: 'landed', workflow: 'web-verify', name: 'Verify the line', version: 3, verifies: true,
  source: { level: 'repository-landed', kind: null, label: null, paths: [] }, graph: MERGE_GRAPH, cells: cellsOf(MERGE_GRAPH),
};

/** A saved workflow that no longer reads: the driver says why and draws nothing. */
export const EFFECTIVE_UNREAD: WorkflowEffective = {
  ...EFFECTIVE_RELEASE, graph: null, cells: [], schema: null, version: null, problem: '`release` keeps no version that reads here.',
};
