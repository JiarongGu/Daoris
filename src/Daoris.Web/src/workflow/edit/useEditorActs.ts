import type * as React from 'react';
import { type KeyboardEvent, useEffect } from 'react';
import { isComposing } from '../../lib/composing';
import { registerOpenDraft } from '../../help/openDraft';
import { isTextField } from '../../menus/press';
import { type ChartEdit } from '../chart/FlowChart';
import { isHoldKind } from '../graphFlow';
import { type GraphProblem, type WorkflowPaletteAnswer, type WorkflowSaveAnswer, type WorkflowSaveAsk } from './answers';
import { draftFlow } from './EditCanvas';
import { carriedSaid, rowSaid, stepTitle } from './editWords';
import { gitTail } from './gitTail';
import { canInsertOn, type Carried, type Clip, commit, connect, copySteps, duplicateStep, type GraphDraft, type History, type InsertAt, insertStep, joinOf, movePlan, pasteSteps, redo, removeStep, type StepChoice, targetsOf, undo, type WayRowKey, type GraphJson } from './graphDraft';
import { type Confirming } from './Inspector';
import { withRefusals } from './refusalMarks';
import { type RolePlace } from './StepRole';
import { HOLDS, PICKABLE, RETURN, type Choosing, type Said, QUIET } from './editorTypes';
import { type TFunction, type i18n } from 'i18next';
import { type Snapshot, type Target } from './WorkflowEditor';

