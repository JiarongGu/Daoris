import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import { askHeld, isPending } from '../state/held';

// The step editor's reads (WORKFLOWEDIT1a): the saved workflows with the places that choose them and the presets built in, one
// version as saved, and what a step graph may hold. The driver composes each from its own tables; the page lists and types them and
// derives nothing. A twin of the driver's `WorkflowOperations.Listing`, `Show` and `WorkflowPalette` (.claude/knowledge/twins.md).

/** A place that chooses a saved workflow: `repository` or `workspace`, its name, and the role it holds there. */
export type WorkflowChosen = { level: 'repository' | 'workspace'; name: string; role: 'default' | 'kind' | 'landed'; kind: string | null };

/** A saved workflow: its versions, and the newest version's schema (1 steps, 2 graph), digest and composed line. */
export type SavedWorkflow = {
  id: string; name: string | null; problem: string | null; versions: number[]; schema: 1 | 2 | null;
  digest: string | null; line: string | null; pipeline: string | null; chosen: WorkflowChosen[];
};

/** A preset built in, never stored until saved. */
export type PresetWorkflow = { id: string; name: string; graph: boolean; line: string; preset: true };

export type WorkflowListAnswer = { workflows: SavedWorkflow[]; presets: PresetWorkflow[] };

/** A problem the graph has as saved, by its code, with the wire or node it is about. */
export type GraphProblem = { code: string; wire: string | null; node: string | null; words: string };

/** One version as saved: `body` is its steps (schema 1) or its graph (schema 2) exactly as the file keeps it. */
export type WorkflowShownAnswer = {
  id: string; name: string; version: number; newest: boolean; versions: number[]; at: string | null; door: string | null;
  digest: string | null; schema: 1 | 2; line: string; body: unknown; problems: GraphProblem[];
};

export type OptionField = { name: string; type: 'choice' | 'text' | 'switch' | 'names'; choices: string[]; required: boolean };

/** A built-in kind with who reaches and fails it, what the person may do to it, and its options. */
export type PaletteKind = {
  kind: string; reach: string; reachBy: string; failureBy: string | null; personJudges: boolean; personSkips: boolean; mayEnd: boolean;
  evidenced: boolean; skipBy: string | null; options: OptionField[];
};

/** A wire kind: whether it makes its target eligible, and the kinds that may leave it (null: any node). */
export type PaletteWire = { kind: string; eligibility: boolean; from: string[] | null; to: string | null };

export type PaletteStep = {
  kind: string; reach: string; rejectable: boolean; skippable: boolean; inputs: Record<string, string>; outputs: Record<string, string>;
};

export type PalettePlugin = { plugin: string; steps: PaletteStep[] };

export type WorkflowPaletteAnswer = { kinds: PaletteKind[]; wires: PaletteWire[]; plugins: PalettePlugin[] };

const readList = (answer: unknown): WorkflowListAnswer =>
  isPending(answer) ? { workflows: [], presets: [] } : (answer as WorkflowListAnswer);
const readShown = (answer: unknown): WorkflowShownAnswer | null => (isPending(answer) ? null : (answer as WorkflowShownAnswer));
const readPalette = (answer: unknown): WorkflowPaletteAnswer | null => (isPending(answer) ? null : (answer as WorkflowPaletteAnswer));

const editKey = (...parts: unknown[]) => [...keys.driver, 'workflowEdit', ...parts] as const;

/** The saved workflows and the presets. Asked again each time the editor shows, since a save or a choice changes both. */
export const useWorkflowList = ({ enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = editKey('list');
  return useQuery({
    queryKey,
    queryFn: () => askHeld('WORKFLOW_LIST', client, queryKey, {}, readList),
    enabled: isAvailable && enabled,
    staleTime: 0,
  });
};

/** One version of a saved workflow as the file keeps it, the newest where `version` is not named. */
export const useWorkflowShown = (id: string | null, version?: number, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = editKey('show', id, version ?? null);
  return useQuery({
    queryKey,
    queryFn: () => askHeld('WORKFLOW_SHOW', client, queryKey, { id, ...(version === undefined ? {} : { version }) }, readShown),
    enabled: isAvailable && enabled && id !== null,
    staleTime: 0,
  });
};

/** What a step graph may hold: the driver's kinds, wires and the installed plugins' steps. */
export const useWorkflowPalette = ({ enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = editKey('palette');
  return useQuery({
    queryKey,
    queryFn: () => askHeld('WORKFLOW_PALETTE', client, queryKey, {}, readPalette),
    enabled: isAvailable && enabled,
    staleTime: 0,
  });
};
