import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import { code } from '../test/code';
import type { RunStep, WorkflowRun } from './run';
import {
  answerOf, FAILED, GRAPH_RUN, GRAPH_RUN_ADDED_LOOK, GRAPH_RUN_GO_AHEAD, GRAPH_RUN_GO_AHEAD_KEPT, GRAPH_RUN_UNJUDGED, HELD_FOR_OPINION, IN_REVIEW, LANDED, MERGED, OPINION_DISPUTED,
  OPINION_SETTLED, PULL_REQUEST_OPEN, SEVERAL, WAITING_ON_YOU, WORKING,
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

  it('names the bound version beside its graph', () => {
    render(<WorkflowRunView answer={answerOf({ ...IN_REVIEW, workflow: { ...IN_REVIEW.workflow, id: 'release', boundVersion: 1 } })} />);
    expect(screen.getByText('engine · release workflow, version 1')).toBeInTheDocument();
    expect(screen.queryByText(/Current workflow/)).toBeNull();
  });

  // FABRIC-PIPELINE1a (the pipeline design §1.3): the run's view heads with the route that chose its workflow, as its binding keeps it.
  it('heads with the route that chose its workflow', async () => {
    const routed = (route: NonNullable<WorkflowRun['workflow']['route']>) =>
      answerOf({ ...IN_REVIEW, workflow: { ...IN_REVIEW.workflow, id: 'release', boundVersion: 1, route } });
    const { unmount } = render(<WorkflowRunView answer={routed({ level: 'workspace-kind', kind: 'docs', label: 'Documentation' })} />);
    expect(screen.getByText('Its route: chosen by the workspace aurora for Documentation.')).toBeInTheDocument();
    unmount();

    const chosen = render(<WorkflowRunView answer={routed({ level: 'task' })} />);
    expect(screen.getByText('Its route: chosen by its ask.')).toBeInTheDocument();
    chosen.unmount();

    await i18n.changeLanguage('zh');
    render(<WorkflowRunView answer={routed({ level: 'repository' })} />);
    expect(screen.getByText('它的路线：engine 的默认。')).toBeInTheDocument();
  });

  it('says no route where a driver before it keeps none', () => {
    render(<WorkflowRunView answer={answerOf(IN_REVIEW)} />);
    expect(screen.queryByText(/^Its route:/)).toBeNull();
  });

  it('shows an unread binding without a completed diagram', () => {
    const run = { ...IN_REVIEW, steps: [], at: null, problem: 'Its stored binding could not be read.' };
    render(<WorkflowRunView answer={answerOf(run)} />);
    expect(screen.getByText('Its stored binding could not be read.')).toBeInTheDocument();
    expect(screen.queryByText('done')).toBeNull();
    expect(screen.queryByText(/Current workflow/)).toBeNull();
    expect(screen.queryByText('Workflow: finished')).toBeNull();
  });

  it('names a bound run in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<WorkflowRunView answer={answerOf({ ...IN_REVIEW, workflow: { ...IN_REVIEW.workflow, id: 'release', boundVersion: 1 } })} />);
    expect(screen.getByText('engine · release 工作流，版本 1')).toBeInTheDocument();
  });

  it('shows a kind-path hold beside the bound graph instead of claiming it only waits for Accept', () => {
    render(<WorkflowRunView answer={answerOf({ ...IN_REVIEW,
      workflowGate: { state: 'kind-paths', holds: true, says: 'Holds: keep this workflow in the terminal.' } })} />);
    expect(screen.getByRole('region', { name: 'Workflow before it lands' })).toHaveTextContent('keep this workflow');
    expect(screen.getByText('Workflow: its workflow holds it')).toBeInTheDocument();
  });

  it('preserves why a bound step cannot start', () => {
    render(<WorkflowRunView answer={answerOf({ ...IN_REVIEW, steps: IN_REVIEW.steps.map((step) => step.step.kind === 'look'
      ? { ...step, state: 'cannot-start', detail: 'cannot-start', words: 'No declared reviewer.', step: { ...step.step, kind: 'opinion' } }
      : step) })} />);
    expect(row('look')).toHaveTextContent('Cannot start: No declared reviewer.');
  });

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

    // Faint at a token ink, never by opacity (FLOWCHART1d: the rail is the flowchart's, the flowchart design §3.3, trap 9).
    expect(landing).toHaveAttribute('data-reached', 'false');
    expect(landing.outerHTML).not.toMatch(/opacity-/);
    expect(landing).toHaveTextContent('not reached');
    expect(within(landing).queryByRole('button')).toBeNull();
    expect(row('look')).not.toHaveAttribute('data-reached');
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

  // FLOWCHART1c: a landing that made a branch opens that branch's page, with how it landed; one into the line keeps no branch to open.
  it("opens a landed branch's page from its landing step, and nothing from a landing into the line", async () => {
    const branch = vi.fn();
    const { unmount } = render(<WorkflowRunView answer={answerOf(PULL_REQUEST_OPEN)} doors={{ branch }} />);
    await userEvent.click(within(row('landing')).getByRole('button', { name: 'How work/q1-fix-the-header landed' }));
    expect(branch).toHaveBeenCalledWith('engine', 'work/q1-fix-the-header');
    unmount();

    render(<WorkflowRunView answer={answerOf(LANDED)} doors={{ branch }} />);
    expect(within(row('landing')).queryByRole('button')).toBeNull();
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

  it("opens the review where the gate holds the work on you, and says Go on anyway's twins where no control is handed", async () => {
    const review = vi.fn();
    render(<WorkflowRunView answer={answerOf(OPINION_DISPUTED)} doors={{ review, session: vi.fn() }} />);
    const opinion = row('opinion');

    expect(opinion).toHaveTextContent('waiting on you');
    expect(within(opinion).getByText('waiting on you')).toHaveClass('text-ink-open');
    expect(opinion).toHaveTextContent('1 finding is disputed: a must the working session did not fix and no recheck withdrew.');
    expect(opinion).toHaveTextContent("Go on anyway is on the session's own page, beside its second opinion, and at a terminal");
    expect(opinion).toHaveTextContent('daoris-driver opinion anyway s1 "…" goes on without it');
    await userEvent.click(within(opinion).getByRole('button', { name: 'Open its review' }));
    expect(review).toHaveBeenCalledWith('s1');
    expect(screen.getByText('Workflow: a second opinion waits for you')).toBeInTheDocument();
  });

  /** XAGENT1g (WORKFLOW1c3's remainder): the gate's own control draws *Go on anyway…* under the step, in the twin's place. */
  it("draws the second opinion's own control under its step where the frame hands it, in place of the terminal's twin", () => {
    render(
      <WorkflowRunView answer={answerOf(OPINION_DISPUTED)} controls={{ opinion: <button type="button">Go on anyway…</button> }} />,
    );
    const opinion = row('opinion');

    expect(within(opinion).getByRole('button', { name: 'Go on anyway…' })).toBeInTheDocument();
    expect(opinion).not.toHaveTextContent('daoris-driver opinion anyway');
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

  // CHATFLOW1b (the chat-flow design §10): a read that names no run comes with a code, which the page words in the reader's language;
  // a code it does not know is the driver's sentence, and no code at all the page's own fallback.
  describe('no run, by its code', () => {
    const NO_RUN: { code: string; id?: string; workflow?: string; en: string; zh: string }[] = [
      { code: 'no-session', id: 's1a2b3c4', en: 'No session s1a2b3c4 is recorded here.', zh: '这里没有记录会话 s1a2b3c4。' },
      { code: 'no-quest', id: 'q9', en: 'No quest #q9 is here.', zh: '这里没有委托 #q9。' },
      { code: 'ask-no-quest', id: 'a1', en: 'Nothing runs here yet: ask #a1 has published no quest.', zh: '这里还没有进展：需求 #a1 还没有发布委托。' },
      {
        code: 'chat-checkout',
        en: 'This chat works in your checkout, not in a tree of its own, so its work runs no workflow: what it changes is yours to commit. To give work a workflow, start a conversation with a tree of its own.',
        zh: '这个聊天在你的检出中工作，没有自己的工作树，所以它的工作不走工作流：它改动的内容由你自己提交。想让工作走工作流，请开始一个有自己工作树的对话。',
      },
      {
        code: 'chat-own-tree',
        en: "This chat works in a tree of its own, but this build reads no workflow for a conversation's work yet.",
        zh: '这个聊天在自己的工作树中工作，但这个版本还不为对话的工作读取工作流。',
      },
      {
        code: 'chat-graph-later', workflow: 'reviewed',
        en: "This chat's repository follows the workflow reviewed, which this build does not run for a conversation's work yet, so its work is landed as before.",
        zh: '这个聊天所在仓库遵循工作流 reviewed，这个版本还不为对话的工作运行它，所以它的工作仍按以前的方式落地。',
      },
      {
        code: 'chat-tree-gone',
        en: "This chat's tree is gone, and its work was never bound to a workflow, so there is no run to show.",
        zh: '这个聊天的工作树已经不在了，它的工作也从未绑定工作流，所以没有进展可显示。',
      },
      {
        code: 'ask-daoris',
        en: "Ask Daoris works in its workspace's room, not in a repository's tree, and a repository's work is asked for through a quest, so no workflow runs for its conversations.",
        zh: '问道衍在工作区的房间里工作，不在任何仓库的工作树中；仓库的工作要通过委托来请求，所以它的对话不走工作流。',
      },
      {
        code: 'reviewer',
        en: "This session reads another's work for a second opinion and changes nothing of its own, so no workflow runs for it.",
        zh: '这个会话为第二意见阅读另一个会话的工作，自己不做任何改动，所以没有工作流为它运行。',
      },
      {
        code: 'helper',
        en: 'This session helps another and changes nothing of its own, so no workflow runs for it: the session it helps has the run.',
        zh: '这个会话协助另一个会话，自己不做任何改动，所以没有工作流为它运行：进展在它协助的会话那里。',
      },
      {
        code: 'partner',
        en: "This session writes one piece that the session that asked it lands, so that session's run is the run.",
        zh: '这个会话写一个分项，由请它来的会话落地，所以那个会话的进展就是这项工作的进展。',
      },
      { code: 'teammate', en: "This session ran on a teammate's machine, which keeps its run.", zh: '这个会话在队友的机器上运行，它的进展保存在那里。' },
      {
        code: 'unanswered',
        en: 'The service did not answer, so where this work stands could not be read. Try again.',
        zh: '服务没有回应，读不到这项工作的进展。请重试。',
      },
    ];
    // The driver's English, which the page must not show where it words the code.
    const DRIVER = 'the driver said this in English';

    it.each(NO_RUN.flatMap((row) => (['en', 'zh'] as const).map((language) => [row.code, language, row] as const)))(
      'words %s in %s',
      async (_code, language, row) => {
        await i18n.changeLanguage(language);
        render(<WorkflowRunView answer={{ runs: [], problem: DRIVER, code: row.code, id: row.id ?? null, workflow: row.workflow ?? null }} />);
        expect(screen.getByText((_content, element) => element?.tagName === 'P' && element.textContent === row[language])).toBeInTheDocument();
        expect(screen.queryByText(DRIVER)).toBeNull();
        if (row.id) expect(screen.getByText(code(row.code === 'no-session' ? row.id : `#${row.id}`))).toBeInTheDocument();
        if (row.workflow) expect(screen.getByText(code(row.workflow))).toBeInTheDocument();
      },
    );

    it('says a chat with no tree of its own in both languages, and an own-tree chat as one', async () => {
      const { rerender } = render(<WorkflowRunView answer={{ runs: [], problem: DRIVER, code: 'chat-checkout' }} />);
      expect(screen.getByText(/This chat works in your checkout, not in a tree of its own/)).toBeInTheDocument();
      await i18n.changeLanguage('zh');
      rerender(<Tooltip.Provider><WorkflowRunView answer={{ runs: [], problem: DRIVER, code: 'chat-checkout' }} /></Tooltip.Provider>);
      expect(screen.getByText(/这个聊天在你的检出中工作，没有自己的工作树/)).toBeInTheDocument();
      rerender(<Tooltip.Provider><WorkflowRunView answer={{ runs: [], problem: DRIVER, code: 'chat-own-tree' }} /></Tooltip.Provider>);
      expect(screen.getByText(/这个聊天在自己的工作树中工作/)).toBeInTheDocument();
      expect(screen.queryByText(/检出/)).toBeNull();
    });

    it("shows the driver's sentence for a code it does not know, in zh too", async () => {
      await i18n.changeLanguage('zh');
      render(<WorkflowRunView answer={{ runs: [], problem: 'a code from a newer driver, said in `English`', code: 'from-a-newer-driver' }} />);
      expect(screen.getByText(code('English'))).toBeInTheDocument();
      expect(screen.getByText(/a code from a newer driver, said in/)).toBeInTheDocument();
    });

    it('offers Try again where the service did not answer, which reads again', async () => {
      const onRetry = vi.fn();
      const { rerender } = render(<WorkflowRunView answer={{ runs: [], problem: DRIVER, code: 'unanswered' }} onRetry={onRetry} />);
      await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
      expect(onRetry).toHaveBeenCalledOnce();

      await i18n.changeLanguage('zh');
      rerender(<Tooltip.Provider><WorkflowRunView answer={{ runs: [], problem: DRIVER, code: 'unanswered' }} onRetry={onRetry} /></Tooltip.Provider>);
      expect(screen.getByRole('button', { name: '重试' })).toBeInTheDocument();

      rerender(<Tooltip.Provider><WorkflowRunView answer={{ runs: [], problem: DRIVER, code: 'chat-checkout' }} onRetry={onRetry} /></Tooltip.Provider>);
      expect(screen.queryByRole('button')).toBeNull();
    });
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

// FABRIC-CUTOVER1e (D190 points 2, 3 and 7; the cutover design §2.3, §6): a run bound to a graph version is drawn from its record, its
// subjects named, its branches said to be its graph's, a look its work added beside it; its nodes are its graph's, a fork and its join among
// them. FABRIC-CUTOVER1d: its landing doors read its record, so no line says they hold it, and no node is said not begun.
describe("a graph run's Workflow view", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("says what its graph observes and that its branches are its graph's, above its nodes", () => {
    render(<WorkflowRunView answer={answerOf(GRAPH_RUN)} />);

    expect(screen.getByText('engine · docs workflow, version 1')).toBeInTheDocument();
    expect(screen.getByText('Workflow: working')).toBeInTheDocument();
    const head = screen.getByRole('list', { name: 'What its graph says' });
    expect(within(head).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'Its graph observes quest #q1, its newest work here, and daoris/q1-fix-the-header, the branch its newest session worked.',
      'Its branches are its graph\'s, not the landing rule\'s: a push, a pull request or a deploy happens for this work only where its graph holds one.',
    ]);
    expect(steps().map((item) => item.getAttribute('data-step'))).toEqual(['claimed', 'build', 'lint', 'both', 'halt', 'opinion', 'handed', 'deployed']);
    expect(row('claimed')).toHaveTextContent('Reached on quest #q1.');
    expect(row('build')).toHaveTextContent('built by acme.ci');
    expect(row('build')).toHaveTextContent('Observed on daoris/q1-fix-the-header at 01234567.');
    expect(row('both')).toHaveTextContent('The join');
    expect(row('opinion')).toHaveTextContent('not reached');
    expect(row('opinion')).not.toHaveTextContent('Not begun');
    expect(row('build')).toHaveAttribute('aria-current', 'step');
  });

  it('draws a look the work added beside its graph, and a go-ahead that waits on you with its act', () => {
    render(<WorkflowRunView answer={answerOf(GRAPH_RUN_ADDED_LOOK)} />);
    expect(row('look')).toHaveTextContent("A look added for this work by its chain's choice.");
    expect(row('release')).toHaveTextContent('Your go-ahead');
    expect(row('release')).toHaveTextContent('Waits for your go-ahead: release to prod');
    expect(screen.getByText('Workflow: waiting on you')).toBeInTheDocument();
  });

  it('says a run Daoris has not judged yet, and that no session of it has worked a branch', () => {
    render(<WorkflowRunView answer={answerOf(GRAPH_RUN_UNJUDGED)} />);
    const head = screen.getByRole('list', { name: 'What its graph says' });
    expect(within(head).getAllByRole('listitem')[0]).toHaveTextContent('Daoris has not judged its graph yet: its next look does.');
    expect(within(head).getAllByRole('listitem')[1]).toHaveTextContent('no session of it has worked a branch here yet.');
    expect(row('claimed')).toHaveTextContent('not reached');
  });

  it("opens a node's record: its quest, the session whose branch it observes, and the ask where a go-ahead waits", async () => {
    const quest = vi.fn();
    const session = vi.fn();
    const ask = vi.fn();
    const { unmount } = render(<WorkflowRunView answer={answerOf(GRAPH_RUN)} doors={{ quest, session }} />);
    await userEvent.click(within(row('claimed')).getByRole('button', { name: 'Open quest #q1' }));
    expect(quest).toHaveBeenCalledWith('q1');
    await userEvent.click(within(row('build')).getByRole('button', { name: 'Open its session' }));
    expect(session).toHaveBeenCalledWith('s1');
    unmount();

    render(<WorkflowRunView answer={answerOf(GRAPH_RUN_ADDED_LOOK)} doors={{ ask }} />);
    await userEvent.click(within(row('release')).getByRole('button', { name: 'Open the ask' }));
    expect(ask).toHaveBeenCalledWith('a1');
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<WorkflowRunView answer={answerOf(GRAPH_RUN)} />);
    const head = screen.getByRole('list', { name: '它的流程图所说的' });
    expect(within(head).getAllByRole('listitem')[1]).toHaveTextContent('它的分支归它的流程图管，而不归落地规则管');
    expect(row('build')).toHaveTextContent('在 daoris/q1-fix-the-header 的 01234567 上观察中。');
    expect(row('opinion')).toHaveTextContent('未到达');
    expect(within(head).getAllByRole('listitem')).toHaveLength(2);
    expect(row('both')).toHaveTextContent('汇合');
  });
});

// FABRIC-CUTOVER1f (D190 point 6; the cutover design §5.2): the person's skip and stop of a graph run's node, offered under each node the
// driver's one table takes them on, a skip asking for words, each press answered in the ask it was pressed in; a word kept is drawn on its
// node until the driver's next look applies it. Seen failing first.
describe("the person's skip and stop of a graph run's node", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('offers them under a node the table takes them on, and nowhere else, and none where nothing presses them', () => {
    const { unmount } = render(<WorkflowRunView answer={answerOf(GRAPH_RUN_GO_AHEAD)} onNodeWord={vi.fn()} />);
    expect(within(row('release')).getByRole('button', { name: 'Pass over…' })).toBeInTheDocument();
    expect(within(row('release')).getByRole('button', { name: 'Stop…' })).toBeInTheDocument();
    expect(within(row('claimed')).queryByRole('button', { name: 'Stop…' })).toBeNull();
    expect(within(row('handed')).queryByRole('button', { name: 'Stop…' })).toBeNull();
    unmount();

    render(<WorkflowRunView answer={answerOf(GRAPH_RUN_GO_AHEAD)} />);
    expect(screen.queryByRole('button', { name: 'Pass over…' })).toBeNull();
  });

  it('asks for the words a skip keeps before it is pressed, then sends the run, the node, the act and the words', async () => {
    const onNodeWord = vi.fn().mockResolvedValue({ run: 'q1', node: 'release', act: 'skip', done: true });
    render(<WorkflowRunView answer={answerOf(GRAPH_RUN_GO_AHEAD)} onNodeWord={onNodeWord} />);

    await userEvent.click(within(row('release')).getByRole('button', { name: 'Pass over…' }));
    const ask = screen.getByRole('group', { name: 'Pass over release for this run' });
    expect(within(ask).getByText(/what waits on it goes on without it/)).toBeInTheDocument();
    expect(within(ask).getByRole('button', { name: 'Pass over' })).toBeDisabled();
    await userEvent.type(within(ask).getByRole('textbox', { name: 'why you pass it over' }), 'nobody raises it');
    await userEvent.click(within(ask).getByRole('button', { name: 'Pass over' }));

    expect(onNodeWord).toHaveBeenCalledWith({ run: 'q1', node: 'release', act: 'skip', words: 'nobody raises it' });
    expect(screen.queryByRole('group', { name: 'Pass over release for this run' })).toBeNull();
  });

  it('sends a stop with no words, and says a refusal in the ask by its code, the driver\'s sentence for one it has no words for', async () => {
    const onNodeWord = vi.fn()
      .mockResolvedValueOnce({ run: 'q1', node: 'release', act: 'stop', done: false, refused: 'worded', message: 'workflow: already has your word.' })
      .mockResolvedValueOnce({ run: 'q1', node: 'release', act: 'stop', done: false, refused: 'something-new', message: 'workflow: a newer refusal.' });
    render(<WorkflowRunView answer={answerOf(GRAPH_RUN_GO_AHEAD)} onNodeWord={onNodeWord} />);

    await userEvent.click(within(row('release')).getByRole('button', { name: 'Stop…' }));
    const ask = screen.getByRole('group', { name: 'Stop release for this run' });
    await userEvent.click(within(ask).getByRole('button', { name: 'Stop' }));
    expect(onNodeWord).toHaveBeenCalledWith({ run: 'q1', node: 'release', act: 'stop', words: null });
    expect(await within(ask).findByRole('alert')).toHaveTextContent("release already has your word, which Daoris's next look applies.");

    await userEvent.click(within(ask).getByRole('button', { name: 'Stop' }));
    expect(await within(ask).findByRole('alert')).toHaveTextContent('workflow: a newer refusal.');
  });

  it('draws a word kept on its node, with its words, until it is applied, and offers nothing more there', () => {
    render(<WorkflowRunView answer={answerOf(GRAPH_RUN_GO_AHEAD_KEPT)} onNodeWord={vi.fn()} />);
    expect(row('release')).toHaveTextContent("Your skip is kept, and Daoris's next look applies it.");
    expect(row('release')).toHaveTextContent('“nobody raises it for this work”');
    expect(within(row('release')).queryByRole('button', { name: 'Pass over…' })).toBeNull();
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    const { unmount } = render(<WorkflowRunView answer={answerOf(GRAPH_RUN_GO_AHEAD)} onNodeWord={vi.fn()} />);
    expect(within(row('release')).getByRole('button', { name: '跳过…' })).toBeInTheDocument();
    expect(within(row('release')).getByRole('button', { name: '停止…' })).toBeInTheDocument();
    unmount();
    render(<WorkflowRunView answer={answerOf(GRAPH_RUN_GO_AHEAD_KEPT)} />);
    expect(row('release')).toHaveTextContent('你的跳过已保存，Daoris 下次查看时会应用它。');
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
