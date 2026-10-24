import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { Button } from '../design/ui';
import {
  answerOf, FAILED, GO_AHEAD_REFUSAL, GRAPH_RUN, GRAPH_RUN_ADDED_LOOK, GRAPH_RUN_GO_AHEAD, GRAPH_RUN_GO_AHEAD_KEPT, GRAPH_RUN_UNJUDGED,
  HELD_FOR_OPINION, IN_REVIEW, LANDED, MERGED, OPINION_DISPUTED, OPINION_SETTLED, PULL_REQUEST_OPEN, SEVERAL, WAITING_ON_YOU, WORKING,
} from './runFixtures';
import type { NodeWordAsk } from './run';
import { OpinionGate } from '../work/gates/OpinionGate';
import { DETAIL, GATES } from '../work/gates/opinionFixtures';
import { WorkflowLine } from './WorkflowLine';
import { WorkflowRunView } from './WorkflowRunView';

// The session's Workflow view (WORKFLOW1c; D157 point 12, the workflow design §5.2, §7), a run at each stage a person meets it,
// drawn from props: working, waiting on you, in review, held for an opinion, landed, its pull request open and merged, failed;
// an ask's two runs; the answer on its way, refused and none; 中文 and dark; and the side bar at its 300 px floor.

const nothing = () => {};
const doors = { session: nothing, quest: nothing, ask: nothing, review: nothing };

/**
 * The side bar at a width: 360 px as it opens beside a session, 300 px, its floor, where the flowchart is its rail (FLOWCHART1d),
 * and 696 px, 45% of the install's 1546 px window (the UI/UX design R6).
 */
const sideAt = (width: number): Decorator => (Story) => (
  <div className="border border-line bg-page p-3" style={{ width }}><Story /></div>
);
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof WorkflowRunView> = {
  title: 'Workflow/WorkflowRunView',
  component: WorkflowRunView,
  args: { answer: answerOf(WORKING), here: 'work', doors },
  decorators: [sideAt(360)],
};
export default meta;

type Story = StoryObj<typeof WorkflowRunView>;

/** **Working**: the agent at work on its quest, *this session* marked on the work; the landing not reached, drawn faint. */
export const Working: Story = {};

/** The run still follows the saved version it started with. */
export const BoundVersion: Story = { args: { here: 'write', answer: answerOf({ ...IN_REVIEW, at: 'inspect',
  workflow: { ...IN_REVIEW.workflow, id: 'release', boundVersion: 1 },
  steps: IN_REVIEW.steps.map((step) => ({ ...step, step: { ...step.step,
    id: step.step.kind === 'work' ? 'write' : step.step.kind === 'look' ? 'inspect' : step.step.id,
    source: { rule: 'workflow' as const, level: 'task' as const },
  } })),
}) } };

/** FABRIC-PIPELINE1a: the run heads with the route that chose its workflow, the workspace's for Documentation. */
export const ChosenByItsRoute: Story = { args: { answer: answerOf({ ...IN_REVIEW,
  workflow: { ...IN_REVIEW.workflow, id: 'docs-to-pr', boundVersion: 2, route: { level: 'workspace-kind', kind: 'docs', label: 'Documentation' } } }) } };
export const ChosenByItsRouteChinese: Story = { ...ChosenByItsRoute, decorators: [chinese] };

/** An unread binding has no replacement graph and no completed mark. */
export const BindingUnread: Story = { args: { answer: answerOf({ ...IN_REVIEW, steps: [], at: null,
  problem: 'Its stored run binding could not be read. Restore the binding to continue.' }) } };
export const BindingUnreadChineseDark: Story = { ...BindingUnread, decorators: [chinese, dark] };

/** **Waiting on you**: a go-ahead its session asked, in open's hue, with the door to the ask where it is answered. */
export const WaitingOnYou: Story = { args: { answer: answerOf(WAITING_ON_YOU) } };

/**
 * **In review**: the work done and its set-up shown in `dev`, waiting for the person's look; the review's own gate drawn under
 * the step (here a stand-in for it), and the set-up step's page its door.
 */
export const InReview: Story = {
  args: {
    answer: answerOf(IN_REVIEW),
    controls: { look: <div className="flex gap-2"><Button>Reviewed</Button><Button variant="ghost">Not yet…</Button></div> },
  },
};

/**
 * **Held for an opinion**: XAGENT1f's gate at `with-session`, Codex's three findings with the working session, which answers them;
 * the run stands at the opinion, and the landing is not reached until the gate settles.
 */
export const HeldForAnOpinion: Story = { args: { answer: answerOf(HELD_FOR_OPINION) } };

/**
 * **A dispute waits on you**, in another session's run: in open's hue, its door the review, where the gate's presses stand beside
 * *Accept…*, and *Go on anyway…*'s twins said, the session's own page and the terminal's.
 */
export const OpinionDisputed: Story = { args: { answer: answerOf(OPINION_DISPUTED), doors } };

/**
 * **A dispute waits on you**, in the attended session's own run: the second opinion's own gate drawn under its step, *Go on
 * anyway…* among its presses (XAGENT1g, WORKFLOW1c3's remainder).
 */
