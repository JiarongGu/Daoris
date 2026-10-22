import type { ReactElement } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import { FULL, NOTHING_SET, WITH_A_FAILURE_PATH } from './fixtures';
import { answerOf, GO_AHEAD_REFUSAL, GRAPH_RUN, IN_REVIEW, LANDED } from './runFixtures';
import { WorkflowRunView } from './WorkflowRunView';
import { WorkflowTab } from './WorkflowTab';

// FLOWCHART1d (D186; the flowchart design §2.4, §2.5, §5.1): the Workflow tab and the side bar's Workflow view draw the workflow and
// its run on `Flow`, top to bottom where the pane holds a step's 19rem, and the rail, today's list, at the side bar's floor. The
// rail's words are `WorkflowTab.test.tsx`'s and `WorkflowRunView.test.tsx`'s, unchanged; jsdom lays nothing out, so the pane's width
// is stood in for here, and the edges' geometry is the measured-box check's.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

/** The pane's width as a rendered browser would measure it, and a resize the page's observer hears. */
let paneWidth: number | undefined;
const observers = new Set<ResizeObserverCallback>();
const resizeTo = (width: number) => act(() => {
  paneWidth = width;
  for (const callback of observers) callback([{ contentRect: { width } } as ResizeObserverEntry], {} as ResizeObserver);
});

beforeEach(() => {
  paneWidth = undefined;
  observers.clear();
  localStorage.removeItem('daoris.flow.list');
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
    const width = this.hasAttribute('data-flow-pane') ? paneWidth ?? 0 : 0;
    return { width, height: 0, top: 0, left: 0, right: width, bottom: 0, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
  });
  vi.stubGlobal('ResizeObserver', class {
    private readonly callback: ResizeObserverCallback;
    constructor(callback: ResizeObserverCallback) { this.callback = callback; observers.add(callback); }
    observe() {}
    unobserve() {}
    disconnect() { observers.delete(this.callback); }
  });
});

afterEach(async () => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  await i18n.changeLanguage('en');
});

const placement = (container: HTMLElement) => container.querySelector('[data-flow-pane]')!.getAttribute('data-placement');
const chart = (name = 'How work in engine moves') => screen.getByRole('list', { name });
const node = (name: string, list = chart()) => within(list).getByRole('listitem', { name: new RegExp(`^${name}`) });
const ids = (list = chart()) => within(list).getAllByRole('listitem').map((item) => item.getAttribute('data-flow-node'));
const feet = (item: HTMLElement) => within(item).queryAllByRole('button').filter((button) => button.textContent?.startsWith('→'))
  .map((button) => button.textContent!.slice(1));

