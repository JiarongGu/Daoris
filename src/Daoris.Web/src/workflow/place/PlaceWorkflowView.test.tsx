import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { type ReactElement, useState } from 'react';
import i18n from '../../locales/i18n';
import { code } from '../../test/code';
import { PlaceAside } from '../../agents/setup/placeAside';
import type { PlaceFlowHold, PlaceView } from '../../agents/setup/placeShell';
import type { CurrentWorkflow, WorkflowEffective } from '../current';
import { LIST, PALETTE } from '../edit/editFixtures';
import {
  cellOf, EFFECTIVE_CURRENT, EFFECTIVE_DOCS, EFFECTIVE_LANDED_NONE, EFFECTIVE_LANDED_VERIFIES, EFFECTIVE_RELEASE, EFFECTIVE_UNREAD,
} from './effectiveFixtures';
import { registerAskFill } from '../../help/askFill';
import { noteTold, openDraftNow } from '../../help/openDraft';
import { MERGE_GRAPH } from '../graphFixtures';
import { RELEASE_PROPOSAL } from '../edit/proposalFixtures';
import { fingerprintOf } from '../draftFingerprint';
import { FULL, GATES_CHANGED, INTEGRATION_CHANGED, NO_VERIFICATION, START, WORKSPACE } from '../fixtures';
import { type LandingHand, PlaceWorkflowView, type PlaceWorkflowProps } from './PlaceWorkflowView';

// SETUPUX1o (D221; the Setup design's §2.3): a place's Workflow draws its routes as a selection, the chosen route's chart (in its own column
// from 64 rem, one press away below), the folds with their owners, and Gates; choosing a route writes nothing. A molecule: every state is
// reached by its props.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const SAVED_DEFAULT: typeof START = {
  ...START,
  routes: [{ kind: null, label: null, paths: [], workflow: 'release', level: 'repository', name: 'Release for engine' }, START.routes[1]!],
};
const WAITING = { ...FULL, start: { ...GATES_CHANGED, integration: INTEGRATION_CHANGED }, effective: EFFECTIVE_CURRENT };
const withRoute = (effective: WorkflowEffective, start: typeof START = START): CurrentWorkflow => ({ ...FULL, start, effective });

const editor = (onOpen = vi.fn()) => ({
  list: LIST, palette: PALETTE, error: null, roles: [], shown: null, onShow: vi.fn(), judged: null, judging: false, onJudge: vi.fn(),
  saving: false, onSave: vi.fn(), using: false, onUse: vi.fn(async () => ({ done: true, refused: null, message: null, changed: true, reach: [] })), onOpen,
});

/** The width the page's pane is, as a browser would measure it. */
let paneWidth: number | undefined;
beforeEach(() => {
  paneWidth = undefined;
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
    const width = this.hasAttribute('data-place-workflow') ? paneWidth ?? 0 : 0;
    return { width, height: 0, top: 0, left: 0, right: width, bottom: 0, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
  });
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
});

// The language goes back inside `act`, with the page still drawn (FRAMEACT1).
afterEach(async () => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  await act(async () => { await i18n.changeLanguage('en'); });
});

/** Holds the route and the step, as the application's shell does above the mounts. */
function Held(props: Partial<PlaceWorkflowProps> & { onView?: (choice: Partial<PlaceView>) => void; first?: Partial<PlaceView> }) {
  const [view, setView] = useState<PlaceView>({ route: 'default', step: null, ...props.first });
  return (
    <PlaceWorkflowView
      name="engine"
      current={WAITING}
      {...props}
      view={view}
      onView={(choice) => {
        props.onView?.(choice);
        setView((was) => ({ route: choice.route ?? was.route, step: choice.route && choice.route !== was.route ? choice.step ?? null : choice.step === undefined ? was.step : choice.step }));
      }}
    />
  );
}

const draw = (props: Partial<Parameters<typeof Held>[0]> = {}) => render(<Held {...props} />);
const routeRow = (label: RegExp | string) => screen.getByRole('button', { name: label });

