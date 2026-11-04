
import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Drawer, Inline, Prose, SkeletonRows } from '../../design/ui';
import { useRootRem, useWidth } from '../../design/useWidth';
import { useDebounced } from '../../lib/useDebounced';
import { fillAskDaoris, useCanFillAsk } from '../../help/askFill';
import { sentence } from '../../locales/format';
import { fingerprintOf } from '../draftFingerprint';
import { EDIT_EVENT } from '../../menus/editing';
import { type EditorRole, type StepStaffing, type WorkflowListAnswer, type WorkflowPaletteAnswer, type WorkflowSaveAnswer, type WorkflowSaveAsk, type WorkflowShownAnswer, type WorkflowUseAnswer, type WorkflowUseAsk } from './answers';
import { EditCanvas } from './EditCanvas';
import { type Clip, commit, draftOf, emptyDraft, type GraphDraft, graphOf, type History, historyOf, type InsertAt, workflowIdFor } from './graphDraft';
import { type Confirming } from './Inspector';
import { ChangeList } from './ChangeList';
import { ProposalBar } from './ProposalBar';
import { type Decided, type ProposalShown } from './proposalTake';
import { ToPutRight } from './Refusals';
import { type SaveTarget, SaveReview } from './SaveReview';
import { StepPicker } from './StepPicker';
import { JUDGE_MS, DRAFT_ID, BESIDE_REM, PICKABLE, RETURN, type Choosing, type Said, QUIET } from './editorTypes';
import { useEditorSave } from './useEditorSave';
import { useEditorActs } from './useEditorActs';

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
  /** The one role editor's reads and presses, for a step's *Taken by* to give the place a role of its own; none, and it offers only the roles there are. */
  staffing?: StepStaffing;
  /** The saved version last asked for by `onShow`, once it is answered. */
  shown: WorkflowShownAnswer | null;
  /** Reads a saved workflow: its newest version, or the one named. */
  onShow: (id: string | null, version?: number) => void;
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
  /** The plan of a choice (`WORKFLOW_USE {plan}`), which writes nothing; none, and *Use here* is today's press (SETUPUX1p). */
  onPlan?: (ask: WorkflowUseAsk) => Promise<WorkflowUseAnswer>;
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
  startFrom?: { graph: unknown; id?: string; name?: string; base?: number | null; told?: boolean };
  /** The route and the pipeline the place opened it for, where the caller knows them: Ask Daoris is told them with the draft. */
  route?: string | null;
  pipeline?: 'development' | 'verification' | null;
  /** The lines the place declares, which a landing's target is chosen from; none, and the target is typed. */
  lines?: string[];
  /** The draft the page held for this place when the editor was last unmounted, which it begins from; and where it reports each change. */
  held?: EditorHold | null;
  onHold?: (hold: EditorHold) => void;
  onClose?: () => void;
};

/**
 * One step of the editor's history: the draft, the words of the change that made it, and what of each of Ask Daoris's proposals was taken
 * and left by then, so one undo takes back a take and its record together (D223 §8.6).
 */
export type Snapshot = { draft: GraphDraft; said: string; decided?: Readonly<Record<string, Decided>> };

/** What the save is aimed at, and the id a new one was copied from (a preset's, or a saved workflow's). */
export type Target = SaveTarget & { from: string | null };

/**
 * What the editor keeps outside itself, so the draft outlives the editor's mount: its history, the target and baseline a save is
 * reviewed against, the consent given, and the version still being read as the baseline.
 */