describe("the Workflow tab's flowchart", () => {
  it('draws the steps top to bottom where the pane holds them, each a node in order and the end of the line, and never left to right', () => {
    paneWidth = 1232;
    const { container } = render(<WorkflowTab page="repository" name="engine" current={FULL} />);
    expect(placement(container)).toBe('down');
    expect(ids()).toEqual(['work', 'opinion', 'look', 'landing', 'pull-request', ':finished']);
    // The rail is not drawn twice beside it.
    expect(container.querySelector('[data-step]')).toBeNull();
  });

  it('says each edge in its node\'s foot, the move its step names, where the rail said it under the step', () => {
    paneWidth = 1232;
    const { container } = render(<WorkflowTab page="repository" name="engine" current={FULL} />);
    expect(feet(node('The work'))).toEqual(['Second opinion, automatically']);
    expect(feet(node('Your look in dev'))).toEqual(['Landing on a branch, once you say it is reviewed']);
    expect(feet(node('Pull request'))).toEqual(['Finished, once you merge it on the platform']);
    expect(container.querySelector('[data-edge]')).toBeNull();
  });

  it('keeps each step\'s words: its part by its shape and word, who acts, what it is set to, where it was set, and its limit whole', () => {
    paneWidth = 1232;
    render(<WorkflowTab page="repository" name="engine" current={FULL} />);
    const look = node('Your look in dev');
    expect(look).toHaveTextContent('agent + you');
    expect(look.querySelector('svg')).toHaveClass('text-ink-soft');
    expect(look).toHaveTextContent("Set by this repository's review rule.");
    expect(look).toHaveTextContent('works in part');
    expect(node('Second opinion')).toHaveTextContent("The work here lands only once another agent's reading of it is settled");
    expect(node('Second opinion')).toHaveAccessibleDescription(/^agent alone, by codex-acp, else dsh\./);
  });

  it('heads the drawing with Before it lands and the plugins that may hold a start, and divides After it lands at the landing', () => {
    paneWidth = 1232;
    const { container } = render(<WorkflowTab page="repository" name="engine" current={FULL} />);
    expect(screen.getByRole('heading', { name: 'Before it lands' })).toBeInTheDocument();
    expect(screen.getByText(/Before the work starts,/)).toHaveTextContent('Before the work starts, example.hold-by-title may hold it.');
    expect(container.querySelector('[data-flow-section]')).toHaveTextContent('After it lands');
    expect(node('Pull request')).toHaveAccessibleDescription(
      'After it lands. you, by example.pull-request. From Landing on a branch: automatically. Next: Finished, once you merge it on the platform.');
  });

  it('opens the Setup that sets a step with Enter on its node, and its door inside it', async () => {
    paneWidth = 1232;
    const setup = vi.fn();
    const workspaceSetup = vi.fn();
    render(<WorkflowTab page="repository" name="engine" current={FULL} doors={{ setup, workspaceSetup }} />);
    node('Your look in dev').focus();
    await userEvent.keyboard('{Enter}');
    expect(setup).toHaveBeenLastCalledWith('work');
    await userEvent.click(within(node('Second opinion')).getByRole('button', { name: 'Open workspace Setup' }));
    expect(workspaceSetup).toHaveBeenCalledOnce();
  });

  it('is the rail at a pane narrower than a step, and Show as list chooses it where the drawing fits', async () => {
    paneWidth = 1232;
    const { container } = render(<WorkflowTab page="repository" name="engine" current={FULL} />);
    // The side bar's 300 px floor is the rail before its gutters as after them.
    resizeTo(300);
    expect(placement(container)).toBe('rail');
    resizeTo(276);
    expect(placement(container)).toBe('rail');
    expect(screen.getByRole('group', { name: 'How work in engine moves' })).toBeInTheDocument();
    expect(container.querySelectorAll('[data-step]')).toHaveLength(5);
    resizeTo(376);
    expect(placement(container)).toBe('down');
    await userEvent.click(screen.getByRole('button', { name: 'Show as list' }));
    expect(container.querySelectorAll('[data-step]')).toHaveLength(5);
  });

  it('hangs a failure path on a step that names one, a dashed edge to its outcome, said under its step on the rail', () => {
    paneWidth = 1232;
    const { container, unmount } = render(<WorkflowTab page="repository" name="engine" current={WITH_A_FAILURE_PATH} />);
    expect(ids()).toContain('checks:failed');
    expect(node('Sent back once, then you').className).toMatch(/border-dashed/);
    // FABRIC-CUTOVER1e: a check is titled as a graph run's check node is, where it was a step this page had no words for.
    expect(feet(node('The checks'))).toEqual([
      'Finished, automatically', 'Sent back once, then you, if it fails',
    ]);
    // A rank of two, the next step and the outcome beside it: 39.5rem, so the rail at a 680 px window's main area.
    resizeTo(576);
    expect(placement(container)).toBe('rail');
    expect(container.querySelector('[data-step="checks"] [data-failed]')).toHaveTextContent('If it fails, it is sent back once, then waits for you.');
    unmount();
    // Nothing a step does not name: today's Current has no failure path and no group.
    paneWidth = 1232;
    render(<WorkflowTab page="repository" name="engine" current={NOTHING_SET} />);
    expect(ids()).toEqual(['work', 'landing', ':finished']);
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    paneWidth = 1232;
    render(<WorkflowTab page="repository" name="engine" current={FULL} />);
    expect(screen.getByRole('heading', { name: '落地之前' })).toBeInTheDocument();
    const list = chart('engine 中的工作如何推进');
    expect(feet(node('工作本身', list))).toEqual(['第二意见，自动继续']);
    expect(node('你在 dev 中审阅', list)).toHaveTextContent('智能体与你');
  });
});

