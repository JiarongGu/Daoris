// A release step's target as the window chooses it:what the Azure DevOps plugin's `discover-releases` found, read
// defensively, and the values the one door (`SET_WORKFLOW_VALUES`, twin `daoris workflow values`) sets on each release step of a workflow.
import { holdsIt, type RunHolder } from '../run';

/** The actions a release step takes on its target, the plugin's own (`target.mjs`' ACTIONS). */
export const RELEASE_ACTIONS = ['observe', 'start-stage', 'create-and-start'] as const;
export type ReleaseAction = typeof RELEASE_ACTIONS[number];

export type FoundStage = { id: number; name: string; after: string[]; deploysOn: string };
export type FoundDefinition = { id: number; name: string; url: string; triggers: string[]; stages: FoundStage[] };
export type FoundReleases = { url: string; pipelines: { id: number; url: string; definitions: FoundDefinition[] }[] };

/** A target chosen: the pipeline whose builds it releases, the definition, its stage and the action. */
export type ReleaseTargetChoice = { pipeline: number; releaseDefinition: number; stage: number; action: ReleaseAction };

const numberOf = (value: unknown): number | null => (typeof value === 'number' && Number.isInteger(value) ? value : null);
const list = (value: unknown): unknown[] => (Array.isArray(value) ? value : []);
const text = (value: unknown): string => (typeof value === 'string' ? value : '');

/** The round's `data` as discovery answers it; anything else found nothing. */
export function foundOf(raw: unknown): FoundReleases {
  const data = raw !== null && typeof raw === 'object' ? raw as Record<string, unknown> : {};
  return {
    url: text(data.url),
    pipelines: list(data.pipelines).flatMap((each) => {
      const pipeline = each as Record<string, unknown>;
      const id = numberOf(pipeline.id);
      if (id === null) return [];
      return [{ id, url: text(pipeline.url), definitions: list(pipeline.definitions).flatMap((one) => {
        const definition = one as Record<string, unknown>;
        const of = numberOf(definition.id);
        return of === null ? [] : [{
          id: of, name: text(definition.name), url: text(definition.url), triggers: list(definition.triggers).map(text),
          stages: list(definition.stages).flatMap((stage) => {
            const read = stage as Record<string, unknown>;
            const at = numberOf(read.id);
            return at === null ? [] : [{ id: at, name: text(read.name), after: list(read.after).map(text), deploysOn: text(read.deploysOn) }];
          }),
        }];
      }) }];
    }),
  };
}

/** The values the door sets, each option's JSON text. */
export const targetValues = (choice: ReleaseTargetChoice): Record<string, string> => ({
  pipeline: String(choice.pipeline), releaseDefinition: String(choice.releaseDefinition), stage: String(choice.stage), action: JSON.stringify(choice.action),
});

/** Whether a node's kind is one of a plugin's release steps (`<plugin>/release`, `<plugin>/release-*`), which read the target. */
export const isReleaseKind = (kind: string | null | undefined): boolean => /^[^/]+\/release(-[a-z]+)?$/.test(kind ?? '');

/** The ids of a workflow's release steps, in their order: each takes the same target. */
export const releaseNodesOf = (nodes: { id: string; kind: string }[]): string[] => nodes.filter((node) => isReleaseKind(node.kind)).map((node) => node.id);

/** The plugin's approval command: the approver role's act, the person's press or the holding agent's tool. */
export const APPROVE_COMMAND = 'approve-release';

/**
 * Whether the card may approve here: the plugin declares its approval command and the person holds the approver role. Where an agent
 * holds it, the command is that agent's tool, so the card opens the system's page instead.
 */
export const canApproveHere = (commands: { name: string; tool: boolean }[], holder: RunHolder | null | undefined): boolean =>
  holdsIt(holder) && commands.some((command) => command.name === APPROVE_COMMAND);

/** The release and the stage an approval wait asks of (`approve <stage> of <release>` and the page's `releaseId`), or null for another wait. */
export function approvalOf(words: string | null | undefined, url: string | null | undefined): { release: string; stage: string } | null {
  const stage = /\bapprove (.+?) of [^,]+,/.exec(words ?? '')?.[1];
  const release = /[?&]releaseId=(\d+)/.exec(url ?? '')?.[1];
  return stage && release ? { release, stage } : null;
}

/** The plugin that serves a release step's kind. */
export const pluginOfKind = (kind: string): string => kind.split('/')[0] ?? '';
