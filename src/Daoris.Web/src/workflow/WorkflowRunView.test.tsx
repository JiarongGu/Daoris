import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { code } from '../test/code';
import type { RunStep, WorkflowRun } from './run';
import {
  answerOf, FAILED, HELD_FOR_OPINION, IN_REVIEW, LANDED, MERGED, OPINION_DISPUTED, OPINION_SETTLED, PULL_REQUEST_OPEN, SEVERAL,
  WAITING_ON_YOU, WORKING,
} from './runFixtures';
import { WorkflowLine } from './WorkflowLine';
import { WorkflowRunView } from './WorkflowRunView';

// WORKFLOW1c (D157 point 12; the workflow design §5.2, §7): the session's side bar view *Workflow* draws its run on the chart of
// the workflow it follows, each step where it stands, the person's part kept as the rail's shape and its word; and a quest's and
// an ask's pages carry the run in one line whose door opens it. Molecules: every state is reached by its props.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

/** The step rows, in order, as a reader meets them. */
const steps = () => screen.getAllByRole('listitem').filter((item) => item.hasAttribute('data-step'));
const row = (id: string) => steps().find((item) => item.getAttribute('data-step') === id)!;

describe("the session's Workflow view", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws the run on the chart: each step, its part, where it stands, and where the run stands in a few words', () => {
    render(<WorkflowRunView answer={answerOf(IN_REVIEW)} here="work" />);

    expect(screen.getByText('Workflow: waits for your look')).toBeInTheDocument();
    expect(screen.getByText('engine · Current workflow, version 06e90f358edc')).toBeInTheDocument();
    expect(steps().map((item) => item.getAttribute('data-step'))).toEqual(['work', 'look', 'landing', 'pull-request']);

    const work = row('work');
    expect(work).toHaveTextContent('The work');
    expect(work).toHaveTextContent('agent alone');
    expect(work).toHaveTextContent('done');
    expect(work).toHaveTextContent('this session');
    expect(work).toHaveTextContent('The agent finished: 1 quest here is done.');

    const look = row('look');
    expect(look).toHaveAttribute('aria-current', 'step');
    expect(look).toHaveTextContent('agent + you');
    expect(look).toHaveTextContent('waiting on you');
    expect(within(look).getByText(/Waits for your look in/)).toHaveTextContent('Waits for your look in dev: shown at 01234567.');
    expect(look.querySelector('code')).toHaveTextContent('dev');
  });

  it("draws a step the work has not come to faint, with no state's words and no door", () => {
    render(<WorkflowRunView answer={answerOf(IN_REVIEW)} doors={{ review: vi.fn(), session: vi.fn() }} />);
    const landing = row('landing');

    expect(landing).toHaveClass('opacity-60');
    expect(landing).toHaveTextContent('not reached');
    expect(within(landing).queryByRole('button')).toBeNull();
    expect(row('look')).not.toHaveClass('opacity-60');
  });

  it("wears open's hue only where the person is waited on, done's green for done, and red only for a failure", () => {
    const { unmount } = render(<WorkflowRunView answer={answerOf(IN_REVIEW)} />);
    expect(within(row('look')).getByText('waiting on you')).toHaveClass('text-ink-open');
    expect(within(row('work')).getByText('done')).toHaveClass('text-ink-done');
    unmount();

    render(<WorkflowRunView answer={answerOf(FAILED)} />);
    expect(within(row('work')).getByText('failed')).toHaveClass('text-ink-danger');
    expect(screen.getByText('Workflow: its last session failed')).toBeInTheDocument();
  });

  it("keeps the person's part the rail's shape and its word, never a hue", () => {
    render(<WorkflowRunView answer={answerOf(IN_REVIEW)} />);
    const marks = steps().map((item) => item.querySelector('svg'));
    for (const mark of marks) expect(mark).toHaveClass('text-ink-soft');
    expect(row('pull-request')).toHaveTextContent('you');
  });

  it('opens each step where its record is', async () => {
    const doors = { session: vi.fn(), quest: vi.fn(), ask: vi.fn(), review: vi.fn() };
    const { unmount } = render(<WorkflowRunView answer={answerOf(IN_REVIEW)} doors={doors} />);
    await userEvent.click(within(row('work')).getByRole('button', { name: 'Open its session' }));
    expect(doors.session).toHaveBeenCalledWith('s1');
    // The set-up step's page, where the review is answered.
    await userEvent.click(within(row('look')).getByRole('button', { name: 'Open quest #q2' }));
    expect(doors.quest).toHaveBeenCalledWith('q2');
    unmount();

    const again = render(<WorkflowRunView answer={answerOf(WAITING_ON_YOU)} doors={doors} />);
    expect(row('work')).toHaveTextContent('Waits for your go-ahead #1: write the configuration to dev');
    await userEvent.click(within(row('work')).getByRole('button', { name: 'Open the ask' }));
    expect(doors.ask).toHaveBeenCalledWith('a1');
    again.unmount();

    const accept = render(<WorkflowRunView answer={answerOf(OPINION_SETTLED)} doors={doors} />);
    await userEvent.click(within(row('landing')).getByRole('button', { name: 'Open its review' }));
    expect(doors.review).toHaveBeenCalledWith('s1');
    accept.unmount();

    render(<WorkflowRunView answer={answerOf(PULL_REQUEST_OPEN)} doors={doors} />);
    expect(within(row('pull-request')).getByRole('link', { name: /Open pull request/ })).toHaveAttribute('href', 'https://example.test/pull/7');
    expect(row('landing')).toHaveTextContent('Landed on work/q1-fix-the-header, accepted automatically.');
    expect(row('pull-request')).toHaveTextContent('Open: waits for you to merge it on the platform.');
    expect(screen.getByText('Workflow: landed; pull request open')).toBeInTheDocument();
  });

  it("draws the owner's control under the step the run stands at, and nowhere else", () => {
    render(<WorkflowRunView answer={answerOf(IN_REVIEW)} controls={{ look: <button type="button">Reviewed</button>, work: <button type="button">Never</button> }} />);

    expect(within(row('look')).getByRole('button', { name: 'Reviewed' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Never' })).toBeNull();
  });

  it("stands at the second opinion while its gate holds, in the gate's words, and past it once settled", () => {
    const { unmount } = render(<WorkflowRunView answer={answerOf(HELD_FOR_OPINION)} />);
    expect(row('opinion')).toHaveAttribute('aria-current', 'step');
    expect(row('opinion')).toHaveTextContent('waiting on an agent');
    expect(row('opinion')).toHaveTextContent('The working session is answering its 3 findings.');
    expect(row('landing')).toHaveTextContent('not reached');
    expect(screen.getByText('Workflow: answering a second opinion')).toBeInTheDocument();
    unmount();

    const settled = render(<WorkflowRunView answer={answerOf(OPINION_SETTLED)} />);
    expect(row('opinion')).toHaveTextContent('Settled: read by Codex (OpenAI), with nothing left disputed.');
    expect(row('landing')).toHaveAttribute('aria-current', 'step');
    settled.unmount();
  });

  it("opens the review where the gate holds the work on you, and says Go on anyway's terminal twin until the review draws it", async () => {
    const review = vi.fn();
    render(<WorkflowRunView answer={answerOf(OPINION_DISPUTED)} doors={{ review, session: vi.fn() }} />);
    const opinion = row('opinion');

    expect(opinion).toHaveTextContent('waiting on you');
    expect(within(opinion).getByText('waiting on you')).toHaveClass('text-ink-open');
    expect(opinion).toHaveTextContent('1 finding is disputed: a must the working session did not fix and no recheck withdrew.');
    expect(opinion).toHaveTextContent('daoris-driver opinion anyway s1 "…" goes on without it');
    // No page's press for it yet (XAGENT1g): the door is the review, beside its Accept.
    expect(within(opinion).queryByRole('button', { name: /Go on anyway/ })).toBeNull();
    await userEvent.click(within(opinion).getByRole('button', { name: 'Open its review' }));
    expect(review).toHaveBeenCalledWith('s1');
    expect(screen.getByText('Workflow: a second opinion waits for you')).toBeInTheDocument();
  });

  it("words each of the gate's states", () => {
    const step = (detail: string, state: string, facts: Partial<RunStep> = {}): WorkflowRun => ({
      ...OPINION_SETTLED, steps: [OPINION_SETTLED.steps[0]!, { ...OPINION_SETTLED.steps[1]!, state, detail, ...facts }, OPINION_SETTLED.steps[2]!],
    });
    const said = (run: WorkflowRun) => {
      const { unmount } = render(<WorkflowRunView answer={answerOf(run)} />);
      const text = row('opinion').textContent;
      unmount();
      return text;
    };

    expect(said(step('not-asked', 'working'))).toContain("Asked at the driver's next look.");
    expect(said(step('reading', 'working'))).toContain('Being read by Codex (OpenAI).');
    expect(said(step('read-again', 'working'))).toContain('Read again by Codex (OpenAI): the commits made in answer.');
    expect(said(step('commits-since', 'waiting-on-you', { count: 2 }))).toContain('2 commits since were not read by another agent');
    expect(said(step('unavailable', 'waiting-on-you', { code: 'no-reviewer' })))
      .toContain('No second opinion could be had: no listed reviewer of another maker is installed. The rule requires one');
    expect(said(step('unavailable', 'skipped', { code: 'out-of-time' }))).toContain('The rule does not require one, so nothing waits for it.');
    expect(said(step('anyway', 'done', { words: 'a typo' }))).toContain('You went on without a settled second opinion.');
    expect(said(step('myself', 'done'))).toContain("You looked at it yourself in place of another agent's reading.");
    expect(said(step('answered', 'done', { code: 'reviewed' }))).toContain('Your Reviewed answered the second opinion.');
    expect(said(step('answered', 'done', { code: 'press' }))).toContain('Your Accept answered the second opinion.');
    expect(said(step('none', 'skipped'))).toContain('No second opinion is asked before this work lands.');
    expect(said(step('landed', 'done'))).toContain('It let the work go when the work landed.');
    expect(said(step('unread', 'not-known', { words: 'the service did not answer' })))
      .toContain('Whether a second opinion holds it could not be read: the service did not answer');
  });

  it('says a run finished: landed, merged, at the end of the line', () => {
    const { unmount } = render(<WorkflowRunView answer={answerOf(LANDED)} />);
    expect(screen.getByText('Workflow: landed')).toBeInTheDocument();
    expect(row('landing')).toHaveTextContent('Landed into its line.');
    expect(screen.getByText('Finished').parentElement).toHaveTextContent('Finisheddone');
    unmount();

    render(<WorkflowRunView answer={answerOf(MERGED)} />);
    expect(screen.getByText('Workflow: merged')).toBeInTheDocument();
    expect(row('pull-request')).toHaveTextContent('Merged on the platform.');
  });

  it("draws an ask's runs apart, each named by its repository", () => {
    render(<WorkflowRunView answer={SEVERAL} />);

    expect(screen.getByText('engine: waits for your look')).toBeInTheDocument();
    expect(screen.getByText('game: landed; pull request open')).toBeInTheDocument();
    expect(screen.getAllByRole('group')).toHaveLength(2);
  });

  it("says a state or a detail it has no words for as recorded, never dropping the step", () => {
    const odd: WorkflowRun = {
      ...WORKING,
      steps: [{ ...WORKING.steps[0]!, state: 'waiting-on-service', detail: 'checks' } as RunStep, WORKING.steps[1]!],
    };
    render(<WorkflowRunView answer={answerOf(odd)} />);

    expect(row('work')).toHaveTextContent('waiting-on-service');
    expect(row('work')).toHaveTextContent('Shown as recorded: waiting-on-service, checks.');
  });

  it("says it is reading, a refusal in the driver's words, and no run in the driver's sentence", () => {
    const { rerender } = render(<WorkflowRunView reading />);
    expect(screen.getByText('Reading where this work stands…')).toBeInTheDocument();

    rerender(<Tooltip.Provider><WorkflowRunView refusal="the driver is still coming up — try again in a moment." /></Tooltip.Provider>);
    expect(screen.getByText('the driver is still coming up — try again in a moment.')).toBeInTheDocument();

    rerender(<Tooltip.Provider><WorkflowRunView answer={{ runs: [], problem: 'session `c1` serves no quest, so no workflow runs for it.' }} /></Tooltip.Provider>);
    expect(screen.getByText(code('c1'))).toBeInTheDocument();
    expect(screen.queryByRole('list')).toBeNull();
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<WorkflowRunView answer={answerOf(IN_REVIEW)} here="work" />);

    expect(screen.getByText('工作流：等你审阅')).toBeInTheDocument();
    expect(row('look')).toHaveTextContent('等你处理');
    expect(row('look')).toHaveTextContent('智能体与你');
    expect(row('work')).toHaveTextContent('本会话');
    expect(row('work')).toHaveTextContent('智能体已完成：这里的 1 个委托已完成。');
    expect(row('landing')).toHaveTextContent('未到达');
  });
});

