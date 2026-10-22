import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { ReactNode } from 'react';
import type { GoAhead, Session } from '../../data/api';
import { chinese } from '../../design/storyLanguage';
import { InTheme } from '../../plugins/storyIcons';
import { DETAIL, GATES } from '../gates/opinionFixtures';
import { STEP_SHOWN } from '../gates/reviewFixtures';
import { WaitStrip } from './WaitStrip';

// What waits on the person, at the foot (WAITFOOT1b, D168): a park's question with its go-ahead still open, the work to
// accept with its ask open, a dispute and a review's look beside it, several folded, an intake's ask, and a discard the line
// holds. Drawn above a stand-in for the session's box, at the main area's width, in both languages and both themes.

const nothing = () => {};

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: '7a82cc', repository: 'engine', adapter: 'claude-code', state: 'completed', kind: 'driven',
  created: '2026-10-15T09:46:00Z', updated: '2026-10-15T11:56:00Z', ...over,
});

const PARKED = session({ state: 'awaiting-person', note: 'I need go-ahead 2 before I write to production.' });
const LANDS = { branch: 'daoris/s-1a2b3c4', tree: 's-1a2b3c4', commits: 2, uncommitted: 0 };
const PLAN = { session: 's1a2b3c4', form: 'branch', target: 'feature/0fda18-dashboard' };

const GO_AHEADS: GoAhead[] = [
  {
    number: 1, kind: 'release', on: 'production', act: 'comparison report', state: 'approved',
    asked: [{ session: 's1a2b3c4', quest: '7a82cc', at: '2026-10-15T11:00:00Z', why: 'Ship it.' }],
    answer: { approved: true, at: '2026-10-15T11:20:00Z' },
  },
  {
    number: 2, kind: 'write', on: 'production', act: 'dashboard configuration', state: 'asked',
    asked: [{ session: 's1a2b3c4', quest: '7a82cc', at: '2026-10-15T11:10:00Z', why: 'The tile reads its target from it.' }],
  },
];

