import type { TFunction, i18n as I18n } from 'i18next';
import { type ReactNode, useEffect, useId, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Flow, type FlowEdge, type FlowNode } from '../../design/Flow';
import { Button, CheckField, Inline, Pill, Prose, SelectField, SkeletonRows } from '../../design/ui';
import { useDebounced } from '../../lib/useDebounced';
import { sentence } from '../../locales/format';
import { WORKFLOW_TRACK } from '../workflowFlow';
import type {
  EditorRole, GraphProblem, WorkflowListAnswer, WorkflowPaletteAnswer, WorkflowSaveAnswer, WorkflowSaveAsk, WorkflowScopeRow, WorkflowShownAnswer,
  WorkflowUseAnswer, WorkflowUseAsk,
} from './answers';
import {
  addStep, addWire, choiceOf, type DraftNode, type DraftWire, draftOf, emptyDraft, entriesOf, type GraphDraft, graphOf, inputsOf, leavesOf, onChoices,
  outcomesOf, outputsOf, removeStep, removeWire, setEnd, setOption, stepChoices, type WireAsk,
} from './graphDraft';
import { StepOptions } from './StepOptions';

/** What the step editor's bridge hands it (`useWorkflowEditor`): the reads, the roles, the judge, the save and the choice. */
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
  judging: boolean;
  onJudge: (ask: WorkflowSaveAsk) => void;
  saving: boolean;
  onSave: (ask: WorkflowSaveAsk) => Promise<WorkflowSaveAnswer>;
  using: boolean;
  onUse: (ask: WorkflowUseAsk) => Promise<WorkflowUseAnswer>;
  /** Told when the editor opens and closes, so its reads are asked only while it shows. */
  onOpen?: (open: boolean) => void;
};

export type WorkflowEditorProps = WorkflowEditorBridge & {
  /** Where *Use here* chooses the saved version: the page the editor opened from. */
  place: { repository: string } | { workspace: string };
  /** The steps it opens on, a graph as a version holds it, and the saved workflow and version they are, where they are one. */
  startFrom?: { graph: unknown; id?: string; name?: string; base?: number | null };
  onClose?: () => void;
};

/** How long a change holds still before it is judged: each judge is a planned save, and typing a name is many changes. */
const JUDGE_MS = 250;

/** The id a draft is judged under before the person names one: a plan writes nothing, so it only has to be an id. */
const DRAFT_ID = 'draft';

const EMPTY = ':empty';
const DEFAULT = 'default:';
const SAVED = 'saved:';