export const OpinionDisputedWithItsGate: Story = {
  args: {
    answer: answerOf(OPINION_DISPUTED),
    doors,
    controls: {
      opinion: (
        <OpinionGate
          gate={{ ...GATES.disputed, answers: null }}
          detail={DETAIL}
          working="s1"
          acts={{ ask: nothing, anyway: nothing, sendBack: nothing, myself: nothing }}
        />
      ),
    },
  },
};

/** The same in 中文. */
export const OpinionDisputedWithItsGateChinese: Story = { ...OpinionDisputedWithItsGate, decorators: [chinese] };

/** **A settled opinion**: done, the run standing at the landing, which waits for *Accept…*. */
export const OpinionSettled: Story = { args: { answer: answerOf(OPINION_SETTLED) } };

/** **Landed**: into its line by the person's accept; the run finished, *done* at the end of the line. */
export const Landed: Story = { args: { answer: answerOf(LANDED) } };

/** **Pull request open**: landed on a branch with no press, its plugin's pull request waiting for the person's merge, its link the door. */
export const PullRequestOpen: Story = { args: { answer: answerOf(PULL_REQUEST_OPEN) } };

/** **Merged**: the pull request completed on the platform; the run finished. */
export const Merged: Story = { args: { answer: answerOf(MERGED) } };

/** **Failed**: its last session failed, red there only, with the door to the session. */
export const Failed: Story = { args: { answer: answerOf(FAILED) } };

/** **An ask's work in two repositories**: two runs, each at its own step, each named by its repository (design §3.8). */
export const SeveralRuns: Story = { args: { answer: SEVERAL, here: null } };

/** The answer on its way: its words, and static rows in its place. */
export const Reading: Story = { args: { answer: undefined, reading: true } };

/** The read refused: the driver's sentence, whole. */
export const Refused: Story = { args: { answer: undefined, refusal: 'the driver is still coming up — its service is not answering yet. A moment.' } };

/** No run from a driver before its codes: the driver's sentence, whole. */
export const NoRun: Story = { args: { answer: { runs: [], problem: 'session `c1` serves no quest, so no workflow runs for it.' }, here: null } };

// CHATFLOW1b (the chat-flow design §10): no run, by its code, in the reader's language; the driver's English beside it is never shown.
const noRun = (code: string, values: { id?: string; workflow?: string } = {}): Story => ({
  args: { answer: { runs: [], problem: 'the driver’s English, not shown', code, id: values.id ?? null, workflow: values.workflow ?? null }, here: null, onRetry: nothing },
});

/** No session by the id asked. */
export const NoRunNoSession: Story = noRun('no-session', { id: 's1a2b3c4' });

/** No quest by the id asked. */
export const NoRunNoQuest: Story = noRun('no-quest', { id: 'q9' });

/** An ask, or its intake, before it published a quest. */
export const NoRunAskNoQuest: Story = noRun('ask-no-quest', { id: 'a1' });

/** A chat in the person's checkout: its work is theirs to commit, and how to have a run. */
export const NoRunChatCheckout: Story = noRun('chat-checkout');

/** A chat in a tree of its own, before a conversation's work has a run. */
export const NoRunChatOwnTree: Story = noRun('chat-own-tree');

/** A chat whose repository follows a graph version, before CHATFLOW1f. */
export const NoRunChatGraphLater: Story = noRun('chat-graph-later', { workflow: 'reviewed' });

/** A chat whose tree is gone, never bound. */
export const NoRunChatTreeGone: Story = noRun('chat-tree-gone');

/** Ask Daoris's conversation. */
export const NoRunAskDaoris: Story = noRun('ask-daoris');

/** A reviewer's session. */
export const NoRunReviewer: Story = noRun('reviewer');

/** A helper's session. */
export const NoRunHelper: Story = noRun('helper');

/** A partner's session. */
export const NoRunPartner: Story = noRun('partner');

/** A teammate's session. */
export const NoRunTeammate: Story = noRun('teammate');

/** The service did not answer: *Try again* reads again. */
export const NoRunUnanswered: Story = noRun('unanswered');

/** A chat in the checkout, in 中文. */
export const NoRunChatCheckoutChinese: Story = { ...NoRunChatCheckout, decorators: [chinese] };

/** The service did not answer, in 中文: 重试. */
export const NoRunUnansweredChinese: Story = { ...NoRunUnanswered, decorators: [chinese] };

/** In 中文: 等你处理, 智能体与你, 本会话, the state words their glossary words. */
export const InReviewChinese: Story = { ...InReview, decorators: [chinese] };

/** In dark: open's hue, done's green and the faint steps not reached. */
export const PullRequestOpenDark: Story = { ...PullRequestOpen, decorators: [dark] };

/** At the side bar's 300 px floor: each title, its chip and who acts wrap; nothing shrinks. */
export const AtTheFloor: Story = { ...InReview, decorators: [sideAt(300)] };

