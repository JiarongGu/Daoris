import { type KeyboardEvent, useEffect, useId, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Drawer, Inline, Prose, SkeletonRows } from '../../design/ui';
import { useWidth } from '../../design/useWidth';
import { isComposing } from '../../lib/composing';
import { useDebounced } from '../../lib/useDebounced';
import { fillAskDaoris, useCanFillAsk } from '../../help/askFill';
import { registerOpenDraft } from '../../help/openDraft';
import { sentence } from '../../locales/format';
import { fingerprintOf } from '../draftFingerprint';
import { EDIT_EVENT } from '../../menus/editing';
import { isTextField } from '../../menus/press';
import type { ChartEdit } from '../chart/FlowChart';
import { isHoldKind } from '../graphFlow';
import type {
  EditorRole, GraphProblem, WorkflowListAnswer, WorkflowPaletteAnswer, WorkflowSaveAnswer, WorkflowSaveAsk, WorkflowShownAnswer,
  WorkflowUseAnswer, WorkflowUseAsk,
} from './answers';
import { type Begin, draftFlow, EditCanvas } from './EditCanvas';
import { carriedSaid, plain, rowSaid, stepTitle } from './editWords';
import {
  canInsertOn, type Carried, type Clip, commit, connect, copySteps, draftOf, duplicateStep, emptyDraft, entriesOf, type GraphDraft, graphOf, historyOf,
  type InsertAt, insertStep, joinOf, moveInChain, movePlan, pasteSteps, redo, removeStep, removeWire, retarget, type StepChoice, targetsOf,
  undo, type WayRowKey, workflowIdFor,
} from './graphDraft';
import { type Confirming, Inspector, type InspectorActs } from './Inspector';
import { presetName } from './presetWords';
import { type ChangeRow, ChangeList, changeSaid, changeStep, MARK_SIGN, withChanges } from './ChangeList';
import { ProposalBar } from './ProposalBar';
import {
  closureOf, type Decided, decide, everyNamed, leftWith, needsOf, type ProposalShown, staleOf, take as takeInto, UNDECIDED, unionOf,
} from './proposalTake';
import { ToPutRight } from './Refusals';
import { withRefusals } from './refusalMarks';
import { type SaveTarget, SaveReview } from './SaveReview';
import { StepPicker } from './StepPicker';

/** What the step editor's bridge hands it (`bridge/repositories/workflowEdit.ts`): the reads, the roles, the judge, the save and the choice. */
export type WorkflowEditorBridge = {
  /** The saved workflows and the presets; null while read. */
  list: WorkflowListAnswer | null;
  /** What a graph may hold; null while read. */
  palette: WorkflowPaletteAnswer | null;
  /** A read refused, said in its place. */
  error: unknown;
  /** The working roles a step may name. */
  roles: EditorRole[];
  /** The saved version last asked for by `onShow`, once it is answered. */
  shown: WorkflowShownAnswer | null;
  onShow: (id: string | null) => void;
  /** The newest planned save's answer, and whether one is on its way. */
  judged: WorkflowSaveAnswer | null;
  /** The fingerprint of the draft `judged` answered (`draftFingerprint.ts`); absent, the answer is not known to be of any draft. */
  judgedOf?: string | null;
  /** The fingerprint of the draft whose newest request failed. */
  judgeFailed?: string | null;
  judging: boolean;
  onJudge: (ask: WorkflowSaveAsk) => void;
  saving: boolean;
  onSave: (ask: WorkflowSaveAsk) => Promise<WorkflowSaveAnswer>;
  using: boolean;
  onUse: (ask: WorkflowUseAsk) => Promise<WorkflowUseAnswer>;
  /** Told when the editor opens and closes, so its reads are asked only while it shows. */
  onOpen?: (open: boolean) => void;
  /** Ask Daoris's newest proposal against a draft of this place (FLOWCHART2f), as the bridge read it; none where none waits. */
  proposal?: ProposalShown | null;
  /** The press naming each change of it taken or left, which settles it (`HELP_APPLY` with `taken` and `left`). */
  onSettleProposal?: (id: string, taken: readonly string[], left: readonly string[]) => Promise<unknown>;
  /** *Not now* on the proposal whole (`HELP_DISMISS`). */
  onDismissProposal?: (id: string) => Promise<unknown>;
};

export type WorkflowEditorProps = WorkflowEditorBridge & {
  /** Where *Use here* chooses the saved version: the page the editor opened from. */
  place: { repository: string } | { workspace: string };
  /** The steps it opens on, a graph as a version holds it, and the saved workflow and version they are, where they are one. */
  startFrom?: { graph: unknown; id?: string; name?: string; base?: number | null };
  /** The route and the pipeline the place opened it for, where the caller knows them: Ask Daoris is told them with the draft. */
  route?: string | null;
  pipeline?: 'development' | 'verification' | null;
  /** The lines the place declares, which a landing's target is chosen from; none, and the target is typed. */
  lines?: string[];
  onClose?: () => void;
};

