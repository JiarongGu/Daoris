import type { ReactNode } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import i18n from '../i18n';
import type { Session } from '../api';
import { DeleteAsk, SessionPageHead, StopAsk } from './SessionPageHead';
import { ViewMain } from './ViewMain';
import { PauseAsk } from './WorkAsks';

// A session's page header (SESSUX1d, D126 §3.2) pinned in Sessions' main area, in each state's acts, each stop's ask
// (§3.3), a teammate's record, below 560 px and with a 中文 title. A molecule, so each is reached by passing it; the
// conversation below is stand-in lines, scrolled under the header.

const SESSION: Session = {
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'working', kind: 'driven',
  created: '2026-10-02T09:00:00Z', updated: '2026-10-02T09:30:00Z',
};

const TITLE = 'Expose a streaming budget on the chunk API so world streaming can cap hydration work per frame';

/** A main area with something to scroll under the header, as a long run's page has. */
function Page({ children }: { children: ReactNode }) {
  return (
    <ViewMain gutters="session" header={children}>
      {Array.from({ length: 40 }, (_, line) => (
        <p key={line} className="m-0 py-1 text-body text-ink-soft">The agent’s words, line {line + 1}.</p>
      ))}
    </ViewMain>
  );
}

const meta: Meta<typeof SessionPageHead> = {
  title: 'Work/SessionPageHead',
  component: SessionPageHead,
  args: {
    session: SESSION, title: TITLE, shown: 'working',
    facts: ['engine', 'account-1', '2m'],
    acts: ['stop', 'review', 'openFolder', 'terminal', 'detach', 'copy'],
    onAct: () => {},
  },
  render: (args) => <Page><SessionPageHead {...args} /></Page>,
  // The frame's height and a main area's width, beside a stand-in list.
  decorators: [(Story) => (
    <div className="flex h-[30rem] w-[56rem] max-w-full border border-line bg-page">
      <div className="w-[17.5rem] shrink-0 border-r border-line p-3 text-meta text-ink-faint">The session list</div>
      <div className="flex min-w-0 flex-1 flex-col"><Story /></div>
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof SessionPageHead>;

/** A running driven session: *Stop…*, then its ⋯ with its review, its folder, a terminal there, its window and its id. */
export const Running: Story = {};

/** Waiting on you: *Stop…* and the ⋯; the answer is the card's under the header, never a second owner here. */
export const WaitingOnYou: Story = {
  args: { session: { ...SESSION, state: 'awaiting-person' }, shown: 'awaiting-person' },
};

/** A parked quest's last session: *Try again*, the loud act, then its ⋯. */
export const Parked: Story = {
  args: {
    session: { ...SESSION, state: 'failed' }, shown: 'parked',
    acts: ['retry', 'review', 'openFolder', 'terminal', 'detach', 'copy'], primary: 'retry',
  },
};

/** Stopped, its stop holding its quest (SESSUX1b): *Try again* releases it. */
export const StoppedHoldingItsQuest: Story = {
  args: {
    session: { ...SESSION, state: 'stopped' }, shown: 'stopped',
    acts: ['retry', 'review', 'openFolder', 'terminal', 'detach', 'archive', 'copy'], primary: 'retry',
  },
};

/** To review: *Review* leads, since nothing but the person will verify the work (D37). */
export const ToReview: Story = {
  args: {
    session: { ...SESSION, state: 'completed' }, shown: 'completed',
    acts: ['review', 'openFolder', 'terminal', 'detach', 'copy'], primary: 'review',
  },
};

/** A teammate's record (SYNC4): read here, offered only what reaches no process. */
export const ATeammatesRecord: Story = {
  args: { session: { ...SESSION, id: 'person@machine-b/s1a2b3c4', state: 'completed' }, shown: 'completed', acts: ['archive', 'copy'] },
};

/** A driven session holding its quest, its stop asking once under the header. */
export const StopAskingHeld: Story = {
  args: {
    asking: <StopAsk sentence={i18n.t('work.stop.drivenHeld')} onStop={() => {}} onCancel={() => {}} />,
  },
};

/** A driven session before it took its quest: another machine may still take it. */
export const StopAskingBeforeTheTake: Story = {
  args: {
    session: { ...SESSION, state: 'queued' }, shown: 'queued',
    asking: <StopAsk sentence={i18n.t('work.stop.drivenOpen', { quest: 'abc123' })} onStop={() => {}} onCancel={() => {}} />,
  },
};

/** Waiting on you with no process left: it stops unanswered. */
export const StopAskingParked: Story = {
  args: {
    session: { ...SESSION, state: 'awaiting-person' }, shown: 'awaiting-person',
    asking: <StopAsk sentence={i18n.t('work.stop.parked')} onStop={() => {}} onCancel={() => {}} />,
  },
};

/** A chat: it ends the conversation, and Finish is named as the gentler ending. */
export const StopAskingAChat: Story = {
  args: {
    session: { ...SESSION, quest: null, kind: 'chat' }, title: 'Cap the hydration per frame', shown: 'idle',
    asking: <StopAsk sentence={i18n.t('work.stop.chat')} onStop={() => {}} onCancel={() => {}} />,
  },
};

/** An intake: the ask stays a proposal, as its card said before its stop moved here. */
export const StopAskingAnIntake: Story = {
  args: {
    session: { ...SESSION, quest: null, kind: 'chat', ask: '0fda18', repository: 'ask #0fda18' }, title: 'Intake for ask #0fda18',
    acts: ['stop', 'detach', 'copy'],
    asking: <StopAsk sentence={i18n.t('work.intake.stopMeans')} onStop={() => {}} onCancel={() => {}} />,
  },
};

/** An ended conversation that served no quest (SESSUX1f): *Delete…* in its ⋯, after the archive marks. */
export const ADeletableConversation: Story = {
  args: {
    session: { ...SESSION, quest: null, kind: 'chat', state: 'completed' }, title: 'Cap the hydration per frame', shown: 'completed',
    acts: ['openFolder', 'terminal', 'detach', 'archive', 'delete', 'copy'],
  },
};

/** Its delete asking once under the header (§5.4): what goes, and that nothing brings it back. */
export const DeleteAsking: Story = {
  args: {
    ...ADeletableConversation.args,
    asking: <DeleteAsk onDelete={() => {}} onCancel={() => {}} />,
  },
};

/** Below 560 px: the acts take their own line under the title. */
export const Narrow: Story = {
  args: { acts: ['stop', 'retry', 'review', 'openFolder', 'detach', 'copy'], primary: 'retry' },
  decorators: [(Story) => (
    <div className="flex h-[30rem] w-[30rem] max-w-full border border-line bg-page"><Story /></div>
  )],
};

/** A 中文 title, one line, whole on its tip. */
export const ChineseTitle: Story = {
  args: { title: '为区块接口提供流式预算，让世界流式加载能按帧限制水合工作量，避免卡顿和掉帧' },
};

// ——— Titles first (UX7c, D152 §7; the UX7 design §5.2): its word, then its title on one line, then its facts by state.
// The install's fourth attempt at a re-filed quest, with and without a short title.

const REFILED_TITLE = '(Re-filed from ask #0181ea, whose quest was taken outside the driver with no session.)';

/** Its quest's short title (SESSUX1j): the head says the work, and the fourth attempt is a fact on its line. */
export const FourthAttemptWithAShortTitle: Story = {
  args: { title: 'Continue the production half', facts: ['engine', 'account-1', '2m', 'attempt 4: the 3 before it failed'] },
};

/** No short title: the title as written, one line, whole in its tip; the word stays where it was. */
export const FourthAttemptWithNoShortTitle: Story = {
  args: { title: REFILED_TITLE, facts: ['engine', 'account-1', '2m', 'attempt 4: the 3 before it failed'] },
};

/** Parked after its failed sessions: its line says so, in the row's words, beside *Try again*. */
export const ParkedFacts: Story = {
  args: {
    session: { ...SESSION, state: 'failed' }, shown: 'parked', title: 'Continue the production half',
    facts: ['engine', 'after 3 failed sessions', 'ran 14m'],
    acts: ['retry', 'review', 'openFolder', 'terminal', 'detach', 'copy'], primary: 'retry',
  },
};

/** The same at 680 px of window (a main area of about 596 px): the acts beside the title still, the facts cut on their line. */
export const FourthAttemptNarrow: Story = {
  args: { ...FourthAttemptWithNoShortTitle.args },
  decorators: [(Story) => (
    <div className="flex h-[30rem] w-[37.25rem] max-w-full border border-line bg-page"><Story /></div>
  )],
};

/** A running driven session whose quest an ask asked (PAUSE1e): *Pause quest…* and *Pause ask…* in its ⋯. */
export const RunningWithItsPauses: Story = {
  args: { acts: ['stop', 'pauseQuest', 'pauseAsk', 'review', 'openFolder', 'terminal', 'detach', 'copy'] },
};

/** Its quest's pause asking once under the header, from the driver's plan of what it stops (D132 §2.6). */
export const PauseAsking: Story = {
  args: {
    acts: ['stop', 'pauseQuest', 'pauseAsk', 'review', 'openFolder', 'terminal', 'detach', 'copy'],
    asking: (
      <PauseAsk
        className="mt-2.5" target={{ scope: 'quest', id: 'abc123' }} meanIt="Pause quest"
        lines={[{ key: 'work.pause.ask.stops', values: { count: 1 } }]} onPause={() => {}} onCancel={() => {}}
      />
    ),
  },
};

/** Stopped by its ask's pause: *Resume ask*, the loud act, in *Try again*'s place (D132 §6.1). */
export const PausedWithItsAsk: Story = {
  args: {
    session: { ...SESSION, state: 'stopped' }, shown: 'stopped',
    acts: ['resumeAsk', 'review', 'openFolder', 'terminal', 'detach', 'copy'], primary: 'resumeAsk',
  },
};
