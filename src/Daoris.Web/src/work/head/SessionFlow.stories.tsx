import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Session } from '../../data/api';
import { atWidths, inDark, inLight } from '../../design/flowFrames';
import { chinese } from '../../design/storyLanguage';
import { SessionLifeFlow } from './SessionFlow';

// A session's states drawn as a flowchart (FLOWCHART1b; the flowchart design §2.2, §3.5, §4.4): the four live states, the
// working ⇄ awaiting-person return on a lane of its own, one node of outcomes, and going on as a guarded edge, at the frame's
// widths, 300 px the side bar's floor (the rail), 400 and 544 top to bottom, 1200 left to right. The record's state is lit in
// the tone of the word the page shows it as. The first decorator is the innermost, so a theme's page wraps the frames.

const meta: Meta<typeof SessionLifeFlow> = {
  title: 'Work/SessionFlow',
  component: SessionLifeFlow,
};
export default meta;

type Story = StoryObj<typeof SessionLifeFlow>;

/** At every width, in light: a story's own decorators, so a story in another look draws no frame inside another. */
const light = [atWidths(), inLight];

const session = (state: Session['state'], over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: '7a82cc', repository: 'engine', adapter: 'claude-code', state, kind: 'driven',
  created: '2026-10-20T09:00:00Z', updated: '2026-10-20T11:00:00Z', ...over,
});

/** Waiting for the repository's slot: queued lit, the outcomes not reached. */
export const Queued: Story = { args: { session: session('queued') }, decorators: light };
/** Working: its return from waiting on you drawn on a lane of its own. */
export const Working: Story = { args: { session: session('working') }, decorators: light };
/** The same in dark. */
export const WorkingDark: Story = { args: { session: session('working') }, decorators: [atWidths(), inDark] };
/** The same in 中文. */
export const WorkingChinese: Story = { args: { session: session('working') }, decorators: [chinese, atWidths(), inLight] };
/** The same in 中文, in dark. */
export const WorkingChineseDark: Story = { args: { session: session('working') }, decorators: [chinese, atWidths(), inDark] };
/** A chat between turns: the record says working, the page says idle. */
export const Idle: Story = { args: { session: session('working', { kind: 'chat' }), shown: 'idle' }, decorators: light };
/** Parked at a checkpoint only the person can clear: waiting on you, in open's hue. */
export const WaitingOnYou: Story = { args: { session: session('awaiting-person') }, decorators: light };
/** The same in 中文. */
export const WaitingOnYouChinese: Story = { args: { session: session('awaiting-person') }, decorators: [chinese, atWidths(), inLight] };
/** A park the person answered: still the record's awaiting-person, the page says answered. */
export const Answered: Story = { args: { session: session('awaiting-person', { answer: 'Go ahead.' }), shown: 'answered' }, decorators: light };
/** Completed: the outcomes lit with *completed*, the one that may go on, a stand-down said never to. */
export const Completed: Story = { args: { session: session('completed') }, decorators: light };
/** Failed on a quest the reader parked: the record failed, the page says parked, in open's hue. */
export const FailedParked: Story = { args: { session: session('failed'), shown: 'parked' }, decorators: light };
/** Stood down: going on drawn refused, with its reason. */
export const StoodDown: Story = { args: { session: session('stood-down') }, decorators: light };
/** An intake: answered through its ask, so going on is drawn refused. */
export const Intake: Story = { args: { session: session('completed', { ask: 'a1b2c3', quest: null }) }, decorators: light };
/** A state a newer host adds: lit as recorded, with no move drawn to it. */
export const UnknownState: Story = { args: { session: session('paused' as Session['state']) }, decorators: light };