describe("a place's routes", () => {
  it('are drawn as a selection of rows, the chosen one marked, and not as radios', () => {
    draw({ current: withRoute(EFFECTIVE_CURRENT) });

    const list = screen.getByRole('list', { name: 'Where work here goes' });
    expect(within(list).getAllByRole('listitem')).toHaveLength(3);
    expect(screen.queryAllByRole('radio')).toHaveLength(0);
    expect(routeRow(/^Default work: Current/)).toHaveAttribute('aria-current', 'true');
    expect(routeRow(/^Documentation: Documentation to a pull request/)).not.toHaveAttribute('aria-current');
    expect(routeRow(/^Landed work: Verify the line/)).not.toHaveAttribute('aria-current');
  });

  it('read a saved default as the workflow it follows, and draw its own graph, labelled as the saved workflow default work follows', async () => {
    paneWidth = 1232;
    draw({ current: withRoute(EFFECTIVE_RELEASE, SAVED_DEFAULT) });

    expect(routeRow(/^Default work: Release for engine, /)).toBeInTheDocument();
    expect(screen.getByText('Release for engine v2, the saved workflow default work follows')).toBeInTheDocument();
    // The release graph's own steps, not Current's.
    expect(document.querySelector('[data-flow-node="find"]')).not.toBeNull();
  });

  it('write nothing when one is chosen: the choice is a read of its graph, and no press goes out', async () => {
    const onView = vi.fn();
    const presses = {
      onAcceptGates: vi.fn(), onAcceptIntegration: vi.fn(), onVerify: vi.fn(), editor: editor(),
    };
    draw({ current: withRoute(EFFECTIVE_CURRENT), onView, ...presses });

    await userEvent.click(routeRow(/^Documentation:/));
    await userEvent.click(routeRow(/^Landed work:/));

    expect(onView).toHaveBeenNthCalledWith(1, { route: 'kind:docs' });
    expect(onView).toHaveBeenNthCalledWith(2, { route: 'landed' });
    expect(presses.onAcceptGates).not.toHaveBeenCalled();
    expect(presses.onAcceptIntegration).not.toHaveBeenCalled();
    expect(presses.onVerify).not.toHaveBeenCalled();
    expect(presses.editor.onUse).not.toHaveBeenCalled();
    expect(presses.editor.onOpen).not.toHaveBeenCalled();
  });

  it('draw the kind chosen once its own graph is answered, and say it is being read until then', () => {
    paneWidth = 1232;
    const { unmount } = draw({ current: withRoute(EFFECTIVE_CURRENT), first: { route: 'kind:docs' } });
    expect(screen.getByText('Reading the chart…')).toBeInTheDocument();
    unmount();

    paneWidth = 1232;
    draw({ current: withRoute(EFFECTIVE_DOCS), first: { route: 'kind:docs' } });
    expect(screen.getByText('Documents workflow v1, the saved workflow Documentation follows')).toBeInTheDocument();
  });

  it('read landed Current as *No verification here* and draw no chart for it', () => {
    paneWidth = 1232;
    draw({ current: withRoute(EFFECTIVE_LANDED_NONE, { ...NO_VERIFICATION }), first: { route: 'landed' } });

    expect(routeRow(/^Landed work: No verification here/)).toHaveAttribute('aria-current', 'true');
    expect(screen.getByText('Landed work follows no verification here, so there is no chart to draw.')).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: /moves/ })).toBeNull();
  });

  it('offer *Verify…* only once the landed pipeline is accepted, and only on the landed route', () => {
    const accepted = withRoute(EFFECTIVE_LANDED_VERIFIES, START);
    const { unmount } = draw({ current: accepted, first: { route: 'landed' }, onVerify: vi.fn() });
    expect(screen.getByRole('button', { name: 'Verify…' })).toBeInTheDocument();
    unmount();

    const waiting = withRoute(EFFECTIVE_LANDED_VERIFIES, GATES_CHANGED);
    const second = draw({ current: waiting, first: { route: 'landed' }, onVerify: vi.fn() });
    expect(screen.queryByRole('button', { name: 'Verify…' })).toBeNull();
    second.unmount();

    draw({ current: accepted, onVerify: vi.fn() });
    expect(screen.queryByRole('button', { name: 'Verify…' })).toBeNull();
  });
});