/** At the floor, in 中文 and dark. */
export const AtTheFloorChineseDark: Story = { ...HeldForAnOpinion, decorators: [sideAt(300), chinese, dark] };

/** At 45% of the install's window: the run drawn as a flowchart top to bottom, the look current, the rest not reached and dashed. */
export const AtHalfTheWindow: Story = { ...InReview, decorators: [sideAt(696)] };

/** At 45% of the window, in 中文 and dark: held for an opinion. */
export const AtHalfTheWindowChineseDark: Story = { ...HeldForAnOpinion, decorators: [sideAt(696), chinese, dark] };

/** At 45% of the window, landed: the end of the line reached. */
export const LandedAtHalfTheWindow: Story = { ...Landed, decorators: [sideAt(696)] };

/**
 * **Passed over, and a go-ahead's refusal** (FLOWCHART1d): a named version's run, its second opinion passed over where nothing asked
 * for one and its look skipped by the person, each said as its record names who; the go-ahead's refusal waits for you, on a dashed
 * failure path once the outcome fits beside the end.
 */
export const PassedOver: Story = { args: { answer: answerOf(GO_AHEAD_REFUSAL), here: null } };

/** Passed over, at 45% of the window, where the refusal's outcome fits beside the end. */
export const PassedOverAtHalfTheWindow: Story = { ...PassedOver, decorators: [sideAt(696)] };

/** Passed over, at the floor in 中文: the rail, the refusal said under its step. */
export const PassedOverAtTheFloorChinese: Story = { ...PassedOver, decorators: [sideAt(300), chinese] };

/**
 * **A run bound to a graph version** (FABRIC-CUTOVER1e), drawn from its record: what its graph observes, that its branches are its
 * graph's and that every landing door still holds it, above its nodes; the claim reached on its quest, a fork of a build observed on the
 * run's branch and a lint reached there, joined before the second opinion, which this build does not begin on a graph run.
 */
export const GraphRun: Story = { args: { answer: answerOf(GRAPH_RUN), here: null } };

/** A graph run at 45% of the window: its fork and its join drawn as its graph's wires hold them, a failure's path to its stop. */
export const GraphRunAtHalfTheWindow: Story = { ...GraphRun, decorators: [sideAt(696)] };

/** A graph run at 45% of the window, in 中文 and dark. */
export const GraphRunAtHalfTheWindowChineseDark: Story = { ...GraphRun, decorators: [sideAt(696), chinese, dark] };

/** A graph run at the side bar's floor, in 中文: the rail, each node in its graph's order. */
export const GraphRunAtTheFloorChinese: Story = { ...GraphRun, decorators: [sideAt(300), chinese] };

/** A graph run whose go-ahead waits on you, with a look the chain's choice added beside its graph (D190 point 7). */
export const GraphRunWithAnAddedLook: Story = { args: { answer: answerOf(GRAPH_RUN_ADDED_LOOK), here: null } };

/** The same at 45% of the window, in 中文: the added look an entry of its own, joined to nothing in the graph. */
export const GraphRunWithAnAddedLookAtHalfTheWindowChinese: Story = { ...GraphRunWithAnAddedLook, decorators: [sideAt(696), chinese] };

/** A graph run Daoris has not judged yet: every node not reached, standing at its entry, no session of it with a branch yet. */
export const GraphRunNotJudgedYet: Story = { args: { answer: answerOf(GRAPH_RUN_UNJUDGED), here: null } };

/** What a press of the person's word answers in a story: kept, so the ask closes. */
const kept = async (ask: NodeWordAsk) => ({ run: ask.run, node: ask.node, act: ask.act, done: true });

/**
 * **A go-ahead nobody raised** (FABRIC-CUTOVER1f; the cutover design §5.2): its node offers the person's *Pass over…* and *Stop…*, the one
 * table's acts where it stands; the landing behind it, not reached, offers neither. Each asks once in the page, a skip with its words.
 */
export const GraphRunPassOverAGoAhead: Story = { args: { answer: answerOf(GRAPH_RUN_GO_AHEAD), here: null, onNodeWord: kept } };

/** The same at the side bar's floor, in 中文 and dark: the rail, the presses under the go-ahead's lines. */
export const GraphRunPassOverAGoAheadAtTheFloorChineseDark: Story = { ...GraphRunPassOverAGoAhead, decorators: [sideAt(300), chinese, dark] };

/** The person's skip kept on the go-ahead and not applied yet: said with their words in place of the presses. */
export const GraphRunWordKept: Story = { args: { answer: answerOf(GRAPH_RUN_GO_AHEAD_KEPT), here: null, onNodeWord: kept } };

/** **The one line** a quest's page carries among its head's facts, and an ask's for each of its runs, each a door into the run. */
export const OneLine: StoryObj<typeof WorkflowLine> = {
  render: () => (
    <div className="grid gap-2 text-small text-ink-faint">
      <WorkflowLine runs={[IN_REVIEW]} onOpen={nothing} />
      <WorkflowLine runs={[WORKING]} onOpen={nothing} />
      <WorkflowLine runs={SEVERAL.runs} onOpen={nothing} />
    </div>
  ),
};