/** How long a change holds still before it is judged: each judge is a planned save, and typing a name is many changes. */
const JUDGE_MS = 250;

/** The id a draft is judged under before it has one: a plan writes nothing, so it only has to be an id. */
const DRAFT_ID = 'draft';

/** The editor's region holds the chart and the inspector side by side from 64rem (the flowchart design §2, §8.8); else a drawer. */
const BESIDE = 64 * 16;

/** The id the drawing gives the folded holds, by their first step (`graphFlow`). */
const HOLDS = 'holds:';

/** The focus asked for the first step that can take the way being chosen. */
const PICKABLE = ':pickable';

/** The focus asked back for the control the picker was opened from. */
const RETURN = ':return';

/**
 * One step of the editor's history: the draft, the words of the change that made it, and what of each of Ask Daoris's proposals was taken
 * and left by then, so one undo takes back a take and its record together (D223 §8.6).
 */
type Snapshot = { draft: GraphDraft; said: string; decided?: Readonly<Record<string, Decided>> };

/** The settling press of a proposal: on its way, answered, or refused in the driver's words. */
type Settling = { state: 'pending' | 'told' } | { state: 'failed'; why: string };

/** A way being chosen on the chart: the step and the row it leaves by. */
type Choosing = { from: string; row: WayRowKey };

type Said = { saved: WorkflowSaveAnswer | null; used: WorkflowUseAnswer | null; failed: string | null };
const QUIET: Said = { saved: null, used: null, failed: null };

/** What the save is aimed at, and the id a new one was copied from (a preset's, or a saved workflow's). */
type Target = SaveTarget & { from: string | null };

/**
 * **The workflow editor** (WORKFLOWEDIT1b; FLOWCHART2b, D223 §3): a workflow composed on its chart. A step is added by a way's ＋, a ◎'s,
 * its + key or the picker; a way's target is chosen in the selected step's *What happens next*, by a drag from a port or by picking on the
 * chart, and its kind is what the row and the target give, never named. A step moves within its chain, is duplicated, copied and pasted,
 * and removed alone or joined; every change is one step of history, undone and redone by the Edit menu and its keys, and said aloud.
 * Each change is judged by a planned save, each refusal said with a door to its step. The inspector stands beside the chart where the
 * editor's region holds both, and in a drawer where it does not.
 *
 * A molecule: the answers arrive as props, the draft and its history are page state, and every press goes out.
 */
export function WorkflowEditor(props: WorkflowEditorProps) {
  const { t } = useTranslation();
  const region = useRef<HTMLDivElement>(null);
  const width = useWidth(region);
  const { list, palette, error } = props;
  return (
    <div ref={region} data-editor-region="" className="min-w-0">
      {palette && list
        ? <Composing {...props} list={list} palette={palette} wide={width !== undefined && width >= BESIDE} />
        : (
          <div aria-busy={!error || undefined} className="grid gap-2">
            <Prose className="text-small">{error ? t('workflow.edit.unread', { why: sentence(error) }) : t('workflow.edit.reading')}</Prose>
            {!error && <SkeletonRows rows={3} />}
          </div>
        )}
    </div>
  );
}