export function useEditorActs({ draft, t, i18n, palette, setHistory, setHeard, setSaid, setConfirming, history, setChoosing, setChosen, setAlso, wide, setDrawer, returnTo, setPicker, focusOn, selected, picker, choosing, also, setClip, clip, menu, judged, judgedOf, fingerprint, judgeFailed, judging, settled, ask, place, target, graph, saveId, route, pipeline, setHoldsOpen, holdsOpen }: {
  draft: GraphDraft;
  t: TFunction<"translation", undefined>;
  i18n: i18n;
  palette: WorkflowPaletteAnswer;
  setHistory: React.Dispatch<React.SetStateAction<History<Snapshot>>>;
  setHeard: React.Dispatch<React.SetStateAction<string>>;
  setSaid: React.Dispatch<React.SetStateAction<Said>>;
  setConfirming: React.Dispatch<React.SetStateAction<Confirming>>;
  history: History<Snapshot>;
  setChoosing: React.Dispatch<React.SetStateAction<Choosing | null>>;
  setChosen: React.Dispatch<React.SetStateAction<string | null>>;
  setAlso: React.Dispatch<React.SetStateAction<{ of: string | null; ids: readonly string[]; }>>;
  wide: boolean;
  setDrawer: React.Dispatch<React.SetStateAction<boolean>>;
  returnTo: React.RefObject<HTMLElement | null>;
  setPicker: React.Dispatch<React.SetStateAction<InsertAt | null>>;
  focusOn: (at: string) => void;
  selected: string | null;
  picker: InsertAt | null;
  choosing: Choosing | null;
  also: string[];
  setClip: React.Dispatch<React.SetStateAction<Clip | null>>;
  clip: Clip | null;
  menu: React.RefObject<Partial<Record<string, () => void>>>;
  judged: WorkflowSaveAnswer | null;
  judgedOf: string | null;
  fingerprint: string;
  judgeFailed: string | null;
  judging: boolean;
  settled: WorkflowSaveAsk;
  ask: WorkflowSaveAsk;
  place: { repository: string; } | { workspace: string; };
  target: Target;
  graph: GraphJson;
  saveId: string;
  route: string | null;
  pipeline: "development" | "verification" | null;
  setHoldsOpen: React.Dispatch<React.SetStateAction<boolean>>;
  holdsOpen: boolean;
}) {
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

  const words = (from: string, row: WayRowKey, to: string) => t('workflow.edit.said.way', { row: rowSaid(t, row, nodeOf(from)?.kind), from: titleOf(from), to: titleOf(to) });
  /** A change's words, with the inputs it proposed and the ones it leaves to ask about. */
  const carried = (said: string, next: GraphDraft, proposed: Carried[], steps: string[]) =>
    [said, carriedSaid(t, i18n, palette, next, proposed, steps)].filter(Boolean).join(' ');
  const select = (id: string) => {
    setChosen(id);
    setAlso({ of: null, ids: [] });
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
    const inserted = insertStep(palette, draft, choice, at);
    if (inserted.draft === draft) return;
    const tailed = gitTail(palette, inserted.draft, inserted.id);
    const made = { ...inserted, draft: tailed.draft };
    const titled = (id: string | null | undefined) => {
      const node = made.draft.nodes.find((each) => each.id === id);
      return node ? stepTitle(t, i18n, palette, node) : id ?? '';
    };
    const behind = made.draft.wires.find((wire) => wire.kind === 'back' && wire.from === made.id && wire.on === 'behind');
    const tail = tailed.tail === 'rebase' && tailed.steps.length === 2
      ? t('workflow.edit.said.gitTail.rebase', { resolve: titled(tailed.steps[0]), task: titled(tailed.steps[1]) })
      : tailed.tail === 'merge' && behind ? t('workflow.edit.said.gitTail.merge', { rebase: titled(behind.to) }) : null;
    const added = t('workflow.edit.said.added', { step: stepTitle(t, i18n, palette, choice) });
    change(made.draft, carried(tail ? `${added} ${tail}` : added, made.draft, made.carried, [made.id, ...tailed.steps]));
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
    const named = { row: rowSaid(t, row, nodeOf(from)?.kind), step: titleOf(from) };
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
  // Ctrl or Shift on a step, or a box, chooses more than one, for Copy alone: the pressed step is the primary, the inspector's.
  const chooseMore = (id: string, how: 'toggle' | 'add') => {
    const present = selected === null ? [] : [selected, ...also];
    setConfirming(null);
    if (present.includes(id)) {
      if (how === 'add') return;
      const rest = present.filter((each) => each !== id);
      setChosen(rest[0] ?? null);
      setAlso({ of: rest[0] ?? null, ids: rest.slice(1) });
      return;
    }
    setChosen(id);
    setAlso({ of: id, ids: present });
  };
  const chooseBox = (ids: string[]) => {
    const steps = ids.filter((id) => nodeOf(id));
    setConfirming(null);
    setChosen(steps[0] ?? null);
    setAlso({ of: steps[0] ?? null, ids: steps.slice(1) });
    if (steps.length > 0) setHeard(t('workflow.edit.said.chosen', { count: steps.length }));
  };
  // Copy takes every step chosen when it is aimed at one of them, else the one it is aimed at.
  const copy = (id: string | null) => {
    if (id === null) return;
    const chosenAll = selected !== null && (id === selected || also.includes(id)) ? [selected, ...also] : [id];
    setClip(copySteps(draft, [...new Set(chosenAll.flatMap(stepsAt))]));
    setHeard(t('workflow.edit.said.copied', { step: chosenAll.map(titleOf).join(t('workflow.listJoin')) }));
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

  // The keys: undo and redo outside a field; on a step, + or Insert to add a step after it, / for the picker, Delete to remove,
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

  // The judgement: the newest answer, only while it is of the draft held now. An edit makes it invalid at once, before the
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
  // Ask Daoris is told the draft open here, registered while the editor is mounted and let go when it closes.
  const placeName = 'repository' in place ? place.repository : place.workspace;
  const rolePlace: RolePlace = 'repository' in place ? { kind: 'repository', name: place.repository } : { kind: 'workspace', name: place.workspace };
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

  const flow = draft.nodes.length > 0
    ? withRefusals(t, i18n, draftFlow(
      t, i18n, palette, draft, { open: holdsOpen || holdRefused, toggle: () => setHoldsOpen((was) => !was) }, select,
    ), problems)
    : null;

  const edit: ChartEdit = {
    selected,
    chosen: new Set(also),
    onSelect: (id, how) => {
      if (choosing) take(id);
      else if (how && nodeOf(id)) chooseMore(id, how);
      else select(id);
    },
    onBox: (ids) => { if (!choosing) chooseBox(ids); },
    insertOn: (edge) => {
      const wire = wireOf(edge.id);
      if (!wire || wire.from === null || !canInsertOn(draft, wire.id)) return null;
      return { label: t('workflow.edit.insert.between', { from: titleOf(wire.from), to: titleOf(wire.to) }), onPress: () => openPicker({ on: wire.id }) };
    },
    insertAt: (node) => {
      const none = /^(.+):none:(.+)$/.exec(node.id);
      if (!none || !nodeOf(none[1]!)) return null;
      return {
        label: t('workflow.edit.insert.at', { from: titleOf(none[1]!), outcome: none[2]! }),
        onPress: () => openPicker({ from: none[1]!, row: `outcome:${none[2]!}` }),
      };
    },
    onPort: (from, outcome) => choose(from, `outcome:${outcome}`),
    onGround: () => { if (choosing) openPicker({ from: choosing.from, row: choosing.row }); },
    pick: choosing ? { key: `${choosing.from}\n${choosing.row}`, can: (id) => takerAt(id) !== null, onPick: take, onCancel: giveUp } : null,
  };
  return { wireOf, titleOf, change, judgement, nodeOf, carried, words, choose, openPicker, duplicate, copy, remove, rolePlace, atStep, atWire, onKeyDown, undoChange, redoChange, problems, stepOf, select, unjudged, flow, edit, pickedStep, closePicker, closeDrawer };
}
