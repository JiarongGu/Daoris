import type * as React from 'react';
import { useEffect, useMemo } from 'react';
import { sentence } from '../../locales/format';
import { fingerprintOf } from '../draftFingerprint';
import { type EditorRole, type GraphProblem, type StepStaffing, type WorkflowListAnswer, type WorkflowPaletteAnswer, type WorkflowSaveAnswer, type WorkflowSaveAsk, type WorkflowUseAnswer, type WorkflowUseAsk } from './answers';
import { type Begin } from './EditCanvas';
import { outcomeSaid } from '../graphFlow';
import { kindIn, rowSaid, stepTitle } from './editWords';
import { canInsertOn, type Carried, connect, draftOf, entriesOf, type GraphDraft, graphOf, type InsertAt, moveInChain, removeWire, retarget, setOption, type WayRowKey, type DraftWire, type GraphJson, type DraftNode } from './graphDraft';
import { type Confirming, Inspector, type InspectorActs } from './Inspector';
import { presetName } from './presetWords';
import { consentKey, changesBetween } from './saveChanges';
import { type ReviewFacts, type UseOffer } from './SaveReview';
import { type RolePlace } from './StepRole';
import { usePlan } from './usePlan';
import { type Said } from './editorTypes';
import { type TFunction, type i18n } from 'i18next';
import { type Target } from './WorkflowEditor';