/** The editor once its reads are answered, told whether its region holds the inspector beside the chart. */
function Composing({
  list, palette, roles, shown, onShow, judged, judgedOf = null, judgeFailed = null, judging, onJudge, saving, onSave, using, onUse, place, startFrom,
  route = null, pipeline = null, lines, onClose, wide, proposal: offered = null, onSettleProposal, onDismissProposal,
}: WorkflowEditorProps & { list: WorkflowListAnswer; palette: WorkflowPaletteAnswer; wide: boolean }) {
  const { t, i18n } = useTranslation();
  const headId = useId();
  const canAsk = useCanFillAsk();
  const inspectorId = useId();
  const region = useRef<HTMLElement>(null);
  // Where the focus goes back to when the picker is given up: a dialog with no trigger of its own gives back none.
  const returnTo = useRef<HTMLElement | null>(null);

  const [history, setHistory] = useState(() => historyOf<Snapshot>({ draft: startFrom ? draftOf(startFrom.graph) : emptyDraft(), said: '' }));
  const draft = history.present.draft;
  const [chosen, setChosen] = useState<string | null>(null);
  const [drawer, setDrawer] = useState(false);
  const [picker, setPicker] = useState<InsertAt | null>(null);
  const [choosing, setChoosing] = useState<Choosing | null>(null);
  const [confirming, setConfirming] = useState<Confirming>(null);
  const [clip, setClip] = useState<Clip | null>(null);
  const [heard, setHeard] = useState('');
  const [waiting, setWaiting] = useState<string | null>(null);
  const [target, setTarget] = useState<Target>(() => ({
    id: startFrom?.id ?? '', name: startFrom?.name ?? '', base: startFrom ? startFrom.base ?? null : null, from: startFrom?.id ?? null,
  }));
  const [authorized, setAuthorized] = useState(false);
  const [holdsOpen, setHoldsOpen] = useState(false);
  const [said, setSaid] = useState<Said>(QUIET);
  const [focusing, setFocusing] = useState<{ at: string; n: number } | null>(null);

  const selected = chosen !== null && draft.nodes.some((node) => node.id === chosen) ? chosen : null;

  // A saved version to begin from, once its answer lands; a version of steps (schema 1) is no graph to compose, and leaves the draft as it was.
  useEffect(() => {
    if (waiting === null || shown?.id !== waiting) return;
    setWaiting(null);
    if (shown.schema !== 2) return;
    const words = t('workflow.edit.said.began', { name: shown.name });
    setHistory((was) => commit(was, { ...was.present, draft: draftOf(shown.body), said: words }));
    setHeard(words);
    setTarget({ id: shown.id, name: shown.name, base: shown.version, from: shown.id });
  }, [waiting, shown, t]);

  // The focus a change asks for, given once the dialogs it closed have let theirs go: a step by its id, the first that can take the way
  // being chosen, or the control the picker was opened from.
  useEffect(() => {
    if (!focusing) return undefined;
    const timer = window.setTimeout(() => {
      if (focusing.at === RETURN) {
        if (returnTo.current?.isConnected) returnTo.current.focus();
        return;
      }
      const nodes = [...(region.current?.querySelectorAll<HTMLElement>('[data-flow-node]') ?? [])];
      const found = focusing.at === PICKABLE ? nodes.find((node) => node.dataset.picking === 'true') : nodes.find((node) => node.dataset.flowNode === focusing.at);
      found?.focus();
    }, 0);
    return () => window.clearTimeout(timer);
  }, [focusing]);
  const focusOn = (at: string) => setFocusing((was) => ({ at, n: (was?.n ?? 0) + 1 }));

  // Each change judged once it holds still, as the driver would save it, under the id it would be saved as.
  const graph = useMemo(() => graphOf(draft), [draft]);
  const fingerprint = useMemo(() => fingerprintOf(graph), [graph]);
  const newId = workflowIdFor(target.name, target.from, list?.workflows.map((each) => each.id) ?? []);
  const saveId = target.base === null ? newId : target.id;
  const ask = useMemo((): WorkflowSaveAsk => ({
    id: saveId || DRAFT_ID, graph, ...(target.base === null ? {} : { base: target.base }),
  }), [graph, saveId, target.base]);
  const settled = useDebounced(ask, JUDGE_MS);
  const judge = useRef(onJudge);
  judge.current = onJudge;
  useEffect(() => { if ((settled.graph as { nodes: unknown[] }).nodes.length > 0) judge.current(settled); }, [settled]);

  // Edit's undo, redo, copy and paste, heard as the event the menu sends a region with a history of its own.
  const menu = useRef<Partial<Record<string, () => void>>>({});
  useEffect(() => {
    const element = region.current;
    if (!element) return undefined;
    const hear = (event: Event) => {
      const run = menu.current[(event as CustomEvent<{ act?: string }>).detail?.act ?? ''];
      if (!run) return;
      event.preventDefault();
      run();
    };
    element.addEventListener(EDIT_EVENT, hear);
    return () => element.removeEventListener(EDIT_EVENT, hear);
  }, []);

  const nodeOf = (id: string) => draft.nodes.find((node) => node.id === id);
  const titleOf = (id: string) => {
    const node = nodeOf(id);
    return node ? stepTitle(t, i18n, palette, node) : id;
  };
  const wireOf = (id: string) => draft.wires.find((wire) => wire.id === id);

  /** One change: a step of history, said aloud, and what it made stale let go. */
  const change = (next: GraphDraft, words: string) => {
    setHistory((was) => commit(was, { ...was.present, draft: next, said: words }));
    setHeard(words);
    setSaid(QUIET);
    setConfirming(null);
  };
  const undoChange = () => {
    if (history.past.length === 0) return;
    setHeard(t('workflow.edit.said.undone', { what: history.present.said }));
    setHistory(undo);
    setChoosing(null);
    setConfirming(null);
    setSaid(QUIET);
  };
  const redoChange = () => {
    const next = history.future[0];
    if (!next) return;
    setHeard(t('workflow.edit.said.redone', { what: next.said }));
    setHistory(redo);
    setChoosing(null);
    setConfirming(null);
    setSaid(QUIET);
  };

  // The drawing's nodes as the draft's steps: a step is itself, the folded holds their run of steps from the first, a ◎ none.
  const holdsRun = (id: string): string[] => {
    const first = id.slice(HOLDS.length);
    const count = flow?.nodes.find((node) => node.id === id)?.step?.group?.count ?? 1;
    const run = [first];
    while (run.length < count) {
      const on = draft.wires.find((wire) => wire.from === run[run.length - 1] && wire.kind === 'after');
      if (!on) break;
      run.push(on.to);
    }
    return run;
  };
  const stepsAt = (id: string): string[] => (nodeOf(id) ? [id] : id.startsWith(HOLDS) ? holdsRun(id) : []);

  const words = (from: string, row: WayRowKey, to: string) => t('workflow.edit.said.way', { row: rowSaid(t, row), from: titleOf(from), to: titleOf(to) });
  /** A change's words, with the inputs it proposed and the ones it leaves to ask about (SETUPUX1r). */
  const carried = (said: string, next: GraphDraft, proposed: Carried[], steps: string[]) =>
    [said, carriedSaid(t, i18n, palette, next, proposed, steps)].filter(Boolean).join(' ');
  const select = (id: string) => {
    setChosen(id);
    setConfirming(null);
    if (!wide) setDrawer(true);
  };
  const openPicker = (at: InsertAt) => {
    returnTo.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setChoosing(null);
    setPicker(at);
  };
  const closePicker = () => {
    setPicker(null);
    focusOn(RETURN);
  };
  const closeDrawer = () => {
    setDrawer(false);
    if (selected) focusOn(selected);
  };
  const pickedStep = (choice: StepChoice) => {
    const at = picker;
    setPicker(null);
    if (!at) return;
    const made = insertStep(palette, draft, choice, at);
    if (made.draft === draft) return;
    change(made.draft, carried(t('workflow.edit.said.added', { step: stepTitle(t, i18n, palette, choice) }), made.draft, made.carried, [made.id]));
    setDrawer(false);
    setChosen(made.id);
    focusOn(made.id);
  };
  const afterOf = (id: string): InsertAt => {
    if (id.startsWith(HOLDS)) return { after: holdsRun(id).at(-1)! };
    const none = /^(.+):none:(.+)$/.exec(id);
    return none ? { from: none[1]!, row: `outcome:${none[2]!}` } : { after: id };
  };

  // Choosing a way's target on the chart: only the steps that can take it are offered, and the focus goes to the first.
  const choose = (from: string, row: WayRowKey) => {
    const offered = targetsOf(palette, draft, from, row);
    const named = { row: rowSaid(t, row), step: titleOf(from) };
    if (offered.length === 0) { setHeard(t('workflow.edit.pick.none', named)); return; }
    setDrawer(false);
    setConfirming(null);
    if (choosing?.from === from && choosing.row === row) return;
    setChoosing({ from, row });
    setHeard(t('workflow.edit.pick.started', { ...named, count: offered.length }));
    focusOn(PICKABLE);
  };
  const takers = new Set(choosing ? targetsOf(palette, draft, choosing.from, choosing.row).map((each) => each.id) : []);
  // The folded holds take a way by their first step, the one a way into the group reaches.
  const takerAt = (id: string) => {
    const first = stepsAt(id)[0];
    return first !== undefined && takers.has(first) ? first : null;
  };
  const take = (id: string) => {
    if (!choosing) return;
    const to = takerAt(id);
    const made = to === null ? null : connect(palette, draft, choosing.from, choosing.row, to);
    if (!made || to === null) return;
    change(made.draft, carried(words(choosing.from, choosing.row, to), made.draft, made.carried, [to]));
    setChoosing(null);
  };
  const giveUp = () => {
    if (!choosing) return;
    const from = choosing.from;
    setChoosing(null);
    setHeard(t('workflow.edit.pick.cancelled'));
    focusOn(from);
  };

  const remove = (id: string, options: { join: boolean; end?: string }) => {
    const join = options.join ? joinOf(draft, id) : null;
    const before = draft.wires.find((wire) => wire.to === id && wire.from !== null && wire.kind !== 'back')?.from ?? null;
    change(removeStep(draft, id, options), join
      ? t('workflow.edit.said.joined', { step: titleOf(id), from: titleOf(join.from), to: titleOf(join.to) })
      : t('workflow.edit.said.removed', { step: titleOf(id) }));
    setChosen(null);
    setDrawer(false);
    const next = join?.from ?? before ?? draft.nodes.find((node) => node.id !== id)?.id;
    if (next) focusOn(next);
  };
  const duplicate = (id: string) => {
    const made = duplicateStep(draft, id);
    change(made.draft, t('workflow.edit.said.duplicated', { step: titleOf(id) }));
    setChosen(made.id);
    setDrawer(false);
    focusOn(made.id);
  };
  const copy = (id: string | null) => {
    if (id === null) return;
    setClip(copySteps(draft, stepsAt(id)));
    setHeard(t('workflow.edit.said.copied', { step: titleOf(id) }));
  };
  const paste = () => {
    if (!clip || clip.nodes.length === 0) { setHeard(t('workflow.edit.said.nothingCopied')); return; }
    const made = pasteSteps(draft, clip);
    const steps = clip.nodes.map((node) => stepTitle(t, i18n, palette, node)).join(t('workflow.listJoin'));
    change(made.draft, t('workflow.edit.said.pasted', { steps, dropped: made.dropped }));
    setChosen(made.ids[0] ?? null);
    focusOn(made.ids[0] ?? '');
  };
  const askMove = (id: string, dir: 'earlier' | 'later') => {
    if (!movePlan(draft, id, dir)) { setHeard(t('workflow.edit.move.cannot')); return; }
    setChosen(id);
    setConfirming(dir);
    if (!wide) setDrawer(true);
  };
  const askRemove = (id: string) => {
    setChosen(id);
    setConfirming('remove');
    if (!wide) setDrawer(true);
  };
  menu.current = { undo: undoChange, redo: redoChange, copy: () => copy(selected), paste };

  // The keys (D223 §3): undo and redo outside a field; on a step, + or Insert to add a step after it, / for the picker, Delete to remove,
  // Alt+↑ and Alt+↓ to move within its chain, Ctrl+C and Ctrl+V to copy and paste. A key aimed at a field is the field's own.
  const onKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (isComposing(event) || event.defaultPrevented) return;
    const aimed = event.target as HTMLElement;
    if (isTextField(aimed) || aimed.closest('[role="listbox"], [role="option"]')) return;
    const ctrl = event.ctrlKey || event.metaKey;
    const key = event.key.toLowerCase();
    const run = (act: () => void) => { event.preventDefault(); act(); };
    if (ctrl && key === 'z') { run(event.shiftKey ? redoChange : undoChange); return; }
    if (ctrl && key === 'y') { run(redoChange); return; }
    if (ctrl && key === 'v') { run(paste); return; }
    const at = aimed.closest<HTMLElement>('[data-flow-node]')?.dataset.flowNode;
    if (event.key === '/' && !ctrl) { run(() => openPicker(selected ? { after: selected } : { free: true })); return; }
    if (!at || choosing) return;
    const own = stepsAt(at);
    if (ctrl && key === 'c') run(() => copy(at));
    else if ((event.key === '+' || event.key === 'Insert') && !ctrl) run(() => openPicker(afterOf(at)));
    else if (event.key === 'Delete' && own.length === 1) run(() => askRemove(own[0]!));
    else if (event.altKey && (event.key === 'ArrowUp' || event.key === 'ArrowDown') && own.length === 1) {
      run(() => askMove(own[0]!, event.key === 'ArrowUp' ? 'earlier' : 'later'));
    }
  };

  // The judgement (FLOWCHART2c): the newest answer, only while it is of the draft held now. An edit makes it invalid at once, before the
  // change is asked; a failed request leaves it so; undoing to the draft it judged takes it back.
  const judgement = judged && judgedOf === fingerprint ? judged : null;
  const unjudged = judgement === null && judgeFailed === fingerprint && !judging && settled === ask;
  // Each refusal at the step or the way it names, and a door from the head to that step.
  const problems = draft.nodes.length > 0 && judgement && !judgement.done ? judgement.problems ?? [] : [];
  const stepOf = (problem: GraphProblem): string | null => {
    const wire = problem.wire ? wireOf(problem.wire) : undefined;
    if (wire?.from && nodeOf(wire.from)) return wire.from;
    return problem.node && nodeOf(problem.node) ? problem.node : null;
  };
  // Ask Daoris is told the draft open here (FLOWCHART2d; D223 §8.3), registered while the editor is mounted and let go when it closes.
  const placeName = 'repository' in place ? place.repository : place.workspace;
  const source = target.base !== null ? `${target.id}@v${target.base}` : target.from ? `preset:${target.from}` : 'new';
  const problemsKey = JSON.stringify(problems);
  useEffect(() => registerOpenDraft({
    place: placeName, graph, workflow: saveId || null, base: target.base, source, route, pipeline, selected,
    problems: JSON.parse(problemsKey) as GraphProblem[],
  }), [placeName, graph, saveId, target.base, source, route, pipeline, selected, problemsKey]);

  const atStep = (id: string) => problems.filter((problem) => problem.node === id && !(problem.wire && wireOf(problem.wire)));
  const atWire = (id: string) => problems.filter((problem) => problem.wire === id);
  const holdRefused = draft.nodes.some((node) => isHoldKind(node.kind) && problems.some((problem) => stepOf(problem) === node.id));
  // A refusal inside the holds opens them and they stay open: the judgement goes invalid on the next edit, and the drawing must not jump.
  useEffect(() => { if (holdRefused) setHoldsOpen(true); }, [holdRefused]);

  // Ask Daoris's proposal (FLOWCHART2f; D223 §4, §8.6): drawn on the chart beside the draft, taken whole or change by change as one step of
  // history each, its record undone with it, and settled by the one press naming every change taken or left. Opening it settles nothing.
  const [settling, setSettling] = useState<Readonly<Record<string, Settling>>>({});
  const [setAside, setSetAside] = useState<readonly string[]>([]);
  const proposal = offered && !setAside.includes(offered.id) ? offered : null;
  const needs = useMemo(() => (proposal ? needsOf(proposal) : new Map<string, string[]>()), [proposal]);
  const decided = (proposal ? history.present.decided?.[proposal.id] : undefined) ?? UNDECIDED;
  const stale = useMemo(() => (proposal ? staleOf(draft, proposal, decided) : new Map<string, string>()), [draft, proposal, decided]);
  const named = (id: string) => decided.taken.includes(id) || decided.left.includes(id);
  const undecided = proposal ? proposal.changes.map((change) => change.id).filter((id) => !named(id)) : [];
  const open = undecided.filter((id) => !stale.has(id));
  const union = proposal ? unionOf(draft, proposal, open) : null;
  const changeWords = (id: string) => {
    const change = proposal?.changes.find((each) => each.id === id);
    return proposal && change ? plain(changeSaid(t, i18n, palette, proposal, change)) : id;
  };
  const wordsOf = (ids: readonly string[]) => ids.map(changeWords).join(t('workflow.listJoin'));
  /** A take or a leave: the draft and the proposal's record as one step of history. */
  const record = (next: Decided, nextDraft: GraphDraft, words: string) => {
    if (!proposal) return;
    const id = proposal.id;
    setHistory((was) => commit(was, { draft: nextDraft, said: words, decided: { ...was.present.decided, [id]: next } }));
    setHeard(words);
    setSaid(QUIET);
    setConfirming(null);
  };
  const takeChanges = (ids: readonly string[]) => {
    if (!proposal || ids.length === 0) return;
    const result = takeInto(draft, proposal, ids, decided);
    if (!result.ok) {
      setHeard(t('workflow.edit.proposal.heard.refused', { change: changeWords(result.at) }));
      return;
    }
    // A take makes a new draft, so the judgement it had goes at once and the head says it is judged again.
    record(decide(decided, { taken: result.taken }), result.draft, t('workflow.edit.proposal.heard.took', { changes: wordsOf(result.taken) }));
  };
  const leaveChanges = (ids: readonly string[]) => {
    if (!proposal) return;
    const left = [...new Set(ids.flatMap((id) => leftWith(proposal, id, needs)))].filter((id) => !named(id));
    if (left.length > 0) record(decide(decided, { left }), draft, t('workflow.edit.proposal.heard.left', { changes: wordsOf(left) }));
  };
  // The press naming every change, once each proposal: told again only after it failed, at the next change of its record.
  const press = useRef(onSettleProposal);
  press.current = onSettleProposal;
  const pressed = useRef(new Map<string, Settling['state']>());
  const complete = proposal !== null && everyNamed(proposal, decided);
  useEffect(() => {
    if (!complete || !proposal || !press.current) return;
    const id = proposal.id;
    if (pressed.current.get(id) === 'pending' || pressed.current.get(id) === 'told') return;
    pressed.current.set(id, 'pending');
    setSettling((was) => ({ ...was, [id]: { state: 'pending' } }));
    press.current(id, decided.taken, decided.left).then(
      () => {
        pressed.current.set(id, 'told');
        setSettling((was) => ({ ...was, [id]: { state: 'told' } }));
        setHeard(t('workflow.edit.proposal.settled'));
      },
      (refused: unknown) => {
        pressed.current.set(id, 'failed');
        setSettling((was) => ({ ...was, [id]: { state: 'failed', why: sentence(refused) } }));
      },
    );
    // Pressed as the record becomes whole: its changes' identity is the record's.
  }, [complete, proposal?.id, decided]);
  const dismiss = () => {
    if (!proposal || !onDismissProposal) return;
    const id = proposal.id;
    setSettling((was) => ({ ...was, [id]: { state: 'pending' } }));
    onDismissProposal(id).then(
      () => {
        setSetAside((was) => [...was, id]);
        setHeard(t('workflow.edit.proposal.heard.dismissed'));
      },
      (refused: unknown) => setSettling((was) => ({ ...was, [id]: { state: 'failed', why: sentence(refused) } })),
    );
  };
  // *Ask again* fills Ask Daoris in with the changes the draft no longer fits, for the person to send with the draft as it is now.
  const askAgain = () => {
    if (!proposal) return;
    const which = proposal.base === null ? proposal.changes.map((change) => change.id) : undecided.filter((id) => stale.has(id));
    fillAskDaoris(t('workflow.edit.proposal.askAgain', { changes: which.map(changeWords).join('; ') }));
  };
  const rows: ChangeRow[] = proposal ? proposal.changes.map((change) => {
    const at = stale.get(change.id);
    const step = changeStep(proposal, change);
    const drawn = step === null ? undefined : union?.draft.nodes.find((node) => node.id === step);
    return {
      id: change.id,
      said: changeSaid(t, i18n, palette, proposal, change),
      mark: change.change,
      step: drawn ? { id: drawn.id, title: stepTitle(t, i18n, palette, drawn) } : null,
      state: decided.taken.includes(change.id) ? 'taken' : decided.left.includes(change.id) ? 'left' : at !== undefined ? 'stale' : 'open',
      with: closureOf(proposal, change.id, needs).filter((id) => id !== change.id && !decided.taken.includes(id)).map(changeWords),
      staleAt: at !== undefined && at !== change.id ? changeWords(at) : null,
    };
  }) : [];
  const busy = proposal !== null && settling[proposal.id]?.state === 'pending';

  const drawnDraft = union?.draft ?? draft;
  const flow = drawnDraft.nodes.length > 0
    ? withRefusals(t, i18n, withChanges(t, draftFlow(
      t, i18n, palette, drawnDraft, { open: holdsOpen || holdRefused, toggle: () => setHoldsOpen((was) => !was) }, select,
    ), union), problems)
    : null;

  const edit: ChartEdit = {
    selected,
    onSelect: (id) => (choosing ? take(id) : select(id)),
    insertOn: (edge) => {
      const wire = wireOf(edge.id);
      if (!wire || wire.from === null || !canInsertOn(draft, wire.id)) return null;
      return { label: t('workflow.edit.insert.between', { from: titleOf(wire.from), to: titleOf(wire.to) }), onPress: () => openPicker({ on: wire.id }) };
    },
    insertAt: (node) => {
      const none = /^(.+):none:(.+)$/.exec(node.id);
      // A ◎ of a step the proposal would add is no place in the draft yet.
      if (!none || !nodeOf(none[1]!)) return null;
      return {
        label: t('workflow.edit.insert.at', { from: titleOf(none[1]!), outcome: none[2]! }),
        onPress: () => openPicker({ from: none[1]!, row: `outcome:${none[2]!}` }),
      };
    },
    onPort: (from, outcome) => choose(from, `outcome:${outcome}`),
    onGround: () => { if (choosing) openPicker({ from: choosing.from, row: choosing.row }); },
    pick: choosing ? { key: `${choosing.from}\n${choosing.row}`, can: (id) => takerAt(id) !== null, onPick: take, onCancel: giveUp } : null,
    wayMark: (edge) => {
      const mark = union?.ways.get(edge);
      return mark ? { sign: MARK_SIGN[mark], word: t(`workflow.edit.proposal.mark.${mark}`) } : null;
    },
  };

  const placeSaid = (at: InsertAt): string => {
    const between = (id: string) => {
      const wire = wireOf(id)!;
      return t('workflow.edit.picker.between', { from: titleOf(wire.from!), to: titleOf(wire.to) });
    };
    if ('on' in at) return between(at.on);
    if ('after' in at) {
      const on = draft.wires.filter((wire) => wire.from === at.after && wire.kind === 'after');
      return on.length === 1 && canInsertOn(draft, on[0]!.id) ? between(on[0]!.id) : t('workflow.edit.picker.after', { from: titleOf(at.after) });
    }
    if ('from' in at) {
      const row = at.row.startsWith('outcome:') ? 'outcome' : at.row;
      return t(`workflow.edit.picker.at.${row}`, { from: titleOf(at.from), outcome: at.row.slice('outcome:'.length) });
    }
    return t('workflow.edit.picker.free');
  };

  const begin = (from: Begin) => {
    if ('saved' in from) {
      setWaiting(from.saved.id);
      onShow(from.saved.id);
      return;
    }
    const name = presetName(t, i18n, from.preset);
    const next = draftOf(from.preset.body);
    change(next, t('workflow.edit.said.began', { name }));
    setTarget({ id: '', name: '', base: null, from: from.preset.id });
    const first = entriesOf(next)[0] ?? next.nodes[0]?.id;
    if (first) focusOn(first);
  };

  const scopes = target.base !== null && judgement?.appended ? judgement.scopes : [];
  const save = async () => {
    try {
      const answer = await onSave({
        id: saveId, graph,
        ...(target.base === null ? (target.name.trim() ? { name: target.name.trim() } : {}) : { base: target.base }),
        ...(authorized && scopes.length > 0 ? { authorize: scopes.map((scope) => scope.key) } : {}),
      });
      setSaid({ saved: answer, used: null, failed: null });
      if (answer.done && answer.written && answer.version !== null) {
        setTarget((was) => ({ ...was, id: answer.id, base: answer.version }));
        setAuthorized(false);
      }
    } catch (refused) {
      setSaid({ saved: null, used: null, failed: sentence(refused) });
    }
  };
  const use = async () => {
    if (target.base === null) return;
    try {
      const answer = await onUse({ ...place, workflow: target.id, version: target.base });
      setSaid((was) => ({ ...was, used: answer }));
    } catch (refused) {
      setSaid((was) => ({ ...was, failed: sentence(refused) }));
    }
  };

  const node = selected ? nodeOf(selected) ?? null : null;
  const acts = (id: string): InspectorActs => ({
    onChange: change,
    onTarget: (row, wire, to) => {
      if (to === null) {
        change(removeWire(draft, wire.id), t('workflow.edit.said.wayRemoved', { row: rowSaid(t, row), from: titleOf(id), to: titleOf(wire.to) }));
        return;
      }
      const next = retarget(palette, draft, wire.id, to);
      if (next) change(next, carried(words(id, row, to), next, [], [to]));
    },
    onAdd: (row, to) => {
      const made = connect(palette, draft, id, row, to);
      if (made) change(made.draft, carried(words(id, row, to), made.draft, made.carried, [to]));
    },
    onPick: (row) => choose(id, row),
    onNew: (row) => openPicker({ from: id, row }),
    onInsert: (wire) => openPicker({ on: wire.id }),
    onAfter: () => openPicker({ after: id }),
    onMove: (dir) => {
      const next = moveInChain(draft, id, dir);
      if (next) change(next, t('workflow.edit.said.moved', { step: titleOf(id) }));
    },
    onDuplicate: () => duplicate(id),
    onCopy: () => copy(id),
    onRemove: (options) => remove(id, options),
  });
  const inspector = node && (
    <Inspector palette={palette} draft={draft} roles={roles} lines={lines} node={node} titleOf={titleOf}
      problems={{ atStep: atStep(node.id), atWire }} confirming={confirming} onConfirm={setConfirming} acts={acts(node.id)} />
  );

  return (
    <section ref={region} data-edit-history="" aria-labelledby={headId} onKeyDown={onKeyDown} className="grid min-w-0 gap-4">
      <div className="flex min-w-0 flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <h3 id={headId} className="m-0 text-title font-semibold text-ink">{t('workflow.edit.title')}</h3>
        <div className="flex flex-wrap gap-1.5">
          <Button variant="ghost" className="border border-line" onClick={() => openPicker(selected ? { after: selected } : { free: true })}>
            {t('workflow.edit.add.open')}
          </Button>
          <Button variant="ghost" className="border border-line" disabled={history.past.length === 0} onClick={undoChange}>{t('workflow.edit.undo')}</Button>
          <Button variant="ghost" className="border border-line" disabled={history.future.length === 0} onClick={redoChange}>{t('workflow.edit.redo')}</Button>
          {onClose && <Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>}
        </div>
      </div>
      <Prose className="text-small">{t('workflow.edit.body')}</Prose>

      {draft.nodes.length > 0 && (
        <div aria-live="polite" className="grid min-w-0 gap-1">
          {judgement?.done ? <p className="m-0 text-small text-ink">{t('workflow.edit.judge.holds')}</p>
            : judgement ? (
              <>
                <p className="m-0 text-small text-ink">{t('workflow.edit.problems.count', { count: problems.length })}</p>
                {problems.length > 0 ? (
                  <ToPutRight
                    items={problems.map((problem) => {
                      const step = stepOf(problem);
                      return { problem, step, title: step ? titleOf(step) : null };
                    })}
                    onDoor={(step) => { select(step); focusOn(step); }}
                    {...(canAsk ? { onAsk: (words: string) => { fillAskDaoris(words); } } : {})}
                  />
                ) : judgement.message && <Prose className="text-small"><Inline text={judgement.message} /></Prose>}
              </>
            )
              : unjudged ? <p className="m-0 text-small text-ink">{t('workflow.edit.judge.failed')}</p>
                : <p className="m-0 text-small text-ink-soft">{t('workflow.edit.judge.judging')}</p>}
        </div>
      )}

      {proposal && (
        <ProposalBar
          maker={proposal.maker}
          why={proposal.why}
          problems={proposal.problems}
          counts={{ taken: decided.taken.length, left: decided.left.length, open: undecided.length }}
          unknownBase={proposal.base === null}
          settling={settling[proposal.id] ?? null}
          canTakeAll={open.length > 0}
          busy={busy}
          onTakeAll={() => takeChanges(open)}
          onLeaveRest={() => leaveChanges(undecided)}
          onDismiss={dismiss}
          {...(canAsk ? { onAskAgain: askAgain } : {})}
        >
          <ChangeList
            rows={rows}
            busy={busy}
            onTake={(id) => takeChanges([id])}
            onLeave={(id) => leaveChanges([id])}
            onShow={(step) => { select(step); focusOn(step); }}
            {...(canAsk ? { onAskAgain: askAgain } : {})}
          />
        </ProposalBar>
      )}

      <div className={wide ? 'grid min-w-0 grid-cols-[minmax(0,1fr)_minmax(18rem,26rem)] items-start gap-6' : 'grid min-w-0 gap-4'}>
        <EditCanvas flow={flow} selected={selected} edit={edit} list={list} waiting={waiting} onBegin={begin} />
        {wide && (node
          ? (
            <section aria-labelledby={inspectorId} className="grid min-w-0 gap-3 rounded-card border border-line bg-raised p-3">
              <h4 id={inspectorId} className="m-0 text-body font-semibold text-ink">{titleOf(node.id)}</h4>
              {inspector}
            </section>
          )
          : <p className="m-0 text-small text-ink-soft">{t('workflow.edit.inspector.none')}</p>)}
      </div>

      <SaveReview
        target={target}
        id={newId}
        onName={(name) => setTarget((was) => ({ ...was, name }))}
        scopes={scopes}
        authorized={authorized}
        onAuthorize={setAuthorized}
        empty={draft.nodes.length === 0}
        saving={saving}
        using={using}
        said={said}
        onSave={() => void save()}
        onUse={() => void use()}
      />

      <p role="status" aria-live="polite" className="sr-only">{heard}</p>
      <StepPicker open={picker !== null} palette={palette} place={picker ? placeSaid(picker) : ''} onPick={pickedStep} onClose={closePicker} />
      {!wide && drawer && node && <Drawer title={titleOf(node.id)} onClose={closeDrawer}>{inspector}</Drawer>}
    </section>
  );
}
