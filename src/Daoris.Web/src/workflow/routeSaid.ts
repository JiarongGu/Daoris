import type { TFunction } from 'i18next';

// What chose a route's workflow, in the page's words (FABRIC-PIPELINE1a, the pipeline design §1.3): the start node's wires and a run's
// view say it alike, from the level the driver named (`WorkflowLevels`), as the run says why (the workflow design §4.1).

/** Whose choices a route reads: the repository where a repository's are read, and the workspace. */
export type RouteScope = { repository: string | null; workspace: string };

/**
 * The level that chose a route's workflow, in words. A level a newer driver names is said as nothing naming one, never dropped.
 *
 * @param kind The kind as the person named it, its label where it has one.
 * @param landed The landed route, which names no workflow where it names no verification.
 */
export function whySaid(t: TFunction, level: string, scope: RouteScope, kind: string | null, landed = false): string {
  const repository = scope.repository ?? '';
  const { workspace } = scope;
  switch (level) {
    case 'task': return t('workflow.start.why.task');
    case 'repository': return t('workflow.start.why.repository', { repository });
    case 'repository-kind': return t('workflow.start.why.repositoryKind', { repository, kind });
    case 'workspace': return t('workflow.start.why.workspace', { workspace });
    case 'workspace-kind': return t('workflow.start.why.workspaceKind', { workspace, kind });
    case 'repository-landed': return t('workflow.start.why.repositoryLanded', { repository });
    case 'workspace-landed': return t('workflow.start.why.workspaceLanded', { workspace });
    default: return t(landed ? 'workflow.start.why.noRoute' : 'workflow.start.why.current');
  }
}
