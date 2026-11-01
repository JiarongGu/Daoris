import { type ReactNode, useContext, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';
import { PlaceAside } from '../../agents/setup/placeAside';
import type { PlaceView } from '../../agents/setup/placeShell';
import { cn } from '../../lib/cn';
import { Button, Inline, Prose, SkeletonRows } from '../../design/ui';
import { useWidth } from '../../design/useWidth';
import { FoldRow, type FoldSpec } from '../../projects/setup/FoldRow';
import type { IntegrationRequest } from '../../settings/repository/IntegrationDeclaration';
import type { Answered } from '../../work/InlineConfirm';
import type { WorkflowStart } from '../current';
import { EffectiveChart } from '../EffectiveChart';
import { WorkflowEditor } from '../edit/WorkflowEditor';
import { type RouteRow, routeRows } from '../effective';
import { partSaid } from '../said';
import { type WorkflowTabProps } from '../WorkflowTab';
import type { WorkflowDoors } from '../WorkflowChart';
import { LandedLines } from '../WorkflowStartNode';
import { ChangeWorkflow, type PresetStart } from './ChangeWorkflow';
import { gatesOwed, gatesSummary, PlaceGates } from './PlaceGates';
import { PlaceRepositories, repositoriesSummary } from './PlaceRepositories';

/** What the folds under the routes are handed: the section a step's door asked to see open (and how many times), and the folds to stand first. */
export type LandingHand = {
  asked: 'landing' | null;
  again: number;
  /** Gates, which the page owns, to stand before the folds of the line and its landing. */
  lead: readonly FoldSpec[];
};

/** What the section draws under the routes: the line's landing rows, handed in by the page that holds the settings. */
export type PlaceWorkflowProps = WorkflowTabProps & {
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
};

type Panel = { is: 'change' } | { is: 'edit'; from?: PresetStart } | null;

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
  view, onView, onAcceptIntegration, landing, onOpenSection,
}: PlaceWorkflowProps) {
  const { t } = useTranslation();
  const pane = useRef<HTMLDivElement>(null);
  const width = useWidth(pane);
  const aside = useContext(PlaceAside);
  const [local, setLocal] = useState<PlaceView>(DEFAULT_VIEW);
  const [panel, setPanel] = useState<Panel>(null);
  const [chartShown, setChartShown] = useState(false);
  const [asked, setAsked] = useState<{ section: 'landing' | null; n: number }>({ section: null, n: 0 });
  const place = page === 'workspace' ? { workspace: name } : { repository: name };
  const shown = view ?? local;
  const choose = (choice: Partial<PlaceView>) => {
    if (onView) onView(choice);
    else setLocal((was) => ({ route: choice.route ?? was.route, step: choice.route && choice.route !== was.route ? choice.step ?? null : choice.step === undefined ? was.step : choice.step }));
  };
  const show = (next: Panel) => { setPanel(next); editor?.onOpen?.(next !== null); };

  useEffect(() => {
    if (asked.section) document.querySelector('[data-place-row="landing"]')?.scrollIntoView?.({ block: 'nearest' });
  }, [asked]);

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

  const chart = panel && editor
    ? panel.is === 'edit'
      ? <WorkflowEditor {...editor} place={place} startFrom={panel.from} onClose={() => show(null)} />
      : (
        <ChangeWorkflow
          list={editor.list}
          error={editor.error}
          current={current}
          place={place}
          using={editor.using}
          onUse={editor.onUse}
          onStartFrom={(from) => show({ is: 'edit', from })}
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
  const buttons = 'min-h-0 border border-line py-0.5 text-small';
  const chartPress = !wide && !panel;
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
                <Button variant="ghost" className={buttons} onClick={() => show({ is: 'change' })}>{t('workflow.place.change.open')}</Button>
                <Button variant="ghost" className={buttons} onClick={() => show({ is: 'edit' })}>{t('workflow.place.steps.open')}</Button>
              </>
            )}
          </div>
        )}
        {landed && start && (
          <LandedLines landed={start.landed} page={page} repository={page === 'repository' ? name : null} onVerify={onVerify} apart />
        )}
        {!editor && <p className="m-0 text-meta text-ink-faint"><Inline text={t('workflow.compose.twin')} /></p>}
        {!wide && (panel || chartShown) && chart}
        {landing ? landing({ asked: asked.section, again: asked.n, lead: gates }) : gates.length + repositories.length > 0 && <FoldRow folds={[...gates, ...repositories]} />}
      </div>
      {wide && (aside ? createPortal(<div className="min-w-0">{chart}</div>, aside) : <div className="min-w-0">{chart}</div>)}
    </div>
  );
}
