import type { ReactNode } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import i18n from '../i18n';
import type { Session } from '../api';
import { SessionPageHead, StopAsk } from './SessionPageHead';
import { ViewMain } from './ViewMain';

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