const plain = (text: string) => text.replace(/`/g, '');

/** A step's kind in the reader's words: a plugin's step by its plugin, a built-in by its name, a kind this page has none for as spelled. */
export function kindTitle(t: TFunction, i18n: I18n, kind: string): string {
  const slash = kind.indexOf('/');
  if (slash > 0) return t('workflow.edit.kind.plugin', { step: kind.slice(slash + 1), plugin: kind.slice(0, slash) });
  const key = `workflow.edit.kind.${kind}`;
  return i18n.exists(key) ? t(key) : t('workflow.edit.unknownKind', { kind });
}

/** A refusal in the reader's words, or the driver's where this page has none for its code (a rule newer than the page). */
const refusalSaid = (t: TFunction, i18n: I18n, problem: GraphProblem) =>
  i18n.exists(`workflow.edit.refusal.${problem.code}`) ? t(`workflow.edit.refusal.${problem.code}`) : problem.words;

/** How a wire goes on, said without its target: the drawing's edge cause and the *How it goes on* choice. */
function howSaid(t: TFunction, wire: Pick<DraftWire, 'kind' | 'outcome' | 'on'>): string {
  switch (wire.kind) {
    case 'when': return t('workflow.edit.on.outcome', { outcome: wire.outcome ?? '' });
    case 'back': return wire.on === 'rejected' ? t('workflow.edit.on.rejected') : wire.on === 'failed' ? t('workflow.edit.on.failed')
      : t('workflow.edit.on.outcome', { outcome: wire.on ?? '' });
    case 'after': case 'trigger': case 'data': case 'failed': case 'rejected': return t(`workflow.edit.how.${wire.kind}`);
    default: return t('workflow.edit.how.recorded', { kind: wire.kind });
  }
}

/** A way on, said with its target: *The landing, once it is reached*; *Back to The work, when you ask for changes*. */
function waySaid(t: TFunction, wire: DraftWire, to: string): string {
  switch (wire.kind) {
    case 'after': case 'trigger': case 'failed': case 'rejected': return t(`workflow.edit.way.${wire.kind}`, { to });
    case 'when': return t('workflow.edit.way.when', { to, outcome: wire.outcome ?? '' });
    case 'data': return t('workflow.edit.way.data', { to, from: wire.fromPort ?? '', input: wire.toPort ?? '' });
    case 'back': return wire.on === 'rejected' || wire.on === 'failed'
      ? t(`workflow.edit.way.back.${wire.on}`, { to }) : t('workflow.edit.way.back.outcome', { to, outcome: wire.on ?? '' });
    default: return t('workflow.edit.way.recorded', { to, kind: wire.kind });
  }
}

/** The edge a wire draws (the run view's own styles, `workflowFlow.ts`): a failure's, a refusal's, a decision's and a way back's in theirs. */
const STYLE: Partial<Record<string, FlowEdge['style']>> = { failed: 'failed', rejected: 'rejected', when: 'conditional', back: 'back' };

/** The draft drawn on the flow canvas: each step a node, each way on between two of them an edge. */
function draftFlow(t: TFunction, i18n: I18n, draft: GraphDraft, ends: string): { nodes: FlowNode[]; edges: FlowEdge[]; entries: string[] } {
  const ids = new Set(draft.nodes.map((node) => node.id));
  const nodes = draft.nodes.map((node): FlowNode => ({
    id: node.id, title: plain(kindTitle(t, i18n, node.kind)), actor: node.id === draft.end ? ends : null,
  }));
  const edges = draft.wires.filter((wire) => wire.from !== null && ids.has(wire.from) && ids.has(wire.to)).map((wire): FlowEdge => ({
    id: wire.id, from: wire.from!, to: wire.to, says: plain(howSaid(t, wire)), style: STYLE[wire.kind],
    ...(wire.kind === 'when' ? { label: wire.outcome ?? '' } : wire.kind === 'back' ? { label: t('workflow.run.flow.back.label') } : {}),
  }));
  const entries = entriesOf(draft);
  return { nodes, edges, entries: entries.length > 0 ? entries : [draft.nodes[0]!.id] };
}

/** What the person is saving to: a new workflow's id and name, or a saved one's id and the version the change is made against. */
type Target = { id: string; name: string; base: number | null };

const NEW: Target = { id: '', name: '', base: null };

/**
 * **The step editor** (WORKFLOWEDIT1b; D173's notes of 2026-11-01): a workflow composed as its steps, from one of Daoris's defaults, a
 * version saved here or nothing. Each step comes from the palette, so a kind added to the driver is offered with no change here; each
 * says where it goes on, by a kind of way the palette lets leave it, its options and the role that takes it. The draft is drawn on the
 * flow canvas and judged as it changes, by a planned save, each refusal said at the step or the way on it names. Saved, a new workflow
 * or a version appended against the one read with the places it reaches authorized, it is chosen here by *Use here*.
 *
 * A molecule: the answers arrive as props, the draft is page state, and every press goes out.
 */
export function WorkflowEditor({
  list, palette, error, roles, shown, onShow, judged, judging, onJudge, saving, onSave, using, onUse, place, startFrom, onClose,
}: WorkflowEditorProps) {
  const { t, i18n } = useTranslation();
  const headId = useId();
  const [source, setSource] = useState(startFrom?.id ? `${SAVED}${startFrom.id}` : EMPTY);
  const [waiting, setWaiting] = useState<string | null>(null);
  const [draft, setDraft] = useState<GraphDraft>(() => (startFrom ? draftOf(startFrom.graph) : emptyDraft()));
  const [target, setTarget] = useState<Target>(() =>
    (startFrom ? { id: startFrom.id ?? '', name: startFrom.name ?? '', base: startFrom.base ?? null } : NEW));
  const [authorized, setAuthorized] = useState(false);
  const [said, setSaid] = useState<{ saved: WorkflowSaveAnswer | null; used: WorkflowUseAnswer | null; failed: string | null }>(
    { saved: null, used: null, failed: null });

  // A saved version, once its answer lands; a version of steps (schema 1) is no graph to compose, and leaves the draft as it was.
  useEffect(() => {
    if (waiting === null || shown?.id !== waiting) return;
    setWaiting(null);
    if (shown.schema === 2) {
      setDraft(draftOf(shown.body));
      setTarget({ id: shown.id, name: shown.name, base: shown.version });
    }
  }, [waiting, shown]);

  // Each change judged once it holds still, as the driver would save it.
  const graph = useMemo(() => graphOf(draft), [draft]);
  const ask = useMemo((): WorkflowSaveAsk => ({
    id: target.id.trim() || DRAFT_ID, graph, ...(target.base === null ? {} : { base: target.base }),
  }), [graph, target.id, target.base]);
  const settled = useDebounced(ask, JUDGE_MS);
  const judge = useRef(onJudge);
  judge.current = onJudge;
  useEffect(() => { if ((settled.graph as { nodes: unknown[] }).nodes.length > 0) judge.current(settled); }, [settled]);

  if (!palette || !list) {
    return (
      <div aria-busy={!error || undefined} className="grid gap-2">
        <Prose className="text-small">{error ? t('workflow.edit.unread', { why: sentence(error) }) : t('workflow.edit.reading')}</Prose>
        {!error && <SkeletonRows rows={3} />}
      </div>
    );
  }

  const choices = stepChoices(palette);
  const titleOf = (node: DraftNode) => kindTitle(t, i18n, node.kind);
  const nodeNamed = (id: string) => draft.nodes.find((node) => node.id === id);
  const titleById = (id: string) => (nodeNamed(id) ? plain(titleOf(nodeNamed(id)!)) : id);
  const change = (next: GraphDraft) => { setDraft(next); setSaid({ saved: null, used: null, failed: null }); };

  const problems = draft.nodes.length > 0 && judged ? judged.problems ?? [] : [];
  const nodeIds = new Set(draft.nodes.map((node) => node.id));
  const wireIds = new Set(draft.wires.map((wire) => wire.id));
  const atNode = (id: string) => problems.filter((problem) => problem.node === id && !(problem.wire && wireIds.has(problem.wire)));
  const atWire = (id: string) => problems.filter((problem) => problem.wire === id);
  const general = problems.filter((problem) => !(problem.wire && wireIds.has(problem.wire)) && !(problem.node && nodeIds.has(problem.node)));

  const chooseSource = (value: string) => {
    setSource(value);
    setAuthorized(false);
    setSaid({ saved: null, used: null, failed: null });
    if (value.startsWith(SAVED)) {
      const id = value.slice(SAVED.length);
      setWaiting(id);
      onShow(id);
      return;
    }
    onShow(null);
    setWaiting(null);
    const preset = value.startsWith(DEFAULT) ? list.presets.find((each) => each.id === value.slice(DEFAULT.length)) : undefined;
    setDraft(preset ? draftOf(preset.body) : emptyDraft());
    setTarget({ ...NEW, name: preset ? (preset.names ? (i18n.language.startsWith('zh') ? preset.names.zh : preset.names.en) : preset.name) : '' });
  };

  const scopes: WorkflowScopeRow[] = target.base !== null && judged?.appended ? judged.scopes : [];
  const save = async () => {
    try {
      const answer = await onSave({
        id: target.id.trim(), graph,
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

  const language = i18n.language.startsWith('zh') ? 'zh' : 'en';
  const flow = draft.nodes.length > 0 ? draftFlow(t, i18n, draft, t('workflow.edit.step.ends')) : null;

  return (
    <section aria-labelledby={headId} className="grid min-w-0 gap-4">
      <div className="flex min-w-0 flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <h3 id={headId} className="m-0 text-title font-semibold text-ink">{t('workflow.edit.title')}</h3>
        {onClose && <Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>}
      </div>
      <Prose className="text-small">{t('workflow.edit.body')}</Prose>

      <div className="grid min-w-0 max-w-[32rem] gap-1">
        <span className="text-meta text-ink-soft">{t('workflow.edit.source.label')}</span>
        <SelectField
          ariaLabel={t('workflow.edit.source.label')}
          value={source}
          onChange={chooseSource}
          options={[
            { value: EMPTY, label: t('workflow.edit.source.empty') },
            ...list.presets.filter((preset) => preset.graph && preset.body).map((preset) => ({
              value: `${DEFAULT}${preset.id}`, label: preset.names?.[language] ?? preset.name, group: t('workflow.edit.source.defaults'),
            })),
            ...list.workflows.filter((saved) => saved.schema === 2 && !saved.problem).map((saved) => ({
              value: `${SAVED}${saved.id}`, group: t('workflow.edit.source.saved'),
              label: t('workflow.edit.source.savedVersion', { name: saved.name ?? saved.id, version: saved.versions.at(-1) ?? 1 }),
            })),
          ]}
        />
        {waiting && <p className="m-0 text-meta text-ink-faint">{t('workflow.edit.source.loading', { id: waiting })}</p>}
      </div>

      {flow && <Flow label={t('workflow.edit.drawing')} nodes={flow.nodes} edges={flow.edges} entries={flow.entries} track={WORKFLOW_TRACK} />}

      {draft.nodes.length === 0 && <Prose className="text-small">{t('workflow.edit.none')}</Prose>}
      <ol aria-label={t('workflow.edit.steps')} className="m-0 grid list-none gap-3 p-0">
        {draft.nodes.map((node) => (
          <Step
            key={node.id}
            node={node}
            draft={draft}
            palette={palette}
            roles={roles}
            title={titleOf(node)}
            titleById={titleById}
            problems={atNode(node.id)}
            wireProblems={atWire}
            onChange={change}
          />
        ))}
      </ol>

      <div className="grid min-w-0 max-w-[32rem] gap-1">
        <span className="text-meta text-ink-soft">{t('workflow.edit.add.label')}</span>
        {/* Drawn again with each step added, so the choice reads as a press and the same kind can be added twice. */}
        <SelectField
          key={draft.nodes.length}
          ariaLabel={t('workflow.edit.add.label')}
          value=""
          placeholder={t('workflow.edit.add.label')}
          onChange={(kind) => { const choice = choices.find((each) => each.kind === kind); if (choice) change(addStep(draft, choice).draft); }}
          options={choices.map((choice) => ({
            value: choice.kind, label: plain(kindTitle(t, i18n, choice.kind)),
            group: choice.plugin ? t('workflow.edit.add.plugin', { plugin: choice.plugin }) : t('workflow.edit.add.daoris'),
          }))}
        />
      </div>

      {draft.nodes.length > 0 && (
        <div aria-live="polite" className="grid min-w-0 gap-1">
          {judging ? <p className="m-0 text-small text-ink-soft">{t('workflow.edit.judge.judging')}</p>
            : judged?.done ? <p className="m-0 text-small text-ink">{t('workflow.edit.judge.holds')}</p>
              : judged ? (
                <>
                  <p className="m-0 text-small text-ink">{t('workflow.edit.judge.refused')}</p>
                  {general.length > 0
                    ? <ul className="m-0 grid list-disc gap-1 pl-5">{general.map((problem, at) => <Refusal key={at} problem={problem} />)}</ul>
                    : judged.message && <Prose className="text-small"><Inline text={judged.message} /></Prose>}
                </>
              ) : null}
        </div>
      )}

      <div className="grid min-w-0 gap-2 border-t border-line pt-3">
        <h4 className="m-0 text-small font-semibold text-ink">{t('workflow.edit.save.title')}</h4>
        {target.base === null ? (
          <div className="flex min-w-0 flex-wrap gap-3">
            <TextField label={t('workflow.edit.save.id')} hint={t('workflow.edit.save.idHint')} value={target.id}
              onChange={(id) => setTarget((was) => ({ ...was, id }))} />
            <TextField label={t('workflow.edit.save.name')} value={target.name} onChange={(name) => setTarget((was) => ({ ...was, name }))} />
          </div>
        ) : (
          <Prose className="text-small">{t('workflow.edit.save.appendSays', { version: target.base + 1, id: target.id })}</Prose>
        )}
        {scopes.length > 0 && (
          <div className="grid min-w-0 gap-1">
            <p className="m-0 text-small text-ink">{t('workflow.edit.save.reach')}</p>
            <ul className="m-0 grid list-disc gap-0.5 pl-5 text-small text-ink-soft">
              {scopes.map((scope) => <li key={scope.key}>{scopeSaid(t, scope)}</li>)}
            </ul>
            <CheckField checked={authorized} onChange={setAuthorized} label={t('workflow.edit.save.authorize')} />
          </div>
        )}
        <div className="flex min-w-0 flex-wrap items-center gap-2">
          <Button disabled={saving || draft.nodes.length === 0 || !target.id.trim()} onClick={() => void save()}>
            {saving ? t('workflow.edit.save.saving')
              : target.base === null ? t('workflow.edit.save.new') : t('workflow.edit.save.append', { version: target.base + 1 })}
          </Button>
          {target.base !== null && (
            <Button variant="ghost" className="border border-line" disabled={using} onClick={() => void use()}>
              {using ? t('workflow.edit.using') : t('workflow.edit.use')}
            </Button>
          )}
        </div>
        {said.saved?.message && <Prose className="text-small"><Inline text={said.saved.message} /></Prose>}
        {said.used?.message && <Prose className="text-small"><Inline text={said.used.message} /></Prose>}
        {said.failed && <Prose className="text-small"><Inline text={said.failed} /></Prose>}
      </div>
    </section>
  );
}

/** A place an append reaches, in words. */
function scopeSaid(t: TFunction, scope: WorkflowScopeRow): string {
  const place = t(`workflow.edit.save.place.${scope.level}`, { name: scope.name });
  return scope.kind ? t('workflow.edit.save.place.kind', { place, kind: scope.kind }) : place;
}

/** A refusal in the reader's words, the driver's beneath where its code is one this page words. */
function Refusal({ problem }: { problem: GraphProblem }) {
  const { t, i18n } = useTranslation();
  const worded = i18n.exists(`workflow.edit.refusal.${problem.code}`);
  return (
    <li className="text-small text-ink">
      {worded ? refusalSaid(t, i18n, problem) : <Inline text={problem.words} />}
      {worded && <p className="m-0 text-meta text-ink-faint"><Inline text={t('workflow.edit.judge.driver', { words: problem.words })} /></p>}
    </li>
  );
}

function TextField({ label, hint, value, onChange }: { label: string; hint?: string; value: string; onChange: (value: string) => void }) {
  const id = useId();
  return (
    <div className="grid min-w-0 gap-1">
      <label htmlFor={id} className="text-meta text-ink-soft">{label}</label>
      <input
        id={id}
        className="min-h-[1.9rem] min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
        value={value}
        aria-describedby={hint ? `${id}-hint` : undefined}
        onChange={(event) => onChange(event.target.value)}
      />
      {hint && <span id={`${id}-hint`} className="text-meta text-ink-faint"><Inline text={hint} /></span>}
    </div>
  );
}

/**
 * One step of the draft: its title and id, where the work ends, its refusals, its options, the ways it goes on with theirs, and the
 * form that adds a way on, offering only what the palette lets leave it.
 */
function Step({ node, draft, palette, roles, title, titleById, problems, wireProblems, onChange }: {
  node: DraftNode;
  draft: GraphDraft;
  palette: WorkflowPaletteAnswer;
  roles: EditorRole[];
  title: string;
  titleById: (id: string) => string;
  problems: GraphProblem[];
  wireProblems: (wire: string) => GraphProblem[];
  onChange: (draft: GraphDraft) => void;
}) {
  const { t } = useTranslation();
  const headId = useId();
  const choice = choiceOf(palette, node.kind);
  const ways = draft.wires.filter((wire) => wire.from === node.id);
  const name = plain(title);

  return (
    <li aria-labelledby={headId} data-step={node.id} className="grid min-w-0 gap-2 rounded-card border border-line bg-raised p-3">
      <div className="flex min-w-0 flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <div className="flex min-w-0 flex-wrap items-baseline gap-2">
          <h4 id={headId} className="m-0 text-body font-semibold text-ink"><Inline text={title} /></h4>
          <code className="text-meta text-ink-faint">{node.id}</code>
          {draft.end === node.id && <Pill tone="neutral">{t('workflow.edit.step.ends')}</Pill>}
        </div>
        <div className="flex flex-wrap gap-2">
          {draft.end !== node.id && (
            <Button variant="ghost" className="border border-line" aria-label={t('workflow.edit.step.endHereNamed', { step: name })}
              onClick={() => onChange(setEnd(draft, node.id))}>
              {t('workflow.edit.step.endHere')}
            </Button>
          )}
          <Button variant="ghost" className="border border-line" aria-label={t('workflow.edit.step.removeNamed', { step: name })}
            onClick={() => onChange(removeStep(draft, node.id))}>
            {t('workflow.edit.step.remove')}
          </Button>
        </div>
      </div>
      {problems.length > 0 && <ul className="m-0 grid list-disc gap-1 pl-5">{problems.map((problem, at) => <Refusal key={at} problem={problem} />)}</ul>}

      {choice && (
        <StepOptions node={node} fields={choice.options} roleTakes={choice.roleTakes} roles={roles}
          onSet={(option, value) => onChange(setOption(draft, node.id, option, value))} />
      )}

      <div className="grid min-w-0 gap-1">
        <span className="text-meta text-ink-soft">{t('workflow.edit.step.goesOn')}</span>
        {ways.length === 0 ? <p className="m-0 text-small text-ink-faint">{t('workflow.edit.step.nowhere')}</p> : (
          <ul aria-label={t('workflow.edit.step.goesOn')} className="m-0 grid list-none gap-1 p-0">
            {ways.map((wire) => (
              <li key={wire.id} className="grid min-w-0 gap-0.5">
                <div className="flex min-w-0 flex-wrap items-center justify-between gap-2">
                  <span className="text-small text-ink"><Inline text={waySaid(t, wire, titleById(wire.to))} /></span>
                  <Button variant="ghost" className="border border-line"
                    aria-label={t('workflow.edit.way.removeNamed', { from: name, way: plain(waySaid(t, wire, titleById(wire.to))) })}
                    onClick={() => onChange(removeWire(draft, wire.id))}>
                    {t('workflow.edit.way.remove')}
                  </Button>
                </div>
                {wireProblems(wire.id).length > 0 && (
                  <ul className="m-0 grid list-disc gap-1 pl-5">{wireProblems(wire.id).map((problem, at) => <Refusal key={at} problem={problem} />)}</ul>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Drawn again whenever the ways change, so a way added leaves the form as it first was. */}
      <WayForm key={draft.wires.length} node={node} draft={draft} palette={palette} name={name} titleById={titleById}
        onAdd={(ask) => onChange(addWire(draft, ask))} />
    </li>
  );
}

/** The form that adds a way on from a step: how, to which step, and a decision's outcome, a way back's `on` or a data wire's ports. */
function WayForm({ node, draft, palette, name, titleById, onAdd }: {
  node: DraftNode;
  draft: GraphDraft;
  palette: WorkflowPaletteAnswer;
  name: string;
  titleById: (id: string) => string;
  onAdd: (ask: WireAsk) => void;
}) {
  const { t } = useTranslation();
  const leaves = leavesOf(palette, node);
  const [kind, setKind] = useState(leaves[0] ?? 'after');
  const [to, setTo] = useState('');
  const [extra, setExtra] = useState<{ outcome?: string; on?: string; fromPort?: string; toPort?: string }>({});
  if (leaves.length === 0) return null;

  const target = draft.nodes.find((each) => each.id === to);
  const ons = onChoices(palette, node);
  const ready = to !== '' && (kind !== 'when' || Boolean(extra.outcome)) && (kind !== 'back' || Boolean(extra.on))
    && (kind !== 'data' || (Boolean(extra.fromPort) && Boolean(extra.toPort)));
  const onSaid = (on: string) => (on === 'rejected' ? t('workflow.edit.on.rejected') : on === 'failed' ? t('workflow.edit.on.failed')
    : plain(t('workflow.edit.on.outcome', { outcome: on })));

  return (
    <div className="flex min-w-0 flex-wrap items-end gap-2">
      <Labelled label={t('workflow.edit.how.label')}>
        <SelectField ariaLabel={t('workflow.edit.how.label')} value={kind} onChange={(chosen) => { setKind(chosen); setExtra({}); }}
          options={leaves.map((leaf) => ({ value: leaf, label: plain(t(`workflow.edit.how.${leaf}`, { kind: leaf, defaultValue: t('workflow.edit.how.recorded', { kind: leaf }) })) }))} />
      </Labelled>
      <Labelled label={t('workflow.edit.to.label')}>
        <SelectField ariaLabel={t('workflow.edit.to.label')} value={to} placeholder={t('workflow.edit.to.label')} onChange={(chosen) => { setTo(chosen); setExtra((was) => ({ ...was, toPort: undefined })); }}
          options={draft.nodes.filter((each) => each.id !== node.id).map((each) => ({
            value: each.id, label: t('workflow.edit.step.named', { title: titleById(each.id), id: each.id }),
          }))} />
      </Labelled>
      {kind === 'when' && (
        <Labelled label={t('workflow.edit.outcome.label')}>
          <SelectField ariaLabel={t('workflow.edit.outcome.label')} value={extra.outcome ?? ''} placeholder={t('workflow.edit.outcome.label')}
            onChange={(outcome) => setExtra({ outcome })} options={outcomesOf(node).map((outcome) => ({ value: outcome, label: outcome }))} />
        </Labelled>
      )}
      {kind === 'back' && (
        <Labelled label={t('workflow.edit.on.label')}>
          <SelectField ariaLabel={t('workflow.edit.on.label')} value={extra.on ?? ''} placeholder={t('workflow.edit.on.label')}
            onChange={(on) => setExtra({ on })} options={ons.map((on) => ({ value: on, label: onSaid(on) }))} />
        </Labelled>
      )}
      {kind === 'data' && (
        <>
          <Labelled label={t('workflow.edit.port.from')}>
            <SelectField ariaLabel={t('workflow.edit.port.from')} value={extra.fromPort ?? ''} placeholder={t('workflow.edit.port.from')}
              onChange={(fromPort) => setExtra((was) => ({ ...was, fromPort }))} options={outputsOf(node).map((port) => ({ value: port, label: port }))} />
          </Labelled>
          <Labelled label={t('workflow.edit.port.to')}>
            <SelectField ariaLabel={t('workflow.edit.port.to')} value={extra.toPort ?? ''} placeholder={t('workflow.edit.port.to')}
              onChange={(toPort) => setExtra((was) => ({ ...was, toPort }))}
              options={(target ? inputsOf(target) : []).map((port) => ({ value: port, label: port }))} />
          </Labelled>
        </>
      )}
      <Button
        variant="ghost"
        className="border border-line"
        disabled={!ready}
        aria-label={t('workflow.edit.way.addNamed', { step: name })}
        onClick={() => { onAdd({ kind, from: node.id, to, ...extra }); setTo(''); setExtra({}); }}
      >
        {t('workflow.edit.way.add')}
      </Button>
    </div>
  );
}

function Labelled({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="grid min-w-0 gap-1">
      <span aria-hidden className="text-meta text-ink-soft">{label}</span>
      {children}
    </div>
  );
}
