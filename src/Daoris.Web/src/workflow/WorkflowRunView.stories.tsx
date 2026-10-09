import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import { Button } from '../ui';
import {
  answerOf, FAILED, HELD_FOR_OPINION, IN_REVIEW, LANDED, MERGED, OPINION_DISPUTED, OPINION_SETTLED, PULL_REQUEST_OPEN, SEVERAL,
  WAITING_ON_YOU, WORKING,
} from './runFixtures';
import { WorkflowLine } from './WorkflowLine';
import { WorkflowRunView } from './WorkflowRunView';

// The session's Workflow view (WORKFLOW1c; D157 point 12, the workflow design §5.2, §7), a run at each stage a person meets it,
// drawn from props: working, waiting on you, in review, held for an opinion, landed, its pull request open and merged, failed;
// an ask's two runs; the answer on its way, refused and none; 中文 and dark; and the side bar at its 300 px floor.

const nothing = () => {};
const doors = { session: nothing, quest: nothing, ask: nothing, review: nothing };

/** The side bar at a width: 360 px as it opens beside a session, and 300 px, its floor. */
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
 * **A dispute waits on you**: in open's hue, its door the review, where the gate's presses stand beside *Accept…*, and *Go on
 * anyway…*'s terminal twin said until the review draws it (XAGENT1g).
 */
export const OpinionDisputed: Story = { args: { answer: answerOf(OPINION_DISPUTED), doors } };

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

/** No run: a conversation serves no quest, said in the driver's sentence. */
export const NoRun: Story = { args: { answer: { runs: [], problem: 'session `c1` serves no quest, so no workflow runs for it.' }, here: null } };

/** In 中文: 等你处理, 智能体与你, 本会话, the state words their glossary words. */
export const InReviewChinese: Story = { ...InReview, decorators: [chinese] };

/** In dark: open's hue, done's green and the faint steps not reached. */
export const PullRequestOpenDark: Story = { ...PullRequestOpen, decorators: [dark] };

/** At the side bar's 300 px floor: each title, its chip and who acts wrap; nothing shrinks. */
export const AtTheFloor: Story = { ...InReview, decorators: [sideAt(300)] };

/** At the floor, in 中文 and dark. */
export const AtTheFloorChineseDark: Story = { ...HeldForAnOpinion, decorators: [sideAt(300), chinese, dark] };

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