export function useEditorSave({ wireOf, t, titleOf, draft, setWaiting, onShow, i18n, change, setTarget, setBaseline, focusOn, judgement, reviewing, fingerprint, setReviewing, baseline, said, target, consented, palette, graph, onSave, saveId, setSaid, setConsented, setRebasing, route, place, list, onPlan, planning, setPlanning, onUse, selected, nodeOf, latest, carried, words, choose, openPicker, duplicate, copy, remove, roles, rolePlace, staffing, lines, atStep, atWire, confirming, setConfirming }: {
  wireOf: (id: string) => DraftWire | undefined;
  t: TFunction<"translation", undefined>;
  titleOf: (id: string) => string;
  draft: GraphDraft;
  setWaiting: React.Dispatch<React.SetStateAction<string | null>>;
  onShow: (id: string | null, version?: number | undefined) => void;
  i18n: i18n;
  change: (next: GraphDraft, words: string) => void;
  setTarget: React.Dispatch<React.SetStateAction<Target>>;
  setBaseline: React.Dispatch<unknown>;
  focusOn: (at: string) => void;
  judgement: WorkflowSaveAnswer | null;
  reviewing: string | null;
  fingerprint: string;
  setReviewing: React.Dispatch<React.SetStateAction<string | null>>;
  baseline: unknown;
  said: Said;
  target: Target;
  consented: string | null;
  palette: WorkflowPaletteAnswer;
  graph: GraphJson;
  onSave: (ask: WorkflowSaveAsk) => Promise<WorkflowSaveAnswer>;
  saveId: string;
  setSaid: React.Dispatch<React.SetStateAction<Said>>;
  setConsented: React.Dispatch<React.SetStateAction<string | null>>;
  setRebasing: React.Dispatch<React.SetStateAction<boolean>>;
  route: string | null;
  place: { repository: string; } | { workspace: string; };
  list: WorkflowListAnswer;
  onPlan: ((ask: WorkflowUseAsk) => Promise<WorkflowUseAnswer>) | undefined;
  planning: boolean;
  setPlanning: React.Dispatch<React.SetStateAction<boolean>>;
  onUse: (ask: WorkflowUseAsk) => Promise<WorkflowUseAnswer>;
  selected: string | null;
  nodeOf: (id: string) => DraftNode | undefined;
  latest: React.RefObject<GraphDraft>;
  carried: (said: string, next: GraphDraft, proposed: Carried[], steps: string[]) => string;
  words: (from: string, row: WayRowKey, to: string) => string;
  choose: (from: string, row: WayRowKey) => void;
  openPicker: (at: InsertAt) => void;
  duplicate: (id: string) => void;
  copy: (id: string | null) => void;
  remove: (id: string, options: { join: boolean; end?: string | undefined; }) => void;
  roles: EditorRole[];
  rolePlace: RolePlace;
  staffing: StepStaffing | undefined;
  lines: string[] | undefined;
  atStep: (id: string) => GraphProblem[];
  atWire: (id: string) => GraphProblem[];
  confirming: Confirming;
  setConfirming: React.Dispatch<React.SetStateAction<Confirming>>;
}) {
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
      return t(`workflow.edit.picker.at.${row}`, { from: titleOf(at.from), outcome: outcomeSaid(t, kindIn(draft, at.from), at.row.slice('outcome:'.length)) });
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
    setBaseline(from.preset.body);
    const first = entriesOf(next)[0] ?? next.nodes[0]?.id;
    if (first) focusOn(first);
  };

  // The review: of this draft, once its judgement holds. Saving is its press, so nothing is saved unreviewed.
  const holds = draft.nodes.length > 0 && judgement?.done === true;
  const reviewOpen = holds && reviewing === fingerprint;
  useEffect(() => { setReviewing(null); }, [fingerprint]);
  const baseDraft = useMemo(() => (baseline === null || baseline === undefined ? null : draftOf(baseline)), [baseline]);
  const baseFingerprint = useMemo(() => (baseDraft ? fingerprintOf(graphOf(baseDraft)) : null), [baseDraft]);
  // The places an append reaches are the driver's last word: a save refused for them names the set now, which replaces the plan's.
  const refusedFor = said.saved?.refused === 'reach' ? said.saved : null;
  const scopes = target.base !== null ? refusedFor?.scopes ?? (judgement?.appended ? judgement.scopes : []) : [];
  const reached = refusedFor?.reach ?? judgement?.reach ?? [];
  const consent = consentKey(scopes);
  const allowed = scopes.length > 0 && consented === consent;
  const review = (): ReviewFacts => {
    const title = (each: { kind: string }) => stepTitle(t, i18n, palette, each);
    const changes = changesBetween(target.base === null ? null : baseDraft, draft);
    return {
      fresh: target.base === null ? draft.nodes.length : null,
      added: changes.added.map(title),
      removed: changes.removed.map(title),
      changed: changes.changed.map(title),
      ways: changes.ways,
      part: draft.nodes.filter((each) => palette.kinds.find((kind) => kind.kind === each.kind)?.personJudges).map(title),
      reach: reached.flatMap((row) => row.cannots.length > 0
        ? [t('workflow.edit.review.cannot', { repository: row.repository, why: row.said ?? row.cannots.join('; ') })]
        : row.noGates ? [t('workflow.edit.review.noGates', { repository: row.repository })] : []),
    };
  };
  const save = async () => {
    const sent = graph;
    try {
      const answer = await onSave({
        id: saveId, graph: sent,
        ...(target.base === null ? (target.name.trim() ? { name: target.name.trim() } : {}) : { base: target.base }),
        ...(allowed ? { authorize: scopes.map((scope) => scope.key) } : {}),
      });
      setSaid({ saved: answer, used: null, failed: null });
      if (answer.done && answer.version !== null) setBaseline(sent);
      if (answer.done && answer.written && answer.version !== null) {
        setTarget((was) => ({ ...was, id: answer.id, base: answer.version }));
        setConsented(null);
      }
      if (answer.done) setReviewing(null);
    } catch (refused) {
      setSaid({ saved: null, used: null, failed: sentence(refused) });
    }
  };
  // *Read it again* after a refusal that the base moved: the newest version is read, and the draft is saved against it.
  const reread = () => {
    setRebasing(true);
    setWaiting(target.id);
    onShow(target.id);
  };

  // The choice: only of a version the draft still is, for the route the editor opened for, with the plan's token where one is read.
  const unchanged = target.base !== null && baseFingerprint === fingerprint;
  const kindOfRoute = route?.startsWith('kind:') ? route.slice('kind:'.length) : null;
  const choice: WorkflowUseAsk | null = target.base === null ? null : {
    ...place, ...(kindOfRoute ? { kind: kindOfRoute } : {}), workflow: target.id, version: target.base,
  };
  const announced = list.use?.includes('plan') === true && onPlan !== undefined;
  const read = usePlan(choice, announced && planning && unchanged, onPlan);
  useEffect(() => { if (!unchanged) setPlanning(false); }, [unchanged]);
  const use = async () => {
    if (!choice) return;
    try {
      const answer = await onUse(read.token ? { ...choice, expected: read.token } : choice);
      setSaid((was) => ({ ...was, used: answer }));
      if (answer.refused === 'moved') read.again();
      else if (answer.done) setPlanning(false);
    } catch (refused) {
      setSaid((was) => ({ ...was, failed: sentence(refused) }));
    }
  };
  const offer: UseOffer = target.base === null || draft.nodes.length === 0 ? { is: 'none' }
    : !unchanged ? { is: 'unsaved' }
      : announced ? { is: 'press', announced: true, reading: planning, plan: read.plan, ready: read.token !== null }
        : { is: 'press', announced: false };

  const node = selected ? nodeOf(selected) ?? null : null;
  const acts = (id: string): InspectorActs => ({
    onChange: change,
    onOption: (option, value, said) => change(setOption(latest.current, id, option, value), said),
    onTarget: (row, wire, to) => {
      if (to === null) {
        change(removeWire(draft, wire.id), t('workflow.edit.said.wayRemoved', { row: rowSaid(t, row, kindIn(draft, id)), from: titleOf(id), to: titleOf(wire.to) }));
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
    <Inspector key={node.id} palette={palette} draft={draft} roles={roles} place={rolePlace} staffing={staffing} lines={lines} node={node} titleOf={titleOf}
      problems={{ atStep: atStep(node.id), atWire }} confirming={confirming} onConfirm={setConfirming} acts={acts(node.id)} />
  );
  return { begin, node, inspector, holds, reviewOpen, review, scopes, allowed, consent, save, reread, offer, use, placeSaid };
}
