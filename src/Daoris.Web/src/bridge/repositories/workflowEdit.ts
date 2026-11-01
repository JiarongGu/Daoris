import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { useRef, useState } from 'react';
import { keys } from '../../data/queries';
import type {
  EditorRole, WorkflowListAnswer, WorkflowPaletteAnswer, WorkflowSaveAnswer, WorkflowSaveAsk, WorkflowShownAnswer, WorkflowUseAsk,
} from '../../workflow/edit/answers';
import type { WorkflowEditorBridge } from '../../workflow/edit/WorkflowEditor';
import { useRoleStructureChain } from '../driver';
import { askHeld, isPending } from '../state/held';
import { askWorkflowSave, askWorkflowUse } from './workflow';

// The step editor's reads (WORKFLOWEDIT1a): the saved workflows with the places that choose them and the presets built in, one
// version as saved, and what a step graph may hold; and its presses (WORKFLOWEDIT1b): a version judged as it is drawn, saved and used
// for a place. The driver composes each from its own tables; the page lists and types them and derives nothing. The answers' types
// are the editor's own (`workflow/edit/answers.ts`), a twin of the driver's (.claude/knowledge/twins.md).

export type {
  EditorRole, GraphProblem, OptionField, PaletteKind, PalettePlugin, PaletteStep, PaletteWire, PresetWorkflow, SavedWorkflow, WorkflowChosen,
  WorkflowListAnswer, WorkflowPaletteAnswer, WorkflowReachRow, WorkflowSaveAnswer, WorkflowSaveAsk, WorkflowScopeRow, WorkflowShownAnswer,
  WorkflowUseAnswer, WorkflowUseAsk,
} from '../../workflow/edit/answers';

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

/** The place the step editor opened from: a repository, with its workspace where known, or a workspace. */
export type EditorPlace = { repository: string; workspace?: string | null } | { workspace: string };

/**
 * **The step editor's bridge** (WORKFLOWEDIT1b): its three reads, the working roles a step may name from the roles' structure at the
 * place it opened from, a planned save judging each change (the newest asked is the one answered), the save, and *Use here*. Every read
 * is asked only while the editor shows; a save and a choice ask the reads again.
 */
export const useWorkflowEditor = (place: EditorPlace, { enabled = true }: { enabled?: boolean } = {}): WorkflowEditorBridge => {
  const client = useQueryClient();
  const [open, setOpen] = useState(false);
  const live = enabled && open;
  const [shownId, setShownId] = useState<string | null>(null);
  const list = useWorkflowList({ enabled: live });
  const palette = useWorkflowPalette({ enabled: live });
  const shown = useWorkflowShown(shownId, undefined, { enabled: live });
  const chain = 'repository' in place ? { repository: place.repository, ...(place.workspace ? { workspace: place.workspace } : {}) } : { workspace: place.workspace };
  const structure = useRoleStructureChain(chain, undefined, { enabled: live });
  const [judge, setJudge] = useState<{ answer: WorkflowSaveAnswer | null; judging: boolean }>({ answer: null, judging: false });
  const asked = useRef(0);
  const again = () => client.invalidateQueries({ queryKey: editKey() });
  const save = useMutation({ mutationFn: (ask: WorkflowSaveAsk) => askWorkflowSave(ask), onSuccess: again });
  const use = useMutation({ mutationFn: (ask: WorkflowUseAsk) => askWorkflowUse(ask), onSuccess: again });
  const roles: EditorRole[] = (structure.data?.roles ?? [])
    .filter((role) => role.state !== 'removed' && role.state !== 'unreadable')
    .map((role) => ({ id: role.id, name: role.nameEn && role.nameZh ? { en: role.nameEn, zh: role.nameZh } : null, takes: role.takes }));

  return {
    list: list.data ?? null,
    palette: palette.data ?? null,
    error: list.error ?? palette.error ?? null,
    roles,
    shown: shown.data ?? null,
    onShow: setShownId,
    judged: judge.answer,
    judging: judge.judging,
    onJudge: (ask) => {
      const mine = ++asked.current;
      setJudge((was) => ({ ...was, judging: true }));
      askWorkflowSave({ ...ask, plan: true }).then(
        (answer) => { if (mine === asked.current) setJudge({ answer, judging: false }); },
        () => { if (mine === asked.current) setJudge((was) => ({ ...was, judging: false })); },
      );
    },
    saving: save.isPending,
    onSave: (ask) => save.mutateAsync({ ...ask, plan: false }),
    using: use.isPending,
    onUse: (ask) => use.mutateAsync(ask),
    onOpen: setOpen,
  };
};
