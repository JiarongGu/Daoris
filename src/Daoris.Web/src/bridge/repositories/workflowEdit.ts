import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { doorLabel } from '../../agents/tools';
import { keys } from '../../data/queries';
import { sentence } from '../../locales/format';
import { fingerprintOf } from '../../workflow/draftFingerprint';
import type {
  EditorRole, StepStaffing, WorkflowListAnswer, WorkflowPaletteAnswer, WorkflowSaveAnswer, WorkflowSaveAsk, WorkflowShownAnswer, WorkflowUseAsk,
} from '../../workflow/edit/answers';
import type { ProposalShown } from '../../workflow/edit/proposalTake';
import type { WorkflowEditorBridge } from '../../workflow/edit/WorkflowEditor';
import { useHarnesses } from '../agents/agents';
import { useDraftProposals, useSettleHelp } from '../help';
import type { WorkflowPlanBridge } from '../../workflow/place/ChangeWorkflow';
import { useActors, useRoles, useRoleStructureChain, useSetRole } from '../driver';
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
 * is asked only while the editor shows; a save and a choice ask the reads again. `onPlan` (SETUPUX1p) reads the plan of a choice and
 * writes nothing; *Change…* sends it only where `WORKFLOW_LIST` announces it.
 */
export const useWorkflowEditor = (place: EditorPlace, { enabled = true }: { enabled?: boolean } = {}): WorkflowEditorBridge & WorkflowPlanBridge => {
  const client = useQueryClient();
  const [open, setOpen] = useState(false);
  const live = enabled && open;
  const [shownAsk, setShownAsk] = useState<{ id: string | null; version?: number }>({ id: null });
  const list = useWorkflowList({ enabled: live });
  const palette = useWorkflowPalette({ enabled: live });
  const shown = useWorkflowShown(shownAsk.id, shownAsk.version, { enabled: live });
  const chain = 'repository' in place ? { repository: place.repository, ...(place.workspace ? { workspace: place.workspace } : {}) } : { workspace: place.workspace };
  const structure = useRoleStructureChain(chain, undefined, { enabled: live });
  // The newest judgement is of one draft, by its fingerprint (FLOWCHART2c): the editor reads it only while the draft it holds is that one.
  const [judge, setJudge] = useState<{ answer: WorkflowSaveAnswer | null; of: string | null; failed: string | null; judging: boolean }>({
    answer: null, of: null, failed: null, judging: false,
  });
  const asked = useRef(0);
  const again = () => client.invalidateQueries({ queryKey: editKey() });
  const save = useMutation({ mutationFn: (ask: WorkflowSaveAsk) => askWorkflowSave(ask), onSuccess: again });
  const use = useMutation({ mutationFn: (ask: WorkflowUseAsk) => askWorkflowUse(ask), onSuccess: again });
  // A role covers this place when it is placed on this machine, in the place's workspace or in the place itself.
  const here = 'repository' in place ? { repository: place.repository, workspace: place.workspace ?? null } : { repository: null, workspace: place.workspace };
  const covers = (kind: string | null | undefined, id: string | null | undefined) =>
    !kind || kind === 'machine' || (kind === 'workspace' && id === here.workspace) || (kind === 'repository' && id === here.repository);
  const roles: EditorRole[] = (structure.data?.roles ?? [])
    .filter((role) => role.state !== 'removed' && role.state !== 'unreadable')
    .map((role) => ({
      id: role.id, name: role.name ?? null, takes: role.takes, scope: { kind: role.scopeKind ?? 'machine', id: role.scopeId ?? null }, revision: role.revision,
      covers: covers(role.scopeKind, role.scopeId),
    }));
  // The one role editor beside a step (SETUPUX1t): its reads are asked only while the editor shows, its press is `SET_ROLE`.
  const { t } = useTranslation();
  const roster = useHarnesses({ enabled: live });
  const catalogue = useRoles({ enabled: live });
  const actors = useActors({ enabled: live });
  const setRole = useSetRole();
  // A role changed changes what the roles' structure, the catalogue and the orders say, and what a step may name.
  const rolesAgain = () => {
    void client.invalidateQueries({ queryKey: [...keys.driver, 'roles'] });
    void client.invalidateQueries({ queryKey: [...keys.driver, 'actors'] });
  };
  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  const catalogueRead = catalogue.data ? 'ok' : catalogue.isError || catalogue.isSuccess ? 'failed' : 'loading';
  const staffing: StepStaffing = {
    catalogue: catalogue.data ?? null,
    read: catalogueRead,
    actors: actors.data ?? [],
    // An agent a person can name is one this machine has: a choice on one not installed would only hold the start.
    agents: harnesses.filter((door) => door.present).map((door) => ({ value: door.harness, label: doorLabel(t, door) })),
    offers: Object.fromEntries(harnesses.map((door) => [door.harness, door.settingsChoices ?? null])),
    busy: setRole.isPending,
    onSet: (press, answered) => setRole.mutate(press, {
      onSuccess: () => { rolesAgain(); answered.done(); },
      onError: (error) => answered.refused(sentence(error)),
    }),
  };
  // FLOWCHART2f: Ask Daoris's newest proposal against a draft of this place, and the presses that settle it, Ask Daoris's own.
  const drafts = useDraftProposals('repository' in place ? place.repository : place.workspace, live);
  const settle = useSettleHelp();
  const newest = drafts.at(-1);
  const card = newest?.proposal ?? null;
  const base = newest?.base ?? null;
  const made = newest?.maker ? JSON.stringify(newest.maker) : null;
  // One proposal object while its card, its base and its maker hold: the editor reckons its changes' closures from it.
  const proposal = useMemo((): ProposalShown | null => (card?.workflow ? {
    id: card.id, base, proposed: card.workflow.proposed ?? {}, changes: card.workflow.changes ?? [], why: card.why,
    problems: card.workflow.problems?.length ?? 0,
    maker: made === null ? null : JSON.parse(made) as ProposalShown['maker'],
  } : null), [card, base, made]);

  return {
    list: list.data ?? null,
    palette: palette.data ?? null,
    error: list.error ?? palette.error ?? null,
    roles,
    staffing,
    shown: shown.data ?? null,
    onShow: (id, version) => setShownAsk(version === undefined ? { id } : { id, version }),
    judged: judge.answer,
    judgedOf: judge.of,
    judgeFailed: judge.failed,
    judging: judge.judging,
    onJudge: (ask) => {
      const mine = ++asked.current;
      const of = fingerprintOf(ask.graph);
      setJudge((was) => ({ ...was, failed: null, judging: true }));
      // Only the newest request is heard: an older answer arriving late is dropped, whether it answered or failed.
      askWorkflowSave({ ...ask, plan: true }).then(
        (answer) => { if (mine === asked.current) setJudge({ answer, of, failed: null, judging: false }); },
        () => { if (mine === asked.current) setJudge((was) => ({ ...was, failed: of, judging: false })); },
      );
    },
    saving: save.isPending,
    onSave: (ask) => save.mutateAsync({ ...ask, plan: false }),
    using: use.isPending,
    onUse: (ask) => use.mutateAsync(ask),
    // A plan writes nothing, so it asks no read again.
    onPlan: (ask) => askWorkflowUse({ ...ask, plan: true }),
    onOpen: setOpen,
    proposal,
    onSettleProposal: (id, taken, left) => settle.mutateAsync({ id, apply: true, taken, left }),
    onDismissProposal: (id) => settle.mutateAsync({ id, apply: false }),
  };
};
