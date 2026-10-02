import type { Meta, StoryObj } from '@storybook/react-vite';
import { outcomeOf, pauseAsk } from './pausing';
import {
  ABANDON_ANSWER, ABANDONED_ENTRY, MIXED_ASK, PAUSABLE_ASK, PAUSABLE_QUEST, SPENT_ASK,
} from './pausingFixtures';
import { AbandonAsk, AbandonedWork, PauseAsk } from './WorkAsks';

// What a pause and an abandon ask under a header, and what an abandon leaves on a page (PAUSE1e, D132 §2.6, §3.1, §4.2): the
// pause's ask on an unwired workspace and a wired one, still reading, and on its way; the abandon's list with things kept,
// with nothing left, and on its way; what went and what stayed, from the answer and from the record. Each in the main area's
// width; 中文 is the window's look.

const nothing = () => {};
const ASK = { scope: 'ask', id: 'a1b2c3' } as const;
const QUEST = { scope: 'quest', id: '9a8b7c' } as const;

const meta: Meta<typeof PauseAsk> = {
  title: 'Work/PauseAndAbandon',
  component: PauseAsk,
  decorators: [(Story) => (
    <div className="@container/main w-[46rem] max-w-full border border-line bg-page p-4">
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof PauseAsk>;

/** A pause that stops one running session, on a workspace no remote reaches: what follows, then *Pause ask*. */
export const PauseAsking: Story = {
  args: { target: ASK, lines: pauseAsk(PAUSABLE_ASK, { wired: false }), meanIt: 'Pause ask', onPause: nothing, onCancel: nothing },
};

/** The same on a wired workspace, with a running intake: another machine may still take its open quest. */
export const PauseAskingWired: Story = {
  args: {
    target: ASK,
    lines: pauseAsk({
      ...PAUSABLE_ASK,
      sessions: [...PAUSABLE_ASK.sessions, { ...PAUSABLE_ASK.sessions[0], session: 'i9n8t7k6', quest: null, intake: true, pause: 'intake', key: 'session:i9n8t7k6' }],
    }, { wired: true }),
    meanIt: 'Pause ask', onPause: nothing, onCancel: nothing,
  },
};

/** One quest's pause, from a session's header. */
export const PauseAskingQuest: Story = {
  args: { target: QUEST, lines: pauseAsk(PAUSABLE_QUEST, { wired: false }), meanIt: 'Pause quest', onPause: nothing, onCancel: nothing },
};

/** The plan is still on its way: no move is offered until it says what the pause stops. */
export const PauseReading: Story = {
  args: { target: ASK, lines: null, meanIt: 'Pause ask', onPause: nothing, onCancel: nothing },
};

/** A pause on its way: the presses wait. */
export const PauseOnItsWay: Story = {
  args: { target: ASK, lines: pauseAsk(PAUSABLE_ASK, { wired: false }), meanIt: 'Pause ask', busy: true, onPause: nothing, onCancel: nothing },
};

/** The abandon's list: what goes, each with what it does; what stays, each with why; the reason the move waits for. */
export const AbandonListing: StoryObj<typeof AbandonAsk> = {
  render: () => (
    <AbandonAsk target={ASK} plan={MIXED_ASK} meanIt="Abandon ask" placeholder="why — kept with each decline" onAbandon={nothing} onCancel={nothing} />
  ),
};

/** One quest's abandon: its decline, its session, and its tree with the files only it holds. */
export const AbandonListingQuest: StoryObj<typeof AbandonAsk> = {
  render: () => (
    <AbandonAsk target={QUEST} plan={PAUSABLE_QUEST} meanIt="Abandon quest" placeholder="why — kept with each decline" onAbandon={nothing} onCancel={nothing} />
  ),
};

/** Nothing left to take: said, with only *Close*. */
export const AbandonNothingLeft: StoryObj<typeof AbandonAsk> = {
  render: () => <AbandonAsk target={ASK} plan={SPENT_ASK} meanIt="Abandon ask" placeholder="why" onAbandon={nothing} onCancel={nothing} />,
};

/** The second press on its way. */
export const AbandonOnItsWay: StoryObj<typeof AbandonAsk> = {
  render: () => (
    <AbandonAsk target={ASK} plan={PAUSABLE_ASK} meanIt="Abandon ask" placeholder="why — kept with each decline" busy onAbandon={nothing} onCancel={nothing} />
  ),
};

/** What went and what stayed, read back from this machine's record. */
export const WhatWentFromTheRecord: StoryObj<typeof AbandonedWork> = {
  render: () => <AbandonedWork target={ASK} outcome={outcomeOf(ABANDONED_ENTRY)} went="What went" stayed="What stayed" />,
};

/** The second press's answer, at once: a piece that changed since the list, and a decline the remote lost. */
export const WhatWentFromTheAnswer: StoryObj<typeof AbandonedWork> = {
  render: () => <AbandonedWork target={ASK} outcome={outcomeOf(ABANDON_ANSWER)} went="What went" stayed="What stayed" />,
};

/** A step that could not finish: the work stays paused, and the page says so. */
export const StillPaused: StoryObj<typeof AbandonedWork> = {
  render: () => (
    <AbandonedWork
      target={ASK}
      outcome={outcomeOf({ ...ABANDON_ANSWER, failed: [{ piece: 'quest:9a8b7c', why: 'refused', detail: 'Quest `#9a8b7c` is already Done.' }], stillPaused: true })}
      went="What went" stayed="What stayed"
    />
  ),
};