/** The column's foot: the strip above a stand-in for the box, as the frame lays them out. */
function Foot({ children }: { children: ReactNode }) {
  return (
    <div className="@container/main flex w-[760px] max-w-full flex-col bg-page">
      {children}
      <div className="border-t border-line px-4 py-3 text-small text-ink-faint">(the session's box)</div>
    </div>
  );
}

const foot: Decorator = (Story) => <Foot><Story /></Foot>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof WaitStrip> = {
  title: 'Work/Wait strip',
  component: WaitStrip,
  args: { onTop: nothing },
  decorators: [foot],
};
export default meta;

type Story = StoryObj<typeof WaitStrip>;

/**
 * Parked asking where no box below takes its answer: its question is read at the top, and its go-ahead still open is answered here.
 * Where the box below answers the park, the box draws both with their presses (PARKVIEW1) and the strip draws neither.
 */
export const ParkedWithAGoAhead: Story = {
  args: { session: PARKED, onResolve: nothing, goAheads: GO_AHEADS, onGoAhead: nothing },
};

/** The same in 中文. */
export const ParkedWithAGoAheadChinese: Story = { ...ParkedWithAGoAhead, decorators: [chinese] };

/** The same in dark. */
export const ParkedWithAGoAheadDark: Story = { ...ParkedWithAGoAhead, decorators: [dark] };

/** PROPOSEHERE1b: a rule the session proposed after auto mode refused it, answered where the person reads (D74). */
const RULE = {
  id: 'r1', state: 'waiting' as const, action: 'add' as const, scope: 'repository' as const, name: 'engine', list: 'allow' as const,
  rule: 'Bash(git branch -D:*)', why: 'Auto mode refused the local branch delete you asked for; this lets me run it.',
  session: 's1a2b3c4', proposed: '2026-10-15T11:40:00Z',
};

/** A rule it proposed, *Allow…* asking once and *Decline* direct, with its call found in the conversation. */
export const ARuleItProposed: Story = {
  args: { session: session(), rules: { proposals: [RULE], onAnswer: nothing, heard: true }, toEvent: { rules: { r1: nothing } } },
};

/** The same in 中文, on a session that takes no words now: the card says it is not told. */
export const ARuleItProposedUnheardChinese: Story = {
  args: { ...ARuleItProposed.args, rules: { proposals: [RULE], onAnswer: nothing, heard: false } },
  decorators: [chinese],
};

/** The same in dark and 中文. */
export const ParkedWithAGoAheadDarkChinese: Story = { ...ParkedWithAGoAhead, decorators: [chinese, dark] };

/**
 * Each line leads to the event in the conversation that raised it, where the conversation found one (WAITFOOT1c): the question
 * and go-ahead 2's call here; the work to accept at the end of its turn.
 */
export const InTheConversation: Story = {
  args: {
    ...ParkedWithAGoAhead.args, lands: LANDS, landing: PLAN, onLand: nothing, onReview: nothing,
    toEvent: { question: nothing, work: nothing, goAheads: { 2: nothing } },
  },
};

/** The same in 中文. */
export const InTheConversationChinese: Story = { ...InTheConversation, decorators: [chinese] };

/** The same in dark and 中文. */
export const InTheConversationDarkChinese: Story = { ...InTheConversation, decorators: [chinese, dark] };

/** A raising block's *To the wait* led here: go-ahead 2's line, outlined. */
export const LedToFromTheConversation: Story = {
  args: { ...InTheConversation.args, seek: { wait: 'goAhead:2', n: 1 } },
};

/** Several, folded to one line naming them. */
export const Folded: Story = {
  ...ParkedWithAGoAhead,
  play: async ({ canvasElement }) => {
    [...canvasElement.querySelectorAll('button')].find((button) => button.textContent?.trim() === 'Fold')?.click();
  },
};

/** Its work to accept: *Accept…* beside its line. */
export const WorkToAccept: Story = { args: { session: session(), lands: LANDS, landing: PLAN, onLand: nothing, onReview: nothing } };

/** The same in 中文 and dark. */
export const WorkToAcceptDarkChinese: Story = { ...WorkToAccept, decorators: [chinese, dark] };

/**
 * WAITFOOT2a: the head's waits are on screen, so the strip stands aside and only the box is drawn here, each wait in the head
 * alone.
 */
export const AsideWhileTheHeadIsOnScreen: Story = { args: { ...ParkedWithAGoAhead.args, aside: true } };

/** WAITFOOT2a: on screen or not, the strip stays up while *Accept…* is asking in it. */
export const AsideButAsking: Story = {
  args: { ...WorkToAccept.args, workAsk: { open: true, onOpen: nothing }, aside: true },
};

/** *Accept…* asking at the foot, the frame holding its ask here. */
export const AcceptAsking: Story = {
  args: { ...WorkToAccept.args, workAsk: { open: true, onOpen: nothing } },
};

/** A dispute *Accept…* answers, with its findings folded. */
export const Disputed: Story = {
  args: {
    session: session(), lands: LANDS, landing: { ...PLAN, opinion: GATES.disputed }, onLand: nothing, onReview: nothing,
    opinion: { detail: DETAIL, acts: { sendBack: nothing, ask: nothing } },
  },
};

/** The review's look waits: its presses at the foot, no *Accept…*. */
export const ReviewWaits: Story = {
  args: {
    session: session(), lands: LANDS, landing: { ...PLAN, review: { state: 'shown', environment: 'local', quest: STEP_SHOWN.id } },
    onLand: nothing, review: { step: STEP_SHOWN, served: true, acts: { reviewed: nothing, notYet: nothing, skip: nothing } },
  },
};

/** The same in dark. */
export const ReviewWaitsDark: Story = { ...ReviewWaits, decorators: [dark] };

/** An intake parked asking: the answer is on its ask. */
export const Intake: Story = {
  args: {
    session: session({ state: 'awaiting-person', kind: 'chat', quest: null, ask: '0fda18', repository: 'ask #0fda18' }),
    onAnswerAsk: nothing,
  },
};

/** The same in 中文. */
export const IntakeChinese: Story = { ...Intake, decorators: [chinese] };

/** The line holds its work by content: *Discard branch…*. */
export const HeldDiscard: Story = {
  args: {
    session: session(),
    discards: {
      branch: 'daoris/s-1a2b3c4', tree: 's-1a2b3c4', says: 'Its work is on `main` by content (a squash merge).',
      keeps: 'refs/daoris/discarded/daoris/s-1a2b3c4',
      keptAt: 'Its commits stay at `refs/daoris/discarded/daoris/s-1a2b3c4` until you delete that ref.',
    },
    onDiscardTree: nothing,
  },
};

/** Without the box below: its question is read and answered at the top. */
export const QuestionAtTheTop: Story = { args: { session: PARKED, onResolve: nothing } };