export type EditorHold = {
  history: History<Snapshot>;
  target: Target;
  baseline: unknown;
  consented: string | null;
  reading: { id: string; version: number } | null;
};

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
  const rem = useRootRem();
  const { list, palette, error } = props;
  return (
    <div ref={region} data-editor-region="" className="min-w-0">
      {palette && list
        ? <Composing {...props} list={list} palette={palette} wide={width !== undefined && width >= BESIDE_REM * rem} />
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
  list, palette, roles, staffing, shown, onShow, judged, judgedOf = null, judgeFailed = null, judging, onJudge, saving, onSave, using, onUse, onPlan, place, startFrom,
  route = null, pipeline = null, lines, held = null, onHold, onClose, wide, proposal: offered = null, onSettleProposal, onDismissProposal,
}: WorkflowEditorProps & { list: WorkflowListAnswer; palette: WorkflowPaletteAnswer; wide: boolean }) {
  const { t, i18n } = useTranslation();
  const headId = useId();
  const canAsk = useCanFillAsk();
  const inspectorId = useId();
  const region = useRef<HTMLElement>(null);
  // Where the focus goes back to when the picker is given up: a dialog with no trigger of its own gives back none.
  const returnTo = useRef<HTMLElement | null>(null);

  const [history, setHistory] = useState(() => held?.history ?? historyOf<Snapshot>({ draft: startFrom ? draftOf(startFrom.graph) : emptyDraft(), said: '' }));
  const draft = history.present.draft;
  // The draft as the newest render holds it, for a press whose answer comes after the draft has moved on.
  const latest = useRef(draft);
  latest.current = draft;
  const [chosen, setChosen] = useState<string | null>(null);
  // The steps chosen with it by Ctrl- or Shift-press or a box: for Copy alone; every other act takes the primary (CANVAS1b).
  // Kept with the primary it was chosen beside, so any act that moves the primary leaves it behind.
  const [alsoOf, setAlso] = useState<{ of: string | null; ids: readonly string[] }>({ of: null, ids: [] });
  const [drawer, setDrawer] = useState(false);
  const [picker, setPicker] = useState<InsertAt | null>(null);
  const [choosing, setChoosing] = useState<Choosing | null>(null);
  const [confirming, setConfirming] = useState<Confirming>(null);
  const [clip, setClip] = useState<Clip | null>(null);
  const [heard, setHeard] = useState('');
  const [waiting, setWaiting] = useState<string | null>(null);
  const [target, setTarget] = useState<Target>(() => held?.target ?? ({
    id: startFrom?.id ?? '', name: startFrom?.name ?? '', base: startFrom ? startFrom.base ?? null : null, from: startFrom?.id ?? null,
  }));
  // The places an append reaches that the person let follow it, as one key (`consentKey`): consent holds for exactly that set.
  const [consented, setConsented] = useState<string | null>(held?.consented ?? null);
  // The steps the draft began from, which a save is reviewed against and *Use here* needs the draft to still be.
  // A draft the conversation was told is not the version it came from: that version is read, and until it is the baseline is unknown.
  const [readingBase, setReadingBase] = useState<{ id: string; version: number } | null>(
    () => (held ? held.reading : startFrom?.told && startFrom.id && startFrom.base != null ? { id: startFrom.id, version: startFrom.base } : null),
  );
  const [baseline, setBaseline] = useState<unknown>(() => (held ? held.baseline : readingBase ? null : startFrom?.graph ?? null));
  const askedBase = useRef(false);
  useEffect(() => {
    if (!readingBase || askedBase.current) return;
    askedBase.current = true;
    onShow(readingBase.id, readingBase.version);
  }, [readingBase, onShow]);
  useEffect(() => {
    if (!readingBase || shown?.id !== readingBase.id || shown.version !== readingBase.version) return;
    setReadingBase(null);
    if (shown.schema === 2) setBaseline(shown.body);
  }, [readingBase, shown]);
  // The page keeps the draft past this mount; the sender is read through a ref, since the page hands a new one each render.
  const report = useRef(onHold);
  report.current = onHold;
  useEffect(() => { report.current?.({ history, target, baseline, consented, reading: readingBase }); }, [history, target, baseline, consented, readingBase]);
  const [reviewing, setReviewing] = useState<string | null>(null);
  const [planning, setPlanning] = useState(false);
  const [rebasing, setRebasing] = useState(false);
  const [holdsOpen, setHoldsOpen] = useState(false);
  const [said, setSaid] = useState<Said>(QUIET);
  const [focusing, setFocusing] = useState<{ at: string; n: number } | null>(null);

  const selected = chosen !== null && draft.nodes.some((node) => node.id === chosen) ? chosen : null;
  const also = alsoOf.of !== null && alsoOf.of === selected ? alsoOf.ids.filter((id) => id !== selected && draft.nodes.some((node) => node.id === id)) : [];

  // A saved version to begin from, once its answer lands; a version of steps (schema 1) is no graph to compose, and leaves the draft as it was.
  useEffect(() => {
    if (waiting === null || shown?.id !== waiting) return;
    if (rebasing) {
      // The newest version read again after a refusal that the base moved: the draft is kept, and is saved against that version. An answer
      // still at the base is the one read before, and the read that follows it is the one waited for.
      if (shown.version === target.base) return;
      setWaiting(null);
      setRebasing(false);
      if (shown.schema !== 2) return;
      setTarget((was) => ({ ...was, base: shown.version }));
      setBaseline(shown.body);
      setReviewing(null);
      setSaid(QUIET);
      setHeard(t('workflow.edit.save.rebased', { id: shown.id, version: shown.version }));
      return;
    }
    setWaiting(null);
    if (shown.schema !== 2) return;
    const words = t('workflow.edit.said.began', { name: shown.name });
    setHistory((was) => commit(was, { ...was.present, draft: draftOf(shown.body), said: words }));
    setHeard(words);
    setTarget({ id: shown.id, name: shown.name, base: shown.version, from: shown.id });
    setBaseline(shown.body);
    setReviewing(null);
  }, [waiting, rebasing, shown, target.base, t]);

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
      // In the canvas's viewport the canvas reveals the step, so the focus scrolls nothing (CANVAS1a, trap 2).
      found?.focus(found.closest('[data-canvas-world]') ? { preventScroll: true } : undefined);
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
  const { wireOf, titleOf, change, judgement, nodeOf, carried, words, choose, openPicker, duplicate, copy, remove, rolePlace, atStep, atWire, onKeyDown, undoChange, redoChange, problems, stepOf, select, unjudged, proposal, decided, undecided, settling, open, busy, takeChanges, leaveChanges, dismiss, askAgain, rows, flow, edit, pickedStep, closePicker, closeDrawer } = useEditorActs({ draft, t, i18n, palette, setHistory, setHeard, setSaid, setConfirming, history, setChoosing, setChosen, setAlso, wide, setDrawer, returnTo, setPicker, focusOn, selected, picker, choosing, also, setClip, clip, menu, judged, judgedOf, fingerprint, judgeFailed, judging, settled, ask, place, target, graph, saveId, route, pipeline, setHoldsOpen, offered, onSettleProposal, onDismissProposal, holdsOpen });
  const { begin, node, inspector, holds, reviewOpen, review, scopes, allowed, consent, save, reread, offer, use, placeSaid } = useEditorSave({ wireOf, t, titleOf, draft, setWaiting, onShow, i18n, change, setTarget, setBaseline, focusOn, judgement, reviewing, fingerprint, setReviewing, baseline, said, target, consented, palette, graph, onSave, saveId, setSaid, setConsented, setRebasing, route, place, list, onPlan, planning, setPlanning, onUse, selected, nodeOf, latest, carried, words, choose, openPicker, duplicate, copy, remove, roles, rolePlace, staffing, lines, atStep, atWire, confirming, setConfirming });

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
        empty={draft.nodes.length === 0}
        holds={holds}
        review={reviewOpen ? review() : null}
        onReview={(open) => setReviewing(open ? fingerprint : null)}
        scopes={scopes}
        consented={allowed}
        onConsent={(on) => setConsented(on ? consent : null)}
        saving={saving}
        using={using}
        said={said}
        onSave={() => void save()}
        onReread={reread}
        use={offer}
        onPlan={() => setPlanning(true)}
        onUse={() => void use()}
      />

      <p role="status" aria-live="polite" className="sr-only">{heard}</p>
      <StepPicker open={picker !== null} palette={palette} place={picker ? placeSaid(picker) : ''} onPick={pickedStep} onClose={closePicker} />
      {!wide && drawer && node && <Drawer title={titleOf(node.id)} onClose={closeDrawer}>{inspector}</Drawer>}
    </section>
  );
}
