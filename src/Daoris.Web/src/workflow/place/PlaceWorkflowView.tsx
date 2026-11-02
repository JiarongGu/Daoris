import { type ReactNode, useContext, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';
import { PlaceAside } from '../../agents/setup/placeAside';
import type { PlaceEdit, PlaceFlowHold, PlaceView } from '../../agents/setup/placeShell';
import { fillAskDaoris, useCanFillAsk } from '../../help/askFill';
import { toldGraph } from '../../help/openDraft';
import { cn } from '../../lib/cn';
import { Button, Inline, Prose, SkeletonRows } from '../../design/ui';
import { useWidth } from '../../design/useWidth';
import { FoldRow, type FoldSpec } from '../../projects/setup/FoldRow';
import type { LandsHere } from '../../projects/setup/RepositorySetup';
import { choiceLabel } from '../edit/editWords';
import { draftOf, optionOf } from '../edit/graphDraft';
import { LandOptions } from './LandOptions';
import type { IntegrationRequest } from '../../settings/repository/IntegrationDeclaration';
import { type Answered, InlineConfirm } from '../../work/InlineConfirm';
import type { WorkflowEffective, WorkflowStart } from '../current';
import { EffectiveChart } from './EffectiveChart';
import { type EditorHold, WorkflowEditor, type WorkflowEditorProps } from '../edit/WorkflowEditor';
import { type RouteRow, routeRows } from './effective';
import { partSaid } from '../said';
import { type WorkflowTabProps } from '../WorkflowTab';
import type { WorkflowDoors } from '../WorkflowChart';
import { LandedLines } from '../WorkflowStartNode';
import { ChangeWorkflow, type WorkflowPlanBridge } from './ChangeWorkflow';
import { gatesOwed, gatesSummary, PlaceGates } from './PlaceGates';
import { PlaceRepositories, repositoriesSummary } from './PlaceRepositories';

/** What the folds under the routes are handed: the section a step's door asked to see open (and how many times), and the folds to stand first. */
export type LandingHand = {
  asked: 'landing' | null;
  again: number;
  /** Gates, which the page owns, to stand before the folds of the line and its landing. */
  lead: readonly FoldSpec[];
  /** How the chosen route lands: the followed version's land options, or Current's door to the conversion; none while the route is read. */
  lands: LandsHere | null;
};

/** The land options of the followed version, which read the palette only while they show. */
function LandEditor({ editor, graph, workflow, lines, onReads }: {
  editor: NonNullable<PlaceWorkflowProps['editor']>;
  graph: unknown;
  workflow: { id: string; version: number };
  lines?: string[];
  onReads: (on: boolean) => void;
}) {
  const { t } = useTranslation();
  useEffect(() => { onReads(true); return () => onReads(false); }, []); // eslint-disable-line react-hooks/exhaustive-deps
  if (!editor.palette) return <p className="m-0 text-small text-ink-soft">{t('workflow.reading')}</p>;
  return (
    <LandOptions
      palette={editor.palette}
      roles={editor.roles}
      graph={graph}
      workflow={workflow}
      lines={lines}
      judged={editor.judged}
      judgedOf={editor.judgedOf ?? null}
      onJudge={editor.onJudge}
      saving={editor.saving}
      onSave={editor.onSave}
    />
  );
}

/** What the section draws under the routes: the line's landing rows, handed in by the page that holds the settings. */
export type PlaceWorkflowProps = WorkflowTabProps & {
  /** The step editor's bridge, with the sender of a choice's plan where the shell has one. */
  editor?: WorkflowTabProps['editor'] & WorkflowPlanBridge;
  /** The repository or the workspace whose place it is. */
  name: string;
  /** A repository's place, or a workspace's. */
  page?: 'repository' | 'workspace';
  /** The route chosen and the step whose cell shows, which the application keeps above the mounts; absent, the section keeps its own. */
  view?: PlaceView;
  onView?: (choice: Partial<PlaceView>) => void;
  /** The integration declaration's acceptance or withdrawal; none, and Gates offers no integration choice. */
  onAcceptIntegration?: (request: IntegrationRequest, answered: Answered) => void;
  /**
   * The folds of the line and its landing: the landing rule, the second opinion, your look, the landed route, the line and *More*, from the
   * one molecule Setup draws them with. Told which fold a chart's step door asked to see open, and given Gates to stand before them.
   */
  landing?: (hand: LandingHand) => ReactNode;
  /** Opens another section of the place page: a step's standing answer is Sessions'. */
  onOpenSection?: (section: 'sessions') => void;
  /**
   * The editor asked open on a draft (FLOWCHART2g): a conversation's draft card's *Open in the editor*. The draft the conversation was told
   * is the editor's start, so the proposal's changes can be taken; the editor is the page's to open once, and `onOpened` says it has.
   */
  opens?: PlaceEdit | null;
  onOpened?: () => void;
  /** The editor's draft the page kept when this section was last unmounted, and where it keeps the draft as the editor reports it (REVIEW2 P5). */
  held?: PlaceFlowHold | null;
  onHold?: (held: PlaceFlowHold | null) => void;
  /** Opens the home's conversion review (WORKFLOWCLEAN1g-i): the one door from a route, a Landing row or a tab that follows Current. */
  onConvert?: () => void;
};

type EditStart = NonNullable<WorkflowEditorProps['startFrom']>;

/** The panel open in place of the chart, and the route it opened on: what it writes is for that route, whichever route is shown after. */
type Panel = { is: 'change'; route: string } | { is: 'edit'; route: string; from?: EditStart } | null;

/**
 * What *Edit steps…* opens the editor on (SETUPUX1r): the graph the selected route follows. A saved workflow's newest version is the version
 * the edit appends to; Current's, or a version of steps drawn as the graph it decodes to, begins a workflow of its own. None where the
 * route draws no graph, or is still being read: the editor begins empty. A route that follows Current has no editor: it goes to the conversion.
 */
function editStart(effective: WorkflowEffective | null): { graph: unknown; id?: string; name?: string; base?: number | null } | undefined {
  if (effective === null || effective.graph === null || effective.graph === undefined) return undefined;
  if (effective.version !== null && effective.schema === 2) {
    return { graph: effective.graph, id: effective.workflow, name: effective.name ?? effective.workflow, base: effective.version };
  }
  return { graph: effective.graph, ...(effective.name ? { name: effective.name } : {}) };
}

/** The page holds 64 rem or more: the chart stands in a column of its own. */
const TWO_COLUMNS = 64 * 16;

const DEFAULT_VIEW: PlaceView = { route: 'default', step: null };

/** The rows of the routes as a selection: one is chosen, and choosing writes nothing. */
function Routes({ rows, chosen, onChoose }: { rows: readonly RouteRow[]; chosen: string; onChoose: (route: string) => void }) {
  const { t } = useTranslation();
  return (
    <ul aria-label={t('workflow.start.title')} className="m-0 grid min-w-0 list-none gap-0 p-0">
      {rows.map((row) => {
        const here = row.key === chosen;
        return (
          <li key={row.key} className="min-w-0">
            <button
              type="button"
              aria-current={here ? 'true' : undefined}
              aria-label={t('workflow.place.route.chosen', { label: row.label, target: row.target, why: row.why })}
              onClick={() => onChoose(row.key)}
              className={cn(
                'flex w-full min-w-0 flex-wrap items-baseline gap-x-2 border-l-2 py-0.5 pl-2.5 text-left text-small hover:bg-raised',
                here ? 'border-accent' : 'border-transparent',
              )}
            >
              <span className={cn('min-w-0 [overflow-wrap:anywhere]', here ? 'font-semibold text-ink' : 'text-ink')}>{row.label}</span>
              <span aria-hidden className="text-ink-faint">→</span>
              <span className="min-w-0 text-ink [overflow-wrap:anywhere]">{row.target}</span>
              <span aria-hidden className="min-w-0 text-small text-ink-soft">{row.why}</span>
            </button>
          </li>
        );
      })}
    </ul>
  );
}

/**
 * **A place's Workflow** (AGENTSPLACE1h, SETUPUX1o; the Setup design's §2.3): how a repository's or a workspace's work moves. The routes are
 * a selection, default work, each kind the workspace declares and landed work, each with the workflow it follows and what chose it; choosing
 * one picks the graph drawn and writes nothing. The chart is the chosen route's effective graph, in a column of its own where the page holds
 * 64 rem and one press away below it, labelled *Current* or the saved workflow and its version, with a step's cell shown on selection.
 * *Gates* and the folds of the line and its landing stand in one row of names under the routes; *Change…* and *Edit steps…* choose and
 * compose a version in place of the chart.
 *
 * @remarks
 * The two policy owners stay visible and apart (D215): the workflow owns the unit's process, the landing rule owns the line's batching,
 * order, gates and promotion. A step's door to the settings that set it opens Landing on this page, and the standing answer's opens the
 * Sessions section. Landed work is chosen by its own press (`WORKFLOW_LANDED`, in *Landed route*), not by *Change…*, and landed Current is
 * no verification, which draws no chart. Nothing here is the page's judgement: every refusal is the driver's sentence.
 *
 * A molecule: the answers arrive as props, and every press goes out.
 */
export function PlaceWorkflowView({
  name, page = 'repository', current, reading = false, refusal = null, doors, onAcceptGates, accepting = false, onVerify, editor,
  view, onView, onAcceptIntegration, landing, onOpenSection, opens = null, onOpened, held = null, onHold, onConvert,
}: PlaceWorkflowProps) {
  const { t } = useTranslation();
  const canAsk = useCanFillAsk();
  const pane = useRef<HTMLDivElement>(null);
  const width = useWidth(pane);
  const aside = useContext(PlaceAside);
  const [local, setLocal] = useState<PlaceView>(DEFAULT_VIEW);
  const [panel, setPanel] = useState<Panel>(() => (held ? { is: 'edit', route: held.route, ...(held.from ? { from: held.from } : {}) } : null));
  const [chartShown, setChartShown] = useState(false);
  const [asked, setAsked] = useState<{ section: 'landing' | null; n: number }>({ section: null, n: 0 });
  const place = page === 'workspace' ? { workspace: name } : { repository: name };
  const shown = view ?? local;
  const choose = (choice: Partial<PlaceView>) => {
    if (onView) onView(choice);
    else setLocal((was) => ({ route: choice.route ?? was.route, step: choice.route && choice.route !== was.route ? choice.step ?? null : choice.step === undefined ? was.step : choice.step }));
  };
  // The editor's draft as it last reported, which the page keeps past this mount (`held`); closing the editor, or replacing it by another
  // panel, lets the draft go.
  const reported = useRef<EditorHold | null>(held?.editor ?? null);
  const [discarding, setDiscarding] = useState(false);
  // The editor's reads are asked while a panel shows or the Landing row's options do.
  const reads = useRef({ panel: false, land: false });
  const tell = () => editor?.onOpen?.(reads.current.panel || reads.current.land);
  const show = (next: Panel) => {
    setPanel(next);
    reads.current.panel = next !== null;
    tell();
    if (next?.is !== 'edit') { reported.current = null; onHold?.(null); } else if (panel?.is !== 'edit') { reported.current = null; onHold?.({ route: next.route, ...(next.from ? { from: next.from } : {}), editor: null }); }
  };
  // The page may have unmounted this section with the editor open (a fold, *Set up…*, a change of layout): the editor is told it is shown.
  useEffect(() => { if (panel) { reads.current.panel = true; tell(); } }, []); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (asked.section) document.querySelector('[data-place-row="landing"]')?.scrollIntoView?.({ block: 'nearest' });
  }, [asked]);

  // A draft card's door (FLOWCHART2g): once the answer is read, the editor opens on the draft the conversation was told, or on the chart's
  // graph where that draft is no longer held. Once per ask: the page lets it go when it has opened.
  const opened = useRef<PlaceEdit | null>(null);
  const ready = current !== undefined && current !== null && editor !== undefined;
  useEffect(() => {
    if (!opens || !ready || opened.current === opens) return;
    opened.current = opens;
    const told = opens.draft ? toldGraph(opens.draft) : null;
    show({
      is: 'edit',
      route: shown.route,
      ...(told ? { from: { graph: told, ...(opens.base != null && opens.workflow ? { id: opens.workflow, base: opens.base, told: true } : {}), ...(opens.name ? { name: opens.name } : {}) } } : {}),
    });
    onOpened?.();
    // `show` reads this render's editor; the ask and the answer arriving are the reasons to open.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [opens, ready]);

  if (!current) {
    return (
      <div aria-busy={reading || undefined} className="mt-1 grid gap-2">
        <Prose className="text-small"><Inline text={refusal ?? t('workflow.reading')} /></Prose>
        {!refusal && <SkeletonRows rows={4} />}
      </div>
    );
  }

  // Inside a place's page the chart stands in the page's own column where it keeps one; outside a page the section measures its own room.
  const wide = aside === undefined ? width !== undefined && width >= TWO_COLUMNS : aside !== null;
  const start: WorkflowStart | null | undefined = current.start;
  const rows = start ? routeRows(t, start, { repository: page === 'repository' ? name : null, workspace: current.workspace }) : [];
  const route = rows.find((row) => row.key === shown.route) ?? rows[0];
  const routeKey = route?.key ?? shown.route;
  const kind = routeKey.startsWith('kind:') ? rows.find((row) => row.key === routeKey)?.label ?? null : null;
  const landed = routeKey === 'landed';
  const effective = current.effective ?? null;
  // The answer of the route before it stays drawn while the next is read, so one that is not this route's is being read.
  const answered = effective !== null && effective.route === routeKey ? effective : null;
  const steps = answered?.cells.map((cell) => cell.step) ?? current.steps;
  // The repository's own choice for the route, which *Use the workspace's* clears; a workspace's has none above it.
  const ownChoiceOf = (key: string) => page === 'repository'
    && (start?.routes.find((each) => (each.kind === null ? 'default' : `kind:${each.kind}`) === key)?.level ?? '').startsWith('repository');
  // Landed work is chosen by its own press, never by a panel: one opened on another route is kept but not shown there.
  const open = panel && editor && !landed ? panel : null;

  const openSessions = () => {
    if (onOpenSection) onOpenSection('sessions');
    else document.getElementById('place-sessions')?.scrollIntoView?.({ block: 'start' });
  };
  const chartDoors: WorkflowDoors = {
    ...doors,
    // The settings that set a step are Landing and Sessions, on this page.
    setup: (section) => {
      if (section === 'work') setAsked((was) => ({ section: 'landing', n: was.n + 1 }));
      else if (section === 'sessions') openSessions();
      else doors?.setup?.(section);
    },
  };

  // How the route lands (WORKFLOWCLEAN1h-b): under a saved version it is the version's `land` options; under Current, only the conversion.
  const land = answered === null || !answered.graph || page !== 'repository' || landed ? undefined : draftOf(answered.graph).nodes.find((node) => node.kind === 'land');
  const lands: LandsHere | null = answered === null || page !== 'repository' || landed ? null
    : answered.workflow === 'current' ? { is: 'current', onConvert }
      : editor && land && answered.schema === 2 && answered.version !== null
        ? {
          is: 'version',
          summary: t('projects.setup.summary.landsVersion', {
            method: choiceLabel(t, 'land', 'method', (optionOf(land, 'method') as string | undefined) ?? 'merge'),
            workflow: answered.name ?? answered.workflow,
            version: answered.version,
          }),
          editor: (
            <LandEditor
              key={`${answered.workflow}@${answered.version}`}
              editor={editor}
              graph={answered.graph}
              workflow={{ id: answered.workflow, version: answered.version }}
              lines={start?.landed.tip?.line ? [start.landed.tip.line] : undefined}
              onReads={(on) => { reads.current.land = on; tell(); }}
            />
          ),
        }
        : null;

  const gates: FoldSpec[] = page === 'repository' && start
    ? [{
      id: 'gates',
      title: t('workflow.place.gates.title'),
      summary: gatesSummary(t, start.landed, start.integration ?? null),
      opens: gatesOwed(start.landed, start.integration ?? null),
      body: (
        <PlaceGates
          bare
          landed={start.landed}
          integration={start.integration ?? null}
          busy={accepting}
          onAcceptLanded={onAcceptGates && start.landed.workflow !== 'current' ? onAcceptGates : undefined}
          onAcceptIntegration={onAcceptIntegration}
        />
      ),
    }]
    : [];

  const chart = open && editor
    ? open.is === 'edit'
      ? (
        <WorkflowEditor
          key={open.route}
          {...editor}
          place={place}
          startFrom={open.from ?? editStart(answered?.route === open.route ? answered : null)}
          route={open.route}
          held={held && held.route === open.route ? held.editor : null}
          onHold={(kept) => {
            reported.current = kept;
            onHold?.({ route: open.route, ...(open.from ? { from: open.from } : {}), editor: kept });
          }}
          pipeline="development"
          lines={page === 'repository' && start?.landed.tip?.line ? [start.landed.tip.line] : undefined}
          onClose={() => show(null)}
        />
      )
      : (
        <ChangeWorkflow
          key={open.route}
          list={editor.list}
          error={editor.error}
          current={current}
          place={place}
          route={{
            key: open.route,
            kind: open.route.startsWith('kind:') ? open.route.slice('kind:'.length) : null,
            label: open.route.startsWith('kind:') ? rows.find((row) => row.key === open.route)?.label ?? null : null,
            own: ownChoiceOf(open.route),
          }}
          effective={answered?.route === open.route ? answered : null}
          shown={editor.shown}
          onShow={editor.onShow}
          using={editor.using}
          saving={editor.saving}
          onUse={editor.onUse}
          onPlan={editor.onPlan}
          onSave={editor.onSave}
          onStartFrom={(from) => show({ is: 'edit', route: open.route, from })}
          onConvert={onConvert}
          onClose={() => show(null)}
        />
      )
    : (
      <div className="grid min-w-0 gap-2">
        {page === 'repository' && current.registered === false && (
          <p className="m-0 text-small text-ink-soft"><Inline text={t('workflow.head.unregistered', { workspace: current.workspace })} /></p>
        )}
        {answered === null
          ? <p className="m-0 text-small text-ink-soft">{t(effective === null ? 'workflow.place.chart.older' : 'workflow.place.chart.reading')}</p>
          : (
            <>
              <Prose className="text-ink"><Inline text={partSaid(t, { steps })} /></Prose>
              <EffectiveChart
                effective={answered}
                routeLabel={kind}
                page={page}
                name={name}
                workspace={current.workspace}
                selected={shown.step}
                onSelect={(step) => choose({ step })}
                doors={chartDoors}
              />
            </>
          )}
      </div>
    );

  // A workspace's repositories are its own fold: how many follow it, and how many set rules of their own.
  const repositories: FoldSpec[] = page === 'workspace'
    ? [{
      id: 'repositories',
      title: t('workflow.repositories.title'),
      summary: repositoriesSummary(t, current.repositories ?? null),
      body: <PlaceRepositories repositories={current.repositories ?? null} onOpen={doors?.repository} />,
    }]
    : [];

  const canChange = editor !== undefined && !landed;
  // A route that follows Current has no steps to edit: its door is the conversion (WORKFLOWCLEAN1h-b).
  const onCurrent = !landed && answered?.workflow === 'current';
  // *Ask Daoris* opens the editor on the chart's graph, so the conversation is told that draft with the person's next words (FLOWCHART2d),
  // and puts the start of a request in its box. The editor, once open, is not opened again.
  const openEdit = () => {
    if (onCurrent) onConvert?.();
    else if (open?.is !== 'edit') show({ is: 'edit', route: routeKey, from: editStart(answered) });
  };
  // Change… replaces the editor, so a draft with changes in it is asked about first.
  const openChange = () => {
    if (open?.is === 'edit' && (reported.current?.history.past.length ?? 0) > 0) setDiscarding(true);
    else show({ is: 'change', route: routeKey });
  };
  const askDaoris = () => {
    if (!onCurrent && open?.is !== 'edit') openEdit();
    fillAskDaoris(t('workflow.place.ask.fill', { place: name }));
  };
  const buttons = 'min-h-0 border border-line py-0.5 text-small';
  const chartPress = !wide && !open;
  const hasActs = chartPress || canChange;

  return (
    <div
      ref={pane}
      data-place-workflow=""
      data-columns={wide ? 'two' : 'one'}
      className={cn('grid min-w-0 gap-4', wide && !aside && 'grid-cols-2 items-start gap-6')}
    >
      <div className="grid min-w-0 gap-1.5">
        <Routes rows={rows} chosen={routeKey} onChoose={(next) => choose({ route: next })} />
        {hasActs && (
          <div className="flex flex-wrap items-center gap-1.5">
            {chartPress && (
              <Button variant="ghost" className={buttons} aria-expanded={chartShown} onClick={() => setChartShown((was) => !was)}>
                {t(chartShown ? 'workflow.place.chart.hide' : 'workflow.place.chart.show')}
              </Button>
            )}
            {canChange && (
              <>
                <Button variant="ghost" className={buttons} onClick={openChange}>{t('workflow.place.change.open')}</Button>
                {(!onCurrent || onConvert) && <Button variant="ghost" className={buttons} onClick={openEdit}>{t('workflow.place.steps.open')}</Button>}
                {canAsk && <Button variant="ghost" className={buttons} onClick={askDaoris}>{t('workflow.place.ask.open')}</Button>}
              </>
            )}
          </div>
        )}
        {onCurrent && !open && (
          <p className="m-0 flex flex-wrap items-baseline gap-x-2 text-small text-ink-soft">
            {t('agents.convert.follows')}
            {onConvert && <Button variant="ghost" className={buttons} onClick={onConvert}>{t('agents.convert.open')}</Button>}
          </p>
        )}
        {discarding && (
          <InlineConfirm
            tone="warn"
            label={t('workflow.place.discard.label')}
            says={t('workflow.place.discard.says')}
            meanIt={t('workflow.place.discard.meanIt')}
            onConfirm={(answered) => { setDiscarding(false); show({ is: 'change', route: routeKey }); answered.done(); }}
            onClose={() => setDiscarding(false)}
          />
        )}
        {landed && start && (
          <LandedLines landed={start.landed} page={page} repository={page === 'repository' ? name : null} onVerify={onVerify} apart />
        )}
        {!editor && <p className="m-0 text-meta text-ink-faint"><Inline text={t('workflow.compose.twin')} /></p>}
        {!wide && (open || chartShown) && chart}
        {landing ? landing({ asked: asked.section, again: asked.n, lead: gates, lands }) : gates.length + repositories.length > 0 && <FoldRow folds={[...gates, ...repositories]} />}
      </div>
      {wide && (aside ? createPortal(<div className="min-w-0">{chart}</div>, aside) : <div className="min-w-0">{chart}</div>)}
    </div>
  );
}