describe("a quest's and an ask's one line", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says where the run stands, and its door opens the run', async () => {
    const open = vi.fn();
    render(<WorkflowLine runs={[IN_REVIEW]} onOpen={open} />);

    const door = screen.getByRole('button', { name: 'Workflow: waits for your look' });
    expect(door).toHaveClass('text-ink-open');
    await userEvent.click(door);
    expect(open).toHaveBeenCalledWith(IN_REVIEW);
  });

  it("says each of an ask's runs apart, and none where there is none", () => {
    const { container, rerender } = render(<WorkflowLine runs={SEVERAL.runs} onOpen={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'engine: waits for your look' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'game: landed; pull request open' })).toBeInTheDocument();

    rerender(<Tooltip.Provider><WorkflowLine runs={[]} /></Tooltip.Provider>);
    expect(container).toBeEmptyDOMElement();
  });

  it('is said with no door where nothing can open it, and its hue is the person only where it waits on them', () => {
    render(<WorkflowLine runs={[WORKING]} />);
    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.getByText('Workflow: the agent is working')).toHaveClass('text-ink-soft');
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<WorkflowLine runs={[PULL_REQUEST_OPEN]} onOpen={vi.fn()} />);
    expect(screen.getByRole('button', { name: '工作流：已落地；拉取请求已开启' })).toBeInTheDocument();
  });
});