describe("the Workflow view's run on the flowchart", () => {
  const run = () => chart('How work in engine moves');

  it('lights the run: each step in its state\'s word and tone, the step it stands at current, one not reached dashed, never faint by opacity', () => {
    paneWidth = 672;
    const { container } = render(<WorkflowRunView answer={answerOf(IN_REVIEW)} here="work" />);
    expect(placement(container)).toBe('down');
    const look = node('Your look in dev', run());
    expect(look).toHaveAttribute('aria-current', 'step');
    expect(within(look).getByText('waiting on you')).toHaveClass('text-ink-open');
    // The pill is the node's, once.
    expect(within(look).getAllByText('waiting on you')).toHaveLength(1);
    expect(within(look).getByText(/Waits for your look in/)).toHaveTextContent('Waits for your look in dev: shown at 01234567.');
    expect(within(node('The work', run())).getByText('done')).toHaveClass('text-ink-done');
    expect(node('The work', run())).toHaveTextContent('this session');
    const landing = node('Landing on a branch', run());
    expect(landing.className).toMatch(/border-dashed/);
    expect(container.innerHTML).not.toMatch(/opacity-/);
    expect(node('Finished', run()).className).toMatch(/border-dashed/);
  });

  it('opens a step\'s door with Enter on its node', async () => {
    paneWidth = 672;
    const doors = { session: vi.fn(), quest: vi.fn(), ask: vi.fn(), review: vi.fn() };
    render(<WorkflowRunView answer={answerOf(IN_REVIEW)} doors={doors} />);
    node('The work', run()).focus();
    await userEvent.keyboard('{Enter}');
    expect(doors.session).toHaveBeenCalledWith('s1');
    node('Your look in dev', run()).focus();
    await userEvent.keyboard('{Enter}');
    expect(doors.quest).toHaveBeenCalledWith('q2');
  });

  it('says who passed a step over as its record names them, never "by you" on every skip, and draws no path', () => {
    paneWidth = 1232;
    render(<WorkflowRunView answer={answerOf(GO_AHEAD_REFUSAL)} />);
    const opinion = node('Second opinion', run());
    expect(opinion).toHaveTextContent('skipped');
    expect(opinion).toHaveTextContent('No second opinion is asked before this work lands.');
    expect(opinion).not.toHaveTextContent(/by you/);
    expect(node('Your look in dev', run())).toHaveTextContent('Skipped for this work, by you.');
    // The go-ahead's refusal waits for the person: its failure path, dashed, to its outcome.
    // FABRIC-CUTOVER1e: a go-ahead is titled as a graph run's go-ahead is, where it was a step this page had no words for.
    expect(feet(node('Your go-ahead', run()))).toEqual(['Finished, automatically', 'Waits for you, if you refuse it']);
    expect(screen.queryByText(/taken/)).toBeNull();
  });

  // FABRIC-CUTOVER1e (D186's FLOWCHART1d note): a graph run's drawing is the first that holds a group: its fork and its join are drawn as
  // its graph's wires hold them, each node lit by its record.
  it("draws a graph run's fork and join by its wires, each node lit by its record", () => {
    paneWidth = 1232;
    const { container } = render(<WorkflowRunView answer={answerOf(GRAPH_RUN)} />);
    expect(placement(container)).toBe('down');
    expect(ids()).toEqual(['claimed', 'build', 'lint', 'both', 'halt', 'opinion', 'handed', 'deployed', ':finished']);
    expect(feet(node('The work'))).toEqual(['built by acme.ci, automatically', 'linted by acme.ci, automatically']);
    expect(feet(node('The join'))).toEqual(['Stop, if it fails', 'Second opinion, automatically']);
    expect(node('built by acme.ci')).toHaveAttribute('aria-current', 'step');
    expect(node('built by acme.ci')).toHaveTextContent('Observed on daoris/q1-fix-the-header at 01234567.');
  });

  it('reaches the end of the line once the run finished', () => {
    paneWidth = 672;
    render(<WorkflowRunView answer={answerOf(LANDED)} />);
    const end = node('Finished', run());
    expect(end).toHaveAccessibleName('Finished done');
    expect(end.className).not.toMatch(/border-dashed/);
  });

  it('is the rail at the side bar\'s floor', () => {
    paneWidth = 276;
    const { container } = render(<WorkflowRunView answer={answerOf(IN_REVIEW)} />);
    expect(placement(container)).toBe('rail');
    expect(container.querySelectorAll('[data-step]')).toHaveLength(4);
  });
});