describe("the chosen route's chart", () => {
  it('stands in its own column where the page holds 64 rem, with no press to show it', () => {
    paneWidth = 1232;
    draw({ current: withRoute(EFFECTIVE_RELEASE, SAVED_DEFAULT) });

    expect(screen.queryByRole('button', { name: 'Show the chart' })).toBeNull();
    expect(document.querySelector('[data-place-workflow]')).toHaveAttribute('data-columns', 'two');
    expect(screen.getByRole('region', { name: 'The chart of default work' })).toBeInTheDocument();
  });

  it("stands in the page's own column when the page keeps one, and is one press away when it keeps none, whatever the section's own room", () => {
    paneWidth = 1232;
    const column = document.body.appendChild(document.createElement('div'));
    const { unmount } = render(
      <PlaceAside.Provider value={column}><PlaceWorkflowView name="engine" current={withRoute(EFFECTIVE_RELEASE, SAVED_DEFAULT)} /></PlaceAside.Provider>,
    );
    expect(within(column).getByRole('region', { name: 'The chart of default work' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Show the chart' })).toBeNull();
    unmount();
    column.remove();

    render(<PlaceAside.Provider value={null}><PlaceWorkflowView name="engine" current={withRoute(EFFECTIVE_RELEASE, SAVED_DEFAULT)} /></PlaceAside.Provider>);
    expect(screen.getByRole('button', { name: 'Show the chart' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'The chart of default work' })).toBeNull();
  });

  it('is one press away below it, and the press takes it away again', async () => {
    paneWidth = 624;
    draw({ current: withRoute(EFFECTIVE_RELEASE, SAVED_DEFAULT) });

    expect(document.querySelector('[data-place-workflow]')).toHaveAttribute('data-columns', 'one');
    expect(screen.queryByRole('region', { name: 'The chart of default work' })).toBeNull();
    const show = screen.getByRole('button', { name: 'Show the chart' });
    expect(show).toHaveAttribute('aria-expanded', 'false');

    await userEvent.click(show);
    expect(screen.getByRole('region', { name: 'The chart of default work' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Hide the chart' }));
    expect(screen.queryByRole('region', { name: 'The chart of default work' })).toBeNull();
  });

  it("shows a step's cell on selection and tells the holder, and the holder keeps it", async () => {
    paneWidth = 1232;
    const onView = vi.fn();
    draw({ current: withRoute(EFFECTIVE_CURRENT), onView });

    await userEvent.click(within(document.querySelector<HTMLElement>('[data-flow-node="review"]')!).getByRole('button', { name: 'Its settings' }));

    expect(onView).toHaveBeenCalledWith({ step: 'review' });
    expect(screen.getByRole('region', { name: 'Your review: its settings' })).toBeInTheDocument();
  });

  it("opens Landing from a step's door, and the Sessions section from the standing answer's", async () => {
    paneWidth = 1232;
    const onOpenSection = vi.fn();
    const landing = vi.fn((hand: LandingHand) => <p data-testid="landing">{hand.asked ?? 'folded'}</p>);
    // Asked twice, it is told twice.
    const lastAgain = () => landing.mock.calls.at(-1)![0].again;
    const work = cellOf({ id: 'work', kind: 'claimed' });
    const current = withRoute({
      ...EFFECTIVE_CURRENT,
      cells: EFFECTIVE_CURRENT.cells.map((cell) => (cell.node === 'work'
        ? { ...cell, step: { ...work, settings: { standing: 'prod only on a yes' }, source: { rule: null, level: 'default' as const } } }
        : cell.node === 'landing' ? { ...cell, step: { ...cell.step, source: { rule: 'landing' as const, level: 'repository' as const } } } : cell)),
    });
    draw({ current, landing, onOpenSection, first: { step: 'landing' } });

    expect(screen.getByTestId('landing')).toHaveTextContent('folded');
    await userEvent.click(within(screen.getByRole('region', { name: 'The landing: its settings' })).getByRole('button', { name: 'Change in Setup' }));
    expect(screen.getByTestId('landing')).toHaveTextContent('landing');
    expect(lastAgain()).toBe(1);
    await userEvent.click(within(screen.getByRole('region', { name: 'The landing: its settings' })).getByRole('button', { name: 'Change in Setup' }));
    expect(lastAgain()).toBe(2);
    expect(onOpenSection).not.toHaveBeenCalled();
  });

  it('opens the Sessions section, which only scrolled before', async () => {
    paneWidth = 1232;
    const onOpenSection = vi.fn();
    const work = cellOf({ id: 'work', kind: 'claimed' });
    const current = withRoute({
      ...EFFECTIVE_CURRENT,
      cells: EFFECTIVE_CURRENT.cells.map((cell) => (cell.node === 'work'
        ? { ...cell, step: { ...work, settings: { standing: 'prod only on a yes' }, source: { rule: null, level: 'default' as const } } } : cell)),
    });
    draw({ current, onOpenSection, first: { step: 'work' } });

    await userEvent.click(within(screen.getByRole('region', { name: 'The work: its settings' })).getByRole('button', { name: 'Change in Setup' }));
    expect(onOpenSection).toHaveBeenCalledWith('sessions');
  });
});

describe("a place's Workflow, its folds and Gates", () => {
  it('draws Gates as a fold that opens for an acceptance owed, with both choices, and folds once it is settled', () => {
    draw({ onAcceptGates: vi.fn(), onAcceptIntegration: vi.fn() });

    expect(screen.getByRole('button', { name: 'Gates' })).toHaveAttribute('aria-expanded', 'true');
    expect(within(screen.getByRole('region', { name: 'Gates' })).getAllByRole('checkbox')).toHaveLength(2);
  });

  it('keeps Gates folded and names where it stands once accepted', () => {
    draw({ current: { ...FULL, start: START, effective: EFFECTIVE_CURRENT } });

    const gates = screen.getByRole('button', { name: 'Gates' });
    expect(gates).toHaveAttribute('aria-expanded', 'false');
    expect(gates).toHaveAccessibleDescription('Accepted, blob 3b18e51');
  });

  it('draws the declaration once, in Gates, and not again on the landed route', () => {
    draw({ onAcceptGates: vi.fn(), onAcceptIntegration: vi.fn(), first: { route: 'landed' } });

    expect(document.querySelectorAll('pre')).toHaveLength(2);
    expect(screen.queryByRole('button', { name: 'Accept the gates' })).toBeNull();
  });

  it('presses the landed acceptance and the integration acceptance from the one confirmation', async () => {
    const onAcceptGates = vi.fn((answered: { done: () => void }) => answered.done());
    const onAcceptIntegration = vi.fn((_request: unknown, answered: { done: () => void }) => answered.done());
    draw({ onAcceptGates, onAcceptIntegration });

    // The ticks start empty (SETUPUX1k).
    await userEvent.click(screen.getByRole('checkbox', { name: 'Accept for landed verification' }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'Accept for integration' }));
    await userEvent.click(screen.getByRole('button', { name: 'Accept the chosen…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));

    expect(onAcceptGates).toHaveBeenCalledOnce();
    expect(onAcceptIntegration).toHaveBeenCalledWith({ gates: '0e5751c026e543b2e8ab2eb06099daa1d1e5df47' }, expect.anything());
  });

  it('hands the line and landing folds Gates to stand before them, and draws what they draw', () => {
    const landing = vi.fn((hand: LandingHand) => <p data-testid="landing">{hand.lead.map((fold) => fold.title).join()}</p>);
    draw({ landing });
    expect(screen.getByTestId('landing')).toHaveTextContent('Gates');
  });
});

describe('Change… and Edit steps…', () => {
  it('open in place of the chart, each told so the editor reads wait for them', async () => {
    const onOpen = vi.fn();
    draw({ editor: editor(onOpen) });

    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    expect(onOpen).toHaveBeenLastCalledWith(true);
    expect(screen.getByRole('region', { name: 'Change what its work follows' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(onOpen).toHaveBeenLastCalledWith(false);

    await userEvent.click(screen.getByRole('button', { name: 'Edit steps…' }));
    expect(onOpen).toHaveBeenLastCalledWith(true);
    expect(screen.getByRole('heading', { name: 'Compose a workflow' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Never mind' }));
    expect(onOpen).toHaveBeenLastCalledWith(false);
  });

  // REVIEW2 P1: Landed work is chosen only by its own press, so no panel opened on another route may reach a choice while it is the route shown.
  it('stay out of the way on the landed route: Use here is not offered there, and the panel is for the route it opened on', async () => {
    const using = editor();
    draw({ editor: { ...using, onShow: vi.fn() }, current: withRoute(EFFECTIVE_CURRENT) });

    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    await userEvent.click(routeRow(/^Landed work:/));
    expect(screen.queryByRole('region', { name: 'Change what its work follows' })).toBeNull();
    expect(screen.queryByRole('button', { name: /^Use / })).toBeNull();

    await userEvent.click(routeRow(/^Default work:/));
    await userEvent.click(screen.getByRole('button', { name: 'Edit steps…' }));
    await userEvent.click(routeRow(/^Landed work:/));
    expect(screen.queryByRole('heading', { name: 'Compose a workflow' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Use here' })).toBeNull();
  });

  // REVIEW2 P5: the editor's draft is kept above the mount, so a fold, *Set up…* or a layout change that unmounts the section leaves it as it was.
  function Kept(props: Partial<Parameters<typeof Held>[0]>) {
    const [held, setHeld] = useState<PlaceFlowHold | null>(null);
    const [mounted, setMounted] = useState(true);
    return (
      <>
        <button type="button" onClick={() => setMounted((was) => !was)}>toggle section</button>
        {mounted && <Held {...props} held={held} onHold={setHeld} />}
      </>
    );
  }
  const duplicated = async () => {
    await userEvent.click(await screen.findByRole('listitem', { name: 'find by local-release' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Duplicate' }));
  };

  it('keep the draft when the section unmounts and mounts again, and ask before Change… replaces a draft with changes in it', async () => {
    render(<Kept editor={editor()} current={withRoute(EFFECTIVE_RELEASE, SAVED_DEFAULT)} />);
    await userEvent.click(screen.getByRole('button', { name: 'Edit steps…' }));
    await duplicated();
    expect(screen.getByRole('button', { name: 'Undo' })).toBeEnabled();

    await userEvent.click(screen.getByRole('button', { name: 'toggle section' }));
    expect(screen.queryByRole('heading', { name: 'Compose a workflow' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'toggle section' }));
    // The editor is back on the draft, history and all.
    expect(await screen.findByRole('heading', { name: 'Compose a workflow' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Undo' })).toBeEnabled();

    // Change… asks first; giving up keeps the editor, going on replaces it and lets the draft go.
    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    expect(screen.getByText(/not saved and cannot be got back/)).toBeInTheDocument();
    // The editor's own Cancel is also *Never mind*; the confirmation stands before it.
    await userEvent.click(screen.getAllByRole('button', { name: 'Never mind' })[0]!);
    expect(screen.queryByText(/not saved and cannot be got back/)).toBeNull();
    expect(screen.getByRole('heading', { name: 'Compose a workflow' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Discard the changes' }));
    expect(await screen.findByRole('region', { name: 'Change what its work follows' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'toggle section' }));
    await userEvent.click(screen.getByRole('button', { name: 'toggle section' }));
    expect(screen.queryByRole('heading', { name: 'Compose a workflow' })).toBeNull();
  });

  it('do not ask before Change… where the editor holds no changes', async () => {
    render(<Kept editor={editor()} current={withRoute(EFFECTIVE_RELEASE, SAVED_DEFAULT)} />);
    await userEvent.click(screen.getByRole('button', { name: 'Edit steps…' }));
    await screen.findByRole('heading', { name: 'Compose a workflow' });
    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    expect(screen.queryByText(/not saved and cannot be got back/)).toBeNull();
    expect(await screen.findByRole('region', { name: 'Change what its work follows' })).toBeInTheDocument();
  });

  // SETUPUX1r: Edit steps… opens on the graph the selected route follows, and tells the editor the route and pipeline it was opened for.
  it("open the selected route's effective graph, a saved workflow's newest version as the version to append to, and tell the editor the route", async () => {
    draw({ editor: editor(), current: withRoute(EFFECTIVE_RELEASE, SAVED_DEFAULT) });

    await userEvent.click(screen.getByRole('button', { name: 'Edit steps…' }));
    const steps = await screen.findByRole('list', { name: 'How the steps go on' });
    expect(within(steps).getByRole('listitem', { name: 'find by local-release' })).toBeInTheDocument();
    expect(screen.getByText(/^Saving adds v3 to release;/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Review and save' })).toBeInTheDocument();
    expect(openDraftNow()).toMatchObject({ place: 'engine', route: 'default', pipeline: 'development', workflow: 'release', base: 2 });
  });

  it("open Current's steps as a new workflow, and a kind's route as the route the editor was opened for", async () => {
    draw({ editor: editor(), current: withRoute(EFFECTIVE_DOCS, SAVED_DEFAULT), first: { route: 'kind:docs' } });

    await userEvent.click(screen.getByRole('button', { name: 'Edit steps…' }));
    await screen.findByRole('list', { name: 'How the steps go on' });
    expect(openDraftNow()).toMatchObject({ route: 'kind:docs', pipeline: 'development', source: 'docs-to-pr@v1' });
    expect(openDraftNow()!.graph.nodes).toHaveLength(7);
  });

  it('open an empty draft where the route draws no graph, as it always did', async () => {
    draw({ editor: editor(), current: withRoute(EFFECTIVE_UNREAD, SAVED_DEFAULT) });

    await userEvent.click(screen.getByRole('button', { name: 'Edit steps…' }));
    await screen.findByRole('heading', { name: 'Compose a workflow' });
    expect(screen.queryByRole('list', { name: 'How the steps go on' })).toBeNull();
    expect(openDraftNow()!.graph.nodes).toHaveLength(0);
  });

  it("chooses a saved version for this repository through Change…, the editor's own press", async () => {
    const bridge = editor();
    draw({ editor: bridge });

    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Current' }));
    await userEvent.click(screen.getByRole('button', { name: 'Use Current here' }));

    expect(bridge.onUse).toHaveBeenCalledWith({ repository: 'engine', workflow: 'current' });
  });

  it("starts the step editor from a preset's steps, from Change…", async () => {
    draw({ editor: editor() });

    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    await userEvent.click(screen.getByRole('tab', { name: 'Presets' }));
    await userEvent.click(screen.getByRole('button', { name: 'Documents workflow' }));
    await userEvent.click(screen.getByRole('button', { name: 'Edit steps from Documents workflow' }));

    expect(screen.getByRole('heading', { name: 'Compose a workflow' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Change what its work follows' })).toBeNull();
  });

  it("choose per route: the selected route's kind goes out with the plan and the press, and the place's own choice is the one it can clear", async () => {
    const onPlan = vi.fn(async () => ({
      done: false, plan: true, refused: null, message: null, changed: true, reach: [], lowers: [], places: [], keeps: null, expected: 'use-docs',
    }));
    const bridge = { ...editor(), list: { ...LIST, use: ['plan'] }, onPlan };
    draw({ editor: bridge, current: withRoute(EFFECTIVE_DOCS, SAVED_DEFAULT), first: { route: 'kind:docs' } });

    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    // Documentation follows what the workspace maps, so there is no choice of the repository's own to clear.
    expect(screen.queryByRole('button', { name: "Use the workspace's" })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Current' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Use Current here' }));

    expect(onPlan).toHaveBeenCalledWith({ repository: 'engine', kind: 'docs', workflow: 'current', plan: true });
    expect(bridge.onUse).toHaveBeenCalledWith({ repository: 'engine', kind: 'docs', workflow: 'current', expected: 'use-docs' });
  });

  it("clears the repository's own default through Use the workspace's, asking nothing of the plan where the driver announces none", async () => {
    const bridge = editor();
    draw({ editor: bridge, current: withRoute(EFFECTIVE_RELEASE, SAVED_DEFAULT) });

    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    await userEvent.click(screen.getByRole('button', { name: "Use the workspace's" }));

    expect(bridge.onUse).toHaveBeenCalledWith({ repository: 'engine' });
  });

  it('are not offered for landed work, which WORKFLOW_USE does not choose', () => {
    draw({ editor: editor(), first: { route: 'landed' } });
    expect(screen.queryByRole('button', { name: 'Change…' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Edit steps…' })).toBeNull();
  });

  it("says the terminal's way where no editor is handed", () => {
    draw();
    expect(screen.queryByRole('button', { name: 'Change…' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Edit steps…' })).toBeNull();
    expect(screen.getByText(code('daoris driver workflow new <id> --from <default>'))).toBeInTheDocument();
  });
});

describe("a workspace's own Workflow", () => {
  const workspace = (): CurrentWorkflow => ({ ...WORKSPACE, start: { ...START, integration: null }, effective: { ...EFFECTIVE_CURRENT, route: 'default' } });

  it('draws its routes and chart, and no Gates, which stay each repository\'s', () => {
    paneWidth = 1232;
    draw({ page: 'workspace', name: 'aurora', current: workspace() });

    expect(screen.getByRole('list', { name: 'Where work here goes' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'The chart of default work' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Gates' })).toBeNull();
  });

  it('lists its repositories, each following it or setting its own, with a door to the one that sets its own', async () => {
    const repository = vi.fn();
    draw({ page: 'workspace', name: 'aurora', current: workspace(), doors: { repository } });

    const fold = screen.getByRole('button', { name: 'Repositories here' });
    expect(fold).toHaveAccessibleDescription('2 repositories, 1 with rules of their own');
    await userEvent.click(fold);
    const list = screen.getByRole('list', { name: 'Repositories here' });
    expect(within(list).getByText('follows this')).toBeInTheDocument();
    await userEvent.click(within(list).getByRole('button', { name: "Open engine's workflow" }));
    expect(repository).toHaveBeenCalledWith('engine');
  });
});

describe('the answer on its way', () => {
  it('says it is reading, and a refusal in the driver\'s words, in place of the body', () => {
    const { unmount } = draw({ current: null, reading: true });
    expect(screen.getByText('Reading how work moves here…')).toBeInTheDocument();
    unmount();

    draw({ current: null, refusal: 'the driver is still coming up — try again in a moment.' });
    expect(screen.getByText('the driver is still coming up — try again in a moment.')).toBeInTheDocument();
  });

  it('says a driver that answers no graph for a route, and keeps the routes', () => {
    paneWidth = 1232;
    draw({ current: { ...FULL, start: START } });
    expect(screen.getByText("This driver does not draw a route's graph yet, so the chart waits for it.")).toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'Where work here goes' })).toBeInTheDocument();
  });

  it('says its words in Chinese', async () => {
    await act(async () => { await i18n.changeLanguage('zh'); });
    draw();
    expect(screen.getByRole('list', { name: '工作在这里如何分流' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '检查项' })).toBeInTheDocument();
  });
});

// FLOWCHART2g (D223 §4.4): asking from a place, and from a conversation's draft card.
describe('Ask Daoris from a place and from a conversation', () => {
  const fingerprint = () => fingerprintOf(MERGE_GRAPH);

  it("is offered only where the window has Ask Daoris: it opens Edit steps… on the chart's graph and puts the words in Ask Daoris's box", async () => {
    const onOpen = vi.fn();
    draw({ editor: editor(onOpen), current: withRoute(EFFECTIVE_RELEASE, SAVED_DEFAULT) });
    expect(screen.queryByRole('button', { name: 'Ask Daoris' })).toBeNull();

    const fill = vi.fn();
    let release = () => {};
    act(() => { release = registerAskFill(fill); });
    await userEvent.click(await screen.findByRole('button', { name: 'Ask Daoris' }));

    expect(onOpen).toHaveBeenLastCalledWith(true);
    const steps = await screen.findByRole('list', { name: 'How the steps go on' });
    expect(within(steps).getByRole('listitem', { name: 'find by local-release' })).toBeInTheDocument();
    // The editor registers its draft, which the conversation is told with the person's next words (FLOWCHART2d).
    expect(openDraftNow()).toMatchObject({ place: 'engine', workflow: 'release', base: 2 });
    expect(fill).toHaveBeenCalledTimes(1);
    expect(fill.mock.calls[0]![0]).toContain('engine');
    act(() => release());
  });

  it('is not offered for landed work, which Change… and Edit steps… are not either', () => {
    const release = registerAskFill(vi.fn());
    draw({ editor: editor(), current: withRoute(EFFECTIVE_LANDED_VERIFIES), first: { route: 'landed' } });
    expect(screen.queryByRole('button', { name: 'Ask Daoris' })).toBeNull();
    release();
  });

  it("opens the editor on the draft a conversation was told, once, for the card's Open in the editor, with the proposal shown", async () => {
    noteTold('s1', { place: 'engine', graph: MERGE_GRAPH, fingerprint: fingerprint() });
    const onOpened = vi.fn();
    const proposal = { ...RELEASE_PROPOSAL, why: 'A release after the line.', problems: 0, maker: { agent: 'Claude Code', models: ['claude-opus-5-5'] } };
    draw({
      editor: { ...editor(), proposal }, current: withRoute(EFFECTIVE_RELEASE, SAVED_DEFAULT), onOpened,
      opens: { draft: fingerprint(), workflow: 'release', name: 'Release for engine', base: 2 },
    });

    // The draft told, not the chart's graph, so the proposal's changes can be taken; the proposal stands above it.
    expect(await screen.findByText('Ask Daoris proposes a change')).toBeInTheDocument();
    expect(openDraftNow()!.graph.nodes).toHaveLength(MERGE_GRAPH.nodes.length);
    expect(openDraftNow()).toMatchObject({ workflow: 'release', base: 2 });
    expect(onOpened).toHaveBeenCalledTimes(1);
  });

  it("falls back to the chart's graph when the draft told is no longer held", async () => {
    const onOpened = vi.fn();
    draw({
      editor: editor(), current: withRoute(EFFECTIVE_RELEASE, SAVED_DEFAULT), onOpened,
      opens: { draft: 'g1-0000000000000000', workflow: 'release', name: 'Release for engine', base: 2 },
    });
    const steps = await screen.findByRole('list', { name: 'How the steps go on' });
    expect(within(steps).getByRole('listitem', { name: 'find by local-release' })).toBeInTheDocument();
    expect(onOpened).toHaveBeenCalledTimes(1);
  });

  it('says it in Chinese', async () => {
    await act(async () => { await i18n.changeLanguage('zh'); });
    const release = registerAskFill(vi.fn());
    draw({ editor: editor(), current: withRoute(EFFECTIVE_RELEASE, SAVED_DEFAULT) });
    expect(await screen.findByRole('button', { name: '问道衍' })).toBeInTheDocument();
    release();
  });
});
